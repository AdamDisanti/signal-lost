using SignalLost.Core;
using SignalLost.Player;
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
    /// ("[V] FINISH") under the crosshair while the fists have a staggered enemy in their sights, a hit flash on the
    /// crosshair when a punch lands, and, while the fists are up, its own panel over the status bar's hand section (the
    /// game HUD only knows the laser and scanner; the melee update is kept to its two scenes). Hidden behind full-screen
    /// menus.
    /// </summary>
    public sealed class RageHud : MonoBehaviour
    {
        public static readonly Color Gauss = new Color(0.4f, 0.9f, 1f);
        private static readonly Color Track = new Color(1f, 1f, 1f, 0.1f);

        private static RageHud instance;

        private RageMeter meter;
        private PlayerGear gear;
        private CanvasGroup group, promptGroup, handsGroup;
        private TextMeshProUGUI handsTitle, handsInfo;
        private readonly Image[] comboPips = new Image[3];
        private Image pullFill, hitFlash;
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
            instance.gear = meter.GetComponent<PlayerGear>();
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

            // The fists' panel, laid exactly over the status bar's hand section (GameHud: bar 1520 wide, 12 up; hands at 840).
            Image hands = UiTheme.Panel("Fists", root, new Color(0.05f, 0.07f, 0.15f, 1f), null, 0.6f);
            RectTransform fistsRect = UiTheme.Place(hands.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(-760f + 832f, 18f), new Vector2(346f, 122f));
            handsGroup = hands.gameObject.AddComponent<CanvasGroup>();
            handsGroup.blocksRaycasts = false;
            handsTitle = UiTheme.Label("Title", fistsRect, 22f, Gauss, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UiTheme.Place(handsTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, -4f), new Vector2(334f, 30f));
            for (int i = 0; i < comboPips.Length; i++)
            {
                comboPips[i] = UiTheme.Panel("Combo Pip", fistsRect, Track, null, 0.35f);
                UiTheme.Place(comboPips[i].rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f + i * 30f, -44f), new Vector2(22f, 22f));
            }
            Image pullBack = UiTheme.Panel("Pull", fistsRect, new Color(0f, 0f, 0f, 0.5f), null, 0.3f);
            RectTransform pb = UiTheme.Place(pullBack.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(108f, -50f), new Vector2(226f, 10f));
            pullFill = UiTheme.Panel("Fill", pb, Gauss, null, 0.3f);
            pullFill.rectTransform.anchorMin = Vector2.zero;
            pullFill.rectTransform.anchorMax = Vector2.one;
            pullFill.rectTransform.offsetMin = pullFill.rectTransform.offsetMax = Vector2.zero;
            handsInfo = UiTheme.Label("Info", fistsRect, 20f, UiTheme.Text, TextAlignmentOptions.BottomLeft);
            UiTheme.Place(handsInfo.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(8f, 14f), new Vector2(334f, 30f));
            handsInfo.textWrappingMode = TextWrappingModes.NoWrap;

            hitFlash = UiTheme.Circle("Hit", root, new Color(1f, 0.85f, 0.2f, 0f), true);
            UiTheme.Place(hitFlash.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(46f, 46f));
            hitFlash.raycastTarget = false;

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
            UpdateFists(menu);
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

        // The fists' panel and hit flash: what the knuckles can do now, the magnet pull's recharge, and the combo.
        private void UpdateFists(bool menu)
        {
            PlayerFists fists = gear != null && gear.InHand == PlayerGear.Held.Fists ? gear.Fists : null;
            handsGroup.alpha = fists != null && !menu && !UiMenus.AnyOpen ? 1f : 0f;
            float hit = fists != null ? Mathf.Clamp01(1f - (Time.time - fists.LastHitTime) / 0.15f) : 0f;
            hitFlash.color = new Color(1f, 0.85f, 0.2f, 0.75f * hit);
            hitFlash.rectTransform.localScale = Vector3.one * (0.8f + 0.5f * hit);
            if (fists == null) return;
            int tier = fists.KnuckleTier;
            handsTitle.text = (tier > 0 ? "GAUSS KNUCKLES " + MeleeLoadout.Numeral(tier) : "FISTS") +
                              $"  <size=80%><color=#A9B4CC>{KeyBindings.Hint("swap")} swap</color></size>";
            float pull = fists.PullCooldown > 0f ? 1f - fists.PullCooldownRemaining / fists.PullCooldown : 1f;
            pullFill.rectTransform.anchorMax = new Vector2(tier >= 2 ? Mathf.Clamp01(pull) : 0f, 1f);
            pullFill.color = fists.PullCooldownRemaining > 0f ? new Color(0.45f, 0.55f, 0.7f) : Gauss;
            handsInfo.text = tier == 0 ? "Punch combo" : tier == 1 ? "Punch to lunge at enemies" :
                (fists.PullCooldownRemaining > 0f ? $"Pull recharging {fists.PullCooldownRemaining:0.0}s" : $"{KeyBindings.Hint("charge")} pull ready") +
                (tier >= 3 ? $"   {KeyBindings.Hint("finisher")} finish" : "");
            for (int i = 0; i < comboPips.Length; i++) comboPips[i].color = i < fists.ComboStep ? Gauss : Track;
        }

        /// <summary>The finisher prompt text for the bound key.</summary>
        public static string FinishText(string target) => $"{KeyBindings.Hint("finisher")}  FINISH {target.ToUpperInvariant()}";
    }
}
