using SignalLost.Core;
using SignalLost.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SignalLost.UI
{
    // Save and load on the title screen (Christian, 10-27). This is the save-only part of MainMenuScreen; the screen is
    // in UI/MainMenuScreen.cs, which only calls AddSaveMenu(root) at the end of Build.
    //
    // New Game picks one of the three save slots to autosave into, Continue loads the most recent save, and a new Load
    // Game entry (under Continue) opens the slots. The "(no save yet)" note shows the latest save's quest instead.
    public sealed partial class MainMenuScreen
    {
        private const float SaveMenuSpacing = 86f; // same gap as the entries in Build
        private GameObject saveSlotsRoot;
        private SaveSlotPanel saveSlots;

        private void AddSaveMenu(RectTransform root)
        {
            int recent = SaveGame.MostRecentSlot;

            // New Game now asks for a slot first.
            Button newGame = EntryButton(root, "New Game");
            if (newGame != null)
            {
                newGame.onClick.RemoveAllListeners();
                newGame.onClick.AddListener(() => ShowSaveSlots(SaveSlotPanel.Mode.NewGame, slot => SaveGame.StartNew(slot, SceneNames.FirstLevel)));
            }

            // Continue loads the most recent save.
            Button resume = EntryButton(root, "Continue");
            if (resume != null)
            {
                resume.onClick.RemoveAllListeners();
                resume.onClick.AddListener(() =>
                {
                    int slot = SaveGame.MostRecentSlot;
                    if (slot > 0) SaveGame.Load(slot);
                });
                resume.interactable = recent > 0;
            }

            // Load Game goes under Continue; the entries below it move down one place.
            RectTransform under = resume != null ? (RectTransform)resume.transform.parent : null;
            if (under != null)
            {
                float y = under.anchoredPosition.y - SaveMenuSpacing;
                foreach ((RectTransform rect, float phase) entry in buttons)
                    if (entry.rect.anchoredPosition.y < under.anchoredPosition.y - 1f)
                        entry.rect.anchoredPosition += new Vector2(0f, -SaveMenuSpacing);
                Button load = TextButton(root, "Load Game", new Vector2(under.anchoredPosition.x, y));
                load.onClick.AddListener(() => ShowSaveSlots(SaveSlotPanel.Mode.Load, slot => SaveGame.Load(slot)));
                load.interactable = recent > 0;
                buttons.Add(((RectTransform)load.transform.parent, buttons.Count * 1.3f));
            }

            Transform note = root.Find("No Save");
            if (note != null && note.TryGetComponent(out TextMeshProUGUI noSave) && recent > 0)
                noSave.text = $"({SaveGame.Peek(recent).label})";

            // The slots, over everything else on the screen.
            saveSlotsRoot = UiTheme.Stretch(UiTheme.Rect("Slots Overlay", root)).gameObject;
            Image dim = UiTheme.Block("Dim", saveSlotsRoot.transform, new Color(0f, 0f, 0f, 0.55f));
            UiTheme.Stretch(dim.rectTransform);
            dim.raycastTarget = true;
            saveSlots = SaveSlotPanel.Create(saveSlotsRoot.transform);
            saveSlotsRoot.SetActive(false);
        }

        private void ShowSaveSlots(SaveSlotPanel.Mode mode, System.Action<int> chosen)
        {
            saveSlotsRoot.SetActive(true);
            saveSlots.Show(mode, chosen, () =>
            {
                saveSlotsRoot.SetActive(false);
                Button first = EntryButton((RectTransform)saveSlotsRoot.transform.parent, "New Game");
                if (EventSystem.current != null && first != null) EventSystem.current.SetSelectedGameObject(first.gameObject);
            });
        }

        // Build names each entry "<label> Entry", with the clickable label inside it.
        private static Button EntryButton(RectTransform root, string label)
        {
            Transform entry = root.Find(label + " Entry");
            Transform text = entry != null ? entry.Find(label) : null;
            return text != null ? text.GetComponent<Button>() : null;
        }
    }
}
