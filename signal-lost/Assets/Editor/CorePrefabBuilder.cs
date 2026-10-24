using System;
using SignalLost.Combat;
using SignalLost.Core;
using SignalLost.Inventory;
using SignalLost.Player;
using SignalLost.Scanner;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SignalLost.Editor
{
    /// <summary>
    /// Builds the shared prefabs every playable scene uses (Prefabs/Core):
    /// <list type="bullet">
    /// <item>Player: a variant of FirstPersonPlayer with health, the tuned mining laser, the scanner, the flask,
    /// inventory, hands, interaction, respawn and the wake-up effect. This is the game's baseline tuning.</item>
    /// <item>GameSystems: the HUD, dialogue, quest tracker, inventory and pause menu.</item>
    /// <item>PlayerSpawnPoint: where the player comes back after dying.</item>
    /// </list>
    /// Rebuilding overwrites them in place (same GUIDs), so tune values here or in the prefab and rebuild only on purpose.
    /// Menu: Signal Lost > Core > Build Player &amp; Game Systems Prefabs.
    /// </summary>
    internal static class CorePrefabBuilder
    {
        public const string Folder = "Assets/Prefabs/Core";
        public const string PlayerPath = Folder + "/Player.prefab";
        public const string SystemsPath = Folder + "/GameSystems.prefab";
        public const string SpawnPath = Folder + "/PlayerSpawnPoint.prefab";

        private const string BasePlayer = "Assets/Prefabs/Player/FirstPersonPlayer.prefab";
        private const string Input = "Assets/InputSystem_Actions.inputactions";
        private const string LaserModel = "Assets/Prefabs/Enviornment objects/TutorialCrashSite/Upgrades/MiningLaser.prefab";
        private const string BeamMaterial = "Assets/Materials/Combat/LaserBeam.mat";
        private const string ScannerRig = "Assets/Prefabs/Player/Scanner/ScannerRig.prefab";
        private const string FlaskModel = "Assets/Prefabs/Enviornment objects/GrayboxProps/AlcoholFlask.prefab";
        private const string DrunkProfile = "Assets/Prefabs/Enviornment objects/TutorialCrashSite/Gameplay/TutorialDrunkProfile.asset";

        [MenuItem("Signal Lost/Core/Build Player & Game Systems Prefabs")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Prefabs", "Core");
            BuildPlayer();
            BuildSimple(SystemsPath, "GameSystems", go => go.AddComponent<GameSystems>());
            BuildSimple(SpawnPath, "PlayerSpawnPoint", go => go.AddComponent<PlayerSpawnPoint>());
            AssetDatabase.SaveAssets();
            Debug.Log("CORE_PREFABS_BUILT");
        }

        private static void BuildPlayer()
        {
            var input = Load<UnityEngine.InputSystem.InputActionAsset>(Input);
            var player = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(BasePlayer));
            try
            {
                player.name = "Player";
                Camera view = player.GetComponentInChildren<Camera>(true);
                view.GetUniversalAdditionalCameraData().renderPostProcessing = true;

                Set(Add<Health>(player), "maxHealth", 100f);

                // Mining laser: the starting tuning (meeting notes 10-20 and 10-22). Hands stay still, so no sway/bob.
                var muzzle = new GameObject("Laser Muzzle").transform;
                muzzle.SetParent(view.transform, false);
                muzzle.localPosition = new Vector3(0.25f, -0.2f, 0.5f);
                var beam = muzzle.gameObject.AddComponent<LineRenderer>();
                beam.sharedMaterial = Load<Material>(BeamMaterial);
                beam.startWidth = 0.05f;
                beam.endWidth = 0.03f;
                beam.positionCount = 2;
                beam.shadowCastingMode = ShadowCastingMode.Off;
                beam.receiveShadows = false;
                beam.enabled = false;
                var laser = Add<MiningLaser>(player);
                var laserData = new SerializedObject(laser);
                laserData.FindProperty("inputActions").objectReferenceValue = input;
                laserData.FindProperty("aimOrigin").objectReferenceValue = view.transform;
                laserData.FindProperty("muzzle").objectReferenceValue = muzzle;
                laserData.FindProperty("beam").objectReferenceValue = beam;
                laserData.FindProperty("heldModel").objectReferenceValue = Load<GameObject>(LaserModel);
                Array(laserData, "comboDamage", 20f, 20f, 40f);
                Array(laserData, "comboDuration", 0.35f, 0.35f, 0.6f);
                laserData.FindProperty("meleeRange").floatValue = 2.5f;
                laserData.FindProperty("comboCooldown").floatValue = 2f;
                laserData.FindProperty("finisherStun").floatValue = 1f;
                laserData.FindProperty("energyColor").colorValue = new Color(0.4f, 0.9f, 1f);
                laserData.FindProperty("swayStrength").floatValue = 0f;
                laserData.FindProperty("bobStrength").floatValue = 0f;
                laserData.FindProperty("minShotDamage").floatValue = 10f;
                laserData.FindProperty("maxShotDamage").floatValue = 70f;
                laserData.FindProperty("shotCooldown").floatValue = 6f;
                laserData.FindProperty("tapShotCooldown").floatValue = 1f;
                laserData.FindProperty("fullChargeBlastRadius").floatValue = 4f;
                laserData.ApplyModifiedPropertiesWithoutUndo();

                var rig = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(ScannerRig), view.transform);
                rig.transform.localPosition = Vector3.zero;
                rig.transform.localRotation = Quaternion.identity;
                // Held a little higher than in the scanner test scene, so its screen clears the HUD status bar.
                rig.transform.Find("ScannerVisualRoot").localPosition = new Vector3(0.3f, -0.19f, 0.6f);

                Add<PlayerInventory>(player);
                var gear = Add<PlayerGear>(player);
                Set(gear, "laser", laser);
                Set(gear, "scanner", rig.GetComponent<ScannerToggle>());

                var vision = new GameObject("Drunk Vision");
                vision.transform.SetParent(player.transform, false);
                var volume = vision.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 10f;
                volume.weight = 0f;
                volume.sharedProfile = Load<VolumeProfile>(DrunkProfile);
                var flask = Add<PlayerFlask>(player);
                Set(flask, "inputActions", input);
                Set(flask, "drunkVolume", volume);

                Add<PlayerLife>(player);
                var interactor = Add<PlayerInteractor>(player);
                Set(interactor, "inputActions", input);
                Set(interactor, "view", view);
                Set(Add<FirstPersonHands>(player), "flaskModel", Load<GameObject>(FlaskModel));
                Set(Add<WakeUpSequence>(player), "cameraPivot", view.transform);
                Add<PlayerUpgrades>(player);

                PrefabUtility.SaveAsPrefabAsset(player, PlayerPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(player);
            }
        }

        private static void BuildSimple(string path, string name, Action<GameObject> setup)
        {
            var go = new GameObject(name);
            try
            {
                setup(go);
                PrefabUtility.SaveAsPrefabAsset(go, path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static T Add<T>(GameObject go) where T : Component
        {
            T existing = go.GetComponent<T>();
            return existing != null ? existing : go.AddComponent<T>();
        }

        private static T Load<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new InvalidOperationException("Missing asset: " + path);
            return asset;
        }

        private static void Set(UnityEngine.Object target, string field, object value)
        {
            var data = new SerializedObject(target);
            SerializedProperty property = data.FindProperty(field);
            if (value is UnityEngine.Object reference) property.objectReferenceValue = reference;
            else if (value is float number) property.floatValue = number;
            else throw new ArgumentException("Unsupported value for " + field);
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Array(SerializedObject data, string field, params float[] values)
        {
            SerializedProperty list = data.FindProperty(field);
            list.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) list.GetArrayElementAtIndex(i).floatValue = values[i];
        }
    }
}
