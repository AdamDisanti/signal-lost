using SignalLost.Inventory;
using SignalLost.UI;
using UnityEngine;

namespace SignalLost.Core
{
    /// <summary>
    /// Drop the GameSystems prefab (Prefabs/Core) into any playable scene next to the Player prefab. It brings up the
    /// shared game UI (<see cref="GameHud"/>, dialogue, quest tracker and item cards, inventory, pause menu), an
    /// EventSystem for clicking menus, and applies the saved settings. Everything else finds the player by itself.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class GameSystems : MonoBehaviour
    {
        private void Awake()
        {
            Time.timeScale = 1f;
            GameSettings.Apply();
            UiTheme.EnsureEventSystem();
            _ = GameHud.Instance;
            _ = DialogueUi.Instance;
            _ = GameplayOverlay.Instance;
            _ = PauseMenu.Instance;
        }

        private void Start()
        {
            var inventory = PlayerInventory.Current != null ? PlayerInventory.Current : FindAnyObjectByType<PlayerInventory>();
            if (inventory != null) InventoryUi.Instance.Bind(inventory);
        }
    }
}
