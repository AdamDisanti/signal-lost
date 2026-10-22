using System;
using SignalLost.Combat;
using SignalLost.Enemies;
using SignalLost.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SignalLost.Editor
{
    /// <summary>
    /// Builds the player HUD prefab (Assets/Prefabs/UI/PlayerHUD.prefab) and HudTest.unity, a copy of the enemy test
    /// arena with the real HUD instead of the debug one, plus a test driver that fakes the scanner and objectives.
    /// Like the other builders it refuses to overwrite the scene. After the first build, edit the prefab in Unity:
    /// "Rebuild Player HUD Prefab" throws those edits away and recreates it from this script.
    /// </summary>
    public static class HudTestBuilder
    {
        public const string ScenePath = "Assets/Scenes/Initial Test Scenes/HudTest.unity";
        public const string HudPrefabPath = UiFolder + "/PlayerHUD.prefab";
        private const string UiFolder = "Assets/Prefabs/UI";
        private const int UiLayer = 5;

        private static readonly Color Panel = new Color(0f, 0f, 0f, 0.45f);
        private static readonly Color BarBack = new Color(0f, 0f, 0f, 0.6f);
        private static readonly Color Accent = new Color(0.3f, 0.85f, 1f);
        private static readonly Color Muted = new Color(0.75f, 0.9f, 1f, 0.8f);

        private static Font font;

        [MenuItem("Signal Lost/Create HUD Test Scene")]
        public static void Create()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                throw new InvalidOperationException("HUD test scene already exists. Edit it in Unity instead.");
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyTestBuilder.PlayerPrefabPath);
            var input = AssetDatabase.LoadAssetAtPath<InputActionAsset>(EnemyTestBuilder.InputPath);
            var beam = AssetDatabase.LoadAssetAtPath<Material>(EnemyTestBuilder.BeamMaterialPath);
            var crab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyTestBuilder.CrabPrefabPath);
            if (playerPrefab == null || input == null || beam == null || crab == null)
                throw new InvalidOperationException("Player prefab, input actions, LaserBeam material or HermitCrab prefab missing. Is your branch up to date with main?");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject hudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath) ?? BuildHudPrefab();
            EnemyTestBuilder.BuildArena();
            EnemyTestBuilder.PlaceCrab(crab, "Hermit Crab A", new Vector3(0f, 0f, 5f), 180f);
            EnemyTestBuilder.PlaceCrab(crab, "Hermit Crab B", new Vector3(-9f, 0f, 11f), 150f);
            EnemyTestBuilder.BuildPlayer(playerPrefab, input, beam);
            EnemyTestBuilder.BuildLighting();

            // Swap the debug HUD for the real one.
            MiningLaser laser = Object.FindAnyObjectByType<MiningLaser>();
            GameObject player = laser.gameObject;
            var debugHud = player.GetComponent<CombatDebugHud>();
            if (debugHud != null) Object.DestroyImmediate(debugHud);

            var hud = (GameObject)PrefabUtility.InstantiatePrefab(hudPrefab);
            var hudData = new SerializedObject(hud.GetComponent<PlayerHud>());
            hudData.FindProperty("playerHealth").objectReferenceValue = player.GetComponent<Health>();
            hudData.FindProperty("laser").objectReferenceValue = laser;
            hudData.FindProperty("viewpoint").objectReferenceValue = player.GetComponentInChildren<Camera>().transform;
            hudData.ApplyModifiedPropertiesWithoutUndo();

            // Stand-in for the scanner's target, on top of the platform.
            GameObject beacon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            beacon.name = "Signal Beacon (test)";
            Object.DestroyImmediate(beacon.GetComponent<Collider>());
            beacon.transform.position = new Vector3(11f, 1.6f, 10f);
            beacon.transform.localScale = Vector3.one * 0.6f;
            beacon.GetComponent<Renderer>().sharedMaterial = beam;

            var driver = new GameObject("HUD Test Driver").AddComponent<HudTestDriver>();
            var driverData = new SerializedObject(driver);
            driverData.FindProperty("signalBeacon").objectReferenceValue = beacon.transform;
            driverData.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("HUD_TEST_SCENE_CREATED_AND_VALIDATED");
        }

        [MenuItem("Signal Lost/Rebuild Player HUD Prefab")]
        public static void Rebuild()
        {
            if (!Application.isBatchMode && !EditorUtility.DisplayDialog("Rebuild player HUD prefab",
                    "This recreates PlayerHUD.prefab from the builder script, replacing any edits made to it in Unity. " +
                    "Scenes keep their HUD instance. Continue?", "Rebuild", "Cancel"))
                return;
            BuildHudPrefab();
            AssetDatabase.SaveAssets();
            Debug.Log("PLAYER_HUD_PREFAB_REBUILT");
        }

        public static void Validate()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var huds = EnemyTestBuilder.All<PlayerHud>();
            EnemyTestBuilder.Require(huds.Count == 1, "Expected one PlayerHud.");
            var data = new SerializedObject(huds[0]);
            foreach (string field in new[] { "playerHealth", "laser", "viewpoint", "healthFill", "healthText", "crosshair",
                         "chargeGroup", "targetGroup", "scannerNeedle", "scannerStatus", "objectiveText", "centerMessage" })
                EnemyTestBuilder.Require(data.FindProperty(field).objectReferenceValue != null, "PlayerHud reference missing: " + field);
            EnemyTestBuilder.Require(data.FindProperty("signalPips").arraySize == 5, "Expected five signal pips.");
            EnemyTestBuilder.Require(EnemyTestBuilder.Count<CombatDebugHud>() == 0, "Debug HUD should be removed.");
            EnemyTestBuilder.Require(EnemyTestBuilder.Count<HermitCrab>() == 2, "Expected two hermit crabs.");
            var drivers = EnemyTestBuilder.All<HudTestDriver>();
            EnemyTestBuilder.Require(drivers.Count == 1 &&
                new SerializedObject(drivers[0]).FindProperty("signalBeacon").objectReferenceValue != null, "HUD test driver or beacon missing.");
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                    EnemyTestBuilder.Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject) == 0, "Missing script: " + item.name);
            Debug.Log("HUD_TEST_SCENE_VALIDATED");
        }

        // ---------- HUD prefab ----------

        private static GameObject BuildHudPrefab()
        {
            EnsureFolder("Assets/Prefabs");
            EnsureFolder(UiFolder);
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Sprite circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

            var root = new GameObject("PlayerHUD", typeof(RectTransform)) { layer = UiLayer };
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var hud = root.AddComponent<PlayerHud>();
            Transform r = root.transform;

            // Red flash over the whole screen when hurt (behind everything else).
            Image hurtFlash = Box("Hurt Flash", r, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Color(0.8f, 0f, 0f, 0f));
            Stretch(hurtFlash.rectTransform);

            // ----- Health: top left -----
            RectTransform health = Node("Health", r, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -36f), new Vector2(440f, 68f));
            Label("Label", health, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(200f, 28f), "HEALTH", 22, TextAnchor.MiddleLeft, Muted);
            Image healthBar = Box("Bar", health, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -32f), new Vector2(400f, 32f), BarBack);
            RectTransform trail = Fill("Damage Trail", healthBar.transform, new Color(1f, 1f, 1f, 0.55f), out _);
            RectTransform healthFill = Fill("Fill", healthBar.transform, new Color(0.45f, 0.9f, 0.45f), out Image healthFillImage);
            RectTransform overheal = Fill("Overheal", healthBar.transform, new Color(1f, 0.82f, 0.3f), out _);
            overheal.anchorMax = new Vector2(0f, 1f);
            Text healthText = Label("Value", healthBar.transform, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, "100 / 100", 22, TextAnchor.MiddleRight, Color.white);
            Stretch(healthText.rectTransform, 10f);

            // ----- Scanner: top center -----
            RectTransform scanner = Node("Scanner", r, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(220f, 156f));
            Image ring = Box("Ring", scanner, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(92f, 92f), Panel, circle);
            Box("Forward Tick", ring.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -3f), new Vector2(3f, 10f), new Color(1f, 1f, 1f, 0.5f));
            RectTransform needleGroup = Node("Needle", ring.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(92f, 92f));
            Box("Stem", needleGroup, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(4f, 30f), Accent);
            Image tip = Box("Tip", needleGroup, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 32f), new Vector2(13f, 13f), Accent);
            tip.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Box("Hub", ring.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 10f), Color.white, circle);
            var pips = new Image[5];
            for (int i = 0; i < pips.Length; i++)
                pips[i] = Box("Signal Pip " + (i + 1), scanner, new Vector2(0.5f, 1f), new Vector2(0.5f, 0f),
                    new Vector2((i - 2) * 18f, -118f), new Vector2(12f, 6f + i * 4f), new Color(1f, 1f, 1f, 0.15f));
            Text scannerStatus = Label("Status", scanner, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -122f), new Vector2(220f, 28f), "NO SIGNAL", 20, TextAnchor.MiddleCenter, Muted);

            // ----- Objective: top right -----
            Image objective = Box("Objective", r, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -36f), new Vector2(500f, 86f), new Color(0f, 0f, 0f, 0.35f));
            Label("Label", objective.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -8f), new Vector2(300f, 24f), "OBJECTIVE", 18, TextAnchor.MiddleLeft, Accent);
            Text objectiveText = Label("Text", objective.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -36f), new Vector2(470f, 38f), "Find the signal", 27, TextAnchor.MiddleLeft, Color.white);

            // ----- Center: crosshair, target readout above, charge bar below -----
            RectTransform crosshair = Node("Crosshair", r, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 40f));
            Image[] crossParts =
            {
                Box("Left", crosshair, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-11f, 0f), new Vector2(10f, 2f), Color.white),
                Box("Right", crosshair, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(11f, 0f), new Vector2(10f, 2f), Color.white),
                Box("Top", crosshair, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 11f), new Vector2(2f, 10f), Color.white),
                Box("Bottom", crosshair, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -11f), new Vector2(2f, 10f), Color.white),
                Box("Dot", crosshair, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2f, 2f), Color.white),
            };

            RectTransform target = Node("Target", r, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(280f, 46f));
            Text targetName = Label("Name", target, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(280f, 30f), "Enemy", 22, TextAnchor.MiddleCenter, Color.white);
            Image targetBar = Box("Bar", target, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(160f, 6f), BarBack);
            RectTransform targetFill = Fill("Fill", targetBar.transform, new Color(0.95f, 0.3f, 0.25f), out _);

            RectTransform charge = Node("Laser Charge", r, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, -48f), new Vector2(240f, 40f));
            Image chargeBar = Box("Bar", charge, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(200f, 8f), BarBack);
            RectTransform chargeFill = Fill("Fill", chargeBar.transform, Accent, out Image chargeFillImage);
            Text chargeText = Label("State", charge, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(240f, 26f), "CHARGING", 18, TextAnchor.MiddleCenter, Muted);

            Text centerMessage = Label("Center Message", r, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 90f), new Vector2(1000f, 140f), "", 52, TextAnchor.MiddleCenter, Color.white);
            centerMessage.gameObject.SetActive(false);
            target.gameObject.SetActive(false);
            charge.gameObject.SetActive(false);
            needleGroup.gameObject.SetActive(false);

            // Hook every piece up to the PlayerHud script.
            var data = new SerializedObject(hud);
            Set(data, "healthFill", healthFill);
            Set(data, "healthTrail", trail);
            Set(data, "overhealFill", overheal);
            Set(data, "healthFillImage", healthFillImage);
            Set(data, "healthText", healthText);
            Set(data, "hurtFlash", hurtFlash);
            Set(data, "crosshair", crosshair);
            SetArray(data.FindProperty("crosshairParts"), crossParts);
            Set(data, "chargeGroup", charge.gameObject);
            Set(data, "chargeFill", chargeFill);
            Set(data, "chargeFillImage", chargeFillImage);
            Set(data, "chargeText", chargeText);
            Set(data, "targetGroup", target.gameObject);
            Set(data, "targetName", targetName);
            Set(data, "targetFill", targetFill);
            Set(data, "scannerNeedleGroup", needleGroup.gameObject);
            Set(data, "scannerNeedle", needleGroup);
            SetArray(data.FindProperty("signalPips"), pips);
            Set(data, "scannerStatus", scannerStatus);
            Set(data, "objectiveGroup", objective.gameObject);
            Set(data, "objectiveText", objectiveText);
            Set(data, "centerMessage", centerMessage);
            data.ApplyModifiedPropertiesWithoutUndo();

            return EnemyTestBuilder.SavePrefab(root, HudPrefabPath);
        }

        // ---------- UI helpers ----------

        private static RectTransform Node(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = UiLayer };
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static Image Box(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size, Color color, Sprite sprite = null)
        {
            var image = Node(name, parent, anchor, pivot, position, size).gameObject.AddComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>A bar fill: stretches over its parent from the left; PlayerHud sets how far right it reaches.</summary>
        private static RectTransform Fill(string name, Transform bar, Color color, out Image image)
        {
            image = Box(name, bar, Vector2.zero, new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero, color);
            RectTransform rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static Text Label(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size,
            string text, int fontSize, TextAnchor alignment, Color color)
        {
            var label = Node(name, parent, anchor, pivot, position, size).gameObject.AddComponent<Text>();
            label.font = font;
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = FontStyle.Bold;
            label.alignment = alignment;
            label.color = color;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            var shadow = label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
            return label;
        }

        private static void Stretch(RectTransform rect, float horizontalPadding = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(horizontalPadding, 0f);
            rect.offsetMax = new Vector2(-horizontalPadding, 0f);
        }

        private static void Set(SerializedObject data, string field, Object value)
        {
            SerializedProperty property = data.FindProperty(field);
            if (property == null) throw new InvalidOperationException("PlayerHud has no field " + field);
            property.objectReferenceValue = value;
        }

        private static void SetArray(SerializedProperty property, Object[] items)
        {
            property.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
