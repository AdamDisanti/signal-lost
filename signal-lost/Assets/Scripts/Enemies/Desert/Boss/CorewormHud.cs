using UnityEngine;

namespace SignalLost.Enemies.Desert
{
    /// <summary>
    /// PLACEHOLDER IMGUI for the desert stage boss: a wide health bar split into four phases, the boss's name card,
    /// subtitles and full-screen fades for the cutscene. Banners still go through <see cref="MiniBossHud"/>.
    /// The shared HUD prefab is untouched.
    /// </summary>
    public sealed class CorewormHud : MonoBehaviour
    {
        private static CorewormHud instance;
        private static Coreworm boss;
        private static float fade, fadeFrom, fadeTo, fadeStart, fadeLength;
        private static Color fadeColor = Color.black;
        private static string cardTitle, cardSubtitle, subtitle;
        private static float cardStart, cardUntil, subtitleUntil;

        private Texture2D white;
        private GUIStyle titleStyle, subStyle, nameStyle, subtitleStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            boss = null;
            fade = fadeFrom = fadeTo = 0f;
            fadeLength = 0f;
            cardUntil = subtitleUntil = 0f;
        }

        public static void Ensure()
        {
            if (instance != null) return;
            instance = new GameObject("Coreworm HUD").AddComponent<CorewormHud>();
        }

        /// <summary>Shows the boss bar for this boss (null hides it).</summary>
        public static void ShowBoss(Coreworm worm)
        {
            Ensure();
            boss = worm;
        }

        /// <summary>Fades the screen to <paramref name="alpha"/> (1 is solid) over some seconds.</summary>
        public static void FadeTo(float alpha, float seconds, Color? color = null)
        {
            Ensure();
            if (color.HasValue) fadeColor = color.Value;
            fadeFrom = fade;
            fadeTo = Mathf.Clamp01(alpha);
            fadeStart = Time.time;
            fadeLength = Mathf.Max(0.01f, seconds);
        }

        public static void NameCard(string title, string sub, float seconds)
        {
            Ensure();
            cardTitle = title;
            cardSubtitle = sub;
            cardStart = Time.time;
            cardUntil = Time.time + seconds;
        }

        /// <summary>A line of narration at the bottom of the screen.</summary>
        public static void Subtitle(string text, float seconds)
        {
            Ensure();
            subtitle = text;
            subtitleUntil = Time.time + seconds;
        }

        private void Update()
        {
            float k = fadeLength > 0f ? Mathf.Clamp01((Time.time - fadeStart) / fadeLength) : 1f;
            fade = Mathf.Lerp(fadeFrom, fadeTo, k);
        }

        private void OnGUI()
        {
            if (white == null)
            {
                white = Texture2D.whiteTexture;
                titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 54, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                subStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Italic, alignment = TextAnchor.MiddleCenter };
                nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
                subtitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            }
            GUI.depth = -10;

            if (boss != null && !boss.IsDefeated) DrawBar();

            if (Time.time < cardUntil)
            {
                float t = Time.time - cardStart, left = cardUntil - Time.time;
                float a = Mathf.Clamp01(t / 0.5f) * Mathf.Clamp01(left / 0.6f);
                Rect band = new Rect(0f, Screen.height * 0.3f, Screen.width, 130f);
                GUI.color = new Color(0f, 0f, 0f, 0.55f * a);
                GUI.DrawTexture(band, white);
                Shadowed(new Rect(0f, band.y + 10f, Screen.width, 70f), cardTitle, titleStyle, new Color(1f, 0.62f, 0.2f, a));
                Shadowed(new Rect(0f, band.y + 78f, Screen.width, 40f), cardSubtitle, subStyle, new Color(1f, 0.9f, 0.75f, a));
            }

            if (fade > 0.001f)
            {
                GUI.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, fade);
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), white);
            }

            if (Time.time < subtitleUntil && !string.IsNullOrEmpty(subtitle))
            {
                float a = Mathf.Clamp01((subtitleUntil - Time.time) / 0.4f);
                Shadowed(new Rect(Screen.width * 0.15f, Screen.height - 150f, Screen.width * 0.7f, 60f), subtitle, subtitleStyle, new Color(1f, 1f, 1f, a));
            }
            GUI.color = Color.white;
        }

        private void DrawBar()
        {
            float width = Mathf.Min(Screen.width * 0.7f, 900f);
            Rect back = new Rect((Screen.width - width) * 0.5f, 22f, width, 22f);
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(new Rect(back.x - 3f, back.y - 3f, back.width + 6f, back.height + 6f), white);
            GUI.color = new Color(0.25f, 0.08f, 0.04f, 0.9f);
            GUI.DrawTexture(back, white);
            Color[] colors = { new Color(1f, 0.62f, 0.2f), new Color(0.9f, 0.75f, 0.45f), new Color(1f, 0.42f, 0.15f), new Color(1f, 0.95f, 0.8f) };
            GUI.color = colors[Mathf.Clamp(boss.Phase - 1, 0, 3)];
            GUI.DrawTexture(new Rect(back.x, back.y, back.width * boss.HealthFraction, back.height), white);
            // Notches where each new phase begins.
            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            foreach (float at in boss.PhaseThresholds)
                GUI.DrawTexture(new Rect(back.x + back.width * at - 1.5f, back.y - 3f, 3f, back.height + 6f), white);
            string label = boss.Invulnerable ? $"{boss.DisplayName}   —   {boss.PhaseName}   (invulnerable)" : $"{boss.DisplayName}   —   {boss.PhaseName}";
            Shadowed(new Rect(back.x, back.y + back.height + 4f, back.width, 26f), label, nameStyle, Color.white);
        }

        private static void Shadowed(Rect rect, string text, GUIStyle style, Color color)
        {
            GUI.color = new Color(0f, 0f, 0f, color.a * 0.8f);
            GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, style);
            GUI.color = color;
            GUI.Label(rect, text, style);
        }
    }
}
