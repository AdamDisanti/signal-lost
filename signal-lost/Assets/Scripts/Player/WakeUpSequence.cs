using System;
using System.Collections;
using SignalLost.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SignalLost.Player
{
    /// <summary>
    /// First-person "waking up" moment (meeting notes 2026-10-20): the eyes blink open slowly a few times while
    /// the view, lying on the ground looking up at the sky, rolls and lifts back to normal through a hangover blur.
    /// The full version opens the tutorial; a short one follows each death or blackout. Movement is paused while
    /// it plays. The eyelids are drawn under the dialogue canvas, so subtitles stay readable in the dark.
    /// </summary>
    [RequireComponent(typeof(FirstPersonController))]
    public sealed class WakeUpSequence : MonoBehaviour
    {
        [SerializeField] private Transform cameraPivot;
        [SerializeField, Min(1f)] private float introSeconds = 7f;
        [SerializeField, Min(0.5f)] private float respawnSeconds = 2.2f;
        [Tooltip("What the mechanic mumbles while waking up in the tutorial, shown as subtitles.")]
        [SerializeField] private string[] introLines =
        {
            "Ugh... what hit me?",
            "...oh. The ground. The ground hit me."
        };

        private FirstPersonController movement;
        private PlayerFlask player;
        private Canvas canvas;
        private RectTransform upperLid, lowerLid;
        private TMPro.TextMeshProUGUI hint;
        private string[] lines;

        public bool Playing { get; private set; }
        public event Action Finished;

        private void Awake()
        {
            movement = GetComponent<FirstPersonController>();
            player = GetComponent<PlayerFlask>();
            if (cameraPivot == null)
            {
                Camera view = GetComponentInChildren<Camera>();
                if (view != null) cameraPivot = view.transform;
            }
        }

        private void OnDestroy()
        {
            if (canvas != null) Destroy(canvas.gameObject);
        }

        /// <summary>Plays the long tutorial-opening version, or the short one used after a respawn.</summary>
        public void Play(bool intro) => Play(intro, null);

        /// <summary>The intro version with the mechanic mumbling <paramref name="mumble"/> instead of the default lines.</summary>
        public void Play(bool intro, string[] mumble)
        {
            lines = mumble ?? introLines;
            StopAllCoroutines();
            StartCoroutine(Run(intro));
        }

        private IEnumerator Run(bool intro)
        {
            Playing = true;
            BuildLids();
            canvas.enabled = true;
            movement.enabled = false;
            Cursor.visible = false;
            float duration = intro ? introSeconds : respawnSeconds;
            int spoken = 0;
            float started = Time.time;
            while (Time.time - started < duration)
            {
                float k = (Time.time - started) / duration;
                PlaceLids(intro ? IntroEyes(k) : RespawnEyes(k));

                // Lying on the back looking up, then rolling and lifting the head with a lazy wobble.
                float rise = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(intro ? 0.3f : 0.1f, 0.95f, k));
                float sway = Mathf.Sin(Time.time * 1.7f) * (1f - rise);
                float pitch = Mathf.Lerp(intro ? -70f : -35f, 0f, rise) + sway * 3f;
                float roll = Mathf.Lerp(intro ? 20f : 10f, 0f, rise) + sway * 2f;
                if (cameraPivot != null) cameraPivot.localRotation = Quaternion.Euler(pitch, sway * 4f, roll);
                if (player != null) player.ExtraDaze = Mathf.Lerp(intro ? 1f : 0.6f, 0f, Mathf.InverseLerp(0.2f, 1f, k));

                if (intro && spoken < lines.Length && k >= 0.18f + spoken * 0.3f)
                    DialogueUi.Instance.Subtitle("Mechanic", lines[spoken++], 2.6f);
                yield return null;
            }

            if (cameraPivot != null) cameraPivot.localRotation = Quaternion.identity;
            if (player != null) player.ExtraDaze = 0f;
            canvas.enabled = false;
            movement.ResetMotion();
            movement.enabled = true;
            Playing = false;
            Finished?.Invoke();
        }

        /// <summary>
        /// Holds the eyes at <paramref name="open"/> (0 shut, 1 wide open) for a scripted scene such as the tutorial's
        /// opening; <see cref="HideEyes"/> takes the eyelids away again.
        /// </summary>
        public void SetEyes(float open)
        {
            BuildLids();
            canvas.enabled = true;
            PlaceLids(open);
        }

        public void HideEyes()
        {
            if (canvas != null) canvas.enabled = false;
            SetHint(null);
        }

        /// <summary>A small hint in the corner of the eyelids (for example how to skip a scene). Null hides it.</summary>
        public void SetHint(string text)
        {
            if (string.IsNullOrEmpty(text) && hint == null) return;
            BuildLids();
            hint.text = text ?? "";
        }

        // Each lid is half a screen tall and slides off its edge as the eyes open.
        private void PlaceLids(float open)
        {
            float upper = Mathf.Lerp(0.5f, 1.02f, open), lower = Mathf.Lerp(0.5f, -0.02f, open);
            upperLid.anchorMin = new Vector2(0f, upper);
            upperLid.anchorMax = new Vector2(1f, upper + 0.55f);
            lowerLid.anchorMin = new Vector2(0f, lower - 0.55f);
            lowerLid.anchorMax = new Vector2(1f, lower);
        }

        // 0 = eyes shut, 1 = wide open: two heavy half-blinks before the eyes finally stay open.
        private static float IntroEyes(float k)
        {
            if (k < 0.1f) return 0f;
            if (k < 0.2f) return Mathf.SmoothStep(0f, 0.3f, (k - 0.1f) / 0.1f);
            if (k < 0.3f) return Mathf.SmoothStep(0.3f, 0f, (k - 0.2f) / 0.1f);
            if (k < 0.45f) return Mathf.SmoothStep(0f, 0.6f, (k - 0.3f) / 0.15f);
            if (k < 0.55f) return Mathf.SmoothStep(0.6f, 0.08f, (k - 0.45f) / 0.1f);
            if (k < 0.8f) return Mathf.SmoothStep(0.08f, 1f, (k - 0.55f) / 0.25f);
            return 1f;
        }

        private static float RespawnEyes(float k)
        {
            if (k < 0.15f) return 0f;
            if (k < 0.45f) return Mathf.SmoothStep(0f, 0.5f, (k - 0.15f) / 0.3f);
            if (k < 0.55f) return Mathf.SmoothStep(0.5f, 0.1f, (k - 0.45f) / 0.1f);
            if (k < 0.85f) return Mathf.SmoothStep(0.1f, 1f, (k - 0.55f) / 0.3f);
            return 1f;
        }

        private void BuildLids()
        {
            if (canvas != null) return;
            canvas = UiTheme.CreateCanvas("Wake-Up Eyelids", 45);
            canvas.GetComponent<GraphicRaycaster>().enabled = false;
            upperLid = Lid("Upper Lid", new Vector2(0f, 0.5f), Vector2.one);
            lowerLid = Lid("Lower Lid", Vector2.zero, new Vector2(1f, 0.5f));
            hint = UiTheme.Label("Hint", canvas.transform, 24f, new Color(1f, 1f, 1f, 0.55f), TMPro.TextAlignmentOptions.BottomRight, TMPro.FontStyles.Normal);
            UiTheme.Place(hint.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 30f), new Vector2(600f, 40f));
        }

        private RectTransform Lid(string name, Vector2 min, Vector2 max)
        {
            Image lid = UiTheme.Block(name, canvas.transform, Color.black);
            RectTransform rect = lid.rectTransform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }
    }
}
