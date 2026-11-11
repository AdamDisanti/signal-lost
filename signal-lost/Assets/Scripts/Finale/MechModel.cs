using SignalLost.Mudlands;
using UnityEngine;

namespace SignalLost.Finale
{
    /// <summary>
    /// Colours for the final level and Cavendish's mech. The mech's armour echoes Cavendish's own suit (off-white,
    /// orange trim, a teal panel); each stolen part keeps its own look from the island it came from.
    /// </summary>
    public static class FinalePalette
    {
        public static readonly Color Armour = new Color(0.86f, 0.84f, 0.78f);
        public static readonly Color ArmourShade = new Color(0.7f, 0.69f, 0.65f);
        public static readonly Color Trim = new Color(0.93f, 0.47f, 0.15f);
        public static readonly Color Frame = new Color(0.24f, 0.25f, 0.28f);
        public static readonly Color DarkFrame = new Color(0.13f, 0.13f, 0.15f);
        public static readonly Color Steel = new Color(0.62f, 0.64f, 0.67f);
        public static readonly Color Rust = new Color(0.55f, 0.27f, 0.13f);
        public static readonly Color Teal = new Color(0.25f, 0.8f, 0.75f);
        public static readonly Color Hazard = new Color(0.98f, 0.8f, 0.15f);
        public static readonly Color Rubber = new Color(0.1f, 0.1f, 0.11f);
        public static readonly Color Coolant = new Color(0.35f, 0.8f, 1f);
        public static readonly Color EngineGlow = new Color(1f, 0.55f, 0.15f);
        public static readonly Color Reactor = new Color(0.75f, 1f, 0.3f);
        public static readonly Color Glass = new Color(0.6f, 0.9f, 1f, 0.22f);
        public static readonly Color Shield = new Color(0.3f, 0.95f, 0.9f, 0.16f);
        public static readonly Color Ice = new Color(0.7f, 0.92f, 1f, 0.75f);
        public static readonly Color Warning = new Color(1f, 0.3f, 0.15f, 0.32f);
        public static readonly Color FrostWarning = new Color(0.4f, 0.8f, 1f, 0.3f);
    }

    /// <summary>The mech's joints, filled in by <see cref="MechModel.Build"/> and animated by <see cref="CavendishMech"/>.</summary>
    public sealed class MechRig
    {
        public Transform Hips, Waist, Chest, Cockpit, Pilot, Dish;
        public Transform LegL, LegR, KneeL, KneeR, FootL, FootR;
        public Transform ShoulderL, ShoulderR, ElbowL, ElbowR;
        public Transform CannonMuzzle, Fist;
        public Transform Reactor, ReactorGlow, ThrusterL, ThrusterR, Turbine;
        public Renderer[] CoolantGlows, EngineGlows;
    }

    /// <summary>
    /// Builds Cavendish's mech ("the Safekeeper", a working name) from code, like the Mudlands bosses: a tall humanoid,
    /// about 13 m, put together from the ship parts the player handed him "for safe keeping". The main parts are its
    /// weapons; the mini-boss parts are bolted on as armour and trim, so players recognise what they gave away:
    /// <list type="bullet">
    /// <item>left forearm: the <b>Cooling System</b> (the Chief Toad's coolant tubes and pump), a freezing cannon;</item>
    /// <item>back: the <b>Engine Core</b> turbine, feeding the desert's <b>rocket thrusters</b>;</item>
    /// <item>shins and feet: the Mudlands' <b>Landing Gear</b> struts and wheels;</item>
    /// <item>shoulders: the desert's <b>wing panels</b> as pauldrons, with <b>Flight Control Module</b> jets behind;</item>
    /// <item>head: the <b>Navigation Computer</b> dish over the cockpit; hip: the desert's <b>fuel tank</b>;</item>
    /// <item>chest: the <b>Reactor Core</b>, its heart and weak spot.</item>
    /// </list>
    /// Cavendish sits in a glass cockpit on top. The root stands on y = 0 facing +Z. Preliminary art: Adam expects the
    /// final mech to be revised.
    /// </summary>
    public static class MechModel
    {
        public const float HipHeight = 7.4f, ThighLength = 3.3f, ShinLength = 3.2f;

        public static MechRig Build(Transform visual, GameObject pilotPrefab)
        {
            var rig = new MechRig();
            rig.Hips = MudArt.Pivot(visual, "Hips", new Vector3(0f, HipHeight, 0f));
            MudArt.Part(rig.Hips, "Pelvis", Vector3.zero, Quaternion.identity, "mech_pelvis", Pelvis);
            MudArt.Part(rig.Hips, "Fuel Tank", new Vector3(-2.1f, -0.2f, -0.6f), Quaternion.Euler(0f, 0f, 8f), "mech_fueltank", FuelTank);

            rig.LegL = Leg(rig.Hips, -1, out rig.KneeL, out rig.FootL);
            rig.LegR = Leg(rig.Hips, 1, out rig.KneeR, out rig.FootR);

            rig.Waist = MudArt.Pivot(rig.Hips, "Waist", new Vector3(0f, 0.5f, 0f));
            rig.Chest = MudArt.Pivot(rig.Waist, "Chest", Vector3.zero);
            MudArt.Part(rig.Chest, "Torso", Vector3.zero, Quaternion.identity, "mech_torso", Torso);
            MudArt.Part(rig.Chest, "Engine Core", new Vector3(0f, 3.1f, -1.95f), Quaternion.identity, "mech_engine", EngineCore);
            rig.Turbine = MudArt.Part(rig.Chest, "Turbine Fan", new Vector3(0f, 3.1f, -3.2f), Quaternion.identity, "mech_turbine", Turbine);
            rig.ThrusterL = MudArt.Pivot(rig.Chest, "Thruster L", new Vector3(-1.25f, 1.5f, -1.7f), Quaternion.Euler(-150f, 0f, 0f));
            rig.ThrusterR = MudArt.Pivot(rig.Chest, "Thruster R", new Vector3(1.25f, 1.5f, -1.7f), Quaternion.Euler(-150f, 0f, 0f));
            MudArt.Part(rig.ThrusterL, "Nozzle", Vector3.zero, Quaternion.identity, "mech_nozzle", Nozzle);
            MudArt.Part(rig.ThrusterR, "Nozzle", Vector3.zero, Quaternion.identity, "mech_nozzle", Nozzle);

            rig.Reactor = MudArt.Pivot(rig.Chest, "Reactor Core", new Vector3(0f, 3.0f, 1.55f));
            MudArt.Part(rig.Reactor, "Cage", Vector3.zero, Quaternion.identity, "mech_reactorcage", ReactorCage);
            rig.ReactorGlow = MudArt.Part(rig.Reactor, "Core", Vector3.zero, Quaternion.identity, "mech_reactorcore",
                k => { k.Smooth = true; k.Ball(Vector3.zero, 0.58f, FinalePalette.Reactor, 2, 2f); });

            // The head is the cockpit: a helmet with a crest and little capybara ears (Cavendish's own), a glass
            // canopy at the front with Cavendish inside, and the Navigation Computer dish on its side.
            rig.Cockpit = MudArt.Pivot(rig.Chest, "Cockpit", new Vector3(0f, 5.65f, 0.15f));
            MudArt.Part(rig.Cockpit, "Helmet", Vector3.zero, Quaternion.identity, "mech_helmet", Helmet);
            if (pilotPrefab != null) rig.Pilot = SeatPilot(rig.Cockpit, pilotPrefab);
            Transform glass = MudArt.Part(rig.Cockpit, "Canopy", Vector3.zero, Quaternion.identity, "mech_canopy",
                k => { k.Smooth = true; k.Ball(new Vector3(0f, 0.05f, 0.45f), new Vector3(1.05f, 0.9f, 0.8f), Quaternion.identity, FinalePalette.Glass, 2); }, false);
            glass.GetComponent<MeshRenderer>().sharedMaterial = MudArt.Clear(FinalePalette.Glass, 0.15f);
            rig.Dish = MudArt.Pivot(rig.Cockpit, "Nav Dish", new Vector3(1.5f, 0.75f, -0.5f));
            MudArt.Part(rig.Dish, "Dish", Vector3.zero, Quaternion.identity, "mech_navdish", NavDish);

            rig.ShoulderL = Arm(rig.Chest, -1, out rig.ElbowL);
            rig.ShoulderR = Arm(rig.Chest, 1, out rig.ElbowR);
            MudArt.Part(rig.ElbowL, "Cooling Cannon", Vector3.zero, Quaternion.identity, "mech_coolingcannon", CoolingCannon);
            rig.CannonMuzzle = MudArt.Pivot(rig.ElbowL, "Muzzle", new Vector3(0f, -3.75f, 0f), Quaternion.Euler(90f, 0f, 0f));
            rig.Fist = MudArt.Part(rig.ElbowR, "Scrap Fist", Vector3.zero, Quaternion.identity, "mech_fist", Fist);

            rig.CoolantGlows = Glows(visual, FinalePalette.Coolant);
            rig.EngineGlows = Glows(visual, FinalePalette.EngineGlow);
            return rig;
        }

        // Renderers with a submesh glowing in this colour (pulsed by the boss).
        private static Renderer[] Glows(Transform root, Color color)
        {
            var found = new System.Collections.Generic.List<Renderer>();
            foreach (MeshRenderer r in root.GetComponentsInChildren<MeshRenderer>(true))
                foreach (Material m in r.sharedMaterials)
                    if (m != null && m.IsKeywordEnabled("_EMISSION") && m.HasProperty("_BaseColor") && Same(m.GetColor("_BaseColor"), color)) { found.Add(r); break; }
            return found.ToArray();
        }

        private static bool Same(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 0.02f;

        // Cavendish, about 1.7 m tall, sitting so his head and shoulders fill the canopy.
        private static Transform SeatPilot(Transform cockpit, GameObject prefab)
        {
            GameObject pilot = Object.Instantiate(prefab, cockpit);
            pilot.name = "Cavendish (pilot)";
            pilot.transform.localPosition = Vector3.zero;
            pilot.transform.localRotation = Quaternion.identity;
            foreach (Collider c in pilot.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
            foreach (MonoBehaviour b in pilot.GetComponentsInChildren<MonoBehaviour>(true)) b.enabled = false;
            Bounds bounds = Measure(pilot.transform);
            float scale = bounds.size.y > 0.01f ? 1.7f / bounds.size.y : 1f;
            pilot.transform.localScale = Vector3.one * scale;
            bounds = Measure(pilot.transform);
            Vector3 top = cockpit.TransformPoint(new Vector3(0f, 0.75f, 0.35f));
            pilot.transform.position += new Vector3(top.x - bounds.center.x, top.y - bounds.max.y, top.z - bounds.center.z);
            return pilot.transform;
        }

        private static Bounds Measure(Transform root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(root.position, Vector3.zero);
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
            return bounds;
        }

        // ---------- Legs: Landing Gear struts and wheels ----------

        private static Transform Leg(Transform hips, int side, out Transform knee, out Transform foot)
        {
            Transform hip = MudArt.Pivot(hips, side < 0 ? "Leg L" : "Leg R", new Vector3(1.9f * side, -0.1f, 0f));
            MudArt.Part(hip, "Thigh", Vector3.zero, Quaternion.identity, "mech_thigh", Thigh);
            knee = MudArt.Pivot(hip, "Knee", new Vector3(0f, -ThighLength, 0f));
            MudArt.Part(knee, "Landing Gear", Vector3.zero, Quaternion.identity, "mech_shin", Shin);
            foot = MudArt.Pivot(knee, "Foot", new Vector3(0f, -ShinLength, 0f));
            MudArt.Part(foot, "Wheel Foot", Vector3.zero, Quaternion.identity, "mech_foot", Foot);
            return hip;
        }

        private static void Thigh(MeshKit k)
        {
            k.Smooth = true;
            k.Ball(Vector3.zero, 0.85f, FinalePalette.Frame, 2); // hip joint
            k.Smooth = false;
            k.Box(new Vector3(0f, -1.5f, 0.05f), new Vector3(1.6f, 2.3f, 1.7f), FinalePalette.Armour);
            k.Box(new Vector3(0f, -1.45f, 0.92f), new Vector3(1.1f, 1.7f, 0.18f), FinalePalette.ArmourShade);
            k.Box(new Vector3(0.85f, -1.5f, 0f), new Vector3(0.14f, 1.6f, 1.1f), FinalePalette.DarkFrame); // side vents
            k.Box(new Vector3(-0.85f, -1.5f, 0f), new Vector3(0.14f, 1.6f, 1.1f), FinalePalette.DarkFrame);
            k.Box(new Vector3(0f, -0.4f, 0.9f), new Vector3(1.3f, 0.22f, 0.12f), FinalePalette.Trim);
            k.Box(new Vector3(0f, -ThighLength + 0.05f, 0.1f), new Vector3(1.15f, 0.9f, 1.25f), FinalePalette.Frame); // knee block
            k.Box(new Vector3(0f, -ThighLength + 0.15f, 0.8f), new Vector3(1.0f, 0.75f, 0.3f), Quaternion.Euler(-15f, 0f, 0f), FinalePalette.Trim); // knee cap
        }

        private static void Shin(MeshKit k)
        {
            // A landing-gear oleo strut: the cylinder, the shiny piston, a torque link and a spring.
            k.Prism(new Vector3(0f, -1.9f, 0f), Quaternion.identity, 0.44f, 0.54f, 1.6f, 10, FinalePalette.Frame);
            k.Prism(new Vector3(0f, -ShinLength + 0.1f, 0f), Quaternion.identity, 0.27f, 0.27f, 1.4f, 10, FinalePalette.Steel);
            for (int i = 0; i < 6; i++)
                k.Prism(new Vector3(0f, -2.95f + i * 0.17f, 0f), Quaternion.identity, 0.42f, 0.42f, 0.07f, 10, i % 2 == 0 ? FinalePalette.Hazard : FinalePalette.DarkFrame);
            k.Plank(new Vector3(0f, -0.5f, 0.6f), new Vector3(0f, -1.6f, 0.85f), 0.2f, 0.12f, FinalePalette.Steel);
            k.Plank(new Vector3(0f, -1.6f, 0.85f), new Vector3(0f, -2.8f, 0.55f), 0.2f, 0.12f, FinalePalette.Steel);
            k.Box(new Vector3(0f, -0.75f, 0.1f), new Vector3(1.3f, 1.3f, 1.3f), FinalePalette.Armour); // shin guard
            k.Box(new Vector3(0f, -0.8f, 0.78f), new Vector3(0.9f, 0.9f, 0.12f), FinalePalette.ArmourShade);
            k.Box(new Vector3(0f, -1.3f, 0.8f), new Vector3(0.95f, 0.18f, 0.14f), FinalePalette.Hazard);
        }

        private static void Foot(MeshKit k)
        {
            k.Box(new Vector3(0f, -0.62f, 0.55f), new Vector3(1.8f, 0.36f, 3.0f), FinalePalette.DarkFrame);  // skid plate
            k.Box(new Vector3(0f, -0.25f, 0.35f), new Vector3(1.4f, 0.5f, 2.0f), FinalePalette.Armour);
            k.Box(new Vector3(0f, -0.2f, 1.36f), new Vector3(1.3f, 0.32f, 0.12f), FinalePalette.Trim);
            for (int i = -1; i <= 1; i++)
                k.Box(new Vector3(i * 0.55f, -0.66f, 2.15f), new Vector3(0.42f, 0.3f, 0.6f), Quaternion.Euler(10f, 0f, 0f), FinalePalette.Steel); // toes
            // The wheel at the heel, with its hub.
            Quaternion axle = Quaternion.Euler(0f, 0f, 90f);
            k.Prism(new Vector3(-0.47f, -0.42f, -0.85f), axle, 0.62f, 0.62f, 0.94f, 14, FinalePalette.Rubber);
            k.Prism(new Vector3(-0.51f, -0.42f, -0.85f), axle, 0.32f, 0.32f, 1.02f, 10, FinalePalette.Steel);
            k.Box(new Vector3(0f, 0.05f, 0.1f), new Vector3(1.0f, 0.15f, 0.25f), FinalePalette.Hazard);
        }

        // ---------- Pelvis and the desert's fuel tank ----------

        private static void Pelvis(MeshKit k)
        {
            k.Smooth = true;
            k.Ball(new Vector3(0f, 0.1f, 0f), new Vector3(2.1f, 0.95f, 1.35f), Quaternion.identity, FinalePalette.Frame, 2);
            k.Smooth = false;
            k.Box(new Vector3(0f, 0.55f, 0f), new Vector3(3.6f, 0.35f, 2.4f), FinalePalette.Trim); // belt
            k.Box(new Vector3(0f, -0.25f, 1.2f), new Vector3(1.4f, 1.1f, 0.35f), Quaternion.Euler(10f, 0f, 0f), FinalePalette.Armour); // codpiece plate
            for (int i = 0; i < 6; i++) k.Ball(new Vector3(-1.5f + i * 0.6f, 0.55f, 1.22f), 0.08f, FinalePalette.Steel, 0); // rivets
        }

        private static void FuelTank(MeshKit k)
        {
            k.Smooth = true;
            k.Ball(Vector3.zero, new Vector3(0.45f, 0.95f, 0.45f), Quaternion.identity, FinalePalette.Rust, 2);
            k.Smooth = false;
            k.Prism(new Vector3(0f, -0.35f, 0f), Quaternion.identity, 0.47f, 0.47f, 0.15f, 12, FinalePalette.Hazard);
            k.Prism(new Vector3(0f, 0.25f, 0f), Quaternion.identity, 0.47f, 0.47f, 0.15f, 12, FinalePalette.Hazard);
            k.Prism(new Vector3(0f, 0.9f, 0f), Quaternion.identity, 0.12f, 0.1f, 0.25f, 8, FinalePalette.Steel); // cap
        }

        // ---------- Torso, engine core, reactor ----------

        // A blocky, V-shaped chest, wide at the shoulders and narrow at the waist (the toy-robot look), on a round waist.
        private static void Torso(MeshKit k)
        {
            k.Smooth = true;
            k.Ball(new Vector3(0f, 0.65f, 0f), new Vector3(1.35f, 0.95f, 1.05f), Quaternion.identity, FinalePalette.Frame, 2); // waist
            k.Smooth = false;
            for (int i = 0; i < 3; i++) k.Box(new Vector3(0f, 0.45f + i * 0.4f, 0.92f), new Vector3(1.5f - i * 0.1f, 0.32f, 0.4f), FinalePalette.DarkFrame); // abs
            k.Box(new Vector3(0f, 1.75f, 0.05f), new Vector3(3.3f, 1.0f, 2.3f), FinalePalette.ArmourShade);  // lower chest
            k.Box(new Vector3(0f, 3.1f, 0f), new Vector3(4.8f, 2.6f, 2.7f), FinalePalette.Armour);          // chest
            k.Box(new Vector3(-1.75f, 3.1f, 1.18f), new Vector3(1.6f, 2.2f, 0.5f), Quaternion.Euler(0f, 20f, 0f), FinalePalette.ArmourShade); // bevels
            k.Box(new Vector3(1.75f, 3.1f, 1.18f), new Vector3(1.6f, 2.2f, 0.5f), Quaternion.Euler(0f, -20f, 0f), FinalePalette.ArmourShade);
            k.Box(new Vector3(-1.45f, 4.05f, 1.42f), new Vector3(1.5f, 0.3f, 0.3f), Quaternion.Euler(0f, 18f, -12f), FinalePalette.Trim);
            k.Box(new Vector3(1.45f, 4.05f, 1.42f), new Vector3(1.5f, 0.3f, 0.3f), Quaternion.Euler(0f, -18f, 12f), FinalePalette.Trim);
            k.Box(new Vector3(0f, 4.35f, 0f), new Vector3(6.0f, 0.75f, 2.2f), FinalePalette.Frame);         // shoulder bridge
            k.Box(new Vector3(0f, 4.85f, 0.1f), new Vector3(2.2f, 0.55f, 1.9f), FinalePalette.Trim);         // collar
            k.Box(new Vector3(0f, 3.0f, -1.5f), new Vector3(4.0f, 2.4f, 0.5f), FinalePalette.Frame);         // back plate
            // The teal panel from Cavendish's suit, a scrap patch and rivets.
            k.Box(new Vector3(-1.75f, 2.55f, 1.46f), new Vector3(0.75f, 0.5f, 0.1f), Quaternion.Euler(0f, 20f, 0f), FinalePalette.Teal, 1.1f);
            k.Box(new Vector3(1.7f, 2.4f, 1.47f), new Vector3(0.55f, 0.4f, 0.1f), Quaternion.Euler(0f, -20f, 0f), FinalePalette.Rust);
            for (int i = 0; i < 6; i++) k.Ball(new Vector3(1.45f + (i % 3) * 0.17f, 2.25f + (i / 3) * 0.32f, 1.56f), 0.05f, FinalePalette.Steel, 0);
            // The reactor's housing ring.
            k.Prism(new Vector3(0f, 3.0f, 1.2f), Quaternion.Euler(90f, 0f, 0f), 0.98f, 0.88f, 0.45f, 12, FinalePalette.Frame);
            k.Prism(new Vector3(0f, 3.0f, 1.6f), Quaternion.Euler(90f, 0f, 0f), 0.9f, 0.94f, 0.1f, 12, FinalePalette.Hazard);
            // Hazard stripes along the bottom of the chest.
            for (int i = 0; i < 9; i++)
                k.Box(new Vector3(-1.6f + i * 0.4f, 1.95f, 1.22f), new Vector3(0.2f, 0.28f, 0.1f), Quaternion.Euler(0f, 0f, 30f), i % 2 == 0 ? FinalePalette.Hazard : FinalePalette.DarkFrame);
        }

        private static void Helmet(MeshKit k)
        {
            Color fur = new Color(0.46f, 0.31f, 0.2f), furInner = new Color(0.3f, 0.19f, 0.13f);
            k.Box(new Vector3(-1.2f, 0f, -0.1f), new Vector3(0.36f, 1.6f, 2.0f), FinalePalette.Armour);   // cheeks
            k.Box(new Vector3(1.2f, 0f, -0.1f), new Vector3(0.36f, 1.6f, 2.0f), FinalePalette.Armour);
            k.Box(new Vector3(0f, 0.95f, -0.1f), new Vector3(2.75f, 0.36f, 2.2f), FinalePalette.Armour);   // crown
            k.Box(new Vector3(0f, 0f, -1.05f), new Vector3(2.4f, 1.6f, 0.4f), FinalePalette.Frame);         // back
            k.Box(new Vector3(0f, -0.88f, 0.55f), new Vector3(1.7f, 0.34f, 1.1f), FinalePalette.Frame);    // chin
            k.Box(new Vector3(0f, 0.78f, 1.0f), new Vector3(2.35f, 0.26f, 0.42f), FinalePalette.Trim);     // brow
            k.Box(new Vector3(0f, 1.45f, -0.15f), new Vector3(0.24f, 0.95f, 1.8f), Quaternion.Euler(-10f, 0f, 0f), FinalePalette.Trim); // crest
            k.Box(new Vector3(-1.4f, -0.2f, 0.6f), new Vector3(0.12f, 0.5f, 0.5f), FinalePalette.Teal, 1.1f);  // cheek lights
            k.Box(new Vector3(1.4f, -0.2f, 0.6f), new Vector3(0.12f, 0.5f, 0.5f), FinalePalette.Teal, 1.1f);
            k.Smooth = true; // little round capybara ears, a nod to the pilot
            k.Ball(new Vector3(-1.05f, 1.25f, -0.45f), new Vector3(0.34f, 0.4f, 0.22f), Quaternion.Euler(0f, 0f, 20f), fur, 2);
            k.Ball(new Vector3(1.05f, 1.25f, -0.45f), new Vector3(0.34f, 0.4f, 0.22f), Quaternion.Euler(0f, 0f, -20f), fur, 2);
            k.Ball(new Vector3(-1.05f, 1.25f, -0.32f), new Vector3(0.2f, 0.24f, 0.12f), Quaternion.Euler(0f, 0f, 20f), furInner, 1);
            k.Ball(new Vector3(1.05f, 1.25f, -0.32f), new Vector3(0.2f, 0.24f, 0.12f), Quaternion.Euler(0f, 0f, -20f), furInner, 1);
            k.Smooth = false;
            k.Box(new Vector3(0f, -0.55f, 0.6f), new Vector3(1.4f, 0.3f, 0.4f), FinalePalette.DarkFrame);  // dashboard
            k.Box(new Vector3(-0.35f, -0.36f, 0.75f), new Vector3(0.3f, 0.16f, 0.05f), FinalePalette.Teal, 1.6f);
            k.Box(new Vector3(0.35f, -0.36f, 0.75f), new Vector3(0.3f, 0.16f, 0.05f), FinalePalette.Reactor, 1.6f);
        }

        private static void ReactorCage(MeshKit k)
        {
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                Vector3 rim = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.92f;
                k.Rod(rim + new Vector3(0f, 0f, -0.2f), rim * 0.55f + new Vector3(0f, 0f, 0.55f), 0.06f, 5, FinalePalette.Steel);
            }
        }

        private static void EngineCore(MeshKit k)
        {
            // The desert's Engine Core, strapped on as a backpack: a turbine housing with glowing intake rings.
            Quaternion along = Quaternion.Euler(90f, 0f, 0f);
            k.Prism(new Vector3(0f, 0f, -0.9f), along, 1.25f, 1.05f, 2.2f, 14, FinalePalette.Frame);
            k.Prism(new Vector3(0f, 0f, -1.05f), along, 1.3f, 1.3f, 0.18f, 14, FinalePalette.EngineGlow, 1.5f);
            k.Prism(new Vector3(0f, 0f, 0.35f), along, 1.12f, 1.12f, 0.16f, 14, FinalePalette.EngineGlow, 1.5f);
            k.Prism(new Vector3(0f, 0f, -1.3f), along, 1.4f, 1.4f, 0.22f, 14, FinalePalette.Steel);
            k.Box(new Vector3(0f, 1.2f, -0.2f), new Vector3(0.5f, 0.35f, 1.7f), FinalePalette.Trim);
            for (int i = -1; i <= 1; i += 2) k.Tube(new[] { new Vector3(0.7f * i, -1f, -0.2f), new Vector3(1.0f * i, -1.4f, 0.2f), new Vector3(1.1f * i, -1.6f, -0.6f) }, 0.12f, 6, FinalePalette.Steel);
        }

        private static void Turbine(MeshKit k)
        {
            for (int i = 0; i < 8; i++)
            {
                Quaternion blade = Quaternion.Euler(0f, 0f, i * 45f);
                k.Box(blade * new Vector3(0f, 0.55f, 0f), new Vector3(0.22f, 0.95f, 0.05f), blade * Quaternion.Euler(0f, 25f, 0f), FinalePalette.Steel);
            }
            k.Prism(Vector3.zero, Quaternion.Euler(-90f, 0f, 0f), 0.25f, 0.15f, 0.25f, 8, FinalePalette.EngineGlow, 1.4f);
        }

        private static void Nozzle(MeshKit k)
        {
            // The desert's rocket thruster: a bell nozzle, glowing inside.
            k.Lathe(Vector3.zero, Quaternion.identity, new[] { new Vector2(0.35f, 0f), new Vector2(0.42f, 0.4f), new Vector2(0.62f, 0.9f), new Vector2(0.78f, 1.3f) }, 12, FinalePalette.Frame);
            k.Prism(new Vector3(0f, 0.05f, 0f), Quaternion.identity, 0.3f, 0.6f, 1.2f, 12, FinalePalette.EngineGlow, 1.6f);
            k.Prism(new Vector3(0f, -0.35f, 0f), Quaternion.identity, 0.45f, 0.4f, 0.4f, 10, FinalePalette.Rust);
        }

        // ---------- Head ----------

        private static void NavDish(MeshKit k)
        {
            // The Fog Forest's Navigation Computer: a little radar dish on a mast.
            k.Rod(new Vector3(0f, -0.9f, 0f), Vector3.zero, 0.06f, 6, FinalePalette.Steel);
            k.Lathe(Vector3.zero, Quaternion.Euler(-70f, 0f, 0f), new[] { new Vector2(0.05f, 0f), new Vector2(0.35f, 0.05f), new Vector2(0.6f, 0.2f) }, 12, FinalePalette.Armour);
            k.Rod(Vector3.zero, Quaternion.Euler(-70f, 0f, 0f) * new Vector3(0f, 0.45f, 0f), 0.03f, 4, FinalePalette.Steel);
            k.Ball(Quaternion.Euler(-70f, 0f, 0f) * new Vector3(0f, 0.48f, 0f), 0.07f, FinalePalette.Teal, 0, 2.5f);
        }

        // ---------- Arms ----------

        private static Transform Arm(Transform chest, int side, out Transform elbow)
        {
            Transform shoulder = MudArt.Pivot(chest, side < 0 ? "Shoulder L" : "Shoulder R", new Vector3(3.35f * side, 4.15f, 0f));
            MudArt.Part(shoulder, "Upper Arm", Vector3.zero, Quaternion.identity, "mech_upperarm", UpperArm);
            // The pauldron mirrors on the left, so it gets its own mesh per side.
            MudArt.Part(shoulder, "Wing Panel Pauldron", Vector3.zero, Quaternion.identity, side < 0 ? "mech_pauldron_l" : "mech_pauldron_r", k => Pauldron(k, side));
            elbow = MudArt.Pivot(shoulder, "Elbow", new Vector3(0f, -2.9f, 0f));
            return shoulder;
        }

        private static void UpperArm(MeshKit k)
        {
            k.Smooth = true;
            k.Ball(Vector3.zero, 1.05f, FinalePalette.Frame, 2);
            k.Ball(new Vector3(0f, -2.9f, 0f), 0.64f, FinalePalette.Frame, 2);
            k.Smooth = false;
            k.Box(new Vector3(0f, -1.45f, 0f), new Vector3(1.35f, 2.1f, 1.35f), FinalePalette.Armour);
            k.Box(new Vector3(0f, -1.45f, -0.72f), new Vector3(0.9f, 1.6f, 0.12f), FinalePalette.ArmourShade);
            k.Box(new Vector3(0f, -1.45f, 0.76f), new Vector3(0.5f, 1.3f, 0.12f), FinalePalette.Trim);
        }

        private static void Pauldron(MeshKit k, int side)
        {
            // Old Buzzard's stolen wing panels, riveted over the shoulder as layered fins.
            for (int i = 0; i < 3; i++)
            {
                Quaternion tilt = Quaternion.Euler(0f, 0f, side * (18f + i * 14f));
                Vector3 at = new Vector3(side * (0.35f + i * 0.35f), 1.0f - i * 0.25f, 0f);
                k.Box(at, new Vector3(1.9f - i * 0.3f, 0.2f, 2.1f - i * 0.25f), tilt, i == 1 ? FinalePalette.Trim : FinalePalette.Armour);
                k.Box(at + tilt * new Vector3(0f, 0.12f, 0.7f), new Vector3(1.5f - i * 0.3f, 0.06f, 0.18f), tilt, FinalePalette.Hazard);
            }
            // Flight Control Module jet pod behind the shoulder.
            k.Prism(new Vector3(side * 0.7f, 0.5f, -1.15f), Quaternion.Euler(-120f, 0f, 0f), 0.38f, 0.3f, 0.9f, 10, FinalePalette.Frame);
            k.Prism(new Vector3(side * 0.7f, 0.5f, -1.15f), Quaternion.Euler(-120f, 0f, 0f), 0.24f, 0.24f, 0.95f, 10, FinalePalette.Teal, 1.6f);
        }

        private static void CoolingCannon(MeshKit k)
        {
            // The Chief Toad's cooling system turned into a freezing cannon: a pump on the forearm, segmented
            // coolant tubes glowing blue, and a wide barrel.
            k.Smooth = true;
            k.Ball(new Vector3(0f, -0.2f, 0f), 0.62f, FinalePalette.Frame, 2);
            k.Smooth = false;
            k.Prism(new Vector3(0f, -3.6f, 0f), Quaternion.identity, 0.64f, 0.8f, 3.1f, 12, FinalePalette.Frame);
            k.Prism(new Vector3(0f, -3.8f, 0f), Quaternion.identity, 0.74f, 0.74f, 0.3f, 12, FinalePalette.Steel);
            k.Prism(new Vector3(0f, -3.72f, 0f), Quaternion.identity, 0.46f, 0.46f, 0.1f, 12, FinalePalette.Coolant, 1.6f);
            for (int i = 0; i < 3; i++)
                k.Prism(new Vector3(0f, -2.9f + i * 0.8f, 0f), Quaternion.identity, 0.86f, 0.86f, 0.18f, 12, FinalePalette.Coolant, 1.3f);
            k.Box(new Vector3(0f, -1.2f, -0.75f), new Vector3(0.9f, 1.1f, 0.6f), FinalePalette.Steel); // pump
            k.Box(new Vector3(0f, -1.2f, -1.07f), new Vector3(0.6f, 0.7f, 0.08f), FinalePalette.Coolant, 1.3f);
            for (int s = -1; s <= 1; s += 2)
                k.Tube(new[] { new Vector3(0.4f * s, -0.8f, -0.85f), new Vector3(0.85f * s, -1.4f, -0.4f), new Vector3(0.85f * s, -2.5f, 0f), new Vector3(0.75f * s, -3.0f, 0.1f) },
                    0.11f, 6, FinalePalette.Coolant, 1.2f);
        }

        private static void Fist(MeshKit k)
        {
            // A scrap-built piston fist.
            k.Smooth = true;
            k.Ball(new Vector3(0f, -0.2f, 0f), 0.6f, FinalePalette.Frame, 2);
            k.Smooth = false;
            k.Prism(new Vector3(0f, -2.0f, 0f), Quaternion.identity, 0.5f, 0.6f, 1.8f, 10, FinalePalette.Armour);
            k.Rod(new Vector3(0.45f, -0.4f, 0.3f), new Vector3(0.45f, -1.9f, 0.3f), 0.1f, 6, FinalePalette.Steel);
            k.Box(new Vector3(0f, -2.7f, 0.05f), new Vector3(1.45f, 1.2f, 1.25f), FinalePalette.Trim);
            for (int i = 0; i < 4; i++) k.Box(new Vector3(-0.54f + i * 0.36f, -3.35f, 0.25f), new Vector3(0.32f, 0.5f, 0.8f), FinalePalette.Frame); // knuckles
            k.Box(new Vector3(0.82f, -2.6f, 0.2f), new Vector3(0.3f, 0.8f, 0.5f), Quaternion.Euler(0f, 0f, -20f), FinalePalette.Frame); // thumb
            k.Box(new Vector3(-0.2f, -2.4f, 0.69f), new Vector3(0.5f, 0.35f, 0.05f), FinalePalette.Rust);
        }

        // ---------- The shield ----------

        /// <summary>The faceted energy dome raised while the boss is immune.</summary>
        public static Transform Shield(Transform parent, float radius)
        {
            Transform dome = MudArt.Part(parent, "Shield", new Vector3(0f, radius * 0.55f, 0f), Quaternion.identity, "mech_shield",
                k => k.Ball(Vector3.zero, new Vector3(1f, 1f, 1f), Quaternion.identity, FinalePalette.Shield, 2), false);
            dome.localScale = new Vector3(radius, radius * 1.15f, radius);
            dome.GetComponent<MeshRenderer>().sharedMaterial = MudArt.Clear(FinalePalette.Shield, 1.4f);
            return dome;
        }
    }
}
