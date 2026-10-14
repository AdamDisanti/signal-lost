using System;
using System.IO;
using SignalLost.Combat;
using SignalLost.Player;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace SignalLost.Editor
{
    public static class TutorialGameplayBuilder
    {
        public const string Root = TutorialCrashSiteBuilder.AssetRoot + "/Gameplay";
        private const string Props = "Assets/Prefabs/Enviornment objects/GrayboxProps/";

        [MenuItem("Signal Lost/Tutorial Crash Site/Add Tutorial Gameplay")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(TutorialCrashSiteBuilder.ScenePath);
            if (UnityEngine.Object.FindObjectsByType<TutorialPlayer>().Length != 0)
                throw new InvalidOperationException("Tutorial gameplay already exists; edit the scene normally.");
            Directory.CreateDirectory("Logs");
            File.Copy(TutorialCrashSiteBuilder.ScenePath, "Logs/TutorialBeforeGameplay.unity", true);
            var players = UnityEngine.Object.FindObjectsByType<FirstPersonController>();
            if (players.Length != 1) throw new InvalidOperationException("Expected exactly one tutorial player.");
            GameObject player = players[0].gameObject;
            Camera camera = player.GetComponentInChildren<Camera>();
            GameObject basin = GameObject.Find("BurntRockBasin");
            GameObject flask = GameObject.Find("AlcoholFlask");
            Transform worldLaser = basin != null ? basin.transform.Find("MiningLaserAnchor/Mining Laser") : null;
            Terrain terrain = UnityEngine.Object.FindFirstObjectByType<Terrain>();
            if (camera == null || worldLaser == null || flask == null || terrain == null)
                throw new InvalidOperationException("Tutorial camera, basin laser, flask or terrain missing.");
            EnsureFolder(Root);
            var gameplay = new GameObject("Tutorial Gameplay").transform;
            Health health = player.AddComponent<Health>();
            var muzzle = new GameObject("Laser Muzzle").transform;
            muzzle.SetParent(camera.transform, false);
            muzzle.localPosition = new Vector3(0.25f, -0.2f, 0.5f);
            LineRenderer line = muzzle.gameObject.AddComponent<LineRenderer>();
            line.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Combat/LaserBeam.mat");
            line.positionCount = 2;
            line.enabled = false;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            MiningLaser laser = player.AddComponent<MiningLaser>();
            Set(laser, "inputActions", AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions"));
            Set(laser, "aimOrigin", camera.transform);
            Set(laser, "muzzle", muzzle);
            Set(laser, "beam", line);
            Set(laser, "heldModel", AssetDatabase.LoadAssetAtPath<GameObject>(TutorialCrashSiteBuilder.AssetRoot + "/Upgrades/MiningLaser.prefab"));
            laser.enabled = false;
            CombatDebugHud combatHud = player.AddComponent<CombatDebugHud>();
            Set(combatHud, "laser", laser);
            combatHud.enabled = false;
            Set(worldLaser.gameObject.AddComponent<TutorialPickup>(), "item", (int)TutorialPickup.Item.Laser);
            Set(flask.AddComponent<TutorialPickup>(), "item", (int)TutorialPickup.Item.Flask);

            GameObject heldFlask = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Props + "AlcoholFlask.prefab"));
            heldFlask.name = "Held Scotch Flask";
            heldFlask.transform.SetParent(camera.transform, false);
            foreach (Collider item in heldFlask.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(item);
            foreach (Renderer item in heldFlask.GetComponentsInChildren<Renderer>())
            { item.shadowCastingMode = ShadowCastingMode.Off; item.receiveShadows = false; }
            Bounds flaskBounds = heldFlask.GetComponentInChildren<Renderer>().bounds;
            foreach (Renderer item in heldFlask.GetComponentsInChildren<Renderer>()) flaskBounds.Encapsulate(item.bounds);
            heldFlask.transform.localScale *= 0.26f / flaskBounds.size.y;
            heldFlask.transform.localPosition = new Vector3(-0.24f, -0.36f, 0.55f);
            heldFlask.SetActive(false);

            GameObject hudObject = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Canvas.prefab"));
            hudObject.name = "Tutorial HUD";
            Canvas canvas = hudObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = hudObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            HUDController hud = hudObject.GetComponent<HUDController>() ?? hudObject.AddComponent<HUDController>();
            hud.healthSlider = hudObject.GetComponentInChildren<Slider>(true);
            hud.healthSlider.interactable = false;
            hud.healthSlider.minValue = 0;
            hud.healthSlider.maxValue = health.Max;
            hud.healthSlider.value = health.Max;
            Rect(hud.healthSlider.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(24, -53), new Vector2(300, 22));
            if (hud.healthSlider.handleRect != null) hud.healthSlider.handleRect.gameObject.SetActive(false);
            hud.healthSlider.fillRect.GetComponent<Image>().color = new Color(0.15f, 0.85f, 0.48f);
            foreach (TextMeshProUGUI text in hudObject.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (text.name == "ObjectiveText") hud.objectiveText = text;
                if (text.name == "ScrapText") text.gameObject.SetActive(false);
            }
            foreach (Transform child in hudObject.GetComponentsInChildren<Transform>(true))
                if (child.name == "ScannerIndicator") child.gameObject.SetActive(false);
            Rect(hud.objectiveText.rectTransform, new Vector2(0, 1), new Vector2(24, -124), new Vector2(900, 42));
            hud.objectiveText.fontSize = 19;
            hud.objectiveText.alignment = TextAlignmentOptions.TopLeft;
            TextMeshProUGUI hpText = Text("Health Readout", hudObject.transform, new Vector2(0, 1), new Vector2(24, -20), new Vector2(400, 30), 21);
            TextMeshProUGUI drunkText = Text("Drunk Stacks", hudObject.transform, new Vector2(0, 1), new Vector2(24, -85), new Vector2(850, 32), 19);
            TextMeshProUGUI prompt = Text("Pickup Prompt", hudObject.transform, new Vector2(0.5f, 0.5f), new Vector2(-230, -50), new Vector2(460, 36), 23);
            prompt.alignment = TextAlignmentOptions.Center;
            var fade = new GameObject("Blackout Fade", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fade.transform.SetParent(hudObject.transform, false);
            RectTransform fadeRect = fade.GetComponent<RectTransform>();
            fadeRect.anchorMin = Vector2.zero; fadeRect.anchorMax = Vector2.one;
            fadeRect.offsetMin = fadeRect.offsetMax = Vector2.zero;
            Image overlay = fade.GetComponent<Image>(); overlay.color = Color.clear;
            TextMeshProUGUI message = Text("Tutorial Message", hudObject.transform, Vector2.zero, new Vector2(24, 100), new Vector2(1180, 36), 20);
            foreach (Graphic graphic in hudObject.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            PrefabUtility.SaveAsPrefabAssetAndConnect(hudObject, Root + "/TutorialHUD.prefab", InteractionMode.AutomatedAction);

            var volume = new GameObject("Drunk Distortion").AddComponent<Volume>();
            volume.transform.SetParent(gameplay, false);
            volume.isGlobal = true; volume.priority = 50f; volume.weight = 0f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Tutorial Drunk";
            var lens = profile.Add<LensDistortion>(true); lens.intensity.Override(-0.2f); lens.scale.Override(1.05f);
            profile.Add<ChromaticAberration>(true).intensity.Override(0.65f);
            AssetDatabase.CreateAsset(profile, Root + "/TutorialDrunkProfile.asset");
            foreach (VolumeComponent effect in profile.components) AssetDatabase.AddObjectToAsset(effect, profile);
            volume.sharedProfile = profile;
            Set(camera.GetUniversalAdditionalCameraData(), "m_RenderPostProcessing", true);

            GameObject enemyRoot = new GameObject("Tutorial Targets"); enemyRoot.transform.SetParent(gameplay, false);
            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            target.name = "Tutorial Training Target";
            target.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Combat/TrainingDummy.mat");
            var dummy = target.AddComponent<TrainingDummy>(); Set(dummy, "respawnOnTimer", false);
            GameObject targetPrefab = PrefabUtility.SaveAsPrefabAsset(target, Root + "/TrainingTarget.prefab");
            UnityEngine.Object.DestroyImmediate(target);
            for (int i = 0; i < 3; i++)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(targetPrefab, enemyRoot.transform);
                instance.name = "Tutorial Target " + (i + 1);
                instance.transform.position = ClearGround(terrain, basin.transform.position + new Vector3(5 + i * 4, 0, 8 + i * 3), 0.7f) + Vector3.up;
            }
            GameObject hazard = MakeHazard();
            GameObject hazardPrefab = PrefabUtility.SaveAsPrefabAsset(hazard, Root + "/DamageTestPad.prefab");
            UnityEngine.Object.DestroyImmediate(hazard);
            GameObject pad = (GameObject)PrefabUtility.InstantiatePrefab(hazardPrefab, gameplay);
            pad.transform.position = ClearGround(terrain, player.transform.position + new Vector3(6, 0, 6), 1.8f);

            TutorialPlayer tutorial = player.AddComponent<TutorialPlayer>();
            Set(tutorial, "inputActions", AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions"));
            Set(tutorial, "view", camera); Set(tutorial, "laser", laser); Set(tutorial, "combatHud", combatHud);
            Set(tutorial, "heldFlask", heldFlask.transform); Set(tutorial, "enemies", enemyRoot);
            Set(tutorial, "drunkVolume", volume); Set(tutorial, "hud", hud);
            Set(tutorial, "healthText", hpText); Set(tutorial, "promptText", prompt); Set(tutorial, "drunkText", drunkText);
            Set(tutorial, "messageText", message); Set(tutorial, "blackoutOverlay", overlay);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save tutorial gameplay.");
            Validate();
            Debug.Log("TUTORIAL_GAMEPLAY_CREATED");
        }

        public static void Validate()
        {
            if (UnityEngine.Object.FindObjectsByType<TutorialPlayer>().Length != 1) throw new InvalidOperationException("Expected one tutorial player.");
            var player = UnityEngine.Object.FindFirstObjectByType<TutorialPlayer>();
            var data = new SerializedObject(player);
            foreach (string field in new[] { "inputActions", "view", "laser", "combatHud", "heldFlask", "enemies", "drunkVolume", "hud", "healthText", "promptText", "drunkText", "messageText", "blackoutOverlay" })
                if (data.FindProperty(field).objectReferenceValue == null) throw new InvalidOperationException("Missing tutorial reference: " + field);
            if (player.GetComponent<MiningLaser>().enabled) throw new InvalidOperationException("Player must start unarmed.");
            if (UnityEngine.Object.FindObjectsByType<TutorialPickup>().Length != 2) throw new InvalidOperationException("Expected laser and flask pickups.");
            if (UnityEngine.Object.FindObjectsByType<PlayerDamageZone>().Length != 1) throw new InvalidOperationException("Damage test pad missing.");
            if (UnityEngine.Object.FindObjectsByType<TrainingDummy>().Length != 3) throw new InvalidOperationException("Training targets missing.");
            foreach (GameObject root in player.gameObject.scene.GetRootGameObjects())
                foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject) != 0) throw new InvalidOperationException("Missing script: " + item.name);
            Debug.Log("TUTORIAL_GAMEPLAY_VALIDATED");
        }

        private static GameObject MakeHazard()
        {
            var root = new GameObject("Damage Test Pad");
            var trigger = root.AddComponent<BoxCollider>(); trigger.isTrigger = true;
            trigger.center = new Vector3(0, 1, 0); trigger.size = new Vector3(3, 2, 3);
            var rigid = root.AddComponent<Rigidbody>(); rigid.isKinematic = true; rigid.useGravity = false;
            root.AddComponent<PlayerDamageZone>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "DamagePadRed" };
            mat.SetColor("_BaseColor", new Color(0.8f, 0.07f, 0.03f));
            mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", new Color(0.7f, 0.04f, 0.01f));
            AssetDatabase.CreateAsset(mat, Root + "/DamagePadRed.mat");
            GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platform.name = "Red Damage Surface"; platform.transform.SetParent(root.transform, false);
            platform.transform.localPosition = new Vector3(0, 0.06f, 0); platform.transform.localScale = new Vector3(3, 0.12f, 3);
            platform.GetComponent<Renderer>().sharedMaterial = mat;
            UnityEngine.Object.DestroyImmediate(platform.GetComponent<Collider>());
            var sign = new GameObject("Damage Instructions").AddComponent<TextMeshPro>();
            sign.transform.SetParent(root.transform, false); sign.transform.localPosition = new Vector3(0, 2.2f, 0.8f);
            sign.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            sign.text = "DAMAGE TEST\n20 HP / SECOND\nWalk onto the red pad";
            sign.fontSize = 3; sign.alignment = TextAlignmentOptions.Center;
            sign.rectTransform.sizeDelta = new Vector2(5, 2); sign.color = new Color(1, 0.7f, 0.5f);
            return root;
        }

        private static Vector3 ClearGround(Terrain terrain, Vector3 desired, float radius)
        {
            Physics.SyncTransforms();
            for (int i = 0; i < 100; i++)
            {
                float angle = i * 2.4f; float offset = Mathf.Sqrt(i) * 0.7f;
                Vector3 point = desired + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * offset;
                point.y = terrain.transform.position.y + terrain.SampleHeight(point);
                bool blocked = false;
                foreach (Collider collider in Physics.OverlapCapsule(point + Vector3.up * (radius + 0.15f), point + Vector3.up * 2.2f, radius, ~0, QueryTriggerInteraction.Ignore))
                    if (!(collider is TerrainCollider)) blocked = true;
                if (!blocked) return point;
            }
            throw new InvalidOperationException("No clear placement near " + desired);
        }

        private static TextMeshProUGUI Text(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, float fontSize)
        {
            var text = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            text.transform.SetParent(parent, false); Rect(text.rectTransform, anchor, position, size);
            text.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            text.fontSize = fontSize; text.color = Color.white; text.alignment = TextAlignmentOptions.TopLeft;
            return text;
        }

        private static void Rect(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        { rect.anchorMin = rect.anchorMax = anchor; rect.pivot = new Vector2(0, 1); rect.anchoredPosition = position; rect.sizeDelta = size; }

        private static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
        { var data = new SerializedObject(target); data.FindProperty(field).objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo(); }
        private static void Set(UnityEngine.Object target, string field, int value)
        { var data = new SerializedObject(target); data.FindProperty(field).intValue = value; data.ApplyModifiedPropertiesWithoutUndo(); }
        private static void Set(UnityEngine.Object target, string field, bool value)
        { var data = new SerializedObject(target); data.FindProperty(field).boolValue = value; data.ApplyModifiedPropertiesWithoutUndo(); }
        private static void EnsureFolder(string path)
        { if (AssetDatabase.IsValidFolder(path)) return; int slash = path.LastIndexOf('/'); EnsureFolder(path.Substring(0, slash)); AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1)); }
    }
}
