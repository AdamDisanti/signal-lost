using System.Text;
using SignalLost.Combat;
using SignalLost.Inventory;
using SignalLost.Player;
using SignalLost.Scanner;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SignalLost.UI
{
    /// <summary>
    /// The player's inventory screen (meeting notes 2026-10-20), opened with Tab (gamepad View/Select): scrap sorted
    /// by type, the three pieces of gear with their current stats, and completed upgrades. A proof of concept with
    /// placeholder text; nothing here can be spent yet. Opening it frees the cursor; walking keeps working.
    /// </summary>
    public sealed class InventoryUi : MonoBehaviour
    {
        private static InventoryUi instance;

        private PlayerInventory inventory;
        private GameObject root;
        private RectTransform panel;
        private readonly TextMeshProUGUI[] scrapCounts = new TextMeshProUGUI[4];
        private TextMeshProUGUI laserText, scannerText, flaskText, upgradesText;
        private float shownAt, nextRefresh;

        /// <summary>The existing instance, or null. Use this in OnDisable/OnDestroy so nothing is created during unload.</summary>
        public static InventoryUi Current => instance;

        public static InventoryUi Instance
        {
            get
            {
                if (instance == null && Application.isPlaying)
                    instance = UiTheme.CreateCanvas("Inventory UI", 60).gameObject.AddComponent<InventoryUi>();
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
            UiMenus.Close(this);
            instance = null;
        }

        public void Bind(PlayerInventory target) => inventory = target;

        public void Open()
        {
            if (IsOpen || inventory == null) return;
            root.SetActive(true);
            shownAt = Time.unscaledTime;
            Refresh();
            UiMenus.Open(this, true);
        }

        public void Close()
        {
            if (!IsOpen) return;
            root.SetActive(false);
            UiMenus.Close(this);
        }

        private void Update()
        {
            bool toggle = Keyboard.current?.tabKey.wasPressedThisFrame == true || Gamepad.current?.selectButton.wasPressedThisFrame == true;
            if (IsOpen)
            {
                panel.localScale = Vector3.one * Mathf.LerpUnclamped(0.9f, 1f, UiTheme.Pop((Time.unscaledTime - shownAt) / 0.3f));
                if (inventory == null || toggle || Keyboard.current?.escapeKey.wasPressedThisFrame == true) { Close(); return; }
                if (Time.unscaledTime >= nextRefresh) Refresh();
                return;
            }
            // Only from normal play: not over another menu, a popup, a cutscene or while the cursor is free.
            if (toggle && inventory != null && !UiMenus.AnyOpen && !UiMenus.ChangedThisFrame &&
                Application.isFocused && Cursor.lockState == CursorLockMode.Locked) Open();
        }

        private void Refresh()
        {
            nextRefresh = Time.unscaledTime + 0.25f;
            for (int i = 0; i < scrapCounts.Length; i++) scrapCounts[i].text = inventory.Scrap(ScrapInfo.All[i]).ToString();

            var laser = inventory.GetComponent<MiningLaser>();
            laserText.text = !inventory.Has(PlayerItem.MiningLaser) || laser == null ? Missing("in the scorched rock basin") :
                $"Combo damage  {string.Join(" / ", laser.ComboDamage)}\n" +
                $"Charged bolt  {laser.MinShotDamage:0}-{laser.MaxShotDamage:0} damage, {laser.ShotRange:0} m; full charge bursts {laser.FullChargeBlastRadius:0} m\n" +
                $"Bolt cooldown  {laser.TapShotCooldown:0.#}-{laser.ShotCooldown:0.#} s (longer the more charged)" +
                (laser.ComboCooldown > 0f ? $"     Combo recharge  {laser.ComboCooldown:0.#} s\nFinisher stuns small critters for {laser.FinisherStun:0.#} s" : "");

            var scanner = inventory.GetComponentInChildren<ScannerController>(true);
            scannerText.text = !inventory.Has(PlayerItem.Scanner) ? Missing("somewhere in the wreckage") :
                $"Critter range  {ScannerRange(scanner)}\nQuest objectives: any distance\nQ  raise / lower (swaps with the laser)";

            var flask = inventory.GetComponent<PlayerFlask>();
            flaskText.text = !inventory.Has(PlayerItem.Flask) ? Missing("a crab ran off with it") : flask == null ? "F  take a sip" :
                $"Next sip  +{flask.NextSipHeal:0.#} HP\nDrunk  {flask.DrunkStacks} / {flask.BlackoutAfterStacks}  (blackout after that)\nF  take a sip";

            var upgrades = new StringBuilder();
            foreach (string upgrade in inventory.Upgrades) upgrades.Append("- ").Append(upgrade).Append('\n');
            upgradesText.text = upgrades.Length > 0 ? upgrades.ToString() :
                "None yet. Bucket & Barter is still stocking its shelves.";
        }

        private static string ScannerRange(ScannerController scanner) => scanner != null ? $"{scanner.Range:0} m" : "-";

        private static string Missing(string hint) => $"<color={UiTheme.Hex(UiTheme.Muted)}><i>Not found yet ({hint}).</i></color>";

        // ---------- Layout ----------

        private void Build()
        {
            var canvasRoot = (RectTransform)transform;
            root = UiTheme.Stretch(UiTheme.Rect("Inventory", canvasRoot)).gameObject;
            Image dim = UiTheme.Block("Dim", root.transform, UiTheme.Dim);
            UiTheme.Stretch(dim.rectTransform);
            dim.raycastTarget = true;

            Image background = UiTheme.Panel("Panel", root.transform, UiTheme.PanelColor, UiTheme.Edge, 1.4f);
            panel = UiTheme.Place(background.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1300f, 780f));

            TextMeshProUGUI title = UiTheme.Label("Title", panel, 54f, UiTheme.Accent, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UiTheme.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(48f, -30f), new Vector2(600f, 70f));
            title.text = "POCKETS";
            title.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 1.5f);
            TextMeshProUGUI tagline = UiTheme.Label("Tagline", panel, 24f, UiTheme.Muted, TextAlignmentOptions.TopLeft);
            UiTheme.Place(tagline.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(50f, -98f), new Vector2(1100f, 34f));
            tagline.text = "Everything you're carrying, sorted automatically. You can't be trusted to do it yourself.";

            Heading("Scrap", new Vector2(48f, -150f));
            for (int i = 0; i < ScrapInfo.All.Length; i++)
            {
                ScrapType type = ScrapInfo.All[i];
                Image row = UiTheme.Panel(ScrapInfo.Name(type), panel, UiTheme.PanelLight, null, 0.7f);
                UiTheme.Place(row.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(48f, -200f - i * 104f), new Vector2(560f, 92f));
                Image swatch = UiTheme.Panel("Swatch", row.transform, ScrapInfo.Color(type), null, 0.4f);
                UiTheme.Place(swatch.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(40f, 40f));
                TextMeshProUGUI name = UiTheme.Label("Name", row.transform, 30f, UiTheme.Text, TextAlignmentOptions.TopLeft, FontStyles.Bold);
                UiTheme.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(76f, -10f), new Vector2(360f, 38f));
                name.text = ScrapInfo.Name(type);
                TextMeshProUGUI blurb = UiTheme.Label("Blurb", row.transform, 22f, UiTheme.Muted, TextAlignmentOptions.TopLeft);
                UiTheme.Place(blurb.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(76f, -50f), new Vector2(380f, 32f));
                blurb.text = ScrapInfo.Blurb(type);
                scrapCounts[i] = UiTheme.Label("Count", row.transform, 46f, ScrapInfo.Color(type), TextAlignmentOptions.Right, FontStyles.Bold);
                UiTheme.Place(scrapCounts[i].rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-20f, 0f), new Vector2(110f, 60f));
            }

            Heading("Gear", new Vector2(660f, -150f));
            laserText = GearCard("Mining Laser", UiTheme.Accent, -200f, 150f);
            scannerText = GearCard("Scanner", UiTheme.SpeakerColor("Cavendish"), -362f, 120f);
            flaskText = GearCard("Scotch Flask", new Color(1f, 0.78f, 0.34f), -494f, 120f);

            Heading("Upgrades", new Vector2(48f, -628f));
            upgradesText = UiTheme.Label("Upgrades", panel, 26f, UiTheme.Text, TextAlignmentOptions.TopLeft);
            UiTheme.Place(upgradesText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(50f, -672f), new Vector2(900f, 80f));

            TextMeshProUGUI hint = UiTheme.Label("Hint", panel, 26f, UiTheme.Accent, TextAlignmentOptions.BottomRight, FontStyles.Bold);
            UiTheme.Place(hint.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 28f), new Vector2(500f, 36f));
            hint.text = "[Tab] / [Esc]  Close";
            root.SetActive(false);
        }

        private void Heading(string text, Vector2 position)
        {
            TextMeshProUGUI heading = UiTheme.Label(text, panel, 26f, UiTheme.Muted, TextAlignmentOptions.TopLeft, FontStyles.Bold | FontStyles.UpperCase);
            UiTheme.Place(heading.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), position, new Vector2(560f, 34f));
            heading.text = text;
        }

        private TextMeshProUGUI GearCard(string title, Color color, float y, float height)
        {
            Image card = UiTheme.Panel(title, panel, UiTheme.PanelLight, null, 0.7f);
            UiTheme.Place(card.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(660f, y), new Vector2(592f, height));
            TextMeshProUGUI name = UiTheme.Label("Name", card.transform, 30f, color, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UiTheme.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -10f), new Vector2(540f, 38f));
            name.text = title;
            TextMeshProUGUI body = UiTheme.Label("Stats", card.transform, 22f, UiTheme.Text, TextAlignmentOptions.TopLeft);
            body.rectTransform.anchorMin = Vector2.zero;
            body.rectTransform.anchorMax = Vector2.one;
            body.rectTransform.offsetMin = new Vector2(22f, 8f);
            body.rectTransform.offsetMax = new Vector2(-18f, -48f);
            return body;
        }
    }
}
