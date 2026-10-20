using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SignalLost.UI
{
    /// <summary>
    /// Non-blocking gameplay UI in the theme of the dialogue screens: the top-right quest tracker and the
    /// "item acquired" cards that introduce new gear. Nothing here takes the cursor or pauses the game.
    /// Built in code on its own overlay canvas the first time it is used.
    /// </summary>
    public sealed class GameplayOverlay : MonoBehaviour
    {
        private static GameplayOverlay instance;

        private CanvasGroup trackerGroup;
        private RectTransform trackerPanel;
        private TextMeshProUGUI trackerHeader, trackerTitle, trackerBody;
        private string trackedKey;
        private float trackerChanged;

        private CanvasGroup cardGroup;
        private RectTransform cardPanel;
        private Image cardStripe;
        private TextMeshProUGUI cardKicker, cardTitle, cardBody;
        private float cardShown, cardUntil;

        public static GameplayOverlay Instance
        {
            get
            {
                if (instance == null && Application.isPlaying)
                    instance = UiTheme.CreateCanvas("Gameplay Overlay", 40).gameObject.AddComponent<GameplayOverlay>();
                return instance;
            }
        }

        /// <summary>The panel area other overlay widgets (the scrap counter) can stack under.</summary>
        public RectTransform Root => (RectTransform)transform;

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            Build();
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        /// <summary>Shows the quest tracker. Pass a null title to hide it. Changes make the panel bounce.</summary>
        public void SetQuest(string title, string objective, bool complete = false)
        {
            string key = title + "|" + objective + "|" + complete;
            if (key == trackedKey) return;
            trackedKey = key;
            trackerChanged = Time.unscaledTime;
            if (string.IsNullOrEmpty(title)) return;
            trackerHeader.text = complete ? "QUEST COMPLETE" : "ACTIVE QUEST";
            trackerHeader.color = complete ? UiTheme.Good : UiTheme.Accent;
            trackerTitle.text = title;
            trackerBody.text = objective ?? "";
            float height = 96f + trackerBody.GetPreferredValues(trackerBody.text, 400f, 0f).y;
            trackerPanel.sizeDelta = new Vector2(460f, height);
        }

        /// <summary>A card at the right of the screen introducing a new item and its controls.</summary>
        public void ShowItemCard(string title, string description, Color color, float seconds = 9f)
        {
            cardKicker.text = "NEW GEAR";
            cardTitle.text = title;
            cardTitle.color = color;
            cardStripe.color = color;
            cardBody.text = description;
            float height = 128f + cardBody.GetPreferredValues(description, 470f, 0f).y;
            cardPanel.sizeDelta = new Vector2(540f, height);
            cardShown = Time.unscaledTime;
            cardUntil = Time.unscaledTime + seconds;
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            bool trackerVisible = !string.IsNullOrEmpty(trackedKey) && !trackedKey.StartsWith("|");
            trackerGroup.alpha = Mathf.MoveTowards(trackerGroup.alpha, trackerVisible ? 1f : 0f, Time.unscaledDeltaTime * 5f);
            trackerPanel.localScale = Vector3.one * Mathf.LerpUnclamped(0.92f, 1f, UiTheme.Pop((now - trackerChanged) / 0.35f));

            float cardIn = UiTheme.Pop((now - cardShown) / 0.35f);
            float cardOut = Mathf.Clamp01((cardUntil - now) / 0.4f);
            cardGroup.alpha = Mathf.Min(Mathf.Clamp01((now - cardShown) / 0.15f), cardOut);
            cardPanel.anchoredPosition = new Vector2(Mathf.LerpUnclamped(-620f, 36f, cardIn), 20f);
        }

        private void Build()
        {
            var root = (RectTransform)transform;

            RectTransform tracker = UiTheme.Place(UiTheme.Rect("Quest Tracker", root),
                Vector2.one, Vector2.one, new Vector2(-36f, -36f), new Vector2(460f, 160f));
            trackerPanel = tracker;
            trackerGroup = tracker.gameObject.AddComponent<CanvasGroup>();
            trackerGroup.alpha = 0f;
            trackerGroup.blocksRaycasts = false;
            UiTheme.Stretch(UiTheme.Panel("Panel", tracker, UiTheme.PanelColor, UiTheme.Edge, 0.8f).rectTransform);
            trackerHeader = UiTheme.Label("Header", tracker, 22f, UiTheme.Accent, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UiTheme.Place(trackerHeader.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26f, -18f), new Vector2(400f, 30f));
            trackerTitle = UiTheme.Label("Title", tracker, 32f, UiTheme.Text, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UiTheme.Place(trackerTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26f, -46f), new Vector2(410f, 42f));
            trackerBody = UiTheme.Label("Objective", tracker, 26f, UiTheme.Muted, TextAlignmentOptions.TopLeft);
            trackerBody.rectTransform.anchorMin = Vector2.zero;
            trackerBody.rectTransform.anchorMax = Vector2.one;
            trackerBody.rectTransform.offsetMin = new Vector2(26f, 16f);
            trackerBody.rectTransform.offsetMax = new Vector2(-26f, -90f);

            // Left side: the held laser and scanner fill the lower right of the view.
            cardPanel = UiTheme.Place(UiTheme.Rect("Item Card", root),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-620f, 20f), new Vector2(540f, 200f));
            cardGroup = cardPanel.gameObject.AddComponent<CanvasGroup>();
            cardGroup.alpha = 0f;
            cardGroup.blocksRaycasts = false;
            UiTheme.Stretch(UiTheme.Panel("Panel", cardPanel, UiTheme.PanelColor, UiTheme.Edge, 1f).rectTransform);
            cardStripe = UiTheme.Panel("Stripe", cardPanel, UiTheme.Accent, null, 0.4f);
            cardStripe.rectTransform.anchorMin = new Vector2(0f, 0f);
            cardStripe.rectTransform.anchorMax = new Vector2(0f, 1f);
            cardStripe.rectTransform.pivot = new Vector2(0f, 0.5f);
            cardStripe.rectTransform.offsetMin = new Vector2(14f, 16f);
            cardStripe.rectTransform.offsetMax = new Vector2(26f, -16f);
            cardKicker = UiTheme.Label("Kicker", cardPanel, 22f, UiTheme.Muted, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UiTheme.Place(cardKicker.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(44f, -18f), new Vector2(460f, 30f));
            cardTitle = UiTheme.Label("Title", cardPanel, 40f, UiTheme.Accent, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UiTheme.Place(cardTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(44f, -44f), new Vector2(470f, 50f));
            cardBody = UiTheme.Label("Body", cardPanel, 26f, UiTheme.Text, TextAlignmentOptions.TopLeft);
            cardBody.rectTransform.anchorMin = Vector2.zero;
            cardBody.rectTransform.anchorMax = Vector2.one;
            cardBody.rectTransform.offsetMin = new Vector2(44f, 24f);
            cardBody.rectTransform.offsetMax = new Vector2(-26f, -100f);
        }
    }
}
