using System;
using UnityEngine;

namespace SignalLost.Mudlands
{
    /// <summary>
    /// The Mudlands' scenery and props, built from low-poly shapes (no imported art), in the spirit of the two
    /// reference images: crooked stilt shacks with glowing windows, plank boardwalks, cypress and willow trees with
    /// hanging moss, cattails, lily pads with purple flowers, glowing mushrooms, tiki torches and lanterns. Every model
    /// stands on y = 0 facing +Z. <see cref="Build"/> fills a <see cref="MeshKit"/>; <see cref="MudProp"/> and
    /// <see cref="MudScenery"/> turn them into objects.
    /// </summary>
    public static class MudModels
    {
        public enum Kind
        {
            // Scenery (combined by MudScenery)
            Cypress, Willow, DeadTree, Cattails, LilyPads, Mushrooms, Rock, Boulder, Stump, Log, Reeds,
            // Buildings and set pieces (MudProp)
            StiltShack, Cabin, DrivingSchool, Kiosk, Sawmill, Outhouse, Barricade, Fence, Crate, HayBale, MoonshineBarrel, MoonshineStill,
            Dam, Gong, CursedLantern, LanternPost, TikiTorch, StepStone, Bridge, Boardwalk, TyreWall, Grandstand, BananaStand,
            Cauldron, WitchHut, DojoGate, StoneLantern, Roadhouse, GasPump, Ramp, GatePost, CountyLine, Windmill, RoadSign, Rowboat,
            Island
        }

        // Alien foliage (teal leaves, violet hanging moss); windows glow mint like the biolights.
        private static readonly Color Leaf = new Color(0.16f, 0.4f, 0.42f), LeafDark = new Color(0.12f, 0.27f, 0.36f),
            MossHang = new Color(0.52f, 0.4f, 0.64f), Window = MudPalette.Pod,
            Copper = new Color(0.72f, 0.42f, 0.22f), Orange = new Color(0.93f, 0.45f, 0.12f), Bronze = new Color(0.72f, 0.55f, 0.25f),
            Banana = new Color(1f, 0.86f, 0.25f), Teal = new Color(0.3f, 0.95f, 0.85f);
        // Alien wood and bark (lavender-grey, violet-brown) for everything built or grown here, and the swamp folk's
        // building stuff: dried pod husk, pale ribs, and giant mushroom caps with pink gills and glowing spots.
        private static readonly Color Wood = new Color(0.52f, 0.45f, 0.58f), DarkWood = new Color(0.32f, 0.26f, 0.38f),
            GreyWood = new Color(0.62f, 0.6f, 0.68f), Bark = new Color(0.28f, 0.22f, 0.32f),
            Husk = new Color(0.56f, 0.5f, 0.62f), HuskDark = new Color(0.36f, 0.3f, 0.44f), Rib = new Color(0.84f, 0.8f, 0.7f),
            Gill = new Color(0.86f, 0.7f, 0.78f), Spot = new Color(0.75f, 1f, 0.88f), Gourd = new Color(0.86f, 0.55f, 0.22f);
        private static readonly Color[] Caps =
        {
            new Color(0.2f, 0.5f, 0.52f), new Color(0.5f, 0.3f, 0.62f), new Color(0.72f, 0.3f, 0.45f), new Color(0.25f, 0.42f, 0.62f)
        };

        /// <summary>Builds a kind (variant picks between looks, size scales lengths where it applies).</summary>
        public static void Build(MeshKit k, Kind kind, int variant, float size = 1f)
        {
            var random = new System.Random(variant * 7919 + (int)kind * 104729);
            float R(float a, float b) => a + (float)random.NextDouble() * (b - a);
            switch (kind)
            {
                case Kind.Cypress:
                {
                    float h = R(9f, 13f);
                    k.Lathe(Vector3.zero, Quaternion.identity, new[] { new Vector2(1.4f, 0f), new Vector2(0.75f, 0.9f), new Vector2(0.5f, 2.5f), new Vector2(0.38f, h), new Vector2(0.0001f, h + 0.2f) }, 7, Bark);
                    for (int i = 0; i < 4; i++) // knees poking out of the mud
                    {
                        Vector3 knee = new Vector3(R(-2f, 2f), 0f, R(-2f, 2f));
                        float tall = R(0.4f, 0.8f);
                        k.Cone(knee, Quaternion.identity, 0.15f, tall, 5, Bark);
                        k.Ball(knee + Vector3.up * tall, 0.07f, Spot, 0, 1.6f); // a glowing bud on each knee
                    }
                    for (int i = 0; i < 3; i++)
                    {
                        float y = h - 1.5f - i * 2f;
                        k.Ball(new Vector3(R(-0.6f, 0.6f), y, R(-0.6f, 0.6f)), new Vector3(R(2.6f, 3.4f) - i * 0.3f, 0.9f, R(2.6f, 3.4f) - i * 0.3f), Quaternion.Euler(0f, R(0f, 360f), 0f), i % 2 == 0 ? Leaf : LeafDark, 1);
                    }
                    for (int i = 0; i < 9; i++) Moss(k, new Vector3(R(-2.6f, 2.6f), h - 2f - R(0f, 3f), R(-2.6f, 2.6f)), R(1.2f, 2.8f));
                    break;
                }
                case Kind.Willow:
                {
                    // The reference's twisted tree: a leaning, gnarled trunk with drooping moss curtains.
                    Vector3[] trunk = { Vector3.zero, new Vector3(0.6f, 2.5f, 0.2f), new Vector3(-0.3f, 5f, 0.6f), new Vector3(0.8f, 7f, -0.2f) };
                    for (int i = 0; i < trunk.Length - 1; i++) k.Rod(trunk[i], trunk[i + 1], 1f - i * 0.25f, 7, Bark, 0f, 0.8f - i * 0.22f);
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i / 5f * Mathf.PI * 2f + R(0f, 0.5f);
                        Vector3 tip = trunk[3] + new Vector3(Mathf.Cos(a) * R(3f, 4.5f), R(-0.5f, 1.2f), Mathf.Sin(a) * R(3f, 4.5f));
                        k.Rod(trunk[2] + Vector3.up, tip, 0.35f, 5, Bark, 0f, 0.12f);
                        k.Ball(tip + Vector3.up * 0.4f, new Vector3(1.8f, 0.7f, 1.8f), Quaternion.identity, Leaf, 0);
                        if (i % 2 == 0) // a glowing seed pod hanging on a strand
                        {
                            k.Rod(tip, tip + Vector3.down * 0.9f, 0.025f, 3, Rib);
                            k.Ball(tip + Vector3.down * 1.05f, new Vector3(0.16f, 0.22f, 0.16f), Quaternion.identity, Spot, 0, 1.6f);
                        }
                        for (int m = 0; m < 3; m++) Moss(k, tip + new Vector3(R(-1.4f, 1.4f), 0f, R(-1.4f, 1.4f)), R(2f, 3.5f));
                    }
                    break;
                }
                case Kind.DeadTree:
                {
                    float h = R(5f, 8f);
                    k.Rod(Vector3.zero, new Vector3(R(-0.5f, 0.5f), h, R(-0.5f, 0.5f)), 0.45f, 6, GreyWood, 0f, 0.15f);
                    for (int i = 0; i < 4; i++)
                    {
                        float y = R(h * 0.4f, h * 0.9f), a = R(0f, Mathf.PI * 2f);
                        Vector3 from = new Vector3(0f, y, 0f), to = from + new Vector3(Mathf.Cos(a) * R(1.5f, 2.5f), R(0.5f, 2f), Mathf.Sin(a) * R(1.5f, 2.5f));
                        k.Rod(from, to, 0.16f, 5, GreyWood, 0f, 0.04f);
                    }
                    break;
                }
                case Kind.Cattails:
                    for (int i = 0; i < 9; i++)
                    {
                        Vector3 b = new Vector3(R(-0.6f, 0.6f), 0f, R(-0.6f, 0.6f));
                        float h = R(1.2f, 2.1f);
                        Vector3 top = b + new Vector3(R(-0.15f, 0.15f), h, R(-0.15f, 0.15f));
                        k.Rod(b, top, 0.025f, 4, MudPalette.Moss);
                        if (i % 2 == 0) k.Rod(top - Vector3.up * 0.38f, top - Vector3.up * 0.05f, 0.06f, 6, new Color(0.7f, 0.3f, 0.65f), 0.8f); // glowing heads
                        else k.Plank(b, b + new Vector3(R(-0.4f, 0.4f), h * 0.8f, R(-0.4f, 0.4f)), 0.06f, 0.01f, MudPalette.Grass);
                    }
                    break;
                case Kind.Reeds:
                    for (int i = 0; i < 12; i++)
                    {
                        Vector3 b = new Vector3(R(-0.8f, 0.8f), 0f, R(-0.8f, 0.8f));
                        k.Plank(b, b + new Vector3(R(-0.3f, 0.3f), R(0.6f, 1.3f), R(-0.3f, 0.3f)), 0.08f, 0.01f, i % 3 == 0 ? MudPalette.Moss : MudPalette.Grass);
                    }
                    break;
                case Kind.LilyPads:
                    for (int i = 0; i < 5; i++)
                    {
                        Vector3 c = new Vector3(R(-1.5f, 1.5f), 0.02f, R(-1.5f, 1.5f));
                        float r = R(0.3f, 0.55f);
                        k.Prism(c, Quaternion.Euler(0f, R(0f, 360f), 0f), r, r, 0.02f, 9, i % 2 == 0 ? MudPalette.Moss : MudPalette.Grass);
                        if (i % 2 == 0)
                            for (int p = 0; p < 5; p++)
                                k.Cone(c + Vector3.up * 0.02f, Quaternion.Euler(R(30f, 55f), p * 72f, 0f), 0.06f, 0.18f, 3, MudPalette.Lily);
                    }
                    break;
                case Kind.Mushrooms:
                    for (int i = 0; i < 5; i++)
                    {
                        Vector3 b = new Vector3(R(-0.4f, 0.4f), 0f, R(-0.4f, 0.4f));
                        float h = R(0.2f, 0.6f);
                        k.Rod(b, b + Vector3.up * h, 0.04f, 5, MudPalette.Cream);
                        k.Lathe(b + Vector3.up * h, Quaternion.identity, new[] { new Vector2(R(0.12f, 0.22f), 0f), new Vector2(0.1f, 0.08f), new Vector2(0.0001f, 0.12f) }, 7, Teal, 1.5f);
                    }
                    break;
                case Kind.Rock:
                    k.Ball(new Vector3(0f, 0.3f, 0f), new Vector3(R(0.7f, 1.1f), R(0.5f, 0.8f), R(0.7f, 1.1f)), Quaternion.Euler(R(0f, 30f), R(0f, 360f), 0f), MudPalette.Stone, 0);
                    break;
                case Kind.Boulder:
                    k.Ball(new Vector3(0f, 0.9f, 0f), new Vector3(R(1.8f, 2.4f), R(1.3f, 1.7f), R(1.8f, 2.4f)), Quaternion.Euler(R(0f, 20f), R(0f, 360f), 0f), MudPalette.Stone, 1);
                    k.Ball(new Vector3(0f, 1.6f, 0f), new Vector3(1.4f, 0.4f, 1.4f), Quaternion.identity, MudPalette.Moss, 0);
                    break;
                case Kind.Stump:
                    k.Prism(Vector3.zero, Quaternion.identity, 0.7f, 0.55f, 0.7f, 7, Bark);
                    k.Prism(new Vector3(0f, 0.7f, 0f), Quaternion.identity, 0.53f, 0.53f, 0.02f, 7, Wood);
                    break;
                case Kind.Log:
                    k.Rod(new Vector3(-2.2f, 0.4f, 0f), new Vector3(2.2f, 0.4f, 0f), 0.4f, 7, Bark, 0f, 0.35f);
                    k.Ball(new Vector3(0.6f, 0.75f, 0f), new Vector3(0.9f, 0.15f, 0.4f), Quaternion.identity, MudPalette.Moss, 0);
                    break;

                case Kind.StiltShack: StiltPod(k, variant, R); break;
                case Kind.Cabin: PodHut(k, variant); break;
                case Kind.DrivingSchool: SchoolHall(k); break;
                case Kind.Kiosk:
                    k.Box(new Vector3(0f, 0.8f, 0f), new Vector3(1f, 1.6f, 0.8f), MudPalette.HullDark); // a console pulled from the ship
                    k.Box(new Vector3(0f, 0.3f, 0.401f), new Vector3(1.002f, 0.12f, 0.01f), MudPalette.HullStripe);
                    k.Box(new Vector3(0f, 1.25f, 0.41f), new Vector3(0.7f, 0.5f, 0.02f), Teal, 2f);
                    k.Box(new Vector3(0f, 1.75f, 0f), new Vector3(1.2f, 0.12f, 1f), MudPalette.Rust);
                    k.Box(new Vector3(0f, 2.15f, 0f), new Vector3(0.9f, 0.45f, 0.08f), MudPalette.Cream);
                    k.Box(new Vector3(0f, 0.7f, 0.42f), new Vector3(0.2f, 0.1f, 0.05f), new Color(0.8f, 0.2f, 0.15f));
                    break;
                case Kind.Sawmill:
                    // Under a giant mushroom cap held up on four twisted stalks.
                    foreach (Vector3 p in new[] { new Vector3(-4f, 0f, -3f), new Vector3(4f, 0f, -3f), new Vector3(-4f, 0f, 3f), new Vector3(4f, 0f, 3f) })
                        k.Tube(new[] { p, p * 0.96f + new Vector3(0.1f, 2f, 0f), p * 0.86f + Vector3.up * 4.3f }, 0.24f, 6, Bark);
                    Cap(k, new Vector3(0f, 4.3f, 0f), 5.2f, 1.8f, Caps[(variant + 1) % Caps.Length]);
                    k.Box(new Vector3(0f, 0.6f, 0f), new Vector3(6f, 1.2f, 1.6f), Wood);
                    k.Prism(new Vector3(0f, 1.2f, 0f), Quaternion.Euler(90f, 0f, 0f), 1.1f, 1.1f, 0.06f, 16, MudPalette.Metal);
                    for (int i = 0; i < 4; i++) k.Rod(new Vector3(-3f, 0.4f + (i % 2) * 0.7f, -2.3f + i * 0.45f), new Vector3(3f, 0.4f + (i % 2) * 0.7f, -2.3f + i * 0.45f), 0.35f, 7, Bark);
                    break;
                case Kind.Outhouse:
                    k.Box(new Vector3(0f, 1.1f, 0f), new Vector3(1.2f, 2.2f, 1.2f), Wood);
                    Cap(k, new Vector3(0f, 2.3f, 0f), 1.05f, 0.6f, Caps[variant % Caps.Length]);
                    k.Box(new Vector3(0f, 1.8f, 0.61f), new Vector3(0.18f, 0.25f, 0.02f), MudPalette.Black); // the moon
                    break;
                case Kind.Barricade:
                    foreach (float x in new[] { -1.6f, 1.6f })
                    {
                        k.Plank(new Vector3(x, 0f, -0.4f), new Vector3(x, 1.2f, 0f), 0.12f, 0.12f, Wood);
                        k.Plank(new Vector3(x, 0f, 0.4f), new Vector3(x, 1.2f, 0f), 0.12f, 0.12f, Wood);
                    }
                    for (int i = 0; i < 2; i++)
                    {
                        k.Box(new Vector3(0f, 0.8f + i * 0.45f, 0f), new Vector3(3.8f, 0.3f, 0.08f), Rib);
                        for (int s = 0; s < 5; s++) k.Box(new Vector3(-1.5f + s * 0.75f, 0.8f + i * 0.45f, 0.045f), new Vector3(0.3f, 0.3f, 0.02f), Quaternion.Euler(0f, 0f, 35f), Orange);
                    }
                    k.Box(new Vector3(0f, 1.75f, 0.06f), new Vector3(1.8f, 0.45f, 0.05f), DarkWood); // ROAD CLOSED
                    break;
                case Kind.Fence:
                    for (int i = 0; i < 3; i++) k.Box(new Vector3(-1.8f + i * 1.8f, 0.6f, 0f), new Vector3(0.14f, 1.2f, 0.14f), GreyWood);
                    k.Box(new Vector3(0f, 0.45f, 0f), new Vector3(3.9f, 0.12f, 0.06f), GreyWood);
                    k.Box(new Vector3(0f, 0.95f, 0f), new Vector3(3.9f, 0.12f, 0.06f), Quaternion.Euler(0f, 0f, R(-4f, 4f)), GreyWood);
                    break;
                case Kind.Crate:
                    k.Box(new Vector3(0f, 0.45f, 0f), new Vector3(0.9f, 0.9f, 0.9f), Wood);
                    k.Box(new Vector3(0f, 0.45f, 0.46f), new Vector3(0.95f, 0.12f, 0.03f), Quaternion.Euler(0f, 0f, 45f), DarkWood);
                    break;
                case Kind.HayBale:
                    // A bale of dried sage-green swamp grass.
                    k.Box(new Vector3(0f, 0.45f, 0f), new Vector3(1.6f, 0.9f, 1f), new Color(0.62f, 0.72f, 0.55f));
                    k.Box(new Vector3(-0.4f, 0.45f, 0f), new Vector3(0.05f, 0.92f, 1.02f), HuskDark);
                    k.Box(new Vector3(0.4f, 0.45f, 0f), new Vector3(0.05f, 0.92f, 1.02f), HuskDark);
                    break;
                case Kind.MoonshineBarrel:
                    k.Lathe(Vector3.zero, Quaternion.identity, new[] { new Vector2(0.0001f, 0f), new Vector2(0.38f, 0f), new Vector2(0.44f, 0.5f), new Vector2(0.38f, 1f), new Vector2(0.0001f, 1f) }, 10, new Color(0.5f, 0.32f, 0.18f));
                    k.Prism(new Vector3(0f, 0.2f, 0f), Quaternion.identity, 0.42f, 0.42f, 0.06f, 10, MudPalette.DarkMetal);
                    k.Prism(new Vector3(0f, 0.75f, 0f), Quaternion.identity, 0.42f, 0.42f, 0.06f, 10, MudPalette.DarkMetal);
                    k.Box(new Vector3(0f, 0.5f, 0.43f), new Vector3(0.4f, 0.16f, 0.02f), MudPalette.Cream);
                    break;
                case Kind.MoonshineStill:
                    // The pot is a giant hollowed gourd with pale ribs; the pipes are copper.
                    k.Lathe(Vector3.zero, Quaternion.identity, new[] { new Vector2(0.0001f, 0f), new Vector2(1.1f, 0f), new Vector2(1.3f, 1.2f), new Vector2(0.9f, 2f), new Vector2(0.3f, 2.4f), new Vector2(0.0001f, 2.4f) }, 12, Gourd);
                    for (int i = 0; i < 12; i++)
                    {
                        float a = (i + 0.5f) * Mathf.PI / 6f;
                        var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                        k.Tube(new[] { dir * 1.08f + Vector3.up * 0.02f, dir * 1.29f + Vector3.up * 1.2f, dir * 0.9f + Vector3.up * 2f, dir * 0.3f + Vector3.up * 2.4f }, 0.05f, 4, Rib);
                    }
                    k.Tube(new[] { new Vector3(0f, 2.4f, 0f), new Vector3(0f, 3f, 0f), new Vector3(1.4f, 2.8f, 0f), new Vector3(2f, 1.8f, 0f) }, 0.12f, 6, Copper);
                    for (int i = 0; i < 4; i++) k.Prism(new Vector3(2.2f, 0.4f + i * 0.3f, 0f), Quaternion.identity, 0.5f, 0.5f, 0.12f, 10, Copper); // the coil
                    k.Box(new Vector3(0f, 0.2f, 1.4f), new Vector3(1.6f, 0.4f, 0.6f), MudPalette.Stone); // the firebox
                    k.Box(new Vector3(0f, 0.25f, 1.71f), new Vector3(0.6f, 0.25f, 0.02f), MudPalette.Fire, 3f);
                    break;
                case Kind.Dam:
                    k.Ball(new Vector3(0f, 1.2f, 0f), new Vector3(9f, 2.2f, 2.6f), Quaternion.identity, MudPalette.Mud, 1);
                    for (int i = 0; i < 9; i++) k.Rod(new Vector3(-8f + i * 2f, 0.6f + (i % 3) * 0.8f, R(-1f, 1f)), new Vector3(-6.5f + i * 2f, 1f + (i % 2) * 0.9f, R(-1.5f, 1.5f)), 0.25f, 6, Bark);
                    break;
                case Kind.Gong:
                    k.Rod(new Vector3(-1.2f, 0f, 0f), new Vector3(-1.2f, 3f, 0f), 0.12f, 6, DarkWood);
                    k.Rod(new Vector3(1.2f, 0f, 0f), new Vector3(1.2f, 3f, 0f), 0.12f, 6, DarkWood);
                    k.Box(new Vector3(0f, 3.05f, 0f), new Vector3(3f, 0.25f, 0.3f), new Color(0.6f, 0.15f, 0.12f));
                    k.Box(new Vector3(0f, 0f, 0f), new Vector3(3f, 0.2f, 1f), MudPalette.Stone);
                    break;
                case Kind.CursedLantern:
                    k.Rod(Vector3.zero, new Vector3(0f, 4.2f, 0f), 0.13f, 6, DarkWood);
                    k.Rod(new Vector3(0f, 4.1f, 0f), new Vector3(0.5f, 4.3f, 0f), 0.06f, 5, DarkWood);
                    k.Box(new Vector3(0.55f, 3.8f, 0f), new Vector3(0.35f, 0.45f, 0.35f), MudPalette.DarkMetal);
                    k.Ball(new Vector3(0.55f, 3.8f, 0f), 0.18f, MudPalette.Curse, 1, 4f);
                    break;
                case Kind.LanternPost:
                    k.Rod(Vector3.zero, new Vector3(0f, 3f, 0f), 0.1f, 6, DarkWood);
                    k.Tube(new[] { new Vector3(0f, 2.9f, 0f), new Vector3(0.1f, 3.3f, 0f), new Vector3(0.5f, 3.3f, 0f), new Vector3(0.55f, 3.05f, 0f) }, 0.04f, 5, DarkWood);
                    k.Box(new Vector3(0.55f, 2.8f, 0f), new Vector3(0.28f, 0.36f, 0.28f), MudPalette.DarkMetal);
                    k.Ball(new Vector3(0.55f, 2.8f, 0f), 0.14f, MudPalette.Lantern, 1, 4f);
                    break;
                case Kind.TikiTorch:
                    k.Rod(Vector3.zero, new Vector3(0f, 1.9f, 0f), 0.06f, 5, DarkWood);
                    k.Prism(new Vector3(0f, 1.85f, 0f), Quaternion.identity, 0.12f, 0.16f, 0.3f, 6, new Color(0.55f, 0.4f, 0.22f));
                    break;
                case Kind.StepStone:
                    k.Prism(new Vector3(0f, -0.6f, 0f), Quaternion.Euler(0f, R(0f, 60f), 0f), 0.9f, 0.8f, 0.8f, 6, MudPalette.Stone);
                    k.Prism(new Vector3(0f, 0.18f, 0f), Quaternion.identity, 0.6f, 0.4f, 0.04f, 6, MudPalette.Moss);
                    break;
                case Kind.Bridge:
                {
                    float length = 10f * size;
                    for (int i = 0; i < Mathf.RoundToInt(length / 0.5f); i++)
                        k.Box(new Vector3(0f, 0f, -length * 0.5f + 0.25f + i * 0.5f), new Vector3(3.6f, 0.12f, 0.44f), Quaternion.Euler(0f, R(-2f, 2f), 0f), i % 3 == 0 ? GreyWood : Wood);
                    foreach (float x in new[] { -1.9f, 1.9f })
                    {
                        for (float z = -length * 0.5f; z <= length * 0.5f + 0.01f; z += 2.5f) k.Rod(new Vector3(x, -1.5f, z), new Vector3(x, 1f, z), 0.12f, 5, DarkWood);
                        k.Box(new Vector3(x, 0.9f, 0f), new Vector3(0.1f, 0.1f, length), DarkWood);
                    }
                    break;
                }
                case Kind.Boardwalk:
                {
                    float length = 6f * size;
                    for (int i = 0; i < Mathf.RoundToInt(length / 0.45f); i++)
                        k.Box(new Vector3(0f, 0f, -length * 0.5f + 0.22f + i * 0.45f), new Vector3(2f, 0.1f, 0.4f), Quaternion.Euler(R(-2f, 2f), R(-3f, 3f), 0f), i % 4 == 0 ? GreyWood : Wood);
                    for (float z = -length * 0.5f; z <= length * 0.5f + 0.01f; z += 2f)
                        foreach (float x in new[] { -0.9f, 0.9f }) k.Rod(new Vector3(x, -1.2f, z), new Vector3(x, 0.05f, z), 0.08f, 5, DarkWood);
                    break;
                }
                case Kind.TyreWall:
                    for (int i = 0; i < 6; i++)
                        for (int j = 0; j < 2; j++)
                            k.Prism(new Vector3(-2.5f + i, j * 0.32f, 0f), Quaternion.identity, 0.48f, 0.48f, 0.3f, 9, (i + j) % 3 == 0 ? new Color(0.85f, 0.15f, 0.1f) : MudPalette.Rubber);
                    break;
                case Kind.Grandstand:
                    for (int i = 0; i < 4; i++)
                        k.Box(new Vector3(0f, 0.3f + i * 0.5f, -i * 0.8f), new Vector3(10f, 0.12f, 0.8f), Wood);
                    for (int i = 0; i < 6; i++) k.Rod(new Vector3(-5f + i * 2f, 0f, -2.6f), new Vector3(-5f + i * 2f, 3.3f, -2.6f), 0.1f, 5, DarkWood);
                    k.Box(new Vector3(0f, 3.4f, -2.8f), new Vector3(10.4f, 0.12f, 1.4f), Quaternion.Euler(-10f, 0f, 0f), new Color(0.85f, 0.15f, 0.1f));
                    break;
                case Kind.BananaStand:
                    k.Box(new Vector3(0f, 0.5f, 0f), new Vector3(2.4f, 1f, 1f), Wood);
                    for (int i = 0; i < 6; i++) k.Rod(new Vector3(-1f + i * 0.4f, 1.1f, 0f), new Vector3(-0.8f + i * 0.4f, 1.15f, 0.25f), 0.08f, 5, Banana);
                    k.Rod(new Vector3(-1.1f, 1f, -0.4f), new Vector3(-1.1f, 2.4f, -0.4f), 0.06f, 5, DarkWood);
                    k.Rod(new Vector3(1.1f, 1f, -0.4f), new Vector3(1.1f, 2.4f, -0.4f), 0.06f, 5, DarkWood);
                    k.Box(new Vector3(0f, 2.45f, -0.2f), new Vector3(2.6f, 0.1f, 1.4f), Quaternion.Euler(-12f, 0f, 0f), Banana);
                    break;
                case Kind.Cauldron:
                    // Open at the top: the rim turns down into an inner wall, so the brew (and anything rising out of it) shows.
                    k.Lathe(Vector3.zero, Quaternion.identity, new[] { new Vector2(0.0001f, 0.1f), new Vector2(0.9f, 0.2f), new Vector2(1.25f, 0.8f), new Vector2(1.1f, 1.4f), new Vector2(1.2f, 1.5f),
                        new Vector2(1.06f, 1.52f), new Vector2(0.99f, 1.2f), new Vector2(0.0001f, 1.2f) }, 12, MudPalette.Black);
                    k.Prism(new Vector3(0f, 1.35f, 0f), Quaternion.identity, 1.08f, 1.08f, 0.05f, 12, MudPalette.Curse, 2.5f);
                    for (int i = 0; i < 3; i++) { float a = i / 3f * Mathf.PI * 2f; k.Rod(new Vector3(Mathf.Cos(a) * 0.8f, 0.3f, Mathf.Sin(a) * 0.8f), new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)), 0.08f, 5, MudPalette.Black); }
                    break;
                case Kind.WitchHut:
                {
                    // Her crooked hut: husk walls with ribs, and a droopy violet witch-hat of a mushroom cap with glowing spots.
                    Quaternion lean = Quaternion.Euler(0f, 0f, 3f);
                    k.Box(new Vector3(0f, 1.6f, 0f), new Vector3(3.6f, 3.2f, 3.6f), lean, HuskDark);
                    for (int i = 0; i < 4; i++)
                        foreach (float sz in new[] { -1f, 1f })
                            k.Tube(new[] { lean * new Vector3(-1.35f + i * 0.9f, 0.05f, sz * 1.82f), lean * new Vector3(-1.35f + i * 0.9f, 1.6f, sz * 1.95f), lean * new Vector3(-1.35f + i * 0.9f, 3.15f, sz * 1.82f) }, 0.07f, 4, Rib);
                    Color hat = new Color(0.36f, 0.2f, 0.46f);
                    k.Lathe(new Vector3(0.2f, 3.05f, 0f), Quaternion.Euler(0f, 0f, -8f), new[] { new Vector2(0.0001f, 0.15f), new Vector2(2.8f, -0.2f) }, 9, Gill);
                    k.Lathe(new Vector3(0.2f, 3.05f, 0f), Quaternion.Euler(0f, 0f, -8f), new[] { new Vector2(2.8f, -0.2f), new Vector2(3.05f, 0.05f), new Vector2(1.9f, 0.9f), new Vector2(1.1f, 2.2f), new Vector2(0.55f, 3.5f), new Vector2(0.0001f, 4f) }, 9, hat);
                    k.Cone(new Vector3(0.62f, 6.45f, 0f), Quaternion.Euler(0f, 0f, 48f), 0.5f, 1.6f, 7, hat); // the tip flops over
                    k.Ball(new Vector3(0.62f, 6.45f, 0f) + Quaternion.Euler(0f, 0f, 48f) * Vector3.up * 1.6f, 0.13f, Spot, 0, 2f);
                    for (int i = 0; i < 7; i++)
                    {
                        float a = i * Mathf.PI * 2f / 7f;
                        k.Ball(new Vector3(0.2f, 3.05f, 0f) + Quaternion.Euler(0f, 0f, -8f) * new Vector3(Mathf.Cos(a) * 1.75f, 1.1f, Mathf.Sin(a) * 1.75f), 0.16f, Spot, 0, 1.2f);
                    }
                    k.Ball(new Vector3(0f, 1.0f, 1.82f), new Vector3(0.55f, 1.0f, 0.1f), Quaternion.identity, DarkWood, 1);
                    FlatWindow(k, new Vector3(1f, 2.3f, 1.82f), Vector3.forward, 0.32f);
                    break;
                }
                case Kind.DojoGate:
                    foreach (float x in new[] { -2.2f, 2.2f }) k.Rod(new Vector3(x, 0f, 0f), new Vector3(x, 4f, 0f), 0.22f, 6, new Color(0.45f, 0.2f, 0.12f));
                    k.Box(new Vector3(0f, 4.1f, 0f), new Vector3(6f, 0.35f, 0.5f), DarkWood);
                    k.Box(new Vector3(0f, 3.3f, 0f), new Vector3(4.8f, 0.25f, 0.3f), new Color(0.45f, 0.2f, 0.12f));
                    break;
                case Kind.StoneLantern:
                    k.Prism(Vector3.zero, Quaternion.identity, 0.35f, 0.3f, 0.2f, 6, MudPalette.Stone);
                    k.Prism(new Vector3(0f, 0.2f, 0f), Quaternion.identity, 0.12f, 0.12f, 0.8f, 6, MudPalette.Stone);
                    k.Box(new Vector3(0f, 1.2f, 0f), new Vector3(0.5f, 0.4f, 0.5f), MudPalette.Stone);
                    k.Box(new Vector3(0f, 1.2f, 0f), new Vector3(0.3f, 0.25f, 0.52f), MudPalette.Lantern, 3f);
                    k.Cone(new Vector3(0f, 1.4f, 0f), Quaternion.identity, 0.55f, 0.35f, 6, MudPalette.Stone);
                    break;
                case Kind.Roadhouse:
                    GableHall(k, 2.2f);
                    k.Box(new Vector3(0f, 0.3f, 4.6f), new Vector3(11f, 0.25f, 2.4f), Wood); // the porch
                    foreach (float x in new[] { -5f, -1.6f, 1.6f, 5f })
                    {
                        k.Tube(new[] { new Vector3(x, 0.3f, 5.6f), new Vector3(x + 0.1f, 1.8f, 5.65f), new Vector3(x, 3.2f, 5.6f) }, 0.13f, 5, Bark);
                        k.Ball(new Vector3(x, 3.05f, 5.75f), 0.14f, Spot, 0, 2f);
                    }
                    // A leaf awning over the porch.
                    for (int i = 0; i < 4; i++)
                    {
                        float x = -4.3f + i * 2.85f;
                        k.Plank(new Vector3(x, 3.45f, 3.6f), new Vector3(x, 3.05f, 6.4f), 3.2f, 0.07f, i % 2 == 0 ? Leaf : LeafDark);
                        k.Rod(new Vector3(x, 3.52f, 3.6f), new Vector3(x, 3.12f, 6.4f), 0.05f, 4, Rib);
                    }
                    k.Box(new Vector3(0f, 6.6f, 5.62f), new Vector3(7f, 1.4f, 0.15f), MudPalette.Black); // sign board on the front gable (neon lights from MudProp)
                    break;
                case Kind.GasPump:
                    k.ScrapPanel(new Vector3(0f, 0.9f, 0f), new Vector3(0.7f, 1.8f, 0.5f), Quaternion.identity, MudPalette.Hull); // a hull-panel cabinet
                    k.Ball(new Vector3(0f, 2f, 0f), new Vector3(0.32f, 0.32f, 0.12f), Quaternion.identity, MudPalette.ShipGlow, 1, 1.5f); // a ship's running light
                    k.Tube(new[] { new Vector3(0.36f, 1.2f, 0f), new Vector3(0.55f, 0.6f, 0.2f), new Vector3(0.45f, 1f, 0.35f) }, 0.04f, 4, MudPalette.Black);
                    break;
                case Kind.Ramp:
                {
                    // A wedge of planks on a dirt bank: 8 m long, 2.6 m high at the lip (+Z).
                    Vector3 a = new Vector3(-2f, 0f, -4f), b = new Vector3(2f, 0f, -4f), c = new Vector3(2f, 2.6f, 4f), d = new Vector3(-2f, 2.6f, 4f);
                    k.Quad(a, d, c, b, Wood);
                    k.Quad(new Vector3(-2f, 0f, 4f), new Vector3(2f, 0f, 4f), c, d, DarkWood);
                    k.Tri(a, new Vector3(-2f, 0f, 4f), d, MudPalette.Mud);
                    k.Tri(b, c, new Vector3(2f, 0f, 4f), MudPalette.Mud);
                    break;
                }
                case Kind.GatePost:
                    k.Box(new Vector3(0f, 1.6f, 0f), new Vector3(0.5f, 3.2f, 0.5f), DarkWood);
                    k.Cone(new Vector3(0f, 3.2f, 0f), Quaternion.identity, 0.4f, 0.4f, 4, DarkWood);
                    break;
                case Kind.CountyLine:
                    foreach (float x in new[] { -5f, 5f }) k.Rod(new Vector3(x, 0f, 0f), new Vector3(x, 5f, 0f), 0.25f, 6, DarkWood);
                    k.Box(new Vector3(0f, 4.6f, 0f), new Vector3(10.6f, 1.2f, 0.2f), new Color(0.2f, 0.4f, 0.25f));
                    k.Box(new Vector3(0f, 4.6f, 0.11f), new Vector3(10f, 0.9f, 0.02f), MudPalette.White);
                    break;
                case Kind.Windmill:
                    k.Prism(Vector3.zero, Quaternion.identity, 1.5f, 0.6f, 9f, 4, GreyWood);
                    k.Ball(new Vector3(0f, 9f, 0.7f), new Vector3(0.45f, 0.45f, 0.6f), Quaternion.identity, HuskDark, 1);
                    for (int i = 0; i < 4; i++)
                    {
                        Quaternion r = Quaternion.Euler(0f, 0f, i * 90f + 20f + (i == 2 ? 50f : 0f));
                        k.Box(new Vector3(0f, 9f, 1.15f) + r * new Vector3(0f, 1.6f, 0f), new Vector3(0.7f, 3f, 0.05f), r, i % 2 == 0 ? Leaf : LeafDark); // leaf sails
                        k.Rod(new Vector3(0f, 9f, 1.2f), new Vector3(0f, 9f, 1.2f) + r * new Vector3(0f, 3.1f, 0f), 0.05f, 4, Rib);
                    }
                    break;
                case Kind.RoadSign:
                    k.Rod(Vector3.zero, new Vector3(0f, 2.4f, 0f), 0.06f, 5, Bark);
                    k.Ball(new Vector3(0f, 2.45f, 0f), 0.1f, Spot, 0, 1.5f);
                    k.Box(new Vector3(0.5f, 2.2f, 0f), new Vector3(1.4f, 0.35f, 0.06f), Quaternion.Euler(0f, 0f, R(-6f, 6f)), Rib);
                    break;
                case Kind.Rowboat:
                    k.Lathe(Vector3.zero, Quaternion.Euler(90f, 0f, 0f), new[] { new Vector2(0.0001f, -1.8f), new Vector2(0.6f, -1.2f), new Vector2(0.75f, 0f), new Vector2(0.6f, 1.2f), new Vector2(0.0001f, 1.8f) }, 6, Wood);
                    break;
                case Kind.Island:
                    k.Ball(new Vector3(0f, -0.4f, 0f), new Vector3(4.5f * size, 1.2f, 4.5f * size), Quaternion.identity, MudPalette.Mud, 1);
                    k.Ball(new Vector3(0f, 0.15f, 0f), new Vector3(4f * size, 0.6f, 4f * size), Quaternion.identity, MudPalette.Grass, 1);
                    break;
            }
        }

        // A curtain of hanging moss: thin flat strips.
        private static void Moss(MeshKit k, Vector3 top, float length)
        {
            for (int i = 0; i < 3; i++)
            {
                Vector3 t = top + new Vector3(i * 0.15f - 0.15f, 0f, (i % 2) * 0.1f);
                k.Plank(t, t + new Vector3(0.05f * i, -length + i * 0.3f, 0.05f), 0.12f, 0.02f, MossHang);
            }
        }

        // ---------- Alien buildings ----------
        // The swamp folk build from what grows here: walls of dried giant-pod husk braced with pale ribs, roofs of
        // giant mushroom caps or overlapping fronds, roots for stilts, and round windows lit by the same mint glow as
        // the biolights. Footprints match the old shacks, so the level's placements still fit.

        private static readonly Vector2[] HutBody =
        {
            new Vector2(0.0001f, 0f), new Vector2(2.0f, 0f), new Vector2(2.3f, 0.9f), new Vector2(2.2f, 2.0f), new Vector2(1.7f, 2.65f), new Vector2(0.0001f, 2.7f)
        };

        private static readonly Vector2[] PodBody =
        {
            new Vector2(0.0001f, 0f), new Vector2(1.55f, 0f), new Vector2(1.9f, 0.9f), new Vector2(1.8f, 2.0f), new Vector2(1.25f, 2.75f), new Vector2(0.0001f, 2.8f)
        };

        // A round husk hut under a mushroom cap (the Flats cabin and the sunken chapel).
        private static void PodHut(MeshKit k, int variant)
        {
            const int sides = 10;
            k.Lathe(Vector3.zero, Quaternion.identity, HutBody, sides, Husk);
            Ribs(k, Vector3.zero, HutBody, sides, 0.05f);
            Door(k, Vector3.zero, HutBody, sides, 0.5f, 1.9f);
            foreach (float deg in new[] { 54f, 126f, 198f, 342f }) PodWindow(k, Vector3.zero, HutBody, sides, deg, 1.45f, 0.3f);
            Cap(k, new Vector3(0f, 2.45f, 0f), 2.9f, 1.5f, Caps[variant % Caps.Length]);
            k.Tube(new[] { new Vector3(0.7f, 3.6f, -0.6f), new Vector3(0.9f, 4.4f, -0.8f), new Vector3(1.3f, 4.9f, -0.6f), new Vector3(1.55f, 4.8f, -0.2f) }, 0.1f, 5, Rib);
            k.Ball(new Vector3(1.6f, 4.72f, -0.05f), 0.2f, Spot, 1, 2f); // a glowing seed on a curled stalk (the chimney)
        }

        // The reference's crooked stilt shack, gone native: a husk pod on twisted root stilts with a porch, a vine
        // ladder, a mushroom cap with a leaning tip and hanging moss.
        private static void StiltPod(MeshKit k, int variant, Func<float, float, float> R)
        {
            const float lift = 1.8f;
            const int sides = 10;
            foreach (Vector3 p in new[] { new Vector3(-1.4f, 0f, -1.5f), new Vector3(1.4f, 0f, -1.5f), new Vector3(-1.4f, 0f, 1.3f), new Vector3(1.4f, 0f, 1.3f), new Vector3(0f, 0f, 2.6f) })
                k.Tube(new[] { new Vector3(p.x * 1.35f, -1.5f, p.z * 1.2f), new Vector3(p.x * 1.25f, 0.3f, p.z * 1.12f), new Vector3(p.x * 1.08f, 1.1f, p.z * 1.03f), new Vector3(p.x, lift, p.z) }, 0.16f, 5, Bark);
            k.Box(new Vector3(0f, lift, 0.4f), new Vector3(3.8f, 0.2f, 4.6f), Wood); // floor and porch
            Vector3 body = new Vector3(0f, lift + 0.1f, -0.1f);
            k.Lathe(body, Quaternion.identity, PodBody, sides, Husk);
            Ribs(k, body, PodBody, sides, 0.05f);
            Door(k, body, PodBody, sides, 0.42f, 1.85f);
            PodWindow(k, body, PodBody, sides, 126f, 1.5f, 0.28f);
            PodWindow(k, body, PodBody, sides, 54f, 1.9f, 0.24f);
            PodWindow(k, body, PodBody, sides, 234f, 1.4f, 0.26f);
            Color cap = Caps[variant % Caps.Length];
            Vector3 capBase = body + Vector3.up * 2.45f;
            Cap(k, capBase, 2.45f, 1.3f, cap);
            k.Cone(capBase + new Vector3(0.1f, 1.2f, 0f), Quaternion.Euler(0f, 0f, -14f), 0.5f, 1.5f, 7, cap); // the crooked tip
            k.Ball(capBase + new Vector3(0.48f, 2.62f, 0f), 0.13f, Spot, 0, 2f);
            for (int i = 0; i < 4; i++)
            {
                float a = (i * 90f + 30f) * Mathf.Deg2Rad;
                Moss(k, capBase + new Vector3(Mathf.Cos(a) * 2.25f, -0.15f, Mathf.Sin(a) * 2.25f), R(0.8f, 1.5f));
            }
            // A vine ladder down from the porch.
            foreach (float x in new[] { -0.35f, 0.35f }) k.Rod(new Vector3(x - 0.4f, 0f, 3.2f), new Vector3(x - 0.4f, lift, 2.7f), 0.05f, 4, Bark);
            for (int i = 0; i < 5; i++) k.Rod(new Vector3(-0.75f, 0.3f + i * 0.35f, 3.13f - i * 0.09f), new Vector3(-0.05f, 0.3f + i * 0.35f, 3.13f - i * 0.09f), 0.03f, 4, Wood);
        }

        // The Bayou Driving School: a long husk hall with fat stalk pillars at the corners, ribbed walls, round
        // windows, and a flat roof deck behind a rim of curled fronds (the possums sit up there), its sign on the front.
        private static void SchoolHall(MeshKit k)
        {
            const float w = 5.9f, d = 6.4f, h = 4.4f;
            k.Box(new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, d), Husk);
            k.Box(new Vector3(0f, h + 0.08f, 0f), new Vector3(w + 0.3f, 0.2f, d + 0.3f), HuskDark); // the roof deck
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                {
                    Vector3 c = new Vector3(sx * w * 0.5f, 0f, sz * d * 0.5f);
                    k.Lathe(c, Quaternion.identity, new[] { new Vector2(0.0001f, 0f), new Vector2(0.6f, 0f), new Vector2(0.45f, 1.2f), new Vector2(0.38f, 4.5f), new Vector2(0.5f, 4.8f), new Vector2(0.0001f, 5.05f) }, 8, Rib);
                    k.Ball(c + Vector3.up * 5.05f, 0.2f, Spot, 0, 2f);
                }
            // Ribs on the walls.
            for (int i = 1; i < 5; i++)
            {
                float x = -w * 0.5f + i * w / 5f;
                foreach (float sz in new[] { -1f, 1f })
                    if (Mathf.Abs(x) > 0.9f || sz < 0f) k.Tube(new[] { new Vector3(x, 0.1f, sz * (d * 0.5f + 0.02f)), new Vector3(x, h * 0.5f, sz * (d * 0.5f + 0.16f)), new Vector3(x, h - 0.1f, sz * (d * 0.5f + 0.02f)) }, 0.09f, 5, Rib);
            }
            for (int i = 1; i < 5; i++)
            {
                float z = -d * 0.5f + i * d / 5f;
                foreach (float sx in new[] { -1f, 1f })
                    k.Tube(new[] { new Vector3(sx * (w * 0.5f + 0.02f), 0.1f, z), new Vector3(sx * (w * 0.5f + 0.16f), h * 0.5f, z), new Vector3(sx * (w * 0.5f + 0.02f), h - 0.1f, z) }, 0.09f, 5, Rib);
            }
            // Arched door and round windows.
            k.Ball(new Vector3(0f, 1.15f, d * 0.5f), new Vector3(0.95f, 1.45f, 0.14f), Quaternion.identity, Rib, 1);
            k.Ball(new Vector3(0f, 1.1f, d * 0.5f + 0.04f), new Vector3(0.75f, 1.3f, 0.12f), Quaternion.identity, DarkWood, 1);
            foreach (float x in new[] { -1.85f, 1.85f }) FlatWindow(k, new Vector3(x, 2.5f, d * 0.5f), Vector3.forward, 0.55f);
            foreach (float sx in new[] { -1f, 1f }) foreach (float z in new[] { -1.4f, 1.4f }) FlatWindow(k, new Vector3(sx * w * 0.5f, 2.4f, z), new Vector3(sx, 0f, 0f), 0.5f);
            ShelfFungi(k, new Vector3(-w * 0.5f, 3.4f, 0.2f), Vector3.left, 0.45f, Caps[2]);
            ShelfFungi(k, new Vector3(w * 0.5f, 1.2f, -0.6f), Vector3.right, 0.4f, Caps[0]);
            ShelfFungi(k, new Vector3(1.0f, 0.5f, -d * 0.5f), Vector3.back, 0.42f, Caps[1]);
            // Curled fronds round the roof deck (not along the front, where the sign is).
            for (int i = 0; i < 5; i++)
            {
                float z = -d * 0.5f + (i + 0.5f) * d / 5f;
                foreach (float sx in new[] { -1f, 1f }) Frond(k, new Vector3(sx * (w * 0.5f + 0.05f), h + 0.15f, z), new Vector3(sx * 0.45f, 0.75f, 0f), 1.25f, i % 2 == 0 ? Leaf : LeafDark);
            }
            for (int i = 0; i < 4; i++)
                Frond(k, new Vector3(-w * 0.5f + (i + 0.5f) * w / 4f, h + 0.15f, -d * 0.5f - 0.05f), new Vector3(0f, 0.75f, -0.45f), 1.4f, i % 2 == 0 ? Leaf : LeafDark);
            // The sign board on the front edge of the deck (text added by MudProp).
            k.Box(new Vector3(0f, 4.95f, d * 0.5f + 0.2f), new Vector3(5.4f, 1f, 0.12f), Rib);
            k.Box(new Vector3(0f, 4.95f, d * 0.5f + 0.15f), new Vector3(5.6f, 1.15f, 0.08f), Orange);
        }

        // A gabled husk hall with a roof of overlapping giant leaves, ribbed walls, round windows and an arched door,
        // scaled (the Greasy Spoon is 2.2). Its sign hangs on the front gable.
        private static void GableHall(MeshKit k, float s)
        {
            float w = 4.2f * s, d = 4.6f * s, h = 2.6f * s, rise = 1.6f * s;
            k.Box(new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, d), Husk);
            for (int i = 0; i <= 6; i++)
            {
                float x = -w * 0.5f + i * w / 6f;
                foreach (float sz in new[] { -1f, 1f })
                    if (Mathf.Abs(x) > 0.75f * s || sz < 0f) k.Tube(new[] { new Vector3(x, 0.1f, sz * (d * 0.5f + 0.02f)), new Vector3(x, h * 0.5f, sz * (d * 0.5f + 0.12f * s)), new Vector3(x, h - 0.05f, sz * (d * 0.5f + 0.02f)) }, 0.08f * s, 5, Rib);
            }
            for (int i = 0; i <= 6; i++)
            {
                float z = -d * 0.5f + i * d / 6f;
                foreach (float sx in new[] { -1f, 1f })
                    k.Tube(new[] { new Vector3(sx * (w * 0.5f + 0.02f), 0.1f, z), new Vector3(sx * (w * 0.5f + 0.12f * s), h * 0.5f, z), new Vector3(sx * (w * 0.5f + 0.02f), h - 0.05f, z) }, 0.08f * s, 5, Rib);
            }
            // The roof: a husk base under rows of overlapping leaves, bulbs along the ridge, husk gable ends.
            Vector3 ridgeA = new Vector3(0f, h + rise, -d * 0.55f), ridgeB = new Vector3(0f, h + rise, d * 0.55f);
            Vector3 l0 = new Vector3(-w * 0.62f, h, -d * 0.55f), l1 = new Vector3(-w * 0.62f, h, d * 0.55f), r0 = new Vector3(w * 0.62f, h, -d * 0.55f), r1 = new Vector3(w * 0.62f, h, d * 0.55f);
            k.Quad(l1, ridgeB, ridgeA, l0, HuskDark);
            k.Quad(r0, ridgeA, ridgeB, r1, HuskDark);
            k.Tri(r1, ridgeB, l1, HuskDark);
            k.Tri(l0, ridgeA, r0, HuskDark);
            LeafRoof(k, l1, l0, ridgeB, ridgeA, 4, 5);
            LeafRoof(k, r0, r1, ridgeA, ridgeB, 4, 5);
            k.Rod(ridgeA, ridgeB, 0.18f * s, 6, LeafDark);
            for (int i = 0; i < 5; i++) k.Ball(Vector3.Lerp(ridgeA, ridgeB, (i + 0.5f) / 5f) + Vector3.up * 0.2f * s, 0.12f * s, Spot, 0, 2f);
            // Arched door and round windows.
            k.Ball(new Vector3(0f, 0.95f * s, d * 0.5f), new Vector3(0.6f * s, 1.0f * s, 0.12f), Quaternion.identity, Rib, 1);
            k.Ball(new Vector3(0f, 0.9f * s, d * 0.5f + 0.04f), new Vector3(0.47f * s, 0.88f * s, 0.1f), Quaternion.identity, DarkWood, 1);
            foreach (float x in new[] { -1.25f, 1.25f }) FlatWindow(k, new Vector3(x * s, 1.35f * s, d * 0.5f), Vector3.forward, 0.36f * s);
            foreach (float sx in new[] { -1f, 1f }) FlatWindow(k, new Vector3(sx * w * 0.5f, 1.35f * s, 0f), new Vector3(sx, 0f, 0f), 0.36f * s);
            ShelfFungi(k, new Vector3(-w * 0.5f, 2.0f * s, -1.3f * s), Vector3.left, 0.3f * s, Caps[2]);
            ShelfFungi(k, new Vector3(w * 0.5f, 0.6f * s, 1.2f * s), Vector3.right, 0.28f * s, Caps[1]);
            ShelfFungi(k, new Vector3(w * 0.3f, 1.9f * s, d * 0.5f), Vector3.forward, 0.22f * s, Caps[0]);
        }

        // A cluster of shelf fungi growing out of a wall (facing out along the given direction), their rims glowing.
        private static void ShelfFungi(MeshKit k, Vector3 at, Vector3 facing, float size, Color color)
        {
            facing.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, facing);
            for (int i = 0; i < 3; i++)
            {
                Vector3 c = at + side * (i - 1) * size * 0.9f + Vector3.up * (i % 2) * size * 0.7f;
                float r = size * (1f - 0.18f * i);
                Quaternion q = Quaternion.LookRotation(facing, Vector3.up);
                k.Ball(c, new Vector3(r, r * 0.22f, r * 0.75f), q, color, 1);
                k.Ball(c + Vector3.down * r * 0.08f + facing * r * 0.05f, new Vector3(r * 0.92f, r * 0.12f, r * 0.7f), q, Spot, 0, 0.8f);
            }
        }

        // A giant mushroom cap for a roof: the brim sits at the centre's height, gills underneath, glowing spots on top.
        private static void Cap(MeshKit k, Vector3 centre, float radius, float height, Color color)
        {
            const int sides = 12;
            k.Lathe(centre, Quaternion.identity, new[] { new Vector2(0.0001f, 0.1f), new Vector2(radius * 0.9f, -0.22f) }, sides, Gill);
            k.Lathe(centre, Quaternion.identity, new[]
            {
                new Vector2(radius * 0.9f, -0.22f), new Vector2(radius, 0.05f), new Vector2(radius * 0.88f, height * 0.42f),
                new Vector2(radius * 0.6f, height * 0.78f), new Vector2(radius * 0.28f, height * 0.97f), new Vector2(0.0001f, height)
            }, sides, color);
            for (int i = 0; i < 10; i++)
            {
                bool outer = i % 2 == 0;
                float a = i * Mathf.PI * 2f / 10f + (outer ? 0f : 0.3f);
                float rr = outer ? radius * 0.74f : radius * 0.44f, y = outer ? height * 0.6f : height * 0.875f;
                Vector2 slope = outer ? new Vector2(-0.28f * radius, 0.36f * height) : new Vector2(-0.32f * radius, 0.19f * height);
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 normal = (dir * slope.y - Vector3.up * slope.x).normalized;
                float spot = radius * 0.075f;
                k.Ball(centre + dir * rr + Vector3.up * y, new Vector3(spot, spot * 0.45f, spot), Quaternion.FromToRotation(Vector3.up, normal), Spot, 0, 1.2f);
            }
        }

        // Pale ribs following a lathe's profile at each corner of its sides (a husk's bracing).
        private static void Ribs(MeshKit k, Vector3 centre, Vector2[] profile, int sides, float offset)
        {
            var points = new System.Collections.Generic.List<Vector2>();
            foreach (Vector2 p in profile) if (p.x > 0.01f) points.Add(p);
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                var path = new Vector3[points.Count];
                for (int j = 0; j < points.Count; j++) path[j] = centre + new Vector3(Mathf.Cos(a) * (points[j].x + offset), points[j].y, Mathf.Sin(a) * (points[j].x + offset));
                k.Tube(path, 0.07f, 4, Rib);
            }
        }

        // How far a lathe's faceted surface is from its axis, at a height, in the middle of a side.
        private static float SurfaceAt(Vector2[] profile, int sides, float y)
        {
            for (int i = 0; i < profile.Length - 1; i++)
                if (y >= profile[i].y && y <= profile[i + 1].y && profile[i + 1].y > profile[i].y)
                    return Mathf.Lerp(profile[i].x, profile[i + 1].x, (y - profile[i].y) / (profile[i + 1].y - profile[i].y)) * Mathf.Cos(Mathf.PI / sides);
            return profile[1].x * Mathf.Cos(Mathf.PI / sides);
        }

        // An arched door on the front (+Z) side of a lathe body.
        private static void Door(MeshKit k, Vector3 centre, Vector2[] profile, int sides, float halfWidth, float height)
        {
            float z = SurfaceAt(profile, sides, height * 0.45f);
            k.Ball(centre + new Vector3(0f, height * 0.5f, z), new Vector3(halfWidth + 0.12f, height * 0.5f + 0.1f, 0.12f), Quaternion.identity, Rib, 1);
            k.Ball(centre + new Vector3(0f, height * 0.47f, z + 0.05f), new Vector3(halfWidth, height * 0.48f, 0.1f), Quaternion.identity, DarkWood, 1);
        }

        // A round glowing window set into the side of a lathe body that faces the given direction (degrees from +X).
        private static void PodWindow(MeshKit k, Vector3 centre, Vector2[] profile, int sides, float degrees, float y, float radius)
        {
            float a = degrees * Mathf.Deg2Rad;
            var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            FlatWindow(k, centre + dir * SurfaceAt(profile, sides, y) + Vector3.up * y, dir, radius);
        }

        private static void FlatWindow(MeshKit k, Vector3 at, Vector3 facing, float radius)
        {
            Quaternion r = Quaternion.LookRotation(facing, Vector3.up);
            k.Ball(at, new Vector3(radius * 1.3f, radius * 1.3f, 0.08f), r, Rib, 1);
            k.Ball(at + facing.normalized * 0.05f, new Vector3(radius, radius, 0.07f), r, Window, 1, 2.2f);
        }

        // One giant curled leaf standing up from a base point, leaning the given way.
        private static void Frond(MeshKit k, Vector3 at, Vector3 lean, float length, Color color)
        {
            Vector3 mid = at + lean.normalized * length * 0.6f, tip = mid + (lean.normalized * 0.5f + Vector3.down * 0.35f) * length * 0.45f;
            Vector3 side = Vector3.Cross(lean, Vector3.up).sqrMagnitude > 1e-4f ? Vector3.Cross(lean, Vector3.up).normalized : Vector3.right;
            Vector3 up = Vector3.Cross(side, lean).normalized;
            k.Plank(at, mid, 0.75f, 0.05f, color, up);
            k.Plank(mid, tip, 0.5f, 0.05f, color, up);
        }

        // Rows of overlapping leaves over a roof plane, eave (e0..e1) to ridge (r0..r1).
        private static void LeafRoof(MeshKit k, Vector3 e0, Vector3 e1, Vector3 r0, Vector3 r1, int rows, int across)
        {
            Vector3 normal = Vector3.Cross(r0 - e0, e1 - e0).normalized;
            if (normal.y < 0f) normal = -normal;
            float width = (e1 - e0).magnitude / across * 1.2f;
            for (int row = 0; row < rows; row++)
                for (int c = 0; c < across; c++)
                {
                    float u = (c + 0.5f + (row % 2) * 0.5f) / (across + 0.5f), t0 = row / (float)rows - (row == 0 ? 0.12f : 0f), t1 = Mathf.Min(1f, (row + 1.35f) / rows);
                    Vector3 eave = Vector3.Lerp(e0, e1, u), ridge = Vector3.Lerp(r0, r1, u);
                    Vector3 lift = normal * (0.06f + row * 0.05f);
                    Vector3 from = eave + (ridge - eave) * t0 + lift, to = eave + (ridge - eave) * t1 + lift;
                    k.Plank(from, to, width, 0.07f, (row + c) % 3 == 0 ? LeafDark : Leaf, normal);
                    k.Rod(from + normal * 0.05f, to + normal * 0.05f, 0.04f, 4, Rib); // the midrib
                }
        }

        /// <summary>One collision box in a model's space: centre, size and rotation (degrees). Roof pieces are left out of the
        /// NavMesh (nothing should walk or land up there), and boxes that are rotated or roofs go on their own child object.</summary>
        public struct Solid
        {
            public Vector3 centre, size, angles;
            public bool roof;

            public Solid(Vector3 centre, Vector3 size, Vector3 angles = default, bool roof = false)
            {
                this.centre = centre;
                this.size = size;
                this.angles = angles;
                this.roof = roof;
            }
        }

        /// <summary>
        /// The boxes that make a kind solid, shaped to the model: walls, roofs and caps, porches and posts, so you can't
        /// walk through anything you can see or stand inside a roof. The level builder turns them into BoxColliders.
        /// </summary>
        public static Solid[] Solids(Kind kind, float size = 1f)
        {
            var list = new System.Collections.Generic.List<Solid>();
            void Box(Vector3 centre, Vector3 boxSize, Vector3 angles = default, bool roof = false) => list.Add(new Solid(centre, boxSize, angles, roof));
            // A round shape (a hut body, a cap) as a plus of two boxes and a square: close to an octagon.
            void Round(float y0, float y1, float r, bool roof, float cz = 0f)
            {
                float height = y1 - y0, y = (y0 + y1) * 0.5f;
                Box(new Vector3(0f, y, cz), new Vector3(r * 1.9f, height, r), default, roof);
                Box(new Vector3(0f, y, cz), new Vector3(r, height, r * 1.9f), default, roof);
                Box(new Vector3(0f, y, cz), new Vector3(r * 1.45f, height, r * 1.45f), default, roof);
            }
            // A sloped roof slab whose top face runs from the eave to the ridge (in the X-Y plane), deep along Z.
            void Slab(Vector2 eave, Vector2 ridge, float depth, float thickness, float cz = 0f)
            {
                Vector2 along = ridge - eave;
                var normal = new Vector2(-along.y, along.x).normalized;
                if (normal.y < 0f) normal = new Vector2(-normal.x, -normal.y);
                Vector2 mid = (eave + ridge) * 0.5f - new Vector2(normal.x, normal.y) * (thickness * 0.5f);
                float angle = Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg;
                Box(new Vector3(mid.x, mid.y, cz), new Vector3(along.magnitude, thickness, depth), new Vector3(0f, 0f, angle), true);
            }
            switch (kind)
            {
                case Kind.Cabin:
                    Round(0f, 2.45f, 2.3f, false);
                    Round(2.25f, 2.9f, 2.9f, true);
                    Round(2.9f, 3.95f, 1.85f, true);
                    break;
                case Kind.StiltShack:
                {
                    const float lift = 1.8f;
                    Box(new Vector3(0f, (lift + 0.1f) * 0.5f, 0.4f), new Vector3(3.8f, lift + 0.1f, 4.6f)); // stilts, floor and porch
                    Round(lift + 0.1f, lift + 2.5f, 1.9f, false, -0.1f);
                    Round(lift + 2.3f, lift + 3.05f, 2.45f, true, -0.1f);
                    Round(lift + 3.05f, lift + 3.8f, 1.55f, true, -0.1f);
                    break;
                }
                case Kind.DrivingSchool:
                    Box(new Vector3(0f, 2.29f, 0f), new Vector3(5.9f, 4.58f, 6.4f)); // walls up to the roof deck the possums sit on
                    foreach (float sx in new[] { -1f, 1f })
                        foreach (float sz in new[] { -1f, 1f }) Box(new Vector3(sx * 2.95f, 2.5f, sz * 3.2f), new Vector3(1.1f, 5f, 1.1f)); // pillars
                    foreach (float sx in new[] { -1f, 1f }) Box(new Vector3(sx * 3.2f, 4.95f, 0f), new Vector3(0.4f, 0.8f, 6.4f), default, true); // the frond rim
                    Box(new Vector3(0f, 4.95f, -3.4f), new Vector3(5.9f, 0.8f, 0.4f), default, true);
                    Box(new Vector3(0f, 4.95f, 3.3f), new Vector3(5.6f, 1.15f, 0.3f), default, true); // the sign
                    break;
                case Kind.Roadhouse:
                {
                    const float s = 2.2f;
                    float w = 4.2f * s, d = 4.6f * s, h = 2.6f * s, rise = 1.6f * s;
                    Box(new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, d));
                    Slab(new Vector2(-w * 0.62f, h), new Vector2(0f, h + rise), d * 1.1f, 0.5f);
                    Slab(new Vector2(w * 0.62f, h), new Vector2(0f, h + rise), d * 1.1f, 0.5f);
                    Box(new Vector3(0f, 0.3f, 4.6f), new Vector3(11f, 0.25f, 2.4f)); // the porch
                    foreach (float x in new[] { -5f, -1.6f, 1.6f, 5f }) Box(new Vector3(x, 1.75f, 5.6f), new Vector3(0.35f, 2.9f, 0.35f));
                    Box(new Vector3(0f, 3.25f, 4.9f), new Vector3(11.4f, 0.25f, 2.8f), new Vector3(-8f, 0f, 0f), true); // the leaf awning
                    Box(new Vector3(0f, 6.6f, 5.62f), new Vector3(7f, 1.4f, 0.3f), default, true); // the sign
                    break;
                }
                case Kind.WitchHut:
                    Box(new Vector3(0f, 1.6f, 0f), new Vector3(3.8f, 3.2f, 3.8f), new Vector3(0f, 0f, 3f));
                    Round(3.0f, 3.9f, 2.7f, true, 0f);
                    Round(3.9f, 5.3f, 1.7f, true, 0f);
                    break;
                case Kind.Sawmill:
                    Box(new Vector3(0f, 0.8f, 0f), new Vector3(6.4f, 1.6f, 5.4f)); // the bench and the logs
                    foreach (float x in new[] { -3.8f, 3.8f })
                        foreach (float z in new[] { -2.85f, 2.85f }) Box(new Vector3(x, 2.15f, z), new Vector3(0.7f, 4.3f, 0.7f)); // the stalks
                    Round(3.95f, 4.7f, 5.1f, true);
                    Round(4.7f, 6.0f, 3.3f, true);
                    break;
                case Kind.Windmill:
                    Box(new Vector3(0f, 2.25f, 0f), new Vector3(2.2f, 4.5f, 2.2f));
                    Box(new Vector3(0f, 6.75f, 0f), new Vector3(1.5f, 4.5f, 1.5f));
                    break;
                case Kind.RoadSign:
                    Box(new Vector3(0f, 1.2f, 0f), new Vector3(0.25f, 2.4f, 0.25f));
                    break;
                case Kind.Outhouse:
                    Box(new Vector3(0f, 1.15f, 0f), new Vector3(1.3f, 2.3f, 1.3f));
                    break;
                case Kind.MoonshineStill:
                    Box(new Vector3(0f, 1.2f, 0.15f), new Vector3(2.7f, 2.4f, 3.3f)); // the gourd and the firebox
                    Box(new Vector3(2.2f, 0.8f, 0f), new Vector3(1.1f, 1.6f, 1.1f)); // the coil
                    break;
                case Kind.Grandstand:
                    for (int i = 0; i < 4; i++) Box(new Vector3(0f, (0.36f + i * 0.5f) * 0.5f, -i * 0.8f), new Vector3(10f, 0.36f + i * 0.5f, 0.8f)); // steps you can climb
                    Box(new Vector3(0f, 3.4f, -2.8f), new Vector3(10.4f, 0.2f, 1.4f), new Vector3(-10f, 0f, 0f), true);
                    break;
                case Kind.BananaStand:
                    Box(new Vector3(0f, 0.6f, 0f), new Vector3(2.5f, 1.2f, 1.2f));
                    foreach (float x in new[] { -1.1f, 1.1f }) Box(new Vector3(x, 1.7f, -0.4f), new Vector3(0.2f, 1.4f, 0.2f));
                    Box(new Vector3(0f, 2.45f, -0.2f), new Vector3(2.6f, 0.15f, 1.4f), new Vector3(-12f, 0f, 0f), true);
                    break;
                default:
                    foreach ((Vector3 centre, Vector3 boxSize) in Colliders(kind, size)) Box(centre, boxSize);
                    break;
            }
            return list.ToArray();
        }

        /// <summary>Axis-aligned boxes for the simple kinds (trees, rocks, props), centre and size. Buildings are in
        /// <see cref="Solids"/>, which every solid prop goes through.</summary>
        public static (Vector3 centre, Vector3 size)[] Colliders(Kind kind, float size = 1f)
        {
            switch (kind)
            {
                case Kind.Cypress: return new[] { (new Vector3(0f, 4f, 0f), new Vector3(1.2f, 8f, 1.2f)) };
                case Kind.Willow: return new[] { (new Vector3(0.3f, 3.5f, 0.2f), new Vector3(1.8f, 7f, 1.8f)) };
                case Kind.DeadTree: return new[] { (new Vector3(0f, 3f, 0f), new Vector3(0.8f, 6f, 0.8f)) };
                case Kind.Rock: return new[] { (new Vector3(0f, 0.35f, 0f), new Vector3(1.6f, 0.9f, 1.6f)) };
                case Kind.Boulder: return new[] { (new Vector3(0f, 1f, 0f), new Vector3(3.6f, 2.4f, 3.6f)) };
                case Kind.Stump: return new[] { (new Vector3(0f, 0.35f, 0f), new Vector3(1.2f, 0.7f, 1.2f)) };
                case Kind.Log: return new[] { (new Vector3(0f, 0.4f, 0f), new Vector3(4.4f, 0.8f, 0.8f)) };
                case Kind.Kiosk: return new[] { (new Vector3(0f, 0.9f, 0f), new Vector3(1.1f, 1.8f, 0.9f)) };
                case Kind.Barricade: return new[] { (new Vector3(0f, 0.9f, 0f), new Vector3(3.9f, 1.8f, 0.9f)) };
                case Kind.Fence: return new[] { (new Vector3(0f, 0.6f, 0f), new Vector3(3.9f, 1.2f, 0.25f)) };
                case Kind.Crate: return new[] { (new Vector3(0f, 0.45f, 0f), new Vector3(0.9f, 0.9f, 0.9f)) };
                case Kind.HayBale: return new[] { (new Vector3(0f, 0.45f, 0f), new Vector3(1.6f, 0.9f, 1f)) };
                case Kind.MoonshineBarrel: return new[] { (new Vector3(0f, 0.5f, 0f), new Vector3(0.85f, 1f, 0.85f)) };
                case Kind.Dam: return new[] { (new Vector3(0f, 1.4f, 0f), new Vector3(17f, 2.8f, 4f)) };
                case Kind.Gong: return new[] { (new Vector3(0f, 1.6f, 0f), new Vector3(3f, 3.2f, 0.6f)) };
                case Kind.CursedLantern: return new[] { (new Vector3(0.2f, 2.2f, 0f), new Vector3(1.1f, 4.4f, 0.6f)) };
                case Kind.LanternPost: return new[] { (new Vector3(0f, 1.5f, 0f), new Vector3(0.3f, 3f, 0.3f)) };
                case Kind.StepStone: return new[] { (new Vector3(0f, -0.2f, 0f), new Vector3(1.6f, 0.8f, 1.6f)) };
                case Kind.Bridge: return new[] { (new Vector3(0f, -0.05f, 0f), new Vector3(3.6f, 0.2f, 10f * size)), (new Vector3(-1.9f, 0.5f, 0f), new Vector3(0.2f, 1f, 10f * size)), (new Vector3(1.9f, 0.5f, 0f), new Vector3(0.2f, 1f, 10f * size)) };
                case Kind.Boardwalk: return new[] { (new Vector3(0f, -0.05f, 0f), new Vector3(2f, 0.2f, 6f * size)) };
                case Kind.TyreWall: return new[] { (new Vector3(0f, 0.3f, 0f), new Vector3(6f, 0.7f, 1f)) };
                case Kind.Cauldron: return new[] { (new Vector3(0f, 0.75f, 0f), new Vector3(2.4f, 1.5f, 2.4f)) };
                case Kind.DojoGate: return new[] { (new Vector3(-2.2f, 2f, 0f), new Vector3(0.5f, 4f, 0.5f)), (new Vector3(2.2f, 2f, 0f), new Vector3(0.5f, 4f, 0.5f)) };
                case Kind.StoneLantern: return new[] { (new Vector3(0f, 0.8f, 0f), new Vector3(0.7f, 1.6f, 0.7f)) };
                case Kind.GasPump: return new[] { (new Vector3(0f, 1f, 0f), new Vector3(0.8f, 2f, 0.6f)) };
                case Kind.GatePost: return new[] { (new Vector3(0f, 1.6f, 0f), new Vector3(0.5f, 3.2f, 0.5f)) };
                case Kind.CountyLine: return new[] { (new Vector3(-5f, 2.5f, 0f), new Vector3(0.5f, 5f, 0.5f)), (new Vector3(5f, 2.5f, 0f), new Vector3(0.5f, 5f, 0.5f)) };
                case Kind.Rowboat: return new[] { (new Vector3(0f, 0.3f, 0f), new Vector3(1.4f, 0.6f, 3.6f)) };
                default: return Array.Empty<(Vector3, Vector3)>();
            }
        }
    }
}
