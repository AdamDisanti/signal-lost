using System.Collections.Generic;
using SignalLost.Combat;
using SignalLost.Enemies.Desert;
using SignalLost.Player;
using SignalLost.UI;
using UnityEngine;
using UnityEngine.AI;

namespace SignalLost.Enemies.Caves
{
    /// <summary>
    /// The four cave beetles as one band. Put it at the centre of their fight zone and list the members.
    ///
    /// - <b>Zone:</b> the beetles never walk out of <see cref="zoneRadius"/> around this object, except to their places in a
    ///   box-in, which always surround the player evenly.
    /// - <b>Together:</b> when one is drawn into the fight, the whole band joins in, under one boss bar showing their
    ///   combined health. The band's phases (Rehearsal, Performance, Encore) follow that combined health.
    /// - <b>Turns:</b> only one beetle attacks at a time; the others stalk the player.
    /// - <b>Box-in:</b> every so often they surround the player (Gather), then the ring appears and the box is set: they
    ///   rear up with their mic lights blinking while the ring closes (Brace: the only warning, no on-screen text). Out of
    ///   the ring before the charge: they break off and chase again, holding their attacks for a moment. Otherwise they
    ///   charge the middle of the box, committed, never turning after the player. A grounded player in the box is hit
    ///   once and they stop short; otherwise (jumped over, or got out during the charge) they slam into each other.
    /// - <b>Anger:</b> each fallen member makes the rest faster and the box-ins more frequent, with a shorter warning.
    ///   With three left it is a triangle, with two a pincer, and a lone survivor charges alone (and skids onto its back
    ///   if jumped). Beetles that slam into each other flip onto their backs: their bellies are the weak spot.
    /// - <b>Talking:</b> a hit beetle says its next line, through the dialogue subtitles, the way the merchant crab's
    ///   cousins bark; the band also calls out its box-ins, its misses and its fallen.
    /// All numbers are placeholders.
    /// </summary>
    public sealed class BeetleBand : MonoBehaviour, IBossBar
    {
        [Tooltip("The name on the shared boss bar and the banners.")]
        [SerializeField] private string bandName = "The Beetles";
        private enum Formation { None, Gather, Brace, Charge }

        [SerializeField] private CaveBeetle[] members = new CaveBeetle[0];
        [Tooltip("The beetles stay inside this radius around the band (their fight zone).")]
        [SerializeField, Min(5f)] private float zoneRadius = 24f;

        [Header("Box-in")]
        [Tooltip("How far from the player the beetles stand when they box them in.")]
        [SerializeField, Min(2f)] private float boxRadius = 6.5f;
        [SerializeField, Min(0f)] private float firstBoxIn = 6f;
        [Tooltip("Seconds between box-ins with the full band; each fallen member takes Interval Per Fallen off.")]
        [SerializeField, Min(1f)] private float boxInInterval = 10f;
        [SerializeField, Min(0f)] private float intervalPerFallen = 1.8f;
        [Tooltip("The warning (rearing up, blinking lights, closing ring) before the charge.")]
        [SerializeField, Min(0.2f)] private float braceTime = 1.2f;
        [SerializeField, Min(0f)] private float bracePerFallen = 0.12f;
        [SerializeField, Min(0.5f)] private float gatherTimeout = 3.5f;
        [Tooltip("Damage when the box-in lands (once, however many beetles arrive).")]
        [SerializeField, Min(0f)] private float chargeDamage = 30f;
        [Tooltip("Speed and attack-rate bonus per fallen member.")]
        [SerializeField, Min(0f)] private float angerPerFallen = 0.15f;
        [SerializeField] private Material ringMaterial;
        [Tooltip("After the player escapes the box before the charge, seconds the beetles chase without attacking.")]
        [SerializeField, Min(0f)] private float calmAfterEscape = 2.5f;

        [Header("Voice")]
        [Tooltip("Lines are subtitled only while the player is this close.")]
        [SerializeField, Min(1f)] private float hearingDistance = 40f;
        [SerializeField] private string[] openingLines = { "An audience! Places, everyone!" };
        [SerializeField] private string[] boxInLines = { "From the top!" };
        [SerializeField] private string[] dodgedLines = { "Ow! Who was on tempo?!" };
        [SerializeField] private string[] landedLines = { "Encore!" };
        [SerializeField] private string[] memberDownLines = { "We're down a member!" };

        public Vector3 FormationCentre { get; private set; }
        public float BraceProgress => formation == Formation.Brace ? Mathf.Clamp01(formationTime / CurrentBrace) : formation == Formation.Charge ? 1f : 0f;
        public float SpeedScale => 1f + angerPerFallen * Fallen;
        public int Fallen { get; private set; }

        // ---------- The shared boss bar ----------

        public string DisplayName => bandName;
        /// <summary>The whole band's health: fallen members count as empty.</summary>
        public float HealthFraction
        {
            get
            {
                float current = 0f, max = 0f;
                foreach (CaveBeetle member in members)
                {
                    if (member == null) continue;
                    var health = member.GetComponent<Health>();
                    current += health.Current;
                    max += health.Max;
                }
                return max > 0f ? current / max : 0f;
            }
        }
        public int DisplayPhase
        {
            get
            {
                int phase = 1;
                foreach (CaveBeetle member in Live(members)) phase = Mathf.Max(phase, member.DuelPhase);
                return phase;
            }
        }
        public string PhaseTitle
        {
            get
            {
                foreach (CaveBeetle member in Live(members))
                    if (member.DuelPhase == DisplayPhase) return member.PhaseTitle;
                return "";
            }
        }
        public float[] PhaseMarks
        {
            get
            {
                foreach (CaveBeetle member in members)
                    if (member != null) return member.PhaseMarks;
                return System.Array.Empty<float>();
            }
        }
        public bool InPhaseShift
        {
            get
            {
                foreach (CaveBeetle member in Live(members))
                    if (member.InPhaseShift) return true;
                return false;
            }
        }

        private readonly List<CaveBeetle> boxing = new List<CaveBeetle>();
        private readonly Dictionary<CaveBeetle, int> nextLine = new Dictionary<CaveBeetle, int>();
        private readonly Dictionary<CaveBeetle, float> slotAngle = new Dictionary<CaveBeetle, float>();
        private Formation formation = Formation.None;
        private float formationTime, nextBoxIn = float.PositiveInfinity, nextVoice;
        private bool playerHit, opened;
        private FirstPersonController player;
        private GroundMarker ring;
        private Transform ringHolder; // GroundMarker pulses its own scale, so the closing shrink goes on a parent
        private Camera viewer;
        private (CaveBeetle speaker, string line, float at) pending;

        private float CurrentBrace => Mathf.Max(0.6f, braceTime - bracePerFallen * Fallen);

        private void Awake()
        {
            foreach (CaveBeetle member in members)
                if (member != null)
                {
                    member.Band = this;
                    nextLine[member] = 0;
                }
        }

        private void Start() => player = FindAnyObjectByType<FirstPersonController>();

        private void OnDisable()
        {
            ClearRing();
            MiniBossHud.Unregister(this);
        }

        // ---------- Box-in ----------

        private void Update()
        {
            if (pending.speaker != null && Time.time >= pending.at)
            {
                if (!pending.speaker.IsDead) Say(pending.speaker, pending.line, true);
                pending = default;
            }
            if (player == null) return;

            bool anyEngaged = false;
            foreach (CaveBeetle member in members)
                if (member != null && !member.IsDead && member.Engaged) anyEngaged = true;
            if (!anyEngaged)
            {
                if (formation != Formation.None) EndFormation();
                return;
            }

            formationTime += Time.deltaTime;
            switch (formation)
            {
                case Formation.None:
                    if (Time.time >= nextBoxIn) TryStartBoxIn();
                    break;
                case Formation.Gather:
                    TrackPlayer();
                    if (AllInPlace() || formationTime >= gatherTimeout)
                    {
                        formation = Formation.Brace;
                        formationTime = 0f;
                        foreach (CaveBeetle member in Live(boxing)) member.BeginBrace();
                        ringHolder = new GameObject("Box-in Ring").transform;
                        ringHolder.position = FormationCentre;
                        ring = GroundMarker.Spawn(ringMaterial, null, FormationCentre, boxRadius);
                        ring.transform.SetParent(ringHolder, true);
                    }
                    break;
                case Formation.Brace:
                    // The box is set once the ring appears; it no longer follows the player. Out of it before the
                    // charge, and the box-in is called off.
                    if ((Flat(player.transform.position) - Flat(FormationCentre)).magnitude > boxRadius)
                    {
                        Disperse();
                        break;
                    }
                    if (ring != null)
                    {
                        // Shrinks from the box to the player, closing just as the beetles arrive: jump then.
                        ring.Progress = formationTime / (CurrentBrace + ChargeTime());
                        ringHolder.localScale = Vector3.one * Mathf.Lerp(1f, 0.25f, Mathf.Clamp01(ring.Progress));
                    }
                    if (formationTime >= CurrentBrace)
                    {
                        formation = Formation.Charge;
                        formationTime = 0f;
                        playerHit = false;
                        foreach (CaveBeetle member in Live(boxing)) member.BeginCharge(FormationCentre, ChargeTime());
                    }
                    break;
                case Formation.Charge:
                    UpdateCharge();
                    break;
            }
        }

        private void TryStartBoxIn()
        {
            boxing.Clear();
            foreach (CaveBeetle member in members)
            {
                if (member == null || member.IsDead || !member.Engaged) continue;
                if (!member.ReadyForFormation) return; // wait until no one is dazed or changing
                boxing.Add(member);
            }
            if (boxing.Count == 0) return;

            // Slots evenly round the player, starting from where the beetles already are (so they don't cross over).
            FormationCentre = player.transform.position;
            boxing.Sort((a, b) => AngleAround(a).CompareTo(AngleAround(b)));
            float start = AngleAround(boxing[0]);
            slotAngle.Clear();
            for (int i = 0; i < boxing.Count; i++)
            {
                slotAngle[boxing[i]] = start + i * 360f / boxing.Count;
                boxing[i].BeginGather(Slot(boxing[i]));
            }
            formation = Formation.Gather;
            formationTime = 0f;
            Say(boxing[Random.Range(0, boxing.Count)], Pick(boxInLines), true);
        }

        private void TrackPlayer()
        {
            FormationCentre = player.transform.position;
            if (ringHolder != null) ringHolder.position = FormationCentre;
            foreach (CaveBeetle member in Live(boxing)) member.UpdateSlot(Slot(member));
        }

        // Evenly round the player (a cross with four, a triangle with three), never pulled in by the zone edge, which
        // used to bunch them up; a slot may lie up to Box Radius outside the zone.
        private Vector3 Slot(CaveBeetle member)
        {
            Vector3 at = FormationCentre + Quaternion.Euler(0f, slotAngle.TryGetValue(member, out float angle) ? angle : 0f, 0f) * Vector3.forward * boxRadius;
            return NavMesh.SamplePosition(at, out NavMeshHit hit, 2f, NavMesh.AllAreas) ? hit.position : at;
        }

        private bool AllInPlace()
        {
            foreach (CaveBeetle member in Live(boxing))
            {
                Vector3 offset = Flat(member.transform.position) - Flat(Slot(member));
                if (offset.magnitude > 1.5f) return false;
            }
            return true;
        }

        private void UpdateCharge()
        {
            if (ring != null) ring.Progress = 1f;
            var chargers = new List<CaveBeetle>();
            foreach (CaveBeetle member in Live(boxing))
                if (member.Charging) chargers.Add(member);

            // They charge the middle of the box, committed. Unless a grounded player in the box took the hit (which
            // ends the box-in straight away), they meet in the middle: jumped over or not there, they collide.
            if (!playerHit)
            {
                if (boxing.Count == 1)
                {
                    // A lone beetle has no one to bump into: it carries on past the middle, skids and is dazed.
                    if (chargers.Count == 1 && formationTime > ChargeTime() + 0.2f) chargers[0].Skid();
                }
                else
                {
                    for (int i = 0; i < chargers.Count; i++)
                    for (int j = i + 1; j < chargers.Count; j++)
                    {
                        CaveBeetle a = chargers[i], b = chargers[j];
                        if (a.Bumped && b.Bumped) continue;
                        Vector3 between = Flat(a.transform.position) - Flat(b.transform.position);
                        if (between.magnitude > 2.2f) continue;
                        bool first = !AnyBumped();
                        a.Bump(between);
                        b.Bump(-between);
                        if (first) Say(a.IsDead ? b : a, Pick(dodgedLines), true);
                    }
                }
            }

            if (chargers.Count == 0 || formationTime > 1.5f) EndFormation();
        }

        private bool AnyBumped()
        {
            foreach (CaveBeetle member in boxing)
                if (member != null && member.Bumped) return true;
            return false;
        }

        /// <summary>A charging beetle reached the player: a hit if they're on the ground, a miss if they jumped.</summary>
        public void Contact(CaveBeetle beetle)
        {
            if (formation != Formation.Charge || playerHit) return;
            if (player.IsGrounded)
            {
                playerHit = true;
                beetle.HitPlayer(chargeDamage, Flat(player.transform.position) - Flat(beetle.transform.position));
                Say(beetle, Pick(landedLines), true);
                EndFormation(); // they stop short of each other
                return;
            }
            beetle.PassUnder();
        }

        /// <summary>
        /// The player got out of the box before the charge: the beetles break formation and chase again, but hold off
        /// attacking for a moment, so nothing hits the player out of nowhere.
        /// </summary>
        private void Disperse()
        {
            foreach (CaveBeetle member in Live(boxing)) member.Disperse(calmAfterEscape);
            boxing.Clear();
            ClearRing();
            formation = Formation.None;
            formationTime = 0f;
            nextBoxIn = Time.time + Mathf.Max(3f, boxInInterval - intervalPerFallen * Fallen);
        }

        private void EndFormation()
        {
            foreach (CaveBeetle member in Live(boxing)) member.EndFormation();
            boxing.Clear();
            ClearRing();
            formation = Formation.None;
            formationTime = 0f;
            nextBoxIn = Time.time + Mathf.Max(3f, boxInInterval - intervalPerFallen * Fallen);
        }

        private void ClearRing()
        {
            if (ringHolder != null) Destroy(ringHolder.gameObject);
            ring = null;
            ringHolder = null;
        }

        /// <summary>Seconds from the charge starting to the beetles reaching the player: when the ring closes.</summary>
        private float ChargeTime() => Mathf.Max(0.1f, (boxRadius - CurrentContact()) / CurrentChargeSpeed());

        private float CurrentChargeSpeed()
        {
            foreach (CaveBeetle member in Live(boxing)) return member.ChargeSpeed;
            return 14f;
        }

        private float CurrentContact()
        {
            foreach (CaveBeetle member in Live(boxing)) return member.ContactDistance;
            return 1.7f;
        }

        // ---------- Turns ----------

        /// <summary>
        /// May this beetle start an attack? Only one attacks at a time, never during a box-in, and nobody starts one
        /// once a box-in is due (so the band is free to form up).
        /// </summary>
        public bool MayAttack(CaveBeetle beetle)
        {
            if (formation != Formation.None || Time.time >= nextBoxIn) return false;
            foreach (CaveBeetle member in Live(members))
                if (member != beetle && member.Attacking) return false;
            return true;
        }

        /// <summary>The living beetle nearest a point (only it pushes the player away when the band roars together).</summary>
        public CaveBeetle Nearest(Vector3 point)
        {
            CaveBeetle best = null;
            float bestDistance = float.MaxValue;
            foreach (CaveBeetle member in Live(members))
            {
                float distance = (Flat(member.transform.position) - Flat(point)).sqrMagnitude;
                if (distance < bestDistance) { bestDistance = distance; best = member; }
            }
            return best;
        }

        /// <summary>True while any bandmate other than this one is still alive.</summary>
        public bool OthersAlive(CaveBeetle beetle)
        {
            foreach (CaveBeetle member in Live(members))
                if (member != beetle) return true;
            return false;
        }

        // ---------- Members ----------

        /// <summary>The beetles never leave their zone: a point outside it is pulled back to its edge.</summary>
        public Vector3 KeepInZone(Vector3 point)
        {
            Vector3 centre = transform.position;
            Vector3 offset = point - centre;
            offset.y = 0f;
            float limit = zoneRadius - 1.5f;
            if (offset.magnitude > limit) point = centre + offset.normalized * limit + Vector3.up * (point.y - centre.y);
            return point;
        }

        public void MemberEngaged(CaveBeetle beetle)
        {
            foreach (CaveBeetle member in members)
                if (member != null && member != beetle && !member.IsDead) member.JoinFight();
            if (opened) return;
            opened = true;
            MiniBossHud.Register(this);
            nextBoxIn = Time.time + firstBoxIn;
            Say(members.Length > 0 && members[0] != null ? members[0] : beetle, Pick(openingLines), true);
        }

        public void MemberHit(CaveBeetle beetle)
        {
            if (beetle.HitLines.Length == 0 || Time.time < nextVoice) return;
            int index = nextLine.TryGetValue(beetle, out int i) ? i : 0;
            if (Say(beetle, beetle.HitLines[index % beetle.HitLines.Length], false)) nextLine[beetle] = index + 1;
        }

        public void MemberDown(CaveBeetle beetle)
        {
            Fallen++;
            Say(beetle, beetle.LastWords, true);
            if (!OthersAlive(beetle)) MiniBossHud.Unregister(this);
            foreach (CaveBeetle member in members)
                if (member != null && !member.IsDead)
                {
                    pending = (member, Pick(memberDownLines), Time.time + 2.8f);
                    break;
                }
        }

        public void MemberReset(CaveBeetle beetle)
        {
            if (formation != Formation.None) EndFormation();
            foreach (CaveBeetle member in members)
                if (member != null && !member.IsDead && member.Engaged) return;
            // The whole band went home: the next fight starts with the opening line and a fresh box-in timer again.
            opened = false;
            nextBoxIn = float.PositiveInfinity;
            MiniBossHud.Unregister(this);
        }

        // ---------- Talking ----------

        private bool Say(CaveBeetle speaker, string line, bool always)
        {
            if (speaker == null || string.IsNullOrEmpty(line) || (!always && Time.time < nextVoice)) return false;
            if (viewer == null || !viewer.isActiveAndEnabled) viewer = Camera.main;
            if (viewer == null || Vector3.Distance(viewer.transform.position, speaker.transform.position) > hearingDistance) return false;
            DialogueUi ui = DialogueUi.Current;
            if (ui == null) return false;
            nextVoice = Time.time + 2.6f;
            ui.Subtitle(speaker.DisplayName, line, 2.5f);
            return true;
        }

        // ---------- Helpers ----------

        private static string Pick(string[] lines) => lines.Length == 0 ? null : lines[Random.Range(0, lines.Length)];

        private static Vector3 Flat(Vector3 point) => new Vector3(point.x, 0f, point.z);

        private float AngleAround(CaveBeetle member)
        {
            Vector3 offset = Flat(member.transform.position) - Flat(FormationCentre);
            return Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
        }

        private static IEnumerable<CaveBeetle> Live(IEnumerable<CaveBeetle> list)
        {
            foreach (CaveBeetle member in list)
                if (member != null && !member.IsDead) yield return member;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.75f, 0.2f, 0.8f);
            const int steps = 48;
            for (int i = 0; i < steps; i++)
            {
                float a = i * Mathf.PI * 2f / steps, b = (i + 1) * Mathf.PI * 2f / steps;
                Gizmos.DrawLine(transform.position + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * zoneRadius,
                    transform.position + new Vector3(Mathf.Sin(b), 0f, Mathf.Cos(b)) * zoneRadius);
            }
        }
    }
}
