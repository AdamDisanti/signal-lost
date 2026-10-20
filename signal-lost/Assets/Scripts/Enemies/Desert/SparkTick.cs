using System.Collections.Generic;
using SignalLost.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace SignalLost.Enemies.Desert
{
    /// <summary>
    /// Swarm fodder: a palm-sized sand tick bristling with stripped copper wire from the ship. They come in packs,
    /// skitter at the player in zigzags, and leap at them from a few metres away. A leap that connects latches on:
    /// the tick clings to the player's visor (visible on screen) and chips away at their health until shaken off.
    ///
    /// Counterplay: one hit of anything kills a tick on the ground, so the wide side bash clears a pack. Latched ticks
    /// can't be hit; a melee swing swats off up to two of them, and jumping throws them all off (they land dazed).
    /// Sidestepping a leap makes it miss and land dazed. All numbers are placeholders.
    /// </summary>
    public sealed class SparkTick : DesertFodder
    {
        private enum State { Idle, Chase, Leap, Latched, Dazed }

        private static readonly List<SparkTick> Latched = new List<SparkTick>();
        private static int swatFrame = -1, swatsThisFrame;

        /// <summary>How many ticks are clinging to the player right now (for the on-screen hint).</summary>
        public static int LatchedCount => Latched.Count;

        // Where latched ticks sit on the view, in metres right/up of the screen centre, 0.55 m in front of the camera.
        private static readonly Vector2[] Slots =
        {
            new Vector2(-0.24f, -0.17f), new Vector2(0.24f, -0.17f), new Vector2(-0.3f, 0.1f), new Vector2(0.3f, 0.1f),
        };

        [Header("Skitter")]
        [SerializeField, Min(0.1f)] private float runSpeed = 6.2f;
        [Tooltip("How far it weaves to each side while running in.")]
        [SerializeField, Min(0f)] private float zigzagWidth = 2.2f;
        [SerializeField, Min(0.1f)] private float zigzagRate = 2.6f;

        [Header("Leap")]
        [SerializeField, Min(0.5f)] private float leapRange = 4f;
        [SerializeField, Min(0.1f)] private float leapTime = 0.45f;
        [SerializeField, Min(0f)] private float leapHeight = 1.1f;
        [Tooltip("Pause after landing before it can leap again.")]
        [SerializeField, Min(0f)] private float leapCooldown = 1.1f;
        [Tooltip("Seconds dazed after a missed leap or being shaken off.")]
        [SerializeField, Min(0f)] private float dazeTime = 0.9f;

        [Header("Latched on")]
        [SerializeField, Min(0f)] private float latchDamage = 2f;
        [SerializeField, Min(0.05f)] private float latchInterval = 0.5f;
        [Tooltip("No more than this many can cling on at once; extra leaps bounce off.")]
        [SerializeField, Range(1, 4)] private int maxLatched = 4;
        [Tooltip("Ticks knocked off by one melee swing.")]
        [SerializeField, Min(1)] private int swatsPerSwing = 2;

        [Header("Rig")]
        [SerializeField] private Transform[] legs = new Transform[0];
        [SerializeField] private Material sparkMaterial;

        private State state = State.Idle;
        private float stateTime, nextLeap, nextRepath, nextSpark, nextBite, zigzagPhase;
        private Vector3 leapFrom, leapTo;
        private int slot;
        private bool wasSwinging, wasGrounded = true;

        protected override void OnStarted()
        {
            zigzagPhase = Random.value * 10f;
            Agent.autoBraking = false;
        }

        private void OnDestroy() => Latched.Remove(this);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Latched.Clear();
            swatFrame = -1;
        }

        private void Enter(State next)
        {
            if (state == State.Latched && next != State.Latched) Latched.Remove(this);
            state = next;
            stateTime = 0f;
        }

        protected override void Tick(float dt)
        {
            stateTime += dt;
            switch (state)
            {
                case State.Idle: UpdateIdle(); break;
                case State.Chase: UpdateChase(); break;
                case State.Leap: UpdateLeap(); break;
                case State.Latched: UpdateLatched(); break;
                case State.Dazed: UpdateDazed(); break;
            }
            Animate(dt);
        }

        // ---------- States ----------

        private void UpdateIdle()
        {
            if (Notices())
            {
                LastSeen = Time.time;
                Enter(State.Chase);
                return;
            }
            // Twitchy little shuffles near home.
            if (AgentReady && !Agent.pathPending && Agent.remainingDistance < 0.3f && stateTime > 1.5f)
            {
                Vector2 r = Random.insideUnitCircle * 2f;
                Chase(Home + new Vector3(r.x, 0f, r.y), runSpeed * 0.3f);
                stateTime = Random.value;
            }
        }

        private void UpdateChase()
        {
            if (!TargetAlive) { Enter(State.Idle); return; }
            float distance = FlatDistance(Target.position);
            if (CanSee(loseRange) || Alerted) LastSeen = Time.time;
            if (distance > loseRange || Time.time - LastSeen > 4f) { Enter(State.Idle); return; }

            if (distance <= leapRange && Time.time >= nextLeap && CanSee(leapRange + 1f))
            {
                BeginLeap();
                return;
            }

            if (Time.time >= nextRepath)
            {
                // Weave side to side, less as it gets close, so it is harder to hit from range.
                Vector3 toward = FlatDirectionTo(Target.position);
                Vector3 side = Vector3.Cross(Vector3.up, toward);
                float weave = Mathf.Sin(Time.time * zigzagRate + zigzagPhase) * zigzagWidth * Mathf.Clamp01((distance - 2f) / 8f);
                Chase(Target.position + side * weave, runSpeed);
                nextRepath = Time.time + 0.15f;
            }
        }

        private void BeginLeap()
        {
            leapFrom = transform.position;
            // Aim where the player stands now, a little past them, so a late sidestep makes it miss.
            Vector3 aim = Target.position + FlatDirectionTo(Target.position) * 0.3f;
            leapTo = new Vector3(aim.x, Target.position.y, aim.z);
            Halt();
            Agent.enabled = false;
            transform.rotation = Quaternion.LookRotation(FlatDirectionTo(leapTo));
            Enter(State.Leap);
        }

        private void UpdateLeap()
        {
            float t = Mathf.Clamp01(stateTime / leapTime);
            Vector3 position = Vector3.Lerp(leapFrom, leapTo, t);
            position.y += Mathf.Sin(t * Mathf.PI) * leapHeight;
            transform.position = position;

            if (TargetAlive && TouchesPlayer(position))
            {
                if (Latched.Count < maxLatched) { Latch(); return; }
                Land(dazed: true); // no room: bounces off
                return;
            }
            if (t >= 1f) Land(dazed: true);
        }

        /// <summary>True if this point is inside the player's capsule (with a little slack).</summary>
        private bool TouchesPlayer(Vector3 point)
        {
            Vector3 feet = Target.position;
            float height = TargetBody != null ? TargetBody.height : 1.8f;
            float radius = (TargetBody != null ? TargetBody.radius : 0.4f) + 0.25f;
            Vector3 flat = point - feet;
            float up = flat.y;
            flat.y = 0f;
            return flat.magnitude <= radius && up >= -0.2f && up <= height + 0.2f;
        }

        private void Latch()
        {
            Enter(State.Latched);
            Latched.Add(this);
            slot = FreeSlot();
            SetColliders(false); // can't be hit, and doesn't block the player, while clinging on
            nextBite = Time.time + latchInterval;
            wasSwinging = TargetLaser != null && TargetLaser.isActiveAndEnabled && TargetLaser.IsSwinging;
            wasGrounded = TargetBody == null || TargetBody.isGrounded;
            FodderStatusOverlay.Ensure();
        }

        private int FreeSlot()
        {
            for (int s = 0; s < Slots.Length; s++)
            {
                bool used = false;
                foreach (SparkTick other in Latched)
                    if (other != this && other.slot == s) used = true;
                if (!used) return s;
            }
            return 0;
        }

        private void UpdateLatched()
        {
            if (!TargetAlive) { ShakeOff(); return; }

            if (Time.time >= nextBite)
            {
                nextBite = Time.time + latchInterval;
                if (TargetHealth != null) TargetHealth.TakeDamage(latchDamage, gameObject);
                if (sparkMaterial != null) ImpactFlash.Spawn(sparkMaterial, transform.position, 0.05f, 0.06f);
            }

            // A melee swing swats some off (killing them); a jump throws them all off.
            bool swinging = TargetLaser != null && TargetLaser.isActiveAndEnabled && TargetLaser.IsSwinging;
            if (swinging && !wasSwinging)
            {
                if (swatFrame != Time.frameCount) { swatFrame = Time.frameCount; swatsThisFrame = 0; }
                if (swatsThisFrame < swatsPerSwing)
                {
                    swatsThisFrame++;
                    if (sparkMaterial != null) ImpactFlash.Spawn(sparkMaterial, transform.position, 0.12f, 0.1f);
                    Kill(Target.gameObject);
                    return;
                }
            }
            wasSwinging = swinging;

            bool grounded = TargetBody == null || TargetBody.isGrounded;
            bool jumped = wasGrounded && !grounded && TargetBody != null && TargetBody.velocity.y > 1.5f;
            wasGrounded = grounded;
            if (jumped) ShakeOff();
        }

        // After the camera has moved this frame, so a clinging tick stays glued to the view instead of lagging.
        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (state != State.Latched || IsDead || Target == null) return;
            // Cling to the visor: a bit in front of the camera, facing it.
            Transform view = TargetView != null ? TargetView : Target;
            Vector2 s = Slots[Mathf.Clamp(slot, 0, Slots.Length - 1)];
            float wiggle = Mathf.Sin(Time.time * 23f + slot) * 0.01f;
            transform.position = view.position + view.forward * 0.55f + view.right * (s.x + wiggle) + view.up * s.y;
            transform.rotation = Quaternion.LookRotation(-view.forward, view.up) * Quaternion.Euler(-70f, 0f, 0f);
        }

        /// <summary>Falls off the player and lands dazed beside them.</summary>
        private void ShakeOff()
        {
            Vector3 near = (Target != null ? Target.position : transform.position) + Random.insideUnitSphere * 1.2f;
            transform.position = near;
            SetColliders(true);
            Land(dazed: true);
        }

        private void Land(bool dazed)
        {
            if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            {
                Kill(); // fell somewhere it can't walk; just let it die
                return;
            }
            transform.position = hit.position;
            Agent.enabled = true;
            Agent.Warp(hit.position);
            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            nextLeap = Time.time + leapCooldown;
            Enter(dazed ? State.Dazed : State.Chase);
            Halt();
        }

        private void UpdateDazed()
        {
            if (stateTime < dazeTime) return;
            LastSeen = Time.time;
            Enter(TargetAlive ? State.Chase : State.Idle);
        }

        // ---------- Hits and death ----------

        protected override void OnDeath()
        {
            bool wasLatched = state == State.Latched;
            Latched.Remove(this);
            if (sparkMaterial != null) ImpactFlash.Spawn(sparkMaterial, transform.position + Vector3.up * 0.1f, 0.25f, 0.12f);
            // A tick swatted off the visor just pops; it doesn't leave a body floating in front of the camera.
            if (wasLatched) Destroy(gameObject);
        }

        // ---------- Animation ----------

        private void Animate(float dt)
        {
            float speed = AgentReady ? Agent.velocity.magnitude : 0f;
            bool frantic = state == State.Latched || state == State.Leap;
            float rate = frantic ? 40f : 8f + speed * 5f;
            for (int i = 0; i < legs.Length; i++)
            {
                if (legs[i] == null) continue;
                float swing = Mathf.Sin(Time.time * rate + i * 2.1f) * (frantic ? 35f : Mathf.Clamp(speed * 7f, 6f, 30f));
                legs[i].localRotation = Quaternion.Euler(0f, swing, 0f);
            }

            float bob = Mathf.Abs(Mathf.Sin(Time.time * rate * 0.5f)) * 0.02f;
            Quaternion lean = Quaternion.identity;
            if (state == State.Dazed) lean = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 18f) * 18f);
            Visual.localPosition = VisualRest + Vector3.up * bob;
            Visual.localRotation = VisualRestRotation * lean;

            // The stripped wires crackle now and then.
            if (sparkMaterial != null && Time.time >= nextSpark)
            {
                nextSpark = Time.time + Random.Range(0.35f, 1.1f);
                ImpactFlash.Spawn(sparkMaterial, transform.position + transform.up * 0.15f + Random.insideUnitSphere * 0.08f, 0.06f, 0.05f);
            }
        }
    }
}
