using System.Collections.Generic;
using System.Linq;
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
    /// The pockets menu (Tab, gamepad View/Select; meeting notes 10-23), four tabs (click or 1-4):
    /// <list type="bullet">
    /// <item><b>Materials:</b> scrap by type, with its model's icon, and key items (mini-boss drops).</item>
    /// <item><b>Gear:</b> the mechanic in 3D on the left (<see cref="GearFigure"/>, drag to turn) wearing every
    /// upgrade, close-ups of the laser, scanner and flask with their upgrades, and supplies to use.</item>
    /// <item><b>Upgrades:</b> what Bucket &amp; Barter sells for this area: installed, affordable or what it costs.</item>
    /// <item><b>Stats:</b> the numbers behind the gear.</item>
    /// </list>
    /// There is no crafting: upgrades are fixed recipes bought from the merchant. Opening it frees the cursor;
    /// walking keeps working.
    /// </summary>
    public sealed class InventoryUi : MonoBehaviour
    {
        private static readonly string[] Tabs = { "Materials", "Gear", "Upgrades", "Stats" };
        private static InventoryUi instance;

        private PlayerInventory inventory;
        private GearFigure figure;
        private GameObject root;
        private RectTransform panel, page;
        private readonly List<Button> tabButtons = new List<Button>();
        private int tab;
        private bool dirty;
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
        public int CurrentTab => tab;
        public GearFigure Figure => figure;

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            Build();
        }

        private void OnDestroy()
        {
            if (figure != null) Destroy(figure.gameObject);
            if (instance != this) return;
            Unbind();
            UiMenus.Close(this);
            instance = null;
        }

        public void Bind(PlayerInventory target)
        {
            Unbind();
            inventory = target;
            if (inventory == null) return;
            inventory.Changed += MarkDirty;
            inventory.ScrapChanged += OnScrapChanged;
        }

        private void Unbind()
        {
            if (inventory == null) return;
            inventory.Changed -= MarkDirty;
            inventory.ScrapChanged -= OnScrapChanged;
        }

        private void MarkDirty() => dirty = true;
        private void OnScrapChanged(ScrapType type, int change, int total) => dirty = true;

        public void Open(int startTab = -1)
        {
            if (IsOpen || inventory == null) return;
            root.SetActive(true);
            shownAt = Time.unscaledTime;
            ShowTab(startTab >= 0 ? startTab : tab);
            UiMenus.Open(this, true);
        }

        public void Close()
        {
            if (!IsOpen) return;
            root.SetActive(false);
            if (figure != null) figure.Show(false);
            UiMenus.Close(this);
        }

        public void ShowTab(int index)
        {
            tab = Mathf.Clamp(index, 0, Tabs.Length - 1);
            for (int i = 0; i < tabButtons.Count; i++)
            {
                bool selected = i == tab;
                tabButtons[i].GetComponent<Image>().color = selected ? UiTheme.Accent : UiTheme.PanelLight;
                tabButtons[i].GetComponentInChildren<TextMeshProUGUI>().color = selected ? new Color(0.08f, 0.08f, 0.12f) : UiTheme.Text;
            }
            Rebuild();
        }

        private void Update()
        {
            bool toggle = Keyboard.current?.tabKey.wasPressedThisFrame == true || Gamepad.current?.selectButton.wasPressedThisFrame == true;
            if (IsOpen)
            {
                panel.localScale = Vector3.one * Mathf.LerpUnclamped(0.9f, 1f, UiTheme.Pop((Time.unscaledTime - shownAt) / 0.3f));
                if (inventory == null || toggle || Keyboard.current?.escapeKey.wasPressedThisFrame == true) { Close(); return; }
                for (int i = 0; i < Tabs.Length; i++)
                    if (Keyboard.current != null && Keyboard.current[Key.Digit1 + i].wasPressedThisFrame) ShowTab(i);
                // Stats change as you drink and sober up; other pages only when the pockets do.
                if (dirty || (tab == 3 && Time.unscaledTime >= nextRefresh)) Rebuild();
                return;
            }
            // Only from normal play: not over another menu, a popup, a cutscene or while the cursor is free.
            if (toggle && inventory != null && !UiMenus.AnyOpen && !UiMenus.ChangedThisFrame &&
                Application.isFocused && Cursor.lockState == CursorLockMode.Locked) Open();
        }

        private void Rebuild()
        {
            dirty = false;
            nextRefresh = Time.unscaledTime + 0.5f;
            foreach (Transform child in page) Destroy(child.gameObject);
            if (figure != null) figure.Show(tab == 1);
            switch (tab)
            {
                case 0: MaterialsPage(); break;
                case 1: GearPage(); break;
                case 2: UpgradesPage(); break;
                default: StatsPage(); break;
            }
        }

        // ---------- Pages ----------

        private void MaterialsPage()
        {
            Heading(page, "Scrap", new Vector2(0f, 0f));
            for (int i = 0; i < ScrapInfo.All.Length; i++)
            {
                ScrapType type = ScrapInfo.All[i];
                RectTransform row = ItemRows.Row(page, ScrapInfo.Name(type), ScrapCatalog.Icon(type), ScrapInfo.Name(type), ScrapInfo.Color(type),
                    ScrapInfo.Blurb(type), new Vector2(0f, -44f - i * 112f), new Vector2(860f, 100f));
                ItemRows.RightLabel(row, inventory.Scrap(type).ToString(), 52f, ScrapInfo.Color(type));
            }
            Heading(page, "Key items", new Vector2(900f, 0f));
            string[] keys = inventory.KeyItems.ToArray();
            TextMeshProUGUI list = UiTheme.Label("Key Items", page, 24f, keys.Length > 0 ? UiTheme.Text : UiTheme.Muted, TextAlignmentOptions.TopLeft);
            UiTheme.Place(list.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(900f, -44f), new Vector2(480f, 400f));
            list.text = keys.Length > 0 ? string.Join("\n", keys.Select(k => "- " + k)) :
                "<i>None yet. Mini-bosses drop rare parts that some upgrades need.</i>";
        }

        private void GearPage()
        {
            if (figure == null) figure = GearFigure.Create(inventory.GetComponent<PlayerGear>());
            figure.Refresh(inventory);
            figure.Show(true);

            // The mechanic, large, on the left.
            Image frame = UiTheme.Panel("Figure Frame", page, new Color(0.08f, 0.1f, 0.16f, 0.9f), UiTheme.Edge, 0.9f);
            RectTransform frameRect = UiTheme.Place(frame.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(420f, 610f));
            var view = new GameObject("Figure", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            view.transform.SetParent(frameRect, false);
            view.texture = figure.Texture(GearFigure.Shot.Figure);
            UiTheme.Stretch(view.rectTransform);
            view.gameObject.AddComponent<DragToTurn>().Figure = figure;
            TextMeshProUGUI drag = UiTheme.Label("Drag", frameRect, 20f, UiTheme.Muted, TextAlignmentOptions.Bottom);
            UiTheme.Place(drag.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(400f, 28f));
            drag.text = "Drag to turn";

            // The three tools, each with a close-up and what is fitted to it.
            (GearFigure.Shot shot, UpgradeSlot slot, PlayerItem item, string missing)[] tools =
            {
                (GearFigure.Shot.Laser, UpgradeSlot.Laser, PlayerItem.MiningLaser, "Somewhere in the wreck."),
                (GearFigure.Shot.Scanner, UpgradeSlot.Scanner, PlayerItem.Scanner, "Somewhere in the wreck."),
                (GearFigure.Shot.Flask, UpgradeSlot.Flask, PlayerItem.Flask, "A crab ran off with it.")
            };
            for (int i = 0; i < tools.Length; i++)
            {
                (GearFigure.Shot shot, UpgradeSlot slot, PlayerItem item, string missing) = tools[i];
                Image card = UiTheme.Panel(slot.ToString(), page, UiTheme.PanelLight, null, 0.7f);
                RectTransform rect = UiTheme.Place(card.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(440f, -i * 158f), new Vector2(560f, 146f));
                if (inventory.Has(item))
                {
                    var close = new GameObject("Close-up", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
                    close.transform.SetParent(rect, false);
                    close.texture = figure.Texture(shot);
                    UiTheme.Place(close.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(195f, 130f));
                }
                TextMeshProUGUI name = UiTheme.Label("Name", rect, 28f, ItemRows.SlotColor(slot), TextAlignmentOptions.TopLeft, FontStyles.Bold);
                UiTheme.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(214f, -10f), new Vector2(330f, 36f));
                name.text = ItemRows.SlotName(slot);
                string[] fitted = inventory.Upgrades.Select(UpgradeCatalog.Find).Where(u => u != null && u.Slot == slot).Select(u => "+ " + u.Name).ToArray();
                TextMeshProUGUI body = UiTheme.Label("Fitted", rect, 21f, UiTheme.Text, TextAlignmentOptions.TopLeft);
                UiTheme.Place(body.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(214f, -48f), new Vector2(330f, 92f));
                body.text = !inventory.Has(item) ? $"<color={UiTheme.Hex(UiTheme.Muted)}><i>Not found yet. {missing}</i></color>" :
                    fitted.Length > 0 ? string.Join("\n", fitted) : $"<color={UiTheme.Hex(UiTheme.Muted)}>No upgrades fitted yet.</color>";
            }
            string[] suit = inventory.Upgrades.Select(UpgradeCatalog.Find).Where(u => u != null && u.Slot == UpgradeSlot.Suit).Select(u => "+ " + u.Name).ToArray();
            TextMeshProUGUI suitText = UiTheme.Label("Suit", page, 22f, UiTheme.Text, TextAlignmentOptions.TopLeft);
            UiTheme.Place(suitText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(440f, -478f), new Vector2(560f, 120f));
            suitText.text = $"<b><color={UiTheme.Hex(ItemRows.SlotColor(UpgradeSlot.Suit))}>Suit</color></b>\n" +
                (suit.Length > 0 ? string.Join("   ", suit) : $"<color={UiTheme.Hex(UiTheme.Muted)}>Standard-issue Maintenance Corps overalls.</color>");

            // Supplies, with a Use button each.
            Heading(page, "Supplies", new Vector2(1030f, 0f));
            int row = 0;
            foreach (Upgrade supply in UpgradeCatalog.All.Where(u => u.IsSupply))
            {
                int count = inventory.SupplyCount(supply.Id);
                RectTransform rect = ItemRows.Row(page, supply.Name, ItemCatalog.Icon(supply.Id), supply.Name,
                    count > 0 ? UiTheme.Text : UiTheme.Muted, $"<b><color={UiTheme.Hex(UiTheme.Text)}>You have {count}</color></b>\n{supply.Description}", new Vector2(1020f, -44f - row * 176f), new Vector2(370f, 166f), 16f);
                Button use = UiTheme.Button("Use", rect, "Use", 22f);
                UiTheme.Place((RectTransform)use.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-12f, 10f), new Vector2(100f, 40f));
                string id = supply.Id;
                use.interactable = count > 0;
                use.onClick.AddListener(() =>
                {
                    var upgrades = inventory.GetComponent<PlayerUpgrades>();
                    if (upgrades != null) upgrades.UseSupply(id);
                    dirty = true;
                });
                row++;
            }
        }

        private void UpgradesPage()
        {
            TextMeshProUGUI note = UiTheme.Label("Note", page, 22f, UiTheme.Muted, TextAlignmentOptions.TopLeft, FontStyles.Italic);
            UiTheme.Place(note.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(1380f, 32f));
            note.text = "No tinkering in the field: every upgrade is a set recipe. Buy them from Eugene at Bucket & Barter.";
            int i = 0;
            foreach (Upgrade upgrade in UpgradeCatalog.All.Where(u => u.OneOff && u.Region == UpgradeRegion.Tutorial))
            {
                bool owned = inventory.Owns(upgrade.Id);
                Vector2 at = new Vector2((i % 2) * 700f, -44f - (i / 2) * 128f);
                RectTransform row = ItemRows.Row(page, upgrade.Name, ItemCatalog.Icon(upgrade.Id), upgrade.Name, owned ? UiTheme.Good : ItemRows.SlotColor(upgrade.Slot),
                    $"<color={UiTheme.Hex(ItemRows.SlotColor(upgrade.Slot))}>{ItemRows.SlotName(upgrade.Slot)}</color>  {upgrade.Description}", at, new Vector2(680f, 116f), 250f);
                string why = inventory.WhyNot(upgrade);
                ItemRows.RightLabel(row, owned ? "INSTALLED" : why == null ? "Affordable" : ItemRows.Price(upgrade, inventory), owned ? 26f : 20f,
                    owned ? UiTheme.Good : why == null ? UiTheme.Accent : UiTheme.Text, 230f);
                i++;
            }
        }

        private void StatsPage()
        {
            var laser = inventory.GetComponent<MiningLaser>();
            var scanner = inventory.GetComponentInChildren<ScannerController>(true);
            var flask = inventory.GetComponent<PlayerFlask>();
            var health = inventory.GetComponent<Health>();
            var movement = inventory.GetComponent<FirstPersonController>();
            var left = new List<(string, string)>
            {
                ("Health", health != null ? $"{Mathf.CeilToInt(health.Current)} / {health.Max:0}" : "-"),
                ("Next sip heals", flask != null && inventory.Has(PlayerItem.Flask) ? $"+{flask.NextSipHeal:0} HP" : "No flask"),
                ("Drinks in you", flask != null ? $"{flask.DrunkStacks} / {flask.BlackoutAfterStacks}  (one wears off every 15 s)" : "-"),
                ("Scanner critter range", scanner != null && inventory.Has(PlayerItem.Scanner) ? $"{scanner.Range:0} m" : "No scanner"),
                ("Slide distance", movement != null ? $"{movement.SlideDistanceScale * 100f:0}%" : "-"),
                ("Upgrades installed", $"{inventory.Upgrades.Count}")
            };
            var right = new List<(string, string)>();
            if (laser != null && inventory.Has(PlayerItem.MiningLaser))
            {
                right.Add(("Combo damage", string.Join(" / ", laser.ComboDamage.Select((d, k) => (k == laser.ComboLength - 1 ? d * laser.FinisherDamageMultiplier : d).ToString("0")))));
                right.Add(("Finisher stun", $"{laser.FinisherStun:0.#} s"));
                right.Add(("Combo recharge", $"{laser.ComboCooldown:0.#} s"));
                right.Add(("Charged bolt", $"{laser.MinShotDamage:0}-{laser.MaxShotDamage:0} damage"));
                right.Add(("Bolt cooldown", $"{laser.TapShotCooldown:0.#}-{laser.ShotCooldown:0.#} s"));
                right.Add(("Full-charge burst", $"{laser.FullChargeBlastRadius:0} m"));
            }
            else right.Add(("Mining laser", "Not found yet"));
            Column(left, 0f);
            Column(right, 700f);
        }

        private void Column(List<(string label, string value)> rows, float x)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                Image row = UiTheme.Panel(rows[i].label, page, i % 2 == 0 ? UiTheme.PanelLight : new Color(1f, 1f, 1f, 0.03f), null, 0.5f);
                RectTransform rect = UiTheme.Place(row.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -i * 70f), new Vector2(680f, 62f));
                TextMeshProUGUI label = UiTheme.Label("Label", rect, 24f, UiTheme.Muted, TextAlignmentOptions.MidlineLeft);
                UiTheme.Place(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(300f, 50f));
                label.text = rows[i].label;
                TextMeshProUGUI value = UiTheme.Label("Value", rect, 26f, UiTheme.Text, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
                UiTheme.Place(value.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-20f, 0f), new Vector2(380f, 50f));
                value.text = rows[i].value;
            }
        }

        // ---------- Layout ----------

        private void Build()
        {
            var canvasRoot = (RectTransform)transform;
            root = UiTheme.Stretch(UiTheme.Rect("Inventory", canvasRoot)).gameObject;
            Image dim = UiTheme.Block("Dim", root.transform, UiTheme.Dim);
            UiTheme.Stretch(dim.rectTransform);
            dim.raycastTarget = true;

            Image background = UiTheme.Panel("Panel", root.transform, UiTheme.PanelColor, UiTheme.Edge, 1.4f);
            panel = UiTheme.Place(background.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(1480f, 880f));

            TextMeshProUGUI title = UiTheme.Label("Title", panel, 54f, UiTheme.Accent, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UiTheme.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(48f, -26f), new Vector2(400f, 70f));
            title.text = "POCKETS";
            title.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 1.5f);

            for (int i = 0; i < Tabs.Length; i++)
            {
                int index = i;
                Button button = UiTheme.Button(Tabs[i], panel, $"{i + 1}  {Tabs[i]}");
                UiTheme.Place((RectTransform)button.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(380f + i * 240f, -32f), new Vector2(226f, 56f));
                button.onClick.AddListener(() => ShowTab(index));
                tabButtons.Add(button);
            }
            page = UiTheme.Place(UiTheme.Rect("Page", panel), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(48f, -118f), new Vector2(1390f, 700f));

            TextMeshProUGUI hint = UiTheme.Label("Hint", panel, 24f, UiTheme.Accent, TextAlignmentOptions.BottomRight, FontStyles.Bold);
            UiTheme.Place(hint.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 20f), new Vector2(700f, 34f));
            hint.text = "[1]-[4]  Tabs     [Tab] / [Esc]  Close";
            root.SetActive(false);
        }

        private static void Heading(Transform parent, string text, Vector2 position)
        {
            TextMeshProUGUI heading = UiTheme.Label(text, parent, 26f, UiTheme.Muted, TextAlignmentOptions.TopLeft, FontStyles.Bold | FontStyles.UpperCase);
            UiTheme.Place(heading.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), position, new Vector2(560f, 34f));
            heading.text = text;
        }
    }
}
