using UnityEngine;

namespace SignalLost.Mudlands
{
    /// <summary>
    /// The Drunk Driving car, built from low-poly parts. Since the Mudlands became the second stage (11-22) the car comes
    /// along to every island after it, and it is dressed for the island it is called on (<see cref="StyleFor"/>):
    /// <list type="bullet">
    /// <item><b>Muscle car</b> (the Mudlands, and anywhere without a style of its own): a faded teal muscle car with rust
    /// patches, a grey primer passenger door, a cream hood stripe, chrome-ish bumpers gone brown, round headlights, twin
    /// exhausts and fuzzy dice.</item>
    /// <item><b>Dune Mule</b> (the Desert Dunes; the user's reference was an open-top off-roader crawling over red rock): a
    /// white, boxy open-top four-by-four with no doors, a flat bonnet, round headlights either side of a seven-slot
    /// grille, black fender flares, big lugged all-terrain tyres on black wheels, a black roll bar, a fold-flat
    /// windscreen frame, a spare on the tailgate and a jerry can. An original design: no maker's names or badges.</item>
    /// <item><b>Geode Crawler</b> (the Crystal Caves): a stubby mining buggy in hazard yellow over slate armour, an open
    /// roll cage with a lamp bar, glowing cave crystals growing off the bonnet and cage, and an ore bin of crystals.</item>
    /// <item><b>Scrap Runner</b> (the Wasteland): a rat rod welded from the crashed ship, rust and soot, hull panels with
    /// the orange stripe for armour, a blower and stacks through the bonnet, a spiked ram, a grille for a windscreen and
    /// a roof rack.</item>
    /// </list>
    /// Every style uses the same body pivot, seat, steering wheel, headlight and wheel spots, so the driving, the
    /// colliders and every rider and anchor point are unchanged (<see cref="DrunkCar"/>). Body parts are separate pieces
    /// so they can fly off when it blows up.
    /// </summary>
    public static class CarModel
    {
        public enum Style { MuscleCar, DuneMule, GeodeCrawler, ScrapRunner }

        /// <summary>Testing (the car pictures): forces a style whatever the scene.</summary>
        public static Style? Override { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Override = null;

        /// <summary>The style for the open scene (or <see cref="Override"/>).</summary>
        public static Style Current => Override ?? StyleFor(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);

        /// <summary>The style an island's car is dressed in, by its scene's name. Test scenes follow their island's look.</summary>
        public static Style StyleFor(string scene)
        {
            switch (scene)
            {
                case "DesertDunes": return Style.DuneMule;
                case "CrystalCaves": return Style.GeodeCrawler;
                case "Wasteland": return Style.ScrapRunner;
            }
            if (SignalLost.World.StageTheme.Current == SignalLost.World.IslandLook.Desert) return Style.DuneMule;
            return Style.MuscleCar;
        }

        // The body pivot is just above the axles; the parts every style shares sit here (DrunkCar puts the seat there).
        private static readonly Vector3 SteeringAt = new Vector3(-0.38f, 0.82f, 0.25f);
        private static readonly Vector3 HeadlightAt = new Vector3(0f, 0.4f, 2.2f);

        /// <summary>The body, origin at the body pivot (just above the axles), facing +Z.</summary>
        public static void BuildBody(Transform pivot) => BuildBody(pivot, Current);

        public static void BuildBody(Transform pivot, Style style)
        {
            switch (style)
            {
                case Style.DuneMule: DuneMule.Body(pivot); break;
                case Style.GeodeCrawler: GeodeCrawler.Body(pivot); break;
                case Style.ScrapRunner: ScrapRunner.Body(pivot); break;
                default: Muscle.Body(pivot); break;
            }
            SteeringWheel(pivot, style);
            Headlights(pivot, style);
        }

        /// <summary>A wheel (tyre, hub and spokes) spinning about its local x, centred on its wheel spot.</summary>
        public static Transform Wheel(Transform parent, string name, Vector3 position, bool left) => Wheel(parent, name, position, left, Current);

        public static Transform Wheel(Transform parent, string name, Vector3 position, bool left, Style style)
        {
            switch (style)
            {
                case Style.DuneMule: return DuneMule.Wheel(parent, name, position);
                case Style.GeodeCrawler: return GeodeCrawler.Wheel(parent, name, position);
                case Style.ScrapRunner: return ScrapRunner.Wheel(parent, name, position);
                default: return Muscle.Wheel(parent, name, position);
            }
        }

        // ---------- Shared ----------

        private static void SteeringWheel(Transform pivot, Style style)
        {
            Color rim = style == Style.DuneMule ? MudPalette.Black : style == Style.GeodeCrawler ? new Color(0.95f, 0.72f, 0.18f) : MudPalette.Black;
            MudArt.Part(pivot, "Steering Wheel", SteeringAt, Quaternion.Euler(-60f, 0f, 0f), "car_wheel_steer_" + style, k =>
            {
                k.Prism(Vector3.zero, Quaternion.identity, 0.2f, 0.2f, 0.04f, 10, rim);
                k.Rod(new Vector3(0f, 0.02f, 0f), new Vector3(0f, -0.3f, 0f), 0.03f, 5, MudPalette.DarkMetal);
            });
        }

        private static void Headlights(Transform pivot, Style style)
        {
            var light = new GameObject("Headlights").AddComponent<Light>();
            light.transform.SetParent(pivot, false);
            light.transform.localPosition = HeadlightAt;
            light.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);
            light.type = LightType.Spot;
            light.spotAngle = style == Style.GeodeCrawler ? 85f : 70f;
            light.range = style == Style.GeodeCrawler ? 32f : 26f; // the caves are dark
            light.intensity = style == Style.GeodeCrawler ? 8f : 6f;
            light.color = style == Style.GeodeCrawler ? new Color(0.8f, 0.97f, 1f)
                : style == Style.ScrapRunner ? new Color(1f, 0.82f, 0.55f) : new Color(1f, 0.93f, 0.75f);
            light.shadows = LightShadows.None;
        }

        // A tyre round local x: a ring of tread lugs on a prism, so it reads as all-terrain from the chase camera.
        private static void Tyre(MeshKit k, float radius, float width, int lugs, Color rubber, float lugDepth)
        {
            Quaternion axis = Quaternion.Euler(0f, 0f, 90f);
            k.Prism(new Vector3(width * 0.5f, 0f, 0f), axis, radius - lugDepth, radius - lugDepth, width, 12, rubber);
            for (int i = 0; i < lugs; i++)
            {
                float a = i * 360f / lugs;
                Quaternion turn = Quaternion.Euler(a, 0f, 0f);
                float stagger = (i % 2 == 0 ? 0.12f : -0.12f) * width;
                k.Box(turn * new Vector3(stagger, radius - lugDepth * 0.5f, 0f), new Vector3(width * 0.72f, lugDepth, 0.11f), turn, rubber);
            }
            // Sidewall blocks on the outer edge (the shoulder lugs).
            for (int i = 0; i < lugs; i++)
            {
                float a = (i + 0.5f) * 360f / lugs;
                Quaternion turn = Quaternion.Euler(a, 0f, 0f);
                foreach (float side in new[] { -1f, 1f })
                    k.Box(turn * new Vector3(side * width * 0.47f, radius - lugDepth * 1.3f, 0f), new Vector3(0.04f, lugDepth * 1.2f, 0.09f), turn, rubber);
            }
        }

        // ---------- The Mudlands: the beaten-up muscle car (unchanged) ----------

        private static class Muscle
        {
            private static readonly Color Paint = new Color(0.27f, 0.55f, 0.55f);
            private static readonly Color PaintDark = new Color(0.19f, 0.4f, 0.41f);
            private static readonly Color Primer = new Color(0.55f, 0.56f, 0.55f);
            private static readonly Color Chrome = new Color(0.62f, 0.58f, 0.52f);
            private static readonly Color Glass = new Color(0.2f, 0.27f, 0.31f);
            private static readonly Color Seat = new Color(0.45f, 0.17f, 0.12f);

            public static void Body(Transform pivot)
            {
                MudArt.Part(pivot, "Chassis", Vector3.zero, Quaternion.identity, "car_chassis", k =>
                {
                    k.Box(new Vector3(0f, 0.2f, 0f), new Vector3(1.85f, 0.5f, 4.15f), Paint);
                    k.Box(new Vector3(0f, -0.08f, 0f), new Vector3(1.7f, 0.12f, 3.9f), MudPalette.DarkMetal);
                    // Wheel arches.
                    foreach (float z in new[] { 1.32f, -1.28f })
                        foreach (float x in new[] { -0.93f, 0.93f })
                            k.Box(new Vector3(x, 0.32f, z), new Vector3(0.06f, 0.18f, 1.05f), PaintDark);
                    // Rust patches and a racing stripe.
                    k.Box(new Vector3(-0.93f, 0.12f, 0.6f), new Vector3(0.02f, 0.18f, 0.5f), MudPalette.Rust);
                    k.Box(new Vector3(0.93f, 0.08f, -1.7f), new Vector3(0.02f, 0.16f, 0.35f), MudPalette.Rust);
                    k.Box(new Vector3(0.93f, 0.2f, -0.35f), new Vector3(0.02f, 0.42f, 1.1f), Primer); // the mismatched door
                    // Seats.
                    k.Box(new Vector3(-0.38f, 0.5f, -0.45f), new Vector3(0.55f, 0.15f, 0.55f), Seat);
                    k.Box(new Vector3(-0.38f, 0.8f, -0.72f), new Vector3(0.55f, 0.6f, 0.12f), Seat);
                    k.Box(new Vector3(0.38f, 0.5f, -0.45f), new Vector3(0.55f, 0.15f, 0.55f), Seat);
                    k.Box(new Vector3(0.38f, 0.8f, -0.72f), new Vector3(0.55f, 0.6f, 0.12f), Seat);
                });
                MudArt.Part(pivot, "Hood", new Vector3(0f, 0.48f, 1.2f), Quaternion.Euler(-3f, 0f, 0f), "car_hood", k =>
                {
                    k.Box(Vector3.zero, new Vector3(1.78f, 0.1f, 1.75f), Paint);
                    k.Box(new Vector3(0f, 0.06f, 0f), new Vector3(0.36f, 0.04f, 1.74f), MudPalette.Cream);
                    k.Box(new Vector3(0f, 0.12f, -0.1f), new Vector3(0.5f, 0.12f, 0.6f), PaintDark); // the scoop
                    k.Box(new Vector3(0f, 0.14f, 0.2f), new Vector3(0.4f, 0.08f, 0.02f), MudPalette.Black);
                });
                MudArt.Part(pivot, "Trunk", new Vector3(0f, 0.48f, -1.6f), Quaternion.identity, "car_trunk", k =>
                {
                    k.Box(Vector3.zero, new Vector3(1.78f, 0.1f, 1f), Paint);
                    k.Box(new Vector3(0f, 0.06f, 0f), new Vector3(0.36f, 0.04f, 0.98f), MudPalette.Cream);
                    k.Box(new Vector3(0f, 0.12f, -0.45f), new Vector3(1.6f, 0.1f, 0.06f), PaintDark); // a little spoiler
                });
                MudArt.Part(pivot, "Roof", new Vector3(0f, 1.0f, -0.45f), Quaternion.identity, "car_roof", k =>
                {
                    k.Box(Vector3.zero, new Vector3(1.55f, 0.08f, 1.45f), Paint);
                    foreach (float x in new[] { -0.74f, 0.74f })
                    {
                        k.Plank(new Vector3(x, -0.02f, 0.7f), new Vector3(x, -0.5f, 1.05f), 0.07f, 0.07f, PaintDark);
                        k.Plank(new Vector3(x, -0.02f, -0.7f), new Vector3(x, -0.5f, -0.95f), 0.07f, 0.07f, PaintDark);
                    }
                    // Glass: windscreen, back window, side windows.
                    k.Box(new Vector3(0f, -0.26f, 0.88f), new Vector3(1.42f, 0.5f, 0.03f), Quaternion.Euler(-36f, 0f, 0f), Glass);
                    k.Box(new Vector3(0f, -0.26f, -0.83f), new Vector3(1.42f, 0.48f, 0.03f), Quaternion.Euler(30f, 0f, 0f), Glass);
                    // Side windows, so the cabin reads as glass from the chase camera rather than an open box.
                    foreach (float x in new[] { -0.745f, 0.745f })
                        k.Box(new Vector3(x, -0.27f, 0.05f), new Vector3(0.03f, 0.5f, 1.5f), Glass);
                    // Fuzzy dice on the mirror.
                    k.Box(new Vector3(0f, -0.08f, 0.62f), new Vector3(0.1f, 0.04f, 0.04f), MudPalette.Black);
                    k.Box(new Vector3(-0.05f, -0.22f, 0.62f), new Vector3(0.08f, 0.08f, 0.08f), new Color(1f, 0.45f, 0.7f));
                    k.Box(new Vector3(0.05f, -0.26f, 0.6f), new Vector3(0.08f, 0.08f, 0.08f), new Color(1f, 0.45f, 0.7f));
                });
                MudArt.Part(pivot, "Front Bumper", new Vector3(0f, 0.06f, 2.12f), Quaternion.identity, "car_front", k =>
                {
                    k.Box(Vector3.zero, new Vector3(1.95f, 0.2f, 0.18f), Chrome);
                    k.Box(new Vector3(0f, 0.26f, -0.03f), new Vector3(1.0f, 0.3f, 0.06f), MudPalette.Black); // grille
                    for (int i = -3; i <= 3; i++) k.Box(new Vector3(i * 0.13f, 0.26f, 0f), new Vector3(0.03f, 0.28f, 0.04f), Chrome);
                    foreach (float x in new[] { -0.7f, 0.7f })
                    {
                        k.Prism(new Vector3(x, 0.28f, -0.05f), Quaternion.Euler(90f, 0f, 0f), 0.14f, 0.14f, 0.08f, 8, Chrome);
                        k.Ball(new Vector3(x, 0.28f, 0.04f), new Vector3(0.12f, 0.12f, 0.06f), Quaternion.identity, new Color(1f, 0.93f, 0.7f), 1, 3f);
                    }
                });
                MudArt.Part(pivot, "Rear Bumper", new Vector3(0f, 0.06f, -2.1f), Quaternion.identity, "car_rear", k =>
                {
                    k.Box(Vector3.zero, new Vector3(1.95f, 0.2f, 0.18f), Chrome);
                    foreach (float x in new[] { -0.72f, 0.72f })
                        k.Box(new Vector3(x, 0.28f, 0.04f), new Vector3(0.32f, 0.12f, 0.05f), new Color(1f, 0.15f, 0.1f), 2.5f);
                    foreach (float x in new[] { -0.45f, -0.3f })
                        k.Prism(new Vector3(x, -0.12f, 0.05f), Quaternion.Euler(-90f, 0f, 0f), 0.06f, 0.07f, 0.35f, 7, MudPalette.DarkMetal);
                    // A licence plate: "BOOZE 1".
                    k.Box(new Vector3(0f, 0.25f, -0.03f), new Vector3(0.45f, 0.2f, 0.03f), MudPalette.Cream);
                });
            }

            public static Transform Wheel(Transform parent, string name, Vector3 position)
            {
                return MudArt.Part(parent, name, position, Quaternion.identity, "car_wheel", k =>
                {
                    Quaternion axis = Quaternion.Euler(0f, 0f, 90f);
                    k.Prism(new Vector3(0.14f, 0f, 0f), axis, 0.38f, 0.38f, 0.28f, 10, MudPalette.Rubber);
                    k.Prism(new Vector3(0.15f, 0f, 0f), axis, 0.2f, 0.18f, 0.3f, 8, Chrome);
                    for (int i = 0; i < 4; i++)
                    {
                        Quaternion spin = Quaternion.Euler(i * 45f, 0f, 0f);
                        k.Box(spin * Vector3.zero, new Vector3(0.32f, 0.04f, 0.34f), spin, MudPalette.DarkMetal);
                    }
                });
            }
        }

        // ---------- The Desert Dunes: the Dune Mule ----------

        private static class DuneMule
        {
            // Off-white paint, a shade for the panel insides, black trim.
            private static readonly Color Paint = new Color(0.94f, 0.94f, 0.91f);
            private static readonly Color PaintShade = new Color(0.78f, 0.78f, 0.76f);
            private static readonly Color Trim = new Color(0.09f, 0.09f, 0.1f);
            private static readonly Color Satin = new Color(0.16f, 0.16f, 0.17f);
            private static readonly Color Tan = new Color(0.25f, 0.24f, 0.23f);       // dark seats, like the reference's
            private static readonly Color Tub = new Color(0.14f, 0.14f, 0.15f);       // the floor inside
            private static readonly Color Glass = new Color(0.32f, 0.42f, 0.46f);
            private static readonly Color Lens = new Color(1f, 0.95f, 0.8f);
            private static readonly Color Amber = new Color(1f, 0.6f, 0.15f);
            private static readonly Color Olive = new Color(0.35f, 0.38f, 0.22f);
            private static readonly Color Flag = new Color(0.95f, 0.45f, 0.12f);

            // Heights in the body pivot's frame: the sills, the tub and bonnet top (the belt line), the floor.
            private const float Bottom = -0.04f, Belt = 0.66f, Floor = 0.36f, DoorSill = 0.3f;
            private const float Front = 2.0f, Back = -1.96f, Cowl = 0.78f, DoorBack = -0.5f;
            private const float Half = 0.8f; // half the tub's width; the flares stick out past it

            public static void Body(Transform pivot)
            {
                // The tub: full-height sides behind the door gaps and at the cowl, only a sill where the doors would be.
                MudArt.Part(pivot, "Chassis", Vector3.zero, Quaternion.identity, "car_mule_tub", k =>
                {
                    k.Box(new Vector3(0f, -0.13f, 0.02f), new Vector3(1.1f, 0.14f, 3.9f), Satin); // the frame rails
                    k.Box(new Vector3(0f, Floor - 0.03f, (Back + Cowl) * 0.5f), new Vector3(Half * 2f - 0.08f, 0.06f, Cowl - Back), Tub);
                    foreach (float side in new[] { -1f, 1f })
                    {
                        float x = side * (Half - 0.03f);
                        // Behind the door gap, to the tailgate.
                        k.Box(new Vector3(x, (Bottom + Belt) * 0.5f, (Back + DoorBack) * 0.5f), new Vector3(0.06f, Belt - Bottom, DoorBack - Back), Paint);
                        // The door gap: a sill, low enough to climb over.
                        k.Box(new Vector3(x, (Bottom + DoorSill) * 0.5f, (DoorBack + Cowl) * 0.5f), new Vector3(0.06f, DoorSill - Bottom, Cowl - DoorBack), Paint);
                        // The cowl post in front of the gap, a hand's width.
                        k.Box(new Vector3(x, (Bottom + Belt) * 0.5f, Cowl - 0.08f), new Vector3(0.06f, Belt - Bottom, 0.16f), Paint);
                        // Exposed hinges where the doors came off.
                        foreach (float y in new[] { 0.16f, 0.48f })
                            k.Box(new Vector3(side * (Half + 0.005f), y, Cowl - 0.12f), new Vector3(0.03f, 0.08f, 0.06f), Trim);
                        // Rock rails under the sills.
                        k.Box(new Vector3(side * (Half - 0.02f), Bottom - 0.05f, (DoorBack + Cowl) * 0.5f + 0.05f), new Vector3(0.14f, 0.09f, 1.55f), Trim);
                        // The belt-line cap, so the top edge reads as a rolled lip.
                        k.Box(new Vector3(x, Belt + 0.015f, (Back + DoorBack) * 0.5f), new Vector3(0.1f, 0.03f, DoorBack - Back), PaintShade);
                    }
                    // Tailgate, with its hinges and handle.
                    k.Box(new Vector3(0f, (Bottom + Belt) * 0.5f, Back + 0.03f), new Vector3(Half * 2f, Belt - Bottom, 0.06f), Paint);
                    k.Box(new Vector3(0f, 0.42f, Back - 0.005f), new Vector3(0.22f, 0.05f, 0.03f), Trim);
                    foreach (float x in new[] { -0.55f, 0.55f })
                        k.Box(new Vector3(x, 0.1f, Back - 0.005f), new Vector3(0.12f, 0.06f, 0.03f), Trim);
                    // Tail lights: tall, narrow, on the corners.
                    foreach (float x in new[] { -0.73f, 0.73f })
                        k.Box(new Vector3(x, 0.36f, Back - 0.01f), new Vector3(0.1f, 0.26f, 0.03f), new Color(1f, 0.12f, 0.08f), 2.5f);
                    // Seats: same places as every car's, two buckets and a rear bench.
                    foreach (float x in new[] { -0.38f, 0.38f })
                    {
                        k.Box(new Vector3(x, 0.5f, -0.45f), new Vector3(0.52f, 0.15f, 0.55f), Tan);
                        k.Box(new Vector3(x, 0.82f, -0.74f), new Vector3(0.52f, 0.62f, 0.12f), Quaternion.Euler(-8f, 0f, 0f), Tan);
                        k.Box(new Vector3(x, 1.18f, -0.8f), new Vector3(0.3f, 0.14f, 0.1f), Tan); // headrest
                    }
                    k.Box(new Vector3(0f, 0.47f, -1.45f), new Vector3(1.4f, 0.14f, 0.5f), Tan);
                    k.Box(new Vector3(0f, 0.74f, -1.66f), new Vector3(1.4f, 0.5f, 0.1f), Tan);
                    // The dash: a black shelf behind the cowl, a grab handle on the passenger's side.
                    k.Box(new Vector3(0f, 0.6f, Cowl - 0.12f), new Vector3(Half * 2f - 0.1f, 0.16f, 0.22f), Trim);
                    k.Rod(new Vector3(0.18f, 0.7f, Cowl - 0.2f), new Vector3(0.62f, 0.7f, Cowl - 0.2f), 0.025f, 6, Satin);
                });

                // The bonnet: flat, square, with latches, vents and a black decal down each side.
                MudArt.Part(pivot, "Hood", new Vector3(0f, Belt - 0.06f, (Cowl + Front) * 0.5f), Quaternion.identity, "car_mule_hood", k =>
                {
                    float length = Front - Cowl;
                    k.Box(Vector3.zero, new Vector3(Half * 2f - 0.06f, 0.12f, length), Paint);
                    // Sides of the engine bay, down to the fenders.
                    foreach (float side in new[] { -1f, 1f })
                    {
                        k.Box(new Vector3(side * (Half - 0.06f), -0.2f, 0f), new Vector3(0.06f, 0.3f, length), Paint);
                        k.Box(new Vector3(side * (Half - 0.025f), -0.04f, -0.25f), new Vector3(0.012f, 0.06f, 0.44f), Trim); // the side decal
                        k.Box(new Vector3(side * 0.62f, -0.02f, length * 0.5f - 0.22f), new Vector3(0.05f, 0.1f, 0.06f), Trim); // latch
                        // Air vents on the bonnet top.
                        for (int i = 0; i < 4; i++)
                            k.Box(new Vector3(side * 0.45f, 0.062f, 0.05f + i * 0.08f), new Vector3(0.18f, 0.005f, 0.03f), Satin);
                    }
                    // A shallow power bulge down the middle.
                    k.Box(new Vector3(0f, 0.075f, -0.05f), new Vector3(0.62f, 0.03f, length - 0.2f), PaintShade);
                });

                // The front: a flat grille panel with seven tall slots, the round lamps either side, a steel bumper with a
                // winch and tow hooks, and black flat-topped fenders over the front wheels.
                MudArt.Part(pivot, "Front Bumper", new Vector3(0f, 0f, Front), Quaternion.identity, "car_mule_front", k =>
                {
                    k.Box(new Vector3(0f, 0.31f, 0.02f), new Vector3(1.32f, 0.56f, 0.06f), Paint, 0f);
                    k.Box(new Vector3(0f, 0.31f, 0.05f), new Vector3(1.22f, 0.5f, 0.01f), Quaternion.identity, PaintShade);
                    for (int i = -3; i <= 3; i++)
                    {
                        float x = i * 0.105f;
                        k.Box(new Vector3(x, 0.33f, 0.055f), new Vector3(0.055f, 0.32f, 0.015f), Trim);
                        k.Ball(new Vector3(x, 0.49f, 0.055f), new Vector3(0.0275f, 0.0275f, 0.008f), Quaternion.identity, Trim, 0); // rounded tops
                    }
                    foreach (float side in new[] { -1f, 1f })
                    {
                        float x = side * 0.5f;
                        k.Prism(new Vector3(x, 0.36f, 0.04f), Quaternion.Euler(90f, 0f, 0f), 0.13f, 0.14f, 0.07f, 12, Trim); // the bezel
                        k.Ball(new Vector3(x, 0.36f, 0.075f), new Vector3(0.105f, 0.105f, 0.04f), Quaternion.identity, Lens, 1, 3f);
                        k.Box(new Vector3(side * 0.6f, 0.13f, 0.06f), new Vector3(0.14f, 0.05f, 0.02f), Amber, 1.5f); // indicator
                        // The front fender: flat-topped, black, out over the tyre.
                        k.Box(new Vector3(side * 0.82f, 0.36f, -0.68f), new Vector3(0.34f, 0.05f, 0.95f), Trim);
                        k.Plank(new Vector3(side * 0.97f, 0.36f, -0.24f), new Vector3(side * 0.99f, 0.12f, -0.02f), 0.06f, 0.05f, Trim, Vector3.right);
                        k.Plank(new Vector3(side * 0.97f, 0.36f, -1.12f), new Vector3(side * 0.99f, 0.14f, -1.3f), 0.06f, 0.05f, Trim, Vector3.right);
                        // Tow hook on the bumper.
                        k.Box(new Vector3(side * 0.42f, -0.03f, 0.16f), new Vector3(0.05f, 0.12f, 0.1f), new Color(0.85f, 0.18f, 0.12f));
                    }
                    // The bumper: a squared steel bar with end caps, a winch in the middle.
                    k.Box(new Vector3(0f, -0.04f, 0.08f), new Vector3(1.86f, 0.18f, 0.16f), Satin);
                    foreach (float side in new[] { -1f, 1f })
                        k.Box(new Vector3(side * 0.88f, -0.04f, 0.03f), new Vector3(0.14f, 0.16f, 0.22f), Quaternion.Euler(0f, side * 25f, 0f), Satin);
                    k.Box(new Vector3(0f, 0.06f, 0.13f), new Vector3(0.42f, 0.14f, 0.12f), Trim);
                    k.Prism(new Vector3(-0.17f, 0.06f, 0.2f), Quaternion.Euler(0f, 0f, -90f), 0.05f, 0.05f, 0.34f, 8, Satin);
                    k.Box(new Vector3(0f, -0.18f, -0.25f), new Vector3(1.0f, 0.04f, 0.5f), Satin); // the skid plate
                });

                // The windscreen: an upright frame with glass, mirrors on its hinges, and the roll bar behind the seats
                // with side bars back to the tailgate. No roof and no doors.
                MudArt.Part(pivot, "Roof", new Vector3(0f, Belt, Cowl), Quaternion.identity, "car_mule_cage", k =>
                {
                    Quaternion lean = Quaternion.Euler(-14f, 0f, 0f);
                    const float tall = 0.56f, wide = Half * 2f - 0.04f;
                    Vector3 Up(float x, float y) => lean * new Vector3(x, y, 0f);
                    // The frame: two posts, a top rail and the bottom rail, white like the body.
                    foreach (float side in new[] { -1f, 1f })
                        k.Plank(Up(side * wide * 0.5f, 0f), Up(side * wide * 0.5f, tall), 0.06f, 0.06f, Paint, Vector3.forward);
                    k.Plank(Up(-wide * 0.5f, tall), Up(wide * 0.5f, tall), 0.08f, 0.06f, Paint, Vector3.up);
                    k.Box(Up(0f, tall * 0.5f), new Vector3(wide - 0.08f, tall - 0.08f, 0.02f), lean, Glass);
                    // Mirrors on stalks at the hinges.
                    foreach (float side in new[] { -1f, 1f })
                    {
                        Vector3 hinge = Up(side * wide * 0.5f, 0.05f);
                        Vector3 head = hinge + new Vector3(side * 0.16f, 0.06f, -0.02f);
                        k.Rod(hinge, head, 0.015f, 5, Trim);
                        k.Box(head + new Vector3(side * 0.05f, 0f, 0f), new Vector3(0.12f, 0.1f, 0.03f), Trim);
                    }
                    // Wipers parked on the glass.
                    foreach (float x in new[] { -0.32f, 0.22f })
                        k.Plank(Up(x, 0.04f), Up(x + 0.26f, 0.08f), 0.02f, 0.012f, Trim, lean * Vector3.forward);

                    // The roll bar: a hoop behind the front seats, bars forward to the windscreen and back to the tailgate.
                    float hoopZ = -0.83f - Cowl, top = 0.56f;
                    var hoopL = new Vector3(-Half + 0.08f, 0f, hoopZ);
                    var hoopR = new Vector3(Half - 0.08f, 0f, hoopZ);
                    k.Tube(new[] { hoopL, hoopL + new Vector3(0.04f, top - 0.08f, 0f), new Vector3(hoopL.x + 0.14f, top, hoopZ),
                        new Vector3(hoopR.x - 0.14f, top, hoopZ), hoopR + new Vector3(-0.04f, top - 0.08f, 0f), hoopR }, 0.04f, 8, Trim);
                    foreach (float side in new[] { -1f, 1f })
                    {
                        Vector3 corner = new Vector3(side * (Half - 0.12f), top, hoopZ);
                        k.Rod(corner, Up(side * (wide * 0.5f - 0.02f), tall - 0.01f), 0.035f, 8, Trim);
                        k.Rod(corner, new Vector3(side * (Half - 0.1f), 0.02f, Back - Cowl + 0.12f), 0.035f, 8, Trim);
                        k.Ball(corner, 0.045f, Trim, 0);
                    }
                    k.Rod(new Vector3(-Half + 0.2f, top - 0.04f, hoopZ - 0.02f), new Vector3(Half - 0.2f, 0.06f, hoopZ - 0.02f), 0.03f, 8, Trim); // the diagonal
                    // A whip antenna with a pennant, for spotting it over the dunes.
                    var mast = new Vector3(Half - 0.08f, 0.02f, Back - Cowl + 0.08f);
                    k.Rod(mast, mast + new Vector3(0f, 0.85f, 0f), 0.008f, 4, Trim);
                    k.Tri(mast + new Vector3(0f, 0.85f, 0f), mast + new Vector3(0f, 0.67f, 0f), mast + new Vector3(0f, 0.76f, -0.26f), Flag);
                    k.Tri(mast + new Vector3(0f, 0.67f, 0f), mast + new Vector3(0f, 0.85f, 0f), mast + new Vector3(0f, 0.76f, -0.26f), Flag);
                });

                // The back: a spare on the tailgate, a jerry can, a step bumper, black rear flares.
                MudArt.Part(pivot, "Trunk", new Vector3(0f, 0.38f, Back - 0.2f), Quaternion.Euler(0f, 90f, 0f), "car_mule_spare", k =>
                {
                    Tyre(k, 0.34f, 0.24f, 12, MudPalette.Rubber, 0.05f);
                    k.Prism(new Vector3(0.13f, 0f, 0f), Quaternion.Euler(0f, 0f, 90f), 0.19f, 0.19f, 0.26f, 10, Satin);
                    k.Box(new Vector3(-0.14f, 0f, 0f), new Vector3(0.02f, 0.3f, 0.06f), Satin);
                    k.Box(new Vector3(-0.14f, 0f, 0f), new Vector3(0.02f, 0.06f, 0.3f), Satin);
                });
                MudArt.Part(pivot, "Rear Bumper", new Vector3(0f, -0.04f, Back - 0.08f), Quaternion.identity, "car_mule_bumper", k =>
                {
                    k.Box(Vector3.zero, new Vector3(1.8f, 0.16f, 0.14f), Satin);
                    k.Box(new Vector3(0f, 0.09f, -0.02f), new Vector3(0.36f, 0.02f, 0.12f), Trim); // the step
                    foreach (float side in new[] { -1f, 1f })
                    {
                        k.Box(new Vector3(side * 0.82f, 0.38f, 0.75f), new Vector3(0.32f, 0.05f, 0.82f), Trim); // the rear flare's top
                        k.Plank(new Vector3(side * 0.97f, 0.38f, 1.12f), new Vector3(side * 0.99f, 0.16f, 1.3f), 0.06f, 0.05f, Trim, Vector3.right);
                        k.Plank(new Vector3(side * 0.97f, 0.38f, 0.38f), new Vector3(side * 0.99f, 0.16f, 0.2f), 0.06f, 0.05f, Trim, Vector3.right);
                    }
                    // A jerry can strapped beside the spare.
                    k.Box(new Vector3(0.6f, 0.4f, -0.08f), new Vector3(0.18f, 0.4f, 0.12f), Olive);
                    k.Box(new Vector3(0.6f, 0.62f, -0.08f), new Vector3(0.06f, 0.05f, 0.06f), Olive);
                    k.Box(new Vector3(0.6f, 0.38f, -0.145f), new Vector3(0.19f, 0.03f, 0.01f), Trim);
                });
            }

            public static Transform Wheel(Transform parent, string name, Vector3 position)
            {
                return MudArt.Part(parent, name, position, Quaternion.identity, "car_mule_wheel", k =>
                {
                    // Big, chunky all-terrains (a touch taller and much wider than the muscle car's) on black five-spoke wheels.
                    const float width = 0.34f;
                    Tyre(k, 0.395f, width, 14, MudPalette.Rubber, 0.06f);
                    Quaternion axis = Quaternion.Euler(0f, 0f, 90f);
                    k.Prism(new Vector3(width * 0.5f + 0.005f, 0f, 0f), axis, 0.22f, 0.22f, width + 0.01f, 10, Trim);
                    for (int i = 0; i < 5; i++)
                    {
                        Quaternion turn = Quaternion.Euler(i * 72f, 0f, 0f);
                        foreach (float side in new[] { -1f, 1f })
                            k.Box(turn * new Vector3(side * (width * 0.5f + 0.01f), 0.11f, 0f), new Vector3(0.02f, 0.2f, 0.06f), turn, Satin);
                    }
                    foreach (float side in new[] { -1f, 1f })
                        k.Prism(new Vector3(side * (width * 0.5f + 0.02f), 0f, 0f), axis, 0.05f, 0.05f, 0.03f * side, 6, Satin);
                });
            }
        }

        // ---------- The Crystal Caves: the Geode Crawler ----------

        private static class GeodeCrawler
        {
            private static readonly Color Hazard = new Color(0.93f, 0.66f, 0.13f);
            private static readonly Color HazardDark = new Color(0.66f, 0.45f, 0.08f);
            private static readonly Color Slate = new Color(0.25f, 0.28f, 0.32f);
            private static readonly Color SlateDark = new Color(0.15f, 0.17f, 0.2f);
            private static readonly Color Cage = new Color(0.12f, 0.13f, 0.15f);
            private static readonly Color Seat = new Color(0.2f, 0.3f, 0.34f);
            private static readonly Color CrystalCyan = new Color(0.35f, 0.95f, 1f);
            private static readonly Color CrystalViolet = new Color(0.75f, 0.45f, 1f);
            private static readonly Color Lamp = new Color(0.85f, 0.98f, 1f);

            // A cluster of pointed crystals from a base point, leaning out along `outward`.
            private static void Crystals(MeshKit k, Vector3 at, Vector3 outward, Color color, float size, int count, int seed)
            {
                var random = new System.Random(seed);
                for (int i = 0; i < count; i++)
                {
                    float spread = 35f;
                    Quaternion tilt = Quaternion.FromToRotation(Vector3.up, outward.normalized)
                        * Quaternion.Euler((float)(random.NextDouble() * 2 - 1) * spread, (float)random.NextDouble() * 360f, (float)(random.NextDouble() * 2 - 1) * spread);
                    float s = size * (0.55f + (float)random.NextDouble() * 0.6f);
                    k.Prism(at, tilt, s * 0.32f, s * 0.26f, s * 1.1f, 6, color, 0.7f);
                    k.Cone(at + tilt * new Vector3(0f, s * 1.1f, 0f), tilt, s * 0.26f, s * 0.5f, 6, color, 0.9f);
                }
            }

            public static void Body(Transform pivot)
            {
                MudArt.Part(pivot, "Chassis", Vector3.zero, Quaternion.identity, "car_geode_chassis", k =>
                {
                    // A low, slab-sided tub on a slate armoured belly.
                    k.Box(new Vector3(0f, -0.1f, 0f), new Vector3(1.55f, 0.16f, 3.9f), SlateDark);
                    k.Box(new Vector3(0f, 0.18f, -0.35f), new Vector3(1.72f, 0.42f, 3.2f), Hazard);
                    foreach (float side in new[] { -1f, 1f })
                    {
                        // Slate armour plates bolted along each side, with rivets.
                        for (int i = 0; i < 3; i++)
                        {
                            float z = -1.45f + i * 0.95f;
                            k.Box(new Vector3(side * 0.875f, 0.14f, z), new Vector3(0.04f, 0.3f, 0.82f), Slate);
                            foreach (float dz in new[] { -0.34f, 0.34f })
                                k.Ball(new Vector3(side * 0.9f, 0.24f, z + dz), 0.025f, MudPalette.Metal, 0);
                        }
                        // Hazard chevrons on the sill.
                        for (int i = 0; i < 5; i++)
                            k.Box(new Vector3(side * 0.868f, -0.03f, -1.6f + i * 0.62f), new Vector3(0.03f, 0.08f, 0.22f), Quaternion.Euler(35f, 0f, 0f), MudPalette.Black);
                    }
                    // Bucket seats, same places as every car's.
                    foreach (float x in new[] { -0.38f, 0.38f })
                    {
                        k.Box(new Vector3(x, 0.5f, -0.45f), new Vector3(0.52f, 0.15f, 0.55f), Seat);
                        k.Box(new Vector3(x, 0.8f, -0.73f), new Vector3(0.52f, 0.6f, 0.12f), Seat);
                    }
                    k.Box(new Vector3(0f, 0.56f, 0.5f), new Vector3(1.4f, 0.18f, 0.28f), Cage); // the dash
                });
                // The nose: a wedge bonnet like an ore cart's prow, with crystals breaking out of the corners.
                MudArt.Part(pivot, "Hood", new Vector3(0f, 0.42f, 1.32f), Quaternion.identity, "car_geode_hood", k =>
                {
                    k.Box(new Vector3(0f, 0f, 0f), new Vector3(1.62f, 0.16f, 1.3f), Quaternion.Euler(6f, 0f, 0f), Hazard);
                    k.Box(new Vector3(0f, -0.2f, 0.62f), new Vector3(1.7f, 0.36f, 0.12f), Quaternion.Euler(-20f, 0f, 0f), HazardDark);
                    k.Box(new Vector3(0f, 0.09f, -0.15f), new Vector3(0.7f, 0.06f, 0.8f), Quaternion.Euler(6f, 0f, 0f), SlateDark); // the intake plate
                    for (int i = 0; i < 4; i++) k.Box(new Vector3(-0.27f + i * 0.18f, 0.13f, -0.15f), new Vector3(0.05f, 0.03f, 0.7f), Quaternion.Euler(6f, 0f, 0f), Cage);
                    Crystals(k, new Vector3(-0.72f, 0.06f, 0.42f), new Vector3(-0.6f, 1f, 0.5f), CrystalCyan, 0.22f, 4, 3);
                    Crystals(k, new Vector3(0.7f, 0.04f, -0.4f), new Vector3(0.7f, 1f, -0.2f), CrystalViolet, 0.18f, 3, 7);
                });
                // Front: black-and-yellow bumper, two round work lamps low, a skid plate.
                MudArt.Part(pivot, "Front Bumper", new Vector3(0f, 0.02f, 2.06f), Quaternion.identity, "car_geode_front", k =>
                {
                    k.Box(Vector3.zero, new Vector3(1.9f, 0.22f, 0.16f), MudPalette.Black);
                    for (int i = 0; i < 7; i++)
                        k.Box(new Vector3(-0.81f + i * 0.27f, 0f, 0.085f), new Vector3(0.12f, 0.22f, 0.01f), Quaternion.Euler(0f, 0f, 35f), Hazard);
                    foreach (float x in new[] { -0.62f, 0.62f })
                    {
                        k.Prism(new Vector3(x, 0.24f, -0.06f), Quaternion.Euler(90f, 0f, 0f), 0.13f, 0.14f, 0.1f, 10, Cage);
                        k.Ball(new Vector3(x, 0.24f, 0.05f), new Vector3(0.11f, 0.11f, 0.05f), Quaternion.identity, Lamp, 1, 3.5f);
                    }
                    k.Box(new Vector3(0f, -0.16f, -0.35f), new Vector3(1.2f, 0.05f, 0.7f), Quaternion.Euler(-12f, 0f, 0f), Slate);
                });
                // The cage: an open roll cage of dark tube, a lamp bar on top, crystals growing on a corner.
                MudArt.Part(pivot, "Roof", new Vector3(0f, 0.62f, -0.4f), Quaternion.identity, "car_geode_cage", k =>
                {
                    float top = 0.56f, x = 0.74f, front = 0.95f, back = -0.75f;
                    foreach (float side in new[] { -1f, 1f })
                    {
                        k.Tube(new[] { new Vector3(side * x, 0f, front), new Vector3(side * x * 0.92f, top, front - 0.25f), new Vector3(side * x * 0.92f, top, back + 0.1f), new Vector3(side * x, 0f, back) }, 0.035f, 8, Cage);
                    }
                    k.Rod(new Vector3(-x * 0.92f, top, front - 0.25f), new Vector3(x * 0.92f, top, front - 0.25f), 0.035f, 8, Cage);
                    k.Rod(new Vector3(-x * 0.92f, top, back + 0.1f), new Vector3(x * 0.92f, top, back + 0.1f), 0.035f, 8, Cage);
                    k.Rod(new Vector3(-x * 0.92f, top, back + 0.1f), new Vector3(x * 0.92f, top, front - 0.25f), 0.03f, 8, Cage);
                    // Mesh panel over the top against falling rock.
                    for (int i = 0; i < 5; i++)
                        k.Rod(new Vector3(-x * 0.9f, top + 0.01f, back + 0.25f + i * 0.2f), new Vector3(x * 0.9f, top + 0.01f, back + 0.25f + i * 0.2f), 0.012f, 4, SlateDark);
                    // The lamp bar: four round lamps on a bar across the front of the cage.
                    k.Box(new Vector3(0f, top + 0.06f, front - 0.25f), new Vector3(1.2f, 0.06f, 0.08f), Cage);
                    for (int i = 0; i < 4; i++)
                    {
                        float lx = -0.45f + i * 0.3f;
                        k.Prism(new Vector3(lx, top + 0.13f, front - 0.28f), Quaternion.Euler(90f, 0f, 0f), 0.07f, 0.075f, 0.08f, 8, Cage);
                        k.Ball(new Vector3(lx, top + 0.13f, front - 0.19f), new Vector3(0.06f, 0.06f, 0.03f), Quaternion.identity, Lamp, 1, 4f);
                    }
                    Crystals(k, new Vector3(-x * 0.92f, top, back + 0.1f), new Vector3(-0.5f, 1f, -0.4f), CrystalCyan, 0.2f, 4, 11);
                });
                // The back: an ore bin heaped with glowing chunks, and the tail lamps.
                MudArt.Part(pivot, "Trunk", new Vector3(0f, 0.5f, -1.55f), Quaternion.identity, "car_geode_bin", k =>
                {
                    k.Box(new Vector3(0f, 0f, 0f), new Vector3(1.5f, 0.08f, 0.85f), SlateDark);
                    foreach (float side in new[] { -1f, 1f })
                    {
                        k.Box(new Vector3(side * 0.73f, 0.16f, 0f), new Vector3(0.05f, 0.32f, 0.85f), Quaternion.Euler(0f, 0f, side * -8f), Slate);
                        k.Box(new Vector3(0f, 0.16f, side * 0.41f), new Vector3(1.5f, 0.32f, 0.05f), Slate);
                    }
                    var random = new System.Random(19);
                    for (int i = 0; i < 9; i++)
                    {
                        var at = new Vector3((float)(random.NextDouble() - 0.5) * 1.1f, 0.1f, (float)(random.NextDouble() - 0.5) * 0.55f);
                        Color c = i % 3 == 0 ? CrystalViolet : i % 3 == 1 ? CrystalCyan : new Color(0.45f, 1f, 0.6f);
                        k.Ball(at + Vector3.up * 0.1f, new Vector3(0.14f, 0.12f, 0.13f), Quaternion.Euler(i * 37f, i * 53f, 0f), SlateDark, 0);
                        k.Prism(at + Vector3.up * 0.12f, Quaternion.Euler((float)(random.NextDouble() - 0.5) * 50f, i * 40f, (float)(random.NextDouble() - 0.5) * 50f), 0.06f, 0.05f, 0.22f, 6, c, 0.7f);
                    }
                });
                MudArt.Part(pivot, "Rear Bumper", new Vector3(0f, 0.02f, -2.04f), Quaternion.identity, "car_geode_rear", k =>
                {
                    k.Box(Vector3.zero, new Vector3(1.85f, 0.2f, 0.16f), MudPalette.Black);
                    for (int i = 0; i < 7; i++)
                        k.Box(new Vector3(-0.81f + i * 0.27f, 0f, -0.085f), new Vector3(0.12f, 0.2f, 0.01f), Quaternion.Euler(0f, 0f, -35f), Hazard);
                    foreach (float x in new[] { -0.72f, 0.72f })
                        k.Box(new Vector3(x, 0.22f, 0.02f), new Vector3(0.2f, 0.12f, 0.05f), new Color(1f, 0.15f, 0.1f), 2.5f);
                });
            }

            public static Transform Wheel(Transform parent, string name, Vector3 position)
            {
                return MudArt.Part(parent, name, position, Quaternion.identity, "car_geode_wheel", k =>
                {
                    const float width = 0.32f;
                    Tyre(k, 0.39f, width, 10, MudPalette.Rubber, 0.07f);
                    Quaternion axis = Quaternion.Euler(0f, 0f, 90f);
                    k.Prism(new Vector3(width * 0.5f + 0.005f, 0f, 0f), axis, 0.2f, 0.2f, width + 0.01f, 8, Slate);
                    foreach (float side in new[] { -1f, 1f })
                    {
                        k.Prism(new Vector3(side * (width * 0.5f + 0.02f), 0f, 0f), axis, 0.09f, 0.07f, 0.03f * side, 6, CrystalCyan, 0.8f);
                        for (int i = 0; i < 6; i++)
                        {
                            Quaternion turn = Quaternion.Euler(i * 60f, 0f, 0f);
                            k.Ball(turn * new Vector3(side * (width * 0.5f + 0.012f), 0.15f, 0f), 0.022f, MudPalette.Metal, 0);
                        }
                    }
                });
            }
        }

        // ---------- The Wasteland: the Scrap Runner ----------

        private static class ScrapRunner
        {
            private static readonly Color Soot = new Color(0.17f, 0.15f, 0.14f);
            private static readonly Color Rust = new Color(0.5f, 0.25f, 0.12f);
            private static readonly Color RustLight = new Color(0.66f, 0.38f, 0.18f);
            private static readonly Color Steel = new Color(0.42f, 0.42f, 0.41f);
            private static readonly Color Seat = new Color(0.36f, 0.24f, 0.15f);
            private static readonly Color Grate = new Color(0.1f, 0.1f, 0.1f);

            public static void Body(Transform pivot)
            {
                MudArt.Part(pivot, "Chassis", Vector3.zero, Quaternion.identity, "car_scrap_chassis", k =>
                {
                    // A chopped coupe body in soot-black primer, rust breaking through.
                    k.Box(new Vector3(0f, 0.16f, 0f), new Vector3(1.78f, 0.44f, 4.0f), Soot);
                    k.Box(new Vector3(0f, -0.1f, 0f), new Vector3(1.5f, 0.12f, 3.9f), MudPalette.DarkMetal);
                    foreach (float side in new[] { -1f, 1f })
                    {
                        k.Box(new Vector3(side * 0.9f, 0.08f, 0.9f), new Vector3(0.02f, 0.2f, 0.6f), Rust);
                        k.Box(new Vector3(side * 0.9f, 0.24f, -1.3f), new Vector3(0.02f, 0.16f, 0.5f), RustLight);
                        // Ship hull panels welded on for armour (the crashed ship's own off-white and orange stripe).
                        k.ScrapPanel(new Vector3(side * 0.93f, 0.17f, -0.3f), new Vector3(1.1f, 0.36f, 0.04f), Quaternion.Euler(0f, side * 90f, side * 4f), MudPalette.Hull);
                        // Side pipes.
                        k.Rod(new Vector3(side * 0.97f, -0.08f, 0.9f), new Vector3(side * 0.97f, -0.08f, -1.2f), 0.06f, 8, Steel);
                    }
                    foreach (float x in new[] { -0.38f, 0.38f })
                    {
                        k.Box(new Vector3(x, 0.5f, -0.45f), new Vector3(0.52f, 0.15f, 0.55f), Seat);
                        k.Box(new Vector3(x, 0.8f, -0.73f), new Vector3(0.52f, 0.6f, 0.12f), Seat);
                    }
                });
                // The bonnet: cut open for a big blower and two stacks.
                MudArt.Part(pivot, "Hood", new Vector3(0f, 0.42f, 1.25f), Quaternion.Euler(-2f, 0f, 0f), "car_scrap_hood", k =>
                {
                    k.Box(Vector3.zero, new Vector3(1.72f, 0.1f, 1.6f), Soot);
                    k.Box(new Vector3(0.45f, 0.055f, 0.3f), new Vector3(0.5f, 0.01f, 0.4f), Rust);
                    k.Box(new Vector3(0f, 0.16f, -0.05f), new Vector3(0.5f, 0.24f, 0.62f), Steel); // the blower
                    for (int i = 0; i < 5; i++) k.Box(new Vector3(0f, 0.29f, -0.3f + i * 0.12f), new Vector3(0.52f, 0.02f, 0.04f), MudPalette.DarkMetal);
                    k.Box(new Vector3(0f, 0.34f, 0.0f), new Vector3(0.34f, 0.08f, 0.3f), MudPalette.HullStripe); // the scoop
                    foreach (float side in new[] { -1f, 1f })
                    {
                        k.Rod(new Vector3(side * 0.62f, 0.05f, -0.45f), new Vector3(side * 0.7f, 0.65f, -0.55f), 0.06f, 8, Steel, 0f, 0.075f);
                        k.Prism(new Vector3(side * 0.7f, 0.65f, -0.55f), Quaternion.Euler(-10f, 0f, 0f), 0.08f, 0.08f, 0.04f, 8, MudPalette.Black);
                    }
                });
                // The front: a spiked ram of scrap bars over a grille of bolts and twin lamps in cages.
                MudArt.Part(pivot, "Front Bumper", new Vector3(0f, 0.06f, 2.08f), Quaternion.identity, "car_scrap_front", k =>
                {
                    k.Box(Vector3.zero, new Vector3(1.85f, 0.22f, 0.16f), Steel);
                    for (int i = -3; i <= 3; i++)
                    {
                        k.Plank(new Vector3(i * 0.26f, -0.12f, 0.02f), new Vector3(i * 0.18f, 0.32f, -0.08f), 0.06f, 0.04f, Rust, Vector3.forward);
                        if (i % 2 == 0) k.Cone(new Vector3(i * 0.26f, 0f, 0.08f), Quaternion.Euler(90f, 0f, 0f), 0.05f, 0.2f, 5, Steel);
                    }
                    foreach (float x in new[] { -0.68f, 0.68f })
                    {
                        k.Ball(new Vector3(x, 0.36f, -0.02f), new Vector3(0.1f, 0.1f, 0.05f), Quaternion.identity, new Color(1f, 0.85f, 0.5f), 1, 3f);
                        k.Box(new Vector3(x, 0.36f, 0.05f), new Vector3(0.22f, 0.02f, 0.02f), Grate);
                        k.Box(new Vector3(x, 0.36f, 0.05f), new Vector3(0.02f, 0.22f, 0.02f), Grate);
                    }
                });
                // The roof: chopped low, a steel grate for a windscreen, a rack with a spare and a can.
                MudArt.Part(pivot, "Roof", new Vector3(0f, 0.92f, -0.45f), Quaternion.identity, "car_scrap_roof", k =>
                {
                    k.Box(Vector3.zero, new Vector3(1.5f, 0.07f, 1.35f), Soot);
                    foreach (float x in new[] { -0.72f, 0.72f })
                    {
                        k.Plank(new Vector3(x, -0.02f, 0.65f), new Vector3(x, -0.42f, 0.98f), 0.07f, 0.07f, Rust);
                        k.Plank(new Vector3(x, -0.02f, -0.65f), new Vector3(x, -0.42f, -0.92f), 0.07f, 0.07f, Soot);
                    }
                    Quaternion rake = Quaternion.Euler(-40f, 0f, 0f);
                    for (int i = 0; i < 9; i++)
                        k.Box(new Vector3(-0.64f + i * 0.16f, -0.21f, 0.82f), new Vector3(0.02f, 0.5f, 0.02f), rake, Grate);
                    for (int i = 0; i < 3; i++)
                        k.Box(new Vector3(0f, -0.08f - i * 0.13f, 0.72f + i * 0.11f), new Vector3(1.36f, 0.02f, 0.02f), Grate);
                    // The rack.
                    foreach (float x in new[] { -0.6f, 0.6f }) k.Rod(new Vector3(x, 0.08f, -0.55f), new Vector3(x, 0.08f, 0.5f), 0.025f, 6, Steel);
                    for (int i = 0; i < 4; i++) k.Rod(new Vector3(-0.6f, 0.08f, -0.5f + i * 0.33f), new Vector3(0.6f, 0.08f, -0.5f + i * 0.33f), 0.02f, 6, Steel);
                    k.Prism(new Vector3(-0.25f, 0.1f, -0.1f), Quaternion.identity, 0.3f, 0.3f, 0.2f, 10, MudPalette.Rubber);
                    k.Box(new Vector3(0.38f, 0.24f, 0.15f), new Vector3(0.18f, 0.3f, 0.12f), new Color(0.55f, 0.13f, 0.1f));
                });
                MudArt.Part(pivot, "Trunk", new Vector3(0f, 0.44f, -1.6f), Quaternion.identity, "car_scrap_trunk", k =>
                {
                    k.Box(Vector3.zero, new Vector3(1.72f, 0.1f, 0.95f), Soot);
                    k.ScrapPanel(new Vector3(0f, 0.07f, 0f), new Vector3(1.0f, 0.5f, 0.03f), Quaternion.Euler(90f, 0f, 0f), MudPalette.Hull, false);
                });
                MudArt.Part(pivot, "Rear Bumper", new Vector3(0f, 0.04f, -2.08f), Quaternion.identity, "car_scrap_rear", k =>
                {
                    k.Box(Vector3.zero, new Vector3(1.8f, 0.2f, 0.16f), Steel);
                    foreach (float x in new[] { -0.7f, 0.7f })
                        k.Box(new Vector3(x, 0.26f, 0.02f), new Vector3(0.22f, 0.1f, 0.05f), new Color(1f, 0.15f, 0.1f), 2.5f);
                    k.Box(new Vector3(0f, 0.24f, -0.03f), new Vector3(0.45f, 0.2f, 0.03f), RustLight); // a plate gone to rust
                });
            }

            public static Transform Wheel(Transform parent, string name, Vector3 position)
            {
                return MudArt.Part(parent, name, position, Quaternion.identity, "car_scrap_wheel", k =>
                {
                    const float width = 0.3f;
                    Tyre(k, 0.385f, width, 12, MudPalette.Rubber, 0.05f);
                    Quaternion axis = Quaternion.Euler(0f, 0f, 90f);
                    k.Prism(new Vector3(width * 0.5f + 0.005f, 0f, 0f), axis, 0.21f, 0.21f, width + 0.01f, 8, Rust);
                    foreach (float side in new[] { -1f, 1f })
                        for (int i = 0; i < 4; i++)
                        {
                            Quaternion turn = Quaternion.Euler(i * 90f + 20f, 0f, 0f);
                            k.Box(turn * new Vector3(side * (width * 0.5f + 0.01f), 0.12f, 0f), new Vector3(0.02f, 0.06f, 0.06f), turn, Steel);
                        }
                });
            }
        }
    }
}
