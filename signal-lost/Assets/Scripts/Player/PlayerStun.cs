using SignalLost.Effects;
using SignalLost.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SignalLost.Player
{
    /// <summary>
    /// A stun on the player (the Chief Toad's scream). For a few seconds they can't walk, jump, use their tools or
    /// drink, though they can still look around and gravity still works. The screen rings with pale shock waves
    /// under a STUNNED banner, and the view shakes. Added to the player the first time it is needed.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FirstPersonController))]
    public sealed class PlayerStun : MonoBehaviour
    {
        private FirstPersonController movement;
        private PlayerGear gear;
        private PlayerFlask flask;
        private float until, length;
        private bool active;
        private Canvas canvas;
        private Image wash;
        private Image[] waves;
        private TextMeshProUGUI banner;

        /// <summary>True while this player is stunned.</summary>
        public bool Active => active;

        /// <summary>Stuns the player that <paramref name="player"/> belongs to for <paramref name="seconds"/>.</summary>
        public static void Apply(Component player, float seconds)
        {
            if (player == null || seconds <= 0f) return;
            var controller = player.GetComponentInParent<FirstPersonController>();
            if (controller == null) return;
            PlayerStun stun = controller.GetComponent<PlayerStun>();
            if (stun == null) stun = controller.gameObject.AddComponent<PlayerStun>();
            stun.Begin(seconds);
        }

        private void Awake()
        {
            movement = GetComponent<FirstPersonController>();
            gear = GetComponent<PlayerGear>();
            flask = GetComponent<PlayerFlask>();
        }

        private void Begin(float seconds)
        {
            until = Mathf.Max(until, Time.time + seconds);
            length = until - Time.time;
            movement.Stun(seconds);
            if (!active)
            {
                active = true;
                if (gear != null) gear.Holster(this);
                if (flask != null) flask.enabled = false;
            }
            CameraShake.Add(0.45f);
            EnsureOverlay();
            canvas.enabled = true;
        }

        private void Update()
        {
            if (!active) return;
            float left = until - Time.time;
            if (left <= 0f)
            {
                End();
                return;
            }
            float fade = Mathf.Clamp01(left / 0.35f); // clears over the last moment
            wash.color = new Color(0.75f, 0.9f, 1f, 0.2f * fade * (0.8f + 0.2f * Mathf.Sin(Time.time * 18f)));
            for (int i = 0; i < waves.Length; i++)
            {
                float k = Mathf.Repeat(Time.time * 1.6f + i / (float)waves.Length, 1f);
                waves[i].rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(200f, 2600f, k);
                waves[i].color = new Color(0.85f, 0.95f, 1f, 0.3f * (1f - k) * fade);
            }
            banner.alpha = fade;
            banner.rectTransform.localScale = Vector3.one * (1f + 0.04f * Mathf.Sin(Time.time * 25f));
            CameraShake.Add(Time.deltaTime * 0.35f * fade);
        }

        private void OnDisable()
        {
            if (active) End();
        }

        private void OnDestroy()
        {
            if (canvas != null) Destroy(canvas.gameObject);
        }

        private void End()
        {
            active = false;
            if (gear != null) gear.Unholster(this);
            if (flask != null) flask.enabled = true;
            if (canvas != null) canvas.enabled = false;
        }

        // Drawn above the HUD and quest overlay, below the wake-up eyelids and dialogue.
        private void EnsureOverlay()
        {
            if (canvas != null) return;
            canvas = UiTheme.CreateCanvas("Stun Overlay", 44);
            canvas.GetComponent<GraphicRaycaster>().enabled = false;
            Transform root = canvas.transform;
            wash = UiTheme.Block("Wash", root, Color.clear);
            UiTheme.Stretch(wash.rectTransform);
            waves = new Image[3];
            for (int i = 0; i < waves.Length; i++)
            {
                waves[i] = UiTheme.Circle("Shock Wave " + (i + 1), root, Color.clear, true);
                UiTheme.Place(waves[i].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * 200f);
            }
            banner = UiTheme.Label("Stunned", root, 64f, new Color(0.9f, 0.97f, 1f), TextAlignmentOptions.Center, FontStyles.Bold);
            UiTheme.Place(banner.rectTransform, new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 90f));
            banner.text = "STUNNED";
        }
    }
}
