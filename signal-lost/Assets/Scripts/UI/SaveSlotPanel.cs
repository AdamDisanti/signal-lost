using System;
using SignalLost.Progression;
using SignalLost.Story;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SignalLost.UI
{
    /// <summary>
    /// The three save slots (10-24), shared by the pause menu's Save Game and the title screen's New Game and Load
    /// Game. Each slot shows its quest, time played and when it was saved. Overwriting a different save, or starting a
    /// new game over one, needs a second click on the same slot. The owner closes it on Esc (<see cref="Hide"/>).
    /// </summary>
    public sealed class SaveSlotPanel : MonoBehaviour
    {
        public enum Mode { Save, Load, NewGame }

        private TextMeshProUGUI title, hint;
        private readonly Button[] slots = new Button[SaveGame.SlotCount];
        private readonly TextMeshProUGUI[] headings = new TextMeshProUGUI[SaveGame.SlotCount];
        private readonly TextMeshProUGUI[] details = new TextMeshProUGUI[SaveGame.SlotCount];
        private Mode mode;
        private Action<int> chosen;
        private Action closed;
        private int confirming;

        public bool IsOpen => gameObject.activeSelf;

        /// <summary>Builds the panel (hidden) in the middle of <paramref name="parent"/>.</summary>
        public static SaveSlotPanel Create(Transform parent)
        {
            Image background = UiTheme.Panel("Save Slots", parent, UiTheme.PanelColor, UiTheme.Edge, 1.4f);
            UiTheme.Place(background.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860f, 660f));
            background.raycastTarget = true;
            var panel = background.gameObject.AddComponent<SaveSlotPanel>();
            panel.Build(background.rectTransform);
            background.gameObject.SetActive(false);
            return panel;
        }

        /// <summary>
        /// Opens the slots for <paramref name="showMode"/>. <paramref name="onChosen"/> gets the slot picked (1-3) after
        /// the panel closes; <paramref name="onClosed"/> runs whenever it closes, picked or not.
        /// </summary>
        public void Show(Mode showMode, Action<int> onChosen, Action onClosed = null)
        {
            mode = showMode;
            chosen = onChosen;
            closed = onClosed;
            confirming = 0;
            UiTheme.EnsureEventSystem();
            gameObject.SetActive(true);
            Refresh();
            foreach (Button slot in slots)
            {
                if (!slot.interactable) continue;
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(slot.gameObject);
                break;
            }
        }

        public void Hide()
        {
            if (!IsOpen) return;
            gameObject.SetActive(false);
            Action done = closed;
            closed = null;
            chosen = null;
            done?.Invoke();
        }

        private void Pick(int slot)
        {
            bool used = SaveGame.HasSave(slot);
            bool overwrite = used && (mode == Mode.NewGame || (mode == Mode.Save && slot != SaveGame.ActiveSlot));
            if (overwrite && confirming != slot)
            {
                confirming = slot;
                Refresh();
                return;
            }
            Action<int> done = chosen;
            Hide();
            done?.Invoke(slot);
        }

        private void Refresh()
        {
            title.text = mode == Mode.Save ? "SAVE GAME" : mode == Mode.Load ? "LOAD GAME" : "NEW GAME";
            hint.text = mode == Mode.Save ? "Pick a slot. The game keeps autosaving to it as you play."
                : mode == Mode.Load ? "Pick a save to carry on from, right where you saved it."
                : "Pick a slot for the new game. It autosaves there as you play.";
            for (int i = 0; i < slots.Length; i++)
            {
                int slot = i + 1;
                SaveData data = SaveGame.Peek(slot);
                bool current = slot == SaveGame.ActiveSlot && mode == Mode.Save;
                headings[i].text = data == null ? $"Slot {slot}  -  Empty" : $"Slot {slot}  -  {data.label}" + (current ? "  (this game)" : "");
                details[i].color = UiTheme.Muted;
                if (confirming == slot)
                {
                    details[i].text = mode == Mode.Save ? "Overwrite this save? Click again to confirm." : "Erase this save and start over? Click again to confirm.";
                    details[i].color = UiTheme.Warning;
                }
                else if (data == null) details[i].text = mode == Mode.Load ? "No save here yet" : "Start fresh here";
                else details[i].text = $"{TutorialRunTimer.Format(data.playSeconds)} played   -   saved {data.SavedLocal:MMM d, h:mm tt}";
                slots[i].interactable = mode != Mode.Load || data != null;
            }
        }

        private void Build(RectTransform root)
        {
            title = UiTheme.Label("Title", root, 50f, UiTheme.Accent, TextAlignmentOptions.Top, FontStyles.Bold);
            UiTheme.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(800f, 62f));
            hint = UiTheme.Label("Hint", root, 22f, UiTheme.Muted, TextAlignmentOptions.Top, FontStyles.Italic);
            UiTheme.Place(hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -96f), new Vector2(780f, 34f));

            for (int i = 0; i < slots.Length; i++)
            {
                int slot = i + 1;
                Button button = UiTheme.Button("Slot " + slot, root, "", 30f);
                UiTheme.Place((RectTransform)button.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f - i * 130f), new Vector2(760f, 112f));
                // The button's own label becomes the heading; a second, smaller line holds the details.
                TextMeshProUGUI heading = button.GetComponentInChildren<TextMeshProUGUI>();
                heading.alignment = TextAlignmentOptions.TopLeft;
                UiTheme.Stretch(heading.rectTransform, 16f);
                TextMeshProUGUI detail = UiTheme.Label("Details", button.transform, 22f, UiTheme.Muted, TextAlignmentOptions.BottomLeft);
                UiTheme.Stretch(detail.rectTransform, 16f);
                button.onClick.AddListener(() => Pick(slot));
                slots[i] = button;
                headings[i] = heading;
                details[i] = detail;
            }

            Button back = UiTheme.Button("Back", root, "Back", 26f);
            UiTheme.Place((RectTransform)back.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(220f, 58f));
            back.onClick.AddListener(Hide);
        }
    }
}
