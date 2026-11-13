using SignalLost.Audio;
using SignalLost.Combat;
using SignalLost.Effects;
using SignalLost.Inventory;
using UnityEngine;
using Random = UnityEngine.Random;

namespace SignalLost.Mudlands
{
    /// <summary>
    /// Heavy area denial: a grumpy blue alien snapping turtle whose shell is riveted hull panels off the crashed ship,
    /// stencilled and striped like a road barrier. It parks
    /// on a road, blows a whistle and waves a STOP sign, and turns slowly to keep its front to you. Bolts glance off
    /// the front of its shell (they bounce back at 30%). Up close it pulls its neck back with its eyes glinting
    /// (0.9 s) and snaps (25, 3 m). A full-charge bolt or a heavy jump smash flips it onto its back for 3 s, legs
    /// waving, belly up for double damage. Cars: head-on it hides in its shell and the car blows up on it; from the
    /// side or behind it is flipped. 160 HP.
    /// </summary>
    public sealed class RoadblockTurtle : MudCritter, IBoltReflector
    {
        public static readonly CritterSpec Defaults = new CritterSpec("Roadblock Turtle", 160f, 0.9f, 1.4f, 1.6f, 13f,
            (ScrapType.ScrapMetal, 3, 4), (ScrapType.CircuitBoards, 0, 1), (ScrapType.FuelCells, 0, 1));

        private enum State { Guard, Advance, Windup, Snap, Hide, Flipped, Righting }

        // Alien colours: deep blue skin and a pale aqua belly.
        private static readonly Color Skin = new Color(0.2f, 0.4f, 0.55f), Belly = new Color(0.7f, 0.86f, 0.8f);

        private State state = State.Guard;
        private float stateTime, nextSnap, nextWhistle;
        private bool bonusHit;
        private Transform shell, neck, sign, eyeGlint;
        private readonly Transform[] legs = new Transform[4];

        protected override void BuildModel(Transform visual)
        {
            shell = MudArt.Pivot(visual, "Shell", new Vector3(0f, 0.75f, 0f));
            MudArt.Part(shell, "Barricade Shell", Vector3.zero, Quaternion.identity, "turtle_shell", k =>
            {
                k.Ball(new Vector3(0f, -0.1f, 0f), new Vector3(0.95f, 0.55f, 1.05f), Quaternion.identity, MudPalette.HullDark, 1);
                // Hull panels riveted on in layers, every other one in the ship's orange like a road barrier.
                for (int i = 0; i < 4; i++)
                {
                    float y = -0.05f + i * 0.14f, w = 1.9f - i * 0.35f, d = 1.6f - i * 0.25f, z = 0.15f - i * 0.05f;
                    k.Box(new Vector3(0f, y, z), new Vector3(w, 0.12f, d), Quaternion.identity, i % 2 == 0 ? MudPalette.Hull : MudPalette.HullStripe);
                    for (int r = 0; r < 6; r++) // rivets along the front and back edges
                        k.Ball(new Vector3(-w * 0.42f + r * w * 0.168f, y + 0.06f, z + (r % 2 == 0 ? 1f : -1f) * d * 0.46f), 0.022f, MudPalette.HullDark, 0);
                }
                k.Box(new Vector3(0f, -0.4f, 0f), new Vector3(1.6f, 0.18f, 1.8f), Belly);
                // A stencilled hull panel on the front edge (where the "ROAD CLOSED" plank was).
                Quaternion front = Quaternion.Euler(-12f, 0f, 0f);
                k.ScrapPanel(new Vector3(0f, 0.05f, 0.92f), new Vector3(1.3f, 0.3f, 0.06f), front, MudPalette.Hull);
                foreach (float x in new[] { -0.36f, -0.22f, 0.22f, 0.36f })
                    k.Box(new Vector3(0f, 0.02f, 0.92f) + front * new Vector3(x, 0f, 0.035f), new Vector3(0.07f, 0.12f, 0.01f), front, MudPalette.Black);
                k.Box(new Vector3(0f, 0.02f, 0.92f) + front * new Vector3(0f, 0f, 0.035f), new Vector3(0.22f, 0.04f, 0.01f), front * Quaternion.Euler(0f, 0f, 45f), MudPalette.Black);
                k.Box(new Vector3(0f, 0.02f, 0.92f) + front * new Vector3(0f, 0f, 0.035f), new Vector3(0.22f, 0.04f, 0.01f), front * Quaternion.Euler(0f, 0f, -45f), MudPalette.Black);
                // A ship's warning beacon stuck on top (where the traffic cone was).
                k.Prism(new Vector3(0.55f, 0.42f, -0.2f), Quaternion.identity, 0.12f, 0.1f, 0.12f, 8, MudPalette.HullDark);
                k.Ball(new Vector3(0.55f, 0.56f, -0.2f), new Vector3(0.09f, 0.1f, 0.09f), Quaternion.identity, MudPalette.HullStripe, 1, 2.5f);
            });
            neck = MudArt.Pivot(visual, "Neck", new Vector3(0f, 0.6f, 0.9f));
            Head = MudArt.Pivot(neck, "Head", new Vector3(0f, 0.08f, 0.35f));
            MudArt.Part(neck, "Neck Skin", Vector3.zero, Quaternion.identity, "turtle_neck", k => k.Rod(Vector3.zero, new Vector3(0f, 0.08f, 0.35f), 0.17f, 7, Skin));
            MudArt.Part(Head, "Face", Vector3.zero, Quaternion.identity, "turtle_head", k =>
            {
                k.Ball(new Vector3(0f, 0f, 0.05f), new Vector3(0.24f, 0.2f, 0.28f), Quaternion.identity, Skin, 1);
                k.Cone(new Vector3(0f, -0.05f, 0.25f), Quaternion.Euler(110f, 0f, 0f), 0.09f, 0.16f, 4, new Color(0.3f, 0.3f, 0.25f)); // hooked beak
                k.Box(new Vector3(-0.1f, 0.15f, 0.16f), new Vector3(0.14f, 0.04f, 0.04f), Quaternion.Euler(0f, 0f, -18f), MudPalette.Black); // grumpy brows
                k.Box(new Vector3(0.1f, 0.15f, 0.16f), new Vector3(0.14f, 0.04f, 0.04f), Quaternion.Euler(0f, 0f, 18f), MudPalette.Black);
                k.Box(new Vector3(0.12f, -0.1f, 0.22f), new Vector3(0.12f, 0.03f, 0.03f), MudPalette.Metal); // the whistle
            });
            (Transform l, Transform r) = MudArt.Eyes(Head, new Vector3(0f, 0.08f, 0.2f), 0.1f, 0.045f, new Color(0.95f, 0.9f, 0.6f), MudPalette.Black, "turtle");
            EyeL = l;
            EyeR = r;
            eyeGlint = MudArt.Part(Head, "Glint", new Vector3(0f, 0.08f, 0.26f), Quaternion.identity, "turtle_glint",
                k => { k.Ball(new Vector3(-0.1f, 0f, 0f), 0.05f, new Color(1f, 0.25f, 0.2f), 0, 4f); k.Ball(new Vector3(0.1f, 0f, 0f), 0.05f, new Color(1f, 0.25f, 0.2f), 0, 4f); });
            eyeGlint.gameObject.SetActive(false);
            sign = MudArt.Pivot(visual, "Stop Sign", new Vector3(0.75f, 0.6f, 0.6f));
            MudArt.Part(sign, "Sign", Vector3.zero, Quaternion.identity, "turtle_sign", k =>
            {
                // A hatch cover off the ship on a strut, with a big painted orange X: this turtle's STOP sign.
                k.Rod(Vector3.zero, new Vector3(0f, 1.1f, 0f), 0.03f, 5, MudPalette.Metal);
                k.ScrapPanel(new Vector3(0f, 1.3f, 0f), new Vector3(0.52f, 0.52f, 0.04f), Quaternion.identity, MudPalette.Hull, false);
                k.Box(new Vector3(0f, 1.3f, 0.025f), new Vector3(0.44f, 0.08f, 0.01f), Quaternion.Euler(0f, 0f, 45f), MudPalette.HullStripe);
                k.Box(new Vector3(0f, 1.3f, 0.025f), new Vector3(0.44f, 0.08f, 0.01f), Quaternion.Euler(0f, 0f, -45f), MudPalette.HullStripe);
            });
            for (int i = 0; i < 4; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f, front = i < 2 ? 1f : -1f;
                legs[i] = MudArt.Part(visual, "Leg " + i, new Vector3(0.7f * side, 0.35f, 0.55f * front), Quaternion.identity, "turtle_leg", k =>
                {
                    k.Rod(Vector3.zero, new Vector3(0f, -0.35f, 0f), 0.15f, 6, Skin, 0f, 0.13f);
                    k.Ball(new Vector3(0f, -0.35f, 0.06f), new Vector3(0.16f, 0.07f, 0.2f), Quaternion.identity, Skin, 0);
                });
            }
        }

        protected override void OnCritterStarted()
        {
            if (AgentReady) Agent.updateRotation = false;
            Enter(State.Guard);
        }

        private void Enter(State next)
        {
            state = next;
            stateTime = 0f;
            eyeGlint.gameObject.SetActive(next == State.Windup);
        }

        public bool Flipped => state == State.Flipped;

        // ---------- Brain ----------

        protected override void Think(float dt)
        {
            stateTime += dt;
            switch (state)
            {
                case State.Guard:
                case State.Advance:
                    if (!Notices() || GaveUp())
                    {
                        Halt();
                        if (state == State.Advance) Enter(State.Guard);
                        if (FlatDistance(Home) > 1.5f) MoveTo(Home, Defaults.Speed);
                        break;
                    }
                    TurnToward(PlayerPosition, 60f * Tempo * dt); // slow: flanking works
                    float distance = PlayerDistance;
                    if (Time.time >= nextWhistle && distance < 16f)
                    {
                        nextWhistle = Time.time + Random.Range(4f, 6f);
                        Sfx.PlayAt("turtle_whistle", transform.position, 0.7f, Random.Range(0.95f, 1.05f), 40f);
                        Say("FWEEET!", new Color(1f, 1f, 1f), 1f, 3f);
                    }
                    if (distance < 3.2f && AngleToPlayer() < 40f && Time.time >= nextSnap) { Enter(State.Windup); Halt(); break; }
                    if (distance > 3.5f) { if (state != State.Advance) Enter(State.Advance); MoveTo(PlayerPosition, Defaults.Speed); }
                    else Halt();
                    break;
                case State.Windup:
                    TurnToward(PlayerPosition, 40f * dt);
                    if (stateTime >= 0.9f) { Enter(State.Snap); Sfx.PlayAt("crab_snip", transform.position, 1f, 0.6f, 30f); }
                    break;
                case State.Snap:
                    if (stateTime >= 0.08f && stateTime - dt < 0.08f) Strike(3.3f, 60f, 25f, 2f);
                    if (stateTime > 0.6f) { Enter(State.Guard); nextSnap = Time.time + 1.6f; }
                    break;
                case State.Hide:
                    Halt();
                    if (stateTime > 1.6f) Enter(State.Guard);
                    break;
                case State.Flipped:
                    Halt();
                    if (Mathf.Repeat(stateTime, 0.5f) < dt) Say(Random.value < 0.5f ? "HELP" : "#@!", new Color(1f, 0.7f, 0.5f), 0.6f, 2.5f);
                    if (stateTime > 3f) { Enter(State.Righting); Sfx.PlayAt("crab_block", transform.position, 0.8f, 0.6f, 30f); }
                    break;
                case State.Righting:
                    if (stateTime > 0.6f) Enter(State.Guard);
                    break;
            }
            if (state == State.Guard || state == State.Advance) WatchPlayer(40f, 3f);
        }

        private float AngleToPlayer()
        {
            Vector3 to = (PlayerPosition - transform.position).Flat();
            return Vector3.Angle(transform.forward, to);
        }

        private void Flip()
        {
            if (state == State.Flipped) return;
            Enter(State.Flipped);
            Squash(0.4f);
            CameraShake.Add(0.15f);
            ParticleBurst.Spawn(ParticleBurst.Kind.Sand, transform.position, Vector3.up, 25, 0.6f, 4f, 70f, new Color(0.4f, 0.32f, 0.22f, 0.8f));
            Sfx.PlayAt("car_crash", transform.position, 0.6f, 0.7f, 30f);
            Say("WHOA!", new Color(1f, 0.8f, 0.4f), 1f, 3.5f);
        }

        // ---------- Shell ----------

        public bool TryReflect(Vector3 hitPoint, Vector3 incoming, Transform shooter, out Vector3 newDirection, out float damageMultiplier)
        {
            damageMultiplier = 0.3f;
            newDirection = -incoming;
            if (IsDead || state == State.Flipped || state == State.Righting) return false;
            // Only the front of the shell (the barricade) turns bolts.
            if (Vector3.Dot(transform.forward, -incoming.normalized) < 0.35f) return false;
            Vector3 normal = (hitPoint - (transform.position + Vector3.up * 0.7f)).normalized;
            newDirection = Vector3.Reflect(incoming.normalized, normal) + Vector3.up * 0.1f;
            ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, hitPoint, normal, 10, 0.06f, 4f, 50f);
            Sfx.PlayAt("crab_block", hitPoint, 0.6f, 1.2f, 25f);
            return true;
        }

        protected override void OnHit(float amount, GameObject source, Vector3 away)
        {
            if (state == State.Flipped && !bonusHit)
            {
                bonusHit = true;
                Health.TakeDamage(amount, source); // belly up: double
                bonusHit = false;
                return;
            }
            bool heavyBlast = !IsMeleeHit(source) && amount >= 45f;
            if (heavyBlast) Flip();
            else if (state == State.Guard) Enter(State.Advance);
        }

        private bool IsMeleeHit(GameObject source) => source != null && TargetLaser != null && source == TargetLaser.gameObject && TargetLaser.IsSwinging;

        public override void OnCarApproaching(DrunkCar car, float secondsAway)
        {
            if (secondsAway > 1.5f || state == State.Flipped || state == State.Hide) return;
            Vector3 toCar = (car.transform.position - transform.position).Flat();
            if (Vector3.Dot(transform.forward, toCar.normalized) > 0.3f)
            {
                Enter(State.Hide);
                Say("HALT!", new Color(1f, 0.3f, 0.25f), 1f, 3.5f);
            }
        }

        public override VehicleResponse OnVehicleHit(VehicleHit hit)
        {
            if (hit.Speed < 6f) return VehicleResponse.Bounce;
            if (hit.HeadOn && state != State.Flipped) return VehicleResponse.Explode; // straight into the barricade
            Health.TakeDamage(30f, hit.Car != null && hit.Car.Driver != null ? hit.Car.Driver.gameObject : null);
            if (!IsDead) Flip();
            return VehicleResponse.Bounce;
        }

        // ---------- Animation ----------

        protected override void Animate(float dt)
        {
            if (IsStunned || Airborne) return;
            float t = Time.time;
            float walk = AgentReady ? Agent.velocity.magnitude : 0f;
            for (int i = 0; i < 4; i++)
            {
                float swing = state == State.Flipped ? Mathf.Sin(t * 18f + i * 1.3f) * 50f : Mathf.Sin(t * 5f + (i == 0 || i == 3 ? 0f : Mathf.PI)) * 25f * Mathf.Clamp01(walk);
                legs[i].localRotation = Quaternion.Euler(swing, 0f, 0f);
                legs[i].gameObject.SetActive(state != State.Hide);
            }
            float neckOut = state switch
            {
                State.Windup => Mathf.Lerp(0f, -0.35f, stateTime / 0.9f),
                State.Snap => Mathf.Lerp(0.6f, 0f, stateTime / 0.6f),
                State.Hide => -0.8f,
                _ => 0f
            };
            neck.localPosition = new Vector3(0f, 0.6f, 0.9f + neckOut);
            neck.localScale = Vector3.one * (state == State.Hide ? 0.2f : 1f);
            sign.localRotation = state == State.Guard || state == State.Advance ? Quaternion.Euler(0f, 0f, Mathf.Sin(t * 4f) * 25f) : Quaternion.Euler(0f, 0f, 70f);
            float flip = state == State.Flipped ? Mathf.Min(1f, stateTime / 0.25f) : state == State.Righting ? 1f - stateTime / 0.6f : 0f;
            Visual.localRotation = VisualRestRotation * Quaternion.Euler(0f, 0f, 180f * flip);
            Visual.localPosition = VisualRest + Vector3.up * (1.2f * flip + Mathf.Sin(flip * Mathf.PI) * 0.4f);
        }

        protected override void DeathPose(float t)
        {
            // Spins on its back like a top, slowing down, then the signs fall off.
            float spin = 720f * (1f - Mathf.Exp(-t * 1.5f));
            Visual.localRotation = VisualRestRotation * Quaternion.Euler(0f, spin, 180f);
            Visual.localPosition = VisualRest + Vector3.up * (1.2f - 0.9f * Mathf.Clamp01((t - 2f) / 1f));
            if (sign != null) sign.localRotation = Quaternion.Euler(0f, 0f, 90f * Mathf.Clamp01(t));
        }
    }
}
