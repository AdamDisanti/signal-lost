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
using Random = UnityEngine.Random;

namespace SignalLost.Mudlands
{
    /// <summary>
    /// The Mudlands' stage boss (Christian's design, docs/mudlands.md): Clutch McCheddar, president of the Swamp Rats
    /// MC, a mouse in a leather jacket and aviators on a long chopper whose back wheel is the ship's landing gear.
    /// <list type="bullet">
    /// <item><b>Phase 1:</b> ride-by ram (headlight flash and rev, then a charge), donuts (a spinning ring that flings
    /// mud), mud rooster (rear tyre spins, a cone of mud behind him: mud in your eyes), gang call (biker mice).</item>
    /// <item><b>Phase 2 (66%):</b> adds a ramp jump onto a marked shadow and a chain whip; bigger gangs; quicker rams.</item>
    /// <item><b>Phase 3 (33%):</b> nitro (purple exhaust, double rams), donuts leave a ring of fire.</item>
    /// <item><b>Wipe-outs:</b> a ram into the gas pumps or the ramp's side, a full-charge bolt during a ram, or the car
    /// throws him off the bike for 2 s, taking 1.5x damage.</item>
    /// <item><b>The getaway:</b> at 15% he stops taking damage and flees up the Gator Causeway. Drunk Driving is free
    /// for the chase; ram him before the County Line gate to finish him. If he gets away he heals back to 33% and
    /// roars back into the lot.</item>
    /// </list>
    /// 1100 HP. Drops the Landing Gear (the stage's main part) and Clutch's Aviators (a camp trophy).
    /// </summary>
    public sealed class ClutchMcCheddar : MudBoss
    {
        private enum State { Circle, Line, Ram, Donuts, Rooster, Gang, ToRamp, Jump, Whip, WipeOut, Getaway, Escaped, Returning, Caught }

        public const string TrophyId = "clutch_aviators";
        private const float EscapeAt = 0.15f, PhaseThree = 0.33f;

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

        private static readonly Color Fur = new Color(0.55f, 0.53f, 0.52f), Pink = new Color(0.95f, 0.62f, 0.68f),
            Leather = new Color(0.1f, 0.09f, 0.1f), Gold = new Color(0.9f, 0.72f, 0.3f), BikePaint = new Color(0.32f, 0.1f, 0.42f),
            Chrome = new Color(0.78f, 0.78f, 0.8f), Nitro = new Color(0.7f, 0.35f, 1f);

        private State state = State.Circle;
        private float stateTime, orbit, nextAttack, nextGang, escapeTimer, nextDrop, nextTick;
        private int ramsLeft, getawayStop;
        private Vector3 ramDirection, jumpFrom, jumpTo, savedHome, spinCentre;
        private bool ramHit, escaped, chaseRunning, taunted;
        private Transform bike, rider, chainArm, glasses, rearWheel, frontWheel, head, exhaust, legL, legR;
        private FireEffect nitroL, nitroR;
        private GroundRing ring;
        private readonly List<GameObject> gang = new List<GameObject>();
        private SfxLoop engine;
        private CombatZone gangZone;

        public override string Subtitle => "President, Swamp Rats MC";
        public override float PhaseThreeAt => PhaseThree;
        public override int DisplayPhase => Stage;
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

        // ---------- Model ----------

        protected override void BuildModel(Transform visual)
        {
            bike = MudArt.Pivot(visual, "Chopper", Vector3.zero);
            MudArt.Part(bike, "Frame", Vector3.zero, Quaternion.identity, "clutch_bike", k =>
            {
                k.Smooth = true;
                k.Ball(new Vector3(0f, 1.05f, 0.35f), new Vector3(0.3f, 0.24f, 0.55f), Quaternion.identity, BikePaint, 1); // teardrop tank
                k.Smooth = false;
                k.Box(new Vector3(0f, 1.12f, 0.35f), new Vector3(0.05f, 0.1f, 0.8f), Gold); // pinstripe
                k.Plank(new Vector3(0f, 0.75f, -0.6f), new Vector3(0f, 0.9f, 0.6f), 0.3f, 0.18f, MudPalette.DarkMetal);
                k.Box(new Vector3(0f, 0.95f, -0.45f), new Vector3(0.45f, 0.14f, 0.6f), Leather); // the seat
                k.Box(new Vector3(0f, 1.2f, -0.8f), new Vector3(0.4f, 0.4f, 0.08f), Quaternion.Euler(-15f, 0f, 0f), Leather); // backrest
                k.Rod(new Vector3(0f, 0.65f, -0.2f), new Vector3(0f, 0.7f, 0.3f), 0.22f, 8, MudPalette.DarkMetal); // engine
                for (int i = 0; i < 4; i++) k.Prism(new Vector3(0f, 0.68f, -0.1f + i * 0.1f), Quaternion.Euler(90f, 0f, 0f), 0.25f, 0.25f, 0.03f, 8, Chrome);
                // Long raked forks and ape-hanger bars.
                foreach (float x in new[] { -0.12f, 0.12f })
                    k.Rod(new Vector3(x, 1.2f, 0.85f), new Vector3(x, 0.45f, 2.05f), 0.04f, 6, Chrome);
                k.Rod(new Vector3(0f, 1.2f, 0.85f), new Vector3(0f, 1.25f, 0.75f), 0.05f, 6, Chrome);
                k.Rod(new Vector3(-0.45f, 1.85f, 0.65f), new Vector3(0.45f, 1.85f, 0.65f), 0.03f, 6, Chrome);
                foreach (float x in new[] { -0.45f, 0.45f }) k.Rod(new Vector3(x * 0.3f, 1.25f, 0.8f), new Vector3(x, 1.85f, 0.65f), 0.03f, 6, Chrome);
                k.Prism(new Vector3(0f, 1.2f, 1.0f), Quaternion.Euler(90f, 0f, 0f), 0.16f, 0.18f, 0.12f, 10, Chrome); // headlight housing
                k.Ball(new Vector3(0f, 1.2f, 1.13f), new Vector3(0.14f, 0.14f, 0.04f), Quaternion.identity, new Color(1f, 0.95f, 0.7f), 1, 3f);
                // Twin fishtail exhausts.
                foreach (float x in new[] { -0.32f, 0.32f })
                    k.Rod(new Vector3(x, 0.55f, 0.1f), new Vector3(x, 0.5f, -1.3f), 0.07f, 7, Chrome, 0f, 0.1f);
            });
            frontWheel = MudArt.Part(bike, "Front Wheel", new Vector3(0f, 0.42f, 2.05f), Quaternion.identity, "clutch_front_wheel", k =>
            {
                k.Prism(new Vector3(0.08f, 0f, 0f), Quaternion.Euler(0f, 0f, 90f), 0.42f, 0.42f, 0.16f, 12, MudPalette.Rubber);
                k.Prism(new Vector3(0.09f, 0f, 0f), Quaternion.Euler(0f, 0f, 90f), 0.3f, 0.3f, 0.18f, 12, Chrome);
            });
            // The ship's landing-gear wheel, chunky and glowing at the hub: the stage's main part.
            rearWheel = MudArt.Part(bike, "Landing Gear", new Vector3(0f, 0.6f, -1.05f), Quaternion.identity, "clutch_rear_wheel", k =>
            {
                k.Prism(new Vector3(0.22f, 0f, 0f), Quaternion.Euler(0f, 0f, 90f), 0.6f, 0.6f, 0.44f, 12, MudPalette.Rubber);
                k.Prism(new Vector3(0.23f, 0f, 0f), Quaternion.Euler(0f, 0f, 90f), 0.36f, 0.36f, 0.46f, 10, MudPalette.Metal);
                k.Prism(new Vector3(0.25f, 0f, 0f), Quaternion.Euler(0f, 0f, 90f), 0.12f, 0.12f, 0.5f, 6, new Color(0.3f, 0.85f, 1f), 3f);
                for (int i = 0; i < 6; i++)
                {
                    Quaternion spin = Quaternion.Euler(i * 60f, 0f, 0f);
                    k.Box(spin * new Vector3(0f, 0.45f, 0f), new Vector3(0.48f, 0.08f, 0.12f), spin, MudPalette.DarkMetal); // tread blocks
                }
            });
            ShipPart = rearWheel;
            exhaust = MudArt.Pivot(bike, "Exhaust", new Vector3(0f, 0.5f, -1.35f), Quaternion.Euler(0f, 180f, 0f));

            rider = MudArt.Pivot(bike, "Rider", new Vector3(0f, 1.0f, -0.35f));
            MudArt.Part(rider, "Body", Vector3.zero, Quaternion.identity, "clutch_body", k =>
            {
                k.Smooth = true;
                k.Ball(new Vector3(0f, 0.45f, 0f), new Vector3(0.42f, 0.5f, 0.34f), Quaternion.identity, Leather, 1);
                k.Ball(new Vector3(0f, 0.75f, 0.02f), new Vector3(0.4f, 0.22f, 0.3f), Quaternion.identity, Leather, 1); // shoulders
                k.Ball(new Vector3(0f, 0.08f, 0f), new Vector3(0.36f, 0.2f, 0.3f), Quaternion.identity, new Color(0.18f, 0.25f, 0.45f), 1); // seat of the jeans
                k.Ball(new Vector3(0f, 0.58f, 0.24f), new Vector3(0.14f, 0.3f, 0.08f), Quaternion.identity, new Color(0.85f, 0.25f, 0.2f), 1); // shirt
                k.Tube(new[] { new Vector3(0f, 0.1f, -0.3f), new Vector3(0f, 0f, -0.7f), new Vector3(0.2f, 0.2f, -1.1f), new Vector3(0.3f, 0.5f, -1.2f) }, 0.04f, 8, Pink);
                k.Smooth = false;
                foreach (float x in new[] { -0.17f, 0.17f })
                    k.Box(new Vector3(x, 0.8f, 0.25f), new Vector3(0.18f, 0.18f, 0.05f), Quaternion.Euler(0f, x > 0 ? -25f : 25f, x > 0 ? -20f : 20f), Leather); // lapels
                k.Box(new Vector3(0f, 0.55f, -0.33f), new Vector3(0.45f, 0.42f, 0.03f), Gold); // the club patch
                k.Ball(new Vector3(0f, 0.58f, -0.35f), new Vector3(0.12f, 0.12f, 0.02f), Quaternion.identity, MudPalette.Black, 0);
            });
            // Legs on their own hips, so one can swing out and drag a boot on the slide.
            legL = Leg(rider, -1f);
            legR = Leg(rider, 1f);
            head = MudArt.Pivot(rider, "Head", new Vector3(0f, 1.15f, 0.1f));
            MudArt.Part(head, "Face", Vector3.zero, Quaternion.identity, "clutch_head", k =>
            {
                k.Smooth = true;
                k.Ball(Vector3.zero, new Vector3(0.3f, 0.28f, 0.3f), Quaternion.identity, Fur, 1);
                // A long tapering snout, round at the tip.
                k.Lathe(new Vector3(0f, -0.05f, 0.12f), Quaternion.Euler(90f, 0f, 0f), new[]
                {
                    new Vector2(0.0001f, -0.02f), new Vector2(0.17f, 0.02f), new Vector2(0.16f, 0.14f), new Vector2(0.1f, 0.3f), new Vector2(0.05f, 0.37f), new Vector2(0.0001f, 0.39f)
                }, 12, Fur);
                k.Ball(new Vector3(0f, -0.04f, 0.5f), 0.06f, Pink, 1);
                k.Ball(new Vector3(0f, -0.12f, 0.33f), new Vector3(0.12f, 0.05f, 0.1f), Quaternion.identity, new Color(0.7f, 0.68f, 0.66f), 1); // chin
                foreach (float x in new[] { -0.28f, 0.28f })
                {
                    // Big round ears.
                    k.Ball(new Vector3(x, 0.26f, -0.05f), new Vector3(0.2f, 0.2f, 0.04f), Quaternion.Euler(-10f, x * 40f, 0f), Fur, 1);
                    k.Ball(new Vector3(x * 1.02f, 0.26f, -0.02f), new Vector3(0.14f, 0.14f, 0.02f), Quaternion.Euler(-10f, x * 40f, 0f), Pink, 1);
                    k.Smooth = false;
                    for (int i = -1; i <= 1; i++) k.Plank(new Vector3(x * 0.3f, -0.07f, 0.43f), new Vector3(x * 1.3f, -0.04f + i * 0.05f, 0.46f), 0.008f, 0.008f, MudPalette.White);
                    k.Smooth = true;
                }
                // A red bandana.
                k.Prism(new Vector3(0f, 0.12f, 0f), Quaternion.Euler(-8f, 0f, 0f), 0.31f, 0.3f, 0.1f, 16, new Color(0.8f, 0.15f, 0.12f));
                k.Smooth = false;
                k.Box(new Vector3(0.18f, 0.16f, -0.3f), new Vector3(0.08f, 0.16f, 0.03f), Quaternion.Euler(20f, 0f, 30f), new Color(0.8f, 0.15f, 0.12f)); // the knot's tails
            });
            glasses = MudArt.Part(head, "Aviators", new Vector3(0f, 0.05f, 0.3f), Quaternion.identity, "clutch_aviators", AviatorsMesh);
            // Eyes behind the aviators (seen when he lowers them), brows on the bandana above, and a cocky mouth.
            (Transform eyeL, Transform eyeR) = MudArt.Eyes(head, new Vector3(0f, 0.05f, 0.27f), 0.11f, 0.045f, MudPalette.White, MudPalette.Black, "clutch");
            Transform browL = BossFace.Brow(head, "clutch_brow", new Vector3(-0.11f, 0.155f, 0.31f), -1f, new Vector3(0.15f, 0.045f, 0.05f), new Color(0.14f, 0.13f, 0.13f), 6f);
            Transform browR = BossFace.Brow(head, "clutch_brow", new Vector3(0.11f, 0.155f, 0.31f), 1f, new Vector3(0.15f, 0.045f, 0.05f), new Color(0.14f, 0.13f, 0.13f), 6f);
            // Under the tip of the snout, angled to be seen from the front: a smirk with two buck teeth.
            Transform mouth = BossFace.Mouth(head, "clutch_mouth", new Vector3(0.02f, -0.125f, 0.4f), 0.13f, 0.05f, new Color(0.45f, 0.42f, 0.42f), true, Quaternion.Euler(-35f, 0f, 0f));
            FaceParts = new BossFace.Rig
            {
                BrowL = browL, BrowR = browR, EyeL = eyeL, EyeR = eyeR, Mouth = mouth, Body = rider,
                BrowTravel = 0.025f, Idle = Mood.Smug, Fight = Mood.Smug,
            };
            chainArm = MudArt.Pivot(rider, "Chain Arm", new Vector3(0.38f, 0.85f, 0.1f));
            MudArt.Part(chainArm, "Arm", Vector3.zero, Quaternion.identity, "clutch_arm", k =>
            {
                k.Smooth = true;
                k.Ball(Vector3.zero, 0.11f, Leather, 1);
                k.Rod(Vector3.zero, new Vector3(0.1f, -0.45f, 0.4f), 0.1f, 8, Leather, 0f, 0.085f);
                k.Ball(new Vector3(0.1f, -0.5f, 0.45f), 0.09f, Fur, 1);
                k.Smooth = false;
                for (int i = 0; i < 9; i++) k.Prism(new Vector3(0.1f, -0.55f - i * 0.11f, 0.45f), Quaternion.Euler(0f, 0f, i % 2 == 0 ? 0f : 90f), 0.035f, 0.035f, 0.09f, 4, Chrome);
            });
            MudArt.Part(rider, "Arm L", new Vector3(-0.38f, 0.85f, 0.1f), Quaternion.Euler(-60f, 0f, -20f), "clutch_arm_l", k =>
            {
                k.Smooth = true;
                k.Ball(Vector3.zero, 0.11f, Leather, 1);
                k.Rod(Vector3.zero, new Vector3(0f, -0.55f, 0.1f), 0.1f, 8, Leather, 0f, 0.085f);
                k.Ball(new Vector3(0f, -0.6f, 0.11f), 0.085f, Fur, 1);
            });
        }

        // A leg from the hip: jeans down to a heavy biker boot, resting forward on the pegs.
        private static Transform Leg(Transform rider, float side)
        {
            Transform hip = MudArt.Pivot(rider, side < 0f ? "Leg L" : "Leg R", new Vector3(0.2f * side, 0.05f, 0.1f));
            MudArt.Part(hip, "Leg", Vector3.zero, Quaternion.identity, "clutch_leg", k =>
            {
                k.Smooth = true;
                k.Ball(Vector3.zero, 0.12f, new Color(0.18f, 0.25f, 0.45f), 1);
                k.Rod(Vector3.zero, new Vector3(0f, -0.35f, 0.5f), 0.11f, 8, new Color(0.18f, 0.25f, 0.45f), 0f, 0.1f);
                k.Ball(new Vector3(0f, -0.4f, 0.56f), new Vector3(0.09f, 0.09f, 0.15f), Quaternion.identity, Leather, 1); // boot
            });
            return hip;
        }

        /// <summary>Gold-rimmed aviators (also the trophy).</summary>
        public static void AviatorsMesh(MeshKit k)
        {
            foreach (float x in new[] { -0.11f, 0.11f })
            {
                k.Ball(new Vector3(x, 0f, 0f), new Vector3(0.1f, 0.08f, 0.02f), Quaternion.identity, new Color(0.08f, 0.1f, 0.15f), 1);
                k.Prism(new Vector3(x, 0f, -0.01f), Quaternion.Euler(90f, 0f, 0f), 0.105f, 0.105f, 0.015f, 10, new Color(0.9f, 0.72f, 0.3f));
            }
            k.Box(new Vector3(0f, 0.05f, 0f), new Vector3(0.12f, 0.015f, 0.015f), new Color(0.9f, 0.72f, 0.3f));
        }

        protected override Action<MeshKit> KeyItemModel() => null;

        // ---------- Entrance: the long sideways slide ----------

        public override IEnumerator Entrance(BossEntrance cine)
        {
            Vector3 home = transform.parent != null ? transform.parent.position : transform.position;
            Transform neon = transform.parent != null ? transform.parent.Find("Neon") : null;
            Light[] signs = neon != null ? neon.GetComponentsInChildren<Light>(true) : new Light[0];
            foreach (Light sign in signs) sign.enabled = false;
            Vector3 approach = escapeRoute.Length > 1 ? escapeRoute[1].position : home + Vector3.forward * 60f;
            Vector3 inbound = (home - approach).Flatten().normalized;
            Vector3 side = Vector3.Cross(Vector3.up, inbound);
            // The roadhouse sign flickers on.
            cine.Cut(home - side * 14f + Vector3.up * 3f - inbound * 4f, neon != null ? neon.position : home + Vector3.up * 4f);
            for (int i = 0; i < 6 && !cine.Skipped; i++)
            {
                foreach (Light sign in signs) sign.enabled = i % 2 == 1 || i == 5;
                Sfx.Play("electric_sizzle", 0.4f, 1.3f);
                yield return cine.Wait(0.18f);
            }
            foreach (Light sign in signs) sign.enabled = true;
            // An engine echoes down the causeway; a headlight in the fog.
            Sfx.Play("rev", 1f, 0.8f);
            Vector3 start = home - inbound * 55f;
            transform.SetPositionAndRotation(Ground(start), Quaternion.LookRotation(inbound));
            cine.Cut(home - inbound * 6f + side * 4f + Vector3.up * 0.8f, start + Vector3.up * 1.2f);
            cine.LookAt(transform, Vector3.up * 1.2f);
            float speed = 25f;
            float slideStart = 20f;
            bool sliding = false;
            float nextMark = 0f;
            for (float t = 0f; t < 8f && !cine.Tick(); t += Time.deltaTime)
            {
                float remaining = Vector3.Dot(home - transform.position, inbound);
                // The slide plays in slow motion (just him: the camera holds still), slowest at its middle.
                float dt = Time.deltaTime;
                if (sliding) dt *= Mathf.Lerp(1f, 0.4f, Mathf.Sin(Mathf.Clamp01(1f - speed / 25f) * Mathf.PI));
                if (!sliding && remaining < slideStart)
                {
                    // The slide, from down on the road in front of him: he comes at the camera broadside, bike laid
                    // over, one boot skating on the tarmac in a fan of sparks, and stops just short of the lens.
                    sliding = true;
                    Sfx.Play("car_skid", 1f, 0.7f);
                    cine.Cut(home + inbound * 5.8f + side * 0.5f + Vector3.up * 2.4f, transform.position + Vector3.up * 0.6f);
                    cine.LookAt(transform, Vector3.up * 0.55f);
                    Face?.Show(Mood.Smug, 5f);
                }
                if (sliding)
                {
                    speed = Mathf.MoveTowards(speed, 0f, dt * 25f * 25f / (2f * slideStart));
                    float k = 1f - speed / 25f;
                    // Broadside (front wheel to the camera's left, the back swung a touch toward it), laid over away from the
                    // lens so it looks down on the tank and seat, coming up as he stops.
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(side - inbound * 0.27f), dt * 7f); // rear wheel a little nearer the lens
                    float lean = Mathf.Lerp(58f, 30f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 1f, k)));
                    bike.localRotation = Quaternion.Slerp(bike.localRotation, Quaternion.Euler(0f, 0f, -lean), dt * 9f);
                    rider.localRotation = Quaternion.Slerp(rider.localRotation, Quaternion.Euler(0f, 0f, lean * 0.15f), dt * 6f);
                    // The near boot (his left, toward the camera) out and down on the road ahead of the bike.
                    Vector3 drag = (inbound * 0.75f + Vector3.down * 0.65f + side * 0.15f).normalized;
                    PointLeg(legL, drag);
                    Vector3 boot = legL.TransformPoint(LegFoot);
                    float fade = Mathf.Clamp01(speed / 6f);
                    if (Random.value < 0.9f * fade)
                        ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, new Vector3(boot.x, Ground(boot).y + 0.05f, boot.z), inbound + Vector3.up * 0.35f, 7, 0.07f, 9f, 35f,
                            new Color(1f, 0.85f, 0.45f));
                    foreach (Transform wheel in new[] { frontWheel, rearWheel })
                    {
                        Vector3 contact = Ground(wheel.position);
                        if (Random.value < 0.7f * fade)
                            ParticleBurst.Spawn(ParticleBurst.Kind.Sand, contact, inbound + Vector3.up * 0.7f, 4, 0.5f, 5f * fade + 1f, 30f, new Color(0.3f, 0.22f, 0.15f, 0.9f));
                        if (t >= nextMark) SkidMark(contact, inbound);
                    }
                    if (t >= nextMark) nextMark = t + 0.045f;
                    if (Mathf.Repeat(t, 0.3f) < Time.deltaTime) CameraShake.Add(0.08f * fade);
                }
                transform.position = Ground(transform.position + inbound * speed * dt);
                rearWheel.Rotate(speed / 0.6f * Mathf.Rad2Deg * dt, 0f, 0f, Space.Self);
                frontWheel.Rotate(speed / 0.42f * Mathf.Rad2Deg * dt, 0f, 0f, Space.Self);
                if (sliding && speed <= 0.05f) break;
                yield return null;
            }
            // Hold the shot a beat while the dust settles.
            yield return cine.Wait(0.45f);
            CameraShake.Add(0.35f);
            // He straightens up, lowers the aviators...
            for (float t = 0f; t < 0.6f && !cine.Tick(); t += Time.deltaTime)
            {
                bike.localRotation = Quaternion.Slerp(bike.localRotation, Quaternion.Euler(0f, 0f, -8f), Time.deltaTime * 8f);
                rider.localRotation = Quaternion.Slerp(rider.localRotation, Quaternion.identity, Time.deltaTime * 8f);
                legL.localRotation = Quaternion.Slerp(legL.localRotation, Quaternion.identity, Time.deltaTime * 8f);
                yield return null;
            }
            legL.localRotation = Quaternion.identity;
            yield return cine.Move(transform.position + transform.forward * 3.5f + transform.right * 0.6f + Vector3.up * 2.4f, head.position, 0.8f);
            for (float t = 0f; t < 0.6f && !cine.Tick(); t += Time.deltaTime)
            {
                glasses.localPosition = new Vector3(0f, 0.05f - 0.07f * (t / 0.6f), 0.3f);
                yield return null;
            }
            DialogueUi.Instance.Subtitle("Clutch McCheddar", "You're in my lane, pal.", 3f);
            Say("You're in my lane, pal.", new Color(1f, 0.85f, 0.4f), 2.4f, 3.5f, 3.4f);
            yield return cine.Wait(1.6f);
            glasses.localPosition = new Vector3(0f, 0.05f, 0.3f);
            // A big rev, flames from the pipes, and the gang rides out pulling wheelies.
            Sfx.Play("rev", 1f, 1.2f);
            ExhaustFlames(true);
            CameraShake.Add(0.3f);
            cine.Cut(home + inbound * 14f + side * 6f + Vector3.up * 3f, home);
            CallGang(3, true);
            yield return cine.Wait(1.6f);
            ExhaustFlames(false);
            bike.localRotation = Quaternion.identity;
            rider.localRotation = Quaternion.identity;
            legL.localRotation = Quaternion.identity;
        }

        // Where a leg's boot sits, in the leg's own space (see Leg).
        private static readonly Vector3 LegFoot = new Vector3(0f, -0.4f, 0.56f);

        // Swings a leg so its boot points along a world direction.
        private static void PointLeg(Transform leg, Vector3 worldDirection)
        {
            Vector3 local = leg.parent.InverseTransformDirection(worldDirection);
            leg.localRotation = Quaternion.Slerp(leg.localRotation, Quaternion.FromToRotation(LegFoot.normalized, local.normalized), Time.deltaTime * 12f);
        }

        // A dark tyre streak left on the ground (fades after a while).
        private static void SkidMark(Vector3 at, Vector3 along)
        {
            Transform mark = MudArt.Part(null, "Skid Mark", at + Vector3.up * 0.03f, Quaternion.LookRotation(along), "clutch_skid",
                k => k.Box(Vector3.zero, new Vector3(0.22f, 0.012f, 0.75f), new Color(0.09f, 0.07f, 0.06f)), false);
            Destroy(mark.gameObject, 30f);
        }

        // ---------- Base hooks ----------

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
            Enter(State.Circle);
        }

        private void Enter(State next)
        {
            if (ring != null && next != State.Jump) { ring.Finish(); ring = null; }
            state = next;
            stateTime = 0f;
        }

        protected override void UpdatePatrol(float dt) => Circle(dt, 9f);

        // ---------- Combat ----------

        protected override void TickCombat(float dt)
        {
            stateTime += dt;
            UpdateShove(dt);
            gang.RemoveAll(g => g == null);
            if (chaseRunning) Home = Target != null ? Target.position : Home; // the leash follows the chase
            bool nitro = Stage == 3;
            ExhaustFlames(nitro && (state == State.Ram || state == State.Circle || state == State.Getaway));
            switch (state)
            {
                case State.Circle: Circle(dt, nitro ? 16f : 13f); Decide(); break;
                case State.Line: UpdateLine(dt, nitro); break;
                case State.Ram: UpdateRam(dt, nitro); break;
                case State.Donuts: UpdateDonuts(dt, nitro); break;
                case State.Rooster: UpdateRooster(dt); break;
                case State.Gang:
                    Halt();
                    if (stateTime > 1f) Enter(State.Circle);
                    break;
                case State.ToRamp: UpdateToRamp(dt); break;
                case State.Jump: UpdateJump(); break;
                case State.Whip: UpdateWhip(dt); break;
                case State.WipeOut:
                    Halt();
                    if (stateTime > 2f) { DamageTakenMultiplier = 1f; Enter(State.Circle); nextAttack = Time.time + 1f; }
                    break;
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
            if (engine != null && engine.Source != null) engine.Source.pitch = 0.9f + Mathf.Clamp01((AgentReady ? Agent.velocity.magnitude : 0f) / 20f) * 0.8f + (state == State.Line || state == State.Rooster ? 0.5f : 0f);
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
            float turn = Vector3.SignedAngle(transform.forward, Agent.desiredVelocity.Flatten(), Vector3.up);
            bike.localRotation = Quaternion.Slerp(bike.localRotation, Quaternion.Euler(0f, 0f, Mathf.Clamp(-turn * 0.5f, -30f, 30f)), dt * 6f);
        }

        private void Decide()
        {
            if (Time.time < nextAttack || !TargetAlive) return;
            float distance = PlayerDistance;
            int stage = Stage;
            if (Time.time >= nextGang && gang.Count < 3)
            {
                Enter(State.Gang);
                Say("BOYS!", new Color(1f, 0.85f, 0.4f), 1.2f, 5f);
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
            if (Mathf.Repeat(stateTime, 0.2f) < dt) HeadlightFlash();
            if (stateTime < windup) return;
            Enter(State.Ram);
            ramHit = false;
        }

        private void HeadlightFlash()
        {
            ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, transform.position + transform.forward * 1.6f + Vector3.up * 1.2f, transform.forward, 4, 0.2f, 1f, 20f, new Color(1f, 0.95f, 0.6f));
        }

        private void UpdateRam(float dt, bool nitro)
        {
            float speed = nitro ? 26f : Stage == 2 ? 24f : 22f;
            if (AgentReady) Agent.Move(ramDirection * speed * dt);
            transform.rotation = Quaternion.LookRotation(ramDirection);
            rearWheel.Rotate(speed / 0.6f * Mathf.Rad2Deg * dt, 0f, 0f, Space.Self);
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

        private float spinAngle, spinRadius;

        // The donuts are ridden round a point between him and the player, starting from where he already is: the
        // circle begins on his wheels, so nothing jumps.
        private void StartDonuts()
        {
            Enter(State.Donuts);
            Say("WATCH THIS!", new Color(1f, 0.85f, 0.4f), 1f, 4.5f);
            Vector3 toPlayer = (PlayerPosition - transform.position).Flatten();
            float reach = Mathf.Clamp(toPlayer.magnitude * 0.6f, 2.5f, 6f);
            spinCentre = transform.position + (toPlayer.sqrMagnitude > 0.01f ? toPlayer.normalized : transform.forward) * reach;
            Vector3 from = transform.position - spinCentre;
            spinAngle = Mathf.Atan2(from.z, from.x);
            spinRadius = from.Flatten().magnitude;
        }

        private void UpdateDonuts(float dt, bool nitro)
        {
            Halt();
            if (stateTime < 0.6f)
            {
                // The rear wheel smokes: the warning. He swings his back end round to start.
                ParticleBurst.Spawn(ParticleBurst.Kind.Smoke, rearWheel.position, Vector3.up, 2, 0.6f, 1f, 30f, new Color(0.8f, 0.8f, 0.8f, 0.6f));
                Quaternion spinFacing = Quaternion.LookRotation(new Vector3(-Mathf.Sin(spinAngle), 0f, Mathf.Cos(spinAngle))) * Quaternion.Euler(0f, -50f, 0f);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, spinFacing, 360f * dt);
                bike.localRotation = Quaternion.Slerp(bike.localRotation, Quaternion.Euler(0f, 0f, 25f), dt * 6f);
                return;
            }
            float t = stateTime - 0.6f;
            // Round and round, tightening onto the 2.5 m ring in the first second.
            spinAngle += dt * Mathf.Lerp(3.5f, 6.5f, Mathf.Clamp01(t / 0.6f));
            spinRadius = Mathf.MoveTowards(spinRadius, 2.5f, dt * 4f);
            Vector3 at = spinCentre + new Vector3(Mathf.Cos(spinAngle), 0f, Mathf.Sin(spinAngle)) * spinRadius;
            Vector3 step = (at - transform.position).Flatten();
            if (AgentReady) Agent.Move(Vector3.ClampMagnitude(step, 22f * dt));
            Quaternion facing = Quaternion.LookRotation(new Vector3(-Mathf.Sin(spinAngle), 0f, Mathf.Cos(spinAngle))) * Quaternion.Euler(0f, -50f, 0f);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, 720f * dt);
            bike.localRotation = Quaternion.Slerp(bike.localRotation, Quaternion.Euler(0f, 0f, 25f), dt * 8f);
            if (Random.value < 0.6f) ParticleBurst.Spawn(ParticleBurst.Kind.Sand, rearWheel.position, transform.right + Vector3.up * 0.5f, 6, 0.6f, 7f, 25f, new Color(0.3f, 0.22f, 0.15f, 0.9f));
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

        private void UpdateRooster(float dt)
        {
            Halt();
            // Turns his back on you, revs, and the rear tyre throws a fan of mud.
            Vector3 away = (transform.position - PlayerPosition).Flatten();
            if (stateTime < 0.6f && away.sqrMagnitude > 0.01f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(away), 360f * dt);
            if (stateTime < 1.2f)
            {
                rearWheel.Rotate(1500f * dt, 0f, 0f, Space.Self);
                if (Random.value < 0.5f) ParticleBurst.Spawn(ParticleBurst.Kind.Sand, rearWheel.position, -transform.forward + Vector3.up * 0.3f, 3, 0.4f, 3f, 20f, new Color(0.3f, 0.22f, 0.15f, 0.9f));
                if (Mathf.Repeat(stateTime, 0.3f) < dt) Sfx.PlayAt("rev", transform.position, 0.6f, 1.4f, 40f);
                return;
            }
            if (stateTime - dt < 1.2f)
            {
                Vector3 back = -transform.forward;
                ParticleBurst.Spawn(ParticleBurst.Kind.Sand, rearWheel.position, back + Vector3.up * 0.25f, 120, 0.8f, 14f, 30f, new Color(0.28f, 0.2f, 0.13f, 0.95f));
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

        private void UpdateToRamp(float dt)
        {
            Vector3 runUp = ramp.position - ramp.forward * 14f;
            if (stateTime < 0.05f) Say("HOLD MY CHEESE!", new Color(1f, 0.85f, 0.4f), 1.4f, 4f);
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
            Enter(State.Jump);
        }

        private void UpdateJump()
        {
            float k = Mathf.Clamp01(stateTime / 1.2f);
            transform.position = Vector3.Lerp(jumpFrom, jumpTo, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 9f;
            bike.localRotation = Quaternion.Slerp(bike.localRotation, Quaternion.Euler(-25f + 40f * k, 0f, Mathf.Sin(k * Mathf.PI * 2f) * 15f), Time.deltaTime * 12f);
            if (ring != null) ring.Progress = k;
            if (k < 1f) return;
            if (ring != null) { ring.Finish(); ring = null; }
            AgentUtil.Reenable(Agent, 6f);
            CameraShake.Add(0.6f);
            Dust(transform.position, 50, 1.2f);
            Sfx.PlayAt("laser_slam", transform.position, 1f, 0.4f, 60f);
            HurtInCircle(transform.position, 4f, 30f, 6f);
            Enter(State.Circle);
            nextAttack = Time.time + 1.2f;
        }

        private void UpdateWhip(float dt)
        {
            Chase(PlayerPosition, 3f);
            FaceVelocity(dt);
            if (stateTime < 0.8f) return;
            if (stateTime - dt < 0.8f)
            {
                Sfx.PlayAt("chain_swish", transform.position, 1f, 0.6f, 40f);
                ExpandingRing.Spawn(EffectMaterials.Additive(EffectMaterials.Look.Glow), transform.position + Vector3.up * 1.2f, 5f, 0.3f, 0.3f);
                HurtInCircle(transform.position, 5f, 16f, 4f);
            }
            if (stateTime > 1.3f) { Enter(State.Circle); nextAttack = Time.time + 1.5f; }
        }

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

        private void ExhaustFlames(bool on)
        {
            if (on && nitroL == null)
            {
                nitroL = FireEffect.SpawnJet(exhaust, new Vector3(-0.32f, 0f, 0f), Quaternion.identity, 0.12f, 0.8f);
                nitroR = FireEffect.SpawnJet(exhaust, new Vector3(0.32f, 0f, 0f), Quaternion.identity, 0.12f, 0.8f);
                nitroL.SetTint(Nitro);
                nitroR.SetTint(Nitro);
            }
            else if (!on && nitroL != null)
            {
                Destroy(nitroL.gameObject);
                Destroy(nitroR.gameObject);
                nitroL = nitroR = null;
            }
        }

        private void WipeOut(string line)
        {
            Enter(State.WipeOut);
            DamageTakenMultiplier = 1.5f;
            Say(line, new Color(1f, 0.7f, 0.4f), 1.4f, 5f);
            CameraShake.Add(0.4f);
            Sfx.PlayAt("car_crash", transform.position, 1f, 0.8f, 60f);
            Dust(transform.position, 40, 1f);
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
            Say(line, new Color(1f, 0.85f, 0.4f), 2.5f, 3f);
            MudHud.Shout("HE'S GETTING AWAY!", new Color(1f, 0.45f, 0.3f), 2f);
            Sfx.PlayAt("rev", transform.position, 1f, 1.3f, 80f);
            var driving = DrunkDriving.Instance;
            if (driving != null) driving.FreeRides = true;
            if (Agent != null) Agent.enabled = false;
        }

        private void UpdateGetaway(float dt)
        {
            if (escapeRoute.Length == 0) { Escape(); return; }
            escapeTimer += dt;
            // A wheelie off the line, then flat out along the causeway, weaving, dropping barrels.
            Vector3 target = escapeRoute[Mathf.Min(getawayStop, escapeRoute.Length - 1)].position;
            Vector3 to = (target - transform.position).Flatten();
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
            if (escapeTimer < 1.2f) speed = 8f;
            else if (chaser == null)
            {
                speed = 9f; // slow enough that the chug and the car's drop don't lose you the race
                if (!taunted && escapeTimer > 2.5f)
                {
                    taunted = true;
                    Say("C'mon, slowpoke!", new Color(1f, 0.85f, 0.4f), 1.4f, 2.5f);
                }
            }
            // The chase car tops out at 26 m/s (less in mud), so far behind he's 15 m/s slower than you and close up
            // only 8: about ten seconds to catch him from a standing start, with the whole causeway to do it in.
            else speed = Mathf.Lerp(18f, 11f, Mathf.InverseLerp(8f, 45f, Flat(chaser.transform.position - transform.position)));
            Vector3 weave = Vector3.Cross(Vector3.up, to.normalized) * Mathf.Sin(escapeTimer * 1.6f) * 0.35f;
            Vector3 heading = (to.normalized + weave).normalized;
            transform.position = Ground(transform.position + heading * speed * dt);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(heading), dt * 5f);
            bike.localRotation = Quaternion.Slerp(bike.localRotation, Quaternion.Euler(escapeTimer < 1f ? -30f : 0f, 0f, -Mathf.Sin(escapeTimer * 1.6f) * 12f), dt * 10f);
            rearWheel.Rotate(speed / 0.6f * Mathf.Rad2Deg * dt, 0f, 0f, Space.Self);
            if (escapeTimer > 2f && Time.time >= nextDrop)
            {
                nextDrop = Time.time + 2.5f;
                DropBarrel(transform.position - transform.forward * 2.5f);
            }
            float left = RouteLeft();
            MudHud.Chase(DrunkDriving.Instance != null && DrunkDriving.Instance.InCar ? "RAM HIM!" : "HE'S GETTING AWAY!  Hold F and RAM HIM!", left);
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
            Say("ROUND TWO!", new Color(1f, 0.85f, 0.4f), 1.6f, 5f);
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

        // Rammed: he sails over the handlebars, lands face down in the mud, and that's that.
        private IEnumerator OverTheHandlebars()
        {
            Vector3 from = transform.position;
            Vector3 to = Ground(from + transform.forward * 9f);
            for (float t = 0f; t < 1.2f; t += Time.deltaTime)
            {
                float k = t / 1.2f;
                transform.position = Vector3.Lerp(from, to, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 6f;
                Visual.localRotation = Quaternion.Euler(k * 540f, 0f, k * 90f);
                yield return null;
            }
            transform.position = to;
            Dust(to, 50, 1.2f);
            Sfx.PlayAt("mud_splat", to, 1f, 0.5f, 60f);
            CameraShake.Add(0.5f);
            Health.Invulnerable = false;
            Health.TakeDamage(Health.Current + 1f, Target != null ? Target.gameObject : null);
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
            if (state != State.Caught && state != State.Jump) WipeOut("MY PAINT JOB!");
            return blast.Damage;
        }

        protected override void OnPhaseTwo()
        {
            Say("Alright, no more Mr. Nice Mouse.", new Color(1f, 0.85f, 0.4f), 2f, 3.5f);
            Sfx.PlayAt("rev", transform.position, 1f, 0.9f, 80f);
        }

        protected override void UpdatePhaseShift(float dt) => Halt();

        protected override void OnReset()
        {
            foreach (GameObject member in gang) if (member != null) Destroy(member);
            gang.Clear();
            chaseRunning = false;
            escaped = false;
            Home = savedHome;
            Health.Invulnerable = false;
            DamageTakenMultiplier = 1f;
            ExhaustFlames(false);
            AgentUtil.Reenable(Agent, 12f);
            var driving = DrunkDriving.Instance;
            if (driving != null) driving.FreeRides = false;
            Enter(State.Circle);
        }

        protected override void OnMudBossDeath()
        {
            if (engine != null) engine.Level = 0f;
            ExhaustFlames(false);
            TrophyPickup.Create(TrophyId, DropSpot(transform.position + transform.right * 2.5f));
            glasses.gameObject.SetActive(false);
            foreach (GameObject member in gang)
                if (member != null) Emote.Show(member.transform, "SCATTER!", new Color(1f, 0.6f, 0.4f), 1.6f, 2f, 3f);
            foreach (GameObject member in gang)
                if (member != null) Destroy(member, 2.5f);
            var driving = DrunkDriving.Instance;
            if (driving != null) driving.FreeRides = false;
        }

        // ---------- Animation ----------

        protected override void AnimateBoss(float dt)
        {
            float speed = AgentReady ? Agent.velocity.magnitude : 0f;
            if (state != State.Ram && state != State.Getaway && state != State.Rooster)
            {
                rearWheel.Rotate(speed / 0.6f * Mathf.Rad2Deg * dt, 0f, 0f, Space.Self);
                frontWheel.Rotate(speed / 0.42f * Mathf.Rad2Deg * dt, 0f, 0f, Space.Self);
            }
            bool twirl = state == State.Whip || state == State.Donuts && stateTime > 0.6f;
            chainArm.localRotation = twirl ? Quaternion.Euler(-160f, Time.time * 800f, 0f) : Quaternion.Slerp(chainArm.localRotation, Quaternion.Euler(-60f, 0f, 20f), dt * 8f);
            Quaternion riderPose = state == State.WipeOut ? Quaternion.Euler(0f, 0f, 70f) : state == State.Line ? Quaternion.Euler(15f, 0f, 0f) : Quaternion.identity;
            rider.localRotation = Quaternion.Slerp(rider.localRotation, riderPose, dt * 10f);
            if (state == State.WipeOut) bike.localRotation = Quaternion.Slerp(bike.localRotation, Quaternion.Euler(0f, 0f, 80f), dt * 10f);
            if (head != null && Target != null)
            {
                Vector3 local = transform.InverseTransformPoint(PlayerPosition + Vector3.up * 1.5f);
                float yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -70f, 70f);
                head.localRotation = Quaternion.Slerp(head.localRotation, Quaternion.Euler(0f, yaw, 0f), dt * 6f);
            }
        }

        protected override void UpdateDeath(float t)
        {
            // Face down in the mud, one hand raised, then still.
            Visual.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Visual.localPosition = VisualRest + Vector3.up * 0.3f;
            chainArm.localRotation = Quaternion.Euler(t < 1.5f ? -170f + Mathf.Sin(t * 8f) * 15f : -10f, 0f, 0f);
        }
    }
}
