using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SignalLost.Editor
{
    // One-shot authored layout; the saved terrain and prefabs, not this recipe, are the editable deliverable.
    public static class TutorialTraversalBuilder
    {
        public const string Root = TutorialCrashSiteBuilder.AssetRoot + "/TraversalLandscape";
        public const string LandscapeName = "Traversal Landscape";
        public static readonly Vector3[][] AccessRoutes = {
            new[] { new Vector3(-28,2,30), new Vector3(-62,8,40), new Vector3(-120,20,35), new Vector3(-150,31,65), new Vector3(-128,52,105), new Vector3(-105,52,105) },
            new[] { new Vector3(-62,8,40), new Vector3(-88,13,58), new Vector3(-105,16,70) },
            new[] { new Vector3(-32,2,-25), new Vector3(-75,0,-55), new Vector3(-100,-7,-93), new Vector3(-100,-7,-117), new Vector3(-130,-4,-135) },
            new[] { new Vector3(32,3,-20), new Vector3(48,7,-52), new Vector3(58,16,-40), new Vector3(68,16,-40) },
            new[] { new Vector3(112,16,-40), new Vector3(126,23,-65), new Vector3(126,27,-83), new Vector3(92,22,-94), new Vector3(92,22,-106), new Vector3(100,22,-106) },
            new[] { new Vector3(112,16,-40), new Vector3(135,25,-15), new Vector3(165,34,30), new Vector3(150,40,85), new Vector3(128,44,122), new Vector3(100,44,122), new Vector3(100,44,110), new Vector3(105,44,110) },
            new[] { new Vector3(90,-9,-40), new Vector3(70,-8,-88), new Vector3(34,-5,-125), new Vector3(-5,-2,-145), new Vector3(-40,0,-110), new Vector3(-32,2,-25) },
            new[] { new Vector3(109,-9,-106), new Vector3(109,-7,-118), new Vector3(136,-7,-118), new Vector3(118,-5,-135), new Vector3(78,-4,-145), new Vector3(34,-5,-125) }
        };
        private static Terrain terrain;
        private static Vector3 center;
        private static Transform landscape;
        private static Material stone, steel, rust, trim, gold, glow;
        private static readonly Vector2[] Sites = { new Vector2(-105,105), new Vector2(-95,60), new Vector2(-100,-105), new Vector2(90,-40), new Vector2(125,-106), new Vector2(130,110) };

        [MenuItem("Signal Lost/Tutorial Crash Site/Build Traversal Landscape Once")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(TutorialCrashSiteBuilder.ScenePath);
            var environment = GameObject.Find(TutorialCrashSiteBuilder.EnvironmentName);
            if (environment == null || environment.transform.Find(LandscapeName) != null || AssetDatabase.IsValidFolder(Root))
                throw new InvalidOperationException("Expected the main-based tutorial without traversal assets; preserve existing edits.");
            terrain = environment.GetComponentInChildren<Terrain>(); center = environment.transform.position;
            if (terrain.terrainData.size.x != 320) throw new InvalidOperationException("Expected the current 320m tutorial.");
            Directory.CreateDirectory("Logs/BeforeTraversalPass");
            File.Copy(TutorialCrashSiteBuilder.ScenePath, "Logs/BeforeTraversalPass/Tutorial.unity", true);
            File.Copy(AssetDatabase.GetAssetPath(terrain.terrainData), "Logs/BeforeTraversalPass/ImpactCrater.asset", true);
            foreach (string path in new[] { Root, Root + "/Meshes", Root + "/Materials", Root + "/Terrain" }) Folder(path);
            var oldDressing = new List<Transform>(); var offsets = new List<float>();
            foreach (string name in new[] { "Basalt Rocks", "Ship Debris" }) foreach (Transform item in environment.transform.Find(name)) { oldDressing.Add(item); offsets.Add(item.position.y - Ground(item.position)); }
            Sculpt(); Paint();
            for (int i = 0; i < oldDressing.Count; i++)
            {
                Transform item=oldDressing[i]; Vector3 p=item.position; Vector2 local=XZ(p-center); float radius=0;
                foreach(Renderer renderer in item.GetComponentsInChildren<Renderer>())radius=Mathf.Max(radius,renderer.bounds.extents.x,renderer.bounds.extents.z);
                if(local.magnitude>45)for(int pass=0;pass<8;pass++)foreach(Vector3[] route in AccessRoutes)for(int j=1;j<route.Length;j++)
                {
                    Vector2 a=XZ(route[j-1]),b=XZ(route[j]);float d=Segment(local,a,b,out float t);
                    if(d>=radius+4)continue;
                    Vector2 closest=Vector2.Lerp(a,b,t),away=d>0.01f?(local-closest).normalized:Vector2.Perpendicular(b-a).normalized;
                    local=closest+away*(radius+4.5f);
                }
                p.x=center.x+local.x;p.z=center.z+local.y;p.y=Ground(p)+offsets[i];item.position=p;PrefabUtility.RecordPrefabInstancePropertyModifications(item);
            }
            foreach (string name in new[] { "FirstPersonPlayer", "CapybaraAstronaut", "BurntRockBasin", "AlcoholFlask", "GrayboxExitGate" })
            {
                Transform item = GameObject.Find(name).transform; CharacterController cc = item.GetComponent<CharacterController>(); if (cc != null) cc.enabled = false;
                Vector3 p = item.position; p.y = Ground(p) + (cc != null ? 0.10f : 0.01f); item.position = p; if (cc != null) cc.enabled = true; PrefabUtility.RecordPrefabInstancePropertyModifications(item);
            }
            Transform cliffs = environment.transform.Find("Perimeter Cliffs"); cliffs.localScale = new Vector3(1.5f, 2.6f, 1.5f); cliffs.localPosition = new Vector3(0,-14,0); PrefabUtility.RecordPrefabInstancePropertyModifications(cliffs);
            landscape = new GameObject(LandscapeName).transform; landscape.SetParent(environment.transform, false);
            stone = Mat("LayeredBasalt", new Color(0.82f,0.86f,0.9f)); stone.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(TutorialCrashSiteBuilder.AssetRoot+"/Textures/FracturedBasalt.png")); steel = Mat("BlueGraphite", new Color(0.16f,0.23f,0.27f));
            rust = Mat("OxideWreckage", new Color(0.57f,0.27f,0.15f)); trim = Mat("WornAlloy", new Color(0.59f,0.64f,0.65f));
            gold = Mat("CrossingOchre", new Color(0.9f,0.61f,0.16f)); glow = Mat("RelayCyan", new Color(0.2f,0.75f,0.8f)); glow.EnableKeyword("_EMISSION"); glow.SetColor("_EmissionColor", new Color(0.1f,0.45f,0.5f));
            Causeway(); Arches(); Observatory(); Tunnel(); SteppingFins(); FallenMast(); Scatter();
            // Save a linked additive prefab so the shared environment and scene do not duplicate all the new geometry.
            PrefabUtility.SaveAsPrefabAssetAndConnect(landscape.gameObject, Root + "/TraversalLandscape.prefab", InteractionMode.AutomatedAction);
            string environmentPath = TutorialCrashSiteBuilder.AssetRoot + "/CraterEnvironment.prefab";
            GameObject reusable = PrefabUtility.LoadPrefabContents(environmentPath);
            try
            {
                Transform oldTerrain = reusable.transform.Find("Rocky Crater Terrain"); oldTerrain.localPosition = terrain.transform.localPosition;
                oldTerrain.GetComponent<Terrain>().basemapDistance=terrain.basemapDistance;oldTerrain.GetComponent<Terrain>().heightmapPixelError=terrain.heightmapPixelError;
                Transform oldCliffs = reusable.transform.Find("Perimeter Cliffs"); oldCliffs.localScale = cliffs.localScale; oldCliffs.localPosition = cliffs.localPosition;
                foreach (string group in new[] { "Basalt Rocks", "Ship Debris" })
                {
                    Transform source = environment.transform.Find(group), target = reusable.transform.Find(group);
                    for (int i=0;i<source.childCount;i++) target.GetChild(i).localPosition = source.GetChild(i).localPosition;
                }
                var linked = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/TraversalLandscape.prefab"), reusable.transform); linked.name = LandscapeName;
                PrefabUtility.SaveAsPrefabAsset(reusable, environmentPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(reusable); }
            RenderSettings.fogStartDistance = 160; RenderSettings.fogEndDistance = 480;
            RenderSettings.ambientSkyColor = new Color(0.37f,0.43f,0.51f); RenderSettings.ambientEquatorColor = new Color(0.25f,0.29f,0.34f);
            foreach (Light light in UnityEngine.Object.FindObjectsByType<Light>()) if (light.enabled && light.type == LightType.Directional) { light.intensity = 1.5f; light.color = new Color(1,0.9f,0.78f); }
            terrain.Flush(); EditorUtility.SetDirty(terrain.terrainData); AssetDatabase.SaveAssets(); Physics.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(scene); if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save traversal tutorial.");
            TutorialGameplayBuilder.Validate(); Debug.Log("TUTORIAL_TRAVERSAL_CREATED");
        }

        private static float Ground(Vector3 p) => terrain.SampleHeight(p) + terrain.transform.position.y;
        private static float Blend(float a,float b,float x) => Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,x));
        private static float Segment(Vector2 p,Vector2 a,Vector2 b,out float t) { Vector2 d=b-a; t=Mathf.Clamp01(Vector2.Dot(p-a,d)/Mathf.Max(d.sqrMagnitude,0.001f)); return Vector2.Distance(p,a+d*t); }
        private static Vector2 XZ(Vector3 p) => new Vector2(p.x,p.z);
        private static float Plateau(Vector2 p,Vector2 at,float inner,float outer,float height) => height*(1-Blend(inner,outer,Vector2.Distance(p,at)));
        private static void Sculpt()
        {
            TerrainData data=terrain.terrainData; Vector3 oldSize=data.size, oldPosition=terrain.transform.position; int oldN=data.heightmapResolution;
            float[,] original=data.GetHeights(0,0,oldN,oldN);
            Func<Vector2,float> oldHeight=p=> {
                Vector3 world=center+new Vector3(p.x,0,p.y); float u=Mathf.Clamp01((world.x-oldPosition.x)/oldSize.x)*(oldN-1), v=Mathf.Clamp01((world.z-oldPosition.z)/oldSize.z)*(oldN-1);
                int x=Mathf.Min((int)u,oldN-2), z=Mathf.Min((int)v,oldN-2);
                return Mathf.Lerp(Mathf.Lerp(original[z,x],original[z,x+1],u-x),Mathf.Lerp(original[z+1,x],original[z+1,x+1],u-x),v-z)*oldSize.y+oldPosition.y;
            };
            const int n=513; var h=new float[n,n];
            Transform gate=GameObject.Find("GrayboxExitGate").transform;
            Vector2 gateStart=XZ(gate.position-center), gateEnd=gateStart+XZ(gate.forward)*55;
            for(int z=0;z<n;z++) for(int x=0;x<n;x++)
            {
                Vector2 p=new Vector2(x*480f/(n-1)-240,z*480f/(n-1)-240); float r=p.magnitude;
                float rough=(Mathf.PerlinNoise(p.x*0.028f+12,p.y*0.028f+4)-0.45f)*7;
                float height=3+rough;
                height=Mathf.Max(height,Plateau(p,new Vector2(-105,105),24,65,52));
                height=Mathf.Max(height,Plateau(p,new Vector2(125,110),20,72,44));
                height=Mathf.Max(height,Plateau(p,new Vector2(140,-65),18,52,34));
                height=Mathf.Max(height,3+Mathf.Pow(Mathf.Clamp01(1-Mathf.Abs(p.x+115+Mathf.Sin(p.y*0.03f)*12)/27),2)*29*Blend(45,80,r));
                Vector2[] canyon={new Vector2(-25,-180),new Vector2(20,-125),new Vector2(70,-90),new Vector2(90,-35),new Vector2(95,35),new Vector2(135,100)};
                float distance=999; for(int i=1;i<canyon.Length;i++) distance=Mathf.Min(distance,Segment(p,canyon[i-1],canyon[i],out _));
                distance=Mathf.Min(distance,Segment(p,new Vector2(103,-106),new Vector2(166,-106),out _));
                height=Mathf.Lerp(-9+(Mathf.PerlinNoise(p.x*0.1f,p.y*0.1f)-0.5f)*0.6f,height,Blend(7,16,distance));
                float routeDistance=float.MaxValue,routeHeight=0,routeWeight=0;
                foreach(Vector3[] route in AccessRoutes) for(int i=1;i<route.Length;i++)
                {
                    float d=Segment(p,XZ(route[i-1]),XZ(route[i]),out float t);routeDistance=Mathf.Min(routeDistance,d);
                    float weight=(1-Blend(2.8f,6.5f,d))/(0.25f+d*d);routeHeight+=Mathf.Lerp(route[i-1].y,route[i].y,t)*weight;routeWeight+=weight;
                }
                // Blend intersecting grades instead of creating a sharp seam between nearby paths.
                if(routeWeight>0)height=Mathf.Lerp(routeHeight/routeWeight,height,Blend(2.8f,6.5f,routeDistance));
                foreach(Vector3 pad in new[]{new Vector3(68,16,-40),new Vector3(112,16,-40),new Vector3(100,22,-106),new Vector3(-105,52,105),new Vector3(112,44,110),new Vector3(-100,-7,-105)})
                {
                    float d=Vector2.Distance(p,XZ(pad)); float width=pad.x==-100?16:pad.x==-105?9:5;
                    height=Mathf.Lerp(pad.y,height,Blend(width,width+4,d));
                }
                height=Mathf.Lerp(height,5+rough*0.25f,Blend(185,198,r));
                float preserve=1-Blend(42,58,r); float corridor=1-Blend(4,8,Segment(p,gateStart,gateEnd,out _)); preserve=Mathf.Max(preserve,corridor);
                height=Mathf.Lerp(height,oldHeight(p),preserve);
                h[z,x]=(height+16)/96;
            }
            data.heightmapResolution=n; data.size=new Vector3(480,96,480); terrain.transform.localPosition=new Vector3(-240,-16,-240);
            data.SetHeights(0,0,h); terrain.basemapDistance=600; terrain.heightmapPixelError=1; PrefabUtility.RecordPrefabInstancePropertyModifications(terrain.transform);
        }
        private static TerrainLayer Layer(string name,Color color)
        {
            var image=new Texture2D(128,128,TextureFormat.RGB24,false);
            for(int y=0;y<128;y++) for(int x=0;x<128;x++) { float shade=0.75f+Mathf.PerlinNoise(x*0.1f,y*0.1f)*0.5f; image.SetPixel(x,y,color*shade); }
            image.Apply(); string path=Root+"/Terrain/"+name+".png"; File.WriteAllBytes(path,image.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(image); AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var layer=new TerrainLayer {name=name,diffuseTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(path),tileSize=Vector2.one*11,smoothness=0.1f}; AssetDatabase.CreateAsset(layer,Root+"/Terrain/"+name+".terrainlayer"); return layer;
        }
        private static void Paint()
        {
            TerrainData data=terrain.terrainData; var layers=new List<TerrainLayer>(data.terrainLayers); layers.Add(Layer("OxideSediment",new Color(0.34f,0.24f,0.19f))); layers.Add(Layer("PaleShale",new Color(0.44f,0.45f,0.48f))); data.terrainLayers=layers.ToArray();
            int n=data.alphamapResolution; var a=new float[n,n,5];
            for(int z=0;z<n;z++)for(int x=0;x<n;x++)
            {
                float u=x/(float)(n-1),v=z/(float)(n-1),height=data.GetInterpolatedHeight(u,v)-16,slope=data.GetSteepness(u,v); Vector2 p=new Vector2(u*480-240,v*480-240);
                float soot=(1-Blend(15,43,p.magnitude))*0.8f, ash=Blend(20,50,height)*0.3f;
                float oxide=(1-Blend(-4,4,height))*0.6f, shale=Blend(20,50,slope)*(0.75f+0.15f*Mathf.Sin(height*0.8f));
                a[z,x,1]=soot; a[z,x,2]=ash; a[z,x,3]=oxide; a[z,x,4]=shale;
                float sum=soot+ash+oxide+shale; if(sum>0.9f)for(int i=1;i<5;i++)a[z,x,i]*=0.9f/sum;
                a[z,x,0]=1-a[z,x,1]-a[z,x,2]-a[z,x,3]-a[z,x,4];
            }
            data.SetAlphamaps(0,0,a);
        }
        private static Material Mat(string name,Color color) { var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,enableInstancing=true}; m.SetColor("_BaseColor",color); m.SetFloat("_Smoothness",0.18f); AssetDatabase.CreateAsset(m,Root+"/Materials/"+name+".mat");return m; }
        private static void Folder(string path) { if(AssetDatabase.IsValidFolder(path))return; int slash=path.LastIndexOf('/');Folder(path.Substring(0,slash));AssetDatabase.CreateFolder(path.Substring(0,slash),path.Substring(slash+1)); }
        private static GameObject Box(string name,Transform parent,Vector3 at,Vector3 size,Material material,Quaternion rotation=default)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=name;obj.transform.SetParent(parent,false);obj.transform.localPosition=at;obj.transform.localScale=size;obj.transform.localRotation=rotation.Equals(default(Quaternion))?Quaternion.identity:rotation;obj.GetComponent<Renderer>().sharedMaterial=material;return obj;
        }
        private static void Beam(string name,Transform parent,Vector3 from,Vector3 to,float width,float thickness,Material material) => Box(name,parent,(from+to)*0.5f,new Vector3(Vector3.Distance(from,to),thickness,width),material,Quaternion.FromToRotation(Vector3.right,to-from));
        private static void Marker(Transform root,string name,Vector3 at) { var marker=new GameObject(name);marker.transform.SetParent(root,false);marker.transform.localPosition=at; }
        private static void MeshPart(string name,Transform root,Mesh mesh,Material material,Vector3 at,Vector3 scale,bool collision)
        {
            var obj=new GameObject(name);obj.transform.SetParent(root,false);obj.transform.localPosition=at;obj.transform.localScale=scale;obj.AddComponent<MeshFilter>().sharedMesh=mesh;obj.AddComponent<MeshRenderer>().sharedMaterial=material;if(collision)obj.AddComponent<MeshCollider>().sharedMesh=mesh;
        }
        private static void Landmark(string name,Vector3 position,Action<Transform> build)
        {
            var root=new GameObject(name);root.transform.SetParent(landscape,false);root.transform.localPosition=position;build(root.transform);Bake(root.transform,name.Replace(" ",""));
            PrefabUtility.SaveAsPrefabAssetAndConnect(root,Root+"/"+name.Replace(" ","")+".prefab",InteractionMode.AutomatedAction);
        }
        private static void Bake(Transform root,string name)
        {
            var batches=new Dictionary<Material,List<CombineInstance>>();var filters=root.GetComponentsInChildren<MeshFilter>();
            foreach(MeshFilter filter in filters)
            {
                Material m=filter.GetComponent<MeshRenderer>().sharedMaterial;if(!batches.ContainsKey(m))batches[m]=new List<CombineInstance>();batches[m].Add(new CombineInstance{mesh=filter.sharedMesh,transform=root.worldToLocalMatrix*filter.transform.localToWorldMatrix});
                UnityEngine.Object.DestroyImmediate(filter.GetComponent<MeshRenderer>());UnityEngine.Object.DestroyImmediate(filter);
            }
            foreach(var batch in batches)
            {
                var mesh=new Mesh{name=name+" "+batch.Key.name};mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;mesh.CombineMeshes(batch.Value.ToArray());AssetDatabase.CreateAsset(mesh,Root+"/Meshes/"+name+"_"+batch.Key.name+".asset");MeshPart(batch.Key.name+" Visuals",root,mesh,batch.Key,Vector3.zero,Vector3.one,false);
            }
            foreach(Transform t in root.GetComponentsInChildren<Transform>())GameObjectUtility.SetStaticEditorFlags(t.gameObject,StaticEditorFlags.BatchingStatic);
        }
        private static void Deck(Transform root,string name,float from,float to,float height,float width=3.6f)
        {
            Box(name,root,new Vector3((from+to)*0.5f,height-0.25f,0),new Vector3(to-from,0.5f,width),steel);
            Beam("Underslung spine",root,new Vector3(from,height-0.6f,0),new Vector3(to,height-0.6f,0),0.5f,0.45f,rust);
            for(float x=from+0.5f;x<to;x+=1.8f)Box("Deck seam",root,new Vector3(x,height+0.012f,0),new Vector3(0.05f,0.025f,width-0.15f),trim).GetComponent<Collider>().enabled=false;
            foreach(float x in new[]{from+0.2f,to-0.2f})Box("Jump edge paint",root,new Vector3(x,height+0.016f,0),new Vector3(0.25f,0.03f,width),gold).GetComponent<Collider>().enabled=false;
            foreach(float side in new[]{-1f,1f})
            {
                for(float x=from+0.5f;x<to-0.3f;x+=3)Box("Rail post",root,new Vector3(x,height+0.55f,side*(width*0.5f+0.08f)),new Vector3(0.13f,1.1f,0.13f),rust);
                Beam("Handrail",root,new Vector3(from+0.5f,height+1.1f,side*(width*0.5f+0.08f)),new Vector3(to-0.5f,height+1.1f,side*(width*0.5f+0.08f)),0.12f,0.12f,trim);
            }
        }
        private static void Causeway()
        {
            Landmark("Broken Relay Causeway",new Vector3(68,16,-40),root=> {
                Deck(root,"West Deck",0,12,0);Deck(root,"Hanging Deck",14,26,0);Deck(root,"East Deck",28,44,0);
                for(float x=1;x<=43;x+=14) { Beam("Trestle left",root,new Vector3(x,-24,-3),new Vector3(x,0,-1.6f),0.5f,0.5f,rust);Beam("Trestle right",root,new Vector3(x,-24,3),new Vector3(x,0,1.6f),0.5f,0.5f,rust); }
                Marker(root,"Start",new Vector3(1,0,0));Marker(root,"Finish",new Vector3(43,0,0));
                Marker(root,"Jump 1 Launch",new Vector3(11.3f,0,0));Marker(root,"Jump 1 Land",new Vector3(15,0,0));Marker(root,"Jump 2 Launch",new Vector3(25.3f,0,0));Marker(root,"Jump 2 Land",new Vector3(29,0,0));
            });
        }
        private static void RockUV(Mesh mesh)
        {
            Vector3[] vertices=mesh.vertices,normals=mesh.normals;var uv=new Vector2[vertices.Length];
            for(int i=0;i<uv.Length;i++)uv[i]=new Vector2(Mathf.Abs(normals[i].x)>Mathf.Abs(normals[i].z)?vertices[i].z:vertices[i].x,vertices[i].y)/6;
            mesh.uv=uv;
        }
        private static void Arches()
        {
            Mesh arch=LowPolyMesh.Tube("Fractured basalt spine",new[]{new Vector3(-14,-4,0),new Vector3(-13,8,0),new Vector3(-8,20,0),new Vector3(0,25,0),new Vector3(9,21,0),new Vector3(14,9,0),new Vector3(15,-4,0)},2.1f,7);
            Vector3[] vertices=arch.vertices;
            for(int i=0;i<vertices.Length;i++)vertices[i]*=0.86f+0.28f*Mathf.PerlinNoise(vertices[i].x*0.28f+10,vertices[i].y*0.19f+vertices[i].z*0.14f+6);
            arch.vertices=vertices;arch.RecalculateNormals();arch.RecalculateBounds();RockUV(arch);
            AssetDatabase.CreateAsset(arch,Root+"/Meshes/BasaltSpine.asset");
            Landmark("Spine Arch Pass",new Vector3(-95,14,60),root=> {
                MeshPart("Great arch",root,arch,stone,Vector3.zero,Vector3.one,true);
                MeshPart("Companion arch",root,arch,stone,new Vector3(-2,0,13),new Vector3(0.78f,0.7f,1.1f),true);
                Marker(root,"Pass South",new Vector3(0,0,-8));Marker(root,"Pass North",new Vector3(0,0,21));
            });
        }
        private static void Observatory()
        {
            Landmark("Split Mesa Observatory",new Vector3(-105,52,105),root=> {
                Box("Overlook deck",root,new Vector3(0,0.25f,0),new Vector3(12,0.5f,10),steel);
                Beam("Walk-on lip",root,new Vector3(-8,-0.1f,0),new Vector3(-5.5f,0.3f,0),3,0.3f,rust);
                for(int i=0;i<4;i++) { float a=i*90;Vector3 at=Quaternion.Euler(0,a,0)*new Vector3(4.7f,0,3.7f);Box("Foundation",root,at-Vector3.up*2,new Vector3(0.7f,4,0.7f),rust); }
                foreach(float z in new[]{-4.7f,4.7f})Beam("Overlook rail",root,new Vector3(-5,1.5f,z),new Vector3(5,1.5f,z),0.15f,0.15f,trim);
                Box("Relay plinth",root,new Vector3(1,1.5f,1),new Vector3(2,2,1.4f),rust);
                Beam("Signal mast",root,new Vector3(1,2,1),new Vector3(1,13,1),0.3f,0.3f,trim);
                for(int i=0;i<3;i++)Box("Signal vane",root,new Vector3(1,9+i*1.3f,1),new Vector3(6-i,0.16f,0.3f),glow,Quaternion.Euler(0,25+i*20,0));
                Marker(root,"Lookout",new Vector3(-2,0.5f,-1));
            });
        }
        private static void Tunnel()
        {
            Landmark("Ribbed Wreck Tunnel",new Vector3(-100,-7,-105),root=> {
                for(int rib=0;rib<7;rib++)
                {
                    float z=-12+rib*4;
                    for(int i=0;i<8;i++) { float a=i*Mathf.PI/8,b=(i+1)*Mathf.PI/8;Beam("Hull rib",root,new Vector3(Mathf.Cos(a)*6,Mathf.Sin(a)*9,z),new Vector3(Mathf.Cos(b)*6,Mathf.Sin(b)*9,z),0.45f,0.4f,trim); }
                    if(rib%2==0)Box("Buckled side plate",root,new Vector3(-5.5f,2.3f,z),new Vector3(0.25f,4.4f,3.5f),rust,Quaternion.Euler(0,0,-12));
                    else Box("Torn roof plate",root,new Vector3(2,8,z),new Vector3(3.5f,0.22f,3.8f),steel,Quaternion.Euler(8,0,-18));
                }
                foreach(float x in new[]{-4.5f,4.5f})Beam("Hull stringer",root,new Vector3(x,4,-13),new Vector3(x,4,13),0.24f,0.3f,rust);
                Marker(root,"South Entry",new Vector3(0,0,-15));Marker(root,"North Entry",new Vector3(0,0,15));
            });
        }
        private static void SteppingFins()
        {
            Landmark("Basalt Stepping Fins",new Vector3(100,22,-106),root=> {
                Deck(root,"Staging Deck",-4,8,0,5);
                for(int i=0;i<5;i++)
                {
                    float x=12.2f+i*6.4f,top=i*0.4f;
                    Mesh pillar=LowPolyMesh.Lathe("Flat fractured fin "+i,new[]{new Vector2(0,-32),new Vector2(1.65f,-32),new Vector2(2.1f,-10),new Vector2(2.2f,top-0.2f),new Vector2(2.2f,top),new Vector2(0,top)},8);
                    AssetDatabase.CreateAsset(pillar,Root+"/Meshes/SteppingFin"+i+".asset");MeshPart("Jump Fin "+(i+1),root,pillar,stone,new Vector3(x,0,0),Vector3.one,true);
                    Box("Landing stripe",root,new Vector3(x,top+0.015f,0),new Vector3(1.8f,0.025f,0.22f),gold).GetComponent<Collider>().enabled=false;
                    Marker(root,"Fin "+(i+1),new Vector3(x,top,0));
                }
                Deck(root,"Isolated Lookout",42,54,2,6);
                foreach(float x in new[]{44f,52f})Beam("Lookout foot",root,new Vector3(x,-34,0),new Vector3(x,1.7f,0),1.1f,1.2f,steel);
                Marker(root,"Start",new Vector3(5,0,0));Marker(root,"Finish",new Vector3(48,2,0));
            });
        }
        private static void FallenMast()
        {
            Landmark("Fallen Relay Mast",new Vector3(112,44.5f,110),root=> {
                Vector3 end=new Vector3(36,12,0);Beam("Sloping catwalk",root,Vector3.zero,end,3.5f,0.45f,steel);
                Beam("Walk-on ramp",root,new Vector3(-5,-0.5f,0),Vector3.zero,3.5f,0.35f,rust);
                for(int i=0;i<=9;i++)
                {
                    float x=i*4,y=i*4/3f;
                    foreach(float z in new[]{-2.5f,2.5f})
                    {
                        if(i==0)Beam("Outer chord",root,new Vector3(0,-2,z),new Vector3(36,10,z),0.2f,0.24f,rust);
                        Beam("Truss rib",root,new Vector3(x,y-2,z),new Vector3(x,y+3,z),0.18f,0.18f,trim);
                        if(i<9)Beam("Diagonal web",root,new Vector3(x,y-2,z),new Vector3(x+4,y+4.33f,z),0.15f,0.15f,trim);
                    }
                    Beam("Broken hoop",root,new Vector3(x,y+3,-2.5f),new Vector3(x,y+3,2.5f),0.16f,0.16f,rust);
                }
                Box("High balcony",root,new Vector3(40,12,0),new Vector3(8,0.5f,7),steel);
                Box("Broken transmitter",root,new Vector3(38,16,2),new Vector3(1,8,1),rust,Quaternion.Euler(0,0,-14));
                Box("Beacon face",root,new Vector3(38,20,2),new Vector3(2.5f,0.25f,1.5f),glow);
                Marker(root,"Start",new Vector3(-3,0,0));Marker(root,"High Balcony",new Vector3(40,12.25f,0));
            });
        }
        private static void Scatter()
        {
            Mesh rock=LowPolyMesh.Icosphere("Fractured scattered basalt",1);AssetDatabase.CreateAsset(rock,Root+"/Meshes/ScatteredBasalt.asset");
            var root=new GameObject("Outlying Rock Fields").transform;root.SetParent(landscape,false);var rng=new System.Random(615802);int placed=0;
            for(int attempt=0;attempt<12000&&placed<260;attempt++)
            {
                Vector2 p=new Vector2((float)rng.NextDouble()*390-195,(float)rng.NextDouble()*390-195);if(p.magnitude<65||p.magnitude>188)continue;
                bool clear=true;foreach(Vector2 site in Sites)if(Vector2.Distance(p,site)<(site.x==125?38:24)){clear=false;break;}
                foreach(Vector3[] route in AccessRoutes)for(int i=1;i<route.Length;i++)if(Segment(p,XZ(route[i-1]),XZ(route[i]),out _)<6){clear=false;break;}
                if(!clear||terrain.terrainData.GetSteepness((p.x+240)/480,(p.y+240)/480)>35)continue;
                Vector3 at=center+new Vector3(p.x,0,p.y);at.y=Ground(at);float size=0.7f+(float)rng.NextDouble()*3.5f;
                MeshPart("Outlying basalt "+(++placed),root,rock,placed%4==0?rust:placed%3==0?trim:stone,at-center,new Vector3(size*1.3f,size*0.9f,size),true);
                root.GetChild(root.childCount-1).localRotation=Quaternion.Euler(0,(float)rng.NextDouble()*360,0);
            }
            Bake(root,"OutlyingRockFields");Debug.Log("TRAVERSAL_DRESSING: "+placed+" additional solid rocks.");
        }
    }
}
