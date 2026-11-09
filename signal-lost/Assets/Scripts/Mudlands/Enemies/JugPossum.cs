using SignalLost.Audio;
using SignalLost.Combat;
using SignalLost.Effects;
using SignalLost.Enemies.Desert;
using SignalLost.Inventory;
using UnityEngine;
using Random = UnityEngine.Random;

namespace SignalLost.Mudlands
{
    /// <summary>
    /// Ranged enemy that uses buildings: a possum in a straw hat, sitting on porches and roofs, swigging from a
    /// moonshine jug, then leaning back and lobbing flaming jugs in an arc (12 and a short fire) at where you are
    /// going. It ducks behind the roof ridge while you aim a charged bolt at it. A good hit makes it play dead: it
    /// keels over (off the roof, if it is on one) and lies still with its tongue out for 2.5 s, taking extra damage,
    /// then scurries off. On the ground it keeps its distance. Cars: it plays dead and is too flat to hit.
    /// 45 HP.
    /// </summary>
    public sealed class JugPossum : MudCritter
    {
        public static readonly CritterSpec Defaults = new CritterSpec("Jug Possum", 45f, 0.35f, 0.9f, 3.2f, 22f,
            (ScrapType.Wiring, 1, 2), (ScrapType.CircuitBoards, 0, 1));

        private enum State { Idle, Aim, Throw, Duck, PlayDead, Scurry, Stunned }

        private static readonly Color Fur = new Color(0.62f, 0.62f, 0.6f), Face = new Color(0.92f, 0.9f, 0.86f),
            Nose = new Color(0.95f, 0.55f, 0.65f), Straw = new Color(0.86f, 0.74f, 0.4f), Jug = new Color(0.55f, 0.36f, 0.2f);

        [Tooltip("Sits where it was placed (a roof or porch) instead of walking the NavMesh, until knocked off.")]
        [SerializeField] private bool perch;

        private State state = State.Idle;
        private Vector3 placed;
        private float stateTime, nextThrow, deadFor;
        private bool perched, bonusHit;
        private Transform torso, armR, jug, tail, tongue, hat;
        private static readonly FireStyle JugFire = new FireStyle { damage = 5f, duration = 2.5f, tint = new Color(1f, 0.85f, 0.5f) };

        protected override void BuildModel(Transform visual)
        {
            torso = MudArt.Pivot(visual, "Torso", new Vector3(0f, 0.35f, 0f));
            MudArt.Part(torso, "Body", Vector3.zero, Quaternion.identity, "possum_body", k =>
            {
                k.Ball(new Vector3(0f, 0.05f, 0f), new Vector3(0.28f, 0.3f, 0.3f), Quaternion.identity, Fur, 1);
                k.Ball(new Vector3(0f, 0.02f, 0.12f), new Vector3(0.2f, 0.22f, 0.18f), Quaternion.identity, Face, 1);
                foreach (float x in new[] { -0.15f, 0.15f })
                    k.Ball(new Vector3(x, -0.3f, 0.08f), new Vector3(0.09f, 0.06f, 0.14f), Quaternion.identity, Nose, 0);
            });
            Head = MudArt.Pivot(torso, "Head", new Vector3(0f, 0.38f, 0.05f));
            MudArt.Part(Head, "Face", Vector3.zero, Quaternion.identity, "possum_head", k =>
            {
                k.Ball(Vector3.zero, new Vector3(0.2f, 0.18f, 0.2f), Quaternion.identity, Face, 1);
                k.Cone(new Vector3(0f, -0.03f, 0.12f), Quaternion.Euler(90f, 0f, 0f), 0.1f, 0.2f, 6, Face);
                k.Ball(new Vector3(0f, -0.03f, 0.33f), 0.035f, Nose, 0);
                k.Ball(new Vector3(-0.15f, 0.13f, -0.02f), new Vector3(0.08f, 0.1f, 0.03f), Quaternion.identity, MudPalette.Black, 0);
                k.Ball(new Vector3(0.15f, 0.13f, -0.02f), new Vector3(0.08f, 0.1f, 0.03f), Quaternion.identity, MudPalette.Black, 0);
            });
            (Transform l, Transform r) = MudArt.Eyes(Head, new Vector3(0f, 0.05f, 0.14f), 0.075f, 0.035f, MudPalette.Black, MudPalette.Black, "possum");
            EyeL = l;
            EyeR = r;
            tongue = MudArt.Part(Head, "Tongue", new Vector3(0.02f, -0.08f, 0.3f), Quaternion.Euler(50f, 0f, 0f), "possum_tongue",
                k => k.Box(new Vector3(0f, 0f, 0.04f), new Vector3(0.04f, 0.01f, 0.09f), Quaternion.identity, Nose));
            tongue.gameObject.SetActive(false);
            hat = MudArt.Part(Head, "Straw Hat", new Vector3(0f, 0.15f, 0f), Quaternion.Euler(-8f, 0f, 6f), "possum_hat", k =>
            {
                k.Prism(Vector3.zero, Quaternion.identity, 0.32f, 0.3f, 0.03f, 10, Straw);
                k.Prism(new Vector3(0f, 0.03f, 0f), Quaternion.identity, 0.16f, 0.13f, 0.14f, 10, Straw);
                k.Prism(new Vector3(0f, 0.04f, 0f), Quaternion.identity, 0.165f, 0.165f, 0.04f, 10, new Color(0.6f, 0.15f, 0.12f));
            });
            armR = MudArt.Pivot(torso, "Arm R", new Vector3(0.24f, 0.15f, 0.05f));
            MudArt.Part(armR, "Arm", Vector3.zero, Quaternion.identity, "possum_arm", k => k.Rod(Vector3.zero, new Vector3(0f, -0.25f, 0.05f), 0.05f, 5, Fur));
            jug = MudArt.Part(armR, "Jug", new Vector3(0f, -0.32f, 0.08f), Quaternion.identity, "moonshine_jug", JugMesh);
            MudArt.Part(torso, "Arm L", new Vector3(-0.24f, 0.1f, 0.05f), Quaternion.Euler(-30f, 0f, -20f), "possum_arm", k => k.Rod(Vector3.zero, new Vector3(0f, -0.25f, 0.05f), 0.05f, 5, Fur));
            tail = MudArt.Part(visual, "Tail", new Vector3(0f, 0.15f, -0.25f), Quaternion.identity, "possum_tail",
                k => k.Tube(new[] { Vector3.zero, new Vector3(0f, -0.05f, -0.25f), new Vector3(0.08f, 0.05f, -0.45f), new Vector3(0.15f, 0.2f, -0.5f) }, 0.03f, 5, Nose));
        }

        /// <summary>A brown stoneware jug with a cream band and "XXX".</summary>
        public static void JugMesh(MeshKit k)
        {
            k.Lathe(Vector3.zero, Quaternion.identity, new[]
            {
                new Vector2(0.0001f, -0.1f), new Vector2(0.09f, -0.1f), new Vector2(0.11f, 0f), new Vector2(0.08f, 0.1f),
                new Vector2(0.03f, 0.13f), new Vector2(0.03f, 0.17f), new Vector2(0.0001f, 0.17f)
            }, 8, Jug);
            k.Prism(new Vector3(0f, -0.03f, 0f), Quaternion.identity, 0.112f, 0.112f, 0.05f, 8, MudPalette.Cream);
            k.Box(new Vector3(0f, -0.005f, 0.113f), new Vector3(0.09f, 0.025f, 0.01f), MudPalette.Black);
        }

        /// <summary>Set by the level builder for possums on roofs and porches.</summary>
        public void SetPerch(bool on) => perch = on;

        protected override void Awake()
        {
            base.Awake();
            placed = transform.position;
        }

        protected override void OnCritterStarted()
        {
            if (perch && Agent != null)
            {
                // Start() snapped it to the NavMesh; put it back on its roof.
                Agent.enabled = false;
                transform.position = placed;
                Home = placed;
                Territory.SetHome(placed);
            }
            perched = perch || !AgentReady;
            Enter(State.Idle);
            nextThrow = Time.time + Random.Range(0.5f, 1.5f);
        }

        private void Enter(State next)
        {
            state = next;
            stateTime = 0f;
            tongue.gameObject.SetActive(next == State.PlayDead);
            SetColliders(next != State.Duck);
        }

        // ---------- Brain ----------

        protected override void Think(float dt)
        {
            stateTime += dt;
            bool aimedAt = TargetLaser != null && TargetLaser.IsCharging && TargetView != null &&
                           Vector3.Dot(TargetView.forward, (transform.position + Vector3.up * 0.5f - TargetView.position).normalized) > 0.97f &&
                           PlayerDistance < 35f;
            switch (state)
            {
                case State.Idle:
                    Halt();
                    if (IdleEmoteDue()) Say("*hic*", new Color(1f, 0.8f, 0.5f), 1f, 2.5f);
                    if (!Notices()) break;
                    TurnToward(PlayerPosition, 360f * dt);
                    if (aimedAt && Random.value < 0.7f) { Duck(); break; }
                    if (!perched) KeepDistance();
                    if (Time.time >= nextThrow && CanSee(28f)) Enter(State.Aim);
                    break;
                case State.Aim:
                    Halt();
                    TurnToward(PlayerPosition, 360f * dt);
                    if (aimedAt && stateTime < 0.4f) { Duck(); break; }
                    if (stateTime >= 0.7f) Throw();
                    break;
                case State.Throw:
                    if (stateTime >= 0.35f) { Enter(State.Idle); nextThrow = Time.time + Random.Range(2.2f, 3.4f); }
                    break;
                case State.Duck:
                    if (!aimedAt && stateTime > 0.5f)
                    {
                        Enter(State.Idle);
                        Say("heh", new Color(1f, 0.85f, 0.6f), 0.8f, 2.5f);
                        nextThrow = Mathf.Min(nextThrow, Time.time + 0.3f); // pops up and throws straight away
                    }
                    break;
                case State.PlayDead:
                    Halt();
                    if (stateTime >= deadFor)
                    {
                        Enter(State.Scurry);
                        Say("!", new Color(1f, 0.85f, 0.3f));
                    }
                    break;
                case State.Scurry:
                    KeepDistance(6f);
                    if (stateTime > 1.2f) Enter(State.Idle);
                    break;
            }
            if (state == State.Idle || state == State.Aim) WatchPlayer(70f);
        }

        private void KeepDistance(float speed = 3.2f)
        {
            if (!AgentReady || Target == null) return;
            float distance = PlayerDistance;
            if (distance > 9f && distance < 18f) { Halt(); return; }
            Vector3 away = transform.position - PlayerPosition;
            away.y = 0f;
            Vector3 goal = distance <= 9f ? transform.position + away.normalized * 4f : PlayerPosition + away.normalized * 13f;
            MoveTo(goal, speed);
        }

        private void Duck()
        {
            Enter(State.Duck);
            Say("!", new Color(1f, 0.9f, 0.4f), 0.6f);
        }

        private void Throw()
        {
            Enter(State.Throw);
            Squash(-0.25f);
            Vector3 from = jug.position + Vector3.up * 0.2f;
            Vector3 lead = Vector3.zero;
            if (TargetBody != null) lead = TargetBody.velocity * 0.6f;
            lead.y = 0f;
            Vector3 aim = PlayerPosition + lead + Random.insideUnitSphere.Flat() * 0.8f;
            float distance = Vector3.Distance(from, aim);
            var model = MudArt.Part(null, "Thrown Jug", Vector3.zero, Quaternion.identity, "moonshine_jug", JugMesh);
            ParticleBurst.Spawn(ParticleBurst.Kind.Fireball, from, Vector3.up, 3, 0.2f, 0.5f);
            GameObject owner = gameObject;
            Lob.Throw(model, from, aim, Mathf.Clamp(distance / 14f, 0.7f, 1.4f), 2.5f + distance * 0.12f, at =>
            {
                Sfx.PlayAt("bottle_smash", at, 0.8f, Random.Range(0.8f, 0.95f), 35f);
                ParticleBurst.Spawn(ParticleBurst.Kind.Fireball, at, Vector3.up, 18, 0.7f, 3f, 70f);
                foreach (Collider hit in Physics.OverlapSphere(at, 1.6f, ~0, QueryTriggerInteraction.Ignore))
                {
                    Health target = hit.GetComponentInParent<Health>();
                    if (target == null || target.IsDead || !SignalLost.Enemies.EnemyAllegiance.CanHurt(owner, target)) continue;
                    target.TakeDamage(12f, owner);
                    break;
                }
                FireZone.Spawn(JugFire, at, 1.4f, owner, null);
            }, 1.6f, new Color(1f, 0.55f, 0.15f, 0.5f), 1f);
            Sfx.PlayAt("chain_swish", from, 0.4f, 0.8f, 25f);
        }

        // ---------- Reactions ----------

        protected override void OnSpotted()
        {
            Say("!", new Color(1f, 0.85f, 0.3f));
            Squash(-0.3f);
        }

        protected override void OnHit(float amount, GameObject source, Vector3 away)
        {
            if (state == State.PlayDead && !bonusHit)
            {
                bonusHit = true;
                Health.TakeDamage(amount * 0.3f, source); // not as dead as it looks
                bonusHit = false;
                return;
            }
            if (amount >= 10f || Random.value < 0.3f) PlayDead(away);
        }

        private void PlayDead(Vector3 away)
        {
            deadFor = 2.5f;
            Enter(State.PlayDead);
            Say("x_x", new Color(0.85f, 0.85f, 0.85f), 1.3f, 3f);
            if (perched)
            {
                // Keels over off the roof.
                perched = false;
                Launch(away * 2.5f + Vector3.up * 2f, 200f);
            }
        }

        protected override void OnLanded()
        {
            perched = !AgentReady;
            if (!IsDead) { Enter(State.PlayDead); deadFor = 2f; }
        }

        protected override void OnAllyDied(MudCritter ally) => Say("*gulp*", new Color(0.8f, 0.9f, 1f), 1f, 2.5f);

        public override void OnCarApproaching(DrunkCar car, float secondsAway)
        {
            if (perched || state == State.PlayDead || secondsAway > 1.5f) return;
            deadFor = 2f;
            Enter(State.PlayDead);
            Say("*plays dead*", new Color(0.85f, 0.85f, 0.85f), 1.3f, 2.5f);
        }

        public override VehicleResponse OnVehicleHit(VehicleHit hit)
        {
            if (state == State.PlayDead) return VehicleResponse.Ignore; // too flat to hit
            return base.OnVehicleHit(hit);
        }

        // ---------- Animation ----------

        protected override void Animate(float dt)
        {
            if (IsStunned || Airborne) return;
            float t = Time.time;
            Quaternion body = Quaternion.identity, arm = Quaternion.Euler(-20f, 0f, 15f);
            float sink = 0f;
            switch (state)
            {
                case State.Idle:
                    body = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 1.3f) * 8f); // a tipsy sway
                    float swig = Mathf.Repeat(t * 0.2f + Seed, 1f);
                    if (swig > 0.85f) arm = Quaternion.Euler(-150f, 0f, -20f);
                    break;
                case State.Aim:
                    float back = Mathf.SmoothStep(0f, 1f, stateTime / 0.6f);
                    body = Quaternion.Euler(-22f * back, 0f, 0f);
                    arm = Quaternion.Euler(Mathf.Lerp(-20f, -200f, back), 0f, 10f);
                    break;
                case State.Throw:
                    body = Quaternion.Euler(18f, 0f, 0f);
                    arm = Quaternion.Euler(-60f, 0f, 10f);
                    break;
                case State.Duck:
                    sink = -0.55f;
                    body = Quaternion.Euler(30f, 0f, 0f);
                    break;
                case State.PlayDead:
                    body = Quaternion.Euler(0f, 0f, 85f);
                    arm = Quaternion.Euler(-90f + Mathf.Sin(t * 25f) * (stateTime > deadFor - 0.6f ? 15f : 0f), 0f, 0f);
                    break;
                case State.Scurry:
                    body = Quaternion.Euler(25f, 0f, Mathf.Sin(t * 30f) * 8f);
                    break;
            }
            torso.localRotation = Quaternion.Slerp(torso.localRotation, body, dt * 10f);
            armR.localRotation = Quaternion.Slerp(armR.localRotation, arm, dt * 14f);
            Visual.localPosition = Vector3.Lerp(Visual.localPosition, VisualRest + Vector3.up * sink, dt * 12f);
            jug.gameObject.SetActive(state != State.Throw);
            if (tail != null) tail.localRotation = Quaternion.Euler(0f, Mathf.Sin(t * 2f) * 20f, 0f);
            if (hat != null) hat.localRotation = Quaternion.Euler(-8f, 0f, 6f + Mathf.Sin(t * 1.3f) * 5f);
        }

        protected override void DeathPose(float t)
        {
            // Plays dead one last time, for real: keels over, a leg twitches, then it sinks away.
            torso.localRotation = Quaternion.Euler(0f, 0f, 90f * Mathf.Clamp01(t / 0.3f));
            tongue.gameObject.SetActive(true);
            armR.localRotation = Quaternion.Euler(-90f + (t > 0.8f && t < 1.6f ? Mathf.Sin(t * 30f) * 20f : 0f), 0f, 0f);
            Visual.localPosition = VisualRest + Vector3.down * 0.5f * Mathf.Clamp01((t - 2f) / 1f);
        }
    }

    internal static class MudVectors
    {
        public static Vector3 Flat(this Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
