using System;
using System.Collections.Generic;
using SignalLost.Combat;
using SignalLost.Enemies;
using SignalLost.Enemies.Desert;
using SignalLost.Enemies.Meadow;
using SignalLost.Inventory;
using SignalLost.Player;
using SignalLost.Scanner;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using static SignalLost.Editor.EnemyTestBuilder;
using Object = UnityEngine.Object;

namespace SignalLost.Editor
{
    /// <summary>
    /// Generates the Chief Toad (the pond boss for the tutorial meadow) as a low-poly prefab, and a test arena for
    /// trying the fight before it goes on the tutorial map: a meadow bowl with its own pond, reeds, lily pads, boulders
    /// for cover from the scream and the beam, and trees, ringed by hills.
    ///
    /// The look follows the concept art: a big green toad with red spiral warpaint, a cream belly and tabard, a red
    /// cape and head cloth, a feathered crown with a glowing gem, gold-and-metal wrist cuffs, and the ship's cooling
    /// system (a pump on its back, segmented coolant tubes glowing blue over its shoulders, and a glowing core on its
    /// chest) strapped on.
    ///
    /// Files: Assets/Prefabs/Enemies/Meadow/ChiefToad.prefab (rebuilt in place, same GUID), its meshes in
    /// Prefabs/Enemies/Meadow/Meshes, materials in Materials/Enemies/Meadow, and Scenes/Bosses/ChiefToadTest.unity.
    /// Menu: Signal Lost > Chief Toad.
    /// </summary>
    internal static class ChiefToadBuilder
    {
        private const string Folder = "Assets/Prefabs/Enemies/Meadow";
        private const string MeshFolder = Folder + "/Meshes";
        private const string MaterialFolder = "Assets/Materials/Enemies/Meadow";
        public const string PrefabPath = Folder + "/ChiefToad.prefab";
        public const string ScenePath = "Assets/Scenes/Bosses/ChiefToadTest.unity";

        private static readonly Vector2 PondCentre = new Vector2(0f, 6f);
        private static readonly Vector2 PondRadii = new Vector2(13f, 9f);
        private const float WaterY = -0.15f;
        private const float ArenaSize = 90f;
        private static readonly Vector3 PlayerStart = new Vector3(0f, 0f, -32f);

        // ---------- Menus ----------

        [MenuItem("Signal Lost/Chief Toad/Build Prefab")]
        public static void BuildPrefabMenu()
        {
            BuildPrefab();
            AssetDatabase.SaveAssets();
            ValidatePrefab();
            Debug.Log("CHIEF_TOAD_PREFAB_BUILT");
        }

        [MenuItem("Signal Lost/Chief Toad/Create Test Arena")]
        public static void CreateTestSceneMenu()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null && !Application.isBatchMode &&
                !EditorUtility.DisplayDialog("Rebuild the Chief Toad test arena?", "This replaces " + ScenePath + ", including any edits made to it.", "Rebuild", "Cancel"))
                return;
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            CreateTestScene();
        }

        [MenuItem("Signal Lost/Chief Toad/Validate")]
        public static void Validate()
        {
            ValidatePrefab();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) return;
            EditorSceneManager.OpenScene(ScenePath);
            Require(Count<ChiefToad>() == 1, ScenePath + ": expected one Chief Toad.");
            Require(Count<FirstPersonController>() == 1 && Count<PlayerGear>() == 1, ScenePath + ": expected one shared Player.");
            Require(Count<SignalLost.Core.GameSystems>() == 1 && Count<PlayerSpawnPoint>() >= 1, ScenePath + ": expected GameSystems and a spawn point.");
            Require(Count<NavMeshSurface>() == 1 && Count<NavMeshBaker>() == 1, ScenePath + ": expected one NavMeshSurface with a NavMeshBaker.");
            Require(Count<Camera>() == 1, ScenePath + ": expected one camera.");
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                    Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject) == 0, ScenePath + ": missing script on " + item.name);
            Debug.Log("CHIEF_TOAD_VALIDATED");
        }

        /// <summary>Batch entry: builds the prefab and the test arena, validates, and exits.</summary>
        public static void BuildBatch()
        {
            try
            {
                BuildPrefab();
                AssetDatabase.SaveAssets();
                CreateTestScene();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void ValidatePrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Require(prefab != null, "Missing prefab " + PrefabPath);
            var brain = prefab.GetComponent<ChiefToad>();
            Require(brain != null && prefab.GetComponent<NavMeshAgent>() != null && prefab.GetComponent<Health>() != null,
                PrefabPath + " needs a ChiefToad, NavMeshAgent and Health.");
            var data = new SerializedObject(brain);
            foreach (string field in new[] { "visual", "body", "head", "jaw", "throat", "mouth", "crown", "cape", "frontLegs", "backLegs", "backKnees",
                         "coolantGlow", "coreHealth", "waterMaterial", "warningMaterial", "shriekMaterial", "shipPart" })
            {
                SerializedProperty property = data.FindProperty(field);
                Require(property != null, PrefabPath + ": unknown field " + field);
                if (property.isArray)
                {
                    Require(property.arraySize > 0, PrefabPath + ": " + field + " is empty.");
                    for (int i = 0; i < property.arraySize; i++)
                        Require(property.GetArrayElementAtIndex(i).objectReferenceValue != null, PrefabPath + ": " + field + "[" + i + "] is empty.");
                }
                else Require(property.objectReferenceValue != null, PrefabPath + ": " + field + " is not assigned.");
            }
        }

        // ---------- The toad ----------

        private static Mesh blob, gem, limb, spike, ring, band, tube;
        private static readonly List<Renderer> Glow = new List<Renderer>();

        private static Material Skin => Lit("ToadSkin", new Color(0.46f, 0.56f, 0.24f), 0f, 0.25f);
        private static Material SkinDark => Lit("ToadSkinDark", new Color(0.32f, 0.42f, 0.17f), 0f, 0.2f);
        private static Material Cream => Lit("ToadCream", new Color(0.88f, 0.8f, 0.6f), 0f, 0.25f);
        private static Material Cloth => Lit("ToadCloth", new Color(0.58f, 0.08f, 0.07f), 0f, 0.15f);
        private static Material ClothDark => Lit("ToadClothLining", new Color(0.33f, 0.04f, 0.04f), 0f, 0.1f);
        private static Material Paint => Lit("ToadWarpaint", new Color(0.7f, 0.12f, 0.09f), 0f, 0.3f);
        private static Material Gold => Lit("ToadGold", new Color(0.86f, 0.66f, 0.26f), 0.6f, 0.55f);
        private static Material Metal => Lit("ToadMetal", new Color(0.17f, 0.18f, 0.21f), 0.85f, 0.55f);
        private static Material Eye => Lit("ToadEye", new Color(0.95f, 0.55f, 0.1f), 0f, 0.9f);
        private static Material Pupil => Lit("ToadPupil", new Color(0.03f, 0.03f, 0.03f), 0f, 0.8f);
        private static Material Coolant => Lit("ToadCoolant", new Color(0.25f, 0.85f, 1f), 0f, 0.8f, new Color(0.3f, 1.2f, 2f));

        private static GameObject BuildPrefab()
        {
            EnsureFolders();
            blob = SaveMesh(LowPolyMesh.Icosphere("ToadBlob", 1));
            gem = SaveMesh(LowPolyMesh.Icosphere("ToadGem", 0));
            limb = SaveMesh(LowPolyMesh.TaperedPrism("ToadLimb", 6, 0.7f));
            spike = SaveMesh(LowPolyMesh.TaperedPrism("ToadFeather", 4, 0f));
            tube = SaveMesh(LowPolyMesh.TaperedPrism("ToadTube", 8, 1f));
            ring = SaveMesh(LowPolyMesh.Lathe("ToadRing", new[] { new Vector2(0.86f, -0.5f), new Vector2(1f, -0.5f), new Vector2(1f, 0.5f), new Vector2(0.86f, 0.5f), new Vector2(0.86f, -0.5f) }, 10));
            band = SaveMesh(LowPolyMesh.Lathe("ToadBand", new[] { new Vector2(0.9f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0.9f, 1f), new Vector2(0.9f, 0f) }, 12));
            Glow.Clear();

            var root = new GameObject("Chief Toad");
            Transform visual = Empty("Visual", root.transform, Vector3.zero);
            Transform body = Empty("Body", visual, new Vector3(0f, 1.2f, 0f));

            // Body: a broad, lumpy toad, warty along the back, with a cream belly.
            Part("Torso", body, blob, Skin, new Vector3(0f, 0f, -0.1f), Quaternion.identity, new Vector3(1.35f, 0.95f, 1.45f));
            Part("Back Hump", body, blob, SkinDark, new Vector3(0f, 0.35f, -0.35f), Quaternion.identity, new Vector3(1.15f, 0.7f, 1.1f));
            Part("Belly", body, blob, Cream, new Vector3(0f, -0.25f, 0.55f), Quaternion.identity, new Vector3(1.05f, 0.75f, 0.85f));
            Vector3[] warts =
            {
                new Vector3(-0.6f, 0.75f, -0.2f), new Vector3(0.55f, 0.8f, -0.45f), new Vector3(-0.3f, 0.95f, -0.75f), new Vector3(0.25f, 0.92f, 0.05f),
                new Vector3(-0.95f, 0.45f, -0.7f), new Vector3(0.95f, 0.5f, -0.15f), new Vector3(0f, 0.85f, -1.05f), new Vector3(0.7f, 0.65f, -0.95f),
            };
            for (int i = 0; i < warts.Length; i++)
                Part("Wart", body, blob, i % 2 == 0 ? Skin : SkinDark, warts[i], Quaternion.Euler(i * 40f, i * 70f, 0f), Vector3.one * (0.12f + 0.03f * (i % 3)));
            // Red spiral warpaint on the flanks and shoulders (concentric rings with a dot).
            Spiral(body, new Vector3(0f, 0f, -0.1f), new Vector3(1.35f, 0.95f, 1.45f), new Vector3(-1f, 0.2f, 0.25f), 0.32f);
            Spiral(body, new Vector3(0f, 0f, -0.1f), new Vector3(1.35f, 0.95f, 1.45f), new Vector3(1f, 0.2f, 0.25f), 0.32f);
            Spiral(body, new Vector3(0f, 0f, -0.1f), new Vector3(1.35f, 0.95f, 1.45f), new Vector3(-0.85f, 0.35f, -0.6f), 0.28f);
            Spiral(body, new Vector3(0f, 0f, -0.1f), new Vector3(1.35f, 0.95f, 1.45f), new Vector3(0.85f, 0.35f, -0.6f), 0.28f);

            // Head, with the jaw and throat sac that open and swell for the beam and the scream.
            Transform head = Empty("Head", body, new Vector3(0f, 0.45f, 1.05f));
            Part("Skull", head, blob, Skin, new Vector3(0f, 0.12f, 0.25f), Quaternion.identity, new Vector3(1.15f, 0.62f, 0.95f));
            Part("Snout", head, blob, Skin, new Vector3(0f, 0.05f, 0.8f), Quaternion.identity, new Vector3(0.95f, 0.42f, 0.42f));
            for (int side = -1; side <= 1; side += 2)
            {
                Part("Brow", head, blob, SkinDark, new Vector3(0.55f * side, 0.5f, 0.55f), Quaternion.Euler(0f, 0f, -12f * side), new Vector3(0.34f, 0.2f, 0.3f));
                Transform eye = Part("Eye", head, blob, Eye, new Vector3(0.6f * side, 0.5f, 0.72f), Quaternion.Euler(0f, 18f * side, 0f), Vector3.one * 0.2f);
                Part("Pupil", eye, blob, Pupil, new Vector3(0f, 0f, 0.82f), Quaternion.identity, new Vector3(0.7f, 0.22f, 0.3f));
                // A stern brow ridge painted red, and stripes down the cheeks.
                Part("Brow Paint", head, blob, Paint, new Vector3(0.52f * side, 0.66f, 0.62f), Quaternion.Euler(0f, 0f, 22f * side), new Vector3(0.28f, 0.035f, 0.08f));
                Part("Cheek Paint", head, blob, Paint, new Vector3(0.78f * side, 0.12f, 0.6f), Quaternion.Euler(0f, 30f * side, -35f * side), new Vector3(0.04f, 0.2f, 0.06f));
                Part("Cheek Paint", head, blob, Paint, new Vector3(0.74f * side, -0.02f, 0.75f), Quaternion.Euler(0f, 30f * side, -35f * side), new Vector3(0.04f, 0.16f, 0.05f));
            }
            Part("Scar", head, blob, Cream, new Vector3(0.38f, 0.55f, 0.78f), Quaternion.Euler(0f, 10f, 48f), new Vector3(0.02f, 0.26f, 0.02f));
            Transform jaw = Empty("Jaw", head, new Vector3(0f, -0.05f, 0.1f));
            Part("Lower Jaw", jaw, blob, Cream, new Vector3(0f, -0.12f, 0.42f), Quaternion.identity, new Vector3(1.05f, 0.28f, 0.85f));
            Part("Lip", jaw, blob, SkinDark, new Vector3(0f, -0.02f, 0.85f), Quaternion.identity, new Vector3(0.85f, 0.06f, 0.35f));
            Transform throat = Empty("Throat", jaw, new Vector3(0f, -0.3f, 0.25f));
            Part("Throat Sac", throat, blob, Cream, Vector3.zero, Quaternion.identity, new Vector3(0.68f, 0.38f, 0.58f));
            Transform mouth = Empty("Mouth", head, new Vector3(0f, 0f, 1.15f));
            var headHitbox = head.gameObject.AddComponent<SphereCollider>(); // the body capsule stops short of the head
            headHitbox.center = new Vector3(0f, 0.15f, 0.45f);
            headHitbox.radius = 0.6f;

            // Head cloth with two tails, and the feathered crown with its glowing gem.
            Part("Head Cloth", head, blob, Cloth, new Vector3(0f, 0.45f, 0.05f), Quaternion.identity, new Vector3(1.1f, 0.35f, 0.8f));
            for (int side = -1; side <= 1; side += 2)
                Segment("Cloth Tail", head, limb, Cloth, new Vector3(0.6f * side, 0.35f, -0.3f), new Vector3(0.85f * side, -0.55f, -0.55f), 0.13f);
            Transform crown = Empty("Crown", head, new Vector3(0f, 0.62f, 0.15f));
            Part("Crown Band", crown, band, Gold, Vector3.zero, Quaternion.identity, new Vector3(0.58f, 0.2f, 0.52f));
            for (int i = 0; i < 6; i++)
            {
                float a = (i - 2.5f) * 30f;
                Part("Band Diamond", crown, gem, Cloth, Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0.1f, 0.58f), Quaternion.Euler(0f, a, 45f), new Vector3(0.06f, 0.06f, 0.03f));
            }
            for (int i = 0; i < 11; i++)
            {
                // Tall feathers fanning round the back and sides, alternating red and cream.
                float a = Mathf.Lerp(-120f, 120f, i / 10f) + 180f;
                Quaternion around = Quaternion.Euler(0f, a, 0f);
                float length = 0.75f + 0.3f * Mathf.Cos((i - 5) * 0.3f);
                Part("Feather", crown, spike, i % 2 == 0 ? Cloth : Cream, around * new Vector3(0f, 0.12f, 0.5f),
                    around * Quaternion.Euler(-70f, 0f, 0f), new Vector3(0.11f, 0.05f, length));
            }
            Part("Crest Plate", crown, gem, Gold, new Vector3(0f, 0.38f, 0.56f), Quaternion.identity, new Vector3(0.18f, 0.4f, 0.07f));
            Glow.Add(Part("Crest Gem", crown, gem, Coolant, new Vector3(0f, 0.4f, 0.62f), Quaternion.identity, new Vector3(0.08f, 0.15f, 0.04f)).GetComponent<Renderer>());

            // The red cape, three hanging panels pinned at the shoulders.
            Transform capeRoot = Empty("Cape", body, new Vector3(0f, 0.75f, -0.45f));
            var cape = new Transform[3];
            for (int i = 0; i < 3; i++)
            {
                float side = i - 1;
                Transform panel = Empty(i == 0 ? "Cape L" : i == 1 ? "Cape M" : "Cape R", capeRoot, new Vector3(0.75f * side, 0f, -0.1f));
                panel.localRotation = Quaternion.Euler(28f, 35f * side, 0f);
                Part("Cloth", panel, blob, Cloth, new Vector3(0f, -0.7f, -0.12f), Quaternion.identity, new Vector3(0.62f, 0.85f, 0.07f));
                Part("Lining", panel, blob, ClothDark, new Vector3(0f, -0.68f, -0.05f), Quaternion.identity, new Vector3(0.58f, 0.8f, 0.05f));
                cape[i] = panel;
            }

            // The ship's cooling system: a pump on its back, coolant tubes over the shoulders and round the hips with
            // glowing segments, and a glowing core hung on its chest.
            Transform pump = Empty("Cooling System", body, new Vector3(0f, 1.0f, -0.45f));
            Part("Pump Housing", pump, tube, Metal, new Vector3(-0.48f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f), new Vector3(0.3f, 0.3f, 0.96f));
            for (int i = 0; i < 3; i++)
                Glow.Add(Part("Pump Window", pump, ring, Coolant, new Vector3(-0.3f + i * 0.3f, 0f, 0f), Quaternion.Euler(0f, 0f, 90f), new Vector3(0.33f, 0.08f, 0.33f)).GetComponent<Renderer>());
            Part("Pump Cap", pump, blob, Gold, new Vector3(-0.5f, 0f, 0f), Quaternion.identity, new Vector3(0.08f, 0.26f, 0.26f));
            Part("Pump Cap", pump, blob, Gold, new Vector3(0.5f, 0f, 0f), Quaternion.identity, new Vector3(0.08f, 0.26f, 0.26f));
            Part("Fan", pump, ring, Metal, new Vector3(0f, 0.05f, -0.3f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.18f, 0.04f, 0.18f));
            for (int side = -1; side <= 1; side += 2)
            {
                // Over the shoulder and down to the chest core.
                CoolantTube(body, side < 0 ? "ToadCoolantShoulderL" : "ToadCoolantShoulderR", new[]
                {
                    new Vector3(0.5f * side, 1.0f, -0.45f), new Vector3(0.95f * side, 0.95f, -0.1f), new Vector3(1.2f * side, 0.6f, 0.35f),
                    new Vector3(1.0f * side, 0.25f, 0.85f), new Vector3(0.5f * side, 0.05f, 1.2f), new Vector3(0.12f * side, 0f, 1.32f),
                });
                // Down the flank to the hip.
                CoolantTube(body, side < 0 ? "ToadCoolantHipL" : "ToadCoolantHipR", new[]
                {
                    new Vector3(0.5f * side, 1.0f, -0.45f), new Vector3(0.95f * side, 0.7f, -0.85f), new Vector3(1.3f * side, 0.15f, -0.75f),
                    new Vector3(1.35f * side, -0.3f, -0.35f),
                });
            }
            Transform core = Empty("Coolant Core", body, new Vector3(0f, -0.02f, 1.36f));
            Part("Core Frame", core, blob, Metal, Vector3.zero, Quaternion.identity, new Vector3(0.27f, 0.27f, 0.1f));
            Glow.Add(Part("Core Light", core, blob, Coolant, new Vector3(0f, 0f, 0.06f), Quaternion.identity, new Vector3(0.18f, 0.18f, 0.06f)).GetComponent<Renderer>());
            Part("Core Bolt", core, gem, Gold, new Vector3(0f, 0.25f, 0.04f), Quaternion.identity, Vector3.one * 0.05f);
            var coreBox = new GameObject("Chief Toad (coolant core)");
            coreBox.transform.SetParent(core, false);
            var coreCollider = coreBox.AddComponent<BoxCollider>();
            coreCollider.center = new Vector3(0f, 0f, 0.15f); // sticks out past the body capsule, so shots reach it
            coreCollider.size = new Vector3(0.65f, 0.65f, 0.4f);
            Health coreHealth = HitboxHealth(coreBox);
            // The tabard hanging below the core: cream with red bands and gold studs, with red cloth strips at its sides.
            Transform tabard = Empty("Tabard", body, new Vector3(0f, -0.25f, 1.32f));
            tabard.localRotation = Quaternion.Euler(-12f, 0f, 0f);
            Part("Tabard", tabard, blob, Cream, new Vector3(0f, -0.45f, 0f), Quaternion.identity, new Vector3(0.3f, 0.5f, 0.04f));
            Part("Tabard Band", tabard, blob, Paint, new Vector3(0f, -0.3f, 0.03f), Quaternion.identity, new Vector3(0.28f, 0.05f, 0.02f));
            Part("Tabard Band", tabard, blob, Paint, new Vector3(0f, -0.62f, 0.03f), Quaternion.identity, new Vector3(0.24f, 0.05f, 0.02f));
            Part("Tabard Diamond", tabard, gem, Paint, new Vector3(0f, -0.46f, 0.04f), Quaternion.Euler(0f, 0f, 45f), new Vector3(0.11f, 0.11f, 0.02f));
            for (int side = -1; side <= 1; side += 2)
            {
                Part("Stud", tabard, gem, Gold, new Vector3(0.2f * side, -0.12f, 0.04f), Quaternion.identity, Vector3.one * 0.05f);
                Part("Side Strip", tabard, blob, Cloth, new Vector3(0.38f * side, -0.35f, -0.02f), Quaternion.Euler(0f, 0f, 6f * side), new Vector3(0.08f, 0.42f, 0.03f));
            }

            // Front legs: shoulder, elbow, a broad hand of three fingers, and a metal cuff with a glowing band.
            var frontLegs = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                float s = i == 0 ? -1f : 1f;
                Transform shoulder = Empty(i == 0 ? "Front Leg L" : "Front Leg R", body, new Vector3(0.95f * s, -0.25f, 0.7f));
                Vector3 elbow = new Vector3(0.22f * s, -0.42f, 0.15f), wrist = new Vector3(0.32f * s, -0.82f, 0.42f);
                Part("Upper Arm", shoulder, blob, Skin, elbow * 0.5f, Quaternion.LookRotation(elbow), new Vector3(0.27f, 0.27f, 0.34f));
                Part("Elbow", shoulder, blob, Skin, elbow, Quaternion.identity, Vector3.one * 0.2f);
                Segment("Forearm", shoulder, limb, Skin, elbow, wrist, 0.2f);
                Part("Palm", shoulder, blob, Skin, wrist + new Vector3(0f, -0.04f, 0.12f), Quaternion.identity, new Vector3(0.24f, 0.08f, 0.24f));
                for (int f = 0; f < 3; f++)
                {
                    Vector3 from = wrist + new Vector3((f - 1) * 0.12f, -0.06f, 0.2f);
                    Segment("Finger", shoulder, limb, Skin, from, from + new Vector3((f - 1) * 0.1f, -0.03f, 0.3f), 0.065f);
                }
                Vector3 cuffAt = Vector3.Lerp(elbow, wrist, 0.7f);
                Quaternion along = Quaternion.FromToRotation(Vector3.up, wrist - elbow);
                Part("Cuff", shoulder, band, Metal, cuffAt - (wrist - elbow).normalized * 0.09f, along, new Vector3(0.24f, 0.18f, 0.24f));
                Glow.Add(Part("Cuff Light", shoulder, band, Coolant, cuffAt - (wrist - elbow).normalized * 0.03f, along, new Vector3(0.245f, 0.06f, 0.245f)).GetComponent<Renderer>());
                Spiral(shoulder, elbow * 0.5f, new Vector3(0.27f, 0.27f, 0.34f), new Vector3(s, 0.2f, 0.2f), 0.16f);
                frontLegs[i] = shoulder;
            }

            // Back legs: big folded thighs, shins folding back, long webbed feet.
            var backLegs = new Transform[2];
            var backKnees = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                float s = i == 0 ? -1f : 1f;
                Transform hip = Empty(i == 0 ? "Back Leg L" : "Back Leg R", body, new Vector3(1.05f * s, -0.45f, -0.55f));
                Part("Thigh", hip, blob, Skin, new Vector3(0.22f * s, 0.05f, 0.1f), Quaternion.Euler(-15f, 10f * s, 0f), new Vector3(0.5f, 0.45f, 0.75f));
                Spiral(hip, new Vector3(0.22f * s, 0.05f, 0.1f), new Vector3(0.5f, 0.45f, 0.75f), new Vector3(s, 0.3f, 0f), 0.24f);
                Transform knee = Empty("Knee", hip, new Vector3(0.42f * s, -0.05f, 0.6f));
                Vector3 ankle = new Vector3(0.08f * s, -0.55f, -0.65f);
                Segment("Shin", knee, limb, Skin, Vector3.zero, ankle, 0.2f);
                Segment("Foot", knee, limb, Skin, ankle, ankle + new Vector3(0.08f * s, -0.12f, 0.75f), 0.15f);
                for (int t = 0; t < 4; t++)
                {
                    Vector3 from = ankle + new Vector3(0.08f * s + (t - 1.5f) * 0.09f, -0.13f, 0.7f);
                    Segment("Toe", knee, limb, Skin, from, from + new Vector3((t - 1.5f) * 0.12f, -0.02f, 0.28f), 0.05f);
                }
                backLegs[i] = hip;
                backKnees[i] = knee;
            }

            var bodyCollider = root.AddComponent<CapsuleCollider>();
            bodyCollider.direction = 2;
            bodyCollider.center = new Vector3(0f, 1.2f, -0.15f);
            bodyCollider.radius = 1.2f;
            bodyCollider.height = 3f;

            var brain = root.AddComponent<ChiefToad>();
            var agent = root.GetComponent<NavMeshAgent>();
            agent.enabled = false; // switched on in Start, once the NavMesh has been built
            agent.radius = 1.3f;
            agent.height = 2.6f;
            agent.speed = 3.6f;
            agent.acceleration = 20f;
            agent.angularSpeed = 300f;
            agent.stoppingDistance = 0.5f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
            var health = new SerializedObject(root.GetComponent<Health>());
            health.FindProperty("maxHealth").floatValue = 750f;
            health.ApplyModifiedPropertiesWithoutUndo();
            root.AddComponent<EnemySignalSource>(); // the scanner can find it

            var data = new SerializedObject(brain);
            data.FindProperty("visual").objectReferenceValue = visual;
            data.FindProperty("displayName").stringValue = "Chief Toad";
            data.FindProperty("partName").stringValue = "Cooling system";
            data.FindProperty("arenaRadius").floatValue = 26f;
            data.FindProperty("phaseTwoAt").floatValue = 0.66f;
            data.FindProperty("patrolRadius").floatValue = 6f;
            data.FindProperty("patrolSpeed").floatValue = 1.6f;
            data.FindProperty("fodderWhileEngaged").boolValue = false;
            data.FindProperty("hitKnockback").floatValue = 0f;
            data.FindProperty("corpseTime").floatValue = 6f;
            data.FindProperty("flashTime").floatValue = 0.05f;
            data.FindProperty("shipPart").objectReferenceValue = pump;
            data.FindProperty("body").objectReferenceValue = body;
            data.FindProperty("head").objectReferenceValue = head;
            data.FindProperty("jaw").objectReferenceValue = jaw;
            data.FindProperty("throat").objectReferenceValue = throat;
            data.FindProperty("mouth").objectReferenceValue = mouth;
            data.FindProperty("crown").objectReferenceValue = crown;
            SetArray(data.FindProperty("cape"), cape);
            SetArray(data.FindProperty("frontLegs"), frontLegs);
            SetArray(data.FindProperty("backLegs"), backLegs);
            SetArray(data.FindProperty("backKnees"), backKnees);
            SerializedProperty glow = data.FindProperty("coolantGlow");
            glow.arraySize = Glow.Count;
            for (int i = 0; i < Glow.Count; i++) glow.GetArrayElementAtIndex(i).objectReferenceValue = Glow[i];
            data.FindProperty("coreHealth").objectReferenceValue = coreHealth;
            data.FindProperty("waterMaterial").objectReferenceValue = WaterMaterial();
            data.FindProperty("warningMaterial").objectReferenceValue = Unlit("ToadWarning", new Color(0.35f, 0.85f, 1f));
            data.FindProperty("shriekMaterial").objectReferenceValue = Unlit("ToadShriek", new Color(0.85f, 0.95f, 1f));
            data.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            if (prefab == null) throw new InvalidOperationException("Could not save prefab " + PrefabPath);
            return prefab;
        }

        // A coolant tube along a path, with glowing segments and dark clamps along it.
        private static void CoolantTube(Transform parent, string meshName, Vector3[] path)
        {
            Mesh pipe = SaveMesh(LowPolyMesh.Tube(meshName, path, 0.07f, 6));
            Part("Coolant Tube", parent, pipe, Metal, Vector3.zero, Quaternion.identity, Vector3.one);
            float total = 0f;
            for (int i = 1; i < path.Length; i++) total += (path[i] - path[i - 1]).magnitude;
            int segments = Mathf.Max(2, Mathf.RoundToInt(total / 0.3f));
            for (int n = 1; n < segments; n++)
            {
                float target = total * n / segments, walked = 0f;
                for (int i = 1; i < path.Length; i++)
                {
                    float length = (path[i] - path[i - 1]).magnitude;
                    if (walked + length < target) { walked += length; continue; }
                    Vector3 at = Vector3.Lerp(path[i - 1], path[i], (target - walked) / length);
                    Quaternion along = Quaternion.FromToRotation(Vector3.up, path[i] - path[i - 1]);
                    if (n % 2 == 1)
                        Glow.Add(Part("Coolant Segment", parent, band, Coolant, at - (path[i] - path[i - 1]).normalized * 0.07f, along, new Vector3(0.105f, 0.14f, 0.105f)).GetComponent<Renderer>());
                    else
                        Part("Clamp", parent, band, Gold, at - (path[i] - path[i - 1]).normalized * 0.03f, along, new Vector3(0.11f, 0.06f, 0.11f));
                    break;
                }
            }
        }

        // A red spiral painted on an ellipsoid (centre, radii) facing a direction: two rings round a dot.
        private static void Spiral(Transform parent, Vector3 centre, Vector3 radii, Vector3 direction, float size)
        {
            Vector3 d = direction.normalized;
            float scale = 1f / Mathf.Sqrt(Sq(d.x / radii.x) + Sq(d.y / radii.y) + Sq(d.z / radii.z));
            Vector3 point = centre + d * scale;
            Vector3 normal = new Vector3(d.x / Sq(radii.x), d.y / Sq(radii.y), d.z / Sq(radii.z)).normalized;
            Quaternion facing = Quaternion.FromToRotation(Vector3.up, normal);
            Part("Spiral", parent, ring, Paint, point + normal * 0.01f, facing, new Vector3(size, 0.02f, size));
            Part("Spiral", parent, ring, Paint, point + normal * 0.012f, facing, new Vector3(size * 0.6f, 0.02f, size * 0.6f));
            Part("Spiral", parent, gem, Paint, point + normal * 0.005f, facing, new Vector3(size * 0.18f, 0.02f, size * 0.18f));
        }

        private static float Sq(float value) => value * value;

        private static Health HitboxHealth(GameObject holder)
        {
            var health = holder.AddComponent<Health>();
            var data = new SerializedObject(health);
            // Never runs out (restored after every hit). Large because Damaged reports damage capped at current HP.
            data.FindProperty("maxHealth").floatValue = 100000f;
            data.ApplyModifiedPropertiesWithoutUndo();
            return health;
        }

        // ---------- Materials and meshes ----------

        private static void EnsureFolders()
        {
            foreach (string folder in new[] { "Assets/Prefabs", "Assets/Prefabs/Enemies", Folder, MeshFolder, "Assets/Materials", "Assets/Materials/Enemies", MaterialFolder, "Assets/Scenes/Bosses" })
            {
                if (AssetDatabase.IsValidFolder(folder)) continue;
                int slash = folder.LastIndexOf('/');
                AssetDatabase.CreateFolder(folder.Substring(0, slash), folder.Substring(slash + 1));
            }
        }

        /// <summary>Saves a mesh asset, replacing an existing one's contents so references and GUIDs survive.</summary>
        private static Mesh SaveMesh(Mesh mesh)
        {
            string path = MeshFolder + "/" + mesh.name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            EditorUtility.CopySerialized(mesh, existing);
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        /// <summary>A URP Lit material in the meadow enemy folder, created or updated to these values.</summary>
        private static Material Lit(string name, Color color, float metallic, float smoothness, Color? emission = null)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            if (emission.HasValue)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; // with None, URP's material check drops _EMISSION on import
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material Unlit(string name, Color color)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        // The water blades' and splash waves' look: clear, glossy, faintly glowing blue, seen from both sides.
        private static Material WaterMaterial()
        {
            Material material = Lit("ToadWater", new Color(0.55f, 0.83f, 1f, 0.6f), 0f, 0.95f, new Color(0.08f, 0.25f, 0.4f));
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetShaderPassEnabled("DepthOnly", false);
            material.SetShaderPassEnabled("ShadowCaster", false);
            EditorUtility.SetDirty(material);
            return material;
        }

        // ---------- Test arena ----------

        private enum Ground { Grass, Mud, Rock }

        private static void CreateTestScene()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null) BuildPrefab();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(MeadowAssetBuilder.PrefabPath("ReedsA")) == null) MeadowAssetBuilder.Build();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(CorePrefabBuilder.PlayerPath) == null) CorePrefabBuilder.Build();
            EnsureFolders();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var random = new System.Random(1023);

            var arena = new GameObject("Chief Toad Arena");
            var surface = arena.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            arena.AddComponent<NavMeshBaker>();
            BuildGround(arena.transform);
            BuildPond(arena.transform, random);
            BuildCover(arena.transform, random);

            var toad = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
            toad.name = "Chief Toad";
            toad.transform.SetPositionAndRotation(Ground3(PondCentre), Quaternion.Euler(0f, 180f, 0f));

            GameObject player = CoreSceneSetup.UpgradeToCorePlayer(scene, new[] { PlayerItem.MiningLaser, PlayerItem.Scanner, PlayerItem.Flask }, null);
            Vector3 start = Ground3(new Vector2(PlayerStart.x, PlayerStart.z)) + Vector3.up * 0.05f;
            player.transform.SetPositionAndRotation(start, Quaternion.identity);
            foreach (PlayerSpawnPoint spawn in All<PlayerSpawnPoint>()) spawn.transform.SetPositionAndRotation(start, Quaternion.identity);
            Camera view = player.GetComponentInChildren<Camera>(true);
            if (view != null) view.clearFlags = CameraClearFlags.Skybox;
            new GameObject("Mini-Boss HUD").AddComponent<MiniBossHud>();
            BuildLighting();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("CHIEF_TOAD_TEST_ARENA_CREATED_AND_VALIDATED");
        }

        // Height of the arena ground: gentle meadow, the pond's basin, and a ring of hills walling it in.
        private static float Height(float x, float z)
        {
            float h = 0.25f * (Mathf.PerlinNoise(x * 0.06f + 40f, z * 0.06f + 11f) - 0.5f) * 2f;
            float r = new Vector2(x, z).magnitude;
            float rise = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(34f, 44f, r));
            h += rise * (6f + 2f * Mathf.PerlinNoise(x * 0.1f, z * 0.1f));
            float e = PondDistance(x, z);
            if (e <= 1f) return WaterY - 0.25f - 0.9f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f, 0.35f, e));
            if (e < 1.4f) return Mathf.Lerp(WaterY + 0.2f, Mathf.Max(h, WaterY + 0.2f), Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f, 1.4f, e)));
            return Mathf.Max(h, e < 1.8f ? WaterY + 0.2f : h);
        }

        private static float PondDistance(float x, float z)
        {
            var local = new Vector2((x - PondCentre.x) / PondRadii.x, (z - PondCentre.y) / PondRadii.y);
            return local.magnitude * (1f + 0.06f * (Mathf.PerlinNoise(x * 0.15f + 3f, z * 0.15f + 7f) - 0.5f) * 2f);
        }

        private static Vector3 Ground3(Vector2 p) => new Vector3(p.x, Height(p.x, p.y), p.y);

        private static void BuildGround(Transform arena)
        {
            const int cells = 60;
            float step = ArenaSize / cells, origin = -ArenaSize * 0.5f;
            var heights = new float[cells + 1, cells + 1];
            for (int i = 0; i <= cells; i++)
            for (int j = 0; j <= cells; j++)
                heights[i, j] = Height(origin + i * step, origin + j * step);
            var kit = new FlatMeshKit<Ground>();
            for (int i = 0; i < cells; i++)
            for (int j = 0; j < cells; j++)
            {
                Vector3 a = new Vector3(origin + i * step, heights[i, j], origin + j * step);
                Vector3 b = new Vector3(origin + (i + 1) * step, heights[i + 1, j], origin + j * step);
                Vector3 c = new Vector3(origin + (i + 1) * step, heights[i + 1, j + 1], origin + (j + 1) * step);
                Vector3 d = new Vector3(origin + i * step, heights[i, j + 1], origin + (j + 1) * step);
                bool flip = (i + j) % 2 == 0;
                Facet(kit, a, flip ? b : d, c);
                Facet(kit, a, c, flip ? d : b);
            }
            Mesh mesh = kit.Build("ChiefToadArenaGround");
            string order = FlatMeshKit<Ground>.Order(mesh);
            string path = MeshFolder + "/ChiefToadArenaGround.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) AssetDatabase.CreateAsset(mesh, path);
            else
            {
                EditorUtility.CopySerialized(mesh, existing);
                existing.name = "ChiefToadArenaGround";
                FlatMeshKit<Ground>.Remember(existing, order);
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(mesh);
                mesh = existing;
            }

            var ground = new GameObject("Ground");
            ground.transform.SetParent(arena, false);
            ground.AddComponent<MeshFilter>().sharedMesh = mesh;
            var materials = new Material[mesh.subMeshCount];
            for (int s = 0; s < materials.Length; s++)
            {
                Ground kind = FlatMeshKit<Ground>.MaterialOf(mesh, s);
                materials[s] = kind == Ground.Grass ? Lit("ArenaGrass", new Color(0.44f, 0.63f, 0.24f), 0f, 0.08f)
                    : kind == Ground.Mud ? Lit("ArenaMud", new Color(0.45f, 0.36f, 0.24f), 0f, 0.15f)
                    : Lit("ArenaRock", new Color(0.55f, 0.52f, 0.48f), 0f, 0.1f);
            }
            ground.AddComponent<MeshRenderer>().sharedMaterials = materials;
            ground.AddComponent<MeshCollider>().sharedMesh = mesh;
            ground.isStatic = true;
        }

        private static void Facet(FlatMeshKit<Ground> kit, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            if (normal.y < 0f) normal = -normal;
            Vector3 centre = (a + b + c) / 3f;
            Ground kind = normal.y < 0.75f ? Ground.Rock : PondDistance(centre.x, centre.z) < 1.12f ? Ground.Mud : Ground.Grass;
            kit.Tri(a, b, c, normal, kind);
        }

        // The pond: stylized water (the tutorial pond's material and shader) over the basin, reeds, cattails and
        // lily pads.
        private static void BuildPond(Transform arena, System.Random random)
        {
            var pond = new GameObject("Pond").transform;
            pond.SetParent(arena, false);
            pond.position = new Vector3(PondCentre.x, WaterY, PondCentre.y);

            const float cell = 1f;
            Vector2 extent = PondRadii * 1.3f;
            int nx = Mathf.CeilToInt(extent.x * 2f / cell), nz = Mathf.CeilToInt(extent.y * 2f / cell);
            var vertices = new List<Vector3>();
            var colours = new List<Color>();
            var e = new float[nx + 1, nz + 1];
            for (int i = 0; i <= nx; i++)
            for (int k = 0; k <= nz; k++)
            {
                float x = -extent.x + i * cell, z = -extent.y + k * cell;
                e[i, k] = PondDistance(PondCentre.x + x, PondCentre.y + z);
                vertices.Add(new Vector3(x, 0f, z));
                colours.Add(new Color(Mathf.InverseLerp(0.55f, 1f, e[i, k]), 0f, 0f, 1f));
            }
            var triangles = new List<int>();
            for (int i = 0; i < nx; i++)
            for (int k = 0; k < nz; k++)
            {
                if (e[i, k] > 1.25f || e[i + 1, k] > 1.25f || e[i + 1, k + 1] > 1.25f || e[i, k + 1] > 1.25f) continue;
                int a = i * (nz + 1) + k, b = (i + 1) * (nz + 1) + k;
                triangles.AddRange(new[] { a, a + 1, b + 1, a, b + 1, b });
            }
            var mesh = new Mesh { name = "ChiefToadPondWater" };
            mesh.SetVertices(vertices);
            mesh.SetColors(colours);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh = SaveMesh(mesh);
            var water = new GameObject("Pond Water");
            water.transform.SetParent(pond, false);
            water.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = water.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(MeadowAssetBuilder.Folder + "/Materials/PondWater.mat") ?? WaterMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;

            for (float angle = 0f; angle < Mathf.PI * 2f; angle += 0.16f)
            {
                if (Mathf.PerlinNoise(Mathf.Cos(angle) * 3f + 5f, Mathf.Sin(angle) * 3f) > 0.62f) continue; // gaps between reed beds
                float reach = Range(random, 0.93f, 1.07f);
                Vector2 at = PondCentre + new Vector2(Mathf.Cos(angle) * PondRadii.x, Mathf.Sin(angle) * PondRadii.y) * reach;
                Place(MeadowAssetBuilder.PrefabPath(MeadowAssetBuilder.ReedClumps[random.Next(2)]), pond, Ground3(at), Range(random, 0f, 360f), Range(random, 0.9f, 1.3f));
            }
            for (int i = 0; i < 7; i++)
            {
                float angle = Range(random, 0f, Mathf.PI * 2f);
                Vector2 at = PondCentre + new Vector2(Mathf.Cos(angle) * PondRadii.x, Mathf.Sin(angle) * PondRadii.y) * Range(random, 0.35f, 0.75f);
                Place(MeadowAssetBuilder.PrefabPath("LilyPads"), pond, new Vector3(at.x, WaterY + 0.04f, at.y), Range(random, 0f, 360f), Range(random, 0.9f, 1.4f));
            }
        }

        // Boulders to hide behind (from the scream and the beam), rocks and bushes on the banks, and a ring of trees.
        private static void BuildCover(Transform arena, System.Random random)
        {
            var cover = new GameObject("Cover").transform;
            cover.SetParent(arena, false);
            Vector2[] boulders =
            {
                new Vector2(-14f, -10f), new Vector2(13f, -12f), new Vector2(-20f, 8f), new Vector2(21f, 4f),
                new Vector2(-9f, 22f), new Vector2(10f, 23f), new Vector2(0f, -17f), new Vector2(-24f, -4f),
            };
            for (int i = 0; i < boulders.Length; i++)
            {
                string kind = i % 3 == 0 ? "BoulderTall" : "Boulder";
                Place(MeadowAssetBuilder.PrefabPath(kind), cover, Ground3(boulders[i]) + Vector3.down * 0.3f, Range(random, 0f, 360f), Range(random, 1.1f, 1.5f));
            }
            for (int i = 0; i < 18; i++)
            {
                float angle = Range(random, 0f, Mathf.PI * 2f);
                Vector2 at = PondCentre + new Vector2(Mathf.Cos(angle) * PondRadii.x, Mathf.Sin(angle) * PondRadii.y) * Range(random, 1.08f, 1.4f);
                Place(MeadowAssetBuilder.PrefabPath(MeadowAssetBuilder.Rocks[random.Next(3)]), cover, Ground3(at) + Vector3.down * 0.1f, Range(random, 0f, 360f), Range(random, 0.9f, 1.8f));
            }
            for (int i = 0; i < 26; i++)
            {
                Vector2 at = new Vector2(Range(random, -30f, 30f), Range(random, -30f, 30f));
                if (PondDistance(at.x, at.y) < 1.5f || at.magnitude > 32f || (at - new Vector2(PlayerStart.x, PlayerStart.z)).magnitude < 5f) continue;
                Place(MeadowAssetBuilder.PrefabPath(MeadowAssetBuilder.Bushes[random.Next(2)]), cover, Ground3(at), Range(random, 0f, 360f), Range(random, 0.9f, 1.6f));
            }
            for (int i = 0; i < 70; i++)
            {
                float angle = i * Mathf.PI * 2f / 70f + Range(random, -0.03f, 0.03f);
                float r = Range(random, 33f, 40f);
                var at = new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r);
                if ((at - new Vector2(PlayerStart.x, PlayerStart.z)).magnitude < 6f) continue;
                Place(MeadowAssetBuilder.PrefabPath(MeadowAssetBuilder.Trees[random.Next(MeadowAssetBuilder.Trees.Length)]), cover,
                    Ground3(at) + Vector3.down * 0.15f, Range(random, 0f, 360f), Range(random, 0.9f, 1.3f));
            }
        }

        private static void Place(string path, Transform parent, Vector3 position, float yaw, float scale)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) throw new InvalidOperationException("Missing prefab " + path);
            var item = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            item.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            item.transform.localScale = Vector3.one * scale;
        }

        private static float Range(System.Random random, float min, float max) => min + (float)random.NextDouble() * (max - min);

        // Late-afternoon light, matching the tutorial meadow.
        private static void BuildLighting()
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.82f, 0.6f);
            sun.intensity = 1.4f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(32f, 60f, 0f);
            RenderSettings.sun = sun;
            var sky = AssetDatabase.LoadAssetAtPath<Material>(MeadowAssetBuilder.Folder + "/Materials/GoldenHourSky.mat");
            if (sky != null) RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.66f, 0.78f);
            RenderSettings.ambientEquatorColor = new Color(0.78f, 0.66f, 0.5f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.32f, 0.2f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.86f, 0.76f, 0.6f);
            RenderSettings.fogStartDistance = 40f;
            RenderSettings.fogEndDistance = 150f;
        }
    }
}
