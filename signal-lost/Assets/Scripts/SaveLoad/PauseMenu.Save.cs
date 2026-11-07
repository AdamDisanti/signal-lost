using SignalLost.Progression;
using UnityEngine;

namespace SignalLost.UI
{
    // Save and load in the pause menu (Christian, 10-27). This is the save-only part of PauseMenu; the menu is in
    // UI/PauseMenu.cs, whose Save button calls SaveFromMenu() first.
    //
    // Save Game opens the three save slots in the pause panel's place. Outside combat, picking a slot saves there;
    // in a fight (or mid-jump, mid-conversation) a message says why it can't save yet.
    public sealed partial class PauseMenu
    {
        private SaveSlotPanel saveSlots;

        /// <summary>Handles the Save Game button. Always returns true, so the old placeholder message never shows.</summary>
        private bool SaveFromMenu()
        {
            if (!SaveGame.CanSaveNow(out string reason))
            {
                ShowSaveToast(reason);
                return true;
            }
            if (saveSlots == null) saveSlots = SaveSlotPanel.Create(root.transform);
            panel.gameObject.SetActive(false);
            saveSlots.Show(SaveSlotPanel.Mode.Save,
                slot => ShowSaveToast(SaveGame.SaveTo(slot) ? $"Saved to slot {slot}." : "Couldn't save. Check the Console for details."),
                () =>
                {
                    openedFrame = Time.frameCount; // the Esc that closed the slots must not also close the pause menu
                    if (IsOpen) panel.gameObject.SetActive(true);
                });
            return true;
        }

        private void ShowSaveToast(string message)
        {
            toast.text = message;
            toastUntil = Time.unscaledTime + 3f;
        }
    }
}
