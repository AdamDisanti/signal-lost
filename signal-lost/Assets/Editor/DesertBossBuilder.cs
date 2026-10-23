using System;
using System.Collections.Generic;
using SignalLost.Combat;
using SignalLost.Enemies;
using SignalLost.Enemies.Desert;
using SignalLost.Inventory;
using SignalLost.Player;
using SignalLost.Scanner;
using SignalLost.UI;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using static SignalLost.Editor.EnemyTestBuilder;
using Object = UnityEngine.Object;

namespace SignalLost.Editor
{
    /// <summary>
    /// Generates the desert stage boss, the Coreworm, as a low-poly placeholder prefab, and a test scene: a small
    /// stretch of desert ending at the stage exit (where the worm ambushes you) and, far away, its round arena with
    /// collapsible floor tiles, rock pillars and hull slabs for cover, a NavMesh and a fodder spawner.
    ///
    /// It only creates new files: the prefab in Assets/Prefabs/Enemies/Desert/Boss, meshes and materials in the
    /// desert enemy folders, and Assets/Scenes/Bosses/CorewormTest.unity (never overwritten). "Regenerate Prefab"
    /// rebuilds the prefab in place (same GUID).
    /// </summary>
    public static class DesertBossBuilder
    {
        private const string Folder = "Assets/Prefabs/Enemies/Desert/Boss";
        public const string WormPath = Folder + "/Coreworm.prefab";
        public const string ScenePath = "Assets/Scenes/Bosses/CorewormTest.unity";

        private static readonly Vector3 ArenaCentre = new Vector3(300f, 0f, 0f);
        private const float ArenaRadius = 34f;
        private const int SegmentCount = 16;

        // ---------- Menus ----------

        [MenuItem("Signal Lost/Desert Boss/Create Test Scene")]
        public static void CreateTestScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                Debug.Log(ScenePath + " already exists; skipped (edit it in Unity, or delete it to rebuild).");
                Validate();
                return;
            }
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            DesertFodderBuilder.EnsureFodderPrefabs();
            EnsureFolders();
            if (DesertFodderBuilder.Load(WormPath) == null) BuildWorm();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Transform exitPoint = BuildStageArea(out Transform eruptPoint, out Transform returnPoint);
            CorewormArena arena = BuildArena(out Transform drop, out FodderSpawner spawner);

            var worm = (GameObject)PrefabUtility.InstantiatePrefab(DesertFodderBuilder.Load(WormPath));
            worm.name = "Coreworm";
            worm.transform.position = ArenaCentre + Vector3.down * 12f;
            var wormData = new SerializedObject(worm.GetComponent<Coreworm>());
            wormData.FindProperty("arena").objectReferenceValue = arena;
            wormData.ApplyModifiedPropertiesWithoutUndo();
            var spawnerData = new SerializedObject(spawner);
            spawnerData.FindProperty("stopWhenDead").objectReferenceValue = worm.GetComponent<Health>();
            spawnerData.ApplyModifiedPropertiesWithoutUndo();

            var exit = exitPoint.gameObject.AddComponent<DesertStageExit>();
            var exitData = new SerializedObject(exit);
            exitData.FindProperty("worm").objectReferenceValue = worm.GetComponent<Coreworm>();
            exitData.FindProperty("arena").objectReferenceValue = arena;
            exitData.FindProperty("eruptPoint").objectReferenceValue = eruptPoint;
            exitData.FindProperty("returnPoint").objectReferenceValue = returnPoint;
            exitData.FindProperty("assumePartsCollected").boolValue = true; // test scene: go straight to the ambush
            exitData.ApplyModifiedPropertiesWithoutUndo();

            UseCorePlayer(scene);
            new GameObject("Mini-Boss HUD").AddComponent<MiniBossHud>();
            new GameObject("Fodder Status Overlay").AddComponent<FodderStatusOverlay>();
            DesertFodderBuilder.BuildDesertLighting();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("DESERT_BOSS_TEST_SCENE_CREATED_AND_VALIDATED");
        }

        /// <summary>
        /// Moves the existing test scene onto the shared Player and GameSystems prefabs (keeping everything else in it),
        /// for scenes made before the core player existed.
        /// </summary>
        [MenuItem("Signal Lost/Desert Boss/Upgrade Test Scene to Core Player")]
        public static void UpgradeTestScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                CreateTestScene();
                return;
            }
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            UseCorePlayer(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Validate();
            Debug.Log("DESERT_BOSS_TEST_SCENE_UPGRADED");
        }

        // The shared Player (laser and scanner; the flask comes with the fight) at the canyon start, with GameSystems
        // and its spawn point, in place of any old test player and HUD.
        private static void UseCorePlayer(Scene scene)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(CorePrefabBuilder.PlayerPath) == null) CorePrefabBuilder.Build();
            GameObject player = CoreSceneSetup.UpgradeToCorePlayer(scene, new[] { PlayerItem.MiningLaser, PlayerItem.Scanner }, null);
            Vector3 start = new Vector3(0f, 0.1f, -30f);
            player.transform.SetPositionAndRotation(start, Quaternion.identity);
            foreach (PlayerSpawnPoint spawn in All<PlayerSpawnPoint>()) spawn.transform.SetPositionAndRotation(start, Quaternion.identity);
        }

        [MenuItem("Signal Lost/Desert Boss/Regenerate Prefab")]
        public static void Regenerate()
        {
            if (!Application.isBatchMode && !EditorUtility.DisplayDialog("Regenerate the Coreworm prefab",
                    "This rebuilds the Coreworm from the builder script, replacing any edits made to the prefab. Materials are " +
                    "kept, and the test scene keeps its worm. Continue?", "Regenerate", "Cancel"))
                return;
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string previous = SceneManager.GetActiveScene().path;
            EnsureFolders();
            BuildWorm();
            AssetDatabase.SaveAssets();
            RepairScene();
            if (!string.IsNullOrEmpty(previous) && AssetDatabase.LoadAssetAtPath<SceneAsset>(previous) != null) EditorSceneManager.OpenScene(previous);
            Debug.Log("DESERT_BOSS_PREFAB_REGENERATED");
        }

        /// <summary>Re-links the test scene's worm to its arena, exit and fodder spawner if rebuilding the prefab knocked them loose.</summary>
        private static void RepairScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) return;
            Scene scene = EditorSceneManager.OpenScene(ScenePath);
            List<Coreworm> worms = All<Coreworm>();
            List<CorewormArena> arenas = All<CorewormArena>();
            if (worms.Count == 0 || arenas.Count == 0) return;
            bool changed = false;
            var wormData = new SerializedObject(worms[0]);
            if (wormData.FindProperty("arena").objectReferenceValue == null)
            {
                wormData.FindProperty("arena").objectReferenceValue = arenas[0];
                wormData.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }
            foreach (DesertStageExit exit in All<DesertStageExit>())
            {
                var exitData = new SerializedObject(exit);
                if (exitData.FindProperty("worm").objectReferenceValue != null) continue;
                exitData.FindProperty("worm").objectReferenceValue = worms[0];
                exitData.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }
            foreach (FodderSpawner spawner in All<FodderSpawner>())
            {
                var spawnerData = new SerializedObject(spawner);
                if (spawnerData.FindProperty("stopWhenDead").objectReferenceValue != null) continue;
                spawnerData.FindProperty("stopWhenDead").objectReferenceValue = worms[0].GetComponent<Health>();
                spawnerData.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }
            if (!changed) return;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log(ScenePath + ": re-linked the Coreworm after regenerating.");
        }

        [MenuItem("Signal Lost/Desert Boss/Validate")]
        public static void Validate()
        {
            GameObject prefab = DesertFodderBuilder.Load(WormPath);
            Require(prefab != null, "Missing prefab " + WormPath);
            var worm = prefab.GetComponent<Coreworm>();
            Require(worm != null && prefab.GetComponent<Health>() != null, WormPath + " needs a Coreworm and Health.");
            var data = new SerializedObject(worm);
            foreach (string field in new[] { "head", "body", "segments", "jaws", "core", "coreRenderer", "coreLight", "ventRenderers", "armorHitboxes",
                         "coreHitbox", "ventHitboxes", "warningMaterial", "slagMaterial", "shrapnelMaterial", "rockMaterial", "blastMaterial", "dustMaterial" })
            {
                SerializedProperty property = data.FindProperty(field);
                Require(property != null, WormPath + ": unknown field " + field);
                if (property.isArray)
                {
                    Require(property.arraySize > 0, WormPath + ": " + field + " is empty.");
                    for (int i = 0; i < property.arraySize; i++)
                        Require(property.GetArrayElementAtIndex(i).objectReferenceValue != null, WormPath + ": " + field + "[" + i + "] is empty.");
                }
                else Require(property.objectReferenceValue != null, WormPath + ": " + field + " is not assigned.");
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                EditorSceneManager.OpenScene(ScenePath);
                Require(Count<FirstPersonController>() == 1 && Count<PlayerGear>() == 1 && Count<MiningLaser>() == 1,
                    ScenePath + ": expected one shared Player (Signal Lost > Desert Boss > Upgrade Test Scene to Core Player).");
                Require(Count<SignalLost.Core.GameSystems>() == 1 && Count<PlayerSpawnPoint>() >= 1, ScenePath + ": expected GameSystems and a spawn point.");
                Require(Count<Camera>() == 1 && Count<AudioListener>() == 1, ScenePath + ": expected one camera and listener.");
                Require(Count<Coreworm>() == 1 && Count<CorewormArena>() == 1 && Count<DesertStageExit>() == 1, ScenePath + ": expected the worm, its arena and the stage exit.");
                Require(Count<NavMeshSurface>() == 1 && Count<NavMeshBaker>() == 1, ScenePath + ": expected one NavMeshSurface with a NavMeshBaker.");
                var exit = new SerializedObject(All<DesertStageExit>()[0]);
                foreach (string field in new[] { "worm", "arena", "eruptPoint", "returnPoint" })
                    Require(exit.FindProperty(field).objectReferenceValue != null, ScenePath + ": the stage exit has no " + field + ".");
                Require(new SerializedObject(All<Coreworm>()[0]).FindProperty("arena").objectReferenceValue != null, ScenePath + ": the worm has no arena.");
                foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                    foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                        Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject) == 0, ScenePath + ": missing script on " + item.name);
            }
            Debug.Log("DESERT_BOSS_VALIDATED");
        }

        // ---------- The worm ----------

        private static Mesh blob, gem, spike, limb;

        // A 29 m sandworm, mouth along +Z. A dark sand-coloured skull open at the front, with a fleshy lip, three petal
        // jaws, rings of bone teeth and the Engine Core glowing in the throat; veins of its energy run back over the skull.
        // One continuous ringed body (CorewormBody) with energy veins pulsing over it; anchors along it carry the hit
        // boxes and the glowing energy blisters on both sides (the weak spots).
        private static GameObject BuildWorm()
        {
            blob = DesertFodderBuilder.SaveDesertMesh(LowPolyMesh.Icosphere("DesertBlob", 1));
            gem = DesertFodderBuilder.SaveDesertMesh(LowPolyMesh.Icosphere("DesertGem", 0));
            spike = DesertFodderBuilder.SaveDesertMesh(LowPolyMesh.TaperedPrism("DesertSpike", 5, 0f));
            limb = DesertFodderBuilder.SaveDesertMesh(LowPolyMesh.TaperedPrism("DesertLimb", 5, 0.6f));
            Material hide = DesertFodderBuilder.Lit("WormDuneHide", new Color(0.38f, 0.27f, 0.17f), 0f, 0.3f);
            Material darkHide = DesertFodderBuilder.Lit("WormDuneHideDark", new Color(0.26f, 0.18f, 0.11f), 0f, 0.3f);
            Material flesh = DesertFodderBuilder.Lit("WormFlesh", new Color(0.42f, 0.13f, 0.09f), 0f, 0.65f);
            Material teeth = DesertFodderBuilder.Lit("WormBoneTeeth", new Color(0.84f, 0.77f, 0.6f), 0f, 0.5f);
            Material metal = DesertFodderBuilder.Lit("WormMetal", new Color(0.3f, 0.3f, 0.33f), 0.85f, 0.45f);
            Material energy = DesertFodderBuilder.Unlit("WormEnergy", new Color(1f, 0.55f, 0.16f));
            Material blister = DesertFodderBuilder.Unlit("WormVent", new Color(1f, 0.5f, 0.15f));
            Material coreMat = DesertFodderBuilder.Unlit("WormCore", new Color(1f, 0.78f, 0.38f));
            Material coreHeart = DesertFodderBuilder.Unlit("WormCoreHeart", new Color(1f, 0.97f, 0.85f));

            var root = new GameObject("Coreworm");
            var armor = new List<Health>();
            var vents = new List<Health>();
            var ventRenderers = new List<Renderer>();

            // ---- Head (scaled up 15%): lathe shells turned from +Y to +Z, open at the front. ----
            Transform head = Empty("Head", root.transform, Vector3.zero);
            head.localScale = Vector3.one * 1.15f;
            Quaternion toZ = Quaternion.Euler(90f, 0f, 0f);
            Mesh skull = DesertFodderBuilder.SaveDesertMesh(LowPolyMesh.Lathe("WormSkull", new[]
            {
                new Vector2(0f, -1.7f), new Vector2(0.9f, -1.5f), new Vector2(1.5f, -0.7f), new Vector2(1.68f, 0.3f),
                new Vector2(1.55f, 1.15f), new Vector2(1.3f, 1.6f), new Vector2(1.12f, 1.65f),
            }, 14));
            Vector2[] throatProfile =
            {
                new Vector2(1.12f, 1.6f), new Vector2(1.32f, 1.0f), new Vector2(1.3f, 0f), new Vector2(0.9f, -0.9f), new Vector2(0f, -1.25f),
            };
            Mesh throatIn = DesertFodderBuilder.SaveDesertMesh(LowPolyMesh.Lathe("WormThroat", throatProfile, 14));
            Array.Reverse(throatProfile);
            Mesh throatOut = DesertFodderBuilder.SaveDesertMesh(LowPolyMesh.Lathe("WormThroatBack", throatProfile, 14));
            Part("Skull", head, skull, hide, Vector3.zero, toZ, Vector3.one);
            Part("Throat", head, throatIn, flesh, Vector3.zero, toZ, Vector3.one);
            Part("Throat Back", head, throatOut, flesh, Vector3.zero, toZ, Vector3.one); // both facings, so it's solid from any side
            Part("Lip", head, DesertFodderBuilder.SaveDesertMesh(LowPolyMesh.Tube("WormLip", Circle(1.2f, 16), 0.2f, 6)), flesh,
                new Vector3(0f, 0f, 1.62f), Quaternion.identity, Vector3.one);
            // Annular ridges round the skull, like the body's.
            Mesh ridge = DesertFodderBuilder.SaveDesertMesh(LowPolyMesh.Tube("WormHeadRidge", Circle(1f, 16), 0.07f, 5));
            Part("Ridge", head, ridge, darkHide, new Vector3(0f, 0f, 0.95f), Quaternion.identity, Vector3.one * 1.6f);
            Part("Ridge", head, ridge, darkHide, new Vector3(0f, 0f, 0.1f), Quaternion.identity, Vector3.one * 1.7f);
            Part("Ridge", head, ridge, darkHide, new Vector3(0f, 0f, -0.75f), Quaternion.identity, Vector3.one * 1.5f);
            // Two rings of bone teeth inside the mouth, angled down the throat like a lamprey's.
            for (int ring = 0; ring < 2; ring++)
                for (int i = 0; i < 14; i++)
                {
                    float a = (i + ring * 0.5f) * Mathf.PI * 2f / 14f;
                    float r = ring == 0 ? 1.05f : 0.85f;
                    Vector3 at = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, ring == 0 ? 1.45f : 1.0f);
                    Quaternion inward = Quaternion.LookRotation((-at.normalized * 0.8f + Vector3.back * 0.6f + Vector3.forward * 0.1f).normalized, Vector3.up);
                    Part("Tooth", head, spike, teeth, at, inward, new Vector3(0.09f, 0.09f, ring == 0 ? 0.42f : 0.3f));
                }
            // Energy veins running back over the skull from the lip, branching.
            for (int i = 0; i < 6; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 2f / 6f;
                Vector3 from = new Vector3(Mathf.Cos(a) * 1.56f, Mathf.Sin(a) * 1.56f, 1.2f);
                Vector3 mid = new Vector3(Mathf.Cos(a + 0.2f) * 1.66f, Mathf.Sin(a + 0.2f) * 1.66f, 0.2f);
                Vector3 to = new Vector3(Mathf.Cos(a + 0.1f) * 1.3f, Mathf.Sin(a + 0.1f) * 1.3f, -1.1f);
                Vector3 branch = new Vector3(Mathf.Cos(a + 0.55f) * 1.55f, Mathf.Sin(a + 0.55f) * 1.55f, -0.5f);
                DisableShadows(Segment("Energy Vein", head, limb, energy, from, mid, 0.06f));
                DisableShadows(Segment("Energy Vein", head, limb, energy, mid, to, 0.05f));
                DisableShadows(Segment("Energy Vein", head, limb, energy, mid, branch, 0.035f));
            }

            // Three petal jaws round the rim; each opens by tilting outward about its own X axis.
            var jaws = new Transform[3];
            for (int i = 0; i < 3; i++)
            {
                float a = 90f + i * 120f;
                Vector3 rim = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad), 0f) * 1.25f + Vector3.forward * 1.55f;
                Transform jaw = Empty("Jaw " + (i + 1), head, rim);
                jaw.localRotation = Quaternion.Euler(0f, 0f, a - 90f); // local +Y points out from the mouth
                Part("Petal", jaw, spike, hide, Vector3.zero, Quaternion.Euler(14f, 0f, 0f), new Vector3(0.7f, 0.4f, 2.1f));
                Part("Petal Flesh", jaw, spike, flesh, new Vector3(0f, -0.08f, 0.05f), Quaternion.Euler(16f, 0f, 0f), new Vector3(0.55f, 0.22f, 1.85f));
                DisableShadows(Segment("Petal Vein", jaw, limb, energy, new Vector3(0f, 0.18f, 0.1f), new Vector3(0f, -0.15f, 1.6f), 0.05f));
                for (int t = 0; t < 4; t++)
                    Part("Fang", jaw, spike, teeth, new Vector3(0f, -0.26f, 0.3f + t * 0.42f), Quaternion.Euler(110f, 0f, 0f), new Vector3(0.09f, 0.09f, 0.36f));
                jaws[i] = jaw;
            }

            // The Engine Core in the throat: a white-hot heart in a glowing shell, still in its metal brace.
            Transform core = Empty("Engine Core", head, new Vector3(0f, 0f, 0.15f));
            Transform coreShell = Part("Core Shell", core, gem, coreMat, Vector3.zero, Quaternion.identity, Vector3.one * 0.75f);
            Part("Core Heart", core, blob, coreHeart, Vector3.zero, Quaternion.identity, Vector3.one * 0.45f);
            Mesh coreRing = DesertFodderBuilder.SaveDesertMesh(LowPolyMesh.Tube("WormCoreRing", Circle(0.82f, 12), 0.06f, 4));
            Part("Core Ring", core, coreRing, metal, Vector3.zero, Quaternion.identity, Vector3.one);
            Part("Core Ring", core, coreRing, metal, Vector3.zero, Quaternion.Euler(0f, 90f, 0f), Vector3.one);
            foreach (Transform piece in core.GetComponentsInChildren<Transform>()) DisableShadows(piece);
            var lightObject = new GameObject("Core Light");
            lightObject.transform.SetParent(core, false);
            lightObject.transform.localPosition = Vector3.forward * 0.9f;
            var coreLight = lightObject.AddComponent<Light>();
            coreLight.type = LightType.Point;
            coreLight.color = new Color(1f, 0.55f, 0.2f);
            coreLight.range = 12f;
            coreLight.intensity = 3f;
            coreLight.shadows = LightShadows.None;
            var coreBox = new GameObject("Coreworm (core)");
            coreBox.transform.SetParent(core, false);
            coreBox.AddComponent<SphereCollider>().radius = 0.9f;
            Health coreHealth = HitboxHealth(coreBox);

            var headBox = new GameObject("Coreworm (head)");
            headBox.transform.SetParent(head, false);
            var headCollider = headBox.AddComponent<CapsuleCollider>();
            headCollider.direction = 2;
            headCollider.center = new Vector3(0f, 0f, -0.6f);
            headCollider.radius = 1.55f;
            headCollider.height = 2.4f; // stops short of the mouth, so the core can be hit from the front
            armor.Add(HitboxHealth(headBox));

            // ---- The continuous body ----
            var bodyObject = new GameObject("Body");
            bodyObject.transform.SetParent(root.transform, false);
            bodyObject.AddComponent<MeshFilter>();
            bodyObject.AddComponent<MeshRenderer>().sharedMaterial = hide;
            var veinObject = new GameObject("Energy Veins");
            veinObject.transform.SetParent(bodyObject.transform, false);
            var veinFilter = veinObject.AddComponent<MeshFilter>();
            var veinRenderer = veinObject.AddComponent<MeshRenderer>();
            veinRenderer.sharedMaterial = energy;
            veinRenderer.shadowCastingMode = ShadowCastingMode.Off;
            var bodyComponent = bodyObject.AddComponent<CorewormBody>();
            var bodyData = new SerializedObject(bodyComponent);
            bodyData.FindProperty("length").floatValue = BodyLength;
            bodyData.FindProperty("veins").objectReferenceValue = veinFilter;
            bodyData.ApplyModifiedPropertiesWithoutUndo();

            // ---- Anchors along the body: hit boxes and the energy blisters (local +Z points toward the head) ----
            var segments = new Transform[SegmentCount];
            for (int i = 0; i < SegmentCount; i++)
            {
                float t = (NeckGap + i * SegmentSpacing) / BodyLength;
                float r = CorewormBody.RadiusAt(t);
                Transform segment = Empty("Anchor " + (i + 1), root.transform, new Vector3(0f, 0f, -NeckGap - i * SegmentSpacing));
                var box = new GameObject("Coreworm (body)");
                box.transform.SetParent(segment, false);
                box.AddComponent<SphereCollider>().radius = r;
                armor.Add(HitboxHealth(box));
                if (i == 2 || i == 5 || i == 8 || i == 11)
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        // A swollen blister of the core's energy bulging through the hide: the weak spot.
                        Vector3 at = new Vector3(side * r * 0.93f, r * 0.1f, 0f);
                        Part("Blister Rim", segment, blob, flesh, at, Quaternion.identity, new Vector3(0.28f, 0.5f, 0.62f) * r);
                        Transform glow = Part("Energy Blister", segment, blob, blister, at + new Vector3(side * r * 0.08f, 0f, 0f), Quaternion.identity, new Vector3(0.24f, 0.4f, 0.5f) * r);
                        DisableShadows(glow);
                        ventRenderers.Add(glow.GetComponent<Renderer>());
                        var ventBox = new GameObject("Coreworm (blister)");
                        ventBox.transform.SetParent(segment, false);
                        ventBox.transform.localPosition = at;
                        ventBox.AddComponent<SphereCollider>().radius = 0.45f * r;
                        vents.Add(HitboxHealth(ventBox));
                    }
                }
                segments[i] = segment;
            }

            var health = root.AddComponent<Health>();
            var healthData = new SerializedObject(health);
            healthData.FindProperty("maxHealth").floatValue = 825f;
            healthData.ApplyModifiedPropertiesWithoutUndo();
            var worm = root.AddComponent<Coreworm>();
            var data = new SerializedObject(worm);
            data.FindProperty("head").objectReferenceValue = head;
            data.FindProperty("body").objectReferenceValue = bodyComponent;
            data.FindProperty("exhaustStacks").arraySize = 0;
            SetArray(data.FindProperty("segments"), segments);
            data.FindProperty("segmentSpacing").floatValue = SegmentSpacing;
            data.FindProperty("neckGap").floatValue = NeckGap;
            SetArray(data.FindProperty("jaws"), jaws);
            data.FindProperty("core").objectReferenceValue = core;
            data.FindProperty("coreRenderer").objectReferenceValue = coreShell.GetComponent<Renderer>();
            data.FindProperty("coreLight").objectReferenceValue = coreLight;
            SetObjects(data.FindProperty("ventRenderers"), ventRenderers.ToArray());
            SetObjects(data.FindProperty("armorHitboxes"), armor.ToArray());
            data.FindProperty("coreHitbox").objectReferenceValue = coreHealth;
            SetObjects(data.FindProperty("ventHitboxes"), vents.ToArray());
            data.FindProperty("warningMaterial").objectReferenceValue = DesertFodderBuilder.Unlit("BossWarning", new Color(1f, 0.3f, 0.15f));
            data.FindProperty("slagMaterial").objectReferenceValue = DesertFodderBuilder.Unlit("WormSlag", new Color(1f, 0.45f, 0.12f));
            data.FindProperty("shrapnelMaterial").objectReferenceValue = DesertFodderBuilder.Unlit("WormShrapnel", new Color(1f, 0.92f, 0.65f));
            data.FindProperty("rockMaterial").objectReferenceValue = DesertFodderBuilder.Lit("WormRockDebris", new Color(0.55f, 0.38f, 0.28f), 0f, 0.15f);
            data.FindProperty("blastMaterial").objectReferenceValue = DesertFodderBuilder.Unlit("BeetleBlast", new Color(1f, 0.55f, 0.15f));
            data.FindProperty("dustMaterial").objectReferenceValue = DesertFodderBuilder.Unlit("DesertDust", new Color(0.88f, 0.75f, 0.54f));
            data.ApplyModifiedPropertiesWithoutUndo();
            return DesertFodderBuilder.SaveDesertPrefab(root, WormPath);
        }

        private const float NeckGap = 1.9f;
        private const float SegmentSpacing = 1.7f;
        private const float BodyLength = NeckGap + SegmentSpacing * SegmentCount;

        private static Vector3[] Circle(float radius, int points)
        {
            var path = new Vector3[points + 1];
            for (int i = 0; i <= points; i++)
            {
                float a = i * Mathf.PI * 2f / points;
                path[i] = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
            }
            return path;
        }

        private static Health HitboxHealth(GameObject holder)
        {
            var health = holder.AddComponent<Health>();
            var data = new SerializedObject(health);
            // Never runs out (restored after every hit). Large because Damaged reports damage capped at current HP.
            data.FindProperty("maxHealth").floatValue = 100000f;
            data.ApplyModifiedPropertiesWithoutUndo();
            return health;
        }

        private static void SetObjects(SerializedProperty property, Object[] items)
        {
            property.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }

        private static void DisableShadows(Transform item)
        {
            var renderer = item.GetComponent<Renderer>();
            if (renderer != null) renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        // ---------- Test scene ----------

        // A 40 x 80 m stretch of desert canyon. The player starts at the south end; the stage exit (a ship beacon
        // between two hull posts) is at the north end, and the worm erupts just in front of it.
        private static Transform BuildStageArea(out Transform eruptPoint, out Transform returnPoint)
        {
            Material sand = DesertFodderBuilder.Lit("TestSand", new Color(0.86f, 0.71f, 0.48f), 0f, 0.1f);
            Material wall = DesertFodderBuilder.Lit("TestSandstone", new Color(0.69f, 0.42f, 0.29f), 0f, 0.15f);
            Material rock = DesertFodderBuilder.Lit("TestRock", new Color(0.55f, 0.36f, 0.27f), 0f, 0.15f);
            Material hull = DesertFodderBuilder.Lit("TestHull", new Color(0.62f, 0.64f, 0.66f), 0.7f, 0.4f);
            Material beacon = DesertFodderBuilder.Unlit("ExitBeacon", new Color(0.45f, 1f, 0.6f));

            Transform area = new GameObject("Desert Stage (test stretch)").transform;
            Block("Sand Floor", new Vector3(0f, -0.5f, 0f), new Vector3(40f, 1f, 80f), sand, area);
            Block("West Cliff", new Vector3(-20f, 4f, 0f), new Vector3(1f, 8f, 80f), wall, area);
            Block("East Cliff", new Vector3(20f, 4f, 0f), new Vector3(1f, 8f, 80f), wall, area);
            Block("South Cliff", new Vector3(0f, 4f, -40f), new Vector3(41f, 8f, 1f), wall, area);
            Block("North Cliff", new Vector3(0f, 4f, 40f), new Vector3(41f, 8f, 1f), wall, area);
            Block("Rock", new Vector3(-9f, 0.8f, -12f), new Vector3(3f, 1.6f, 2.4f), rock, area);
            Block("Rock", new Vector3(10f, 1f, 4f), new Vector3(2.6f, 2f, 3f), rock, area);
            Block("Hull Plate", new Vector3(-6f, 0.7f, 14f), new Vector3(3.2f, 1.4f, 0.25f), hull, area).transform.rotation = Quaternion.Euler(0f, 35f, 6f);

            Transform exit = new GameObject("Desert Exit (ship beacon)").transform;
            exit.SetParent(area, false);
            exit.position = new Vector3(0f, 0f, 34f);
            Block("Exit Post", new Vector3(-4f, 3f, 36f), new Vector3(1f, 6f, 1f), hull, exit);
            Block("Exit Post", new Vector3(4f, 3f, 36f), new Vector3(1f, 6f, 1f), hull, exit);
            Block("Exit Beam", new Vector3(0f, 6.2f, 36f), new Vector3(9.5f, 0.5f, 1f), hull, exit);
            GameObject lamp = Block("Beacon", new Vector3(0f, 7f, 36f), new Vector3(0.8f, 0.8f, 0.8f), beacon, exit);
            Object.DestroyImmediate(lamp.GetComponent<Collider>());
            var light = new GameObject("Beacon Light").AddComponent<Light>();
            light.transform.SetParent(exit, false);
            light.transform.position = new Vector3(0f, 7f, 35f);
            light.type = LightType.Point;
            light.color = new Color(0.45f, 1f, 0.6f);
            light.range = 14f;
            light.intensity = 3f;

            eruptPoint = new GameObject("Worm Erupts Here").transform;
            eruptPoint.SetParent(exit, false);
            eruptPoint.position = new Vector3(0f, 0f, 30f);
            returnPoint = new GameObject("Return Point").transform;
            returnPoint.SetParent(exit, false);
            returnPoint.position = new Vector3(0f, 0f, 24f);
            return exit;
        }

        // A round bowl 68 m across, far from the stage stretch: floor tiles that can collapse, a ring of sandstone walls,
        // six rock pillars and three hull slabs for cover, a NavMesh for fodder, and the fodder spawner.
        private static CorewormArena BuildArena(out Transform drop, out FodderSpawner spawner)
        {
            Material sand = DesertFodderBuilder.Lit("ArenaSand", new Color(0.84f, 0.68f, 0.45f), 0f, 0.1f);
            Material wall = DesertFodderBuilder.Lit("TestSandstone", new Color(0.69f, 0.42f, 0.29f), 0f, 0.15f);
            Material rock = DesertFodderBuilder.Lit("TestRock", new Color(0.55f, 0.36f, 0.27f), 0f, 0.15f);
            Material hull = DesertFodderBuilder.Lit("TestHull", new Color(0.62f, 0.64f, 0.66f), 0.7f, 0.4f);

            var root = new GameObject("Coreworm Arena");
            root.transform.position = ArenaCentre;
            var surface = root.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            root.AddComponent<NavMeshBaker>();
            Transform a = root.transform;
            Transform centre = Empty("Centre", a, Vector3.zero);

            var tiles = new List<Transform>();
            for (float x = -32.5f; x <= 32.5f; x += 5f)
                for (float z = -32.5f; z <= 32.5f; z += 5f)
                    if (new Vector2(x, z).magnitude <= ArenaRadius)
                        tiles.Add(Block("Floor Tile", ArenaCentre + new Vector3(x, -0.5f, z), new Vector3(5f, 1f, 5f), sand, a).transform);

            for (int i = 0; i < 28; i++)
            {
                float angle = i * 360f / 28f;
                Vector3 at = ArenaCentre + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * 37f + Vector3.up * 3.5f;
                Block("Dune Wall", at, new Vector3(9f, 7f, 2f), wall, a).transform.rotation = Quaternion.Euler(0f, angle, 0f);
            }

            var cover = new List<Transform>();
            for (int i = 0; i < 6; i++)
            {
                float angle = i * 60f + 15f;
                Vector3 at = ArenaCentre + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * 17f + Vector3.up * 4f;
                GameObject pillar = Block("Rock Pillar", at, new Vector3(3.2f, 8f, 3.2f), rock, a);
                pillar.transform.rotation = Quaternion.Euler(0f, angle * 1.7f, 0f);
                cover.Add(pillar.transform);
            }
            for (int i = 0; i < 3; i++)
            {
                float angle = i * 120f + 45f;
                Vector3 at = ArenaCentre + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * 26f + Vector3.up * 1.75f;
                GameObject slab = Block("Hull Slab", at, new Vector3(5f, 3.5f, 0.8f), hull, a);
                slab.transform.rotation = Quaternion.Euler(0f, angle + 90f, 4f);
                cover.Add(slab.transform);
            }

            drop = Empty("Player Drop", a, new Vector3(0f, 0f, -24f));

            var spawnerObject = new GameObject("Fodder Spawner");
            spawnerObject.transform.SetParent(a, false);
            spawner = spawnerObject.AddComponent<FodderSpawner>();
            var entries = new (GameObject prefab, float weight, int min, int max)[]
            {
                (DesertFodderBuilder.Load(DesertFodderBuilder.TickPath), 1.5f, 3, 4),
                (DesertFodderBuilder.Load(DesertFodderBuilder.ArmadilloPath), 1f, 1, 1),
                (DesertFodderBuilder.Load(DesertFodderBuilder.BeetlePath), 1f, 1, 2),
            };
            var spawnerData = new SerializedObject(spawner);
            SerializedProperty list = spawnerData.FindProperty("entries");
            list.arraySize = entries.Length;
            for (int i = 0; i < entries.Length; i++)
            {
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("prefab").objectReferenceValue = entries[i].prefab;
                entry.FindPropertyRelative("weight").floatValue = entries[i].weight;
                entry.FindPropertyRelative("minGroup").intValue = entries[i].min;
                entry.FindPropertyRelative("maxGroup").intValue = entries[i].max;
            }
            spawnerData.FindProperty("maxAlive").intValue = 7;
            spawnerData.FindProperty("waveInterval").floatValue = 7f;
            spawnerData.FindProperty("firstWaveDelay").floatValue = 2f;
            spawnerData.FindProperty("spawnRadiusMin").floatValue = 10f;
            spawnerData.FindProperty("spawnRadiusMax").floatValue = 26f;
            spawnerData.FindProperty("minPlayerDistance").floatValue = 7f;
            spawnerData.FindProperty("activationRange").floatValue = 0f;
            spawnerData.ApplyModifiedPropertiesWithoutUndo();
            spawner.enabled = false;

            var arena = root.AddComponent<CorewormArena>();
            var data = new SerializedObject(arena);
            data.FindProperty("centre").objectReferenceValue = centre;
            data.FindProperty("radius").floatValue = ArenaRadius;
            data.FindProperty("playerDrop").objectReferenceValue = drop;
            SetArray(data.FindProperty("floorTiles"), tiles.ToArray());
            SetArray(data.FindProperty("cover"), cover.ToArray());
            data.FindProperty("fodderSpawner").objectReferenceValue = spawner;
            data.FindProperty("warningMaterial").objectReferenceValue = DesertFodderBuilder.Unlit("BossWarning", new Color(1f, 0.3f, 0.15f));
            data.ApplyModifiedPropertiesWithoutUndo();
            return arena;
        }

        private static void EnsureFolders()
        {
            DesertFodderBuilder.EnsureDesertFolders();
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Prefabs/Enemies/Desert", "Boss");
            if (!AssetDatabase.IsValidFolder("Assets/Scenes/Bosses")) AssetDatabase.CreateFolder("Assets/Scenes", "Bosses");
        }
    }
}
