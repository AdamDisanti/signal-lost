using System;
using System.Collections;
using System.Collections.Generic;
using SignalLost.Audio;
using SignalLost.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace SignalLost.Enemies.Desert
{
    /// <summary>
    /// Base for the revamped desert bosses (Briarwing, Cindermaw, Pitchclaw; docs/desert-bosses.md): Souls-leaning duels
    /// with several phases, readable wind-ups and punishable recoveries.
    ///
    /// What it adds to <see cref="DesertMiniBoss"/> (health bar, leash, reset, ship-part drop):
    /// - <b>Phases</b> at the health fractions in Phase At. Each change is a short roar the boss can't be interrupted out
    ///   of, then <see cref="OnDuelPhase"/>.
    /// - <b>Animation</b> from the Blender-made model: an Animator state per clip, cross-faded. Attacks wait for named moments
    ///   in the clip table (<see cref="BossClipTable"/>), so hits land on the frame the animation strikes.
    /// - <b>Attacks</b> as coroutines, picked by range, angle, phase, weight and cooldown (<see cref="Attack"/>), with a short,
    ///   phase-dependent breather between them in which it stalks the player.
    /// - <b>Poise</b>: enough damage in a short time staggers it out of an interruptible attack (taking extra damage).
    /// - <b>Tempo</b>: later phases play everything a little faster.
    /// - Its head turns to follow the player between attacks.
    /// </summary>
    public abstract class DuelBoss : DesertMiniBoss
    {
        [Header("Duel")]
        [Tooltip("<Boss>Clips.json from tools/blender/desert_bosses.py: clip lengths and attack timings.")]
        [SerializeField] private TextAsset clipTable;
        [SerializeField] private Animator animator;
        [Tooltip("Health fractions where phases 2, 3 (and 4) begin, highest first.")]
        [SerializeField] private float[] phaseAt = { 0.65f, 0.3f };
        [Tooltip("Shown on the boss bar and the phase banner, one per phase.")]
        [SerializeField] private string[] phaseTitles = { "", "", "" };
        [Tooltip("Animation and attack speed in each phase (1 = as animated).")]
        [SerializeField] private float[] phaseTempo = { 1f, 1.06f, 1.12f, 1.18f };
        [Tooltip("Seconds between attacks in each phase (a random amount between this and 1.6x it).")]
        [SerializeField] private float[] phaseBreather = { 1.1f, 0.85f, 0.6f, 0.45f };
        [Tooltip("Multiplies every attack's damage (difficulty tuning).")]
        [SerializeField, Min(0f)] private float damageScale = 1f;

        [Header("Poise")]
        [Tooltip("Damage it soaks before staggering out of an interruptible attack.")]
        [SerializeField, Min(1f)] private float poise = 140f;
        [Tooltip("Seconds without being hit before its poise recovers.")]
        [SerializeField, Min(0f)] private float poiseRecovery = 4f;
        [SerializeField, Min(0.2f)] private float staggerTime = 1.5f;

        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float walkSpeed = 3.2f;
        [SerializeField, Min(0.1f)] private float runSpeed = 7.5f;
        [Tooltip("Degrees a second it turns while tracking the player.")]
        [SerializeField, Min(10f)] private float turnSpeed = 140f;
        [Tooltip("The distance it likes to keep while stalking between attacks.")]
        [SerializeField, Min(1f)] private float stalkDistance = 7f;

        [Header("Head tracking")]
        [Tooltip("Neck to head: they turn toward the player between attacks, shared out along the chain.")]
        [SerializeField] private Transform[] lookBones = new Transform[0];
        [SerializeField, Range(0f, 80f)] private float lookLimit = 45f;

        [Header("Effects")]
        [SerializeField] protected Material markerMaterial;
        [SerializeField] protected Material shadowMaterial;
        [SerializeField] protected Mesh puffMesh;
        [SerializeField] protected Mesh spikeMesh;

        protected BossClipTable Table { get; private set; }
        public int DuelPhase { get; private set; } = 1;
        public override bool HasPhaseTwo => false;
        public override int DisplayPhase => DuelPhase;
        public override float[] PhaseMarks => phaseAt;
        public override string PhaseTitle => DuelPhase - 1 < phaseTitles.Length ? phaseTitles[DuelPhase - 1] : "";
        public override bool InPhaseShift => shifting;
        public int PhaseCount => phaseAt.Length + 1;
        /// <summary>The attack running now (null between attacks): for tests and the HUD.</summary>
        public string CurrentMove { get; private set; }
        public bool Staggered => staggered;
        protected float Tempo => phaseTempo.Length == 0 ? 1f : phaseTempo[Mathf.Clamp(DuelPhase - 1, 0, phaseTempo.Length - 1)];
        protected float DamageScale => damageScale;
        protected float WalkSpeed => walkSpeed;
        protected float RunSpeed => runSpeed;
        protected Animator Anim => animator;

        protected sealed class Attack
        {
            public string Name;
            public Func<IEnumerator> Run;
            public float MinRange, MaxRange = 99f;
            /// <summary>Only when the player is within this many degrees of straight ahead (180 = anywhere).</summary>
            public float MaxAngle = 180f;
            /// <summary>Only when the player is at least this many degrees off straight ahead (beside or behind it).</summary>
            public float MinAngle;
            public int MinPhase = 1, MaxPhase = 9;
            public float Weight = 1f, Cooldown = 3f;
            public Func<bool> When;
            /// <summary>Big committed attacks: damage doesn't stagger it out of them.</summary>
            public bool HyperArmor;
            internal float ReadyAt;
        }

        protected readonly List<Attack> Attacks = new List<Attack>();
        private readonly List<GameObject> hazards = new List<GameObject>();
        private readonly List<GameObject> moveTemp = new List<GameObject>();
        private readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
        private Coroutine move;
        private Attack running;
        private string lastAttack, playing, locomotion;
        private float clipStart, nextDecision, poiseDamage, lastHit, staggerTaken, strafeSign = 1f, strafeUntil, lookWeight;
        private bool shifting, staggered, airborne;
        private int pendingPhase;

        // ---------- DesertMiniBoss hooks ----------

        protected override void OnBossStarted()
        {
            Table = BossClipTable.Parse(clipTable);
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator != null) animator.applyRootMotion = false;
            Attacks.Clear();
            DefineAttacks();
            Play("Idle", 0f, false);
        }

        protected override void OnEngaged()
        {
            nextDecision = Time.time + 0.5f;
            MiniBossHud.Message(DisplayName.ToUpperInvariant() + "\n" + SignalLost.Core.KeyBindings.Hint("dodge") + " to dodge",
                new Color(1f, 0.85f, 0.5f), 3.5f);
            Run(Intro(), "Intro", null);
        }

        protected override void OnPhaseTwo() { }

        protected override void TickCombat(float dt)
        {
            UpdateShove(dt);
            if (pendingPhase > DuelPhase && !shifting && !airborne)
            {
                Run(PhaseShift(pendingPhase), "PhaseShift", null);
                return;
            }
            if (Time.time - lastHit > poiseRecovery) poiseDamage = 0f;
            if (move != null || !TargetAlive) return;
            Decide();
        }

        protected override void UpdatePhaseShift(float dt) => UpdateShove(dt);

        protected override void OnBossHit(float amount, GameObject source, Vector3 away)
        {
            lastHit = Time.time;
            int target = 1;
            foreach (float at in phaseAt)
                if (Health.Normalized <= at) target++;
            if (target > DuelPhase) pendingPhase = Mathf.Max(pendingPhase, target);
            poiseDamage += amount * (IsMeleeHit(source) ? 1.25f : 1f);
            if (poiseDamage >= poise && !shifting && !staggered && !airborne && (running == null || !running.HyperArmor))
            {
                poiseDamage = 0f;
                Run(Stagger(staggerTime), "Stagger", null);
            }
        }

        protected override void OnReset()
        {
            CancelMove();
            if (AgentReady) Agent.Warp(Home);
            ClearHazards();
            DuelPhase = 1;
            pendingPhase = 0;
            poiseDamage = 0f;
            shifting = staggered = false;
            foreach (Attack a in Attacks) a.ReadyAt = 0f;
            OnDuelReset();
            Play("Idle", 0.2f, false);
        }

        protected override void OnBossDeath()
        {
            CancelMove();
            // The stolen part rides hidden inside the model; it shows as it drops.
            if (ShipPart != null)
                foreach (Renderer r in ShipPart.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
            foreach (GameObject h in hazards)
                if (h != null && h.GetComponent<SmokeCloud>() is SmokeCloud c) c.Clear();
            OnDuelDeath();
            if (Table != null && Table.Has("Death")) Play("Death", 0.15f, false);
            Sfx.PlayAt("kong_roar", transform.position, 0.8f, 0.6f, 80f);
        }

        protected override void UpdateDeath(float t)
        {
            float sink = Mathf.Clamp01((t - CorpseSink) / 2.5f);
            Visual.localPosition = VisualRest + Vector3.down * (sink * 3f);
        }

        /// <summary>Seconds after death before the body starts sinking into the sand.</summary>
        protected virtual float CorpseSink => 3.5f;

        protected override void AnimateBoss(float dt)
        {
            if (animator == null || IsDead) return;
            if (move == null && !shifting)
            {
                float speed = AgentReady ? Agent.velocity.magnitude : 0f;
                string want = speed > (walkSpeed + runSpeed) * 0.5f ? "Run" : speed > 0.35f ? "Walk" : "Idle";
                if (want != locomotion || playing != want)
                {
                    locomotion = want;
                    Play(want, 0.25f, false);
                }
                animator.speed = want == "Walk" ? Mathf.Clamp(speed / walkSpeed, 0.6f, 1.5f)
                    : want == "Run" ? Mathf.Clamp(speed / runSpeed, 0.7f, 1.4f) : 1f;
            }
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (IsDead || lookBones.Length == 0) return;
            bool free = Engaged && TargetAlive && (move == null || allowLook);
            lookWeight = Mathf.MoveTowards(lookWeight, free ? 1f : 0f, Time.deltaTime * 2.5f);
            if (lookWeight <= 0.001f || Target == null) return;
            Vector3 to = Target.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.01f) return;
            float yaw = Mathf.Clamp(Vector3.SignedAngle(transform.forward, to, Vector3.up), -lookLimit, lookLimit) * lookWeight;
            float share = yaw / lookBones.Length;
            foreach (Transform bone in lookBones)
                if (bone != null) bone.rotation = Quaternion.AngleAxis(share, Vector3.up) * bone.rotation;
        }

        private bool allowLook;

        // ---------- For each boss ----------

        /// <summary>Fill <see cref="Attacks"/>.</summary>
        protected abstract void DefineAttacks();
        /// <summary>A new phase begins (after its roar starts): change visuals, unlock moves.</summary>
        protected virtual void OnDuelPhase(int phase) { }
        protected virtual void OnDuelReset() { }
        protected virtual void OnDuelDeath() { }
        /// <summary>A move ended or was cut short: put anything it changed back (glows, airborne state, sounds).</summary>
        protected virtual void OnMoveEnded() { }
        /// <summary>The roar when the fight starts.</summary>
        protected virtual IEnumerator Intro() => Roar(false);

        // ---------- Moves ----------

        protected bool Busy => move != null;

        protected void Run(IEnumerator routine, string name, Attack attack)
        {
            CancelMove();
            running = attack;
            CurrentMove = name;
            Halt();
            if (Agent != null && Agent.enabled) Agent.updateRotation = false; // attacks turn it by hand
            move = StartCoroutine(Runner(routine));
        }

        private IEnumerator Runner(IEnumerator routine)
        {
            yield return routine;
            move = null;
            FinishMove();
        }

        protected void CancelMove()
        {
            if (move != null) StopCoroutine(move);
            bool had = move != null;
            move = null;
            if (had || running != null || CurrentMove != null) FinishMove();
        }

        private void FinishMove()
        {
            foreach (GameObject temp in moveTemp)
                if (temp != null) Destroy(temp);
            moveTemp.Clear();
            if (airborne) SetAirborne(false);
            DamageTakenMultiplier = 1f;
            allowLook = false;
            running = null;
            CurrentMove = null;
            staggered = false;
            shifting = false;
            if (animator != null) animator.speed = 1f;
            nextDecision = Time.time + Breather();
            OnMoveEnded();
        }

        private float Breather()
        {
            float b = phaseBreather.Length == 0 ? 1f : phaseBreather[Mathf.Clamp(DuelPhase - 1, 0, phaseBreather.Length - 1)];
            return UnityEngine.Random.Range(b, b * 1.6f);
        }

        private void Decide()
        {
            float distance = FlatDistance(Target.position);
            float angle = AngleToTarget();
            if (Time.time >= nextDecision)
            {
                Attack pick = Choose(distance, angle);
                if (pick != null)
                {
                    pick.ReadyAt = Time.time + pick.Cooldown / Tempo;
                    lastAttack = pick.Name;
                    Run(pick.Run(), pick.Name, pick);
                    return;
                }
            }
            Stalk(distance, angle);
        }

        private Attack Choose(float distance, float angle)
        {
            float total = 0f;
            var options = new List<Attack>();
            foreach (Attack a in Attacks)
            {
                if (DuelPhase < a.MinPhase || DuelPhase > a.MaxPhase || Time.time < a.ReadyAt) continue;
                if (distance < a.MinRange || distance > a.MaxRange || angle > a.MaxAngle || angle < a.MinAngle) continue;
                if (a.When != null && !a.When()) continue;
                options.Add(a);
                total += a.Name == lastAttack ? a.Weight * 0.35f : a.Weight;
            }
            if (options.Count == 0) return null;
            float roll = UnityEngine.Random.value * total;
            foreach (Attack a in options)
            {
                roll -= a.Name == lastAttack ? a.Weight * 0.35f : a.Weight;
                if (roll <= 0f) return a;
            }
            return options[options.Count - 1];
        }

        /// <summary>Between attacks: closes in when far, circles at its favourite distance when near, always facing the player.</summary>
        private void Stalk(float distance, float angle)
        {
            if (!AgentReady) return;
            Vector3 to = FlatDirectionTo(Target.position);
            if (distance > stalkDistance * 1.6f)
            {
                Agent.updateRotation = true;
                Chase(Target.position, distance > stalkDistance * 3f ? runSpeed : walkSpeed * 1.3f);
                return;
            }
            if (Time.time > strafeUntil)
            {
                strafeSign = UnityEngine.Random.value < 0.5f ? -1f : 1f;
                strafeUntil = Time.time + UnityEngine.Random.Range(1.5f, 3f);
            }
            Vector3 side = Vector3.Cross(Vector3.up, to) * strafeSign;
            Vector3 goal = Target.position - to * stalkDistance + side * 3f;
            Chase(goal, walkSpeed);
            Agent.updateRotation = false;
            TurnToward(Target.position, turnSpeed * Time.deltaTime);
        }

        // ---------- Building blocks for attacks ----------

        protected Transform Bone(string name)
        {
            if (bones.TryGetValue(name, out Transform found) && found != null) return found;
            found = FindDeep(Visual != null ? Visual : transform, name);
            bones[name] = found;
            return found;
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            foreach (Transform child in parent)
            {
                Transform hit = FindDeep(child, name);
                if (hit != null) return hit;
            }
            return null;
        }

        protected Vector3 BonePosition(string name) => Bone(name) != null ? Bone(name).position : transform.position + Vector3.up * 2f;

        /// <summary>Cross-fades to a clip. Attacks play at the phase's tempo; locomotion at its own speed.</summary>
        protected void Play(string clip, float fade = 0.12f, bool atTempo = true)
        {
            if (animator == null) return;
            playing = clip;
            if (clip != "Idle" && clip != "Walk" && clip != "Run") locomotion = null;
            clipStart = Time.time;
            animator.speed = atTempo ? Tempo : 1f;
            animator.CrossFadeInFixedTime(clip, fade);
        }

        protected string Playing => playing;
        /// <summary>Seconds into the playing clip, in the clip's own time.</summary>
        protected float ClipTime => (Time.time - clipStart) * (animator != null ? animator.speed : 1f);
        protected float Moment(string name, int index = 0) => Table.Time(playing, name, index);

        /// <summary>Waits until the playing clip reaches a time (in clip seconds), running <paramref name="each"/> every frame.</summary>
        protected IEnumerator UntilTime(float clipSeconds, Action each = null)
        {
            while (ClipTime < clipSeconds)
            {
                each?.Invoke();
                yield return null;
            }
        }

        protected IEnumerator Until(string moment, int index = 0, Action each = null) => UntilTime(Moment(moment, index), each);

        /// <summary>Waits out the rest of the clip (minus a short blend into whatever follows).</summary>
        protected IEnumerator Finish(Action each = null, float blend = 0.15f) => UntilTime(Table.Length(playing) - blend, each);

        protected IEnumerator Seconds(float seconds, Action each = null)
        {
            float end = Time.time + seconds;
            while (Time.time < end)
            {
                each?.Invoke();
                yield return null;
            }
        }

        /// <summary>Turns toward the player (degrees a second scaled by tempo).</summary>
        protected void Track(float rate = -1f)
        {
            if (Target == null) return;
            TurnToward(Target.position, (rate < 0f ? turnSpeed : rate) * Tempo * Time.deltaTime);
        }

        /// <summary>Steps forward this frame at a speed (metres a second, at tempo): the lurch behind a swing.</summary>
        protected void Lurch(float speed)
        {
            if (AgentReady) Agent.Move(transform.forward * (speed * Tempo * Time.deltaTime));
        }

        private readonly Dictionary<string, float> emitters = new Dictionary<string, float>();

        /// <summary>How many puffs an effect should emit this frame at a steady rate, whatever the frame rate.</summary>
        protected int Emit(string effect, float perSecond)
        {
            emitters.TryGetValue(effect, out float owed);
            owed += perSecond * Time.deltaTime;
            int count = Mathf.Min(8, (int)owed);
            emitters[effect] = owed - (int)owed;
            return count;
        }

        /// <summary>During a melee wind-up: turns toward the player and steps in if they're further than it wants.</summary>
        protected void CloseIn(float want, float rate = 150f)
        {
            Track(rate);
            if (Target != null && FlatDistance(Target.position) > want) Lurch(5.5f);
        }

        /// <summary>Lets the head follow the player during this move (stalks, wind-ups).</summary>
        protected void LookDuringMove(bool on) => allowLook = on;

        /// <summary>
        /// Moves it across the ground (on the NavMesh, so it stops at walls and edges) along a direction over some
        /// seconds. Returns through <paramref name="blocked"/> whether it hit something and stopped short.
        /// </summary>
        protected IEnumerator Travel(Vector3 direction, float distance, float seconds, Action<float> each = null, Ref<bool> blocked = null,
            bool ease = true)
        {
            direction.y = 0f;
            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
            float t = 0f, done = 0f, stuck = 0f;
            seconds = Mathf.Max(0.02f, seconds / Tempo);
            while (t < 1f)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / seconds);
                float want = distance * (ease ? Mathf.SmoothStep(0f, 1f, t) : t);
                float step = want - done;
                Vector3 before = transform.position;
                if (AgentReady) Agent.Move(direction * step);
                else transform.position += direction * step;
                float moved = Flat(transform.position - before);
                done = want;
                stuck = step > 0.02f && moved < step * 0.35f ? stuck + Time.deltaTime : 0f;
                each?.Invoke(t);
                if (stuck > 0.08f)
                {
                    if (blocked != null) blocked.Value = true;
                    yield break;
                }
                yield return null;
            }
        }

        /// <summary>A small box for passing results out of coroutines.</summary>
        protected sealed class Ref<T>
        {
            public T Value;
        }

        /// <summary>
        /// Hurts the player once if they come within <paramref name="radius"/> of a point that moves with the attack,
        /// between two clip times. Ground attacks (jumpable) miss a player who is in the air.
        /// </summary>
        protected IEnumerator StrikeWindow(float from, float to, Func<Vector3> centre, float radius, float damage, float knock,
            bool jumpable = false, Action each = null, Ref<bool> landed = null)
        {
            bool hit = false;
            while (ClipTime < to)
            {
                each?.Invoke();
                if (!hit && ClipTime >= from && Strike(centre(), radius, damage, knock, jumpable))
                {
                    hit = true;
                    if (landed != null) landed.Value = true;
                }
                yield return null;
            }
        }

        /// <summary>One strike now: hurts and knocks the player away if they are within reach.</summary>
        protected bool Strike(Vector3 centre, float radius, float damage, float knock, bool jumpable = false)
        {
            if (!TargetAlive) return false;
            if (jumpable && !BossFx.PlayerGrounded) return false;
            if (BossFx.DistanceToPlayer(centre) > radius) return false;
            if (!HurtPlayer(damage * damageScale)) return false;
            Vector3 away = BossFx.PlayerFeet - transform.position;
            away.y = 0f;
            if (knock > 0f) ShovePlayer(away, knock);
            return true;
        }

        /// <summary>Damage to the player (not via a strike window), scaled for difficulty.</summary>
        protected bool Hurt(float damage) => HurtPlayer(damage * damageScale);

        /// <summary>Keeps a hazard to clear on reset.</summary>
        protected T Keep<T>(T thing) where T : Component
        {
            if (thing != null) hazards.Add(thing.gameObject);
            if (hazards.Count > 300) hazards.RemoveAll(h => h == null);
            return thing;
        }

        /// <summary>Something that lasts only while the current move runs (markers, glows).</summary>
        protected T Temp<T>(T thing) where T : Component
        {
            if (thing != null) moveTemp.Add(thing.gameObject);
            return thing;
        }

        protected GroundMarker Marker(Vector3 point, float radius, bool shadow = false) =>
            Temp(GroundMarker.Spawn(markerMaterial, shadow ? shadowMaterial : null, point, radius));

        private void ClearHazards()
        {
            foreach (GameObject h in hazards)
            {
                if (h == null) continue;
                if (h.GetComponent<SmokeCloud>() is SmokeCloud cloud) cloud.Clear();
                else Destroy(h);
            }
            hazards.Clear();
        }

        /// <summary>Leaves the NavMesh to fly or leap (moved by hand), or lands back on it.</summary>
        protected void SetAirborne(bool on)
        {
            if (on == airborne || Agent == null) return;
            airborne = on;
            if (on)
            {
                if (AgentReady) Halt();
                Agent.enabled = false;
                return;
            }
            Vector3 ground = GroundBelow(transform.position, 1f);
            transform.position = ground;
            Agent.enabled = true;
            if (NavMesh.SamplePosition(ground, out NavMeshHit hit, 6f, NavMesh.AllAreas)) Agent.Warp(hit.position);
        }

        protected bool Airborne => airborne;

        /// <summary>A point on the ground near the player, kept inside the arena.</summary>
        protected Vector3 ClampToArena(Vector3 point, float margin = 3f)
        {
            Vector3 offset = point - Home;
            offset.y = 0f;
            float max = Mathf.Max(2f, ArenaRadius - margin);
            if (offset.magnitude > max) point = Home + offset.normalized * max + Vector3.up * point.y;
            return GroundBelow(point, 4f);
        }

        // ---------- Shared moves ----------

        /// <summary>The roar: rears up, pushes a close player back. With a banner when a phase begins.</summary>
        protected IEnumerator Roar(bool phaseBanner)
        {
            LookDuringMove(false);
            Play("Roar", 0.2f, false);
            yield return Until("roar", 0, () => Track(90f));
            Sfx.PlayAt("kong_roar", transform.position, 1f, phaseBanner ? 0.75f : 0.85f, 90f);
            if (Target != null && FlatDistance(Target.position) < 8f)
                ShovePlayer(Target.position - transform.position, 4f);
            yield return Finish();
        }

        private IEnumerator PhaseShift(int phase)
        {
            shifting = true;
            DuelPhase = phase;
            pendingPhase = 0;
            string title = PhaseTitle;
            MiniBossHud.Message(string.IsNullOrEmpty(title) ? DisplayName.ToUpperInvariant() : DisplayName.ToUpperInvariant() + ": " + title.ToUpperInvariant(),
                new Color(1f, 0.55f, 0.25f), 3f);
            OnDuelPhase(phase);
            yield return Roar(true);
        }

        private IEnumerator Stagger(float seconds)
        {
            staggered = true;
            DamageTakenMultiplier = 1.3f;
            Play("Stagger", 0.08f, false);
            Sfx.PlayAt("crab_whimper", transform.position, 0.8f, 0.5f, 50f);
            float length = Table.Length("Stagger");
            yield return Seconds(Mathf.Max(seconds, length * 0.9f));
        }

        /// <summary>Staggers it now, even out of a big attack (a weak spot's backfire). Not while airborne or roaring.</summary>
        protected void ForceStagger(float seconds)
        {
            if (shifting || airborne || IsDead) return;
            poiseDamage = 0f;
            Run(Stagger(seconds), "Stagger", null);
        }

        /// <summary>For tests: forces the next phase change now.</summary>
        public void DebugEnterPhase(int phase) => pendingPhase = Mathf.Clamp(phase, 1, PhaseCount);

        /// <summary>For tests: starts a named attack now, if it exists and the phase allows it.</summary>
        public bool DebugRunAttack(string name)
        {
            foreach (Attack a in Attacks)
                if (a.Name == name)
                {
                    if (!Engaged) Engage();
                    Run(a.Run(), a.Name, a);
                    return true;
                }
            return false;
        }

        public IEnumerable<string> AttackNames
        {
            get
            {
                foreach (Attack a in Attacks) yield return a.Name;
            }
        }
    }
}
