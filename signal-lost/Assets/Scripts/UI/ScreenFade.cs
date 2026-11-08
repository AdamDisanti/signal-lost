using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SignalLost.UI
{
    /// <summary>
    /// A full-screen black fade with an optional caption, kept across scene loads so a cutscene can fade out in one
    /// scene and back in the next (the island voyage, 11-07), or so the deep-water rescue can black out briefly. It sits
    /// just above the HUD's own death fade. Created on first use.
    /// </summary>
    public sealed class ScreenFade : MonoBehaviour
    {
        private static ScreenFade instance;
        private Image black;
        private TextMeshProUGUI title, subtitle;

        public static ScreenFade Instance
        {
            get
            {
                if (instance == null) Create();
                return instance;
            }
        }

        /// <summary>How dark the screen is now, 0 to 1.</summary>
        public float Alpha => black.color.a;

        private static void Create()
        {
            Canvas canvas = UiTheme.CreateCanvas("Screen Fade (cutscenes)", 82);
            canvas.GetComponent<GraphicRaycaster>().enabled = false;
            DontDestroyOnLoad(canvas.gameObject);
            instance = canvas.gameObject.AddComponent<ScreenFade>();
            instance.black = UiTheme.Block("Black", canvas.transform, Color.clear);
            instance.black.raycastTarget = false;
            UiTheme.Stretch(instance.black.rectTransform);
            instance.title = UiTheme.Label("Title", instance.black.transform, 54f, UiTheme.Text, TextAlignmentOptions.Center, FontStyles.Bold);
            UiTheme.Place(instance.title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 24f), new Vector2(1400f, 72f));
            instance.subtitle = UiTheme.Label("Subtitle", instance.black.transform, 28f, UiTheme.Muted, TextAlignmentOptions.Center);
            UiTheme.Place(instance.subtitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -36f), new Vector2(1200f, 40f));
            instance.SetCaption("");
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        /// <summary>Text shown while the screen is dark (it fades with the black). Empty clears it.</summary>
        public void SetCaption(string heading, string sub = "")
        {
            title.text = heading ?? "";
            subtitle.text = sub ?? "";
            Apply(Alpha);
        }

        public void Set(float alpha) => Apply(Mathf.Clamp01(alpha));

        /// <summary>Fades to <paramref name="alpha"/> over <paramref name="seconds"/> of real time (pauses don't stop it).</summary>
        public IEnumerator FadeTo(float alpha, float seconds)
        {
            float from = Alpha;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                Apply(Mathf.Lerp(from, alpha, Mathf.SmoothStep(0f, 1f, t / seconds)));
                yield return null;
            }
            Apply(alpha);
        }

        private void Apply(float alpha)
        {
            black.color = new Color(0f, 0f, 0f, alpha);
            // The caption only shows once the screen is mostly dark, so it never floats over the game.
            float text = Mathf.InverseLerp(0.6f, 1f, alpha);
            title.alpha = subtitle.alpha = text;
        }
    }
}
