using System.Collections.Generic;
using SignalLost.Audio;
using SignalLost.Effects;
using UnityEngine;
using Random = UnityEngine.Random;

namespace SignalLost.Mudlands
{
    /// <summary>
    /// Weak swarm fodder: an alien salamander pup (teal, with glowing frilly gills and eyes on short stalks) that lurks in the mud with only its eyes showing
    /// (bolts fly over it; it can't be hit), pops up with its pack when you come close, zig-zag hops in and nibbles,
    /// then hops back and often dives into its hole again. It sometimes plays dead after a hit, belly up with its
    /// tongue out, and jumps up to bite if you walk past (hit it while it fakes and it takes extra).
    /// It panics at cars, scattering with its arms in the air and sometimes running into another pup.
    /// Latching (11-20, like the desert's Spark Tick): from a few metres it crouches for a beat, then leaps at you. A
    /// leap that connects clamps onto your visor (you see it chewing on the edge of the screen): 2 every 0.6 s and 10%
    /// slower per pup, up to three at once. A swing swats up to two off (they take 10 and land dazed); a jump throws
    /// them all off. A sidestepped leap lands it dazed.
    /// 20 HP.
    /// </summary>
    public sealed class Mudpuppy : MudCritter
    {
        public static readonly CritterSpec Defaults = new CritterSpec("Mudpuppy", 20f, 0.32f, 0.6f, 3.5f, 14f,
            (Inventory.ScrapType.ScrapMetal, 0, 1), (Inventory.ScrapType.Wiring, 0, 1));

        private enum State { Lurk, Emerge, Hop, Windup, Bite, Retreat, Dive, PlayDead, Panic, Tumble, Crouch, Leap, Latched }

        // ---------- Latching numbers ----------
        private const float LeapRange = 3.6f, LeapChance = 0.55f, CrouchTime = 0.3f, LeapTime = 0.42f, LeapHeight = 0.9f, LeapCooldown = 2.2f;
        private const float ChewDamage = 2f, ChewInterval = 0.6f, SlowPerPup = 0.1f, SwatDamage = 10f, LatchedScale = 0.38f;
        private const int MaxClinging = 3, SwatsPerSwing = 2;
        // Where clinging pups sit on the view, in metres right/up of the screen centre, 0.62 m in front of the camera:
        // hanging off the bottom corners and the bottom edge, just above the HUD, clear of the crosshair.
        private static readonly Vector2[] Slots = { new Vector2(-0.44f, -0.34f), new Vector2(0.44f, -0.34f), new Vector2(-0.12f, -0.4f) };
        private static readonly List<Mudpuppy> Clinging = new List<Mudpuppy>();
        private static int swatFrame = -1, swatsThisFrame;

        /// <summary>How many pups are clinging to the player right now (for the on-screen hint).</summary>
        public static int ClingingCount => Clinging.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetClinging()
        {
            Clinging.Clear();
            swatFrame = -1;
        }

        private Vector3 leapFrom, leapTo;
        private int slot;
        private float nextChew, nextLeap;
        private bool wasSwinging, wasRising;
        private SignalLost.Player.FirstPersonController walker;

        // Alien colours: teal skin, a mint belly, violet spots and glowing magenta gills.
        private static readonly Color Skin = new Color(0.27f, 0.52f, 0.56f), Belly = new Color(0.72f, 0.9f, 0.82f),
            Gill = new Color(1f, 0.4f, 0.78f), Spot = new Color(0.4f, 0.26f, 0.58f);

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
                // Eye stalks: the eyes sit up on them, so a lurking pup is a pair of eyes on stalks poking out of the mud.
                foreach (float x in new[] { -0.1f, 0.1f }) k.Rod(new Vector3(x, 0.06f, 0.12f), new Vector3(x, 0.2f, 0.14f), 0.024f, 5, Skin, 0f, 0.018f);
            });
            mouth = MudArt.Part(Head, "Smile", new Vector3(0f, -0.04f, 0.25f), Quaternion.identity, "pup_mouth",
                k => k.Box(Vector3.zero, new Vector3(0.22f, 0.02f, 0.03f), Quaternion.identity, Spot));
            tongue = MudArt.Part(Head, "Tongue", new Vector3(0.03f, -0.07f, 0.26f), Quaternion.Euler(30f, 0f, 0f), "pup_tongue",
                k => k.Box(new Vector3(0f, 0f, 0.05f), new Vector3(0.06f, 0.015f, 0.12f), Quaternion.identity, Gill));
            tongue.gameObject.SetActive(false);
            (Transform l, Transform r) = MudArt.Eyes(Head, new Vector3(0f, 0.21f, 0.14f), 0.1f, 0.06f, MudPalette.White, MudPalette.Black, "pup");
            EyeL = l;
            EyeR = r;
            for (int i = 0; i < 6; i++)
            {
                float side = i < 3 ? -1f : 1f;
                int n = i % 3;
                gills[i] = MudArt.Part(Head, "Gill " + i, new Vector3(0.17f * side, 0.05f, -0.02f), Quaternion.Euler(-40f + n * 40f, 0f, side * (50f + n * 8f)), "pup_gill",
                    k => k.Cone(Vector3.zero, Quaternion.identity, 0.035f, 0.18f, 5, Gill, 1.2f));
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
            if (state == State.Latched && next != State.Latched) Unclamp();
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
                    nextLeap = Mathf.Max(nextLeap, Time.time + Random.Range(0.4f, 1.4f));
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
                case State.Crouch:
                    Halt();
                    TurnToward(PlayerPosition, 900f * dt);
                    if (stateTime >= CrouchTime) BeginLeap();
                    break;
                case State.Leap: UpdateLeap(); break;
                case State.Latched: UpdateLatched(); break;
            }
            if (state == State.Latched || state == State.Leap) return; // riding the player: no leash, no head-turning
            if (GaveUp()) { lurkFor = 0.5f; Enter(State.Dive); }
            if (state != State.PlayDead && state != State.Lurk) WatchPlayer();
        }

        // ---------- Leaping and latching ----------

        private static bool PlayerDriving => DrunkDriving.Instance != null && DrunkDriving.Instance.Current == DrunkDriving.State.Driving;

        private bool CanLeap()
        {
            if (!TargetAlive || PlayerDriving || Time.time < nextLeap || Clinging.Count >= MaxClinging) return false;
            float distance = PlayerDistance;
            return distance <= LeapRange && distance >= 0.8f && Random.value < LeapChance && CanSee(LeapRange + 1f);
        }

        // The tell: a quick crouch with its gills flared and a squeak, then the leap.
        private void StartCrouch()
        {
            Enter(State.Crouch);
            Squash(0.4f);
            Sfx.PlayAt("pup_squeak", transform.position, 0.6f, Random.Range(0.85f, 0.95f), 20f);
        }

        private void BeginLeap()
        {
            if (!TargetAlive) { StartHop(); return; }
            leapFrom = transform.position;
            // At where the player stands now, a little past them, so a late sidestep makes it miss.
            Vector3 aim = PlayerPosition + FlatDirectionTo(PlayerPosition) * 0.3f;
            leapTo = new Vector3(aim.x, PlayerPosition.y, aim.z);
            Halt();
            if (Agent != null) Agent.enabled = false;
            Vector3 face = FlatDirectionTo(leapTo);
            if (face.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(face);
            Enter(State.Leap);
            Squash(-0.45f);
            nextLeap = Time.time + LeapCooldown;
        }

        private void UpdateLeap()
        {
            float t = Mathf.Clamp01(stateTime / LeapTime);
            Vector3 position = Vector3.Lerp(leapFrom, leapTo, t);
            position.y += Mathf.Sin(t * Mathf.PI) * LeapHeight;
            transform.position = position;
            if (TargetAlive && TouchesPlayer(position + Vector3.up * 0.2f))
            {
                if (Clinging.Count < MaxClinging && !PlayerDriving) { Clamp(); return; }
                MissedLeap(); // no room: bounces off
                return;
            }
            if (t >= 1f) MissedLeap();
        }

        /// <summary>True if this point is inside the player's capsule (with a little slack).</summary>
        private bool TouchesPlayer(Vector3 point)
        {
            Vector3 feet = PlayerPosition;
            float height = TargetBody != null ? TargetBody.height : 1.8f;
            float radius = (TargetBody != null ? TargetBody.radius : 0.4f) + 0.3f;
            Vector3 flat = point - feet;
            float up = flat.y;
            flat.y = 0f;
            return flat.magnitude <= radius && up >= -0.3f && up <= height + 0.3f;
        }

        private void MissedLeap()
        {
            Settle();
            Enter(State.Tumble);
            Say("@_@", new Color(1f, 1f, 0.6f), 0.9f, 3f);
            Squash(0.4f);
            Sfx.PlayAt("mud_splat", transform.position, 0.35f, 1.6f, 20f);
        }

        // Back on its feet on the NavMesh (or left where it is if there is none close).
        private void Settle()
        {
            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            AgentUtil.Reenable(Agent, 3f);
        }

        private void Clamp()
        {
            Enter(State.Latched);
            Clinging.Add(this);
            slot = FreeSlot();
            SetColliders(false); // can't be shot and doesn't block the player while it clings on
            transform.localScale = Vector3.one * LatchedScale;
            nextChew = Time.time + ChewInterval;
            wasSwinging = PlayerSwinging();
            if (walker == null && Target != null) walker = Target.GetComponentInParent<SignalLost.Player.FirstPersonController>();
            wasRising = Rising;
            Sfx.Play("pup_squeak", 0.7f, Random.Range(1.7f, 1.9f));
            Sfx.Play("crab_snip", 0.5f, 1.9f);
            CameraShake.Add(0.12f);
            CritterOverlay.Ensure();
        }

        private int FreeSlot()
        {
            for (int s = 0; s < Slots.Length; s++)
            {
                bool used = false;
                foreach (Mudpuppy other in Clinging)
                    if (other != this && other.slot == s) used = true;
                if (!used) return s;
            }
            return 0;
        }

        // Leaving the Latched state (any way): off the player's face and back to full size and solid.
        private void Unclamp()
        {
            Clinging.Remove(this);
            transform.localScale = Vector3.one;
            if (!IsDead && !Health.IsDead) SetColliders(true);
        }

        private void OnDestroy() => Clinging.Remove(this);

        private bool PlayerSwinging()
        {
            if (TargetLaser != null && TargetLaser.isActiveAndEnabled && TargetLaser.IsSwinging) return true;
            var fists = Target != null ? Target.GetComponentInParent<SignalLost.Melee.PlayerFists>() : null;
            return fists != null && fists.isActiveAndEnabled && fists.IsBusy;
        }

        private void UpdateLatched()
        {
            if (!TargetAlive || PlayerDriving || TargetBody == null || !TargetBody.enabled) { ThrownOff(false); return; }

            if (Time.time >= nextChew)
            {
                nextChew = Time.time + ChewInterval;
                TargetHealth?.TakeDamage(ChewDamage, gameObject);
                Squash(0.25f);
                if (Random.value < 0.5f) Sfx.Play("crab_snip", 0.3f, Random.Range(1.8f, 2.1f));
            }
            if (Clinging.Count > 0 && Clinging[0] == this)
                MudlandsPlayerSlow.Ensure(TargetBody.gameObject).SlowFor(Clinging.Count * SlowPerPup, 0.2f);

            // A swing swats up to two off; a jump throws them all off.
            bool swinging = PlayerSwinging();
            if (swinging && !wasSwinging)
            {
                if (swatFrame != Time.frameCount) { swatFrame = Time.frameCount; swatsThisFrame = 0; }
                if (swatsThisFrame < SwatsPerSwing)
                {
                    swatsThisFrame++;
                    ThrownOff(true);
                    return;
                }
            }
            wasSwinging = swinging;

            // The jump read from the controller itself: the mud slow's extra Move hides it from CharacterController.velocity.
            bool rising = Rising;
            bool jumped = rising && !wasRising;
            wasRising = rising;
            if (jumped) ThrownOff(false);
        }

        private bool Rising => walker != null ? walker.VerticalSpeed > 2f : TargetBody != null && TargetBody.velocity.y > 2f;

        /// <summary>Comes off the player: swatted (hurt, flung further) or shaken off by a jump; lands dazed.</summary>
        private void ThrownOff(bool swatted)
        {
            Transform view = TargetView != null ? TargetView : transform;
            Vector2 s = Slots[Mathf.Clamp(slot, 0, Slots.Length - 1)];
            float side = s.x < -0.01f ? -1f : s.x > 0.01f ? 1f : (Random.value < 0.5f ? -1f : 1f);
            Vector3 sideways = new Vector3(view.right.x, 0f, view.right.z).normalized * side;
            Vector3 ahead = new Vector3(view.forward.x, 0f, view.forward.z).normalized;
            Vector3 feet = Target != null ? Target.position : transform.position;
            Enter(State.Tumble); // unclamps
            transform.position = feet + Vector3.up * 1.1f + sideways * 0.7f + ahead * 0.5f;
            transform.rotation = Quaternion.LookRotation(-sideways);
            Say(swatted ? "!!" : "!?", new Color(1f, 0.7f, 0.5f), 0.8f, 3f);
            Sfx.PlayAt("pup_squeak", transform.position, 0.6f, Random.Range(1.4f, 1.7f), 20f);
            Launch((sideways * (swatted ? 5f : 3f) + ahead * 1.5f) + Vector3.up * (swatted ? 3.5f : 2.5f), swatted ? 720f : 400f);
            if (swatted) Health.TakeDamage(SwatDamage, Target != null ? Target.gameObject : null);
        }

        // Clinging pups stay glued to the view after the camera has moved this frame.
        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (state != State.Latched || IsDead || TargetView == null) return;
            Transform view = TargetView;
            Vector2 s = Slots[Mathf.Clamp(slot, 0, Slots.Length - 1)];
            float wiggle = Mathf.Sin(Time.time * 19f + slot * 2f) * 0.012f;
            transform.position = view.position + view.forward * 0.62f + view.right * (s.x + wiggle) + view.up * (s.y + Mathf.Abs(wiggle));
            // Face the camera, nose up into the middle of the screen, tilted toward its corner.
            transform.rotation = Quaternion.LookRotation(-view.forward, view.up) * Quaternion.Euler(-35f, 0f, -s.x * 70f);
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
            if (CanLeap()) { StartCrouch(); return; }
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
            if (state == State.Latched || state == State.Leap) return; // a blast caught it on the player: just the damage
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
            if (secondsAway > 1.8f || state == State.Panic || state == State.Lurk || state == State.PlayDead || state == State.Tumble ||
                state == State.Latched || state == State.Leap) return;
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
            if (state == State.Latched) { AnimateClinging(t); return; }
            float sink = state == State.Lurk ? -0.4f : state == State.Emerge ? Mathf.Lerp(-0.4f, 0f, stateTime / 0.35f) :
                state == State.Crouch ? -0.08f * Mathf.Clamp01(stateTime / 0.12f) : 0f;
            float hopLift = state == State.Hop ? Mathf.Sin(Mathf.Clamp01(hopTime / (0.38f / Tempo)) * Mathf.PI) * 0.3f :
                state == State.Retreat ? Mathf.Abs(Mathf.Sin(stateTime * 10f)) * 0.2f :
                state == State.Panic ? Mathf.Abs(Mathf.Sin(stateTime * 18f)) * 0.15f : 0f;
            if (!IsDead && !Airborne && !IsStunned)
            {
                Visual.localPosition = VisualRest + Vector3.up * (sink + hopLift + (state == State.Lurk ? Mathf.Sin(bob * 2f) * 0.02f : 0f));
                float roll = state == State.PlayDead ? Mathf.Min(1f, stateTime / 0.2f) * 175f : state == State.Tumble ? Mathf.Sin(stateTime * 20f) * 40f : 0f;
                float pitch = state == State.Windup ? -15f : state == State.Bite ? 12f : state == State.Crouch ? -12f : state == State.Leap ? -30f : 0f;
                Visual.localRotation = VisualRestRotation * Quaternion.Euler(pitch, 0f, roll);
            }
            if (tail != null) tail.localRotation = Quaternion.Euler(0f, Mathf.Sin(t * (state == State.Panic ? 25f : 7f)) * 25f, 0f);
            for (int i = 0; i < gills.Length; i++)
                if (gills[i] != null) gills[i].localScale = Vector3.one * ((state == State.Crouch || state == State.Leap ? 1.45f : 1f) + Mathf.Sin(t * 9f + i) * 0.12f);
            for (int i = 0; i < 4; i++)
            {
                if (legs[i] == null) continue;
                bool front = i < 2;
                float swing = state == State.Leap ? (front ? -75f : 55f) :
                    state == State.Panic && front ? -120f + Mathf.Sin(t * 30f + i) * 50f :
                    state == State.PlayDead ? Mathf.Sin(t * 3f + i) * 10f :
                    Mathf.Sin(t * 14f + i * 1.7f) * (state == State.Hop || state == State.Retreat ? 35f : 6f);
                legs[i].localRotation = Quaternion.Euler(swing, 0f, 0f);
            }
            if (mouth != null) mouth.localScale = new Vector3(1f, state == State.Windup || state == State.Leap ? 4f : state == State.Bite ? 2.5f : 1f, 1f);
        }

        // On the player's visor: chomping, gills flared, legs scrabbling, tail whipping.
        private void AnimateClinging(float t)
        {
            Visual.localPosition = VisualRest;
            Visual.localRotation = VisualRestRotation * Quaternion.Euler(0f, 0f, Mathf.Sin(t * 13f + slot) * 9f);
            if (Head != null) Head.localRotation = Quaternion.Euler(-18f + Mathf.Sin(t * 26f) * 16f, Mathf.Sin(t * 7f) * 10f, 0f);
            if (mouth != null) mouth.localScale = new Vector3(1f, 2.5f + Mathf.Sin(t * 26f) * 1.5f, 1f);
            if (tail != null) tail.localRotation = Quaternion.Euler(0f, Mathf.Sin(t * 22f) * 40f, 0f);
            for (int i = 0; i < gills.Length; i++)
                if (gills[i] != null) gills[i].localScale = Vector3.one * (1.4f + Mathf.Sin(t * 16f + i) * 0.2f);
            for (int i = 0; i < 4; i++)
                if (legs[i] != null) legs[i].localRotation = Quaternion.Euler(Mathf.Sin(t * 30f + i * 1.9f) * 45f, 0f, 0f);
        }

        protected override void OnCritterDeath()
        {
            // Killed while clinging on (a blast, the player dying): it drops off rather than dying on the screen.
            if (state == State.Latched) ThrownOff(false);
            else if (state == State.Leap) Settle();
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
