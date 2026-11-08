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
    /// Controls (11-07): click a key, then press the new key or mouse button (Esc cancels); a key already in use swaps
    /// over. Bindings live in <see cref="KeyBindings"/>. The sound sliders and video options save through
    /// <see cref="GameSettings"/>.
    /// </summary>
    public sealed class SettingsPanel : MonoBehaviour
    {
        public enum Page { Controls, Sound, Video }

        // Controls that can't be changed, listed after the rebindable ones.
        private static readonly (string action, string keys, string pad)[] FixedControls =
        {
            ("Look", "Mouse", "Right stick"), ("Pause / back", "Esc", "Menu")
        };
        private const float KeyRowHeight = 46f, KeyRowGap = 6f;

        private readonly Dictionary<string, (Button button, TextMeshProUGUI label)> keyButtons = new Dictionary<string, (Button, TextMeshProUGUI)>();
        private TextMeshProUGUI controlsNote;
        private string waitingFor;

        /// <summary>The control waiting for a new key, or null.</summary>
        public string WaitingFor => waitingFor;

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
                UiTheme.Place((RectTransform)tab.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(UiTheme.Pad + index++ * 200f, -UiTheme.Pad), new Vector2(186f, 52f));
                tab.onClick.AddListener(() => Show(captured));
                tabs[page] = tab;
                RectTransform body = UiTheme.Rect(page + " Page", root);
                body.anchorMin = Vector2.zero;
                body.anchorMax = Vector2.one;
                body.offsetMin = new Vector2(UiTheme.Pad, UiTheme.Pad);
                body.offsetMax = new Vector2(-UiTheme.Pad, -(UiTheme.Pad + 52f + 20f)); // below the tabs
                pages[page] = body.gameObject;
            }

            // The page is the panel inside the padding, below the tabs (see the page rects above).
            BuildControls((RectTransform)pages[Page.Controls].transform,
                new Vector2(size.x - 2f * UiTheme.Pad, size.y - 2f * UiTheme.Pad - 72f));

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

        // ---------- Controls ----------

        // A scrolling list: each rebindable control with its key button and its (fixed) gamepad button, then the
        // fixed controls; a Reset button and a note underneath.
        private void BuildControls(RectTransform page, Vector2 pageSize)
        {
            const float footer = 52f;
            // Leaves room on the right for the scrollbar (8 px gap + 10 px bar).
            RectTransform area = UiTheme.Place(UiTheme.Rect("Bindings", page), new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero,
                new Vector2(pageSize.x - 18f, pageSize.y - footer - UiTheme.PadSmall));
            RectTransform list = UiTheme.ScrollList(area);

            int index = 0;
            foreach (KeyBindings.Binding binding in KeyBindings.All)
            {
                RectTransform row = KeyRow(list, index++, binding.Label, binding.Gamepad);
                Button button = UiTheme.Button("Key " + binding.Id, row, "", 22f);
                UiTheme.Place((RectTransform)button.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-170f, 0f), new Vector2(220f, KeyRowHeight - 6f));
                string id = binding.Id;
                button.onClick.AddListener(() => BeginRebind(id));
                keyButtons[id] = (button, button.GetComponentInChildren<TextMeshProUGUI>());
            }
            foreach ((string action, string keys, string pad) in FixedControls)
            {
                RectTransform row = KeyRow(list, index++, action, pad);
                TextMeshProUGUI key = UiTheme.Label("Key", row, 22f, UiTheme.Muted, TextAlignmentOptions.Center, FontStyles.Bold);
                UiTheme.Place(key.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-170f, 0f), new Vector2(220f, KeyRowHeight - 6f));
                key.text = keys;
            }
            list.sizeDelta = new Vector2(0f, index * (KeyRowHeight + KeyRowGap));

            Button reset = UiTheme.Button("Reset Bindings", page, "Reset to defaults", 22f);
            UiTheme.Place((RectTransform)reset.transform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(240f, footer));
            reset.onClick.AddListener(() =>
            {
                CancelRebind();
                KeyBindings.ResetAll();
                SetNote("All keys are back to their defaults.");
            });
            controlsNote = UiTheme.Label("Note", page, 21f, UiTheme.Muted, TextAlignmentOptions.MidlineLeft, FontStyles.Italic);
            UiTheme.Place(controlsNote.rectTransform, Vector2.zero, Vector2.zero, new Vector2(240f + UiTheme.PadSmall, 0f), new Vector2(560f, footer));
            SetNote(null);
            KeyBindings.Changed += RefreshKeys;
            RefreshKeys();
        }

        private static RectTransform KeyRow(RectTransform list, int index, string label, string pad)
        {
            Image back = UiTheme.Panel(label, list, index % 2 == 0 ? UiTheme.PanelLight : new Color(1f, 1f, 1f, 0.03f), null, 0.5f);
            RectTransform row = back.rectTransform;
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.anchoredPosition = new Vector2(0f, -index * (KeyRowHeight + KeyRowGap));
            row.sizeDelta = new Vector2(0f, KeyRowHeight);
            TextMeshProUGUI text = UiTheme.Label("Label", row, 23f, UiTheme.Text, TextAlignmentOptions.MidlineLeft);
            UiTheme.Stretch(text.rectTransform, UiTheme.PadSmall);
            text.rectTransform.offsetMax = new Vector2(-410f, -4f);
            text.rectTransform.offsetMin = new Vector2(UiTheme.PadSmall, 4f);
            text.enableAutoSizing = true;
            text.fontSizeMin = 16f;
            text.fontSizeMax = 23f;
            text.text = label;
            TextMeshProUGUI gamepad = UiTheme.Label("Gamepad", row, 20f, UiTheme.Muted, TextAlignmentOptions.MidlineRight);
            UiTheme.Place(gamepad.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-UiTheme.PadSmall, 0f), new Vector2(140f, KeyRowHeight));
            gamepad.textWrappingMode = TextWrappingModes.NoWrap;
            gamepad.enableAutoSizing = true;
            gamepad.fontSizeMin = 14f;
            gamepad.fontSizeMax = 20f;
            gamepad.text = pad;
            return row;
        }

        /// <summary>Starts waiting for a new key for <paramref name="id"/> (the Controls page's key buttons call this).</summary>
        public void BeginRebind(string id)
        {
            waitingFor = id;
            RefreshKeys();
            SetNote("Press a key or mouse button.   <b>Esc</b> cancels.");
            KeyBindings.ListenForKey(path =>
            {
                string target = waitingFor;
                waitingFor = null;
                if (path == null || target == null) { RefreshKeys(); SetNote(null); return; }
                string swapped = KeyBindings.Rebind(target, path);
                RefreshKeys();
                SetNote(swapped != null
                    ? $"{KeyBindings.DisplayPath(path)} was on \"{KeyBindings.Find(swapped).Label}\": that now uses {KeyBindings.Display(swapped)}."
                    : null);
            });
        }

        private void CancelRebind()
        {
            if (waitingFor == null) return;
            waitingFor = null;
            KeyBindings.StopListening();
            RefreshKeys();
            SetNote(null);
        }

        private void RefreshKeys()
        {
            foreach (KeyBindings.Binding binding in KeyBindings.All)
            {
                if (!keyButtons.TryGetValue(binding.Id, out var key) || key.label == null) continue;
                bool waiting = binding.Id == waitingFor;
                key.label.text = waiting ? "Press a key..." : KeyBindings.Display(binding.Id);
                key.label.color = waiting ? new Color(0.08f, 0.08f, 0.12f) : UiTheme.Accent;
                key.button.GetComponent<Image>().color = waiting ? UiTheme.Accent : UiTheme.PanelLight;
            }
        }

        private void SetNote(string text)
        {
            if (controlsNote != null) controlsNote.text = text ?? "Click a key to change it. Gamepad is fixed for now.";
        }

        private void OnDisable() => CancelRebind();

        private void OnDestroy() => KeyBindings.Changed -= RefreshKeys;

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
