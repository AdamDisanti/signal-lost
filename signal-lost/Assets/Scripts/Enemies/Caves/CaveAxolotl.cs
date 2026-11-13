using System.Collections;
using System.Collections.Generic;
using SignalLost.Audio;
using SignalLost.Combat;
using SignalLost.Effects;
using SignalLost.Enemies.Desert;
using SignalLost.Scanner;
using UnityEngine;
using UnityEngine.AI;

namespace SignalLost.Enemies.Caves
{
    /// <summary>
    /// The Cave Axolotl, the Crystal Caves stage boss (docs/cave-axolotl.md): a huge pale axolotl crusted with cave
    /// crystals, guarding the ship's Flight Control Module. Built on <see cref="DuelBoss"/> like the Shard Mole (boss
    /// bar, phases, poise, leash and reset) with a Blender model (tools/blender/cave_axolotl.py); its lair
    /// (<see cref="AxolotlLair"/>) runs the Core Worm-style entrance, the retry and the stage clear.
    ///
    /// Moves: the tail swipe (a full spin with its crystal tail), the roll (it curls into a ball, a line of rings shows
    /// its lane, then it rolls down it untouchable; a roll into a wall or spire leaves it dazed with its gills open),
    /// the shard ring (phase 2+: it arches, its crystals glow, and shards fly off its back in a ring at shin height:
    /// jump them), and the hide (it burrows and poses as a crystal beside a real batch, recoloured to match; only the
    /// Crystal Disruptor scanner, raised and close, brings it back up, harmlessly, with a small shove).
    ///
    /// Phases: 1 Still Pool; 2 Crystal Bloom (crystal spires grow up round the arena); 3 Cave-In (ceiling crystals fall
    /// on marked rings near you, and rolls come in pairs); 4 Frenzy (faster, roll-into-shard-ring combos, double rings).
    /// It hides at every phase change and every <see cref="hideEvery"/> seconds from phase 2.
    ///
    /// Damage: its crystal-crusted hide takes <see cref="armour"/> of each hit; the gills take full damage (double while
    /// it's dazed).
    /// </summary>
    public sealed class CaveAxolotl : DuelBoss, IScannerDisruptable
    {
        public const string Stage = ShardMole.Stage;
        public const string MainPart = "Flight Control Module";
        private const float BallRadius = 1.3f;

        [Header("Cave Axolotl")]
        [Tooltip("The cave crystal material on its body now. It changes each time it hides.")]
        [SerializeField] private Material crystalMaterial;
        [SerializeField] private SkinnedMeshRenderer body;
        [SerializeField] private Light[] crystalLights = new Light[0];
        [Tooltip("The level's CrystalBatch prefab: its fake crystal while it hides is a real batch.")]
        [SerializeField] private GameObject disguisePrefab;
        [Tooltip("The gill hitboxes (weak spots).")]
        [SerializeField] private Health[] gills = new Health[0];
        [Tooltip("The Flight Control Module, hidden inside it until it drops.")]
        [SerializeField] private Transform mainPart;
        [Tooltip("The cave's crystal spire mesh (Crystal Bloom).")]
        [SerializeField] private Mesh spireMesh;
        [SerializeField] private Color dustColor = new Color(0.42f, 0.43f, 0.5f);

        [Header("Damage taken")]
        [Tooltip("Share of each hit its crystal-crusted hide takes.")]
        [SerializeField, Range(0.05f, 1f)] private float armour = 0.5f;
        [SerializeField, Min(0f)] private float gillMultiplier = 1f;
        [SerializeField, Min(0f)] private float dazedGillMultiplier = 2f;

        [Header("Damage dealt")]
        [SerializeField, Min(0f)] private float swipeDamage = 22f;
        [SerializeField, Min(0f)] private float swipeKnockback = 3.5f;
        [SerializeField, Min(0f)] private float rollDamage = 26f;
        [SerializeField, Min(0f)] private float shardDamage = 16f;
        [SerializeField, Min(0f)] private float caveInDamage = 20f;

        [Header("Hiding")]
        [Tooltip("How close the raised, upgraded scanner has to get to its fake crystal.")]
        [SerializeField, Min(0.5f)] private float revealRadius = 4f;
        [Tooltip("From phase 2: seconds of fighting before it hides again.")]
        [SerializeField, Min(3f)] private float hideEvery = 25f;
        [Tooltip("How far it pushes a close player when it bursts back up (no damage).")]
        [SerializeField, Min(0f)] private float emergeShove = 2.5f;
        [SerializeField, Min(1f)] private float emergeShoveReach = 5f;
        [Tooltip("It hides at least this far from the player when it can.")]
        [SerializeField, Min(0f)] private float hideAwayFromPlayer = 8f;

        [Header("Roll")]
        [SerializeField, Min(1f)] private float rollSpeed = 15f;
        [SerializeField, Min(2f)] private float rollLength = 18f;
        [Tooltip("How long the lane shows before the first roll (the aim locks 60% of the way in).")]
        [SerializeField, Min(0.3f)] private float rollWarning = 1.2f;
        [SerializeField, Min(0.3f)] private float rollHitRadius = 1.4f;
        [SerializeField, Min(0f)] private float rollKnockback = 4f;

        [Header("Shard ring")]
        [SerializeField, Range(6, 60)] private int shardCount = 24;
        [SerializeField, Min(1f)] private float shardSpeed = 12f;
        [Tooltip("Height above the floor the shards fly at: low enough to jump.")]
        [SerializeField, Min(0.1f)] private float shardHeight = 0.4f;

        [Header("Arena")]
        [SerializeField, Range(0, 12)] private int spireCount = 6;
        [SerializeField, Min(0.5f)] private float spireHeight = 3.2f;
        [SerializeField, Min(1f)] private float caveInEvery = 5f;
        [SerializeField, Range(1, 8)] private int caveInCount = 3;
        [SerializeField, Min(0.3f)] private float caveInRadius = 1.6f;
        [SerializeField, Min(0.4f)] private float caveInWarning = 1.3f;

        // IScannerDisruptable: only while it poses as a crystal mid-fight.
        public bool IsDisguised => hidden && !IsDead;
        public Vector3 DisguisePosition => disguise != null ? disguise.transform.position : transform.position;
        public float RevealRadius => revealRadius;

        /// <summary>True while it's underground posing as a crystal (mid-fight).</summary>
        public bool IsHiding => hidden;
        public bool IsRolling => rolling;
        public bool IsDazed => dazed;
        /// <summary>Waiting under its crystal before the fight (see <see cref="AxolotlLair"/>).</summary>
        public bool Dormant => !enabled;
        public Material CurrentCrystal => crystalMaterial;
        /// <summary>Its Start has run (the lair waits for this before <see cref="BeginFight"/>).</summary>
        public bool Started { get; private set; }

        private GameObject disguise;
        private Light disguiseLight;
        private int crystalSlot = -1;
        private bool hidden, disrupted, hideQueued, rolling, dazed;
        private float nextHideAt = float.PositiveInfinity, nextCaveIn = float.PositiveInfinity, spin, glowBoost = 1f, flare;
        private float[] lightBase = new float[0];
        private CrystalBatch lastHideBatch;
        private readonly List<Coroutine> caveIns = new List<Coroutine>();
        private readonly List<GameObject> caveInBits = new List<GameObject>();

        protected override string PartStage => Stage;
        protected override Transform KeepHidden => mainPart;

        // ---------- Setup ----------

        protected override void OnBossStarted()
        {
            base.OnBossStarted();
            for (int i = 0; i < gills.Length; i++) RelayPart(gills[i], i);
            Health.DamageModifier = (amount, source) => amount * armour;
            lightBase = new float[crystalLights.Length];
            for (int i = 0; i < crystalLights.Length; i++) lightBase[i] = crystalLights[i] != null ? crystalLights[i].intensity : 0f;
            FindCrystalSlot();
            Started = true;
        }

        private void FindCrystalSlot()
        {
            if (crystalSlot >= 0 || body == null) return;
            Material[] mats = body.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
                if (mats[i] == crystalMaterial) crystalSlot = i;
        }

        // The relayed gill damage goes through the body's Health, so undo the armour it'll get there.
        protected override float OnPartHit(int part, float amount, GameObject source) =>
            amount * (dazed ? dazedGillMultiplier : gillMultiplier) / armour;

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (disguise != null) Destroy(disguise);
        }

        protected override void DefineAttacks()
        {
            Attacks.Add(new Attack { Name = "TailSwipe", Run = TailSwipe, MaxRange = 6.5f, Weight = 3f, Cooldown = 3.5f, HyperArmor = true });
            Attacks.Add(new Attack { Name = "TailSwipeFlank", Run = TailSwipe, MaxRange = 6.5f, MinAngle = 70f, Weight = 5f, Cooldown = 3.5f, HyperArmor = true });
            Attacks.Add(new Attack { Name = "Roll", Run = () => Roll(null), MinRange = 5f, MaxRange = 26f, Weight = 3f, Cooldown = 6f, HyperArmor = true });
            Attacks.Add(new Attack { Name = "ShardRing", Run = ShardRing, MinPhase = 2, Weight = 2.5f, Cooldown = 8f, HyperArmor = true });
            Attacks.Add(new Attack { Name = "Frenzy", Run = Frenzy, MinRange = 5f, MinPhase = 4, Weight = 3f, Cooldown = 10f, HyperArmor = true });
        }

        // ---------- The lair's hooks ----------

        /// <summary>Before the fight: it waits under a crystal at home, still and switched off, until the lair calls <see cref="BeginFight"/>.</summary>
        public void Lurk()
        {
            if (Agent != null && Agent.enabled && Agent.isOnNavMesh) Agent.Warp(Home);
            else transform.position = Home;
            SetBodyHidden(true);
            CrystalBatch near = NearestBatch(transform.position, null);
            if (near != null) Recolour(near.CrystalMaterial);
            ShowDisguise(transform.position, near);
            enabled = false;
        }

        /// <summary>Wakes it: it bursts out of its crystal (the intro) and the fight starts.</summary>
        public void BeginFight()
        {
            enabled = true;
            Engage();
        }

        // The intro: up out of its crystal, then the roar.
        protected override IEnumerator Intro()
        {
            yield return Emerge(false);
            yield return Roar(false);
        }

        // ---------- Phases and hiding ----------

        protected override void OnDuelPhase(int phase)
        {
            hideQueued = true;
            glowBoost = phase >= 4 ? 2.2f : phase == 3 ? 1.8f : phase == 2 ? 1.4f : 1f;
            if (phase == 2) StartCoroutine(Bloom());
            if (phase == 3) nextCaveIn = Time.time + 3f;
        }

        protected override void TickCombat(float dt)
        {
            // A phase change waiting to start goes first (DuelBoss runs it); hiding comes after its roar.
            if (!Busy && TargetAlive && !PhasePending())
            {
                if (hideQueued || (DuelPhase >= 2 && Time.time >= nextHideAt))
                {
                    hideQueued = false;
                    Run(Hide(), "Hide", null);
                    return;
                }
            }
            if (DuelPhase >= 3 && !hidden && Time.time >= nextCaveIn)
            {
                nextCaveIn = Time.time + caveInEvery / Tempo;
                caveIns.Add(StartCoroutine(CaveIn()));
            }
            base.TickCombat(dt);
        }

        private bool PhasePending()
        {
            int phase = 1;
            foreach (float at in PhaseMarks)
                if (Health.Normalized <= at) phase++;
            return phase > DuelPhase;
        }

        /// <summary>
        /// Burrows, picks a spot beside a real crystal batch (away from the player), takes that batch's colour, and
        /// waits there as a crystal until the Crystal Disruptor finds it (<see cref="Disrupt"/>).
        /// </summary>
        private IEnumerator Hide()
        {
            Health.Invulnerable = true;
            Play("BurrowIn");
            Sfx.PlayAt("wood_smash", transform.position, 0.8f, 0.5f, 50f);
            yield return Until("under", 0, () =>
            {
                Track(120f);
                if (Emit("dig", 16f) > 0) Dust(transform.position + Random.insideUnitSphere * 1.4f, 6, 1f, 4f);
            });
            SetBodyHidden(true);
            Dust(transform.position + Vector3.up * 0.3f, 20, 1.4f, 5f);

            CrystalBatch beside = PickHideBatch();
            Vector3 spot = HideSpot(beside);
            if (AgentReady) Agent.Warp(spot);
            else transform.position = spot;
            Vector3 face = Home - spot;
            face.y = 0f;
            if (face.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(face);
            lastHideBatch = beside;
            if (beside != null) Recolour(beside.CrystalMaterial);
            ShowDisguise(transform.position, beside);

            disrupted = false;
            hidden = true;
            MiniBossHud.Message("THE AXOLOTL IS HIDING AMONG THE CRYSTALS\nRaise the scanner " + SignalLost.Core.KeyBindings.Hint("swap") + " and find it",
                new Color(1f, 0.6f, 0.85f), 4f);
            while (!disrupted)
            {
                Halt();
                yield return null;
            }
            hidden = false;
            yield return Emerge(true);
            nextHideAt = Time.time + hideEvery;
        }

        /// <summary>The Crystal Disruptor found it: it comes back up (harmlessly) and the fight goes on.</summary>
        [ContextMenu("Disrupt")] // testing without the scanner
        public void Disrupt()
        {
            if (hidden && !IsDead) disrupted = true;
        }

        /// <summary>Up out of its crystal: no damage either way until it has landed, and a small shove for a close player.</summary>
        private IEnumerator Emerge(bool shove)
        {
            Health.Invulnerable = true;
            Vector3 at = transform.position;
            HideDisguise(true);
            Vector3 face = BossFx.PlayerFeet - at;
            face.y = 0f;
            if (face.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(face);
            SetBodyHidden(false);
            Play("BurrowOut", 0.02f);
            Sfx.PlayAt("stone_rise", at, 1f, 0.6f, 60f);
            yield return Until("erupt", 0);
            CameraShake.Add(0.35f);
            Sfx.PlayAt("crab_snip", at, 1f, 0.35f, 60f);
            Dust(at + Vector3.up * 0.4f, 30, 1.8f, 7f);
            for (int i = 0; i < 12; i++)
                BossPuff.Spawn(puffMesh, crystalMaterial, at + Vector3.up + Random.insideUnitSphere, Random.onUnitSphere * 3f + Vector3.up * 5f,
                    1.4f, Random.Range(0.15f, 0.35f), 0.08f, 16f);
            if (shove && TargetAlive && BossFx.FlatDistanceToPlayer(at) < emergeShoveReach)
                ShovePlayer(BossFx.PlayerFeet - at, emergeShove);
            yield return Until("recovered", 0, () => Track(120f));
            Health.Invulnerable = false;
            yield return Finish();
        }

        private CrystalBatch PickHideBatch()
        {
            var options = new List<CrystalBatch>();
            CrystalBatch fallback = null;
            foreach (CrystalBatch batch in FindObjectsByType<CrystalBatch>())
            {
                if (disguise != null && batch.gameObject == disguise) continue;
                if (batch.CrystalMaterial == null || Flat(batch.transform.position - Home) > ArenaRadius + 6f) continue;
                if (batch == lastHideBatch) { fallback = batch; continue; }
                if (BossFx.FlatDistanceToPlayer(batch.transform.position) < hideAwayFromPlayer) { fallback ??= batch; continue; }
                options.Add(batch);
            }
            return options.Count > 0 ? options[Random.Range(0, options.Count)] : fallback;
        }

        // A couple of metres in front of the batch, toward the middle, so the fake crystal reads as part of the group.
        private Vector3 HideSpot(CrystalBatch batch)
        {
            Vector3 spot;
            if (batch != null)
            {
                Vector3 inward = Home - batch.transform.position;
                inward.y = 0f;
                spot = batch.transform.position + (inward.sqrMagnitude > 0.01f ? inward.normalized : Vector3.forward) * 2.2f;
            }
            else spot = Home + (Vector3)(Random.insideUnitCircle * ArenaRadius * 0.6f).XZ();
            spot = ClampToArena(spot, 2f);
            return NavMesh.SamplePosition(spot, out NavMeshHit hit, 4f, NavMesh.AllAreas) ? hit.position : spot;
        }

        private CrystalBatch NearestBatch(Vector3 point, CrystalBatch skip)
        {
            CrystalBatch best = null;
            float bestDistance = float.MaxValue;
            foreach (CrystalBatch batch in FindObjectsByType<CrystalBatch>())
            {
                if (batch == skip || (disguise != null && batch.gameObject == disguise) || batch.CrystalMaterial == null) continue;
                float d = Flat(batch.transform.position - point);
                if (d < bestDistance) { bestDistance = d; best = batch; }
            }
            return best;
        }

        /// <summary>Its body crystals (and lights) take a batch's colour.</summary>
        private void Recolour(Material material)
        {
            if (material == null) return;
            FindCrystalSlot();
            if (body != null && crystalSlot >= 0)
            {
                Material[] mats = body.sharedMaterials;
                mats[crystalSlot] = material;
                body.sharedMaterials = mats;
            }
            crystalMaterial = material;
            Color glow = CrystalBatch.GlowColor(material);
            foreach (Light l in crystalLights)
                if (l != null) l.color = glow;
        }

        // The fake crystal is a real batch, dressed and lit like the one it hides beside.
        private void ShowDisguise(Vector3 at, CrystalBatch like)
        {
            if (disguisePrefab == null) return;
            if (disguise == null)
            {
                disguise = Instantiate(disguisePrefab);
                disguise.name = "Cave Axolotl (hiding)";
                disguiseLight = disguise.GetComponentInChildren<Light>();
            }
            disguise.transform.SetPositionAndRotation(BossFx.Ground(at), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            float scale = like != null ? like.transform.lossyScale.x * 0.9f : 1f;
            disguise.transform.localScale = Vector3.one * scale;
            foreach (MeshRenderer r in disguise.GetComponentsInChildren<MeshRenderer>(true)) r.sharedMaterial = crystalMaterial;
            Light likeLight = like != null ? like.GetComponentInChildren<Light>() : null;
            if (disguiseLight != null)
            {
                disguiseLight.color = CrystalBatch.GlowColor(crystalMaterial);
                if (likeLight != null)
                {
                    disguiseLight.range = likeLight.range;
                    disguiseLight.intensity = likeLight.intensity;
                }
            }
            disguise.SetActive(true);
        }

        private void HideDisguise(bool shatter)
        {
            if (disguise == null || !disguise.activeSelf) return;
            if (shatter)
                for (int i = 0; i < 10; i++)
                    BossPuff.Spawn(puffMesh, crystalMaterial, disguise.transform.position + Vector3.up * 0.6f + Random.insideUnitSphere * 0.6f,
                        Random.onUnitSphere * 4f + Vector3.up * 4f, 1.2f, Random.Range(0.12f, 0.3f), 0.05f, 16f);
            disguise.SetActive(false);
        }

        private void SetBodyHidden(bool on)
        {
            SetRenderersVisible(!on);
            SetColliders(!on);
            foreach (Light l in crystalLights)
                if (l != null) l.enabled = !on;
        }

        // ---------- Reset, death, animation ----------

        protected override void OnDuelReset()
        {
            StopCaveIns();
            hidden = disrupted = hideQueued = rolling = dazed = false;
            nextHideAt = nextCaveIn = float.PositiveInfinity;
            glowBoost = 1f;
            Health.Invulnerable = false;
        }

        protected override void OnDuelDeath()
        {
            StopCaveIns();
            HideDisguise(false);
            Health.Invulnerable = false;
            foreach (Light l in crystalLights)
                if (l != null) l.enabled = false;
            if (mainPart == null) return;
            // The Flight Control Module comes free: picking it up clears the Crystal Caves.
            Transform part = mainPart;
            mainPart = null;
            part.SetParent(null, true);
            foreach (Renderer r in part.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
            foreach (Collider c in part.GetComponentsInChildren<Collider>(true)) Destroy(c);
            foreach (Health h in part.GetComponentsInChildren<Health>(true)) Destroy(h);
            EngineCorePickup.Create(part, MainPart, ClampToArena(transform.position + transform.forward * 3f), "NextStage", Stage);
        }

        protected override void OnMoveEnded()
        {
            flare = 0f;
            rolling = dazed = false;
            spin = 0f;
            if (IsDead) return;
            Health.Invulnerable = false;
            if (!hidden) return;
            // A hide cut short (a reset): come back up.
            hidden = false;
            HideDisguise(false);
            SetBodyHidden(false);
        }

        protected override void AnimateBoss(float dt)
        {
            base.AnimateBoss(dt);
            for (int i = 0; i < crystalLights.Length; i++)
                if (crystalLights[i] != null) crystalLights[i].intensity = lightBase[i] * glowBoost * (1f + 2f * flare);
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (!rolling || IsDead) return;
            // The ball spins forward as it rolls (on top of the curled pose).
            spin += rollSpeed * Tempo / BallRadius * Mathf.Rad2Deg * Time.deltaTime;
            Transform pivot = Bone("Pivot");
            if (pivot != null) pivot.rotation = Quaternion.AngleAxis(spin, transform.right) * pivot.rotation;
        }

        private void Dust(Vector3 at, int count, float size, float speed) =>
            ParticleBurst.Spawn(ParticleBurst.Kind.Sand, at, Vector3.up, count, size, speed, 45f, dustColor);

        private void StopCaveIns()
        {
            foreach (Coroutine c in caveIns)
                if (c != null) StopCoroutine(c);
            caveIns.Clear();
            foreach (GameObject bit in caveInBits)
                if (bit != null) Destroy(bit);
            caveInBits.Clear();
        }

        // ---------- Tail swipe ----------

        private IEnumerator TailSwipe()
        {
            Play("TailSwipe");
            float from = Moment("hit_start"), to = Moment("hit_end");
            // Turning away with the crystal tail cocked (its crystals flare) is the warning.
            yield return UntilTime(from - 0.1f, () => { Track(90f); flare = Mathf.MoveTowards(flare, 1f, Time.deltaTime * 2f); });
            Sfx.PlayAt("chain_swish", transform.position, 1f, 0.5f, 50f);
            yield return StrikeWindow(from, to, ClosestTail, 1.7f, swipeDamage, swipeKnockback);
            flare = 0f;
            yield return Finish();
        }

        private Vector3 ClosestTail()
        {
            Vector3 best = BonePosition("TailTip");
            float d = BossFx.DistanceToPlayer(best);
            foreach (string bone in new[] { "Tail4", "Tail3", "Tail2" })
            {
                Vector3 p = BonePosition(bone);
                float pd = BossFx.DistanceToPlayer(p);
                if (pd < d) { d = pd; best = p; }
            }
            return best;
        }

        // ---------- Roll ----------

        /// <summary>
        /// Curls into a ball, shows its lane as a line of rings (it aims at the player, then locks), and rolls down it
        /// untouchable. Phase 3+ rolls twice. Hitting a wall or spire ends it: dazed, gills exposed.
        /// </summary>
        private IEnumerator Roll(Ref<bool> crashed)
        {
            Health.Invulnerable = true;
            LookDuringMove(false);
            Play("RollStart");
            Sfx.PlayAt("stone_rise", transform.position, 0.7f, 0.8f, 40f);
            yield return Until("curled", 0, () => { Track(140f); flare = Mathf.MoveTowards(flare, 1f, Time.deltaTime * 2f); });

            int rolls = DuelPhase >= 3 ? 2 : 1;
            for (int n = 0; n < rolls; n++)
            {
                // The lane: rings every 2 m from it toward the player; they follow the player, then lock.
                var markers = new List<GroundMarker>();
                float warning = (n == 0 ? rollWarning : rollWarning * 0.6f) / Tempo, t = 0f;
                Vector3 dir = transform.forward;
                float length = rollLength;
                bool locked = false;
                if (n > 0) Play("Roll", 0.05f);
                while (t < warning)
                {
                    t += Time.deltaTime;
                    if (!locked)
                    {
                        Vector3 to = BossFx.PlayerFeet - transform.position;
                        to.y = 0f;
                        if (to.sqrMagnitude > 0.01f) dir = to.normalized;
                        length = LaneLength(dir, to.magnitude);
                        transform.rotation = Quaternion.LookRotation(dir);
                        locked = t > warning * 0.6f;
                    }
                    int count = Mathf.Max(2, Mathf.CeilToInt(length / 2f));
                    while (markers.Count < count) markers.Add(Marker(transform.position, 1.2f));
                    for (int i = 0; i < markers.Count; i++)
                    {
                        bool used = i < count;
                        markers[i].gameObject.SetActive(used);
                        if (!used) continue;
                        markers[i].MoveTo(GroundBelow(transform.position + dir * (2f + i * (length - 2f) / Mathf.Max(1, count - 1))));
                        markers[i].Progress = t / warning;
                    }
                    if (Emit("rev", 12f) > 0) Dust(transform.position - dir, 3, 0.8f, 3f);
                    yield return null;
                }

                Play("Roll", 0.05f);
                rolling = true;
                Sfx.PlayAt("stone_rise", transform.position, 1f, 0.5f, 60f);
                bool hit = false, blocked = false;
                // Its own loop, not Travel: Agent.Move slides along NavMesh edges, so a glancing roll would never stop.
                for (float done = 0f; done < length && !blocked; )
                {
                    float step = Mathf.Min(rollSpeed * Tempo * Time.deltaTime, length - done);
                    Vector3 from = transform.position;
                    if (NavMesh.Raycast(from, from + dir * (step + 0.4f), out _, NavMesh.AllAreas)) { blocked = true; break; }
                    if (AgentReady) Agent.Move(dir * step);
                    else transform.position += dir * step;
                    done += step;
                    if (!hit && Strike(BonePosition("Pivot"), rollHitRadius, rollDamage, 0f))
                    {
                        hit = true;
                        Vector3 side = Vector3.Cross(Vector3.up, dir);
                        if (Vector3.Dot(BossFx.PlayerFeet - transform.position, side) < 0f) side = -side;
                        ShovePlayer(side + dir * 0.5f, rollKnockback);
                    }
                    if (Emit("roll", 18f) > 0) Dust(transform.position, 4, 0.9f, 3f);
                    yield return null;
                }
                foreach (GroundMarker m in markers)
                    if (m != null) m.gameObject.SetActive(false);
                rolling = false;
                spin = 0f;

                if (blocked)
                {
                    if (crashed != null) crashed.Value = true;
                    yield return Crash();
                    yield break;
                }
            }
            Play("RollEnd", 0.08f);
            flare = 0f;
            yield return Finish(() => Track(90f));
        }

        // As far as the player plus a bit, but not past the arena's edge.
        private float LaneLength(Vector3 dir, float toPlayer)
        {
            float want = Mathf.Min(rollLength, toPlayer + 6f);
            Vector3 from = transform.position - Home;
            from.y = 0f;
            float limit = ArenaRadius + 2f;
            // Solve |from + dir * d| = limit for d.
            float b = Vector3.Dot(from, dir), c = from.sqrMagnitude - limit * limit;
            float disc = b * b - c;
            float reach = disc > 0f ? -b + Mathf.Sqrt(disc) : 0f;
            return Mathf.Clamp(Mathf.Min(want, reach), 3f, rollLength);
        }

        private IEnumerator Crash()
        {
            Health.Invulnerable = false;
            dazed = true;
            flare = 0f;
            Play("RollCrash", 0.05f);
            CameraShake.Add(0.4f);
            Sfx.PlayAt("wood_smash", transform.position, 1f, 0.45f, 60f);
            Dust(transform.position + transform.forward + Vector3.up * 0.8f, 26, 1.6f, 6f);
            MiniBossHud.Message("IT'S DAZED!  Hit the gills!", new Color(1f, 0.85f, 0.5f), 2f);
            yield return Until("recovered", 0);
            dazed = false;
            yield return Finish();
        }

        // ---------- Shard ring (phase 2+) ----------

        private IEnumerator ShardRing()
        {
            LookDuringMove(false);
            Play("ShardRing");
            // Arching with its crystals standing up and glowing brighter is the warning.
            yield return Until("fire", 0, () => { Track(60f); flare = Mathf.MoveTowards(flare, 2f, Time.deltaTime * 2.5f); });
            int rings = DuelPhase >= 4 ? 2 : 1;
            for (int r = 0; r < rings; r++)
            {
                FireRing(r * 0.5f);
                if (r < rings - 1) yield return Seconds(0.6f / Tempo);
            }
            flare = 0f;
            yield return Finish();
        }

        private void FireRing(float offset)
        {
            Vector3 centre = transform.position;
            Vector3 floor = GroundBelow(centre);
            Sfx.PlayAt("crab_snip", centre, 1f, 0.5f, 60f);
            Sfx.PlayAt("chain_swish", centre, 1f, 0.8f, 60f);
            for (int i = 0; i < shardCount; i++)
            {
                float a = (i + offset) * Mathf.PI * 2f / shardCount;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 from = floor + dir * 1.8f + Vector3.up * shardHeight;
                ThornShot.Fire(spikeMesh, crystalMaterial, from, dir, shardSpeed * Tempo, shardDamage * DamageScale, gameObject, 2f);
            }
            for (int k = 0; k < 8; k++)
                BossPuff.Spawn(puffMesh, crystalMaterial, BonePosition("Spine") + Vector3.up, Random.onUnitSphere * 3f + Vector3.up * 3f,
                    0.8f, Random.Range(0.1f, 0.2f), 0.05f, 16f);
        }

        // ---------- Frenzy (phase 4): a roll straight into a shard ring ----------

        private IEnumerator Frenzy()
        {
            var crashed = new Ref<bool>();
            yield return Roll(crashed);
            if (crashed.Value) yield break;
            Health.Invulnerable = false;
            yield return ShardRing();
        }

        // ---------- Arena: Crystal Bloom (phase 2) ----------

        /// <summary>Crystal spires grow up round the arena: cover from the shards, and walls its rolls crash into.</summary>
        private IEnumerator Bloom()
        {
            if (spireMesh == null || spireCount == 0) yield break;
            CameraShake.Add(0.3f);
            var spires = new List<Transform>();
            float offset = Random.value * 360f;
            for (int i = 0; i < spireCount; i++)
            {
                float angle = offset + i * 360f / spireCount + Random.Range(-12f, 12f);
                Vector3 at = Home + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * (ArenaRadius * Random.Range(0.45f, 0.65f));
                at = GroundBelow(at);
                if (BossFx.FlatDistanceToPlayer(at) < 2.5f || Flat(at - transform.position) < 3f) continue;
                float scale = spireHeight / Mathf.Max(0.01f, spireMesh.bounds.size.y);
                GameObject spire = BossFx.Prop("Bloom Spire", spireMesh, crystalMaterial, at,
                    Quaternion.Euler(Random.Range(-6f, 6f), Random.Range(0f, 360f), Random.Range(-6f, 6f)), Vector3.one * scale, true);
                var box = spire.AddComponent<BoxCollider>();
                box.center = spireMesh.bounds.center;
                box.size = spireMesh.bounds.size;
                var obstacle = spire.AddComponent<NavMeshObstacle>();
                obstacle.center = box.center;
                obstacle.size = box.size;
                obstacle.carving = true;
                spire.transform.localScale = new Vector3(scale, scale * 0.05f, scale);
                Keep(spire.transform);
                spires.Add(spire.transform);
                Dust(at + Vector3.up * 0.3f, 14, 1.2f, 4f);
            }
            Sfx.PlayAt("stone_rise", Home, 1f, 0.6f, 80f);
            float full = spireHeight / Mathf.Max(0.01f, spireMesh.bounds.size.y);
            for (float t = 0f; t < 1.2f; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0.05f, 1f, t / 1.2f);
                foreach (Transform s in spires)
                    if (s != null) s.localScale = new Vector3(full, full * k, full);
                yield return null;
            }
            foreach (Transform s in spires)
                if (s != null) s.localScale = Vector3.one * full;
        }

        // ---------- Arena: Cave-In (phase 3+) ----------

        /// <summary>Crystals shake loose from the ceiling onto marked rings: the first where the player is heading.</summary>
        private IEnumerator CaveIn()
        {
            var spots = new List<Vector3>();
            Vector3 lead = BossFx.PlayerFeet;
            if (BossFx.PlayerBody != null) lead += BossFx.PlayerBody.velocity * 0.4f;
            spots.Add(GroundBelow(lead, 4f));
            for (int i = 1; i < caveInCount; i++)
                spots.Add(ClampToArena(BossFx.PlayerFeet + (Vector3)(Random.insideUnitCircle * 6f).XZ(), 2f));
            Sfx.PlayAt("wood_smash", BossFx.PlayerFeet + Vector3.up * 6f, 0.6f, 0.4f, 60f);

            var markers = new List<GroundMarker>();
            var tops = new List<Vector3>();
            var shards = new Transform[spots.Count];
            foreach (Vector3 spot in spots)
            {
                markers.Add(Keep(GroundMarker.Spawn(markerMaterial, null, spot, caveInRadius)));
                caveInBits.Add(markers[markers.Count - 1].gameObject);
                tops.Add(spot + Vector3.up * (Physics.Raycast(spot + Vector3.up * 0.5f, Vector3.up, out RaycastHit roof, 15f, sightMask,
                    QueryTriggerInteraction.Ignore) ? roof.distance : 8f));
            }
            var landed = new bool[spots.Count];
            float stagger = 0.2f, warning = caveInWarning / Tempo, fall = 0.35f, t = 0f;
            while (t < warning + stagger * spots.Count + 0.05f)
            {
                t += Time.deltaTime;
                for (int i = 0; i < spots.Count; i++)
                {
                    float local = t - i * stagger;
                    if (local < 0f || landed[i]) continue;
                    if (markers[i] != null) markers[i].Progress = local / warning;
                    if (local >= warning - fall && shards[i] == null)
                        shards[i] = Keep(BossFx.Prop("Falling Crystal", spikeMesh, crystalMaterial, tops[i], Quaternion.Euler(90f, 0f, 0f),
                            new Vector3(0.5f, 0.5f, 1.6f), true).transform);
                    if (shards[i] != null && !caveInBits.Contains(shards[i].gameObject)) caveInBits.Add(shards[i].gameObject);
                    if (shards[i] != null)
                        shards[i].position = Vector3.Lerp(tops[i], spots[i] + Vector3.up * 1.6f, Mathf.Clamp01((local - (warning - fall)) / fall));
                    if (local < warning) continue;
                    landed[i] = true;
                    Strike(spots[i] + Vector3.up, caveInRadius, caveInDamage, 1.5f);
                    Sfx.PlayAt("crab_snip", spots[i], 1f, 0.3f, 40f);
                    for (int k = 0; k < 6; k++)
                        BossPuff.Spawn(puffMesh, crystalMaterial, spots[i] + Vector3.up * 0.5f, Random.onUnitSphere * 3f + Vector3.up * 3f,
                            0.9f, Random.Range(0.12f, 0.25f), 0.05f, 16f);
                    if (shards[i] != null) Destroy(shards[i].gameObject);
                    if (markers[i] != null) Destroy(markers[i].gameObject);
                }
                yield return null;
            }
        }
    }
}
