using SignalLost.Combat;
using SignalLost.Player;
using UnityEngine;

namespace SignalLost.Enemies.Desert
{
    /// <summary>
    /// PLACEHOLDER on-screen feedback for the desert fodder mechanics, drawn with IMGUI so it needs no changes to the
    /// shared PlayerHUD prefab: a red "SPOTTED" banner and edge tint while a Dish Meerkat has tagged the player,
    /// "SCANNER JAMMED" while a meerkat's radar is jamming the raised scanner, and a "shake them off" hint while
    /// Spark Ticks are latched on. Created automatically the first time it is needed; one per scene.
    /// Replace with real HUD elements during the UI pass.
    /// </summary>
    public sealed class FodderStatusOverlay : MonoBehaviour
    {
        private static FodderStatusOverlay instance;

        private GUIStyle banner, small;
        private Health playerHealth;
        private bool playerSearched;

        /// <summary>Makes sure an overlay exists in the scene.</summary>
        public static void Ensure()
        {
            if (instance != null) return;
            instance = FindAnyObjectByType<FodderStatusOverlay>();
            if (instance == null) instance = new GameObject("Fodder Status Overlay").AddComponent<FodderStatusOverlay>();
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

        private void Update()
        {
            if (!playerSearched)
            {
                playerSearched = true;
                var player = FindAnyObjectByType<FirstPersonController>();
                if (player != null) playerHealth = player.GetComponent<Health>();
            }
            // Dying ends the spotted state, so the player doesn't respawn already tagged.
            if (playerHealth != null && playerHealth.IsDead) SpotterAlert.Clear();
        }

        private void OnGUI()
        {
            if (playerHealth != null && playerHealth.IsDead) return;
            if (banner == null)
            {
                banner = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                small = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            }

            float cx = Screen.width * 0.5f;
            float y = Screen.height * 0.2f;

            if (SpotterAlert.IsMarked)
            {
                // Pulsing red edges, then the banner and a draining timer bar.
                float pulse = 0.18f + 0.1f * Mathf.Sin(Time.time * 10f);
                var edge = new Color(0.9f, 0.1f, 0.05f, pulse);
                float w = Screen.width, h = Screen.height, t = Mathf.Max(8f, h * 0.025f);
                Fill(new Rect(0, 0, w, t), edge);
                Fill(new Rect(0, h - t, w, t), edge);
                Fill(new Rect(0, 0, t, h), edge);
                Fill(new Rect(w - t, 0, t, h), edge);

                Label(new Rect(cx - 200f, y, 400f, 34f), "SPOTTED!", banner, new Color(1f, 0.3f, 0.2f));
                float left = Mathf.Clamp01(SpotterAlert.TimeLeft / 6f);
                Fill(new Rect(cx - 80f, y + 36f, 160f, 5f), new Color(0f, 0f, 0f, 0.5f));
                Fill(new Rect(cx - 80f, y + 36f, 160f * left, 5f), new Color(1f, 0.35f, 0.2f));
                y += 52f;
            }

            if (ScannerJamReceiver.IsJammedAndRaised)
            {
                Label(new Rect(cx - 200f, y, 400f, 26f), Random.value < 0.85f ? "SCANNER JAMMED" : "SC4NN3R J4MM#D", small,
                    new Color(1f, 0.85f, 0.3f));
                y += 30f;
            }

            if (SparkTick.LatchedCount > 0)
            {
                Label(new Rect(cx - 250f, Screen.height * 0.72f, 500f, 26f),
                    $"{SparkTick.LatchedCount} SPARK TICK{(SparkTick.LatchedCount > 1 ? "S" : "")} ON YOU!  Swing or jump to shake them off",
                    small, new Color(1f, 0.75f, 0.35f));
            }
        }

        private static void Label(Rect rect, string text, GUIStyle style, Color color)
        {
            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
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
