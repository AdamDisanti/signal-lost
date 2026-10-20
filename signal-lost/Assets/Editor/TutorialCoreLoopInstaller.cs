using System;
using System.Collections.Generic;
using System.Linq;
using SignalLost.Combat;
using SignalLost.Inventory;
using SignalLost.NPC;
using SignalLost.Player;
using SignalLost.Scanner;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Applies the tutorial core-loop changes (meeting notes 2026-10-20) to the saved Tutorial.unity.
/// Every step checks whether it has already run, so running it again changes nothing. It only adds or
/// rewires tutorial objects; it never regenerates terrain or the lunar layout.
/// Menu: Signal Lost > Tutorial > Install Core Loop.
/// </summary>
public static class TutorialCoreLoopInstaller
{
    private const string ScenePath = "Assets/Scenes/Tutorial.unity";
    private const string ScannerRigPath = "Assets/Prefabs/Player/Scanner/ScannerRig.prefab";
    private static readonly Color ScannerTeal = new Color(0.42f, 0.9f, 0.86f);
    private static readonly List<string> Log = new List<string>();

    [MenuItem("Signal Lost/Tutorial/Install Core Loop")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Exit Play Mode before installing the tutorial core loop.");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Log.Clear();

        RemoveLegacyNpcWindows(scene);

        TutorialPlayer player = Require(scene, "TutorialPlayer");
        InstallPlayerGear(player);
        InstallScannerPickup(scene);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("TUTORIAL_CORE_LOOP_INSTALLED\n" + string.Join("\n", Log));
    }

    /// <summary>Batch-mode entry point: installs, then exits with 0 on success or 1 on an exception.</summary>
    public static void InstallBatch()
    {
        try
        {
            Install();
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    // ---------- Package 1: dialogue UI ----------

    // The code-built DialogueUi and GameplayOverlay replace the old TutorialNpcWindows prefab.
    private static void RemoveLegacyNpcWindows(Scene scene)
    {
        // Once the prefab asset is gone the instance is named "Tutorial NPC Windows (Missing Prefab ...)".
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (!root.name.StartsWith("Tutorial NPC Windows", StringComparison.Ordinal)) continue;
            UnityEngine.Object.DestroyImmediate(root);
            Log.Add("Removed the legacy Tutorial NPC Windows canvas.");
        }
    }

    // ---------- Package 2: player items ----------

    // Scanner on Q (swapping with the laser), the inventory that owns the three items, and the slower starting laser.
    private static void InstallPlayerGear(TutorialPlayer player)
    {
        ScannerToggle toggle = player.GetComponentInChildren<ScannerToggle>(true);
        if (toggle == null)
        {
            Camera view = player.GetComponentInChildren<Camera>(true);
            var rigAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ScannerRigPath);
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(rigAsset, view.transform);
            rig.transform.localPosition = Vector3.zero;
            rig.transform.localRotation = Quaternion.identity;
            toggle = rig.GetComponent<ScannerToggle>();
            Log.Add("Mounted the ScannerRig under the tutorial player's camera.");
        }
        SetReference(player, "scanner", toggle);

        if (player.GetComponent<PlayerInventory>() == null)
        {
            player.gameObject.AddComponent<PlayerInventory>();
            Log.Add("Added PlayerInventory to the tutorial player.");
        }

        // Meeting notes: the starting laser should feel weak; upgrades come later.
        var laser = new SerializedObject(player.GetComponent<MiningLaser>());
        laser.FindProperty("shotCooldown").floatValue = 10f;
        laser.FindProperty("comboCooldown").floatValue = 3f;
        laser.ApplyModifiedPropertiesWithoutUndo();
    }

    // The scanner lies in the open wreckage between the ship and Cavendish, glowing so it can be found.
    private static void InstallScannerPickup(Scene scene)
    {
        if (FindPickup(scene, TutorialPickup.Item.Scanner) != null) return;
        Transform cavendish = FindActor(scene, NpcActor.Role.Cavendish).transform;
        Transform ship = FindRoot(scene, "Tutorial Impact Site").transform.Find("Enhanced Crashed Rocket");
        Vector3 spot = OpenGroundBetween(ship.position, cavendish.position, 0.45f);

        var root = new GameObject("Scanner Pickup");
        root.transform.SetParent(FindRoot(scene, "Tutorial Gameplay").transform, true);
        root.transform.SetPositionAndRotation(spot, Quaternion.Euler(0f, 35f, 0f));

        var rigAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ScannerRigPath);
        GameObject model = UnityEngine.Object.Instantiate(rigAsset.transform.Find("ScannerVisualRoot/ScannerModel").gameObject, root.transform);
        model.name = "Scanner Model";
        model.transform.localPosition = new Vector3(0f, 0.35f, 0f);
        model.transform.localRotation = Quaternion.Euler(-20f, 0f, 0f);
        model.transform.localScale = Vector3.one * 0.6f;

        Bounds bounds = new Bounds(model.transform.position, Vector3.zero);
        foreach (Renderer part in model.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(part.bounds);
        var box = root.AddComponent<BoxCollider>();
        box.center = root.transform.InverseTransformPoint(bounds.center);
        box.size = Vector3.Max(bounds.size + Vector3.one * 0.15f, Vector3.one * 0.6f);

        var pickup = root.AddComponent<TutorialPickup>();
        var data = new SerializedObject(pickup);
        data.FindProperty("item").enumValueIndex = (int)TutorialPickup.Item.Scanner;
        data.ApplyModifiedPropertiesWithoutUndo();

        var glowObject = new GameObject("Glint");
        glowObject.transform.SetParent(root.transform, false);
        glowObject.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        var glow = glowObject.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = ScannerTeal;
        glow.range = 4f;
        glow.intensity = 3f;
        glow.shadows = LightShadows.None;

        var glint = root.AddComponent<PickupGlint>();
        var glintData = new SerializedObject(glint);
        glintData.FindProperty("glow").objectReferenceValue = glow;
        glintData.FindProperty("model").objectReferenceValue = model.transform;
        glintData.ApplyModifiedPropertiesWithoutUndo();
        Log.Add($"Placed the scanner pickup in the wreckage at {spot:F1}.");
    }

    // ---------- Helpers ----------

    private static TutorialPlayer Require(Scene scene, string what)
    {
        TutorialPlayer player = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TutorialPlayer>(true)).FirstOrDefault();
        if (player == null) throw new InvalidOperationException("Tutorial.unity has no " + what + ".");
        return player;
    }

    private static TutorialPickup FindPickup(Scene scene, TutorialPickup.Item kind) =>
        scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TutorialPickup>(true)).FirstOrDefault(p => p.Kind == kind);

    private static NpcActor FindActor(Scene scene, NpcActor.Role role) =>
        scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<NpcActor>(true))
            .First(a => (role == NpcActor.Role.Cavendish) == a.IsQuestGiver);

    private static void SetReference(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        var data = new SerializedObject(target);
        data.FindProperty(field).objectReferenceValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// A spot on bare terrain roughly <paramref name="fraction"/> of the way from <paramref name="from"/> to
    /// <paramref name="to"/>, clear of rocks and debris, found by sampling around that line.
    /// </summary>
    private static Vector3 OpenGroundBetween(Vector3 from, Vector3 to, float fraction)
    {
        Physics.SyncTransforms();
        Vector3 along = to - from;
        along.y = 0f;
        Vector3 side = Vector3.Cross(Vector3.up, along.normalized);
        var colliders = new Collider[16];
        for (int ring = 0; ring < 12; ring++)
        for (int k = -ring; k <= ring; k++)
        {
            Vector3 probe = from + along * Mathf.Clamp01(fraction + ring * 0.025f * Mathf.Sign(k + 0.5f)) + side * (k * 1.5f);
            if (!Physics.Raycast(probe + Vector3.up * 40f, Vector3.down, out RaycastHit hit, 80f, ~0, QueryTriggerInteraction.Ignore)) continue;
            if (!(hit.collider is TerrainCollider)) continue;
            int count = Physics.OverlapSphereNonAlloc(hit.point + Vector3.up * 0.8f, 1.2f, colliders, ~0, QueryTriggerInteraction.Ignore);
            bool clear = true;
            for (int i = 0; i < count; i++) clear &= colliders[i] is TerrainCollider;
            if (clear) return hit.point;
        }
        throw new InvalidOperationException("No open ground found for a tutorial pickup.");
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == name) return root;
        return null;
    }
}
