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
    /// Quest line 3's mini-boss, Granny Drowse the Sloth Witch (Christian's design, docs/mudlands.md). She flies on a
    /// broom 6-9 m up, out of melee reach, and everything she does is lazy and slow except her magic.
    /// <list type="bullet">
    /// <item><b>Hex orbs:</b> three slow homing orbs (10 each) flicked off her wand; bolts pop them.</item>
    /// <item><b>Sleepy spores:</b> a pod from her belt pouch: a purple cloud, half speed and a blur while you are in it.</item>
    /// <item><b>Cauldron rain:</b> she stirs the air with her wand and the cauldron spits five potion globs onto marked rings (12).</item>
    /// <item><b>Clone beam:</b> she splits into 3 (5 in phase 2) around you; all charge a beam for 3 s. Only the real
    /// one wears the hat and only she can be hurt; hit her to interrupt. Otherwise the beams fire at where you stood
    /// when they locked (80%), 15 each.</item>
    /// <item><b>Knocked off:</b> a full-charge bolt knocks her off the broom. She can't fly without it: she tumbles
    /// down (bouncing off roofs, trees, posts and the cauldron rather than landing on them), lies dazed, gets up and
    /// waddles to wherever the broom caught itself, taking 1.5x damage on the ground. Hit the broom to knock it further
    /// away. If she can't reach it she whistles it over. On the ground the car can ram her.</item>
    /// </list>
    /// The body is a code-built rig posed every frame by two-bone IK (hands and feet are placed, elbows and knees
    /// follow), with springy hat, cape and broom lantern. 420 HP. Drops the Antigrav Coil and a Witch Ember.
    /// </summary>
    public sealed class SlothWitch : MudBoss
    {
        private enum State { Fly, Hex, Spores, Rain, Clones, Wobble, Falling, Dazed, GetUp, Walk, Call, Mount }
        private enum BroomMode { Riding, Tumble, Flying, Hover, Dropped }

        private static readonly Color Fur = new Color(0.5f, 0.44f, 0.36f), FurDark = new Color(0.36f, 0.3f, 0.24f),
            FurLight = new Color(0.68f, 0.61f, 0.5f), Mask = new Color(0.88f, 0.82f, 0.68f), Patch = new Color(0.25f, 0.2f, 0.16f),
            Hair = new Color(0.72f, 0.71f, 0.69f), Robe = new Color(0.27f, 0.15f, 0.35f), RobeDark = new Color(0.18f, 0.1f, 0.24f),
            Lining = new Color(0.45f, 0.18f, 0.3f), Shawl = new Color(0.66f, 0.52f, 0.24f), ShawlDark = new Color(0.5f, 0.38f, 0.17f),
            HatColor = new Color(0.16f, 0.1f, 0.2f), HatUnder = new Color(0.24f, 0.15f, 0.29f), Band = new Color(0.45f, 0.75f, 0.3f),
            Gold = new Color(0.86f, 0.72f, 0.32f), Leather = new Color(0.38f, 0.25f, 0.15f), LeatherDark = new Color(0.28f, 0.18f, 0.11f),
            StripeA = new Color(0.33f, 0.18f, 0.42f), StripeB = new Color(0.1f, 0.08f, 0.11f), Claw = new Color(0.84f, 0.78f, 0.62f),
            Straw = new Color(0.8f, 0.68f, 0.38f), StrawDark = new Color(0.62f, 0.5f, 0.26f), Twine = new Color(0.55f, 0.16f, 0.13f),
            Copper = new Color(0.72f, 0.42f, 0.22f), Handle = new Color(0.4f, 0.29f, 0.19f), Wart = new Color(0.56f, 0.52f, 0.36f),
            Toadstool = new Color(0.78f, 0.2f, 0.16f), PatchCloth = new Color(0.42f, 0.3f, 0.2f),
            Hex = new Color(0.45f, 1f, 0.35f), Sleepy = new Color(0.85f, 0.8f, 1f), Arcane = new Color(0.6f, 0.4f, 1f, 0.7f);

        // ---------- Rig sizes (metres, her own frame: +Z forward, feet at y = 0) ----------
        private const float UpperArm = 0.38f, Forearm = 0.38f, Thigh = 0.26f, Shin = 0.26f, Ankle = 0.085f, StandHip = 0.6f;
        private static readonly Vector3 SpineAt = new Vector3(0f, 0.04f, 0f), NeckAt = new Vector3(0f, 0.78f, 0.06f),
            ShoulderAt = new Vector3(0.31f, 0.58f, -0.02f), HipJointAt = new Vector3(0.15f, -0.03f, 0f),
            HeadCentre = new Vector3(0f, 0.2f, 0.02f), BroomRest = new Vector3(0f, 0.6f, 0f), WandGrip = new Vector3(0f, -0.075f, 0.035f);
        // A waiting broom hovers at her hip height, so it can slide in under her.
        private const float MountHeight = 0.44f;
        // The cauldron's brew, above its base (MudModels.Kind.Cauldron).
        private const float Surface = 1.38f;
        private const float WalkSpeed = 1.3f, GetUpTime = 1.5f, MountRiseStart = 1.15f;
        // Bones run down their local -Y; this turns a look rotation (+Z along the bone) into that.
        private static readonly Quaternion BoneFix = Quaternion.Euler(-90f, 0f, 0f);
        // The wand pokes out of her fist forward and a little down (in the hand's frame).
        private static readonly Vector3 WandLocal = new Vector3(0f, -0.35f, 1f).normalized;
        private static readonly Quaternion WandFix = Quaternion.Inverse(Quaternion.LookRotation(WandLocal, Vector3.up));

        private State state = State.Fly;
        private float stateTime, orbit, nextHex, nextSpores, nextRain, nextClones, hexFired, lastHexAt;
        private Vector3 fallVelocity, tumbleSpin, beamLock;
        private bool beamLocked, dazedLong, dazedBySpores, phaseFlash;
        private float fallTime;
        private int bounces;

        private Transform body, spine, head, hat, hatMid, hatTip, armL, armR, elbowL, elbowR, handL, handR;
        private Transform legL, legR, kneeL, kneeR, footL, footR, broom, lantern, wand, wandTip, stars;
        private readonly Transform[] capes = new Transform[3];
        private readonly List<Transform> clones = new List<Transform>();
        private readonly List<(Transform real, Transform copy)> cloneLinks = new List<(Transform, Transform)>();
        private GroundRing beamRing;
        private PlayerFlask playerFlask;

        // The pose, the springs and how she's moving.
        private Pose pose;
        private bool posed;
        private float lookWeight, lastDt, kick, kickVelocity, walkPhase, walkWeight, flourishUntil, nextSnore;
        private Vector2 lookAngles;
        private readonly List<Jiggle> jiggles = new List<Jiggle>();
        private Vector3 lastPosition, flightVelocity;
        private bool tracked, liftedOff;

        // The broom, when it isn't under her.
        private BroomMode broomMode = BroomMode.Riding;
        private Vector3 broomSpot, broomVelocity, broomSpin, broomFrom, broomTo, mountFrom;
        private Quaternion mountFromRot = Quaternion.identity;
        private float broomTime, broomFlight, broomArc, walkBest, walkProgressAt;

        // Open, reachable floor in the clearing (never a roof, a rock top or the cauldron), found once.
        private readonly List<Vector3> spots = new List<Vector3>();
        private bool spotsFound;

        private bool deathAirborne, vanished;
        private float landedAt = -1f;
        private Vector3 entranceSurface;

        private static readonly RaycastHit[] Hits = new RaycastHit[32];
        private static readonly Collider[] Overlaps = new Collider[32];

        public override string Subtitle => "Granny Drowse, the Sloth Witch";
        public override string Title => "Granny Drowse";
        public override Vector3 FightStart(Vector3 home) => home + Vector3.up * 3.5f;
        public override bool WalksAfterEntrance => false; // she flies

        private bool Riding => state == State.Fly || state == State.Hex || state == State.Spores || state == State.Rain ||
                               state == State.Clones || state == State.Wobble;
        private bool BroomAttached => broomMode == BroomMode.Riding;
        private int HexCount => Phase == 2 ? 4 : 3;
        // The clearing's middle, where the cauldron stands (Home is only the nearest NavMesh point to it).
        private Vector3 ArenaCentre => transform.parent != null ? transform.parent.position : Home;

        protected override void Awake()
        {
            base.Awake();
            corpseTime = Mathf.Max(corpseTime, 6f); // time to fall, curl up and snore before she vanishes
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (broom != null && broom.parent == null) Destroy(broom.gameObject);
            foreach (Transform clone in clones) if (clone != null) Destroy(clone.gameObject);
        }

        // =====================================================================================================
        // Model
        // =====================================================================================================

        protected override void BuildModel(Transform visual)
        {
            broom = BuildBroom(visual);
            body = MudArt.Pivot(visual, "Body", new Vector3(0f, StandHip, 0f));
            MudArt.Part(body, "Hips", Vector3.zero, Quaternion.identity, "witch2_hips", HipsMesh);
            spine = MudArt.Pivot(body, "Spine", SpineAt);
            MudArt.Part(spine, "Torso", Vector3.zero, Quaternion.identity, "witch2_torso", TorsoMesh);
            BuildCape();
            head = MudArt.Pivot(spine, "Head", NeckAt);
            BuildHead();
            armL = BuildArm(-1f, out elbowL, out handL);
            armR = BuildArm(1f, out elbowR, out handR);
            legL = BuildLeg(-1f, out kneeL, out footL);
            legR = BuildLeg(1f, out kneeR, out footR);
            wand = MudArt.Part(handR, "Wand", WandGrip, Quaternion.LookRotation(WandLocal, Vector3.up), "witch2_wand", WandMesh);
            wandTip = MudArt.Pivot(wand, "Wand Tip", new Vector3(0f, 0f, 0.53f));

            jiggles.Add(new Jiggle(hatMid, new Vector3(0f, 0.3f, 0f), 110f, 7f, 0f));
            jiggles.Add(new Jiggle(hatTip, new Vector3(0f, 0.36f, 0f), 85f, 6f, 0.6f));
            foreach (Transform cape in capes) jiggles.Add(new Jiggle(cape, new Vector3(0f, -0.3f, 0f), 45f, 3.2f, 2.5f));
            jiggles.Add(new Jiggle(lantern, new Vector3(0f, -0.25f, 0f), 24f, 2.4f, 6f));
        }

        // ---------- The broom ----------

        private Transform BuildBroom(Transform visual)
        {
            Transform root = MudArt.Pivot(visual, "Broom", BroomRest);
            MudArt.Part(root, "Broom Model", Vector3.zero, Quaternion.identity, "witch2_broom", BroomMesh);
            // The ship's antigrav coil is what makes the broom fly.
            ShipPart = MudArt.Part(root, "Antigrav Coil", new Vector3(0f, -0.11f, 0.52f), Quaternion.identity, "witch2_coil", CoilMesh);
            lantern = MudArt.Pivot(root, "Broom Lantern", new Vector3(0f, 0.12f, 1.37f));
            MudArt.Part(lantern, "Lantern", Vector3.zero, Quaternion.identity, "witch2_lantern", LanternMesh);
            var glow = new GameObject("Lantern Light").AddComponent<Light>();
            glow.transform.SetParent(lantern, false);
            glow.transform.localPosition = new Vector3(0f, -0.21f, 0f);
            glow.type = LightType.Point;
            glow.color = new Color(0.78f, 1f, 0.62f);
            glow.range = 2.6f;
            glow.intensity = 0.55f;
            glow.shadows = LightShadows.None;
            return root;
        }

        private static void BroomMesh(MeshKit k)
        {
            // A gnarled handle, curling up at the front where the lantern hangs.
            k.Smooth = true;
            k.Tube(new[]
            {
                new Vector3(0f, 0f, -0.88f), new Vector3(0.006f, 0.01f, -0.4f), new Vector3(-0.01f, -0.008f, 0.1f),
                new Vector3(0.012f, 0.012f, 0.6f), new Vector3(-0.006f, 0.004f, 1f), new Vector3(0f, 0f, 1.25f)
            }, 0.042f, 8, Handle);
            k.Tube(new[]
            {
                new Vector3(0f, 0f, 1.25f), new Vector3(0f, 0.05f, 1.34f), new Vector3(0f, 0.12f, 1.37f),
                new Vector3(0f, 0.17f, 1.33f), new Vector3(0f, 0.16f, 1.27f)
            }, 0.032f, 8, Handle);
            k.Ball(new Vector3(0.034f, 0.012f, 0.78f), new Vector3(0.034f, 0.03f, 0.045f), Quaternion.identity, Handle, 1); // knots
            k.Ball(new Vector3(-0.03f, -0.01f, -0.16f), 0.03f, Handle, 1);
            // A knitted cushion to sit on (she's not getting any younger).
            k.Ball(new Vector3(0f, 0.045f, -0.1f), new Vector3(0.16f, 0.05f, 0.22f), Quaternion.identity, Shawl, 1);
            foreach (float x in new[] { -0.14f, 0.14f })
                foreach (float z in new[] { -0.3f, 0.1f })
                    k.Ball(new Vector3(x, 0.03f, z), 0.024f, ShawlDark, 1);
            k.Smooth = false;
            k.Box(new Vector3(0f, 0.09f, -0.17f), new Vector3(0.24f, 0.014f, 0.035f), Robe);
            k.Box(new Vector3(0f, 0.093f, -0.04f), new Vector3(0.25f, 0.014f, 0.035f), Robe);
            k.Rod(new Vector3(0.03f, 0.01f, 0.3f), new Vector3(0.09f, 0.06f, 0.36f), 0.012f, 4, Handle, 0f, 0.004f); // a twig stub
            // Bindings and the bristle bundle: a core, then twigs fanning out.
            k.Prism(new Vector3(0f, 0f, -0.86f), Quaternion.Euler(90f, 0f, 0f), 0.07f, 0.075f, 0.07f, 8, Twine);
            k.Prism(new Vector3(0f, 0f, -1.02f), Quaternion.Euler(90f, 0f, 0f), 0.13f, 0.135f, 0.05f, 8, Twine);
            k.Prism(new Vector3(0f, 0f, -0.88f), Quaternion.Euler(-90f, 0f, 0f), 0.07f, 0.26f, 0.62f, 8, StrawDark);
            for (int i = 0; i < 36; i++)
            {
                float f = (i + 0.5f) / 36f, a = i * 2.39996f, r = Mathf.Sqrt(f);
                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                float length = 0.68f + 0.12f * Mathf.Sin(i * 1.7f);
                Vector3 from = new Vector3(0f, 0f, -0.86f) + dir * (0.06f * r);
                Vector3 to = new Vector3(0f, -0.03f, -0.86f - length) + dir * (0.1f + 0.24f * r);
                k.Rod(from, to, 0.016f, 4, i % 3 == 0 ? StrawDark : Straw, 0f, 0.005f);
            }
        }

        private static void CoilMesh(MeshKit k)
        {
            k.Smooth = true;
            k.Ball(Vector3.zero, new Vector3(0.055f, 0.055f, 0.14f), Quaternion.identity, new Color(0.6f, 0.4f, 1f), 1, 3f);
            k.Smooth = false;
            for (int i = 0; i < 4; i++)
                k.Prism(new Vector3(0f, 0f, -0.12f + i * 0.07f), Quaternion.Euler(90f, 0f, 0f), 0.08f, 0.08f, 0.024f, 10, Copper);
            k.Box(new Vector3(0f, 0.065f, 0f), new Vector3(0.03f, 0.05f, 0.24f), MudPalette.DarkMetal); // clamped to the handle
            k.Box(new Vector3(0f, 0f, -0.17f), new Vector3(0.16f, 0.012f, 0.06f), MudPalette.DarkMetal);   // a stubby fin
            k.Rod(new Vector3(0.06f, 0f, -0.14f), new Vector3(0.03f, 0.08f, -0.26f), 0.007f, 4, Copper);
        }

        private static void LanternMesh(MeshKit k)
        {
            k.Rod(Vector3.zero, new Vector3(0f, -0.08f, 0f), 0.006f, 4, MudPalette.DarkMetal);
            k.Cone(new Vector3(0f, -0.13f, 0f), Quaternion.identity, 0.075f, 0.05f, 6, MudPalette.DarkMetal);
            k.Prism(new Vector3(0f, -0.28f, 0f), Quaternion.identity, 0.045f, 0.045f, 0.15f, 6, Hex, 2.5f);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f + 0.4f;
                var o = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.055f;
                k.Rod(o + Vector3.down * 0.28f, o + Vector3.down * 0.13f, 0.007f, 4, MudPalette.DarkMetal);
            }
            k.Prism(new Vector3(0f, -0.305f, 0f), Quaternion.identity, 0.065f, 0.06f, 0.025f, 6, MudPalette.DarkMetal);
        }

        // ---------- Body ----------

        private static void HipsMesh(MeshKit k)
        {
            k.Smooth = true;
            k.Ball(new Vector3(0f, 0.04f, -0.01f), new Vector3(0.34f, 0.22f, 0.3f), Quaternion.identity, Robe, 1);
            var skirt = new[] { new Vector2(0.48f, -0.36f), new Vector2(0.45f, -0.24f), new Vector2(0.4f, -0.1f), new Vector2(0.365f, 0.02f), new Vector2(0.35f, 0.1f) };
            k.Lathe(new Vector3(0f, 0f, -0.01f), Quaternion.identity, skirt, 18, Robe);
            k.Lathe(new Vector3(0f, 0f, -0.01f), Quaternion.identity, Inner(skirt, 0.965f), 18, Lining);
            // The belt pouch (spore pods) on her left hip.
            k.Ball(new Vector3(-0.375f, -0.07f, 0.13f), new Vector3(0.08f, 0.1f, 0.065f), Quaternion.Euler(0f, -32f, 0f), Leather, 1);
            k.Ball(new Vector3(-0.38f, 0.0f, 0.14f), new Vector3(0.085f, 0.04f, 0.072f), Quaternion.Euler(0f, -32f, 0f), LeatherDark, 1);
            k.Ball(new Vector3(-0.35f, -0.01f, 0.205f), 0.014f, Gold, 0);
            // Potions hanging off her right hip: a round flask, a tall vial and a squat square bottle. Dark glass with
            // a soft glow in the brew, so they read as bottles, not fairy lights.
            Color glass = new Color(0.2f, 0.26f, 0.22f);
            {
                Vector3 at = Hip3(68f, 0.44f, -0.16f);
                k.Smooth = true;
                k.Ball(at, new Vector3(0.058f, 0.06f, 0.058f), Quaternion.identity, glass, 1);
                k.Ball(at + Vector3.down * 0.012f, new Vector3(0.05f, 0.04f, 0.05f), Quaternion.identity, Hex, 1, 0.7f);
                k.Smooth = false;
                k.Prism(at + Vector3.up * 0.05f, Quaternion.identity, 0.02f, 0.017f, 0.05f, 6, glass);
                k.Prism(at + Vector3.up * 0.097f, Quaternion.identity, 0.022f, 0.019f, 0.025f, 6, MudPalette.Wood);
                k.Rod(at + Vector3.up * 0.12f, Hip3(66f, 0.39f, 0.02f), 0.006f, 4, Leather);
            }
            {
                Vector3 at = Hip3(88f, 0.445f, -0.15f);
                k.Prism(at + Vector3.down * 0.06f, Quaternion.identity, 0.024f, 0.024f, 0.13f, 6, glass);
                k.Prism(at + Vector3.down * 0.055f, Quaternion.identity, 0.019f, 0.019f, 0.09f, 6, MudPalette.Magic, 0.7f);
                k.Prism(at + Vector3.up * 0.07f, Quaternion.identity, 0.027f, 0.022f, 0.03f, 6, MudPalette.Wood);
                k.Rod(at + Vector3.up * 0.1f, Hip3(86f, 0.39f, 0.02f), 0.006f, 4, Leather);
            }
            {
                Vector3 at = Hip3(108f, 0.43f, -0.15f);
                k.Box(at, new Vector3(0.075f, 0.075f, 0.06f), Quaternion.Euler(0f, 18f, 0f), glass);
                k.Box(at + Vector3.down * 0.012f, new Vector3(0.068f, 0.05f, 0.054f), Quaternion.Euler(0f, 18f, 0f), new Color(1f, 0.55f, 0.2f), 0.7f);
                k.Prism(at + Vector3.up * 0.037f, Quaternion.identity, 0.016f, 0.014f, 0.03f, 5, glass);
                k.Prism(at + Vector3.up * 0.067f, Quaternion.identity, 0.018f, 0.016f, 0.02f, 5, MudPalette.Wood);
                k.Rod(at + Vector3.up * 0.087f, Hip3(106f, 0.39f, 0.02f), 0.006f, 4, Leather);
            }
            k.Smooth = false;
            k.Prism(new Vector3(0f, -0.005f, -0.01f), Quaternion.identity, 0.378f, 0.372f, 0.065f, 18, Leather); // the belt
            k.Box(new Vector3(0f, 0.027f, 0.375f), new Vector3(0.1f, 0.08f, 0.02f), Gold);
            k.Box(new Vector3(0f, 0.027f, 0.386f), new Vector3(0.055f, 0.04f, 0.008f), Leather);
            // A wooden stirring spoon tucked in at the back.
            k.Rod(new Vector3(-0.14f, -0.06f, -0.41f), new Vector3(-0.22f, 0.38f, -0.43f), 0.016f, 5, MudPalette.Wood);
            k.Smooth = true;
            k.Ball(new Vector3(-0.227f, 0.42f, -0.432f), new Vector3(0.045f, 0.06f, 0.02f), Quaternion.Euler(0f, 0f, -10f), MudPalette.Wood, 1);
            // The ragged hem.
            k.Smooth = false;
            for (int i = 0; i < 16; i++)
            {
                float a0 = i / 16f * Mathf.PI * 2f, a1 = (i + 1) / 16f * Mathf.PI * 2f, am = (a0 + a1) * 0.5f;
                float drop = 0.06f + 0.05f * ((i * 7) % 3) / 2f;
                TwoTri(k, Hem(a0, 0.48f, -0.36f), Hem(a1, 0.48f, -0.36f), Hem(am, 0.495f, -0.36f - drop), Robe, Lining);
            }
        }

        // A point round her hips: degrees from the front toward her right, radius, height.
        private static Vector3 Hip3(float degrees, float radius, float y)
        {
            float a = degrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(a) * radius, y, Mathf.Cos(a) * radius - 0.01f);
        }

        private static Vector3 Hem(float a, float r, float y) => new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r - 0.01f);

        private static void TorsoMesh(MeshKit k)
        {
            k.Smooth = true;
            k.Ball(new Vector3(0f, 0.3f, 0f), new Vector3(0.37f, 0.42f, 0.33f), Quaternion.identity, Robe, 1);
            k.Ball(new Vector3(0f, 0.5f, -0.1f), new Vector3(0.32f, 0.27f, 0.27f), Quaternion.identity, Robe, 1);     // the hunch
            k.Ball(new Vector3(0f, 0.44f, 0.15f), new Vector3(0.2f, 0.22f, 0.17f), Quaternion.identity, FurLight, 1); // fur at the neckline
            foreach (float x in new[] { -0.3f, 0.3f }) // puffed shoulders
                k.Ball(new Vector3(x, 0.56f, -0.02f), new Vector3(0.13f, 0.12f, 0.13f), Quaternion.identity, Robe, 1);
            // A knitted shawl round her shoulders, pinned with a glowing brooch.
            var collar = new[] { new Vector2(0.43f, 0f), new Vector2(0.42f, 0.06f), new Vector2(0.35f, 0.16f), new Vector2(0.24f, 0.25f), new Vector2(0.16f, 0.29f) };
            k.Lathe(new Vector3(0f, 0.46f, -0.03f), Quaternion.identity, collar, 20, Shawl);
            k.Lathe(new Vector3(0f, 0.46f, -0.03f), Quaternion.identity, Inner(collar, 0.97f), 20, ShawlDark);
            for (int i = 0; i < 9; i++) // neck fluff
            {
                float a = i / 9f * Mathf.PI * 2f;
                k.Ball(new Vector3(Mathf.Cos(a) * 0.15f, 0.74f, Mathf.Sin(a) * 0.13f + 0.03f), new Vector3(0.06f, 0.08f, 0.06f),
                    Quaternion.Euler(Mathf.Sin(a) * 20f, 0f, -Mathf.Cos(a) * 20f), FurLight, 1);
            }
            k.Ball(new Vector3(0f, 0.6f, 0.355f), 0.038f, Hex, 1, 2.2f);
            k.Smooth = false;
            k.Prism(new Vector3(0f, 0.6f, 0.33f), Quaternion.Euler(90f, 0f, 0f), 0.056f, 0.05f, 0.022f, 8, Gold);
            // The shawl's point down her back, and its fringe.
            TwoQuad(k, new Vector3(-0.3f, 0.5f, -0.37f), new Vector3(0.3f, 0.5f, -0.37f), new Vector3(0.13f, 0.22f, -0.38f), new Vector3(-0.13f, 0.22f, -0.38f), Shawl, ShawlDark);
            TwoTri(k, new Vector3(-0.13f, 0.22f, -0.38f), new Vector3(0.13f, 0.22f, -0.38f), new Vector3(0f, 0.08f, -0.35f), Shawl, ShawlDark);
            for (int i = 0; i < 18; i++)
            {
                float a = i / 18f * Mathf.PI * 2f;
                k.Cone(new Vector3(Mathf.Cos(a) * 0.43f, 0.465f, Mathf.Sin(a) * 0.43f - 0.03f), Quaternion.Euler(180f, 0f, 0f), 0.018f, 0.075f, 4, ShawlDark);
            }
            k.Cone(new Vector3(0f, 0.09f, -0.35f), Quaternion.Euler(180f, 0f, 0f), 0.025f, 0.1f, 4, ShawlDark);
            // The bodice lacing.
            for (int i = 0; i < 3; i++)
            {
                float y = 0.1f + i * 0.075f;
                float z = 0.33f * Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((y - 0.3f) / 0.42f, 2f))) + 0.004f;
                k.Box(new Vector3(0f, y, z), new Vector3(0.09f, 0.012f, 0.012f), Quaternion.Euler(0f, 0f, 28f), Leather);
                k.Box(new Vector3(0f, y, z), new Vector3(0.09f, 0.012f, 0.012f), Quaternion.Euler(0f, 0f, -28f), Leather);
            }
        }

        // A tattered cape in three springy pieces, hanging off the back of her shoulders.
        private void BuildCape()
        {
            float[] widths = { 0.62f, 0.56f, 0.5f, 0.44f };
            Transform parent = spine;
            var at = new Vector3(0f, 0.62f, -0.26f);
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                Transform piece = MudArt.Pivot(parent, "Cape " + (i + 1), at, Quaternion.Euler(i == 0 ? 28f : 6f, 0f, 0f));
                MudArt.Part(piece, "Cloth", Vector3.zero, Quaternion.identity, "witch2_cape" + i, k => CapeMesh(k, widths[index], widths[index + 1], 0.3f, index == 2));
                capes[i] = piece;
                parent = piece;
                at = new Vector3(0f, -0.3f, 0f);
            }
        }

        private static void CapeMesh(MeshKit k, float top, float bottom, float length, bool ragged)
        {
            const int columns = 6;
            Vector3 P(float u, float v)
            {
                float width = Mathf.Lerp(top, bottom, v);
                return new Vector3((u - 0.5f) * width, -length * v, -0.06f * (1f - 4f * (u - 0.5f) * (u - 0.5f)));
            }
            for (int c = 0; c < columns; c++)
            {
                float u0 = c / (float)columns, u1 = (c + 1f) / columns;
                if (!ragged)
                {
                    TwoQuad(k, P(u0, 0f), P(u1, 0f), P(u1, 1f), P(u0, 1f), RobeDark, Lining);
                    continue;
                }
                TwoQuad(k, P(u0, 0f), P(u1, 0f), P(u1, 0.7f), P(u0, 0.7f), RobeDark, Lining);
                TwoTri(k, P(u0, 0.7f), P(u1, 0.7f), P((u0 + u1) * 0.5f, c % 2 == 0 ? 1.15f : 0.95f), RobeDark, Lining);
            }
        }

        // ---------- Head ----------

        private void BuildHead()
        {
            Vector3 c = HeadCentre;
            MudArt.Part(head, "Face", Vector3.zero, Quaternion.identity, "witch2_head", HeadMesh);
            (Transform l, Transform r) = MudArt.Eyes(head, c + new Vector3(0f, 0.03f, 0.255f), 0.09f, 0.032f, MudPalette.Black, MudPalette.Black, "witch2");
            foreach (Transform eye in new[] { l, r })
                MudArt.Part(eye, "Glint", new Vector3(0.012f, 0.013f, 0.026f), Quaternion.identity, "witch2_glint", k => k.Ball(Vector3.zero, 0.008f, MudPalette.White, 0, 1.5f));
            Transform lids = MudArt.Part(head, "Lids", c + new Vector3(0f, 0.05f, 0.262f), Quaternion.identity, "witch2_lids", k =>
            {
                k.Smooth = true;
                k.Ball(new Vector3(-0.09f, 0f, 0f), new Vector3(0.05f, 0.018f, 0.02f), Quaternion.identity, Patch, 1);
                k.Ball(new Vector3(0.09f, 0f, 0f), new Vector3(0.05f, 0.018f, 0.02f), Quaternion.identity, Patch, 1);
            });
            Transform browL = BossFace.Brow(head, "witch2_brow", c + new Vector3(-0.1f, 0.115f, 0.24f), -1f, new Vector3(0.1f, 0.032f, 0.04f), Hair, -6f);
            Transform browR = BossFace.Brow(head, "witch2_brow", c + new Vector3(0.1f, 0.115f, 0.24f), 1f, new Vector3(0.1f, 0.032f, 0.04f), Hair, -6f);
            Transform mouth = BossFace.Mouth(head, "witch2_mouth", c + new Vector3(0f, -0.13f, 0.265f), 0.11f, 0.045f, new Color(0.5f, 0.42f, 0.34f), false, Quaternion.Euler(-20f, 0f, 0f));
            MudArt.Part(mouth, "Tooth", new Vector3(0.024f, 0.014f, 0.014f), Quaternion.Euler(180f, 0f, 0f), "witch2_tooth",
                k => k.Cone(Vector3.zero, Quaternion.identity, 0.011f, 0.03f, 5, MudPalette.Cream)); // one snaggle tooth
            FaceParts = new BossFace.Rig
            {
                BrowL = browL, BrowR = browR, EyeL = l, EyeR = r, Lids = lids, Mouth = mouth,
                BrowTravel = 0.02f, Idle = Mood.Sleepy, Fight = Mood.Smug,
            };
            MudArt.Part(head, "Spectacles", c + new Vector3(0f, -0.012f, 0.318f), Quaternion.identity, "witch2_specs", SpectaclesMesh);
            hat = MudArt.Part(head, "Witch Hat", c + new Vector3(0f, 0.2f, -0.02f), Quaternion.Euler(-10f, 0f, 8f), "witch2_hat", HatMesh);
            hatMid = MudArt.Pivot(hat, "Hat Mid", new Vector3(0f, 0.3f, 0f), Quaternion.Euler(-14f, 0f, 0f));
            MudArt.Part(hatMid, "Crown", Vector3.zero, Quaternion.identity, "witch2_hat_mid", HatMidMesh);
            hatTip = MudArt.Pivot(hatMid, "Hat Tip", new Vector3(0f, 0.3f, 0f), Quaternion.Euler(-30f, 0f, 0f));
            MudArt.Part(hatTip, "Tip", Vector3.zero, Quaternion.identity, "witch2_hat_tip", HatTipMesh);
            // Stars that circle round her hat while she's seeing them.
            stars = MudArt.Pivot(head, "Dazed Stars", c + new Vector3(0f, 0.26f, 0f));
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.PI * 2f / 3f;
                MudArt.Part(stars, "Star " + i, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.62f, Quaternion.identity, "witch2_star",
                    k => Star(k, Vector3.zero, 0.075f, 0.025f, new Color(1f, 0.88f, 0.35f), 2.5f));
            }
            stars.gameObject.SetActive(false);
        }

        private static void HeadMesh(MeshKit k)
        {
            Vector3 c = HeadCentre;
            k.Smooth = true;
            k.Ball(c, new Vector3(0.29f, 0.26f, 0.27f), Quaternion.identity, Fur, 1);
            k.Ball(c + new Vector3(0f, -0.03f, 0.12f), new Vector3(0.22f, 0.17f, 0.17f), Quaternion.identity, Mask, 1);
            foreach (float x in new[] { -0.09f, 0.09f }) // the drooping dark patches round her eyes
                k.Ball(c + new Vector3(x, 0.01f, 0.245f), new Vector3(0.085f, 0.045f, 0.035f), Quaternion.Euler(0f, 0f, x > 0f ? 25f : -25f), Patch, 1);
            k.Ball(c + new Vector3(0f, -0.05f, 0.3f), new Vector3(0.055f, 0.04f, 0.035f), Quaternion.identity, MudPalette.Black, 1);
            k.Ball(c + new Vector3(0.016f, -0.036f, 0.328f), 0.011f, new Color(0.45f, 0.45f, 0.48f), 0);
            foreach (float x in new[] { -0.25f, 0.25f })
            {
                k.Ball(c + new Vector3(x, -0.08f, 0.06f), 0.1f, Fur, 1); // fluffy cheeks
                k.Ball(c + new Vector3(x * 1.04f, 0.1f, -0.06f), new Vector3(0.06f, 0.07f, 0.04f), Quaternion.identity, FurDark, 1); // ears
            }
            k.Ball(c + new Vector3(0f, -0.19f, 0.13f), new Vector3(0.12f, 0.07f, 0.09f), Quaternion.identity, FurLight, 1); // chin
            // Wispy grey hair poking out under the hat at the sides and back (her face stays clear).
            for (int i = 0; i < 13; i++)
            {
                float a = Mathf.Lerp(165f, 375f, i / 12f) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float vary = Mathf.Abs(Mathf.Sin(i * 2.3f));
                Color tone = Color.Lerp(Hair, FurLight, 0.35f * Mathf.Abs(Mathf.Sin(i * 1.7f)));
                k.Ball(c + dir * (0.255f + 0.02f * vary) + Vector3.up * (0.06f - 0.07f * vary), new Vector3(0.042f, 0.075f + 0.03f * vary, 0.035f),
                    Quaternion.LookRotation(dir) * Quaternion.Euler(35f + 20f * vary, 0f, (i % 2 == 0 ? 15f : -15f)), tone, 1);
            }
            // Shaggy fur round the back of the head and the jaw.
            for (int i = 0; i < 9; i++)
            {
                float a = Mathf.Lerp(160f, 380f, i / 8f) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                k.Ball(c + dir * 0.25f + Vector3.down * 0.12f, new Vector3(0.07f, 0.1f, 0.06f), Quaternion.LookRotation(dir) * Quaternion.Euler(-25f, 0f, 0f), Fur, 1);
            }
            k.Ball(c + new Vector3(0.1f, -0.06f, 0.27f), 0.017f, Wart, 1); // a wart, of course
            k.Smooth = false;
            k.Rod(c + new Vector3(0.105f, -0.05f, 0.283f), c + new Vector3(0.12f, -0.015f, 0.3f), 0.0035f, 3, MudPalette.Black); // with a hair
            foreach (float x in new[] { -0.035f, 0f, 0.035f }) // chin whiskers
                k.Cone(c + new Vector3(x, -0.22f, 0.16f), Quaternion.Euler(160f, 0f, x * 300f), 0.018f, 0.07f, 4, FurLight);
        }

        // Round reading glasses, perched low on her nose (she peers over them).
        private static void SpectaclesMesh(MeshKit k)
        {
            const float r = 0.047f, wire = 0.0055f;
            foreach (float side in new[] { -1f, 1f })
            {
                var centre = new Vector3(0.088f * side, 0f, 0f);
                for (int i = 0; i < 14; i++)
                {
                    float a0 = i * Mathf.PI * 2f / 14f, a1 = (i + 1) * Mathf.PI * 2f / 14f;
                    k.Rod(centre + new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f) * r, centre + new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f) * r, wire, 4, Gold);
                }
                k.Rod(new Vector3((0.088f + r) * side, 0.01f, -0.005f), new Vector3(0.25f * side, 0.04f, -0.2f), wire, 4, Gold);
            }
            k.Rod(new Vector3(-0.041f, 0.012f, 0f), new Vector3(0f, 0.026f, 0.008f), wire, 4, Gold);
            k.Rod(new Vector3(0f, 0.026f, 0.008f), new Vector3(0.041f, 0.012f, 0f), wire, 4, Gold);
        }

        private static void HatMesh(MeshKit k)
        {
            const int n = 36;
            for (int i = 0; i < n; i++)
            {
                float a0 = i * Mathf.PI * 2f / n, a1 = (i + 1) * Mathf.PI * 2f / n;
                TwoQuad(k, Brim(a0, true), Brim(a0, false), Brim(a1, false), Brim(a1, true), HatUnder, HatColor);
            }
            k.Smooth = true;
            k.Prism(Vector3.zero, Quaternion.identity, 0.25f, 0.185f, 0.31f, 16, HatColor);
            k.Prism(new Vector3(0f, 0.015f, 0f), Quaternion.identity, 0.258f, 0.242f, 0.08f, 16, Band);
            k.Smooth = false;
            k.Box(new Vector3(0f, 0.055f, 0.254f), new Vector3(0.11f, 0.095f, 0.022f), Quaternion.Euler(-4f, 0f, 0f), Gold);
            k.Box(new Vector3(0f, 0.055f, 0.266f), new Vector3(0.06f, 0.05f, 0.01f), Quaternion.Euler(-4f, 0f, 0f), Band);
            // A patch, stitched on.
            Vector3 outward = new Vector3(-0.94f, 0f, 0.34f).normalized;
            Quaternion facing = Quaternion.LookRotation(outward) * Quaternion.Euler(-12f, 0f, 8f);
            Vector3 at = new Vector3(0f, 0.19f, 0f) + outward * 0.22f;
            k.Box(at, new Vector3(0.1f, 0.09f, 0.014f), facing, PatchCloth);
            for (int s = 0; s < 8; s++)
            {
                float a = s * Mathf.PI / 4f;
                var offset = new Vector3(Mathf.Cos(a) * 0.052f, Mathf.Sin(a) * 0.047f, 0.009f);
                k.Box(at + facing * offset, new Vector3(0.018f, 0.006f, 0.006f), facing * Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg + 90f), MudPalette.Cream);
            }
            // A toadstool tucked in the band.
            var stem = new Vector3(0.22f, 0.05f, 0.12f);
            Quaternion lean = Quaternion.Euler(0f, 0f, -18f);
            k.Prism(stem, lean, 0.016f, 0.013f, 0.08f, 6, MudPalette.Cream);
            Vector3 cap = stem + lean * new Vector3(0f, 0.085f, 0f);
            k.Smooth = true;
            k.Ball(cap, new Vector3(0.055f, 0.032f, 0.055f), lean, Toadstool, 1);
            foreach (Vector3 dot in new[] { new Vector3(0.02f, 0.025f, 0.015f), new Vector3(-0.025f, 0.022f, -0.01f), new Vector3(0.005f, 0.03f, -0.03f) })
                k.Ball(cap + lean * dot, 0.009f, MudPalette.White, 0);
        }

        // A wavy, drooping brim: the inner edge under the crown, the outer edge rippled and sagging at the front.
        private static Vector3 Brim(float a, bool inner)
        {
            if (inner) return new Vector3(Mathf.Cos(a) * 0.2f, 0.005f, Mathf.Sin(a) * 0.2f);
            float r = 0.53f + 0.035f * Mathf.Sin(5f * a) + 0.015f * Mathf.Sin(3f * a + 1f);
            float front = Mathf.Max(0f, Mathf.Sin(a));
            float y = -0.085f * front * front + 0.025f * Mathf.Sin(4f * a + 0.5f) - 0.01f;
            return new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
        }

        private static void HatMidMesh(MeshKit k)
        {
            k.Smooth = true;
            // Starts a little inside the crown below, so the crooked bend never opens a gap.
            k.Prism(new Vector3(0f, -0.07f, 0f), Quaternion.identity, 0.198f, 0.122f, 0.38f, 14, HatColor);
            k.Smooth = false; // a darned tear
            k.Box(new Vector3(0.11f, 0.16f, 0.115f), new Vector3(0.008f, 0.07f, 0.006f), Quaternion.LookRotation(new Vector3(0.69f, 0f, 0.72f)) * Quaternion.Euler(-12f, 0f, 20f), MudPalette.Cream);
            k.Box(new Vector3(0.11f, 0.16f, 0.115f), new Vector3(0.03f, 0.006f, 0.006f), Quaternion.LookRotation(new Vector3(0.69f, 0f, 0.72f)) * Quaternion.Euler(-12f, 0f, 20f), MudPalette.Cream);
        }

        private static void HatTipMesh(MeshKit k)
        {
            k.Smooth = true;
            k.Prism(new Vector3(0f, -0.06f, 0f), Quaternion.identity, 0.134f, 0.014f, 0.43f, 12, HatColor);
            k.Ball(new Vector3(0f, 0.37f, 0f), 0.016f, HatColor, 1);
            k.Smooth = false;
            k.Rod(new Vector3(0f, 0.36f, 0f), new Vector3(0f, 0.29f, 0.05f), 0.004f, 3, Gold); // a thread...
            Star(k, new Vector3(0f, 0.25f, 0.06f), 0.045f, 0.015f, Hex, 2.5f);                // ...with a lucky star on it
        }

        // ---------- Arms, hands, legs ----------

        private Transform BuildArm(float side, out Transform elbow, out Transform hand)
        {
            Transform shoulder = MudArt.Pivot(spine, side < 0f ? "Arm L" : "Arm R", new Vector3(ShoulderAt.x * side, ShoulderAt.y, ShoulderAt.z));
            MudArt.Part(shoulder, "Sleeve", Vector3.zero, Quaternion.identity, "witch2_upper", UpperArmMesh);
            elbow = MudArt.Pivot(shoulder, "Elbow", new Vector3(0f, -UpperArm, 0f));
            MudArt.Part(elbow, "Forearm", Vector3.zero, Quaternion.identity, "witch2_fore", ForearmMesh);
            hand = MudArt.Pivot(elbow, "Hand", new Vector3(0f, -Forearm, 0f));
            MudArt.Part(hand, "Paw", Vector3.zero, Quaternion.identity, "witch2_hand", HandMesh);
            return shoulder;
        }

        private static void UpperArmMesh(MeshKit k)
        {
            k.Smooth = true;
            k.Ball(new Vector3(0f, -0.02f, 0f), 0.115f, Robe, 1);
            k.Rod(Vector3.zero, new Vector3(0f, -UpperArm, 0f), 0.1f, 12, Robe, 0f, 0.082f);
            k.Ball(new Vector3(0f, -UpperArm, 0f), 0.083f, Robe, 1);
        }

        private static void ForearmMesh(MeshKit k)
        {
            // A witch's bell sleeve (open, lined), with the long furry forearm inside it.
            var bell = new[] { new Vector2(0.084f, 0f), new Vector2(0.095f, 0.08f), new Vector2(0.13f, 0.17f), new Vector2(0.18f, 0.25f), new Vector2(0.205f, 0.27f) };
            Quaternion down = Quaternion.Euler(180f, 0f, 0f);
            k.Smooth = true;
            k.Lathe(Vector3.zero, down, bell, 16, Robe);
            k.Lathe(Vector3.zero, down, Inner(bell, 0.93f), 16, Lining);
            k.Rod(new Vector3(0f, -0.1f, 0f), new Vector3(0f, -Forearm + 0.03f, 0f), 0.062f, 10, Fur, 0f, 0.052f);
            k.Ball(new Vector3(0f, -0.3f, 0f), new Vector3(0.068f, 0.05f, 0.068f), Quaternion.identity, FurLight, 1);
            k.Smooth = false;
            for (int i = 0; i < 10; i++) // ragged cuff
            {
                float a0 = i / 10f * Mathf.PI * 2f, a1 = (i + 1) / 10f * Mathf.PI * 2f, am = (a0 + a1) * 0.5f;
                TwoTri(k, new Vector3(Mathf.Cos(a0) * 0.205f, -0.27f, Mathf.Sin(a0) * 0.205f), new Vector3(Mathf.Cos(a1) * 0.205f, -0.27f, Mathf.Sin(a1) * 0.205f),
                    new Vector3(Mathf.Cos(am) * 0.215f, -0.31f - 0.02f * (i % 2), Mathf.Sin(am) * 0.215f), Robe, Lining);
            }
        }

        // Fingers point down the hand's -Y; the three long sloth claws curl toward the palm (+Z).
        private static void HandMesh(MeshKit k)
        {
            k.Smooth = true;
            k.Ball(new Vector3(0f, -0.055f, 0.008f), new Vector3(0.07f, 0.075f, 0.05f), Quaternion.identity, Fur, 1);
            k.Ball(new Vector3(0f, -0.1f, 0.022f), new Vector3(0.072f, 0.032f, 0.045f), Quaternion.identity, FurDark, 1);
            k.Smooth = false;
            for (int i = -1; i <= 1; i++)
            {
                float x = i * 0.035f;
                Vector3 a = new Vector3(x, -0.108f, 0.03f), b = new Vector3(x * 1.12f, -0.195f, 0.065f), c = new Vector3(x * 1.18f, -0.245f, 0.125f);
                k.Rod(a, b, 0.017f, 6, Claw, 0f, 0.013f);
                k.Ball(b, 0.013f, Claw, 0);
                k.Rod(b, c, 0.013f, 6, Claw, 0f, 0.002f);
            }
        }

        private static void WandMesh(MeshKit k)
        {
            k.Smooth = true;
            k.Tube(new[] { new Vector3(0f, 0f, -0.09f), new Vector3(0.008f, 0.008f, 0.1f), new Vector3(-0.01f, 0.003f, 0.27f), new Vector3(0.006f, -0.004f, 0.44f) },
                0.016f, 8, MudPalette.DarkWood);
            k.Ball(new Vector3(0.008f, 0.012f, 0.18f), 0.02f, MudPalette.DarkWood, 1);
            k.Ball(new Vector3(0f, 0f, -0.095f), 0.022f, MudPalette.DarkWood, 1);
            k.Ball(new Vector3(0.004f, -0.002f, 0.5f), new Vector3(0.028f, 0.028f, 0.05f), Quaternion.identity, Hex, 1, 3f);
            k.Smooth = false;
            for (int i = 0; i < 3; i++) // twiggy prongs holding the crystal
            {
                float a = i * Mathf.PI * 2f / 3f;
                var o = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                k.Rod(new Vector3(0.006f, -0.004f, 0.44f) + o * 0.01f, new Vector3(0.004f, -0.002f, 0.5f) + o * 0.035f, 0.005f, 4, MudPalette.DarkWood, 0f, 0.002f);
            }
        }

        private Transform BuildLeg(float side, out Transform knee, out Transform foot)
        {
            Transform hip = MudArt.Pivot(body, side < 0f ? "Leg L" : "Leg R", new Vector3(HipJointAt.x * side, HipJointAt.y, HipJointAt.z));
            MudArt.Part(hip, "Thigh", Vector3.zero, Quaternion.identity, "witch2_thigh", k => Stocking(k, Thigh, 0.088f, 0.07f, Robe));
            knee = MudArt.Pivot(hip, "Knee", new Vector3(0f, -Thigh, 0f));
            MudArt.Part(knee, "Shin", Vector3.zero, Quaternion.identity, "witch2_shin", k => Stocking(k, Shin, 0.068f, 0.05f, StripeB));
            foot = MudArt.Pivot(knee, "Foot", new Vector3(0f, -Shin, 0f));
            MudArt.Part(foot, "Paw", Vector3.zero, Quaternion.identity, "witch2_foot", FootMesh);
            return hip;
        }

        // Striped stockings.
        private static void Stocking(MeshKit k, float length, float top, float bottom, Color joint)
        {
            k.Smooth = true;
            k.Ball(Vector3.zero, top * 1.04f, joint, 1);
            const int bands = 4;
            for (int i = 0; i < bands; i++)
            {
                float y0 = -length * i / bands, y1 = -length * (i + 1) / bands;
                k.Prism(new Vector3(0f, y1, 0f), Quaternion.identity, Mathf.Lerp(top, bottom, (i + 1f) / bands), Mathf.Lerp(top, bottom, (float)i / bands),
                    y0 - y1, 12, i % 2 == 0 ? StripeA : StripeB);
            }
        }

        // Toes along +Z, sole at y = -Ankle; big hooked claws.
        private static void FootMesh(MeshKit k)
        {
            k.Smooth = true;
            k.Ball(new Vector3(0f, -0.015f, 0f), new Vector3(0.066f, 0.055f, 0.066f), Quaternion.identity, FurLight, 1);
            k.Ball(new Vector3(0f, -0.042f, 0.06f), new Vector3(0.075f, 0.043f, 0.12f), Quaternion.identity, Fur, 1);
            k.Smooth = false;
            for (int i = -1; i <= 1; i++)
            {
                float x = i * 0.034f;
                Vector3 a = new Vector3(x, -0.04f, 0.155f), b = new Vector3(x * 1.2f, -0.05f, 0.225f), c = new Vector3(x * 1.25f, -0.083f, 0.255f);
                k.Rod(a, b, 0.014f, 6, Claw, 0f, 0.01f);
                k.Rod(b, c, 0.01f, 6, Claw, 0f, 0.0015f);
            }
        }

        protected override Action<MeshKit> KeyItemModel() => k =>
        {
            k.Ball(Vector3.zero, 0.15f, new Color(0.5f, 1f, 0.35f), 1, 3f);
            k.Cone(new Vector3(0f, 0.1f, 0f), Quaternion.identity, 0.1f, 0.25f, 5, new Color(0.4f, 0.9f, 0.3f), 2f);
        };

        // ---------- Mesh helpers ----------

        private static void TwoTri(MeshKit k, Vector3 a, Vector3 b, Vector3 c, Color front, Color back)
        {
            k.Tri(a, b, c, front);
            k.Tri(a, c, b, back);
        }

        private static void TwoQuad(MeshKit k, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color front, Color back)
        {
            k.Quad(a, b, c, d, front);
            k.Quad(a, d, c, b, back);
        }

        // A lathe profile turned inside out (and a touch smaller): the lining of a sleeve or skirt.
        private static Vector2[] Inner(Vector2[] profile, float scale)
        {
            var inner = new Vector2[profile.Length];
            for (int i = 0; i < profile.Length; i++)
            {
                Vector2 p = profile[profile.Length - 1 - i];
                inner[i] = new Vector2(p.x * scale, p.y);
            }
            return inner;
        }

        // A puffy five-pointed star facing +Z.
        private static void Star(MeshKit k, Vector3 centre, float radius, float depth, Color color, float glow)
        {
            var ring = new Vector3[10];
            for (int i = 0; i < 10; i++)
            {
                float a = Mathf.PI * 0.5f + i * Mathf.PI / 5f, r = i % 2 == 0 ? radius : radius * 0.45f;
                ring[i] = centre + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
            }
            Vector3 front = centre + Vector3.forward * depth, back = centre - Vector3.forward * depth;
            for (int i = 0; i < 10; i++)
            {
                int j = (i + 1) % 10;
                k.Tri(front, ring[j], ring[i], color, glow);
                k.Tri(front, ring[i], ring[j], color, glow);
                k.Tri(back, ring[i], ring[j], color, glow);
                k.Tri(back, ring[j], ring[i], color, glow);
            }
        }

        // =====================================================================================================
        // Entrance
        // =====================================================================================================

        public override IEnumerator Entrance(BossEntrance cine)
        {
            Vector3 home = transform.parent != null ? transform.parent.position : transform.position;
            Vector3 toPlayer = (cine.PlayerPosition - home).Flatten().normalized;
            if (toPlayer.sqrMagnitude < 0.01f) toPlayer = Vector3.back;
            Quaternion facing = Quaternion.LookRotation(toPlayer);
            var lanterns = new List<Light>(transform.parent != null ? transform.parent.GetComponentsInChildren<Light>() : new Light[0]);
            lanterns.RemoveAll(light => light.transform.IsChildOf(transform)); // not her own broom lantern
            cine.Cut(home + toPlayer * 16f + Vector3.up * 4f, home + Vector3.up * 2f);
            // The clearing's lanterns go out one by one.
            foreach (Light lantern in lanterns)
            {
                if (cine.Skipped) break;
                lantern.enabled = false;
                Sfx.Play("ui_close", 0.5f, 0.7f);
                yield return cine.Wait(0.25f);
            }
            // She waits deep in the cauldron (its shell hides her), broom and all.
            AttachBroom();
            broom.gameObject.SetActive(false);
            transform.SetPositionAndRotation(home + Vector3.down * 2.4f, facing);
            HoldWandInTeeth(true);
            Face?.Show(Mood.Sleepy, 14f);
            yield return cine.Move(home + toPlayer * 8f + Vector3.up * 2.5f, home + Vector3.up * 1.5f, 1f);
            // The cauldron bubbles harder and harder...
            Sfx.Play("magic_charge", 0.8f, 0.6f);
            Vector3 surface = home + Vector3.up * Surface;
            entranceSurface = surface;
            var glow = new GameObject("Cauldron Glow").AddComponent<Light>();
            glow.transform.position = surface + Vector3.up * 0.6f;
            glow.type = LightType.Point;
            glow.color = Hex;
            glow.range = 9f;
            glow.intensity = 0f;
            for (float t = 0f; t < 0.9f && !cine.Tick(); t += Time.deltaTime)
            {
                glow.intensity = t * 4f;
                if (Random.value < 0.6f) Bubble(surface, 0.5f + t);
                yield return null;
            }
            // ...and erupts. Her broom comes out first, and settles across the pot.
            Sfx.Play("mud_splat", 1f, 0.6f);
            ParticleBurst.Spawn(ParticleBurst.Kind.Mist, surface, Vector3.up, 45, 1.6f, 4f, 35f, new Color(0.55f, 1f, 0.5f, 0.75f));
            ParticleBurst.Spawn(ParticleBurst.Kind.Water, surface, Vector3.up, 30, 0.25f, 6f, 40f, new Color(0.45f, 0.95f, 0.35f, 0.9f));
            Vector3 bar = home + Vector3.up * (Surface + 0.36f);
            yield return BroomFromCauldron(cine, surface, bar, facing);
            glow.intensity = 2.4f;
            // Then two big claws come up out of the brew and hook over the handle: she hangs off it like any sloth off
            // a branch, wand in her teeth, and yawns.
            cine.LookAt(head, Vector3.zero);
            float hangY = bar.y - 2.02f, pullY = bar.y - 1.32f, seatY = bar.y - BroomRest.y;
            yield return Climb(cine, bar, facing, home.y - 2.4f, hangY, 1.3f, 0.03f, k => HangPose(broom.localPosition, broom.localRotation, Time.time, 0f));
            Face?.Show(Mood.Sleepy, 3f);
            Say("*yaaawn*", Sleepy, 1.6f, 4f, 2.2f);
            yield return Climb(cine, bar, facing, hangY, hangY, 1.1f, 0.05f, k => HangPose(broom.localPosition, broom.localRotation, Time.time, 0f));
            // A slow sloth pull-up...
            yield return Climb(cine, bar, facing, hangY, pullY, 1.3f, 0.05f,
                k => Pose.Lerp(HangPose(broom.localPosition, broom.localRotation, Time.time, 0f), HangPose(broom.localPosition, broom.localRotation, Time.time, 1f), k));
            // ...a leg over, and she plonks down on it.
            yield return Climb(cine, bar, facing, pullY, seatY, 0.75f, 0.03f,
                k => LegOver(HangPose(broom.localPosition, broom.localRotation, Time.time, 1f), RidePose(broom.localPosition, broom.localRotation, Time.time), k));
            Sfx.Play("player_land", 0.8f, 0.7f);
            ParticleBurst.Spawn(ParticleBurst.Kind.Mist, bar, Vector3.up, 20, 1f, 2f, 60f, new Color(0.55f, 1f, 0.5f, 0.6f));
            for (float t = 0f; t < 0.4f && !cine.Tick(); t += Time.deltaTime)
            {
                HoldBroom(bar, facing, -Mathf.Sin(t / 0.4f * Mathf.PI) * 0.1f); // the broom sags under her
                PoseNow(RidePose(broom.localPosition, broom.localRotation, Time.time), 16f, Time.deltaTime, false);
                StepJiggles(Time.deltaTime);
                yield return null;
            }
            HoldWandInTeeth(false);
            // A very slow blink...
            Face?.Show(Mood.Sleepy, 1.4f);
            yield return Hold(cine, bar, facing, 1.2f, false);
            Face?.Show(Mood.Laugh, 2f);
            Destroy(glow.gameObject, 0.1f);
            // ...then a cackle at full speed, and the lanterns come back green.
            Sfx.Play("cackle", 1f);
            Say("HEHEHEHEHE!", Hex, 1.6f, 6f, 2.2f);
            foreach (Light lantern in lanterns)
            {
                lantern.color = Hex;
                lantern.enabled = true;
            }
            yield return Hold(cine, bar, facing, 1.2f, true);
            // And up she goes, on her broom.
            Vector3 seat = new Vector3(bar.x, seatY, bar.z), top = FightStart(home);
            Sfx.Play("magic_charge", 0.6f, 1.3f);
            ParticleBurst.Spawn(ParticleBurst.Kind.Mist, bar, Vector3.down, 25, 1f, 3f, 70f, Arcane);
            for (float t = 0f; t < 1.1f && !cine.Tick(); t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / 1.1f);
                transform.position = Vector3.Lerp(seat, top, k);
                broom.localPosition = BroomRest;
                broom.localRotation = Quaternion.Euler(-12f * Mathf.Sin(k * Mathf.PI), 0f, 0f);
                PoseNow(RidePose(broom.localPosition, broom.localRotation, Time.time), 14f, Time.deltaTime, false);
                StepJiggles(Time.deltaTime);
                yield return null;
            }
            // A skip leaves her wherever she was: put her up where the fight starts, on the broom.
            if (cine.Skipped)
            {
                transform.SetPositionAndRotation(top, facing);
                HoldWandInTeeth(false);
            }
            broom.gameObject.SetActive(true);
            AttachBroom();
            PoseNow(RidePose(BroomRest, Quaternion.identity, Time.time), 0f, 0f, true, true);
        }

        private static void Bubble(Vector3 surface, float size)
        {
            Vector3 at = surface + Random.insideUnitSphere.Flatten() * 0.8f;
            ParticleBurst.Spawn(ParticleBurst.Kind.Mist, at, Vector3.up, 2, 0.35f * size, 1.2f, 20f, new Color(0.5f, 1f, 0.45f, 0.7f));
            if (Random.value < 0.3f) ParticleBurst.Spawn(ParticleBurst.Kind.Water, at, Vector3.up, 4, 0.08f, 2.5f, 30f, new Color(0.45f, 0.95f, 0.35f, 0.9f));
        }

        // The broom shoots out of the pot on end (it only fits that way), spinning, flips flat at the top and drops
        // to lie across the cauldron, bobbing.
        private IEnumerator BroomFromCauldron(BossEntrance cine, Vector3 surface, Vector3 bar, Quaternion facing)
        {
            Vector3 deep = surface + Vector3.down * 1.3f, high = bar + Vector3.up * 2.6f;
            Quaternion upright = facing * Quaternion.Euler(-90f, 0f, 0f);
            broom.gameObject.SetActive(true);
            broom.SetPositionAndRotation(deep, upright);
            Sfx.Play("chain_swish", 0.9f, 0.7f);
            ParticleBurst.Spawn(ParticleBurst.Kind.Mist, surface, Vector3.up, 25, 1.2f, 5f, 25f, new Color(0.55f, 1f, 0.5f, 0.7f));
            const float rise = 0.55f, flip = 0.5f, settle = 0.45f;
            for (float t = 0f; t < rise && !cine.Tick(); t += Time.deltaTime)
            {
                float k = 1f - (1f - t / rise) * (1f - t / rise); // ease out
                broom.SetPositionAndRotation(Vector3.Lerp(deep, high, k), upright * Quaternion.Euler(0f, 0f, 720f * k));
                StepJiggles(Time.deltaTime);
                yield return null;
            }
            for (float t = 0f; t < flip && !cine.Tick(); t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / flip);
                broom.SetPositionAndRotation(Vector3.Lerp(high, bar + Vector3.up * 0.7f, k), Quaternion.Slerp(upright, facing, k) * Quaternion.Euler(0f, 0f, 360f * k));
                StepJiggles(Time.deltaTime);
                yield return null;
            }
            for (float t = 0f; t < settle && !cine.Tick(); t += Time.deltaTime)
            {
                float k = t / settle;
                HoldBroom(bar, facing, 0.7f * (1f - k) * (1f - k) - 0.12f * Mathf.Sin(k * Mathf.PI));
                StepJiggles(Time.deltaTime);
                yield return null;
            }
            HoldBroom(bar, facing, 0f);
            ParticleBurst.Spawn(ParticleBurst.Kind.Mist, broom.position, Vector3.zero, 12, 0.6f, 1.5f, 80f, Arcane);
            Sfx.Play("magic_charge", 0.5f, 1.4f);
        }

        private void HoldBroom(Vector3 bar, Quaternion facing, float dip)
        {
            float t = Time.time;
            broom.SetPositionAndRotation(bar + Vector3.up * dip, facing * Quaternion.Euler(Mathf.Sin(t * 1.3f) * 2f, 0f, Mathf.Sin(t * 1.7f) * 3f));
        }

        // One stretch of the climb: her body moves from one height to another while the pose follows the broom.
        private IEnumerator Climb(BossEntrance cine, Vector3 bar, Quaternion facing, float fromY, float toY, float seconds, float sag, Func<float, Pose> posed)
        {
            for (float t = 0f; t < seconds && !cine.Tick(); t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / seconds);
                transform.position = new Vector3(bar.x, Mathf.Lerp(fromY, toY, k), bar.z);
                HoldBroom(bar, facing, -sag + Mathf.Sin(Time.time * 2.3f) * 0.015f);
                PoseNow(posed(k), 18f, Time.deltaTime, false);
                StepJiggles(Time.deltaTime);
                // A thick collar of steam on the brew, so nothing is seen passing through it.
                if (Random.value < 0.7f)
                    ParticleBurst.Spawn(ParticleBurst.Kind.Mist, entranceSurface + Random.insideUnitSphere.Flatten() * 0.7f, Vector3.up, 3, 1.1f, 1.2f, 30f, new Color(0.55f, 1f, 0.5f, 0.55f));
                yield return null;
            }
        }

        private IEnumerator Hold(BossEntrance cine, Vector3 bar, Quaternion facing, float seconds, bool flourish)
        {
            for (float t = 0f; t < seconds && !cine.Tick(); t += Time.deltaTime)
            {
                HoldBroom(bar, facing, Mathf.Sin(Time.time * 1.6f) * 0.03f);
                Pose p = RidePose(broom.localPosition, broom.localRotation, Time.time);
                if (flourish) Flourish(ref p, broom.localPosition, broom.localRotation);
                PoseNow(p, 12f, Time.deltaTime, false);
                StepJiggles(Time.deltaTime);
                yield return null;
            }
        }

        // While climbing she carries the wand in her teeth.
        private void HoldWandInTeeth(bool teeth)
        {
            if (wand == null) return;
            if (teeth)
            {
                wand.SetParent(head, false);
                wand.localPosition = HeadCentre + new Vector3(-0.2f, -0.14f, 0.27f);
                wand.localRotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
            }
            else
            {
                wand.SetParent(handR, false);
                wand.localPosition = WandGrip;
                wand.localRotation = Quaternion.LookRotation(WandLocal, Vector3.up);
            }
        }

        // =====================================================================================================
        // The fight
        // =====================================================================================================

        protected override void OnBossStarted()
        {
            if (Agent != null) Agent.enabled = false; // she flies
            MakeBroomTarget();
            playerFlask = Target != null ? Target.GetComponentInParent<PlayerFlask>() : null;
            nextHex = Time.time + 1.5f;
            nextSpores = Time.time + 5f;
            nextRain = Time.time + 8f;
            nextClones = Time.time + 10f;
            Enter(State.Fly);
        }

        private void MakeBroomTarget()
        {
            var hit = broom.gameObject.AddComponent<BoxCollider>();
            hit.size = new Vector3(0.45f, 0.4f, 2.9f);
            hit.center = new Vector3(0f, 0f, -0.15f);
            var health = broom.gameObject.AddComponent<Health>();
            health.SetMax(1000f);
            health.Restore();
            health.Damaged += (h, amount, source) =>
            {
                h.Restore();
                OnBroomHit();
            };
        }

        protected override void UpdatePatrol(float dt)
        {
            Hover(dt, 7f);
            if (Time.time < nextSnore) return;
            nextSnore = Time.time + 2.4f;
            Emote.Show(transform, "z", Sleepy, 2.6f, 1.6f, 3f); // dozing on the job
        }

        private void Enter(State next)
        {
            state = next;
            stateTime = 0f;
        }

        // ---------- Flying ----------

        private void Hover(float dt, float height)
        {
            orbit += dt * 0.25f; // lazy circles
            Vector3 centre = ArenaCentre + new Vector3(Mathf.Cos(orbit), 0f, Mathf.Sin(orbit)) * 11f;
            Vector3 target = centre + Vector3.up * (height + Mathf.Sin(Time.time * 0.8f + BossSeed) * 1f);
            transform.position = Vector3.MoveTowards(transform.position, target, dt * 4f);
            Vector3 face = Target != null && Engaged ? (PlayerPosition - transform.position).Flatten() : new Vector3(-Mathf.Sin(orbit), 0f, Mathf.Cos(orbit));
            if (face.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(face), dt * 2f);
        }

        protected override void TickCombat(float dt)
        {
            stateTime += dt;
            UpdateShove(dt);
            UpdateSpores();
            switch (state)
            {
                case State.Fly:
                    Hover(dt, 7.5f);
                    if (Time.time >= nextClones) { StartClones(); break; }
                    if (Time.time >= nextRain) { Enter(State.Rain); Say("Stir, stir...", Hex, 1.2f, 4f); break; }
                    if (Time.time >= nextSpores) { Enter(State.Spores); break; }
                    if (Time.time >= nextHex)
                    {
                        Enter(State.Hex);
                        hexFired = 0f;
                        lastHexAt = -10f;
                        Sfx.PlayAt("magic_charge", transform.position, 0.5f, 1.5f, 40f);
                    }
                    break;
                case State.Hex:
                    Hover(dt, 7.5f);
                    if (stateTime > 0.5f + hexFired * 0.4f && hexFired < HexCount)
                    {
                        hexFired++;
                        lastHexAt = stateTime;
                        HexOrb.Fire(wandTip.position, gameObject, Phase == 2 ? 8.5f : 7f);
                        ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, wandTip.position, Vector3.zero, 10, 0.07f, 3f, 80f, Hex);
                    }
                    if (hexFired >= HexCount && stateTime > lastHexAt + 0.3f) { Enter(State.Fly); nextHex = Time.time + (Phase == 2 ? 3f : 4f); }
                    break;
                case State.Spores:
                    Hover(dt, 7.5f);
                    if (stateTime < 0.6f) break;
                    SporeCloud.Drop(handL.position, Ground(PlayerPosition), gameObject); // a pod from her pouch
                    nextSpores = Time.time + 9f;
                    Enter(State.Fly);
                    break;
                case State.Rain: UpdateRain(dt); break;
                case State.Clones: UpdateClones(dt); break;
                case State.Wobble:
                    Hover(dt, 6f);
                    if (stateTime > 1.2f) Enter(State.Fly);
                    break;
                default: TickGround(dt); break;
            }
        }

        // Everything she does off the broom (falling, lying there, getting up, walking back, climbing on).
        private void TickGround(float dt)
        {
            switch (state)
            {
                case State.Falling:
                    if (FallStep(dt)) Land();
                    break;
                case State.Dazed:
                    Halt();
                    if (stateTime > (dazedLong ? 2.5f : 1.5f)) Enter(State.GetUp);
                    break;
                case State.GetUp:
                    Halt();
                    if (stateTime < GetUpTime) break;
                    Say("Oh, bother.", Sleepy, 1.6f, 4f);
                    StartWalk();
                    break;
                case State.Walk: UpdateWalk(dt); break;
                case State.Call: UpdateCall(dt); break;
                case State.Mount: UpdateMount(dt); break;
            }
        }

        // ---------- Attacks ----------

        private void UpdateRain(float dt)
        {
            Hover(dt, 7.5f);
            Vector3 pot = ArenaCentre + Vector3.up * (Surface + 0.1f);
            if (stateTime < 0.8f)
            {
                if (Random.value < 0.4f) ParticleBurst.Spawn(ParticleBurst.Kind.Mist, pot, Vector3.up, 4, 0.8f, 3f, 30f, new Color(0.55f, 1f, 0.5f, 0.7f));
                return;
            }
            for (int i = 0; i < 5; i++)
            {
                Vector2 spread = Random.insideUnitCircle * 5f;
                Vector3 land = Ground(PlayerPosition + new Vector3(spread.x, 0f, spread.y) * (i == 0 ? 0.1f : 1f));
                Transform glob = MudArt.Part(null, "Potion Glob", Vector3.zero, Quaternion.identity, "witch_glob", k => k.Ball(Vector3.zero, 0.3f, Hex, 1, 2f));
                Lob.Throw(glob, pot, land, 1.2f + i * 0.12f, 7f, at =>
                {
                    ParticleBurst.Spawn(ParticleBurst.Kind.Mist, at, Vector3.up, 14, 0.8f, 3f, 70f, new Color(0.55f, 1f, 0.45f, 0.8f));
                    Sfx.PlayAt("hex_zap", at, 0.6f, 0.8f, 30f);
                    if (TargetAlive && (PlayerPosition - at).Flatten().magnitude < 1.7f) HurtPlayer(12f);
                }, 1.6f, new Color(0.5f, 1f, 0.35f, 0.5f));
            }
            Sfx.PlayAt("mud_splat", pot, 1f, 0.7f, 40f);
            nextRain = Time.time + 12f;
            flourishUntil = Time.time + 0.6f; // arms up as the pot spits
            Enter(State.Fly);
        }

        private void StartClones()
        {
            Enter(State.Clones);
            beamLocked = false;
            int count = Phase == 2 ? 5 : 3;
            Sfx.PlayAt("cackle", transform.position, 1f, 1.2f, 60f);
            Say("Which one am I?", Hex, 1.4f, 4f);
            // The real witch takes one spot at random; the clones (no hats, can't be hurt) take the rest.
            int real = Random.Range(0, count);
            float start = Random.value * 360f;
            for (int i = 0; i < count; i++)
            {
                float angle = (start + i * 360f / count) * Mathf.Deg2Rad;
                Vector3 spot = PlayerPosition + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 11f + Vector3.up * 4.5f;
                if (i == real)
                {
                    ParticleBurst.Spawn(ParticleBurst.Kind.Mist, transform.position, Vector3.zero, 30, 1f, 3f, 80f, Arcane);
                    transform.position = spot;
                    continue;
                }
                Transform copy = Instantiate(Visual.gameObject, spot, Quaternion.identity).transform;
                copy.name = "Witch Clone";
                foreach (MonoBehaviour behaviour in copy.GetComponentsInChildren<MonoBehaviour>()) Destroy(behaviour);
                foreach (Collider collider in copy.GetComponentsInChildren<Collider>()) Destroy(collider);
                foreach (Light light in copy.GetComponentsInChildren<Light>()) light.enabled = false;
                Transform copyHat = FindChild(copy, "Witch Hat");
                if (copyHat != null) Destroy(copyHat.gameObject);
                // A bolt that hits a clone fizzles: it has a body to hit, but can't be hurt.
                var shape = copy.gameObject.AddComponent<CapsuleCollider>();
                shape.center = new Vector3(0f, 1.2f, 0f);
                shape.height = 2.2f;
                shape.radius = 0.5f;
                var health = copy.gameObject.AddComponent<Health>();
                health.Invulnerable = true;
                health.Blocked += (h, a, s) => ParticleBurst.Spawn(ParticleBurst.Kind.Mist, h.transform.position + Vector3.up, Vector3.up, 10, 0.5f, 2f, 60f, Arcane);
                clones.Add(copy);
                LinkClone(copy);
                ParticleBurst.Spawn(ParticleBurst.Kind.Mist, spot, Vector3.zero, 30, 1f, 3f, 80f, Arcane);
            }
            beamRing = GroundRing.Spawn(PlayerPosition, 2f, new Color(0.6f, 1f, 0.35f, 0.5f));
            Sfx.PlayAt("magic_charge", transform.position, 1f, 0.8f, 60f);
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) if (child.name == name) return child;
            return null;
        }

        // The clones move exactly as she does: each joint of a copy follows the same joint of hers.
        private void LinkClone(Transform copy)
        {
            foreach (Transform real in Visual.GetComponentsInChildren<Transform>(true))
            {
                if (real == Visual) continue;
                Transform twin = copy.Find(PathUnder(Visual, real));
                if (twin != null) cloneLinks.Add((real, twin));
            }
        }

        private static string PathUnder(Transform root, Transform t)
        {
            string path = t.name;
            for (Transform p = t.parent; p != null && p != root; p = p.parent) path = p.name + "/" + path;
            return path;
        }

        private void SyncClones()
        {
            foreach ((Transform real, Transform copy) in cloneLinks)
            {
                if (real == null || copy == null) continue;
                copy.localPosition = real.localPosition;
                copy.localRotation = real.localRotation;
            }
        }

        private void UpdateClones(float dt)
        {
            const float charge = 3f;
            float k = stateTime / charge;
            Vector3 face = (PlayerPosition - transform.position).Flatten();
            if (face.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(face);
            foreach (Transform clone in clones)
            {
                if (clone == null) continue;
                Vector3 cf = (PlayerPosition - clone.position).Flatten();
                if (cf.sqrMagnitude > 0.01f) clone.rotation = Quaternion.LookRotation(cf);
            }
            if (!beamLocked)
            {
                beamLock = PlayerPosition;
                if (beamRing != null) beamRing.MoveTo(Ground(beamLock));
                if (k >= 0.8f) { beamLocked = true; Sfx.PlayAt("ui_click", transform.position, 1f, 1.6f, 60f); }
            }
            if (beamRing != null) beamRing.Progress = k;
            if (Random.value < 0.5f)
            {
                ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, wandTip.position, Vector3.zero, 2, 0.08f, 1f, 80f, Hex);
                foreach (Transform clone in clones)
                {
                    Transform tip = clone != null ? FindChild(clone, "Wand Tip") : null;
                    if (tip != null) ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, tip.position, Vector3.zero, 2, 0.08f, 1f, 80f, Hex);
                }
            }
            if (k < 1f) return;
            // Fire: every caster's beam lands on the locked spot.
            int beams = clones.Count + 1;
            Beam(wandTip.position, beamLock);
            foreach (Transform clone in clones)
            {
                Transform tip = clone != null ? FindChild(clone, "Wand Tip") : null;
                if (tip != null) Beam(tip.position, beamLock);
            }
            CameraShake.Add(0.4f);
            Sfx.PlayAt("hex_zap", beamLock, 1f, 0.6f, 60f);
            if (TargetAlive && (PlayerPosition - beamLock).Flatten().magnitude < 1.8f) HurtPlayer(15f * beams);
            EndClones(false);
        }

        private void EndClones(bool interrupted)
        {
            foreach (Transform clone in clones)
            {
                if (clone == null) continue;
                ParticleBurst.Spawn(ParticleBurst.Kind.Mist, clone.position + Vector3.up, Vector3.zero, 25, 1f, 3f, 80f, Arcane);
                Destroy(clone.gameObject);
            }
            clones.Clear();
            cloneLinks.Clear();
            if (beamRing != null) { beamRing.Finish(); beamRing = null; }
            nextClones = Time.time + (Phase == 2 ? 11f : 14f);
            if (IsDead) return;
            if (interrupted) { Enter(State.Wobble); Say("OW! My concentration!", Hex, 1.4f, 4f); }
            else if (state == State.Clones) Enter(State.Fly);
        }

        private static void Beam(Vector3 from, Vector3 to)
        {
            var go = new GameObject("Hex Beam");
            var line = go.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            line.widthMultiplier = 0.5f;
            line.sharedMaterial = EffectMaterials.Additive(EffectMaterials.Look.Beam);
            line.startColor = line.endColor = Hex;
            Destroy(go, 0.35f);
            ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, to, Vector3.up, 20, 0.1f, 6f, 80f, Hex);
        }

        // =====================================================================================================
        // Off the broom. She can't fly without it: knocked off, she falls, gets up and walks back to it.
        // =====================================================================================================

        private void KnockOff(Vector3 push)
        {
            if (!Riding) return;
            if (state == State.Clones) EndClones(false);
            Vector3 flat = push.Flatten();
            if (flat.sqrMagnitude > 1f) flat.Normalize();
            fallVelocity = flat * 4f + Vector3.up * 3f;
            tumbleSpin = RandomSpin(340f);
            fallTime = 0f;
            bounces = 0;
            dazedLong = false;
            // The broom spins off the other way and catches itself on open ground she can walk to.
            Vector3 away = (-flat + Random.insideUnitSphere.Flatten() * 0.6f).Flatten();
            if (away.sqrMagnitude < 0.01f) away = Random.insideUnitSphere.Flatten();
            away.Normalize();
            broomSpot = PickSpot(transform.position + away * 8f, transform.position, 5f, 11f, away);
            DetachBroom(away * 5f + Vector3.up * 3f, false);
            Enter(State.Falling);
            Say("WAAAH!", Hex, 1.2f, 5f);
            Sfx.PlayAt("cackle", transform.position, 0.8f, 0.6f, 50f);
        }

        // One frame of falling. She bounces off anything that isn't the ground (a roof, a treetop, a lantern post,
        // the cauldron's rim) instead of landing on top of it. Returns true once she's down on open ground.
        private bool FallStep(float dt)
        {
            fallTime += dt;
            fallVelocity += Physics.gravity * dt;
            Vector3 step = fallVelocity * dt;
            if (SolidCast(transform.position + Vector3.up * 0.6f, 0.4f, step, out RaycastHit hit))
            {
                transform.position += step.normalized * Mathf.Max(0f, hit.distance - 0.02f);
                if (hit.normal.y > 0.55f)
                {
                    if (hit.point.y - FloorAt(hit.point).y < 0.45f) return true; // the ground
                    BounceOff(hit);
                    return false;
                }
                // A wall or a trunk: glance off it.
                Vector3 slide = Vector3.ProjectOnPlane(fallVelocity, hit.normal) * 0.5f + hit.normal.Flatten() * 1.5f;
                slide.y = Mathf.Min(slide.y, 0f);
                fallVelocity = slide;
                Thump(hit.point, 0.5f);
                return false;
            }
            transform.position += step;
            if (transform.position.y <= FloorAt(transform.position).y && fallVelocity.y <= 0f) return true;
            if (fallTime > 4f || bounces > 4)
            {
                // Caught in a tangle somewhere: a puff of smoke and she's on the ground nearby.
                ParticleBurst.Spawn(ParticleBurst.Kind.Mist, transform.position + Vector3.up, Vector3.zero, 30, 1f, 3f, 80f, Arcane);
                transform.position = PickSpot(transform.position, transform.position, 0f, 3f, Vector3.zero);
                return true;
            }
            return false;
        }

        private void BounceOff(RaycastHit hit)
        {
            bounces++;
            Vector3 away = (transform.position - hit.collider.bounds.center).Flatten();
            Vector3 toFloor = (PickSpot(transform.position, transform.position, 0f, 4f, Vector3.zero) - transform.position).Flatten();
            Vector3 dir = (away.normalized + toFloor.normalized).Flatten();
            if (dir.sqrMagnitude < 0.01f) dir = away.sqrMagnitude > 0.01f ? away : transform.forward;
            fallVelocity = dir.normalized * 4.5f + Vector3.up * 3.5f;
            tumbleSpin = RandomSpin(380f);
            Thump(hit.point, 1f);
            Face?.Show(Mood.Hurt, 0.8f);
            if (bounces == 1) Say("OOF!", Sleepy, 1f, 5f);
        }

        private void Land()
        {
            transform.position = FloorAt(transform.position);
            fallVelocity = Vector3.zero;
            // She lands feet toward the broom, so she gets up facing it.
            Vector3 toBroom = (broomSpot - transform.position).Flatten();
            if (toBroom.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(toBroom);
            Thump(transform.position, 1.6f);
            Dust(transform.position, 35, 0.9f);
            GroundAgent();
            DamageTakenMultiplier = 1.5f;
            Enter(State.Dazed);
            Face?.Show(Mood.Hurt, 0.8f);
            Say("@_@", Sleepy, 1.4f, 5f);
        }

        private void Thump(Vector3 at, float strength)
        {
            Sfx.PlayAt("player_land", at, Mathf.Min(1f, 0.6f * strength), 0.75f, 35f);
            Dust(at, Mathf.RoundToInt(18f * strength), 0.6f);
            CameraShake.Add(0.12f * strength);
        }

        // Puts her NavMesh agent on the open floor right under or beside her (never a roof's or the cauldron's NavMesh).
        private bool GroundAgent()
        {
            if (Agent == null) return false;
            if (AgentReady) return true;
            Vector3 here = transform.position;
            for (int i = 0; i < 9; i++)
            {
                Vector3 probe = i == 0 ? here : here + Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward * 1.2f;
                if (!NavMesh.SamplePosition(probe, out NavMeshHit hit, 1.2f, NavMesh.AllAreas)) continue;
                if (Mathf.Abs(hit.position.y - FloorAt(hit.position).y) > 0.35f) continue;
                transform.position = hit.position;
                Agent.enabled = true;
                Agent.Warp(hit.position);
                return AgentReady;
            }
            return false;
        }

        private void StartWalk()
        {
            Enter(State.Walk);
            walkBest = float.MaxValue;
            walkProgressAt = Time.time;
            if (!AgentReady && !GroundAgent()) CallBroom();
        }

        private void UpdateWalk(float dt)
        {
            if (broomMode == BroomMode.Tumble) { Halt(); return; } // it's still spinning: wait and see where it ends up
            Vector3 goal = broomMode == BroomMode.Hover ? FloorAt(broom.position) : broomSpot;
            float distance = Flat(goal - transform.position);
            if (broomMode == BroomMode.Hover && distance < 0.95f) { StartMount(); return; }
            if (!AgentReady) { CallBroom(); return; }
            Chase(goal, WalkSpeed); // as fast as a sloth goes
            if (distance < walkBest - 0.25f)
            {
                walkBest = distance;
                walkProgressAt = Time.time;
            }
            // Can't get there (a partial path), or not getting any closer: whistle it over instead.
            bool hopeless = stateTime > 0.4f && !Agent.pathPending && Agent.pathStatus != NavMeshPathStatus.PathComplete;
            if (hopeless || Time.time - walkProgressAt > 2.5f) CallBroom();
        }

        private void CallBroom()
        {
            Halt();
            Enter(State.Call);
            Say("Here, broom!", Sleepy, 1.4f, 4f);
            broomSpot = transform.position + transform.forward * 0.5f;
            SendBroom(broomSpot, 3.5f);
            Sfx.PlayAt("magic_charge", transform.position, 0.5f, 1.8f, 40f);
        }

        private void UpdateCall(float dt)
        {
            Halt();
            TurnToward(broom.position, 180f * dt);
            if (broomMode == BroomMode.Hover) { StartMount(); return; }
            if (stateTime > 4f) // never leave her stranded
            {
                broom.position = broomTo;
                broomMode = BroomMode.Hover;
                broomTime = 0f;
            }
        }

        // She grabs the handle, the broom slides in under her and lifts her off her feet, sags, and takes off.
        private void StartMount()
        {
            Halt();
            if (Agent != null) Agent.enabled = false;
            broom.SetParent(Visual, true);
            broom.localScale = Vector3.one;
            broomMode = BroomMode.Riding;
            mountFrom = broom.localPosition;
            mountFromRot = broom.localRotation;
            pose.Broom = mountFrom;
            pose.BroomRot = mountFromRot;
            liftedOff = false;
            Enter(State.Mount);
            Say("Up we go...", Sleepy, 1.4f, 4f);
        }

        private void UpdateMount(float dt)
        {
            if (stateTime < MountRiseStart) return;
            if (!liftedOff)
            {
                liftedOff = true;
                Dust(transform.position, 30, 0.8f);
                ParticleBurst.Spawn(ParticleBurst.Kind.Mist, transform.position + Vector3.up * 0.4f, Vector3.down, 20, 0.8f, 2f, 70f, Arcane);
                Sfx.PlayAt("magic_charge", transform.position, 0.6f, 1.3f, 40f);
            }
            float k = Mathf.Clamp01((stateTime - MountRiseStart) / 0.5f);
            transform.position += Vector3.up * (5f * k * dt);
            if (stateTime > MountRiseStart + 1.2f)
            {
                Enter(State.Fly);
                DamageTakenMultiplier = 1f;
            }
        }

        // ---------- The broom on its own ----------

        private void OnBroomHit()
        {
            if (IsDead || BroomAttached || broomMode == BroomMode.Dropped) return;
            // Knocked further away while she walks back to it.
            Vector3 from = Target != null ? Target.position : transform.position;
            Vector3 away = (broom.position - from).Flatten();
            away = away.sqrMagnitude > 0.01f ? away.normalized : Random.insideUnitSphere.Flatten().normalized;
            broomSpot = PickSpot(broom.position + away * 8f, broom.position, 5f, 12f, away);
            broomVelocity = away * 6f + Vector3.up * 3.5f;
            broomSpin = RandomSpin(500f);
            broomMode = BroomMode.Tumble;
            broomTime = 0.35f; // a short spin, then it sorts itself out
            Say("MY BROOM!", Hex, 1.2f, 5f);
            Sfx.PlayAt("wood_smash", broom.position, 0.7f, 1.4f, 30f);
            ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, broom.position, Vector3.up, 14, 0.1f, 5f, 70f, new Color(0.7f, 0.5f, 1f));
            if (state == State.Call || state == State.Walk) StartWalk();
        }

        private void DetachBroom(Vector3 velocity, bool dead)
        {
            if (broom.parent != null)
            {
                broom.SetParent(null, true);
                broom.localScale = Vector3.one;
            }
            broomVelocity = velocity;
            broomSpin = RandomSpin(dead ? 200f : 450f);
            broomTime = 0f;
            broomMode = dead ? BroomMode.Dropped : BroomMode.Tumble;
        }

        private void AttachBroom()
        {
            broom.SetParent(Visual, false);
            broom.localPosition = BroomRest;
            broom.localRotation = Quaternion.identity;
            broom.localScale = Vector3.one;
            broomMode = BroomMode.Riding;
        }

        private void SendBroom(Vector3 spot, float arc)
        {
            broomFrom = broom.position;
            broomTo = spot + Vector3.up * MountHeight;
            broomFlight = Mathf.Clamp(Vector3.Distance(broomFrom, broomTo) / 7f + 0.35f, 0.5f, 2.5f);
            // Over anything in the way, not through it.
            if (SolidCast(broomFrom, 0.25f, broomTo - broomFrom, out _)) arc = Mathf.Max(arc, 6f);
            broomArc = arc;
            broomTime = 0f;
            broomMode = BroomMode.Flying;
        }

        private Vector3 CoilPoint() => broom.TransformPoint(new Vector3(0f, -0.11f, 0.52f));

        private void UpdateBroom(float dt)
        {
            if (dt <= 0f || broom == null) return;
            switch (broomMode)
            {
                case BroomMode.Tumble:
                    broomTime += dt;
                    broomVelocity += Physics.gravity * (0.5f * dt);
                    MoveBroom(broomVelocity * dt);
                    broom.Rotate(broomSpin * dt, Space.World);
                    if (broomTime >= 0.8f) SendBroom(broomSpot, 1.4f); // the coil catches
                    break;
                case BroomMode.Flying:
                {
                    broomTime += dt;
                    float k = Mathf.Clamp01(broomTime / broomFlight);
                    float e = Mathf.SmoothStep(0f, 1f, k);
                    Vector3 heading = (broomTo - broomFrom).Flatten();
                    broom.position = Vector3.Lerp(broomFrom, broomTo, e) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * broomArc);
                    Quaternion want = heading.sqrMagnitude > 0.01f
                        ? Quaternion.LookRotation(heading) * Quaternion.Euler(-15f * Mathf.Cos(k * Mathf.PI), 0f, 15f * Mathf.Sin(k * 2f * Mathf.PI))
                        : broom.rotation;
                    broom.rotation = Quaternion.Slerp(broom.rotation, want, 1f - Mathf.Exp(-6f * dt));
                    if (Random.value < 0.4f)
                        ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, CoilPoint(), -heading.normalized, 2, 0.06f, 1.5f, 40f, new Color(0.7f, 0.5f, 1f));
                    if (k >= 1f)
                    {
                        broomMode = BroomMode.Hover;
                        broomTime = 0f;
                    }
                    break;
                }
                case BroomMode.Hover:
                {
                    broomTime += dt;
                    broom.position = broomTo + Vector3.up * (Mathf.Sin(broomTime * 2.2f) * 0.035f);
                    Quaternion level = Quaternion.Euler(0f, broom.eulerAngles.y, Mathf.Sin(broomTime * 1.4f) * 4f);
                    broom.rotation = Quaternion.Slerp(broom.rotation, level, 1f - Mathf.Exp(-4f * dt));
                    if (Random.value < 0.06f)
                        ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, CoilPoint(), Vector3.down, 3, 0.05f, 1.2f, 50f, new Color(0.7f, 0.5f, 1f));
                    break;
                }
                case BroomMode.Dropped:
                {
                    float floor = FloorAt(broom.position).y + 0.06f;
                    if (broom.position.y > floor || broomVelocity.y > 0f)
                    {
                        broomVelocity += Physics.gravity * dt;
                        broom.position += broomVelocity * dt;
                        broom.Rotate(broomSpin * dt, Space.World);
                        if (broom.position.y <= floor)
                        {
                            broom.position = new Vector3(broom.position.x, floor, broom.position.z);
                            broomVelocity = Vector3.zero;
                            Sfx.PlayAt("wood_smash", broom.position, 0.6f, 0.9f, 30f);
                            Dust(broom.position, 15, 0.5f);
                        }
                    }
                    else broom.rotation = Quaternion.Slerp(broom.rotation, Quaternion.Euler(0f, broom.eulerAngles.y, 0f), 1f - Mathf.Exp(-10f * dt));
                    break;
                }
            }
        }

        private void MoveBroom(Vector3 delta)
        {
            Vector3 from = broom.position, to = from + delta;
            if (SolidCast(from, 0.3f, delta, out RaycastHit hit))
            {
                to = from + delta.normalized * Mathf.Max(0f, hit.distance - 0.02f);
                broomVelocity = Vector3.Reflect(broomVelocity, hit.normal) * 0.4f;
                Sfx.PlayAt("wood_smash", hit.point, 0.4f, 1.6f, 25f);
            }
            float floor = FloorAt(to).y + 0.25f;
            if (to.y < floor)
            {
                to.y = floor;
                if (broomVelocity.y < 0f) broomVelocity.y *= -0.3f;
            }
            broom.position = to;
        }

        private static Vector3 RandomSpin(float speed) => Random.onUnitSphere * speed;

        // ---------- The clearing: what's ground and what isn't ----------

        // Solid, still scenery: not her, her broom or clones, not anything alive or breakable, not moving things.
        private bool Solid(Collider c)
        {
            if (c == null || c.isTrigger || c.attachedRigidbody != null || c is CharacterController) return false;
            Transform t = c.transform;
            if (t.IsChildOf(transform) || (broom != null && t.IsChildOf(broom))) return false;
            foreach (Transform clone in clones) if (clone != null && t.IsChildOf(clone)) return false;
            return c.GetComponentInParent<Health>() == null;
        }

        // The terrain under a point: the lowest solid surface straight down (props all stand on it).
        private Vector3 FloorAt(Vector3 point)
        {
            int count = Physics.RaycastNonAlloc(point + Vector3.up * 40f, Vector3.down, Hits, 100f, ~0, QueryTriggerInteraction.Ignore);
            Vector3 floor = point;
            float lowest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (!Solid(Hits[i].collider) || Hits[i].point.y >= lowest) continue;
                lowest = Hits[i].point.y;
                floor = Hits[i].point;
            }
            return floor;
        }

        private bool SolidCast(Vector3 origin, float radius, Vector3 delta, out RaycastHit best)
        {
            best = default;
            float length = delta.magnitude;
            if (length < 1e-5f) return false;
            int count = Physics.SphereCastNonAlloc(origin, radius, delta / length, Hits, length, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                // A hit at distance 0 is something it started inside: ignore it rather than get stuck.
                if (Hits[i].distance <= 0f || Hits[i].distance >= nearest || !Solid(Hits[i].collider)) continue;
                nearest = Hits[i].distance;
                best = Hits[i];
            }
            return nearest < float.MaxValue;
        }

        private void EnsureSpots()
        {
            if (spotsFound) return;
            spotsFound = true;
            Vector3 centre = ArenaCentre;
            var path = new NavMeshPath();
            bool haveAnchor = false;
            Vector3 anchor = centre;
            for (int ring = 0; ring < 6; ring++)
            {
                float radius = 4f + ring * 3f;
                if (radius > ArenaRadius * 0.75f) break;
                int count = 10 + ring * 4;
                for (int i = 0; i < count; i++)
                {
                    float a = (i + 0.5f * (ring % 2)) / count * Mathf.PI * 2f;
                    if (!OpenFloor(centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius, centre, out Vector3 spot)) continue;
                    if (!haveAnchor)
                    {
                        haveAnchor = true;
                        anchor = spot;
                        spots.Add(spot);
                        continue;
                    }
                    // All on the one piece of NavMesh she can walk round (not a cut-off pocket).
                    if (NavMesh.CalculatePath(spot, anchor, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete) spots.Add(spot);
                }
            }
        }

        private bool OpenFloor(Vector3 point, Vector3 centre, out Vector3 spot)
        {
            spot = point;
            if (!NavMesh.SamplePosition(point, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)) return false;
            spot = hit.position;
            if (Mathf.Abs(spot.y - FloorAt(spot).y) > 0.35f) return false; // a roof or a rock top
            if (Flat(spot - centre) < 3f) return false;                     // the cauldron
            if (Physics.Raycast(spot + Vector3.up * 0.3f, Vector3.up, out RaycastHit over, 6f, ~0, QueryTriggerInteraction.Ignore) && Solid(over.collider))
                return false;                                               // under something
            int count = Physics.OverlapSphereNonAlloc(spot + Vector3.up * 1.1f, 0.75f, Overlaps, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++) if (Solid(Overlaps[i])) return false; // hard up against a wall, a trunk or a post
            return true;
        }

        // The best open spot near where it's wanted, between two distances from an origin, leaning toward a direction.
        private Vector3 PickSpot(Vector3 desired, Vector3 origin, float minDistance, float maxDistance, Vector3 prefer)
        {
            EnsureSpots();
            if (spots.Count == 0) return NavMesh.SamplePosition(desired, out NavMeshHit hit, 6f, NavMesh.AllAreas) ? hit.position : FloorAt(origin);
            Vector3 best = spots[0];
            float bestScore = float.MaxValue;
            foreach (Vector3 spot in spots)
            {
                float d = Flat(spot - origin);
                float score = Flat(spot - desired);
                if (d < minDistance) score += (minDistance - d) * 3f;
                if (d > maxDistance) score += (d - maxDistance) * 3f;
                if (prefer.sqrMagnitude > 0.01f && d > 0.1f) score -= Vector3.Dot((spot - origin).Flatten() / d, prefer) * 2f;
                if (score >= bestScore) continue;
                bestScore = score;
                best = spot;
            }
            return best;
        }

        // ---------- Spores ----------

        private void UpdateSpores()
        {
            bool inside = SporeCloud.Contains(PlayerPosition);
            if (inside)
            {
                if (Target != null) MudlandsPlayerSlow.Ensure(Target.gameObject).SlowFor(0.5f, 0.25f);
                if (playerFlask != null) playerFlask.ExtraDaze = Mathf.Max(playerFlask.ExtraDaze, 0.5f);
                dazedBySpores = true;
            }
            else if (dazedBySpores)
            {
                dazedBySpores = false;
                if (playerFlask != null) playerFlask.ExtraDaze = 0f;
            }
        }

        // ---------- Damage, the car, phases, reset, death ----------

        protected override void OnBossHit(float amount, GameObject source, Vector3 away)
        {
            kickVelocity += Mathf.Clamp(amount / 25f, 0.2f, 1.2f) * 6f; // rocks back on the broom
            if (state == State.Clones && amount >= 5f) { EndClones(true); return; }
            if (Riding && IsFullCharge(amount, source)) KnockOff(away);
        }

        public override VehicleResponse OnVehicleHit(VehicleHit hit) => VehicleResponse.Explode;

        public override float OnCarBlast(CarBlastInfo blast)
        {
            bool grounded = !Riding && state != State.Falling && !(state == State.Mount && stateTime > MountRiseStart);
            if (!grounded) return blast.Damage;
            // Launched (without a broom, so straight back down again), and down for longer.
            if (BroomAttached) // caught mid-mount: the broom goes flying too
            {
                broomSpot = PickSpot(transform.position, transform.position, 5f, 10f, Vector3.zero);
                DetachBroom(Vector3.up * 4f + Random.insideUnitSphere.Flatten() * 3f, false);
            }
            if (Agent != null) Agent.enabled = false;
            Vector3 away = (transform.position - blast.Centre).Flatten();
            if (away.sqrMagnitude < 0.01f) away = -transform.forward;
            fallVelocity = away.normalized * 3.5f + Vector3.up * 7f;
            tumbleSpin = RandomSpin(420f);
            fallTime = 0f;
            bounces = 0;
            Enter(State.Falling);
            dazedLong = true;
            Say("@_@", Sleepy, 2f, 5f);
            return blast.Damage;
        }

        protected override void OnPhaseTwo()
        {
            phaseFlash = false;
            Say("Now I'm AWAKE!", Hex, 1.8f, 5f);
            Sfx.PlayAt("cackle", transform.position, 1f, 0.9f, 60f);
        }

        // The phase change only pauses her attacks: off the broom she keeps falling, getting up and walking.
        protected override void UpdatePhaseShift(float dt)
        {
            if (!Riding)
            {
                stateTime += dt;
                TickGround(dt);
                return;
            }
            Hover(dt, 8f);
            if (phaseFlash || PhaseShiftElapsed < 0.8f) return;
            phaseFlash = true;
            ParticleBurst.Spawn(ParticleBurst.Kind.Mist, transform.position + Vector3.up * 1.5f, Vector3.zero, 40, 1.2f, 5f, 90f, new Color(0.5f, 1f, 0.4f, 0.7f));
            ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, transform.position + Vector3.up * 1.5f, Vector3.up, 30, 0.1f, 6f, 90f, Hex);
            CameraShake.Add(0.25f);
        }

        protected override void OnReset()
        {
            if (state == State.Clones) EndClones(false);
            AttachBroom();
            HoldWandInTeeth(false);
            if (Agent != null) Agent.enabled = false;
            transform.position = ArenaCentre + Vector3.up * 7f;
            DamageTakenMultiplier = 1f;
            Visual.localRotation = VisualRestRotation;
            posed = false;
            Enter(State.Fly);
            if (playerFlask != null) playerFlask.ExtraDaze = 0f;
        }

        protected override void OnMudBossDeath()
        {
            bool onBroom = BroomAttached;
            deathAirborne = state == State.Falling || (onBroom && transform.position.y > FloorAt(transform.position).y + 0.3f);
            EndClones(false);
            SporeCloud.ClearAll();
            if (playerFlask != null) playerFlask.ExtraDaze = 0f;
            Sfx.PlayAt("cackle", transform.position, 0.8f, 0.5f, 50f);
            HoldWandInTeeth(false);
            // The broom drops out of the air (its coil is the prize).
            if (broomMode != BroomMode.Dropped) DetachBroom(transform.forward * 2f + Vector3.up * 1.5f, true);
            if (deathAirborne && state != State.Falling)
            {
                fallVelocity = Vector3.up * 1.5f;
                tumbleSpin = RandomSpin(200f);
            }
            fallTime = 0f;
            bounces = 0;
            stars.gameObject.SetActive(false);
            Face?.Show(Mood.Shock, 1.5f);
        }

        protected override void UpdateDeath(float t)
        {
            // Falls (no broom, no flying), curls up on her side and goes to sleep. Snoring. Then a puff of smoke.
            float dt = Time.deltaTime;
            UpdateBroom(dt);
            Pose target;
            if (deathAirborne)
            {
                Visual.localRotation *= Quaternion.Euler(tumbleSpin * dt);
                if (FallStep(dt))
                {
                    deathAirborne = false;
                    transform.position = FloorAt(transform.position);
                    Thump(transform.position, 1.4f);
                }
                target = FlailPose(Time.time);
            }
            else
            {
                Visual.localRotation = Quaternion.Slerp(Visual.localRotation, VisualRestRotation, 1f - Mathf.Exp(-12f * dt));
                if (landedAt < 0f)
                {
                    landedAt = t;
                    Face?.Show(Mood.Sleepy, 30f);
                }
                target = SleepPose(Time.time);
                if (t - landedAt > 0.8f && Time.time >= nextSnore)
                {
                    nextSnore = Time.time + 1.5f;
                    Emote.Show(transform, "zzz", Sleepy, 1.2f, 1.4f, 4f);
                }
            }
            lookWeight = 0f;
            PoseNow(target, deathAirborne ? 20f : 6f, dt, false);
            StepJiggles(dt);
            if (vanished || t < corpseTime - 0.3f) return;
            vanished = true;
            ParticleBurst.Spawn(ParticleBurst.Kind.Mist, transform.position + Vector3.up * 0.5f, Vector3.up, 40, 1.2f, 2.5f, 80f, Arcane);
            SetRenderersVisible(false);
            if (broom != null) Destroy(broom.gameObject);
        }

        // =====================================================================================================
        // Animation: every frame a target pose is built for what she's doing, the current pose eases toward it, and
        // the rig is placed from it (IK for the limbs). Then the springs (hat, cape, lantern) and the clones.
        // =====================================================================================================

        protected override void AnimateBoss(float dt)
        {
            float t = Time.time;
            TrackFlight(dt);
            kickVelocity += (-kick * 170f - kickVelocity * 13f) * dt;
            kick += kickVelocity * dt;
            if (!BroomAttached) UpdateBroom(dt);

            Pose target;
            float rate, look;
            switch (state)
            {
                case State.Falling: target = FlailPose(t); rate = 20f; look = 0f; break;
                case State.Dazed: target = LyingPose(t); rate = 10f; look = 0f; break;
                case State.GetUp: target = GetUpPose(stateTime, t); rate = 16f; look = 0.2f; break;
                case State.Walk: target = WalkPose(dt, t); rate = 12f; look = 0.35f; break;
                case State.Call: target = CallPose(t); rate = 9f; look = 0f; break;
                case State.Mount: target = MountPose(stateTime, t); rate = 16f; look = 0.3f; break;
                default:
                    target = FlightPose(t, InPhaseShift);
                    rate = state == State.Hex || state == State.Clones || state == State.Spores ? 16f : 9f;
                    look = !Engaged ? 0f : state == State.Rain ? 0.4f : 1f;
                    break;
            }
            // Tumbling end over end while she falls, upright again once she's down.
            if (state == State.Falling) Visual.localRotation *= Quaternion.Euler(tumbleSpin * dt);
            else Visual.localRotation = Quaternion.Slerp(Visual.localRotation, VisualRestRotation, 1f - Mathf.Exp(-14f * dt));
            lookWeight = Mathf.MoveTowards(lookWeight, look, dt * 3f);
            PoseNow(target, rate, dt, BroomAttached);
            StepJiggles(dt);
            UpdateStars();
            SyncClones();
        }

        private void TrackFlight(float dt)
        {
            if (dt <= 0f) return;
            Vector3 v = tracked ? (transform.position - lastPosition) / dt : Vector3.zero;
            if (v.sqrMagnitude > 144f) v = Vector3.zero; // a teleport (the clone shuffle)
            flightVelocity = Vector3.Lerp(flightVelocity, v, 1f - Mathf.Exp(-5f * dt));
            lastPosition = transform.position;
            tracked = true;
        }

        private void UpdateStars()
        {
            bool on = state == State.Dazed || state == State.Wobble || (state == State.GetUp && stateTime < 0.6f);
            if (stars.gameObject.activeSelf != on) stars.gameObject.SetActive(on);
            if (on) stars.localRotation = Quaternion.Euler(0f, Time.time * 220f, 0f);
        }

        private void StepJiggles(float dt)
        {
            dt = Mathf.Min(dt, 1f / 30f);
            foreach (Jiggle jiggle in jiggles) jiggle.Step(dt);
        }

        // ---------- Riding ----------

        private Pose FlightPose(float t, bool phaseShift)
        {
            // Banks into her drift and noses down as she speeds up; a lazy bob; rocked back by hits.
            Vector3 local = transform.InverseTransformDirection(flightVelocity);
            float bank = Mathf.Clamp(-local.x * 6f, -24f, 24f);
            float pitch = Mathf.Clamp(local.z * 3f - local.y * 2.5f, -16f, 16f);
            Quaternion r = Quaternion.Euler(pitch + Mathf.Sin(t * 1.1f) * 3f - kick * 10f, Mathf.Sin(t * 0.7f) * 4f, bank + Mathf.Sin(t * 0.9f) * 4f);
            if (state == State.Wobble) r = Quaternion.Euler(0f, stateTime * 300f, Mathf.Sin(stateTime * 15f) * 12f) * r; // spinning, dizzy
            Vector3 b = BroomRest + Vector3.up * (Mathf.Sin(t * 1.6f + BossSeed) * 0.05f);
            Pose p = RidePose(b, r, t);
            p.Spine = Quaternion.Euler(-kick * 16f, 0f, 0f) * p.Spine;
            if (phaseShift) PhaseShiftGesture(ref p, b, r, PhaseShiftElapsed);
            else if (!Engaged) Doze(ref p, t);
            else
            {
                switch (state)
                {
                    case State.Hex: HexGesture(ref p, b, r); break;
                    case State.Spores: SporeGesture(ref p, b, r); break;
                    case State.Rain: StirGesture(ref p, b, r); break;
                    case State.Clones: CloneGesture(ref p, b, r); break;
                    case State.Wobble: WobbleGesture(ref p, b, r); break;
                    default: if (Time.time < flourishUntil) Flourish(ref p, b, r); break;
                }
            }
            return p;
        }

        // Astride the broom, hunched over it: left claw on the handle, wand up in the right, feet swinging.
        private static Pose RidePose(Vector3 b, Quaternion r, float t)
        {
            var p = new Pose
            {
                Broom = b, BroomRot = r,
                Hip = b + r * new Vector3(0f, 0.15f, -0.08f),
                HipRot = r * Quaternion.Euler(-6f, 0f, 0f),
                Spine = Quaternion.Euler(32f, 0f, 0f),
                Head = Quaternion.Euler(-26f, 0f, 0f),
                HandL = b + r * new Vector3(-0.08f, 0.1f, 0.44f),
                HandLRot = r * Grip(new Vector3(0.35f, -1f, 0.15f), Vector3.right),
                HandR = b + r * new Vector3(0.25f, 0.36f, 0.3f),
                HandRRot = WandAim(r * new Vector3(0.15f, 0.45f, 1f).normalized),
            };
            float swing = Mathf.Sin(t * 1.3f) * 0.07f;
            p.FootL = b + r * new Vector3(-0.3f, -0.32f, 0.06f + swing);
            p.FootR = b + r * new Vector3(0.3f, -0.32f + 0.03f * Mathf.Sin(t * 2.1f), 0.06f - swing * 0.8f);
            Quaternion toes = r * Quaternion.LookRotation(new Vector3(0f, -0.5f, 1f), new Vector3(0f, 1f, 0.5f));
            p.FootLRot = toes * Quaternion.Euler(0f, -15f, 0f);
            p.FootRRot = toes * Quaternion.Euler(0f, 15f, 0f);
            return p;
        }

        // Hex orbs: wand up, a little wind-up, a flick at you for each orb.
        private void HexGesture(ref Pose p, Vector3 b, Quaternion r)
        {
            Vector3 shoulder = b + r * new Vector3(0.31f, 0.72f, 0.17f);
            Vector3 aim = AimFrom(shoulder, PlayerPosition + Vector3.up * 1.2f);
            float raise = Mathf.SmoothStep(0f, 1f, stateTime / 0.35f);
            float next = 0.5f + hexFired * 0.4f - stateTime;
            float wind = hexFired < HexCount ? 1f - Mathf.Clamp01(next / 0.3f) : 0f;
            float strike = 1f - Mathf.Clamp01((stateTime - lastHexAt) / 0.25f);
            Vector3 hand = Vector3.Lerp(p.HandR, b + r * new Vector3(0.36f, 0.98f, -0.02f), raise);
            hand = Vector3.Lerp(hand, b + r * new Vector3(0.38f, 1.02f, -0.26f), wind);
            p.HandR = Vector3.Lerp(hand, shoulder + aim * 0.72f, strike);
            Vector3 up = Vector3.Slerp(r * new Vector3(0.15f, 1f, 0.35f).normalized, r * new Vector3(0.1f, 0.75f, -0.66f).normalized, wind);
            Vector3 dir = Vector3.Slerp(Vector3.Slerp(r * new Vector3(0.15f, 0.45f, 1f).normalized, up, raise), aim, strike);
            p.HandRRot = WandAim(dir);
            p.Spine = Quaternion.Euler(32f - 12f * wind + 16f * strike, -14f * wind + 10f * strike, 0f);
        }

        // Spores: a rummage in the belt pouch, then an underarm toss.
        private void SporeGesture(ref Pose p, Vector3 b, Quaternion r)
        {
            float dig = Mathf.SmoothStep(0f, 1f, stateTime / 0.3f), toss = Mathf.SmoothStep(0f, 1f, (stateTime - 0.3f) / 0.3f);
            Vector3 pouch = p.Hip + p.HipRot * new Vector3(-0.44f, 0.05f, 0.16f);
            Vector3 front = b + r * new Vector3(-0.26f, 0.8f, 0.62f);
            p.HandL = Vector3.Lerp(Vector3.Lerp(p.HandL, pouch, dig), front, toss);
            p.HandLRot = Quaternion.Slerp(r * Grip(new Vector3(0f, -1f, 0.2f), Vector3.right), r * Grip(new Vector3(0f, -0.3f, 1f), Vector3.up), toss);
            p.Spine = Quaternion.Euler(32f + 6f * toss, 16f * dig - 26f * toss, -8f * dig);
        }

        // Cauldron rain: leans over and stirs the air with her wand, pointed at the pot.
        private void StirGesture(ref Pose p, Vector3 b, Quaternion r)
        {
            Vector3 middle = b + r * new Vector3(0.24f, 0.46f, 0.42f);
            Vector3 aim = AimFrom(middle, ArenaCentre + Vector3.up * (Surface + 0.2f));
            float w = stateTime * 10f;
            Vector3 swirl = r * new Vector3(Mathf.Cos(w), 0f, Mathf.Sin(w));
            p.HandR = middle + swirl * 0.1f;
            p.HandRRot = WandAim((aim + swirl * 0.3f).normalized);
            p.HandL = b + r * new Vector3(-0.1f, 0.1f, 0.44f);
            p.Spine = Quaternion.Euler(40f, -8f + Mathf.Sin(w) * 6f, Mathf.Cos(w) * 4f);
            p.Head = Quaternion.Euler(-12f, 0f, 0f);
        }

        // Clone beam: claws up, wand levelled at you, trembling as it charges.
        private void CloneGesture(ref Pose p, Vector3 b, Quaternion r)
        {
            float up = Mathf.SmoothStep(0f, 1f, stateTime / 0.5f), charge = Mathf.Clamp01(stateTime / 3f);
            Vector3 shoulder = b + r * new Vector3(0.31f, 0.72f, 0.17f);
            Vector3 aim = AimFrom(shoulder, PlayerPosition + Vector3.up * 1.2f);
            float shake = Mathf.Sin(Time.time * 40f) * 0.012f * charge;
            p.HandL = Vector3.Lerp(p.HandL, b + r * new Vector3(-0.5f, 1.45f + shake, 0.25f), up);
            p.HandLRot = Quaternion.Slerp(p.HandLRot, r * Grip(new Vector3(-0.3f, 1f, 0.2f), Vector3.forward), up);
            p.HandR = Vector3.Lerp(p.HandR, shoulder + aim * 0.72f + Vector3.up * shake, up);
            p.HandRRot = WandAim(Vector3.Slerp(r * Vector3.up, aim, up));
            p.Spine = Quaternion.Euler(22f + 8f * charge, 0f, 0f);
            p.Head = Quaternion.Euler(-20f, 0f, 0f);
        }

        // Interrupted: arms flopping, head lolling round in circles.
        private void WobbleGesture(ref Pose p, Vector3 b, Quaternion r)
        {
            float s = stateTime * 7f;
            p.HandL = b + r * new Vector3(-0.45f, 0.3f, 0.22f);
            p.HandR = b + r * new Vector3(0.45f, 0.3f, 0.22f);
            p.HandLRot = r * Grip(Vector3.down, Vector3.forward);
            p.HandRRot = WandAim(r * new Vector3(0.3f, -1f, 0.3f).normalized);
            p.Head = Quaternion.Euler(-10f + Mathf.Sin(s) * 18f, 0f, Mathf.Cos(s) * 26f);
            p.Spine = Quaternion.Euler(25f, 0f, Mathf.Sin(s) * 10f);
        }

        // Both arms up, head thrown back: the cackle.
        private void Flourish(ref Pose p, Vector3 b, Quaternion r)
        {
            float wave = Mathf.Sin(Time.time * 14f) * 0.05f;
            p.HandL = b + r * new Vector3(-0.45f, 1.5f + wave, 0.15f);
            p.HandR = b + r * new Vector3(0.45f, 1.5f - wave, 0.15f);
            p.HandLRot = r * Grip(new Vector3(-0.3f, 1f, 0.1f), Vector3.forward);
            p.HandRRot = WandAim(r * new Vector3(0.3f, 1f, 0.15f).normalized);
            p.Spine = Quaternion.Euler(10f, 0f, 0f);
            p.Head = Quaternion.Euler(-30f + Mathf.Sin(Time.time * 20f) * 6f, 0f, 0f);
        }

        // Out of the fight: nodding off on the broom, and catching herself.
        private static void Doze(ref Pose p, float t)
        {
            float nod = Mathf.SmoothStep(0f, 1f, Mathf.PingPong(t * 0.25f, 1f));
            p.Spine = Quaternion.Euler(36f + 10f * nod, 0f, 0f);
            p.Head = Quaternion.Euler(-10f + 45f * nod, 0f, 10f * nod);
        }

        // Phase two: slaps her cheeks awake, then throws her arms up.
        private void PhaseShiftGesture(ref Pose p, Vector3 b, Quaternion r, float s)
        {
            if (s >= 0.8f)
            {
                Flourish(ref p, b, r);
                return;
            }
            float slap = Mathf.Abs(Mathf.Sin(s * 14f));
            p.HandL = HeadPoint(new Vector3(-0.32f - 0.12f * slap, -0.32f, 0.08f));
            p.HandR = HeadPoint(new Vector3(0.32f + 0.12f * slap, -0.32f, 0.08f));
            p.HandLRot = r * Grip(new Vector3(0.2f, 1f, 0f), Vector3.right);
            p.HandRRot = WandAim(r * new Vector3(0f, 1f, -0.4f).normalized);
            p.Head = Quaternion.Euler(-20f, Mathf.Sin(s * 30f) * 18f, 0f);
            p.Spine = Quaternion.Euler(20f, 0f, 0f);
        }

        // ---------- On foot ----------

        // Standing: the granny hunch, long arms hanging forward, the wand held like a walking stick.
        private static Pose StandPose(float t)
        {
            float breathe = Mathf.Sin(t * 1.7f);
            return new Pose
            {
                Hip = new Vector3(0f, StandHip - 0.015f + breathe * 0.006f, -0.02f),
                HipRot = Quaternion.identity,
                Spine = Quaternion.Euler(24f + breathe * 1.5f, 0f, 0f),
                Head = Quaternion.Euler(-20f, 0f, 0f),
                HandL = new Vector3(-0.38f, 0.52f, 0.22f),
                HandR = new Vector3(0.38f, 0.52f, 0.22f),
                HandLRot = Grip(new Vector3(-0.1f, -1f, 0.3f), new Vector3(0.6f, 0f, 0.4f)),
                HandRRot = WandAim(new Vector3(0.1f, -0.65f, 1f).normalized),
                FootL = new Vector3(-0.17f, Ankle, 0.03f),
                FootR = new Vector3(0.17f, Ankle, 0.03f),
                FootLRot = Quaternion.Euler(0f, -14f, 0f),
                FootRRot = Quaternion.Euler(0f, 14f, 0f),
                Broom = BroomRest, BroomRot = Quaternion.identity,
            };
        }

        // The sloth walk: short shuffling steps, hips rolling, long arms swimming forward one after the other.
        private Pose WalkPose(float dt, float t)
        {
            Vector3 velocity = AgentReady ? Agent.velocity : flightVelocity;
            float speed = velocity.Flatten().magnitude;
            walkWeight = Mathf.MoveTowards(walkWeight, Mathf.Clamp01(speed / 0.8f), dt * 4f);
            walkPhase = Mathf.Repeat(walkPhase + speed / 0.36f * Mathf.PI * dt, Mathf.PI * 2f); // half a cycle a step
            float s = Mathf.Sin(walkPhase), c = Mathf.Cos(walkPhase), w = walkWeight, stride = 0.17f * w;
            Pose p = StandPose(t);
            p.FootL = new Vector3(-0.16f, Ankle + 0.09f * Mathf.Max(0f, c) * w, stride * s + 0.03f);
            p.FootR = new Vector3(0.16f, Ankle + 0.09f * Mathf.Max(0f, -c) * w, -stride * s + 0.03f);
            p.FootLRot = Quaternion.Euler(-12f * Mathf.Max(0f, c) * w, -10f, 0f);
            p.FootRRot = Quaternion.Euler(-12f * Mathf.Max(0f, -c) * w, 10f, 0f);
            // She waddles: the whole body rocks onto each planted foot, and the head bobs a beat later.
            p.Hip = new Vector3(0.05f * c * w, StandHip - 0.02f - 0.045f * Mathf.Abs(s) * w, 0f);
            p.HipRot = Quaternion.Euler(0f, 12f * s * w, -9f * c * w);
            p.Spine = Quaternion.Euler(32f + 4f * Mathf.Abs(c) * w, -16f * s * w, 8f * c * w);
            p.Head = Quaternion.Euler(-28f + 7f * Mathf.Abs(Mathf.Sin(walkPhase - 0.6f)) * w, 9f * s * w, -6f * c * w);
            p.HandL = new Vector3(-0.36f - 0.04f * Mathf.Max(0f, -s) * w, 0.56f + 0.2f * Mathf.Max(0f, -s) * w, 0.22f - 0.32f * s * w);
            p.HandR = new Vector3(0.36f + 0.04f * Mathf.Max(0f, s) * w, 0.56f + 0.2f * Mathf.Max(0f, s) * w, 0.22f + 0.32f * s * w);
            p.HandLRot = Grip(new Vector3(0f, -1f, 0.4f + 0.4f * Mathf.Max(0f, -s)), Vector3.forward);
            p.HandRRot = WandAim(new Vector3(0.1f, -0.55f + 0.3f * Mathf.Max(0f, s) * w, 1f).normalized);
            return p;
        }

        // Tumbling: arms windmilling, legs bicycling.
        private static Pose FlailPose(float t)
        {
            float w = t * 15f;
            Pose p = StandPose(t);
            p.Hip = new Vector3(0f, StandHip, 0f);
            p.HipRot = Quaternion.Euler(Mathf.Sin(w * 0.5f) * 15f, 0f, 0f);
            p.Spine = Quaternion.Euler(-15f + Mathf.Sin(w * 0.7f) * 20f, 0f, Mathf.Sin(w * 0.45f) * 15f);
            p.Head = Quaternion.Euler(-20f + Mathf.Sin(w * 0.9f) * 15f, 0f, Mathf.Cos(w * 0.6f) * 20f);
            p.HandL = new Vector3(-0.6f, 1.25f + 0.32f * Mathf.Sin(w), 0.3f * Mathf.Cos(w));
            p.HandR = new Vector3(0.6f, 1.25f + 0.32f * Mathf.Sin(w + 2.1f), 0.3f * Mathf.Cos(w + 2.1f));
            p.HandLRot = Grip(new Vector3(-0.6f, 0.8f, 0f), Vector3.forward);
            p.HandRRot = WandAim(new Vector3(0.5f, 1f, 0.2f).normalized);
            p.FootL = new Vector3(-0.22f, 0.15f + 0.13f * Mathf.Sin(w * 1.3f), 0.18f * Mathf.Cos(w * 1.3f));
            p.FootR = new Vector3(0.22f, 0.15f - 0.13f * Mathf.Sin(w * 1.3f), -0.18f * Mathf.Cos(w * 1.3f));
            p.FootLRot = Quaternion.identity;
            p.FootRRot = Quaternion.identity;
            return p;
        }

        // Flat on her back, limbs flung out, head lolling.
        private static Pose LyingPose(float t)
        {
            float breathe = Mathf.Sin(t * 2.2f);
            float twitch = Mathf.Sin(t * 3.1f) > 0.95f ? Mathf.Sin(t * 40f) * 0.03f : 0f;
            Pose p = StandPose(t);
            p.Hip = new Vector3(0f, 0.33f, 0f);
            p.HipRot = Quaternion.Euler(-82f, 0f, 0f);
            p.Spine = Quaternion.Euler(-4f + breathe * 2f, 0f, 0f);
            p.Head = Quaternion.Euler(-8f, 0f, Mathf.Sin(t * 1.1f) * 25f);
            p.HandL = new Vector3(-0.82f, 0.12f, -0.42f);
            p.HandR = new Vector3(0.8f, 0.12f, -0.5f);
            p.HandLRot = Grip(new Vector3(-1f, 0f, 0.2f), Vector3.up);
            p.HandRRot = WandAim(new Vector3(1f, -0.1f, -0.3f).normalized);
            p.FootL = new Vector3(-0.3f, 0.12f, 0.42f);
            p.FootR = new Vector3(0.32f, 0.12f + twitch, 0.4f);
            p.FootLRot = Quaternion.LookRotation(new Vector3(-0.3f, 1f, 0.2f), Vector3.back);
            p.FootRRot = Quaternion.LookRotation(new Vector3(0.3f, 1f, 0.2f), Vector3.back);
            return p;
        }

        private static Pose SitPose(float t)
        {
            Pose p = StandPose(t);
            p.Hip = new Vector3(0f, 0.24f, -0.12f);
            p.HipRot = Quaternion.Euler(-28f, 0f, 0f);
            p.Spine = Quaternion.Euler(24f, 0f, 0f);
            p.Head = Quaternion.Euler(-6f, 0f, Mathf.Sin(t * 3f) * 8f);
            p.HandL = new Vector3(-0.4f, 0.13f, -0.32f);
            p.HandR = new Vector3(0.4f, 0.13f, -0.32f);
            p.HandLRot = Grip(new Vector3(0f, -1f, -0.3f), Vector3.back);
            p.HandRRot = WandAim(new Vector3(0.3f, -0.4f, 1f).normalized);
            p.FootL = new Vector3(-0.2f, Ankle, 0.42f);
            p.FootR = new Vector3(0.22f, Ankle, 0.38f);
            p.FootLRot = Quaternion.Euler(-30f, -15f, 0f);
            p.FootRRot = Quaternion.Euler(-30f, 15f, 0f);
            return p;
        }

        private static Pose CrouchPose(float t)
        {
            Pose p = StandPose(t);
            p.Hip = new Vector3(0f, 0.42f, 0.1f);
            p.HipRot = Quaternion.Euler(30f, 0f, 0f);
            p.Spine = Quaternion.Euler(18f, 0f, 0f);
            p.Head = Quaternion.Euler(-38f, 0f, 0f);
            p.HandL = new Vector3(-0.26f, 0.42f, 0.42f);
            p.HandR = new Vector3(0.26f, 0.42f, 0.42f);
            p.HandLRot = Grip(new Vector3(0f, -1f, 0.2f), Vector3.back);
            p.HandRRot = WandAim(new Vector3(0.2f, -0.7f, 1f).normalized);
            p.FootL = new Vector3(-0.18f, Ankle, 0.14f);
            p.FootR = new Vector3(0.18f, Ankle, 0.14f);
            return p;
        }

        // Back to sitting, hands on the knees, and up (slowly; she's a sloth).
        private static Pose GetUpPose(float s, float t)
        {
            if (s < 0.5f) return Pose.Lerp(LyingPose(t), SitPose(t), Mathf.SmoothStep(0f, 1f, s / 0.5f));
            if (s < 1f) return Pose.Lerp(SitPose(t), CrouchPose(t), Mathf.SmoothStep(0f, 1f, (s - 0.5f) / 0.5f));
            return Pose.Lerp(CrouchPose(t), StandPose(t), Mathf.SmoothStep(0f, 1f, (s - 1f) / 0.45f));
        }

        // Whistling for the broom: claws to her lips, wand beckoning overhead.
        private Pose CallPose(float t)
        {
            Pose p = StandPose(t);
            float beckon = Mathf.Sin(t * 9f);
            p.HandL = HeadPoint(new Vector3(-0.03f, -0.36f, 0.3f));
            p.HandLRot = Grip(new Vector3(0.1f, 1f, 0.1f), Vector3.back);
            p.HandR = new Vector3(0.42f, 1.42f + beckon * 0.05f, 0.18f + beckon * 0.1f);
            p.HandRRot = WandAim(new Vector3(0f, 1f, 0.35f + 0.45f * beckon).normalized);
            p.Spine = Quaternion.Euler(14f, 0f, 0f);
            p.Head = Quaternion.Euler(-12f, 0f, 0f);
            return p;
        }

        // Leaning over the broom with both claws on it.
        private static Pose ReachPose(Vector3 b, Quaternion r, float t)
        {
            Pose p = StandPose(t);
            p.Hip = new Vector3(0f, StandHip - 0.04f, -0.04f);
            p.Spine = Quaternion.Euler(34f, 0f, 0f);
            p.Head = Quaternion.Euler(-30f, 0f, 0f);
            p.HandL = b + r * new Vector3(-0.08f, 0.1f, 0.44f);
            p.HandLRot = r * Grip(new Vector3(0.35f, -1f, 0.15f), Vector3.right);
            p.HandR = b + r * new Vector3(0.25f, 0.36f, 0.3f);
            p.HandRRot = WandAim(r * new Vector3(0.15f, 0.45f, 1f).normalized);
            p.Broom = b;
            p.BroomRot = r;
            return p;
        }

        private Pose MountPose(float m, float t)
        {
            var seat = new Vector3(0f, MountHeight, 0.05f);
            if (m < 0.45f)
            {
                // The broom glides in between her legs as she grabs it.
                float k = Mathf.SmoothStep(0f, 1f, m / 0.45f);
                Vector3 b = Vector3.Lerp(mountFrom, seat, k);
                Quaternion r = Quaternion.Slerp(mountFromRot, Quaternion.identity, k);
                Pose p = Pose.Lerp(StandPose(t), ReachPose(b, r, t), k);
                p.Broom = b;
                p.BroomRot = r;
                return p;
            }
            if (m < 0.9f)
            {
                // It lifts her off her feet, with a little hop.
                float k = (m - 0.45f) / 0.45f, e = Mathf.SmoothStep(0f, 1f, k);
                Vector3 b = Vector3.Lerp(seat, BroomRest, e);
                Pose p = Pose.Lerp(ReachPose(b, Quaternion.identity, t), RidePose(b, Quaternion.identity, t), e);
                p.Hip += Vector3.up * (0.08f * Mathf.Sin(k * Mathf.PI));
                return p;
            }
            if (m < MountRiseStart)
            {
                // It sags under her...
                float k = (m - 0.9f) / (MountRiseStart - 0.9f);
                return RidePose(BroomRest + Vector3.down * (0.09f * Mathf.Sin(k * Mathf.PI)), Quaternion.Euler(5f * Mathf.Sin(k * Mathf.PI * 2f), 0f, 0f), t);
            }
            // ...then noses up and off they go.
            float lift = Mathf.Clamp01((m - MountRiseStart) / 1.2f);
            return RidePose(BroomRest, Quaternion.Euler(-14f * Mathf.Sin(lift * Mathf.PI), 0f, 0f), t);
        }

        // Entrance: hanging under the handle like a sloth on a branch (pull 0), or chin up over it (pull 1).
        private static Pose HangPose(Vector3 b, Quaternion r, float t, float pull)
        {
            Pose p = StandPose(t);
            p.Spine = Quaternion.Euler(-4f - 4f * pull, 0f, Mathf.Sin(t * 1.4f) * 4f);
            p.Head = Quaternion.Euler(-20f + 10f * pull, 0f, 0f);
            p.HandL = b + r * new Vector3(-0.075f, -0.1f, 0.12f);
            p.HandR = b + r * new Vector3(0.075f, -0.1f, -0.03f);
            p.HandLRot = r * Grip(new Vector3(0.3f, 1f, 0f), Vector3.right);
            p.HandRRot = r * Grip(new Vector3(-0.3f, 1f, 0f), Vector3.left);
            float sway = Mathf.Sin(t * 1.7f) * 0.05f, kick = Mathf.Sin(t * 5f) * 0.08f * pull;
            p.FootL = new Vector3(-0.13f, 0.03f + 0.12f * pull, 0.05f + sway + kick);
            p.FootR = new Vector3(0.13f, 0.05f + 0.12f * pull, 0.05f - sway - kick);
            p.FootLRot = Quaternion.Euler(30f, -10f, 0f); // toes pointing down
            p.FootRRot = Quaternion.Euler(30f, 10f, 0f);
            return p;
        }

        // From hanging to sitting: a leg swung up and over.
        private static Pose LegOver(Pose from, Pose to, float k)
        {
            Pose p = Pose.Lerp(from, to, k);
            float arc = Mathf.Sin(k * Mathf.PI);
            p.FootR += new Vector3(0.25f, 0.45f, -0.1f) * arc;
            p.Hip += Vector3.up * (0.08f * arc);
            p.HipRot = Quaternion.Euler(0f, 0f, -14f * arc) * p.HipRot;
            return p;
        }

        // Dead: curled up on her side, claws under her cheek, asleep.
        private static Pose SleepPose(float t)
        {
            float breathe = Mathf.Sin(t * 1.4f);
            Pose p = StandPose(t);
            p.Hip = new Vector3(0f, 0.6f, 0f);
            p.HipRot = Quaternion.identity;
            p.Spine = Quaternion.Euler(42f + breathe * 2f, 0f, 0f);
            p.Head = Quaternion.Euler(22f, 0f, 0f);
            p.FootL = new Vector3(-0.12f, 0.5f, 0.36f);
            p.FootR = new Vector3(0.12f, 0.46f, 0.38f);
            p.FootLRot = Quaternion.Euler(20f, 0f, 0f);
            p.FootRRot = Quaternion.Euler(20f, 0f, 0f);
            p.HandL = new Vector3(-0.2f, 1.05f, 0.62f);
            p.HandR = new Vector3(-0.08f, 1f, 0.58f);
            p.HandLRot = Grip(new Vector3(0.2f, 1f, 0.2f), Vector3.right);
            p.HandRRot = WandAim(new Vector3(-0.3f, 0.2f, 1f).normalized);
            return Rotated(p, new Vector3(0f, 0.6f, 0f), Quaternion.Euler(0f, 0f, 80f), new Vector3(0.1f, -0.25f, 0f));
        }

        // ---------- Pose maths ----------

        /// <summary>A whole-body pose in her Visual's space: where the hips, hands and feet go (the limbs reach them by
        /// IK), how the hips, spine and head turn, and where the broom sits while she rides it.</summary>
        private struct Pose
        {
            public Vector3 Hip, HandL, HandR, FootL, FootR, Broom;
            public Quaternion HipRot, Spine, Head, HandLRot, HandRRot, FootLRot, FootRRot, BroomRot;

            public static Pose Lerp(in Pose a, in Pose b, float t) => new Pose
            {
                Hip = Vector3.Lerp(a.Hip, b.Hip, t),
                HandL = Vector3.Lerp(a.HandL, b.HandL, t),
                HandR = Vector3.Lerp(a.HandR, b.HandR, t),
                FootL = Vector3.Lerp(a.FootL, b.FootL, t),
                FootR = Vector3.Lerp(a.FootR, b.FootR, t),
                Broom = Vector3.Lerp(a.Broom, b.Broom, t),
                HipRot = Quaternion.Slerp(a.HipRot, b.HipRot, t),
                Spine = Quaternion.Slerp(a.Spine, b.Spine, t),
                Head = Quaternion.Slerp(a.Head, b.Head, t),
                HandLRot = Quaternion.Slerp(a.HandLRot, b.HandLRot, t),
                HandRRot = Quaternion.Slerp(a.HandRRot, b.HandRRot, t),
                FootLRot = Quaternion.Slerp(a.FootLRot, b.FootLRot, t),
                FootRRot = Quaternion.Slerp(a.FootRRot, b.FootRRot, t),
                BroomRot = Quaternion.Slerp(a.BroomRot, b.BroomRot, t),
            };
        }

        private static Pose Rotated(Pose p, Vector3 pivot, Quaternion turn, Vector3 shift)
        {
            Vector3 T(Vector3 v) => pivot + turn * (v - pivot) + shift;
            p.Hip = T(p.Hip);
            p.HandL = T(p.HandL);
            p.HandR = T(p.HandR);
            p.FootL = T(p.FootL);
            p.FootR = T(p.FootR);
            p.Broom = T(p.Broom);
            p.HipRot = turn * p.HipRot;
            p.HandLRot = turn * p.HandLRot;
            p.HandRRot = turn * p.HandRRot;
            p.FootLRot = turn * p.FootLRot;
            p.FootRRot = turn * p.FootRRot;
            p.BroomRot = turn * p.BroomRot;
            return p;
        }

        // A bone pointing along a direction (its local -Y), with its local +Z toward a hint.
        private static Quaternion Bone(Vector3 along, Vector3 hint)
        {
            if (along.sqrMagnitude < 1e-8f) along = Vector3.down;
            if (Vector3.Cross(along, hint).sqrMagnitude < 1e-6f * along.sqrMagnitude * hint.sqrMagnitude)
                hint = Mathf.Abs(along.normalized.y) < 0.9f ? Vector3.up : Vector3.forward;
            return Quaternion.LookRotation(along, hint) * BoneFix;
        }

        // A hand with its fingers along one direction and its palm (where the claws curl) toward another.
        private static Quaternion Grip(Vector3 fingers, Vector3 palm) => Bone(fingers, palm);

        // The wand hand, turned so the wand points along a direction.
        private static Quaternion WandAim(Vector3 dir)
        {
            Vector3 up = Vector3.ProjectOnPlane(Vector3.up, dir);
            if (up.sqrMagnitude < 0.01f) up = Vector3.ProjectOnPlane(Vector3.back, dir);
            return Quaternion.LookRotation(dir, up) * WandFix;
        }

        private Vector3 AimFrom(Vector3 visualPoint, Vector3 world)
        {
            Vector3 d = Visual.InverseTransformPoint(world) - visualPoint;
            return d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.forward;
        }

        // A point on her head (relative to its centre) in Visual space, as the head is posed right now.
        private Vector3 HeadPoint(Vector3 local)
        {
            (Vector3 position, Quaternion rotation) = InVisual(head);
            return position + rotation * (HeadCentre + local);
        }

        // Where a joint is in Visual space (everything under the Visual has unit scale).
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

        private void PoseNow(Pose target, float rate, float dt, bool poseBroom, bool snap = false)
        {
            lastDt = dt;
            if (!posed || snap)
            {
                pose = target;
                posed = true;
            }
            else pose = Pose.Lerp(pose, target, 1f - Mathf.Exp(-rate * Mathf.Max(0f, dt)));
            Apply(pose, poseBroom);
        }

        private void Apply(in Pose p, bool poseBroom)
        {
            body.localPosition = p.Hip;
            body.localRotation = p.HipRot;
            spine.localRotation = p.Spine;
            head.localRotation = Quaternion.Slerp(p.Head, LookTurn(p.Head), lookWeight);
            // Knees bend forward (and a little out); elbows back and out.
            Vector3 knees = p.HipRot * Vector3.forward;
            Limb(legL, kneeL, footL, Thigh, Shin, p.FootL, p.FootLRot, knees + p.HipRot * Vector3.left * 0.2f);
            Limb(legR, kneeR, footR, Thigh, Shin, p.FootR, p.FootRRot, knees + p.HipRot * Vector3.right * 0.2f);
            Quaternion chest = p.HipRot * p.Spine;
            Limb(armL, elbowL, handL, UpperArm, Forearm, p.HandL, p.HandLRot, chest * new Vector3(-0.8f, -0.3f, -0.55f));
            Limb(armR, elbowR, handR, UpperArm, Forearm, p.HandR, p.HandRRot, chest * new Vector3(0.8f, -0.3f, -0.55f));
            if (!poseBroom) return;
            broom.localPosition = p.Broom;
            broom.localRotation = p.BroomRot;
        }

        // Her head turning toward whoever she's looking at (in the spine's frame), clamped to what a neck can do.
        private Quaternion LookTurn(Quaternion fallback)
        {
            Vector3? point = FaceLookPoint;
            if (point == null || lookWeight <= 0.001f) return fallback;
            (Vector3 neck, Quaternion spineTurn) = InVisual(spine);
            neck += spineTurn * NeckAt;
            Vector3 local = Quaternion.Inverse(spineTurn) * (Visual.InverseTransformPoint(point.Value) - neck);
            float yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -55f, 55f);
            float pitch = Mathf.Clamp(-Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg, -45f, 35f);
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

        /// <summary>A springy joint (hat tip, cape, lantern): it lags behind and swings back to its rest pose.</summary>
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
                if (joint == null || dt <= 0f) return;
                joint.localRotation = rest;
                Vector3 pivot = joint.position, want = joint.TransformPoint(tip);
                float length = (want - pivot).magnitude;
                if (!ready || (point - want).sqrMagnitude > 1f)
                {
                    point = want;
                    velocity = Vector3.zero;
                    ready = true;
                }
                velocity += ((want - point) * stiffness + Vector3.down * gravity) * dt;
                velocity *= Mathf.Exp(-damping * dt); // air drag: it trails behind as she flies
                point += velocity * dt;
                Vector3 to = point - pivot;
                if (to.sqrMagnitude < 1e-8f || length < 1e-5f) return;
                point = pivot + to.normalized * length;
                joint.rotation = Quaternion.FromToRotation(want - pivot, point - pivot) * joint.rotation;
            }
        }
    }

    /// <summary>The witch's slow homing hex orb: 10 on contact; any bolt or swing pops it.</summary>
    public sealed class HexOrb : MonoBehaviour
    {
        private Vector3 velocity;
        private float speed, born;
        private Transform target;
        private GameObject owner;

        public static void Fire(Vector3 from, GameObject owner, float speed)
        {
            var go = new GameObject("Hex Orb");
            go.transform.position = from;
            MudArt.Part(go.transform, "Orb", Vector3.zero, Quaternion.identity, "hex_orb", k => k.Ball(Vector3.zero, 0.3f, new Color(0.5f, 1f, 0.35f), 1, 3f));
            go.AddComponent<SphereCollider>().radius = 0.35f;
            var health = go.AddComponent<Health>();
            health.SetMax(1f);
            health.Restore();
            var orb = go.AddComponent<HexOrb>();
            health.Died += _ => orb.Pop();
            orb.owner = owner;
            orb.speed = speed;
            orb.born = Time.time;
            var player = FindAnyObjectByType<FirstPersonController>();
            orb.target = player != null ? player.transform : null;
            orb.velocity = Vector3.down * speed * 0.5f;
            Sfx.PlayAt("hex_zap", from, 0.4f, 1.6f, 30f);
        }

        private void Update()
        {
            if (Time.time - born > 6f || target == null) { Pop(); return; }
            Vector3 aim = (target.position + Vector3.up * 1.2f - transform.position).normalized * speed;
            velocity = Vector3.RotateTowards(velocity, aim, 60f * Mathf.Deg2Rad * Time.deltaTime, speed * Time.deltaTime);
            transform.position += velocity * Time.deltaTime;
            transform.Rotate(0f, 400f * Time.deltaTime, 0f);
            if ((target.position + Vector3.up * 1.2f - transform.position).sqrMagnitude < 0.8f)
            {
                var health = target.GetComponent<Health>();
                if (health != null) health.TakeDamage(10f, owner);
                Pop();
            }
        }

        private void Pop()
        {
            ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, transform.position, Vector3.zero, 16, 0.08f, 4f, 80f, new Color(0.5f, 1f, 0.35f));
            Sfx.PlayAt("hex_zap", transform.position, 0.4f, 2f, 25f);
            Destroy(gameObject);
        }
    }

    /// <summary>The witch's sleepy spore cloud: anyone inside is slowed and drowsy (the witch applies it to the player).</summary>
    public sealed class SporeCloud : MonoBehaviour
    {
        private static readonly List<SporeCloud> Clouds = new List<SporeCloud>();
        private const float Radius = 3.5f;
        private float until;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Clouds.Clear();

        public static void Drop(Vector3 from, Vector3 at, GameObject owner)
        {
            Transform pod = MudArt.Part(null, "Spore Pod", Vector3.zero, Quaternion.identity, "spore_pod", k => k.Ball(Vector3.zero, 0.25f, MudPalette.Magic, 1, 1.5f));
            Lob.Throw(pod, from, at, 1f, 1f, landed =>
            {
                var go = new GameObject("Spore Cloud");
                go.transform.position = landed;
                var cloud = go.AddComponent<SporeCloud>();
                cloud.until = Time.time + 5f;
                Clouds.Add(cloud);
                Sfx.PlayAt("mud_splat", landed, 0.6f, 1.6f, 30f);
            }, Radius, new Color(0.6f, 0.4f, 1f, 0.45f));
        }

        public static bool Contains(Vector3 point)
        {
            foreach (SporeCloud cloud in Clouds)
                if (cloud != null && (cloud.transform.position - point).Flatten().magnitude < Radius) return true;
            return false;
        }

        public static void ClearAll()
        {
            foreach (SporeCloud cloud in Clouds) if (cloud != null) Destroy(cloud.gameObject);
            Clouds.Clear();
        }

        private void Update()
        {
            if (Time.time > until) { Clouds.Remove(this); Destroy(gameObject); return; }
            // A haze you can still see through from inside (it slows and dazes; it shouldn't blind).
            if (Random.value < 0.35f)
                ParticleBurst.Spawn(ParticleBurst.Kind.Mist, transform.position + Random.insideUnitSphere.Flatten() * Radius * 0.8f, Vector3.up, 2, 1.1f, 0.6f, 30f,
                    new Color(0.65f, 0.45f, 1f, 0.28f));
        }
    }
}
