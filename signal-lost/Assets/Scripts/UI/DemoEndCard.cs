using SignalLost.Core;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SignalLost.UI
{
    /// <summary>
    /// "Thanks for playing" at the end of the tutorial demo (meeting notes 10-23). It waits for the player: Keep
    /// Exploring closes it and leaves them free to roam the meadow, Main Menu returns to the title screen. It shows the
    /// tutorial completion time for playtests (<see cref="SignalLost.Story.TutorialRunTimer"/>).
    /// </summary>
    public sealed class DemoEndCard : MonoBehaviour
    {
        private static DemoEndCard instance;
        private GameObject root;
        private RectTransform panel;
        private TextMeshProUGUI time, saved;
        private float shownAt;

        public static DemoEndCard Current => instance;
        public bool IsOpen => root != null && root.activeSelf;

        /// <summary>Shows the card with the run's completion time in seconds (negative hides it) and how often the player went down.</summary>
        public static DemoEndCard Show(float seconds = -1f, int deaths = 0)
        {
            if (instance == null) instance = UiTheme.CreateCanvas("Demo End Card", 75).gameObject.AddComponent<DemoEndCard>();
            bool timed = seconds >= 0f;
            instance.time.gameObject.SetActive(timed);
            instance.saved.gameObject.SetActive(timed);
            if (timed)
            {
                instance.time.text = $"Completion time  <color={UiTheme.Hex(UiTheme.Accent)}><b>{SignalLost.Story.TutorialRunTimer.Format(seconds)}</b></color>" +
                                     $"<color={UiTheme.Hex(UiTheme.Muted)}>   ·   {deaths} {(deaths == 1 ? "knockout" : "knockouts")}</color>";
                instance.saved.text = "Saved for playtesting: " + SignalLost.Story.TutorialRunTimer.FilePath;
            }
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
            UiTheme.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(900f, 90f));
            title.text = "THANKS FOR PLAYING";
            title.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 1.5f);
            time = UiTheme.Label("Time", panel, 36f, UiTheme.Text, TextAlignmentOptions.Center);
            UiTheme.Place(time.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(900f, 50f));
            time.textWrappingMode = TextWrappingModes.NoWrap;
            TextMeshProUGUI body = UiTheme.Label("Body", panel, 28f, UiTheme.Text, TextAlignmentOptions.Top);
            UiTheme.Place(body.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -205f), new Vector2(860f, 170f));
            body.text = "That's the end of the <b>Signal Lost</b> demo.\n" +
                        $"<color={UiTheme.Hex(UiTheme.Muted)}>The Chief Toad's crown is on the stand, the cooling system is recovered, and the desert awaits.\n" +
                        "Keep exploring the meadow, or head back to the title screen.</color>";
            saved = UiTheme.Label("Saved", panel, 17f, UiTheme.Muted, TextAlignmentOptions.Bottom);
            UiTheme.Place(saved.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 134f), new Vector2(920f, 46f));
            saved.overflowMode = TextOverflowModes.Ellipsis;
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
