using SignalLost.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SignalLost.UI
{
    /// <summary>
    /// Esc (gamepad Menu) pauses the game (meeting notes 10-22): Continue, Save, Controls, Settings and Exit to Main
    /// Menu. Time and audio stop while it is open. Saving is a placeholder for now. Esc closes other menus and
    /// popups first; only with nothing else open does it pause.
    /// </summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        private static PauseMenu instance;

        private GameObject root;
        private RectTransform panel;
        private SettingsPanel settings;
        private TextMeshProUGUI toast;
        private float toastUntil, shownAt;
        private int openedFrame = -1;

        /// <summary>The existing instance, or null. Use this in OnDisable/OnDestroy so nothing is created during unload.</summary>
        public static PauseMenu Current => instance;

        public static PauseMenu Instance
        {
            get
            {
                if (instance == null && Application.isPlaying)
                    instance = UiTheme.CreateCanvas("Pause Menu", 70).gameObject.AddComponent<PauseMenu>();
                return instance;
            }
        }

        public bool IsOpen => root != null && root.activeSelf;

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            Build();
        }

        private void OnDestroy()
        {
            if (instance != this) return;
            if (IsOpen) Resume();
            instance = null;
        }

        public void Open()
        {
            if (IsOpen) return;
            UiTheme.EnsureEventSystem();
            root.SetActive(true);
            settings.gameObject.SetActive(false);
            shownAt = Time.unscaledTime;
            openedFrame = Time.frameCount;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            UiMenus.Open(this, true);
        }

        public void Resume()
        {
            if (!IsOpen) return;
            root.SetActive(false);
            Time.timeScale = 1f;
            AudioListener.pause = false;
            UiMenus.Close(this);
        }

        private void Update()
        {
            bool pausePressed = Keyboard.current?.escapeKey.wasPressedThisFrame == true || Gamepad.current?.startButton.wasPressedThisFrame == true;
            if (IsOpen)
            {
                panel.localScale = Vector3.one * Mathf.LerpUnclamped(0.9f, 1f, UiTheme.Pop((Time.unscaledTime - shownAt) / 0.25f));
                toast.alpha = Mathf.Clamp01((toastUntil - Time.unscaledTime) / 0.3f);
                if (pausePressed && openedFrame != Time.frameCount)
                {
                    if (settings.gameObject.activeSelf) settings.gameObject.SetActive(false);
                    else Resume();
                }
                return;
            }
            if (pausePressed && !UiMenus.AnyOpen && !UiMenus.ChangedThisFrame) Open();
        }

        private void ShowSettings(SettingsPanel.Page page)
        {
            settings.gameObject.SetActive(true);
            settings.Show(page);
        }

        private void Save()
        {
            toast.text = "Saving is coming in a later update. Your progress lasts until you quit.";
            toastUntil = Time.unscaledTime + 3f;
        }

        private void ExitToMainMenu()
        {
            Resume();
            SceneManager.LoadScene(SceneNames.MainMenu);
        }

        private void Build()
        {
            root = UiTheme.Stretch(UiTheme.Rect("Pause", transform)).gameObject;
            Image dim = UiTheme.Block("Dim", root.transform, new Color(0.02f, 0.03f, 0.08f, 0.65f));
            UiTheme.Stretch(dim.rectTransform);
            dim.raycastTarget = true;

            Image background = UiTheme.Panel("Panel", root.transform, UiTheme.PanelColor, UiTheme.Edge, 1.4f);
            panel = UiTheme.Place(background.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(120f, 0f), new Vector2(460f, 640f));
            TextMeshProUGUI title = UiTheme.Label("Title", panel, 58f, UiTheme.Accent, TextAlignmentOptions.Top, FontStyles.Bold);
            UiTheme.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(420f, 70f));
            title.text = "PAUSED";
            title.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 2f);
            TextMeshProUGUI flavour = UiTheme.Label("Flavour", panel, 22f, UiTheme.Muted, TextAlignmentOptions.Top, FontStyles.Italic);
            UiTheme.Place(flavour.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -106f), new Vector2(400f, 30f));
            flavour.text = "The aliens can wait. Probably.";

            (string label, UnityEngine.Events.UnityAction action)[] buttons =
            {
                ("Continue", Resume), ("Save Game", Save), ("Controls", () => ShowSettings(SettingsPanel.Page.Controls)),
                ("Settings", () => ShowSettings(SettingsPanel.Page.Sound)), ("Exit to Main Menu", ExitToMainMenu)
            };
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = UiTheme.Button(buttons[i].label, panel, buttons[i].label, 30f);
                UiTheme.Place((RectTransform)button.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -160f - i * 84f), new Vector2(360f, 68f));
                button.onClick.AddListener(buttons[i].action);
            }

            settings = SettingsPanel.Create(root.transform, new Vector2(900f, 640f));
            UiTheme.Place((RectTransform)settings.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(620f, 0f), new Vector2(900f, 640f));

            toast = UiTheme.Label("Toast", root.transform, 26f, UiTheme.Text, TextAlignmentOptions.Center, FontStyles.Bold);
            UiTheme.Place(toast.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(1200f, 40f));
            toast.alpha = 0f;
            root.SetActive(false);
        }
    }
}
