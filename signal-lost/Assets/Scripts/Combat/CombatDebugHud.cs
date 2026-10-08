using UnityEngine;

namespace SignalLost.Combat
{
    /// <summary>
    /// Temporary on-screen readout for testing combat: crosshair, laser charge bar, and the health
    /// of whatever the beam is hitting. Placeholder only; the real HUD will replace it.
    /// </summary>
    public sealed class CombatDebugHud : MonoBehaviour
    {
        [SerializeField] private MiningLaser laser;

        private GUIStyle label;

        private void Awake()
        {
            if (laser == null) laser = GetComponentInChildren<MiningLaser>();
        }

        private void OnGUI()
        {
            if (laser == null) return;
            if (label == null)
                label = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter };

            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;

            if (Cursor.lockState != CursorLockMode.Locked)
            {
                GUI.Label(new Rect(cx - 200f, cy - 15f, 400f, 30f), "Click the Game view to resume", label);
                return;
            }

            // Crosshair
            Fill(new Rect(cx - 8f, cy - 1f, 16f, 2f), Color.white);
            Fill(new Rect(cx - 1f, cy - 8f, 2f, 16f), Color.white);

            // Charge bar
            const float width = 260f, height = 14f;
            var bar = new Rect(cx - width * 0.5f, Screen.height - 60f, width, height);
            Fill(bar, new Color(0f, 0f, 0f, 0.6f));
            Color fillColor = laser.IsOverheated ? new Color(0.95f, 0.25f, 0.15f) : new Color(0.3f, 0.85f, 1f);
            Fill(new Rect(bar.x, bar.y, bar.width * laser.Charge01, bar.height), fillColor);
            string state = laser.IsOverheated ? "OVERHEATED" : $"LASER {Mathf.RoundToInt(laser.Charge01 * 100f)}%";
            GUI.Label(new Rect(bar.x, bar.y - 26f, bar.width, 24f), state, label);

            // Target readout
            Health target = laser.CurrentTarget;
            if (target != null)
                GUI.Label(new Rect(cx - 200f, cy + 20f, 400f, 24f),
                    $"{target.name}  {Mathf.CeilToInt(target.Current)} / {Mathf.CeilToInt(target.Max)}", label);
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
