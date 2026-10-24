using System.Collections.Generic;
using System.Linq;
using System.Text;
using SignalLost.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SignalLost.UI
{
    /// <summary>
    /// Bucket &amp; Barter, Eugene's shop (meeting notes 10-20, stocked 10-23). Listings come from
    /// <see cref="UpgradeCatalog"/>, filtered by type (Mining Laser, Scanner, Flask, Player, Supplies, Trades; click or
    /// 1-6). Upgrades already bought move to the "Purchased" filter at the bottom, out of the way. Prices show amounts
    /// with scrap icons. Buying takes two presses: the first selects the item and previews the scrap left afterwards
    /// in the balance line, the second (Confirm) buys it. E, Escape, Close or walking away closes the shop.
    /// </summary>
    public sealed class MerchantMenuUi : MonoBehaviour
    {
        private const float Range = 6f;
        private const float RowHeight = 108f, RowGap = 8f;

        private static readonly (string label, UpgradeSlot slot)[] Filters =
        {
            ("Laser", UpgradeSlot.Laser), ("Scanner", UpgradeSlot.Scanner), ("Flask", UpgradeSlot.Flask),
            ("Player", UpgradeSlot.Suit), ("Supplies", UpgradeSlot.Supply), ("Trades", UpgradeSlot.Trade)
        };

        /// <summary>The area whose stock is on the shelves (only the tutorial's so far).</summary>
        public UpgradeRegion Region { get; set; } = UpgradeRegion.Tutorial;

        private static MerchantMenuUi instance;

        private GameObject root;
        private RectTransform panel, listArea, listContent;
        private TextMeshProUGUI balanceText, emptyText;
        private Button purchasedButton;
        private readonly List<Button> tabButtons = new List<Button>();
        private readonly List<GameObject> rows = new List<GameObject>();
        private Transform owner, player;
        private int tab, openedFrame = -1;
        private bool showPurchased;
        private Upgrade selected;
        private float shownAt;

        /// <summary>The existing instance, or null. Use this in OnDisable/OnDestroy so nothing is created during unload.</summary>
        public static MerchantMenuUi Current => instance;

        public static MerchantMenuUi Instance
        {
            get
            {
                if (instance == null && Application.isPlaying)
                    instance = UiTheme.CreateCanvas("Merchant Menu UI", 60).gameObject.AddComponent<MerchantMenuUi>();
                return instance;
            }
        }

        public bool IsOpen => root != null && root.activeSelf;
        public Transform Owner => owner;
        public int CurrentTab => tab;
        public bool ShowingPurchased => showPurchased;
        public int ListingCount => rows.Count;
        /// <summary>The item awaiting Confirm, or null.</summary>
        public Upgrade Selected => selected;

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

        /// <summary>Opens the shop for the NPC at <paramref name="merchant"/>; it closes if the player walks away.</summary>
        public void Open(Transform merchant, Transform customer)
        {
            UiTheme.EnsureEventSystem();
            owner = merchant;
            player = customer;
            root.SetActive(true);
            shownAt = Time.unscaledTime;
            openedFrame = Time.frameCount;
            ShowTab(0);
            UiMenus.Open(this, true);
        }

        public void Close()
        {
            if (!IsOpen) return;
            root.SetActive(false);
            owner = null;
            selected = null;
            UiMenus.Close(this);
        }

        /// <summary>Shows one type of listing (0-5: Laser, Scanner, Flask, Player, Supplies, Trades).</summary>
        public void ShowTab(int index)
        {
            tab = Mathf.Clamp(index, 0, Filters.Length - 1);
            showPurchased = false;
            selected = null;
            Refresh();
        }

        /// <summary>Shows the upgrades already bought.</summary>
        public void ShowPurchased()
        {
            showPurchased = true;
            selected = null;
            Refresh();
        }

        /// <summary>The Buy button: the first press selects and previews, the second press on the same item buys it.</summary>
        public void Press(Upgrade listing)
        {
            PlayerInventory inventory = PlayerInventory.Current;
            if (inventory == null || inventory.WhyNot(listing) != null) return;
            if (selected != listing)
            {
                selected = listing;
                Refresh();
                return;
            }
            selected = null;
            inventory.Buy(listing);
            Refresh();
        }

        private void Refresh()
        {
            for (int i = 0; i < tabButtons.Count; i++)
            {
                bool on = !showPurchased && i == tab;
                tabButtons[i].GetComponent<Image>().color = on ? UiTheme.Accent : UiTheme.PanelLight;
                tabButtons[i].GetComponentInChildren<TextMeshProUGUI>().color = on ? new Color(0.08f, 0.08f, 0.12f) : UiTheme.Text;
            }
            PlayerInventory inventory = PlayerInventory.Current;
            int owned = inventory != null ? inventory.Upgrades.Count : 0;
            TextMeshProUGUI purchasedLabel = purchasedButton.GetComponentInChildren<TextMeshProUGUI>();
            purchasedLabel.text = $"Purchased ({owned})";
            purchasedLabel.color = showPurchased ? new Color(0.08f, 0.08f, 0.12f) : UiTheme.Text;
            purchasedButton.GetComponent<Image>().color = showPurchased ? UiTheme.Accent : UiTheme.PanelLight;

            foreach (GameObject row in rows) Destroy(row);
            rows.Clear();
            int slot = 0;
            foreach (Upgrade listing in Listings(inventory)) rows.Add(Row(listing, slot++, inventory));
            listContent.sizeDelta = new Vector2(0f, rows.Count * (RowHeight + RowGap));
            listContent.anchoredPosition = Vector2.zero; // back to the top
            emptyText.text = rows.Count > 0 ? "" : showPurchased ? "Nothing bought yet." : "Sold out! Everything here is already yours: see Purchased.";
            RefreshBalance();
        }

        // The current filter's listings: by type and not yet bought, or (Purchased) everything bought, sorted by type.
        private IEnumerable<Upgrade> Listings(PlayerInventory inventory)
        {
            if (showPurchased)
                return UpgradeCatalog.All.Where(u => u.OneOff && inventory != null && inventory.Owns(u.Id)).OrderBy(u => u.Slot);
            UpgradeSlot type = Filters[tab].slot;
            return UpgradeCatalog.All.Where(u => u.Slot == type && (u.Region == Region || !u.OneOff) && !(u.OneOff && inventory != null && inventory.Owns(u.Id)));
        }

        private void Update()
        {
            if (!IsOpen) return;
            panel.localScale = Vector3.one * Mathf.LerpUnclamped(0.88f, 1f, UiTheme.Pop((Time.unscaledTime - shownAt) / 0.3f));
            panel.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.unscaledTime * 0.8f) * 0.3f);
            RefreshBalance();
            bool walkedAway = owner == null || player == null || Vector3.Distance(owner.position, player.position) > Range;
            if (walkedAway || Keyboard.current?.escapeKey.wasPressedThisFrame == true) { Close(); return; }
            if (openedFrame == Time.frameCount || Keyboard.current == null) return;
            if (Keyboard.current.eKey.wasPressedThisFrame) { Close(); return; }
            for (int i = 0; i < Filters.Length; i++)
                if (Keyboard.current[Key.Digit1 + i].wasPressedThisFrame) ShowTab(i);
        }

        // Your scrap; while an item is selected, what you would have left after buying it.
        private void RefreshBalance()
        {
            PlayerInventory inventory = PlayerInventory.Current;
            var text = new StringBuilder("Your scrap:   ");
            foreach (ScrapType type in ScrapInfo.All)
            {
                int have = inventory != null ? inventory.Scrap(type) : 0;
                int cost = selected != null ? selected.Cost.Where(c => c.type == type).Sum(c => c.amount) : 0;
                int gain = selected != null && selected.IsTrade && selected.Gives == type ? selected.GivesAmount : 0;
                if (cost > 0 || gain > 0)
                    text.Append($"{have} <color={UiTheme.Hex(UiTheme.Muted)}>→</color> <color={UiTheme.Hex(UiTheme.Accent)}><b>{have - cost + gain}</b></color> {ScrapInfo.Inline(type)}      ");
                else
                    text.Append($"{have} {ScrapInfo.Inline(type)}      ");
            }
            if (selected != null) text.Append($"<color={UiTheme.Hex(UiTheme.Accent)}>CONFIRM to buy</color>");
            balanceText.text = text.ToString();
        }

        // ---------- Layout ----------

        private void Build()
        {
            var canvasRoot = (RectTransform)transform;
            root = UiTheme.Stretch(UiTheme.Rect("Merchant Menu", canvasRoot)).gameObject;
            Image dim = UiTheme.Block("Dim", root.transform, UiTheme.Dim);
            UiTheme.Stretch(dim.rectTransform);
            dim.raycastTarget = true;

            Image background = UiTheme.Panel("Panel", root.transform, UiTheme.PanelColor, UiTheme.SpeakerColor("crab"), 1.4f);
            panel = UiTheme.Place(background.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(1300f, 940f));

            Image sign = UiTheme.Panel("Sign", panel, UiTheme.SpeakerColor("crab"), null, 0.8f);
            RectTransform signRect = UiTheme.Place(sign.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(40f, 0f), new Vector2(640f, 86f));
            signRect.localRotation = Quaternion.Euler(0f, 0f, 2.5f);
            TextMeshProUGUI title = UiTheme.Label("Title", signRect, 50f, new Color(0.1f, 0.07f, 0.06f), TextAlignmentOptions.Center, FontStyles.Bold);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            UiTheme.Stretch(title.rectTransform);
            title.text = "BUCKET & BARTER";

            TextMeshProUGUI tagline = UiTheme.Label("Tagline", panel, 24f, UiTheme.Muted, TextAlignmentOptions.TopLeft, FontStyles.Italic);
            UiTheme.Place(tagline.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(46f, -58f), new Vector2(900f, 34f));
            tagline.text = "Est. whenever you crashed. No refunds. No questions. Some claws.";

            balanceText = UiTheme.Label("Balance", panel, 28f, UiTheme.Text, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            UiTheme.Place(balanceText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(46f, -96f), new Vector2(1220f, 44f));

            for (int i = 0; i < Filters.Length; i++)
            {
                int index = i;
                Button button = UiTheme.Button(Filters[i].label, panel, $"{i + 1}  {Filters[i].label}", 24f);
                UiTheme.Place((RectTransform)button.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(46f + i * 203f, -150f), new Vector2(193f, 56f));
                button.onClick.AddListener(() => ShowTab(index));
                tabButtons.Add(button);
            }

            // Up to five rows show at once; the Purchased list can be longer, so it scrolls.
            listArea = UiTheme.Place(UiTheme.Rect("Listings", panel), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(46f, -226f), new Vector2(1208f, 576f));
            listContent = UiTheme.ScrollList(listArea);
            emptyText = UiTheme.Label("Empty", listArea, 26f, UiTheme.Muted, TextAlignmentOptions.Center, FontStyles.Italic);
            UiTheme.Place(emptyText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(1000f, 40f));

            // Below the list: what you already own, kept out of the way.
            purchasedButton = UiTheme.Button("Purchased", panel, "Purchased (0)", 24f);
            UiTheme.Place((RectTransform)purchasedButton.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(46f, 76f), new Vector2(260f, 52f));
            purchasedButton.onClick.AddListener(ShowPurchased);

            Button close = UiTheme.Button("Close", panel, "Close");
            UiTheme.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -30f), new Vector2(150f, 52f));
            close.onClick.AddListener(Close);

            TextMeshProUGUI hint = UiTheme.Label("Hint", panel, 24f, UiTheme.Accent, TextAlignmentOptions.BottomRight, FontStyles.Bold);
            UiTheme.Place(hint.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 24f), new Vector2(900f, 34f));
            hint.text = "[1]-[6]  Filters     BUY, then CONFIRM     [E] / [Esc]  Close";
            TextMeshProUGUI note = UiTheme.Label("Note", panel, 22f, UiTheme.Muted, TextAlignmentOptions.BottomLeft, FontStyles.Italic);
            UiTheme.Place(note.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(46f, 24f), new Vector2(600f, 34f));
            note.text = "All sales final. Installation free. Claws optional.";
            root.SetActive(false);
        }

        private GameObject Row(Upgrade listing, int index, PlayerInventory inventory)
        {
            bool owned = listing.OneOff && inventory != null && inventory.Owns(listing.Id);
            bool isSelected = listing == selected;
            Sprite icon = listing.IsTrade ? ScrapCatalog.Icon(listing.Gives) : ItemCatalog.Icon(listing.Id);
            string body = listing.IsTrade ? TradeText(listing) : listing.Description;
            if (listing.IsSupply && inventory != null) body += $"   <color={UiTheme.Hex(UiTheme.Text)}>(you have {inventory.SupplyCount(listing.Id)})</color>";
            if (showPurchased) body = $"<color={UiTheme.Hex(ItemRows.SlotColor(listing.Slot))}>{ItemRows.SlotName(listing.Slot)}</color>  " + body;
            // The description stops short of the price and button (just the button when it's already yours).
            RectTransform row = ItemRows.Row(listContent, listing.Name, icon, listing.Name, isSelected ? UiTheme.Accent : UiTheme.Text, body,
                new Vector2(0f, -index * (RowHeight + RowGap)), new Vector2(1208f, RowHeight), owned ? 240f : 580f);
            if (isSelected) row.GetComponent<Image>().color = Color.Lerp(UiTheme.PanelLight, UiTheme.Accent, 0.25f);

            if (!owned)
            {
                TextMeshProUGUI cost = UiTheme.Label("Price", row, 28f, UiTheme.Text, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
                UiTheme.Place(cost.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-228f, 0f), new Vector2(330f, 80f));
                cost.textWrappingMode = TextWrappingModes.NoWrap;
                cost.enableAutoSizing = true;
                cost.fontSizeMin = 18f;
                cost.fontSizeMax = 28f;
                cost.text = ItemRows.Price(listing, inventory);
            }

            string why = inventory != null ? inventory.WhyNot(listing) : "No pockets";
            string label = owned ? "INSTALLED" : isSelected ? "CONFIRM" : "BUY";
            Button buy = UiTheme.Button("Buy", row, label, 22f);
            UiTheme.Place((RectTransform)buy.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(196f, 56f));
            buy.interactable = why == null;
            if (isSelected) buy.GetComponent<Image>().color = UiTheme.Good;
            buy.onClick.AddListener(() => Press(listing));
            return row.gameObject;
        }

        private static string TradeText(Upgrade trade) =>
            string.Join("  ", trade.Cost.Select(c => ScrapInfo.Amount(c.type, c.amount))) + $"  <color={UiTheme.Hex(UiTheme.Muted)}>→</color>  " +
            ScrapInfo.Amount(trade.Gives, trade.GivesAmount);
    }
}
