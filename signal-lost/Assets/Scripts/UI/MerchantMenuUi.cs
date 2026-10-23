using System.Collections.Generic;
using System.Text;
using SignalLost.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SignalLost.UI
{
    /// <summary>
    /// Bucket &amp; Barter, the merchant crab's shop menu (meeting notes 10-20, stocked 10-23): upgrades, supplies and
    /// scrap trades from <see cref="UpgradeCatalog"/>, each a fixed recipe of scrap (and sometimes a mini-boss key
    /// item). Buy pays and installs straight away. Shows the player's sorted scrap so prices can be compared. Tabs by
    /// click or 1/2/3; E, Escape, the Close button or walking away closes it. Opening it frees the cursor.
    /// </summary>
    public sealed class MerchantMenuUi : MonoBehaviour
    {
        private const float Range = 6f;

        private static readonly string[] Tabs = { "Upgrades", "Supplies", "Trade Scrap" };
        private static readonly UpgradeSlot[][] TabSlots =
        {
            new[] { UpgradeSlot.Laser, UpgradeSlot.Scanner, UpgradeSlot.Flask, UpgradeSlot.Suit },
            new[] { UpgradeSlot.Supply },
            new[] { UpgradeSlot.Trade }
        };

        /// <summary>The area whose stock is on the shelves (only the tutorial's so far).</summary>
        public UpgradeRegion Region { get; set; } = UpgradeRegion.Tutorial;

        private static MerchantMenuUi instance;

        private GameObject root;
        private RectTransform panel, listArea;
        private TextMeshProUGUI balanceText;
        private readonly List<Button> tabButtons = new List<Button>();
        private readonly List<GameObject> rows = new List<GameObject>();
        private Transform owner, player;
        private int tab, openedFrame = -1;
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
        public int ListingCount => rows.Count;

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
            foreach (GameObject row in rows) Destroy(row);
            rows.Clear();
            int slot = 0;
            foreach (Upgrade listing in UpgradeCatalog.For(Region, TabSlots[tab])) rows.Add(Row(listing, slot++));
            foreach (Upgrade listing in UpgradeCatalog.All)
                if (System.Array.IndexOf(TabSlots[tab], listing.Slot) >= 0 && (listing.IsSupply || listing.IsTrade) && listing.Region != Region)
                    rows.Add(Row(listing, slot++)); // supplies and trades are sold everywhere
            RefreshBalance();
        }

        private void Update()
        {
            if (!IsOpen) return;
            panel.localScale = Vector3.one * Mathf.LerpUnclamped(0.88f, 1f, UiTheme.Pop((Time.unscaledTime - shownAt) / 0.3f));
            panel.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.unscaledTime * 0.8f) * 0.3f);
            RefreshBalance();
            bool walkedAway = owner == null || player == null || Vector3.Distance(owner.position, player.position) > Range;
            if (walkedAway || Keyboard.current?.escapeKey.wasPressedThisFrame == true) { Close(); return; }
            if (openedFrame == Time.frameCount) return;
            if (Keyboard.current?.eKey.wasPressedThisFrame == true) { Close(); return; }
            if (Keyboard.current?.digit1Key.wasPressedThisFrame == true) ShowTab(0);
            if (Keyboard.current?.digit2Key.wasPressedThisFrame == true) ShowTab(1);
            if (Keyboard.current?.digit3Key.wasPressedThisFrame == true) ShowTab(2);
        }

        private void RefreshBalance()
        {
            PlayerInventory inventory = PlayerInventory.Current;
            var text = new StringBuilder("Your scrap:   ");
            foreach (ScrapType type in ScrapInfo.All)
                text.Append($"<color={UiTheme.Hex(ScrapInfo.Color(type))}>{(inventory != null ? inventory.Scrap(type) : 0)} {ScrapInfo.Name(type)}</color>     ");
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

            balanceText = UiTheme.Label("Balance", panel, 24f, UiTheme.Text, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UiTheme.Place(balanceText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(46f, -100f), new Vector2(1200f, 34f));

            for (int i = 0; i < Tabs.Length; i++)
            {
                int index = i;
                Button button = UiTheme.Button(Tabs[i], panel, $"{i + 1}  {Tabs[i]}");
                UiTheme.Place((RectTransform)button.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(46f + i * 250f, -150f), new Vector2(236f, 56f));
                button.onClick.AddListener(() => ShowTab(index));
                tabButtons.Add(button);
            }

            listArea = UiTheme.Place(UiTheme.Rect("Listings", panel), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(46f, -226f), new Vector2(1208f, 640f));

            Button close = UiTheme.Button("Close", panel, "Close");
            UiTheme.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -30f), new Vector2(150f, 52f));
            close.onClick.AddListener(Close);

            TextMeshProUGUI hint = UiTheme.Label("Hint", panel, 24f, UiTheme.Accent, TextAlignmentOptions.BottomRight, FontStyles.Bold);
            UiTheme.Place(hint.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 24f), new Vector2(900f, 34f));
            hint.text = "[1] [2] [3]  Tabs     [E] / [Esc]  Close";
            TextMeshProUGUI note = UiTheme.Label("Note", panel, 24f, UiTheme.Muted, TextAlignmentOptions.BottomLeft, FontStyles.Italic);
            UiTheme.Place(note.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(46f, 24f), new Vector2(600f, 34f));
            note.text = "All sales final. Installation free. Claws optional.";
            root.SetActive(false);
        }

        private GameObject Row(Upgrade listing, int index)
        {
            PlayerInventory inventory = PlayerInventory.Current;
            Sprite icon = listing.IsTrade ? ScrapCatalog.Icon(listing.Gives) : ItemCatalog.Icon(listing.Id);
            string body = listing.OneOff ? $"<color={UiTheme.Hex(ItemRows.SlotColor(listing.Slot))}>{ItemRows.SlotName(listing.Slot)}</color>  {listing.Description}" : listing.Description;
            if (listing.IsSupply && inventory != null) body += $"   <color={UiTheme.Hex(UiTheme.Text)}>(you have {inventory.SupplyCount(listing.Id)})</color>";
            RectTransform row = ItemRows.Row(listArea, listing.Name, icon, listing.Name, UiTheme.Text, body, new Vector2(0f, -index * 90f), new Vector2(1208f, 84f), 590f);

            TextMeshProUGUI cost = UiTheme.Label("Price", row, 22f, UiTheme.Text, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
            UiTheme.Place(cost.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-230f, 0f), new Vector2(330f, 80f));
            cost.text = ItemRows.Price(listing, inventory);

            string why = inventory != null ? inventory.WhyNot(listing) : "No pockets";
            Button buy = UiTheme.Button("Buy", row, why == null ? "BUY" : why == "Owned" ? "INSTALLED" : "BUY", 22f);
            UiTheme.Place((RectTransform)buy.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(196f, 56f));
            buy.interactable = why == null;
            buy.onClick.AddListener(() =>
            {
                if (inventory != null && inventory.Buy(listing)) ShowTab(tab);
            });
            return row.gameObject;
        }
    }
}
