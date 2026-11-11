using SignalLost.Core;
using SignalLost.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SignalLost.Melee
{
    /// <summary>
    /// The rage meter, in the game's UI style: ten gauss-blue pips just above the status bar that light up with each
    /// stack and flash when one is gained, the damage and speed bonus beside them, and a thin bar under them that runs
    /// down to when the stacks start draining. At full rage the pips pulse white-hot. Also shows the finisher prompt
    /// ("[V] FINISH") under the crosshair while the fists have a staggered enemy in their sights. Hidden behind full-screen
    /// menus and while there is no rage and nothing to finish.
    /// </summary>
    public sealed class RageHud : MonoBehaviour
    {
        public static readonly Color Gauss = new Color(0.4f, 0.9f, 1f);
        private static readonly Color Track = new Color(1f, 1f, 1f, 0.1f);

        private static RageHud instance;

        private RageMeter meter;
        private CanvasGroup group, promptGroup;
        private readonly Image[] pips = new Image[RageMeter.MaxStacks];
        private Image hold;
        private TextMeshProUGUI title, bonus, prompt;
        private float shown;

        /// <summary>The finisher prompt under the crosshair; set each frame by the fists (empty hides it).</summary>
        public static string FinisherPrompt { get; set; } = "";

        public static void Ensure(RageMeter meter)
        {
            if (instance == null) instance = UiTheme.CreateCanvas("Rage HUD", 31).gameObject.AddComponent<RageHud>();
            instance.meter = meter;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => FinisherPrompt = "";

        private void Awake() => Build();

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void Build()
        {
            var root = (RectTransform)transform;
            Image panel = UiTheme.Panel("Rage", root, UiTheme.PanelColor, new Color(Gauss.r, Gauss.g, Gauss.b, 0.9f), 0.8f);
            RectTransform p = UiTheme.Place(panel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, GameHud.BottomReserved + 6f), new Vector2(520f, 66f));
            group = panel.gameObject.AddComponent<CanvasGroup>();
            title = UiTheme.Label("Title", p, 22f, Gauss, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            UiTheme.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -4f), new Vector2(150f, 30f));
            bonus = UiTheme.Label("Bonus", p, 18f, UiTheme.Text, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
            UiTheme.Place(bonus.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-14f, -4f), new Vector2(300f, 30f));
            for (int i = 0; i < pips.Length; i++)
            {
                Image pip = UiTheme.Panel("Pip " + i, p, Track, null, 0.25f);
                UiTheme.Place(pip.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(14f + i * 49.5f, 14f), new Vector2(43f, 16f));
                pips[i] = pip;
            }
            Image holdBack = UiTheme.Panel("Hold", p, new Color(0f, 0f, 0f, 0.5f), null, 0.2f);
            RectTransform h = UiTheme.Place(holdBack.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 5f), new Vector2(492f, 5f));
            hold = UiTheme.Panel("Fill", h, Gauss, null, 0.2f);
            hold.rectTransform.anchorMin = Vector2.zero;
            hold.rectTransform.anchorMax = Vector2.one;
            hold.rectTransform.offsetMin = hold.rectTransform.offsetMax = Vector2.zero;

            prompt = UiTheme.Label("Finisher", root, 30f, Gauss, TextAlignmentOptions.Center, FontStyles.Bold);
            UiTheme.Place(prompt.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(600f, 44f));
            promptGroup = prompt.gameObject.AddComponent<CanvasGroup>();
        }

        private void Update()
        {
            bool menu = UiMenus.FullScreenOpen;
            int stacks = meter != null ? meter.Stacks : 0;
            shown = Mathf.MoveTowards(shown, meter != null && meter.isActiveAndEnabled && stacks > 0 && !menu ? 1f : 0f, Time.unscaledDeltaTime * 4f);
            group.alpha = shown;
            promptGroup.alpha = menu || string.IsNullOrEmpty(FinisherPrompt) ? 0f : 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 12f);
            prompt.text = FinisherPrompt;
            if (meter == null || shown <= 0f) return;

            float flash = Mathf.Clamp01(1f - (Time.time - meter.LastGain) / 0.35f);
            float pulse = meter.Maxed ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 9f) : 0f;
            Color lit = Color.Lerp(Gauss, Color.white, Mathf.Max(flash * 0.8f, pulse * 0.7f));
            for (int i = 0; i < pips.Length; i++)
            {
                bool on = i < stacks;
                pips[i].color = on ? lit : Track;
                pips[i].rectTransform.localScale = on && i == stacks - 1 ? Vector3.one * (1f + 0.35f * flash) : Vector3.one;
            }
            title.text = meter.Maxed ? "RAGE  MAX" : $"RAGE  x{stacks}";
            title.color = meter.Maxed ? Color.Lerp(Gauss, UiTheme.Accent, pulse) : Gauss;
            bonus.text = $"+{(meter.DamageMultiplier - 1f) * 100f:0}% damage   +{(meter.SpeedMultiplier - 1f) * 100f:0}% speed";
            hold.rectTransform.anchorMax = new Vector2(meter.HoldLeft, 1f);
            hold.color = meter.HoldLeft > 0.25f ? Gauss : UiTheme.Warning;
        }

        /// <summary>The finisher prompt text for the bound key.</summary>
        public static string FinishText(string target) => $"{KeyBindings.Hint("finisher")}  FINISH {target.ToUpperInvariant()}";
    }
}
