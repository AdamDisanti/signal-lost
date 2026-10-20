using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using SignalLost.Editor;
using SignalLost.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Install alongside CrashSiteSmoke only in a disposable temp project's Assets/Editor.
public static class TutorialTraversalSmoke
{
    private static void Check(bool condition,string message) { if(!condition)throw new InvalidOperationException(message); }
    private static void Guard()
    {
        string project=Path.GetFullPath(Path.Combine(Application.dataPath,".."));
        Check(Application.isBatchMode&&project.StartsWith(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),"Disposable batch project required.");
    }
    public static void CheckOnly() { Guard();EditorSceneManager.OpenScene(TutorialCrashSiteBuilder.ScenePath);Validate(); }
    public static void Validate()
    {
        Guard();Transform environment=GameObject.Find(TutorialCrashSiteBuilder.EnvironmentName).transform;
        Terrain terrain=environment.GetComponentInChildren<Terrain>();TerrainData data=terrain.terrainData;
        Check(data.size==new Vector3(480,96,480)&&data.heightmapResolution==513,"Expected redesigned 480m tutorial with 513 height samples.");
        Transform landscape=environment.Find(TutorialTraversalBuilder.LandscapeName);Check(landscape!=null,"Missing traversal landscape.");
        string[] sites={"Broken Relay Causeway","Spine Arch Pass","Split Mesa Observatory","Ribbed Wreck Tunnel","Basalt Stepping Fins","Fallen Relay Mast"};
        foreach(string name in sites)
        {
            Transform site=landscape.Find(name);Check(site!=null&&PrefabUtility.IsPartOfPrefabInstance(site.gameObject),"Missing linked landmark: "+name);
            Check(site.GetComponentsInChildren<Collider>().Length>0,"Landmark has no collision: "+name);
            foreach(Renderer renderer in site.GetComponentsInChildren<Renderer>())foreach(Material material in renderer.sharedMaterials)Check(material!=null&&material.shader.name=="Universal Render Pipeline/Lit","Invalid landmark material.");
        }
        float min=99,max=-99,slope=0;int steep=0;
        for(int z=0;z<=160;z++)for(int x=0;x<=160;x++)
        {
            float u=x/160f,v=z/160f,h=data.GetInterpolatedHeight(u,v)+terrain.transform.position.y;
            min=Mathf.Min(min,h);max=Mathf.Max(max,h);float s=data.GetSteepness(u,v);slope=Mathf.Max(slope,s);if(s>50)steep++;
        }
        Check(min < -7&&max>=51&&max<60&&slope>60&&steep>150,"Relief is not meaningfully more extreme: "+min+".."+max+"; slope="+slope);
        Check(data.terrainLayers.Length==5,"Expected original layers plus oxide sediment and pale shale.");
        Check(landscape.Find("Outlying Rock Fields").GetComponentsInChildren<MeshCollider>().Length>=240,"Missing outer-area solid dressing.");
        Check(landscape.GetComponentsInChildren<Light>().Length==0,"No extra foliage/structure point lights expected.");
        foreach(GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())foreach(Transform child in root.GetComponentsInChildren<Transform>(true))Check(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject)==0,"Missing script: "+child.name);
        CompareSavedState(environment,terrain);
        string report="480 x 480m; elevation "+min.ToString("F2")+".."+max.ToString("F2")+"m; maximum slope "+slope.ToString("F1")+" degrees; "+steep+" steep samples; six linked collidable landmarks; five terrain layers.";
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/TutorialTraversalValidation.txt",report);Debug.Log("TUTORIAL_TRAVERSAL_VALIDATED: "+report);
    }
    private static float Ground(Terrain terrain,Vector3 at)=>terrain.SampleHeight(at)+terrain.transform.position.y;
    private static void CompareSavedState(Transform environment,Terrain terrain)
    {
        const string folder="Logs/BeforeTraversalPass/",terrainPath="Assets/Editor/TraversalBaseline.asset",scenePath="Assets/Editor/TraversalBaseline.unity";
        Check(File.Exists(folder+"ImpactCrater.asset")&&File.Exists(folder+"Tutorial.unity"),"Missing latest-saved baseline.");
        Check(!File.Exists(terrainPath)&&!File.Exists(scenePath),"Refusing to overwrite test assets.");
        File.Copy(folder+"ImpactCrater.asset",terrainPath);File.Copy(folder+"Tutorial.unity",scenePath);
        AssetDatabase.ImportAsset(terrainPath,ImportAssetOptions.ForceSynchronousImport);AssetDatabase.ImportAsset(scenePath,ImportAssetOptions.ForceSynchronousImport);
        var current=UnityEngine.SceneManagement.SceneManager.GetActiveScene();var before=EditorSceneManager.OpenScene(scenePath,OpenSceneMode.Additive);
        try
        {
            TerrainData old=AssetDatabase.LoadAssetAtPath<TerrainData>(terrainPath);float error=0;
            for(int z=-36;z<=36;z+=3)for(int x=-36;x<=36;x+=3)
            {
                if(new Vector2(x,z).magnitude>38)continue;
                float h=old.GetInterpolatedHeight((x+160)/320f,(z+160)/320f)-2;
                Vector3 p=environment.position+new Vector3(x,0,z);error=Mathf.Max(error,Mathf.Abs(h-Ground(terrain,p)));
            }
            Check(error<0.04f,"Original crash floor changed: max delta "+error);
            foreach(string name in new[]{"FirstPersonPlayer","CapybaraAstronaut","BurntRockBasin","AlcoholFlask","GrayboxExitGate","Tutorial Gameplay","Tutorial HUD","Enhanced Crashed Rocket"})
            {
                Transform a=Find(before,name),b=Find(current,name);Vector3 delta=a.position-b.position;delta.y=0;
                Check(delta.magnitude<0.001f&&a.localScale==b.localScale&&Quaternion.Angle(a.rotation,b.rotation)<0.001f,"Saved core actor moved/rescaled: "+name);
            }
            foreach(string group in new[]{"Basalt Rocks","Ship Debris"})
            {
                Transform a=Find(before,group),b=Find(current,group);Check(a.childCount==b.childCount,"Original dressing removed.");
                for(int i=0;i<a.childCount;i++)Check(a.GetChild(i).name==b.GetChild(i).name&&a.GetChild(i).localScale==b.GetChild(i).localScale&&Quaternion.Angle(a.GetChild(i).rotation,b.GetChild(i).rotation)<0.001f,"Original dressing model changed.");
            }
            Debug.Log("TRAVERSAL_SAVED_STATE_PASS: crash floor max deviation "+error.ToString("F4")+"m; core actors/ship unchanged in XZ, rotation and scale; original dressing counts/scales/rotations retained.");
        }
        finally {EditorSceneManager.CloseScene(before,true);UnityEngine.SceneManagement.SceneManager.SetActiveScene(current);AssetDatabase.DeleteAsset(terrainPath);AssetDatabase.DeleteAsset(scenePath);}
    }
    private static Transform Find(UnityEngine.SceneManagement.Scene scene,string name)
    {
        foreach(GameObject root in scene.GetRootGameObjects())foreach(Transform child in root.GetComponentsInChildren<Transform>(true))if(child.name==name)return child;
        throw new InvalidOperationException("Missing saved actor "+name);
    }
    public static void TestRoutes(CharacterController cc,Terrain terrain,Transform environment,FirstPersonController controller)
    {
        Vector3 origin=environment.position;
        for(int r=0;r<TutorialTraversalBuilder.AccessRoutes.Length;r++)
        {
            Vector3[] route=TutorialTraversalBuilder.AccessRoutes[r];Vector3 start=origin+route[0];start.y=Ground(terrain,start)+0.1f;CrashSiteSmoke.Reset(cc,start);
            for(int i=1;i<route.Length;i++) {Vector3 at=origin+route[i];at.y=Ground(terrain,at);WalkTerrain(cc,at,terrain,Setting(controller,"walkSpeed"),Setting(controller,"gravity"));}
            Debug.Log("TRAVERSAL_ACCESS_ROUTE_PASS: "+r);
        }
        Transform landscape=environment.Find(TutorialTraversalBuilder.LandscapeName),bridge=landscape.Find("Broken Relay Causeway");
        float gravity=Setting(controller,"gravity"),jump=Setting(controller,"jumpHeight"),speed=Setting(controller,"sprintSpeed");
        NoJump(cc,bridge.Find("Jump 1 Launch").position,bridge.Find("Jump 1 Land").position,gravity,speed);
        Recover(cc,terrain,environment,TutorialTraversalBuilder.AccessRoutes[6],Setting(controller,"walkSpeed"),gravity);
        CrashSiteSmoke.Reset(cc,bridge.Find("Start").position+Vector3.up*0.1f);
        for(int i=1;i<=2;i++)
        {
            WalkSurface(cc,bridge.Find("Jump "+i+" Launch").position);
            Jump(cc,bridge.Find("Jump "+i+" Launch").position,bridge.Find("Jump "+i+" Land").position,bridge.position.y,gravity,jump,speed,0.6f);
        }
        WalkSurface(cc,bridge.Find("Finish").position);Debug.Log("BROKEN_CAUSEWAY_JUMPS_PASS");
        Transform fins=landscape.Find("Basalt Stepping Fins");
        Vector3 launch=fins.position+new Vector3(7.3f,0,0),first=fins.Find("Fin 1").position;
        NoJump(cc,launch,first,gravity,speed);
        Recover(cc,terrain,environment,TutorialTraversalBuilder.AccessRoutes[7],Setting(controller,"walkSpeed"),gravity);
        CrashSiteSmoke.Reset(cc,fins.Find("Start").position+Vector3.up*0.1f);
        for(int i=1;i<=5;i++)
        {
            Transform target=fins.Find("Fin "+i);
            launch=i==1?fins.position+new Vector3(7.3f,0,0):fins.Find("Fin "+(i-1)).position+Vector3.right*1.5f;
            WalkSurface(cc,launch);Jump(cc,launch,target.position,target.position.y,gravity,jump,speed,1.9f);
        }
        launch=fins.Find("Fin 5").position+Vector3.right*1.5f;WalkSurface(cc,launch);
        Jump(cc,launch,fins.position+new Vector3(43,2,0),fins.position.y+2,gravity,jump,speed,0.7f);
        WalkSurface(cc,fins.Find("Finish").position);Debug.Log("BASALT_FINS_SIX_JUMPS_PASS");
        Transform mast=landscape.Find("Fallen Relay Mast");CrashSiteSmoke.Reset(cc,mast.Find("Start").position+Vector3.up*0.1f);WalkSurface(cc,mast.Find("High Balcony").position);Debug.Log("FALLEN_MAST_CLIMB_PASS");
        Transform overlook=landscape.Find("Split Mesa Observatory");CrashSiteSmoke.Reset(cc,overlook.position+new Vector3(-9,0.1f,0));WalkSurface(cc,overlook.Find("Lookout").position);Debug.Log("MESA_OBSERVATORY_ACCESS_PASS");
        Debug.Log("TUTORIAL_TRAVERSAL_ROUTES_PASS: access/switchbacks/tunnel/recovery routes, eight required jumps, fallen-mast climb and summit lookout.");
    }
    private static void Recover(CharacterController cc,Terrain terrain,Transform environment,Vector3[] route,float speed,float gravity)
    {
        foreach(Vector3 waypoint in route){Vector3 target=environment.position+waypoint;target.y=Ground(terrain,target);WalkTerrain(cc,target,terrain,speed,gravity);}
        Debug.Log("TRAVERSAL_FALL_RECOVERY_PASS: actual fall to the canyon floor and walk-only escape, without a reset/teleport.");
    }
    private static float Setting(FirstPersonController controller,string name)=>(float)typeof(FirstPersonController).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(controller);
    private static void WalkTerrain(CharacterController cc,Vector3 target,Terrain terrain,float speed,float gravity)
    {
        Vector3 delta=target-cc.transform.position;delta.y=0;int budget=Mathf.CeilToInt(delta.magnitude/speed*60)*5+200;float vertical=-2;
        while(budget-->0)
        {
            delta=target-cc.transform.position;delta.y=0;if(delta.magnitude<0.13f)break;
            if(cc.isGrounded&&vertical<0)vertical=-2;vertical-=gravity/60;
            cc.Move(delta.normalized*Mathf.Min(speed/60,delta.magnitude)+Vector3.up*(vertical/60));
        }
        delta=target-cc.transform.position;delta.y=0;Vector3 p=cc.transform.position-terrain.transform.position;
        bool floor=Physics.Raycast(cc.transform.position+Vector3.up*0.1f,Vector3.down,out RaycastHit hit,0.4f,~0,QueryTriggerInteraction.Ignore)&&!(hit.collider is CharacterController);
        Check(delta.magnitude<0.25f&&floor&&cc.isGrounded,"Walk-only terrain/structure access blocked: "+cc.transform.position+" -> "+target+"; terrain slope="+terrain.terrainData.GetSteepness(p.x/480,p.z/480)+"; controller limit="+cc.slopeLimit);
    }
    private static void WalkSurface(CharacterController cc,Vector3 target)
    {
        int budget=4000;
        while(budget-->0)
        {
            Vector3 d=target-cc.transform.position;d.y=0;if(d.magnitude<0.13f)break;
            cc.Move(d.normalized*Mathf.Min(4f/60,d.magnitude)+Vector3.down*(2f+20f/60)/60);
        }
        Vector3 delta=target-cc.transform.position;delta.y=0;
        Check(delta.magnitude<0.25f&&Mathf.Abs(target.y-cc.transform.position.y)<0.3f&&cc.isGrounded,"Structure route blocked: "+cc.transform.position+" -> "+target);
    }
    private static void NoJump(CharacterController cc,Vector3 start,Vector3 end,float gravity,float speed)
    {
        CrashSiteSmoke.Reset(cc,start+Vector3.up*0.1f);float vertical=-2;bool fell=false;
        for(int frame=0;frame<70;frame++)
        {
            vertical-=gravity/60;cc.Move(new Vector3(speed/60,vertical/60,0));if(cc.transform.position.y<start.y-0.65f){fell=true;break;}
        }
        Check(fell,"The intended jump gap can be crossed without jumping: "+start+" -> "+end);
        for(int frame=0;frame<300&&!cc.isGrounded;frame++){vertical-=gravity/60;cc.Move(Vector3.up*(vertical/60));}
        Check(cc.isGrounded&&cc.transform.position.y<start.y-5,"Missed jump did not reach a recoverable canyon floor.");
        Debug.Log("REQUIRED_JUMP_CONFIRMED: walking/sprinting off "+start+" falls below the landing and reaches the canyon floor.");
    }
    private static void Jump(CharacterController cc,Vector3 launch,Vector3 target,float floor,float gravity,float jump,float speed,float halfLanding)
    {
        Check(cc.isGrounded,"Jump launch is not grounded: "+cc.transform.position);
        float vertical=Mathf.Sqrt(2*gravity*jump);bool airborne=false,landed=false;
        for(int frame=0;frame<100;frame++)
        {
            vertical-=gravity/60;float dx=Mathf.Clamp(target.x-cc.transform.position.x,0,speed/60);
            cc.Move(new Vector3(dx,vertical/60,0));if(!cc.isGrounded)airborne=true;
            if(frame>5&&airborne&&cc.isGrounded&&vertical<0){landed=true;break;}
            if(cc.transform.position.y<floor-1)break;
        }
        Check(landed&&Mathf.Abs(cc.transform.position.y-floor)<0.25f&&cc.transform.position.x>=target.x-halfLanding,"Jump failed: "+launch+" -> "+target+"; landed="+cc.transform.position);
    }
    public static void CaptureViews(Transform environment)
    {
        Vector3 c=environment.position;bool fog=RenderSettings.fog;RenderSettings.fog=false;
        Capture(c+new Vector3(-285,260,-315),c+new Vector3(0,12,0),"TraversalOverview",58);RenderSettings.fog=fog;
        Capture(c+new Vector3(63,28,-70),c+new Vector3(91,16,-40),"TraversalCauseway",66);
        Capture(c+new Vector3(73,18,-40),c+new Vector3(104,17,-40),"TraversalJumpApproach",75);
        Capture(c+new Vector3(94,35,-132),c+new Vector3(128,22,-106),"TraversalSteppingFins",65);
        Capture(c+new Vector3(-68,25,32),c+new Vector3(-95,28,65),"TraversalSpineArches",68);
        Capture(c+new Vector3(-132,63,80),c+new Vector3(-105,55,105),"TraversalMesaObservatory",66);
        Capture(c+new Vector3(-100,-4,-126),c+new Vector3(-100,-3,-100),"TraversalWreckTunnel",75);
        Capture(c+new Vector3(105,49,88),c+new Vector3(135,54,110),"TraversalFallenMast",70);
    }
    private static void Capture(Vector3 at,Vector3 target,string name,float fov)
    {
        var root=new GameObject("Temporary traversal camera");Camera camera=root.AddComponent<Camera>();camera.enabled=false;camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
        camera.transform.position=at;camera.transform.LookAt(target);camera.fieldOfView=fov;camera.nearClipPlane=0.1f;camera.farClipPlane=1000;camera.clearFlags=CameraClearFlags.Skybox;
        var output=new RenderTexture(1600,1000,24,RenderTextureFormat.ARGB32);output.Create();var request=new RenderPipeline.StandardRequest{destination=output};
        Check(RenderPipeline.SupportsRenderRequest(camera,request),"URP render request unavailable.");RenderPipeline.SubmitRenderRequest(camera,request);
        RenderTexture previous=RenderTexture.active;RenderTexture.active=output;var image=new Texture2D(1600,1000,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,1600,1000),0,0);image.Apply();Directory.CreateDirectory("Logs");File.WriteAllBytes("Logs/"+name+".png",image.EncodeToPNG());
        RenderTexture.active=previous;UnityEngine.Object.Destroy(image);output.Release();UnityEngine.Object.Destroy(output);UnityEngine.Object.Destroy(root);
    }
}
