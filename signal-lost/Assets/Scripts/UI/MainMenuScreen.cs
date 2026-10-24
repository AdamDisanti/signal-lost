using System.Collections.Generic;
using SignalLost.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SignalLost.UI
{
    /// <summary>
    /// The title screen (MainMenu scene, meeting notes 10-22), in the spirit of Terraria's: the title in the middle
    /// over a golden-hour low-poly scene (mountains, hills, pines, the smoking wreck and Cavendish by his campfire),
    /// with New Game, Continue, Settings and Exit. Continue and parts of Settings are placeholders until saving lands.
    /// Everything is drawn in code, so there are no art assets; tweak colours and shapes here.
    /// </summary>
    public sealed class MainMenuScreen : MonoBehaviour
    {
        private static readonly Color Title = new Color(1f, 0.6f, 0.18f);

        private RectTransform art;
        private TextMeshProUGUI title;
        private SettingsPanel settings;
        private GameObject settingsRoot;
        private readonly List<(Image image, float phase)> stars = new List<(Image, float)>();
        private readonly List<(RectTransform rect, Image image, float offset, Vector2 origin, float drift)> smoke = new List<(RectTransform, Image, float, Vector2, float)>();
        private readonly List<(RectTransform rect, float phase)> buttons = new List<(RectTransform, float)>();
        private UiShape fire;
        private Image fireGlow;

        private void Awake()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            GameSettings.Apply();
            UiTheme.EnsureEventSystem();
            Canvas canvas = UiTheme.CreateCanvas("Main Menu", 10);
            canvas.transform.SetParent(transform, false);
            Build((RectTransform)canvas.transform);
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            foreach ((Image image, float phase) in stars)
                image.color = new Color(1f, 1f, 0.9f, 0.35f + 0.45f * Mathf.Abs(Mathf.Sin(now * 1.3f + phase)));
            foreach (var puff in smoke)
            {
                float k = Mathf.Repeat(now * 0.12f + puff.offset, 1f);
                puff.rect.anchoredPosition = puff.origin + new Vector2(k * puff.drift + Mathf.Sin(now + puff.offset * 9f) * 6f, k * 260f);
                puff.rect.localScale = Vector3.one * Mathf.Lerp(0.5f, 2.1f, k);
                puff.image.color = new Color(0.32f, 0.3f, 0.32f, 0.5f * (1f - k) * Mathf.Clamp01(k * 6f));
            }
            float flicker = 0.85f + Mathf.PerlinNoise(now * 6f, 0f) * 0.35f;
            fire.rectTransform.localScale = new Vector3(1f, flicker, 1f);
            fireGlow.color = new Color(1f, 0.6f, 0.2f, 0.45f * flicker);
            AnimateTitle(now);
            for (int i = 0; i < buttons.Count; i++)
            {
                GameObject selectedObject = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
                bool selected = selectedObject != null && selectedObject.transform.parent == buttons[i].rect;
                float target = selected ? 1.15f : 1f;
                RectTransform rect = buttons[i].rect;
                rect.localScale = Vector3.one * Mathf.MoveTowards(rect.localScale.x, target, Time.unscaledDeltaTime * 2f);
                rect.localRotation = Quaternion.Euler(0f, 0f, selected ? Mathf.Sin(now * 5f + buttons[i].phase) * 2f : 0f);
            }
            if (settingsRoot.activeSelf && Keyboard.current?.escapeKey.wasPressedThisFrame == true) settingsRoot.SetActive(false);
        }

        // Letters bob in a gentle wave.
        private void AnimateTitle(float now)
        {
            title.ForceMeshUpdate();
            TMP_TextInfo info = title.textInfo;
            for (int i = 0; i < info.characterCount; i++)
            {
                TMP_CharacterInfo character = info.characterInfo[i];
                if (!character.isVisible) continue;
                Vector3[] vertices = info.meshInfo[character.materialReferenceIndex].vertices;
                Vector3 offset = new Vector3(0f, Mathf.Sin(now * 2.2f + i * 0.55f) * 9f, 0f);
                for (int v = 0; v < 4; v++) vertices[character.vertexIndex + v] += offset;
            }
            for (int m = 0; m < info.meshInfo.Length; m++)
            {
                info.meshInfo[m].mesh.vertices = info.meshInfo[m].vertices;
                title.UpdateGeometry(info.meshInfo[m].mesh, m);
            }
        }

        // ---------- Actions ----------

        // A new game starts with an empty camp: placed trophies are the only thing saved so far.
        private void NewGame()
        {
            SignalLost.Progression.CampSave.Clear();
            SceneManager.LoadScene(SceneNames.FirstLevel);
        }

        private void OpenSettings()
        {
            settingsRoot.SetActive(true);
            settings.Show(SettingsPanel.Page.Controls);
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ---------- Layout ----------

        private void Build(RectTransform root)
        {
            // Art is laid out on a fixed 1920 x 1080 board, centred and slightly over-scanned.
            art = UiTheme.Place(UiTheme.Rect("Art", root), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1920f, 1080f));
            art.localScale = Vector3.one * 1.08f;
            Image sky = art.gameObject.AddComponent<Image>();
            sky.sprite = Gradient(new Color(0.15f, 0.2f, 0.4f), new Color(0.95f, 0.6f, 0.4f), new Color(1f, 0.86f, 0.55f));
            sky.raycastTarget = false;

            var random = new System.Random(7);
            for (int i = 0; i < 45; i++)
            {
                Image star = UiTheme.Panel("Star", art, Color.white, null, 0.2f);
                float size = 2f + (float)random.NextDouble() * 3f;
                UiTheme.Place(star.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(-940f + (float)random.NextDouble() * 1880f, 260f + (float)random.NextDouble() * 270f), new Vector2(size, size));
                stars.Add((star, (float)random.NextDouble() * 6f));
            }
            Circle(art, "Sun Glow", new Vector2(520f, 60f), 620f, new Color(1f, 0.85f, 0.55f, 0.55f), true);
            Circle(art, "Sun", new Vector2(520f, 60f), 210f, new Color(1f, 0.92f, 0.68f));

            UiShape.Ridge("Far Mountains", art, new Color(0.56f, 0.45f, 0.6f), -540f,
                V(-960, 40), V(-820, 150), V(-700, 60), V(-560, 210), V(-430, 90), V(-300, 170), V(-150, 70), V(0, 140),
                V(130, 60), V(260, 190), V(400, 80), V(560, 230), V(700, 100), V(840, 170), V(960, 60)).SetTopTint(new Color(1.25f, 1.15f, 1.2f));
            UiShape.Ridge("Hills", art, new Color(0.36f, 0.56f, 0.3f), -540f,
                V(-960, -90), V(-760, -40), V(-520, -80), V(-300, -30), V(-80, -70), V(150, -20), V(380, -60), V(620, -10), V(820, -60), V(960, -40)).SetTopTint(new Color(1.2f, 1.2f, 0.95f));

            // The crashed ship on the hill, nose in the dirt, smoke drifting up.
            RectTransform ship = UiTheme.Place(UiTheme.Rect("Ship", art), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(600f, -55f), new Vector2(10f, 10f));
            ship.localRotation = Quaternion.Euler(0f, 0f, -14f);
            ship.localScale = Vector3.one * 0.8f;
            UiShape.Create("Hull", ship, new Color(0.88f, 0.86f, 0.8f), V(-150, -10), V(-120, 30), V(60, 40), V(140, 10), V(150, -15), V(-140, -25));
            UiShape.Create("Stripe", ship, new Color(0.95f, 0.47f, 0.13f), V(-140, -2), V(140, 4), V(146, -6), V(-142, -12));
            UiShape.Create("Fin", ship, new Color(0.82f, 0.8f, 0.74f), V(-150, 20), V(-185, 75), V(-150, 70), V(-110, 28));
            UiShape.Create("Wing", ship, new Color(0.7f, 0.68f, 0.64f), V(-40, -20), V(40, -20), V(-10, -55), V(-70, -55));
            for (int i = 0; i < 9; i++)
            {
                Image puff = UiTheme.Circle("Smoke", art, Color.gray);
                Vector2 origin = new Vector2(500f + (i % 3) * 24f, -40f);
                UiTheme.Place(puff.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), origin, new Vector2(46f, 46f));
                smoke.Add((puff.rectTransform, puff, i / 9f, origin, -60f - i * 8f));
            }

            UiShape.Ridge("Near Hills", art, new Color(0.2f, 0.4f, 0.22f), -540f,
                V(-960, -170), V(-700, -210), V(-450, -180), V(-200, -240), V(60, -200), V(320, -250), V(600, -190), V(960, -230)).SetTopTint(new Color(1.2f, 1.25f, 1f));
            foreach ((float x, float y, float scale) in new[] { (-860f, -220f, 1.6f), (-760f, -230f, 1.15f), (-560f, -205f, 0.8f), (-120f, -240f, 0.6f),
                         (480f, -230f, 0.7f), (760f, -230f, 1.2f), (880f, -240f, 1.7f) })
                Pine(art, new Vector2(x, y), scale);

            // Cavendish at his campfire.
            fireGlow = Circle(art, "Fire Glow", new Vector2(-380f, -240f), 300f, new Color(1f, 0.6f, 0.2f, 0.45f), true);
            RectTransform fireRoot = UiTheme.Place(UiTheme.Rect("Fire", art), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), new Vector2(-380f, -265f), new Vector2(10f, 10f));
            fire = UiShape.Create("Flame", fireRoot, new Color(1f, 0.55f, 0.15f), V(-22, 0), V(0, 62), V(22, 0));
            UiShape.Create("Inner Flame", fireRoot, new Color(1f, 0.85f, 0.35f), V(-11, 0), V(0, 34), V(11, 0)).transform.SetParent(fire.transform, false);
            RectTransform cav = UiTheme.Place(UiTheme.Rect("Cavendish", art), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), new Vector2(-300f, -268f), new Vector2(10f, 10f));
            // A capybara astronaut sitting by the fire, facing it.
            Blob(cav, "Helmet", new Vector2(-6f, 80f), new Vector2(70f, 64f), new Color(0.42f, 0.9f, 0.86f, 0.3f));
            Blob(cav, "Body", new Vector2(0f, 30f), new Vector2(66f, 60f), new Color(0.9f, 0.9f, 0.86f));
            Blob(cav, "Suit Stripe", new Vector2(0f, 38f), new Vector2(66f, 8f), new Color(0.42f, 0.9f, 0.86f));
            Blob(cav, "Ear", new Vector2(6f, 98f), new Vector2(12f, 12f), new Color(0.38f, 0.25f, 0.16f));
            Blob(cav, "Head", new Vector2(-6f, 78f), new Vector2(48f, 40f), new Color(0.52f, 0.36f, 0.22f));
            Blob(cav, "Snout", new Vector2(-28f, 72f), new Vector2(26f, 22f), new Color(0.44f, 0.3f, 0.19f));
            Blob(cav, "Eye", new Vector2(-14f, 84f), new Vector2(6f, 6f), new Color(0.08f, 0.06f, 0.05f));

            // Title.
            Shadow(root, "SIGNAL LOST", 170f, new Vector2(0f, 300f), new Vector2(1600f, 200f), new Vector2(6f, -8f));
            title = UiTheme.Label("Title", root, 170f, Title, TextAlignmentOptions.Center, FontStyles.Bold);
            UiTheme.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 300f), new Vector2(1600f, 200f));
            title.text = "SIGNAL LOST";
            title.textWrappingMode = TextWrappingModes.NoWrap;
            TextMeshProUGUI tagline = UiTheme.Label("Tagline", root, 32f, new Color(1f, 0.95f, 0.85f), TextAlignmentOptions.Center, FontStyles.Italic);
            UiTheme.Place(tagline.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 190f), new Vector2(1200f, 44f));
            tagline.text = "a stranded space mechanic's misadventure";

            // Menu: big text buttons stacked in the middle.
            (string label, UnityEngine.Events.UnityAction action, bool enabled)[] entries =
            {
                ("New Game", NewGame, true), ("Continue", null, false), ("Settings", OpenSettings, true), ("Exit", Quit, true)
            };
            Button first = null;
            for (int i = 0; i < entries.Length; i++)
            {
                Button button = TextButton(root, entries[i].label, new Vector2(0f, 60f - i * 86f));
                if (entries[i].action != null) button.onClick.AddListener(entries[i].action);
                button.interactable = entries[i].enabled;
                buttons.Add(((RectTransform)button.transform.parent, i * 1.3f));
                if (first == null) first = button;
            }
            TextMeshProUGUI noSave = UiTheme.Label("No Save", root, 22f, UiTheme.Muted, TextAlignmentOptions.Left, FontStyles.Italic);
            UiTheme.Place(noSave.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(150f, -26f), new Vector2(300f, 30f));
            noSave.text = "(no save yet)";
            noSave.color = new Color(1f, 0.95f, 0.85f, 0.85f);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(first.gameObject);

            TextMeshProUGUI footer = UiTheme.Label("Footer", root, 20f, new Color(1f, 1f, 1f, 0.7f), TextAlignmentOptions.BottomLeft);
            UiTheme.Place(footer.rectTransform, Vector2.zero, Vector2.zero, new Vector2(24f, 16f), new Vector2(900f, 30f));
            footer.text = "Prototype build  -  Group 18";
            TextMeshProUGUI joke = UiTheme.Label("Approved", root, 20f, new Color(1f, 1f, 1f, 0.7f), TextAlignmentOptions.BottomRight, FontStyles.Italic);
            UiTheme.Place(joke.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 16f), new Vector2(900f, 30f));
            joke.text = "Approved* by the Gleebinglorb Republic Maintenance Corps   *pending review";

            settingsRoot = UiTheme.Stretch(UiTheme.Rect("Settings Overlay", root)).gameObject;
            Image dim = UiTheme.Block("Dim", settingsRoot.transform, new Color(0f, 0f, 0f, 0.55f));
            UiTheme.Stretch(dim.rectTransform);
            dim.raycastTarget = true;
            settings = SettingsPanel.Create(settingsRoot.transform, new Vector2(1000f, 680f));
            UiTheme.Place((RectTransform)settings.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 680f));
            Button close = UiTheme.Button("Close", settings.transform, "Close", 24f);
            UiTheme.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -26f), new Vector2(140f, 52f));
            close.onClick.AddListener(() => settingsRoot.SetActive(false));
            settingsRoot.SetActive(false);
        }

        private static Vector2 V(float x, float y) => new Vector2(x, y);

        private static Image Circle(RectTransform parent, string name, Vector2 centre, float size, Color color, bool soft = false)
        {
            Image circle = UiTheme.Circle(name, parent, color, soft);
            UiTheme.Place(circle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), centre, new Vector2(size, size));
            return circle;
        }

        private static void Blob(RectTransform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            Image blob = UiTheme.Panel(name, parent, color, null, 1.2f);
            UiTheme.Place(blob.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), position, size);
        }

        // A stacked low-poly pine: three triangles on a trunk.
        private static void Pine(RectTransform parent, Vector2 foot, float scale)
        {
            RectTransform tree = UiTheme.Place(UiTheme.Rect("Pine", parent), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), foot, new Vector2(10f, 10f));
            tree.localScale = Vector3.one * scale;
            UiShape.Create("Trunk", tree, new Color(0.3f, 0.2f, 0.13f), V(-9, -10), V(-9, 40), V(9, 40), V(9, -10));
            Color leaves = new Color(0.12f, 0.3f, 0.17f);
            for (int tier = 0; tier < 3; tier++)
            {
                float y = 30f + tier * 60f, width = 95f - tier * 22f;
                UiShape.Create("Tier", tree, leaves, V(-width, y), V(0, y + 110f), V(width, y)).SetTopTint(new Color(1.35f, 1.35f, 1.1f));
            }
        }

        // A dark copy of a line of text, offset behind it, so it reads on any background.
        private static void Shadow(RectTransform parent, string content, float size, Vector2 position, Vector2 box, Vector2 offset)
        {
            TextMeshProUGUI shadow = UiTheme.Label("Shadow", parent, size, new Color(0.1f, 0.05f, 0.08f, 0.75f), TextAlignmentOptions.Center, FontStyles.Bold);
            UiTheme.Place(shadow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position + offset, box);
            shadow.text = content;
            shadow.textWrappingMode = TextWrappingModes.NoWrap;
        }

        // The text and its shadow share a parent, so the hover grow and wobble move them together.
        private static Button TextButton(RectTransform parent, string label, Vector2 position)
        {
            RectTransform holder = UiTheme.Place(UiTheme.Rect(label + " Entry", parent), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, new Vector2(520f, 76f));
            Shadow(holder, label, 58f, Vector2.zero, new Vector2(520f, 76f), new Vector2(3f, -4f));
            TextMeshProUGUI text = UiTheme.Label(label, holder, 58f, new Color(1f, 0.97f, 0.9f), TextAlignmentOptions.Center, FontStyles.Bold);
            UiTheme.Place(text.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 76f));
            text.text = label;
            text.raycastTarget = true;
            var button = text.gameObject.AddComponent<Button>();
            button.targetGraphic = text;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Title;
            colors.selectedColor = Title;
            colors.pressedColor = new Color(1f, 0.8f, 0.5f);
            colors.disabledColor = new Color(0.6f, 0.6f, 0.65f, 0.6f);
            button.colors = colors;
            return button;
        }

        private static Sprite Gradient(Color top, Color middle, Color bottom)
        {
            var texture = new Texture2D(1, 256, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < 256; y++)
            {
                float t = y / 255f;
                texture.SetPixel(0, y, t < 0.5f ? Color.Lerp(bottom, middle, t / 0.5f) : Color.Lerp(middle, top, (t - 0.5f) / 0.5f));
            }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, 1f, 256f), new Vector2(0.5f, 0.5f));
        }
    }
}
