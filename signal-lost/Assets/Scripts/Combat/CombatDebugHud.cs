using UnityEngine;

namespace SignalLost.Combat
{
    /// <summary>
    /// Temporary on-screen readout for testing combat: crosshair with hit marker, combo counter,
    /// charge bar, controls hint, and the health of whatever is under the crosshair. With Show Player Health
    /// on (and a Health on the player) it also shows the player's health and flashes red when they are hurt.
    /// It is off by default so scenes with their own health UI, like the tutorial, don't show two bars.
    /// Placeholder only; the real HUD will replace it.
    /// </summary>
    public sealed class CombatDebugHud : MonoBehaviour
    {
        [SerializeField] private MiningLaser laser;
        [Tooltip("Show the player's health bar, a red flash when hurt, and a respawn message. For test scenes without a real HUD.")]
        [SerializeField] private bool showPlayerHealth;

        private GUIStyle label, hint, corner;
        private Health playerHealth;
        private float hurtFlash; // 1 when just hit, fades to 0

        private void Awake()
        {
            if (laser == null) laser = GetComponentInChildren<MiningLaser>();
            if (showPlayerHealth) playerHealth = GetComponent<Health>();
        }

        private void OnEnable()
        {
            if (playerHealth != null) playerHealth.Damaged += OnPlayerDamaged;
        }

        private void OnDisable()
        {
            if (playerHealth != null) playerHealth.Damaged -= OnPlayerDamaged;
        }

        private void OnPlayerDamaged(Health _, float amount, GameObject source) => hurtFlash = 1f;

        private void Update() => hurtFlash = Mathf.MoveTowards(hurtFlash, 0f, Time.deltaTime / 0.35f);

        private void OnGUI()
        {
            if (laser == null) return;
            if (label == null)
            {
                label = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
                hint = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.LowerLeft };
                corner = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleLeft };
            }

            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;

            if (playerHealth != null)
            {
                // Red flash over the whole screen when hurt, then the player's health bar in the top left.
                if (hurtFlash > 0f) Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.8f, 0f, 0f, 0.35f * hurtFlash));

                var hpBar = new Rect(20f, 20f, 220f, 14f);
                Fill(hpBar, new Color(0f, 0f, 0f, 0.6f));
                Fill(new Rect(hpBar.x, hpBar.y, hpBar.width * playerHealth.Normalized, hpBar.height), new Color(0.35f, 0.9f, 0.35f));
                GUI.Label(new Rect(hpBar.x, hpBar.yMax + 2f, 300f, 24f),
                    $"HP {Mathf.CeilToInt(playerHealth.Current)} / {Mathf.CeilToInt(playerHealth.Max)}", corner);

                if (playerHealth.IsDead)
                {
                    GUI.Label(new Rect(cx - 200f, cy - 15f, 400f, 30f), "PINCHED! Respawning...", label);
                    return;
                }
            }

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
                // Long cooldowns (the tutorial's 10 s) need a countdown; the old half-second one just says COOLING.
                string cooling = laser.ShotCooldown > 2f ? $"BOLT COOLING  {Mathf.CeilToInt(laser.ShotCooldownRemaining)}s" : "COOLING";
                GUI.Label(new Rect(bar.x, bar.y - 26f, bar.width, 24f), cooling, label);
            }
            else if (laser.ComboStep > 0)
            {
                GUI.Label(new Rect(bar.x, bar.y - 26f, bar.width, 24f), $"SWING {laser.ComboStep} / {laser.ComboLength}", label);
            }
            if (laser.ComboCooldownRemaining > 0f)
                GUI.Label(new Rect(bar.x, bar.y - 50f, bar.width, 24f), $"COMBO RECHARGING  {laser.ComboCooldownRemaining:0.0}s", label);

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
