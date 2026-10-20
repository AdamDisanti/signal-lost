using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SignalLost.UI
{
    /// <summary>
    /// Shared look for the code-built game screens (dialogue, item cards, inventory, merchant menu):
    /// translucent rounded panels, warm laser-orange accents and big readable type. Restyle the game here
    /// rather than in each screen. The rounded sprites are generated at runtime, so no art assets are needed.
    /// </summary>
    public static class UiTheme
    {
        public static readonly Color PanelColor = new Color(0.05f, 0.07f, 0.15f, 0.8f);
        public static readonly Color PanelLight = new Color(0.16f, 0.2f, 0.33f, 0.85f);
        public static readonly Color Edge = new Color(1f, 0.62f, 0.22f, 0.95f);
        public static readonly Color Text = new Color(1f, 0.96f, 0.88f);
        public static readonly Color Muted = new Color(0.74f, 0.79f, 0.9f);
        public static readonly Color Accent = new Color(1f, 0.62f, 0.22f);
        public static readonly Color Good = new Color(0.56f, 0.9f, 0.66f);
        public static readonly Color Warning = new Color(1f, 0.3f, 0.26f);
        public static readonly Color WarningPanel = new Color(0.32f, 0.03f, 0.04f, 0.86f);
        public static readonly Color Dim = new Color(0f, 0f, 0f, 0.4f);

        // Reference resolution for every overlay canvas; sizes below are in these pixels.
        public static readonly Vector2 Reference = new Vector2(1920f, 1080f);

        private static Sprite rounded, outline;

        /// <summary>Speaker name colours, so each character is recognisable at a glance.</summary>
        public static Color SpeakerColor(string speaker)
        {
            string name = (speaker ?? "").ToLowerInvariant();
            if (name.Contains("cavendish")) return new Color(0.42f, 0.9f, 0.86f);
            if (name.Contains("crab")) return new Color(1f, 0.62f, 0.36f);
            if (name.Contains("mechanic") || name.Contains("you")) return new Color(1f, 0.86f, 0.42f);
            return Accent;
        }

        public static string Hex(Color color) => "#" + ColorUtility.ToHtmlStringRGB(color);

        /// <summary>A filled rounded rectangle, nine-sliced so it scales to any size.</summary>
        public static Sprite Rounded => rounded != null ? rounded : rounded = MakeRounded(false);

        /// <summary>The border of the same rounded rectangle, for outlines.</summary>
        public static Sprite Outline => outline != null ? outline : outline = MakeRounded(true);

        public static Canvas CreateCanvas(string name, int sortingOrder)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = Reference;
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var item = new GameObject(name, typeof(RectTransform));
            item.transform.SetParent(parent, false);
            return (RectTransform)item.transform;
        }

        public static RectTransform Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            return rect;
        }

        public static RectTransform Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        /// <summary>A rounded panel with an optional coloured outline. Corner size scales with <paramref name="corner"/>.</summary>
        public static Image Panel(string name, Transform parent, Color fill, Color? edge = null, float corner = 1f)
        {
            RectTransform rect = Rect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = Rounded;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f / Mathf.Max(0.05f, corner);
            image.color = fill;
            image.raycastTarget = false;
            if (edge.HasValue)
            {
                var border = Stretch(Rect("Edge", rect)).gameObject.AddComponent<Image>();
                border.sprite = Outline;
                border.type = Image.Type.Sliced;
                border.pixelsPerUnitMultiplier = image.pixelsPerUnitMultiplier;
                border.color = edge.Value;
                border.raycastTarget = false;
            }
            return image;
        }

        public static Image Block(string name, Transform parent, Color color)
        {
            var image = Rect(name, parent).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static TextMeshProUGUI Label(string name, Transform parent, float size, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.Left, FontStyles style = FontStyles.Normal)
        {
            var text = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.fontStyle = style;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>Overshooting ease (0 to 1, briefly above 1) for the bouncy pop-in animations.</summary>
        public static float Pop(float t)
        {
            t = Mathf.Clamp01(t);
            const float c = 1.9f;
            float p = t - 1f;
            return 1f + (c + 1f) * p * p * p + c * p * p;
        }

        private static Sprite MakeRounded(bool border)
        {
            const int size = 64;
            const float radius = 24f, line = 3f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = border ? "UiRoundedOutline" : "UiRounded",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // Signed distance to a rounded square: negative inside, positive outside.
                float px = Mathf.Abs(x + 0.5f - size * 0.5f) - (size * 0.5f - radius);
                float py = Mathf.Abs(y + 0.5f - size * 0.5f) - (size * 0.5f - radius);
                float outside = new Vector2(Mathf.Max(px, 0f), Mathf.Max(py, 0f)).magnitude + Mathf.Min(Mathf.Max(px, py), 0f) - radius;
                float alpha = Mathf.Clamp01(0.5f - outside);
                if (border) alpha *= Mathf.Clamp01(outside + line + 0.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            float slice = radius + 2f;
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(slice, slice, slice, slice));
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
