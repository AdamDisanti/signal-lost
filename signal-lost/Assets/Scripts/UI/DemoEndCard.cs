using SignalLost.Core;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SignalLost.UI
{
    /// <summary>
    /// "Thanks for playing" at the end of the tutorial demo (meeting notes 10-23). It waits for the player: Keep
    /// Exploring closes it and leaves them free to roam the meadow, Main Menu returns to the title screen.
    /// </summary>
    public sealed class DemoEndCard : MonoBehaviour
    {
        private static DemoEndCard instance;
        private GameObject root;
        private RectTransform panel;
        private float shownAt;

        public static DemoEndCard Current => instance;
        public bool IsOpen => root != null && root.activeSelf;

        public static DemoEndCard Show()
        {
            if (instance == null) instance = UiTheme.CreateCanvas("Demo End Card", 75).gameObject.AddComponent<DemoEndCard>();
            instance.Open();
            return instance;
        }

        private void Awake()
        {
            instance = this;
            Build();
        }

        private void OnDestroy()
        {
            if (instance != this) return;
            UiMenus.Close(this);
            instance = null;
        }

        private void Open()
        {
            UiTheme.EnsureEventSystem();
            root.SetActive(true);
            shownAt = Time.unscaledTime;
            UiMenus.Open(this, true);
        }

        public void KeepExploring()
        {
            if (!IsOpen) return;
            root.SetActive(false);
            UiMenus.Close(this);
        }

        public void ToMainMenu()
        {
            KeepExploring();
            SceneManager.LoadScene(SceneNames.MainMenu);
        }

        private void Update()
        {
            if (IsOpen) panel.localScale = Vector3.one * Mathf.LerpUnclamped(0.85f, 1f, UiTheme.Pop((Time.unscaledTime - shownAt) / 0.4f));
        }

        private void Build()
        {
            root = UiTheme.Stretch(UiTheme.Rect("End Card", transform)).gameObject;
            Image dim = UiTheme.Block("Dim", root.transform, new Color(0.02f, 0.03f, 0.08f, 0.75f));
            UiTheme.Stretch(dim.rectTransform);
            dim.raycastTarget = true;

            Image background = UiTheme.Panel("Panel", root.transform, UiTheme.PanelColor, UiTheme.Accent, 1.4f);
            panel = UiTheme.Place(background.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 560f));
            TextMeshProUGUI title = UiTheme.Label("Title", panel, 72f, UiTheme.Accent, TextAlignmentOptions.Top, FontStyles.Bold);
            UiTheme.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(900f, 90f));
            title.text = "THANKS FOR PLAYING";
            title.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 1.5f);
            TextMeshProUGUI body = UiTheme.Label("Body", panel, 30f, UiTheme.Text, TextAlignmentOptions.Top);
            UiTheme.Place(body.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(860f, 200f));
            body.text = "That's the end of the <b>Signal Lost</b> demo.\n" +
                        $"<color={UiTheme.Hex(UiTheme.Muted)}>The Chief Toad's crown is on the stand, the cooling system is back, and the desert awaits.\n" +
                        "Keep exploring the meadow, or head back to the title screen.</color>";
            (string label, UnityEngine.Events.UnityAction action)[] buttons = { ("Keep Exploring", KeepExploring), ("Main Menu", ToMainMenu) };
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = UiTheme.Button(buttons[i].label, panel, buttons[i].label, 30f);
                UiTheme.Place((RectTransform)button.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-190f + i * 380f, 50f), new Vector2(340f, 72f));
                button.onClick.AddListener(buttons[i].action);
            }
            root.SetActive(false);
        }
    }
}
