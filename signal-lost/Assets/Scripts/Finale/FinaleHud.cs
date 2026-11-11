using SignalLost.Core;
using SignalLost.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SignalLost.Finale
{
    /// <summary>
    /// The final fight's HUD, in the game's UI style: Cavendish's boss bar at the top (name, phase, notches where the
    /// shield goes up, and a teal "SHIELDED" cover while he is immune), and the crew call-in at the right (ready, in
    /// the fight, or cooling down). Created by <see cref="FinaleEncounter"/>; hidden behind full-screen menus.
    /// </summary>
    public sealed class FinaleHud : MonoBehaviour
    {
        private static readonly Color Fill = new Color(0.95f, 0.36f, 0.14f), ShieldColor = new Color(0.3f, 0.95f, 0.9f, 0.85f);

        private FinaleEncounter encounter;
        private GameObject boss, crew;
        private RectTransform bar, crewBar;
        private Image fill, shieldCover, crewFill;
        private TextMeshProUGUI phase, shieldLabel, attack, crewLabel;
        private float shown;
        private readonly RobotRow[] robotRows = new RobotRow[6];

        private sealed class RobotRow
        {
            public GameObject Root;
            public TextMeshProUGUI Name;
            public Image Fill;
        }

        public static FinaleHud Ensure(FinaleEncounter encounter)
        {
            var hud = UiTheme.CreateCanvas("Finale HUD", 20).gameObject.AddComponent<FinaleHud>();
            hud.encounter = encounter;
            return hud;
        }

        private void Awake() => Build();

        private void Build()
        {
            var root = (RectTransform)transform;
            Image panel = UiTheme.Panel("Boss", root, UiTheme.PanelColor, UiTheme.Edge, 1f);
            RectTransform p = UiTheme.Place(panel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(820f, 112f));
            boss = panel.gameObject;
            TextMeshProUGUI name = UiTheme.Label("Name", p, 30f, UiTheme.Accent, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            UiTheme.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -10f), new Vector2(520f, 40f));
            name.text = "CAVENDISH  <size=70%><color=#A9B4CC>in THE SAFEKEEPER</color></size>";
            phase = UiTheme.Label("Phase", p, 24f, UiTheme.Text, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
            UiTheme.Place(phase.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -12f), new Vector2(260f, 36f));

            Image back = UiTheme.Panel("Bar", p, new Color(0f, 0f, 0f, 0.6f), null, 0.4f);
            bar = UiTheme.Place(back.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(770f, 26f));
            fill = UiTheme.Panel("Fill", bar, Fill, null, 0.4f);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = new Vector2(3f, 3f);
            fill.rectTransform.offsetMax = new Vector2(-3f, -3f);
            shieldCover = UiTheme.Panel("Shield", bar, ShieldColor, null, 0.4f);
            UiTheme.Stretch(shieldCover.rectTransform);
            shieldLabel = UiTheme.Label("Shielded", bar, 18f, new Color(0.05f, 0.15f, 0.15f), TextAlignmentOptions.Center, FontStyles.Bold);
            UiTheme.Stretch(shieldLabel.rectTransform);
            shieldLabel.text = "REPAIRING";
            attack = UiTheme.Label("Attack", p, 18f, UiTheme.Muted, TextAlignmentOptions.MidlineLeft, FontStyles.Italic);
            UiTheme.Place(attack.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(26f, 4f), new Vector2(500f, 24f));

            Image crewPanel = UiTheme.Panel("Crew", root, UiTheme.PanelColor, UiTheme.Edge, 0.8f);
            RectTransform c = UiTheme.Place(crewPanel.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 190f), new Vector2(330f, 74f));
            crew = crewPanel.gameObject;
            crewLabel = UiTheme.Label("Label", c, 20f, UiTheme.Text, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            UiTheme.Place(crewLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -6f), new Vector2(300f, 32f));
            Image crewBack = UiTheme.Panel("Bar", c, new Color(0f, 0f, 0f, 0.55f), null, 0.4f);
            crewBar = UiTheme.Place(crewBack.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(296f, 16f));
            crewFill = UiTheme.Panel("Fill", crewBar, UiTheme.Good, null, 0.4f);
            crewFill.rectTransform.anchorMin = Vector2.zero;
            crewFill.rectTransform.anchorMax = Vector2.one;
            crewFill.rectTransform.offsetMin = new Vector2(2f, 2f);
            crewFill.rectTransform.offsetMax = new Vector2(-2f, -2f);
            // The robot mini-bosses, listed under the boss bar while they're up.
            for (int i = 0; i < robotRows.Length; i++)
            {
                Image row = UiTheme.Panel("Robot " + i, root, UiTheme.PanelColor, null, 0.6f);
                RectTransform r = UiTheme.Place(row.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2((i % 3 - 1) * 276f, -138f - (i / 3) * 44f), new Vector2(266f, 38f));
                TextMeshProUGUI label = UiTheme.Label("Name", r, 17f, UiTheme.Text, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
                UiTheme.Place(label.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -2f), new Vector2(246f, 20f));
                Image barBack = UiTheme.Panel("Bar", r, new Color(0f, 0f, 0f, 0.55f), null, 0.3f);
                RectTransform b = UiTheme.Place(barBack.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 5f), new Vector2(246f, 10f));
                Image f = UiTheme.Panel("Fill", b, new Color(0.3f, 0.95f, 0.9f), null, 0.3f);
                f.rectTransform.anchorMin = Vector2.zero;
                f.rectTransform.anchorMax = Vector2.one;
                f.rectTransform.offsetMin = new Vector2(1f, 1f);
                f.rectTransform.offsetMax = new Vector2(-1f, -1f);
                robotRows[i] = new RobotRow { Root = row.gameObject, Name = label, Fill = f };
                row.gameObject.SetActive(false);
            }
            boss.SetActive(false);
            crew.SetActive(false);
        }

        private bool notched;

        private void AddNotches()
        {
            notched = true;
            foreach (float at in encounter.Mech.ShieldThresholds)
            {
                Image notch = UiTheme.Block("Notch", bar, new Color(1f, 1f, 1f, 0.85f));
                notch.rectTransform.anchorMin = new Vector2(at, 0f);
                notch.rectTransform.anchorMax = new Vector2(at, 1f);
                notch.rectTransform.sizeDelta = new Vector2(3f, 8f);
                notch.rectTransform.anchoredPosition = Vector2.zero;
            }
            shieldCover.transform.SetAsLastSibling();
            shieldLabel.transform.SetAsLastSibling();
        }

        private void Update()
        {
            if (encounter == null) { Destroy(gameObject); return; }
            CavendishMech mech = encounter.Mech;
            bool fight = encounter.Current == FinaleEncounter.State.Fighting && mech.Current != CavendishMech.State.Dormant;
            bool visible = !UiMenus.FullScreenOpen;
            boss.SetActive(visible && (fight || (encounter.Current == FinaleEncounter.State.Won && Time.time - shown < 4f)));
            if (fight) shown = Time.time;
            if (boss.activeSelf)
            {
                if (!notched) AddNotches();
                fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(mech.HealthFraction), 1f);
                fill.color = mech.Phase >= 3 ? new Color(0.9f, 0.12f, 0.12f) : mech.Phase == 2 ? new Color(1f, 0.25f, 0.12f) : Fill;
                bool shielded = mech.Shielded;
                shieldCover.gameObject.SetActive(shielded);
                shieldLabel.gameObject.SetActive(shielded);
                if (shielded)
                {
                    shieldCover.color = new Color(ShieldColor.r, ShieldColor.g, ShieldColor.b, 0.55f + 0.25f * Mathf.Sin(Time.time * 5f));
                    shieldLabel.text = $"REPAIRING: ROBOTS {encounter.RobotsTotal - encounter.RobotsLeft} / {encounter.RobotsTotal}   CONDUITS {encounter.ConduitsTotal - encounter.ConduitsLeft} / {encounter.ConduitsTotal}";
                }
                phase.text = mech.Current == CavendishMech.State.Defeated ? "DOWN!" : $"PHASE {mech.Phase} / 3";
                attack.text = string.IsNullOrEmpty(mech.AttackName) ? "" : mech.AttackName + "!";
            }

            int shownRows = 0;
            if (boss.activeSelf)
                foreach (FinaleRobots.Robot robot in encounter.Robots)
                {
                    if (!robot.Alive || shownRows >= robotRows.Length) continue;
                    RobotRow row = robotRows[shownRows++];
                    row.Root.SetActive(true);
                    row.Name.text = robot.Name.ToUpperInvariant();
                    row.Fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(robot.Health.Normalized), 1f);
                }
            for (int i = shownRows; i < robotRows.Length; i++) robotRows[i].Root.SetActive(false);

            CrewCallIn call = encounter.Crew;
            crew.SetActive(visible && call != null && call.Enabled);
            if (!crew.activeSelf) return;
            string key = KeyBindings.Hint("call_crew");
            if (call.Active)
            {
                crewLabel.text = $"Eugene's crew: in the fight  <color=#A9B4CC>{call.ActiveRemaining:0} s</color>";
                crewFill.color = UiTheme.Accent;
                crewFill.rectTransform.anchorMax = new Vector2(call.ActiveRemaining / CrewCallIn.Duration, 1f);
            }
            else if (call.CooldownRemaining > 0f)
            {
                crewLabel.text = $"<color=#A9B4CC>{key} Eugene's crew  {call.CooldownRemaining:0} s</color>";
                crewFill.color = UiTheme.Muted;
                crewFill.rectTransform.anchorMax = new Vector2(1f - call.CooldownRemaining / CrewCallIn.Cooldown, 1f);
            }
            else
            {
                crewLabel.text = $"{key} Call Eugene's crew!";
                crewFill.color = UiTheme.Good;
                crewFill.rectTransform.anchorMax = Vector2.one;
            }
        }
    }
}
