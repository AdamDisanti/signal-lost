using System.Collections.Generic;
using System.Text;
using SignalLost.Combat;
using SignalLost.Inventory;
using SignalLost.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SignalLost.UI
{
    /// <summary>
    /// The in-game HUD for every scene (meeting notes 10-22). A Signal Lost status bar runs along the bottom, Doom
    /// style: health, the flask and how drunk you are, the mechanic's reacting face, what is in your hands (bolt
    /// cooldown, combo) and your sorted scrap. Around the crosshair: hit marker, charge meter, the target's health and
    /// the E prompt. Full-screen layers handle the hurt flash and the death/blackout fade.
    /// It finds the player's components (<see cref="PlayerLife"/>, <see cref="PlayerGear"/>, <see cref="PlayerFlask"/>,
    /// <see cref="PlayerInteractor"/>) by itself, so it needs no wiring. Created by <see cref="GameSystems"/>.
    /// </summary>
    public sealed class GameHud : MonoBehaviour
    {
        private const float BarHeight = 132f;
        /// <summary>Height the status bar takes at the bottom of the screen; other UI sits above it.</summary>
        public const float BottomReserved = BarHeight + 24f;

        private static GameHud instance;

        private PlayerLife life;
        private PlayerGear gear;
        private PlayerFlask flask;
        private PlayerInteractor interactor;
        private Health health;
        private float trail = 1f, hurtFlash, nextFind, shownHealth = -1f;

        private CanvasGroup barGroup, centreGroup;
        private TextMeshProUGUI healthValue, flaskTitle, flaskInfo, handTitle, handInfo, prompt, targetName, chargeText, centreMessage, centreSub;
        private Image healthFill, healthTrail, chargeFill, cooldownFill, targetFill, crosshairH, crosshairV, hurtImage, fade;
        private RectTransform crosshair, chargeGroup, targetGroup;
        private readonly List<Image> drunkPips = new List<Image>();
        private readonly List<Image> comboPips = new List<Image>();
        private readonly List<Image> drunkFills = new List<Image>();
        private readonly List<Image> comboFills = new List<Image>();
        private static readonly Color PipTrack = new Color(1f, 1f, 1f, 0.12f);
        private readonly TextMeshProUGUI[] scrapCounts = new TextMeshProUGUI[4];
        private TextMeshProUGUI scrapToast;
        private readonly Dictionary<ScrapType, int> recentScrap = new Dictionary<ScrapType, int>();
        private float toastUntil;
        private PlayerInventory boundInventory;
        private MechanicPortrait portrait;
        private string respawnMessage;
        private float fadeTarget;
        private Canvas fadeCanvas;

        /// <summary>The existing instance, or null. Use this in OnDisable/OnDestroy so nothing is created during unload.</summary>
        public static GameHud Current => instance;

        public static GameHud Instance
        {
            get
            {
                if (instance == null && Application.isPlaying)
                    instance = UiTheme.CreateCanvas("Game HUD", 30).gameObject.AddComponent<GameHud>();
                return instance;
            }
        }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            Build();
        }

        private void OnDestroy()
        {
            Unbind();
            if (fadeCanvas != null) Destroy(fadeCanvas.gameObject);
            if (instance == this) instance = null;
        }

        // ---------- Binding to the player ----------

        private void Bind()
        {
            if (life != null || Time.unscaledTime < nextFind) return;
            nextFind = Time.unscaledTime + 0.5f;
            life = FindAnyObjectByType<PlayerLife>();
            if (life == null) return;
            gear = life.GetComponent<PlayerGear>();
            flask = life.GetComponent<PlayerFlask>();
            interactor = life.GetComponent<PlayerInteractor>();
            health = life.Health;
            health.Damaged += OnHurt;
            life.RespawnStarted += OnRespawnStarted;
            life.Respawned += OnRespawned;
            boundInventory = gear != null ? gear.Inventory : null;
            if (boundInventory != null) boundInventory.ScrapChanged += OnScrapChanged;
        }

        private void Unbind()
        {
            if (health != null) health.Damaged -= OnHurt;
            if (life != null) { life.RespawnStarted -= OnRespawnStarted; life.Respawned -= OnRespawned; }
            if (boundInventory != null) boundInventory.ScrapChanged -= OnScrapChanged;
        }

        private void OnHurt(Health _, float amount, GameObject source)
        {
            hurtFlash = 1f;
            portrait.Hurt();
        }

        private void OnRespawnStarted(string message)
        {
            respawnMessage = message;
            fadeTarget = 1f;
        }

        private void OnRespawned()
        {
            fadeTarget = 0f;
            respawnMessage = null;
        }

        private void OnScrapChanged(ScrapType type, int change, int total)
        {
            if (change <= 0) return;
            if (Time.unscaledTime > toastUntil) recentScrap.Clear();
            recentScrap.TryGetValue(type, out int sum);
            recentScrap[type] = sum + change;
            var text = new StringBuilder();
            foreach (ScrapType kind in ScrapInfo.All)
                if (recentScrap.TryGetValue(kind, out int count))
                    text.Append($"<color={UiTheme.Hex(ScrapInfo.Color(kind))}>+{count} {ScrapInfo.Name(kind)}</color>  ");
            scrapToast.text = text.ToString();
            toastUntil = Time.unscaledTime + 2.2f;
        }

        // ---------- Every frame ----------

        private void Update()
        {
            Bind();
            bool hasPlayer = life != null;
            bool fullScreenMenu = UiMenus.FullScreenOpen;
            barGroup.alpha = Mathf.MoveTowards(barGroup.alpha, hasPlayer && !fullScreenMenu ? 1f : 0f, Time.unscaledDeltaTime * 6f);
            centreGroup.alpha = hasPlayer && !UiMenus.AnyOpen && Cursor.lockState == CursorLockMode.Locked ? 1f : 0f;
            hurtFlash = Mathf.MoveTowards(hurtFlash, 0f, Time.unscaledDeltaTime / 0.35f);
            hurtImage.color = new Color(0.75f, 0f, 0f, 0.32f * hurtFlash);
            fade.color = new Color(0f, 0f, 0f, Mathf.MoveTowards(fade.color.a, fadeTarget, Time.unscaledDeltaTime / 0.45f));
            centreMessage.text = respawnMessage ?? "";
            centreSub.text = respawnMessage != null ? "Coming round..." : "";
            if (!hasPlayer) return;

            UpdateHealth();
            UpdateFlask();
            UpdateHands();
            UpdateScrap();
            UpdateCrosshair();
            portrait.Set(health.Normalized, flask != null ? flask.Drunkenness : 0f, life.IsDead || life.IsRespawning);
        }

        private void UpdateHealth()
        {
            float now = health.Normalized;
            if (shownHealth < 0f) trail = now;
            shownHealth = now;
            trail = now > trail ? now : Mathf.MoveTowards(trail, now, Time.unscaledDeltaTime * 0.6f);
            healthFill.rectTransform.anchorMax = new Vector2(now, 1f);
            healthTrail.rectTransform.anchorMax = new Vector2(trail, 1f);
            bool low = now < 0.25f;
            healthFill.color = low ? Color.Lerp(UiTheme.Warning, new Color(1f, 0.6f, 0.5f), Mathf.PingPong(Time.unscaledTime * 3f, 1f)) : UiTheme.Good;
            healthValue.text = $"{Mathf.CeilToInt(health.Current)}<size=55%><color={UiTheme.Hex(UiTheme.Muted)}> / {Mathf.CeilToInt(health.Max)}</color></size>";
        }

        private void UpdateFlask()
        {
            bool owned = gear != null && gear.Has(PlayerItem.Flask) && flask != null;
            flaskTitle.text = owned ? "SCOTCH  <size=80%>[F]</size>" : "SCOTCH";
            if (!owned)
            {
                flaskInfo.text = $"<color={UiTheme.Hex(UiTheme.Muted)}>No flask</color>";
                foreach (Image pip in drunkPips) pip.color = new Color(1f, 1f, 1f, 0.08f);
                foreach (Image fill in drunkFills) SetFill(fill, 0f, true);
                return;
            }
            flaskInfo.text = flask.DrunkStacks == 0 ? $"Sober  <color={UiTheme.Hex(UiTheme.Good)}>next sip +{flask.NextSipHeal:0} HP</color>" :
                $"Drunk {flask.DrunkStacks}/{flask.BlackoutAfterStacks}  next sip +{flask.NextSipHeal:0.#} HP";
            // Lit pips are drinks in you; the top one drains as it wears off (a visual timer for the next sip).
            bool danger = flask.DrunkStacks >= flask.BlackoutAfterStacks;
            Color drunkColor = danger ? UiTheme.Warning : new Color(1f, 0.72f, 0.3f);
            for (int i = 0; i < drunkPips.Count; i++)
            {
                drunkPips[i].color = i < flask.BlackoutAfterStacks ? PipTrack : new Color(1f, 1f, 1f, 0.04f);
                float fill = i < flask.DrunkStacks - 1 ? 1f : i == flask.DrunkStacks - 1 ? flask.StackWearing : 0f;
                drunkFills[i].color = drunkColor;
                SetFill(drunkFills[i], fill, true);
            }
        }

        private void UpdateHands()
        {
            MiningLaser laser = gear != null ? gear.Laser : null;
            PlayerGear.Held held = gear != null ? gear.InHand : PlayerGear.Held.Nothing;
            handTitle.text = held == PlayerGear.Held.Laser ? "MINING LASER" : held == PlayerGear.Held.Scanner ? "SCANNER" : "EMPTY HANDS";
            bool canSwap = gear != null && gear.Has(PlayerItem.Scanner) && gear.Has(PlayerItem.MiningLaser);
            if (canSwap) handTitle.text += "  <size=80%><color=#A9B4CC>[Q] swap</color></size>";

            if (laser == null || !gear.Has(PlayerItem.MiningLaser))
            {
                handInfo.text = gear != null && gear.Has(PlayerItem.Scanner) ? "Follow the arrow on the scanner" : "Find your gear in the wreck";
                cooldownFill.rectTransform.anchorMax = new Vector2(0f, 1f);
                foreach (Image pip in comboPips) pip.color = new Color(1f, 1f, 1f, 0.08f);
                foreach (Image fill in comboFills) SetFill(fill, 0f, false);
                return;
            }
            float cooldown = laser.CurrentCooldownLength > 0f ? 1f - laser.ShotCooldownRemaining / laser.CurrentCooldownLength : 1f;
            cooldownFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(cooldown), 1f);
            cooldownFill.color = laser.IsCoolingDown ? new Color(0.45f, 0.55f, 0.7f) : new Color(0.4f, 0.9f, 1f);
            handInfo.text = laser.IsCoolingDown ? $"Bolt cooling  {laser.ShotCooldownRemaining:0.0}s" :
                laser.ComboCooldownRemaining > 0f ? $"Bolt ready   Combo recharging {laser.ComboCooldownRemaining:0.0}s" : "Bolt ready   Combo ready";
            // Pips count the combo; after the stunning third hit all three drain right to left as its recharge timer.
            bool recharging = laser.ComboCooldownRemaining > 0f;
            float left = recharging && laser.ComboCooldown > 0f ? laser.ComboCooldownRemaining / laser.ComboCooldown : 0f;
            for (int i = 0; i < comboPips.Count; i++)
            {
                comboPips[i].color = recharging ? PipTrack : new Color(1f, 1f, 1f, 0.3f);
                comboFills[i].color = recharging ? new Color(1f, 0.55f, 0.25f) : UiTheme.Accent;
                SetFill(comboFills[i], recharging ? Mathf.Clamp01(left * comboPips.Count - i) : i < laser.ComboStep ? 1f : 0f, false);
            }
        }

        // A coloured bar inside a pip; SetFill grows it upward (vertical) or rightward.
        private static Image Fill(Image pip)
        {
            Image fill = UiTheme.Panel("Fill", pip.transform, Color.white, null, 0.35f);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            return fill;
        }

        private static void SetFill(Image fill, float amount, bool vertical)
        {
            amount = Mathf.Clamp01(amount);
            fill.enabled = amount > 0.01f;
            fill.rectTransform.anchorMax = vertical ? new Vector2(1f, amount) : new Vector2(amount, 1f);
        }

        private void UpdateScrap()
        {
            PlayerInventory inventory = gear != null ? gear.Inventory : null;
            for (int i = 0; i < scrapCounts.Length; i++) scrapCounts[i].text = inventory != null ? inventory.Scrap(ScrapInfo.All[i]).ToString() : "0";
            scrapToast.alpha = UiMenus.FullScreenOpen ? 0f : Mathf.Clamp01((toastUntil - Time.unscaledTime) / 0.4f);
        }

        private void UpdateCrosshair()
        {
            MiningLaser laser = gear != null && gear.InHand == PlayerGear.Held.Laser ? gear.Laser : null;
            bool justHit = laser != null && Time.time - laser.LastHitTime < 0.15f;
            Color cross = justHit ? new Color(1f, 0.85f, 0.2f) : new Color(1f, 1f, 1f, 0.85f);
            crosshairH.color = crosshairV.color = cross;
            crosshair.localScale = Vector3.one * (justHit ? 1.5f : 1f);

            bool charging = laser != null && laser.IsCharging;
            chargeGroup.gameObject.SetActive(charging);
            if (charging)
            {
                float charge = laser.Charge01;
                chargeFill.rectTransform.anchorMax = new Vector2(charge, 1f);
                chargeFill.color = charge >= 1f ? UiTheme.Accent : new Color(0.4f, 0.9f, 1f);
                chargeText.text = charge >= 1f ? "FULL CHARGE  -  BLAST" : $"CHARGE {Mathf.RoundToInt(charge * 100f)}%";
            }

            Health target = laser != null ? laser.CurrentTarget : null;
            targetGroup.gameObject.SetActive(target != null && !target.Invulnerable);
            if (target != null)
            {
                targetName.text = target.name.Replace("(Clone)", "").Trim();
                targetFill.rectTransform.anchorMax = new Vector2(target.Normalized, 1f);
            }
            prompt.text = interactor != null ? interactor.Prompt : "";
        }

        // ---------- Layout ----------

        private void Build()
        {
            var root = (RectTransform)transform;

            hurtImage = UiTheme.Block("Hurt Flash", root, Color.clear);
            UiTheme.Stretch(hurtImage.rectTransform);

            // Crosshair, charge meter, target readout and prompt.
            RectTransform centre = UiTheme.Stretch(UiTheme.Rect("Centre", root));
            centreGroup = centre.gameObject.AddComponent<CanvasGroup>();
            centreGroup.blocksRaycasts = false;
            crosshair = UiTheme.Place(UiTheme.Rect("Crosshair", centre), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24f, 24f));
            crosshairH = UiTheme.Block("H", crosshair, Color.white);
            UiTheme.Place(crosshairH.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22f, 3f));
            crosshairV = UiTheme.Block("V", crosshair, Color.white);
            UiTheme.Place(crosshairV.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(3f, 22f));

            chargeGroup = UiTheme.Place(UiTheme.Rect("Charge", centre), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(300f, 50f));
            chargeText = UiTheme.Label("Label", chargeGroup, 24f, UiTheme.Text, TextAlignmentOptions.Center, FontStyles.Bold);
            UiTheme.Place(chargeText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(300f, 28f));
            chargeFill = Meter(chargeGroup, new Vector2(0f, -32f), new Vector2(300f, 14f));

            targetGroup = UiTheme.Place(UiTheme.Rect("Target", centre), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(320f, 50f));
            targetName = UiTheme.Label("Name", targetGroup, 24f, UiTheme.Text, TextAlignmentOptions.Center, FontStyles.Bold);
            UiTheme.Place(targetName.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(320f, 28f));
            targetFill = Meter(targetGroup, new Vector2(0f, -32f), new Vector2(240f, 10f));
            targetFill.color = UiTheme.Warning;

            prompt = UiTheme.Label("Prompt", centre, 30f, UiTheme.Text, TextAlignmentOptions.Center, FontStyles.Bold);
            UiTheme.Place(prompt.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(900f, 40f));

            // Status bar.
            Image bar = UiTheme.Panel("Status Bar", root, UiTheme.PanelColor, UiTheme.Edge, 1.2f);
            RectTransform barRect = UiTheme.Place(bar.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(1520f, BarHeight));
            barGroup = barRect.gameObject.AddComponent<CanvasGroup>();
            barGroup.blocksRaycasts = false;
            barGroup.alpha = 0f;

            RectTransform healthBlock = Block(barRect, "Health", 24f, 360f, "HEALTH");
            healthValue = UiTheme.Label("Value", healthBlock, 48f, UiTheme.Text, TextAlignmentOptions.Left, FontStyles.Bold);
            UiTheme.Place(healthValue.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -30f), new Vector2(360f, 52f));
            RectTransform meter = UiTheme.Place(UiTheme.Rect("Meter", healthBlock), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 14f), new Vector2(360f, 20f));
            UiTheme.Stretch(UiTheme.Panel("Back", meter, new Color(0f, 0f, 0f, 0.5f), null, 0.4f).rectTransform);
            healthTrail = FillBar(meter, new Color(1f, 0.95f, 0.8f, 0.55f));
            healthFill = FillBar(meter, UiTheme.Good);

            RectTransform flaskBlock = Block(barRect, "Flask", 404f, 270f, "SCOTCH");
            flaskTitle = flaskBlock.Find("Title").GetComponent<TextMeshProUGUI>();
            for (int i = 0; i < 6; i++) // five drinks, a sixth with the Cast-Iron Liver upgrade
            {
                Image pip = UiTheme.Panel("Drunk Pip", flaskBlock, Color.white, null, 0.35f);
                UiTheme.Place(pip.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(i * 34f, -40f), new Vector2(26f, 34f));
                drunkPips.Add(pip);
                drunkFills.Add(Fill(pip));
            }
            flaskInfo = UiTheme.Label("Info", flaskBlock, 21f, UiTheme.Text, TextAlignmentOptions.BottomLeft);
            UiTheme.Place(flaskInfo.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 10f), new Vector2(280f, 30f));

            portrait = MechanicPortrait.Create(barRect, 116f);
            UiTheme.Place((RectTransform)portrait.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(116f, 116f));

            RectTransform handBlock = Block(barRect, "Hands", 840f, 330f, "EMPTY HANDS");
            handTitle = handBlock.Find("Title").GetComponent<TextMeshProUGUI>();
            for (int i = 0; i < 3; i++)
            {
                Image pip = UiTheme.Panel("Combo Pip", handBlock, Color.white, null, 0.35f);
                UiTheme.Place(pip.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(i * 30f, -40f), new Vector2(22f, 22f));
                comboPips.Add(pip);
                comboFills.Add(Fill(pip));
            }
            cooldownFill = Meter(handBlock, new Vector2(110f, -46f), new Vector2(210f, 12f), new Vector2(0f, 1f));
            handInfo = UiTheme.Label("Info", handBlock, 21f, UiTheme.Text, TextAlignmentOptions.BottomLeft);
            UiTheme.Place(handInfo.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 10f), new Vector2(340f, 30f));

            RectTransform scrapBlock = Block(barRect, "Scrap", 1196f, 300f, "SCRAP  <size=80%>[Tab]</size>");
            for (int i = 0; i < ScrapInfo.All.Length; i++)
            {
                ScrapType type = ScrapInfo.All[i];
                RectTransform chip = UiTheme.Place(UiTheme.Rect(ScrapInfo.Name(type), scrapBlock), new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2((i % 2) * 150f, -38f - (i / 2) * 38f), new Vector2(140f, 32f));
                Sprite icon = ScrapCatalog.Icon(type);
                Image dot = icon != null ? UiTheme.Icon("Icon", chip, icon) : UiTheme.Panel("Dot", chip, ScrapInfo.Color(type), null, 0.3f);
                UiTheme.Place(dot.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), icon != null ? new Vector2(-8f, 0f) : Vector2.zero,
                    icon != null ? new Vector2(40f, 40f) : new Vector2(20f, 20f));
                scrapCounts[i] = UiTheme.Label("Count", chip, 26f, UiTheme.Text, TextAlignmentOptions.Left, FontStyles.Bold);
                UiTheme.Place(scrapCounts[i].rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(30f, 0f), new Vector2(110f, 32f));
            }
            scrapToast = UiTheme.Label("Scrap Toast", root, 26f, UiTheme.Text, TextAlignmentOptions.Right, FontStyles.Bold);
            UiTheme.Place(scrapToast.rectTransform, new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(752f, BottomReserved), new Vector2(900f, 36f));
            scrapToast.alpha = 0f;

            // Death / blackout fade on its own canvas, above dialogue and menus' text.
            fadeCanvas = UiTheme.CreateCanvas("Screen Fade", 80);
            fadeCanvas.GetComponent<GraphicRaycaster>().enabled = false;
            fade = UiTheme.Block("Fade", fadeCanvas.transform, Color.clear);
            UiTheme.Stretch(fade.rectTransform);
            centreMessage = UiTheme.Label("Message", fade.transform, 64f, UiTheme.Warning, TextAlignmentOptions.Center, FontStyles.Bold);
            UiTheme.Place(centreMessage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(1400f, 80f));
            centreSub = UiTheme.Label("Sub", fade.transform, 28f, UiTheme.Muted, TextAlignmentOptions.Center);
            UiTheme.Place(centreSub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(1000f, 40f));
        }

        private static RectTransform Block(RectTransform bar, string name, float x, float width, string title)
        {
            RectTransform block = UiTheme.Place(UiTheme.Rect(name, bar), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(x, 0f), new Vector2(width, BarHeight - 16f));
            TextMeshProUGUI label = UiTheme.Label("Title", block, 22f, UiTheme.Accent, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UiTheme.Place(label.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(width, 30f));
            label.text = title;
            return block;
        }

        private static Image FillBar(RectTransform parent, Color color)
        {
            Image fill = UiTheme.Panel("Fill", parent, color, null, 0.4f);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            return fill;
        }

        private static Image Meter(RectTransform parent, Vector2 position, Vector2 size, Vector2? anchor = null)
        {
            Vector2 a = anchor ?? new Vector2(0.5f, 1f);
            RectTransform meter = UiTheme.Place(UiTheme.Rect("Meter", parent), a, new Vector2(a.x, 1f), position, size);
            UiTheme.Stretch(UiTheme.Panel("Back", meter, new Color(0f, 0f, 0f, 0.55f), null, 0.4f).rectTransform);
            return FillBar(meter, new Color(0.4f, 0.9f, 1f));
        }
    }
}
