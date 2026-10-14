using UnityEngine;

namespace SignalLost.Combat
{
    /// <summary>
    /// Temporary on-screen readout for testing combat: crosshair with hit marker, combo counter,
    /// charge bar, controls hint, and the health of whatever is under the crosshair.
    /// Placeholder only; the real HUD will replace it.
    /// </summary>
    public sealed class CombatDebugHud : MonoBehaviour
    {
        [SerializeField] private MiningLaser laser;

        private GUIStyle label, hint;

        private void Awake()
        {
            if (laser == null) laser = GetComponentInChildren<MiningLaser>();
        }

        private void OnGUI()
        {
            if (laser == null) return;
            if (label == null)
            {
                label = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
                hint = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.LowerLeft };
            }

            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;

            if (Cursor.lockState != CursorLockMode.Locked)
            {
                GUI.Label(new Rect(cx - 200f, cy - 15f, 400f, 30f), "Click the Game view to resume", label);
                return;
            }

            // Crosshair turns into a yellow hit marker briefly after landing a hit.
            bool justHit = Time.time - laser.LastHitTime < 0.15f;
            Color crosshair = justHit ? new Color(1f, 0.85f, 0.2f) : Color.white;
            float size = justHit ? 12f : 8f;
            Fill(new Rect(cx - size, cy - 1f, size * 2f, 2f), crosshair);
            Fill(new Rect(cx - 1f, cy - size, 2f, size * 2f), crosshair);

            const float width = 260f, height = 14f;
            var bar = new Rect(cx - width * 0.5f, Screen.height - 60f, width, height);

            if (laser.IsCharging)
            {
                float charge = laser.Charge01;
                bool full = charge >= 1f;
                Fill(bar, new Color(0f, 0f, 0f, 0.6f));
                Fill(new Rect(bar.x, bar.y, bar.width * charge, bar.height),
                    full ? new Color(1f, 0.55f, 0.2f) : new Color(0.3f, 0.85f, 1f));
                string state = full ? "FULL CHARGE" : $"CHARGE {Mathf.RoundToInt(charge * 100f)}%";
                GUI.Label(new Rect(bar.x, bar.y - 26f, bar.width, 24f), state, label);
            }
            else if (laser.IsCoolingDown)
            {
                GUI.Label(new Rect(bar.x, bar.y - 26f, bar.width, 24f), "COOLING", label);
            }
            else if (laser.ComboStep > 0)
            {
                GUI.Label(new Rect(bar.x, bar.y - 26f, bar.width, 24f), $"SWING {laser.ComboStep} / {laser.ComboLength}", label);
            }

            GUI.Label(new Rect(16f, Screen.height - 40f, 600f, 24f),
                "Left click: swing (combo)    Hold right click: charge, release to fire", hint);

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
