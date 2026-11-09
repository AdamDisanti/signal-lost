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

        private static readonly Color Leaf = new Color(0.22f, 0.38f, 0.24f), LeafDark = new Color(0.15f, 0.28f, 0.2f),
            MossHang = new Color(0.4f, 0.5f, 0.32f), Window = new Color(1f, 0.72f, 0.32f), Tin = new Color(0.5f, 0.52f, 0.5f),
            Copper = new Color(0.72f, 0.42f, 0.22f), Orange = new Color(0.93f, 0.45f, 0.12f), Bronze = new Color(0.72f, 0.55f, 0.25f),
            Banana = new Color(1f, 0.86f, 0.25f), Teal = new Color(0.3f, 0.95f, 0.85f);

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
                    k.Lathe(Vector3.zero, Quaternion.identity, new[] { new Vector2(1.4f, 0f), new Vector2(0.75f, 0.9f), new Vector2(0.5f, 2.5f), new Vector2(0.38f, h), new Vector2(0.0001f, h + 0.2f) }, 7, MudPalette.Bark);
                    for (int i = 0; i < 4; i++) // knees poking out of the mud
                        k.Cone(new Vector3(R(-2f, 2f), 0f, R(-2f, 2f)), Quaternion.identity, 0.15f, R(0.4f, 0.8f), 5, MudPalette.Bark);
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
                    for (int i = 0; i < trunk.Length - 1; i++) k.Rod(trunk[i], trunk[i + 1], 1f - i * 0.25f, 7, MudPalette.Bark, 0f, 0.8f - i * 0.22f);
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i / 5f * Mathf.PI * 2f + R(0f, 0.5f);
                        Vector3 tip = trunk[3] + new Vector3(Mathf.Cos(a) * R(3f, 4.5f), R(-0.5f, 1.2f), Mathf.Sin(a) * R(3f, 4.5f));
                        k.Rod(trunk[2] + Vector3.up, tip, 0.35f, 5, MudPalette.Bark, 0f, 0.12f);
                        k.Ball(tip + Vector3.up * 0.4f, new Vector3(1.8f, 0.7f, 1.8f), Quaternion.identity, Leaf, 0);
                        for (int m = 0; m < 3; m++) Moss(k, tip + new Vector3(R(-1.4f, 1.4f), 0f, R(-1.4f, 1.4f)), R(2f, 3.5f));
                    }
                    break;
                }
                case Kind.DeadTree:
                {
                    float h = R(5f, 8f);
                    k.Rod(Vector3.zero, new Vector3(R(-0.5f, 0.5f), h, R(-0.5f, 0.5f)), 0.45f, 6, MudPalette.GreyWood, 0f, 0.15f);
                    for (int i = 0; i < 4; i++)
                    {
                        float y = R(h * 0.4f, h * 0.9f), a = R(0f, Mathf.PI * 2f);
                        Vector3 from = new Vector3(0f, y, 0f), to = from + new Vector3(Mathf.Cos(a) * R(1.5f, 2.5f), R(0.5f, 2f), Mathf.Sin(a) * R(1.5f, 2.5f));
                        k.Rod(from, to, 0.16f, 5, MudPalette.GreyWood, 0f, 0.04f);
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
                        if (i % 2 == 0) k.Rod(top - Vector3.up * 0.38f, top - Vector3.up * 0.05f, 0.06f, 6, new Color(0.38f, 0.24f, 0.13f));
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
                    k.Prism(Vector3.zero, Quaternion.identity, 0.7f, 0.55f, 0.7f, 7, MudPalette.Bark);
                    k.Prism(new Vector3(0f, 0.7f, 0f), Quaternion.identity, 0.53f, 0.53f, 0.02f, 7, MudPalette.Wood);
                    break;
                case Kind.Log:
                    k.Rod(new Vector3(-2.2f, 0.4f, 0f), new Vector3(2.2f, 0.4f, 0f), 0.4f, 7, MudPalette.Bark, 0f, 0.35f);
                    k.Ball(new Vector3(0.6f, 0.75f, 0f), new Vector3(0.9f, 0.15f, 0.4f), Quaternion.identity, MudPalette.Moss, 0);
                    break;

                case Kind.StiltShack: StiltShack(k, R); break;
                case Kind.Cabin: Cabin(k); break;
                case Kind.DrivingSchool:
                    Cabin(k, 1.4f);
                    k.Box(new Vector3(0f, 4.3f, 2.9f), new Vector3(5.4f, 1f, 0.12f), MudPalette.Cream); // sign board (text added by MudProp)
                    k.Box(new Vector3(0f, 4.3f, 2.85f), new Vector3(5.6f, 1.15f, 0.08f), Orange);
                    break;
                case Kind.Kiosk:
                    k.Box(new Vector3(0f, 0.8f, 0f), new Vector3(1f, 1.6f, 0.8f), MudPalette.GreyWood);
                    k.Box(new Vector3(0f, 1.25f, 0.41f), new Vector3(0.7f, 0.5f, 0.02f), Teal, 2f);
                    k.Box(new Vector3(0f, 1.75f, 0f), new Vector3(1.2f, 0.12f, 1f), MudPalette.Rust);
                    k.Box(new Vector3(0f, 2.15f, 0f), new Vector3(0.9f, 0.45f, 0.08f), MudPalette.Cream);
                    k.Box(new Vector3(0f, 0.7f, 0.42f), new Vector3(0.2f, 0.1f, 0.05f), new Color(0.8f, 0.2f, 0.15f));
                    break;
                case Kind.Sawmill:
                    foreach (Vector3 p in new[] { new Vector3(-4f, 0f, -3f), new Vector3(4f, 0f, -3f), new Vector3(-4f, 0f, 3f), new Vector3(4f, 0f, 3f) })
                        k.Rod(p, p + Vector3.up * 4.2f, 0.25f, 6, MudPalette.DarkWood);
                    k.Box(new Vector3(0f, 4.4f, 0f), new Vector3(9.5f, 0.2f, 7.5f), Quaternion.Euler(0f, 0f, 6f), Tin);
                    k.Box(new Vector3(0f, 0.6f, 0f), new Vector3(6f, 1.2f, 1.6f), MudPalette.Wood);
                    k.Prism(new Vector3(0f, 1.2f, 0f), Quaternion.Euler(90f, 0f, 0f), 1.1f, 1.1f, 0.06f, 16, MudPalette.Metal);
                    for (int i = 0; i < 4; i++) k.Rod(new Vector3(-3f, 0.4f + (i % 2) * 0.7f, -2.3f + i * 0.45f), new Vector3(3f, 0.4f + (i % 2) * 0.7f, -2.3f + i * 0.45f), 0.35f, 7, MudPalette.Bark);
                    break;
                case Kind.Outhouse:
                    k.Box(new Vector3(0f, 1.1f, 0f), new Vector3(1.2f, 2.2f, 1.2f), MudPalette.Wood);
                    k.Box(new Vector3(0f, 2.3f, 0f), new Vector3(1.4f, 0.12f, 1.4f), Quaternion.Euler(8f, 0f, 0f), Tin);
                    k.Box(new Vector3(0f, 1.8f, 0.61f), new Vector3(0.18f, 0.25f, 0.02f), MudPalette.Black); // the moon
                    break;
                case Kind.Barricade:
                    foreach (float x in new[] { -1.6f, 1.6f })
                    {
                        k.Plank(new Vector3(x, 0f, -0.4f), new Vector3(x, 1.2f, 0f), 0.12f, 0.12f, MudPalette.Wood);
                        k.Plank(new Vector3(x, 0f, 0.4f), new Vector3(x, 1.2f, 0f), 0.12f, 0.12f, MudPalette.Wood);
                    }
                    for (int i = 0; i < 2; i++)
                    {
                        k.Box(new Vector3(0f, 0.8f + i * 0.45f, 0f), new Vector3(3.8f, 0.3f, 0.08f), MudPalette.White);
                        for (int s = 0; s < 5; s++) k.Box(new Vector3(-1.5f + s * 0.75f, 0.8f + i * 0.45f, 0.045f), new Vector3(0.3f, 0.3f, 0.02f), Quaternion.Euler(0f, 0f, 35f), Orange);
                    }
                    k.Box(new Vector3(0f, 1.75f, 0.06f), new Vector3(1.8f, 0.45f, 0.05f), new Color(0.85f, 0.15f, 0.1f)); // ROAD CLOSED
                    break;
                case Kind.Fence:
                    for (int i = 0; i < 3; i++) k.Box(new Vector3(-1.8f + i * 1.8f, 0.6f, 0f), new Vector3(0.14f, 1.2f, 0.14f), MudPalette.GreyWood);
                    k.Box(new Vector3(0f, 0.45f, 0f), new Vector3(3.9f, 0.12f, 0.06f), MudPalette.GreyWood);
                    k.Box(new Vector3(0f, 0.95f, 0f), new Vector3(3.9f, 0.12f, 0.06f), Quaternion.Euler(0f, 0f, R(-4f, 4f)), MudPalette.GreyWood);
                    break;
                case Kind.Crate:
                    k.Box(new Vector3(0f, 0.45f, 0f), new Vector3(0.9f, 0.9f, 0.9f), MudPalette.Wood);
                    k.Box(new Vector3(0f, 0.45f, 0.46f), new Vector3(0.95f, 0.12f, 0.03f), Quaternion.Euler(0f, 0f, 45f), MudPalette.DarkWood);
                    break;
                case Kind.HayBale:
                    k.Box(new Vector3(0f, 0.45f, 0f), new Vector3(1.6f, 0.9f, 1f), new Color(0.82f, 0.7f, 0.38f));
                    k.Box(new Vector3(-0.4f, 0.45f, 0f), new Vector3(0.05f, 0.92f, 1.02f), new Color(0.5f, 0.3f, 0.15f));
                    k.Box(new Vector3(0.4f, 0.45f, 0f), new Vector3(0.05f, 0.92f, 1.02f), new Color(0.5f, 0.3f, 0.15f));
                    break;
                case Kind.MoonshineBarrel:
                    k.Lathe(Vector3.zero, Quaternion.identity, new[] { new Vector2(0.0001f, 0f), new Vector2(0.38f, 0f), new Vector2(0.44f, 0.5f), new Vector2(0.38f, 1f), new Vector2(0.0001f, 1f) }, 10, new Color(0.5f, 0.32f, 0.18f));
                    k.Prism(new Vector3(0f, 0.2f, 0f), Quaternion.identity, 0.42f, 0.42f, 0.06f, 10, MudPalette.DarkMetal);
                    k.Prism(new Vector3(0f, 0.75f, 0f), Quaternion.identity, 0.42f, 0.42f, 0.06f, 10, MudPalette.DarkMetal);
                    k.Box(new Vector3(0f, 0.5f, 0.43f), new Vector3(0.4f, 0.16f, 0.02f), MudPalette.Cream);
                    break;
                case Kind.MoonshineStill:
                    k.Lathe(Vector3.zero, Quaternion.identity, new[] { new Vector2(0.0001f, 0f), new Vector2(1.1f, 0f), new Vector2(1.3f, 1.2f), new Vector2(0.9f, 2f), new Vector2(0.3f, 2.4f), new Vector2(0.0001f, 2.4f) }, 12, Copper);
                    k.Tube(new[] { new Vector3(0f, 2.4f, 0f), new Vector3(0f, 3f, 0f), new Vector3(1.4f, 2.8f, 0f), new Vector3(2f, 1.8f, 0f) }, 0.12f, 6, Copper);
                    for (int i = 0; i < 4; i++) k.Prism(new Vector3(2.2f, 0.4f + i * 0.3f, 0f), Quaternion.identity, 0.5f, 0.5f, 0.12f, 10, Copper); // the coil
                    k.Box(new Vector3(0f, 0.2f, 1.4f), new Vector3(1.6f, 0.4f, 0.6f), MudPalette.Black); // the firebox
                    k.Box(new Vector3(0f, 0.25f, 1.71f), new Vector3(0.6f, 0.25f, 0.02f), MudPalette.Fire, 3f);
                    break;
                case Kind.Dam:
                    k.Ball(new Vector3(0f, 1.2f, 0f), new Vector3(9f, 2.2f, 2.6f), Quaternion.identity, MudPalette.Mud, 1);
                    for (int i = 0; i < 9; i++) k.Rod(new Vector3(-8f + i * 2f, 0.6f + (i % 3) * 0.8f, R(-1f, 1f)), new Vector3(-6.5f + i * 2f, 1f + (i % 2) * 0.9f, R(-1.5f, 1.5f)), 0.25f, 6, MudPalette.Bark);
                    break;
                case Kind.Gong:
                    k.Rod(new Vector3(-1.2f, 0f, 0f), new Vector3(-1.2f, 3f, 0f), 0.12f, 6, MudPalette.DarkWood);
                    k.Rod(new Vector3(1.2f, 0f, 0f), new Vector3(1.2f, 3f, 0f), 0.12f, 6, MudPalette.DarkWood);
                    k.Box(new Vector3(0f, 3.05f, 0f), new Vector3(3f, 0.25f, 0.3f), new Color(0.6f, 0.15f, 0.12f));
                    k.Box(new Vector3(0f, 0f, 0f), new Vector3(3f, 0.2f, 1f), MudPalette.Stone);
                    break;
                case Kind.CursedLantern:
                    k.Rod(Vector3.zero, new Vector3(0f, 4.2f, 0f), 0.13f, 6, MudPalette.DarkWood);
                    k.Rod(new Vector3(0f, 4.1f, 0f), new Vector3(0.5f, 4.3f, 0f), 0.06f, 5, MudPalette.DarkWood);
                    k.Box(new Vector3(0.55f, 3.8f, 0f), new Vector3(0.35f, 0.45f, 0.35f), MudPalette.DarkMetal);
                    k.Ball(new Vector3(0.55f, 3.8f, 0f), 0.18f, MudPalette.Curse, 1, 4f);
                    break;
                case Kind.LanternPost:
                    k.Rod(Vector3.zero, new Vector3(0f, 3f, 0f), 0.1f, 6, MudPalette.DarkWood);
                    k.Tube(new[] { new Vector3(0f, 2.9f, 0f), new Vector3(0.1f, 3.3f, 0f), new Vector3(0.5f, 3.3f, 0f), new Vector3(0.55f, 3.05f, 0f) }, 0.04f, 5, MudPalette.DarkWood);
                    k.Box(new Vector3(0.55f, 2.8f, 0f), new Vector3(0.28f, 0.36f, 0.28f), MudPalette.DarkMetal);
                    k.Ball(new Vector3(0.55f, 2.8f, 0f), 0.14f, MudPalette.Lantern, 1, 4f);
                    break;
                case Kind.TikiTorch:
                    k.Rod(Vector3.zero, new Vector3(0f, 1.9f, 0f), 0.06f, 5, MudPalette.DarkWood);
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
                        k.Box(new Vector3(0f, 0f, -length * 0.5f + 0.25f + i * 0.5f), new Vector3(3.6f, 0.12f, 0.44f), Quaternion.Euler(0f, R(-2f, 2f), 0f), i % 3 == 0 ? MudPalette.GreyWood : MudPalette.Wood);
                    foreach (float x in new[] { -1.9f, 1.9f })
                    {
                        for (float z = -length * 0.5f; z <= length * 0.5f + 0.01f; z += 2.5f) k.Rod(new Vector3(x, -1.5f, z), new Vector3(x, 1f, z), 0.12f, 5, MudPalette.DarkWood);
                        k.Box(new Vector3(x, 0.9f, 0f), new Vector3(0.1f, 0.1f, length), MudPalette.DarkWood);
                    }
                    break;
                }
                case Kind.Boardwalk:
                {
                    float length = 6f * size;
                    for (int i = 0; i < Mathf.RoundToInt(length / 0.45f); i++)
                        k.Box(new Vector3(0f, 0f, -length * 0.5f + 0.22f + i * 0.45f), new Vector3(2f, 0.1f, 0.4f), Quaternion.Euler(R(-2f, 2f), R(-3f, 3f), 0f), i % 4 == 0 ? MudPalette.GreyWood : MudPalette.Wood);
                    for (float z = -length * 0.5f; z <= length * 0.5f + 0.01f; z += 2f)
                        foreach (float x in new[] { -0.9f, 0.9f }) k.Rod(new Vector3(x, -1.2f, z), new Vector3(x, 0.05f, z), 0.08f, 5, MudPalette.DarkWood);
                    break;
                }
                case Kind.TyreWall:
                    for (int i = 0; i < 6; i++)
                        for (int j = 0; j < 2; j++)
                            k.Prism(new Vector3(-2.5f + i, j * 0.32f, 0f), Quaternion.identity, 0.48f, 0.48f, 0.3f, 9, (i + j) % 3 == 0 ? new Color(0.85f, 0.15f, 0.1f) : MudPalette.Rubber);
                    break;
                case Kind.Grandstand:
                    for (int i = 0; i < 4; i++)
                        k.Box(new Vector3(0f, 0.3f + i * 0.5f, -i * 0.8f), new Vector3(10f, 0.12f, 0.8f), MudPalette.Wood);
                    for (int i = 0; i < 6; i++) k.Rod(new Vector3(-5f + i * 2f, 0f, -2.6f), new Vector3(-5f + i * 2f, 2f, -2.6f), 0.1f, 5, MudPalette.DarkWood);
                    k.Box(new Vector3(0f, 3.4f, -2.8f), new Vector3(10.4f, 0.12f, 1.4f), Quaternion.Euler(-10f, 0f, 0f), new Color(0.85f, 0.15f, 0.1f));
                    break;
                case Kind.BananaStand:
                    k.Box(new Vector3(0f, 0.5f, 0f), new Vector3(2.4f, 1f, 1f), MudPalette.Wood);
                    for (int i = 0; i < 6; i++) k.Rod(new Vector3(-1f + i * 0.4f, 1.1f, 0f), new Vector3(-0.8f + i * 0.4f, 1.15f, 0.25f), 0.08f, 5, Banana);
                    k.Rod(new Vector3(-1.1f, 1f, -0.4f), new Vector3(-1.1f, 2.4f, -0.4f), 0.06f, 5, MudPalette.DarkWood);
                    k.Rod(new Vector3(1.1f, 1f, -0.4f), new Vector3(1.1f, 2.4f, -0.4f), 0.06f, 5, MudPalette.DarkWood);
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
                    k.Box(new Vector3(0f, 1.6f, 0f), new Vector3(3.6f, 3.2f, 3.6f), Quaternion.Euler(0f, 0f, 3f), new Color(0.32f, 0.28f, 0.3f));
                    k.Cone(new Vector3(0.2f, 3.1f, 0f), Quaternion.Euler(0f, 0f, -8f), 3f, 4f, 5, new Color(0.28f, 0.18f, 0.32f));
                    k.Cone(new Vector3(-0.5f, 6.5f, 0f), Quaternion.Euler(0f, 0f, 25f), 0.6f, 1.6f, 5, new Color(0.28f, 0.18f, 0.32f));
                    k.Box(new Vector3(0f, 1.0f, 1.81f), new Vector3(1f, 2f, 0.05f), MudPalette.DarkWood);
                    k.Box(new Vector3(1f, 2.3f, 1.81f), new Vector3(0.6f, 0.6f, 0.04f), MudPalette.Curse, 2f);
                    break;
                case Kind.DojoGate:
                    foreach (float x in new[] { -2.2f, 2.2f }) k.Rod(new Vector3(x, 0f, 0f), new Vector3(x, 4f, 0f), 0.22f, 6, new Color(0.45f, 0.2f, 0.12f));
                    k.Box(new Vector3(0f, 4.1f, 0f), new Vector3(6f, 0.35f, 0.5f), MudPalette.DarkWood);
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
                    Cabin(k, 2.2f);
                    k.Box(new Vector3(0f, 0.3f, 4.6f), new Vector3(11f, 0.25f, 2.4f), MudPalette.Wood); // the porch
                    foreach (float x in new[] { -5f, -1.6f, 1.6f, 5f }) k.Rod(new Vector3(x, 0.3f, 5.6f), new Vector3(x, 3.2f, 5.6f), 0.12f, 5, MudPalette.DarkWood);
                    k.Box(new Vector3(0f, 3.25f, 4.9f), new Vector3(11.4f, 0.15f, 2.8f), Quaternion.Euler(-8f, 0f, 0f), Tin);
                    k.Box(new Vector3(0f, 6.6f, 3.3f), new Vector3(7f, 1.4f, 0.15f), MudPalette.Black); // sign board (neon lights from MudProp)
                    break;
                case Kind.GasPump:
                    k.Box(new Vector3(0f, 0.9f, 0f), new Vector3(0.7f, 1.8f, 0.5f), new Color(0.8f, 0.15f, 0.12f));
                    k.Ball(new Vector3(0f, 2f, 0f), new Vector3(0.32f, 0.32f, 0.12f), Quaternion.identity, MudPalette.White, 1, 1.5f);
                    k.Tube(new[] { new Vector3(0.36f, 1.2f, 0f), new Vector3(0.55f, 0.6f, 0.2f), new Vector3(0.45f, 1f, 0.35f) }, 0.04f, 4, MudPalette.Black);
                    break;
                case Kind.Ramp:
                {
                    // A wedge of planks on a dirt bank: 8 m long, 2.6 m high at the lip (+Z).
                    Vector3 a = new Vector3(-2f, 0f, -4f), b = new Vector3(2f, 0f, -4f), c = new Vector3(2f, 2.6f, 4f), d = new Vector3(-2f, 2.6f, 4f);
                    k.Quad(a, d, c, b, MudPalette.Wood);
                    k.Quad(new Vector3(-2f, 0f, 4f), new Vector3(2f, 0f, 4f), c, d, MudPalette.DarkWood);
                    k.Tri(a, new Vector3(-2f, 0f, 4f), d, MudPalette.Mud);
                    k.Tri(b, c, new Vector3(2f, 0f, 4f), MudPalette.Mud);
                    break;
                }
                case Kind.GatePost:
                    k.Box(new Vector3(0f, 1.6f, 0f), new Vector3(0.5f, 3.2f, 0.5f), MudPalette.DarkWood);
                    k.Cone(new Vector3(0f, 3.2f, 0f), Quaternion.identity, 0.4f, 0.4f, 4, MudPalette.DarkWood);
                    break;
                case Kind.CountyLine:
                    foreach (float x in new[] { -5f, 5f }) k.Rod(new Vector3(x, 0f, 0f), new Vector3(x, 5f, 0f), 0.25f, 6, MudPalette.DarkWood);
                    k.Box(new Vector3(0f, 4.6f, 0f), new Vector3(10.6f, 1.2f, 0.2f), new Color(0.2f, 0.4f, 0.25f));
                    k.Box(new Vector3(0f, 4.6f, 0.11f), new Vector3(10f, 0.9f, 0.02f), MudPalette.White);
                    break;
                case Kind.Windmill:
                    k.Prism(Vector3.zero, Quaternion.identity, 1.5f, 0.6f, 9f, 4, MudPalette.GreyWood);
                    k.Box(new Vector3(0f, 9f, 0.6f), new Vector3(0.6f, 0.6f, 1f), MudPalette.DarkMetal);
                    for (int i = 0; i < 4; i++)
                    {
                        Quaternion r = Quaternion.Euler(0f, 0f, i * 90f + 20f + (i == 2 ? 50f : 0f));
                        k.Box(new Vector3(0f, 9f, 1.15f) + r * new Vector3(0f, 1.6f, 0f), new Vector3(0.5f, 3f, 0.05f), r, Tin);
                    }
                    break;
                case Kind.RoadSign:
                    k.Rod(Vector3.zero, new Vector3(0f, 2.4f, 0f), 0.06f, 5, MudPalette.GreyWood);
                    k.Box(new Vector3(0.5f, 2.2f, 0f), new Vector3(1.4f, 0.35f, 0.06f), Quaternion.Euler(0f, 0f, R(-6f, 6f)), MudPalette.Cream);
                    break;
                case Kind.Rowboat:
                    k.Lathe(Vector3.zero, Quaternion.Euler(90f, 0f, 0f), new[] { new Vector2(0.0001f, -1.8f), new Vector2(0.6f, -1.2f), new Vector2(0.75f, 0f), new Vector2(0.6f, 1.2f), new Vector2(0.0001f, 1.8f) }, 6, MudPalette.Wood);
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

        // The reference's crooked stilt shack: plank walls, a tall pointed roof, a glowing window, a ladder and a porch.
        private static void StiltShack(MeshKit k, Func<float, float, float> R)
        {
            float lift = 1.8f;
            foreach (Vector3 p in new[] { new Vector3(-1.6f, 0f, -1.6f), new Vector3(1.6f, 0f, -1.6f), new Vector3(-1.6f, 0f, 1.6f), new Vector3(1.6f, 0f, 1.6f), new Vector3(0f, 0f, 2.6f) })
                k.Rod(p + Vector3.down * 1.5f, p + Vector3.up * lift, 0.14f, 5, MudPalette.DarkWood);
            k.Box(new Vector3(0f, lift, 0.4f), new Vector3(3.8f, 0.2f, 4.6f), MudPalette.Wood); // floor and porch
            for (int i = 0; i < 9; i++)
                k.Box(new Vector3(-1.6f + i * 0.4f, lift + 1.5f, 0f), new Vector3(0.38f, 2.8f + R(-0.1f, 0.15f), 3.4f), Quaternion.Euler(0f, 0f, R(-1.5f, 1.5f)), i % 3 == 0 ? MudPalette.GreyWood : new Color(0.38f, 0.42f, 0.45f));
            // A tall, leaning, pointed roof.
            Vector3 ridgeA = new Vector3(0.2f, lift + 5.6f, -1.9f), ridgeB = new Vector3(0.35f, lift + 5.8f, 1.9f);
            Vector3 l0 = new Vector3(-2.2f, lift + 2.8f, -1.9f), l1 = new Vector3(-2.2f, lift + 2.8f, 1.9f), r0 = new Vector3(2.2f, lift + 2.8f, -1.9f), r1 = new Vector3(2.2f, lift + 2.8f, 1.9f);
            Color roof = new Color(0.36f, 0.33f, 0.36f);
            k.Quad(l1, ridgeB, ridgeA, l0, roof);
            k.Quad(r0, ridgeA, ridgeB, r1, roof);
            k.Tri(r1, ridgeB, l1, MudPalette.GreyWood);
            k.Tri(l0, ridgeA, r0, MudPalette.GreyWood);
            k.Box(new Vector3(0f, lift + 3.7f, 1.75f), new Vector3(0.55f, 0.45f, 0.06f), Window, 2.5f); // the glowing gable window
            k.Box(new Vector3(0.9f, lift + 1.4f, 1.72f), new Vector3(0.12f, 2.2f, 0.08f), Window, 2f); // light round the door
            k.Box(new Vector3(0.5f, lift + 1.2f, 1.71f), new Vector3(0.8f, 2.2f, 0.06f), MudPalette.DarkWood);
            // Ladder down from the porch, and a stove pipe.
            foreach (float x in new[] { -0.35f, 0.35f }) k.Rod(new Vector3(x - 0.4f, 0f, 3.2f), new Vector3(x - 0.4f, lift, 2.7f), 0.05f, 4, MudPalette.Wood);
            for (int i = 0; i < 5; i++) k.Rod(new Vector3(-0.75f, 0.3f + i * 0.35f, 3.13f - i * 0.09f), new Vector3(-0.05f, 0.3f + i * 0.35f, 3.13f - i * 0.09f), 0.03f, 4, MudPalette.Wood);
            k.Tube(new[] { new Vector3(-1.4f, lift + 2.5f, -1.75f), new Vector3(-1.9f, lift + 3.5f, -2.1f), new Vector3(-1.9f, lift + 6f, -2.1f), new Vector3(-2.3f, lift + 6.4f, -2.1f) }, 0.12f, 6, Tin);
        }

        // A low log cabin (the second reference), scaled.
        private static void Cabin(MeshKit k, float s = 1f)
        {
            float w = 4.2f * s, d = 4.6f * s, h = 2.6f * s;
            for (int i = 0; i < 6; i++)
            {
                float y = 0.2f * s + i * h / 6f;
                k.Rod(new Vector3(-w * 0.5f, y, -d * 0.5f), new Vector3(-w * 0.5f, y, d * 0.5f), 0.22f * s, 6, MudPalette.Wood);
                k.Rod(new Vector3(w * 0.5f, y, -d * 0.5f), new Vector3(w * 0.5f, y, d * 0.5f), 0.22f * s, 6, MudPalette.Wood);
                k.Rod(new Vector3(-w * 0.5f, y + h / 12f, d * 0.5f), new Vector3(w * 0.5f, y + h / 12f, d * 0.5f), 0.22f * s, 6, MudPalette.DarkWood);
                k.Rod(new Vector3(-w * 0.5f, y + h / 12f, -d * 0.5f), new Vector3(w * 0.5f, y + h / 12f, -d * 0.5f), 0.22f * s, 6, MudPalette.DarkWood);
            }
            k.Box(new Vector3(0f, h * 0.5f, 0f), new Vector3(w - 0.2f, h, d - 0.2f), MudPalette.DarkWood);
            Vector3 ridgeA = new Vector3(0f, h + 1.6f * s, -d * 0.55f), ridgeB = new Vector3(0f, h + 1.6f * s, d * 0.55f);
            Vector3 l0 = new Vector3(-w * 0.62f, h, -d * 0.55f), l1 = new Vector3(-w * 0.62f, h, d * 0.55f), r0 = new Vector3(w * 0.62f, h, -d * 0.55f), r1 = new Vector3(w * 0.62f, h, d * 0.55f);
            Color roof = new Color(0.33f, 0.26f, 0.2f);
            k.Quad(l1, ridgeB, ridgeA, l0, roof);
            k.Quad(r0, ridgeA, ridgeB, r1, roof);
            k.Tri(r1, ridgeB, l1, MudPalette.Wood);
            k.Tri(l0, ridgeA, r0, MudPalette.Wood);
            k.Box(new Vector3(0f, 0.95f * s, d * 0.5f + 0.02f), new Vector3(0.9f * s, 1.9f * s, 0.06f), MudPalette.Bark);
            k.Box(new Vector3(-1.2f * s, 1.3f * s, d * 0.5f + 0.02f), new Vector3(0.7f * s, 0.6f * s, 0.05f), Window, 2.5f);
            k.Box(new Vector3(1.2f * s, 1.3f * s, d * 0.5f + 0.02f), new Vector3(0.7f * s, 0.6f * s, 0.05f), Window, 2.5f);
        }

        /// <summary>The boxes a kind's colliders should cover (centre, size), so the level builder can make it solid.</summary>
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
                case Kind.StiltShack: return new[] { (new Vector3(0f, 3.5f, 0f), new Vector3(3.8f, 7f, 3.6f)) };
                case Kind.Cabin: return new[] { (new Vector3(0f, 1.6f, 0f), new Vector3(4.4f, 3.2f, 4.8f)) };
                case Kind.DrivingSchool: return new[] { (new Vector3(0f, 2.2f, 0f), new Vector3(6.2f, 4.4f, 6.6f)) };
                case Kind.Kiosk: return new[] { (new Vector3(0f, 0.9f, 0f), new Vector3(1.1f, 1.8f, 0.9f)) };
                case Kind.Sawmill: return new[] { (new Vector3(0f, 0.8f, 0f), new Vector3(6.4f, 1.6f, 5.4f)) };
                case Kind.Outhouse: return new[] { (new Vector3(0f, 1.1f, 0f), new Vector3(1.3f, 2.2f, 1.3f)) };
                case Kind.Barricade: return new[] { (new Vector3(0f, 0.9f, 0f), new Vector3(3.9f, 1.8f, 0.9f)) };
                case Kind.Fence: return new[] { (new Vector3(0f, 0.6f, 0f), new Vector3(3.9f, 1.2f, 0.25f)) };
                case Kind.Crate: return new[] { (new Vector3(0f, 0.45f, 0f), new Vector3(0.9f, 0.9f, 0.9f)) };
                case Kind.HayBale: return new[] { (new Vector3(0f, 0.45f, 0f), new Vector3(1.6f, 0.9f, 1f)) };
                case Kind.MoonshineBarrel: return new[] { (new Vector3(0f, 0.5f, 0f), new Vector3(0.85f, 1f, 0.85f)) };
                case Kind.MoonshineStill: return new[] { (new Vector3(0.4f, 1.2f, 0.3f), new Vector3(3.6f, 2.4f, 3f)) };
                case Kind.Dam: return new[] { (new Vector3(0f, 1.4f, 0f), new Vector3(17f, 2.8f, 4f)) };
                case Kind.Gong: return new[] { (new Vector3(0f, 1.6f, 0f), new Vector3(3f, 3.2f, 0.6f)) };
                case Kind.CursedLantern: return new[] { (new Vector3(0.2f, 2.2f, 0f), new Vector3(1.1f, 4.4f, 0.6f)) };
                case Kind.LanternPost: return new[] { (new Vector3(0f, 1.5f, 0f), new Vector3(0.3f, 3f, 0.3f)) };
                case Kind.StepStone: return new[] { (new Vector3(0f, -0.2f, 0f), new Vector3(1.6f, 0.8f, 1.6f)) };
                case Kind.Bridge: return new[] { (new Vector3(0f, -0.05f, 0f), new Vector3(3.6f, 0.2f, 10f * size)), (new Vector3(-1.9f, 0.5f, 0f), new Vector3(0.2f, 1f, 10f * size)), (new Vector3(1.9f, 0.5f, 0f), new Vector3(0.2f, 1f, 10f * size)) };
                case Kind.Boardwalk: return new[] { (new Vector3(0f, -0.05f, 0f), new Vector3(2f, 0.2f, 6f * size)) };
                case Kind.TyreWall: return new[] { (new Vector3(0f, 0.3f, 0f), new Vector3(6f, 0.7f, 1f)) };
                case Kind.Grandstand: return new[] { (new Vector3(0f, 1.2f, -1.2f), new Vector3(10f, 2.4f, 3.4f)) };
                case Kind.BananaStand: return new[] { (new Vector3(0f, 0.6f, 0f), new Vector3(2.5f, 1.2f, 1.2f)) };
                case Kind.Cauldron: return new[] { (new Vector3(0f, 0.75f, 0f), new Vector3(2.4f, 1.5f, 2.4f)) };
                case Kind.WitchHut: return new[] { (new Vector3(0f, 2f, 0f), new Vector3(3.8f, 4f, 3.8f)) };
                case Kind.DojoGate: return new[] { (new Vector3(-2.2f, 2f, 0f), new Vector3(0.5f, 4f, 0.5f)), (new Vector3(2.2f, 2f, 0f), new Vector3(0.5f, 4f, 0.5f)) };
                case Kind.StoneLantern: return new[] { (new Vector3(0f, 0.8f, 0f), new Vector3(0.7f, 1.6f, 0.7f)) };
                case Kind.Roadhouse: return new[] { (new Vector3(0f, 3f, 0f), new Vector3(9.4f, 6f, 10.2f)) };
                case Kind.GasPump: return new[] { (new Vector3(0f, 1f, 0f), new Vector3(0.8f, 2f, 0.6f)) };
                case Kind.GatePost: return new[] { (new Vector3(0f, 1.6f, 0f), new Vector3(0.5f, 3.2f, 0.5f)) };
                case Kind.CountyLine: return new[] { (new Vector3(-5f, 2.5f, 0f), new Vector3(0.5f, 5f, 0.5f)), (new Vector3(5f, 2.5f, 0f), new Vector3(0.5f, 5f, 0.5f)) };
                case Kind.Windmill: return new[] { (new Vector3(0f, 4.5f, 0f), new Vector3(2f, 9f, 2f)) };
                case Kind.Rowboat: return new[] { (new Vector3(0f, 0.3f, 0f), new Vector3(1.4f, 0.6f, 3.6f)) };
                default: return Array.Empty<(Vector3, Vector3)>();
            }
        }
    }
}
