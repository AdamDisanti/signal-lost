using UnityEngine;

namespace SignalLost.Mudlands
{
    /// <summary>
    /// The Drunk Driving car, built from low-poly parts: a faded teal muscle car with rust
    /// patches, a grey primer passenger door, a cream hood stripe, chrome-ish bumpers gone brown, round headlights,
    /// twin exhausts and fuzzy dice. Body parts are separate pieces so they can fly off when it blows up.
    /// </summary>
    public static class CarModel
    {
        private static readonly Color Paint = new Color(0.27f, 0.55f, 0.55f);
        private static readonly Color PaintDark = new Color(0.19f, 0.4f, 0.41f);
        private static readonly Color Primer = new Color(0.55f, 0.56f, 0.55f);
        private static readonly Color Chrome = new Color(0.62f, 0.58f, 0.52f);
        private static readonly Color Glass = new Color(0.2f, 0.27f, 0.31f);
        private static readonly Color Seat = new Color(0.45f, 0.17f, 0.12f);

        /// <summary>The body, origin at the body pivot (just above the axles), facing +Z.</summary>
        public static void BuildBody(Transform pivot)
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
            // A steering wheel in front of the driver's seat.
            MudArt.Part(pivot, "Steering Wheel", new Vector3(-0.38f, 0.82f, 0.25f), Quaternion.Euler(-60f, 0f, 0f), "car_wheel_steer", k =>
            {
                k.Prism(Vector3.zero, Quaternion.identity, 0.2f, 0.2f, 0.04f, 10, MudPalette.Black);
                k.Rod(new Vector3(0f, 0.02f, 0f), new Vector3(0f, -0.3f, 0f), 0.03f, 5, MudPalette.DarkMetal);
            });

            var light = new GameObject("Headlights").AddComponent<Light>();
            light.transform.SetParent(pivot, false);
            light.transform.localPosition = new Vector3(0f, 0.4f, 2.2f);
            light.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);
            light.type = LightType.Spot;
            light.spotAngle = 70f;
            light.range = 26f;
            light.intensity = 6f;
            light.color = new Color(1f, 0.93f, 0.75f);
            light.shadows = LightShadows.None;
        }

        /// <summary>A wheel (tyre, hub and spokes) spinning about its local x.</summary>
        public static Transform Wheel(Transform parent, string name, Vector3 position, bool left)
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
}
