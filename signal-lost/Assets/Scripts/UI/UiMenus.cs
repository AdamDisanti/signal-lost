using System.Collections.Generic;
using SignalLost.Player;
using UnityEngine;

namespace SignalLost.UI
{
    /// <summary>
    /// Tracks which screen-space menus (quest popups, inventory, merchant menu) are open. While any is open the
    /// cursor is freed and mouse look pauses; walking keeps working, as the lead asked for NPC popups.
    /// The frame a menu opens or closes is remembered so the same E press cannot also trigger a world interaction.
    /// </summary>
    public static class UiMenus
    {
        private static readonly HashSet<object> open = new HashSet<object>();
        private static readonly HashSet<object> fullScreen = new HashSet<object>();
        private static int changedFrame = -1;

        public static bool AnyOpen => open.Count > 0;
        /// <summary>True while a full-screen menu (inventory, shop) is open; the HUD steps aside for those.</summary>
        public static bool FullScreenOpen => fullScreen.Count > 0;
        public static bool ChangedThisFrame => changedFrame == Time.frameCount;
        public static bool IsOpen(object owner) => open.Contains(owner);

        public static void Open(object owner, bool isFullScreen = false)
        {
            if (isFullScreen) fullScreen.Add(owner);
            if (!open.Add(owner)) return;
            changedFrame = Time.frameCount;
            if (open.Count == 1) SetMenuState(true);
        }

        public static void Close(object owner)
        {
            fullScreen.Remove(owner);
            if (!open.Remove(owner)) return;
            changedFrame = Time.frameCount;
            if (open.Count == 0) SetMenuState(false);
        }

        // Domain reload may be disabled in the editor; never carry menu state between Play sessions.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            open.Clear();
            fullScreen.Clear();
            changedFrame = -1;
        }

        private static void SetMenuState(bool menuOpen)
        {
            // Also told while disabled (cutscenes, respawn), so it never keeps a stale "menu open" flag.
            var controller = Object.FindFirstObjectByType<FirstPersonController>();
            if (controller != null) controller.SetMenuOpen(menuOpen);
            else if (menuOpen)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }
}
