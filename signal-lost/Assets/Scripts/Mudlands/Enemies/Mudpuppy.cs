using SignalLost.Audio;
using SignalLost.Effects;
using UnityEngine;
using Random = UnityEngine.Random;

namespace SignalLost.Mudlands
{
    /// <summary>
    /// Weak swarm fodder: a salamander pup with frilly pink gills that lurks in the mud with only its eyes showing
    /// (bolts fly over it; it can't be hit), pops up with its pack when you come close, zig-zag hops in and nibbles,
    /// then hops back and often dives into its hole again. It sometimes plays dead after a hit, belly up with its
    /// tongue out, and jumps up to bite if you walk past (hit it while it fakes and it takes extra).
    /// It panics at cars, scattering with its arms in the air and sometimes running into another pup.
    /// 20 HP.
    /// </summary>
    public sealed class Mudpuppy : MudCritter
    {
        public static readonly CritterSpec Defaults = new CritterSpec("Mudpuppy", 20f, 0.32f, 0.6f, 3.5f, 14f,
            (Inventory.ScrapType.ScrapMetal, 0, 1), (Inventory.ScrapType.Wiring, 0, 1));

        private enum State { Lurk, Emerge, Hop, Windup, Bite, Retreat, Dive, PlayDead, Panic, Tumble }

        private static readonly Color Skin = new Color(0.5f, 0.42f, 0.3f), Belly = new Color(0.78f, 0.66f, 0.5f),
            Gill = new Color(0.95f, 0.48f, 0.6f), Spot = new Color(0.32f, 0.26f, 0.18f);

        private State state = State.Lurk;
        private float stateTime, hopTime, nextZig = 1f, lurkFor, bob;
        private int fakes;
        private bool bonusHit;
        private Vector3 hopFrom, hopTo, panicDirection;
        private Transform body, tail, mouth, tongue;
        private readonly Transform[] legs = new Transform[4];
        private readonly Transform[] gills = new Transform[6];

        protected override void BuildModel(Transform visual)
        {
            body = MudArt.Pivot(visual, "Body", new Vector3(0f, 0.2f, 0f));
            MudArt.Part(body, "Torso", Vector3.zero, Quaternion.identity, "pup_torso", k =>
            {
                k.Ball(Vector3.zero, new Vector3(0.2f, 0.16f, 0.32f), Quaternion.identity, Skin, 1);
                k.Ball(new Vector3(0f, -0.05f, 0.02f), new Vector3(0.17f, 0.11f, 0.27f), Quaternion.identity, Belly, 1);
                k.Ball(new Vector3(0.09f, 0.11f, -0.05f), new Vector3(0.05f, 0.03f, 0.06f), Quaternion.identity, Spot, 0);
                k.Ball(new Vector3(-0.07f, 0.12f, 0.12f), new Vector3(0.04f, 0.03f, 0.05f), Quaternion.identity, Spot, 0);
            });
            Head = MudArt.Pivot(body, "Head", new Vector3(0f, 0.06f, 0.3f));
            MudArt.Part(Head, "Skull", Vector3.zero, Quaternion.identity, "pup_head", k =>
            {
                k.Ball(new Vector3(0f, 0f, 0.08f), new Vector3(0.22f, 0.15f, 0.19f), Quaternion.identity, Skin, 1);
                k.Ball(new Vector3(0f, -0.06f, 0.1f), new Vector3(0.19f, 0.07f, 0.16f), Quaternion.identity, Belly, 1);
            });
            mouth = MudArt.Part(Head, "Smile", new Vector3(0f, -0.04f, 0.25f), Quaternion.identity, "pup_mouth",
                k => k.Box(Vector3.zero, new Vector3(0.22f, 0.02f, 0.03f), Quaternion.identity, Spot));
            tongue = MudArt.Part(Head, "Tongue", new Vector3(0.03f, -0.07f, 0.26f), Quaternion.Euler(30f, 0f, 0f), "pup_tongue",
                k => k.Box(new Vector3(0f, 0f, 0.05f), new Vector3(0.06f, 0.015f, 0.12f), Quaternion.identity, Gill));
            tongue.gameObject.SetActive(false);
            (Transform l, Transform r) = MudArt.Eyes(Head, new Vector3(0f, 0.11f, 0.14f), 0.1f, 0.06f, MudPalette.White, MudPalette.Black, "pup");
            EyeL = l;
            EyeR = r;
            for (int i = 0; i < 6; i++)
            {
                float side = i < 3 ? -1f : 1f;
                int n = i % 3;
                gills[i] = MudArt.Part(Head, "Gill " + i, new Vector3(0.17f * side, 0.05f, -0.02f), Quaternion.Euler(-40f + n * 40f, 0f, side * (50f + n * 8f)), "pup_gill",
                    k => k.Cone(Vector3.zero, Quaternion.identity, 0.035f, 0.18f, 5, Gill));
            }
            for (int i = 0; i < 4; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f, front = i < 2 ? 1f : -1f;
                legs[i] = MudArt.Part(body, "Leg " + i, new Vector3(0.16f * side, -0.06f, 0.18f * front), Quaternion.identity, "pup_leg", k =>
                {
                    k.Rod(Vector3.zero, new Vector3(0f, -0.14f, 0.02f), 0.035f, 5, Skin);
                    k.Ball(new Vector3(0f, -0.15f, 0.04f), new Vector3(0.05f, 0.025f, 0.06f), Quaternion.identity, Skin, 0);
                });
            }
            tail = MudArt.Part(body, "Tail", new Vector3(0f, 0.02f, -0.28f), Quaternion.identity, "pup_tail", k =>
            {
                k.Prism(Vector3.zero, Quaternion.Euler(-95f, 0f, 0f), 0.09f, 0.01f, 0.42f, 5, Skin);
                k.Box(new Vector3(0f, 0.04f, -0.2f), new Vector3(0.015f, 0.08f, 0.32f), Quaternion.identity, Gill);
            });
        }

        protected override void OnCritterStarted()
        {
            Halt();
            Enter(State.Lurk);
            lurkFor = 0f;
        }

        private void Enter(State next)
        {
            state = next;
            stateTime = 0f;
            switch (next)
            {
                case State.Lurk:
                    SetColliders(false);
                    Halt();
                    break;
                case State.Emerge:
                    SetColliders(true);
                    Sfx.PlayAt("pup_squeak", transform.position, 0.6f, Random.Range(1f, 1.3f), 20f);
                    ParticleBurst.Spawn(ParticleBurst.Kind.Sand, transform.position, Vector3.up, 10, 0.3f, 2.5f, 60f, new Color(0.3f, 0.22f, 0.15f, 0.8f));
                    Squash(-0.4f);
                    break;
                case State.PlayDead:
                    tongue.gameObject.SetActive(true);
                    Halt();
                    Say("x_x", new Color(0.8f, 0.8f, 0.8f), 1.2f, 3f);
                    break;
            }
            if (next != State.PlayDead) tongue.gameObject.SetActive(false);
        }

        // ---------- Brain ----------

        protected override void Think(float dt)
        {
            stateTime += dt;
            switch (state)
            {
                case State.Lurk:
                    bob += dt;
                    lurkFor -= dt;
                    if (Random.value < dt * 0.6f) ParticleBurst.Spawn(ParticleBurst.Kind.Mist, transform.position + Vector3.up * 0.05f, Vector3.up, 1, 0.15f, 0.4f, 20f, new Color(0.4f, 0.35f, 0.25f, 0.6f));
                    if (lurkFor <= 0f && Notices() && PlayerDistance < 9f) Enter(State.Emerge);
                    break;
                case State.Emerge:
                    if (stateTime >= 0.35f) StartHop();
                    break;
                case State.Hop: UpdateHop(); break;
                case State.Windup:
                    Halt();
                    TurnToward(PlayerPosition, 720f * dt);
                    if (stateTime >= 0.28f) { Enter(State.Bite); Squash(-0.35f); }
                    break;
                case State.Bite:
                    if (stateTime >= 0.08f && stateTime - dt < 0.08f)
                    {
                        if (Strike(1.7f, 110f, 5f)) Say("nom", new Color(1f, 0.7f, 0.7f), 0.6f, 2.5f);
                        Sfx.PlayAt("crab_snip", transform.position, 0.5f, 1.8f, 20f);
                    }
                    if (stateTime >= 0.3f) StartRetreat();
                    break;
                case State.Retreat:
                    MoveTo(hopTo, 6f);
                    if (stateTime >= 0.6f)
                    {
                        if (FlatDistance(Home) < 7f && Random.value < 0.45f) Enter(State.Dive);
                        else StartHop();
                    }
                    break;
                case State.Dive:
                    MoveTo(Home, 6f);
                    if (FlatDistance(Home) < 0.6f || stateTime > 2.5f)
                    {
                        lurkFor = Random.Range(1.2f, 2.6f);
                        Enter(State.Lurk);
                        Sfx.PlayAt("mud_splat", transform.position, 0.4f, 1.4f, 20f);
                    }
                    break;
                case State.PlayDead:
                    if (stateTime > 0.8f && PlayerDistance < 1.7f)
                    {
                        Say("SURPRISE!", new Color(1f, 0.5f, 0.3f), 1f, 3f);
                        Enter(State.Windup);
                        stateTime = 0.15f;
                    }
                    else if (stateTime > 3f) StartHop();
                    break;
                case State.Panic:
                    MoveTo(transform.position + panicDirection * 4f, 6.5f);
                    foreach (MudCritter other in Living)
                    {
                        if (other == this || !(other is Mudpuppy pup) || pup.state != State.Panic) continue;
                        if ((other.transform.position - transform.position).sqrMagnitude > 0.7f) continue;
                        Bonk();
                        pup.Bonk();
                        break;
                    }
                    if (stateTime > 1.3f) StartHop();
                    break;
                case State.Tumble:
                    if (stateTime > 0.7f) StartHop();
                    break;
            }
            if (GaveUp()) { lurkFor = 0.5f; Enter(State.Dive); }
            if (state != State.PlayDead && state != State.Lurk) WatchPlayer();
        }

        private void Bonk()
        {
            if (state == State.Tumble) return;
            Enter(State.Tumble);
            Halt();
            Say("@_@", new Color(1f, 1f, 0.6f), 1f, 3f);
            Squash(0.4f);
        }

        private void StartHop()
        {
            if (!TargetAlive) { Enter(State.Dive); return; }
            Enter(State.Hop);
            NextHop();
        }

        private void NextHop()
        {
            hopTime = 0f;
            hopFrom = transform.position;
            Vector3 toPlayer = PlayerPosition - transform.position;
            toPlayer.y = 0f;
            float distance = toPlayer.magnitude;
            Vector3 side = Vector3.Cross(Vector3.up, toPlayer.normalized);
            nextZig = -nextZig;
            float step = Mathf.Min(2.1f, Mathf.Max(0.5f, distance - 1.2f));
            hopTo = transform.position + toPlayer.normalized * step + side * nextZig * Random.Range(0.7f, 1.4f);
            Squash(0.25f);
        }

        private void UpdateHop()
        {
            hopTime += Time.deltaTime;
            float duration = 0.38f / Tempo;
            MoveTo(hopTo, Vector3.Distance(hopFrom, hopTo) / duration);
            if (hopTime < duration) return;
            Squash(0.3f);
            if (Random.value < 0.25f) Sfx.PlayAt("pup_squeak", transform.position, 0.3f, Random.Range(1.2f, 1.6f), 15f);
            if (PlayerDistance < 1.6f) { Enter(State.Windup); Squash(0.35f); return; }
            if (!Notices() && !ReturningHome) { Enter(State.Dive); return; }
            NextHop();
        }

        private void StartRetreat()
        {
            Enter(State.Retreat);
            Vector3 away = transform.position - PlayerPosition;
            away.y = 0f;
            hopTo = transform.position + away.normalized * Random.Range(2f, 3f);
        }

        // ---------- Reactions ----------

        protected override void OnSpotted()
        {
            base.OnSpotted();
            // The whole den comes up together.
            foreach (MudCritter other in Living)
                if (other is Mudpuppy pup && pup != this && pup.state == State.Lurk && (pup.transform.position - transform.position).sqrMagnitude < 64f)
                {
                    pup.lurkFor = Random.Range(0.05f, 0.4f);
                    pup.LastSeen = Time.time;
                }
        }

        protected override void OnHit(float amount, GameObject source, Vector3 away)
        {
            if (state == State.Lurk) { Enter(State.Emerge); return; }
            if (state == State.PlayDead && !bonusHit)
            {
                bonusHit = true;
                Say("OW!", new Color(1f, 0.5f, 0.4f), 0.8f, 3f);
                Health.TakeDamage(amount * 0.5f, source); // caught faking
                bonusHit = false;
                if (!IsDead) StartHop();
                return;
            }
            if (fakes < 2 && Random.value < 0.3f)
            {
                fakes++;
                Enter(State.PlayDead);
                return;
            }
            Squash(0.3f);
            Sfx.PlayAt("pup_squeak", transform.position, 0.5f, Random.Range(1.5f, 1.8f), 20f);
            if (state == State.Windup || state == State.Bite) StartRetreat();
        }

        protected override void OnPlayerHurt()
        {
            if (state == State.Lurk || state == State.PlayDead) return;
            base.OnPlayerHurt();
        }

        public override void OnCarApproaching(DrunkCar car, float secondsAway)
        {
            if (secondsAway > 1.8f || state == State.Panic || state == State.Lurk || state == State.PlayDead || state == State.Tumble) return;
            if (FlatDistance(Home) < 3f) { Enter(State.Dive); return; }
            Enter(State.Panic);
            Say(Random.value < 0.5f ? "AAAA!" : "EEK!", new Color(1f, 0.6f, 0.6f), 1f, 3f);
            Vector3 path = car.Velocity;
            path.y = 0f;
            Vector3 side = Vector3.Cross(Vector3.up, path.normalized);
            panicDirection = Vector3.Dot(side, transform.position - car.transform.position) >= 0f ? side : -side;
            panicDirection = (panicDirection + Random.insideUnitSphere * 0.6f).normalized;
            panicDirection.y = 0f;
        }

        public override VehicleResponse OnVehicleHit(VehicleHit hit)
        {
            if (state == State.Lurk) return VehicleResponse.Ignore; // in the mud: the car rolls over it
            if (hit.Speed < 6f) return VehicleResponse.Bounce;
            Sfx.PlayAt("pup_squeak", transform.position, 0.8f, 2f, 30f);
            RunOver(hit, 0.8f);
            return VehicleResponse.RunOver;
        }

        // ---------- Animation ----------

        protected override void Animate(float dt)
        {
            float t = Time.time;
            float sink = state == State.Lurk ? -0.4f : state == State.Emerge ? Mathf.Lerp(-0.4f, 0f, stateTime / 0.35f) : 0f;
            float hopLift = state == State.Hop ? Mathf.Sin(Mathf.Clamp01(hopTime / (0.38f / Tempo)) * Mathf.PI) * 0.3f :
                state == State.Retreat ? Mathf.Abs(Mathf.Sin(stateTime * 10f)) * 0.2f :
                state == State.Panic ? Mathf.Abs(Mathf.Sin(stateTime * 18f)) * 0.15f : 0f;
            if (!IsDead && !Airborne && !IsStunned)
            {
                Visual.localPosition = VisualRest + Vector3.up * (sink + hopLift + (state == State.Lurk ? Mathf.Sin(bob * 2f) * 0.02f : 0f));
                float roll = state == State.PlayDead ? Mathf.Min(1f, stateTime / 0.2f) * 175f : state == State.Tumble ? Mathf.Sin(stateTime * 20f) * 40f : 0f;
                float pitch = state == State.Windup ? -15f : state == State.Bite ? 12f : 0f;
                Visual.localRotation = VisualRestRotation * Quaternion.Euler(pitch, 0f, roll);
            }
            if (tail != null) tail.localRotation = Quaternion.Euler(0f, Mathf.Sin(t * (state == State.Panic ? 25f : 7f)) * 25f, 0f);
            for (int i = 0; i < gills.Length; i++)
                if (gills[i] != null) gills[i].localScale = Vector3.one * (1f + Mathf.Sin(t * 9f + i) * 0.12f);
            for (int i = 0; i < 4; i++)
            {
                if (legs[i] == null) continue;
                bool front = i < 2;
                float swing = state == State.Panic && front ? -120f + Mathf.Sin(t * 30f + i) * 50f :
                    state == State.PlayDead ? Mathf.Sin(t * 3f + i) * 10f :
                    Mathf.Sin(t * 14f + i * 1.7f) * (state == State.Hop || state == State.Retreat ? 35f : 6f);
                legs[i].localRotation = Quaternion.Euler(swing, 0f, 0f);
            }
            if (mouth != null) mouth.localScale = new Vector3(1f, state == State.Windup ? 4f : state == State.Bite ? 2.5f : 1f, 1f);
        }

        protected override void DeathPose(float t)
        {
            // Curls up into a ball, eyes shut, and sinks into the mud.
            float k = Mathf.SmoothStep(0f, 1f, t / 0.4f);
            Visual.localRotation = VisualRestRotation * Quaternion.Euler(0f, 0f, 160f * k);
            Visual.localPosition = VisualRest + Vector3.up * (0.15f * Mathf.Sin(k * Mathf.PI) - 0.5f * Mathf.Clamp01((t - 1.2f) / 1.4f));
            if (tail != null) tail.localRotation = Quaternion.Euler(-60f * k, 0f, 0f);
            if (EyeL != null) { EyeL.localScale = new Vector3(1f, 0.1f, 1f); EyeR.localScale = new Vector3(1f, 0.1f, 1f); }
        }
    }
}
