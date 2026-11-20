using UnityEngine;

namespace SignalLost.Mudlands
{
    /// <summary>
    /// The Swamp Rats' makeshift hovercycles (Christian, 11-10: "convert the motorcycles to makeshift hovercycles",
    /// after a low, sleek racing speeder with two forward prongs). Nothing here was bought: the keels are swamp logs,
    /// the prongs are bone tusks, the fins are giant teal leaves, the lift rings are tyres and bucket rims, the panels
    /// are hull plate off the crashed ship (purple paint over the orange stripe), and it is all lashed together with
    /// glowing swamp vine. Each bike floats on lift rings (a fan of leaf blades on a glowing core, a glow under it).
    /// <para>Shared shapes, Clutch's big hovercycle (<see cref="Clutch"/>) and the gang's three small kinds
    /// (<see cref="Mini"/>). Every mesh is built in the bike's own frame: +Z forward, ground at y = 0.</para>
    /// </summary>
    public static class Hoverbikes
    {
        // Swamp stuff: alien bark (violet-brown), bone, teal leaves, glowing vine, gourds; and the lift glow.
        public static readonly Color Bark = new Color(0.3f, 0.23f, 0.34f), BarkDark = new Color(0.19f, 0.15f, 0.23f),
            Bone = new Color(0.88f, 0.84f, 0.72f), BoneDark = new Color(0.68f, 0.62f, 0.5f),
            Leaf = new Color(0.16f, 0.46f, 0.44f), LeafLight = new Color(0.32f, 0.66f, 0.56f),
            Vine = new Color(0.2f, 0.38f, 0.26f), VineGlow = new Color(0.5f, 1f, 0.62f),
            Gourd = new Color(0.86f, 0.55f, 0.22f), GourdRib = new Color(0.68f, 0.38f, 0.14f),
            Rust = new Color(0.55f, 0.29f, 0.15f), Lift = new Color(0.3f, 0.95f, 0.85f), LiftCore = new Color(0.55f, 1f, 0.95f);

        // =====================================================================================================
        // Shared shapes
        // =====================================================================================================

        /// <summary>A ring round local +Y (tyre, rim, hoop, lashing) of the rotation, centred at <paramref name="centre"/>.</summary>
        public static void Torus(MeshKit k, Vector3 centre, Quaternion rotation, float radius, float tube, Color color, float glow = 0f, int sides = 24)
        {
            int steps = k.Faceted ? 4 : 12;
            var profile = new Vector2[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                float a = -Mathf.PI * 0.5f + i * Mathf.PI * 2f / steps;
                profile[i] = new Vector2(radius + Mathf.Cos(a) * tube, Mathf.Sin(a) * tube);
            }
            k.Lathe(centre, rotation, profile, sides, color, glow);
        }

        /// <summary>
        /// A flat lift ring (round local +Y): the shell ring, a thin glowing disc just under it (the lift field), a hub
        /// with a glowing core, and three struts holding the hub. The fan goes in separately so it can spin.
        /// </summary>
        public static void LiftRing(MeshKit k, Vector3 centre, float radius, float tube, Color shell, float hub)
        {
            Torus(k, centre, Quaternion.identity, radius, tube, shell);
            k.Prism(centre + Vector3.down * (tube + 0.012f), Quaternion.identity, radius, radius, 0.012f, 20, Lift, 2.2f);
            k.Prism(centre + Vector3.down * tube * 0.7f, Quaternion.identity, hub, hub * 0.8f, tube * 1.2f, 12, MudPalette.DarkMetal);
            k.Prism(centre + Vector3.down * tube * 0.9f, Quaternion.identity, hub * 0.55f, hub * 0.55f, tube * 1.6f, 10, LiftCore, 3f);
            for (int i = 0; i < 3; i++)
            {
                Vector3 dir = Quaternion.Euler(0f, 30f + i * 120f, 0f) * Vector3.forward;
                k.Rod(centre + dir * hub * 0.9f, centre + dir * (radius - tube * 0.6f), Mathf.Max(0.008f, tube * 0.18f), 6, MudPalette.Metal);
            }
        }

        /// <summary>A fan of leaf blades round local +Y at the origin (spun about Y), pitched like a propeller.</summary>
        public static void Fan(MeshKit k, int blades, float hub, float radius, float thickness, Color blade, Color vein) =>
            Fan(k, blades, hub, radius, thickness, blade, vein, Vector3.zero, Quaternion.identity);

        /// <summary>A fan placed at <paramref name="centre"/>, its axis along the rotation's +Y.</summary>
        public static void Fan(MeshKit k, int blades, float hub, float radius, float thickness, Color blade, Color vein, Vector3 centre, Quaternion rotation)
        {
            for (int i = 0; i < blades; i++)
            {
                Quaternion spin = rotation * Quaternion.Euler(0f, i * 360f / blades, 0f);
                Quaternion pitch = spin * Quaternion.Euler(0f, 0f, 28f);
                float len = radius - hub;
                Vector3 mid = centre + spin * new Vector3(0f, 0f, hub + len * 0.5f);
                k.Ball(mid, new Vector3(len * 0.22f, thickness, len * 0.5f), pitch, blade, 1);
                k.Rod(centre + spin * new Vector3(0f, 0.002f, hub), centre + spin * new Vector3(0f, 0.002f, radius * 0.95f), thickness * 0.7f, 4, vein);
            }
        }

        /// <summary>A vine lashing round a part: two tight turns of vine with glowing buds on them.</summary>
        public static void Lash(MeshKit k, Vector3 centre, Vector3 axis, float radius)
        {
            Quaternion r = Quaternion.FromToRotation(Vector3.up, axis.normalized);
            float tube = Mathf.Max(0.008f, radius * 0.12f);
            Torus(k, centre - r * Vector3.up * tube * 1.1f, r, radius + tube * 0.6f, tube, Vine, 0f, 14);
            Torus(k, centre + r * Vector3.up * tube * 1.1f, r, radius + tube * 0.6f, tube, Vine, 0f, 14);
            for (int i = 0; i < 3; i++)
            {
                Vector3 around = r * (Quaternion.Euler(0f, i * 120f + 20f, 0f) * Vector3.forward);
                k.Ball(centre + around * (radius + tube * 1.6f), tube * 1.3f, VineGlow, 0, 2f);
            }
        }

        /// <summary>A tapering curved bone tusk through the points, thick at the root.</summary>
        public static void Tusk(MeshKit k, Vector3[] path, float rootRadius, float tipRadius, Color color)
        {
            for (int i = 0; i < path.Length - 1; i++)
            {
                float r0 = Mathf.Lerp(rootRadius, tipRadius, (float)i / (path.Length - 1));
                float r1 = Mathf.Lerp(rootRadius, tipRadius, (float)(i + 1) / (path.Length - 1));
                k.Rod(path[i], path[i + 1], r0, 10, color, 0f, r1);
                if (i > 0) k.Ball(path[i], r0, color, 1);
            }
            k.Ball(path[path.Length - 1], tipRadius, color, 1);
        }

        /// <summary>A giant teal leaf from root to tip, its face toward <paramref name="normal"/>, with a pale midrib and veins.</summary>
        public static void LeafBlade(MeshKit k, Vector3 root, Vector3 tip, Vector3 normal, float width, Color color, Color vein)
        {
            Vector3 along = tip - root;
            float len = along.magnitude;
            Quaternion r = Quaternion.LookRotation(along, normal);
            k.Ball(root + along * 0.5f, new Vector3(width * 0.5f, Mathf.Max(0.008f, width * 0.04f), len * 0.5f), r, color, 1);
            k.Rod(root, tip - along * 0.04f, Mathf.Max(0.006f, width * 0.035f), 5, vein);
            Vector3 side = r * Vector3.right, up = r * Vector3.up;
            for (int i = 1; i <= 3; i++)
            {
                Vector3 on = root + along * (0.2f + i * 0.17f) + up * width * 0.03f;
                foreach (float s in new[] { -1f, 1f })
                    k.Rod(on, on + side * s * width * 0.32f + along.normalized * width * 0.18f, Mathf.Max(0.004f, width * 0.018f), 4, vein);
            }
        }

        /// <summary>A hull plate off the ship (the orange stripe, rivets), facing <paramref name="normal"/>, its height along <paramref name="up"/>.</summary>
        public static void Plate(MeshKit k, Vector3 centre, Vector3 normal, Vector3 up, float width, float height, Color color, bool stripe = true)
        {
            k.ScrapPanel(centre, new Vector3(width, height, 0.02f), Quaternion.LookRotation(normal, up), color, stripe);
        }

        /// <summary>A glowing seed-pod lamp, facing +Z of the rotation, in a little scrap cup.</summary>
        public static void SeedLamp(MeshKit k, Vector3 at, Quaternion rotation, float size, Color glow)
        {
            k.Prism(at - rotation * Vector3.forward * size * 0.6f, rotation * Quaternion.Euler(90f, 0f, 0f), size * 0.9f, size * 1.15f, size * 0.7f, 12, MudPalette.Hull);
            k.Ball(at + rotation * Vector3.forward * size * 0.25f, new Vector3(size, size, size * 0.8f), rotation, glow, 1, 3f);
            for (int i = 0; i < 5; i++) // the pod's dried petals round it
            {
                Quaternion around = rotation * Quaternion.Euler(0f, 0f, i * 72f);
                k.Ball(at + around * new Vector3(0f, size * 1.15f, size * 0.1f), new Vector3(size * 0.3f, size * 0.5f, size * 0.1f), around * Quaternion.Euler(-30f, 0f, 0f), BoneDark, 0);
            }
        }

        /// <summary>A gold cheese wedge with holes, facing +Z of the rotation (Clutch's buckle and back patch).</summary>
        public static void CheeseWedge(MeshKit k, Vector3 at, Quaternion rotation, float size, float depth, Color cheese, Color hole)
        {
            Vector3 a = at + rotation * new Vector3(-size, -size * 0.55f, 0f), b = at + rotation * new Vector3(size, -size * 0.55f, 0f), c = at + rotation * new Vector3(-size * 0.6f, size * 0.6f, 0f);
            Vector3 back = rotation * Vector3.back * depth, front = rotation * Vector3.forward;
            Side(k, a, b, c, c, front, cheese);
            Side(k, a + back, b + back, c + back, c + back, -front, cheese);
            Side(k, a, b, b + back, a + back, rotation * Vector3.down, hole);
            Side(k, b, c, c + back, b + back, rotation * new Vector3(1f, 1f, 0f), hole);
            Side(k, c, a, a + back, c + back, rotation * Vector3.left, hole);
            foreach (Vector3 spot in new[] { new Vector3(-0.35f, -0.15f, 0f), new Vector3(0.25f, -0.25f, 0f), new Vector3(-0.45f, 0.3f, 0f) })
                k.Ball(at + rotation * (spot * size + Vector3.forward * 0.002f), new Vector3(size * 0.14f, size * 0.14f, size * 0.05f), rotation, hole, 0);
        }

        /// <summary>A quad wound to face a direction.</summary>
        public static void Side(MeshKit k, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward, Color color)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) >= 0f) k.Quad(a, b, c, d, color);
            else k.Quad(d, c, b, a, color);
        }

        // =====================================================================================================
        // Clutch's hovercycle
        // =====================================================================================================

        /// <summary>
        /// Clutch's long, low hovercycle. It keeps the old chopper's rider points (bars, saddle, pegs, pipes, tail
        /// light), so every pose still fits: a swamp-log keel under a ship fuel cell (the engine), a purple-painted
        /// gourd tank with the flames, two long bone tusks out front with glowing tips, purple hull-plate cowls, leaf
        /// fins at the back, and two lift rings: a bucket rim under the steering, and the ship's landing-gear tyre laid
        /// flat under the tail as the big rear lift ring (still the stage's part). Built low-poly, in a few big
        /// faceted shapes like the desert bosses, since 11-19.
        /// </summary>
        public static class Clutch
        {
            public static readonly Vector3 RearAxle = new Vector3(0f, 0.52f, -1.05f), FrontAxle = new Vector3(0f, 0.42f, 1.72f),
                SteerHead = new Vector3(0f, 1.1f, 0.78f), Seat = new Vector3(0f, 0.86f, -0.24f), PegAt = new Vector3(0.3f, 0.36f, 0.36f),
                PipeEndLow = new Vector3(0.29f, 0.5f, -1.42f), PipeEndHigh = new Vector3(0.31f, 0.64f, -1.4f), TailLightAt = new Vector3(0f, 0.88f, -1.55f);
            /// <summary>The landing-gear lift ring's centre (it lies flat under the tail) and the front ring's.</summary>
            public static readonly Vector3 RearPod = new Vector3(0f, 0.32f, -1.05f), FrontPod = new Vector3(0f, 0.3f, 1.72f);
            public const float RingRadius = 0.39f, RingTube = 0.13f, FrontRadius = 0.22f, FrontTube = 0.06f;
            /// <summary>How far the bike sticks out sideways at the back, and how high that edge is: laid over, it rests on it.</summary>
            public const float SideReach = RingRadius + RingTube, SideClearance = 0.19f;
            public static Vector3 ForkAxis => (FrontAxle - SteerHead).normalized;

            // Swamp palette (11-19): a deep swamp-violet paint job and ember flames, so the bike sits in the Mudlands.
            public static readonly Color Paint = new Color(0.3f, 0.2f, 0.38f), PaintHi = new Color(0.42f, 0.3f, 0.5f),
                Chrome = new Color(0.82f, 0.83f, 0.86f), Leather = new Color(0.1f, 0.09f, 0.1f), LeatherHi = new Color(0.2f, 0.18f, 0.19f),
                Hide = new Color(0.36f, 0.24f, 0.3f), HideDark = new Color(0.27f, 0.17f, 0.22f),
                FlameRed = new Color(0.8f, 0.3f, 0.14f), FlameOrange = new Color(0.9f, 0.5f, 0.16f), FlameYellow = new Color(0.92f, 0.78f, 0.3f),
                Cheese = new Color(0.86f, 0.7f, 0.3f), CheeseHole = new Color(0.7f, 0.52f, 0.16f), Hub = new Color(0.3f, 0.85f, 1f),
                Lamp = new Color(1f, 0.93f, 0.7f), TailRed = new Color(1f, 0.12f, 0.08f);

            /// <summary>The keel, struts, cowls, tusks, fins, engine cell, pipes and pegs (the fixed body). Low-poly since 11-19.</summary>
            public static void Body(MeshKit k)
            {
                // The keel: a swamp log tapering to a nose knob under the cowls.
                k.Rod(new Vector3(0f, 0.35f, -0.52f), new Vector3(0f, 0.4f, 0.95f), 0.13f, 6, Bark, 0f, 0.11f);
                k.Rod(new Vector3(0f, 0.4f, 0.95f), new Vector3(0f, 0.47f, 1.42f), 0.11f, 6, Bark, 0f, 0.06f);
                k.Ball(new Vector3(0f, 0.47f, 1.42f), 0.07f, BarkDark, 0);

                // The frame: bent ship struts (hull white) from the steering head down to the log, back to the saddle.
                Vector3 steer = SteerHead;
                k.Rod(steer + new Vector3(0f, 0.08f, 0f), steer + ForkAxis * 0.12f, 0.075f, 6, MudPalette.HullDark);
                k.Rod(steer + new Vector3(0f, -0.02f, -0.04f), new Vector3(0f, 0.9f, -0.12f), 0.055f, 5, MudPalette.Hull);
                foreach (float x in new[] { -0.08f, 0.08f })
                {
                    k.Rod(steer + new Vector3(x, -0.06f, -0.03f), new Vector3(x * 1.1f, 0.46f, 0.62f), 0.04f, 4, MudPalette.Hull);
                    k.Rod(new Vector3(x, 0.88f, -0.14f), new Vector3(x * 1.5f, 0.82f, -1.42f), 0.035f, 4, MudPalette.Hull); // tail rails over the ring
                }
                k.Rod(new Vector3(0f, 0.9f, -0.12f), new Vector3(0f, 0.44f, -0.36f), 0.05f, 5, MudPalette.Hull); // seat post
                Lash(k, new Vector3(0f, 0.46f, 0.6f), Vector3.forward, 0.12f);

                // The engine: a salvaged ship fuel cell on its side, orange band and dark end caps, a glowing gauge.
                Quaternion along = Quaternion.Euler(90f, 0f, 0f);
                k.Prism(new Vector3(0f, 0.58f, -0.4f), along, 0.18f, 0.18f, 0.72f, 6, MudPalette.Hull);
                k.Prism(new Vector3(0f, 0.58f, -0.08f), along, 0.19f, 0.19f, 0.1f, 6, MudPalette.HullStripe);
                k.Box(new Vector3(-0.17f, 0.6f, 0.12f), new Vector3(0.03f, 0.09f, 0.18f), MudPalette.ShipGlow, 2f);

                // Exhaust: two scrap pipes swept back over the lift ring to flared ends (where the flames come out).
                Pipe(k, new[] { new Vector3(0.1f, 0.6f, -0.38f), new Vector3(0.26f, 0.55f, -0.7f), PipeEndLow });
                Pipe(k, new[] { new Vector3(0.06f, 0.7f, -0.38f), new Vector3(0.24f, 0.69f, -0.75f), PipeEndHigh });
                foreach (Vector3 end in new[] { PipeEndLow, PipeEndHigh })
                    k.Prism(end, Quaternion.Euler(-90f, 0f, 0f), 0.05f, 0.085f, 0.16f, 5, Bone);

                foreach (float s in new[] { -1f, 1f })
                {
                    // Two long bone tusks out front (the speeder's prongs), sheathed in purple plate, glowing at the tips.
                    Vector3 root = new Vector3(0.1f * s, 0.42f, 0.62f), mid = new Vector3(0.34f * s, 0.38f, 1.6f), tip = new Vector3(0.3f * s, 0.56f, 2.42f);
                    k.Rod(root, mid, 0.08f, 5, Bone, 0f, 0.06f);
                    k.Rod(mid, tip, 0.06f, 5, Bone, 0f, 0.028f);
                    k.Box(new Vector3(0.3f * s, 0.43f, 1.35f), new Vector3(0.04f, 0.17f, 0.55f), Quaternion.Euler(0f, 8f * s, 0f), Paint);
                    k.Ball(tip + new Vector3(0f, 0.01f, 0.01f), 0.055f, MudPalette.Pod, 0, 2.5f);
                    // The nose cowls: purple hull plate either side of the steering, with a painted flame.
                    Quaternion cowl = Quaternion.LookRotation(new Vector3(s, 0.35f, 0.15f), new Vector3(0f, 1f, -0.25f));
                    k.Box(new Vector3(0.17f * s, 0.66f, 0.98f), new Vector3(0.62f, 0.34f, 0.03f), cowl, Paint);
                    k.Box(new Vector3(0.19f * s, 0.62f, 0.98f), new Vector3(0.4f, 0.06f, 0.03f), cowl * Quaternion.Euler(0f, 0f, -6f * s), FlameOrange);
                    k.Box(new Vector3(0.195f * s, 0.69f, 0.95f), new Vector3(0.28f, 0.05f, 0.03f), cowl * Quaternion.Euler(0f, 0f, -6f * s), FlameYellow);
                    // A leaf fin at the back, swept up and out (the speeder's wing).
                    Vector3 finRoot = new Vector3(0.22f * s, 0.74f, -0.9f), finTip = new Vector3(0.74f * s, 1.02f, -1.5f);
                    k.Ball((finRoot + finTip) * 0.5f, new Vector3(0.17f, 0.015f, (finTip - finRoot).magnitude * 0.5f), Quaternion.LookRotation(finTip - finRoot, new Vector3(0.25f * s, 1f, 0.15f)), Leaf, 0);
                    // A peg for his boot.
                    Vector3 peg = new Vector3(PegAt.x * s, PegAt.y, PegAt.z);
                    k.Rod(new Vector3(0.08f * s, 0.36f, PegAt.z - 0.02f), peg + new Vector3(0.1f * s, 0f, 0f), 0.03f, 4, MudPalette.Metal);
                }
            }

            private static void Pipe(MeshKit k, Vector3[] path)
            {
                for (int i = 0; i < path.Length - 1; i++) k.Rod(path[i], path[i + 1], 0.045f, 5, MudPalette.Metal);
            }

            /// <summary>A teardrop gourd painted purple, with the flames, the speedo and the nitro button.</summary>
            public static void Tank(MeshKit k)
            {
                k.Ball(new Vector3(0f, 1.02f, 0.38f), new Vector3(0.19f, 0.15f, 0.35f), Quaternion.Euler(-6f, 0f, 0f), Paint, 1);
                k.Rod(new Vector3(0f, 1.13f, 0.72f), new Vector3(0f, 1.21f, 0.82f), 0.035f, 4, Gourd); // the stalk
                foreach (float s in new[] { -1f, 1f }) // the flames
                {
                    Quaternion lick = Quaternion.Euler(-4f, 0f, 0f);
                    k.Ball(new Vector3(0.155f * s, 1.02f, 0.44f), new Vector3(0.03f, 0.045f, 0.16f), lick, FlameRed, 0);
                    k.Ball(new Vector3(0.165f * s, 1.02f, 0.46f), new Vector3(0.025f, 0.025f, 0.11f), lick, FlameYellow, 0);
                }
                k.Ball(new Vector3(0f, 1.15f, 0.25f), new Vector3(0.065f, 0.03f, 0.065f), Quaternion.identity, MudPalette.ShipGlow, 0, 1.5f); // the speedo
                k.Prism(new Vector3(0f, 1.13f, 0.17f), Quaternion.identity, 0.045f, 0.045f, 0.045f, 5, MudPalette.DarkMetal);
                k.Ball(new Vector3(0f, 1.18f, 0.17f), new Vector3(0.035f, 0.018f, 0.035f), Quaternion.identity, new Color(1f, 0.15f, 0.12f), 0, 1.2f);
            }

            /// <summary>The hide saddle with a pillion hump, the bone sissy bar, the tail cowl, light and plate.</summary>
            public static void BackEnd(MeshKit k)
            {
                k.Ball(Seat + new Vector3(0f, -0.06f, 0f), new Vector3(0.22f, 0.08f, 0.31f), Quaternion.identity, Hide, 1);
                k.Ball(new Vector3(0f, 0.86f, -0.66f), new Vector3(0.16f, 0.08f, 0.17f), Quaternion.Euler(-10f, 0f, 0f), HideDark, 0);
                // The sissy bar: two bones up and a cross bone, a leather pad.
                foreach (float x in new[] { -0.12f, 0.12f })
                    k.Rod(new Vector3(x, 0.84f, -0.8f), new Vector3(x * 0.8f, 1.72f, -0.98f), 0.035f, 4, Bone, 0f, 0.025f);
                k.Rod(new Vector3(-0.11f, 1.72f, -0.98f), new Vector3(0.11f, 1.72f, -0.98f), 0.03f, 4, Bone);
                k.Box(new Vector3(0f, 1.25f, -0.88f), new Vector3(0.24f, 0.32f, 0.06f), Quaternion.Euler(-12f, 0f, 0f), Leather);
                // A hull-plate tail cowl over the ring, with the tail light and a plate.
                Quaternion cowl = Quaternion.LookRotation(new Vector3(0f, 1f, -0.35f), Vector3.forward);
                k.Box(new Vector3(0f, 0.86f, -1.36f), new Vector3(0.32f, 0.36f, 0.03f), cowl, Paint);
                k.Ball(TailLightAt, new Vector3(0.08f, 0.045f, 0.045f), Quaternion.identity, TailRed, 0, 2.5f);
                k.Box(TailLightAt + new Vector3(0f, -0.12f, 0.03f), new Vector3(0.2f, 0.12f, 0.02f), Quaternion.Euler(-30f, 0f, 0f), new Color(0.95f, 0.85f, 0.2f));
            }

            /// <summary>
            /// The landing gear (built round <see cref="RearPod"/>, the stage's ship part): the fat aircraft tyre laid flat
            /// as the rear lift ring, its tread out, the bolted hub with the glowing core in the middle, the lift glow
            /// under it, and the oleo struts it hangs from.
            /// </summary>
            public static void LandingGear(MeshKit k)
            {
                Torus(k, Vector3.zero, Quaternion.identity, RingRadius, RingTube, MudPalette.Rubber);
                for (int i = 0; i < 8; i++) // chunky aircraft tread, round the outside
                {
                    Quaternion spin = Quaternion.Euler(0f, i * 45f + 22.5f, 0f);
                    k.Box(spin * new Vector3(0f, 0f, RingRadius + RingTube - 0.005f), new Vector3(0.1f, 0.2f, 0.05f), spin, MudPalette.Rubber);
                }
                k.Prism(new Vector3(0f, -RingTube - 0.014f, 0f), Quaternion.identity, RingRadius + 0.02f, RingRadius + 0.02f, 0.014f, 8, Lift, 2.2f);
                k.Prism(new Vector3(0f, -0.07f, 0f), Quaternion.identity, 0.12f, 0.1f, 0.15f, 6, MudPalette.Metal);
                k.Prism(new Vector3(0f, -0.1f, 0f), Quaternion.identity, 0.07f, 0.07f, 0.24f, 5, Hub, 3f);
                for (int i = 0; i < 3; i++)
                {
                    Vector3 dir = Quaternion.Euler(0f, 30f + i * 120f, 0f) * Vector3.forward;
                    k.Rod(dir * 0.1f + Vector3.up * 0.04f, dir * (RingRadius - 0.08f), 0.022f, 4, MudPalette.Metal);
                }
                // The oleo struts: chrome over a dark sleeve, from the ring up to the tail rails.
                foreach (float s in new[] { -1f, 1f })
                {
                    Vector3 foot = new Vector3(0.3f * s, 0.08f, 0.16f);
                    k.Rod(foot, foot + new Vector3(-0.08f * s, 0.24f, 0.08f), 0.05f, 5, MudPalette.DarkMetal);
                    k.Rod(foot + new Vector3(-0.07f * s, 0.2f, 0.07f), new Vector3(0.12f * s, 0.5f, 0.27f), 0.035f, 5, Chrome);
                }
            }

            /// <summary>The rear lift fan (spins about Y inside the landing-gear ring): five teal leaf blades.</summary>
            public static void RearFan(MeshKit k) => ChunkyFan(k, 5, 0.1f, RingRadius - 0.1f, 0.015f);

            /// <summary>Leaf blades round local +Y, pitched like a propeller: one faceted leaf each.</summary>
            private static void ChunkyFan(MeshKit k, int blades, float hub, float radius, float thickness)
            {
                for (int i = 0; i < blades; i++)
                {
                    Quaternion spin = Quaternion.Euler(0f, i * 360f / blades, 0f);
                    float len = radius - hub;
                    k.Ball(spin * new Vector3(0f, 0f, hub + len * 0.5f), new Vector3(len * 0.24f, thickness, len * 0.5f), spin * Quaternion.Euler(0f, 0f, 28f), Leaf, 0);
                }
            }

            /// <summary>
            /// The steering (built round <see cref="SteerHead"/>, turning about <see cref="ForkAxis"/>): a triple clamp,
            /// two hull struts down to the front lift ring (a bucket rim) with a little mouse skull on its nose, the
            /// seed-pod headlight, and his ape-hangers.
            /// </summary>
            public static void FrontEnd(MeshKit k)
            {
                Vector3 pod = FrontPod - SteerHead, axis = ForkAxis;
                k.Box(new Vector3(0f, 0.05f, 0.02f), new Vector3(0.3f, 0.05f, 0.13f), MudPalette.HullDark);
                foreach (float x in new[] { -0.11f, 0.11f })
                    k.Rod(new Vector3(x, 0.07f, 0.03f), pod + new Vector3(x * 1.2f, FrontTube + 0.02f, -0.04f), 0.035f, 4, MudPalette.Hull);
                // The front lift ring: a rusty bucket rim, the lift glow under it.
                LiftRing(k, pod, FrontRadius, FrontTube, MudPalette.Metal, 0.06f);
                // A little mouse skull (real bone) on the ring's nose.
                Vector3 skull = pod + new Vector3(0f, 0.07f, FrontRadius + 0.05f);
                k.Ball(skull, new Vector3(0.05f, 0.045f, 0.065f), Quaternion.identity, Bone, 0);
                foreach (float x in new[] { -0.03f, 0.03f })
                    k.Ball(skull + new Vector3(x, 0.015f, 0.045f), 0.015f, MudPalette.Black, 0);
                // The headlight: a glowing seed pod in a scrap cup.
                Vector3 lamp = new Vector3(0f, -0.1f, 0.26f);
                k.Prism(lamp + new Vector3(0f, 0f, -0.07f), Quaternion.Euler(90f, 0f, 0f), 0.1f, 0.13f, 0.08f, 6, MudPalette.Hull);
                k.Ball(lamp + new Vector3(0f, 0f, 0.02f), new Vector3(0.11f, 0.11f, 0.09f), Quaternion.identity, Lamp, 0, 3f);
                k.Rod(new Vector3(0f, 0.02f, 0.04f), lamp + new Vector3(0f, 0f, -0.06f), 0.02f, 4, MudPalette.Metal);
                // Ape-hanger bars: risers, tall uprights, back to the rubber grips.
                foreach (float s in new[] { -1f, 1f })
                {
                    Vector3 riser = new Vector3(0.09f * s, 0.06f, 0.0f), up = new Vector3(0.2f * s, 0.6f, -0.3f), grip = new Vector3(0.44f * s, 0.59f, -0.41f);
                    k.Rod(riser, up, 0.028f, 4, MudPalette.Metal);
                    k.Rod(up, grip, 0.028f, 4, MudPalette.Metal);
                    k.Rod(grip, grip + new Vector3(0.14f * s, -0.01f, -0.01f), 0.038f, 5, MudPalette.Rubber);
                }
                k.Rod(new Vector3(-0.2f, 0.6f, -0.3f), new Vector3(0.2f, 0.6f, -0.3f), 0.022f, 4, MudPalette.Metal); // cross bar
            }

            /// <summary>The front lift fan (spins about Y at <see cref="FrontPod"/>): four leaf blades.</summary>
            public static void FrontFan(MeshKit k) => ChunkyFan(k, 4, 0.06f, FrontRadius - 0.04f, 0.012f);
        }

        // =====================================================================================================
        // The gang's little hovers
        // =====================================================================================================

        /// <summary>
        /// The biker mice's small hovers, in three kinds (each on two little lift rings, front and back; the rider
        /// sits at about (0, 0.5, -0.1)): 0, the <b>longhorn</b> (was the chopper): a log keel with two long bone
        /// prongs, a painted gourd tank and tall bars; 1, the <b>skipper</b> (was the dirt bike): a shell of painted
        /// hull plate with a number plate and a big leaf fin; 2, the <b>tub</b> (was the scooter): a round gourd body,
        /// a leaf legshield with a seed-pod lamp, and a fan in a bucket on its back.
        /// </summary>
        public static class Mini
        {
            public const float PodRadius = 0.14f, PodTube = 0.045f, PodHeight = 0.18f, RearZ = -0.45f;

            public static float FrontZ(int kind) => kind == 0 ? 0.75f : 0.55f;

            public static void Body(MeshKit k, int kind, Color paint)
            {
                k.Smooth = false;
                float front = FrontZ(kind);
                switch (kind)
                {
                    case 0: // longhorn: log keel, bone prongs, gourd tank, tall bars
                        k.Rod(new Vector3(0f, 0.3f, -0.5f), new Vector3(0f, 0.34f, 0.42f), 0.085f, 10, Bark, 0f, 0.07f);
                        k.Ball(new Vector3(0f, 0.3f, -0.5f), new Vector3(0.085f, 0.085f, 0.03f), Quaternion.identity, BoneDark, 0);
                        k.Ball(new Vector3(0f, 0.47f, 0.12f), new Vector3(0.13f, 0.1f, 0.2f), Quaternion.identity, paint, 1);
                        k.Ball(new Vector3(0f, 0.52f, 0.12f), new Vector3(0.07f, 0.05f, 0.14f), Quaternion.identity, Color.Lerp(paint, Color.white, 0.25f), 1);
                        foreach (float s in new[] { -1f, 1f })
                            Tusk(k, new[] { new Vector3(0.05f * s, 0.32f, 0.3f), new Vector3(0.15f * s, 0.28f, 0.7f), new Vector3(0.17f * s, 0.32f, 1.0f), new Vector3(0.14f * s, 0.4f, 1.14f) }, 0.04f, 0.015f, Bone);
                        foreach (float s in new[] { -1f, 1f })
                            k.Ball(new Vector3(0.14f * s, 0.41f, 1.15f), 0.025f, MudPalette.Pod, 0, 2.5f);
                        k.Rod(new Vector3(0f, 0.55f, 0.32f), new Vector3(0f, PodHeight + PodTube, front), 0.025f, 5, MudPalette.Hull);
                        k.Rod(new Vector3(-0.18f, 0.95f, 0.25f), new Vector3(0.18f, 0.95f, 0.25f), 0.025f, 5, MudPalette.Metal);
                        k.Rod(new Vector3(0f, 0.55f, 0.3f), new Vector3(0f, 0.95f, 0.25f), 0.025f, 5, MudPalette.Metal);
                        Lash(k, new Vector3(0f, 0.33f, 0.3f), Vector3.forward, 0.08f);
                        break;
                    case 1: // skipper: a shell of painted hull plate, number plate, leaf fin
                        k.Plank(new Vector3(0f, 0.3f, -0.48f), new Vector3(0f, 0.34f, 0.36f), 0.18f, 0.06f, MudPalette.HullDark);
                        foreach (float s in new[] { -1f, 1f })
                            Plate(k, new Vector3(0.1f * s, 0.43f, -0.05f), new Vector3(s, 0.3f, 0f), Vector3.up, 0.7f, 0.2f, paint);
                        Plate(k, new Vector3(0f, 0.5f, 0.1f), new Vector3(0f, 1f, 0.2f), Vector3.forward, 0.18f, 0.45f, paint, false);
                        Plate(k, new Vector3(0f, 0.62f, 0.47f), new Vector3(0f, 0.15f, 1f), Vector3.up, 0.18f, 0.16f, MudPalette.Hull);
                        k.Ball(new Vector3(0f, 0.62f, 0.485f), new Vector3(0.035f, 0.05f, 0.006f), Quaternion.identity, MudPalette.Black, 0); // its number
                        LeafBlade(k, new Vector3(0f, 0.5f, -0.3f), new Vector3(0f, 0.85f, -0.62f), Vector3.right, 0.22f, Leaf, LeafLight);
                        k.Rod(new Vector3(0f, 0.5f, 0.3f), new Vector3(0f, PodHeight + PodTube, front), 0.03f, 5, MudPalette.Hull);
                        k.Rod(new Vector3(-0.2f, 0.75f, 0.32f), new Vector3(0.2f, 0.75f, 0.32f), 0.025f, 5, MudPalette.Black);
                        k.Rod(new Vector3(0f, 0.5f, 0.3f), new Vector3(0f, 0.75f, 0.32f), 0.025f, 5, MudPalette.Black);
                        break;
                    default: // tub: gourd body, leaf legshield with a seed-pod lamp, bucket fan
                        k.Ball(new Vector3(0f, 0.42f, -0.2f), new Vector3(0.2f, 0.18f, 0.32f), Quaternion.identity, Gourd, 1);
                        foreach (float x in new[] { -0.12f, 0f, 0.12f })
                            k.Ball(new Vector3(x, 0.44f, -0.2f), new Vector3(0.035f, 0.17f, 0.31f), Quaternion.identity, GourdRib, 0);
                        k.Prism(new Vector3(0f, 0.3f, -0.2f), Quaternion.identity, 0.205f, 0.205f, 0.06f, 12, paint); // a painted band
                        k.Plank(new Vector3(0f, 0.24f, -0.05f), new Vector3(0f, 0.24f, 0.4f), 0.22f, 0.05f, Bark);
                        LeafBlade(k, new Vector3(0f, 0.24f, 0.42f), new Vector3(0f, 0.78f, 0.47f), new Vector3(0f, 0.1f, 1f), 0.3f, Leaf, LeafLight);
                        SeedLamp(k, new Vector3(0f, 0.7f, 0.5f), Quaternion.identity, 0.045f, new Color(1f, 0.95f, 0.7f));
                        k.Rod(new Vector3(-0.18f, 0.8f, 0.4f), new Vector3(0.18f, 0.8f, 0.4f), 0.025f, 5, MudPalette.Metal);
                        k.Rod(new Vector3(0f, 0.8f, 0.4f), new Vector3(0f, PodHeight + PodTube, front), 0.025f, 5, MudPalette.Metal);
                        // The bucket on its back with a fan in it.
                        k.Prism(new Vector3(0f, 0.45f, -0.5f), Quaternion.Euler(-90f, 0f, 0f), 0.12f, 0.14f, 0.16f, 12, MudPalette.Metal);
                        k.Ball(new Vector3(0.06f, 0.5f, -0.58f), new Vector3(0.04f, 0.03f, 0.05f), Quaternion.identity, Rust, 0);
                        Fan(k, 4, 0.02f, 0.12f, 0.008f, Leaf, LeafLight, new Vector3(0f, 0.45f, -0.6f), Quaternion.Euler(-90f, 0f, 0f));
                        break;
                }
                // Struts down to the back ring, and an exhaust can.
                k.Rod(new Vector3(0f, 0.32f, -0.3f), new Vector3(0.08f, PodHeight + PodTube, RearZ + 0.05f), 0.02f, 5, MudPalette.Hull);
                k.Rod(new Vector3(0f, 0.32f, -0.3f), new Vector3(-0.08f, PodHeight + PodTube, RearZ + 0.05f), 0.02f, 5, MudPalette.Hull);
                k.Rod(new Vector3(0.08f, 0.32f, -0.45f), new Vector3(0.1f, 0.38f, -0.7f), 0.03f, 6, MudPalette.Metal);
                k.Ball(new Vector3(0.1f, 0.38f, -0.7f), 0.03f, Bone, 0);
            }

            /// <summary>A little lift ring (at its own origin; it sits at <see cref="PodHeight"/>).</summary>
            public static void Pod(MeshKit k) => LiftRing(k, Vector3.zero, PodRadius, PodTube, MudPalette.Metal, 0.035f);

            /// <summary>Its fan (spins about Y).</summary>
            public static void PodFan(MeshKit k) => Fan(k, 4, 0.035f, PodRadius - 0.02f, 0.008f, Leaf, LeafLight);
        }
    }
}
