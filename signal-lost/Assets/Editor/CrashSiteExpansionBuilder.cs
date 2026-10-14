using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SignalLost.Editor
{
    public static partial class CrashSiteUpgradeBuilder
    {
        public const float ExpandedSize = 320f;
        public const int CliffSegments = 128;

        [MenuItem("Signal Lost/Tutorial Crash Site/Double Map And Add Cliffs")]
        public static void ExpandMapAndAddCliffs()
        {
            var scene = OpenSavedScene();
            GameObject environment = GameObject.Find(TutorialCrashSiteBuilder.EnvironmentName);
            if (environment == null || environment.transform.Find("Enhanced Crashed Rocket") == null)
                throw new InvalidOperationException("Create the ship and laser upgrades first.");
            if (environment.transform.Find("Perimeter Cliffs") != null)
                throw new InvalidOperationException("Expanded map already exists; preserve authored edits.");
            Terrain terrain = environment.GetComponentInChildren<Terrain>();
            TerrainData data = terrain.terrainData;
            if (Mathf.Abs(data.size.x-160)>0.01f && Mathf.Abs(data.size.x-ExpandedSize)>0.01f)
                throw new InvalidOperationException("Unexpected initial terrain dimensions.");
            Backup("TutorialBeforeMapExpansion.unity");
            string assetPath=AssetDatabase.GetAssetPath(data);
            if (!File.Exists("Logs/ImpactCraterBeforeExpansion.asset")) File.Copy(assetPath,"Logs/ImpactCraterBeforeExpansion.asset");
            float[,] originalHeights=data.GetHeights(0,0,data.heightmapResolution,data.heightmapResolution);
            Vector3 shipScale=environment.transform.Find("Enhanced Crashed Rocket/Crashed Ship").lossyScale;
            // Only terrain dimensions and dressing positions change. Never scale the whole environment root.
            data.size=new Vector3(ExpandedSize,data.size.y,ExpandedSize);
            terrain.transform.position=environment.transform.position+new Vector3(-ExpandedSize/2,-2,-ExpandedSize/2);
            terrain.basemapDistance=400;
            terrain.Flush(); EditorUtility.SetDirty(data);
            foreach(string groupName in new[]{"Basalt Rocks","Ship Debris"})
            {
                Transform group=environment.transform.Find(groupName);
                if(group==null) throw new InvalidOperationException("Missing original dressing group "+groupName);
                foreach(Transform item in group)
                {
                    Vector3 p=item.localPosition; p.x*=2; p.z*=2; item.localPosition=p;
                }
            }
            GroundSceneProps(terrain);
            BuildCliffs(environment.transform,terrain);
            // Update the original environment prefab's existing GUID; exclude ship and author-placed props.
            var reusable=new GameObject("CraterEnvironment");
            try
            {
                foreach(string groupName in new[]{"Rocky Crater Terrain","Basalt Rocks","Ship Debris","Perimeter Cliffs"})
                {
                    Transform source=environment.transform.Find(groupName);
                    var copy=UnityEngine.Object.Instantiate(source.gameObject,reusable.transform);
                    copy.name=source.name; copy.transform.localPosition=source.localPosition;
                    copy.transform.localRotation=source.localRotation; copy.transform.localScale=source.localScale;
                }
                PrefabUtility.SaveAsPrefabAsset(reusable,TutorialCrashSiteBuilder.AssetRoot+"/CraterEnvironment.prefab");
            }
            finally{UnityEngine.Object.DestroyImmediate(reusable);}
            RenderSettings.fogStartDistance=140; RenderSettings.fogEndDistance=320;
            Physics.SyncTransforms();
            float[,] finalHeights=data.GetHeights(0,0,data.heightmapResolution,data.heightmapResolution);
            for(int z=0;z<data.heightmapResolution;z++) for(int x=0;x<data.heightmapResolution;x++)
                if(originalHeights[z,x]!=finalHeights[z,x]) throw new InvalidOperationException("Terrain elevation samples changed.");
            if(Vector3.Distance(shipScale,environment.transform.Find("Enhanced Crashed Rocket/Crashed Ship").lossyScale)>0.001f)
                throw new InvalidOperationException("Map expansion changed ship size.");
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save expanded map.");
            Debug.Log("MAP_DOUBLED_WITH_HEIGHT_SAMPLES_AND_MODEL_SCALES_PRESERVED");
        }

        private static void GroundSceneProps(Terrain terrain)
        {
            foreach(string name in new[]{"FirstPersonPlayer","CapybaraAstronaut","BurntRockBasin","AlcoholFlask","GrayboxExitGate"})
            {
                GameObject obj=GameObject.Find(name); if(obj==null) throw new InvalidOperationException("Missing preserved object "+name);
                CharacterController cc=obj.GetComponent<CharacterController>(); if(cc!=null)cc.enabled=false;
                Vector3 p=obj.transform.position; p.y=Ground(terrain,p)+(name=="FirstPersonPlayer"?0.10f:0.01f);
                obj.transform.position=p; if(cc!=null)cc.enabled=true;
                PrefabUtility.RecordPrefabInstancePropertyModifications(obj.transform);
            }
        }

        /// <summary>Art-pass operation: replaces the generated cliff mesh in place, preserving its GUID.</summary>
        public static void RefineCliffArt()
        {
            var scene=OpenSavedScene();
            Transform environment=GameObject.Find(TutorialCrashSiteBuilder.EnvironmentName).transform;
            if(environment.Find("Perimeter Cliffs")==null)throw new InvalidOperationException("Expand the map first.");
            CliffMesh(environment.GetComponentInChildren<Terrain>(),environment.position);
            CliffMaterials();AssetDatabase.SaveAssets();
            Debug.Log("FRACTURED_CLIFF_ART_UPDATED");
            CrashSiteUpgradeValidation.Validate();
        }

        private static Material[] CliffMaterials()
        {
            return new[]{CliffMaterial("CliffBasalt",new Color(0.90f,0.91f,0.93f)),
                CliffMaterial("CliffAsh",new Color(0.87f,0.83f,0.76f)),CliffMaterial("CliffShadow",new Color(0.56f,0.61f,0.65f))};
        }

        private static void BuildCliffs(Transform parent,Terrain terrain)
        {
            Mesh mesh=CliffMesh(terrain,parent.position);
            Material[] materials=CliffMaterials();
            SavePrototype("NaturalCliffPerimeter",root=>
            {
                GameObject faces=MeshPart("Continuous fractured cliff faces",root.transform,mesh,materials[0]);
                faces.GetComponent<Renderer>().sharedMaterials=materials;
            });
            GameObject cliff=Instantiate("NaturalCliffPerimeter",parent);cliff.name="Perimeter Cliffs";
            cliff.transform.localPosition=Vector3.zero; PrefabUtility.RecordPrefabInstancePropertyModifications(cliff.transform);
        }

        private static Material CliffMaterial(string name,Color color)
        {
            Material mat=Mat(name,color,0.02f,0.08f);
            mat.SetColor("_BaseColor",color);
            mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(TutorialCrashSiteBuilder.AssetRoot+"/Textures/FracturedBasalt.png"));
            mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(TutorialCrashSiteBuilder.AssetRoot+"/Textures/FracturedBasaltNormal.png"));
            mat.SetFloat("_BumpScale",0.55f);mat.EnableKeyword("_NORMALMAP");EditorUtility.SetDirty(mat);return mat;
        }

        private static Mesh CliffMesh(Terrain terrain,Vector3 origin)
        {
            string path=Root+"/Meshes/NaturalCliffPerimeter.asset";
            Mesh existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            const int rings=9;
            var points=new Vector3[rings,CliffSegments];
            var rng=new System.Random(5913);
            for(int i=0;i<CliffSegments;i++)
            {
                float a=i*Mathf.PI*2/CliffSegments;
                float irregular=Mathf.Sin(a*7)*2.3f+Mathf.Sin(a*13+0.3f)*1.15f;
                float tooth=(float)rng.NextDouble()*3.8f-1.9f;
                float ridge=(float)rng.NextDouble()*2.8f-1.4f;
                float height=30+Mathf.Sin(a*5+0.7f)*4+Mathf.Sin(a*11)*2.5f+tooth;
                // Fractured, overhanging faces. The first vertical tier is far higher than the player's jump.
                float[] radius={134+irregular,143+irregular,140+irregular+ridge,143+irregular-ridge,
                    139+irregular+ridge,141+irregular+tooth,150+irregular*0.55f,155+irregular*0.45f,158+irregular*0.35f};
                float[] elevation={0.10f,0.38f,6.5f+ridge,12.5f-ridge,19+ridge,height,height+3.5f+ridge,height-1.5f,-3};
                for(int ring=0;ring<rings;ring++)
                {
                    Vector3 p=new Vector3(Mathf.Cos(a)*radius[ring],0,Mathf.Sin(a)*radius[ring]);
                    float ground=Ground(terrain,origin+p)-origin.y;
                    p.y=ground+elevation[ring];points[ring,i]=p;
                }
            }
            var vertices=new List<Vector3>();var uv=new List<Vector2>();
            var indices=new[]{new List<int>(),new List<int>(),new List<int>()};
            for(int ring=0;ring<rings;ring++) for(int i=0;i<CliffSegments;i++)
            {
                int next=(i+1)%CliffSegments,other=(ring+1)%rings;
                int material=ring==0?1:ring>=7?2:(i+ring*7)%11==0?2:(i+ring*3)%7==0?1:0;
                Triangle(vertices,uv,indices[material],points[ring,i],points[ring,next],points[other,next]);
                int second=(ring>0 && ring<7 && (i+ring)%13==0)?1:material;
                Triangle(vertices,uv,indices[second],points[ring,i],points[other,next],points[other,i]);
            }
            for(int i=0;i<vertices.Count;i++)
            {
                Vector3 v=vertices[i];float a=Mathf.Atan2(v.z,v.x);if(a<0)a+=Mathf.PI*2;
                uv[i]=new Vector2(a*145/8,v.y/7);
            }
            var mesh=new Mesh{name="Natural continuous basalt cliffs",subMeshCount=3};
            mesh.SetVertices(vertices);mesh.SetUVs(0,uv);
            for(int i=0;i<3;i++)mesh.SetTriangles(indices[i],i);
            mesh.RecalculateNormals();mesh.RecalculateBounds();
            if(existing!=null)
            {
                EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(existing);return existing;
            }
            AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
    }
}
