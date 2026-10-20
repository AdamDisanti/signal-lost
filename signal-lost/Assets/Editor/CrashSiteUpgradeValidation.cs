using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SignalLost.Editor
{
    public static class CrashSiteUpgradeValidation
    {
        [MenuItem("Signal Lost/Tutorial Crash Site/Validate Upgraded Scene")]
        public static void Validate()
        {
            Scene scene=SceneManager.GetActiveScene();
            if(scene.path!=TutorialCrashSiteBuilder.ScenePath) throw new InvalidOperationException("Open Tutorial first.");
            TutorialCrashSiteBuilder.Validate();
            Transform environment=Find(scene,TutorialCrashSiteBuilder.EnvironmentName);
            Terrain terrain=environment.GetComponentInChildren<Terrain>();
            Vector3 size=terrain.terrainData.size;
            bool traversal=environment.Find(TutorialTraversalBuilder.LandscapeName)!=null || environment.Find("Lunar Landscape")!=null;
            float expectedSize=traversal?480:320, perimeterScale=traversal?1.5f:1;
            if(Mathf.Abs(size.x-expectedSize)>0.01f || Mathf.Abs(size.z-expectedSize)>0.01f) throw new InvalidOperationException("Unexpected tutorial dimensions: "+size);
            if(environment.lossyScale!=Vector3.one) throw new InvalidOperationException("Environment was scaled instead of terrain.");
            Transform ship=Find(scene,"Crashed Ship");
            Transform assembly=Find(scene,"Enhanced Crashed Rocket");
            if(ship.parent!=assembly || ship.Find("RocketWindow")==null || ship.Find("Base Engines").childCount!=3)
                throw new InvalidOperationException("Ship window, engines or assembly missing.");
            Transform contact=assembly.Find("Ground Contact Debris");
            if(contact==null || contact.childCount<16) throw new InvalidOperationException("Insufficient close-in wreck dressing.");
            Transform basin=Find(scene,"BurntRockBasin"), laser=basin.Find("MiningLaserAnchor/Mining Laser");
            if(laser==null || laser.GetComponent<BoxCollider>()==null) throw new InvalidOperationException("Basin laser missing.");
            Bounds gun=BoundsIn(laser,basin);
            if(gun.min.y<0.64f || gun.min.y>0.68f || Mathf.Max(Mathf.Abs(gun.min.x),Mathf.Abs(gun.max.x))>0.55f ||
                Mathf.Max(Mathf.Abs(gun.min.z),Mathf.Abs(gun.max.z))>0.47f)
                throw new InvalidOperationException("Laser does not fit basin floor: "+gun);
            foreach(Renderer r in laser.GetComponentsInChildren<Renderer>()) foreach(Material m in r.sharedMaterials)
                if(m==null || m.shader.name!="Universal Render Pipeline/Lit") throw new InvalidOperationException("Laser material import failed.");
            Transform cliff=Find(scene,"Perimeter Cliffs");
            MeshCollider wall=cliff.GetComponentInChildren<MeshCollider>();
            if(wall==null || !wall.enabled || wall.convex || wall.GetComponent<Rigidbody>()!=null)
                throw new InvalidOperationException("Continuous static cliff collision missing.");
            Physics.SyncTransforms();
            for(int i=0;i<256;i++)
            {
                float a=i*Mathf.PI*2/256;Vector3 dir=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                Vector3 p=environment.position+dir*(110*perimeterScale);p.y=terrain.SampleHeight(p)+terrain.transform.position.y+0.9f;
                if(!wall.Raycast(new Ray(p,dir),out RaycastHit hit,60*perimeterScale) || hit.distance>45*perimeterScale)
                    throw new InvalidOperationException("Perimeter collision gap at angle "+a*Mathf.Rad2Deg);
            }
            var report=new List<string>{expectedSize+" x "+expectedSize+"m native terrain, environment root unscaled.",
                "Three base engines, blue side porthole and "+contact.childCount+" close-in contact rocks/hull chips.",
                "Laser fitted at basin floor: "+gun+"; URP materials and collider present.",
                "Continuous non-convex static cliff collision passed 256 radial gap checks."};
            foreach(string name in new[]{"MiningLaser","RocketWindow","RocketEngine","ContactRock","ContactHullShard","EnhancedCrashedRocket","NaturalCliffPerimeter"})
            {
                string path=CrashSiteUpgradeBuilder.Root+"/"+name+".prefab";
                GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if(prefab==null || GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefab)>0)
                    throw new InvalidOperationException("New prefab missing or invalid: "+path);
            }
            // Optional snapshot assets exist only in the disposable validation project.
            if(!traversal && File.Exists("Assets/TestBaseline.unity")) CompareSnapshot(scene,report);
            Directory.CreateDirectory("Logs");File.WriteAllLines("Logs/CrashSiteUpgradeValidation.txt",report);
            Debug.Log("CRASH_SITE_UPGRADE_VALIDATED\n"+string.Join("\n",report));
        }

        private static void CompareSnapshot(Scene current,List<string> report)
        {
            Scene baseline=EditorSceneManager.OpenScene("Assets/TestBaseline.unity",OpenSceneMode.Additive);
            try
            {
                foreach(string name in new[]{"FirstPersonPlayer","CapybaraAstronaut","BurntRockBasin","AlcoholFlask","GrayboxExitGate"})
                {
                    Transform before=Find(baseline,name),after=Find(current,name);
                    Vector3 delta=after.position-before.position;delta.y=0;
                    if(delta.magnitude>0.0001f || Quaternion.Angle(before.rotation,after.rotation)>0.001f || Vector3.Distance(before.lossyScale,after.lossyScale)>0.0001f)
                        throw new InvalidOperationException("Preserved prop moved in XZ, rotated or scaled: "+name);
                }
                Transform oldShip=Find(baseline,"Crashed Ship"),newShip=Find(current,"Crashed Ship");
                if(Vector3.Distance(oldShip.lossyScale,newShip.lossyScale)>0.0001f || Quaternion.Angle(oldShip.rotation,newShip.rotation)>0.001f)
                    throw new InvalidOperationException("Original rocket size or orientation changed.");
                foreach(Renderer before in oldShip.Find("rocket").GetComponentsInChildren<Renderer>())
                {
                    Transform matching=newShip.Find("rocket").Find(before.transform.name);
                    if(matching==null || Vector3.Distance(before.bounds.size,matching.GetComponent<Renderer>().bounds.size)>0.001f)
                        throw new InvalidOperationException("Original ship mesh dimensions changed: "+before.name);
                }
                foreach(string groupName in new[]{"Basalt Rocks","Ship Debris"})
                {
                    Transform before=Find(baseline,groupName),after=Find(current,groupName);
                    if(before.childCount!=after.childCount) throw new InvalidOperationException("Original dressing count changed.");
                    foreach(Transform item in before)
                    {
                        Transform match=after.Find(item.name);
                        if(match==null || Vector3.Distance(item.lossyScale,match.lossyScale)>0.0001f || Quaternion.Angle(item.rotation,match.rotation)>0.001f)
                            throw new InvalidOperationException("Existing dressing resized: "+item.name);
                        Vector3 expected=item.localPosition;expected.x*=2;expected.z*=2;
                        if(Vector3.Distance(match.localPosition,expected)>0.0001f)throw new InvalidOperationException("Dressing distribution not doubled.");
                    }
                }
                TerrainData original=AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/TestBaselineTerrain.asset");
                TerrainData expanded=Find(current,TutorialCrashSiteBuilder.EnvironmentName).GetComponentInChildren<Terrain>().terrainData;
                int resolution=original.heightmapResolution;
                float[,] a=original.GetHeights(0,0,resolution,resolution),b=expanded.GetHeights(0,0,resolution,resolution);
                for(int z=0;z<resolution;z++)for(int x=0;x<resolution;x++)
                    if(a[z,x]!=b[z,x])throw new InvalidOperationException("Approved height sample changed.");
                report.Add("Snapshot comparison: all approved height samples identical; all 96 rocks/30 debris pieces unchanged in size/rotation, distribution doubled.");
                report.Add("Snapshot comparison: original ship render bounds/scale/orientation and all five tutorial objects' XZ/rotation/scale unchanged.");
            }
            finally{EditorSceneManager.CloseScene(baseline,true);SceneManager.SetActiveScene(current);}
        }

        private static Transform Find(Scene scene,string name)
        {
            foreach(GameObject root in scene.GetRootGameObjects())foreach(Transform t in root.GetComponentsInChildren<Transform>(true))
                if(t.name==name)return t;
            throw new InvalidOperationException("Missing scene object "+name);
        }

        private static Bounds BoundsIn(Transform root,Transform reference)
        {
            bool first=true;Bounds result=default;
            foreach(MeshFilter f in root.GetComponentsInChildren<MeshFilter>())
            {
                Bounds b=f.sharedMesh.bounds;
                for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
                {
                    Vector3 p=reference.InverseTransformPoint(f.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z))));
                    if(first){result=new Bounds(p,Vector3.zero);first=false;}else result.Encapsulate(p);
                }
            }
            return result;
        }
    }
}
