using SignalLost.Audio;
using SignalLost.Effects;
using SignalLost.Inventory;
using UnityEngine;
using Random = UnityEngine.Random;

namespace SignalLost.Mudlands
{
    /// <summary>
    /// Pressure enemy: a big alien boar (lilac hide, glowing back spines, pipe-stub tusks) in denim overalls with a
    /// club made from a bent strut off the crashed ship, a show-off who flexes and kisses his
    /// biceps when idle and argues with any other brute nearby. He paws the ground and snorts steam (1 s, aim
    /// tracking until 70%), then shoulder-charges in a line (22 and a shove); charging into mud gets him stuck for
    /// 2 s. Up close he raises the club overhead over a ring (0.75 s) and slams (18). He belly-laughs when he hurts
    /// you, which is an opening. Too big to run over: he plants his feet and the car blows up on him, unless it has
    /// the Reinforced Bumper. 110 HP.
    /// </summary>
    public sealed class HogBrute : MudCritter
    {
        public static readonly CritterSpec Defaults = new CritterSpec("Hog Brute", 110f, 0.65f, 2.1f, 3f, 14f,
            (ScrapType.ScrapMetal, 2, 3), (ScrapType.FuelCells, 0, 1));

        private enum State { Idle, Argue, Chase, Paw, Charge, Skid, Stuck, Raise, Slam, Laugh, Brace }

        // Alien colours: lilac hide, a pink-violet snout and a row of glowing teal spines down his back.
        private static readonly Color Hide = new Color(0.6f, 0.45f, 0.66f), Snout = new Color(0.86f, 0.62f, 0.86f),
            Denim = new Color(0.25f, 0.38f, 0.6f), Bristle = new Color(0.3f, 0.2f, 0.36f), Spine = new Color(0.35f, 1f, 0.8f);

        private State state = State.Idle;
        private float stateTime, nextCharge, flex;
        private Vector3 chargeDirection;
        private bool chargeHit;
        private HogBrute rival;
        private GroundRing ring;
        private Transform torso, armL, armR, legL, legR, club, nostrils;

        protected override void BuildModel(Transform visual)
        {
            torso = MudArt.Pivot(visual, "Torso", new Vector3(0f, 0.95f, 0f));
            MudArt.Part(torso, "Belly", Vector3.zero, Quaternion.identity, "hog_body", k =>
            {
                k.Ball(new Vector3(0f, 0.15f, 0.05f), new Vector3(0.62f, 0.62f, 0.55f), Quaternion.identity, Hide, 1);
                k.Ball(new Vector3(0f, 0.55f, -0.05f), new Vector3(0.55f, 0.35f, 0.45f), Quaternion.identity, Hide, 1);
                // Overalls: bib, straps, buttons.
                k.Box(new Vector3(0f, -0.05f, 0.4f), new Vector3(0.72f, 0.6f, 0.3f), Quaternion.Euler(-8f, 0f, 0f), Denim);
                k.Box(new Vector3(0f, -0.3f, 0.02f), new Vector3(1.18f, 0.4f, 1.0f), Denim);
                foreach (float x in new[] { -0.26f, 0.26f })
                {
                    k.Plank(new Vector3(x, 0.25f, 0.5f), new Vector3(x * 1.1f, 0.85f, -0.1f), 0.1f, 0.04f, Denim);
                    k.Ball(new Vector3(x, 0.22f, 0.56f), 0.05f, MudPalette.Metal, 0);
                }
                k.Box(new Vector3(0.12f, -0.02f, 0.56f), new Vector3(0.2f, 0.16f, 0.02f), new Color(0.2f, 0.3f, 0.5f)); // pocket
                // Glowing spines down his back (where the bristles were), set on the curve of the body.
                float[] spineY = { 0.82f, 0.68f, 0.52f, 0.36f, 0.2f }, spineZ = { -0.3f, -0.43f, -0.46f, -0.43f, -0.46f };
                for (int i = 0; i < 5; i++)
                    k.Cone(new Vector3(0f, spineY[i], spineZ[i]), Quaternion.Euler(-35f - i * 12f, 0f, 0f), 0.07f, 0.3f - Mathf.Abs(i - 1) * 0.04f, 5, Spine, 1.4f);
            });
            Head = MudArt.Pivot(torso, "Head", new Vector3(0f, 0.85f, 0.25f));
            MudArt.Part(Head, "Face", Vector3.zero, Quaternion.identity, "hog_head", k =>
            {
                k.Ball(new Vector3(0f, 0.05f, 0.05f), new Vector3(0.38f, 0.34f, 0.36f), Quaternion.identity, Hide, 1);
                k.Prism(new Vector3(0f, -0.02f, 0.32f), Quaternion.Euler(90f, 0f, 0f), 0.17f, 0.19f, 0.2f, 8, Snout);
                k.Cone(new Vector3(-0.26f, 0.3f, 0.02f), Quaternion.Euler(10f, 0f, 35f), 0.12f, 0.22f, 4, Hide);
                k.Cone(new Vector3(0.26f, 0.3f, 0.02f), Quaternion.Euler(10f, 0f, -35f), 0.12f, 0.22f, 4, Hide);
                // Tusks: two pipe stubs off the wreck, jammed into his jaw.
                foreach (float x in new[] { -0.15f, 0.15f })
                {
                    Vector3 root = new Vector3(x, -0.12f, 0.35f), tip = root + Quaternion.Euler(-20f, 0f, x > 0 ? -20f : 20f) * Vector3.up * 0.17f;
                    k.Rod(root, tip, 0.035f, 6, MudPalette.Metal);
                    k.Rod(root + (tip - root) * 0.3f, root + (tip - root) * 0.45f, 0.042f, 6, MudPalette.HullStripe);
                    k.Ball(tip, 0.026f, MudPalette.Black, 0); // the open end
                }
                k.Box(new Vector3(0f, 0.2f, 0.28f), new Vector3(0.36f, 0.05f, 0.05f), Quaternion.Euler(0f, 0f, 0f), Bristle); // a single angry brow
            });
            nostrils = MudArt.Part(Head, "Nostrils", new Vector3(0f, -0.02f, 0.53f), Quaternion.identity, "hog_nose", k =>
            {
                k.Ball(new Vector3(-0.06f, 0f, 0f), new Vector3(0.035f, 0.05f, 0.02f), Quaternion.identity, MudPalette.Black, 0);
                k.Ball(new Vector3(0.06f, 0f, 0f), new Vector3(0.035f, 0.05f, 0.02f), Quaternion.identity, MudPalette.Black, 0);
            });
            (Transform l, Transform r) = MudArt.Eyes(Head, new Vector3(0f, 0.13f, 0.3f), 0.12f, 0.045f, MudPalette.White, MudPalette.Black, "hog");
            EyeL = l;
            EyeR = r;
            armL = Arm(torso, -1f);
            armR = Arm(torso, 1f);
            // The club: a landing strut torn off the ship, bent in the middle, with a wrapped grip and a broken bracket.
            club = MudArt.Part(armR, "Club", new Vector3(0f, -0.62f, 0.08f), Quaternion.Euler(80f, 0f, 0f), "hog_club", k =>
            {
                Vector3 grip = new Vector3(0f, -0.2f, 0f), bend = new Vector3(0f, 0.45f, 0f), end = new Vector3(0f, 1.05f, 0.16f);
                k.Rod(grip, bend, 0.06f, 7, MudPalette.Hull, 0f, 0.07f);
                k.Rod(bend, end, 0.07f, 7, MudPalette.Hull, 0f, 0.09f);
                k.Ball(bend, 0.07f, MudPalette.Hull, 0);
                k.Rod(grip, grip + Vector3.up * 0.32f, 0.075f, 7, MudPalette.HullDark); // tape round the grip
                k.Rod(bend + (end - bend) * 0.35f, bend + (end - bend) * 0.5f, 0.088f, 7, MudPalette.HullStripe);
                k.Box(end, new Vector3(0.28f, 0.1f, 0.22f), Quaternion.FromToRotation(Vector3.up, end - bend), MudPalette.HullDark); // the torn bracket
                k.Box(end + new Vector3(0.1f, 0.06f, 0.03f), new Vector3(0.04f, 0.1f, 0.04f), MudPalette.Metal); // a sheared bolt
            });
            legL = Leg(visual, -1f);
            legR = Leg(visual, 1f);
        }

        private static Transform Arm(Transform torso, float side)
        {
            Transform shoulder = MudArt.Pivot(torso, side < 0 ? "Arm L" : "Arm R", new Vector3(0.62f * side, 0.45f, 0.05f));
            MudArt.Part(shoulder, "Arm", Vector3.zero, Quaternion.identity, "hog_arm", k =>
            {
                k.Ball(Vector3.zero, new Vector3(0.2f, 0.22f, 0.2f), Quaternion.identity, Hide, 1);
                k.Rod(Vector3.zero, new Vector3(0f, -0.5f, 0.05f), 0.15f, 6, Hide, 0f, 0.12f);
                k.Ball(new Vector3(0f, -0.6f, 0.06f), new Vector3(0.14f, 0.12f, 0.14f), Quaternion.identity, Bristle, 0);
            });
            return shoulder;
        }

        private static Transform Leg(Transform visual, float side)
        {
            Transform hip = MudArt.Pivot(visual, side < 0 ? "Leg L" : "Leg R", new Vector3(0.3f * side, 0.6f, 0f));
            MudArt.Part(hip, "Leg", Vector3.zero, Quaternion.identity, "hog_leg", k =>
            {
                k.Rod(Vector3.zero, new Vector3(0f, -0.5f, 0f), 0.17f, 6, Denim, 0f, 0.15f);
                k.Box(new Vector3(0f, -0.55f, 0.06f), new Vector3(0.24f, 0.14f, 0.32f), MudPalette.Black); // hooves
            });
            return hip;
        }

        protected override void OnCritterStarted() => Enter(State.Idle);

        private void Enter(State next)
        {
            if (ring != null && next != State.Slam) { ring.Finish(); ring = null; }
            state = next;
            stateTime = 0f;
        }

        // ---------- Brain ----------

        protected override void Think(float dt)
        {
            stateTime += dt;
            switch (state)
            {
                case State.Idle:
                case State.Argue:
                    Halt();
                    if (Notices()) { Enter(State.Chase); break; }
                    UpdateIdle(dt);
                    break;
                case State.Chase:
                    if (GaveUp() || !TargetAlive) { Enter(State.Idle); break; }
                    float distance = PlayerDistance;
                    if (distance < 2.8f) { Enter(State.Raise); ring = GroundRing.Spawn(transform.position + transform.forward * 1.8f, 2.8f, new Color(1f, 0.35f, 0.2f, 0.5f)); break; }
                    if (distance > 5f && distance < 16f && Time.time >= nextCharge && CanSee(18f)) { Enter(State.Paw); Halt(); break; }
                    MoveTo(PlayerPosition, Defaults.Speed);
                    break;
                case State.Paw:
                    if (stateTime < 0.7f) TurnToward(PlayerPosition, 400f * dt);
                    chargeDirection = transform.forward;
                    if (Mathf.Repeat(stateTime, 0.33f) < dt)
                    {
                        ParticleBurst.Spawn(ParticleBurst.Kind.Sand, transform.position + transform.forward * 0.4f, -transform.forward + Vector3.up, 8, 0.35f, 3f, 30f, new Color(0.35f, 0.27f, 0.2f, 0.8f));
                        ParticleBurst.Spawn(ParticleBurst.Kind.Mist, nostrils.position, transform.forward + Vector3.down * 0.3f, 6, 0.25f, 2.5f, 20f, new Color(1f, 1f, 1f, 0.7f));
                    }
                    if (stateTime >= 1f)
                    {
                        Enter(State.Charge);
                        chargeHit = false;
                        Sfx.PlayAt("kong_roar", transform.position, 0.5f, 1.6f, 35f);
                        Say("HRRNK!", new Color(1f, 0.5f, 0.35f), 0.9f, 3.5f);
                    }
                    break;
                case State.Charge: UpdateCharge(dt); break;
                case State.Skid:
                    Halt();
                    if (stateTime > 1.4f) { Enter(State.Chase); nextCharge = Time.time + Random.Range(3f, 5f); }
                    break;
                case State.Stuck:
                    Halt();
                    if (Mathf.Repeat(stateTime, 0.4f) < dt) ParticleBurst.Spawn(ParticleBurst.Kind.Sand, transform.position, Vector3.up, 6, 0.4f, 2f, 50f, new Color(0.25f, 0.18f, 0.12f, 0.9f));
                    if (stateTime > 2f) { Enter(State.Chase); nextCharge = Time.time + 4f; }
                    break;
                case State.Raise:
                    Halt();
                    TurnToward(PlayerPosition, 120f * dt);
                    if (ring != null) { ring.MoveTo(transform.position + transform.forward * 1.8f); ring.Progress = stateTime / 0.75f; }
                    if (stateTime >= 0.75f) Enter(State.Slam);
                    break;
                case State.Slam:
                    if (stateTime >= 0.08f && stateTime - dt < 0.08f)
                    {
                        Vector3 at = transform.position + transform.forward * 1.8f;
                        CameraShake.Add(0.2f);
                        ParticleBurst.Spawn(ParticleBurst.Kind.Sand, at, Vector3.up, 30, 0.6f, 5f, 75f, new Color(0.35f, 0.27f, 0.2f, 0.8f));
                        Sfx.PlayAt("laser_slam", at, 0.8f, 0.6f, 35f);
                        if (TargetAlive && (PlayerPosition - at).sqrMagnitude < 2.8f * 2.8f && TargetHealth.TakeDamage(18f, gameObject)) Laugh();
                        if (ring != null) { ring.Finish(); ring = null; }
                    }
                    if (stateTime >= 0.7f && state == State.Slam) Enter(State.Chase);
                    break;
                case State.Laugh:
                    Halt();
                    if (stateTime > 1.1f) Enter(State.Chase);
                    break;
                case State.Brace:
                    Halt();
                    if (stateTime > 1.6f) Enter(State.Chase);
                    break;
            }
            if (state != State.Charge && state != State.Stuck) WatchPlayer(60f);
        }

        private void UpdateIdle(float dt)
        {
            // Two brutes near each other argue; alone, he flexes.
            if (rival == null || rival.IsDead || (rival.transform.position - transform.position).sqrMagnitude > 49f)
            {
                rival = null;
                foreach (MudCritter other in Living)
                    if (other is HogBrute brute && brute != this && !brute.IsDead && (brute.transform.position - transform.position).sqrMagnitude < 49f) { rival = brute; break; }
            }
            if (rival != null)
            {
                if (state != State.Argue) Enter(State.Argue);
                TurnToward(rival.transform.position, 180f * dt);
                if (IdleEmoteDue() || Mathf.Repeat(stateTime + Seed, 3.2f) < dt)
                    Say(Random.value < 0.5f ? "#@!" : Random.value < 0.5f ? "OH YEAH?" : "MY CLUB!", new Color(1f, 0.55f, 0.4f), 1.2f, 2.8f);
                return;
            }
            if (state == State.Argue) Enter(State.Idle);
            if (IdleEmoteDue()) Say(Random.value < 0.5f ? "*flex*" : "<3", new Color(1f, 0.7f, 0.8f), 1.2f, 2.8f);
        }

        private void UpdateCharge(float dt)
        {
            float speed = 13f * Tempo;
            if (AgentReady) Agent.Move(chargeDirection * speed * dt);
            transform.rotation = Quaternion.LookRotation(chargeDirection);
            if (Mathf.Repeat(stateTime, 0.12f) < dt) ParticleBurst.Spawn(ParticleBurst.Kind.Sand, transform.position, -chargeDirection + Vector3.up, 5, 0.4f, 3f, 30f, new Color(0.4f, 0.32f, 0.22f, 0.7f));
            if (!chargeHit && TargetAlive && PlayerDistance < 1.7f)
            {
                chargeHit = true;
                Strike(2f, 140f, 22f, 5f);
                Laugh();
                return;
            }
            // Ran into mud: stuck. Ran into a wall: dazed. Ran far enough: skid to a stop.
            if (MudZone.At(transform.position).kind == MudZone.Kind.Mud && MudZone.At(transform.position).strength > 0.5f)
            {
                Enter(State.Stuck);
                Say("STUCK!?", new Color(1f, 0.9f, 0.5f), 1.6f, 3.2f);
                Sfx.PlayAt("mud_splat", transform.position, 0.8f, 0.7f, 30f);
                return;
            }
            bool wall = Physics.Raycast(transform.position + Vector3.up * 0.9f, chargeDirection, 1.1f, sightMask, QueryTriggerInteraction.Ignore);
            if (wall)
            {
                Enter(State.Skid);
                Say("@_@", new Color(1f, 1f, 0.6f), 1.4f, 3f);
                CameraShake.Add(0.15f);
                Sfx.PlayAt("car_crash", transform.position, 0.5f, 0.8f, 30f);
                return;
            }
            if (stateTime > 1.25f) { Enter(State.Skid); stateTime = 0.6f; }
        }

        private void Laugh()
        {
            Enter(State.Laugh);
            Say("HAW HAW!", new Color(1f, 0.7f, 0.5f), 1.1f, 3.2f);
        }

        // ---------- Reactions ----------

        protected override void OnSpotted()
        {
            Say("OI!", new Color(1f, 0.55f, 0.35f), 1.1f, 3.5f);
            Squash(-0.25f);
            nextCharge = Time.time + 1.2f;
        }

        protected override void OnHit(float amount, GameObject source, Vector3 away)
        {
            if (state == State.Idle || state == State.Argue) Enter(State.Chase);
            if (amount >= 20f) Squash(0.2f);
        }

        protected override void OnAllyDied(MudCritter ally)
        {
            Say(ally is HogBrute ? "BROTHER!" : "GRR!", new Color(1f, 0.4f, 0.3f), 1.2f, 3f);
            Buff(1.2f, 6f); // angrier
        }

        public override void OnCarApproaching(DrunkCar car, float secondsAway)
        {
            if (secondsAway > 2f || state == State.Brace || state == State.Charge) return;
            Enter(State.Brace);
            Say("C'MON!", new Color(1f, 0.6f, 0.3f), 1.4f, 3.5f);
            Vector3 toCar = car.transform.position - transform.position;
            toCar.y = 0f;
            if (toCar.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(toCar);
        }

        public override VehicleResponse OnVehicleHit(VehicleHit hit)
        {
            if (hit.Speed < 6f) return VehicleResponse.Bounce;
            if (!hit.Reinforced) return VehicleResponse.Explode; // he holds; the car doesn't
            Say("NOT THE FACE!", new Color(1f, 0.5f, 0.4f), 1f, 3f);
            Sfx.PlayAt("kong_roar", transform.position, 0.6f, 1.8f, 30f);
            Health.TakeDamage(hit.Damage * 1.5f, hit.Car != null && hit.Car.Driver != null ? hit.Car.Driver.gameObject : null);
            Launch(hit.Direction * hit.Speed * 0.35f + Vector3.up * 7f, 400f);
            return VehicleResponse.RunOver;
        }

        // ---------- Animation ----------

        protected override void Animate(float dt)
        {
            if (IsStunned || Airborne) return;
            float t = Time.time;
            float walk = AgentReady ? Agent.velocity.magnitude : 0f;
            if (state == State.Charge) walk = 13f;
            float stride = Mathf.Sin(t * (walk > 6f ? 16f : 7f)) * Mathf.Clamp01(walk / 3f);
            legL.localRotation = Quaternion.Euler(stride * 35f, 0f, 0f);
            legR.localRotation = Quaternion.Euler(-stride * 35f, 0f, 0f);
            float belly = Mathf.Abs(stride) * 0.06f;
            torso.localPosition = new Vector3(0f, 0.95f + belly, 0f);
            Quaternion lean = Quaternion.identity;
            Quaternion left = Quaternion.Euler(Mathf.Sin(t * 7f) * 20f * Mathf.Clamp01(walk / 3f), 0f, -10f);
            Quaternion right = Quaternion.Euler(-Mathf.Sin(t * 7f) * 20f * Mathf.Clamp01(walk / 3f) - 30f, 0f, 10f);
            switch (state)
            {
                case State.Idle:
                    flex = Mathf.Repeat(t * 0.25f + Seed, 1f);
                    if (flex > 0.7f) { left = Quaternion.Euler(-80f, 0f, -110f); right = Quaternion.Euler(-80f, 0f, 110f); lean = Quaternion.Euler(-5f, 0f, 0f); }
                    break;
                case State.Argue:
                    float wave = Mathf.Sin(t * 6f + Seed);
                    left = Quaternion.Euler(-60f + wave * 30f, 0f, -40f);
                    right = Quaternion.Euler(-100f - wave * 20f, 0f, 20f);
                    lean = Quaternion.Euler(8f + wave * 4f, 0f, 0f);
                    break;
                case State.Paw:
                    lean = Quaternion.Euler(18f, 0f, 0f);
                    legR.localRotation = Quaternion.Euler(-30f + Mathf.Sin(stateTime * 19f) * 35f, 0f, 0f);
                    right = Quaternion.Euler(-20f, 0f, 20f);
                    break;
                case State.Charge:
                    lean = Quaternion.Euler(30f, 0f, 0f);
                    left = Quaternion.Euler(40f, 0f, -20f);
                    right = Quaternion.Euler(40f, 0f, 20f);
                    break;
                case State.Stuck:
                    lean = Quaternion.Euler(Mathf.Sin(t * 14f) * 10f, 0f, Mathf.Sin(t * 9f) * 12f);
                    left = Quaternion.Euler(-150f + Mathf.Sin(t * 18f) * 30f, 0f, -30f);
                    right = Quaternion.Euler(-150f - Mathf.Sin(t * 18f) * 30f, 0f, 30f);
                    break;
                case State.Raise:
                    float lift = Mathf.SmoothStep(0f, 1f, stateTime / 0.6f);
                    right = Quaternion.Euler(Mathf.Lerp(-30f, -190f, lift), 0f, 10f);
                    left = Quaternion.Euler(Mathf.Lerp(0f, -170f, lift), 0f, -10f);
                    lean = Quaternion.Euler(-12f * lift, 0f, 0f);
                    break;
                case State.Slam:
                    right = Quaternion.Euler(-40f, 0f, 10f);
                    left = Quaternion.Euler(-30f, 0f, -10f);
                    lean = Quaternion.Euler(25f, 0f, 0f);
                    break;
                case State.Laugh:
                case State.Skid:
                    float shake = Mathf.Sin(t * 30f);
                    lean = Quaternion.Euler(-14f + shake * 3f, 0f, 0f);
                    left = Quaternion.Euler(-20f, 0f, -20f);
                    right = Quaternion.Euler(-20f, 0f, 20f);
                    torso.localPosition += Vector3.up * Mathf.Abs(shake) * 0.05f;
                    break;
                case State.Brace:
                    lean = Quaternion.Euler(-6f, 0f, 0f);
                    left = Quaternion.Euler(-80f, 0f, -110f);
                    right = Quaternion.Euler(-80f, 0f, 110f);
                    break;
            }
            torso.localRotation = Quaternion.Slerp(torso.localRotation, lean, dt * 10f);
            armL.localRotation = Quaternion.Slerp(armL.localRotation, left, dt * 12f);
            armR.localRotation = Quaternion.Slerp(armR.localRotation, right, dt * (state == State.Slam ? 40f : 12f));
        }

        protected override void DeathPose(float t)
        {
            // Topples backwards like a felled tree, then a dusty thud.
            float fall = Mathf.Clamp01(t / 0.9f);
            float angle = 90f * fall * fall;
            Visual.localRotation = VisualRestRotation * Quaternion.Euler(-angle, 0f, 0f);
            Visual.localPosition = VisualRest + Vector3.down * 0.4f * Mathf.Clamp01((t - 2f) / 1f);
            if (fall >= 1f && t - Time.deltaTime < 0.9f)
            {
                ParticleBurst.Spawn(ParticleBurst.Kind.Sand, transform.position - transform.forward * 1.2f, Vector3.up, 30, 0.8f, 3f, 80f, new Color(0.4f, 0.32f, 0.22f, 0.8f));
                CameraShake.Add(0.15f);
                Sfx.PlayAt("mud_splat", transform.position, 0.9f, 0.6f, 30f);
            }
        }
    }
}
