using SignalLost.Progression;
using TMPro;
using UnityEngine;

namespace SignalLost.UI
{
    /// <summary>
    /// A small "Autosaved" note in the bottom-right corner that fades after each autosave (10-24). Manual saves show
    /// their own message in the pause menu. Created by <see cref="SignalLost.Core.GameSystems"/> in every playable scene.
    /// </summary>
    public sealed class SaveIndicator : MonoBehaviour
    {
        private const float ShowSeconds = 2f;

        private static SaveIndicator instance;
        private TextMeshProUGUI label;
        private float until = float.NegativeInfinity;

        public static SaveIndicator Instance
        {
            get
            {
                if (instance == null && Application.isPlaying)
                    instance = UiTheme.CreateCanvas("Save Indicator", 65).gameObject.AddComponent<SaveIndicator>();
                return instance;
            }
        }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            label = UiTheme.Label("Autosaved", transform, 24f, UiTheme.Text, TextAlignmentOptions.BottomRight, FontStyles.Italic);
            UiTheme.Place(label.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-28f, 22f), new Vector2(400f, 34f));
            label.text = "Autosaved";
            label.alpha = 0f;
        }

        private void OnEnable() => SaveGame.Saved += OnSaved;
        private void OnDisable() => SaveGame.Saved -= OnSaved;

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void OnSaved(int slot, bool autosave)
        {
            if (!autosave) return;
            label.text = $"Autosaved  (slot {slot})";
            until = Time.unscaledTime + ShowSeconds;
        }

        private void Update()
        {
            if (label == null) return;
            // Fades in quickly, holds, then fades out over the last half second.
            float left = until - Time.unscaledTime;
            label.alpha = left <= 0f ? 0f : Mathf.Min(1f, left / 0.5f, (ShowSeconds - left) / 0.15f + 0.2f);
        }
    }
}
