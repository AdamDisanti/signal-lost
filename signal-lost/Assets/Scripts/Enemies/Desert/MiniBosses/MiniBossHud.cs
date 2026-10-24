using System.Collections.Generic;
using UnityEngine;

namespace SignalLost.Enemies.Desert
{
    /// <summary>
    /// PLACEHOLDER mini-boss UI, drawn with IMGUI so the shared PlayerHUD prefab is untouched:
    /// - a boss health bar at the top while a mini-boss is engaged (name, health, a notch where phase 2 starts and one where phase 3 starts, if it has one);
    /// - short centre-screen messages (part damaged, defeated, part recovered);
    /// - the sand-blind overlay from Old Buzzard's wing gust.
    /// Created automatically when needed; one per scene. Replace with real HUD elements during the UI pass.
    /// </summary>
    public sealed class MiniBossHud : MonoBehaviour
    {
        private static MiniBossHud instance;
        private static readonly List<DesertMiniBoss> Engaged = new List<DesertMiniBoss>();
        private static string message;
        private static Color messageColor;
        private static float messageUntil = float.NegativeInfinity;
        private static float sandUntil = float.NegativeInfinity, sandLength = 1f;

        private GUIStyle title, banner;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            Engaged.Clear();
            messageUntil = sandUntil = float.NegativeInfinity;
        }

        public static void Ensure()
        {
            if (instance != null) return;
            instance = FindAnyObjectByType<MiniBossHud>();
            if (instance == null) instance = new GameObject("Mini-Boss HUD").AddComponent<MiniBossHud>();
        }

        public static void Register(DesertMiniBoss boss)
        {
            Ensure();
            if (boss != null && !Engaged.Contains(boss)) Engaged.Add(boss);
        }

        public static void Unregister(DesertMiniBoss boss) => Engaged.Remove(boss);

        public static void Message(string text, Color color, float seconds = 3f)
        {
            Ensure();
            message = text;
            messageColor = color;
            messageUntil = Time.time + seconds;
        }

        /// <summary>Sand in the eyes: a tan haze over the screen that fades over <paramref name="seconds"/>.</summary>
        public static void SandBlind(float seconds)
        {
            Ensure();
            sandLength = Mathf.Max(0.1f, seconds);
            sandUntil = Mathf.Max(sandUntil, Time.time + seconds);
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }
            instance = this;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void OnGUI()
        {
            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                banner = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            }

            // Sand haze first, so the bars and messages stay readable on top of it.
            float sand = Mathf.Clamp01((sandUntil - Time.time) / sandLength);
            if (sand > 0f)
            {
                Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0.86f, 0.72f, 0.5f, 0.92f * Mathf.Sqrt(sand)));
                for (int i = 0; i < 40; i++) // drifting grit
                {
                    float x = Mathf.Repeat(i * 97.3f + Time.time * (60f + i * 7f), Screen.width);
                    float y = Mathf.Repeat(i * 53.1f + Mathf.Sin(Time.time * 2f + i) * 30f, Screen.height);
                    Fill(new Rect(x, y, 4f, 3f), new Color(0.55f, 0.4f, 0.25f, 0.8f * sand));
                }
            }

            float cx = Screen.width * 0.5f;
            float y0 = 14f;
            for (int i = Engaged.Count - 1; i >= 0; i--)
                if (Engaged[i] == null) Engaged.RemoveAt(i);
            foreach (DesertMiniBoss boss in Engaged)
            {
                const float width = 460f, height = 14f;
                var bar = new Rect(cx - width * 0.5f, y0 + 26f, width, height);
                int phase = boss.DisplayPhase;
                string name = phase == 3 ? boss.DisplayName + "  (furious)" : phase == 2 ? boss.DisplayName + "  (enraged)" : boss.DisplayName;
                Label(new Rect(cx - 300f, y0, 600f, 26f), name, title, Color.white);
                Fill(new Rect(bar.x - 2f, bar.y - 2f, bar.width + 4f, bar.height + 4f), new Color(0f, 0f, 0f, 0.7f));
                Color fill = phase == 3 ? new Color(0.9f, 0.12f, 0.12f) : phase == 2 ? new Color(1f, 0.35f, 0.15f) : new Color(0.9f, 0.6f, 0.15f);
                if (boss.InPhaseShift && Mathf.Sin(Time.time * 30f) > 0f) fill = Color.white;
                Fill(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(boss.HealthFraction), bar.height), fill);
                Fill(new Rect(bar.x + bar.width * boss.PhaseTwoAt - 1f, bar.y - 3f, 2f, bar.height + 6f), new Color(1f, 1f, 1f, 0.8f));
                if (boss.PhaseThreeAt > 0f)
                    Fill(new Rect(bar.x + bar.width * boss.PhaseThreeAt - 1f, bar.y - 3f, 2f, bar.height + 6f), new Color(1f, 1f, 1f, 0.8f));
                y0 += 52f;
            }

            if (Time.time < messageUntil && !string.IsNullOrEmpty(message))
            {
                float alpha = Mathf.Clamp01((messageUntil - Time.time) / 0.5f);
                Label(new Rect(cx - 360f, Screen.height * 0.3f, 720f, 70f), message, banner,
                    new Color(messageColor.r, messageColor.g, messageColor.b, alpha));
            }
        }

        private static void Label(Rect rect, string text, GUIStyle style, Color color)
        {
            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.75f * color.a);
            GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, style);
            GUI.color = color;
            GUI.Label(rect, text, style);
            GUI.color = previous;
        }

        private static void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
