using System;
using System.Collections.Generic;
using System.IO;
using SignalLost.Editor;
using SignalLost.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Copy alongside CrashSiteSmoke only into a disposable temp project's Assets/Editor.
public static class LunarTutorialSmoke
{
    private static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    private static void Guard()
    {
        string project=Path.GetFullPath(Path.Combine(Application.dataPath,"..")),temp=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
        Check(Application.isBatchMode&&project.StartsWith(temp,StringComparison.OrdinalIgnoreCase),"Disposable batch project required.");
    }
    public static void CheckDistributionOnly(){Guard();EditorSceneManager.OpenScene(TutorialCrashSiteBuilder.ScenePath);Distribution(GameObject.Find(TutorialCrashSiteBuilder.EnvironmentName).GetComponentInChildren<Terrain>());}
    public static void Validate()
    {
        Guard();Transform environment=GameObject.Find(TutorialCrashSiteBuilder.EnvironmentName).transform;Terrain terrain=environment.GetComponentInChildren<Terrain>();
        Check(terrain.terrainData.size==new Vector3(480,96,480)&&terrain.terrainData.heightmapResolution==513,"Map footprint/resolution changed.");
        Check(environment.Find(LunarTutorialBuilder.LandscapeName)!=null&&environment.Find(TutorialTraversalBuilder.LandscapeName)==null,"Replace the industrial traversal instances with the lunar landscape.");
        Distribution(terrain);Check(terrain.terrainData.terrainLayers.Length==3,"Expected three neutral lunar layers.");
        Check(LunarTutorialBuilder.Craters.Length==57,"Missing multi-scale impact craters.");
        Check(!RenderSettings.fog&&RenderSettings.skybox.GetFloat("_AtmosphereThickness")==0,"Expected airless sky without terrestrial distance fog.");
        Transform landscape=environment.Find(LunarTutorialBuilder.LandscapeName);Check(PrefabUtility.IsPartOfPrefabInstance(landscape.gameObject),"Lunar landscape should remain linked.");
        MeshCollider[] boulders=landscape.GetComponentsInChildren<MeshCollider>();Check(boulders.Length==215,"Expected 215 solid boulders among 10000 combined stones.");
        foreach(MeshCollider collider in boulders)
        {
            Check(collider.enabled&&!collider.isTrigger&&!collider.convex&&collider.sharedMesh!=null,"Boulder collision missing.");
            Check(Mathf.Abs(collider.transform.position.y-Ground(terrain,collider.transform.position))<0.2f,"Floating boulder: "+collider.transform.position);
        }
        Check(landscape.GetComponentsInChildren<MeshRenderer>().Length==3&&landscape.GetComponentsInChildren<Light>().Length==0,"Stone field should be three combined material batches, without extra lights.");
        ulong indices=0;foreach(MeshFilter filter in landscape.GetComponentsInChildren<MeshFilter>())indices+=filter.sharedMesh.GetIndexCount(0);
        Check(indices==212900UL*3,"Missing combined low-poly pebble geometry.");
        CompareSavedState(environment,terrain);Debug.Log("LUNAR_TUTORIAL_VALIDATED");
    }
    private static float Ground(Terrain terrain,Vector3 p)=>terrain.SampleHeight(p)+terrain.transform.position.y;
    private static void Distribution(Terrain terrain)
    {
        TerrainData data=terrain.terrainData;float min=999,max=-999;var slopes=new List<float>();int steep=0,tiles=0,varied=0;float worstRange=99;
        for(int z=-180;z<=180;z+=3)for(int x=-180;x<=180;x+=3)
        {
            float r=new Vector2(x,z).magnitude;if(r<50||r>183)continue;float u=(x+240)/480f,v=(z+240)/480f;
            float h=data.GetInterpolatedHeight(u,v)+terrain.transform.position.y,s=data.GetSteepness(u,v);min=Mathf.Min(min,h);max=Mathf.Max(max,h);slopes.Add(s);if(s>30)steep++;
        }
        for(int z=-156;z<=156;z+=24)for(int x=-156;x<=156;x+=24)
        {
            float r=new Vector2(x,z).magnitude;if(r<65||r>168)continue;float lo=999,hi=-999;
            for(int dz=-12;dz<=12;dz+=4)for(int dx=-12;dx<=12;dx+=4){float h=data.GetInterpolatedHeight((x+dx+240)/480f,(z+dz+240)/480f);lo=Mathf.Min(lo,h);hi=Mathf.Max(hi,h);}
            tiles++;if(hi-lo>=1.1f)varied++;worstRange=Mathf.Min(worstRange,hi-lo);
        }
        slopes.Sort();float p95=slopes[(int)(slopes.Count*0.95f)],coverage=varied/(float)tiles,steepFraction=steep/(float)slopes.Count;
        string report="Explorable interior (outside core/containment): elevation "+min.ToString("F2")+".."+max.ToString("F2")+"m; 95th-percentile slope "+p95.ToString("F2")+" degrees; "+(steepFraction*100).ToString("F2")+"% above 30 degrees; "+varied+"/"+tiles+" 24m patches have >=1.1m local relief; least patch relief "+worstRange.ToString("F2")+"m.";
        Debug.Log("LUNAR_RELIEF_DISTRIBUTION: "+report);Directory.CreateDirectory("Logs");File.WriteAllText("Logs/LunarTerrainDistribution.txt",report);
        Check(min>-6&&max<20&&p95<27&&steepFraction<0.01f&&coverage>=0.9f,"Terrain still has isolated extremes or insufficient distributed rolling variation. "+report);
    }
    private static void CompareSavedState(Transform environment,Terrain terrain)
    {
        const string asset="Assets/Editor/LunarBaseline.asset",scenePath="Assets/Editor/LunarBaseline.unity",folder="Logs/BeforeLunarPass/";
        Check(!File.Exists(asset)&&!File.Exists(scenePath),"Refusing to overwrite test fixtures.");File.Copy(folder+"ImpactCrater.asset",asset);File.Copy(folder+"Tutorial.unity",scenePath);
        AssetDatabase.ImportAsset(asset,ImportAssetOptions.ForceSynchronousImport);AssetDatabase.ImportAsset(scenePath,ImportAssetOptions.ForceSynchronousImport);
        var current=UnityEngine.SceneManagement.SceneManager.GetActiveScene();var before=EditorSceneManager.OpenScene(scenePath,OpenSceneMode.Additive);
        try
        {
            TerrainData old=AssetDatabase.LoadAssetAtPath<TerrainData>(asset);float error=0;
            for(int z=-38;z<=38;z+=2)for(int x=-38;x<=38;x+=2)
            {
                if(new Vector2(x,z).magnitude>38)continue;
                error=Mathf.Max(error,Mathf.Abs(old.GetInterpolatedHeight((x+240)/480f,(z+240)/480f)-16-Ground(terrain,environment.position+new Vector3(x,0,z))));
            }
            Check(error<0.002f,"Core crash floor changed: "+error);
            foreach(string name in new[]{"FirstPersonPlayer","CapybaraAstronaut","BurntRockBasin","AlcoholFlask","GrayboxExitGate","Tutorial Gameplay","Tutorial HUD","Enhanced Crashed Rocket"})
            {
                Transform a=Find(before,name),b=Find(current,name);Check(Vector3.Distance(a.position,b.position)<0.001f&&a.localScale==b.localScale&&Quaternion.Angle(a.rotation,b.rotation)<0.001f,"Saved core actor changed: "+name);
            }
            foreach(string name in new[]{"Basalt Rocks","Ship Debris"})
            {
                Transform a=Find(before,name),b=Find(current,name);Check(a.childCount==b.childCount,"Original dressing removed.");
                for(int i=0;i<a.childCount;i++)Check(a.GetChild(i).name==b.GetChild(i).name&&a.GetChild(i).localScale==b.GetChild(i).localScale&&Quaternion.Angle(a.GetChild(i).rotation,b.GetChild(i).rotation)<0.001f,"Original dressing resized/replaced.");
            }
            Debug.Log("LUNAR_SAVED_STATE_PASS: core floor max delta "+error+"m; full core actor/ship transforms and original rock/debris count/scale/rotation retained.");
        }
        finally{EditorSceneManager.CloseScene(before,true);UnityEngine.SceneManagement.SceneManager.SetActiveScene(current);AssetDatabase.DeleteAsset(asset);AssetDatabase.DeleteAsset(scenePath);}
    }
    private static Transform Find(UnityEngine.SceneManagement.Scene scene,string name)
    {
        foreach(GameObject root in scene.GetRootGameObjects())foreach(Transform t in root.GetComponentsInChildren<Transform>(true))if(t.name==name)return t;throw new InvalidOperationException("Missing actor "+name);
    }
    public static void TestRoutes(CharacterController cc,Terrain terrain,Transform environment,FirstPersonController controller)
    {
        float speed=(float)typeof(FirstPersonController).GetField("walkSpeed",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(controller);
        float gravity=(float)typeof(FirstPersonController).GetField("gravity",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(controller);
        for(int r=0;r<LunarTutorialBuilder.Routes.Length;r++)
        {
            Vector2[] route=LunarTutorialBuilder.Routes[r];Vector3 start=environment.position+new Vector3(route[0].x,0,route[0].y);start.y=Ground(terrain,start)+0.1f;CrashSiteSmoke.Reset(cc,start);
            for(int i=1;i<route.Length;i++)Walk(cc,environment.position+new Vector3(route[i].x,0,route[i].y),terrain,speed,gravity);
            Debug.Log("LUNAR_WALK_ROUTE_PASS: "+r);
        }
        Debug.Log("LUNAR_EXPLORATION_ROUTES_PASS: eight uncarved walk-only circuits through hills/crater fields; existing movement unchanged.");
    }
    private static void Walk(CharacterController cc,Vector3 target,Terrain terrain,float speed,float gravity)
    {
        Vector3 delta=target-cc.transform.position;delta.y=0;int budget=Mathf.CeilToInt(delta.magnitude/speed*60)*5+200;float vertical=-2;
        while(budget-->0)
        {
            delta=target-cc.transform.position;delta.y=0;if(delta.magnitude<0.13f)break;if(cc.isGrounded&&vertical<0)vertical=-2;vertical-=gravity/60;
            cc.Move(delta.normalized*Mathf.Min(speed/60,delta.magnitude)+Vector3.up*(vertical/60));
        }
        delta=target-cc.transform.position;delta.y=0;Check(delta.magnitude<0.25f&&cc.isGrounded&&Mathf.Abs(cc.transform.position.y-Ground(terrain,cc.transform.position))<0.25f,"Lunar route blocked: "+cc.transform.position+" -> "+target);
    }
    public static void CaptureViews(Transform environment,Terrain terrain)
    {
        Vector3 c=environment.position;Capture(c+new Vector3(-270,230,-290),c+Vector3.up*4,"LunarOverview",58);
        GroundView(c,terrain,new Vector2(-28,-17),new Vector2(-80,50),"LunarSpawnHorizon",1.7f);
        GroundView(c,terrain,new Vector2(-84,44),new Vector2(-105,77),"LunarWesternCraters",2.2f);
        GroundView(c,terrain,new Vector2(3,102),new Vector2(45,137),"LunarNorthernHills",2.8f);
        GroundView(c,terrain,new Vector2(-85,-88),new Vector2(-48,-132),"LunarSouthernBasin",3);
        GroundView(c,terrain,new Vector2(85,-78),new Vector2(116,-110),"LunarEjectaField",2);
    }
    private static void GroundView(Vector3 c,Terrain terrain,Vector2 at,Vector2 target,string name,float height)
    {
        Vector3 a=c+new Vector3(at.x,0,at.y),b=c+new Vector3(target.x,0,target.y);a.y=Ground(terrain,a)+height;b.y=Ground(terrain,b)+0.5f;Capture(a,b,name,72);
    }
    private static void Capture(Vector3 at,Vector3 target,string name,float fov)
    {
        var obj=new GameObject("Temporary lunar camera");var camera=obj.AddComponent<Camera>();camera.enabled=false;camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;camera.transform.position=at;camera.transform.LookAt(target);camera.fieldOfView=fov;camera.nearClipPlane=0.1f;camera.farClipPlane=1000;camera.clearFlags=CameraClearFlags.Skybox;
        var output=new RenderTexture(1600,1000,24,RenderTextureFormat.ARGB32);output.Create();var request=new RenderPipeline.StandardRequest{destination=output};Check(RenderPipeline.SupportsRenderRequest(camera,request),"URP request unavailable.");RenderPipeline.SubmitRenderRequest(camera,request);
        RenderTexture previous=RenderTexture.active;RenderTexture.active=output;var image=new Texture2D(1600,1000,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,1000),0,0);image.Apply();File.WriteAllBytes("Logs/"+name+".png",image.EncodeToPNG());RenderTexture.active=previous;UnityEngine.Object.Destroy(image);output.Release();UnityEngine.Object.Destroy(output);UnityEngine.Object.Destroy(obj);
    }
}
