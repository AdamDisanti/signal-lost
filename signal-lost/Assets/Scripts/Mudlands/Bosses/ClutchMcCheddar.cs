using System;
using System.Collections;
using System.Collections.Generic;
using SignalLost.Audio;
using SignalLost.Combat;
using SignalLost.Effects;
using SignalLost.Enemies;
using SignalLost.Enemies.Desert;
using SignalLost.Player;
using SignalLost.Progression;
using SignalLost.UI;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

namespace SignalLost.Mudlands
{
    /// <summary>
    /// The Mudlands' stage boss (Christian's design, docs/mudlands.md): Clutch McCheddar, president of the Swamp Rats
    /// MC, a mouse in a leather jacket and aviators on a long chopper whose back wheel is the ship's landing gear.
    /// <list type="bullet">
    /// <item><b>Entrance:</b> the true Akira slide: he blasts past a camera low on the road, throws the chopper sideways
    /// and slides <i>away</i> from it in slow motion, bike laid over toward the lens, left boot braking in a fan of
    /// sparks, three smoke trails and the tail light streaking; then the aviators, the line, the gang and his name.</item>
    /// <item><b>Phase 1:</b> ride-by ram (feet down, burnout and headlight flash, then a charge), donuts (inside boot
    /// planted, chain twirling, flinging mud), mud rooster (waves bye-bye over his shoulder, then a cone of mud: mud in
    /// your eyes), gang call (a two-finger whistle: biker mice).</item>
    /// <item><b>Phase 2 (66%):</b> he wraps his chain round his fist; adds a ramp jump (a no-footer in the air onto a
    /// marked shadow) and a chain whip; bigger gangs; quicker rams.</item>
    /// <item><b>Phase 3 (33%):</b> he slaps the nitro button: purple exhaust and underglow, double rams, donuts leave a
    /// ring of fire.</item>
    /// <item><b>Wipe-outs:</b> a ram into the gas pumps or the ramp's side, a full-charge bolt during a ram, or the car:
    /// the bike slides out from under him, he tumbles, sits dazed, dusts himself off and gets back on (3 s at 1.5x
    /// damage).</item>
    /// <item><b>The getaway:</b> at 15% he stops taking damage and pulls a wheelie up the Gator Causeway. Drunk Driving
    /// is free for the chase; ram him before the County Line gate and he goes over the handlebars. If he gets away he
    /// heals back to 33% and roars back into the lot.</item>
    /// </list>
    /// 1100 HP. Drops the Landing Gear (the stage's main part) and Clutch's Aviators (a camp trophy).
    /// </summary>
    public sealed class ClutchMcCheddar : MudBoss
    {
        private enum State { Circle, Line, Ram, Donuts, Rooster, Gang, ToRamp, Jump, Whip, WipeOut, Rally, Nitro, Getaway, Escaped, Returning, Caught }
        private enum Gesture { None, Point, Beckon, Wave, FistPump, Whistle, Toss }

        public const string TrophyId = "clutch_aviators";
        private const float EscapeAt = 0.15f, PhaseThree = 0.33f, WipeOutTime = 3f;

        [Header("Clutch")]
        [SerializeField] private GameObject gangPrefab;
        [Tooltip("Where his gang rides out from (the roadhouse doors).")]
        [SerializeField] private Transform[] gangDoors = Array.Empty<Transform>();
        [Tooltip("The dirt jump ramp's lip (its forward is the jump direction).")]
        [SerializeField] private Transform ramp;
        [Tooltip("The getaway road, from the lot out to the County Line gate.")]
        [SerializeField] private Transform[] escapeRoute = Array.Empty<Transform>();
        [Tooltip("Solid things a ram can wipe out on (the gas pumps).")]
        [SerializeField] private Transform[] wipeOutProps = Array.Empty<Transform>();

        private static readonly Color Fur = new Color(0.58f, 0.56f, 0.55f), FurLight = new Color(0.78f, 0.76f, 0.74f),
            FurDark = new Color(0.42f, 0.4f, 0.4f), Pink = new Color(0.95f, 0.62f, 0.68f), PinkDark = new Color(0.8f, 0.45f, 0.52f),
            Leather = new Color(0.1f, 0.09f, 0.1f), LeatherHi = new Color(0.2f, 0.18f, 0.19f), Denim = new Color(0.2f, 0.28f, 0.48f),
            DenimDark = new Color(0.14f, 0.2f, 0.36f), Boot = new Color(0.3f, 0.19f, 0.11f), Sole = new Color(0.08f, 0.07f, 0.07f),
            Shirt = new Color(0.85f, 0.22f, 0.18f), Bandana = new Color(0.82f, 0.14f, 0.12f), Gold = new Color(0.95f, 0.75f, 0.3f),
            Cheese = new Color(1f, 0.82f, 0.25f), CheeseHole = new Color(0.85f, 0.6f, 0.12f), BikePaint = new Color(0.36f, 0.1f, 0.48f),
            PaintHi = new Color(0.5f, 0.2f, 0.66f), Chrome = new Color(0.82f, 0.83f, 0.86f), Frame = new Color(0.12f, 0.12f, 0.14f),
            FlameRed = new Color(0.95f, 0.25f, 0.1f), FlameOrange = new Color(1f, 0.55f, 0.12f), FlameYellow = new Color(1f, 0.85f, 0.25f),
            Saddle = new Color(0.34f, 0.2f, 0.12f), Hub = new Color(0.3f, 0.85f, 1f), Nitro = new Color(0.7f, 0.35f, 1f),
            Lamp = new Color(1f, 0.93f, 0.7f), TailRed = new Color(1f, 0.12f, 0.08f), Line = new Color(1f, 0.85f, 0.4f);

        // ---------- The chopper (its own frame: +Z forward, ground at y = 0, wheels on the Z axis) ----------
        private static readonly Vector3 RearAxle = new Vector3(0f, 0.52f, -1.05f), FrontAxle = new Vector3(0f, 0.42f, 1.72f),
            SteerHead = new Vector3(0f, 1.1f, 0.78f), Seat = new Vector3(0f, 0.86f, -0.24f), HipOnSeat = new Vector3(0f, 0.99f, -0.22f), PegAt = new Vector3(0.3f, 0.36f, 0.36f),
            PipeEndLow = new Vector3(0.29f, 0.5f, -1.42f), PipeEndHigh = new Vector3(0.31f, 0.64f, -1.4f), TailLightAt = new Vector3(0f, 0.88f, -1.55f);
        private static readonly Vector3 GlassesUp = new Vector3(0f, 0.25f, 0.3f), GlassesDown = new Vector3(0f, 0.19f, 0.37f);
        private const float RearRadius = 0.52f, FrontRadius = 0.42f, RearHalfWidth = 0.19f;
        private static Vector3 ForkAxis => (FrontAxle - SteerHead).normalized;

        // ---------- The rider (his own frame: +Z forward, feet at y = 0) ----------
        private const float UpperArm = 0.42f, Forearm = 0.42f, Thigh = 0.44f, Shin = 0.43f, Ankle = 0.1f, StandHeight = 0.93f;
        private static readonly Vector3 SpineAt = new Vector3(0f, 0.1f, -0.02f), NeckAt = new Vector3(0f, 0.6f, 0.03f),
            ShoulderAt = new Vector3(0.27f, 0.5f, 0f), HipJointAt = new Vector3(0.14f, -0.02f, 0.02f);
        private static readonly Quaternion BoneFix = Quaternion.Euler(-90f, 0f, 0f);

        private State state = State.Circle;
        private float stateTime, orbit, nextAttack, nextGang, escapeTimer, nextDrop, nextTick, nextGesture;
        private int ramsLeft, getawayStop;
        private Vector3 ramDirection, jumpFrom, jumpTo, savedHome, spinCentre;
        private bool ramHit, escaped, chaseRunning, taunted, nitroDone, titleShown, slowMo;
        private GroundRing ring;
        private readonly List<GameObject> gang = new List<GameObject>();
        private SfxLoop engine;
        private CombatZone gangZone;
        private FireEffect flameL, flameR;
        private bool flamesNitro;

        // The rig.
        private Transform bike, frontEnd, frontSpin, rearSpin, gripL, gripR, headlightPivot, chainHeld, chainSpin, beltChain;
        private Transform body, spine, head, armL, armR, elbowL, elbowR, handL, handR, legL, legR, kneeL, kneeR, footL, footR;
        private Transform glasses, toothpick, nitroCap;
        private Light headlight, tailLight, underglow;
        private TrailRenderer tailTrail;
        private readonly List<Jiggle> jiggles = new List<Jiggle>();

        // The bike's pose (in his Visual's space): lean (+ = top to his right), pitch (+ = front up, about the rear
        // tyre), yaw and an offset (wipe-outs), and the steering.
        private float bikeLean, bikePitch, bikeYaw, bikeLift, steer, frontRoll, rearRoll, bikeShake;
        private Vector3 bikeOffset;
        private bool bikeDetached;

        // Posing.
        private Pose pose;
        private bool posed;
        private float lastDt, lookWeight = 1f, lookYawLimit = 60f, speedNow, yawRate, lastYaw, gestureStart, gestureUntil;
        private Vector3 lastPosition;
        private Vector2 lookAngles;
        private Gesture gesture;
        private Vector3 flightFrom, flightTo;
        private float flightTime = -1f;

        public override string Subtitle => "President, Swamp Rats MC";
        public override float PhaseThreeAt => PhaseThree;
        public override int DisplayPhase => Stage;
        public override bool ShowsOwnTitle => true;
        private int Stage => Health.Normalized <= PhaseThree ? 3 : Phase;
        public bool Escaping => state == State.Getaway;

        public void Configure(GameObject gangPrefabAsset, Transform[] doors, Transform rampLip, Transform[] route, Transform[] pumps)
        {
            gangPrefab = gangPrefabAsset;
            gangDoors = doors;
            ramp = rampLip;
            escapeRoute = route;
            wipeOutProps = pumps;
        }

        protected override void Awake()
        {
            base.Awake();
            corpseTime = Mathf.Max(corpseTime, 6f); // face down in the mud, one hand up, then still
        }

        // =====================================================================================================
        // Model
        // =====================================================================================================

        protected override void BuildModel(Transform visual)
        {
            BuildBike(visual);
            body = MudArt.Pivot(visual, "Rider", HipOnSeat);
            MudArt.Part(body, "Hips", Vector3.zero, Quaternion.identity, "clutch2_hips", HipsMesh);
            beltChain = MudArt.Part(body, "Belt Chain", Vector3.zero, Quaternion.identity, "clutch2_belt_chain", BeltChainMesh);
            spine = MudArt.Pivot(body, "Spine", SpineAt);
            MudArt.Part(spine, "Jacket", Vector3.zero, Quaternion.identity, "clutch2_torso", TorsoMesh);
            head = MudArt.Pivot(spine, "Head", NeckAt);
            BuildHead();
            armL = BuildArm(-1f, out elbowL, out handL);
            armR = BuildArm(1f, out elbowR, out handR);
            legL = BuildLeg(-1f, out kneeL, out footL);
            legR = BuildLeg(1f, out kneeR, out footR);
            BuildTail();

            // The chain he whips with, in his right fist (hidden until he draws it).
            chainHeld = MudArt.Pivot(handR, "Chain", new Vector3(0f, -0.12f, 0.03f));
            chainSpin = MudArt.Part(chainHeld, "Links", Vector3.zero, Quaternion.identity, "clutch2_chain", k => ChainMesh(k, 12, 0.105f));
            chainHeld.gameObject.SetActive(false);
        }

        // ---------- The chopper ----------

        private void BuildBike(Transform visual)
        {
            bike = MudArt.Pivot(visual, "Chopper", Vector3.zero);
            MudArt.Part(bike, "Frame", Vector3.zero, Quaternion.identity, "clutch2_frame", FrameMesh);
            MudArt.Part(bike, "Engine", Vector3.zero, Quaternion.identity, "clutch2_engine", EngineMesh);
            MudArt.Part(bike, "Tank", Vector3.zero, Quaternion.identity, "clutch2_tank", TankMesh);
            MudArt.Part(bike, "Back End", Vector3.zero, Quaternion.identity, "clutch2_back", BackEndMesh);
            nitroCap = MudArt.Part(bike, "Nitro Cap", new Vector3(0f, 1.17f, 0.12f), Quaternion.identity, "clutch2_nitro_cap", k =>
            {
                k.Smooth = true;
                k.Box(new Vector3(0f, 0.025f, 0.05f), new Vector3(0.1f, 0.012f, 0.1f), MudPalette.Metal);
                k.Box(new Vector3(0f, 0.012f, 0.1f), new Vector3(0.1f, 0.02f, 0.012f), MudPalette.Metal);
            });

            // The landing gear: a fat aircraft tyre on a glowing hub, with its own oleo strut (the stage's main part).
            rearSpin = MudArt.Pivot(bike, "Landing Gear", RearAxle);
            MudArt.Part(rearSpin, "Wheel", Vector3.zero, Quaternion.identity, "clutch2_rear_wheel", RearWheelMesh);
            ShipPart = rearSpin;

            frontEnd = MudArt.Pivot(bike, "Front End", SteerHead);
            MudArt.Part(frontEnd, "Forks", Vector3.zero, Quaternion.identity, "clutch2_forks", ForksMesh);
            frontSpin = MudArt.Pivot(frontEnd, "Front Wheel", FrontAxle - SteerHead);
            MudArt.Part(frontSpin, "Wheel", Vector3.zero, Quaternion.identity, "clutch2_front_wheel", FrontWheelMesh);
            gripL = MudArt.Pivot(frontEnd, "Grip L", new Vector3(-0.5f, 0.59f, -0.41f));
            gripR = MudArt.Pivot(frontEnd, "Grip R", new Vector3(0.5f, 0.59f, -0.41f));
            headlightPivot = MudArt.Pivot(frontEnd, "Headlight", new Vector3(0f, -0.1f, 0.2f));
            headlight = new GameObject("Beam").AddComponent<Light>();
            headlight.transform.SetParent(headlightPivot, false);
            headlight.transform.localPosition = new Vector3(0f, 0f, 0.12f);
            headlight.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);
            headlight.type = LightType.Spot;
            headlight.spotAngle = 55f;
            headlight.range = 24f;
            headlight.intensity = 3f;
            headlight.color = Lamp;
            headlight.shadows = LightShadows.None;

            tailLight = new GameObject("Tail Light").AddComponent<Light>();
            tailLight.transform.SetParent(bike, false);
            tailLight.transform.localPosition = TailLightAt + new Vector3(0f, 0f, -0.15f);
            tailLight.type = LightType.Point;
            tailLight.range = 2.5f;
            tailLight.intensity = 1.2f;
            tailLight.color = TailRed;
            tailLight.shadows = LightShadows.None;
            tailTrail = TailStreak(bike, TailLightAt + new Vector3(0f, 0f, -0.06f));

            underglow = new GameObject("Underglow").AddComponent<Light>();
            underglow.transform.SetParent(bike, false);
            underglow.transform.localPosition = new Vector3(0f, 0.25f, 0.1f);
            underglow.type = LightType.Point;
            underglow.range = 4.5f;
            underglow.intensity = 0f;
            underglow.color = Nitro;
            underglow.shadows = LightShadows.None;
        }

        // The tail light's red streak (the Akira shot's light trail), drawn only while he's really moving.
        private static TrailRenderer TailStreak(Transform parent, Vector3 at)
        {
            var holder = new GameObject("Tail Streak");
            holder.transform.SetParent(parent, false);
            holder.transform.localPosition = at;
            var trail = holder.AddComponent<TrailRenderer>();
            trail.sharedMaterial = EffectMaterials.Additive(EffectMaterials.Look.Glow);
            trail.time = 0.3f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.2f));
            trail.widthMultiplier = 0.06f;
            trail.minVertexDistance = 0.08f;
            trail.numCapVertices = 2;
            trail.alignment = LineAlignment.View;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(TailRed, 0f), new GradientColorKey(new Color(1f, 0.3f, 0.2f), 1f) },
                new[] { new GradientAlphaKey(0.6f, 0f), new GradientAlphaKey(0.25f, 0.5f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = gradient;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.emitting = false;
            return trail;
        }

        private static void FrameMesh(MeshKit k)
        {
            k.Smooth = false;
            // Neck, backbone, down tubes, the cradle under the engine, seat stays to the rear axle.
            k.Rod(SteerHead + new Vector3(0f, 0.08f, 0f), SteerHead + ForkAxis * 0.12f, 0.07f, 10, Frame);
            k.Rod(SteerHead + new Vector3(0f, -0.02f, -0.04f), new Vector3(0f, 0.9f, -0.12f), 0.05f, 8, Frame);
            foreach (float x in new[] { -0.07f, 0.07f })
            {
                k.Rod(SteerHead + new Vector3(x, -0.06f, -0.03f), new Vector3(x, 0.3f, 0.42f), 0.04f, 8, Frame);
                k.Rod(new Vector3(x, 0.3f, 0.42f), new Vector3(x * 1.4f, 0.26f, -0.42f), 0.04f, 8, Frame);
                k.Rod(new Vector3(x * 1.4f, 0.26f, -0.42f), new Vector3(x * 2.6f, RearAxle.y, RearAxle.z), 0.035f, 8, Frame);
                k.Rod(new Vector3(x, 0.88f, -0.14f), new Vector3(x * 2.6f, RearAxle.y + 0.04f, RearAxle.z), 0.032f, 8, Frame);
            }
            k.Rod(new Vector3(0f, 0.9f, -0.12f), new Vector3(0f, 0.3f, -0.44f), 0.045f, 8, Frame); // seat post
            // Pegs (mid controls) with rubber, and the brake and shift levers.
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 peg = new Vector3(PegAt.x * s, PegAt.y, PegAt.z);
                k.Rod(new Vector3(0.08f * s, 0.3f, PegAt.z - 0.02f), peg, 0.02f, 6, Chrome);
                k.Rod(peg, peg + new Vector3(0.1f * s, 0f, 0f), 0.034f, 8, MudPalette.Rubber);
                k.Plank(peg + new Vector3(0f, 0.05f, 0.04f), peg + new Vector3(0.02f * s, 0.12f, 0.2f), 0.025f, 0.02f, Chrome);
            }
            // Kickstand, folded along the frame.
            k.Rod(new Vector3(-0.12f, 0.26f, -0.2f), new Vector3(-0.16f, 0.22f, -0.62f), 0.018f, 6, Frame);
        }

        private static void EngineMesh(MeshKit k)
        {
            k.Smooth = true;
            // Crankcase, the V-twin's two finned barrels and chrome rocker boxes, the round air cleaner (a gold cheese
            // wedge on it), the primary cover on the left.
            k.Ball(new Vector3(0f, 0.46f, 0.08f), new Vector3(0.15f, 0.17f, 0.24f), Quaternion.identity, MudPalette.Metal, 1);
            foreach (float tilt in new[] { 32f, -32f })
            {
                Quaternion lean = Quaternion.Euler(tilt, 0f, 0f);
                Vector3 baseAt = new Vector3(0f, 0.58f, 0.08f + Mathf.Sign(tilt) * 0.08f);
                k.Smooth = false;
                for (int i = 0; i < 6; i++)
                    k.Prism(baseAt + lean * new Vector3(0f, i * 0.045f, 0f), lean, 0.115f, 0.115f, 0.022f, 14, MudPalette.DarkMetal);
                k.Prism(baseAt, lean, 0.085f, 0.085f, 0.3f, 12, MudPalette.Metal);
                k.Smooth = true;
                k.Ball(baseAt + lean * new Vector3(0f, 0.31f, 0f), new Vector3(0.12f, 0.06f, 0.13f), lean, Chrome, 1);
                k.Ball(baseAt + lean * new Vector3(0f, 0.36f, 0f), new Vector3(0.09f, 0.04f, 0.1f), lean, Chrome, 1);
            }
            k.Smooth = false;
            k.Prism(new Vector3(0.14f, 0.7f, 0.08f), Quaternion.Euler(0f, 0f, -90f), 0.14f, 0.14f, 0.07f, 16, Chrome);
            k.Prism(new Vector3(0.2f, 0.7f, 0.08f), Quaternion.Euler(0f, 0f, -90f), 0.1f, 0.1f, 0.02f, 16, MudPalette.DarkMetal);
            Wedge(k, new Vector3(0.226f, 0.7f, 0.08f), Quaternion.Euler(0f, 90f, 0f), 0.07f, 0.02f);
            k.Smooth = true;
            k.Ball(new Vector3(-0.15f, 0.4f, -0.22f), new Vector3(0.05f, 0.14f, 0.3f), Quaternion.Euler(-8f, 0f, 0f), Chrome, 1); // primary
            k.Ball(new Vector3(-0.13f, 0.48f, 0.12f), new Vector3(0.05f, 0.09f, 0.09f), Quaternion.identity, Chrome, 1); // points cover
            // Drag pipes on the right, swept back to fishtails (where the flames come out).
            k.Smooth = false;
            Pipe(k, new[] { new Vector3(0.03f, 0.84f, 0.32f), new Vector3(0.2f, 0.82f, 0.36f), new Vector3(0.27f, 0.6f, 0.18f), new Vector3(0.29f, 0.5f, -0.3f), PipeEndLow });
            Pipe(k, new[] { new Vector3(0.03f, 0.84f, -0.16f), new Vector3(0.2f, 0.8f, -0.2f), new Vector3(0.3f, 0.66f, -0.42f), PipeEndHigh });
            foreach (Vector3 end in new[] { PipeEndLow, PipeEndHigh })
                k.Prism(end, Quaternion.Euler(-90f, 0f, 0f), 0.05f, 0.075f, 0.16f, 12, Chrome); // fishtail flare
            // The belt drive on the left: front pulley, belt, rear pulley.
            k.Prism(new Vector3(-0.2f, 0.4f, -0.24f), Quaternion.Euler(0f, 0f, 90f), 0.1f, 0.1f, 0.03f, 16, MudPalette.DarkMetal);
            k.Plank(new Vector3(-0.21f, 0.5f, -0.24f), new Vector3(-0.23f, RearAxle.y + 0.2f, RearAxle.z), 0.03f, 0.02f, MudPalette.Rubber);
            k.Plank(new Vector3(-0.21f, 0.3f, -0.24f), new Vector3(-0.23f, RearAxle.y - 0.2f, RearAxle.z), 0.03f, 0.02f, MudPalette.Rubber);
        }

        private static void Pipe(MeshKit k, Vector3[] path)
        {
            for (int i = 0; i < path.Length - 1; i++) k.Rod(path[i], path[i + 1], 0.042f, 10, Chrome);
            for (int i = 1; i < path.Length - 1; i++) k.Ball(path[i], 0.042f, Chrome, 1);
        }

        private static void TankMesh(MeshKit k)
        {
            k.Smooth = true;
            // A teardrop tank in deep purple with a lighter top and hand-painted flames licking back along each side.
            k.Ball(new Vector3(0f, 1.02f, 0.38f), new Vector3(0.18f, 0.14f, 0.34f), Quaternion.Euler(-6f, 0f, 0f), BikePaint, 2);
            k.Ball(new Vector3(0f, 1.1f, 0.4f), new Vector3(0.12f, 0.06f, 0.26f), Quaternion.Euler(-6f, 0f, 0f), PaintHi, 1);
            k.Prism(new Vector3(0f, 1.15f, 0.48f), Quaternion.identity, 0.035f, 0.035f, 0.02f, 12, Chrome); // filler cap
            foreach (float s in new[] { -1f, 1f })
            {
                for (int i = 0; i < 4; i++)
                {
                    float z = 0.6f - i * 0.05f, y = 1.0f + (i % 2 == 0 ? 0.03f : -0.025f);
                    float len = 0.2f + i * 0.04f;
                    Quaternion lick = Quaternion.Euler(i % 2 == 0 ? -10f : 8f, 0f, 0f);
                    k.Ball(new Vector3(0.158f * s, y, z - len * 0.5f), new Vector3(0.022f, 0.028f, len * 0.5f), lick, FlameRed, 1);
                    k.Ball(new Vector3(0.163f * s, y, z - len * 0.45f), new Vector3(0.018f, 0.018f, len * 0.42f), lick, FlameOrange, 1);
                    k.Ball(new Vector3(0.167f * s, y, z - len * 0.35f), new Vector3(0.014f, 0.01f, len * 0.3f), lick, FlameYellow, 1);
                }
            }
            // Dash: a speedo pod on the tank, and the nitro button (red under its flip cap).
            k.Ball(new Vector3(0f, 1.15f, 0.25f), new Vector3(0.06f, 0.03f, 0.06f), Quaternion.identity, Chrome, 1);
            k.Ball(new Vector3(0f, 1.17f, 0.25f), new Vector3(0.045f, 0.012f, 0.045f), Quaternion.identity, MudPalette.White, 0);
            k.Prism(new Vector3(0f, 1.13f, 0.17f), Quaternion.identity, 0.04f, 0.04f, 0.04f, 12, MudPalette.DarkMetal);
            k.Ball(new Vector3(0f, 1.175f, 0.17f), new Vector3(0.03f, 0.015f, 0.03f), Quaternion.identity, new Color(1f, 0.15f, 0.12f), 1, 1.2f);
        }

        private static void BackEndMesh(MeshKit k)
        {
            k.Smooth = true;
            // The saddle: low, tuck-and-roll, with a pillion hump; a tall sissy bar with a pad.
            k.Ball(Seat + new Vector3(0f, -0.06f, 0f), new Vector3(0.21f, 0.07f, 0.3f), Quaternion.identity, Saddle, 1);
            for (int i = 0; i < 4; i++)
                k.Ball(Seat + new Vector3(0f, -0.005f, -0.18f + i * 0.12f), new Vector3(0.19f, 0.022f, 0.035f), Quaternion.identity, new Color(0.28f, 0.16f, 0.1f), 1);
            k.Ball(new Vector3(0f, 0.86f, -0.66f), new Vector3(0.15f, 0.07f, 0.16f), Quaternion.Euler(-10f, 0f, 0f), Saddle, 1);
            k.Smooth = false;
            foreach (float x in new[] { -0.12f, 0.12f })
                k.Rod(new Vector3(x, 0.84f, -0.8f), new Vector3(x * 0.8f, 1.72f, -0.98f), 0.022f, 8, Chrome);
            k.Rod(new Vector3(-0.1f, 1.72f, -0.98f), new Vector3(0.1f, 1.72f, -0.98f), 0.022f, 8, Chrome);
            k.Smooth = true;
            k.Ball(new Vector3(0f, 1.25f, -0.88f), new Vector3(0.12f, 0.17f, 0.04f), Quaternion.Euler(-12f, 0f, 0f), Leather, 1);
            // Rear fender over the landing gear, with the tail light and a plate.
            k.Smooth = false;
            Arc(k, RearAxle, RearRadius + 0.08f, 0.025f, 0.24f, 15f, 170f, 14, BikePaint);
            k.Ball(TailLightAt, new Vector3(0.07f, 0.04f, 0.04f), Quaternion.identity, TailRed, 1, 2.5f);
            k.Box(TailLightAt + new Vector3(0f, -0.12f, 0.03f), new Vector3(0.2f, 0.12f, 0.015f), Quaternion.Euler(-30f, 0f, 0f), new Color(0.95f, 0.85f, 0.2f));
            // The landing gear's oleo strut, chrome over a dark sleeve, from the axle up to the fender.
            k.Rod(RearAxle + new Vector3(0.27f, 0f, 0f), RearAxle + new Vector3(0.27f, 0.36f, 0.2f), 0.045f, 10, MudPalette.DarkMetal);
            k.Rod(RearAxle + new Vector3(0.27f, 0.3f, 0.17f), RearAxle + new Vector3(0.24f, 0.62f, 0.36f), 0.03f, 10, Chrome);
            k.Prism(RearAxle + new Vector3(0.22f, 0f, 0f), Quaternion.Euler(0f, 0f, -90f), 0.07f, 0.07f, 0.06f, 12, Chrome);
            k.Prism(RearAxle + new Vector3(-0.28f, 0f, 0f), Quaternion.Euler(0f, 0f, -90f), 0.07f, 0.07f, 0.06f, 12, Chrome);
            // Studded leather saddlebags with fringe.
            foreach (float s in new[] { -1f, 1f })
            {
                k.Smooth = true;
                Vector3 bag = new Vector3(0.34f * s, 0.78f, -0.78f);
                k.Ball(bag, new Vector3(0.09f, 0.17f, 0.24f), Quaternion.identity, Leather, 1);
                k.Ball(bag + new Vector3(0.03f * s, 0.12f, 0f), new Vector3(0.08f, 0.06f, 0.25f), Quaternion.identity, LeatherHi, 1);
                for (int i = 0; i < 4; i++)
                    k.Ball(bag + new Vector3(0.09f * s, 0.06f, -0.15f + i * 0.1f), 0.016f, Chrome, 0);
                k.Smooth = false;
                for (int i = 0; i < 7; i++)
                {
                    Vector3 top = bag + new Vector3(0.07f * s, -0.14f, -0.18f + i * 0.06f);
                    k.Plank(top, top + new Vector3(0.015f * s, -0.12f, -0.02f), 0.015f, 0.006f, Leather);
                }
            }
        }

        private static void RearWheelMesh(MeshKit k)
        {
            k.Smooth = true;
            Quaternion axle = Quaternion.Euler(0f, 0f, 90f);
            Torus(k, Vector3.zero, axle, 0.39f, 0.13f, MudPalette.Rubber);
            k.Smooth = false;
            for (int i = 0; i < 16; i++) // chunky aircraft tread
            {
                Quaternion spin = Quaternion.Euler(i * 22.5f, 0f, 0f);
                k.Box(spin * new Vector3(0f, 0.505f, 0f), new Vector3(0.2f, 0.04f, 0.08f), spin, MudPalette.Rubber);
            }
            // The landing-gear hub: a bolted aluminium disc, a glowing core, and the oleo strut on the right.
            k.Prism(new Vector3(-0.17f, 0f, 0f), axle * Quaternion.Euler(180f, 0f, 0f), 0.3f, 0.3f, 0.34f, 18, MudPalette.Metal);
            k.Prism(new Vector3(-0.19f, 0f, 0f), axle * Quaternion.Euler(180f, 0f, 0f), 0.12f, 0.12f, 0.38f, 12, Hub, 3f);
            foreach (float s in new[] { -1f, 1f })
                for (int i = 0; i < 8; i++)
                {
                    Quaternion a = Quaternion.Euler(i * 45f, 0f, 0f);
                    k.Prism(new Vector3(0.18f * s, 0f, 0f) + a * new Vector3(0f, 0.2f, 0f), Quaternion.Euler(0f, 0f, -90f * s), 0.022f, 0.022f, 0.02f, 6, Chrome);
                }
        }

        private static void FrontWheelMesh(MeshKit k)
        {
            k.Smooth = true;
            Quaternion axle = Quaternion.Euler(0f, 0f, 90f);
            Torus(k, Vector3.zero, axle, 0.36f, 0.06f, MudPalette.Rubber);
            k.Smooth = false;
            Torus(k, Vector3.zero, axle, 0.31f, 0.018f, Chrome);
            k.Prism(new Vector3(-0.06f, 0f, 0f), Quaternion.Euler(0f, 0f, -90f), 0.06f, 0.06f, 0.12f, 12, Chrome);
            for (int i = 0; i < 20; i++) // laced spokes, crossing
            {
                float a = i * 18f * Mathf.Deg2Rad;
                float side = i % 2 == 0 ? -0.05f : 0.05f;
                Vector3 rim = new Vector3(0f, Mathf.Cos(a + 0.2f) * 0.3f, Mathf.Sin(a + 0.2f) * 0.3f);
                k.Rod(new Vector3(side, Mathf.Cos(a) * 0.05f, Mathf.Sin(a) * 0.05f), rim, 0.006f, 4, Chrome);
            }
        }

        private static void ForksMesh(MeshKit k)
        {
            Vector3 axle = FrontAxle - SteerHead, axis = ForkAxis;
            k.Smooth = false;
            // Triple trees, long chrome springer legs (the rear legs dark), the springs, the front fender.
            k.Box(new Vector3(0f, 0.05f, 0.02f), new Vector3(0.28f, 0.04f, 0.12f), Chrome);
            k.Box(axis * 0.1f + new Vector3(0f, 0f, 0.02f), new Vector3(0.28f, 0.04f, 0.12f), Chrome);
            foreach (float x in new[] { -0.11f, 0.11f })
            {
                k.Rod(new Vector3(x, 0.07f, 0.03f), axle + new Vector3(x * 0.85f, 0f, 0f), 0.028f, 10, Chrome);
                k.Rod(new Vector3(x, 0.02f, -0.04f) + axis * 0.15f, axle + new Vector3(x * 0.85f, 0.06f, -0.08f), 0.016f, 8, MudPalette.DarkMetal);
                for (int i = 0; i < 6; i++)
                    k.Prism(new Vector3(x, 0.1f, 0.03f) + axis * (0.12f + i * 0.025f), Quaternion.FromToRotation(Vector3.up, axis), 0.035f, 0.035f, 0.012f, 10, Chrome);
            }
            k.Rod(axle + new Vector3(-0.1f, 0f, 0f), axle + new Vector3(0.1f, 0f, 0f), 0.02f, 6, Chrome);
            Vector3 fenderCentre = axle;
            k.Smooth = false;
            Arc(k, fenderCentre, FrontRadius + 0.05f, 0.02f, 0.12f, 60f, 140f, 8, BikePaint);
            // A little chrome mouse skull on the fender.
            k.Smooth = true;
            Vector3 skull = fenderCentre + new Vector3(0f, (FrontRadius + 0.08f) * Mathf.Sin(100f * Mathf.Deg2Rad), (FrontRadius + 0.08f) * Mathf.Cos(100f * Mathf.Deg2Rad));
            k.Ball(skull, new Vector3(0.04f, 0.035f, 0.05f), Quaternion.identity, Chrome, 1);
            foreach (float x in new[] { -0.035f, 0.035f }) k.Ball(skull + new Vector3(x, 0.035f, -0.01f), new Vector3(0.025f, 0.025f, 0.008f), Quaternion.identity, Chrome, 1);
            // The headlight bucket and lens.
            k.Smooth = false;
            Vector3 lamp = new Vector3(0f, -0.1f, 0.2f);
            k.Prism(lamp, Quaternion.Euler(90f, 0f, 0f), 0.1f, 0.14f, 0.12f, 16, Chrome);
            k.Smooth = true;
            k.Ball(lamp + new Vector3(0f, 0f, 0.12f), new Vector3(0.125f, 0.125f, 0.03f), Quaternion.identity, Lamp, 1, 3f);
            k.Smooth = false;
            k.Rod(new Vector3(-0.1f, 0.02f, 0.04f), lamp + new Vector3(-0.06f, 0f, 0f), 0.015f, 6, Chrome);
            k.Rod(new Vector3(0.1f, 0.02f, 0.04f), lamp + new Vector3(0.06f, 0f, 0f), 0.015f, 6, Chrome);
            // Ape-hanger bars: risers, tall uprights, back to the grips; rubber grips with leather tassels; mirrors.
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 riser = new Vector3(0.09f * s, 0.06f, 0.0f), top = new Vector3(0.12f * s, 0.2f, -0.02f);
                Vector3 up = new Vector3(0.2f * s, 0.6f, -0.3f), grip = new Vector3(0.44f * s, 0.59f, -0.41f);
                k.Rod(riser, top, 0.03f, 8, Chrome);
                k.Rod(top, up, 0.022f, 8, Chrome);
                k.Ball(up, 0.022f, Chrome, 0);
                k.Rod(up, grip, 0.022f, 8, Chrome);
                k.Rod(grip, grip + new Vector3(0.14f * s, -0.01f, -0.01f), 0.032f, 10, MudPalette.Rubber);
                for (int i = 0; i < 3; i++)
                    k.Plank(grip + new Vector3((0.15f + i * 0.008f) * s, -0.01f, 0f), grip + new Vector3((0.15f + i * 0.008f) * s, -0.16f, -0.02f + i * 0.01f), 0.01f, 0.004f, Leather);
                k.Rod(up + new Vector3(0.02f * s, 0f, 0f), up + new Vector3(0.1f * s, 0.18f, 0.02f), 0.01f, 6, Chrome);
                k.Ball(up + new Vector3(0.1f * s, 0.22f, 0.02f), new Vector3(0.05f, 0.04f, 0.012f), Quaternion.identity, Chrome, 1);
            }
            k.Rod(new Vector3(-0.2f, 0.6f, -0.3f), new Vector3(0.2f, 0.6f, -0.3f), 0.018f, 8, Chrome); // cross bar
        }

        // ---------- The rider ----------

        private static void HipsMesh(MeshKit k)
        {
            k.Smooth = true;
            k.Ball(new Vector3(0f, 0.02f, 0f), new Vector3(0.24f, 0.17f, 0.2f), Quaternion.identity, Denim, 1);
            k.Ball(new Vector3(0f, 0.1f, 0f), new Vector3(0.25f, 0.06f, 0.21f), Quaternion.identity, Leather, 1); // belt
            // The buckle: a gold cheese wedge with holes.
            Wedge(k, new Vector3(0f, 0.1f, 0.205f), Quaternion.identity, 0.085f, 0.03f);
            foreach (float x in new[] { -0.14f, 0.14f }) // pocket rivets
                k.Ball(new Vector3(x, 0.03f, 0.17f), 0.012f, Gold, 0);
        }

        // A wallet chain swinging from the belt on his right hip.
        private static void BeltChainMesh(MeshKit k) => ChainArc(k, new Vector3(0.18f, 0.08f, 0.12f), new Vector3(0.2f, 0.06f, -0.15f), 0.13f, 9);

        private static void TorsoMesh(MeshKit k)
        {
            k.Smooth = true;
            // A black leather biker jacket over a red shirt: barrel chest, belly, padded shoulders, wide lapels.
            k.Ball(new Vector3(0f, 0.32f, 0f), new Vector3(0.3f, 0.32f, 0.23f), Quaternion.identity, Leather, 2);
            k.Ball(new Vector3(0f, 0.12f, 0.03f), new Vector3(0.27f, 0.17f, 0.22f), Quaternion.identity, Leather, 1);
            k.Ball(new Vector3(0f, 0.32f, 0.16f), new Vector3(0.11f, 0.24f, 0.08f), Quaternion.identity, Shirt, 1);
            k.Ball(new Vector3(0f, 0.45f, 0.18f), new Vector3(0.07f, 0.09f, 0.05f), Quaternion.identity, FurLight, 1); // chest fur
            foreach (float s in new[] { -1f, 1f })
            {
                k.Ball(new Vector3(0.27f * s, 0.5f, 0f), new Vector3(0.13f, 0.08f, 0.14f), Quaternion.identity, LeatherHi, 1); // epaulets
                k.Ball(new Vector3(0.13f * s, 0.5f, 0.17f), new Vector3(0.1f, 0.12f, 0.035f), Quaternion.Euler(0f, -22f * s, -24f * s), LeatherHi, 1); // lapels
                for (int i = 0; i < 3; i++) // shoulder studs
                    k.Cone(new Vector3((0.21f + i * 0.05f) * s, 0.565f, 0f), Quaternion.identity, 0.018f, 0.03f, 6, Chrome);
            }
            // The upturned collar round the back of the neck.
            k.Ball(new Vector3(0f, 0.6f, -0.06f), new Vector3(0.2f, 0.08f, 0.14f), Quaternion.Euler(-20f, 0f, 0f), LeatherHi, 1);
            // The diagonal zip and snaps.
            k.Smooth = false;
            k.Plank(new Vector3(-0.13f, 0.08f, 0.215f), new Vector3(-0.15f, 0.45f, 0.18f), 0.016f, 0.01f, Chrome);
            k.Smooth = true;
            foreach (Vector3 snap in new[] { new Vector3(0.2f, 0.42f, 0.16f), new Vector3(-0.2f, 0.42f, 0.16f), new Vector3(0.2f, 0.1f, 0.18f) })
                k.Ball(snap, 0.016f, Chrome, 0);
            // The club patch on his back: a dark disc, gold top and bottom rockers, a winged cheese wedge.
            k.Ball(new Vector3(0f, 0.34f, -0.2f), new Vector3(0.16f, 0.16f, 0.04f), Quaternion.identity, MudPalette.Black, 1);
            k.Ball(new Vector3(0f, 0.53f, -0.175f), new Vector3(0.14f, 0.028f, 0.02f), Quaternion.Euler(-30f, 0f, 0f), Gold, 1); // top rocker
            k.Ball(new Vector3(0f, 0.15f, -0.2f), new Vector3(0.13f, 0.026f, 0.02f), Quaternion.Euler(25f, 0f, 0f), Gold, 1); // bottom rocker
            k.Smooth = false;
            Wedge(k, new Vector3(0f, 0.33f, -0.246f), Quaternion.Euler(0f, 180f, 0f), 0.1f, 0.02f);
            k.Smooth = true;
            foreach (float s in new[] { -1f, 1f })
                k.Ball(new Vector3(0.12f * s, 0.37f, -0.246f), new Vector3(0.06f, 0.025f, 0.008f), Quaternion.Euler(0f, 0f, 20f * s), MudPalette.White, 1);
        }

        private void BuildHead()
        {
            MudArt.Part(head, "Skull", Vector3.zero, Quaternion.identity, "clutch2_head", HeadMesh);
            glasses = MudArt.Part(head, "Aviators", GlassesUp, Quaternion.Euler(4f, 0f, 0f), "clutch_aviators", AviatorsMesh);
            toothpick = MudArt.Part(head, "Toothpick", new Vector3(0.07f, 0.02f, 0.37f), Quaternion.Euler(10f, 40f, 0f), "clutch2_toothpick",
                k => k.Rod(Vector3.zero, new Vector3(0f, 0.01f, 0.16f), 0.008f, 5, new Color(0.9f, 0.8f, 0.55f)));
            foreach (float s in new[] { -1f, 1f })
            {
                Transform ear = MudArt.Pivot(head, s < 0f ? "Ear L" : "Ear R", new Vector3(0.21f * s, 0.43f, -0.04f), Quaternion.Euler(-8f, 0f, 28f * -s));
                MudArt.Part(ear, "Ear", Vector3.zero, Quaternion.identity, s < 0f ? "clutch2_ear_l" : "clutch2_ear_r", k =>
                {
                    k.Smooth = true;
                    k.Ball(new Vector3(0f, 0.17f, 0f), new Vector3(0.19f, 0.19f, 0.045f), Quaternion.identity, Fur, 1);
                    k.Ball(new Vector3(0f, 0.17f, 0.025f), new Vector3(0.14f, 0.14f, 0.025f), Quaternion.identity, Pink, 1);
                    k.Ball(new Vector3(0.12f * s, 0.29f, 0.01f), new Vector3(0.03f, 0.03f, 0.05f), Quaternion.identity, Fur, 0); // notch tuft
                    if (s < 0f) // a gold hoop in the left ear
                    {
                        k.Smooth = false;
                        Torus(k, new Vector3(-0.1f, 0.05f, 0.02f), Quaternion.Euler(90f, 0f, 0f), 0.035f, 0.008f, Gold);
                    }
                });
                jiggles.Add(new Jiggle(ear, new Vector3(0f, 0.3f, 0f), 60f, 6f, 0.5f));
                // The bandana's knot tails at the back of his head.
                Transform tail = MudArt.Pivot(head, "Bandana Tail", new Vector3(0.07f * s, 0.34f, -0.24f), Quaternion.Euler(30f, 15f * s, 10f * s));
                MudArt.Part(tail, "Cloth", Vector3.zero, Quaternion.identity, "clutch2_bandana_tail", k =>
                {
                    k.Smooth = true;
                    k.Ball(new Vector3(0f, -0.1f, 0f), new Vector3(0.04f, 0.11f, 0.015f), Quaternion.identity, Bandana, 1);
                });
                jiggles.Add(new Jiggle(tail, new Vector3(0f, -0.2f, 0f), 25f, 3f, 3f));
            }
            (Transform eyeL, Transform eyeR) = MudArt.Eyes(head, new Vector3(0f, 0.25f, 0.24f), 0.1f, 0.05f, MudPalette.White, MudPalette.Black, "clutch2");
            Transform browL = BossFace.Brow(head, "clutch2_brow", new Vector3(-0.1f, 0.345f, 0.25f), -1f, new Vector3(0.14f, 0.04f, 0.05f), FurDark, 6f);
            Transform browR = BossFace.Brow(head, "clutch2_brow", new Vector3(0.1f, 0.345f, 0.25f), 1f, new Vector3(0.14f, 0.04f, 0.05f), FurDark, 6f);
            // Under the snout, angled to be seen from the front: a smirk with two buck teeth.
            Transform mouth = BossFace.Mouth(head, "clutch2_mouth", new Vector3(0.02f, 0.015f, 0.39f), 0.13f, 0.05f, FurDark, true, Quaternion.Euler(-35f, 0f, 0f));
            FaceParts = new BossFace.Rig
            {
                BrowL = browL, BrowR = browR, EyeL = eyeL, EyeR = eyeR, Mouth = mouth, Body = spine,
                BrowTravel = 0.025f, Idle = Mood.Smug, Fight = Mood.Smug,
            };
        }

        private static void HeadMesh(MeshKit k)
        {
            k.Smooth = true;
            // A big round mouse head, chubby cheeks, a long snout with a pink nose, fluffy sideburns, whiskers; a red
            // bandana with white dots over the crown.
            k.Ball(new Vector3(0f, 0.22f, 0f), new Vector3(0.27f, 0.25f, 0.26f), Quaternion.identity, Fur, 2);
            k.Ball(new Vector3(0f, 0.04f, 0.02f), new Vector3(0.12f, 0.08f, 0.1f), Quaternion.identity, Fur, 1); // neck
            foreach (float s in new[] { -1f, 1f })
            {
                k.Ball(new Vector3(0.14f * s, 0.12f, 0.14f), new Vector3(0.12f, 0.1f, 0.11f), Quaternion.identity, FurLight, 1); // cheeks
                k.Ball(new Vector3(0.23f * s, 0.12f, 0.04f), new Vector3(0.06f, 0.12f, 0.08f), Quaternion.Euler(0f, 0f, -15f * s), FurDark, 1); // sideburns
            }
            k.Lathe(new Vector3(0f, 0.14f, 0.12f), Quaternion.Euler(100f, 0f, 0f), new[]
            {
                new Vector2(0.0001f, -0.02f), new Vector2(0.15f, 0.02f), new Vector2(0.14f, 0.14f), new Vector2(0.09f, 0.28f), new Vector2(0.045f, 0.34f), new Vector2(0.0001f, 0.36f)
            }, 14, Fur);
            k.Ball(new Vector3(0f, 0.09f, 0.33f), new Vector3(0.1f, 0.05f, 0.09f), Quaternion.identity, FurLight, 1); // muzzle underside
            k.Ball(new Vector3(0f, 0.1f, 0.48f), new Vector3(0.06f, 0.05f, 0.05f), Quaternion.identity, Pink, 1); // nose
            k.Ball(new Vector3(0.015f, 0.12f, 0.51f), new Vector3(0.015f, 0.01f, 0.01f), Quaternion.identity, MudPalette.White, 0); // nose shine
            k.Smooth = false;
            foreach (float s in new[] { -1f, 1f })
                for (int i = -1; i <= 1; i++)
                    k.Plank(new Vector3(0.07f * s, 0.09f + i * 0.015f, 0.42f), new Vector3(0.32f * s, 0.11f + i * 0.06f, 0.4f - Mathf.Abs(i) * 0.02f), 0.007f, 0.007f, MudPalette.White);
            k.Smooth = true;
            // The bandana: a cap over the crown down to the brow, a band round the back, white dots.
            k.Ball(new Vector3(0f, 0.32f, -0.02f), new Vector3(0.28f, 0.17f, 0.27f), Quaternion.Euler(-12f, 0f, 0f), Bandana, 2);
            k.Ball(new Vector3(0f, 0.34f, -0.24f), new Vector3(0.07f, 0.06f, 0.05f), Quaternion.identity, Bandana, 1); // knot
            for (int i = 0; i < 9; i++)
            {
                float a = i / 9f * Mathf.PI * 2f;
                Vector3 n = Quaternion.Euler(-12f, 0f, 0f) * new Vector3(Mathf.Cos(a) * 0.7f, 0.7f, Mathf.Sin(a) * 0.7f).normalized;
                k.Ball(new Vector3(0f, 0.32f, -0.02f) + Vector3.Scale(n, new Vector3(0.28f, 0.17f, 0.27f)), 0.018f, MudPalette.White, 0);
            }
        }

        /// <summary>Gold-rimmed teardrop aviators (also the trophy).</summary>
        public static void AviatorsMesh(MeshKit k)
        {
            k.Smooth = true;
            foreach (float s in new[] { -1f, 1f })
            {
                Quaternion tilt = Quaternion.Euler(0f, 0f, 12f * s);
                k.Ball(new Vector3(0.1f * s, -0.01f, 0f), new Vector3(0.09f, 0.075f, 0.018f), tilt, new Color(0.08f, 0.1f, 0.16f), 1);
                k.Ball(new Vector3(0.12f * s, 0.02f, 0.012f), new Vector3(0.025f, 0.015f, 0.006f), tilt, new Color(0.6f, 0.75f, 0.95f), 0); // glint
                k.Smooth = false;
                Torus(k, new Vector3(0.1f * s, -0.01f, -0.002f), Quaternion.Euler(90f, 0f, 0f) * Quaternion.Euler(0f, 12f * s, 0f), 0.085f, 0.008f, Gold, 0.82f);
                k.Rod(new Vector3(0.18f * s, 0.02f, -0.01f), new Vector3(0.26f * s, 0.03f, -0.2f), 0.007f, 5, Gold); // temple arm
                k.Smooth = true;
            }
            k.Smooth = false;
            k.Rod(new Vector3(-0.03f, 0.045f, 0f), new Vector3(0.03f, 0.045f, 0f), 0.008f, 5, Gold); // double bridge
            k.Rod(new Vector3(-0.025f, 0.015f, 0f), new Vector3(0.025f, 0.015f, 0f), 0.006f, 5, Gold);
        }

        private Transform BuildArm(float side, out Transform elbow, out Transform hand)
        {
            Transform shoulder = MudArt.Pivot(spine, side < 0f ? "Arm L" : "Arm R", new Vector3(ShoulderAt.x * side, ShoulderAt.y, ShoulderAt.z));
            MudArt.Part(shoulder, "Sleeve", Vector3.zero, Quaternion.identity, "clutch2_upper", k =>
            {
                k.Smooth = true;
                k.Ball(Vector3.zero, 0.12f, Leather, 1);
                k.Rod(Vector3.zero, new Vector3(0f, -UpperArm, 0f), 0.105f, 10, Leather, 0f, 0.09f);
                k.Ball(new Vector3(0f, -UpperArm, 0f), 0.09f, Leather, 1);
            });
            elbow = MudArt.Pivot(shoulder, "Elbow", new Vector3(0f, -UpperArm, 0f));
            MudArt.Part(elbow, "Forearm", Vector3.zero, Quaternion.identity, "clutch2_fore", k =>
            {
                k.Smooth = true;
                k.Rod(Vector3.zero, new Vector3(0f, -Forearm + 0.08f, 0f), 0.09f, 10, Leather, 0f, 0.085f);
                k.Smooth = false;
                k.Prism(new Vector3(0f, -Forearm + 0.06f, 0f), Quaternion.identity, 0.092f, 0.092f, 0.03f, 12, Chrome); // zip cuff
                k.Smooth = true;
                k.Ball(new Vector3(0f, -Forearm + 0.02f, 0f), new Vector3(0.07f, 0.05f, 0.07f), Quaternion.identity, Fur, 1); // wrist
            });
            hand = MudArt.Pivot(elbow, "Hand", new Vector3(0f, -Forearm, 0f));
            MudArt.Part(hand, "Glove", Vector3.zero, Quaternion.identity, side < 0f ? "clutch2_hand_l" : "clutch2_hand_r", k => HandMesh(k, side));
            return shoulder;
        }

        // A fist in a fingerless black glove (furry fingers). Fingers curl down the hand's -Y; the palm faces +Z.
        private static void HandMesh(MeshKit k, float side)
        {
            k.Smooth = true;
            k.Ball(new Vector3(0f, -0.07f, 0f), new Vector3(0.085f, 0.085f, 0.06f), Quaternion.identity, Leather, 1);
            k.Ball(new Vector3(0f, -0.06f, -0.045f), new Vector3(0.05f, 0.04f, 0.012f), Quaternion.identity, Chrome, 0); // knuckle stud
            for (int i = 0; i < 4; i++)
            {
                float x = (i - 1.5f) * 0.038f;
                k.Ball(new Vector3(x, -0.14f, 0.02f), new Vector3(0.022f, 0.03f, 0.03f), Quaternion.identity, Fur, 1);
                k.Ball(new Vector3(x, -0.13f, 0.055f), new Vector3(0.019f, 0.02f, 0.02f), Quaternion.identity, Pink, 0);
            }
            k.Ball(new Vector3(-0.07f * side, -0.08f, 0.045f), new Vector3(0.025f, 0.04f, 0.025f), Quaternion.Euler(0f, 0f, 30f * side), Fur, 1); // thumb
        }

        private Transform BuildLeg(float side, out Transform knee, out Transform foot)
        {
            Transform hip = MudArt.Pivot(body, side < 0f ? "Leg L" : "Leg R", new Vector3(HipJointAt.x * side, HipJointAt.y, HipJointAt.z));
            MudArt.Part(hip, "Thigh", Vector3.zero, Quaternion.identity, "clutch2_thigh", k =>
            {
                k.Smooth = true;
                k.Ball(Vector3.zero, 0.12f, Denim, 1);
                k.Rod(Vector3.zero, new Vector3(0f, -Thigh, 0f), 0.115f, 10, Denim, 0f, 0.095f);
                k.Ball(new Vector3(0f, -Thigh, 0f), 0.095f, Denim, 1);
                k.Ball(new Vector3(0f, -Thigh + 0.02f, 0.06f), new Vector3(0.05f, 0.06f, 0.04f), Quaternion.identity, DenimDark, 0); // worn knee
            });
            knee = MudArt.Pivot(hip, "Knee", new Vector3(0f, -Thigh, 0f));
            MudArt.Part(knee, "Shin", Vector3.zero, Quaternion.identity, "clutch2_shin", k =>
            {
                k.Smooth = true;
                k.Rod(Vector3.zero, new Vector3(0f, -Shin * 0.45f, 0f), 0.092f, 10, Denim, 0f, 0.1f);
                // An engineer boot: a tall shaft with a buckled strap.
                k.Rod(new Vector3(0f, -Shin * 0.42f, 0f), new Vector3(0f, -Shin, 0f), 0.1f, 12, Boot, 0f, 0.085f);
                k.Smooth = false;
                k.Prism(new Vector3(0f, -Shin * 0.5f, 0f), Quaternion.identity, 0.104f, 0.104f, 0.03f, 12, Leather);
                k.Box(new Vector3(0.1f * side, -Shin * 0.5f + 0.015f, 0f), new Vector3(0.012f, 0.04f, 0.05f), Chrome);
            });
            foot = MudArt.Pivot(knee, "Foot", new Vector3(0f, -Shin, 0f));
            MudArt.Part(foot, "Boot", Vector3.zero, Quaternion.identity, "clutch2_boot", BootMesh);
            return hip;
        }

        // A chunky boot, toe along +Z, sole at y = -Ankle.
        private static void BootMesh(MeshKit k)
        {
            k.Smooth = true;
            k.Ball(new Vector3(0f, -0.04f, 0.06f), new Vector3(0.095f, 0.075f, 0.17f), Quaternion.identity, Boot, 1);
            k.Ball(new Vector3(0f, -0.03f, 0.17f), new Vector3(0.085f, 0.06f, 0.07f), Quaternion.identity, Boot, 1); // round toe
            k.Smooth = false;
            k.Box(new Vector3(0f, -Ankle + 0.015f, 0.07f), new Vector3(0.19f, 0.03f, 0.36f), Sole);
            k.Box(new Vector3(0f, -Ankle + 0.035f, -0.07f), new Vector3(0.16f, 0.04f, 0.09f), Sole); // heel
            k.Box(new Vector3(0f, -0.02f, 0.02f), new Vector3(0.2f, 0.025f, 0.03f), Leather); // instep strap
            k.Box(new Vector3(0.1f, -0.02f, 0.02f), new Vector3(0.012f, 0.04f, 0.045f), Chrome);
        }

        private void BuildTail()
        {
            Transform parent = body;
            Vector3 at = new Vector3(0f, -0.02f, -0.2f);
            float radius = 0.045f;
            for (int i = 0; i < 4; i++)
            {
                Transform segment = MudArt.Pivot(parent, "Tail " + (i + 1), at, i == 0 ? Quaternion.Euler(10f, 0f, 0f) : Quaternion.Euler(-10f, 0f, 0f));
                float r0 = radius, r1 = radius * 0.78f;
                MudArt.Part(segment, "Piece", Vector3.zero, Quaternion.identity, "clutch2_tail_" + i, k =>
                {
                    k.Smooth = true;
                    k.Rod(Vector3.zero, new Vector3(0f, 0f, -0.27f), r0, 8, Pink, 0f, r1);
                    k.Ball(new Vector3(0f, 0f, -0.27f), r1, Pink, 0);
                    for (int j = 1; j < 3; j++) k.Ball(new Vector3(0f, 0f, -0.09f * j), new Vector3(r0 * 1.05f, r0 * 1.05f, 0.008f), Quaternion.identity, PinkDark, 0); // rings
                });
                jiggles.Add(new Jiggle(segment, new Vector3(0f, 0f, -0.27f), 45f - i * 6f, 4f, 0.4f));
                parent = segment;
                at = new Vector3(0f, 0f, -0.27f);
                radius = r1;
            }
        }

        // ---------- Mesh helpers ----------

        private enum Plane { YZ, XY }

        // A curved strip (a fender, a patch rocker): an arc round a centre, angles in degrees from +Z toward +Y (YZ plane,
        // width along X) or from +X toward +Y (XY plane, width along Z).
        private static void Arc(MeshKit k, Vector3 centre, float radius, float thickness, float width, float from, float to, int segments, Color color,
            Plane plane = Plane.YZ)
        {
            Vector3 P(float degrees, float r, float w)
            {
                float a = degrees * Mathf.Deg2Rad;
                return plane == Plane.YZ
                    ? centre + new Vector3(w, Mathf.Sin(a) * r, Mathf.Cos(a) * r)
                    : centre + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, w);
            }
            float h = width * 0.5f, ro = radius + thickness * 0.5f, ri = radius - thickness * 0.5f;
            for (int i = 0; i < segments; i++)
            {
                float a0 = Mathf.Lerp(from, to, (float)i / segments), a1 = Mathf.Lerp(from, to, (float)(i + 1) / segments);
                Vector3 mid = (P(a0, radius, 0f) + P(a1, radius, 0f)) * 0.5f;
                Vector3 outward = (mid - centre).normalized;
                Vector3 across = plane == Plane.YZ ? Vector3.right : Vector3.forward;
                Side(k, P(a0, ro, -h), P(a0, ro, h), P(a1, ro, h), P(a1, ro, -h), outward, color);
                Side(k, P(a0, ri, -h), P(a0, ri, h), P(a1, ri, h), P(a1, ri, -h), -outward, color);
                Side(k, P(a0, ri, h), P(a0, ro, h), P(a1, ro, h), P(a1, ri, h), across, color);
                Side(k, P(a0, ri, -h), P(a0, ro, -h), P(a1, ro, -h), P(a1, ri, -h), -across, color);
            }
        }

        // A quad wound to face a direction.
        private static void Side(MeshKit k, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward, Color color)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) >= 0f) k.Quad(a, b, c, d, color);
            else k.Quad(d, c, b, a, color);
        }

        // A ring (tyre, rim, hoop) round local +Y of the rotation.
        private static void Torus(MeshKit k, Vector3 centre, Quaternion rotation, float radius, float tube, Color color, float glow = 0f)
        {
            const int steps = 12;
            var profile = new Vector2[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                float a = -Mathf.PI * 0.5f + i * Mathf.PI * 2f / steps;
                profile[i] = new Vector2(radius + Mathf.Cos(a) * tube, Mathf.Sin(a) * tube);
            }
            k.Lathe(centre, rotation, profile, 24, color, glow);
        }

        // A gold cheese wedge with holes, facing +Z (belt buckle, air cleaner, back patch).
        private static void Wedge(MeshKit k, Vector3 at, Quaternion rotation, float size, float depth)
        {
            Vector3 a = at + rotation * new Vector3(-size, -size * 0.55f, 0f), b = at + rotation * new Vector3(size, -size * 0.55f, 0f), c = at + rotation * new Vector3(-size * 0.6f, size * 0.6f, 0f);
            Vector3 back = rotation * Vector3.back * depth, front = rotation * Vector3.forward;
            Side(k, a, b, c, c, front, Cheese);
            Side(k, a + back, b + back, c + back, c + back, -front, Cheese);
            Side(k, a, b, b + back, a + back, rotation * Vector3.down, CheeseHole);
            Side(k, b, c, c + back, b + back, rotation * new Vector3(1f, 1f, 0f), CheeseHole);
            Side(k, c, a, a + back, c + back, rotation * Vector3.left, CheeseHole);
            foreach (Vector3 hole in new[] { new Vector3(-0.35f, -0.15f, 0f), new Vector3(0.25f, -0.25f, 0f), new Vector3(-0.45f, 0.3f, 0f) })
                k.Ball(at + rotation * (hole * size + Vector3.forward * 0.002f), new Vector3(size * 0.14f, size * 0.14f, size * 0.05f), rotation, CheeseHole, 0);
        }

        // Chain links along -Y from the hand, alternating flat and edge-on.
        private static void ChainMesh(MeshKit k, int links, float spacing)
        {
            for (int i = 0; i < links; i++)
                Torus(k, new Vector3(0f, -i * spacing, 0f), Quaternion.Euler(0f, i % 2 == 0 ? 0f : 90f, 90f), 0.035f, 0.011f, Chrome);
            k.Ball(new Vector3(0f, -links * spacing, 0f), 0.04f, Chrome, 0);
        }

        // A drooping chain between two points.
        private static void ChainArc(MeshKit k, Vector3 from, Vector3 to, float sag, int links)
        {
            for (int i = 0; i <= links; i++)
            {
                float t = (float)i / links;
                Vector3 p = Vector3.Lerp(from, to, t) + Vector3.down * Mathf.Sin(t * Mathf.PI) * sag;
                Torus(k, p, Quaternion.Euler(i % 2 == 0 ? 0f : 90f, 0f, 0f), 0.018f, 0.006f, Chrome);
            }
        }

        protected override Action<MeshKit> KeyItemModel() => null;

        // =====================================================================================================
        // Entrance: the Akira slide
        // =====================================================================================================

        // What the rig is doing in the entrance (Fight = the fight's own states drive it).
        private enum Act { Fight, Ride, Slide, Parked, Glasses, Fist }
        private Act act = Act.Fight;
        private float slideProgress, actTime;

        private const float SlideSpeed = 24f, SlideLength = 11f, SlowMotion = 0.28f;

        private Vector3 RoutePoint(int i, Vector3 home) =>
            i < escapeRoute.Length && escapeRoute[i] != null ? escapeRoute[i].position : home + new Vector3(-22f - i * 30f, 0f, 6f + i * 8f);

        /// <summary>
        /// The neon comes on; an engine echoes down the causeway and a headlight comes out of the fog; he blasts past
        /// the camera, a tracking shot alongside; then the Akira slide, shot from behind on the road: he throws the
        /// chopper sideways and slides away from the lens in slow motion, the bike laid over toward it, his left boot
        /// braking in a fan of sparks, smoke trailing from both tyres and the boot, his tail light streaking; he looks
        /// back over his shoulder at us. He kicks the bike up, lowers his aviators ("You're in my lane, pal."), throws a
        /// fist up, revs into a sheet of flame, and the neon flares as his gang rides out: his name.
        /// </summary>
        public override IEnumerator Entrance(BossEntrance cine)
        {
            Vector3 home = transform.parent != null ? transform.parent.position : transform.position;
            Transform neon = transform.parent != null ? transform.parent.Find("Neon") : null;
            Light[] signs = neon != null ? neon.GetComponentsInChildren<Light>(true) : new Light[0];
            var signLevels = new float[signs.Length];
            for (int i = 0; i < signs.Length; i++) { signLevels[i] = signs[i].intensity; signs[i].enabled = false; }
            Camera cam = cine.Camera;
            float fov = cam != null ? cam.fieldOfView : 50f;
            titleShown = false;
            bool ganged = false;
            lookWeight = 1f;

            // The road in: the causeway's last straight, the bend, then straight into the lot to where he stops.
            Vector3 r0 = RoutePoint(0, home), r1 = RoutePoint(1, home), r2 = RoutePoint(2, home);
            Vector3 u = (r1 - r2).Flatten().normalized;
            Vector3 d = (home - r0).Flatten().normalized;
            if (d.sqrMagnitude < 0.5f) d = Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, d); // the slide camera's right
            var path = new List<Vector3> { r1 - u * 40f, r1, r0, home };
            float total = PathLength(path);
            float kickAt = total - SlideLength;
            Vector3 kick = home - d * SlideLength;

            // He starts out of sight, up the causeway.
            act = Act.Ride;
            slideProgress = 0f;
            PlaceOnPath(path, 0f);
            SnapRig();

            // ---- 1. The roadhouse sign flickers on.
            if (!cine.Skipped)
            {
                Vector3 look = neon != null ? neon.position : home + Vector3.forward * 20f + Vector3.up * 6f;
                Vector3 toSign = (look - home).Flatten().normalized;
                Vector3 from = home - toSign * 4f + Vector3.Cross(Vector3.up, toSign) * 3f + Vector3.up * 1.6f;
                cine.Cut(from, look);
                StartCoroutineSafe(cine.Move(from + (look - from).normalized * 3f, look, 1.25f));
                for (int i = 0; i < 7 && !cine.Skipped; i++)
                {
                    for (int s = 0; s < signs.Length; s++) signs[s].enabled = (i + s) % 2 == 1 || i >= 5;
                    Sfx.Play("electric_sizzle", 0.4f, 1.2f + i * 0.05f);
                    yield return cine.Wait(i < 5 ? 0.16f : 0.25f);
                }
                foreach (Light sign in signs) sign.enabled = true;
            }

            // ---- 2. An engine echoes; a headlight in the fog, coming fast; he blasts past the lens.
            float dist = 0f;
            headlight.intensity = 6f;
            tailTrail.Clear();
            if (!cine.Skipped)
            {
                Sfx.Play("rev", 1f, 0.75f);
                Vector3 camAt = r1 - Vector3.Cross(Vector3.up, u) * 3.4f + Vector3.up * 0.7f + u; // on the outside of the bend he takes
                cine.Cut(camAt, r1 - u * 30f + Vector3.up * 1.2f);
                if (cam != null) cam.fieldOfView = 40f;
                bool passed = false;
                float passDist = PathDistanceOf(path, r1) + 9f;
                for (float t = 0f; dist < passDist && t < 5f && !cine.Tick(); t += Time.deltaTime)
                {
                    dist += SlideSpeed * Time.deltaTime;
                    PlaceOnPath(path, dist);
                    Animate(Time.deltaTime);
                    Backfire(0.06f);
                    // The camera holds until he is close, then whips round after him as he goes by.
                    Vector3 to = transform.position + Vector3.up * 1.2f - cam.transform.position;
                    float rate = to.magnitude < 9f ? 14f : 4f;
                    cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, Quaternion.LookRotation(to), 1f - Mathf.Exp(-rate * Time.deltaTime));
                    if (!passed && Vector3.Dot(transform.position - camAt, u) > -1f)
                    {
                        passed = true;
                        CameraShake.Add(0.3f);
                        Sfx.Play("blackout_whoosh", 0.7f, 1.4f);
                    }
                    yield return null;
                }
            }

            // ---- 3. Alongside him at wheel height, flat out down the causeway.
            if (!cine.Skipped)
            {
                if (cam != null) cam.fieldOfView = 46f;
                engineBlast = 1f;
                for (float t = 0f; dist < kickAt - 1.8f && t < 4f && !cine.Tick(); t += Time.deltaTime)
                {
                    dist += SlideSpeed * Time.deltaTime;
                    PlaceOnPath(path, dist);
                    Animate(Time.deltaTime);
                    Backfire(0.05f);
                    Vector3 right = transform.right, ahead = transform.forward;
                    Vector3 eye = transform.position + right * 3.1f + ahead * 1.5f + Vector3.up * 0.75f;
                    cine.Cut(eye, transform.position + Vector3.up * 1.05f + ahead * 0.2f);
                    yield return null;
                }
            }

            // ---- 4. The Akira slide: from behind, down on the road.
            if (!cine.Skipped)
            {
                // He's just ahead of the lens, tearing away up the lot...
                Vector3 camStart = kick - d * 3.8f + side * 0.35f + Vector3.up * 1.25f;
                cine.Cut(camStart, kick + d * 8f + Vector3.up * 0.7f);
                if (cam != null) cam.fieldOfView = 31f;
                while (dist < kickAt && !cine.Tick())
                {
                    dist += SlideSpeed * Time.deltaTime;
                    PlaceOnPath(path, dist);
                    act = Act.Ride;
                    slideProgress = Mathf.Clamp01(1f - (kickAt - dist) / 1.8f); // the flick into the lean
                    Animate(Time.deltaTime);
                    yield return null;
                }
                // ...and throws it sideways.
                Sfx.Play("car_skid", 1f, 0.62f);
                Sfx.Play("electric_sizzle", 0.6f, 0.5f);
                Face?.Show(Mood.Smug, 8f);
                act = Act.Slide;
                actTime = 0f;
                float speed = SlideSpeed, slid = 0f, nextMark = 0f, nextPuff = 0f, slow = 1f;
                float decel = SlideSpeed * SlideSpeed / (2f * SlideLength);
                Vector3 start = Ground(kick);
                tailTrail.emitting = true;
                for (float t = 0f; !cine.Tick(); t += Time.deltaTime)
                {
                    // Slow motion for everything (sparks, smoke, dust), slowest through the middle of the slide.
                    float aim = slideProgress < 0.9f ? SlowMotion : 1f;
                    slow = Mathf.MoveTowards(slow, aim, Time.unscaledDeltaTime * (aim < slow ? 3.5f : 1.6f));
                    SetSlowMotion(slow);

                    float dt = Time.deltaTime;
                    actTime += dt;
                    speed = Mathf.Max(0f, speed - decel * dt);
                    slid += speed * dt;
                    slideProgress = Mathf.Clamp01(slid / SlideLength);
                    float s = slideProgress;
                    // The rear steps out to his right and the bike swings nose-left, past broadside a touch (the nose a
                    // little back toward the camera), while he keeps sliding straight on, away from it.
                    float swing = -100f * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(s / 0.2f)) - 7f * Mathf.Sin(Mathf.Clamp01((s - 0.12f) / 0.4f) * Mathf.PI);
                    transform.SetPositionAndRotation(Ground(start + d * slid), Quaternion.LookRotation(d) * Quaternion.Euler(0f, swing, 0f));
                    Animate(dt);

                    // The three trails: both tyres and the boot. Sparks from the boot and the bike's low front.
                    float fade = speed / SlideSpeed;
                    Vector3 rearContact = Ground(bike.TransformPoint(new Vector3(0f, 0f, RearAxle.z)));
                    Vector3 frontContact = Ground(bike.TransformPoint(new Vector3(0f, 0f, FrontAxle.z)));
                    Vector3 boot = footL.position;
                    Vector3 bootContact = new Vector3(boot.x, Ground(boot).y + 0.04f, boot.z);
                    Vector3 peg = bike.TransformPoint(new Vector3(-PegAt.x - 0.1f, PegAt.y, PegAt.z));
                    if (Random.value < 0.55f * fade + 0.1f)
                    {
                        ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, bootContact, Vector3.up * 0.9f - d * 0.25f - side * 0.5f, Mathf.CeilToInt(3f * fade), 0.04f, 6f, 30f, new Color(1f, 0.85f, 0.45f));
                        ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, peg, Vector3.up * 0.8f + side * 0.6f, Mathf.CeilToInt(2f * fade), 0.035f, 5f, 30f, new Color(1f, 0.7f, 0.3f));
                    }
                    if (actTime >= nextPuff)
                    {
                        nextPuff = actTime + 0.04f;
                        foreach (Vector3 contact in new[] { rearContact, frontContact, bootContact })
                            ParticleBurst.Spawn(ParticleBurst.Kind.Smoke, contact + Vector3.up * 0.1f, Vector3.up, 1, 0.35f + 0.45f * fade, 0.5f, 40f,
                                new Color(0.82f, 0.8f, 0.78f, 0.28f * fade + 0.05f));
                        // Mud ploughed up ahead of the tyres (it's sliding sideways into it).
                        ParticleBurst.Spawn(ParticleBurst.Kind.Sand, rearContact, d + Vector3.up * 0.8f, 3, 0.45f, 5f * fade + 1f, 30f, new Color(0.3f, 0.22f, 0.15f, 0.9f));
                        ParticleBurst.Spawn(ParticleBurst.Kind.Sand, frontContact, d + Vector3.up * 0.7f, 2, 0.35f, 4f * fade + 1f, 30f, new Color(0.3f, 0.22f, 0.15f, 0.9f));
                    }
                    if (actTime >= nextMark && fade > 0.03f)
                    {
                        nextMark = actTime + 0.03f;
                        SkidMark(rearContact, d, 0.3f, false);
                        SkidMark(frontContact, d, 0.16f, false);
                        SkidMark(bootContact, d, 0.08f, true);
                    }
                    if (Mathf.Repeat(t, 0.2f) < dt) CameraShake.Add(0.05f * fade);

                    // The camera creeps after him (staying low), a slow push on a long lens.
                    if (cam != null)
                    {
                        Vector3 eye = camStart + d * (slid * 0.42f);
                        cam.transform.position = eye;
                        Vector3 aimAt = transform.position + Vector3.up * 0.7f + d * 0.4f;
                        cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, Quaternion.LookRotation(aimAt - eye), 1f - Mathf.Exp(-5f * Time.unscaledDeltaTime));
                        cam.fieldOfView = Mathf.Lerp(31f, 25f, s);
                    }
                    if (speed <= 0.01f && slow >= 0.99f) break;
                    yield return null;
                }
                SetSlowMotion(1f);
                tailTrail.emitting = false;
                Dust(transform.position + d * 0.6f, 45, 1.3f);
                CameraShake.Add(0.3f);
            }
            SetSlowMotion(1f);

            // ---- 5. A beat, then he kicks the bike up off his boot.
            if (!cine.Skipped)
            {
                for (float t = 0f; t < 0.55f && !cine.Tick(); t += Time.deltaTime)
                {
                    Animate(Time.deltaTime);
                    yield return null;
                }
                act = Act.Parked;
                actTime = 0f;
                Sfx.Play("rev", 0.5f, 0.7f);
                Vector3 eye = cam.transform.position;
                StartCoroutineSafe(cine.Move(eye + d * 2.5f + Vector3.up * 0.35f, transform.position + Vector3.up * 1.2f, 1.1f));
                for (float t = 0f; t < 1.1f && !cine.Tick(); t += Time.deltaTime)
                {
                    actTime = t;
                    Animate(Time.deltaTime);
                    yield return null;
                }
            }

            // ---- 6. Up close: the aviators come down. "You're in my lane, pal."
            if (!cine.Skipped)
            {
                act = Act.Glasses;
                actTime = 0f;
                Vector3 f = transform.forward;
                if (cam != null) cam.fieldOfView = 36f;
                cine.Cut(transform.position + f * 1.7f - d * 1.9f + Vector3.up * 2.25f, head.position + Vector3.up * 0.05f);
                cine.LookAt(head, Vector3.up * 0.1f);
                bool spoke = false;
                for (float t = 0f; t < 2.9f && !cine.Tick(); t += Time.deltaTime)
                {
                    actTime = t;
                    if (!spoke && t > 0.75f)
                    {
                        spoke = true;
                        DialogueUi.Instance.Subtitle("Clutch McCheddar", "You're in my lane, pal.", 3f);
                        Say("You're in my lane, pal.", Line, 2.2f, 3.5f, 3.6f);
                    }
                    Animate(Time.deltaTime);
                    yield return null;
                }
            }

            // ---- 7. A fist in the air, a roar and a sheet of flame; the neon flares; the gang rides out. His name.
            if (!cine.Skipped)
            {
                act = Act.Fist;
                actTime = 0f;
                Vector3 f = transform.forward;
                if (cam != null) cam.fieldOfView = 44f;
                Vector3 sign = neon != null ? neon.position : home + Vector3.forward * 20f;
                Vector3 toRoadhouse = (sign - home).Flatten().normalized;
                Vector3 low = transform.position - toRoadhouse * 2.6f + Vector3.Cross(Vector3.up, toRoadhouse) * 0.8f + Vector3.up * 0.9f;
                cine.Cut(low, transform.position + toRoadhouse * 6f + Vector3.up * 2.2f);
                Sfx.Play("rev", 1f, 1.15f);
                Say("YEEEHAW!", Line, 1.4f, 3f, 3.1f);
                ExhaustFlames(true, false);
                engineBlast = 1f;
                CameraShake.Add(0.4f);
                CallGang(3, true);
                ganged = true;
                ShowTitle();
                Vector3 wide = home - toRoadhouse * 11f + Vector3.Cross(Vector3.up, toRoadhouse) * 3f + Vector3.up * 6.5f;
                StartCoroutineSafe(cine.Move(wide, home + toRoadhouse * 7f + Vector3.up * 1f, 2.6f));
                for (float t = 0f; t < 2.9f && !cine.Tick(); t += Time.deltaTime)
                {
                    actTime = t;
                    for (int i = 0; i < signs.Length; i++)
                        signs[i].intensity = signLevels[i] * (t < 1.2f ? 1.6f + Mathf.PerlinNoise(t * 9f, i) * 1.4f : Mathf.Lerp(2.2f, 1f, (t - 1.2f) / 1.7f));
                    Animate(Time.deltaTime);
                    yield return null;
                }
            }

            // ---- Wrap up (also after a skip): parked in the middle of the lot, everything back to normal.
            SetSlowMotion(1f);
            for (int i = 0; i < signs.Length; i++) { signs[i].enabled = true; signs[i].intensity = signLevels[i]; }
            ExhaustFlames(false, false);
            tailTrail.emitting = false;
            if (cam != null) cam.fieldOfView = fov;
            if (cine.Skipped)
            {
                transform.SetPositionAndRotation(Ground(home), Quaternion.LookRotation(-side - d * 0.15f));
                glasses.localPosition = GlassesUp;
            }
            if (!ganged) CallGang(3, false);
            ShowTitle();
            act = Act.Fight;
            headlight.intensity = 3f;
            bikeLean = bikePitch = bikeYaw = bikeLift = steer = 0f;
            bikeOffset = Vector3.zero;
            gesture = Gesture.None;
            SnapRig();
        }

        private void ShowTitle()
        {
            if (titleShown) return;
            titleShown = true;
            MudHud.Title(Title, Subtitle, 3.6f);
        }

        // A camera glide that runs alongside the entrance's own frame loop (it stops itself on a skip).
        private void StartCoroutineSafe(IEnumerator routine)
        {
            if (transform.parent != null && transform.parent.TryGetComponent(out MudBossEncounter host) && host.isActiveAndEnabled) host.StartCoroutine(routine);
        }

        // Slow motion for the slide. Never touches a paused game (the pause menu owns time scale 0).
        private void SetSlowMotion(float scale)
        {
            if (Time.timeScale <= 0f) return;
            slowMo = scale < 0.999f;
            Time.timeScale = slowMo ? scale : 1f;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (slowMo && Time.timeScale > 0f) Time.timeScale = 1f;
            slowMo = false;
        }

        // Pops of flame from the pipes at full throttle.
        private void Backfire(float chance)
        {
            if (Random.value > chance) return;
            foreach (Vector3 end in new[] { PipeEndLow, PipeEndHigh })
            {
                Vector3 at = bike.TransformPoint(end + Vector3.back * 0.12f);
                ParticleBurst.Spawn(ParticleBurst.Kind.Fireball, at, -bike.forward, 3, 0.22f, 2.5f, 15f, new Color(1f, 0.6f, 0.25f));
            }
            Sfx.PlayAt("car_crash", transform.position, 0.15f, 2.4f, 30f);
        }

        // ---------- The entrance road ----------

        private static float PathLength(List<Vector3> path)
        {
            float total = 0f;
            for (int i = 1; i < path.Count; i++) total += Flat(path[i] - path[i - 1]);
            return total;
        }

        private static Vector3 PathPoint(List<Vector3> path, float distance)
        {
            if (distance <= 0f) return path[0] + (path[1] - path[0]).Flatten().normalized * distance;
            for (int i = 1; i < path.Count; i++)
            {
                float leg = Flat(path[i] - path[i - 1]);
                if (distance <= leg || i == path.Count - 1)
                    return path[i - 1] + (path[i] - path[i - 1]).Flatten().normalized * distance;
                distance -= leg;
            }
            return path[path.Count - 1];
        }

        private static float PathDistanceOf(List<Vector3> path, Vector3 point)
        {
            float total = 0f;
            for (int i = 1; i < path.Count; i++)
            {
                if ((path[i - 1] - point).sqrMagnitude < 0.01f) return total;
                total += Flat(path[i] - path[i - 1]);
            }
            return total;
        }

        // On the road at a distance along it, facing a little ahead (so the bends are ridden, not snapped).
        private void PlaceOnPath(List<Vector3> path, float distance)
        {
            Vector3 at = PathPoint(path, distance);
            Vector3 ahead = (PathPoint(path, distance + 4f) - PathPoint(path, distance - 2f)).Flatten();
            transform.SetPositionAndRotation(Ground(at), ahead.sqrMagnitude > 0.01f ? Quaternion.LookRotation(ahead) : transform.rotation);
        }

        // A streak on the ground: tyres leave dark rubber, the boot a pale scrape. Fades after a while.
        private static void SkidMark(Vector3 at, Vector3 along, float width, bool scrape)
        {
            Color color = scrape ? new Color(0.55f, 0.5f, 0.42f) : new Color(0.08f, 0.065f, 0.055f);
            string key = scrape ? "clutch2_scrape" : "clutch2_skid_" + Mathf.RoundToInt(width * 100f);
            Transform mark = MudArt.Part(null, "Skid Mark", at + Vector3.up * 0.03f, Quaternion.LookRotation(along), key,
                k => k.Box(Vector3.zero, new Vector3(width, 0.012f, 0.8f), color), false);
            Destroy(mark.gameObject, 30f);
        }

        // =====================================================================================================
        // Fight
        // =====================================================================================================

        private float engineBlast; // a burst of revs (sound and shake), decays
        private bool chainOut; // from phase 2 the chain hangs from his right fist

        protected override void OnBossStarted()
        {
            if (AgentReady)
            {
                Agent.updateRotation = false;
                Agent.acceleration = 40f;
            }
            savedHome = Home;
            gangZone = gameObject.AddComponent<CombatZone>();
            gangZone.Radius = ArenaRadius;
            engine = Sfx.AddLoop(gameObject, "bike_engine", 0.5f, true, 4f, 60f);
            nextAttack = Time.time + 2f;
            nextGang = Time.time + 12f;
            nextGesture = Time.time + 3f;
            Enter(State.Circle);
        }

        private void Enter(State next)
        {
            if (ring != null && next != State.Jump) { ring.Finish(); ring = null; }
            state = next;
            stateTime = 0f;
            if (next != State.Getaway && next != State.Circle) gesture = Gesture.None;
        }

        protected override void UpdatePatrol(float dt) => Circle(dt, 9f);

        protected override void TickCombat(float dt)
        {
            stateTime += dt;
            UpdateShove(dt);
            gang.RemoveAll(g => g == null);
            if (chaseRunning) Home = Target != null ? Target.position : Home; // the leash follows the chase
            bool nitro = Stage == 3 && nitroDone;
            switch (state)
            {
                case State.Circle:
                    Circle(dt, nitro ? 16f : 13f);
                    if (Stage == 3 && !nitroDone) { Enter(State.Nitro); break; }
                    Decide();
                    break;
                case State.Line: UpdateLine(dt, nitro); break;
                case State.Ram: UpdateRam(dt, nitro); break;
                case State.Donuts: UpdateDonuts(dt, nitro); break;
                case State.Rooster: UpdateRooster(dt); break;
                case State.Gang:
                    Halt();
                    if (stateTime > 1.3f) Enter(State.Circle);
                    break;
                case State.ToRamp: UpdateToRamp(dt); break;
                case State.Jump: UpdateJump(); break;
                case State.Whip: UpdateWhip(dt); break;
                case State.WipeOut: UpdateWipeOut(); break;
                case State.Rally:
                    Halt();
                    if (stateTime - dt < 0.15f && stateTime >= 0.15f) { Sfx.PlayAt("rev", transform.position, 1f, 1.1f, 80f); ExhaustFlames(true, false); engineBlast = 1f; CameraShake.Add(0.25f); }
                    if (stateTime > 1.1f) { ExhaustFlames(false, false); Enter(State.Circle); nextAttack = Time.time + 1f; }
                    break;
                case State.Nitro: UpdateNitro(); break;
                case State.Getaway: UpdateGetaway(dt); break;
                case State.Escaped:
                    if (stateTime > 3f) ReturnToLot();
                    break;
                case State.Returning:
                    Chase(savedHome, 15f);
                    FaceVelocity(dt);
                    if (Flat(savedHome - transform.position) < 6f || stateTime > 6f) { Enter(State.Circle); nextAttack = Time.time + 1.5f; }
                    break;
            }
            if (state == State.Circle) MaybeGesture();
            bool flaming = state == State.Rally || state == State.Nitro && stateTime > 0.6f;
            if (!flaming) ExhaustFlames(nitro && (state == State.Ram || state == State.Circle || state == State.Getaway), true);
            if (engine != null && engine.Source != null)
            {
                float revving = state == State.Line || state == State.Rooster || state == State.Rally ? 0.5f : 0f;
                engine.Source.pitch = 0.9f + Mathf.Clamp01(speedNow / 20f) * 0.8f + revving + engineBlast * 0.6f;
            }
        }

        private void Circle(float dt, float speed)
        {
            orbit += dt * speed / 18f;
            Vector3 centre = Target != null && Engaged ? Vector3.Lerp(Home, PlayerPosition, 0.6f) : Home;
            Vector3 goal = centre + new Vector3(Mathf.Cos(orbit), 0f, Mathf.Sin(orbit)) * 16f;
            Chase(goal, speed);
            FaceVelocity(dt);
        }

        private void FaceVelocity(float dt)
        {
            if (!AgentReady) return;
            Vector3 v = Agent.velocity.Flatten();
            if (v.sqrMagnitude > 1f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(v), 300f * dt);
        }

        // Showing off while he circles: a point, a "come on" wave of the fingers, a fist pump.
        private void MaybeGesture()
        {
            if (gesture != Gesture.None || Time.time < nextGesture || !Engaged) return;
            nextGesture = Time.time + Random.Range(4f, 7f);
            float roll = Random.value;
            Gesture pick = roll < 0.4f ? Gesture.Point : roll < 0.75f ? Gesture.Beckon : Gesture.FistPump;
            StartGesture(pick, 1.3f);
            if (pick == Gesture.Beckon && Random.value < 0.5f) Say("C'mon!", Line, 1f, 4f);
        }

        private void StartGesture(Gesture g, float seconds)
        {
            gesture = g;
            gestureStart = Time.time;
            gestureUntil = Time.time + seconds;
        }

        private void Decide()
        {
            if (Time.time < nextAttack || !TargetAlive) return;
            float distance = PlayerDistance;
            int stage = Stage;
            if (Time.time >= nextGang && gang.Count < 3)
            {
                Enter(State.Gang);
                StartGesture(Gesture.Whistle, 1.2f);
                Say("BOYS!", Line, 1.2f, 5f);
                Sfx.PlayAt("turtle_whistle", transform.position, 1f, 0.8f, 60f);
                CallGang(stage == 1 ? 4 : 6, false);
                nextGang = Time.time + (stage == 1 ? 22f : 18f);
                return;
            }
            float roll = Random.value;
            if (stage >= 2 && distance < 5.5f && roll < 0.5f) { Enter(State.Whip); Say("!", new Color(1f, 0.6f, 0.3f), 0.6f, 5f); return; }
            if (stage >= 2 && ramp != null && roll < 0.25f) { Enter(State.ToRamp); return; }
            if (roll < 0.45f) { ramsLeft = stage == 3 ? 2 : 1; StartLine(); return; }
            if (roll < 0.72f) { StartDonuts(); return; }
            Enter(State.Rooster);
        }

        // ---------- Ride-by ram ----------

        private void StartLine()
        {
            Enter(State.Line);
            Sfx.PlayAt("rev", transform.position, 1f, 1f, 60f);
        }

        private void UpdateLine(float dt, bool nitro)
        {
            Halt();
            float windup = Stage == 1 ? 1f : 0.8f;
            if (nitro) windup = ramsLeft == 1 && ramDirection != Vector3.zero ? 0.5f : 0.7f;
            if (stateTime < windup * 0.8f) TurnToward(PlayerPosition, 360f * dt);
            ramDirection = transform.forward;
            // A burnout on the spot: smoke off the back tyre.
            if (Random.value < 0.6f) ParticleBurst.Spawn(ParticleBurst.Kind.Smoke, transform.position - transform.forward * 1.1f + Vector3.up * 0.2f, Vector3.up - transform.forward, 1, 0.7f, 1.2f, 30f, new Color(0.85f, 0.85f, 0.85f, 0.5f));
            if (stateTime < windup) return;
            Enter(State.Ram);
            ramHit = false;
            Sfx.PlayAt("car_skid", transform.position, 0.6f, 1.3f, 40f);
        }

        private void UpdateRam(float dt, bool nitro)
        {
            float speed = nitro ? 26f : Stage == 2 ? 24f : 22f;
            if (AgentReady) Agent.Move(ramDirection * speed * dt);
            transform.rotation = Quaternion.LookRotation(ramDirection);
            if (Random.value < 0.5f) ParticleBurst.Spawn(ParticleBurst.Kind.Sand, transform.position - transform.forward * 1.2f, -ramDirection + Vector3.up, 4, 0.5f, 4f, 30f, new Color(0.35f, 0.27f, 0.2f, 0.8f));
            if (!ramHit && PlayerDistance < 2f)
            {
                ramHit = true;
                if (HurtPlayer(25f)) ShovePlayer(ramDirection + Vector3.Cross(Vector3.up, ramDirection) * 0.5f, 6f);
            }
            foreach (Transform prop in wipeOutProps)
                if (prop != null && Flat(prop.position - transform.position) < 2.4f) { WipeOut("CRASH!"); return; }
            if (ramp != null && Flat(ramp.position - transform.position) < 2.5f && Vector3.Angle(ramp.forward, ramDirection) > 60f) { WipeOut("OOF!"); return; }
            if (stateTime < 1.5f) return;
            ramsLeft--;
            if (ramsLeft > 0) StartLine();
            else { Enter(State.Circle); nextAttack = Time.time + Random.Range(1.5f, 2.5f); }
        }

        // ---------- Donuts ----------

        private float spinAngle, spinRadius;

        // The donuts are ridden round a point between him and the player, starting from where he already is: the
        // circle begins on his wheels, so nothing jumps.
        private void StartDonuts()
        {
            Enter(State.Donuts);
            Say("WATCH THIS!", Line, 1f, 4.5f);
            Vector3 toPlayer = (PlayerPosition - transform.position).Flatten();
            float reach = Mathf.Clamp(toPlayer.magnitude * 0.6f, 2.5f, 6f);
            spinCentre = transform.position + (toPlayer.sqrMagnitude > 0.01f ? toPlayer.normalized : transform.forward) * reach;
            Vector3 from = transform.position - spinCentre;
            spinAngle = Mathf.Atan2(from.z, from.x);
            spinRadius = from.Flatten().magnitude;
        }

        private Quaternion SpinFacing => Quaternion.LookRotation(new Vector3(-Mathf.Sin(spinAngle), 0f, Mathf.Cos(spinAngle))) * Quaternion.Euler(0f, -50f, 0f);

        private void UpdateDonuts(float dt, bool nitro)
        {
            Halt();
            if (stateTime < 0.6f)
            {
                // The rear wheel smokes: the warning. He swings his back end round to start.
                ParticleBurst.Spawn(ParticleBurst.Kind.Smoke, rearSpin.position, Vector3.up, 2, 0.6f, 1f, 30f, new Color(0.8f, 0.8f, 0.8f, 0.6f));
                transform.rotation = Quaternion.RotateTowards(transform.rotation, SpinFacing, 360f * dt);
                return;
            }
            float t = stateTime - 0.6f;
            // Round and round, tightening onto the 2.5 m ring in the first second.
            spinAngle += dt * Mathf.Lerp(3.5f, 6.5f, Mathf.Clamp01(t / 0.6f));
            spinRadius = Mathf.MoveTowards(spinRadius, 2.5f, dt * 4f);
            Vector3 at = spinCentre + new Vector3(Mathf.Cos(spinAngle), 0f, Mathf.Sin(spinAngle)) * spinRadius;
            Vector3 step = (at - transform.position).Flatten();
            if (AgentReady) Agent.Move(Vector3.ClampMagnitude(step, 22f * dt));
            transform.rotation = Quaternion.RotateTowards(transform.rotation, SpinFacing, 720f * dt);
            if (Random.value < 0.6f) ParticleBurst.Spawn(ParticleBurst.Kind.Sand, rearSpin.position, transform.right + Vector3.up * 0.5f, 6, 0.6f, 7f, 25f, new Color(0.3f, 0.22f, 0.15f, 0.9f));
            if (Random.value < 0.4f) ParticleBurst.Spawn(ParticleBurst.Kind.Smoke, rearSpin.position, Vector3.up, 1, 0.8f, 1f, 30f, new Color(0.8f, 0.8f, 0.8f, 0.5f));
            if (Time.time >= nextTick)
            {
                nextTick = Time.time + 0.5f;
                Sfx.PlayAt("chain_swish", transform.position, 0.7f, 0.8f, 30f);
                HurtInCircle(spinCentre, 4.5f, 8f, 3f);
                Vector3 fling = spinCentre + Random.insideUnitSphere.Flatten().normalized * Random.Range(5f, 8f);
                MudZone.Spawn(Ground(fling), 1.8f, 6f);
            }
            if (t < 3f) return;
            if (nitro)
                for (int i = 0; i < 6; i++)
                {
                    float a = i / 6f * Mathf.PI * 2f;
                    FireZone.Spawn(new FireStyle { damage = 6f, duration = 4f, tint = Nitro }, Ground(spinCentre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 4.5f), 1.3f, gameObject, transform);
                }
            Enter(State.Circle);
            nextAttack = Time.time + 1.5f;
        }

        // ---------- Mud rooster ----------

        private void UpdateRooster(float dt)
        {
            Halt();
            // Turns his back on you, waves bye-bye over his shoulder, revs, and the rear tyre throws a fan of mud.
            Vector3 away = (transform.position - PlayerPosition).Flatten();
            if (stateTime < 0.6f && away.sqrMagnitude > 0.01f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(away), 360f * dt);
            if (stateTime - dt < 0.3f && stateTime >= 0.3f) { StartGesture(Gesture.Wave, 0.85f); Say("Bye-bye!", Line, 0.9f, 4f); }
            if (stateTime < 1.2f)
            {
                if (Random.value < 0.5f) ParticleBurst.Spawn(ParticleBurst.Kind.Sand, rearSpin.position, -transform.forward + Vector3.up * 0.3f, 3, 0.4f, 3f, 20f, new Color(0.3f, 0.22f, 0.15f, 0.9f));
                if (Mathf.Repeat(stateTime, 0.3f) < dt) Sfx.PlayAt("rev", transform.position, 0.6f, 1.4f, 40f);
                return;
            }
            if (stateTime - dt < 1.2f)
            {
                Vector3 back = -transform.forward;
                engineBlast = 1f;
                ParticleBurst.Spawn(ParticleBurst.Kind.Sand, rearSpin.position, back + Vector3.up * 0.25f, 120, 0.8f, 14f, 30f, new Color(0.28f, 0.2f, 0.13f, 0.95f));
                Sfx.PlayAt("mud_splat", transform.position, 1f, 0.6f, 50f);
                Vector3 to = (PlayerPosition - transform.position).Flatten();
                if (TargetAlive && to.magnitude < 9f && Vector3.Angle(back, to) < 50f && HurtPlayer(10f))
                {
                    MiniBossHud.SandBlind(2f);
                    MudHud.Toast("Mud in your eyes!", new Color(0.8f, 0.6f, 0.4f));
                    if (Target != null) MudlandsPlayerSlow.Ensure(Target.gameObject).SlowFor(0.5f, 2f);
                }
                for (int i = 0; i < 3; i++) MudZone.Spawn(Ground(transform.position + back * (3f + i * 2.5f)), 1.8f, 6f);
            }
            if (stateTime > 1.8f) { Enter(State.Circle); nextAttack = Time.time + 1.5f; }
        }

        // ---------- Ramp jump ----------

        private void UpdateToRamp(float dt)
        {
            Vector3 runUp = ramp.position - ramp.forward * 14f;
            if (stateTime < 0.05f) Say("HOLD MY CHEESE!", Line, 1.4f, 4f);
            if (Flat(runUp - transform.position) > 2f && stateTime < 5f)
            {
                Chase(runUp, 16f);
                FaceVelocity(dt);
                return;
            }
            // Full throttle up the ramp, then airborne toward the player's marked spot.
            if (AgentReady)
            {
                Agent.Move((ramp.position - transform.position).Flatten().normalized * 22f * dt);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(ramp.forward), 360f * dt);
            }
            if (Flat(ramp.position - transform.position) > 1.5f && stateTime < 7f) return;
            jumpFrom = transform.position;
            jumpTo = Ground(PlayerPosition);
            ring = GroundRing.Spawn(jumpTo, 4f, new Color(1f, 0.35f, 0.2f, 0.55f));
            if (Agent != null) Agent.enabled = false;
            Sfx.PlayAt("rev", transform.position, 1f, 1.3f, 60f);
            engineBlast = 1f;
            Enter(State.Jump);
        }

        private void UpdateJump()
        {
            float k = Mathf.Clamp01(stateTime / 1.2f);
            transform.position = Vector3.Lerp(jumpFrom, jumpTo, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 9f;
            Vector3 flat = (jumpTo - jumpFrom).Flatten();
            if (flat.sqrMagnitude > 0.01f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(flat), 360f * Time.deltaTime);
            if (ring != null) ring.Progress = k;
            if (stateTime > 0.45f && stateTime - Time.deltaTime <= 0.45f) Say("WOOO!", Line, 0.8f, 4.5f);
            if (k < 1f) return;
            if (ring != null) { ring.Finish(); ring = null; }
            AgentUtil.Reenable(Agent, 6f);
            CameraShake.Add(0.6f);
            Dust(transform.position, 50, 1.2f);
            Sfx.PlayAt("laser_slam", transform.position, 1f, 0.4f, 60f);
            HurtInCircle(transform.position, 4f, 30f, 6f);
            landedAt = Time.time;
            Enter(State.Circle);
            nextAttack = Time.time + 1.2f;
        }

        private float landedAt = -10f;

        // ---------- Chain whip ----------

        private void UpdateWhip(float dt)
        {
            Chase(PlayerPosition, 3f);
            FaceVelocity(dt);
            if (Mathf.Repeat(stateTime, 0.25f) < dt && stateTime < 0.8f) Sfx.PlayAt("chain_swish", transform.position, 0.5f, 1.3f, 30f);
            if (stateTime < 0.8f) return;
            if (stateTime - dt < 0.8f)
            {
                Sfx.PlayAt("chain_swish", transform.position, 1f, 0.6f, 40f);
                ExpandingRing.Spawn(EffectMaterials.Additive(EffectMaterials.Look.Glow), transform.position + Vector3.up * 1.2f, 5f, 0.3f, 0.3f);
                HurtInCircle(transform.position, 5f, 16f, 4f);
            }
            if (stateTime > 1.3f) { Enter(State.Circle); nextAttack = Time.time + 1.5f; }
        }

        // ---------- Gang ----------

        private void CallGang(int count, bool wheelies)
        {
            if (gangPrefab == null) return;
            for (int i = 0; i < count; i++)
            {
                Transform door = gangDoors.Length > 0 ? gangDoors[i % gangDoors.Length] : transform;
                Vector3 at = door.position + Random.insideUnitSphere.Flatten() * 1.5f;
                if (NavMesh.SamplePosition(at, out NavMeshHit hit, 4f, NavMesh.AllAreas)) at = hit.position;
                GameObject mouse = Instantiate(gangPrefab, at, door.rotation);
                foreach (ITerritorial member in mouse.GetComponentsInChildren<ITerritorial>()) member.Territory.Zone = gangZone != null ? gangZone : null;
                gang.Add(mouse);
                if (wheelies) Emote.Show(mouse.transform, "WOO!", new Color(1f, 0.8f, 0.4f), 1.6f, 1.5f, 3f);
            }
        }

        // ---------- Flames ----------

        // Flames from both fishtails: orange for a show-off rev, purple for nitro.
        private void ExhaustFlames(bool on, bool nitro)
        {
            if (on && flameL != null && flamesNitro != nitro) ExhaustFlames(false, nitro);
            if (on && flameL == null)
            {
                flameL = FireEffect.SpawnJet(bike, PipeEndLow + Vector3.back * 0.14f, Quaternion.Euler(-90f, 0f, 0f), 0.11f, nitro ? 0.9f : 0.7f);
                flameR = FireEffect.SpawnJet(bike, PipeEndHigh + Vector3.back * 0.14f, Quaternion.Euler(-90f, 0f, 0f), 0.11f, nitro ? 0.9f : 0.7f);
                if (nitro) { flameL.SetTint(Nitro); flameR.SetTint(Nitro); }
                flamesNitro = nitro;
            }
            else if (!on && flameL != null)
            {
                Destroy(flameL.gameObject);
                Destroy(flameR.gameObject);
                flameL = flameR = null;
            }
        }

        // ---------- Wipe-outs ----------

        private Vector3 wipeBikeSlide, wipeThrow;

        private void WipeOut(string line)
        {
            Enter(State.WipeOut);
            DamageTakenMultiplier = 1.5f;
            Say(line, new Color(1f, 0.7f, 0.4f), 1.4f, 5f);
            Face?.Show(Mood.Shock, 0.8f);
            CameraShake.Add(0.4f);
            Sfx.PlayAt("car_crash", transform.position, 1f, 0.8f, 60f);
            Dust(transform.position, 40, 1f);
            ExhaustFlames(false, false);
            // The bike skids on a little and goes down on its left side; he's thrown off to the right.
            wipeBikeSlide = new Vector3(-0.3f, 0f, 1.3f);
            wipeThrow = new Vector3(1.35f, 0f, 0.6f);
        }

        private void UpdateWipeOut()
        {
            Halt();
            if (stateTime > 0.6f && stateTime - Time.deltaTime <= 0.6f)
            {
                Emote.Show(transform, "@ @", new Color(1f, 0.9f, 0.5f), 2.6f, 1.2f, 4f);
                Face?.Show(Mood.Hurt, 1.2f);
            }
            if (stateTime > 1.85f && stateTime - Time.deltaTime <= 1.85f) Face?.Show(Mood.Angry, 1.5f);
            if (stateTime < WipeOutTime) return;
            // Back on: the root moves to where the bike lies so the fight carries on from there.
            Remount();
            DamageTakenMultiplier = 1f;
            Enter(State.Circle);
            nextAttack = Time.time + 1f;
        }

        // Moves the boss to the bike (which slid off during the wipe-out) and zeroes the bike's offset, keeping the
        // rider's pose where it is in the world.
        private void Remount()
        {
            Vector3 world = Visual.TransformPoint(bikeOffset);
            Quaternion turn = Quaternion.Euler(0f, bikeYaw, 0f);
            Matrix4x4 before = Visual.localToWorldMatrix;
            transform.SetPositionAndRotation(Ground(world), transform.rotation * turn);
            if (AgentReady) Agent.Warp(transform.position);
            Matrix4x4 change = Visual.worldToLocalMatrix * before;
            Quaternion spin = Quaternion.Inverse(turn);
            pose.Hip = change.MultiplyPoint3x4(pose.Hip);
            pose.HandL = change.MultiplyPoint3x4(pose.HandL);
            pose.HandR = change.MultiplyPoint3x4(pose.HandR);
            pose.FootL = change.MultiplyPoint3x4(pose.FootL);
            pose.FootR = change.MultiplyPoint3x4(pose.FootR);
            pose.HipRot = spin * pose.HipRot;
            pose.HandLRot = spin * pose.HandLRot;
            pose.HandRRot = spin * pose.HandRRot;
            pose.FootLRot = spin * pose.FootLRot;
            pose.FootRRot = spin * pose.FootRRot;
            bikeOffset = Vector3.zero;
            bikeYaw = 0f;
            lastPosition = transform.position;
            lastYaw = transform.eulerAngles.y;
        }

        // ---------- Phase changes ----------

        protected override void OnPhaseTwo()
        {
            Say("Alright, no more Mr. Nice Mouse.", Line, 2f, 3.5f);
            Sfx.PlayAt("rev", transform.position, 1f, 0.9f, 80f);
            gesture = Gesture.None;
        }

        protected override void UpdatePhaseShift(float dt)
        {
            Halt();
            float t = PhaseShiftElapsed;
            if (t > 0.55f && !chainOut)
            {
                chainOut = true;
                Sfx.PlayAt("chain_swish", transform.position, 1f, 0.9f, 40f);
            }
        }

        protected override void OnPhaseShiftEnded()
        {
            chainOut = true;
            Enter(State.Rally);
        }

        // Phase 3: he flips the cap off the red button on his tank and slaps it.
        private void UpdateNitro()
        {
            Halt();
            if (stateTime > 0.25f && stateTime - Time.deltaTime <= 0.25f) Sfx.PlayAt("ui_click", transform.position, 1f, 0.6f, 30f);
            if (stateTime > 0.6f && stateTime - Time.deltaTime <= 0.6f)
            {
                nitroDone = true;
                Say("NITRO!", Nitro, 1.4f, 6f);
                ExhaustFlames(true, true);
                engineBlast = 1f;
                Sfx.PlayAt("rev", transform.position, 1f, 1.5f, 90f);
                Sfx.PlayAt("magic_charge", transform.position, 0.8f, 1.4f, 40f);
                CameraShake.Add(0.45f);
                ExpandingRing.Spawn(EffectMaterials.Additive(EffectMaterials.Look.Glow), transform.position + Vector3.up * 0.3f, 6f, 0.5f, 0.4f);
            }
            if (stateTime > 1.5f) { Enter(State.Circle); nextAttack = Time.time + 0.8f; }
        }

        // ---------- The getaway ----------

        private void StartGetaway()
        {
            Health.Invulnerable = true;
            Enter(State.Getaway);
            getawayStop = 0;
            escapeTimer = 0f;
            taunted = false;
            chaseRunning = true;
            foreach (GameObject member in gang) if (member != null) Emote.Show(member.transform, "GO BOSS GO!", new Color(1f, 0.8f, 0.4f), 1.6f, 2f, 2.5f);
            const string line = "Catch me if you can, cheese-for-brains!";
            DialogueUi.Instance.Subtitle("Clutch McCheddar", line, 3.5f);
            Say(line, Line, 2.5f, 3f);
            MudHud.Shout("HE'S GETTING AWAY!", new Color(1f, 0.45f, 0.3f), 2f);
            Sfx.PlayAt("rev", transform.position, 1f, 1.3f, 80f);
            engineBlast = 1f;
            var driving = DrunkDriving.Instance;
            if (driving != null) driving.FreeRides = true;
            if (Agent != null) Agent.enabled = false;
            BuildEscapeRamp();
        }

        // ---------- The escape ramp: his way out, at the end of the causeway ----------

        private Transform escapeRamp;
        private bool launching;
        private float launchTime;
        private Vector3 launchFrom, launchTo;

        private Vector3 RampLip => escapeRamp.TransformPoint(new Vector3(0f, 2.6f, 4f));

        // A plank jump ramp on the road just short of the County Line (built once, on his first getaway).
        private void BuildEscapeRamp()
        {
            if (escapeRamp != null || escapeRoute.Length < 2) return;
            Vector3 end = escapeRoute[escapeRoute.Length - 1].position, before = escapeRoute[escapeRoute.Length - 2].position;
            Vector3 dir = (end - before).Flatten().normalized;
            var root = new GameObject("Clutch's Escape Ramp").transform;
            root.SetPositionAndRotation(Ground(end - dir * 6f), Quaternion.LookRotation(dir));
            MudArt.Part(root, "Model", Vector3.zero, Quaternion.identity, "clutch2_escape_ramp", k => MudModels.Build(k, MudModels.Kind.Ramp, 0, 1f));
            var surface = new GameObject("Ramp Surface");
            surface.transform.SetParent(root, false);
            surface.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            surface.transform.localRotation = Quaternion.Euler(-18f, 0f, 0f);
            surface.AddComponent<BoxCollider>().size = new Vector3(4f, 0.2f, 8.4f);
            escapeRamp = root;
        }

        // Off the lip: a huge jump over the County Line and away (unless you rammed him first).
        private void Launch()
        {
            launching = true;
            launchTime = 0f;
            launchFrom = transform.position;
            Vector3 dir = escapeRamp.forward.Flatten().normalized;
            launchTo = Ground(launchFrom + dir * 40f);
            transform.rotation = Quaternion.LookRotation(dir);
            Say("SO LONG, SUCKER!", Line, 1.6f, 5f);
            Sfx.PlayAt("rev", transform.position, 1f, 1.4f, 120f);
            engineBlast = 1f;
            ExhaustFlames(true, Stage == 3 && nitroDone);
            MudHud.Shout("HE JUMPED IT!", new Color(1f, 0.5f, 0.3f), 2f);
        }

        private void UpdateLaunch(float dt)
        {
            launchTime += dt;
            float k = Mathf.Clamp01(launchTime / 1.6f);
            transform.position = Vector3.Lerp(launchFrom, launchTo, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 10f;
            if (k < 1f) return;
            launching = false;
            ExhaustFlames(false, false);
            Dust(transform.position, 40, 1.2f);
            Escape();
        }

        private void UpdateGetaway(float dt)
        {
            if (escapeRoute.Length == 0) { Escape(); return; }
            escapeTimer += dt;
            // A wheelie off the line, then flat out along the causeway, weaving, dropping barrels.
            Vector3 target = escapeRoute[Mathf.Min(getawayStop, escapeRoute.Length - 1)].position;
            Vector3 to = (target - transform.position).Flatten();
            if (escapeRamp != null && getawayStop >= escapeRoute.Length - 1 && Flat(RampLip - transform.position) < 2f) { Launch(); return; }
            if (to.magnitude < 3f)
            {
                getawayStop++;
                if (getawayStop >= escapeRoute.Length) { Escape(); return; }
            }
            // Off the line in a wheelie; then he dawdles (taunting) until you are in a car, and from there he
            // rubber-bands: flat out when you are close, easing off when you fall far behind, so the chase stays
            // winnable but you have to drive well (and dodge his barrels) to get alongside.
            DrunkCar chaser = DrunkDriving.Instance != null ? DrunkDriving.Instance.Car : null;
            float speed;
            if (launching) { UpdateLaunch(dt); return; }
            if (escapeTimer < 1.2f) speed = 8f;
            else if (chaser == null)
            {
                speed = 6f; // slow enough that the chug and the car's drop don't lose you the race
                if (!taunted && escapeTimer > 2.5f)
                {
                    taunted = true;
                    Say("C'mon, slowpoke!", Line, 1.4f, 2.5f);
                    StartGesture(Gesture.Wave, 1.2f);
                }
            }
            // The chase car tops out at 26 m/s (less in mud). Close up he does 13, far behind 7, and if you've fallen
            // way back he all but waits for you: always catchable, it just takes a clean run (11-09: at 11-18 he
            // always got away).
            else
            {
                float behind = Flat(chaser.transform.position - transform.position);
                speed = behind > 60f ? 4f : Mathf.Lerp(13f, 7f, Mathf.InverseLerp(8f, 45f, behind));
            }
            Vector3 weave = Vector3.Cross(Vector3.up, to.normalized) * Mathf.Sin(escapeTimer * 1.6f) * 0.35f;
            Vector3 heading = (to.normalized + weave).normalized;
            transform.position = Ground(transform.position + heading * speed * dt);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(heading), dt * 5f);
            if (escapeTimer > 2f && Time.time >= nextDrop)
            {
                nextDrop = Time.time + 3.5f;
                StartGesture(Gesture.Toss, 0.6f);
                DropBarrel(transform.position - transform.forward * 2.5f);
            }
            float left = RouteLeft();
            MudHud.Chase(DrunkDriving.Instance != null && DrunkDriving.Instance.InCar ? "RAM HIM before the ramp!" : "HE'S GETTING AWAY!  Hold F and RAM HIM!", left);
        }

        private float RouteLeft()
        {
            if (escapeRoute.Length == 0) return 0f;
            float total = 0f, remaining = Flat(escapeRoute[Mathf.Min(getawayStop, escapeRoute.Length - 1)].position - transform.position);
            for (int i = 1; i < escapeRoute.Length; i++) total += Flat(escapeRoute[i].position - escapeRoute[i - 1].position);
            for (int i = getawayStop + 1; i < escapeRoute.Length; i++) remaining += Flat(escapeRoute[i].position - escapeRoute[i - 1].position);
            return total > 0f ? Mathf.Clamp01(remaining / total) : 0f;
        }

        private static void DropBarrel(Vector3 at)
        {
            var go = new GameObject("Oil Barrel");
            go.transform.position = Ground(at);
            MudArt.Part(go.transform, "Model", Vector3.up * 0.45f, Quaternion.identity, "kong_barrel_red", RollingBarrel.Mesh(true));
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.45f, 0f);
            box.size = new Vector3(0.8f, 0.9f, 0.8f);
            go.AddComponent<Destructible>().Configure(Destructible.Substance.Metal, 15f, true, 3.5f, 20f);
            Destroy(go, 30f);
        }

        private void Escape()
        {
            chaseRunning = false;
            Home = savedHome;
            Enter(State.Escaped);
            escaped = true;
            launching = false;
            transform.position = Ground(savedHome + (savedHome - (escapeRoute.Length > 0 ? escapeRoute[0].position : savedHome)).Flatten().normalized * 70f);
            // He got away: phase 3's health comes back.
            Health.Invulnerable = false;
            float target = Health.Max * PhaseThree;
            if (Health.Current < target) Health.Heal(target - Health.Current);
            MudHud.Toast("He got away... and healed up. Here he comes again!", new Color(1f, 0.6f, 0.4f), 3.5f);
            var driving = DrunkDriving.Instance;
            if (driving != null) driving.FreeRides = false;
        }

        private void ReturnToLot()
        {
            AgentUtil.Reenable(Agent, 30f);
            Sfx.PlayAt("rev", transform.position, 1f, 1f, 100f);
            Say("ROUND TWO!", Line, 1.6f, 5f);
            Enter(State.Returning);
        }

        private void Caught()
        {
            if (state == State.Caught || IsDead) return;
            chaseRunning = false;
            Home = savedHome;
            Enter(State.Caught);
            MudHud.Shout("GOTCHA!", new Color(1f, 0.85f, 0.35f), 2f);
            Face?.Show(Mood.Shock, 3f);
            var driving = DrunkDriving.Instance;
            if (driving != null) driving.FreeRides = false;
            StartCoroutine(OverTheHandlebars());
        }

        // Rammed: the chopper stands on its nose and he sails over the handlebars, arms and legs everywhere; the bike
        // cartwheels off on its own; he lands face down in the mud and slides to a stop. That's that.
        private IEnumerator OverTheHandlebars()
        {
            ExhaustFlames(false, false);
            Vector3 heading = transform.forward;
            // The stoppie.
            for (float t = 0f; t < 0.25f; t += Time.deltaTime)
            {
                transform.position = Ground(transform.position + heading * Mathf.Lerp(10f, 2f, t / 0.25f) * Time.deltaTime);
                yield return null;
            }
            // The bike goes its own way.
            DetachBike();
            Transform wreck = bike;
            Vector3 bikeFrom = wreck.position;
            Quaternion bikeTurn = wreck.rotation;
            Vector3 bikeTo = Ground(bikeFrom + heading * 4.5f + Vector3.Cross(Vector3.up, heading) * 1.2f);
            flightFrom = transform.position;
            flightTo = Ground(flightFrom + heading * 9f);
            flightTime = 0f;
            Sfx.PlayAt("car_crash", transform.position, 1f, 0.7f, 70f);
            for (float t = 0f; t < 1.5f; t += Time.deltaTime)
            {
                flightTime = t;
                float k = Mathf.Clamp01(t / 1.2f);
                if (t < 1.2f) transform.position = Vector3.Lerp(flightFrom, flightTo, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 5f;
                else transform.position = Ground(flightTo + heading * (t - 1.2f) * 4f); // face down, sliding through the mud
                if (t >= 1.2f && t - Time.deltaTime < 1.2f)
                {
                    Dust(flightTo, 50, 1.2f);
                    Sfx.PlayAt("mud_splat", flightTo, 1f, 0.5f, 60f);
                    CameraShake.Add(0.5f);
                    MudZone.Spawn(flightTo, 2f, 8f);
                }
                if (wreck != null)
                {
                    float b = Mathf.Clamp01(t / 1.1f);
                    wreck.position = Vector3.Lerp(bikeFrom, bikeTo, b) + Vector3.up * (Mathf.Sin(b * Mathf.PI) * 1.4f + 0.42f * b);
                    wreck.rotation = Quaternion.AngleAxis(b * 360f, Vector3.Cross(Vector3.up, heading)) * Quaternion.AngleAxis(Mathf.SmoothStep(0f, 80f, b), heading) * bikeTurn;
                    if (b >= 1f && Random.value < 0.3f) ParticleBurst.Spawn(ParticleBurst.Kind.Smoke, wreck.position + Vector3.up * 0.4f, Vector3.up, 1, 0.8f, 0.8f, 30f, new Color(0.4f, 0.4f, 0.4f, 0.5f));
                }
                yield return null;
            }
            flightTime = 1.5f;
            Health.Invulnerable = false;
            Health.TakeDamage(Health.Current + 1f, Target != null ? Target.gameObject : null);
        }

        // The chopper leaves him (over the handlebars): it becomes a wreck in the world, the landing gear still on it.
        private void DetachBike()
        {
            if (bikeDetached) return;
            bikeDetached = true;
            bike.SetParent(null, true);
            if (headlight != null) headlight.intensity = 1f;
            tailTrail.emitting = false;
            Destroy(bike.gameObject, 120f);
        }

        // ---------- Damage and the car ----------

        protected override void OnBossHit(float amount, GameObject source, Vector3 away)
        {
            // At 15% he makes a run for it (again, if he got away last time and came back).
            if (state != State.Getaway && state != State.Caught && Health.Normalized <= EscapeAt) { StartGetaway(); return; }
            if (state == State.Ram && IsFullCharge(amount, source)) WipeOut("WHOA!");
        }

        public override void OnCarApproaching(DrunkCar car, float secondsAway) { }

        public override VehicleResponse OnVehicleHit(VehicleHit hit)
        {
            if (state == State.Getaway) Caught();
            return VehicleResponse.Explode;
        }

        public override float OnCarBlast(CarBlastInfo blast)
        {
            if (state == State.Getaway) { Caught(); return 0f; }
            if (state != State.Caught && state != State.Jump && state != State.WipeOut) WipeOut("MY PAINT JOB!");
            return blast.Damage;
        }

        protected override void OnReset()
        {
            foreach (GameObject member in gang) if (member != null) Destroy(member);
            gang.Clear();
            chaseRunning = false;
            escaped = false;
            launching = false;
            Home = savedHome;
            Health.Invulnerable = false;
            DamageTakenMultiplier = 1f;
            ExhaustFlames(false, false);
            if (bikeOffset != Vector3.zero || bikeYaw != 0f) Remount();
            AgentUtil.Reenable(Agent, 12f);
            var driving = DrunkDriving.Instance;
            if (driving != null) driving.FreeRides = false;
            chainOut = false;
            nitroDone = false;
            Enter(State.Circle);
        }

        protected override void OnMudBossDeath()
        {
            if (engine != null) engine.Level = 0f;
            ExhaustFlames(false, false);
            SetSlowMotion(1f);
            // His aviators fly off (they're the trophy).
            TrophyPickup.Create(TrophyId, DropSpot(transform.position + transform.right * 2.5f));
            ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, glasses.position, Vector3.up, 6, 0.06f, 3f, 60f, Gold);
            glasses.gameObject.SetActive(false);
            toothpick.gameObject.SetActive(false);
            foreach (GameObject member in gang)
                if (member != null) Emote.Show(member.transform, "SCATTER!", new Color(1f, 0.6f, 0.4f), 1.6f, 2f, 3f);
            foreach (GameObject member in gang)
                if (member != null) Destroy(member, 2.5f);
            var driving = DrunkDriving.Instance;
            if (driving != null) driving.FreeRides = false;
            deathFromBike = !bikeDetached;
        }

        private bool deathFromBike;

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (slowMo && Time.timeScale > 0f) Time.timeScale = 1f;
        }

        // =====================================================================================================
        // Animation
        // =====================================================================================================

        protected override void AnimateBoss(float dt) => Animate(dt);

        // Puts the rig straight into its pose (after a jump in position: the entrance's cuts, a skip).
        private void SnapRig()
        {
            posed = false;
            lastPosition = transform.position;
            lastYaw = transform.eulerAngles.y;
            speedNow = yawRate = 0f;
            Animate(0f);
        }

        private void Animate(float dt)
        {
            lastDt = dt;
            // How he's moving: measured from the root, so it works for the agent, the entrance and the getaway alike.
            Vector3 moved = (transform.position - lastPosition).Flatten();
            float yaw = transform.eulerAngles.y;
            if (dt > 1e-5f)
            {
                bool jumped = moved.magnitude > 6f;
                float k = 1f - Mathf.Exp(-10f * dt);
                speedNow = Mathf.Lerp(speedNow, jumped ? speedNow : moved.magnitude / dt, k);
                yawRate = Mathf.Lerp(yawRate, jumped ? 0f : Mathf.Clamp(Mathf.DeltaAngle(lastYaw, yaw) / dt, -400f, 400f), k);
            }
            lastPosition = transform.position;
            lastYaw = yaw;
            engineBlast = Mathf.MoveTowards(engineBlast, 0f, dt * 1.2f);

            if (!bikeDetached)
            {
                PoseBike(dt);
                ApplyBike();
            }
            lookYawLimit = state == State.Rooster || state == State.Getaway || act == Act.Slide ? 85f : 60f;
            PoseNow(RiderPose(), RiderRate(), dt, !posed);
            AnimateChain();
            foreach (Jiggle jiggle in jiggles) jiggle.Step(dt);
            Lights();
        }

        // ---------- The bike ----------

        private void PoseBike(float dt)
        {
            // Riding: it leans into turns as a bike must (lean = atan(v * turn rate / g)), steers a touch, rolls.
            float lean = Mathf.Clamp(Mathf.Atan2(speedNow * yawRate * Mathf.Deg2Rad, 9.81f) * Mathf.Rad2Deg, -40f, 40f);
            float pitch = 0f, yawOff = 0f, lift = 0f, steerTo = Mathf.Clamp(yawRate * 0.05f, -18f, 18f), rate = 8f;
            float frontSpeed = speedNow, rearSpeed = speedNow;
            Vector3 offset = Vector3.zero;
            bool direct = false; // follow the targets exactly (scripted curves)
            float t = stateTime;
            if (act != Act.Fight)
            {
                switch (act)
                {
                    case Act.Ride:
                        lean += -16f * slideProgress; // the flick into the slide
                        break;
                    case Act.Slide:
                    {
                        // Laid over toward the camera (his left), most of the way through, coming up as he stops;
                        // opposite lock on the bars; the back wheel locked, the front still turning.
                        float s = slideProgress;
                        lean = s < 0.7f ? -(18f + 37f * Mathf.SmoothStep(0f, 1f, s / 0.25f)) : Mathf.Lerp(-55f, -38f, (s - 0.7f) / 0.3f);
                        steerTo = 28f * Mathf.SmoothStep(0f, 1f, s / 0.2f);
                        rearSpeed = 0f;
                        frontSpeed *= 0.4f;
                        rate = 14f;
                        break;
                    }
                    case Act.Parked:
                        lean = Mathf.Lerp(-38f, -5f, Mathf.SmoothStep(0f, 1f, (actTime - 0.15f) / 0.6f));
                        steerTo = Mathf.Lerp(28f, 6f, actTime / 1.1f);
                        rate = 12f;
                        break;
                    case Act.Glasses:
                        lean = -5f;
                        steerTo = 6f;
                        break;
                    case Act.Fist:
                        lean = -4f;
                        steerTo = 6f;
                        rearSpeed = actTime < 1.4f ? 28f : 0f; // the big rev spins the back wheel
                        break;
                }
            }
            else if (InPhaseShift) { lean = -3f; rearSpeed = 6f; }
            else switch (state)
            {
                case State.Line:
                    lean = 0f;
                    pitch = Mathf.Sin(t * 22f) * 0.6f;
                    rearSpeed = 25f; // a burnout on the spot
                    break;
                case State.Ram:
                    pitch = t < 0.4f ? 14f * Mathf.Sin(t / 0.4f * Mathf.PI) : 0f; // launches with the front up
                    steerTo = 0f;
                    break;
                case State.Donuts:
                    lean = -26f;
                    steerTo = 24f; // opposite lock
                    rearSpeed = 18f;
                    break;
                case State.Rooster:
                    lean = 0f;
                    pitch = -2f;
                    rearSpeed = t < 1.3f ? 30f : 0f;
                    break;
                case State.Gang:
                case State.Rally:
                    lean = -3f;
                    rearSpeed = state == State.Rally ? 20f : 0f;
                    break;
                case State.Jump:
                {
                    float k = Mathf.Clamp01(t / 1.2f);
                    pitch = Mathf.Lerp(18f, -12f, k);
                    lean = Mathf.Sin(k * Mathf.PI * 2f) * 10f;
                    rearSpeed = 20f;
                    break;
                }
                case State.WipeOut:
                {
                    // Down on its left side and skidding on (on its bars and bags), then heaved back up.
                    float fall = Mathf.SmoothStep(0f, 1f, t / 0.45f), rise = Mathf.SmoothStep(0f, 1f, (t - 2.3f) / 0.45f);
                    float slideOut = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 0.6f), 2f);
                    lean = -85f * fall * (1f - rise);
                    lift = 0.25f * fall * (1f - rise);
                    offset = wipeBikeSlide * slideOut;
                    yawOff = 50f * slideOut;
                    frontSpeed = 0f;
                    rearSpeed = Mathf.Max(0f, 14f - t * 8f);
                    steerTo = -30f * fall * (1f - rise);
                    direct = true;
                    break;
                }
                case State.Nitro:
                    lean = -2f;
                    pitch = t > 0.6f ? 22f * Mathf.Sin(Mathf.Clamp01((t - 0.6f) / 0.9f) * Mathf.PI) : 0f;
                    rearSpeed = t > 0.6f ? 30f : 0f;
                    break;
                case State.Getaway when launching:
                    pitch = Mathf.Lerp(24f, -10f, Mathf.Clamp01(launchTime / 1.6f));
                    rearSpeed = 25f;
                    break;
                case State.Getaway:
                    pitch = escapeTimer < 1f ? 30f * Mathf.SmoothStep(0f, 1f, escapeTimer / 0.25f) : Mathf.Lerp(30f, 0f, (escapeTimer - 1f) / 0.4f);
                    break;
                case State.Caught:
                    pitch = -30f * Mathf.SmoothStep(0f, 1f, t / 0.22f); // a stoppie
                    rate = 20f;
                    break;
            }
            if (landedAt > 0f && Time.time - landedAt < 0.4f) lift -= 0.12f * Mathf.Sin((Time.time - landedAt) / 0.4f * Mathf.PI); // the landing squash
            float blend = direct ? 1f : 1f - Mathf.Exp(-rate * dt);
            if (!posed) blend = 1f;
            bikeLean = Mathf.Lerp(bikeLean, lean, blend);
            bikePitch = Mathf.Lerp(bikePitch, pitch, blend);
            bikeYaw = Mathf.Lerp(bikeYaw, yawOff, blend);
            bikeLift = Mathf.Lerp(bikeLift, lift, blend);
            bikeOffset = Vector3.Lerp(bikeOffset, offset, blend);
            steer = Mathf.Lerp(steer, steerTo, 1f - Mathf.Exp(-10f * dt));
            frontRoll = Mathf.Repeat(frontRoll + frontSpeed / FrontRadius * Mathf.Rad2Deg * dt, 360f);
            rearRoll = Mathf.Repeat(rearRoll + rearSpeed / RearRadius * Mathf.Rad2Deg * dt, 360f);
            // The engine shakes the whole bike when it's standing still (and hard when he guns it).
            bool idling = speedNow < 2f && state != State.WipeOut;
            bikeShake = Mathf.MoveTowards(bikeShake, (idling ? 0.6f : 0.15f) + engineBlast * 2.2f, dt * 6f);
        }

        private void ApplyBike()
        {
            Quaternion turn = Quaternion.Euler(0f, bikeYaw, 0f), lean = Quaternion.Euler(0f, 0f, -bikeLean), pitch = Quaternion.Euler(-bikePitch, 0f, 0f);
            // Wheelies pivot on the back tyre, stoppies on the front.
            Vector3 pivot = new Vector3(0f, 0f, bikePitch >= 0f ? RearAxle.z : FrontAxle.z);
            Vector3 pitched = pivot - pitch * pivot;
            // Laid over, it rides on the edge of its fat back tyre (and on its side, on the bars and bags).
            float tyre = RearHalfWidth * Mathf.Sin(Mathf.Abs(bikeLean) * Mathf.Deg2Rad);
            float n = Time.time * 23f;
            Quaternion shake = Quaternion.Euler((Mathf.PerlinNoise(n, 0.3f) - 0.5f) * bikeShake, 0f, (Mathf.PerlinNoise(0.7f, n) - 0.5f) * bikeShake);
            bike.localRotation = turn * lean * pitch * shake;
            bike.localPosition = bikeOffset + turn * (lean * pitched + Vector3.up * (tyre + bikeLift));
            frontEnd.localRotation = Quaternion.AngleAxis(steer, ForkAxis);
            frontSpin.localRotation = Quaternion.Euler(frontRoll, 0f, 0f);
            rearSpin.localRotation = Quaternion.Euler(rearRoll, 0f, 0f);
        }

        // A point on the bike, in his Visual's space (wherever the bike is).
        private Vector3 B(Vector3 local) => Visual.InverseTransformPoint(bike.TransformPoint(local));
        private Quaternion BikeTurn => Quaternion.Inverse(Visual.rotation) * bike.rotation;
        private Vector3 InVisualPoint(Transform t) => Visual.InverseTransformPoint(t.position);

        private void Lights()
        {
            if (headlight != null && !bikeDetached)
            {
                float beam = 3f;
                if (act == Act.Ride || act == Act.Slide) beam = 6f;
                else if (state == State.Line && act == Act.Fight) beam = Mathf.Repeat(stateTime, 0.2f) < 0.1f ? 8f : 2f; // flashing his high beam
                else if (state == State.Ram || state == State.Getaway) beam = 6f;
                headlight.intensity = Mathf.Lerp(headlight.intensity, beam, 0.5f);
            }
            bool streaking = !bikeDetached && (act == Act.Slide || speedNow > 15f && (act == Act.Ride || act == Act.Fight));
            if (tailTrail != null && tailTrail.emitting != streaking) tailTrail.emitting = streaking;
            bool nitro = nitroDone && Stage == 3 && act == Act.Fight;
            if (underglow != null) underglow.intensity = nitro ? 2.4f + Mathf.Sin(Time.time * 13f) * 0.6f : 0f;
            if (nitroCap != null)
            {
                bool open = nitroDone || state == State.Nitro && stateTime > 0.25f;
                nitroCap.localRotation = Quaternion.Slerp(nitroCap.localRotation, Quaternion.Euler(open ? -115f : 0f, 0f, 0f), 0.35f);
            }
        }

        // ---------- The chain ----------

        private void AnimateChain()
        {
            bool shift = InPhaseShift && PhaseShiftElapsed > 0.55f;
            bool twirl = act == Act.Fight && (state == State.Whip && stateTime < 0.8f || state == State.Donuts && stateTime > 0.6f || shift && PhaseShiftElapsed < 1.1f);
            bool crack = act == Act.Fight && state == State.Whip && stateTime >= 0.8f && stateTime < 1.15f;
            bool show = act == Act.Fight && (chainOut || twirl || crack || state == State.Whip || state == State.Donuts) && state != State.Caught;
            if (chainHeld.gameObject.activeSelf != show) chainHeld.gameObject.SetActive(show);
            if (!show) return;
            Vector3 dir;
            if (twirl)
            {
                float a = Time.time * 14f;
                dir = Visual.rotation * new Vector3(Mathf.Cos(a), -0.2f, Mathf.Sin(a));
            }
            else if (crack)
            {
                Vector3 to = (PlayerPosition - transform.position).Flatten();
                dir = (to.sqrMagnitude > 0.01f ? to.normalized : transform.forward) + Vector3.down * 0.15f;
            }
            else dir = Vector3.down - transform.forward * Mathf.Clamp(speedNow * 0.04f, 0f, 1.2f) + transform.right * Mathf.Sin(Time.time * 3f) * 0.1f;
            chainHeld.rotation = Quaternion.FromToRotation(Vector3.down, dir.normalized);
            // Swinging, it's at full length; just hanging from his fist it's mostly wrapped round it.
            chainHeld.localScale = Vector3.Lerp(chainHeld.localScale, twirl || crack ? Vector3.one : new Vector3(1f, 0.4f, 1f), 0.3f);
        }

        // ---------- The rider ----------

        private float RiderRate()
        {
            if (act == Act.Slide) return 14f;
            if (state == State.WipeOut || state == State.Caught || state == State.Jump) return 20f;
            return 10f;
        }

        private Pose RiderPose()
        {
            switch (act)
            {
                case Act.Ride: return Ride(0.45f + 0.4f * slideProgress);
                case Act.Slide: return SlidePose();
                case Act.Parked: return ParkedPose(actTime);
                case Act.Glasses: return GlassesPose(actTime);
                case Act.Fist: return FistPose(actTime);
            }
            if (state == State.Caught) return flightTime >= 0f ? Flight(flightTime) : Lunge(stateTime);
            if (state == State.WipeOut) return WipeOutPose(stateTime);
            if (InPhaseShift) return ChainDraw(PhaseShiftElapsed);
            float t = stateTime;
            Pose p;
            switch (state)
            {
                case State.Line:
                    p = FeetDown(Ride(0.85f), 1f, 1f);
                    p.Hip += Vector3.up * Mathf.Sin(t * 30f) * 0.01f;
                    return p;
                case State.Ram: return Ride(1f);
                case State.Donuts:
                {
                    p = Ride(0.5f);
                    p.FootL = new Vector3(-0.78f, Ankle, 0.5f); // the inside boot planted, pivoting
                    p.FootLRot = Quaternion.LookRotation(new Vector3(-0.4f, 0f, 1f));
                    if (t > 0.6f) p = Overhead(p, 1f);
                    return p;
                }
                case State.Rooster:
                    p = Ride(0.2f);
                    p.Spine = Quaternion.Euler(0f, 35f, 0f) * p.Spine; // twisting round to look back
                    return WithGesture(p);
                case State.Gang:
                    p = WithGesture(FeetDown(Ride(0.2f), 1f, 1f));
                    p = PumpRight(p, Mathf.Abs(Mathf.Sin(t * 9f)));
                    return p;
                case State.Rally:
                    p = FeetDown(Ride(0.25f), 1f, 1f);
                    return PumpRight(p, Mathf.Abs(Mathf.Sin(t * 8f)));
                case State.Nitro: return NitroPose(t);
                case State.Jump: return JumpPose(t);
                case State.Whip:
                    p = Ride(0.3f, 0.5f);
                    if (t < 0.8f) return Overhead(p, Mathf.Clamp01(t / 0.2f));
                    if (t < 1.15f) return Crack(p, (t - 0.8f) / 0.35f);
                    return p;
                case State.Getaway:
                    if (launching) return JumpPose(launchTime * 0.75f); // the no-footer, over the County Line
                    return WithGesture(Ride(escapeTimer < 1f ? 0.1f : 0.7f));
                default:
                {
                    float land = landedAt > 0f ? Mathf.Clamp01(1f - (Time.time - landedAt) / 0.35f) : 0f;
                    p = WithGesture(Ride(0.3f + land * 0.6f));
                    p.Hip -= Vector3.up * land * 0.1f;
                    return p;
                }
            }
        }

        // In the saddle: hips on the seat, boots on the pegs, fists round the grips. crouch 0 = sitting back
        // chopper-style, 1 = tucked over the tank; standUp lifts him onto the pegs.
        private Pose Ride(float crouch, float standUp = 0f)
        {
            Quaternion br = BikeTurn;
            var p = new Pose
            {
                Hip = B(HipOnSeat + new Vector3(0f, standUp * 0.3f, crouch * 0.06f + standUp * 0.12f)),
                HipRot = br * Quaternion.Euler(-6f + crouch * 22f + standUp * 14f, 0f, 0f),
                Spine = Quaternion.Euler(-4f + crouch * 20f, 0f, 0f),
                Head = Quaternion.Euler(-crouch * 32f - standUp * 10f, 0f, 0f),
            };
            for (int i = 0; i < 2; i++)
            {
                float s = i == 0 ? -1f : 1f;
                Vector3 grip = InVisualPoint(i == 0 ? gripL : gripR);
                // Fingers wrap forward and down round the grip; the wrist sits just behind and above it.
                Vector3 fingers = br * new Vector3(0.1f * s, -0.6f, 0.75f);
                Vector3 wrist = grip - fingers.normalized * 0.085f + br * Vector3.up * 0.01f;
                Quaternion hand = Bone(fingers, br * Vector3.down);
                Vector3 ankle = B(new Vector3(PegAt.x * s, PegAt.y + Ankle, PegAt.z - 0.05f));
                Quaternion foot = br * Quaternion.Euler(8f, -4f * s, 0f);
                if (i == 0) { p.HandL = wrist; p.HandLRot = hand; p.FootL = ankle; p.FootLRot = foot; }
                else { p.HandR = wrist; p.HandRRot = hand; p.FootR = ankle; p.FootRRot = foot; }
            }
            return p;
        }

        // Both (or one) boots down on the ground beside the bike.
        private Pose FeetDown(Pose p, float left, float right)
        {
            Quaternion flat = Quaternion.Euler(0f, BikeTurn.eulerAngles.y, 0f);
            if (left > 0f)
            {
                p.FootL = Vector3.Lerp(p.FootL, flat * new Vector3(-0.52f, Ankle, 0.22f) + new Vector3(bikeOffset.x, 0f, bikeOffset.z), left);
                p.FootLRot = Quaternion.Slerp(p.FootLRot, flat * Quaternion.Euler(0f, -12f, 0f), left);
            }
            if (right > 0f)
            {
                p.FootR = Vector3.Lerp(p.FootR, flat * new Vector3(0.52f, Ankle, 0.22f) + new Vector3(bikeOffset.x, 0f, bikeOffset.z), right);
                p.FootRRot = Quaternion.Slerp(p.FootRRot, flat * Quaternion.Euler(0f, 12f, 0f), right);
            }
            return p;
        }

        private static Vector3 Chest(in Pose p) => p.Hip + p.HipRot * (SpineAt + p.Spine * new Vector3(0f, 0.42f, 0.04f));
        private static Vector3 Shoulder(in Pose p, float side) => p.Hip + p.HipRot * (SpineAt + p.Spine * new Vector3(ShoulderAt.x * side, ShoulderAt.y, ShoulderAt.z));
        private static Vector3 HeadPoint(in Pose p) => p.Hip + p.HipRot * (SpineAt + p.Spine * (NeckAt + p.Head * new Vector3(0f, 0.2f, 0.05f)));

        // The right fist up over his head, the chain whirling round (amount 0..1 from the grip).
        private Pose Overhead(Pose p, float amount)
        {
            Quaternion chest = p.HipRot * p.Spine;
            float a = Time.time * 14f;
            Vector3 up = Shoulder(p, 1f) + chest * new Vector3(-0.12f, 0.72f, 0.05f) + Visual.InverseTransformDirection(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a))) * 0.08f;
            p.HandR = Vector3.Lerp(p.HandR, up, amount);
            p.HandRRot = Quaternion.Slerp(p.HandRRot, Bone(Vector3.up, chest * Vector3.forward), amount);
            return p;
        }

        // The chain cracks out: the arm swings down and across toward the player.
        private Pose Crack(Pose p, float k)
        {
            Vector3 to = Visual.InverseTransformDirection((PlayerPosition - transform.position).Flatten());
            if (to.sqrMagnitude < 0.01f) to = Vector3.forward;
            to.Normalize();
            Vector3 shoulder = Shoulder(p, 1f);
            Vector3 from = shoulder + Vector3.up * 0.7f;
            Vector3 strike = shoulder + to * 0.78f + Vector3.down * 0.1f;
            float swing = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k * 2.5f));
            p.HandR = Vector3.Lerp(from, strike, swing);
            p.HandRRot = Bone(to + Vector3.down * 0.2f, Vector3.up);
            p.Spine = Quaternion.Euler(10f * swing, -20f * swing, 0f) * p.Spine;
            return p;
        }

        // A fist pumped at the sky (0..1 how high this beat).
        private Pose PumpRight(Pose p, float beat)
        {
            Quaternion chest = p.HipRot * p.Spine;
            p.HandR = Shoulder(p, 1f) + chest * new Vector3(0.02f, 0.55f + beat * 0.18f, 0.08f);
            p.HandRRot = Bone(chest * Vector3.up, chest * Vector3.back);
            return p;
        }

        private Pose WithGesture(Pose p)
        {
            if (gesture == Gesture.None) return p;
            if (Time.time > gestureUntil) { gesture = Gesture.None; return p; }
            float age = Time.time - gestureStart;
            float w = Mathf.SmoothStep(0f, 1f, age / 0.2f) * Mathf.SmoothStep(0f, 1f, (gestureUntil - Time.time) / 0.25f);
            Quaternion chest = p.HipRot * p.Spine;
            switch (gesture)
            {
                case Gesture.Point:
                {
                    Vector3 shoulder = Shoulder(p, 1f);
                    Vector3 to = Visual.InverseTransformPoint(PlayerPosition + Vector3.up * 1.2f) - shoulder;
                    if (to.sqrMagnitude < 0.01f) to = Vector3.forward;
                    p.HandR = Vector3.Lerp(p.HandR, shoulder + to.normalized * 0.8f, w);
                    p.HandRRot = Quaternion.Slerp(p.HandRRot, Bone(to, Vector3.up), w);
                    break;
                }
                case Gesture.Beckon:
                {
                    // Palm up, fingers curling him toward you: "come on".
                    float curl = Mathf.Sin(age * 14f) * 0.5f + 0.5f;
                    Vector3 at = Shoulder(p, -1f) + chest * new Vector3(0.05f, -0.12f, 0.55f);
                    p.HandL = Vector3.Lerp(p.HandL, at, w);
                    p.HandLRot = Quaternion.Slerp(p.HandLRot, Bone(chest * new Vector3(0f, 0.3f + curl * 0.9f, 1f), chest * Vector3.up), w);
                    break;
                }
                case Gesture.Wave:
                {
                    Vector3 at = Shoulder(p, -1f) + chest * new Vector3(-0.15f + Mathf.Sin(age * 15f) * 0.1f, 0.55f, 0.05f);
                    p.HandL = Vector3.Lerp(p.HandL, at, w);
                    p.HandLRot = Quaternion.Slerp(p.HandLRot, Bone(chest * new Vector3(Mathf.Sin(age * 15f) * 0.4f, 1f, 0f), chest * Vector3.forward), w);
                    break;
                }
                case Gesture.FistPump:
                {
                    Pose pumped = PumpRight(p, Mathf.Abs(Mathf.Sin(age * 9f)));
                    p.HandR = Vector3.Lerp(p.HandR, pumped.HandR, w);
                    p.HandRRot = Quaternion.Slerp(p.HandRRot, pumped.HandRRot, w);
                    break;
                }
                case Gesture.Whistle:
                {
                    // Two fingers to his snout.
                    Vector3 at = HeadPoint(p) + chest * new Vector3(-0.04f, -0.12f, 0.42f);
                    p.HandL = Vector3.Lerp(p.HandL, at - chest * new Vector3(0f, 0.08f, 0.05f), w);
                    p.HandLRot = Quaternion.Slerp(p.HandLRot, Bone(chest * new Vector3(0f, 1f, 0.6f), chest * Vector3.back), w);
                    break;
                }
                case Gesture.Toss:
                {
                    // Swings a barrel off the back.
                    float k = Mathf.Clamp01(age / 0.6f);
                    Vector3 at = Shoulder(p, 1f) + chest * new Vector3(0.2f, Mathf.Lerp(-0.3f, 0.2f, k), Mathf.Lerp(0.1f, -0.65f, k));
                    p.HandR = Vector3.Lerp(p.HandR, at, w);
                    p.HandRRot = Quaternion.Slerp(p.HandRRot, Bone(chest * Vector3.back, chest * Vector3.down), w);
                    break;
                }
            }
            return p;
        }

        // ---------- The entrance's poses ----------

        // The Akira slide: down with the bike, his left boot out braking on the road, his head turning back to us.
        private Pose SlidePose()
        {
            float s = slideProgress;
            Pose p = Ride(0.55f);
            Quaternion flat = Quaternion.identity;
            Vector3 boot = flat * new Vector3(-0.9f, Ankle, 0.42f);
            float down = Mathf.SmoothStep(0f, 1f, s / 0.15f);
            p.FootL = Vector3.Lerp(p.FootL, boot, down);
            p.FootLRot = Quaternion.Slerp(p.FootLRot, Quaternion.LookRotation(new Vector3(-0.35f, 0.25f, 1f)), down);
            // His upper body stays a little more upright than the bike.
            p.Spine = Quaternion.Euler(0f, 0f, -12f * down) * p.Spine;
            lookWeight = Mathf.SmoothStep(0f, 1f, (s - 0.2f) / 0.3f);
            return p;
        }

        // Stopped: he shoves the bike up off his boot and plants the other foot.
        private Pose ParkedPose(float t)
        {
            lookWeight = 1f;
            Pose p = Ride(0.2f);
            float push = Mathf.SmoothStep(0f, 1f, (t - 0.1f) / 0.6f);
            p.FootL = Vector3.Lerp(new Vector3(-0.9f, Ankle, 0.42f), new Vector3(-0.52f, Ankle, 0.25f), push);
            p.FootLRot = Quaternion.LookRotation(new Vector3(-0.2f, 0f, 1f));
            return FeetDown(p, 0f, Mathf.SmoothStep(0f, 1f, (t - 0.6f) / 0.4f));
        }

        // Up close: a finger hooks the aviators down his snout, a look over them, the line, the toothpick flicks
        // across, and the glasses go back up.
        private Pose GlassesPose(float t)
        {
            lookWeight = 1f;
            Pose p = FeetDown(Ride(0.15f), 1f, 1f);
            float reach = Mathf.SmoothStep(0f, 1f, t / 0.3f) * (1f - Mathf.SmoothStep(0f, 1f, (t - 2.2f) / 0.35f));
            float lowered = Mathf.SmoothStep(0f, 1f, (t - 0.25f) / 0.3f) * (1f - Mathf.SmoothStep(0f, 1f, (t - 1.95f) / 0.25f));
            glasses.localPosition = Vector3.Lerp(GlassesUp, GlassesDown, lowered);
            // A finger hooked on the left temple of the frames: the hand comes up beside his head, not across his face.
            Vector3 temple = Visual.InverseTransformPoint(glasses.TransformPoint(new Vector3(-0.22f, 0.02f, -0.06f)));
            Quaternion chest = p.HipRot * p.Spine;
            p.HandL = Vector3.Lerp(p.HandL, temple + chest * new Vector3(-0.07f, -0.1f, -0.02f), reach);
            p.HandLRot = Quaternion.Slerp(p.HandLRot, Bone(chest * new Vector3(0.35f, 0.9f, 0.3f), chest * Vector3.right), reach);
            // The toothpick rolls from one corner of his mouth to the other.
            float flick = Mathf.SmoothStep(0f, 1f, (t - 1.5f) / 0.15f);
            toothpick.localPosition = Vector3.Lerp(new Vector3(0.07f, 0.02f, 0.37f), new Vector3(-0.06f, 0.02f, 0.37f), flick);
            toothpick.localRotation = Quaternion.Euler(10f, Mathf.Lerp(40f, -40f, flick), 0f);
            return p;
        }

        // The fist goes up and the engine roars.
        private Pose FistPose(float t)
        {
            lookWeight = 0.4f;
            Pose p = FeetDown(Ride(0.1f), 1f, 1f);
            float up = Mathf.SmoothStep(0f, 1f, t / 0.25f);
            Pose pumped = PumpRight(p, 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(t * 6f)));
            p.HandR = Vector3.Lerp(p.HandR, pumped.HandR, up);
            p.HandRRot = Quaternion.Slerp(p.HandRRot, pumped.HandRRot, up);
            p.Spine = Quaternion.Euler(-10f * up, 0f, 0f) * p.Spine;
            p.Head = Quaternion.Euler(-25f * up, 0f, 0f);
            return p;
        }

        // ---------- The fight's set pieces ----------

        // Phase 2: he unhooks the chain from his belt, whirls it up, and slaps it into his palm.
        private Pose ChainDraw(float t)
        {
            Pose p = FeetDown(Ride(0.2f), 1f, 1f);
            Quaternion chest = p.HipRot * p.Spine;
            if (t < 0.55f)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / 0.35f);
                p.HandR = Vector3.Lerp(p.HandR, p.Hip + p.HipRot * new Vector3(0.26f, 0.06f, 0.05f), k);
                p.HandRRot = Quaternion.Slerp(p.HandRRot, Bone(p.HipRot * Vector3.down, p.HipRot * Vector3.left), k);
            }
            else if (t < 1.1f) p = Overhead(p, Mathf.SmoothStep(0f, 1f, (t - 0.55f) / 0.2f));
            else
            {
                // Smack: the chain fist into the open left palm in front of his chest.
                float k = Mathf.SmoothStep(0f, 1f, (t - 1.1f) / 0.15f);
                Vector3 palm = Chest(p) + chest * new Vector3(0f, 0f, 0.42f);
                p.HandL = Vector3.Lerp(p.HandL, palm + chest * new Vector3(-0.06f, -0.03f, 0f), k);
                p.HandLRot = Quaternion.Slerp(p.HandLRot, Bone(chest * new Vector3(0.6f, 0.3f, 0.5f), chest * Vector3.right), k);
                p.HandR = Vector3.Lerp(Shoulder(p, 1f) + chest * new Vector3(0f, 0.3f, 0.3f), palm + chest * new Vector3(0.05f, 0.03f, 0.02f), k);
                p.HandRRot = Bone(chest * Vector3.left, chest * Vector3.up);
                p.Head = Quaternion.Euler(15f, 0f, 0f);
            }
            return p;
        }

        // Phase 3: the cap flips up, a wind-up, and a slap on the red button.
        private Pose NitroPose(float t)
        {
            Pose p = t < 0.6f ? FeetDown(Ride(0.3f), 1f, 1f) : Ride(0.75f);
            Quaternion br = BikeTurn;
            Vector3 button = B(new Vector3(0f, 1.22f, 0.17f));
            float to = Mathf.SmoothStep(0f, 1f, t / 0.25f) * (1f - Mathf.SmoothStep(0f, 1f, (t - 0.75f) / 0.2f));
            float raise = t < 0.35f ? 0f : t < 0.55f ? Mathf.SmoothStep(0f, 1f, (t - 0.35f) / 0.15f) : 1f - Mathf.SmoothStep(0f, 1f, (t - 0.55f) / 0.06f);
            p.HandL = Vector3.Lerp(p.HandL, button + br * new Vector3(0f, 0.06f + raise * 0.35f, -0.04f), to);
            p.HandLRot = Quaternion.Slerp(p.HandLRot, Bone(br * Vector3.down, br * Vector3.forward), to);
            return p;
        }

        // The ramp: up on the pegs, a no-footer in the air (boots kicked off the pegs and out), back down to land.
        private Pose JumpPose(float t)
        {
            float k = Mathf.Clamp01(t / 1.2f);
            Pose p = Ride(0.35f, Mathf.SmoothStep(0f, 1f, k / 0.2f) * (1f - Mathf.SmoothStep(0f, 1f, (k - 0.75f) / 0.25f)));
            float trick = Mathf.Sin(Mathf.Clamp01((k - 0.28f) / 0.42f) * Mathf.PI);
            if (trick > 0f)
            {
                p.FootL = Vector3.Lerp(p.FootL, B(new Vector3(-0.8f, 0.85f, -0.55f)), trick);
                p.FootR = Vector3.Lerp(p.FootR, B(new Vector3(0.8f, 0.85f, -0.55f)), trick);
                p.Hip = Vector3.Lerp(p.Hip, B(HipOnSeat + new Vector3(0f, 0.05f, 0.15f)), trick);
                p.Head = Quaternion.Euler(-20f * trick, 0f, 0f);
            }
            return p;
        }

        // The wipe-out: thrown, a tumble, sat dazed, up, dusted off, the bike heaved up, back on.
        private Pose WipeOutPose(float t)
        {
            lookWeight = t > 0.6f && t < 1.85f ? 0f : 1f;
            Vector3 sat = wipeThrow;
            Quaternion facing = Quaternion.Euler(0f, 35f, 0f);
            if (t < 0.6f)
            {
                // Thrown up and off to the right, rolling over once.
                float k = t / 0.6f;
                Pose start = Ride(0.6f);
                Vector3 hip = Vector3.Lerp(start.Hip, sat + Vector3.up * 0.24f, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.9f;
                Quaternion roll = Quaternion.Slerp(Quaternion.identity, facing, k) * Quaternion.Euler(0f, 0f, -360f * (1f - Mathf.Pow(1f - k, 2f)));
                var p = new Pose
                {
                    Hip = hip, HipRot = roll, Spine = Quaternion.Euler(20f, 0f, 0f), Head = Quaternion.Euler(-10f, 0f, 0f),
                    HandL = hip + roll * new Vector3(-0.75f, 0.6f, 0.1f), HandR = hip + roll * new Vector3(0.75f, 0.6f, 0.1f),
                    FootL = hip + roll * new Vector3(-0.35f, -0.6f, 0.4f), FootR = hip + roll * new Vector3(0.4f, -0.5f, -0.3f),
                    HandLRot = roll * Bone(Vector3.left, Vector3.forward), HandRRot = roll * Bone(Vector3.right, Vector3.forward),
                    FootLRot = roll, FootRRot = roll,
                };
                return p;
            }
            if (t < 1.85f)
            {
                // Sat in the mud, legs out, leaning back on his hands, head lolling.
                Pose p = SitPose(sat, facing);
                float bump = Mathf.Clamp01(1f - (t - 0.6f) / 0.25f);
                p.Hip += Vector3.up * Mathf.Sin(bump * Mathf.PI) * 0.06f;
                p.Head = Quaternion.Euler(Mathf.Sin(t * 6f) * 12f + 8f, Mathf.Cos(t * 4.5f) * 20f, Mathf.Sin(t * 7f) * 10f);
                return p;
            }
            Vector3 bikeSide = bikeOffset + Quaternion.Euler(0f, bikeYaw, 0f) * new Vector3(0.62f, 0f, -0.15f);
            if (t < 2.3f)
            {
                // Up onto his feet, a swipe of the dust off his jacket, two steps to the bike.
                float up = Mathf.SmoothStep(0f, 1f, (t - 1.85f) / 0.25f);
                float walk = Mathf.SmoothStep(0f, 1f, (t - 2.05f) / 0.25f);
                Vector3 at = Vector3.Lerp(sat, bikeSide, walk);
                Quaternion face = Quaternion.Slerp(facing, Quaternion.LookRotation((bikeSide - sat).Flatten().sqrMagnitude > 0.01f ? (bikeSide - sat).Flatten() : Vector3.forward), Mathf.Clamp01(walk * 2f));
                Pose stand = StandPose(at, face, walk > 0f && walk < 1f ? (t - 2.05f) * 14f : 0f);
                Pose p = Pose.Lerp(SitPose(sat, facing), stand, up);
                if (walk <= 0f && up >= 1f) // dusting off
                {
                    Quaternion chest = p.HipRot * p.Spine;
                    p.HandR = Chest(p) + chest * new Vector3(-0.12f + Mathf.Sin(t * 30f) * 0.08f, -0.05f, 0.3f);
                }
                return p;
            }
            {
                // Hands on the bars and the seat; heave; swing a leg over.
                Quaternion face = Quaternion.Euler(0f, bikeYaw - 70f, 0f);
                Pose p = StandPose(bikeSide, face, 0f);
                float heave = Mathf.SmoothStep(0f, 1f, (t - 2.3f) / 0.45f);
                p.Hip -= Vector3.up * 0.18f * (1f - heave);
                p.HandL = Vector3.Lerp(p.HandL, InVisualPoint(gripR) + Vector3.up * 0.06f, 0.9f);
                p.HandR = Vector3.Lerp(p.HandR, B(Seat + new Vector3(0.1f, 0.12f, -0.2f)), 0.9f);
                float on = Mathf.SmoothStep(0f, 1f, (t - 2.75f) / 0.25f);
                return Pose.Lerp(p, Ride(0.3f), on);
            }
        }

        // The ram knocks him forward over the bars before the bike throws him.
        private Pose Lunge(float t)
        {
            Pose p = Ride(1f);
            float k = Mathf.SmoothStep(0f, 1f, t / 0.25f);
            p.Hip += Vector3.forward * 0.3f * k + Vector3.up * 0.15f * k;
            return p;
        }

        // Over the handlebars: arms and legs everywhere, flipping, then face down sliding through the mud.
        private Pose Flight(float t)
        {
            lookWeight = 0f;
            float k = Mathf.Clamp01(t / 1.2f);
            if (k < 1f)
            {
                Quaternion spin = Quaternion.Euler(450f * k, 0f, Mathf.Sin(k * Mathf.PI) * 30f);
                Vector3 hip = new Vector3(0f, 0.9f, 0f);
                float flail = Mathf.Sin(t * 22f) * 0.15f;
                return new Pose
                {
                    Hip = hip, HipRot = spin, Spine = Quaternion.Euler(-15f, 0f, 0f), Head = Quaternion.Euler(-20f, 0f, 0f),
                    HandL = hip + spin * new Vector3(-0.8f, 0.55f + flail, 0.15f), HandR = hip + spin * new Vector3(0.8f, 0.55f - flail, 0.15f),
                    FootL = hip + spin * new Vector3(-0.45f, -0.75f, -0.1f + flail), FootR = hip + spin * new Vector3(0.45f, -0.75f, -0.1f - flail),
                    HandLRot = spin * Bone(Vector3.left, Vector3.forward), HandRRot = spin * Bone(Vector3.right, Vector3.forward),
                    FootLRot = spin, FootRRot = spin,
                };
            }
            return FaceDown(Vector3.zero, Quaternion.identity, 0f);
        }

        private Pose SitPose(Vector3 at, Quaternion facing)
        {
            Vector3 hip = at + Vector3.up * 0.22f;
            return new Pose
            {
                Hip = hip, HipRot = facing * Quaternion.Euler(-18f, 0f, 0f), Spine = Quaternion.Euler(-8f, 0f, 0f), Head = Quaternion.identity,
                FootL = at + facing * new Vector3(-0.3f, Ankle, 0.82f), FootR = at + facing * new Vector3(0.32f, Ankle, 0.7f),
                FootLRot = facing * Quaternion.Euler(-50f, -15f, 0f), FootRRot = facing * Quaternion.Euler(-50f, 15f, 0f),
                HandL = at + facing * new Vector3(-0.42f, 0.16f, -0.32f), HandR = at + facing * new Vector3(0.42f, 0.16f, -0.32f),
                HandLRot = facing * Bone(Vector3.down, Vector3.back), HandRRot = facing * Bone(Vector3.down, Vector3.back),
            };
        }

        // Standing on his own two feet; phase > 0 walks (a step cycle).
        private Pose StandPose(Vector3 at, Quaternion facing, float phase)
        {
            float sway = Mathf.Sin(Time.time * 2f) * 0.01f;
            float stepL = phase > 0f ? Mathf.Sin(phase) : 0f;
            return new Pose
            {
                Hip = at + Vector3.up * (StandHeight + sway - Mathf.Abs(stepL) * 0.03f), HipRot = facing * Quaternion.Euler(4f, 0f, 0f),
                Spine = Quaternion.Euler(2f, 0f, 0f), Head = Quaternion.identity,
                FootL = at + facing * new Vector3(-0.17f, Ankle + Mathf.Max(0f, stepL) * 0.12f, stepL * 0.22f),
                FootR = at + facing * new Vector3(0.17f, Ankle + Mathf.Max(0f, -stepL) * 0.12f, -stepL * 0.22f),
                FootLRot = facing, FootRRot = facing,
                HandL = at + facing * new Vector3(-0.38f, 0.55f, -stepL * 0.15f), HandR = at + facing * new Vector3(0.38f, 0.55f, stepL * 0.15f),
                HandLRot = facing * Bone(Vector3.down, Vector3.right), HandRRot = facing * Bone(Vector3.down, Vector3.left),
            };
        }

        // Flat on his front: body along +Z from the hips, arms out ahead, cheek on the ground; one hand can come up.
        private Pose FaceDown(Vector3 at, Quaternion facing, float handUp)
        {
            Quaternion prone = facing * Quaternion.Euler(90f, 0f, 0f);
            Vector3 hip = at + Vector3.up * 0.24f;
            float tremble = Mathf.Sin(Time.time * 26f) * 0.03f * handUp;
            return new Pose
            {
                Hip = hip, HipRot = prone, Spine = Quaternion.Euler(-4f, 0f, 0f), Head = Quaternion.Euler(-35f, 0f, 70f),
                FootL = at + facing * new Vector3(-0.22f, 0.14f, -0.85f), FootR = at + facing * new Vector3(0.24f, 0.14f, -0.82f),
                FootLRot = facing * Quaternion.Euler(-80f, 0f, 0f), FootRRot = facing * Quaternion.Euler(-80f, 0f, 0f),
                HandL = at + facing * new Vector3(-0.5f, 0.12f, 0.95f),
                HandR = at + facing * Vector3.Lerp(new Vector3(0.5f, 0.12f, 0.9f), new Vector3(0.35f + tremble, 1.05f, 0.75f), handUp),
                HandLRot = facing * Bone(Vector3.forward, Vector3.down),
                HandRRot = facing * Quaternion.Slerp(Bone(Vector3.forward, Vector3.down), Bone(Vector3.up, Vector3.forward), handUp),
            };
        }

        // ---------- Death ----------

        protected override void UpdateDeath(float t)
        {
            float dt = Time.deltaTime;
            lookWeight = 0f;
            // One hand comes up out of the mud, trembles... and flops down.
            float hand = Mathf.SmoothStep(0f, 1f, (t - 0.5f) / 0.4f) * (1f - Mathf.SmoothStep(0f, 1f, (t - 2.4f) / 0.3f));
            if (deathFromBike && !bikeDetached)
            {
                // Killed in the saddle: the chopper topples onto its left side and he spills off the other way.
                float fall = Mathf.SmoothStep(0f, 1f, t / 0.5f);
                bikeLean = -85f * fall;
                bikeLift = 0.25f * fall;
                bikePitch = bikeYaw = 0f;
                frontRoll += dt * 60f;
                ApplyBike();
                Pose down = FaceDown(new Vector3(1.1f, 0f, 0.1f), Quaternion.Euler(0f, 60f, 0f), hand);
                PoseNow(t < 0.25f ? Ride(0.6f) : down, 9f, dt);
            }
            else PoseNow(FaceDown(Vector3.zero, Quaternion.identity, hand), 12f, dt);
            if (t > 0.5f && t - dt <= 0.5f) Face?.Show(Mood.Hurt, 4f);
            foreach (Jiggle jiggle in jiggles) jiggle.Step(dt);
        }

        // =====================================================================================================
        // Pose maths (the same rig approach as Kart Kong and the witch)
        // =====================================================================================================

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
            Vector3 knees = p.HipRot * (Vector3.forward + Vector3.up * 0.25f);
            Limb(legL, kneeL, footL, Thigh, Shin, p.FootL, p.FootLRot, knees + p.HipRot * Vector3.left * 0.55f);
            Limb(legR, kneeR, footR, Thigh, Shin, p.FootR, p.FootRRot, knees + p.HipRot * Vector3.right * 0.55f);
            Quaternion chest = p.HipRot * p.Spine;
            Limb(armL, elbowL, handL, UpperArm, Forearm, p.HandL, p.HandLRot, chest * new Vector3(-0.85f, -0.45f, -0.5f));
            Limb(armR, elbowR, handR, UpperArm, Forearm, p.HandR, p.HandRRot, chest * new Vector3(0.85f, -0.45f, -0.5f));
        }

        // His head turning toward whoever he's looking at (in the spine's frame), clamped to what a neck can do.
        private Quaternion LookTurn(Quaternion fallback)
        {
            Vector3? point = FaceLookPoint;
            if (point == null || lookWeight <= 0.001f) return fallback;
            (Vector3 neck, Quaternion spineTurn) = InVisual(spine);
            neck += spineTurn * NeckAt;
            Vector3 local = Quaternion.Inverse(spineTurn) * (Visual.InverseTransformPoint(point.Value) - neck);
            float yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -lookYawLimit, lookYawLimit);
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

        /// <summary>A springy joint (ears, bandana tails, tail): it lags behind and swings back to its rest pose.</summary>
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
}
