using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SignalLost.Combat;
using SignalLost.Enemies;
using SignalLost.Enemies.Desert;
using SignalLost.Scanner;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace SignalLost.Editor
{
    /// <summary>
    /// Builds the revamped desert bosses (docs/desert-bosses.md) from the models and animations made in Blender by
    /// tools/blender/desert_bosses.py: Briarwing, Cindermaw and Pitchclaw.
    ///
    /// For each boss, from <c>Assets/Prefabs/Enemies/Desert/Bosses/&lt;Boss&gt;/</c>:
    /// - sets up the FBX import (Generic rig, axis conversion baked, clips named and looped from the clip table),
    /// - recreates its flat-colour materials in URP from the clip table and remaps the model onto them,
    /// - makes an Animator Controller with a state per clip (the boss scripts cross-fade between them),
    /// - builds the prefab: the brain, NavMeshAgent and Health, bone hitboxes, the weak spot, the hidden ship part
    ///   that drops on defeat, and the effect meshes and materials,
    /// - and a test scene for each in Assets/Scenes/Bosses (skipped if it already exists).
    /// Rebuilding keeps the prefab's GUID, so scenes that place it keep working.
    /// </summary>
    public static class DesertDuelBossBuilder
    {
        public const string Root = "Assets/Prefabs/Enemies/Desert/Bosses";
        public const string BriarwingPath = Root + "/Briarwing/Briarwing.prefab";
        public const string CindermawPath = Root + "/Cindermaw/Cindermaw.prefab";
        public const string PitchclawPath = Root + "/Pitchclaw/Pitchclaw.prefab";
        private const string ShipPanelMeshPath = "Assets/Prefabs/Enviornment objects/TutorialCrashSite/Meshes/BentShipPanel.asset";
        private const string HullMaterialPath = "Assets/Prefabs/Enviornment objects/TutorialCrashSite/Materials/DamagedRocketHull.mat";
        private const string RocketEnginePath = "Assets/Prefabs/Enviornment objects/TutorialCrashSite/Upgrades/RocketEngine.prefab";
        private const string TankMeshPath = "Assets/Prefabs/Enemies/Desert/Meshes/SludgeTankShell.asset";

        private sealed class Hitbox
        {
            public string Bone;
            public float Radius;
            public Hitbox(string bone, float radius) { Bone = bone; Radius = radius; }
        }

        [MenuItem("Signal Lost/Desert Bosses/Build Briarwing, Cindermaw and Pitchclaw")]
        public static void BuildAll()
        {
            BuildBriarwing();
            BuildCindermaw();
            BuildPitchclaw();
            AssetDatabase.SaveAssets();
            Debug.Log("DESERT_DUEL_BOSSES_BUILT");
        }

        [MenuItem("Signal Lost/Desert Bosses/Create Test Scenes")]
        public static void CreateTestScenes()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(BriarwingPath) == null) BuildAll();
            int made = 0;
            if (DesertMiniBossBuilder.CreateArenaTestScene(DesertMiniBossBuilder.Theme.WreckField, BriarwingPath, "Briarwing", "Assets/Scenes/Bosses/BriarwingTest.unity")) made++;
            if (DesertMiniBossBuilder.CreateArenaTestScene(DesertMiniBossBuilder.Theme.MesaPass, CindermawPath, "Cindermaw", "Assets/Scenes/Bosses/CindermawTest.unity")) made++;
            if (DesertMiniBossBuilder.CreateArenaTestScene(DesertMiniBossBuilder.Theme.BuriedHull, PitchclawPath, "Pitchclaw", "Assets/Scenes/Bosses/PitchclawTest.unity")) made++;
            AssetDatabase.SaveAssets();
            Debug.Log($"DESERT_DUEL_TEST_SCENES ({made} new)");
        }

        // ---------- The three bosses ----------

        public static GameObject BuildBriarwing()
        {
            GameObject root = Begin("Briarwing", out Transform model, out BossClipTable table, out Dictionary<string, Material> mats);
            var boss = root.AddComponent<Briarwing>();
            Health throat = WeakSpot(model, "Head", "Jaw", 0.15f, 0.75f, "Briarwing (throat)");
            Transform part = HiddenPart(root, model, "Spine", "Wing panels", go =>
            {
                Mesh panel = AssetDatabase.LoadAssetAtPath<Mesh>(ShipPanelMeshPath);
                Material hull = AssetDatabase.LoadAssetAtPath<Material>(HullMaterialPath);
                AddMesh(go, panel, hull, Vector3.zero, Quaternion.identity, Vector3.one * 1.2f);
            });
            Hitboxes(model, new[]
            {
                new Hitbox("Hips", 1.35f), new Hitbox("Spine", 1.5f), new Hitbox("Chest", 1.4f), new Hitbox("Neck2", 0.8f),
                new Hitbox("Head", 0.95f), new Hitbox("Tail2", 0.7f), new Hitbox("Tail4", 0.5f), new Hitbox("ThighL", 0.8f),
                new Hitbox("ThighR", 0.8f), new Hitbox("WingForeL", 0.5f), new Hitbox("WingForeR", 0.5f),
            });
            var data = Common(boss, root, model, table, "Briarwing, the Thorn Wyvern", "Wing panels", part, 1300f, 3.4f, 7.5f,
                new[] { 0.65f, 0.3f }, new[] { "Briar", "Bristling", "Thornstorm" }, new[] { "Neck1", "Neck2", "Neck3", "Head" }, 2.2f);
            data.FindProperty("thornMaterial").objectReferenceValue = mats["BriarwingThorn"];
            data.FindProperty("throat").objectReferenceValue = throat;
            data.FindProperty("poise").floatValue = 150f;
            data.ApplyModifiedPropertiesWithoutUndo();
            return Save(root, BriarwingPath);
        }

        public static GameObject BuildCindermaw()
        {
            GameObject root = Begin("Cindermaw", out Transform model, out BossClipTable table, out _);
            var boss = root.AddComponent<Cindermaw>();
            Health thruster = WeakSpot(model, "Nozzle", "Spine", 0.3f, 0.95f, "Cindermaw (thruster)");
            Transform part = HiddenPart(root, model, "Spine", "Rocket thruster", go =>
            {
                var engine = AssetDatabase.LoadAssetAtPath<GameObject>(RocketEnginePath);
                if (engine == null) return;
                var copy = (GameObject)PrefabUtility.InstantiatePrefab(engine);
                PrefabUtility.UnpackPrefabInstance(copy, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                foreach (MonoBehaviour behaviour in copy.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(behaviour);
                foreach (Collider collider in copy.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                copy.transform.SetParent(go.transform, false);
            });
            Transform nozzle = FindDeep(model, "Nozzle");
            var lightObject = new GameObject("Thruster Glow");
            lightObject.transform.SetParent(nozzle, false);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.5f, 0.15f);
            light.range = 12f;
            light.intensity = 1.5f;
            light.shadows = LightShadows.None;
            Hitboxes(model, new[]
            {
                new Hitbox("Hips", 1.3f), new Hitbox("Spine", 1.4f), new Hitbox("Chest", 1.3f), new Hitbox("Neck2", 0.8f),
                new Hitbox("Head", 0.85f), new Hitbox("Tail2", 0.6f), new Hitbox("Club", 0.7f),
            });
            var data = Common(boss, root, model, table, "Cindermaw, the Ash Drake", "Rocket thruster", part, 1500f, 3.2f, 7.5f,
                new[] { 0.75f, 0.45f, 0.2f }, new[] { "Smoulder", "Smokescreen", "Afterburner", "Meltdown" }, new[] { "Neck1", "Neck2", "Head" }, 2.0f);
            data.FindProperty("flameMaterial").objectReferenceValue = FxFlame();
            data.FindProperty("thruster").objectReferenceValue = thruster;
            data.FindProperty("nozzleLight").objectReferenceValue = light;
            data.FindProperty("poise").floatValue = 170f;
            SetFire(data, "fire", new Color(1f, 0.85f, 0.75f), 0.65f, 0.8f);
            data.ApplyModifiedPropertiesWithoutUndo();
            return Save(root, CindermawPath);
        }

        public static GameObject BuildPitchclaw()
        {
            GameObject root = Begin("Pitchclaw", out Transform model, out BossClipTable table, out _);
            var boss = root.AddComponent<Pitchclaw>();
            Health tank = WeakSpot(model, "Abdomen", null, 0f, 1.0f, "Pitchclaw (fuel tank)", Vector3.up * 0.85f);
            Transform part = HiddenPart(root, model, "Abdomen", "Fuel tank", go =>
            {
                Mesh shell = AssetDatabase.LoadAssetAtPath<Mesh>(TankMeshPath);
                Material metal = DesertFodderBuilder.Lit("DuelFuelTank", new Color(0.86f, 0.84f, 0.8f), 0.5f, 0.55f);
                AddMesh(go, shell, metal, Vector3.zero, Quaternion.Euler(90f, 0f, 0f), Vector3.one * 1.4f);
            });
            Hitboxes(model, new[]
            {
                new Hitbox("Body", 1.45f), new Hitbox("Abdomen", 1.35f), new Hitbox("ClawL", 0.95f), new Hitbox("ClawR", 0.95f),
                new Hitbox("FingerL", 0.5f), new Hitbox("FingerR", 0.5f), new Hitbox("ForeL", 0.5f), new Hitbox("ForeR", 0.5f),
                new Hitbox("Tail2", 0.65f), new Hitbox("Tail4", 0.6f), new Hitbox("Tail6", 0.55f), new Hitbox("Tail8", 0.5f),
                new Hitbox("Tail10", 0.45f), new Hitbox("Tail12", 0.42f), new Hitbox("Stinger", 0.6f),
            });
            var data = Common(boss, root, model, table, "Pitchclaw, the Oilstone Scorpion", "Fuel tank", part, 1700f, 3.0f, 6.8f,
                new[] { 0.7f, 0.4f, 0.15f }, new[] { "Stone", "Spark", "Cracked", "Pyre" }, new string[0], 2.4f);
            data.FindProperty("oilMaterial").objectReferenceValue = DesertFodderBuilder.Lit("DuelOil", new Color(0.05f, 0.045f, 0.05f), 0.3f, 0.92f);
            data.FindProperty("poolMaterial").objectReferenceValue = OilPoolMaterial(Root + "/Pitchclaw/Materials");
            data.FindProperty("flameMaterial").objectReferenceValue = FxFlame();
            data.FindProperty("sparkMaterial").objectReferenceValue = DesertFodderBuilder.Unlit("BossSpark", new Color(1f, 0.9f, 0.5f));
            data.FindProperty("rockMaterial").objectReferenceValue = DesertFodderBuilder.Lit("DuelRockChip", new Color(0.62f, 0.44f, 0.28f), 0f, 0.1f);
            data.FindProperty("tank").objectReferenceValue = tank;
            data.FindProperty("poise").floatValue = 190f;
            SetFire(data, "fire", new Color(1f, 0.8f, 0.6f), 0.8f, 0.8f);
            data.ApplyModifiedPropertiesWithoutUndo();
            return Save(root, PitchclawPath);
        }

        // ---------- Shared steps ----------

        /// <summary>Imports the model, makes its materials and animator, and starts the prefab root with the model under "Visual".</summary>
        private static GameObject Begin(string name, out Transform model, out BossClipTable table, out Dictionary<string, Material> mats)
        {
            string folder = Root + "/" + name;
            string fbx = folder + "/" + name + ".fbx";
            var tableAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(folder + "/" + name + "Clips.json");
            if (tableAsset == null) throw new InvalidOperationException($"{name}Clips.json missing: run tools/blender/desert_bosses.py.");
            table = BossClipTable.Parse(tableAsset);
            mats = Materials(folder, table);
            Import(fbx, table, mats);
            AnimatorController controller = Controller(folder, name, fbx);

            var root = new GameObject(name);
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            instance.name = name + " Model";
            instance.transform.SetParent(visual, false);
            model = instance.transform;
            // Face +Z, whichever way the axis conversion left it: the head must be in front of the hips.
            Transform head = FindDeep(model, "Head") ?? FindDeep(model, "Mouth"), hips = FindDeep(model, "Hips") ?? FindDeep(model, "Abdomen");
            if (head != null && hips != null && head.position.z < hips.position.z) model.localRotation = Quaternion.Euler(0f, 180f, 0f) * model.localRotation;
            Bounds bounds = Bounds(model);
            model.localPosition += Vector3.up * -bounds.min.y; // feet on the ground

            var animator = instance.GetComponent<Animator>() ?? instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            foreach (SkinnedMeshRenderer skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                skin.updateWhenOffscreen = true;
                skin.shadowCastingMode = ShadowCastingMode.On;
            }
            return root;
        }

        private static Dictionary<string, Material> Materials(string folder, BossClipTable table)
        {
            string matFolder = folder + "/Materials";
            if (!AssetDatabase.IsValidFolder(matFolder)) AssetDatabase.CreateFolder(folder, "Materials");
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            var result = new Dictionary<string, Material>();
            foreach (BossClipTable.MaterialInfo info in table.materials)
            {
                string path = matFolder + "/" + info.name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(lit) { name = info.name };
                    AssetDatabase.CreateAsset(material, path);
                }
                // The cracks flow: Pitchclaw's fuel and Cindermaw's molten seams use the flowing-oil shader.
                if (info.key == "Oil" || info.key == "Ember")
                {
                    FlowingCrack(material, info.key == "Ember");
                    result[info.name] = material;
                    continue;
                }
                if (material.shader != lit) material.shader = lit;
                // The table's colours are linear (Blender's); Unity material colours are set in gamma space.
                var color = new Color(info.color[0], info.color[1], info.color[2]).gamma;
                material.SetColor("_BaseColor", color);
                material.SetFloat("_Metallic", info.metallic);
                material.SetFloat("_Smoothness", Mathf.Clamp(info.smoothness, 0f, 0.95f));
                // Every material can glow (the boss scripts light thorns, embers and oil up by phase).
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                Color emission = new Color(info.emission[0], info.emission[1], info.emission[2]) * Mathf.Min(info.emissionStrength, 3f) * 0.6f;
                material.SetColor("_EmissionColor", info.emissionStrength > 0f ? emission : Color.black);
                EditorUtility.SetDirty(material);
                result[info.name] = material;
            }
            return result;
        }

        public const string FlowingOilShader = "Signal Lost/Flowing Oil";

        /// <summary>The flowing-oil shader set up for fuel (dark, amber pulses) or for lava (Cindermaw's molten seams).</summary>
        private static void FlowingCrack(Material material, bool lava)
        {
            Shader shader = Shader.Find(FlowingOilShader);
            if (shader == null) throw new InvalidOperationException("Shader " + FlowingOilShader + " missing.");
            material.shader = shader;
            material.SetColor("_BaseColor", lava ? new Color(0.22f, 0.05f, 0.02f) : new Color(0.03f, 0.025f, 0.03f));
            material.SetColor("_SheenColor", lava ? new Color(0.6f, 0.2f, 0.05f) : new Color(0.32f, 0.28f, 0.5f));
            material.SetColor("_GlowColor", lava ? new Color(1f, 0.38f, 0.08f) : new Color(1f, 0.42f, 0.1f));
            material.SetFloat("_Glow", lava ? 1.6f : 1f);
            material.SetFloat("_FlowSpeed", lava ? 1.4f : 2.2f);
            material.SetFloat("_PulseSpacing", lava ? 1.8f : 2.6f);
            material.SetFloat("_PulseSharpness", lava ? 3f : 5f);
            material.SetFloat("_Swell", lava ? 0.02f : 0.05f);
            material.SetFloat("_Smoothness", lava ? 0.5f : 0.9f);
            EditorUtility.SetDirty(material);
        }

        /// <summary>The ground pools of Pitchclaw's fuel: the flowing shader, ripples running out from the middle.</summary>
        private static Material OilPoolMaterial(string folder)
        {
            string path = folder + "/PitchclawFuelPool.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find(FlowingOilShader)) { name = "PitchclawFuelPool" };
                AssetDatabase.CreateAsset(material, path);
            }
            FlowingCrack(material, false);
            material.SetFloat("_Glow", 0.12f);
            material.SetFloat("_PulseSpacing", 0.9f);
            material.SetFloat("_FlowSpeed", 0.5f);
            material.SetFloat("_Swell", 0f);
            material.SetFloat("_Facets", 0.3f);
            return material;
        }

        private static void Import(string fbx, BossClipTable table, Dictionary<string, Material> mats)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(fbx);
            if (importer == null) throw new InvalidOperationException(fbx + " missing.");
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.bakeAxisConversion = true;
            importer.importAnimation = true;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.animationCompression = ModelImporterAnimationCompression.KeyframeReduction;
            foreach (KeyValuePair<string, Material> pair in mats)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key), pair.Value);
            importer.SaveAndReimport();

            // Clips: one per Blender action, named without the armature prefix, looped as the table says.
            var clips = new List<ModelImporterClipAnimation>();
            foreach (ModelImporterClipAnimation clip in importer.defaultClipAnimations)
            {
                string name = ClipName(clip.takeName, Path.GetFileNameWithoutExtension(fbx));
                if (!table.Has(name)) continue;
                clip.name = name;
                BossClipTable.Clip info = table.clips.First(c => c.name == name);
                clip.loopTime = info.loop;
                clip.loopPose = false;
                clip.lockRootRotation = clip.lockRootHeightY = clip.lockRootPositionXZ = false;
                clips.Add(clip);
            }
            Debug.Log($"{Path.GetFileName(fbx)} takes: {string.Join(", ", importer.defaultClipAnimations.Select(c => c.takeName))}");
            if (clips.Count == 0) throw new InvalidOperationException(fbx + " has no animation takes matching its clip table.");
            importer.clipAnimations = clips.ToArray();
            importer.SaveAndReimport();
        }

        /// <summary>"BriarwingRig|Briarwing_Idle" (or an older "Rig|Idle.001") to "Idle".</summary>
        internal static string ClipName(string take, string boss)
        {
            string name = take.Contains("|") ? take.Substring(take.LastIndexOf('|') + 1) : take;
            if (name.StartsWith(boss + "_")) name = name.Substring(boss.Length + 1);
            return System.Text.RegularExpressions.Regex.Replace(name, @"\.\d+$", "");
        }

        private static AnimatorController Controller(string folder, string name, string fbx)
        {
            string path = folder + "/" + name + ".controller";
            AssetDatabase.DeleteAsset(path);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
            foreach (AnimationClip clip in clips)
            {
                AnimatorState state = machine.AddState(clip.name);
                state.motion = clip;
                state.writeDefaultValues = false;
                if (clip.name == "Idle") machine.defaultState = state;
            }
            EditorUtility.SetDirty(controller);
            Debug.Log($"{name}: {clips.Length} clips in the animator ({string.Join(", ", clips.Select(c => c.name))})");
            return controller;
        }

        private static SerializedObject Common(DuelBoss boss, GameObject root, Transform model, BossClipTable table, string displayName, string partName,
            Transform part, float health, float walk, float run, float[] phaseAt, string[] titles, string[] lookBones, float agentRadius)
        {
            var agent = root.GetComponent<NavMeshAgent>();
            agent.enabled = false; // switched on in Start, once the NavMesh exists
            agent.radius = agentRadius;
            agent.height = 4f;
            agent.speed = walk;
            agent.acceleration = 24f;
            agent.angularSpeed = 240f;
            agent.stoppingDistance = 0.5f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
            var healthData = new SerializedObject(root.GetComponent<Health>());
            healthData.FindProperty("maxHealth").floatValue = health;
            healthData.ApplyModifiedPropertiesWithoutUndo();
            root.AddComponent<EnemySignalSource>();

            string folder = Root + "/" + boss.GetType().Name;
            var data = new SerializedObject(boss);
            data.FindProperty("visual").objectReferenceValue = root.transform.Find("Visual");
            data.FindProperty("displayName").stringValue = displayName;
            data.FindProperty("partName").stringValue = partName;
            data.FindProperty("hitKnockback").floatValue = 0f;
            data.FindProperty("corpseTime").floatValue = 7f;
            data.FindProperty("flashTime").floatValue = 0.05f;
            data.FindProperty("arenaRadius").floatValue = 24f;
            data.FindProperty("fodderWhileEngaged").boolValue = false; // pure duels (Lawson, 11-10)
            data.FindProperty("shipPart").objectReferenceValue = part;
            data.FindProperty("clipTable").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(folder + "/" + boss.GetType().Name + "Clips.json");
            data.FindProperty("animator").objectReferenceValue = model.GetComponent<Animator>();
            SetFloats(data.FindProperty("phaseAt"), phaseAt);
            SerializedProperty titleList = data.FindProperty("phaseTitles");
            titleList.arraySize = titles.Length;
            for (int i = 0; i < titles.Length; i++) titleList.GetArrayElementAtIndex(i).stringValue = titles[i];
            data.FindProperty("walkSpeed").floatValue = walk;
            data.FindProperty("runSpeed").floatValue = run;
            SerializedProperty look = data.FindProperty("lookBones");
            var found = lookBones.Select(b => FindDeep(model, b)).Where(t => t != null).ToArray();
            look.arraySize = found.Length;
            for (int i = 0; i < found.Length; i++) look.GetArrayElementAtIndex(i).objectReferenceValue = found[i];
            data.FindProperty("markerMaterial").objectReferenceValue = DesertFodderBuilder.Unlit("BossWarning", new Color(1f, 0.3f, 0.15f));
            data.FindProperty("shadowMaterial").objectReferenceValue = DesertFodderBuilder.Unlit("DuelShadow", new Color(0.12f, 0.1f, 0.09f));
            data.FindProperty("puffMesh").objectReferenceValue = DesertFodderBuilder.SaveDesertMesh(LowPolyMesh.Icosphere("DesertGem", 0));
            data.FindProperty("spikeMesh").objectReferenceValue = DesertFodderBuilder.SaveDesertMesh(LowPolyMesh.TaperedPrism("DesertSpike", 5, 0f));
            return data;
        }

        private static void SetFloats(SerializedProperty list, float[] values)
        {
            list.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) list.GetArrayElementAtIndex(i).floatValue = values[i];
        }

        private static void SetFire(SerializedObject data, string field, Color tint, float smoke, float fierceness)
        {
            SerializedProperty fire = data.FindProperty(field);
            fire.FindPropertyRelative("tint").colorValue = tint;
            fire.FindPropertyRelative("smoke").floatValue = smoke;
            fire.FindPropertyRelative("fierceness").floatValue = fierceness;
        }

        private static Material FxFlame() => DesertFodderBuilder.Unlit("BossFlame", new Color(1f, 0.55f, 0.12f));

        /// <summary>Sphere hitboxes riding on bones: bolts and the laser hit these and damage the boss.</summary>
        private static void Hitboxes(Transform model, IEnumerable<Hitbox> boxes)
        {
            foreach (Hitbox box in boxes)
            {
                Transform bone = FindDeep(model, box.Bone);
                if (bone == null) throw new InvalidOperationException("No bone " + box.Bone);
                var go = new GameObject("Hitbox " + box.Bone);
                go.transform.SetParent(bone, false);
                var sphere = go.AddComponent<SphereCollider>();
                float scale = Mathf.Max(0.0001f, bone.lossyScale.x);
                sphere.radius = box.Radius / scale;
            }
        }

        /// <summary>A weak spot: its own collider and a never-emptying Health whose hits the boss relays (with a bonus).</summary>
        private static Health WeakSpot(Transform model, string bone, string toward, float t, float radius, string name, Vector3 offset = default)
        {
            Transform parent = FindDeep(model, bone);
            Vector3 at = parent.position;
            if (toward != null) at = Vector3.Lerp(at, FindDeep(model, toward).position, t);
            var go = new GameObject(name);
            go.transform.SetParent(parent, true);
            go.transform.position = at + offset;
            var sphere = go.AddComponent<SphereCollider>();
            sphere.radius = radius / Mathf.Max(0.0001f, go.transform.lossyScale.x);
            var health = go.AddComponent<Health>();
            var data = new SerializedObject(health);
            data.FindProperty("maxHealth").floatValue = 100000f; // restored after every hit
            data.ApplyModifiedPropertiesWithoutUndo();
            return health;
        }

        /// <summary>The stolen part, hidden inside the boss until it is defeated (then it drops as a pickup).</summary>
        private static Transform HiddenPart(GameObject root, Transform model, string bone, string label, Action<GameObject> build)
        {
            var go = new GameObject(label);
            go.transform.SetParent(root.transform, false);
            Transform at = FindDeep(model, bone);
            go.transform.position = at != null ? at.position : root.transform.position + Vector3.up * 2f;
            build(go);
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            return go.transform;
        }

        private static void AddMesh(GameObject go, Mesh mesh, Material material, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            var child = new GameObject("Mesh");
            child.transform.SetParent(go.transform, false);
            child.transform.SetLocalPositionAndRotation(position, rotation);
            child.transform.localScale = scale;
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            child.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static Bounds Bounds(Transform model)
        {
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            var b = new Bounds(model.position, Vector3.zero);
            foreach (Renderer r in renderers)
            {
                if (r is SkinnedMeshRenderer skin && skin.sharedMesh != null)
                {
                    var baked = new Mesh();
                    skin.BakeMesh(baked, true);
                    foreach (Vector3 v in baked.vertices) b.Encapsulate(skin.transform.TransformPoint(v));
                    Object.DestroyImmediate(baked);
                }
                else b.Encapsulate(r.bounds);
            }
            return b;
        }

        internal static Transform FindDeep(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            foreach (Transform child in parent)
            {
                Transform hit = FindDeep(child, name);
                if (hit != null) return hit;
            }
            return null;
        }

        private static GameObject Save(GameObject root, string path)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            if (prefab == null) throw new InvalidOperationException("Could not save " + path);
            return prefab;
        }
    }
}
