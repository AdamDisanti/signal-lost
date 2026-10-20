using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace SignalLost.Editor
{
    // One-shot revision; delivered terrain/prefabs remain ordinary editable Unity assets.
    public static class LunarTutorialBuilder
    {
        public const string Root=TutorialCrashSiteBuilder.AssetRoot+"/LunarLandscape", LandscapeName="Lunar Landscape";
        public static readonly Vector2[][] Routes={
            new[]{new Vector2(-30,30),new Vector2(-75,65),new Vector2(-120,90),new Vector2(-135,115)},
            new[]{new Vector2(-30,10),new Vector2(-95,0),new Vector2(-150,-35),new Vector2(-165,35)},
            new[]{new Vector2(-25,-30),new Vector2(-65,-90),new Vector2(-110,-125),new Vector2(-135,-85)},
            new[]{new Vector2(0,-40),new Vector2(20,-95),new Vector2(-25,-145),new Vector2(-65,-160)},
            new[]{new Vector2(30,-25),new Vector2(70,-65),new Vector2(105,-106),new Vector2(135,-125)},
            new[]{new Vector2(35,5),new Vector2(90,20),new Vector2(150,45),new Vector2(165,-15)},
            new[]{new Vector2(25,35),new Vector2(65,90),new Vector2(115,125),new Vector2(155,85)},
            new[]{new Vector2(-5,40),new Vector2(-15,100),new Vector2(25,145),new Vector2(-40,170)}
        };
        // X/Z, radius and bowl depth. Smaller overlapping impacts are generated reproducibly below.
        public static readonly Vector4[] Craters=MakeCraters();
        private static Terrain terrain;
        private static Transform environment,landscape;
        private static Material[] rocks;
        private static Vector4[] MakeCraters()
        {
            var result=new List<Vector4>{new Vector4(-100,70,28,3.8f),new Vector4(-78,96,17,2.2f),new Vector4(42,132,34,3.6f),new Vector4(125,30,24,3.2f),new Vector4(-55,-125,32,3.6f),new Vector4(102,-106,23,3),new Vector4(125,-127,12,1.5f),new Vector4(-139,-33,22,2.6f),new Vector4(-115,132,19,2),new Vector4(8,-76,18,2),new Vector4(-70,-40,14,1.8f),new Vector4(-8,72,16,2)};
            var rng=new System.Random(94271);
            for(int attempt=0;attempt<2000&&result.Count<57;attempt++)
            {
                var p=new Vector2((float)rng.NextDouble()*340-170,(float)rng.NextDouble()*340-170);if(p.magnitude<58||p.magnitude>172)continue;
                float radius=5+(float)rng.NextDouble()*7;bool clear=true;
                foreach(Vector4 c in result)if(Vector2.Distance(p,new Vector2(c.x,c.y))<(radius+c.z)*0.55f){clear=false;break;}
                if(clear)result.Add(new Vector4(p.x,p.y,radius,0.5f+radius*0.06f));
            }
            return result.ToArray();
        }
        private static float Blend(float a,float b,float x)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,x));
        private static float Bowl(Vector2 p,Vector4 crater,out float q)
        {
            Vector2 delta=p-new Vector2(crater.x,crater.y);float angle=Mathf.Atan2(delta.y,delta.x);
            q=delta.magnitude/(crater.z*(1+0.055f*Mathf.Sin(angle*3+crater.x)));if(q>1.7f)return 0;
            float depression=q<1?-crater.w*Mathf.Pow(1-q*q,2):0;
            float rim=crater.w*0.45f*Mathf.Exp(-Mathf.Pow((q-1.02f)/0.25f,2));return depression+rim;
        }
        private static float Rolling(Vector2 p)
        {
            return 6+2.8f*Mathf.Sin(p.x/28+0.4f*Mathf.Sin(p.y/60))+2.4f*Mathf.Cos(p.y/35+0.35f*Mathf.Sin(p.x/44))
                +1.6f*Mathf.Sin((p.x*0.65f+p.y*0.78f)/16)+(Mathf.PerlinNoise(p.x*0.045f+21,p.y*0.045f+8)-0.5f)*2;
        }
        private static float Ground(Vector3 p)=>terrain.SampleHeight(p)+terrain.transform.position.y;
        private static Vector3 World(Vector2 p)=>environment.position+new Vector3(p.x,0,p.y);
        private static float Segment(Vector2 p,Vector2 a,Vector2 b,out float t){Vector2 d=b-a;t=Mathf.Clamp01(Vector2.Dot(p-a,d)/d.sqrMagnitude);return Vector2.Distance(p,a+d*t);}
        private static void Folder(string path){if(AssetDatabase.IsValidFolder(path))return;int slash=path.LastIndexOf('/');Folder(path.Substring(0,slash));AssetDatabase.CreateFolder(path.Substring(0,slash),path.Substring(slash+1));}
        private static Material Mat(string name,Color color)
        {
            var material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,enableInstancing=true};material.color=color;material.SetFloat("_Smoothness",0.04f);AssetDatabase.CreateAsset(material,Root+"/Materials/"+name+".mat");return material;
        }
        [MenuItem("Signal Lost/Tutorial Crash Site/Build Lunar Revision Once")]
        public static void Create()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
            if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            var scene=EditorSceneManager.OpenScene(TutorialCrashSiteBuilder.ScenePath);environment=GameObject.Find(TutorialCrashSiteBuilder.EnvironmentName).transform;
            if(environment.Find(LandscapeName)!=null||AssetDatabase.IsValidFolder(Root)||environment.Find(TutorialTraversalBuilder.LandscapeName)==null)throw new InvalidOperationException("Expected the saved traversal pass; do not overwrite an authored lunar revision.");
            terrain=environment.GetComponentInChildren<Terrain>();if(terrain.terrainData.size.x!=480||terrain.terrainData.heightmapResolution!=513)throw new InvalidOperationException("Expected the 480m tutorial.");
            Directory.CreateDirectory("Logs/BeforeLunarPass");File.Copy(TutorialCrashSiteBuilder.ScenePath,"Logs/BeforeLunarPass/Tutorial.unity",true);File.Copy(AssetDatabase.GetAssetPath(terrain.terrainData),"Logs/BeforeLunarPass/ImpactCrater.asset",true);
            foreach(string path in new[]{Root,Root+"/Meshes",Root+"/Materials",Root+"/Terrain"})Folder(path);
            var items=new List<Transform>();var offsets=new List<float>();
            foreach(string name in new[]{"Basalt Rocks","Ship Debris"})foreach(Transform item in environment.Find(name)){items.Add(item);offsets.Add(item.position.y-Ground(item.position));}
            Sculpt();rocks=new[]{Mat("MoonBasalt",new Color(0.31f,0.32f,0.34f)),Mat("MoonBreccia",new Color(0.43f,0.44f,0.46f)),Mat("MoonAnorthosite",new Color(0.57f,0.58f,0.59f))};Paint();
            for(int i=0;i<items.Count;i++)
            {
                Transform item=items[i];Vector3 p=item.position;Vector2 local=new Vector2(p.x-environment.position.x,p.z-environment.position.z);float radius=0;
                foreach(Renderer renderer in item.GetComponentsInChildren<Renderer>())radius=Mathf.Max(radius,renderer.bounds.extents.x,renderer.bounds.extents.z);
                if(local.magnitude>45)for(int pass=0;pass<8;pass++)foreach(Vector2[] route in Routes)for(int j=1;j<route.Length;j++)
                {
                    float d=Segment(local,route[j-1],route[j],out float t);if(d>=radius+2)continue;
                    Vector2 q=Vector2.Lerp(route[j-1],route[j],t),away=d>0.01f?(local-q).normalized:Vector2.Perpendicular(route[j]-route[j-1]).normalized;local=q+away*(radius+2.5f);
                }
                p=World(local);p.y=Ground(p)+offsets[i];item.position=p;PrefabUtility.RecordPrefabInstancePropertyModifications(item);
                if(item.parent.name=="Basalt Rocks")foreach(Renderer renderer in item.GetComponentsInChildren<Renderer>()){renderer.sharedMaterial=rocks[i%rocks.Length];PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);}
            }
            // Remove scene instances only: original traversal prefabs/meshes and their GUIDs remain intact.
            UnityEngine.Object.DestroyImmediate(environment.Find(TutorialTraversalBuilder.LandscapeName).gameObject);
            landscape=new GameObject(LandscapeName).transform;landscape.SetParent(environment,false);Scatter();
            PrefabUtility.SaveAsPrefabAssetAndConnect(landscape.gameObject,Root+"/LunarLandscape.prefab",InteractionMode.AutomatedAction);
            Transform cliffs=environment.Find("Perimeter Cliffs");cliffs.localScale=new Vector3(1.5f,1,1.5f);cliffs.localPosition=new Vector3(0,-4,0);PrefabUtility.RecordPrefabInstancePropertyModifications(cliffs);
            foreach(Renderer renderer in cliffs.GetComponentsInChildren<Renderer>()){renderer.sharedMaterials=new[]{rocks[1],rocks[2],rocks[0]};PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);}
            SaveEnvironment();Lighting();terrain.Flush();EditorUtility.SetDirty(terrain.terrainData);AssetDatabase.SaveAssets();Physics.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save lunar tutorial.");TutorialGameplayBuilder.Validate();Debug.Log("LUNAR_TUTORIAL_CREATED");
        }
        private static void Sculpt()
        {
            TerrainData data=terrain.terrainData;int n=data.heightmapResolution;float[,] before=data.GetHeights(0,0,n,n),height=new float[n,n];
            for(int z=0;z<n;z++)for(int x=0;x<n;x++)
            {
                Vector2 p=new Vector2(x*480f/(n-1)-240,z*480f/(n-1)-240);float h=Rolling(p);
                foreach(Vector4 c in Craters)h+=Bowl(p,c,out _);
                h=Mathf.Lerp(h,3,Blend(183,198,p.magnitude));
                float angle=Mathf.Atan2(p.y,p.x),rim=Blend(195,215,p.magnitude);
                h+=(16.5f+2*Mathf.Sin(angle*5)+Mathf.Sin(angle*9))*rim;
                h=Mathf.Lerp(before[z,x]*96-16,h,Blend(42,64,p.magnitude));height[z,x]=(h+16)/96;
            }
            data.SetHeights(0,0,height);terrain.heightmapPixelError=2;
        }
        private static TerrainLayer Layer(string name,Color color)
        {
            var image=new Texture2D(256,256,TextureFormat.RGB24,false);var rng=new System.Random(1138);
            for(int y=0;y<256;y++)for(int x=0;x<256;x++){float grain=(float)rng.NextDouble();float shade=grain<0.04f?0.58f:0.84f+Mathf.PerlinNoise(x*0.035f,y*0.035f)*0.16f+grain*0.12f;image.SetPixel(x,y,color*shade);}
            image.Apply();string path=Root+"/Terrain/"+name+".png";File.WriteAllBytes(path,image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var layer=new TerrainLayer{name=name,diffuseTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(path),tileSize=Vector2.one*5,smoothness=0.02f};AssetDatabase.CreateAsset(layer,Root+"/Terrain/"+name+".terrainlayer");return layer;
        }
        private static void Paint()
        {
            TerrainData data=terrain.terrainData;data.terrainLayers=new[]{Layer("GreyRegolith",new Color(0.46f,0.47f,0.48f)),Layer("PaleEjecta",new Color(0.56f,0.57f,0.58f)),Layer("DarkImpactDust",new Color(0.31f,0.32f,0.33f))};
            int n=data.alphamapResolution;var alpha=new float[n,n,3];
            for(int z=0;z<n;z++)for(int x=0;x<n;x++)
            {
                Vector2 p=new Vector2(x*480f/(n-1)-240,z*480f/(n-1)-240);float ejecta=0,dark=(1-Blend(8,30,p.magnitude))*0.7f;
                foreach(Vector4 c in Craters){Bowl(p,c,out float q);ejecta=Mathf.Max(ejecta,Mathf.Exp(-Mathf.Pow((q-1.12f)/0.36f,2))*0.65f);dark=Mathf.Max(dark,(1-Blend(0.25f,0.9f,q))*0.38f);}
                ejecta*=Blend(28,48,p.magnitude);alpha[z,x,1]=ejecta;alpha[z,x,2]=dark;alpha[z,x,0]=1-ejecta-dark;
            }
            data.SetAlphamaps(0,0,alpha);
        }
        private static void Scatter()
        {
            var meshes=new Mesh[8];
            for(int form=0;form<meshes.Length;form++)
            {
                Mesh mesh=LowPolyMesh.Icosphere("Lunar impact breccia "+form,form<4?1:0);Vector3[] vertices=mesh.vertices;
                for(int i=0;i<vertices.Length;i++){Vector3 v=vertices[i];float k=0.78f+Mathf.PerlinNoise(v.x*3+form*7,v.y*2+v.z*2+form)*0.44f;vertices[i]=new Vector3(v.x*k,v.y*k*(0.55f+(form%4)*0.12f),v.z*k);}
                mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,Root+"/Meshes/Breccia"+form+".asset");meshes[form]=mesh;
            }
            var rng=new System.Random(150874);int placed=0,solid=0;
            for(int attempt=0;attempt<40000&&placed<10000;attempt++)
            {
                var p=new Vector2((float)rng.NextDouble()*380-190,(float)rng.NextDouble()*380-190);if(p.magnitude<43||p.magnitude>188)continue;
                bool clear=true;foreach(Vector2[] route in Routes)for(int i=1;i<route.Length;i++)if(Segment(p,route[i-1],route[i],out _)<2.3f){clear=false;break;}if(!clear)continue;
                float size=placed<1500&&placed%7==0?0.9f+(float)rng.NextDouble()*0.9f:0.07f+(float)rng.NextDouble()*0.25f;
                Mesh shape=meshes[(size>=0.9f?0:4)+placed%4];
                Vector3 at=World(p);at.y=Ground(at)-size*0.08f;var obj=new GameObject(size>=0.9f?"Solid lunar boulder":"Regolith stone");obj.transform.SetParent(landscape,false);obj.transform.position=at;obj.transform.localScale=new Vector3(size, size*(0.65f+(float)rng.NextDouble()*0.45f),size*(0.7f+(float)rng.NextDouble()*0.5f));obj.transform.rotation=Quaternion.Euler(0,(float)rng.NextDouble()*360,0);
                obj.AddComponent<MeshFilter>().sharedMesh=shape;obj.AddComponent<MeshRenderer>().sharedMaterial=rocks[placed%3];if(size>=0.9f){obj.AddComponent<MeshCollider>().sharedMesh=shape;solid++;}placed++;
            }
            var batches=new Dictionary<Material,List<CombineInstance>>();foreach(MeshFilter filter in landscape.GetComponentsInChildren<MeshFilter>())
            {
                Material material=filter.GetComponent<MeshRenderer>().sharedMaterial;if(!batches.ContainsKey(material))batches[material]=new List<CombineInstance>();batches[material].Add(new CombineInstance{mesh=filter.sharedMesh,transform=landscape.worldToLocalMatrix*filter.transform.localToWorldMatrix});
                var obj=filter.gameObject;UnityEngine.Object.DestroyImmediate(filter.GetComponent<MeshRenderer>());UnityEngine.Object.DestroyImmediate(filter);if(obj.GetComponent<Collider>()==null)UnityEngine.Object.DestroyImmediate(obj);
            }
            foreach(var batch in batches)
            {
                var mesh=new Mesh{name="Lunar stone field "+batch.Key.name,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(batch.Value.ToArray());AssetDatabase.CreateAsset(mesh,Root+"/Meshes/StoneField_"+batch.Key.name+".asset");var obj=new GameObject(batch.Key.name+" Visuals");obj.transform.SetParent(landscape,false);obj.AddComponent<MeshFilter>().sharedMesh=mesh;obj.AddComponent<MeshRenderer>().sharedMaterial=batch.Key;
            }
            foreach(Transform child in landscape.GetComponentsInChildren<Transform>())GameObjectUtility.SetStaticEditorFlags(child.gameObject,StaticEditorFlags.BatchingStatic);
            Debug.Log("LUNAR_STONES: "+placed+" stones; "+solid+" solid boulders; tiny stones are decorative to avoid controller snagging.");
        }
        private static void SaveEnvironment()
        {
            string path=TutorialCrashSiteBuilder.AssetRoot+"/CraterEnvironment.prefab";GameObject reusable=PrefabUtility.LoadPrefabContents(path);
            try
            {
                UnityEngine.Object.DestroyImmediate(reusable.transform.Find(TutorialTraversalBuilder.LandscapeName).gameObject);
                foreach(string name in new[]{"Basalt Rocks","Ship Debris","Perimeter Cliffs"})
                {
                    Transform source=environment.Find(name),target=reusable.transform.Find(name);target.localPosition=source.localPosition;target.localScale=source.localScale;
                    if(name!="Perimeter Cliffs")for(int i=0;i<source.childCount;i++)target.GetChild(i).localPosition=source.GetChild(i).localPosition;
                    Renderer[] a=source.GetComponentsInChildren<Renderer>(),b=target.GetComponentsInChildren<Renderer>();for(int i=0;i<a.Length;i++)b[i].sharedMaterials=a[i].sharedMaterials;
                }
                reusable.GetComponentInChildren<Terrain>().heightmapPixelError=terrain.heightmapPixelError;var linked=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/LunarLandscape.prefab"),reusable.transform);linked.name=LandscapeName;PrefabUtility.SaveAsPrefabAsset(reusable,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(reusable);}
        }
        private static void Lighting()
        {
            var sky=new Material(Shader.Find("Skybox/Procedural")){name="Airless Lunar Sky"};sky.SetFloat("_AtmosphereThickness",0);sky.SetColor("_SkyTint",Color.black);sky.SetColor("_GroundColor",Color.black);sky.SetFloat("_Exposure",0.8f);sky.SetFloat("_SunSize",0.015f);AssetDatabase.CreateAsset(sky,Root+"/Materials/AirlessSky.mat");RenderSettings.skybox=sky;RenderSettings.fog=false;
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(0.23f,0.24f,0.27f);RenderSettings.ambientEquatorColor=new Color(0.12f,0.13f,0.15f);RenderSettings.ambientGroundColor=new Color(0.05f,0.06f,0.07f);
            foreach(Light light in UnityEngine.Object.FindObjectsByType<Light>())if(light.enabled&&light.type==LightType.Directional){light.intensity=1.8f;light.color=new Color(1,0.98f,0.95f);light.transform.rotation=Quaternion.Euler(22,-35,0);RenderSettings.sun=light;}
        }
    }
}
