using System;
using System.Collections.Generic;
using SignalLost.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SignalLost.UI
{
    /// <summary>
    /// Controls, Sound and Video settings, shared by the title screen and the pause menu (meeting notes 10-22).
    /// A proof of concept to fill in later: the controls list is read-only (rebinding is a placeholder), the sound
    /// sliders and video options save through <see cref="GameSettings"/>.
    /// </summary>
    public sealed class SettingsPanel : MonoBehaviour
    {
        public enum Page { Controls, Sound, Video }

        private static readonly (string action, string keys)[] Controls =
        {
            ("Move", "W A S D  /  left stick"), ("Look", "Mouse  /  right stick"), ("Sprint", "Left Shift  /  stick click"),
            ("Jump", "Space  /  A"), ("Laser combo", "Left click  /  X"), ("Charge bolt", "Hold right click  /  RT"),
            ("Swap scanner / laser", "Q  /  LB"), ("Drink from the flask", "F  /  D-pad up"), ("Talk / pick up", "E  /  Y"),
            ("Inventory", "Tab  /  View"), ("Pause", "Esc  /  Menu")
        };

        private readonly Dictionary<Page, GameObject> pages = new Dictionary<Page, GameObject>();
        private readonly Dictionary<Page, Button> tabs = new Dictionary<Page, Button>();
        private TextMeshProUGUI fullscreenLabel, qualityLabel;

        public Page Current { get; private set; }

        public static SettingsPanel Create(Transform parent, Vector2 size)
        {
            RectTransform root = UiTheme.Rect("Settings", parent);
            root.sizeDelta = size;
            var panel = root.gameObject.AddComponent<SettingsPanel>();
            panel.Build(root, size);
            return panel;
        }

        public void Show(Page page)
        {
            Current = page;
            foreach (var pair in pages) pair.Value.SetActive(pair.Key == page);
            foreach (var pair in tabs)
            {
                bool selected = pair.Key == page;
                pair.Value.GetComponent<Image>().color = selected ? UiTheme.Accent : UiTheme.PanelLight;
                pair.Value.GetComponentInChildren<TextMeshProUGUI>().color = selected ? new Color(0.08f, 0.08f, 0.12f) : UiTheme.Text;
            }
            RefreshVideo();
        }

        private void Build(RectTransform root, Vector2 size)
        {
            UiTheme.Stretch(UiTheme.Panel("Panel", root, UiTheme.PanelColor, UiTheme.Edge, 1.2f).rectTransform);
            int index = 0;
            foreach (Page page in Enum.GetValues(typeof(Page)))
            {
                Page captured = page;
                Button tab = UiTheme.Button(page + " Tab", root, page.ToString().ToUpperInvariant(), 24f);
                UiTheme.Place((RectTransform)tab.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f + index++ * 200f, -26f), new Vector2(186f, 52f));
                tab.onClick.AddListener(() => Show(captured));
                tabs[page] = tab;
                RectTransform body = UiTheme.Rect(page + " Page", root);
                body.anchorMin = Vector2.zero;
                body.anchorMax = Vector2.one;
                body.offsetMin = new Vector2(34f, 30f);
                body.offsetMax = new Vector2(-34f, -100f);
                pages[page] = body.gameObject;
            }

            // Controls: a readable list; rebinding comes later.
            RectTransform controls = (RectTransform)pages[Page.Controls].transform;
            for (int i = 0; i < Controls.Length; i++)
            {
                Row(controls, i, Controls[i].action, out RectTransform row, 40f);
                TextMeshProUGUI keys = UiTheme.Label("Keys", row, 24f, UiTheme.Accent, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
                UiTheme.Stretch(keys.rectTransform);
                keys.text = Controls[i].keys;
            }
            Note(controls, "Key rebinding is coming in a later update.");

            // Sound: working sliders saved to this machine.
            RectTransform sound = (RectTransform)pages[Page.Sound].transform;
            SliderRow(sound, 0, "Master volume", GameSettings.MasterVolume, v => GameSettings.MasterVolume = v);
            SliderRow(sound, 1, "Music", GameSettings.MusicVolume, v => GameSettings.MusicVolume = v);
            SliderRow(sound, 2, "Sound effects", GameSettings.EffectsVolume, v => GameSettings.EffectsVolume = v);
            Note(sound, "Music and effects get their own mixers once the soundtrack lands.");

            // Video: fullscreen, quality and look sensitivity.
            RectTransform video = (RectTransform)pages[Page.Video].transform;
            fullscreenLabel = ButtonRow(video, 0, "Display", () => GameSettings.Fullscreen = !GameSettings.Fullscreen);
            qualityLabel = ButtonRow(video, 1, "Graphics quality", () => GameSettings.Quality = (GameSettings.Quality + 1) % QualitySettings.names.Length);
            SliderRow(video, 2, "Look sensitivity", GameSettings.LookSensitivity, v => GameSettings.LookSensitivity = v, 0.25f, 3f);
            Note(video, "Resolution, field of view and brightness are coming later.");
            Show(Page.Controls);
        }

        private void RefreshVideo()
        {
            if (fullscreenLabel != null) fullscreenLabel.text = GameSettings.Fullscreen ? "Fullscreen" : "Windowed";
            if (qualityLabel != null) qualityLabel.text = QualitySettings.names[GameSettings.Quality];
        }

        private static TextMeshProUGUI Row(RectTransform page, int index, string label, out RectTransform row, float height = 58f)
        {
            row = UiTheme.Place(UiTheme.Rect(label, page), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -index * (height + 6f)), new Vector2(0f, height));
            row.anchorMax = new Vector2(1f, 1f);
            row.sizeDelta = new Vector2(0f, height);
            TextMeshProUGUI text = UiTheme.Label("Label", row, 26f, UiTheme.Text, TextAlignmentOptions.MidlineLeft);
            UiTheme.Stretch(text.rectTransform);
            text.text = label;
            return text;
        }

        private static void SliderRow(RectTransform page, int index, string label, float value, Action<float> changed, float min = 0f, float max = 1f)
        {
            Row(page, index, label, out RectTransform row);
            Slider slider = UiTheme.Slider("Slider", row, value, min, max);
            UiTheme.Place((RectTransform)slider.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(380f, 22f));
            slider.onValueChanged.AddListener(v => changed(v));
        }

        private TextMeshProUGUI ButtonRow(RectTransform page, int index, string label, Action clicked)
        {
            Row(page, index, label, out RectTransform row);
            Button button = UiTheme.Button("Option", row, "", 24f);
            UiTheme.Place((RectTransform)button.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(380f, 48f));
            button.onClick.AddListener(() => { clicked(); RefreshVideo(); });
            return button.GetComponentInChildren<TextMeshProUGUI>();
        }

        private static void Note(RectTransform page, string text)
        {
            TextMeshProUGUI note = UiTheme.Label("Note", page, 22f, UiTheme.Muted, TextAlignmentOptions.BottomLeft, FontStyles.Italic);
            UiTheme.Stretch(note.rectTransform);
            note.text = text;
        }
    }
}
