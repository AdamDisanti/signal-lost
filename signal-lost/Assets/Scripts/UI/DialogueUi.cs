using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SignalLost.UI
{
    /// <summary>
    /// The three ways NPCs talk (meeting notes 2026-10-20):
    /// <list type="bullet">
    /// <item>Subtitles: one plain line at the bottom middle for quick mid-action chat and idle talk.</item>
    /// <item>Speech bubbles: <see cref="SpeechBubble"/>s that pop up over the speaker's head.</item>
    /// <item>Quest popups: a focused, rounded conversation box for important quest information. Click or E
    /// finishes the line or moves on; Escape or walking away cancels without running the completion callback.</item>
    /// </list>
    /// Built in code on its own overlay canvas the first time anything asks for it; restyle it in <see cref="UiTheme"/>.
    /// </summary>
    public sealed class DialogueUi : MonoBehaviour
    {
        private const float CharactersPerSecond = 55f;
        private const float PopupRange = 6f;

        private static DialogueUi instance;

        private RectTransform bubbleLayer;
        private CanvasGroup bubbleGroup;
        private CanvasGroup subtitleGroup;
        private Image subtitlePanel;
        private TextMeshProUGUI subtitleText;
        private float subtitleUntil, subtitleShown;
        private bool subtitleWarning;

        private GameObject popupRoot;
        private RectTransform popupPanel, nameTag, hint;
        private Image nameTagImage;
        private TextMeshProUGUI nameText, titleText, bodyText, hintText, counterText;
        private string[] lines;
        private int index, openedFrame = -1;
        private float lineStarted, popupShown;
        private Action completed;
        private string finishLabel;
        private Transform player;

        public static DialogueUi Instance
        {
            get
            {
                if (instance == null && Application.isPlaying)
                    instance = UiTheme.CreateCanvas("Dialogue UI", 50).gameObject.AddComponent<DialogueUi>();
                return instance;
            }
        }

        /// <summary>The NPC (or other transform) the open popup belongs to.</summary>
        public Transform Owner { get; private set; }
        public bool PopupOpen => popupRoot != null && popupRoot.activeSelf;
        public string CurrentLine => PopupOpen ? lines[index] : "";
        public RectTransform BubbleLayer => bubbleLayer;

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

        /// <summary>Lets the popup close itself when the player walks more than a few metres from its owner.</summary>
        public void BindPlayer(Transform target) => player = target;

        // ---------- Subtitles ----------

        /// <summary>Shows one line at the bottom middle of the screen. A newer line replaces the current one.</summary>
        public void Subtitle(string speaker, string line, float seconds = 4f, bool warning = false)
        {
            if (string.IsNullOrEmpty(line)) return;
            subtitleWarning = warning;
            Color name = warning ? Color.white : UiTheme.SpeakerColor(speaker);
            string prefix = string.IsNullOrEmpty(speaker) ? "" : $"<b><color={UiTheme.Hex(name)}>{speaker}:</color></b>  ";
            subtitleText.text = prefix + line;
            subtitleText.color = warning ? new Color(1f, 0.86f, 0.84f) : UiTheme.Text;
            subtitleText.fontSize = warning ? 38f : 32f;
            subtitlePanel.color = warning ? UiTheme.WarningPanel : UiTheme.PanelColor;
            Vector2 size = subtitleText.GetPreferredValues(subtitleText.text, 1100f, 0f);
            ((RectTransform)subtitlePanel.transform).sizeDelta = new Vector2(Mathf.Min(size.x, 1100f) + 56f, size.y + 30f);
            subtitleShown = Time.unscaledTime;
            subtitleUntil = Time.unscaledTime + Mathf.Max(1.5f, seconds);
        }

        public void ClearSubtitle() => subtitleUntil = 0f;

        // ---------- Speech bubbles ----------

        public SpeechBubble CreateBubble(Transform anchor, string speaker) => SpeechBubble.Create(this, anchor, speaker);

        // ---------- Quest popups ----------

        /// <summary>
        /// Opens the focused conversation popup. <paramref name="onCompleted"/> runs only if the player reads to the end.
        /// </summary>
        public void BeginConversation(Transform owner, string speaker, string title, string[] dialogue,
            Action onCompleted = null, string finishWith = "Got it!")
        {
            CloseAll();
            if (dialogue == null || dialogue.Length == 0) return;
            Owner = owner;
            lines = dialogue;
            index = 0;
            completed = onCompleted;
            finishLabel = finishWith;
            openedFrame = Time.frameCount;
            popupShown = Time.unscaledTime;

            Color color = UiTheme.SpeakerColor(speaker);
            nameText.text = speaker;
            nameTagImage.color = color;
            titleText.text = title ?? "";
            Vector2 tag = nameText.GetPreferredValues(speaker, 600f, 0f);
            nameTag.sizeDelta = new Vector2(tag.x + 48f, 58f);

            popupRoot.SetActive(true);
            UiMenus.Open(this);
            ShowLine();
        }

        /// <summary>Finishes the current line if it is still typing, otherwise moves to the next one.</summary>
        public bool Advance()
        {
            if (!PopupOpen) return false;
            if (!InRange()) { CloseAll(); return false; }
            if (Typing) { lineStarted = -999f; bodyText.maxVisibleCharacters = bodyText.textInfo.characterCount; return true; }
            index++;
            if (index >= lines.Length)
            {
                Action action = completed;
                CloseAll();
                action?.Invoke();
            }
            else ShowLine();
            return true;
        }

        /// <summary>Closes the popup without running its completion callback.</summary>
        public void CloseAll()
        {
            if (popupRoot != null) popupRoot.SetActive(false);
            lines = null;
            completed = null;
            Owner = null;
            UiMenus.Close(this);
        }

        public void CloseFor(Transform owner)
        {
            if (Owner == owner) CloseAll();
        }

        private bool Typing => PopupOpen && bodyText.maxVisibleCharacters < bodyText.textInfo.characterCount;

        private void ShowLine()
        {
            bodyText.text = lines[index];
            bodyText.maxVisibleCharacters = 0;
            bodyText.ForceMeshUpdate();
            lineStarted = Time.unscaledTime;
            counterText.text = lines.Length > 1 ? $"{index + 1} / {lines.Length}" : "";
            bool last = index == lines.Length - 1;
            hintText.text = last ? $"<b>[E]</b> / Click  -  {finishLabel}" : "<b>[E]</b> / Click  -  Next";
        }

        private bool InRange() => Owner == null || player == null || Vector3.Distance(player.position, Owner.position) <= PopupRange;

        private void Update()
        {
            UpdateSubtitle();
            if (!PopupOpen) return;

            float age = Time.unscaledTime - popupShown;
            popupPanel.localScale = Vector3.one * Mathf.LerpUnclamped(0.85f, 1f, UiTheme.Pop(age / 0.28f));
            nameTag.localRotation = Quaternion.Euler(0f, 0f, 4f + Mathf.Sin(Time.unscaledTime * 2.2f) * 1.2f);
            hint.anchoredPosition = new Vector2(-36f, 26f + Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f)) * 6f);
            bodyText.maxVisibleCharacters = Mathf.FloorToInt((Time.unscaledTime - lineStarted) * CharactersPerSecond);

            if (!InRange() || Keyboard.current?.escapeKey.wasPressedThisFrame == true) { CloseAll(); return; }
            if (openedFrame == Time.frameCount) return;
            bool next = Keyboard.current?.eKey.wasPressedThisFrame == true ||
                        Mouse.current?.leftButton.wasPressedThisFrame == true ||
                        Gamepad.current?.buttonNorth.wasPressedThisFrame == true ||
                        Gamepad.current?.buttonSouth.wasPressedThisFrame == true;
            if (next) Advance();
        }

        private void UpdateSubtitle()
        {
            float now = Time.unscaledTime;
            float fadeIn = Mathf.Clamp01((now - subtitleShown) / 0.15f);
            float fadeOut = Mathf.Clamp01((subtitleUntil - now) / 0.35f);
            // Popups and menus carry their own text; subtitles and bubbles underneath would only compete.
            subtitleGroup.alpha = UiMenus.AnyOpen ? 0f : Mathf.Min(fadeIn, fadeOut);
            bubbleGroup.alpha = Mathf.MoveTowards(bubbleGroup.alpha, UiMenus.AnyOpen ? 0f : 1f, Time.unscaledDeltaTime * 8f);
            float shake = subtitleWarning && now - subtitleShown < 0.4f ? Mathf.Sin(now * 70f) * 6f : 0f;
            subtitlePanel.rectTransform.anchoredPosition = new Vector2(shake, 0f);
        }

        // ---------- Layout ----------

        private void Build()
        {
            var root = (RectTransform)transform;

            bubbleLayer = UiTheme.Stretch(UiTheme.Rect("Speech Bubbles", root));
            bubbleGroup = bubbleLayer.gameObject.AddComponent<CanvasGroup>();
            bubbleGroup.blocksRaycasts = false;

            RectTransform subtitle = UiTheme.Place(UiTheme.Rect("Subtitle", root),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(1160f, 120f));
            subtitleGroup = subtitle.gameObject.AddComponent<CanvasGroup>();
            subtitleGroup.alpha = 0f;
            subtitleGroup.blocksRaycasts = false;
            subtitlePanel = UiTheme.Panel("Panel", subtitle, UiTheme.PanelColor, null, 0.7f);
            UiTheme.Place(subtitlePanel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(400f, 64f));
            subtitleText = UiTheme.Label("Line", subtitlePanel.transform, 32f, UiTheme.Text, TextAlignmentOptions.Center);
            UiTheme.Stretch(subtitleText.rectTransform, 14f);
            subtitleText.rectTransform.offsetMin = new Vector2(28f, 14f);
            subtitleText.rectTransform.offsetMax = new Vector2(-28f, -14f);

            popupRoot = UiTheme.Stretch(UiTheme.Rect("Quest Popup", root)).gameObject;
            Image dim = UiTheme.Block("Dim", popupRoot.transform, UiTheme.Dim);
            UiTheme.Stretch(dim.rectTransform);
            dim.raycastTarget = true; // clicks advance the popup instead of reaching the game

            Image panel = UiTheme.Panel("Panel", popupRoot.transform, UiTheme.PanelColor, UiTheme.Edge, 1.3f);
            popupPanel = UiTheme.Place(panel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 110f), new Vector2(1240f, 330f));
            popupPanel.localRotation = Quaternion.Euler(0f, 0f, -0.6f);

            nameTagImage = UiTheme.Panel("Name Tag", popupPanel, UiTheme.Accent, null, 0.8f);
            nameTag = UiTheme.Place(nameTagImage.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                new Vector2(38f, 4f), new Vector2(260f, 58f));
            nameText = UiTheme.Label("Name", nameTag, 34f, new Color(0.08f, 0.08f, 0.12f), TextAlignmentOptions.Center, FontStyles.Bold);
            UiTheme.Stretch(nameText.rectTransform);

            titleText = UiTheme.Label("Title", popupPanel, 24f, UiTheme.Muted, TextAlignmentOptions.TopRight, FontStyles.Bold | FontStyles.UpperCase);
            UiTheme.Place(titleText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-36f, -22f), new Vector2(700f, 36f));

            bodyText = UiTheme.Label("Body", popupPanel, 38f, UiTheme.Text, TextAlignmentOptions.TopLeft);
            UiTheme.Stretch(bodyText.rectTransform);
            bodyText.rectTransform.offsetMin = new Vector2(48f, 76f);
            bodyText.rectTransform.offsetMax = new Vector2(-48f, -66f);
            bodyText.lineSpacing = 6f;

            counterText = UiTheme.Label("Counter", popupPanel, 24f, UiTheme.Muted, TextAlignmentOptions.BottomLeft);
            UiTheme.Place(counterText.rectTransform, Vector2.zero, Vector2.zero, new Vector2(48f, 26f), new Vector2(200f, 34f));

            hintText = UiTheme.Label("Hint", popupPanel, 26f, UiTheme.Accent, TextAlignmentOptions.BottomRight);
            hint = UiTheme.Place(hintText.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-36f, 26f), new Vector2(600f, 36f));

            popupRoot.SetActive(false);
        }
    }
}
