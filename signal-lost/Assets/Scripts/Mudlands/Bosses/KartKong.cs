using System;
using System.Collections;
using System.Collections.Generic;
using SignalLost.Audio;
using SignalLost.Combat;
using SignalLost.Effects;
using SignalLost.Enemies;
using SignalLost.Inventory;
using SignalLost.Player;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace SignalLost.Mudlands
{
    /// <summary>
    /// Quest line 1's mini-boss (Christian's design, docs/mudlands.md): a huge, grinning silverback squashed into a
    /// comically small go-kart at the Banana Speedway, wearing a self-awarded champion's belt.
    /// <list type="bullet">
    /// <item><b>Happy (in the kart):</b> drives slow laps steering, waving, honking, thumbs-up and fist pumps. Attacks:
    /// <b>rev and ram</b> (stops, revs with smoking tyres for 0.8 s, then charges in a nearly straight line, a comedy
    /// "BONK!" for 4 and a small shove; a miss ends in a skid) and the <b>donut</b> (when you're close: a ring warning,
    /// then he spins on the spot flinging mud, 3 inside 4 m, leaving a slowing mud patch). The kart stage is meant to be
    /// nearly harmless. The kart soaks up 75% of damage, and gives out at half health if it hasn't broken by then.</item>
    /// <item><b>Breaking the kart:</b> a charged bolt (20+), the combo finisher, a heavy jump smash or the car. The kart
    /// spins out and falls apart, he's thrown clear and lands on his backside holding the steering wheel, stares at it,
    /// gets up, beats his chest and screams "MY GO-KART!!!". Enraged from then on, on his knuckles.</item>
    /// <item><b>Enraged:</b> barrel toss (produces a barrel from behind his back, hoists it and hurls it; rolling barrels
    /// to jump or shoot; red ones explode), a backhand swipe with a big wind-up, a <b>ground pound</b> (rears up, both
    /// fists overhead, slams: a shockwave you jump over), a charge rush (chest-beat wind-up, then a gallop; dazed if he
    /// hits a tyre wall), and a banana snack (sits on the ground and peels a real banana; heals 12% unless a heavy hit
    /// makes him choke; the peel slips him if he charges over it).</item>
    /// <item><b>Furious</b> (base phase 2, half health): he roars and pounds the ground; quicker wind-ups, two barrels.</item>
    /// </list>
    /// The body is a code-built rig posed every frame (two-bone IK for the arms and legs; a knuckle-walk and a gallop
    /// gait with planted hands and feet), with a flapping scarf tail and flag. 520 HP. Drops the Auxiliary Starter Motor
    /// and the Kart Wheel (Mud Tires).
    /// </summary>
    public sealed class KartKong : MudBoss
    {
        private enum State { Laps, Rev, Ram, Donut, Breaking, Idle, Swipe, Pound, Rush, Dazed, Toss, Snack, Choke, Slip }
        private enum Gesture { None, Wave, ThumbsUp, FistPump, Honk, Vroom, Whee }

        private static readonly Color Fur = new Color(0.3f, 0.2f, 0.15f), FurDark = new Color(0.2f, 0.13f, 0.1f),
            Silver = new Color(0.56f, 0.54f, 0.52f), Skin = new Color(0.82f, 0.64f, 0.48f), ChestSkin = new Color(0.55f, 0.4f, 0.31f),
            HandSkin = new Color(0.36f, 0.27f, 0.23f), Nail = new Color(0.2f, 0.16f, 0.14f),
            KartYellow = new Color(0.98f, 0.78f, 0.15f), KartRed = new Color(0.85f, 0.18f, 0.12f), Banana = new Color(1f, 0.88f, 0.25f),
            BananaFlesh = new Color(0.98f, 0.94f, 0.75f), Gold = new Color(0.95f, 0.75f, 0.25f), Belt = new Color(0.14f, 0.12f, 0.12f),
            Leather = new Color(0.36f, 0.24f, 0.15f), Chrome = new Color(0.78f, 0.8f, 0.83f), Lens = new Color(0.55f, 0.85f, 1f),
            Happy = new Color(1f, 0.9f, 0.4f), Angry = new Color(1f, 0.5f, 0.3f);

        // ---------- Rig sizes (metres, his own frame: +Z forward, feet at y = 0) ----------
        private const float UpperArm = 0.92f, Forearm = 0.88f, Thigh = 0.5f, Shin = 0.45f, Ankle = 0.12f, WristToKnuckle = 0.44f;
        private static readonly Vector3 SpineAt = new Vector3(0f, 0.3f, 0f), NeckAt = new Vector3(0f, 1.32f, 0.3f),
            ShoulderAt = new Vector3(0.98f, 1.0f, 0f), HipJointAt = new Vector3(0.4f, -0.18f, 0.05f),
            WheelAt = new Vector3(0f, 0.98f, 0.3f), Exhaust = new Vector3(0.52f, 0.66f, -1.02f), HornBulb = new Vector3(0.42f, 0.56f, 0.56f);
        private const float WheelRadius = 0.25f;
        private static readonly Quaternion WheelTilt = Quaternion.LookRotation(new Vector3(0f, -0.92f, 0.39f), Vector3.forward);
        private static readonly Quaternion BoneFix = Quaternion.Euler(-90f, 0f, 0f);

        private State state = State.Laps;
        private float stateTime, nextBump, nextDonut, nextRush, nextToss, nextPound, nextSnack, nextLapEmote, eaten;
        private bool kartBroken, interrupted, poundLanded, roared;
        private int lap;
        private readonly List<Vector3> laps = new List<Vector3>();
        private Vector3 rushDirection, launchDirection;
        private bool rushHit;
        private GroundRing ring;

        // The rig.
        private Transform body, spine, head, armL, armR, elbowL, elbowR, handL, handR, legL, legR, kneeL, kneeR, footL, footR;
        private Transform kart, steering, flag, scarfTail, held, stars, banana, bananaFlesh;
        private readonly Transform[] kartWheels = new Transform[4];
        private readonly Transform[] wheelSpin = new Transform[4];
        private readonly Transform[] peels = new Transform[3];
        private readonly List<Jiggle> jiggles = new List<Jiggle>();

        // Posing.
        private Pose pose;
        private bool posed;
        private float lastDt, lookWeight, kick, kickVelocity, gaitPhase, gaitWeight, steer, lastYaw, kartBounce;
        private Vector2 lookAngles;
        private readonly float[] lastLimbPhase = new float[4];
        private Gesture gesture;
        private float gestureUntil, gestureStart, nextExhaust;
        private bool deathFell;

        private static readonly Collider[] Overlaps = new Collider[16];

        public override string Subtitle => "Speedway champion (self-declared)";
        public override int DisplayPhase => !kartBroken ? 1 : Phase == 2 ? 3 : 2;
        public bool KartBroken => kartBroken;

        protected override void Awake()
        {
            base.Awake();
            corpseTime = Mathf.Max(corpseTime, 5f); // time to stagger, sit and flop over
        }

        // =====================================================================================================
        // Model
        // =====================================================================================================

        protected override void BuildModel(Transform visual)
        {
            BuildKart(visual);
            body = MudArt.Pivot(visual, "Body", new Vector3(0f, 0.92f, -0.42f));
            MudArt.Part(body, "Hips", Vector3.zero, Quaternion.identity, "kong2_hips", HipsMesh);
            spine = MudArt.Pivot(body, "Spine", SpineAt);
            MudArt.Part(spine, "Torso", Vector3.zero, Quaternion.identity, "kong2_torso", TorsoMesh);
            // The ship's auxiliary starter motor, strapped on his back like a backpack.
            ShipPart = MudArt.Part(spine, "Auxiliary Starter Motor", new Vector3(0f, 0.72f, -0.92f), Quaternion.Euler(0f, 0f, 90f), "kong2_motor", MotorMesh);
            scarfTail = MudArt.Pivot(spine, "Scarf Tail", new Vector3(0.32f, 1.16f, 0.4f), Quaternion.Euler(18f, 0f, 14f));
            MudArt.Part(scarfTail, "Tail", Vector3.zero, Quaternion.identity, "kong2_scarf_tail", ScarfTailMesh);
            head = MudArt.Pivot(spine, "Head", NeckAt);
            BuildHead();
            armL = BuildArm(-1f, out elbowL, out handL);
            armR = BuildArm(1f, out elbowR, out handR);
            legL = BuildLeg(-1f, out kneeL, out footL);
            legR = BuildLeg(1f, out kneeR, out footR);
            held = MudArt.Pivot(visual, "Held", new Vector3(0f, 2.5f, 0.6f));
            BuildBanana();

            jiggles.Add(new Jiggle(scarfTail, new Vector3(0f, -0.45f, 0f), 30f, 2.6f, 4f));
            jiggles.Add(new Jiggle(flag, new Vector3(0f, 0f, -0.35f), 18f, 1.6f, 1f));
        }

        // ---------- The kart ----------

        private void BuildKart(Transform visual)
        {
            kart = MudArt.Pivot(visual, "Kart", Vector3.zero);
            MudArt.Part(kart, "Kart Body", Vector3.zero, Quaternion.identity, "kong2_kart", KartMesh);
            steering = MudArt.Pivot(kart, "Steering Wheel", WheelAt, WheelTilt);
            MudArt.Part(steering, "Wheel", Vector3.zero, Quaternion.identity, "kong2_steering", SteeringMesh);
            flag = MudArt.Pivot(kart, "Flag", new Vector3(-0.52f, 1.92f, -0.97f));
            MudArt.Part(flag, "Checkers", Vector3.zero, Quaternion.identity, "kong2_flag", FlagMesh);
            for (int i = 0; i < 4; i++)
            {
                bool front = i < 2;
                float side = i % 2 == 0 ? -1f : 1f;
                var at = front ? new Vector3(0.62f * side, 0.21f, 0.66f) : new Vector3(0.67f * side, 0.25f, -0.62f);
                kartWheels[i] = MudArt.Pivot(kart, "Kart Wheel " + i, at);
                float radius = front ? 0.21f : 0.25f, width = front ? 0.2f : 0.27f;
                wheelSpin[i] = MudArt.Part(kartWheels[i], "Tyre", Vector3.zero, Quaternion.identity, front ? "kong2_wheel_f" + (side < 0 ? "l" : "r") : "kong2_wheel_r" + (side < 0 ? "l" : "r"),
                    k => WheelMesh(k, radius, width, side));
            }
        }

        private static void KartMesh(MeshKit k)
        {
            // Floor pan and the tubular frame.
            k.Box(new Vector3(0f, 0.15f, -0.02f), new Vector3(1.02f, 0.05f, 1.78f), MudPalette.DarkMetal);
            foreach (float s in new[] { -1f, 1f })
            {
                k.Rod(new Vector3(0.5f * s, 0.18f, -0.9f), new Vector3(0.5f * s, 0.18f, 0.9f), 0.03f, 6, MudPalette.DarkMetal);
                k.Rod(new Vector3(0.69f * s, 0.21f, -0.42f), new Vector3(0.69f * s, 0.21f, 0.45f), 0.032f, 6, Chrome); // nerf bars
                k.Rod(new Vector3(0.69f * s, 0.21f, -0.42f), new Vector3(0.5f * s, 0.18f, -0.5f), 0.032f, 6, Chrome);
                k.Rod(new Vector3(0.69f * s, 0.21f, 0.45f), new Vector3(0.5f * s, 0.18f, 0.52f), 0.032f, 6, Chrome);
                // Side pods: yellow, a red racing stripe, a checker strip.
                k.Box(new Vector3(0.53f * s, 0.29f, -0.02f), new Vector3(0.2f, 0.2f, 1.05f), KartYellow);
                k.Box(new Vector3(0.53f * s, 0.4f, -0.02f), new Vector3(0.21f, 0.03f, 1.06f), KartRed);
                for (int i = 0; i < 8; i++)
                    k.Box(new Vector3(0.635f * s, 0.27f, -0.47f + i * 0.13f), new Vector3(0.01f, 0.06f, 0.065f), MudPalette.Black);
                for (int i = 0; i < 8; i++)
                    k.Box(new Vector3(0.635f * s, 0.21f, -0.405f + i * 0.13f), new Vector3(0.01f, 0.06f, 0.065f), MudPalette.Black);
                k.Box(new Vector3(0.16f * s, 0.25f, 0.64f), new Vector3(0.1f, 0.04f, 0.16f), Quaternion.Euler(-25f, 0f, 0f), MudPalette.DarkMetal); // pedals
            }
            // The nose cone with a "1" roundel.
            k.Smooth = true;
            k.Ball(new Vector3(0f, 0.32f, 0.86f), new Vector3(0.4f, 0.16f, 0.32f), Quaternion.Euler(-8f, 0f, 0f), KartRed, 1);
            k.Smooth = false;
            k.Box(new Vector3(0f, 0.3f, 0.62f), new Vector3(0.78f, 0.2f, 0.36f), Quaternion.Euler(-14f, 0f, 0f), KartRed);
            k.Prism(new Vector3(0f, 0.43f, 0.82f), Quaternion.Euler(-30f, 0f, 0f), 0.14f, 0.14f, 0.02f, 14, MudPalette.White);
            k.Box(new Vector3(0f, 0.455f, 0.825f), new Vector3(0.04f, 0.012f, 0.16f), Quaternion.Euler(-30f, 0f, 0f), MudPalette.Black);
            k.Box(new Vector3(-0.03f, 0.47f, 0.87f), new Vector3(0.05f, 0.012f, 0.03f), Quaternion.Euler(-30f, 0f, -35f), MudPalette.Black);
            // Bumpers.
            k.Tube(new[] { new Vector3(-0.62f, 0.22f, 0.82f), new Vector3(-0.5f, 0.22f, 1.1f), new Vector3(0.5f, 0.22f, 1.1f), new Vector3(0.62f, 0.22f, 0.82f) }, 0.035f, 6, Chrome);
            k.Tube(new[] { new Vector3(-0.78f, 0.26f, -0.78f), new Vector3(-0.62f, 0.26f, -1.02f), new Vector3(0.62f, 0.26f, -1.02f), new Vector3(0.78f, 0.26f, -0.78f) }, 0.04f, 6, Chrome);
            // The seat: a tiny bucket, much too small for him.
            k.Box(new Vector3(0f, 0.4f, -0.4f), new Vector3(0.55f, 0.08f, 0.46f), MudPalette.Black);
            k.Box(new Vector3(0f, 0.64f, -0.66f), new Vector3(0.55f, 0.46f, 0.08f), Quaternion.Euler(-12f, 0f, 0f), MudPalette.Black);
            k.Box(new Vector3(0f, 0.64f, -0.62f), new Vector3(0.12f, 0.44f, 0.01f), Quaternion.Euler(-12f, 0f, 0f), KartRed);
            // Engine, fuel tank, exhaust.
            k.Box(new Vector3(0.34f, 0.36f, -0.74f), new Vector3(0.34f, 0.28f, 0.3f), MudPalette.DarkMetal);
            for (int i = 0; i < 4; i++) k.Box(new Vector3(0.34f, 0.52f + i * 0.04f, -0.74f), new Vector3(0.3f - i * 0.03f, 0.015f, 0.26f), MudPalette.Metal);
            k.Prism(new Vector3(0.12f, 0.42f, -0.74f), Quaternion.Euler(0f, 0f, 90f), 0.08f, 0.09f, 0.1f, 10, KartRed); // air filter
            k.Prism(new Vector3(-0.3f, 0.26f, -0.74f), Quaternion.identity, 0.13f, 0.13f, 0.24f, 10, KartRed);       // fuel tank
            k.Prism(new Vector3(-0.3f, 0.5f, -0.74f), Quaternion.identity, 0.04f, 0.04f, 0.04f, 8, MudPalette.Black);
            k.Tube(new[] { new Vector3(0.45f, 0.38f, -0.85f), new Vector3(0.52f, 0.42f, -0.95f), new Vector3(0.52f, 0.58f, -1.0f), new Vector3(0.52f, 0.64f, -1.02f) }, 0.045f, 8, Chrome);
            k.Prism(new Vector3(0.52f, 0.6f, -1.02f), Quaternion.identity, 0.06f, 0.07f, 0.06f, 8, MudPalette.Black);
            // Steering column.
            k.Rod(new Vector3(0f, 0.26f, 0.62f), WheelAt, 0.03f, 6, MudPalette.DarkMetal);
            // A bicycle horn with a red bulb.
            k.Cone(new Vector3(0.42f, 0.5f, 0.66f), Quaternion.Euler(80f, 0f, 0f), 0.07f, 0.17f, 8, Gold);
            k.Rod(new Vector3(0.42f, 0.5f, 0.66f), HornBulb, 0.015f, 5, Gold);
            k.Smooth = true;
            k.Ball(HornBulb, new Vector3(0.07f, 0.07f, 0.09f), Quaternion.identity, KartRed, 1);
            k.Smooth = false;
            // The whip antenna (the flag is its own springy part).
            k.Rod(new Vector3(-0.52f, 0.3f, -0.86f), new Vector3(-0.52f, 1.92f, -0.97f), 0.012f, 4, Chrome);
            // A banana air freshener hanging off the column.
            k.Rod(new Vector3(0f, 0.86f, 0.36f), new Vector3(0.03f, 0.72f, 0.42f), 0.004f, 3, MudPalette.White);
            k.Tube(new[] { new Vector3(0.0f, 0.72f, 0.42f), new Vector3(0.05f, 0.64f, 0.43f), new Vector3(0.05f, 0.56f, 0.42f) }, 0.022f, 6, Banana);
        }

        private static void SteeringMesh(MeshKit k)
        {
            const int n = 18;
            for (int i = 0; i < n; i++)
            {
                float a0 = i * Mathf.PI * 2f / n, a1 = (i + 1) * Mathf.PI * 2f / n;
                k.Rod(new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f) * WheelRadius, new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f) * WheelRadius, 0.035f, 6, MudPalette.Black);
            }
            for (int i = 0; i < 3; i++)
            {
                float a = (i * 120f - 90f) * Mathf.Deg2Rad;
                k.Rod(Vector3.zero, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * WheelRadius, 0.018f, 5, Chrome);
            }
            k.Prism(new Vector3(0f, 0f, 0.03f), Quaternion.Euler(-90f, 0f, 0f), 0.07f, 0.06f, 0.05f, 10, KartRed);
            k.Box(new Vector3(0f, WheelRadius, -0.01f), new Vector3(0.04f, 0.05f, 0.075f), KartYellow); // the top marker
        }

        private static void FlagMesh(MeshKit k)
        {
            for (int x = 0; x < 4; x++)
                for (int y = 0; y < 3; y++)
                {
                    Color c = (x + y) % 2 == 0 ? MudPalette.Black : MudPalette.White;
                    var centre = new Vector3(0f, -0.04f - y * 0.08f, -0.04f - x * 0.08f);
                    k.Box(centre, new Vector3(0.012f, 0.08f, 0.08f), c);
                }
        }

        private static void WheelMesh(MeshKit k, float radius, float width, float side)
        {
            float o = side * width * 0.5f;
            Quaternion axle = Quaternion.Euler(0f, 0f, 90f);
            k.Smooth = true;
            k.Prism(new Vector3(-o, 0f, 0f), axle, radius, radius, width, 14, MudPalette.Rubber);
            k.Smooth = false;
            for (int i = 0; i < 14; i++) // tread blocks
            {
                float a = i * Mathf.PI * 2f / 14f;
                var dir = new Vector3(0f, Mathf.Cos(a), Mathf.Sin(a));
                k.Box(dir * (radius + 0.01f), new Vector3(width * 0.9f, 0.03f, 0.05f), Quaternion.LookRotation(Vector3.Cross(dir, Vector3.right), dir), MudPalette.Rubber);
            }
            k.Prism(new Vector3(side * width * 0.52f, 0f, 0f), Quaternion.Euler(0f, 0f, side * -90f), radius * 0.6f, radius * 0.55f, 0.02f, 10, KartRed);
            k.Prism(new Vector3(side * width * 0.54f, 0f, 0f), Quaternion.Euler(0f, 0f, side * -90f), radius * 0.2f, radius * 0.16f, 0.03f, 8, Chrome);
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2f / 5f;
                k.Box(new Vector3(side * width * 0.53f, Mathf.Cos(a) * radius * 0.38f, Mathf.Sin(a) * radius * 0.38f), new Vector3(0.02f, 0.03f, 0.03f), Chrome);
            }
        }

        // ---------- Body ----------

        private static void HipsMesh(MeshKit k)
        {
            k.Smooth = true;
            k.Ball(new Vector3(0f, 0.05f, 0.02f), new Vector3(0.82f, 0.6f, 0.7f), Quaternion.identity, Fur, 1);
            k.Ball(new Vector3(0f, 0.12f, 0.36f), new Vector3(0.6f, 0.5f, 0.38f), Quaternion.identity, ChestSkin, 1); // pot belly
            k.Ball(new Vector3(0f, 0.04f, 0.735f), new Vector3(0.035f, 0.03f, 0.02f), Quaternion.identity, FurDark, 1);
            // The champion's belt (self-awarded): black leather, a gold plate with a "1" and a red stone.
            k.Prism(new Vector3(0f, -0.1f, 0.02f), Quaternion.identity, 0.84f, 0.8f, 0.18f, 22, Belt);
            k.Smooth = false;
            k.Box(new Vector3(0f, -0.01f, 0.8f), new Vector3(0.48f, 0.32f, 0.06f), Quaternion.Euler(-4f, 0f, 0f), Gold);
            k.Box(new Vector3(0f, -0.01f, 0.835f), new Vector3(0.38f, 0.22f, 0.02f), Quaternion.Euler(-4f, 0f, 0f), new Color(0.75f, 0.55f, 0.15f));
            k.Box(new Vector3(0.06f, -0.01f, 0.85f), new Vector3(0.05f, 0.18f, 0.02f), Quaternion.Euler(-4f, 0f, 0f), MudPalette.Black);
            k.Box(new Vector3(0.03f, 0.065f, 0.85f), new Vector3(0.05f, 0.03f, 0.02f), Quaternion.Euler(-4f, 0f, 30f), MudPalette.Black);
            k.Ball(new Vector3(-0.1f, 0.05f, 0.855f), 0.04f, KartRed, 1, 1.2f);
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 side = new Vector3(Mathf.Sin(s * 0.75f) * 0.82f, -0.01f, Mathf.Cos(s * 0.75f) * 0.78f);
                k.Box(side, new Vector3(0.18f, 0.22f, 0.05f), Quaternion.Euler(0f, s * 43f, 0f), Gold);
            }
            // Shaggy fur round the hips.
            k.Smooth = true;
            for (int i = 0; i < 10; i++)
            {
                float a = (i / 10f + 0.05f) * Mathf.PI * 2f;
                if (Mathf.Cos(a) > 0.6f) continue; // not over the belt plate
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                k.Ball(dir * 0.76f + Vector3.down * 0.28f, new Vector3(0.13f, 0.17f, 0.1f), Quaternion.LookRotation(dir) * Quaternion.Euler(-20f, 0f, 0f), Fur, 1);
            }
        }

        private static void TorsoMesh(MeshKit k)
        {
            k.Smooth = true;
            k.Ball(new Vector3(0f, 0.68f, 0f), new Vector3(1.0f, 0.82f, 0.72f), Quaternion.identity, Fur, 1);           // barrel chest
            k.Ball(new Vector3(0f, 1.0f, -0.05f), new Vector3(0.95f, 0.5f, 0.6f), Quaternion.identity, Fur, 1);         // shoulders and traps
            k.Ball(new Vector3(0f, 0.62f, -0.4f), new Vector3(0.86f, 0.62f, 0.4f), Quaternion.identity, Silver, 1);     // the silver saddle
            k.Ball(new Vector3(0f, 0.75f, 0.42f), new Vector3(0.6f, 0.56f, 0.34f), Quaternion.identity, ChestSkin, 1);  // chest plate
            foreach (float x in new[] { -0.28f, 0.28f })
                k.Ball(new Vector3(x, 0.88f, 0.52f), new Vector3(0.31f, 0.22f, 0.22f), Quaternion.Euler(0f, 0f, x * 30f), ChestSkin, 1); // pecs
            foreach (float x in new[] { -0.82f, 0.82f })
                k.Ball(new Vector3(x, 1.12f, -0.08f), new Vector3(0.3f, 0.22f, 0.34f), Quaternion.Euler(0f, 0f, x * 32f), Fur, 1); // shoulder tufts
            for (int i = 0; i < 8; i++) // shaggy fringe round the chest plate
            {
                float a = Mathf.Lerp(-80f, 80f, i / 7f) * Mathf.Deg2Rad;
                k.Ball(new Vector3(Mathf.Sin(a) * 0.68f, 0.3f + Mathf.Abs(Mathf.Sin(a)) * 0.2f, Mathf.Cos(a) * 0.55f), new Vector3(0.12f, 0.16f, 0.1f), Quaternion.Euler(-15f, a * Mathf.Rad2Deg, 0f), Fur, 1);
            }
            // The checkered racing scarf.
            k.Smooth = false;
            for (int i = 0; i < 16; i++)
            {
                float a = i / 16f * Mathf.PI * 2f;
                k.Box(new Vector3(Mathf.Sin(a) * 0.52f, 1.22f, Mathf.Cos(a) * 0.46f + 0.12f), new Vector3(0.22f, 0.18f, 0.12f), Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f), i % 2 == 0 ? MudPalette.Black : MudPalette.White);
                k.Box(new Vector3(Mathf.Sin(a) * 0.53f, 1.11f, Mathf.Cos(a) * 0.47f + 0.12f), new Vector3(0.22f, 0.06f, 0.12f), Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f), i % 2 == 1 ? MudPalette.Black : MudPalette.White);
            }
            k.Smooth = true;
            k.Ball(new Vector3(0.32f, 1.16f, 0.5f), new Vector3(0.12f, 0.1f, 0.08f), Quaternion.identity, MudPalette.White, 1); // the knot
            // A gold medal on a red ribbon (he gave it to himself).
            k.Smooth = false;
            k.Rod(new Vector3(-0.15f, 1.12f, 0.62f), new Vector3(0f, 0.82f, 0.79f), 0.025f, 4, KartRed);
            k.Rod(new Vector3(0.15f, 1.12f, 0.62f), new Vector3(0f, 0.82f, 0.79f), 0.025f, 4, KartRed);
            k.Prism(new Vector3(0f, 0.74f, 0.79f), Quaternion.Euler(80f, 0f, 0f), 0.13f, 0.13f, 0.035f, 14, Gold, 0.4f);
            k.Box(new Vector3(0f, 0.75f, 0.83f), new Vector3(0.025f, 0.11f, 0.01f), Quaternion.Euler(-10f, 0f, 0f), new Color(0.6f, 0.42f, 0.1f));
            // Backpack straps for the starter motor.
            foreach (float s in new[] { -1f, 1f })
                k.Tube(new[] { new Vector3(0.42f * s, 0.45f, 0.55f), new Vector3(0.5f * s, 1.05f, 0.42f), new Vector3(0.5f * s, 1.32f, 0.05f), new Vector3(0.45f * s, 1.15f, -0.55f), new Vector3(0.4f * s, 0.55f, -0.85f) }, 0.045f, 6, Leather);
        }

        private static void ScarfTailMesh(MeshKit k)
        {
            for (int i = 0; i < 5; i++)
                for (int x = 0; x < 2; x++)
                    k.Box(new Vector3(-0.05f + x * 0.1f, -0.05f - i * 0.09f, 0f), new Vector3(0.1f, 0.09f, 0.03f), (i + x) % 2 == 0 ? MudPalette.Black : MudPalette.White);
            for (int x = 0; x < 3; x++) k.Box(new Vector3(-0.07f + x * 0.07f, -0.5f, 0f), new Vector3(0.025f, 0.06f, 0.02f), MudPalette.White); // fringe
        }

        private static void MotorMesh(MeshKit k)
        {
            k.Prism(new Vector3(0f, -0.35f, 0f), Quaternion.identity, 0.28f, 0.28f, 0.7f, 12, MudPalette.Metal);
            k.Prism(new Vector3(0f, 0.35f, 0f), Quaternion.identity, 0.15f, 0.1f, 0.2f, 8, MudPalette.DarkMetal);
            for (int i = 0; i < 4; i++) k.Prism(new Vector3(0f, -0.25f + i * 0.15f, 0f), Quaternion.identity, 0.3f, 0.3f, 0.04f, 12, new Color(0.25f, 0.6f, 0.9f));
            k.Ball(new Vector3(0f, 0.1f, 0.3f), 0.06f, new Color(0.3f, 0.9f, 1f), 0, 3f);
            k.Rod(new Vector3(0.2f, -0.4f, 0.15f), new Vector3(0.28f, 0.3f, 0.18f), 0.02f, 5, KartRed); // a wire
        }

        // ---------- Head ----------

        private void BuildHead()
        {
            MudArt.Part(head, "Face", Vector3.zero, Quaternion.identity, "kong2_head", HeadMesh);
            Transform mouth = BossFace.Mouth(head, "kong_mouth", new Vector3(0f, -0.27f, 0.6f), 0.42f, 0.16f, new Color(0.55f, 0.36f, 0.28f), true, Quaternion.Euler(-12f, 0f, 0f));
            (Transform l, Transform r) = MudArt.Eyes(head, new Vector3(0f, 0.13f, 0.47f), 0.15f, 0.085f, MudPalette.White, MudPalette.Black, "kong");
            Transform browL = BossFace.Brow(head, "kong_brow", new Vector3(-0.15f, 0.27f, 0.53f), -1f, new Vector3(0.24f, 0.07f, 0.09f), FurDark, 8f);
            Transform browR = BossFace.Brow(head, "kong_brow", new Vector3(0.15f, 0.27f, 0.53f), 1f, new Vector3(0.24f, 0.07f, 0.09f), FurDark, 8f);
            FaceParts = new BossFace.Rig
            {
                BrowL = browL, BrowR = browR, EyeL = l, EyeR = r, Mouth = mouth,
                BrowTravel = 0.05f, Idle = Mood.Laugh, Fight = Mood.Angry,
            };
            MudArt.Part(head, "Helmet", Vector3.zero, Quaternion.identity, "kong2_helmet", HelmetMesh);
            stars = MudArt.Pivot(head, "Dazed Stars", new Vector3(0f, 0.75f, -0.05f));
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f;
                MudArt.Part(stars, "Star " + i, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.7f, Quaternion.identity, "kong2_star", k => Star(k, 0.12f, 0.04f));
            }
            stars.gameObject.SetActive(false);
        }

        private static void HeadMesh(MeshKit k)
        {
            k.Smooth = true;
            k.Ball(new Vector3(0f, 0.05f, -0.05f), new Vector3(0.55f, 0.52f, 0.5f), Quaternion.identity, Fur, 1);
            k.Ball(new Vector3(0f, 0.36f, -0.18f), new Vector3(0.3f, 0.32f, 0.42f), Quaternion.identity, Fur, 1); // the sagittal crest
            foreach (float x in new[] { -0.15f, 0.15f })
                k.Ball(new Vector3(x, 0.14f, 0.3f), new Vector3(0.24f, 0.2f, 0.22f), Quaternion.identity, Skin, 1);
            k.Ball(new Vector3(0f, -0.17f, 0.34f), new Vector3(0.43f, 0.3f, 0.32f), Quaternion.identity, Skin, 1); // muzzle
            k.Ball(new Vector3(0f, -0.05f, 0.56f), new Vector3(0.2f, 0.11f, 0.12f), Quaternion.identity, Skin, 1); // nose bridge
            k.Ball(new Vector3(0f, 0.27f, 0.4f), new Vector3(0.38f, 0.09f, 0.15f), Quaternion.identity, FurDark, 1); // the heavy brow ridge
            foreach (float x in new[] { -0.08f, 0.08f }) // flared nostrils
            {
                k.Ball(new Vector3(x * 1.3f, -0.07f, 0.62f), new Vector3(0.08f, 0.06f, 0.06f), Quaternion.Euler(0f, 0f, x * 200f), Skin, 1);
                k.Ball(new Vector3(x, -0.07f, 0.665f), new Vector3(0.045f, 0.03f, 0.03f), Quaternion.Euler(0f, 0f, x * 200f), new Color(0.2f, 0.12f, 0.1f), 1);
            }
            foreach (float x in new[] { -0.47f, 0.47f }) // ears
            {
                k.Ball(new Vector3(x, 0.06f, -0.05f), new Vector3(0.1f, 0.14f, 0.07f), Quaternion.identity, Skin, 1);
                k.Ball(new Vector3(x * 1.02f, 0.06f, -0.03f), new Vector3(0.05f, 0.08f, 0.04f), Quaternion.identity, new Color(0.62f, 0.45f, 0.33f), 1);
            }
            foreach (float x in new[] { -0.36f, 0.36f }) // jowl fur
                k.Ball(new Vector3(x, -0.22f, 0.12f), new Vector3(0.16f, 0.2f, 0.16f), Quaternion.Euler(0f, 0f, x * 40f), Fur, 1);
            k.Ball(new Vector3(0f, -0.4f, 0.28f), new Vector3(0.24f, 0.08f, 0.14f), Quaternion.identity, Skin, 1); // chin
            for (int i = 0; i < 3; i++) // wrinkles on the muzzle
                k.Ball(new Vector3(0f, 0.04f + i * 0.04f, 0.5f), new Vector3(0.1f - i * 0.02f, 0.008f, 0.02f), Quaternion.identity, new Color(0.65f, 0.48f, 0.36f), 0);
        }

        // A racing helmet pushed back on his head (much too small), with goggles on the brim.
        private static void HelmetMesh(MeshKit k)
        {
            k.Smooth = true;
            Quaternion tip = Quaternion.Euler(-20f, 0f, 0f);
            k.Ball(new Vector3(0f, 0.52f, -0.22f), new Vector3(0.33f, 0.22f, 0.33f), tip, KartRed, 1);
            k.Smooth = false;
            k.Box(new Vector3(0f, 0.6f, -0.12f), new Vector3(0.07f, 0.16f, 0.42f), tip, MudPalette.White);
            k.Box(new Vector3(0f, 0.56f, 0.08f), new Vector3(0.4f, 0.03f, 0.12f), Quaternion.Euler(-35f, 0f, 0f), MudPalette.Black); // visor peak
            // Goggles: a strap and two lenses resting on the brim.
            k.Tube(new[] { new Vector3(-0.33f, 0.43f, -0.05f), new Vector3(-0.3f, 0.47f, 0.12f), new Vector3(0f, 0.5f, 0.24f), new Vector3(0.3f, 0.47f, 0.12f), new Vector3(0.33f, 0.43f, -0.05f) }, 0.025f, 5, Leather);
            foreach (float x in new[] { -0.12f, 0.12f })
            {
                Quaternion face = Quaternion.Euler(-60f, 0f, 0f);
                k.Prism(new Vector3(x, 0.5f, 0.23f), face, 0.085f, 0.085f, 0.04f, 12, Chrome);
                k.Prism(new Vector3(x, 0.52f, 0.24f), face, 0.065f, 0.065f, 0.035f, 12, Lens, 0.4f);
            }
            k.Box(new Vector3(0f, 0.5f, 0.24f), new Vector3(0.06f, 0.03f, 0.03f), Chrome);
        }

        // ---------- Arms, hands, legs ----------

        private Transform BuildArm(float side, out Transform elbow, out Transform hand)
        {
            Transform shoulder = MudArt.Pivot(spine, side < 0f ? "Arm L" : "Arm R", new Vector3(ShoulderAt.x * side, ShoulderAt.y, ShoulderAt.z));
            MudArt.Part(shoulder, "Upper", Vector3.zero, Quaternion.identity, "kong2_upper", UpperArmMesh);
            elbow = MudArt.Pivot(shoulder, "Elbow", new Vector3(0f, -UpperArm, 0f));
            MudArt.Part(elbow, "Forearm", Vector3.zero, Quaternion.identity, "kong2_fore", ForearmMesh);
            hand = MudArt.Pivot(elbow, "Hand", new Vector3(0f, -Forearm, 0f));
            MudArt.Part(hand, "Fist", Vector3.zero, Quaternion.identity, side < 0f ? "kong2_hand_l" : "kong2_hand_r", k => HandMesh(k, side));
            return shoulder;
        }

        private static void UpperArmMesh(MeshKit k)
        {
            k.Smooth = true;
            k.Ball(Vector3.zero, 0.38f, Fur, 1);
            k.Rod(Vector3.zero, new Vector3(0f, -UpperArm, 0f), 0.33f, 12, Fur, 0f, 0.26f);
            k.Ball(new Vector3(0f, -UpperArm, 0f), 0.27f, Fur, 1);
            for (int i = 0; i < 3; i++) // shaggy fur down the back of the arm
                k.Ball(new Vector3(0f, -0.25f - i * 0.25f, -0.26f + i * 0.02f), new Vector3(0.14f, 0.2f, 0.1f), Quaternion.Euler(25f, 0f, 0f), Fur, 1);
        }

        private static void ForearmMesh(MeshKit k)
        {
            k.Smooth = true;
            k.Rod(Vector3.zero, new Vector3(0f, -Forearm + 0.06f, 0f), 0.27f, 12, Fur, 0f, 0.29f); // gorilla forearms: thicker at the wrist
            for (int i = 0; i < 2; i++)
                k.Ball(new Vector3(0f, -0.3f - i * 0.25f, -0.25f), new Vector3(0.13f, 0.18f, 0.1f), Quaternion.Euler(25f, 0f, 0f), Fur, 1);
            k.Ball(new Vector3(0f, -Forearm + 0.1f, 0f), new Vector3(0.33f, 0.13f, 0.33f), Quaternion.identity, Fur, 1); // shaggy cuff
            k.Smooth = false;
            k.Prism(new Vector3(0f, -Forearm + 0.0f, 0f), Quaternion.identity, 0.27f, 0.28f, 0.08f, 14, KartRed); // racing glove cuff
            k.Prism(new Vector3(0f, -Forearm + 0.03f, 0f), Quaternion.identity, 0.285f, 0.285f, 0.02f, 14, MudPalette.White);
        }

        // A huge knuckly fist in a fingerless racing glove. Fingers point down the hand's -Y; the palm faces +Z.
        private static void HandMesh(MeshKit k, float side)
        {
            k.Smooth = true;
            k.Ball(new Vector3(0f, -0.2f, 0.02f), new Vector3(0.27f, 0.23f, 0.22f), Quaternion.identity, HandSkin, 1);
            k.Ball(new Vector3(0f, -0.17f, -0.07f), new Vector3(0.28f, 0.2f, 0.16f), Quaternion.identity, KartRed, 1); // glove back
            k.Ball(new Vector3(0f, -0.15f, -0.22f), new Vector3(0.08f, 0.1f, 0.01f), Quaternion.identity, MudPalette.White, 0); // a white "K" patch
            for (int i = 0; i < 4; i++)
            {
                float x = (i - 1.5f) * 0.13f;
                k.Ball(new Vector3(x, -0.39f, 0.03f), new Vector3(0.075f, 0.075f, 0.085f), Quaternion.identity, HandSkin, 1); // knuckles
                k.Ball(new Vector3(x * 0.95f, -0.36f, 0.14f), new Vector3(0.065f, 0.07f, 0.09f), Quaternion.identity, HandSkin, 1); // curled fingers
                k.Ball(new Vector3(x * 0.95f, -0.3f, 0.21f), new Vector3(0.04f, 0.02f, 0.03f), Quaternion.identity, Nail, 0);
            }
            k.Ball(new Vector3(-0.22f * side, -0.26f, 0.15f), new Vector3(0.08f, 0.12f, 0.08f), Quaternion.Euler(0f, 0f, 25f * side), HandSkin, 1); // thumb
        }

        private Transform BuildLeg(float side, out Transform knee, out Transform foot)
        {
            Transform hip = MudArt.Pivot(body, side < 0f ? "Leg L" : "Leg R", new Vector3(HipJointAt.x * side, HipJointAt.y, HipJointAt.z));
            MudArt.Part(hip, "Thigh", Vector3.zero, Quaternion.identity, "kong2_thigh", k =>
            {
                k.Smooth = true;
                k.Ball(Vector3.zero, 0.3f, Fur, 1);
                k.Rod(Vector3.zero, new Vector3(0f, -Thigh, 0f), 0.29f, 12, Fur, 0f, 0.23f);
            });
            knee = MudArt.Pivot(hip, "Knee", new Vector3(0f, -Thigh, 0f));
            MudArt.Part(knee, "Shin", Vector3.zero, Quaternion.identity, "kong2_shin", k =>
            {
                k.Smooth = true;
                k.Ball(Vector3.zero, 0.23f, Fur, 1);
                k.Rod(Vector3.zero, new Vector3(0f, -Shin, 0f), 0.22f, 12, Fur, 0f, 0.17f);
                k.Ball(new Vector3(0f, -Shin + 0.06f, 0f), new Vector3(0.22f, 0.1f, 0.22f), Quaternion.identity, Fur, 1);
            });
            foot = MudArt.Pivot(knee, "Foot", new Vector3(0f, -Shin, 0f));
            MudArt.Part(foot, "Sole", Vector3.zero, Quaternion.identity, side < 0f ? "kong2_foot_l" : "kong2_foot_r", k => FootMesh(k, side));
            return hip;
        }

        // Toes along +Z, sole at y = -Ankle, the big grabbing toe out to the inside.
        private static void FootMesh(MeshKit k, float side)
        {
            k.Smooth = true;
            k.Ball(new Vector3(0f, -0.05f, 0.1f), new Vector3(0.2f, 0.08f, 0.27f), Quaternion.identity, HandSkin, 1);
            k.Ball(new Vector3(0f, -0.02f, -0.05f), new Vector3(0.16f, 0.1f, 0.16f), Quaternion.identity, HandSkin, 1);
            for (int i = 0; i < 4; i++)
                k.Ball(new Vector3((i - 1.2f) * 0.085f * side * -1f, -0.08f, 0.36f), new Vector3(0.05f, 0.045f, 0.07f), Quaternion.identity, HandSkin, 1);
            k.Ball(new Vector3(-0.2f * side, -0.08f, 0.24f), new Vector3(0.06f, 0.05f, 0.1f), Quaternion.Euler(0f, -35f * side, 0f), HandSkin, 1);
        }

        // ---------- Held things ----------

        private void BuildBanana()
        {
            banana = MudArt.Pivot(held, "Banana", Vector3.zero);
            MudArt.Part(banana, "Skin", Vector3.zero, Quaternion.identity, "kong2_banana_skin", k =>
            {
                k.Smooth = true;
                k.Tube(new[] { new Vector3(0f, -0.25f, 0.05f), new Vector3(0f, -0.12f, 0f), new Vector3(0f, 0f, 0f) }, 0.07f, 8, Banana);
                k.Smooth = false;
                k.Cone(new Vector3(0f, -0.25f, 0.05f), Quaternion.Euler(180f, 0f, 0f), 0.05f, 0.06f, 6, new Color(0.3f, 0.22f, 0.1f));
            });
            bananaFlesh = MudArt.Part(banana, "Flesh", Vector3.zero, Quaternion.identity, "kong2_banana_flesh", k =>
            {
                k.Smooth = true;
                k.Tube(new[] { Vector3.zero, new Vector3(0f, 0.14f, -0.02f), new Vector3(0f, 0.24f, -0.07f) }, 0.055f, 8, BananaFlesh);
            });
            for (int i = 0; i < 3; i++)
            {
                peels[i] = MudArt.Pivot(banana, "Peel " + i, Vector3.zero, Quaternion.Euler(0f, i * 120f, 0f));
                MudArt.Part(peels[i], "Strip", Vector3.zero, Quaternion.identity, "kong2_banana_peel", k =>
                {
                    k.Box(new Vector3(0f, 0.12f, 0.06f), new Vector3(0.07f, 0.24f, 0.015f), Banana);
                    k.Box(new Vector3(0f, 0.12f, 0.052f), new Vector3(0.06f, 0.22f, 0.005f), BananaFlesh);
                });
            }
            banana.gameObject.SetActive(false);
        }

        protected override Action<MeshKit> KeyItemModel() => k =>
        {
            k.Prism(new Vector3(0.1f, 0f, 0f), Quaternion.Euler(0f, 0f, 90f), 0.3f, 0.3f, 0.2f, 10, MudPalette.Rubber);
            k.Prism(new Vector3(0.105f, 0f, 0f), Quaternion.Euler(0f, 0f, 90f), 0.13f, 0.13f, 0.21f, 6, KartRed);
        };

        private static void Star(MeshKit k, float radius, float depth)
        {
            var ring = new Vector3[10];
            for (int i = 0; i < 10; i++)
            {
                float a = Mathf.PI * 0.5f + i * Mathf.PI / 5f, r = i % 2 == 0 ? radius : radius * 0.45f;
                ring[i] = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
            }
            Vector3 front = Vector3.forward * depth, back = -front;
            Color c = new Color(1f, 0.88f, 0.35f);
            for (int i = 0; i < 10; i++)
            {
                int j = (i + 1) % 10;
                k.Tri(front, ring[j], ring[i], c, 2.5f);
                k.Tri(front, ring[i], ring[j], c, 2.5f);
                k.Tri(back, ring[i], ring[j], c, 2.5f);
                k.Tri(back, ring[j], ring[i], c, 2.5f);
            }
        }

        // =====================================================================================================
        // Entrance
        // =====================================================================================================

        public override IEnumerator Entrance(BossEntrance cine)
        {
            Vector3 home = Home0;
            BuildLaps(home);
            Vector3 start = laps[0], beforeStart = laps[laps.Count - 3];
            Vector3 side = Vector3.Cross(Vector3.up, (start - beforeStart).normalized);
            // A traffic light on the start line: red, red, green.
            Transform light = MudArt.Part(null, "Start Light", start + side * 5f, Quaternion.LookRotation(-side), "kong_light", k =>
            {
                k.Rod(Vector3.zero, new Vector3(0f, 3f, 0f), 0.08f, 6, MudPalette.DarkMetal);
                k.Box(new Vector3(0f, 3.4f, 0f), new Vector3(0.5f, 1.3f, 0.3f), MudPalette.Black);
            });
            var lamps = new Transform[3];
            for (int i = 0; i < 3; i++)
                lamps[i] = MudArt.Part(light, "Lamp " + i, new Vector3(0f, 3.8f - i * 0.4f, 0.16f), Quaternion.identity, i < 2 ? "kong_lamp_red" : "kong_lamp_green",
                    k => k.Ball(Vector3.zero, 0.14f, new Color(0.15f, 0.15f, 0.15f), 1));
            cine.Cut(start + side * 9f + Vector3.up * 2.5f, light.position + Vector3.up * 3.4f);
            transform.position = beforeStart;
            transform.rotation = Quaternion.LookRotation(start - beforeStart);
            kart.gameObject.SetActive(true);
            PoseNow(KartPose(0f, Time.time), 0f, 0f, true);
            for (int i = 0; i < 3 && !cine.Skipped; i++)
            {
                yield return cine.Wait(0.6f);
                Color lit = i < 2 ? new Color(1f, 0.2f, 0.15f) : new Color(0.3f, 1f, 0.35f);
                lamps[i].GetComponent<MeshRenderer>().sharedMaterial = MudArt.Glow(lit, 3f);
                Sfx.Play(i < 2 ? "ui_click" : "quest_chime", 0.8f, i < 2 ? 0.7f : 1f);
                if (i < 2) Sfx.Play("rev", 0.6f, 0.8f + i * 0.2f); // revving off camera
            }
            // He tears round the bend honking, past the camera, and skids onto the line.
            cine.LookAt(transform, Vector3.up * 1.8f);
            Sfx.Play("kart_horn", 1f);
            Sfx.Play("car_engine", 0.7f, 1.4f);
            float speed = 14f;
            int index = laps.Count - 3;
            lastYaw = transform.eulerAngles.y;
            for (float t = 0f; t < 3.2f && !cine.Tick(); t += Time.deltaTime)
            {
                Vector3 target = laps[(index + 1) % laps.Count];
                Vector3 to = (target - transform.position).Flatten();
                if (to.magnitude < 1.5f) index = (index + 1) % laps.Count;
                transform.position += to.normalized * speed * Time.deltaTime;
                transform.position = Ground(transform.position);
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), Time.deltaTime * 6f);
                if (t > 2.6f) speed = Mathf.MoveTowards(speed, 0f, Time.deltaTime * 25f);
                KartFrame(Time.deltaTime, speed, true);
                gesture = t > 1.2f && t < 2.2f ? Gesture.Whee : Gesture.None;
                Pose p = KartPose(steer, Time.time);
                ApplyGesture(ref p, Time.time);
                PoseNow(p, 14f, Time.deltaTime);
                StepJiggles(Time.deltaTime);
                yield return null;
            }
            transform.position = home;
            transform.rotation = Quaternion.LookRotation((cine.PlayerPosition - home).Flatten().normalized);
            cine.Cut(home + transform.forward * 6f + Vector3.up * 2.2f, home + Vector3.up * 2.2f);
            Dust(home, 30);
            Sfx.Play("car_skid", 0.8f);
            // A skid-stop rock, then a big wave and two happy honks of the horn bulb.
            for (float t = 0f; t < 2.4f && !cine.Tick(); t += Time.deltaTime)
            {
                kartBounce = t < 0.4f ? Mathf.Sin(t / 0.4f * Mathf.PI) * 0.12f : 0f;
                gesture = t < 1.2f ? Gesture.Wave : Gesture.Honk;
                gestureStart = t < 1.2f ? 0f : 1.2f;
                if (Mathf.Abs(t - 1.35f) < Time.deltaTime || Mathf.Abs(t - 1.85f) < Time.deltaTime) Sfx.Play("kart_horn", 0.9f, 1.15f);
                Pose p = KartPose(0f, Time.time);
                ApplyGesture(ref p, t);
                PoseNow(p, 12f, Time.deltaTime);
                ApplyKart(0f);
                StepJiggles(Time.deltaTime);
                yield return null;
            }
            Say("BEEP BEEP!", Happy, 1.2f, 6f);
            gesture = Gesture.ThumbsUp;
            for (float t = 0f; t < 0.8f && !cine.Tick(); t += Time.deltaTime)
            {
                Pose p = KartPose(0f, Time.time);
                ApplyGesture(ref p, t);
                PoseNow(p, 12f, Time.deltaTime);
                StepJiggles(Time.deltaTime);
                yield return null;
            }
            gesture = Gesture.None;
            Destroy(light.gameObject, 6f);
        }

        private Vector3 Home0 => transform.parent != null ? transform.parent.position : transform.position;

        private void BuildLaps(Vector3 centre)
        {
            laps.Clear();
            for (int i = 0; i < 14; i++)
            {
                float a = i / 14f * Mathf.PI * 2f;
                Vector3 p = centre + new Vector3(Mathf.Cos(a) * 22f, 0f, Mathf.Sin(a) * 13f);
                laps.Add(NavMesh.SamplePosition(p, out NavMeshHit hit, 4f, NavMesh.AllAreas) ? hit.position : p);
            }
        }

        // =====================================================================================================
        // The fight
        // =====================================================================================================

        protected override void OnBossStarted()
        {
            if (AgentReady) Agent.updateRotation = false;
            BuildLaps(Home);
            Enter(State.Laps);
            nextSnack = Time.time + 12f;
            nextDonut = Time.time + 6f;
        }

        protected override void OnEngaged()
        {
            nextBump = Time.time + 2f;
            if (!kartBroken) Say("WHEEE!", Happy, 1.2f, 6f);
        }

        private void Enter(State next)
        {
            state = next;
            stateTime = 0f;
            gesture = Gesture.None;
            gestureUntil = 0f;
            if (ring != null && next != State.Donut && next != State.Pound) { ring.Finish(); ring = null; }
            if (next != State.Toss) DropHeld();
            if (next != State.Snack && banana != null) banana.gameObject.SetActive(false);
        }

        private void DropHeld()
        {
            for (int i = held.childCount - 1; i >= 0; i--)
            {
                Transform child = held.GetChild(i);
                if (child == banana || child == steering) continue;
                Destroy(child.gameObject);
            }
        }

        protected override void UpdatePatrol(float dt) => DriveLaps(dt);

        protected override void TickCombat(float dt)
        {
            stateTime += dt;
            UpdateShove(dt);
            if (!kartBroken) { KartPhase(dt); return; }
            float quick = Phase == 2 ? 0.75f : 1f;
            switch (state)
            {
                case State.Breaking: UpdateBreaking(); break;
                case State.Idle: Decide(dt, quick); break;
                case State.Swipe: UpdateSwipe(dt, quick); break;
                case State.Pound: UpdatePound(dt, quick); break;
                case State.Rush: UpdateRush(dt, quick); break;
                case State.Dazed:
                case State.Choke:
                case State.Slip:
                    Halt();
                    if (stateTime > (state == State.Slip ? 2.5f : state == State.Choke ? 1.5f : 2f)) Enter(State.Idle);
                    break;
                case State.Toss: UpdateToss(dt, quick); break;
                case State.Snack: UpdateSnack(dt); break;
                default: Enter(State.Idle); break; // a kart state with no kart: never sit back down
            }
        }

        private void Decide(float dt, float quick)
        {
            float distance = PlayerDistance;
            TurnToward(PlayerPosition, 200f * dt);
            if (Health.Normalized < 0.7f && Time.time >= nextSnack && distance > 7f)
            {
                Enter(State.Snack);
                eaten = 0f;
                interrupted = false;
                Say("*munch*", Happy, 1.2f, 5f);
                return;
            }
            if (distance < 3.6f) { Enter(State.Swipe); Say("!", Angry, 0.6f, 6f); return; }
            if (distance < 7.5f && Time.time >= nextPound)
            {
                Enter(State.Pound);
                poundLanded = false;
                ring = GroundRing.Spawn(transform.position + transform.forward * 1.2f, PoundRadius, new Color(1f, 0.55f, 0.25f, 0.5f));
                Say("HRRRNGH!", Angry, 0.9f, 6f);
                return;
            }
            if (distance > 6f && distance < 24f && Time.time >= nextRush && Random.value < 0.6f)
            {
                Enter(State.Rush);
                rushHit = false;
                return;
            }
            if (distance > 5f && Time.time >= nextToss) { Enter(State.Toss); return; }
            Chase(PlayerPosition, 4.2f);
            FaceMovement(dt);
        }

        // ---------- The kart ----------

        private void KartPhase(float dt)
        {
            switch (state)
            {
                case State.Rev: UpdateRev(dt); return;
                case State.Ram: UpdateRam(dt); return;
                case State.Donut: UpdateDonut(dt); return;
            }
            if (state != State.Laps) Enter(State.Laps);
            DriveLaps(dt);
            if (Engaged && Time.time >= nextDonut && PlayerDistance < 6.5f)
            {
                Enter(State.Donut);
                Halt();
                ring = GroundRing.Spawn(transform.position, DonutRadius, new Color(0.55f, 0.4f, 0.25f, 0.5f));
                Sfx.PlayAt("rev", transform.position, 1f, 1.2f, 40f);
                Say("WHEEE!", Happy, 1f, 6f);
                return;
            }
            if (Time.time >= nextBump && PlayerDistance < 16f)
            {
                Enter(State.Rev);
                Halt();
                Sfx.PlayAt("rev", transform.position, 1f, 0.9f, 40f);
                Say("VROOM VROOM!", Happy, 1f, 5f);
                return;
            }
            if (Time.time >= nextLapEmote)
            {
                nextLapEmote = Time.time + Random.Range(3f, 6f);
                switch (Random.Range(0, 5))
                {
                    case 0: StartGesture(Gesture.Whee, 1.2f); Say("WHEEE!", Happy, 1.1f, 4.5f); break;
                    case 1: StartGesture(Gesture.ThumbsUp, 1.2f); Say(":D", Happy, 1.1f, 4.5f); break;
                    case 2: StartGesture(Gesture.Vroom, 1.2f); Say("VROOM!", Happy, 1.1f, 4.5f); break;
                    case 3: StartGesture(Gesture.Wave, 1.4f); Say("*waves*", Happy, 1.1f, 4.5f); break;
                    default: StartGesture(Gesture.FistPump, 1.2f); Say("LAP " + (lap / laps.Count + 1) + "!", Happy, 1.1f, 4.5f); break;
                }
            }
        }

        private void StartGesture(Gesture g, float seconds)
        {
            gesture = g;
            gestureStart = Time.time;
            gestureUntil = Time.time + seconds;
        }

        private void DriveLaps(float dt)
        {
            if (kartBroken || laps.Count == 0) return;
            Vector3 target = laps[lap % laps.Count];
            if (Flat(target - transform.position) < 2.5f) lap++;
            Chase(laps[lap % laps.Count], 5.5f);
            FaceMovement(dt);
        }

        // Rev and ram: stops, revs hard (smoking back tyres, the kart shaking, both fists on the wheel), honks, then
        // charges nearly straight at where you are (a slight homing), 10 and a shove; a miss skids out.
        private void UpdateRev(float dt)
        {
            Halt();
            TurnToward(PlayerPosition, 220f * dt);
            if (Random.value < 0.5f) ParticleBurst.Spawn(ParticleBurst.Kind.Smoke, transform.TransformPoint(new Vector3(0f, 0.2f, -0.7f)), Vector3.up - transform.forward, 2, 0.5f, 1.5f, 40f, new Color(0.7f, 0.7f, 0.7f, 0.5f));
            if (stateTime < 0.8f) return;
            Enter(State.Ram);
            rushDirection = (PlayerPosition - transform.position).Flatten().normalized;
            if (rushDirection.sqrMagnitude < 0.01f) rushDirection = transform.forward;
            rushHit = false;
            Sfx.PlayAt("kart_horn", transform.position, 1f, 1f, 40f);
            Sfx.PlayAt("car_skid", transform.position, 0.6f, 1.4f, 30f);
            Dust(transform.TransformPoint(new Vector3(0f, 0f, -0.8f)), 25, 0.6f);
        }

        private void UpdateRam(float dt)
        {
            const float speed = 11f;
            Vector3 toPlayer = (PlayerPosition - transform.position).Flatten();
            if (!rushHit && toPlayer.sqrMagnitude > 1f)
                rushDirection = Vector3.RotateTowards(rushDirection, toPlayer.normalized, 35f * Mathf.Deg2Rad * dt, 0f);
            float slow = rushHit || stateTime > 1.5f ? Mathf.Clamp01(1f - (stateTime - (rushHit ? 0f : 1.5f)) * 2f) : 1f;
            if (AgentReady) Agent.Move(rushDirection * speed * slow * dt);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(rushDirection), 300f * dt);
            if (Random.value < 0.4f) Dust(transform.TransformPoint(new Vector3(0f, 0f, -0.8f)), 3, 0.4f);
            if (!rushHit && PlayerDistance < 2.2f)
            {
                rushHit = true;
                if (HurtPlayer(RamDamage)) ShovePlayer(PlayerPosition - transform.position, 3f);
                Sfx.PlayAt("kart_horn", transform.position, 1f, 1.3f, 35f);
                Sfx.PlayAt("crab_block", transform.position, 0.8f, 0.7f, 35f);
                Say("BONK!", Happy, 0.9f, 5f);
                kickVelocity -= 6f;
                stateTime = 1.5f; // straight into the skid
            }
            if (stateTime < 1.5f) return;
            if (stateTime < 1.52f && !rushHit) { Sfx.PlayAt("car_skid", transform.position, 0.8f, 1f, 35f); Say("WHOA!", Happy, 0.8f, 4.5f); }
            if (stateTime > 2f)
            {
                nextBump = Time.time + Random.Range(4f, 6f);
                Enter(State.Laps);
            }
        }

        // Donut: a ring on the ground, then he spins the kart round on the spot flinging mud; 8 and a shove inside it,
        // and it leaves a mud patch.
        // The kart stage is the happy, mostly harmless one (Christian, 11-09): its moves are comedy, barely a scratch.
        private const float DonutRadius = 4f, DonutDamage = 3f, RamDamage = 4f;

        private void UpdateDonut(float dt)
        {
            Halt();
            const float warn = 0.55f, spin = 1.4f;
            if (ring != null) ring.Progress = Mathf.Clamp01(stateTime / warn);
            if (stateTime < warn) return;
            float k = (stateTime - warn) / spin;
            transform.rotation *= Quaternion.Euler(0f, 600f * Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI) * dt, 0f);
            if (Random.value < 0.7f)
            {
                Vector3 back = transform.TransformPoint(new Vector3(0f, 0.15f, -0.9f));
                ParticleBurst.Spawn(ParticleBurst.Kind.Sand, back, -transform.forward + transform.right * 0.8f + Vector3.up * 0.4f, 6, 0.5f, 7f, 30f, new Color(0.3f, 0.22f, 0.14f, 0.9f));
            }
            if (k >= 0.5f && !poundLanded)
            {
                poundLanded = true; // the hit lands mid-spin
                if (ring != null) { ring.Finish(); ring = null; }
                HurtInCircle(transform.position, DonutRadius, DonutDamage, 2f);
                MudZone.Spawn(transform.position, DonutRadius * 0.8f, 6f);
                Sfx.PlayAt("mud_splat", transform.position, 1f, 0.7f, 40f);
                CameraShake.Add(0.15f);
            }
            if (k < 1f) return;
            poundLanded = false;
            nextDonut = Time.time + Random.Range(8f, 11f);
            nextBump = Mathf.Max(nextBump, Time.time + 2f);
            Enter(State.Laps);
        }

        private void FaceMovement(float dt)
        {
            if (!AgentReady) return;
            Vector3 v = Agent.velocity.Flatten();
            if (v.sqrMagnitude > 0.2f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(v), 360f * dt);
        }

        private void BreakKart()
        {
            if (kartBroken || IsDead) return;
            kartBroken = true;
            if (ring != null) { ring.Finish(); ring = null; }
            Enter(State.Breaking);
            Halt();
            gesture = Gesture.None;
            launchDirection = -transform.forward;
            CameraShake.Add(0.4f);
            Sfx.PlayAt("car_crash", transform.position, 1f, 1.2f, 50f);
            Sfx.PlayAt("kart_horn", transform.position, 1f, 0.7f, 50f);
        }

        // The kart's bits fly off (the original hides; it comes back if the fight resets). He keeps the steering wheel.
        private void ShatterKart()
        {
            steering.SetParent(held, true);
            foreach (MeshRenderer piece in kart.GetComponentsInChildren<MeshRenderer>())
            {
                GameObject copy = Instantiate(piece.gameObject, piece.transform.position, piece.transform.rotation);
                copy.transform.localScale = piece.transform.lossyScale;
                var rb = copy.AddComponent<Rigidbody>();
                copy.AddComponent<BoxCollider>();
                rb.linearVelocity = (Random.insideUnitSphere + Vector3.up * 1.5f) * 6f;
                rb.angularVelocity = Random.insideUnitSphere * 12f;
                copy.AddComponent<Debris>().Begin(5f);
            }
            kart.gameObject.SetActive(false);
            ParticleBurst.Spawn(ParticleBurst.Kind.Smoke, transform.position + Vector3.up * 0.5f, Vector3.up, 30, 1f, 4f, 80f, new Color(0.35f, 0.33f, 0.3f, 0.8f));
            ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, transform.position + Vector3.up * 0.5f, Vector3.up, 25, 0.1f, 7f, 80f, new Color(1f, 0.8f, 0.4f));
            Sfx.PlayAt("wood_smash", transform.position, 1f, 0.8f, 40f);
        }

        // Spin-out, thrown clear onto his backside with the steering wheel in his hands, a long stare, up, chest-beat,
        // "MY GO-KART!!!", and both fists into the ground.
        private const float SpinOut = 0.6f, Landed = 1.4f, StandUp = 2.6f, Roar = 3.2f, Slam = 4.5f, BreakEnd = 5f;

        private void UpdateBreaking()
        {
            float t = stateTime, dt = Time.deltaTime;
            if (t < SpinOut)
            {
                transform.rotation *= Quaternion.Euler(0f, 720f * (1f - t / SpinOut) * dt, 0f);
                if (Random.value < 0.6f) Dust(transform.position, 4, 0.5f);
                return;
            }
            if (t - dt < SpinOut) ShatterKart();
            if (t < Landed)
            {
                if (AgentReady) Agent.Move(launchDirection * 3.2f * dt);
                return;
            }
            if (t - dt < Landed)
            {
                Thump(transform.position, 1.3f);
                Face?.Show(Mood.Shock, 1.2f);
                Say("...", Happy, 1f, 6f);
            }
            if (t > StandUp && t - dt <= StandUp)
            {
                DropSteeringWheel();
                Sfx.PlayAt("crab_whimper", transform.position, 0.7f, 0.5f, 40f);
            }
            if (t > Roar && t - dt <= Roar)
            {
                MudHud.Shout("MY GO-KART!!!", new Color(1f, 0.85f, 0.3f), 2.4f);
                Sfx.PlayAt("kong_roar", transform.position, 1f, 0.9f, 80f);
                CameraShake.Add(0.6f);
                Say("MY GO-KART!!!", Angry, 2.4f, 7f);
            }
            if (t > Roar && t < Slam - 0.3f && Mathf.Repeat(t, 0.14f) < dt) Sfx.PlayAt("player_land", transform.position, 0.6f, 0.55f, 30f); // chest thumps
            if (t > Slam && t - dt <= Slam) GroundSlam(3f, 0f, 0f, 0.5f);
            if (t > BreakEnd)
            {
                Enter(State.Idle);
                nextRush = Time.time + 1.5f;
                nextToss = Time.time + 0.5f;
                nextPound = Time.time + 6f;
            }
        }

        private void DropSteeringWheel()
        {
            if (steering.parent != held) return;
            steering.SetParent(null, true);
            var rb = steering.gameObject.AddComponent<Rigidbody>();
            steering.gameObject.AddComponent<SphereCollider>().radius = WheelRadius;
            rb.linearVelocity = transform.forward * 1.5f + Vector3.up * 1f;
            rb.angularVelocity = Random.insideUnitSphere * 4f;
        }

        private void RestoreSteeringWheel()
        {
            if (steering.TryGetComponent(out Rigidbody rb)) Destroy(rb);
            if (steering.TryGetComponent(out SphereCollider sc)) Destroy(sc);
            steering.SetParent(kart, false);
            steering.localPosition = WheelAt;
            steering.localRotation = WheelTilt;
            steering.localScale = Vector3.one;
        }

        // ---------- Attacks ----------

        // The swipe: rears back with the right arm cocked behind him, then a huge backhand across, 20 and a shove.
        private void UpdateSwipe(float dt, float quick)
        {
            Halt();
            if (stateTime < 0.45f * quick) TurnToward(PlayerPosition, 300f * dt);
            if (stateTime >= 0.6f * quick && stateTime - dt < 0.6f * quick)
            {
                Sfx.PlayAt("chain_swish", transform.position, 1f, 0.6f, 35f);
                CameraShake.Add(0.15f);
                Dust(transform.position + transform.forward * 1.5f, 15, 0.6f);
                if (PlayerDistance < 3.8f && AngleToTarget() < 75f && HurtPlayer(20f)) ShovePlayer(PlayerPosition - transform.position + transform.right * -2f, 5f);
            }
            if (stateTime > 1.15f * quick) Enter(State.Idle);
        }

        // The ground pound: rears up on his legs, both fists overhead, a little hop, then slams them down. A shockwave
        // ring: 18 to anyone on the ground inside it (jump it).
        private const float PoundRadius = 5f;

        private void UpdatePound(float dt, float quick)
        {
            Halt();
            float windup = 0.85f * quick;
            if (stateTime < windup * 0.6f) TurnToward(PlayerPosition, 240f * dt);
            if (ring != null)
            {
                ring.MoveTo(Ground(transform.position + transform.forward * 1.2f));
                ring.Progress = Mathf.Clamp01(stateTime / windup);
            }
            if (stateTime >= windup && !poundLanded)
            {
                poundLanded = true;
                if (ring != null) { ring.Finish(); ring = null; }
                GroundSlam(PoundRadius, 18f, 7f, 1f);
            }
            if (stateTime > windup + 0.75f)
            {
                nextPound = Time.time + (Phase == 2 ? 6f : 8f);
                Enter(State.Idle);
            }
        }

        private void GroundSlam(float radius, float damage, float shove, float strength)
        {
            Vector3 at = Ground(transform.position + transform.forward * 1.2f);
            CameraShake.Add(0.35f * strength);
            Sfx.PlayAt("laser_slam", at, 1f, 0.55f, 50f);
            Sfx.PlayAt("stone_rise", at, 0.7f, 0.7f, 40f);
            for (int i = 0; i < 16; i++)
            {
                float a = i / 16f * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                ParticleBurst.Spawn(ParticleBurst.Kind.Sand, at + dir * radius * 0.5f, dir + Vector3.up * 0.3f, 5, 0.7f * strength, 6f, 25f, new Color(0.38f, 0.3f, 0.21f, 0.85f));
            }
            Dust(at, 40, 1.1f * strength);
            if (damage <= 0f || !TargetAlive) return;
            bool airborne = TargetBody != null && !TargetBody.isGrounded;
            if (airborne) { MudHud.Toast("Jumped it!", Happy, 1.2f); return; }
            HurtInCircle(at, radius, damage, shove);
        }

        // The charge: stands tall and beats his chest, then gallops at you in a straight line on all fours.
        private void UpdateRush(float dt, float quick)
        {
            float windup = 1f * quick;
            if (stateTime < windup)
            {
                Halt();
                if (stateTime < windup * 0.75f) TurnToward(PlayerPosition, 400f * dt);
                rushDirection = transform.forward;
                if (stateTime < dt) Say("HOO HOO HOO!", Angry, windup, 5.5f);
                if (Mathf.Repeat(stateTime, 0.14f) < dt) Sfx.PlayAt("player_land", transform.position, 0.6f, 0.6f, 30f);
                return;
            }
            float run = stateTime - windup;
            if (run - dt <= 0f) Sfx.PlayAt("kong_roar", transform.position, 0.6f, 1.3f, 50f);
            if (AgentReady) Agent.Move(rushDirection * 16f * dt);
            transform.rotation = Quaternion.LookRotation(rushDirection);
            if (!rushHit && PlayerDistance < 2.4f)
            {
                rushHit = true;
                if (HurtPlayer(28f)) ShovePlayer(rushDirection, 7f);
            }
            if (BananaPeel.SlipsAt(transform.position))
            {
                Enter(State.Slip);
                Say("WHOOPS!", Happy, 1.6f, 6f);
                MudHud.Toast("He slipped on his own peel!", Happy);
                CameraShake.Add(0.3f);
                Dust(transform.position, 40, 1f);
                return;
            }
            if (Physics.Raycast(transform.position + Vector3.up * 1.2f, rushDirection, 1.8f, sightMask, QueryTriggerInteraction.Ignore))
            {
                Enter(State.Dazed);
                Say("@_@", new Color(1f, 1f, 0.6f), 2f, 6f);
                CameraShake.Add(0.35f);
                Sfx.PlayAt("car_crash", transform.position, 0.8f, 0.7f, 40f);
                kickVelocity -= 10f;
                nextRush = Time.time + 6f;
                return;
            }
            if (run > 1.3f) { Enter(State.Idle); nextRush = Time.time + 5f; }
        }

        // The barrel toss: reaches behind his back and pulls out a barrel (don't ask), hoists it overhead, hurls it.
        private const float TossReach = 0.35f, TossLift = 0.8f;

        private void UpdateToss(float dt, float quick)
        {
            Halt();
            TurnToward(PlayerPosition, 300f * dt);
            if (held.childCount == CountKept() && stateTime >= TossReach * quick)
            {
                MudArt.Part(held, "Barrel", Vector3.zero, Quaternion.identity, "kong_barrel", RollingBarrel.Mesh(false));
                Sfx.PlayAt("crab_block", transform.position, 0.6f, 0.6f, 30f);
            }
            if (stateTime < TossLift * quick || state != State.Toss) return;
            if (stateTime - dt < TossLift * quick)
            {
                DropHeld();
                int count = Phase == 2 ? 2 : 1;
                for (int i = 0; i < count; i++)
                {
                    bool red = Random.value < 0.25f;
                    Vector3 side = transform.right * (count == 1 ? 0f : i == 0 ? -2.5f : 2.5f);
                    Vector3 land = Vector3.Lerp(transform.position, PlayerPosition, 0.45f) + side;
                    Transform model = MudArt.Part(null, "Barrel", Vector3.zero, Quaternion.Euler(0f, 0f, 90f), red ? "kong_barrel_red" : "kong_barrel", RollingBarrel.Mesh(red));
                    Vector3 aim = PlayerPosition + side * 0.5f;
                    GameObject owner = gameObject;
                    Lob.Throw(model, held.position, Ground(land), 0.6f, 2f, at => RollingBarrel.Spawn(at, (aim - at).Flatten().normalized, red, owner), 1.2f);
                }
                Sfx.PlayAt("chain_swish", transform.position, 0.9f, 0.5f, 35f);
                Say(Random.value < 0.5f ? "CATCH!" : "HNNGH!", Angry, 0.8f, 5f);
                nextToss = Time.time + (Phase == 2 ? 2.6f : 3.6f);
            }
            if (stateTime > (TossLift + 0.45f) * quick) Enter(State.Idle);
        }

        private int CountKept()
        {
            int kept = 0;
            for (int i = 0; i < held.childCount; i++)
            {
                Transform child = held.GetChild(i);
                if (child == banana || child == steering) kept++;
            }
            return kept;
        }

        // The snack: backs off (on his knuckles), plops down on his backside, pulls out a banana, peels it in three
        // strips, munches. Heals 12% unless a heavy hit makes him choke. Drops the peel.
        private const float BackOff = 0.9f, Plop = 0.35f, EatTime = 2.8f;

        private void UpdateSnack(float dt)
        {
            if (stateTime < BackOff)
            {
                Vector3 away = (transform.position - PlayerPosition).Flatten().normalized;
                Chase(transform.position + away * 3f, 3.2f);
                TurnToward(PlayerPosition, 200f * dt);
                return;
            }
            Halt();
            if (stateTime - dt < BackOff + Plop && stateTime >= BackOff + Plop)
            {
                Thump(transform.position, 0.6f);
                banana.gameObject.SetActive(true);
            }
            if (stateTime < BackOff + Plop) return;
            eaten = Mathf.Clamp01((stateTime - BackOff - Plop) / EatTime);
            MudHud.WorldBar(transform, eaten, Banana, 4.4f);
            if (eaten > 0.3f && Mathf.Repeat(stateTime, 0.45f) < dt) Sfx.PlayAt("crab_snip", transform.position, 0.5f, 0.6f, 25f);
            if (eaten < 1f) return;
            Health.Heal(Health.Max * 0.12f);
            Say("*BURP*", new Color(0.8f, 1f, 0.5f), 1.4f, 6f);
            Sfx.PlayAt("flask_burp", transform.position, 1f, 0.5f, 40f);
            BananaPeel.Drop(transform.position + transform.forward * 1.4f);
            nextSnack = Time.time + 18f;
            Enter(State.Idle);
        }

        private void InterruptSnack()
        {
            if (state != State.Snack || stateTime < BackOff) return;
            Enter(State.Choke);
            nextSnack = Time.time + 10f;
            Say("HKK!", new Color(1f, 0.6f, 0.4f), 1.4f, 6f);
            MudHud.Toast("Snack interrupted!", new Color(1f, 0.85f, 0.4f));
        }

        private void Thump(Vector3 at, float strength)
        {
            Sfx.PlayAt("player_land", at, Mathf.Min(1f, 0.7f * strength), 0.55f, 40f);
            Dust(at, Mathf.RoundToInt(20f * strength), 0.8f);
            CameraShake.Add(0.15f * strength);
        }

        // ---------- Damage ----------

        protected override void OnBossHit(float amount, GameObject source, Vector3 away)
        {
            kickVelocity += Mathf.Clamp(amount / 25f, 0.2f, 1.2f) * 6f;
            bool heavy = IsHeavyRanged(amount, source) || IsFinisher(source);
            if (!kartBroken)
            {
                if (heavy) { BreakKart(); return; }
                if (!IsDead) Health.Heal(amount * 0.75f); // the kart takes most of it
                Say("*clang*", new Color(0.85f, 0.85f, 0.85f), 0.5f, 4f);
                return;
            }
            if (heavy) InterruptSnack();
        }

        public override float OnCarBlast(CarBlastInfo blast)
        {
            if (!kartBroken) BreakKart();
            else if (state == State.Snack) InterruptSnack();
            else if (state != State.Breaking) { Enter(State.Dazed); Say("@_@", new Color(1f, 1f, 0.6f), 2f, 6f); }
            return blast.Damage;
        }

        protected override void OnPhaseTwo()
        {
            roared = false;
            // Furious always means on foot: if he's somehow still driving at half health, the kart gives out now.
            if (!kartBroken) { BreakKart(); return; }
            if (state == State.Snack) Enter(State.Idle);
            Say("GRAAAH!", new Color(1f, 0.4f, 0.3f), 1.6f, 7f);
            Sfx.PlayAt("kong_roar", transform.position, 1f, 0.8f, 60f);
        }

        // Half health: standing tall, roaring and beating his chest, then both fists into the ground (no damage).
        protected override void UpdatePhaseShift(float dt)
        {
            Halt();
            if (!kartBroken || state == State.Breaking) { if (state == State.Breaking) { stateTime += dt; UpdateBreaking(); } return; }
            if (!roared && PhaseShiftElapsed > PhaseShiftTime - 0.3f)
            {
                roared = true;
                GroundSlam(3.5f, 0f, 0f, 0.8f);
            }
            if (Mathf.Repeat(PhaseShiftElapsed, 0.14f) < dt && PhaseShiftElapsed < PhaseShiftTime - 0.4f)
                Sfx.PlayAt("player_land", transform.position, 0.6f, 0.6f, 30f);
        }

        protected override void OnPhaseShiftEnded()
        {
            if (kartBroken && (state == State.Snack || state == State.Laps)) Enter(State.Idle);
        }

        protected override void OnReset()
        {
            kartBroken = false;
            kart.gameObject.SetActive(true);
            RestoreSteeringWheel();
            DropHeld();
            banana.gameObject.SetActive(false);
            if (ring != null) { ring.Finish(); ring = null; }
            lap = 0;
            gesture = Gesture.None;
            Visual.localRotation = VisualRestRotation;
            Visual.localPosition = VisualRest;
            posed = false;
            Enter(State.Laps);
            BananaPeel.ClearAll();
        }

        protected override void OnMudBossDeath()
        {
            Sfx.PlayAt("kart_horn", transform.position, 1f, 0.5f, 50f);
            if (ring != null) { ring.Finish(); ring = null; }
            DropHeld();
            banana.gameObject.SetActive(false);
            stars.gameObject.SetActive(false);
            BananaPeel.ClearAll();
            Face?.Show(Mood.Hurt, 2f);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (steering != null && steering.parent == null) Destroy(steering.gameObject);
            if (ring != null) ring.Finish();
        }

        // =====================================================================================================
        // Animation: each frame a target pose is built for what he's doing, the current pose eases toward it, and the
        // rig is placed from it (two-bone IK for arms and legs). The kart steers, bounces and smokes; the scarf tail and
        // flag swing on springs.
        // =====================================================================================================

        private Vector3 lastPosition, velocity;
        private bool tracked;

        protected override void AnimateBoss(float dt)
        {
            float t = Time.time;
            Track(dt);
            kickVelocity += (-kick * 150f - kickVelocity * 12f) * dt;
            kick += kickVelocity * dt;
            if (kart.gameObject.activeSelf) KartFrame(dt, Speed, true);
            if (gesture != Gesture.None && Time.time > gestureUntil && gestureUntil > 0f) gesture = Gesture.None;

            Pose target;
            float rate = 12f, look = 1f;
            Vector3 lift = Vector3.zero;
            Mood? mood = null;
            switch (state)
            {
                case State.Laps:
                    target = KartPose(steer, t);
                    ApplyGesture(ref target, Time.time - gestureStart);
                    look = Engaged ? 0.7f : 0.3f;
                    mood = Mood.Laugh;
                    break;
                case State.Rev:
                {
                    target = KartPose(0f, t);
                    target.Spine = Quaternion.Euler(34f, 0f, 0f) * Quaternion.Euler(Mathf.Sin(t * 40f) * 2f, 0f, 0f);
                    target.Head = Quaternion.Euler(-34f, 0f, 0f);
                    target.Hip += new Vector3(Mathf.Sin(t * 47f) * 0.015f, Mathf.Sin(t * 53f) * 0.02f, 0f);
                    mood = Mood.Smug;
                    break;
                }
                case State.Ram:
                    target = KartPose(steer, t);
                    target.Spine = Quaternion.Euler(30f, 0f, 0f);
                    target.Head = Quaternion.Euler(-30f, 0f, 0f);
                    if (rushHit || stateTime > 1.5f) { gesture = Gesture.FistPump; gestureStart = Time.time - stateTime; ApplyGesture(ref target, stateTime); }
                    mood = Mood.Laugh;
                    break;
                case State.Donut:
                    target = KartPose(stateTime > 0.55f ? 1f : 0f, t);
                    gesture = Gesture.Whee;
                    ApplyGesture(ref target, stateTime);
                    look = 0.3f;
                    mood = Mood.Laugh;
                    break;
                case State.Breaking: target = BreakingPose(t, ref rate, ref look, ref lift, ref mood); break;
                case State.Idle: target = GroundPose(dt, t, false); rate = 14f; break;
                case State.Swipe: target = SwipePose(t); rate = 22f; mood = Mood.Furious; break;
                case State.Pound: target = PoundPose(t, ref rate); mood = Mood.Furious; break;
                case State.Rush:
                    float windup = Phase == 2 ? 0.75f : 1f;
                    if (stateTime < windup) { target = ChestBeat(t, 3.5f); rate = 20f; look = 0.6f; }
                    else { target = GroundPose(dt, t, true); rate = 16f; }
                    mood = Mood.Furious;
                    break;
                case State.Dazed:
                    target = DazedPose(t);
                    rate = 8f;
                    look = 0f;
                    mood = Mood.Sleepy;
                    break;
                case State.Choke: target = ChokePose(t); rate = 14f; look = 0.2f; mood = Mood.Hurt; break;
                case State.Slip: target = SlipPose(t, ref rate); look = 0f; mood = Mood.Shock; break;
                case State.Toss: target = TossPose(t); rate = 16f; mood = Mood.Angry; break;
                case State.Snack: target = SnackPose(dt, t, ref rate, ref look); mood = Mood.Laugh; break;
                default: target = GroundPose(dt, t, false); break;
            }
            if (InPhaseShift && kartBroken && state != State.Breaking)
            {
                float s = PhaseShiftElapsed;
                target = s < PhaseShiftTime - 0.3f ? ChestBeat(t, 4f) : PoundSlam();
                rate = 20f;
                mood = Mood.Furious;
            }
            if (mood.HasValue && Face != null) Face.Show(mood.Value, 0.15f);
            if (state == State.Snack && stateTime > BackOff + Plop && eaten > 0.3f && Face != null) Face.Talk(0.1f); // munching
            target.Spine = Quaternion.Euler(-kick * 10f, 0f, 0f) * target.Spine;
            Visual.localPosition = Vector3.Lerp(Visual.localPosition, VisualRest + lift, 1f - Mathf.Exp(-18f * dt));
            if (state != State.Breaking || stateTime > Landed)
                Visual.localRotation = Quaternion.Slerp(Visual.localRotation, VisualRestRotation, 1f - Mathf.Exp(-14f * dt));
            lookWeight = Mathf.MoveTowards(lookWeight, Target != null && Engaged ? look : 0f, dt * 3f);
            PoseNow(target, rate, dt);
            PlaceHeld();
            StepJiggles(dt);
            bool dizzy = state == State.Dazed || (state == State.Slip && stateTime > 0.4f && stateTime < 2f);
            if (stars.gameObject.activeSelf != dizzy) stars.gameObject.SetActive(dizzy);
            if (dizzy) stars.localRotation = Quaternion.Euler(0f, t * 200f, 0f);
        }

        private float Speed => velocity.Flatten().magnitude;

        private void Track(float dt)
        {
            if (dt <= 0f) return;
            Vector3 v = tracked ? (transform.position - lastPosition) / dt : Vector3.zero;
            if (v.sqrMagnitude > 900f) v = Vector3.zero; // a teleport
            velocity = Vector3.Lerp(velocity, v, 1f - Mathf.Exp(-10f * dt));
            lastPosition = transform.position;
            tracked = true;
        }

        private void StepJiggles(float dt)
        {
            dt = Mathf.Min(dt, 1f / 30f);
            foreach (Jiggle jiggle in jiggles) jiggle.Step(dt);
        }

        // ---------- The kart ----------

        // Steering from how fast he's turning, spinning wheels, the steering wheel, a bounce, exhaust puffs.
        private void KartFrame(float dt, float speed, bool spin)
        {
            if (dt <= 0f) return;
            float yaw = transform.eulerAngles.y;
            float rate = Mathf.DeltaAngle(lastYaw, yaw) / dt;
            lastYaw = yaw;
            steer = Mathf.Lerp(steer, Mathf.Clamp(rate / 80f, -1f, 1f), 1f - Mathf.Exp(-6f * dt));
            if (spin)
                for (int i = 0; i < 4; i++)
                    if (wheelSpin[i] != null) wheelSpin[i].Rotate(speed / (i < 2 ? 0.21f : 0.25f) * Mathf.Rad2Deg * dt, 0f, 0f, Space.Self);
            if (state == State.Rev && spin)
                for (int i = 2; i < 4; i++) wheelSpin[i].Rotate(2000f * dt, 0f, 0f, Space.Self); // burning rubber
            float rumble = state == State.Rev ? 0.025f : Mathf.Clamp01(speed / 8f) * 0.012f;
            kartBounce = Mathf.MoveTowards(kartBounce, 0f, dt * 0.5f);
            kart.localPosition = new Vector3(0f, kartBounce + Mathf.Sin(Time.time * 37f) * rumble, 0f);
            kart.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 29f) * rumble * 40f, 0f, steer * 4f);
            ApplyKart(steer);
            if (Time.time >= nextExhaust && (speed > 0.5f || state == State.Rev))
            {
                nextExhaust = Time.time + (state == State.Rev ? 0.05f : 0.18f);
                ParticleBurst.Spawn(ParticleBurst.Kind.Smoke, kart.TransformPoint(Exhaust), Vector3.up - transform.forward, state == State.Rev ? 3 : 1, 0.25f, 1.2f, 30f, new Color(0.5f, 0.5f, 0.5f, 0.5f));
            }
        }

        private void ApplyKart(float steerValue)
        {
            for (int i = 0; i < 2; i++) kartWheels[i].localRotation = Quaternion.Euler(0f, steerValue * 26f, 0f);
            if (steering.parent == kart) steering.localRotation = WheelTilt * Quaternion.Euler(0f, 0f, -steerValue * 100f);
        }

        // Squashed into the kart: knees up round his ears, both fists on the little wheel, leaning into the turns.
        private Pose KartPose(float steerValue, float t)
        {
            Quaternion wheel = WheelTilt * Quaternion.Euler(0f, 0f, -steerValue * 100f);
            Vector3 centre = WheelAt + kart.localPosition;
            Vector3 rimL = centre + wheel * new Vector3(-WheelRadius, 0f, 0f), rimR = centre + wheel * new Vector3(WheelRadius, 0f, 0f);
            float bob = Mathf.Sin(t * 9f) * Mathf.Clamp01(Speed / 6f) * 0.015f;
            var p = new Pose
            {
                Hip = new Vector3(0f, 0.92f + bob, -0.42f) + kart.localPosition,
                HipRot = Quaternion.Euler(-8f, 0f, -steerValue * 7f),
                Spine = Quaternion.Euler(16f, -steerValue * 10f, steerValue * 6f),
                Head = Quaternion.Euler(-14f, steerValue * 18f, -steerValue * 6f),
                HandL = rimL + new Vector3(-0.05f, 0.2f, -0.1f),
                HandR = rimR + new Vector3(0.05f, 0.2f, -0.1f),
                HandLRot = Bone((centre - rimL) * 0.5f + Vector3.down, Vector3.forward),
                HandRRot = Bone((centre - rimR) * 0.5f + Vector3.down, Vector3.forward),
                FootL = new Vector3(-0.32f, 0.3f, 0.22f),
                FootR = new Vector3(0.32f, 0.3f, 0.22f),
                FootLRot = Quaternion.Euler(-25f, -12f, 0f),
                FootRRot = Quaternion.Euler(-25f, 12f, 0f),
            };
            return p;
        }

        // Showing off while he drives: a wave, a thumbs-up, a fist pump, a honk of the horn bulb, a hunch, "WHEEE!".
        private void ApplyGesture(ref Pose p, float s)
        {
            switch (gesture)
            {
                case Gesture.Wave:
                    p.HandR = new Vector3(1.0f + Mathf.Sin(s * 12f) * 0.2f, 3.0f, 0.35f);
                    p.HandRRot = Bone(Vector3.up, Vector3.forward);
                    p.Head = Quaternion.Euler(-14f, 20f, 8f);
                    break;
                case Gesture.ThumbsUp:
                    p.HandR = new Vector3(0.8f, 2.3f, 1.0f);
                    p.HandRRot = Bone(Vector3.forward, Vector3.left);
                    p.Head = Quaternion.Euler(-10f, 10f, -8f);
                    break;
                case Gesture.FistPump:
                    p.HandR = new Vector3(0.65f, 2.95f + Mathf.Abs(Mathf.Sin(s * 11f)) * 0.5f, 0.25f);
                    p.HandRRot = Bone(Vector3.up, Vector3.back);
                    p.Head = Quaternion.Euler(-25f, 0f, 0f);
                    break;
                case Gesture.Honk:
                    p.HandR = HornBulb + kart.localPosition + new Vector3(0.05f, 0.28f - Mathf.Abs(Mathf.Sin(s * 8f)) * 0.06f, -0.08f);
                    p.HandRRot = Bone(Vector3.down + Vector3.forward * 0.3f, Vector3.left);
                    p.Head = Quaternion.Euler(0f, 25f, 0f);
                    break;
                case Gesture.Vroom:
                    p.Spine = Quaternion.Euler(34f, 0f, 0f);
                    p.Head = Quaternion.Euler(-32f, 0f, 0f);
                    break;
                case Gesture.Whee:
                    p.HandR = new Vector3(0.6f + Mathf.Sin(s * 9f) * 0.15f, 3.35f, 0.1f);
                    p.HandRRot = Bone(Vector3.up, Vector3.forward);
                    p.Head = Quaternion.Euler(-28f, 0f, 0f);
                    p.Spine = Quaternion.Euler(4f, 0f, 0f);
                    break;
            }
        }

        // ---------- On foot ----------

        // On his knuckles: a big forward lean, arms straight down to the ground, legs bent.
        private static Pose KnuckleBase(float t)
        {
            float breathe = Mathf.Sin(t * 1.6f);
            return new Pose
            {
                Hip = new Vector3(0f, 1.05f + breathe * 0.01f, -0.1f),
                HipRot = Quaternion.Euler(15f, 0f, 0f),
                Spine = Quaternion.Euler(25f + breathe * 1.5f, 0f, 0f),
                Head = Quaternion.Euler(-35f, 0f, 0f),
                HandL = new Vector3(-0.92f, WristToKnuckle, 1.15f),
                HandR = new Vector3(0.92f, WristToKnuckle, 1.15f),
                HandLRot = Knuckle(-1f),
                HandRRot = Knuckle(1f),
                FootL = new Vector3(-0.38f, Ankle, -0.2f),
                FootR = new Vector3(0.38f, Ankle, -0.2f),
                FootLRot = Quaternion.Euler(0f, -14f, 0f),
                FootRRot = Quaternion.Euler(0f, 14f, 0f),
            };
        }

        private static Quaternion Knuckle(float side) => Bone(Vector3.down + Vector3.forward * 0.1f, Vector3.back + Vector3.right * side * 0.4f);

        // The knuckle-walk (diagonal pairs: left hand, right foot, right hand, left foot) or, charging, a gallop (both
        // hands, then both feet). Hands and feet plant, the shoulders roll and the head bobs.
        private static readonly float[] WalkOffsets = { 0f, 0.5f, 0.75f, 0.25f }, GallopOffsets = { 0f, 0.08f, 0.5f, 0.58f };

        private Pose GroundPose(float dt, float t, bool gallop)
        {
            Vector3 local = transform.InverseTransformDirection(velocity);
            float forward = local.z, speed = Mathf.Abs(forward) + Mathf.Abs(local.x) * 0.5f;
            float cycle = gallop ? 3.2f : 1.7f;
            gaitPhase = Mathf.Repeat(gaitPhase + Mathf.Sign(forward == 0f ? 1f : forward) * speed / cycle * dt, 1f);
            gaitWeight = Mathf.MoveTowards(gaitWeight, Mathf.Clamp01(speed / 1.2f), dt * 4f);
            float w = gaitWeight;
            Pose p = KnuckleBase(t);
            if (gallop)
            {
                p.Hip = new Vector3(0f, 1.1f, -0.25f);
                p.HipRot = Quaternion.Euler(25f, 0f, 0f);
                p.Spine = Quaternion.Euler(28f, 0f, 0f);
                p.HandL.z = p.HandR.z = 1.45f;
            }
            float[] offsets = gallop ? GallopOffsets : WalkOffsets;
            float handStride = gallop ? 1.5f : 0.75f, footStride = gallop ? 1.3f : 0.6f;
            float handLift = gallop ? 0.45f : 0.22f, footLift = gallop ? 0.3f : 0.14f;
            for (int i = 0; i < 4; i++)
            {
                float ph = Mathf.Repeat(gaitPhase + offsets[i], 1f);
                bool hand = i < 2;
                Vector2 step = Step(ph, hand ? handStride : footStride, hand ? handLift : footLift);
                Vector3 add = new Vector3(0f, step.y, step.x) * w;
                switch (i)
                {
                    case 0: p.HandL += add; break;
                    case 1: p.HandR += add; break;
                    case 2: p.FootL += add; break;
                    default: p.FootR += add; break;
                }
                // A plant: a thud (heavy ones for the hands) and a puff of dust.
                if (w > 0.4f && lastLimbPhase[i] < SwingPart && ph >= SwingPart && speed > 0.5f)
                {
                    Vector3 at = Visual.TransformPoint(i == 0 ? p.HandL : i == 1 ? p.HandR : i == 2 ? p.FootL : p.FootR);
                    at.y = transform.position.y;
                    if (hand || gallop) Sfx.PlayAt("footstep_dirt_" + Random.Range(1, 5), at, hand ? 0.7f : 0.45f, hand ? 0.55f : 0.65f, 25f);
                    if (hand) ParticleBurst.Spawn(ParticleBurst.Kind.Sand, at, Vector3.up, gallop ? 8 : 4, 0.35f, 2f, 70f, new Color(0.38f, 0.3f, 0.21f, 0.7f));
                    if (gallop && hand) CameraShake.Add(0.04f);
                }
                lastLimbPhase[i] = ph;
            }
            float c = Mathf.Cos(gaitPhase * Mathf.PI * 2f), s2 = Mathf.Abs(Mathf.Sin(gaitPhase * Mathf.PI * 4f));
            if (gallop)
            {
                float rock = Mathf.Sin(gaitPhase * Mathf.PI * 2f);
                p.Hip += new Vector3(0f, 0.12f * rock * w, 0f);
                p.Spine = Quaternion.Euler(28f + 10f * rock * w, 0f, 0f);
                p.Head = Quaternion.Euler(-42f - 8f * rock * w, 0f, 0f);
            }
            else
            {
                p.Hip += new Vector3(0.04f * c * w, -0.05f * s2 * w, 0f);
                p.HipRot = Quaternion.Euler(15f, 6f * c * w, 4f * c * w);
                p.Spine = Quaternion.Euler(25f + 3f * s2 * w, -10f * c * w, -5f * c * w);
                p.Head = Quaternion.Euler(-35f + 4f * s2 * w, 8f * c * w, 3f * c * w);
            }
            return p;
        }

        private const float SwingPart = 0.38f;

        // One limb's stride: (forward offset, lift). Swing forward through the first part of the cycle, then plant
        // and slide back under the body.
        private static Vector2 Step(float p, float stride, float lift)
        {
            if (p < SwingPart)
            {
                float k = p / SwingPart;
                return new Vector2(Mathf.Lerp(-stride * 0.5f, stride * 0.5f, Mathf.SmoothStep(0f, 1f, k)), Mathf.Sin(k * Mathf.PI) * lift);
            }
            return new Vector2(Mathf.Lerp(stride * 0.5f, -stride * 0.5f, (p - SwingPart) / (1f - SwingPart)), 0f);
        }

        // Standing up tall, long arms hanging past his knees.
        private static Pose StandPose(float t)
        {
            float breathe = Mathf.Sin(t * 1.6f);
            return new Pose
            {
                Hip = new Vector3(0f, 1.1f, 0f),
                HipRot = Quaternion.identity,
                Spine = Quaternion.Euler(-2f + breathe, 0f, 0f),
                Head = Quaternion.Euler(-6f, 0f, 0f),
                HandL = new Vector3(-1.18f, 1.0f, 0.3f),
                HandR = new Vector3(1.18f, 1.0f, 0.3f),
                HandLRot = Bone(Vector3.down, Vector3.right),
                HandRRot = Bone(Vector3.down, Vector3.left),
                FootL = new Vector3(-0.44f, Ankle, 0.05f),
                FootR = new Vector3(0.44f, Ankle, 0.05f),
                FootLRot = Quaternion.Euler(0f, -18f, 0f),
                FootRRot = Quaternion.Euler(0f, 18f, 0f),
            };
        }

        // Up tall, head back, fists drumming on his chest.
        private Pose ChestBeat(float t, float rate)
        {
            Pose p = StandPose(t);
            p.Spine = Quaternion.Euler(-10f, 0f, 0f);
            p.Head = Quaternion.Euler(-28f, 0f, 0f);
            p.Hip.y = 1.12f;
            Vector3 chest = SpinePoint(p, new Vector3(0f, 0.85f, 0.75f));
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                float pull = 0.5f + 0.5f * Mathf.Sin(t * rate * Mathf.PI * 2f + (side < 0f ? 0f : Mathf.PI));
                Vector3 hand = chest + new Vector3(0.3f * side, 0f, 0.4f + 0.32f * pull);
                Quaternion rot = Bone(Vector3.back + Vector3.down * 0.2f, Vector3.down);
                if (side < 0f) { p.HandL = hand; p.HandLRot = rot; }
                else { p.HandR = hand; p.HandRRot = rot; }
            }
            p.Hip += new Vector3(0f, 0f, Mathf.Sin(t * rate * Mathf.PI * 4f) * 0.015f);
            return p;
        }

        // A point on his torso (spine frame) for a given pose, in his Visual's space.
        private static Vector3 SpinePoint(in Pose p, Vector3 local) => p.Hip + p.HipRot * (SpineAt + p.Spine * local);

        // The backhand: right arm cocked high behind him, the body twisted away, then a sweep across the front.
        private Pose SwipePose(float t)
        {
            float quick = Phase == 2 ? 0.75f : 1f, s = stateTime / quick;
            Pose p = KnuckleBase(t);
            var rest = p.HandR;
            var cock = new Vector3(1.35f, 2.3f, -0.6f);
            var through = new Vector3(0.6f, 1.75f, 2.25f);
            var end = new Vector3(-1.0f, 1.3f, 1.6f);
            float twist;
            if (s < 0.55f)
            {
                float w = Mathf.SmoothStep(0f, 1f, s / 0.55f);
                p.HandR = Vector3.Lerp(rest, cock, w);
                twist = 38f * w;
                p.HandRRot = Bone(Vector3.Lerp(Vector3.down, Vector3.back, w), Vector3.left);
            }
            else if (s < 0.72f)
            {
                float k = (s - 0.55f) / 0.17f;
                p.HandR = Bezier(cock, through, end, k);
                twist = Mathf.Lerp(38f, -42f, k);
                p.HandRRot = Bone(Vector3.Lerp(Vector3.right, Vector3.left, k) + Vector3.forward * 0.3f, Vector3.forward);
            }
            else
            {
                float k = Mathf.SmoothStep(0f, 1f, (s - 0.72f) / 0.43f);
                p.HandR = Vector3.Lerp(end, rest, k);
                twist = Mathf.Lerp(-42f, 0f, k);
                p.HandRRot = Quaternion.Slerp(Bone(Vector3.left, Vector3.forward), p.HandRRot, k);
            }
            p.Spine = Quaternion.Euler(15f, twist, -twist * 0.15f);
            p.HipRot = Quaternion.Euler(15f, twist * 0.4f, 0f);
            p.Head = Quaternion.Euler(-30f, -twist * 0.6f, 0f);
            p.HandL = new Vector3(-0.85f, WristToKnuckle, 1.25f); // the left fist stays planted
            return p;
        }

        private static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t) => Vector3.Lerp(Vector3.Lerp(a, b, t), Vector3.Lerp(b, c, t), t);

        // The ground pound: up on his legs, fists high, a hop, then both into the dirt.
        private Pose PoundPose(float t, ref float rate)
        {
            float quick = Phase == 2 ? 0.75f : 1f, windup = 0.85f * quick;
            if (stateTime < windup)
            {
                float k = stateTime / windup;
                Pose p = StandPose(t);
                p.Spine = Quaternion.Euler(-14f, 0f, 0f);
                p.Head = Quaternion.Euler(-20f, 0f, 0f);
                p.HandL = new Vector3(-0.55f, 3.45f, 0.15f);
                p.HandR = new Vector3(0.55f, 3.45f, 0.15f);
                p.HandLRot = Bone(Vector3.up, Vector3.forward);
                p.HandRRot = Bone(Vector3.up, Vector3.forward);
                float hop = k > 0.7f ? Mathf.Sin((k - 0.7f) / 0.3f * Mathf.PI) : 0f;
                p.Hip.y += 0.3f * hop;
                p.FootL.y += 0.25f * hop;
                p.FootR.y += 0.25f * hop;
                if (k < 0.3f) p.Hip.y -= 0.15f * Mathf.Sin(k / 0.3f * Mathf.PI); // a dip before the rise
                rate = 14f;
                return p;
            }
            rate = stateTime < windup + 0.12f ? 40f : 10f;
            return stateTime < windup + 0.45f ? PoundSlam() : KnuckleBase(t);
        }

        private static Pose PoundSlam()
        {
            Pose p = KnuckleBase(0f);
            p.Hip = new Vector3(0f, 0.8f, 0.1f);
            p.HipRot = Quaternion.Euler(28f, 0f, 0f);
            p.Spine = Quaternion.Euler(45f, 0f, 0f);
            p.Head = Quaternion.Euler(-35f, 0f, 0f);
            p.HandL = new Vector3(-0.5f, WristToKnuckle - 0.05f, 1.55f);
            p.HandR = new Vector3(0.5f, WristToKnuckle - 0.05f, 1.55f);
            p.FootL = new Vector3(-0.5f, Ankle, -0.1f);
            p.FootR = new Vector3(0.5f, Ankle, -0.1f);
            return p;
        }

        // The toss: hands behind his back, up comes a barrel, over his head, and hurled.
        private Pose TossPose(float t)
        {
            float quick = Phase == 2 ? 0.75f : 1f, s = stateTime / quick;
            Pose p = StandPose(t);
            var behind = new Vector3(0.55f, 1.75f, -1.05f);
            var over = new Vector3(0.48f, 3.25f, -0.2f);
            var thrown = new Vector3(0.48f, 1.95f, 1.55f);
            Vector3 right;
            if (s < TossReach)
            {
                float k = Mathf.SmoothStep(0f, 1f, s / TossReach);
                right = Vector3.Lerp(new Vector3(1.18f, 1.0f, 0.3f), behind, k);
                p.Spine = Quaternion.Euler(-4f, 0f, 0f);
                p.Head = Quaternion.Euler(5f, 25f * k, 0f); // a sly look over his shoulder
            }
            else if (s < TossLift)
            {
                float k = Mathf.SmoothStep(0f, 1f, (s - TossReach) / (TossLift - TossReach));
                right = Vector3.Lerp(behind, over, k);
                p.Spine = Quaternion.Euler(Mathf.Lerp(-4f, -16f, k), 0f, 0f);
                p.Head = Quaternion.Euler(-22f * k, 0f, 0f);
                p.Hip.y -= 0.1f * Mathf.Sin(k * Mathf.PI);
            }
            else
            {
                float k = Mathf.Clamp01((s - TossLift) / 0.18f);
                right = Vector3.Lerp(over, thrown, Mathf.SmoothStep(0f, 1f, k));
                p.Spine = Quaternion.Euler(Mathf.Lerp(-16f, 38f, k), 0f, 0f);
                p.Head = Quaternion.Euler(-30f, 0f, 0f);
                p.FootR.z += 0.35f * k; // a step into the throw
            }
            p.HandR = right;
            p.HandL = new Vector3(-right.x, right.y, right.z);
            p.HandRRot = Bone(Vector3.left + Vector3.up * 0.3f, Vector3.forward);
            p.HandLRot = Bone(Vector3.right + Vector3.up * 0.3f, Vector3.forward);
            if (s < TossReach) { p.HandL = new Vector3(-1.18f, 1.0f, 0.3f); p.HandLRot = Bone(Vector3.down, Vector3.right); }
            return p;
        }

        // Sitting on the ground: legs out in front, splayed.
        private static Pose SitPose(float t)
        {
            float breathe = Mathf.Sin(t * 1.6f);
            return new Pose
            {
                Hip = new Vector3(0f, 0.66f, -0.15f),
                HipRot = Quaternion.Euler(-20f, 0f, 0f),
                Spine = Quaternion.Euler(14f + breathe, 0f, 0f),
                Head = Quaternion.Euler(-12f, 0f, 0f),
                HandL = new Vector3(-0.72f, 0.95f, 0.65f),
                HandR = new Vector3(0.72f, 0.95f, 0.65f),
                HandLRot = Bone(Vector3.down + Vector3.forward, Vector3.right),
                HandRRot = Bone(Vector3.down + Vector3.forward, Vector3.left),
                FootL = new Vector3(-0.62f, Ankle + 0.02f, 0.78f),
                FootR = new Vector3(0.62f, Ankle + 0.02f, 0.78f),
                FootLRot = Quaternion.Euler(-55f, -28f, 0f),
                FootRRot = Quaternion.Euler(-55f, 28f, 0f),
            };
        }

        // Flat on his back with his feet in the air.
        private static Pose LyingPose(float t, float kickAmount)
        {
            float k1 = Mathf.Sin(t * 13f) * kickAmount, k2 = Mathf.Sin(t * 13f + 2f) * kickAmount;
            return new Pose
            {
                Hip = new Vector3(0f, 0.68f, 0.1f),
                HipRot = Quaternion.Euler(-82f, 0f, 0f),
                Spine = Quaternion.Euler(4f, 0f, 0f),
                Head = Quaternion.Euler(-15f, 0f, Mathf.Sin(t * 1.3f) * 15f),
                HandL = new Vector3(-1.5f, 0.45f, -0.6f),
                HandR = new Vector3(1.5f, 0.45f, -0.6f),
                HandLRot = Bone(Vector3.left, Vector3.up),
                HandRRot = Bone(Vector3.right, Vector3.up),
                FootL = new Vector3(-0.42f, 1.0f + 0.15f * k1, 0.85f + 0.1f * k1),
                FootR = new Vector3(0.42f, 1.0f + 0.15f * k2, 0.85f + 0.1f * k2),
                FootLRot = Quaternion.Euler(-100f, -10f, 0f),
                FootRRot = Quaternion.Euler(-100f, 10f, 0f),
            };
        }

        private Pose DazedPose(float t)
        {
            Pose p = SitPose(t);
            p.Head = Quaternion.Euler(-5f + Mathf.Sin(t * 2.3f) * 15f, Mathf.Sin(t * 1.7f) * 25f, Mathf.Cos(t * 2.3f) * 20f);
            p.Spine = Quaternion.Euler(18f, 0f, Mathf.Sin(t * 1.7f) * 8f);
            p.HandL = new Vector3(-1.05f, WristToKnuckle, 0.15f);
            p.HandR = new Vector3(1.05f, WristToKnuckle, 0.15f);
            p.HandLRot = Knuckle(-1f);
            p.HandRRot = Knuckle(1f);
            return p;
        }

        // Choking on the banana: fists at his throat, coughing, then a thump on his own chest.
        private Pose ChokePose(float t)
        {
            Pose p = StandPose(t);
            float cough = Mathf.Abs(Mathf.Sin(t * 14f));
            p.Spine = Quaternion.Euler(18f + cough * 8f, 0f, 0f);
            p.Head = Quaternion.Euler(-25f + cough * 10f, 0f, 0f);
            Vector3 throat = SpinePoint(p, new Vector3(0f, 1.2f, 0.75f));
            p.HandL = throat + new Vector3(-0.3f, -0.1f, 0.45f);
            p.HandLRot = Bone(Vector3.back + Vector3.right * 0.5f, Vector3.down);
            p.HandR = stateTime > 0.6f
                ? SpinePoint(p, new Vector3(0.25f, 0.85f, 0.75f)) + new Vector3(0.25f, 0f, 0.45f + 0.3f * Mathf.Abs(Mathf.Sin(t * 9f)))
                : throat + new Vector3(0.3f, -0.1f, 0.45f);
            p.HandRRot = Bone(Vector3.back + Vector3.left * 0.5f, Vector3.down);
            p.Hip += new Vector3(Mathf.Sin(t * 3f) * 0.08f, 0f, 0f); // staggering
            return p;
        }

        // Slipping on the peel: feet fly up, flat on his back with legs kicking, then up again.
        private Pose SlipPose(float t, ref float rate)
        {
            float s = stateTime;
            if (s < 0.35f) { rate = 26f; return LyingPose(t, 0f); }
            if (s < 1.8f) { rate = 10f; return LyingPose(t, 1f); }
            if (s < 2.2f) { rate = 8f; return SitPose(t); }
            rate = 8f;
            return KnuckleBase(t);
        }

        // The snack: walks back on his knuckles, plops down, peels the banana in three pulls, then chomps.
        private Pose SnackPose(float dt, float t, ref float rate, ref float look)
        {
            if (stateTime < BackOff) return GroundPose(dt, t, false);
            Pose p = SitPose(t);
            rate = stateTime < BackOff + Plop ? 12f : 10f;
            look = 0.3f;
            // The banana in the right fist, held up in front of his face.
            Vector3 mouth = SpinePoint(p, NeckAt + new Vector3(0f, -0.1f, 0.95f));
            float peel = Mathf.Clamp01(eaten / 0.3f);
            float bite = eaten > 0.3f ? Mathf.Clamp01(Mathf.Sin(Mathf.Repeat(stateTime, 0.45f) / 0.45f * Mathf.PI)) : 0f;
            p.HandR = Vector3.Lerp(new Vector3(0.45f, 1.85f, 1.25f), mouth + new Vector3(0.25f, -0.35f, 0.2f), bite);
            p.HandRRot = Bone(Vector3.left + Vector3.down * 0.4f, Vector3.back);
            if (eaten < 0.3f)
            {
                // Each pull: the left fist yanks a strip down.
                float pull = Mathf.Repeat(peel * 3f, 1f);
                p.HandL = new Vector3(0.05f, 2.1f - pull * 0.5f, 1.3f);
                p.HandLRot = Bone(Vector3.right + Vector3.down * 0.5f, Vector3.back);
                p.Head = Quaternion.Euler(-4f, 10f, 0f);
            }
            else
            {
                p.HandL = new Vector3(-0.45f, 1.3f, 0.95f + Mathf.Abs(Mathf.Sin(t * 5f)) * 0.08f); // patting his belly
                p.HandLRot = Bone(Vector3.back + Vector3.down, Vector3.right);
                p.Head = Quaternion.Euler(-14f + bite * 8f, 0f, 0f);
            }
            for (int i = 0; i < 3; i++) peels[i].localRotation = Quaternion.Euler(0f, i * 120f, 0f) * Quaternion.Euler(Mathf.Clamp01(peel * 3f - i) * 150f, 0f, 0f);
            bananaFlesh.localScale = new Vector3(1f, Mathf.Max(0.05f, 1f - Mathf.Clamp01((eaten - 0.3f) / 0.7f)), 1f);
            return p;
        }

        // Breaking: the spin-out in the kart, thrown up and back flailing, a bump on his backside with the steering
        // wheel in his hands, a long sad look at it, up, chest-beating fury, both fists into the ground.
        private Pose BreakingPose(float t, ref float rate, ref float look, ref Vector3 lift, ref Mood? mood)
        {
            float s = stateTime;
            look = 0f;
            if (s < SpinOut)
            {
                Pose p = KartPose(1f, t);
                gesture = Gesture.Whee;
                ApplyGesture(ref p, s);
                p.HandL = new Vector3(-0.9f, 3.0f, 0.2f);
                mood = Mood.Shock;
                return p;
            }
            if (s < Landed)
            {
                float k = (s - SpinOut) / (Landed - SpinOut);
                lift = Vector3.up * (1.8f * 4f * k * (1f - k));
                Visual.localRotation = VisualRestRotation * Quaternion.Euler(-40f * Mathf.Sin(k * Mathf.PI), Mathf.Sin(k * Mathf.PI * 2f) * 20f, 0f);
                Pose p = LyingPose(t * 1.5f, 1f);
                p.HipRot = Quaternion.Euler(-40f, 0f, 0f);
                p.Hip = new Vector3(0f, 0.9f, 0f);
                p.HandL = new Vector3(-1.4f, 2.4f + Mathf.Sin(t * 20f) * 0.3f, 0.2f);
                p.HandR = new Vector3(1.4f, 2.4f + Mathf.Cos(t * 20f) * 0.3f, 0.2f);
                rate = 18f;
                mood = Mood.Shock;
                return p;
            }
            if (s < StandUp)
            {
                // Holding the wheel in both hands, staring at it, sniffling.
                Pose p = SitPose(t);
                float sob = Mathf.Sin(t * 18f) * 0.01f;
                p.HandL = new Vector3(-0.3f, 1.9f + sob, 1.2f);
                p.HandR = new Vector3(0.3f, 1.9f + sob, 1.2f);
                p.HandLRot = Bone(Vector3.right + Vector3.up * 0.3f, Vector3.forward);
                p.HandRRot = Bone(Vector3.left + Vector3.up * 0.3f, Vector3.forward);
                p.Head = Quaternion.Euler(18f, Mathf.Sin(t * 0.8f) * 6f, 0f);
                p.Spine = Quaternion.Euler(24f, 0f, 0f);
                rate = 8f;
                mood = Mood.Hurt;
                return p;
            }
            if (s < Roar)
            {
                float k = Mathf.SmoothStep(0f, 1f, (s - StandUp) / (Roar - StandUp));
                rate = 10f;
                mood = Mood.Angry;
                return Pose.Lerp(SitPose(t), StandPose(t), k);
            }
            mood = Mood.Furious;
            if (s < Slam) { rate = 20f; return ChestBeat(t, 3.5f); }
            rate = s < Slam + 0.12f ? 40f : 10f;
            return s < Slam + 0.35f ? PoundSlam() : KnuckleBase(t);
        }

        // Places whatever he's holding: the barrel between his fists, the banana in his right fist, the wheel in both.
        private void PlaceHeld()
        {
            Vector3 l = FistPoint(handL), r = FistPoint(handR);
            if (state == State.Snack)
            {
                // The banana sticks up out of his right fist, tipped toward his mouth.
                Vector3 mouth = SpinePoint(pose, NeckAt + new Vector3(0f, -0.1f, 0.95f));
                Vector3 up = Vector3.Lerp(Vector3.up, (mouth - r).normalized, 0.5f);
                held.localPosition = r + up * 0.12f;
                held.localRotation = Quaternion.FromToRotation(Vector3.up, up.normalized);
                return;
            }
            held.localPosition = (l + r) * 0.5f + Vector3.up * 0.1f;
            Vector3 across = r - l;
            if (steering.parent == held)
            {
                steering.localPosition = Vector3.zero;
                steering.localRotation = Quaternion.identity;
                held.localRotation = Quaternion.LookRotation(Vector3.back + Vector3.up * 0.4f, Vector3.up);
                return;
            }
            if (across.sqrMagnitude > 1e-4f) held.localRotation = Quaternion.FromToRotation(Vector3.up, across.normalized);
        }

        private Vector3 FistPoint(Transform hand)
        {
            (Vector3 pos, Quaternion rot) = InVisual(hand);
            return pos + rot * new Vector3(0f, -0.22f, 0.05f);
        }

        // ---------- Death ----------

        protected override void UpdateDeath(float t)
        {
            float dt = Time.deltaTime;
            Pose target;
            float rate = 8f;
            if (kart.gameObject.activeSelf)
            {
                // Still in the kart: slumps over the wheel, the horn bleating under his chin.
                target = KartPose(0f, Time.time);
                target.Spine = Quaternion.Euler(Mathf.Lerp(16f, 55f, Mathf.Clamp01(t / 0.8f)), 0f, 0f);
                target.Head = Quaternion.Euler(30f, 0f, 20f);
                target.HandL = new Vector3(-1.2f, 0.9f, 0.1f);
                target.HandR = new Vector3(1.2f, 0.9f, 0.1f);
            }
            else if (t < 0.7f)
            {
                target = StandPose(Time.time);
                target.Hip += new Vector3(Mathf.Sin(t * 9f) * 0.12f, 0f, 0f);
                target.Spine = Quaternion.Euler(10f, 0f, Mathf.Sin(t * 9f) * 10f);
                target.Head = Quaternion.Euler(-10f, 0f, Mathf.Sin(t * 7f) * 25f);
            }
            else if (t < 1.3f) target = SitPose(Time.time);
            else
            {
                target = LyingPose(Time.time, t < 2.5f ? Mathf.Clamp01(2.5f - t) : 0f);
                rate = 6f;
                if (!deathFell)
                {
                    deathFell = true;
                    Thump(transform.position, 1.2f);
                    Sfx.PlayAt("kart_horn", transform.position, 0.7f, 0.4f, 40f);
                }
            }
            Visual.localPosition = Vector3.Lerp(Visual.localPosition, VisualRest, 1f - Mathf.Exp(-10f * dt));
            Visual.localRotation = Quaternion.Slerp(Visual.localRotation, VisualRestRotation, 1f - Mathf.Exp(-10f * dt));
            lookWeight = 0f;
            PoseNow(target, rate, dt);
            StepJiggles(dt);
        }

        // ---------- Pose maths ----------

        /// <summary>A whole-body pose in his Visual's space: where the hips, hands (wrists) and feet (ankles) go (the
        /// limbs reach them by IK), and how the hips, spine and head turn.</summary>
        private struct Pose
        {
            public Vector3 Hip, HandL, HandR, FootL, FootR;
            public Quaternion HipRot, Spine, Head, HandLRot, HandRRot, FootLRot, FootRRot;

            public static Pose Lerp(in Pose a, in Pose b, float t) => new Pose
            {
                Hip = Vector3.Lerp(a.Hip, b.Hip, t),
                HandL = Vector3.Lerp(a.HandL, b.HandL, t),
                HandR = Vector3.Lerp(a.HandR, b.HandR, t),
                FootL = Vector3.Lerp(a.FootL, b.FootL, t),
                FootR = Vector3.Lerp(a.FootR, b.FootR, t),
                HipRot = Quaternion.Slerp(a.HipRot, b.HipRot, t),
                Spine = Quaternion.Slerp(a.Spine, b.Spine, t),
                Head = Quaternion.Slerp(a.Head, b.Head, t),
                HandLRot = Quaternion.Slerp(a.HandLRot, b.HandLRot, t),
                HandRRot = Quaternion.Slerp(a.HandRRot, b.HandRRot, t),
                FootLRot = Quaternion.Slerp(a.FootLRot, b.FootLRot, t),
                FootRRot = Quaternion.Slerp(a.FootRRot, b.FootRRot, t),
            };
        }

        // A bone pointing along a direction (its local -Y), with its local +Z toward a hint.
        private static Quaternion Bone(Vector3 along, Vector3 hint)
        {
            if (along.sqrMagnitude < 1e-8f) along = Vector3.down;
            if (Vector3.Cross(along, hint).sqrMagnitude < 1e-6f * along.sqrMagnitude * Mathf.Max(hint.sqrMagnitude, 1e-6f))
                hint = Mathf.Abs(along.normalized.y) < 0.9f ? Vector3.up : Vector3.forward;
            return Quaternion.LookRotation(along, hint) * BoneFix;
        }

        private (Vector3 position, Quaternion rotation) InVisual(Transform t)
        {
            Vector3 position = Vector3.zero;
            Quaternion rotation = Quaternion.identity;
            for (Transform x = t; x != null && x != Visual; x = x.parent)
            {
                position = x.localPosition + x.localRotation * position;
                rotation = x.localRotation * rotation;
            }
            return (position, rotation);
        }

        private void PoseNow(Pose target, float rate, float dt, bool snap = false)
        {
            lastDt = dt;
            if (!posed || snap)
            {
                pose = target;
                posed = true;
            }
            else pose = Pose.Lerp(pose, target, 1f - Mathf.Exp(-rate * Mathf.Max(0f, dt)));
            Apply(pose);
        }

        private void Apply(in Pose p)
        {
            body.localPosition = p.Hip;
            body.localRotation = p.HipRot;
            spine.localRotation = p.Spine;
            head.localRotation = Quaternion.Slerp(p.Head, LookTurn(p.Head), lookWeight);
            Vector3 knees = p.HipRot * (Vector3.forward + Vector3.up * 0.2f);
            Limb(legL, kneeL, footL, Thigh, Shin, p.FootL, p.FootLRot, knees + p.HipRot * Vector3.left * 0.45f);
            Limb(legR, kneeR, footR, Thigh, Shin, p.FootR, p.FootRRot, knees + p.HipRot * Vector3.right * 0.45f);
            Quaternion chest = p.HipRot * p.Spine;
            Limb(armL, elbowL, handL, UpperArm, Forearm, p.HandL, p.HandLRot, chest * new Vector3(-0.85f, -0.2f, -0.5f));
            Limb(armR, elbowR, handR, UpperArm, Forearm, p.HandR, p.HandRRot, chest * new Vector3(0.85f, -0.2f, -0.5f));
        }

        // His head turning toward whoever he's looking at (in the spine's frame), clamped to what a neck can do.
        private Quaternion LookTurn(Quaternion fallback)
        {
            Vector3? point = FaceLookPoint;
            if (point == null || lookWeight <= 0.001f) return fallback;
            (Vector3 neck, Quaternion spineTurn) = InVisual(spine);
            neck += spineTurn * NeckAt;
            Vector3 local = Quaternion.Inverse(spineTurn) * (Visual.InverseTransformPoint(point.Value) - neck);
            float yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -55f, 55f);
            float pitch = Mathf.Clamp(-Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg, -50f, 30f);
            lookAngles = Vector2.Lerp(lookAngles, new Vector2(pitch, yaw), 1f - Mathf.Exp(-8f * lastDt));
            return Quaternion.Euler(lookAngles.x, lookAngles.y, 0f);
        }

        // Two-bone IK: places a limb so its end reaches the target (as near as it can), bending toward the pole.
        private void Limb(Transform root, Transform mid, Transform end, float a, float b, Vector3 target, Quaternion endRotation, Vector3 pole)
        {
            (Vector3 parentPosition, Quaternion parentRotation) = InVisual(root.parent);
            Vector3 start = parentPosition + parentRotation * root.localPosition;
            Vector3 d = target - start;
            float length = d.magnitude;
            Vector3 dir = length > 1e-4f ? d / length : parentRotation * Vector3.down;
            length = Mathf.Clamp(length, Mathf.Abs(a - b) + 0.01f, a + b - 0.002f);
            float along = (a * a - b * b + length * length) / (2f * length);
            float height = Mathf.Sqrt(Mathf.Max(0f, a * a - along * along));
            Vector3 bend = Vector3.ProjectOnPlane(pole, dir);
            if (bend.sqrMagnitude < 1e-6f) bend = Vector3.ProjectOnPlane(parentRotation * Vector3.forward, dir);
            bend.Normalize();
            Vector3 joint = start + dir * along + bend * height;
            Vector3 tip = start + dir * length;
            Quaternion upper = Bone(joint - start, bend), lower = Bone(tip - joint, bend);
            root.localRotation = Quaternion.Inverse(parentRotation) * upper;
            mid.localRotation = Quaternion.Inverse(upper) * lower;
            end.localRotation = Quaternion.Inverse(lower) * endRotation;
        }

        /// <summary>A springy joint (scarf tail, flag): it lags behind and swings back to its rest pose.</summary>
        private sealed class Jiggle
        {
            private readonly Transform joint;
            private readonly Quaternion rest;
            private readonly Vector3 tip;
            private readonly float stiffness, damping, gravity;
            private Vector3 point, velocity;
            private bool ready;

            public Jiggle(Transform joint, Vector3 tip, float stiffness, float damping, float gravity)
            {
                this.joint = joint;
                rest = joint.localRotation;
                this.tip = tip;
                this.stiffness = stiffness;
                this.damping = damping;
                this.gravity = gravity;
            }

            public void Step(float dt)
            {
                if (joint == null || dt <= 0f || !joint.gameObject.activeInHierarchy) return;
                joint.localRotation = rest;
                Vector3 pivot = joint.position, want = joint.TransformPoint(tip);
                float length = (want - pivot).magnitude;
                if (!ready || (point - want).sqrMagnitude > 4f)
                {
                    point = want;
                    velocity = Vector3.zero;
                    ready = true;
                }
                velocity += ((want - point) * stiffness + Vector3.down * gravity) * dt;
                velocity *= Mathf.Exp(-damping * dt);
                point += velocity * dt;
                Vector3 to = point - pivot;
                if (to.sqrMagnitude < 1e-8f || length < 1e-5f) return;
                point = pivot + to.normalized * length;
                joint.rotation = Quaternion.FromToRotation(want - pivot, point - pivot) * joint.rotation;
            }
        }
    }

    /// <summary>Kart Kong's barrel, rolling across the ground at the player: jump it or break it. Red ones explode.</summary>
    public sealed class RollingBarrel : MonoBehaviour
    {
        private Vector3 direction;
        private bool red, done;
        private float born;
        private GameObject owner;
        private Health health;
        private GameObject lastHitBy;

        public static Action<MeshKit> Mesh(bool red) => k =>
        {
            Color wood = red ? new Color(0.75f, 0.18f, 0.12f) : new Color(0.55f, 0.36f, 0.2f);
            k.Lathe(new Vector3(0f, -0.45f, 0f), Quaternion.identity, new[] { new Vector2(0.0001f, 0f), new Vector2(0.32f, 0f), new Vector2(0.38f, 0.45f), new Vector2(0.32f, 0.9f), new Vector2(0.0001f, 0.9f) }, 10, wood);
            k.Prism(new Vector3(0f, -0.3f, 0f), Quaternion.identity, 0.36f, 0.36f, 0.05f, 10, MudPalette.DarkMetal);
            k.Prism(new Vector3(0f, 0.25f, 0f), Quaternion.identity, 0.36f, 0.36f, 0.05f, 10, MudPalette.DarkMetal);
            if (red) k.Box(new Vector3(0f, 0f, 0.38f), new Vector3(0.25f, 0.25f, 0.02f), MudPalette.White);
        };

        public static RollingBarrel Spawn(Vector3 at, Vector3 direction, bool red, GameObject owner)
        {
            var go = new GameObject(red ? "Red Barrel" : "Barrel");
            go.transform.position = at + Vector3.up * 0.4f;
            go.transform.rotation = Quaternion.LookRotation(direction.sqrMagnitude > 0.01f ? direction : Vector3.forward);
            MudArt.Part(go.transform, "Model", Vector3.zero, Quaternion.Euler(0f, 0f, 90f), red ? "kong_barrel_red" : "kong_barrel", Mesh(red));
            var shape = go.AddComponent<CapsuleCollider>();
            shape.direction = 0;
            shape.radius = 0.38f;
            shape.height = 0.9f;
            var barrel = go.AddComponent<RollingBarrel>();
            barrel.direction = direction.Flatten().normalized;
            barrel.red = red;
            barrel.owner = owner;
            barrel.born = Time.time;
            barrel.health = go.AddComponent<Health>();
            barrel.health.SetMax(10f);
            barrel.health.Restore();
            barrel.health.Damaged += (h, amount, source) => barrel.lastHitBy = source;
            barrel.health.Died += _ => barrel.Burst(barrel.lastHitBy);
            Sfx.PlayAt("crab_block", at, 0.6f, 0.5f, 30f);
            return barrel;
        }

        private void Update()
        {
            if (done) return;
            float t = Time.time - born;
            if (t > 4f) { Burst(null, false); return; }
            transform.position += direction * 9f * Time.deltaTime;
            if (GroundProbe.Find(transform.position, 1f, 2f, out Vector3 ground))
                transform.position = new Vector3(transform.position.x, ground.y + 0.4f, transform.position.z);
            transform.GetChild(0).Rotate(Vector3.up, 9f / 0.38f * Mathf.Rad2Deg * Time.deltaTime, Space.Self);
            var player = FindAnyObjectByType<FirstPersonController>();
            if (player == null) return;
            Vector3 to = player.transform.position - transform.position;
            float feet = player.transform.position.y;
            // Jumping clears it: the player's feet above the barrel's top.
            if (to.Flatten().magnitude < 0.9f && feet < transform.position.y + 0.3f)
            {
                var hp = player.GetComponent<Health>();
                if (hp != null && !red && hp.TakeDamage(18f, owner))
                    MudHud.Toast("Jump the barrels!", new Color(1f, 0.85f, 0.5f), 1.5f);
                Burst(owner, true);
            }
        }

        private void Burst(GameObject by, bool explodeRed = true)
        {
            if (done) return;
            done = true;
            Vector3 at = transform.position;
            Sfx.PlayAt("wood_smash", at, 0.8f, 0.9f, 35f);
            ParticleBurst.Spawn(ParticleBurst.Kind.Smoke, at, Vector3.up, 16, 0.5f, 3f, 70f, new Color(0.5f, 0.4f, 0.3f, 0.7f));
            if (red && explodeRed)
            {
                ParticleBurst.Explosion(at, 4f);
                CameraShake.Add(0.3f);
                Sfx.PlayAt("car_explode", at, 0.7f, 1.3f, 50f);
                foreach (Collider hit in Physics.OverlapSphere(at, 4f, ~0, QueryTriggerInteraction.Ignore))
                {
                    Health target = hit.GetComponentInParent<Health>();
                    if (target == null || target == health || target.IsDead || !EnemyAllegiance.CanHurt(by, target)) continue;
                    // A red barrel the player broke counts as the player's: it hurts Kong.
                    if (target.GetComponentInParent<KartKong>() != null && by == owner) continue;
                    target.TakeDamage(25f, by);
                }
            }
            Destroy(gameObject);
        }
    }

    /// <summary>A banana peel left on the ground after Kart Kong's snack: he slips on it if he charges over it.</summary>
    public sealed class BananaPeel : MonoBehaviour
    {
        private static readonly List<BananaPeel> Peels = new List<BananaPeel>();
        private float until;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Peels.Clear();

        public static void Drop(Vector3 at)
        {
            var go = new GameObject("Banana Peel");
            if (GroundProbe.Find(at, 2f, 3f, out Vector3 ground)) at = ground;
            go.transform.position = at + Vector3.up * 0.03f;
            go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            MudArt.Part(go.transform, "Peel", Vector3.zero, Quaternion.identity, "banana_peel", k =>
            {
                Color yellow = new Color(1f, 0.88f, 0.25f);
                for (int i = 0; i < 4; i++)
                {
                    Quaternion r = Quaternion.Euler(0f, i * 90f + 20f, 0f);
                    k.Box(r * new Vector3(0f, 0.03f, 0.22f), new Vector3(0.12f, 0.03f, 0.35f), r, yellow);
                }
                k.Prism(Vector3.zero, Quaternion.identity, 0.08f, 0.06f, 0.12f, 6, yellow);
                k.Prism(new Vector3(0f, 0.12f, 0f), Quaternion.identity, 0.03f, 0.02f, 0.06f, 5, new Color(0.3f, 0.22f, 0.1f));
            });
            var peel = go.AddComponent<BananaPeel>();
            peel.until = Time.time + 25f;
            Peels.Add(peel);
        }

        public static bool SlipsAt(Vector3 point)
        {
            foreach (BananaPeel peel in Peels)
            {
                if (peel == null) continue;
                if ((peel.transform.position - point).Flatten().sqrMagnitude < 1.6f * 1.6f)
                {
                    Peels.Remove(peel);
                    Destroy(peel.gameObject);
                    return true;
                }
            }
            return false;
        }

        public static void ClearAll()
        {
            foreach (BananaPeel peel in Peels) if (peel != null) Destroy(peel.gameObject);
            Peels.Clear();
        }

        private void Update()
        {
            if (Time.time < until) return;
            Peels.Remove(this);
            Destroy(gameObject);
        }
    }

    internal static class MudVectorExtensions
    {
        public static Vector3 Flatten(this Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
