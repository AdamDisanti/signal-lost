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
    /// Bucket &amp; Barter, the merchant crab's shop menu (meeting notes 2026-10-20). A placeholder: three tabs of
    /// listings priced in scrap, every purchase disabled until upgrades and trading exist. Shows the player's
    /// sorted scrap so prices can be compared. Tabs by click or 1/2/3; E, Escape, the Close button or walking
    /// away closes it. Opening it frees the cursor; walking keeps working.
    /// </summary>
    public sealed class MerchantMenuUi : MonoBehaviour
    {
        private const float Range = 6f;

        private struct Listing
        {
            public string Name, Description;
            public (ScrapType type, int amount)[] Price;
        }

        // Placeholder stock; names and prices are for the look of the menu only.
        private static readonly string[] Tabs = { "Upgrades", "Supplies", "Trade Scrap" };
        private static readonly Listing[][] Stock =
        {
            new[]
            {
                new Listing { Name = "Overclocked Coil", Description = "Charged bolts hit 25% harder.", Price = new[] { (ScrapType.ScrapMetal, 10), (ScrapType.Wiring, 4) } },
                new Listing { Name = "Quick-Cool Fins", Description = "Bolt cooldown 10 s, down to 7 s.", Price = new[] { (ScrapType.ScrapMetal, 8), (ScrapType.CircuitBoards, 3) } },
                new Listing { Name = "Heavy Slam", Description = "The combo finisher hits harder and stuns longer.", Price = new[] { (ScrapType.ScrapMetal, 12), (ScrapType.FuelCells, 2) } },
                new Listing { Name = "Long-Nose Antenna", Description = "Scanner finds critters 10 m further away.", Price = new[] { (ScrapType.Wiring, 6), (ScrapType.CircuitBoards, 2) } }
            },
            new[]
            {
                new Listing { Name = "Hangover Pills", Description = "Instantly sober up by one drink. Probably safe.", Price = new[] { (ScrapType.Wiring, 3) } },
                new Listing { Name = "Bucket Helmet", Description = "Purely cosmetic. Extremely shiny.", Price = new[] { (ScrapType.ScrapMetal, 20) } },
                new Listing { Name = "Mystery Snack", Description = "The crab won't say what's in it.", Price = new[] { (ScrapType.FuelCells, 1) } }
            },
            new[]
            {
                new Listing { Name = "Metal for Wiring", Description = "5 Scrap Metal into 1 Wiring.", Price = new[] { (ScrapType.ScrapMetal, 5) } },
                new Listing { Name = "Wiring for Boards", Description = "4 Wiring into 1 Circuit Board.", Price = new[] { (ScrapType.Wiring, 4) } },
                new Listing { Name = "Boards for a Cell", Description = "3 Circuit Boards into 1 Fuel Cell.", Price = new[] { (ScrapType.CircuitBoards, 3) } }
            }
        };

        private static MerchantMenuUi instance;

        private GameObject root;
        private RectTransform panel, listArea;
        private TextMeshProUGUI balanceText;
        private readonly List<Button> tabButtons = new List<Button>();
        private readonly List<GameObject> rows = new List<GameObject>();
        private Transform owner, player;
        private int tab, openedFrame = -1;
        private float shownAt;

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
            Listing[] listings = Stock[tab];
            for (int i = 0; i < listings.Length; i++) rows.Add(Row(listings[i], i));
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
            panel = UiTheme.Place(background.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1300f, 800f));

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

            listArea = UiTheme.Place(UiTheme.Rect("Listings", panel), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(46f, -226f), new Vector2(1208f, 470f));

            Button close = UiTheme.Button("Close", panel, "Close");
            UiTheme.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -30f), new Vector2(150f, 52f));
            close.onClick.AddListener(Close);

            TextMeshProUGUI hint = UiTheme.Label("Hint", panel, 24f, UiTheme.Accent, TextAlignmentOptions.BottomRight, FontStyles.Bold);
            UiTheme.Place(hint.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 24f), new Vector2(900f, 34f));
            hint.text = "[1] [2] [3]  Tabs     [E] / [Esc]  Close";
            TextMeshProUGUI note = UiTheme.Label("Note", panel, 24f, UiTheme.Muted, TextAlignmentOptions.BottomLeft, FontStyles.Italic);
            UiTheme.Place(note.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(46f, 24f), new Vector2(600f, 34f));
            note.text = "Shelves still being stocked. Check back soon!";
            root.SetActive(false);
        }

        private GameObject Row(Listing listing, int index)
        {
            Image row = UiTheme.Panel(listing.Name, listArea, UiTheme.PanelLight, null, 0.7f);
            UiTheme.Place(row.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -index * 114f), new Vector2(1208f, 102f));
            TextMeshProUGUI name = UiTheme.Label("Name", row.transform, 30f, UiTheme.Text, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UiTheme.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -12f), new Vector2(520f, 40f));
            name.text = listing.Name;
            TextMeshProUGUI description = UiTheme.Label("Description", row.transform, 22f, UiTheme.Muted, TextAlignmentOptions.TopLeft);
            UiTheme.Place(description.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -56f), new Vector2(560f, 34f));
            description.text = listing.Description;

            PlayerInventory inventory = PlayerInventory.Current;
            var price = new StringBuilder();
            foreach ((ScrapType type, int amount) in listing.Price)
            {
                bool affordable = inventory != null && inventory.Scrap(type) >= amount;
                Color color = affordable ? ScrapInfo.Color(type) : UiTheme.Muted;
                price.Append($"<color={UiTheme.Hex(color)}>{amount} {ScrapInfo.Name(type)}</color>   ");
            }
            TextMeshProUGUI cost = UiTheme.Label("Price", row.transform, 24f, UiTheme.Text, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            UiTheme.Place(cost.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(600f, 0f), new Vector2(380f, 60f));
            cost.text = price.ToString();

            Button buy = UiTheme.Button("Buy", row.transform, "COMING SOON", 22f);
            UiTheme.Place((RectTransform)buy.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-20f, 0f), new Vector2(200f, 56f));
            buy.interactable = false;
            return row.gameObject;
        }
    }
}
