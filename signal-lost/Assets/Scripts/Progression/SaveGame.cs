using System;
using System.Collections.Generic;
using System.IO;
using SignalLost.Combat;
using SignalLost.Enemies.Desert;
using SignalLost.Inventory;
using SignalLost.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SignalLost.Progression
{
    /// <summary>
    /// Everything one save slot remembers. Stored as JSON (<see cref="JsonUtility"/>), so only public fields of simple
    /// types and lists. Enums are stored by name, so reordering an enum never scrambles old saves.
    /// Bump <see cref="CurrentVersion"/> when a change would make old saves load wrongly.
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        /// <summary>When it was saved (UTC ticks), for the slot list and for Continue.</summary>
        public long savedTicks;

        // Where
        /// <summary>The scene to load (a level that implements <see cref="ISaveableLevel"/>).</summary>
        public string scene;
        /// <summary>The level's own progress marker, e.g. a tutorial story stage name.</summary>
        public string stage;
        /// <summary>Shown in the slot list, e.g. the current quest's title.</summary>
        public string label;
        /// <summary>Level-specific yes/no facts, e.g. "partGiven".</summary>
        public List<string> flags = new List<string>();
        /// <summary>Progress inside the current step: names of enemies already beaten (the cousins).</summary>
        public List<string> downed = new List<string>();
        /// <summary>Progress inside the current step as a number (frogs beaten).</summary>
        public int count;

        // Player
        /// <summary>True when <see cref="position"/> holds where the player was standing.</summary>
        public bool hasPosition;
        public Vector3 position;
        /// <summary>Which way the player faced (degrees around the up axis).</summary>
        public float yaw;
        /// <summary>Health when saved; 0 in saves made before it was kept (those load at full health).</summary>
        public float health;
        /// <summary>Drinks in the system, and seconds until the current one wears off.</summary>
        public int drinks;
        public float drinkSecondsLeft;

        // Scrap lying on the ground, not yet picked up
        public List<string> droppedScrap = new List<string>();
        public List<Vector3> droppedAt = new List<Vector3>();

        // Run
        public float playSeconds;
        public int deaths;

        // Pockets
        public List<string> items = new List<string>();
        public List<int> scrap = new List<int>();
        public List<string> upgrades = new List<string>();
        public List<string> supplyIds = new List<string>();
        public List<int> supplyCounts = new List<int>();
        public List<string> keyItems = new List<string>();

        // Progress
        public List<string> partNames = new List<string>();
        public List<string> partStages = new List<string>();
        public List<string> clearedStages = new List<string>();
        public List<string> unlockedStages = new List<string>();
        public List<string> mainParts = new List<string>();
        public List<string> trophies = new List<string>();
        public List<int> stands = new List<int>();

        public bool HasFlag(string flag) => flags.Contains(flag);

        public void SetFlag(string flag, bool on)
        {
            if (on && !flags.Contains(flag)) flags.Add(flag);
            else if (!on) flags.Remove(flag);
        }

        public DateTime SavedLocal => new DateTime(Math.Max(0L, savedTicks), DateTimeKind.Utc).ToLocalTime();
    }

    /// <summary>
    /// A playable level that can be saved and restored (the tutorial story for now; the desert later). It registers
    /// itself with <see cref="SaveGame.Register"/> while enabled, writes its own progress into a save, and on Start
    /// calls <see cref="SaveGame.TakePending"/> to see whether it should restore one instead of starting fresh.
    /// </summary>
    public interface ISaveableLevel
    {
        /// <summary>False (with a reason for the player) while saving makes no sense, e.g. during a cutscene.</summary>
        bool CanSave(out string reason);

        /// <summary>Fills in the level's part of a save: scene, stage, label, flags and run time.</summary>
        void Capture(SaveData data);
    }

    /// <summary>
    /// Save and load (10-24): three save slots, autosaves at quest milestones and purchases, and manual saves from the
    /// pause menu anywhere outside combat. A save keeps where the player stood, the pockets, and progress inside the
    /// current step (enemies already beaten stay beaten), so loading never hands out the same loot twice. A fight in
    /// progress cannot be saved; an autosave that falls during one waits until it is over (<see cref="Tick"/>). Slots are JSON files in the player's data folder
    /// (<c>signal_lost_save_1.json</c> to <c>_3</c>). The camp's trophies (<see cref="CampSave"/>), the recovered
    /// ship parts and stage progress are part of each slot.
    /// </summary>
    public static class SaveGame
    {
        public const int SlotCount = 3;

        private static ISaveableLevel level;
        private static SaveData pending;
        private static bool autosaveWaiting;
        private static float nextWaitingCheck;
        private static readonly SaveData[] cache = new SaveData[SlotCount + 1];
        private static readonly bool[] cached = new bool[SlotCount + 1];

        /// <summary>The slot this play session saves into (1-3), or 0 if none was chosen yet (e.g. Play pressed in a level).</summary>
        public static int ActiveSlot { get; private set; }

        /// <summary>Raised after a save is written. Arguments: slot, whether it was an autosave.</summary>
        public static event Action<int, bool> Saved;

        public static string PathFor(int slot) => Path.Combine(Application.persistentDataPath, $"signal_lost_save_{slot}.json");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            level = null;
            pending = null;
            autosaveWaiting = false;
            nextWaitingCheck = 0f;
            ActiveSlot = 0;
            Saved = null;
            Array.Clear(cache, 0, cache.Length);
            Array.Clear(cached, 0, cached.Length);
        }

        // ---------- Levels ----------

        public static void Register(ISaveableLevel saveable)
        {
            level = saveable;
            autosaveWaiting = false;
        }

        public static void Unregister(ISaveableLevel saveable)
        {
            if (level != saveable) return;
            level = null;
            autosaveWaiting = false;
        }

        /// <summary>Whether the pause menu's Save Game can save right now, and if not, why.</summary>
        public static bool CanSaveNow(out string reason)
        {
            if (level == null)
            {
                reason = "This area can't be saved.";
                return false;
            }
            return level.CanSave(out reason);
        }

        /// <summary>
        /// The save to restore, if the scene was loaded from one (Continue or Load Game), and only once. Levels call
        /// this in Start; a null result means start the level fresh.
        /// </summary>
        public static SaveData TakePending()
        {
            SaveData data = pending;
            pending = null;
            if (data != null && data.scene != SceneManager.GetActiveScene().name) return null;
            return data;
        }

        // ---------- Slots ----------

        /// <summary>The save in a slot, or null if it is empty or unreadable.</summary>
        public static SaveData Peek(int slot)
        {
            if (slot < 1 || slot > SlotCount) return null;
            if (cached[slot]) return cache[slot];
            cached[slot] = true;
            cache[slot] = null;
            try
            {
                string path = PathFor(slot);
                if (File.Exists(path))
                {
                    SaveData data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                    if (data != null && !string.IsNullOrEmpty(data.scene)) cache[slot] = data;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Could not read save slot {slot}. " + exception.Message);
            }
            return cache[slot];
        }

        public static bool HasSave(int slot) => Peek(slot) != null;

        /// <summary>The most recently saved slot (for Continue), or 0 if there are no saves.</summary>
        public static int MostRecentSlot
        {
            get
            {
                int best = 0;
                long bestTicks = long.MinValue;
                for (int slot = 1; slot <= SlotCount; slot++)
                {
                    SaveData data = Peek(slot);
                    if (data == null || data.savedTicks <= bestTicks) continue;
                    best = slot;
                    bestTicks = data.savedTicks;
                }
                return best;
            }
        }

        public static void Delete(int slot)
        {
            if (slot < 1 || slot > SlotCount) return;
            try { if (File.Exists(PathFor(slot))) File.Delete(PathFor(slot)); }
            catch (Exception exception) { Debug.LogWarning($"Could not delete save slot {slot}. " + exception.Message); }
            cached[slot] = true;
            cache[slot] = null;
        }

        // ---------- Saving ----------

        /// <summary>Saves the current game into a slot, which becomes the active slot. Returns false if it could not.</summary>
        public static bool SaveTo(int slot, bool autosave = false)
        {
            if (slot < 1 || slot > SlotCount || level == null) return false;
            if (!level.CanSave(out _)) return false;
            var data = new SaveData { savedTicks = DateTime.UtcNow.Ticks };
            level.Capture(data);
            CaptureShared(data);
            if (slot == ActiveSlot || ActiveSlot == 0) autosaveWaiting = false; // this save covers it
            if (string.IsNullOrEmpty(data.scene)) data.scene = SceneManager.GetActiveScene().name;
            try
            {
                // Write to a temporary file first, so a crash mid-write never leaves a broken slot.
                string path = PathFor(slot), temporary = path + ".tmp";
                File.WriteAllText(temporary, JsonUtility.ToJson(data, true));
                if (File.Exists(path)) File.Delete(path);
                File.Move(temporary, path);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Could not write save slot {slot}. " + exception.Message);
                return false;
            }
            cached[slot] = true;
            cache[slot] = data;
            ActiveSlot = slot;
            Saved?.Invoke(slot, autosave);
            return true;
        }

        /// <summary>
        /// Saves into the active slot, if there is one. If the level can't save right now (a fight, mid-jump), the
        /// autosave waits and happens as soon as it can (<see cref="Tick"/>).
        /// </summary>
        public static void Autosave()
        {
            if (ActiveSlot <= 0 || level == null) return;
            if (level.CanSave(out _)) { autosaveWaiting = false; SaveTo(ActiveSlot, true); }
            else autosaveWaiting = true;
        }

        /// <summary>True while an autosave is waiting for a fight to end.</summary>
        public static bool AutosaveWaiting => autosaveWaiting;

        /// <summary>Levels call this every frame: it writes a waiting autosave once saving is allowed again.</summary>
        public static void Tick()
        {
            if (!autosaveWaiting || level == null || Time.unscaledTime < nextWaitingCheck) return;
            nextWaitingCheck = Time.unscaledTime + 0.5f; // the combat check looks through the scene
            if (!level.CanSave(out _)) return;
            autosaveWaiting = false;
            SaveTo(ActiveSlot, true);
        }

        private static void CaptureShared(SaveData data)
        {
            PlayerInventory inventory = PlayerInventory.Current;
            if (inventory != null && inventory.TryGetComponent(out Health playerHealth)) data.health = playerHealth.Current;
            if (inventory != null && inventory.TryGetComponent(out PlayerFlask flask))
            {
                data.drinks = flask.DrunkStacks;
                data.drinkSecondsLeft = flask.StackSecondsLeft;
            }
            foreach (ScrapPickup scrap in UnityEngine.Object.FindObjectsByType<ScrapPickup>())
            {
                data.droppedScrap.Add(scrap.Type.ToString());
                data.droppedAt.Add(scrap.transform.position);
            }
            if (inventory != null)
            {
                foreach (PlayerItem item in inventory.Items) data.items.Add(item.ToString());
                foreach (ScrapType type in ScrapInfo.All) data.scrap.Add(inventory.Scrap(type));
                data.upgrades.AddRange(inventory.Upgrades);
                foreach (KeyValuePair<string, int> supply in inventory.Supplies)
                {
                    if (supply.Value <= 0) continue;
                    data.supplyIds.Add(supply.Key);
                    data.supplyCounts.Add(supply.Value);
                }
                data.keyItems.AddRange(inventory.KeyItems);
            }
            ShipPartDrop.CaptureRecovered(data.partNames, data.partStages);
            StageProgress.Capture(data.clearedStages, data.unlockedStages, data.mainParts);
            CampSave.Capture(data.trophies, data.stands);
        }

        // ---------- Starting and loading ----------

        /// <summary>
        /// New Game in a slot: clears that slot and everything carried between scenes, then loads the first level.
        /// The first autosave fills the slot again.
        /// </summary>
        public static void StartNew(int slot, string firstScene)
        {
            Delete(slot);
            ActiveSlot = slot;
            pending = null;
            ResetSharedProgress();
            SceneManager.LoadScene(firstScene);
        }

        /// <summary>Loads a slot: restores the progress carried between scenes, then the saved scene, which restores the rest.</summary>
        public static bool Load(int slot)
        {
            SaveData data = Peek(slot);
            if (data == null) return false;
            if (data.version > SaveData.CurrentVersion)
                Debug.LogWarning($"Save slot {slot} is from a newer version of the game; loading it anyway.");
            ActiveSlot = slot;
            pending = data;
            ShipPartDrop.RestoreRecovered(data.partNames, data.partStages);
            StageProgress.Restore(data.clearedStages, data.unlockedStages, data.mainParts);
            CampSave.Restore(data.trophies, data.stands);
            SceneManager.LoadScene(data.scene);
            return true;
        }

        private static void ResetSharedProgress()
        {
            ShipPartDrop.RestoreRecovered(null, null);
            StageProgress.Restore(null, null, null);
            CampSave.Clear();
        }

        /// <summary>
        /// Puts the player's health and drinks back as they were (call after upgrades are applied, since they change
        /// max health and how many drinks fit). Saves from before these were kept leave the player fresh.
        /// </summary>
        public static void RestorePlayerState(SaveData data, PlayerInventory inventory)
        {
            if (data == null || inventory == null) return;
            if (data.health > 0f && inventory.TryGetComponent(out Health playerHealth)) playerHealth.SetCurrent(data.health);
            if (inventory.TryGetComponent(out PlayerFlask flask)) flask.RestoreStacks(data.drinks, data.drinkSecondsLeft);
        }

        /// <summary>Puts back the scrap that was lying on the ground, where it lay (it drops into place).</summary>
        public static void RestoreDroppedScrap(SaveData data)
        {
            if (data == null) return;
            for (int i = 0; i < data.droppedScrap.Count && i < data.droppedAt.Count; i++)
                if (Enum.TryParse(data.droppedScrap[i], out ScrapType type))
                    ScrapPickup.Spawn(type, data.droppedAt[i] + Vector3.up * 0.2f, Vector3.zero);
        }

        /// <summary>
        /// Puts a save's pockets back on the player: gear, scrap, upgrades, supplies and key items. The level then
        /// re-applies upgrades and refreshes the gear in hand.
        /// </summary>
        public static void RestorePockets(SaveData data, PlayerInventory inventory)
        {
            if (data == null || inventory == null) return;
            var items = new List<PlayerItem>();
            foreach (string name in data.items)
                if (Enum.TryParse(name, out PlayerItem item)) items.Add(item);
            var supplies = new Dictionary<string, int>();
            for (int i = 0; i < data.supplyIds.Count && i < data.supplyCounts.Count; i++) supplies[data.supplyIds[i]] = data.supplyCounts[i];
            inventory.Restore(items, data.scrap, data.upgrades, supplies, data.keyItems);
        }
    }
}
