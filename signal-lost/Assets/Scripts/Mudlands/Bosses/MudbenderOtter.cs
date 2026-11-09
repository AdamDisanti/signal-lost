using System;
using System.Collections;
using System.Collections.Generic;
using SignalLost.Audio;
using SignalLost.Combat;
using SignalLost.Effects;
using SignalLost.Player;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace SignalLost.Mudlands
{
    /// <summary>
    /// Quest line 2's mini-boss, Silt the Mudbender (Christian's idea, docs/mudlands.md): a blindfolded otter martial
    /// artist in loose green robes who bends mud and stone. He fights by feel: he aims at where he last <i>heard</i> you
    /// (sprinting, landing, swinging and the car are loud; walking is quieter; crouching is almost silent, so his
    /// throws land wide).
    /// <list type="bullet">
    /// <item><b>Mud balls:</b> stamps and pulls a boulder of mud out of the ground (0.7 s), throws it (14, a mud patch).
    /// Phase 2: three in a fan. Bolts pop them in the air.</item>
    /// <item><b>Earth wall:</b> raises a stone wall between you (blocks bolts, lasts 6 s, cover for you too). Against the
    /// car he raises it in its path at once, so it crashes, unless he is mid-throw. A full-charge bolt or a heavy jump
    /// smash breaks a wall.</item>
    /// <item><b>Earth rush:</b> punches the ground and a line of spikes runs at you over cracking ground (20). Jump or
    /// sidestep; he is winded for 1.2 s after.</item>
    /// <item><b>Phase 2:</b> a pillar leap with an elbow drop (24 in a ring), and a listening stance that parries melee
    /// from the front with a palm strike (12, a big shove). Hit him from the side or behind.</item>
    /// </list>
    /// 480 HP. Drops the Gyro Stabilizer and a Mudstone Bead (Shockwave Shocks).
    /// </summary>
    public sealed class MudbenderOtter : MudBoss
    {
        private enum State { Idle, Reposition, MudBall, Wall, Rush, Winded, Pillar, Leap, Stance, Palm, Dazed, Opener, Stomp }

        private static readonly Color Fur = new Color(0.42f, 0.28f, 0.18f), Cream = new Color(0.9f, 0.82f, 0.66f),
            Robe = new Color(0.27f, 0.52f, 0.28f), RobeDark = new Color(0.16f, 0.34f, 0.2f), Blindfold = new Color(0.96f, 0.96f, 0.94f);

        private State state = State.Idle;
        private float stateTime, nextBall, nextWall, nextRush, nextPillar, nextStance, heardAt, lastLoud, nextHop, nextStomp;
        private Vector3 heard, leapFrom, leapTo;
        private Transform torso, armL, armR, legL, legR, tail, ties, held, pillar;
        private readonly List<EarthWall> walls = new List<EarthWall>();
        private GroundRing ring;
        private FirstPersonController playerMovement;

        public override string Subtitle => "Master of the Muddy Way";

        // ---------- Model ----------

        protected override void BuildModel(Transform visual)
        {
            torso = MudArt.Pivot(visual, "Torso", new Vector3(0f, 0.9f, 0f));
            MudArt.Part(torso, "Body", Vector3.zero, Quaternion.identity, "otter_body", k =>
            {
                k.Smooth = true;
                k.Ball(new Vector3(0f, 0.28f, 0f), new Vector3(0.38f, 0.55f, 0.32f), Quaternion.identity, Robe, 1);
                k.Ball(new Vector3(0f, 0.52f, 0.02f), new Vector3(0.36f, 0.24f, 0.3f), Quaternion.identity, Robe, 1); // shoulders
                k.Prism(new Vector3(0f, -0.1f, 0f), Quaternion.identity, 0.4f, 0.4f, 0.2f, 10, RobeDark); // sash
                k.Prism(new Vector3(0f, -0.55f, 0f), Quaternion.identity, 0.55f, 0.38f, 0.45f, 10, Robe); // flowing trousers' top
                k.Ball(new Vector3(0f, 0.72f, 0.02f), new Vector3(0.21f, 0.1f, 0.2f), Quaternion.identity, Cream, 1); // collar
                k.Ball(new Vector3(0f, 0.3f, 0.2f), new Vector3(0.24f, 0.36f, 0.14f), Quaternion.identity, new Color(0.62f, 0.48f, 0.35f), 1); // pale belly fur
                k.Smooth = false;
                k.Box(new Vector3(0f, 0.1f, 0.28f), new Vector3(0.42f, 0.75f, 0.06f), Quaternion.Euler(-4f, 0f, 0f), Cream); // the apron
                k.Box(new Vector3(0.15f, -0.25f, 0.33f), new Vector3(0.08f, 0.35f, 0.03f), Quaternion.Euler(0f, 0f, -10f), RobeDark); // the sash's tail
            });
            Head = MudArt.Pivot(torso, "Head", new Vector3(0f, 0.95f, 0.05f));
            MudArt.Part(Head, "Face", Vector3.zero, Quaternion.identity, "otter_head", k =>
            {
                k.Smooth = true;
                k.Ball(Vector3.zero, new Vector3(0.27f, 0.25f, 0.26f), Quaternion.identity, Fur, 1);
                k.Ball(new Vector3(0f, -0.08f, 0.17f), new Vector3(0.19f, 0.14f, 0.16f), Quaternion.identity, Cream, 1); // muzzle
                foreach (float x in new[] { -0.08f, 0.08f }) // whisker pads
                    k.Ball(new Vector3(x, -0.08f, 0.26f), new Vector3(0.08f, 0.065f, 0.06f), Quaternion.identity, Cream, 1);
                k.Ball(new Vector3(0f, -0.01f, 0.31f), new Vector3(0.065f, 0.048f, 0.045f), Quaternion.identity, MudPalette.Black, 1); // nose
                foreach (float x in new[] { -0.2f, 0.2f })
                {
                    k.Ball(new Vector3(x, 0.18f, -0.02f), new Vector3(0.075f, 0.065f, 0.045f), Quaternion.identity, Fur, 1); // ears
                    k.Smooth = false;
                    for (int i = -1; i <= 1; i++) k.Plank(new Vector3(x * 0.45f, -0.07f, 0.29f), new Vector3(x * 1.4f, -0.05f + i * 0.045f, 0.31f), 0.006f, 0.006f, MudPalette.White);
                    k.Smooth = true;
                }
                // The blindfold: a white band all the way round.
                k.Prism(new Vector3(0f, 0.02f, 0f), Quaternion.identity, 0.278f, 0.276f, 0.09f, 16, Blindfold);
            });
            ties = MudArt.Part(Head, "Blindfold Ties", new Vector3(0f, 0.06f, -0.27f), Quaternion.identity, "otter_ties", k =>
            {
                k.Plank(Vector3.zero, new Vector3(-0.08f, -0.08f, -0.3f), 0.06f, 0.01f, Blindfold);
                k.Plank(Vector3.zero, new Vector3(0.07f, -0.15f, -0.25f), 0.06f, 0.01f, Blindfold);
            });
            // No eyes to read (the blindfold), so the brows and mouth do the talking.
            Transform browL = BossFace.Brow(Head, "otter_brow", new Vector3(-0.09f, 0.155f, 0.235f), -1f, new Vector3(0.11f, 0.03f, 0.04f), new Color(0.3f, 0.2f, 0.13f), 4f);
            Transform browR = BossFace.Brow(Head, "otter_brow", new Vector3(0.09f, 0.155f, 0.235f), 1f, new Vector3(0.11f, 0.03f, 0.04f), new Color(0.3f, 0.2f, 0.13f), 4f);
            Transform mouth = BossFace.Mouth(Head, "otter_mouth", new Vector3(0f, -0.14f, 0.3f), 0.1f, 0.05f, new Color(0.55f, 0.42f, 0.32f), true, Quaternion.Euler(-15f, 0f, 0f));
            FaceParts = new BossFace.Rig
            {
                BrowL = browL, BrowR = browR, Mouth = mouth, BrowTravel = 0.018f, Idle = Mood.Focus, Fight = Mood.Focus,
            };
            armL = Arm(torso, -1f);
            armR = Arm(torso, 1f);
            legL = Leg(visual, -1f);
            legR = Leg(visual, 1f);
            tail = MudArt.Part(visual, "Tail", new Vector3(0f, 0.35f, -0.3f), Quaternion.identity, "otter_tail",
                k =>
                {
                    k.Smooth = true;
                    k.Prism(Vector3.zero, Quaternion.Euler(-110f, 0f, 0f), 0.14f, 0.04f, 0.9f, 8, Fur);
                    k.Ball(Vector3.zero, 0.14f, Fur, 1);
                });
            held = MudArt.Pivot(torso, "Held", new Vector3(0f, 1.25f, 0.45f));
            // The ship's gyro stabilizer, hung from his sash like a bell.
            ShipPart = MudArt.Part(torso, "Gyro Stabilizer", new Vector3(-0.38f, -0.18f, 0.05f), Quaternion.identity, "otter_gyro", k =>
            {
                k.Ball(Vector3.zero, 0.16f, MudPalette.Metal, 1);
                k.Prism(new Vector3(0f, -0.02f, 0f), Quaternion.Euler(90f, 0f, 0f), 0.2f, 0.2f, 0.04f, 12, new Color(0.25f, 0.6f, 0.9f));
                k.Prism(new Vector3(0f, -0.02f, 0f), Quaternion.Euler(0f, 0f, 90f), 0.2f, 0.2f, 0.04f, 12, new Color(0.25f, 0.6f, 0.9f));
                k.Ball(Vector3.zero, 0.05f, new Color(0.3f, 0.9f, 1f), 0, 3f);
            });
        }

        private static Transform Arm(Transform torso, float side)
        {
            Transform shoulder = MudArt.Pivot(torso, side < 0 ? "Arm L" : "Arm R", new Vector3(0.36f * side, 0.6f, 0f));
            MudArt.Part(shoulder, "Sleeve", Vector3.zero, Quaternion.identity, "otter_arm", k =>
            {
                k.Smooth = true;
                k.Ball(Vector3.zero, 0.13f, Robe, 1);
                k.Prism(new Vector3(0f, 0f, 0f), Quaternion.Euler(180f, 0f, 0f), 0.13f, 0.25f, 0.5f, 8, Robe); // a wide sleeve
                k.Rod(new Vector3(0f, -0.45f, 0f), new Vector3(0f, -0.65f, 0.02f), 0.06f, 6, Fur);
                k.Ball(new Vector3(0f, -0.7f, 0.03f), 0.08f, Fur, 0);
            });
            return shoulder;
        }

        private static Transform Leg(Transform visual, float side)
        {
            Transform hip = MudArt.Pivot(visual, side < 0 ? "Leg L" : "Leg R", new Vector3(0.22f * side, 0.55f, 0f));
            MudArt.Part(hip, "Leg", Vector3.zero, Quaternion.identity, "otter_leg", k =>
            {
                k.Smooth = true;
                k.Prism(Vector3.zero, Quaternion.Euler(180f, 0f, 0f), 0.2f, 0.17f, 0.45f, 8, Robe);
                k.Rod(new Vector3(0f, -0.45f, 0f), new Vector3(0f, -0.52f, 0.05f), 0.06f, 6, Fur);
                k.Ball(new Vector3(0f, -0.53f, 0.1f), new Vector3(0.09f, 0.04f, 0.14f), Quaternion.identity, Fur, 0); // bare feet
            });
            return hip;
        }

        protected override Action<MeshKit> KeyItemModel() => k =>
        {
            k.Ball(Vector3.zero, 0.18f, new Color(0.45f, 0.36f, 0.28f), 1);
            k.Prism(new Vector3(0f, -0.2f, 0f), Quaternion.identity, 0.03f, 0.03f, 0.4f, 5, new Color(0.3f, 0.6f, 0.35f), 1f);
        };

        // ---------- Entrance ----------

        // The tea rock's top is 0.9 m above its base; sitting cross-legged, his seat is 0.32 m above his feet.
        private const float RockTop = 0.9f, SeatHeight = RockTop - 0.32f, RockRise = 1.2f, RockBack = 3f;
        // He somersaults about his middle (not his feet), so his head swings round him instead of through the rock.
        private const float SpinPivot = 1f;
        // The boulder he lifts at the end of his entrance and throws to open the fight.
        private Transform boulder;
        private const float OpenerWindup = 0.8f;
        private Vector3 HoverPoint => transform.position + Vector3.up * 3.5f;

        public override IEnumerator Entrance(BossEntrance cine)
        {
            Vector3 home = transform.parent != null ? transform.parent.position : transform.position;
            Vector3 toPlayer = (cine.PlayerPosition - home).Flatten().normalized;
            if (toPlayer.sqrMagnitude < 0.01f) toPlayer = Vector3.back;
            Vector3 side = Vector3.Cross(Vector3.up, toPlayer);
            Color voice = new Color(0.8f, 1f, 0.8f);
            // The rock sits behind his fighting spot, so his leap off it lands him right where the fight starts.
            Vector3 rockAt = Ground(home - toPlayer * RockBack);
            transform.SetPositionAndRotation(rockAt + Vector3.up * SeatHeight, Quaternion.LookRotation(toPlayer));
            // Cross-legged on a rock with a cup of tea. The rock is the boulder he'll throw, on a stone column that
            // lifts it (hidden in the ground for now).
            if (boulder != null) Destroy(boulder.gameObject);
            boulder = MudArt.Part(null, "Tea Rock", rockAt + Vector3.up * 0.4f, Quaternion.identity, "otter_boulder", BoulderMesh);
            Transform column = MudArt.Part(null, "Rock Column", rockAt, Quaternion.identity, "otter_column",
                k => k.Prism(new Vector3(0f, -1.35f, 0f), Quaternion.identity, 0.75f, 0.55f, 1.45f, 7, MudPalette.Stone));
            Transform cup = MudArt.Part(armR, "Tea", new Vector3(0f, -0.7f, 0.1f), Quaternion.identity, "otter_cup", k =>
            {
                k.Prism(Vector3.zero, Quaternion.identity, 0.06f, 0.08f, 0.1f, 8, MudPalette.White);
                k.Prism(new Vector3(0f, 0.085f, 0f), Quaternion.identity, 0.07f, 0.07f, 0.01f, 8, new Color(0.5f, 0.35f, 0.15f));
            });
            Pose(legs: -80f, armRLift: -110f, armLLift: -30f, lean: 0f);
            cine.Cut(rockAt + toPlayer * 9f + Vector3.up * 2.2f, rockAt + Vector3.up * 1.4f);
            yield return cine.Wait(1.2f);
            // A slow sip, a close-up on the blindfold.
            yield return cine.Move(rockAt + toPlayer * 2.2f + Vector3.up * 2.1f, Head.position, 1.4f);
            Say("...", Blindfold, 1.4f, 4f, 2.3f);
            yield return cine.Wait(1f);
            Say("You walk like a startled yak.", voice, 2.2f, 2.6f, 2.4f);
            yield return cine.Wait(1.6f);

            // He sets the cup down, stamps, and the column pushes the rock (and him) up in a shower of mud.
            Destroy(cup.gameObject);
            cine.Cut(home + toPlayer * 7f + side * 3f + Vector3.up * 3f, rockAt + Vector3.up * 2f);
            Sfx.Play("stone_rise", 1f, 0.8f);
            CameraShake.Add(0.5f);
            for (float t = 0f; t < 1.2f && !cine.Tick(); t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / 1.2f);
                boulder.position = rockAt + Vector3.up * (0.4f + k * RockRise);
                column.position = rockAt + Vector3.up * k * RockRise;
                transform.position = rockAt + Vector3.up * (SeatHeight + k * RockRise);
                if (Random.value < 0.4f) Dust(rockAt + Random.insideUnitSphere.Flatten() * 1.5f, 6, 0.6f);
                yield return null;
            }

            // He uncrosses his legs and stands up on top of the rock.
            Vector3 seat = rockAt + Vector3.up * (SeatHeight + RockRise), top = rockAt + Vector3.up * (RockTop + RockRise);
            for (float t = 0f; t < 0.4f && !cine.Tick(); t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / 0.4f);
                transform.position = Vector3.Lerp(seat, top, k);
                Pose(Mathf.Lerp(-80f, -30f, k), Mathf.Lerp(-110f, -60f, k), Mathf.Lerp(-30f, -60f, k), 10f * k);
                yield return null;
            }

            // A tucked front somersault off the rock, landing well clear of it on his fighting spot.
            Vector3 land = Ground(home);
            Sfx.PlayAt("chain_swish", top, 0.8f, 0.7f, 40f);
            Pose(legs: -95f, armRLift: -50f, armLLift: -50f, lean: 35f);
            for (float t = 0f; t < 0.9f && !cine.Tick(); t += Time.deltaTime)
            {
                float k = t / 0.9f;
                transform.position = Vector3.Lerp(top, land, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 1.6f;
                SpinAboutMiddle(360f * Mathf.SmoothStep(0f, 1f, k));
                if (k > 0.75f) Pose(Mathf.Lerp(-95f, -35f, (k - 0.75f) * 4f), -40f, -40f, Mathf.Lerp(35f, 15f, (k - 0.75f) * 4f));
                yield return null;
            }
            SpinAboutMiddle(0f);
            transform.position = land;
            Dust(land, 40, 1f);
            CameraShake.Add(0.3f);
            Sfx.PlayAt("laser_slam", land, 1f, 0.6f, 50f);
            Pose(legs: -40f, armRLift: -20f, armLLift: -20f, lean: 15f);
            yield return cine.Wait(0.4f);

            // Earthbending: a low shot as he draws his arms down and back, and the rock behind him shudders...
            cine.Cut(home + toPlayer * 7f + side * 2.5f + Vector3.up * 1.1f, home - toPlayer * 1.2f + Vector3.up * 2.4f);
            Vector3 rockRest = boulder.position;
            Sfx.PlayAt("stone_rise", rockRest, 0.6f, 0.6f, 40f);
            for (float t = 0f; t < 0.5f && !cine.Tick(); t += Time.deltaTime)
            {
                float k = t / 0.5f;
                Pose(-40f, Mathf.Lerp(-20f, 15f, k), Mathf.Lerp(-20f, 15f, k), Mathf.Lerp(15f, 22f, k));
                boulder.position = rockRest + Random.insideUnitSphere * 0.06f * k;
                yield return null;
            }

            // ...then he sweeps them up: the column crumbles back into the ground and the boulder floats up over his head.
            CameraShake.Add(0.4f);
            Dust(rockAt, 30, 1f);
            Sfx.PlayAt("stone_rise", rockAt, 1f, 0.9f, 50f);
            Vector3 columnTop = column.position;
            for (float t = 0f; t < 1.1f && !cine.Tick(); t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / 1.1f);
                Pose(-40f, Mathf.Lerp(15f, -170f, k), Mathf.Lerp(15f, -170f, k), Mathf.Lerp(22f, -8f, k));
                column.position = columnTop + Vector3.down * 1.6f * Mathf.Clamp01(t / 0.6f);
                boulder.position = Vector3.Lerp(rockRest, HoverPoint, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 1.2f;
                boulder.Rotate(new Vector3(20f, 45f, 0f) * Time.deltaTime, Space.Self);
                if (Random.value < 0.3f) Dust(rockAt + Random.insideUnitSphere.Flatten() * 1.2f, 5, 0.5f);
                yield return null;
            }
            Say("Catch!", voice, 1.4f, 6f, 5.2f);
            for (float t = 0f; t < 1f && !cine.Tick(); t += Time.deltaTime)
            {
                boulder.position = HoverPoint + Vector3.up * Mathf.Sin(t * 2.2f) * 0.12f;
                boulder.Rotate(new Vector3(9f, 23f, 5f) * Time.deltaTime, Space.Self);
                yield return null;
            }

            // Tidy up (this also runs straight through when the entrance is skipped). The boulder stays aloft for the
            // fight's opening throw.
            if (cup != null) Destroy(cup.gameObject);
            if (column != null) Destroy(column.gameObject);
            SpinAboutMiddle(0f);
            Pose(legs: -40f, armRLift: -170f, armLLift: -170f, lean: -8f);
        }

        // Rolls the body forward by an angle about his middle (SpinPivot up from his feet) instead of about his feet.
        private void SpinAboutMiddle(float degrees)
        {
            Quaternion spin = Quaternion.Euler(degrees, 0f, 0f);
            Visual.localRotation = VisualRestRotation * spin;
            Visual.localPosition = VisualRest + Vector3.up * SpinPivot - spin * (Vector3.up * SpinPivot);
        }

        private static void BoulderMesh(MeshKit k)
        {
            k.Ball(Vector3.zero, new Vector3(0.9f, 0.5f, 0.8f), Quaternion.identity, MudPalette.Stone, 1);
            // A couple of low lumps round the sides (kept under the flat top he sits on).
            k.Ball(new Vector3(0.5f, -0.12f, -0.35f), new Vector3(0.42f, 0.33f, 0.38f), Quaternion.Euler(0f, 30f, 10f), MudPalette.Stone, 0);
            k.Ball(new Vector3(-0.45f, -0.18f, 0.3f), new Vector3(0.38f, 0.28f, 0.35f), Quaternion.Euler(0f, -20f, -8f), MudPalette.Stone, 0);
            k.Ball(new Vector3(0f, -0.38f, 0f), new Vector3(0.8f, 0.16f, 0.7f), Quaternion.identity, MudPalette.Mud, 0); // mud caked underneath
        }

        private void Pose(float legs, float armRLift, float armLLift, float lean)
        {
            legL.localRotation = Quaternion.Euler(legs, 0f, -18f);
            legR.localRotation = Quaternion.Euler(legs, 0f, 18f);
            armR.localRotation = Quaternion.Euler(armRLift, 0f, 15f);
            armL.localRotation = Quaternion.Euler(armLLift, 0f, -25f);
            torso.localRotation = Quaternion.Euler(lean, 0f, 0f);
        }

        // ---------- Base hooks ----------

        protected override void OnBossStarted()
        {
            if (AgentReady) Agent.updateRotation = false;
            playerMovement = Target != null ? Target.GetComponentInParent<FirstPersonController>() : null;
            heard = PlayerPosition;
            // He opens the fight by throwing the boulder from his entrance (snapped overhead in case it was skipped).
            if (boulder != null) boulder.position = HoverPoint;
            Enter(boulder != null ? State.Opener : State.Idle);
            nextBall = Time.time + 1f;
            nextWall = Time.time + 4f;
            nextRush = Time.time + 6f;
            nextPillar = Time.time + 8f;
            nextStance = Time.time + 6f;
        }

        private void Enter(State next)
        {
            if (ring != null && next != State.Leap) { ring.Finish(); ring = null; }
            if (held.childCount > 0 && next != State.MudBall) Destroy(held.GetChild(0).gameObject);
            state = next;
            stateTime = 0f;
        }

        // ---------- Hearing ----------

        /// <summary>How much noise the player is making (0 quiet to 1 loud), and how often he gets a fix on it.</summary>
        private void Listen()
        {
            if (Target == null) return;
            float noise = 0.4f;
            Vector3 velocity = TargetBody != null ? TargetBody.velocity : Vector3.zero;
            float speed = velocity.Flatten().magnitude;
            if (DrunkDriving.Instance != null && DrunkDriving.Instance.InCar) noise = 1f;
            else if (TargetLaser != null && (TargetLaser.IsSwinging || TargetLaser.IsCharging)) noise = 0.9f;
            else if (playerMovement != null && playerMovement.IsCrouched && !playerMovement.IsSliding) noise = speed > 0.3f ? 0.12f : 0.05f;
            else if (speed > 5.5f || (TargetBody != null && !TargetBody.isGrounded)) noise = 0.85f;
            else if (speed < 0.3f) noise = 0.2f;
            if (noise >= 0.8f) lastLoud = Time.time;
            float interval = noise >= 0.8f ? 0f : noise >= 0.4f ? 0.6f : noise >= 0.15f ? 1.5f : 2.5f;
            if (Time.time - heardAt < interval) return;
            heardAt = Time.time;
            float error = noise >= 0.8f ? 0f : noise >= 0.4f ? 1.5f : 4.5f;
            Vector2 miss = Random.insideUnitCircle * error;
            heard = PlayerPosition + new Vector3(miss.x, 0f, miss.y);
        }

        private bool Quiet => Time.time - lastLoud > 1.5f && playerMovement != null && playerMovement.IsCrouched;

        // ---------- Combat ----------

        protected override void TickCombat(float dt)
        {
            stateTime += dt;
            UpdateShove(dt);
            Listen();
            walls.RemoveAll(w => w == null);
            switch (state)
            {
                case State.Idle:
                case State.Reposition: Decide(dt); break;
                case State.MudBall: UpdateMudBall(dt); break;
                case State.Wall:
                    Halt();
                    if (stateTime > 0.6f) Enter(State.Idle);
                    break;
                case State.Rush: UpdateRush(dt); break;
                case State.Winded:
                case State.Dazed:
                    Halt();
                    if (stateTime > (state == State.Dazed ? 2.5f : 1.2f)) Enter(State.Idle);
                    break;
                case State.Pillar: UpdatePillar(dt); break;
                case State.Leap: UpdateLeap(dt); break;
                case State.Stance:
                    Halt();
                    TurnToward(heard, 220f * dt);
                    if (stateTime > 2.5f) { Enter(State.Idle); nextStance = Time.time + Random.Range(6f, 9f); }
                    break;
                case State.Palm:
                    if (stateTime > 0.6f) Enter(State.Idle);
                    break;
                case State.Opener: UpdateOpener(dt); break;
                case State.Stomp: UpdateStomp(dt); break;
            }
        }

        private void Decide(float dt)
        {
            TurnToward(heard, 240f * dt);
            if (boulder != null) { Enter(State.Opener); return; } // the opening throw got interrupted (a car wall)
            float distance = PlayerDistance;
            if (Phase == 2 && Time.time >= nextStance && distance < 6f) { Enter(State.Stance); Say("...", Blindfold, 1.2f, 4f); return; }
            if (Phase == 2 && Time.time >= nextPillar && distance > 5f) { Enter(State.Pillar); return; }
            if (distance < 4f && state != State.Reposition)
            {
                // Too close. He hops back once in a while (sideways along the dojo's edge when his back is to it);
                // otherwise he holds his ground and stamps you away.
                if (Time.time >= nextHop)
                {
                    Enter(State.Reposition);
                    hopTo = HopTarget();
                    nextHop = Time.time + Random.Range(3.5f, 5f);
                    Dust(transform.position, 14, 0.6f);
                    return;
                }
                if (Time.time >= nextStomp)
                {
                    Enter(State.Stomp);
                    ring = GroundRing.Spawn(transform.position, StompRadius, new Color(1f, 0.55f, 0.2f, 0.5f));
                    return;
                }
            }
            if (state == State.Reposition)
            {
                Chase(hopTo, 7f);
                if (stateTime > 0.6f) Enter(State.Idle);
                return;
            }
            if (Time.time >= nextWall && walls.Count == 0 && distance > 7f && Random.value < 0.4f) { RaiseWall(heard, false); return; }
            if (Time.time >= nextRush && distance > 6f && distance < 20f && Random.value < 0.5f) { Enter(State.Rush); spikesLaid = 0; Say("!", Blindfold, 0.6f, 5f); return; }
            if (Time.time >= nextBall) { Enter(State.MudBall); return; }
            // Drift sideways, keeping 8-14 m, but never off his dojo.
            float want = Mathf.Clamp(distance, 8f, 14f);
            Vector3 side = Vector3.Cross(Vector3.up, (PlayerPosition - transform.position).Flatten().normalized) * Mathf.Sign(Mathf.Sin(Time.time * 0.4f + BossSeed));
            Vector3 goal = PlayerPosition + (transform.position - PlayerPosition).Flatten().normalized * want + side * 3f;
            Chase(OnDojo(goal), 3f);
        }

        // ---------- Staying on his dojo ----------

        // He fights on his dojo's flat (the stone lanterns stand 13 m out); every place he walks to is kept inside this.
        private const float DojoRadius = 11f, StompRadius = 3.4f;
        private Vector3 hopTo;

        /// <summary>The point, pulled back inside the dojo if it is outside.</summary>
        private Vector3 OnDojo(Vector3 point)
        {
            Vector3 offset = (point - Home).Flatten();
            if (offset.magnitude > DojoRadius) offset = offset.normalized * DojoRadius;
            return Home + offset;
        }

        // Straight away from the player if there is room; else sideways along the edge, whichever side has more room;
        // and never off the dojo.
        private Vector3 HopTarget()
        {
            Vector3 away = (transform.position - heard).Flatten().normalized;
            if (away.sqrMagnitude < 0.01f) away = -transform.forward;
            Vector3 straight = transform.position + away * 6f;
            if ((straight - Home).Flatten().magnitude <= DojoRadius) return straight;
            Vector3 side = Vector3.Cross(Vector3.up, away);
            Vector3 left = OnDojo(transform.position + (side + away * 0.3f).normalized * 6f);
            Vector3 right = OnDojo(transform.position + (-side + away * 0.3f).normalized * 6f);
            return (left - heard).Flatten().sqrMagnitude > (right - heard).Flatten().sqrMagnitude ? left : right;
        }

        // Crowded with nowhere to hop: he crouches, then stamps a shockwave that shoves you off him (a ring warns).
        private void UpdateStomp(float dt)
        {
            Halt();
            TurnToward(heard, 300f * dt);
            if (ring != null) ring.Progress = Mathf.Clamp01(stateTime / 0.55f);
            if (stateTime < 0.55f) return;
            CameraShake.Add(0.25f);
            Dust(transform.position, 35, 0.9f);
            Sfx.PlayAt("laser_slam", transform.position, 0.9f, 0.7f, 40f);
            HurtInCircle(transform.position, StompRadius, 10f, 7f);
            nextStomp = Time.time + Random.Range(4f, 6f);
            Enter(State.Palm); // the follow-through pose, then back to idle
            Say("HUP!", new Color(0.8f, 1f, 0.8f), 0.8f, 6f);
        }

        private void UpdateMudBall(float dt)
        {
            Halt();
            TurnToward(heard, 300f * dt);
            if (held.childCount == 0)
            {
                MudArt.Part(held, "Mud Ball", Vector3.zero, Quaternion.identity, "otter_mudball", MudBallMesh);
                Sfx.PlayAt("stone_rise", transform.position, 0.6f, 1.4f, 30f);
                Dust(transform.position + transform.forward, 12, 0.5f);
            }
            float windup = Phase == 2 ? 0.55f : 0.7f;
            if (held.childCount > 0) held.GetChild(0).localScale = Vector3.one * Mathf.Clamp01(stateTime / windup);
            if (stateTime < windup) return;
            int count = Phase == 2 ? 3 : 1;
            Vector3 aim = heard;
            Vector3 side = Vector3.Cross(Vector3.up, (aim - transform.position).Flatten().normalized);
            for (int i = 0; i < count; i++)
            {
                Vector3 target = aim + side * (count == 1 ? 0f : (i - 1) * 4f);
                Transform ball = MudArt.Part(null, "Mud Ball", Vector3.zero, Quaternion.identity, "otter_mudball", MudBallMesh);
                GameObject owner = gameObject;
                float distance = Vector3.Distance(held.position, target);
                Lob.Throw(ball, held.position, Ground(target), Mathf.Clamp(distance / 18f, 0.6f, 1.1f), 2f + distance * 0.1f, at =>
                {
                    Sfx.PlayAt("mud_splat", at, 1f, Random.Range(0.8f, 1f), 40f);
                    Dust(at, 20, 0.8f);
                    MudZone.Spawn(at, 2.4f, 6f);
                    if (TargetAlive && (PlayerPosition - at).Flatten().magnitude < 2f) HurtPlayer(14f);
                }, 2f, new Color(0.55f, 0.35f, 0.2f, 0.5f), 1f);
            }
            if (Quiet) Say("?", Blindfold, 0.8f, 5f);
            Sfx.PlayAt("chain_swish", transform.position, 0.8f, 0.6f, 35f);
            nextBall = Time.time + (Phase == 2 ? 2.2f : 3f);
            Enter(State.Idle);
        }

        // The opening move: he draws the boulder back over his head, then hurls it at the player (a ring marks where).
        private void UpdateOpener(float dt)
        {
            Halt();
            TurnToward(PlayerPosition, 360f * dt);
            if (stateTime < OpenerWindup)
            {
                if (boulder == null) Enter(State.Idle);
                return;
            }
            if (boulder != null) ThrowBoulder();
            if (stateTime > OpenerWindup + 0.5f) Enter(State.Idle); // the follow-through
        }

        private void ThrowBoulder()
        {
            Transform rock = boulder;
            boulder = null;
            Vector3 target = Ground(PlayerPosition);
            float distance = Vector3.Distance(rock.position, target);
            Lob.Throw(rock, rock.position, target, Mathf.Clamp(distance / 12f, 1.1f, 1.5f), 1.5f + distance * 0.08f, BoulderLanded,
                2.8f, new Color(1f, 0.45f, 0.2f, 0.55f));
            Say("HAAA!", new Color(0.8f, 1f, 0.8f), 1f, 6f);
            Sfx.PlayAt("chain_swish", transform.position, 1f, 0.45f, 40f);
            CameraShake.Add(0.15f);
            nextBall = Time.time + 3f;
            nextWall = Mathf.Max(nextWall, Time.time + 4f);
            nextRush = Mathf.Max(nextRush, Time.time + 6f);
        }

        private void BoulderLanded(Vector3 at)
        {
            if (this == null) return;
            CameraShake.Add(0.5f);
            Dust(at, 60, 1.3f);
            Sfx.PlayAt("laser_slam", at, 1f, 0.4f, 60f);
            Sfx.PlayAt("stone_rise", at, 0.9f, 1.3f, 40f);
            HurtInCircle(at, 2.8f, 22f, 6f);
            // It bursts into rubble.
            for (int i = 0; i < 6; i++)
            {
                Vector2 scatter = Random.insideUnitCircle.normalized * Random.Range(1.5f, 3.5f);
                Transform chunk = MudArt.Part(null, "Rubble", Vector3.zero, Quaternion.identity, "otter_rubble",
                    k => k.Ball(Vector3.zero, new Vector3(0.28f, 0.2f, 0.24f), Quaternion.identity, MudPalette.Stone, 0));
                chunk.localScale = Vector3.one * Random.Range(0.6f, 1.3f);
                Lob.Throw(chunk, at + Vector3.up * 0.4f, Ground(at + new Vector3(scatter.x, 0f, scatter.y)), Random.Range(0.45f, 0.7f),
                    Random.Range(0.6f, 1.4f), p => Dust(p, 6, 0.4f));
            }
        }

        private void DropBoulder()
        {
            if (boulder == null) return;
            Dust(boulder.position, 30, 1f);
            Destroy(boulder.gameObject);
            boulder = null;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (boulder != null) Destroy(boulder.gameObject);
        }

        private static void MudBallMesh(MeshKit k) => k.Ball(Vector3.zero, new Vector3(0.45f, 0.42f, 0.45f), Quaternion.identity, MudPalette.Mud, 1);

        // A stone wall between him and a point (or across the car's path).
        private void RaiseWall(Vector3 towards, bool againstCar, Vector3 carPath = default)
        {
            Vector3 dir = (towards - transform.position).Flatten();
            float distance = Mathf.Min(dir.magnitude * 0.5f, 6f);
            dir = dir.sqrMagnitude > 0.01f ? dir.normalized : transform.forward;
            Vector3 at = transform.position + dir * Mathf.Max(3f, distance);
            Quaternion facing = Quaternion.LookRotation(againstCar && carPath.sqrMagnitude > 0.01f ? -carPath.Flatten().normalized : -dir);
            walls.Add(EarthWall.Raise(Ground(at), facing, againstCar ? 0.15f : 0.5f));
            Sfx.PlayAt("stone_rise", at, 1f, 1f, 50f);
            nextWall = Time.time + 7f;
            Enter(State.Wall);
            Pose(0f, -170f, -170f, -10f);
        }

        private int spikesLaid;
        private float nextSpike;
        private Vector3 rushFrom, rushDirection;

        private void UpdateRush(float dt)
        {
            Halt();
            if (stateTime < 0.6f)
            {
                TurnToward(heard, 400f * dt);
                rushFrom = transform.position;
                rushDirection = (heard - transform.position).Flatten().normalized;
                return;
            }
            if (spikesLaid == 0) { CameraShake.Add(0.2f); Sfx.PlayAt("laser_slam", transform.position, 1f, 0.5f, 40f); }
            if (Time.time >= nextSpike && spikesLaid < 15)
            {
                nextSpike = Time.time + 0.11f;
                spikesLaid++;
                RockSpike.Spawn(Ground(rushFrom + rushDirection * (1.5f + spikesLaid * 1.25f)), gameObject, 20f);
            }
            if (spikesLaid >= 15 && stateTime > 2.4f)
            {
                Enter(State.Winded);
                Say("*huff*", Blindfold, 1.2f, 4f);
                nextRush = Time.time + 8f;
            }
        }

        private void UpdatePillar(float dt)
        {
            Halt();
            if (pillar == null)
            {
                pillar = MudArt.Part(null, "Stone Pillar", transform.position, Quaternion.identity, "otter_pillar",
                    k => k.Prism(new Vector3(0f, -4f, 0f), Quaternion.identity, 1f, 0.8f, 4f, 7, MudPalette.Stone));
                Sfx.PlayAt("stone_rise", transform.position, 1f, 0.7f, 50f);
                leapFrom = transform.position;
                if (AgentReady) Agent.enabled = false;
            }
            float rise = Mathf.SmoothStep(0f, 1f, stateTime / 0.6f);
            pillar.position = leapFrom + Vector3.up * 4f * rise;
            transform.position = leapFrom + Vector3.up * 4f * rise;
            TurnToward(heard, 300f * dt);
            if (stateTime < 1.4f) return;
            leapTo = Ground(OnDojo(heard));
            ring = GroundRing.Spawn(leapTo, 3.5f, new Color(1f, 0.4f, 0.2f, 0.55f));
            Enter(State.Leap);
            Say("HAAA!", new Color(0.8f, 1f, 0.8f), 1f, 6f);
        }

        private void UpdateLeap(float dt)
        {
            float k = Mathf.Clamp01(stateTime / 0.9f);
            Vector3 top = leapFrom + Vector3.up * 4f;
            transform.position = Vector3.Lerp(top, leapTo, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 3f;
            if (ring != null) ring.Progress = k;
            if (k < 1f) return;
            if (ring != null) { ring.Finish(); ring = null; }
            AgentUtil.Reenable(Agent, 4f);
            CameraShake.Add(0.45f);
            Dust(transform.position, 40, 1f);
            Sfx.PlayAt("laser_slam", transform.position, 1f, 0.45f, 50f);
            MudZone.Spawn(transform.position, 3f, 6f);
            HurtInCircle(transform.position, 3.5f, 24f, 5f);
            if (pillar != null) { Destroy(pillar.gameObject, 3f); pillar = null; }
            nextPillar = Time.time + Random.Range(9f, 12f);
            Enter(State.Winded);
        }

        // ---------- Reactions ----------

        protected override void OnBossHit(float amount, GameObject source, Vector3 away)
        {
            if (state == State.Stance)
            {
                Vector3 toPlayer = (PlayerPosition - transform.position).Flatten().normalized;
                bool fromFront = Vector3.Angle(transform.forward, toPlayer) < 70f;
                if (IsMeleeHit(source) && fromFront)
                {
                    Health.Heal(amount); // parried
                    Enter(State.Palm);
                    Say("HUP!", new Color(0.8f, 1f, 0.8f), 1f, 6f);
                    Sfx.PlayAt("laser_slam", transform.position, 0.7f, 1.4f, 35f);
                    if (HurtPlayer(12f)) ShovePlayer(toPlayer, 8f);
                    return;
                }
                Enter(State.Dazed);
                stateTime = 1.5f; // a short stagger
                Say("!?", Blindfold, 0.8f, 5f);
                return;
            }
            if (state == State.MudBall && held.childCount > 0 && IsHeavyRanged(amount, source))
            {
                // A heavy hit bursts the ball in his hands.
                Dust(held.position, 25, 0.8f);
                MudZone.Spawn(transform.position, 2.5f, 5f);
                Enter(State.Dazed);
                stateTime = 1f;
            }
        }

        public override void OnCarApproaching(DrunkCar car, float secondsAway)
        {
            if (!Engaged || IsDead || state == State.MudBall || state == State.Leap || state == State.Pillar || secondsAway > 1.4f) return;
            if (walls.Count > 0 && Time.time - lastCarWall < 2f) return;
            lastCarWall = Time.time;
            RaiseWall(car.transform.position, true, car.Velocity);
            Say("I HEAR YOU!", new Color(0.8f, 1f, 0.8f), 1.2f, 5f);
        }

        private float lastCarWall = -10f;

        public override float OnCarBlast(CarBlastInfo blast)
        {
            Enter(State.Dazed);
            Say("@_@", Blindfold, 2f, 6f);
            return blast.Damage;
        }

        protected override void OnPhaseTwo()
        {
            Say("Now I am listening.", new Color(0.8f, 1f, 0.8f), 2f, 4f);
            Sfx.PlayAt("gong", transform.position, 0.8f, 0.8f, 60f);
        }

        protected override void UpdatePhaseShift(float dt) => Halt();

        protected override void OnReset()
        {
            foreach (EarthWall wall in walls) if (wall != null) Destroy(wall.gameObject);
            walls.Clear();
            if (pillar != null) Destroy(pillar.gameObject);
            pillar = null;
            DropBoulder();
            AgentUtil.Reenable(Agent, 8f);
            Enter(State.Idle);
        }

        protected override void OnMudBossDeath()
        {
            foreach (EarthWall wall in walls) if (wall != null) wall.Crumble();
            if (pillar != null) Destroy(pillar.gameObject);
            DropBoulder();
            Say("...well fought.", new Color(0.8f, 1f, 0.8f), 2.5f, 4f);
        }

        // ---------- Animation ----------

        // The walk: a real stride driven by how far he moves (one step every StepLength metres), with the legs
        // swinging opposite each other along his direction of travel (forward, back or a sidestep shuffle), the arms
        // counter-swinging, a hip roll and dip each step, a shoulder twist and a swishing tail. Turning on the spot
        // shuffles his feet too.
        private const float StepLength = 0.7f;
        private float stride, walkWeight, lastYaw;
        private Vector3 walkDirection = Vector3.forward;

        protected override void AnimateBoss(float dt)
        {
            float t = Time.time;
            dt = Mathf.Max(dt, 0.0001f);

            // How he's moving, in his own frame.
            Vector3 velocity = AgentReady ? Agent.velocity : Vector3.zero;
            Vector3 local = transform.InverseTransformDirection(velocity);
            local.y = 0f;
            float speed = local.magnitude;
            float yaw = transform.eulerAngles.y;
            float turnRate = Mathf.Abs(Mathf.DeltaAngle(lastYaw, yaw)) / dt;
            lastYaw = yaw;
            bool free = state == State.Idle || state == State.Reposition || !Engaged;
            float turning = free && speed < 0.4f ? Mathf.Clamp01((turnRate - 40f) / 160f) : 0f;
            float wantWalk = free ? Mathf.Max(Mathf.Clamp01(speed / 1.2f), turning) : 0f;
            walkWeight = Mathf.MoveTowards(walkWeight, wantWalk, dt * 5f);
            Vector3 heading = speed > 0.15f ? local / speed : turning > 0f ? Vector3.forward : walkDirection;
            walkDirection = Vector3.Lerp(walkDirection, heading, Mathf.Min(1f, dt * 10f));
            stride = Mathf.Repeat(stride + (speed + turning * 1.2f) / StepLength * Mathf.PI * dt, Mathf.PI * 2f); // half a cycle a step
            float forward = walkDirection.z, sideways = walkDirection.x;
            float swing = Mathf.Sin(stride) * Mathf.Lerp(16f, 34f, Mathf.Clamp01(speed / 5f)) * walkWeight; // + = left foot forward

            float legs = -25f, armRLift = -60f, armLLift = -70f, lean = 0f, torsoY = 0.9f;
            float spread = 22f; // the wide horse stance
            switch (state)
            {
                case State.Idle:
                case State.Reposition:
                    // Standing: a slow, breathing guard.
                    armRLift = -80f + Mathf.Sin(t * 1.5f + BossSeed) * 10f;
                    armLLift = -100f + Mathf.Cos(t * 1.5f + BossSeed) * 10f;
                    torsoY += Mathf.Sin(t * 1.5f + BossSeed) * 0.01f;
                    break;
                case State.MudBall:
                    float lift = Mathf.Clamp01(stateTime / 0.6f);
                    armRLift = Mathf.Lerp(-20f, -170f, lift);
                    armLLift = Mathf.Lerp(-20f, -170f, lift);
                    legs = -35f;
                    lean = -10f * lift;
                    break;
                case State.Wall:
                    armRLift = -170f;
                    armLLift = -170f;
                    lean = -10f;
                    break;
                case State.Rush:
                    armRLift = stateTime < 0.6f ? -170f : 20f;
                    lean = stateTime < 0.6f ? -12f : 35f;
                    legs = -40f;
                    break;
                case State.Winded:
                    lean = 25f;
                    armRLift = -10f;
                    armLLift = -10f;
                    torsoY += Mathf.Sin(t * 10f) * 0.03f;
                    break;
                case State.Stance:
                    armRLift = -90f;
                    armLLift = -90f;
                    legs = -45f;
                    spread = 30f;
                    break;
                case State.Palm:
                    armRLift = -90f;
                    armLLift = -10f;
                    lean = 12f;
                    break;
                case State.Dazed:
                    lean = Mathf.Sin(t * 5f) * 15f;
                    armRLift = -30f;
                    armLLift = -30f;
                    break;
                case State.Leap:
                    lean = 60f * Mathf.Clamp01(stateTime / 0.9f);
                    armRLift = -120f;
                    armLLift = -40f;
                    break;
                case State.Stomp:
                    // Crouched low, arms drawn back, ready to stamp.
                    legs = -50f;
                    spread = 32f;
                    armRLift = 25f;
                    armLLift = 25f;
                    lean = 18f;
                    break;
                case State.Opener:
                    legs = -40f;
                    spread = 28f;
                    if (stateTime < OpenerWindup)
                    {
                        // Arms up, leaning back as he draws the boulder behind him.
                        armRLift = -170f;
                        armLLift = -170f;
                        lean = -14f * Mathf.Clamp01(stateTime / OpenerWindup);
                    }
                    else
                    {
                        // The follow-through, arms flung after it.
                        armRLift = -70f;
                        armLLift = -60f;
                        lean = 22f;
                    }
                    break;
            }

            // Blend the walk over the pose.
            legs = Mathf.Lerp(legs, -4f, walkWeight);
            spread = Mathf.Lerp(spread, 9f, walkWeight);
            float armSwing = swing * 0.8f * forward;
            armRLift = Mathf.Lerp(armRLift, -22f, walkWeight) - armSwing;
            armLLift = Mathf.Lerp(armLLift, -22f, walkWeight) + armSwing;
            lean = Mathf.Lerp(lean, 2f + 6f * forward * Mathf.Clamp01(speed / 3f), walkWeight);
            float twist = -swing * 0.35f * forward;
            float step = swing * sideways * 0.7f;
            float rate = Mathf.Lerp(10f, 25f, walkWeight);
            legL.localRotation = Quaternion.Slerp(legL.localRotation, Quaternion.Euler(legs - swing * forward, 0f, -spread + step), dt * rate);
            legR.localRotation = Quaternion.Slerp(legR.localRotation, Quaternion.Euler(legs + swing * forward, 0f, spread + step), dt * rate);
            armR.localRotation = Quaternion.Slerp(armR.localRotation, Quaternion.Euler(armRLift, 0f, 15f), dt * 12f);
            armL.localRotation = Quaternion.Slerp(armL.localRotation, Quaternion.Euler(armLLift, 0f, -25f), dt * 12f);
            torso.localRotation = Quaternion.Slerp(torso.localRotation, Quaternion.Euler(lean, twist, 0f), dt * rate);
            torso.localPosition = new Vector3(0f, torsoY, 0f);
            // Crouched low in the stance, as in the reference. Walking, he rises a little and dips each step as his
            // straight legs reach out (so both feet stay on the ground), rolling his hips over the planted foot.
            float crouch = Mathf.Lerp(state == State.Stance ? 0.18f : 0.1f, 0.03f, walkWeight);
            float dip = 0.55f * (1f - Mathf.Cos(swing * Mathf.Deg2Rad));
            Visual.localPosition = VisualRest + Vector3.down * (crouch + dip);
            Visual.localRotation = VisualRestRotation * Quaternion.Euler(0f, 0f, -Mathf.Cos(stride) * 3f * walkWeight);
            tail.localRotation = Quaternion.Euler(Mathf.Sin(t * 1.6f) * 4f,
                Mathf.Sin(t * 2f) * 15f * (1f - walkWeight) + Mathf.Sin(stride) * 22f * walkWeight, 0f);
            float flutter = 1f + Mathf.Clamp01(speed / 4f);
            ties.localRotation = Quaternion.Euler(Mathf.Sin(t * 3f * flutter) * 12f * flutter, Mathf.Sin(t * 2.3f * flutter) * 15f, 0f);
            // He turns his ear, not his eyes, toward what he hears.
            if (Head != null)
            {
                Vector3 toward = transform.InverseTransformPoint(heard);
                float look = Mathf.Clamp(Mathf.Atan2(toward.x, toward.z) * Mathf.Rad2Deg, -40f, 40f);
                Head.localRotation = Quaternion.Slerp(Head.localRotation, Quaternion.Euler(0f, look - twist * 0.6f, Quiet ? 20f : 0f), dt * 4f);
            }
            // The boulder from his entrance hangs over his head until he throws it, drawn back in the wind-up.
            if (boulder != null)
            {
                float pull = state == State.Opener ? Mathf.SmoothStep(0f, 1f, stateTime / OpenerWindup) : 0f;
                Vector3 hover = HoverPoint - transform.forward * 1.2f * pull + Vector3.up * (Mathf.Sin(t * 2.2f) * 0.12f + 0.4f * pull);
                boulder.position = Vector3.Lerp(boulder.position, hover, Mathf.Min(1f, dt * 10f));
                boulder.Rotate(new Vector3(9f, 23f, 5f) * dt, Space.Self);
            }
        }

        private Transform Head;

        protected override void UpdateDeath(float t)
        {
            // Bows, then sits down cross-legged.
            float bow = Mathf.Clamp01(t / 0.8f);
            torso.localRotation = Quaternion.Euler(50f * Mathf.Sin(bow * Mathf.PI), 0f, 0f);
            Visual.localPosition = VisualRest + Vector3.down * 0.45f * Mathf.Clamp01((t - 0.8f) / 0.5f);
            legL.localRotation = Quaternion.Euler(-80f * Mathf.Clamp01((t - 0.8f) / 0.5f), 0f, -40f);
            legR.localRotation = Quaternion.Euler(-80f * Mathf.Clamp01((t - 0.8f) / 0.5f), 0f, 40f);
        }
    }

    /// <summary>
    /// The otter's stone wall: rises out of the ground, blocks bolts and the car (a solid surface, so the car crashes
    /// into it), and crumbles after 6 s. A full-charge bolt, a heavy jump smash or the car's explosion breaks it.
    /// </summary>
    public sealed class EarthWall : MonoBehaviour
    {
        private float born, rise, life = 6f;
        private Vector3 rest;
        private bool crumbling;

        public static EarthWall Raise(Vector3 at, Quaternion facing, float riseSeconds)
        {
            var go = new GameObject("Earth Wall");
            go.transform.SetPositionAndRotation(at + Vector3.down * 3.2f, facing);
            MudArt.Part(go.transform, "Slab", Vector3.zero, Quaternion.identity, "earth_wall", k =>
            {
                k.Box(new Vector3(0f, 1.5f, 0f), new Vector3(6f, 3f, 0.9f), MudPalette.Stone);
                k.Box(new Vector3(-1.6f, 3.1f, 0f), new Vector3(1.4f, 0.4f, 0.8f), Quaternion.Euler(0f, 0f, 8f), MudPalette.Stone);
                k.Box(new Vector3(1.2f, 3.05f, 0.05f), new Vector3(2f, 0.3f, 0.7f), Quaternion.Euler(0f, 0f, -5f), MudPalette.Stone);
                k.Box(new Vector3(0f, 0.2f, 0.47f), new Vector3(6.2f, 0.5f, 0.1f), MudPalette.Mud);
            });
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 1.5f, 0f);
            box.size = new Vector3(6f, 3f, 0.9f);
            var health = go.AddComponent<Health>();
            health.SetMax(1000f);
            health.Restore();
            var wall = go.AddComponent<EarthWall>();
            health.Damaged += (h, amount, source) =>
            {
                h.Restore();
                if (amount >= 45f) wall.Crumble();
            };
            wall.born = Time.time;
            wall.rise = Mathf.Max(0.05f, riseSeconds);
            wall.rest = at;
            Dust(at, 30);
            CameraShake.Add(0.25f);
            return wall;
        }

        private static void Dust(Vector3 at, int count) =>
            ParticleBurst.Spawn(ParticleBurst.Kind.Sand, at, Vector3.up, count, 0.8f, 4f, 80f, new Color(0.45f, 0.38f, 0.3f, 0.8f));

        private void Update()
        {
            float t = Time.time - born;
            if (!crumbling)
            {
                transform.position = Vector3.Lerp(rest + Vector3.down * 3.2f, rest, Mathf.SmoothStep(0f, 1f, t / rise));
                if (t > life) Crumble();
                return;
            }
            transform.position += Vector3.down * Time.deltaTime * 4f;
            if (transform.position.y < rest.y - 3.3f) Destroy(gameObject);
        }

        public void Crumble()
        {
            if (crumbling) return;
            crumbling = true;
            foreach (Collider part in GetComponents<Collider>()) part.enabled = false;
            Dust(rest + Vector3.up * 1.5f, 40);
            Sfx.PlayAt("stone_rise", rest, 0.8f, 1.5f, 40f);
        }
    }

    /// <summary>One rock spike of the otter's Earth Rush: the ground cracks for 0.35 s, then a spike erupts (20, a launch).</summary>
    public sealed class RockSpike : MonoBehaviour
    {
        private float born;
        private bool erupted;
        private GameObject owner;
        private float damage;
        private Transform spike, crack;

        public static void Spawn(Vector3 at, GameObject owner, float damage)
        {
            var go = new GameObject("Rock Spike");
            go.transform.position = at;
            go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            var rock = go.AddComponent<RockSpike>();
            rock.owner = owner;
            rock.damage = damage;
            rock.born = Time.time;
            rock.crack = MudArt.Part(go.transform, "Crack", Vector3.up * 0.04f, Quaternion.identity, "rock_crack", k =>
            {
                k.Box(Vector3.zero, new Vector3(1.2f, 0.02f, 0.12f), Quaternion.Euler(0f, 30f, 0f), MudPalette.Black);
                k.Box(Vector3.zero, new Vector3(0.9f, 0.02f, 0.1f), Quaternion.Euler(0f, -40f, 0f), MudPalette.Black);
            });
            rock.spike = MudArt.Part(go.transform, "Spike", Vector3.down * 1.6f, Quaternion.identity, "rock_spike", k =>
            {
                k.Cone(Vector3.zero, Quaternion.identity, 0.55f, 1.6f, 5, MudPalette.Stone);
                k.Cone(new Vector3(0.4f, 0f, 0.2f), Quaternion.Euler(0f, 0f, -15f), 0.3f, 0.9f, 4, MudPalette.Stone);
            });
        }

        private void Update()
        {
            float t = Time.time - born;
            if (!erupted && t >= 0.35f)
            {
                erupted = true;
                Sfx.PlayAt("stone_rise", transform.position, 0.5f, 1.6f, 30f);
                ParticleBurst.Spawn(ParticleBurst.Kind.Sand, transform.position, Vector3.up, 10, 0.5f, 4f, 50f, new Color(0.45f, 0.38f, 0.3f, 0.8f));
                var player = FindAnyObjectByType<FirstPersonController>();
                if (player != null && (player.transform.position - transform.position).Flatten().magnitude < 1.1f && player.transform.position.y < transform.position.y + 0.9f)
                {
                    var health = player.GetComponent<Health>();
                    if (health != null) health.TakeDamage(damage, owner);
                    PlayerStun.Apply(player, 0.3f);
                }
            }
            if (erupted) spike.localPosition = Vector3.up * Mathf.Lerp(-1.6f, 0f, Mathf.Clamp01((t - 0.35f) / 0.08f)) + Vector3.down * Mathf.Clamp01((t - 1.6f) / 0.5f) * 1.6f;
            if (t > 2.2f) Destroy(gameObject);
        }
    }
}
