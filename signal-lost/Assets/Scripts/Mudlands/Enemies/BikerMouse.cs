using SignalLost.Audio;
using SignalLost.Combat;
using SignalLost.Effects;
using SignalLost.Inventory;
using UnityEngine;
using Random = UnityEngine.Random;

namespace SignalLost.Mudlands
{
    /// <summary>
    /// The stage boss's gang, the Swamp Rats MC: small alien mice (green-furred in three shades, magenta skin, a third
    /// eye over their shades, antennae with glowing bulbs, a glowing tail tip) on makeshift mini hovers, each one
    /// different (<see cref="Hoverbikes.Mini"/>: a longhorn, a skipper or a tub; four paints; a black, brown or red
    /// jacket; no helmet, a hull-plate dome or a bone-spiked one). They float on two little lift rings. They circle you
    /// at speed, then swing in, twirling a chain overhead (0.5 s), and swipe as they pass (8). A heavy hit or the
    /// combo finisher knocks one off its bike: it runs after the bike shaking its fist, and climbs back on. They
    /// swerve away from cars they see coming. 35 HP, fodder level.
    /// </summary>
    public sealed class BikerMouse : MudCritter
    {
        public static readonly CritterSpec Defaults = new CritterSpec("Biker Mouse", 35f, 0.5f, 1.1f, 9f, 40f,
            (ScrapType.ScrapMetal, 0, 1), (ScrapType.Wiring, 0, 1));

        private enum State { Circle, Swing, Pass, Thrown, Run, Mount }

        // Alien mice: three greens of fur, magenta skin, mint glowing bits (like their president).
        private static readonly Color[] Furs = { new Color(0.47f, 0.62f, 0.42f), new Color(0.4f, 0.6f, 0.58f), new Color(0.6f, 0.65f, 0.36f) };
        private static readonly Color Pink = new Color(0.84f, 0.4f, 0.8f), PinkDark = new Color(0.58f, 0.24f, 0.6f), Glow = new Color(0.55f, 1f, 0.78f);
        private static readonly Color[] Jackets = { new Color(0.12f, 0.12f, 0.13f), new Color(0.42f, 0.26f, 0.16f), new Color(0.6f, 0.13f, 0.12f) };
        private static readonly Color[] Paints = { new Color(0.75f, 0.15f, 0.15f), new Color(0.2f, 0.35f, 0.75f), new Color(0.9f, 0.7f, 0.15f), new Color(0.2f, 0.6f, 0.35f) };

        [SerializeField, Range(-1, 35)] private int variant = -1;

        private State state = State.Circle;
        private float stateTime, orbitAngle, orbitDirection = 1f, swerveUntil, nextSwing;
        private Vector3 swerve, passTarget;
        private bool swiped;
        private Transform bike, rider, chainArm, chain, wheelF, wheelB, eyeM; // wheelF/B: the lift fans
        private SfxLoop engine;

        protected override void BuildModel(Transform visual)
        {
            if (variant < 0) variant = Random.Range(0, 36);
            int bikeKind = variant % 3, jacket = (variant / 3) % 3, helmet = (variant / 9) % 3, paint = variant % 4, furKind = (variant / 4) % 3;
            bike = MudArt.Pivot(visual, "Hover", Vector3.zero);
            MudArt.Part(bike, "Frame", Vector3.zero, Quaternion.identity, "mouse_hover_" + bikeKind + "_" + paint, k => Hoverbikes.Mini.Body(k, bikeKind, Paints[paint]));
            // Two little lift rings, each with a fan of leaf blades that spins.
            float podY = Hoverbikes.Mini.PodHeight;
            foreach (float z in new[] { Hoverbikes.Mini.FrontZ(bikeKind), Hoverbikes.Mini.RearZ })
                MudArt.Part(bike, z > 0f ? "Pod F" : "Pod B", new Vector3(0f, podY, z), Quaternion.identity, "mouse_pod", Hoverbikes.Mini.Pod);
            wheelF = MudArt.Part(bike, "Fan F", new Vector3(0f, podY, Hoverbikes.Mini.FrontZ(bikeKind)), Quaternion.identity, "mouse_pod_fan", Hoverbikes.Mini.PodFan);
            wheelB = MudArt.Part(bike, "Fan B", new Vector3(0f, podY, Hoverbikes.Mini.RearZ), Quaternion.Euler(0f, 45f, 0f), "mouse_pod_fan", Hoverbikes.Mini.PodFan);

            rider = MudArt.Pivot(bike, "Rider", new Vector3(0f, 0.5f, -0.1f));
            Color coat = Jackets[jacket], fur = Furs[furKind];
            MudArt.Part(rider, "Body", Vector3.zero, Quaternion.identity, "mouse_body_" + jacket, k =>
            {
                k.Ball(new Vector3(0f, 0.2f, 0f), new Vector3(0.16f, 0.2f, 0.14f), Quaternion.identity, coat, 1);
                k.Ball(new Vector3(0f, 0.05f, 0.05f), new Vector3(0.15f, 0.1f, 0.18f), Quaternion.identity, MudPalette.DarkMetal, 0);
                k.Box(new Vector3(0f, 0.28f, -0.13f), new Vector3(0.18f, 0.14f, 0.02f), MudPalette.Cream); // the patch
            });
            Head = MudArt.Pivot(rider, "Head", new Vector3(0f, 0.48f, 0.05f));
            MudArt.Part(Head, "Face", Vector3.zero, Quaternion.identity, "mouse_head_" + helmet + "_" + furKind, k =>
            {
                k.Ball(Vector3.zero, new Vector3(0.14f, 0.13f, 0.14f), Quaternion.identity, fur, 1);
                k.Cone(new Vector3(0f, -0.02f, 0.08f), Quaternion.Euler(90f, 0f, 0f), 0.08f, 0.14f, 6, fur);
                k.Ball(new Vector3(0f, -0.02f, 0.23f), 0.03f, Pink, 0);
                foreach (float x in new[] { -0.13f, 0.13f })
                {
                    // Big frilled alien ears, glowing skin inside.
                    k.Prism(new Vector3(x, 0.13f, -0.02f), Quaternion.Euler(90f, 0f, 0f), 0.11f, 0.11f, 0.02f, 9, fur);
                    k.Prism(new Vector3(x, 0.13f, -0.005f), Quaternion.Euler(90f, 0f, 0f), 0.075f, 0.075f, 0.02f, 9, Pink, 0.4f);
                    for (int i = 0; i < 4; i++)
                    {
                        float a = (20f + i * 50f) * Mathf.Deg2Rad;
                        k.Ball(new Vector3(x + Mathf.Cos(a) * 0.105f * Mathf.Sign(x), 0.13f + Mathf.Sin(a) * 0.105f, -0.01f), 0.025f, fur, 0);
                    }
                    // Antennae with glowing bulbs, and glowing freckles on the snout.
                    float s = Mathf.Sign(x);
                    k.Rod(new Vector3(0.04f * s, 0.1f, 0f), new Vector3(0.07f * s, 0.24f, -0.03f), 0.01f, 5, PinkDark);
                    k.Rod(new Vector3(0.07f * s, 0.24f, -0.03f), new Vector3(0.1f * s, 0.3f, -0.07f), 0.009f, 5, PinkDark);
                    k.Ball(new Vector3(0.1f * s, 0.31f, -0.08f), 0.025f, Glow, 0, 2.5f);
                    k.Ball(new Vector3(0.045f * s, 0.0f, 0.17f), 0.01f, Glow, 0, 1.6f);
                }
                k.Box(new Vector3(0f, 0.04f, 0.12f), new Vector3(0.2f, 0.04f, 0.03f), MudPalette.Black); // shades
                if (helmet == 1) // a dome beaten out of hull plate, its orange stripe round it
                {
                    k.Ball(new Vector3(0f, 0.04f, -0.01f), new Vector3(0.16f, 0.12f, 0.16f), Quaternion.identity, MudPalette.Hull, 1);
                    k.Prism(new Vector3(0f, 0.035f, -0.01f), Quaternion.identity, 0.162f, 0.162f, 0.025f, 9, MudPalette.HullStripe);
                }
                if (helmet == 2) // a dark shell with a row of bone spikes
                {
                    k.Ball(new Vector3(0f, 0.04f, -0.01f), new Vector3(0.16f, 0.12f, 0.16f), Quaternion.identity, Hoverbikes.BarkDark, 1);
                    for (int i = 0; i < 3; i++) k.Cone(new Vector3(0f, 0.13f, 0.08f - i * 0.08f), Quaternion.identity, 0.025f, 0.08f, 4, Hoverbikes.Bone);
                }
            });
            (Transform l, Transform r) = MudArt.Eyes(Head, new Vector3(0f, 0.07f, 0.1f), 0.05f, 0.02f, MudPalette.Black, MudPalette.Black, "mouse");
            EyeL = l;
            EyeR = r;
            // A third eye above the shades, the alien kind: it looks and blinks with the other two.
            eyeM = MudArt.Pivot(Head, "Eye M", new Vector3(0f, 0.105f, 0.105f));
            MudArt.Part(eyeM, "White", Vector3.zero, Quaternion.identity, "mouse_eye3_white", k => k.Ball(Vector3.zero, 0.028f, MudPalette.White, 1));
            MudArt.Part(eyeM, "Pupil", new Vector3(0f, 0f, 0.02f), Quaternion.identity, "mouse_eye3_pupil",
                k => k.Ball(Vector3.zero, new Vector3(0.012f, 0.016f, 0.01f), Quaternion.identity, MudPalette.Black, 1));
            chainArm = MudArt.Pivot(rider, "Chain Arm", new Vector3(0.14f, 0.3f, 0.02f));
            MudArt.Part(chainArm, "Arm", Vector3.zero, Quaternion.identity, "mouse_arm_" + jacket, k => k.Rod(Vector3.zero, new Vector3(0.05f, -0.2f, 0.12f), 0.035f, 5, coat));
            chain = MudArt.Part(chainArm, "Chain", new Vector3(0.05f, -0.2f, 0.12f), Quaternion.identity, "mouse_chain", k =>
            {
                for (int i = 0; i < 6; i++) k.Prism(new Vector3(0f, -i * 0.07f, 0f), Quaternion.Euler(0f, 0f, i % 2 == 0 ? 0f : 90f), 0.025f, 0.025f, 0.06f, 4, MudPalette.Metal);
            });
            MudArt.Part(rider, "Tail", new Vector3(0f, 0.05f, -0.15f), Quaternion.identity, "mouse_tail", k =>
            {
                k.Tube(new[] { Vector3.zero, new Vector3(0f, 0.05f, -0.25f), new Vector3(0.1f, 0.18f, -0.4f) }, 0.015f, 4, Pink);
                k.Ball(new Vector3(0.1f, 0.18f, -0.4f), 0.03f, Glow, 0, 2.2f); // glowing tip
            });
        }

        protected override void OnCritterStarted()
        {
            orbitAngle = Random.value * Mathf.PI * 2f;
            orbitDirection = Random.value < 0.5f ? -1f : 1f;
            nextSwing = Time.time + Random.Range(1.5f, 3.5f);
            if (AgentReady)
            {
                Agent.acceleration = 30f;
                Agent.angularSpeed = 540f;
            }
            engine = Sfx.AddLoop(gameObject, "bike_engine", 0.12f, true, 2f, 22f);
            if (engine != null && engine.Source != null) engine.Source.pitch = Random.Range(1.3f, 1.7f);
        }

        private void Enter(State next)
        {
            state = next;
            stateTime = 0f;
        }

        protected override void Think(float dt)
        {
            stateTime += dt;
            if (!TargetAlive) { Halt(); return; }
            switch (state)
            {
                case State.Circle:
                    orbitAngle += orbitDirection * dt * 0.75f * Tempo;
                    Vector3 goal = PlayerPosition + new Vector3(Mathf.Cos(orbitAngle), 0f, Mathf.Sin(orbitAngle)) * 10f;
                    if (Time.time < swerveUntil) goal = transform.position + swerve * 6f;
                    MoveTo(goal, Defaults.Speed);
                    if (Time.time >= nextSwing && PlayerDistance < 16f)
                    {
                        Enter(State.Swing);
                        swiped = false;
                        Say("!", new Color(1f, 0.85f, 0.3f), 0.6f);
                        Sfx.PlayAt("rev", transform.position, 0.4f, 1.6f, 25f);
                    }
                    break;
                case State.Swing:
                    MoveTo(PlayerPosition, Defaults.Speed * 1.15f);
                    if (stateTime > 0.5f && !swiped && PlayerDistance < 2.4f)
                    {
                        swiped = true;
                        Sfx.PlayAt("chain_swish", transform.position, 0.8f, 1.1f, 25f);
                        if (Strike(2.6f, 200f, 8f, 1.5f)) Say("YEEHAW!", new Color(1f, 0.7f, 0.4f), 0.9f, 2.5f);
                        passTarget = transform.position + transform.forward * 9f;
                        Enter(State.Pass);
                    }
                    else if (stateTime > 3f) { Enter(State.Circle); nextSwing = Time.time + 2f; }
                    break;
                case State.Pass:
                    MoveTo(passTarget, Defaults.Speed);
                    if (stateTime > 1f) { Enter(State.Circle); nextSwing = Time.time + Random.Range(2.5f, 4.5f); orbitDirection = -orbitDirection; }
                    break;
                case State.Thrown:
                    Halt();
                    if (stateTime > 0.9f) Enter(State.Run);
                    break;
                case State.Run:
                    MoveTo(bikeRest, 4f);
                    if (FlatDistance(bikeRest) < 0.8f) { Enter(State.Mount); Halt(); }
                    if (Mathf.Repeat(stateTime, 1.2f) < dt) Say(Random.value < 0.5f ? "MY BIKE!" : "#@!", new Color(1f, 0.6f, 0.4f), 0.9f, 2.5f);
                    break;
                case State.Mount:
                    if (stateTime > 0.5f) Enter(State.Circle);
                    break;
            }
            WatchPlayer(70f);
        }

        private Vector3 bikeRest;

        protected override void OnHit(float amount, GameObject source, Vector3 away)
        {
            if (state == State.Thrown || state == State.Run || state == State.Mount) return;
            if (amount >= 20f || IsStunned) KnockOff(away);
        }

        private void KnockOff(Vector3 away)
        {
            Enter(State.Thrown);
            rider.SetParent(Visual, false);
            rider.localRotation = Quaternion.identity;
            bike.SetParent(null, true);
            bikeRest = transform.position + away * 3f;
            Say("WHOA!", new Color(1f, 0.8f, 0.4f), 0.8f, 3f);
            Sfx.PlayAt("car_crash", transform.position, 0.4f, 1.6f, 25f);
        }

        public override void OnCarApproaching(DrunkCar car, float secondsAway)
        {
            if (secondsAway > 1.6f || Time.time < swerveUntil || state == State.Thrown || state == State.Run) return;
            if (Random.value > 0.7f) return; // not all of them see it coming
            Vector3 path = car.Velocity.Flat();
            Vector3 side = Vector3.Cross(Vector3.up, path.normalized);
            if (Vector3.Dot(side, transform.position - car.transform.position) < 0f) side = -side;
            swerve = side;
            swerveUntil = Time.time + 0.8f;
            Enter(State.Circle);
            Say("!!", new Color(1f, 0.6f, 0.4f), 0.6f);
        }

        public override VehicleResponse OnVehicleHit(VehicleHit hit)
        {
            if (hit.Speed < 6f) return VehicleResponse.Bounce;
            Say("NOOO!", new Color(1f, 0.5f, 0.4f), 0.9f, 3f);
            RunOver(hit, 1f);
            return VehicleResponse.RunOver;
        }

        protected override void Animate(float dt)
        {
            if (IsStunned || Airborne) return;
            float speed = AgentReady ? Agent.velocity.magnitude : 0f;
            float spin = (600f + speed * 60f) * dt; // the lift fans: always turning over, faster with speed
            bool riding = bike.parent == Visual;
            if (wheelF != null && riding) { wheelF.Rotate(0f, spin, 0f, Space.Self); wheelB.Rotate(0f, -spin, 0f, Space.Self); }
            if (riding) bike.localPosition = new Vector3(0f, 0.03f + Mathf.Sin(Time.time * 3.1f + variant) * 0.025f, 0f); // bobbing on its lift
            if (eyeM != null && EyeL != null) { eyeM.localRotation = EyeL.localRotation; eyeM.localScale = EyeL.localScale; }
            // Leans into turns; pops a wheelie when it speeds off.
            float turn = AgentReady ? Vector3.SignedAngle(transform.forward, Agent.desiredVelocity.Flat(), Vector3.up) : 0f;
            float wheelie = state == State.Pass ? Mathf.Sin(Mathf.Clamp01(stateTime / 0.8f) * Mathf.PI) * 25f : 0f;
            if (bike.parent == Visual)
                bike.localRotation = Quaternion.Slerp(bike.localRotation, Quaternion.Euler(-wheelie, 0f, Mathf.Clamp(-turn * 0.4f, -25f, 25f)), dt * 8f);
            bool twirl = state == State.Swing;
            chainArm.localRotation = twirl ? Quaternion.Euler(-160f, Time.time * 900f, 0f) : Quaternion.Euler(state == State.Run ? -150f + Mathf.Sin(Time.time * 20f) * 30f : -10f, 0f, 0f);
            if (state == State.Thrown || state == State.Run || state == State.Mount)
            {
                rider.localPosition = new Vector3(0f, state == State.Run ? Mathf.Abs(Mathf.Sin(Time.time * 18f)) * 0.08f : 0f, 0f);
                if (state == State.Mount && bike.parent != Visual)
                {
                    bike.SetParent(Visual, true);
                    bike.localPosition = Vector3.zero;
                    bike.localRotation = Quaternion.identity;
                    rider.SetParent(bike, false);
                }
            }
            else rider.localPosition = new Vector3(0f, 0.5f, -0.1f);
            if (engine != null) engine.Level = state == State.Thrown || state == State.Run ? 0.2f : 1f;
        }

        protected override void OnCritterDeath()
        {
            if (engine != null) engine.Level = 0f;
            if (rider != null) rider.SetParent(Visual, true);
            if (bike != null && bike.parent == Visual)
            {
                bike.SetParent(null, true);
                var rb = bike.gameObject.AddComponent<Rigidbody>();
                bike.gameObject.AddComponent<BoxCollider>();
                rb.linearVelocity = transform.forward * 4f + Vector3.up * 3f;
                rb.angularVelocity = Random.insideUnitSphere * 6f;
                bike.gameObject.AddComponent<Debris>().Begin(3f);
            }
            else if (bike != null) bike.gameObject.AddComponent<Debris>().Begin(2f);
        }

        private void OnDestroy()
        {
            if (bike != null && bike.parent != Visual && bike.GetComponent<Debris>() == null) Destroy(bike.gameObject);
        }
    }
}
