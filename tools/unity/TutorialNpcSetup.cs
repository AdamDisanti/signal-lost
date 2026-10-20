using System;
using System.IO;
using SignalLost.Combat;
using SignalLost.Enemies;
using SignalLost.NPC;
using SignalLost.Player;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

// Run only in a disposable copy. Generated Unity assets are copied back with their metadata after validation.
public static class TutorialNpcSetup
{
    public const string Root = "Assets/Prefabs/NPC";
    public const string MerchantPath = Root + "/MerchantCrab.prefab";
    private static Terrain terrain;
    private static Material copper, brass, teal, dark;

    public static void Create()
    {
        TutorialEnemiesSetup.RequireTemp();
        EditorSceneManager.OpenScene("Assets/Scenes/Tutorial.unity");
        if (UnityEngine.Object.FindFirstObjectByType<TutorialQuestFlow>() != null) throw new InvalidOperationException("NPC quest already exists.");
        Folder(Root); Folder(Root + "/Materials");
        copper = Mat("MerchantCopper", new Color(0.7f, 0.32f, 0.12f), 0.65f);
        brass = Mat("MerchantBrass", new Color(0.95f, 0.68f, 0.18f), 0.7f);
        teal = Mat("MerchantTeal", new Color(0.06f, 0.38f, 0.39f), 0.25f);
        dark = Mat("MerchantLeather", new Color(0.13f, 0.075f, 0.04f), 0);
        terrain = UnityEngine.Object.FindFirstObjectByType<Terrain>();
        var player = UnityEngine.Object.FindFirstObjectByType<TutorialPlayer>();
        var cavendish = GameObject.Find("CapybaraAstronaut");
        var looseFlask = GameObject.Find("AlcoholFlask");
        var basin = GameObject.Find("BurntRockBasin");
        if (terrain == null || player == null || cavendish == null || looseFlask == null) throw new InvalidOperationException("Tutorial dependencies missing.");
        var actors = new GameObject("Tutorial NPCs");
        var quest = actors.AddComponent<TutorialQuestFlow>();
        var cavActor = cavendish.AddComponent<NpcActor>();
        var cavSpeech = Speech(cavendish.transform, "Cavendish", new Vector3(0, 2.8f, 0));
        Set(cavActor, "quest", quest);
        var destination = new GameObject("Merchant Stand Beside Cavendish").transform;
        destination.SetParent(actors.transform);
        // Find clear ground near the quest giver before baking, with enough space for the larger crab.
        destination.position = Clear(cavendish.transform.position + Vector3.right * 3.5f, 0.9f);
        destination.rotation = cavendish.transform.rotation;

        var surface = GameObject.Find("Tutorial Encounter Navigation").GetComponent<NavMeshSurface>();
        // Cover pickup area, merchant's fight location, and the walk back to Cavendish.
        Vector3 midpoint = (cavendish.transform.position + basin.transform.position) * 0.5f;
        surface.transform.position = midpoint;
        surface.center = Vector3.zero;
        surface.size = new Vector3(150, 50, 150);
        surface.BuildNavMesh();
        var nav = surface.navMeshData;
        var existingNav = AssetDatabase.LoadAssetAtPath<NavMeshData>(TutorialEnemiesSetup.NavPath);
        EditorUtility.CopySerialized(nav, existingNav);
        surface.RemoveData();
        surface.navMeshData = existingNav;
        surface.AddData();
        if (!NavMesh.SamplePosition(destination.position, out var stand, 3, NavMesh.AllAreas)) throw new InvalidOperationException("Merchant stand is not navigable.");
        destination.position = stand.position;

        BuildMerchant();
        var merchantObject = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(MerchantPath), actors.transform);
        merchantObject.name = "Merchant Crab";
        Vector3 direction = Vector3.ProjectOnPlane(basin.transform.position - player.transform.position, Vector3.up).normalized;
        Vector3 fight = Clear(basin.transform.position + direction * 19 + Vector3.Cross(Vector3.up, direction) * 7, 1f);
        if (!NavMesh.SamplePosition(fight, out var spawn, 3, NavMesh.AllAreas)) throw new InvalidOperationException("Merchant spawn off navigation.");
        var route = new NavMeshPath();
        if (!NavMesh.CalculatePath(spawn.position, stand.position, NavMesh.AllAreas, route) || route.status != NavMeshPathStatus.PathComplete)
            throw new InvalidOperationException("Merchant cannot walk back to Cavendish.");
        merchantObject.transform.SetPositionAndRotation(spawn.position, Quaternion.LookRotation(Vector3.ProjectOnPlane(basin.transform.position - spawn.position, Vector3.up)));
        var merchant = merchantObject.GetComponent<MerchantCrab>();
        Set(merchant, "returnPoint", destination);
        Set(merchant, "flaskDrop", looseFlask.GetComponent<TutorialPickup>());
        Set(quest, "player", player); Set(quest, "cavendish", cavSpeech); Set(quest, "merchant", merchant);
        Set(player, "quest", quest);
        // Reuse the existing world flask as the one-time drop, with its original collider and metadata.
        looseFlask.SetActive(false);
        EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(player.gameObject.scene);
        TutorialNpcUiSetup.Apply();
        Debug.Log("TUTORIAL_NPC_SETUP_PASS merchant=" + spawn.position + " stand=" + stand.position + " cavendish=" + cavendish.transform.position);
    }

    private static void BuildMerchant()
    {
        var originalShell = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Shells/MetalBucket.prefab");
        var shell = (GameObject)PrefabUtility.InstantiatePrefab(originalShell);
        shell.name = "Merchant Copper Trading Shell";
        foreach (var renderer in shell.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = copper;
        // The silhouette carries matching side satchels, brass armour plates, antenna and a hanging trade sign.
        Piece("Port brass armour", PrimitiveType.Cube, shell.transform, new Vector3(-0.34f, 0.2f, -0.32f), new Vector3(0.055f, 0.45f, 0.5f), brass);
        Piece("Starboard brass armour", PrimitiveType.Cube, shell.transform, new Vector3(0.34f, 0.2f, -0.32f), new Vector3(0.055f, 0.45f, 0.5f), brass);
        Piece("Port trade satchel", PrimitiveType.Cube, shell.transform, new Vector3(-0.43f, 0.12f, -0.34f), new Vector3(0.2f, 0.32f, 0.35f), teal);
        Piece("Starboard trade satchel", PrimitiveType.Cube, shell.transform, new Vector3(0.43f, 0.12f, -0.34f), new Vector3(0.2f, 0.32f, 0.35f), teal);
        Piece("Antenna", PrimitiveType.Cylinder, shell.transform, new Vector3(-0.2f, 0.72f, -0.4f), new Vector3(0.025f, 0.3f, 0.025f), brass);
        Piece("Antenna light", PrimitiveType.Sphere, shell.transform, new Vector3(-0.2f, 1.02f, -0.4f), Vector3.one * 0.1f, teal);
        var shellAsset = PrefabUtility.SaveAsPrefabAsset(shell, Root + "/MerchantShell.prefab");
        UnityEngine.Object.DestroyImmediate(shell);
        var crab = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/HermitCrab.prefab"));
        crab.name = "MerchantCrab";
        crab.transform.localScale = Vector3.one * 1.6f;
        var brain = crab.GetComponent<HermitCrab>();
        brain.enabled = false;
        Set(brain, "attackRange", 2.1f); Set(brain, "attackReach", 2.5f);
        Set(crab.GetComponent<Health>(), "maxHealth", 500f);
        Set(crab.GetComponent<HermitCrabRig>(), "shellPrefab", shellAsset);
        // The carried reward is attached to the back independently of the hiding shell.
        var rig = new SerializedObject(crab.GetComponent<HermitCrabRig>());
        var visual = rig.FindProperty("visual").objectReferenceValue as Transform;
        var carried = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enviornment objects/GrayboxProps/AlcoholFlask.prefab"));
        carried.name = "Carried Healing Flask";
        carried.transform.SetParent(visual, false);
        foreach (Collider c in carried.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(c);
        Bounds b = carried.GetComponentInChildren<Renderer>().bounds;
        foreach (Renderer r in carried.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
        carried.transform.localScale *= 0.6f / Mathf.Max(0.01f, b.size.y / crab.transform.lossyScale.y);
        carried.transform.localPosition = new Vector3(0.36f, 0.8f, -0.25f);
        carried.transform.localRotation = Quaternion.Euler(0, 0, -20);
        Piece("Flask leather strap", PrimitiveType.Cube, visual, new Vector3(0.35f, 0.66f, -0.25f), new Vector3(0.22f, 0.1f, 0.2f), dark);
        var speech = Speech(crab.transform, "Merchant Crab", new Vector3(0, 1.9f, 0));
        var merchant = crab.AddComponent<MerchantCrab>();
        var actor = crab.AddComponent<NpcActor>(); Set(actor, "role", 1); Set(actor, "merchant", merchant);
        Set(merchant, "carriedFlask", carried); Set(merchant, "dialogue", speech);
        var shop = crab.AddComponent<MerchantShop>();
        Set(shop, "dialogue", speech); Set(merchant, "shop", shop);
        PrefabUtility.SaveAsPrefabAsset(crab, MerchantPath);
        UnityEngine.Object.DestroyImmediate(crab);
    }

    private static NpcDialogue Speech(Transform parent, string speaker, Vector3 localPosition)
    {
        var obj = new GameObject(speaker + " Dialogue");
        obj.transform.SetParent(parent, false); obj.transform.localPosition = localPosition;
        var text = obj.AddComponent<TextMeshPro>();
        text.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        text.fontSize = 2.1f; text.alignment = TextAlignmentOptions.Center;
        text.rectTransform.sizeDelta = new Vector2(5.8f, 2.5f);
        text.color = new Color(1, 0.92f, 0.7f);
        var speech = obj.AddComponent<NpcDialogue>();
        Set(speech, "text", text); Set(speech, "speaker", speaker);
        return speech;
    }

    private static void Piece(string name, PrimitiveType kind, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        var obj = GameObject.CreatePrimitive(kind); obj.name = name; obj.transform.SetParent(parent, false);
        obj.transform.localPosition = position; obj.transform.localScale = scale;
        obj.GetComponent<Renderer>().sharedMaterial = material;
        UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
    }
    private static Vector3 Clear(Vector3 desired, float radius)
    {
        Physics.SyncTransforms();
        for (int i = 0; i < 150; i++)
        {
            Vector3 p = desired + new Vector3(Mathf.Cos(i * 2.4f), 0, Mathf.Sin(i * 2.4f)) * Mathf.Sqrt(i) * 0.5f;
            p.y = terrain.SampleHeight(p) + terrain.transform.position.y;
            bool blocked = false;
            foreach (Collider c in Physics.OverlapCapsule(p + Vector3.up * (radius + 0.08f), p + Vector3.up * 2.5f, radius, ~0, QueryTriggerInteraction.Ignore))
                if (!(c is TerrainCollider)) blocked = true;
            if (!blocked) return p;
        }
        throw new InvalidOperationException("No clear placement near " + desired);
    }
    private static void Folder(string path)
    { if (AssetDatabase.IsValidFolder(path)) return; int n = path.LastIndexOf('/'); Folder(path.Substring(0, n)); AssetDatabase.CreateFolder(path.Substring(0, n), path.Substring(n + 1)); }
    private static Material Mat(string name, Color color, float metal)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
        m.SetColor("_BaseColor", color); m.SetFloat("_Metallic", metal); m.SetFloat("_Smoothness", 0.3f);
        AssetDatabase.CreateAsset(m, Root + "/Materials/" + name + ".mat"); return m;
    }
    private static void Set(UnityEngine.Object obj, string field, UnityEngine.Object value) { var s = new SerializedObject(obj); s.FindProperty(field).objectReferenceValue = value; s.ApplyModifiedPropertiesWithoutUndo(); }
    private static void Set(UnityEngine.Object obj, string field, float value) { var s = new SerializedObject(obj); s.FindProperty(field).floatValue = value; s.ApplyModifiedPropertiesWithoutUndo(); }
    private static void Set(UnityEngine.Object obj, string field, int value) { var s = new SerializedObject(obj); s.FindProperty(field).intValue = value; s.ApplyModifiedPropertiesWithoutUndo(); }
    private static void Set(UnityEngine.Object obj, string field, string value) { var s = new SerializedObject(obj); s.FindProperty(field).stringValue = value; s.ApplyModifiedPropertiesWithoutUndo(); }
}
