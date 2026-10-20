using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SignalLost.UI
{
    /// <summary>
    /// A chat bubble that floats over a character's head, drawn on the screen-space dialogue canvas so it stays
    /// crisp and readable at any distance. It pops in with a little bounce for every new line, bobs gently,
    /// shrinks with distance and hides behind the camera or beyond <see cref="VisibleDistance"/>.
    /// Warnings use a red bubble that shakes. Create one through <see cref="DialogueUi.CreateBubble"/>.
    /// </summary>
    public sealed class SpeechBubble : MonoBehaviour
    {
        private static readonly Color Paper = new Color(1f, 0.97f, 0.9f, 0.96f);
        private static readonly Color Ink = new Color(0.1f, 0.1f, 0.16f);

        private Transform anchor;
        private RectTransform rect, layer, body;
        private CanvasGroup group;
        private Image panel, tail, edge;
        private TextMeshProUGUI text;
        private Camera viewer;
        private float shownAt, seed;
        private bool warning, visible;
        private string current;

        public float VisibleDistance { get; set; } = 16f;
        public bool HasText => !string.IsNullOrEmpty(current);

        internal static SpeechBubble Create(DialogueUi ui, Transform anchor, string speaker)
        {
            RectTransform root = UiTheme.Rect("Bubble " + speaker, ui.BubbleLayer);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0f);
            var bubble = root.gameObject.AddComponent<SpeechBubble>();
            bubble.Build(ui.BubbleLayer, anchor, UiTheme.SpeakerColor(speaker));
            return bubble;
        }

        /// <summary>Shows a line (null or empty hides the bubble). Each new line pops in again.</summary>
        public void Show(string line, bool isWarning = false, string hint = null)
        {
            string content = string.IsNullOrEmpty(line) ? null : line + (string.IsNullOrEmpty(hint) ? "" : $"\n<size=70%><color=#7A6F60>{hint}</color></size>");
            if (content == current && isWarning == warning) return;
            bool fresh = content != null && (current == null || content != current);
            current = content;
            warning = isWarning;
            if (content == null) return;

            text.text = content;
            text.color = warning ? Color.white : Ink;
            panel.color = warning ? UiTheme.Warning : Paper;
            tail.color = panel.color;
            Vector2 size = text.GetPreferredValues(content, 460f, 0f);
            body.sizeDelta = new Vector2(Mathf.Clamp(size.x, 60f, 460f) + 40f, size.y + 26f);
            if (fresh) shownAt = Time.unscaledTime;
        }

        public void Hide() => Show(null);

        private void Build(RectTransform bubbleLayer, Transform target, Color speakerColor)
        {
            layer = bubbleLayer;
            anchor = target;
            rect = (RectTransform)transform;
            rect.sizeDelta = new Vector2(10f, 10f);
            seed = Random.value * 10f;
            group = gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.alpha = 0f;

            // The tail is a small rotated square tucked under the bubble, pointing at the speaker.
            tail = UiTheme.Block("Tail", rect, Paper);
            UiTheme.Place(tail.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 14f), new Vector2(22f, 22f));
            tail.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            panel = UiTheme.Panel("Body", rect, Paper, null, 0.9f);
            body = UiTheme.Place(panel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(200f, 60f));
            edge = UiTheme.Stretch(UiTheme.Rect("Edge", body)).gameObject.AddComponent<Image>();
            edge.sprite = UiTheme.Outline;
            edge.type = Image.Type.Sliced;
            edge.pixelsPerUnitMultiplier = panel.pixelsPerUnitMultiplier;
            edge.color = speakerColor;
            edge.raycastTarget = false;

            text = UiTheme.Label("Text", body, 30f, Ink, TextAlignmentOptions.Center);
            UiTheme.Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(20f, 13f);
            text.rectTransform.offsetMax = new Vector2(-20f, -13f);
        }

        private void LateUpdate()
        {
            if (anchor == null) { Destroy(gameObject); return; }
            if (viewer == null || !viewer.isActiveAndEnabled) viewer = Camera.main;

            bool show = HasText && viewer != null && anchor.gameObject.activeInHierarchy;
            float distance = 0f;
            Vector3 screen = Vector3.zero;
            if (show)
            {
                screen = viewer.WorldToScreenPoint(anchor.position);
                distance = Vector3.Distance(viewer.transform.position, anchor.position);
                show = screen.z > 0.1f && distance <= VisibleDistance;
            }
            if (show != visible)
            {
                visible = show;
                if (show) shownAt = Time.unscaledTime;
            }
            group.alpha = Mathf.MoveTowards(group.alpha, show ? 1f : 0f, Time.unscaledDeltaTime * 8f);
            if (!show) return;

            Canvas canvas = layer.GetComponentInParent<Canvas>();
            Camera canvasCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, screen, canvasCamera, out Vector2 local);
            Rect bounds = layer.rect;
            float scale = Mathf.Lerp(1f, 0.72f, Mathf.InverseLerp(4f, 14f, distance));
            Vector2 half = body.sizeDelta * scale * 0.5f;
            local.x = Mathf.Clamp(local.x, bounds.xMin + half.x + 12f, bounds.xMax - half.x - 12f);
            local.y = Mathf.Clamp(local.y, bounds.yMin + 12f, bounds.yMax - body.sizeDelta.y * scale - 40f);

            float age = Time.unscaledTime - shownAt;
            float pop = Mathf.LerpUnclamped(0.6f, 1f, UiTheme.Pop(age / 0.3f));
            float bob = Mathf.Sin((Time.unscaledTime + seed) * 2.4f) * 4f;
            float shake = warning ? Mathf.Sin(Time.unscaledTime * 60f) * Mathf.Clamp01(1f - age / 0.6f) * 7f : 0f;
            rect.anchoredPosition = local + new Vector2(shake, bob);
            rect.localScale = Vector3.one * scale * pop;
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin((Time.unscaledTime + seed) * 1.3f) * 1.5f);
        }
    }
}
