using System;
using SignalLost.NPC;
using SignalLost.Player;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class TutorialNpcUiSetup
{
    public const string UiPath = "Assets/Prefabs/UI/TutorialNpcWindows.prefab";
    public static void Apply()
    {
        TutorialEnemiesSetup.RequireTemp();
        EditorSceneManager.OpenScene("Assets/Scenes/Tutorial Testing.unity");
        if (UnityEngine.Object.FindFirstObjectByType<TutorialNpcUi>() != null) throw new InvalidOperationException("Screen-space NPC windows already installed.");
        // Remove the obsolete world-space panel from the reusable merchant variant, not just its scene instance.
        var merchantPrefab = PrefabUtility.LoadPrefabContents(TutorialNpcSetup.MerchantPath);
        var obsolete = merchantPrefab.transform.Find("Merchant Shop Preview");
        if (obsolete != null) UnityEngine.Object.DestroyImmediate(obsolete.gameObject);
        PrefabUtility.SaveAsPrefabAsset(merchantPrefab, TutorialNpcSetup.MerchantPath);
        PrefabUtility.UnloadPrefabContents(merchantPrefab);

        var root = new GameObject("Tutorial NPC Windows", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 90;
        var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
        var ui = root.AddComponent<TutorialNpcUi>();
        var dialogue = Panel(root.transform, "NPC Conversation", new Vector2(860, 350), new Vector2(0, -170));
        var title = Label(dialogue.transform, "Title", "QUEST", new Vector2(0, 125), new Vector2(780, 48), 28, Gold);
        var body = Label(dialogue.transform, "Dialogue", "", new Vector2(0, 10), new Vector2(780, 150), 26, Color.white);
        body.alignment = TextAlignmentOptions.TopLeft;
        var hint = Label(dialogue.transform, "Continue Hint", "E  Next   •   Esc  Close", new Vector2(0, -130), new Vector2(780, 40), 21, Gold);
        dialogue.SetActive(false);
        var shop = Panel(root.transform, "Merchant Shop Window", new Vector2(740, 590), Vector2.zero);
        Label(shop.transform, "Title", "BUCKET & BARTER", new Vector2(0, 235), new Vector2(670, 60), 36, Gold);
        Label(shop.transform, "Balance", "Scrap balance: —\nTrading is not available in this prototype.", new Vector2(0, 150), new Vector2(670, 70), 24, Color.white);
        string[] items = { "SUPPLIES  —  Coming soon", "UPGRADES  —  Coming soon", "TRADE SCRAP  —  Coming soon" };
        for (int i = 0; i < items.Length; i++)
        {
            var row = Panel(shop.transform, "Disabled Purchase " + i, new Vector2(650, 65), new Vector2(0, 40 - 85 * i));
            row.GetComponent<Image>().color = new Color(0.13f, 0.17f, 0.19f, 1);
            var button = row.AddComponent<Button>(); button.targetGraphic = row.GetComponent<Image>(); button.interactable = false;
            Label(row.transform, "Label", items[i], Vector2.zero, new Vector2(610, 60), 24, new Color(0.65f, 0.69f, 0.7f));
        }
        Label(shop.transform, "Close Hint", "E or Esc  Close    •    Walk away to leave", new Vector2(0, -240), new Vector2(670, 45), 21, Gold);
        shop.SetActive(false);
        var tracker = Panel(root.transform, "Quest Tracker", new Vector2(390, 195), Vector2.zero);
        var trackerRect = tracker.GetComponent<RectTransform>();
        trackerRect.anchorMin = trackerRect.anchorMax = trackerRect.pivot = Vector2.one;
        trackerRect.anchoredPosition = new Vector2(-28, -28);
        var trackedText = Label(tracker.transform, "Quest Progress", "", Vector2.zero, new Vector2(350, 160), 21, Color.white);
        trackedText.alignment = TextAlignmentOptions.TopLeft;
        tracker.SetActive(false);
        Set(ui, "conversationPanel", dialogue); Set(ui, "shopPanel", shop); Set(ui, "trackerPanel", tracker);
        Set(ui, "conversationTitle", title); Set(ui, "conversationBody", body); Set(ui, "conversationHint", hint); Set(ui, "trackerText", trackedText);
        // Persist a reusable UI prefab without scene references, then bind its scene instance.
        PrefabUtility.SaveAsPrefabAssetAndConnect(root, UiPath, InteractionMode.AutomatedAction);
        var player = UnityEngine.Object.FindFirstObjectByType<TutorialPlayer>();
        Set(ui, "player", player.transform);
        Set(player, "npcUi", ui);
        Set(UnityEngine.Object.FindFirstObjectByType<TutorialQuestFlow>(), "ui", ui);
        Set(UnityEngine.Object.FindFirstObjectByType<MerchantShop>(), "ui", ui);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(player.gameObject.scene);
        Debug.Log("TUTORIAL_NPC_UI_SETUP_PASS");
    }
    private static readonly Color Gold = new Color(0.96f, 0.77f, 0.38f);
    private static GameObject Panel(Transform parent, string name, Vector2 size, Vector2 position)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(Image)); obj.transform.SetParent(parent, false);
        var rect = obj.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f); rect.sizeDelta = size; rect.anchoredPosition = position;
        var image = obj.GetComponent<Image>(); image.color = new Color(0.025f, 0.045f, 0.065f, 0.97f); image.raycastTarget = false;
        return obj;
    }
    private static TextMeshProUGUI Label(Transform parent, string name, string text, Vector2 position, Vector2 size, float fontSize, Color color)
    {
        var label = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        label.transform.SetParent(parent, false); label.rectTransform.sizeDelta = size; label.rectTransform.anchoredPosition = position;
        label.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        label.fontSize = fontSize; label.color = color; label.text = text; label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
        return label;
    }
    private static void Set(UnityEngine.Object obj, string name, UnityEngine.Object value)
    { var s = new SerializedObject(obj); s.FindProperty(name).objectReferenceValue = value; s.ApplyModifiedPropertiesWithoutUndo(); }
}
