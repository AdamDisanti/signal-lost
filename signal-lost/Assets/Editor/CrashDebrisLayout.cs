using System;
using System.Collections.Generic;
using UnityEngine;

namespace SignalLost.Editor
{
    /// <summary>
    /// Lays out the tutorial wreck's debris so it reads as a crash rather than a scatter (Lawson, 11-07):
    /// - a scorched skid trail down the trench, with wreckage dug in and pointing the way the ship slid, bunched where
    ///   it struck and bounced (the trench fires mark those spots);
    /// - a torn-off wing section lying on the trench lip at the bounce;
    /// - cargo spilled out of the open side of the hull in a line, as if thrown;
    /// - light junk flung forward past the nose when it stopped;
    /// - ripped frame pieces lying against the hull, and ploughed-up dirt along the trench edges.
    /// Positions are in ship space: u along the ship's travel (+ toward the nose), v across it.
    /// </summary>
    internal sealed class CrashDebrisLayout
    {
        public delegate GameObject PlaceFn(string prefabPath, Transform parent, Vector3 position, Quaternion rotation, float scale, bool alignToGround);

        private readonly Transform parent;
        private readonly Vector2 ship, forward, side;
        private readonly Func<Vector2, Vector3> ground;
        private readonly PlaceFn place;
        private readonly System.Random random = new System.Random(20261107);
        private readonly List<Vector3> taken = new List<Vector3>(); // x, z, radius
        private readonly float travelYaw;

        private static readonly Dictionary<string, float> Footprint = new Dictionary<string, float>
        {
            { "HullChunk", 2.4f }, { "BrokenWing", 3.2f }, { "HullPanel", 1.2f }, { "HullPanelSmall", 0.6f },
            { "Crate", 0.8f }, { "Barrel", 0.5f }, { "Pipe", 1f }, { "Rib", 1.5f }, { "Seat", 0.55f },
            { "Gear", 0.6f }, { "CableBundle", 0.7f }, { "Strut", 1.1f },
        };

        public CrashDebrisLayout(Transform parent, Vector2 ship, Vector2 forward, Func<Vector2, Vector3> ground, PlaceFn place)
        {
            this.parent = parent;
            this.ship = ship;
            this.forward = forward.normalized;
            side = new Vector2(this.forward.y, -this.forward.x);
            this.ground = ground;
            this.place = place;
            travelYaw = Mathf.Atan2(this.forward.x, this.forward.y) * Mathf.Rad2Deg;
        }

        /// <summary>Keeps debris off a spot already used (the rocks the nose ploughed up).</summary>
        public void Reserve(Vector2 worldPoint, float radius) => taken.Add(new Vector3(worldPoint.x, worldPoint.y, radius));

        public void Build()
        {
            // The scanner's hiding place (TutorialStoryInstaller): a fallen panel on the port lip by the tail.
            Reserve(ShipPoint(-15f, -5f), 2.4f);
            SkidTrail();
            Touchdown(-43f);
            Bounce(-33f);
            EngineShedding(-25f);
            LastScrape(-17f);
            TrailFragments();
            Furrows();
            CargoSpill();
            HullFrame();
            ForwardThrow();
        }

        // ---------- The trench ----------

        // Overlapping scorch marks down the middle of the trench, burnt darkest (largest) where it hit hardest.
        private void SkidTrail()
        {
            for (float u = -47f; u <= -11f; u += 3.1f)
            {
                float heavy = Mathf.Max(Peak(u, -43f), Peak(u, -33f), Peak(u, -25f) * 0.7f);
                Scorch(u, R(-0.7f, 0.7f), R(1.1f, 1.4f) + heavy * 0.9f);
            }
        }

        private static float Peak(float u, float at) => Mathf.Clamp01(1f - Mathf.Abs(u - at) / 4f);

        // First touchdown: the belly plating tore off and dug in.
        private void Touchdown(float u)
        {
            Piece("HullChunk", u + 0.5f, 1.2f, R(-12f, 12f), tilt: 24f, sink: 0.55f);
            Piece("HullPanel", u - 2.5f, -2.2f, R(-20f, 20f), tilt: 30f, sink: 0.35f);
            Piece("HullPanel", u + 3.5f, 3.4f, R(-25f, 25f), tilt: 18f, sink: 0.3f);
            Piece("HullPanelSmall", u - 1f, 3.1f, R(-30f, 30f), tilt: 25f, sink: 0.15f);
            Piece("Strut", u + 2f, -3.3f, R(-15f, 15f), tilt: 8f, sink: 0.1f);
        }

        // The bounce: it lost a wing section here. The wing lies across the starboard lip, its torn root and cables at the
        // trench edge and its tip out in the grass.
        private void Bounce(float u)
        {
            Reserve(ShipPoint(u - 1f, 8f), 2.6f); // the outer half of the wing
            Piece("BrokenWing", u - 1f, 4.6f, R(-12f, 12f), tilt: -6f, sink: 0.2f);
            Piece("CableBundle", u + 1.5f, 3.6f, R(-30f, 30f), tilt: 0f, sink: 0f);
            Piece("HullChunk", u + 1.5f, -2.4f, R(-15f, 15f), tilt: 18f, sink: 0.6f);
            Piece("HullPanelSmall", u - 3f, -3.8f, R(-40f, 40f), tilt: 20f, sink: 0.12f);
            Piece("Pipe", u + 3.5f, 0.6f, R(-25f, 25f), tilt: 5f, sink: 0.12f);
        }

        // The engine bay split: gears and pipework spilled out over a few metres.
        private void EngineShedding(float u)
        {
            Piece("Gear", u - 1f, 1.8f, R(0f, 360f), tilt: R(5f, 15f), sink: 0.08f);
            Piece("Gear", u + 2.2f, -1.5f, R(0f, 360f), tilt: 70f, sink: 0.35f); // stuck on edge in the dirt
            Piece("Pipe", u + 0.5f, -3.2f, R(-20f, 20f), tilt: 6f, sink: 0.1f);
            Piece("Pipe", u - 2.6f, 2.8f, 180f + R(-20f, 20f), tilt: 4f, sink: 0.1f);
            Piece("HullPanelSmall", u + 3.6f, 1.2f, R(-35f, 35f), tilt: 22f, sink: 0.15f);
            Piece("CableBundle", u - 3.6f, -0.8f, R(-25f, 25f), tilt: 0f, sink: 0f);
        }

        // The last scrape before it stopped: hull ribs and a panel peeled off.
        private void LastScrape(float u)
        {
            Piece("HullPanel", u - 0.5f, -2.6f, R(-15f, 15f), tilt: 26f, sink: 0.3f);
            Piece("Rib", u + 1.5f, 2.8f, 90f + R(-15f, 15f), tilt: 12f, sink: 0.25f);
            Piece("Strut", u - 2.8f, 1.6f, R(-20f, 20f), tilt: 6f, sink: 0.12f);
            Piece("HullPanelSmall", u + 2.5f, -0.4f, R(-30f, 30f), tilt: 18f, sink: 0.12f);
        }

        // Small bits shed between the big strikes, thinning out on the lips.
        private void TrailFragments()
        {
            string[] small = { "HullPanelSmall", "HullPanelSmall", "Gear", "CableBundle", "Pipe" };
            for (int i = 0; i < 9; i++)
            {
                float u = R(-46f, -13f);
                float v = R(-1f, 1f) * R(1.5f, 4.8f);
                Piece(small[random.Next(small.Length)], u, v, R(-30f, 30f), tilt: R(4f, 20f), sink: 0.1f);
            }
        }

        // Dirt ploughed up along both edges of the trench.
        private void Furrows()
        {
            for (float u = -46f; u <= -14f; u += R(3.5f, 5f))
                foreach (float edge in new[] { -1f, 1f })
                {
                    Vector2 at = ShipPoint(u + R(-1f, 1f), edge * R(5.2f, 6.4f));
                    if (Blocked(at, 0.7f)) continue;
                    Reserve(at, 0.7f);
                    place(MeadowAssetBuilder.PrefabPath("RockC"), parent, ground(at) + Vector3.down * 0.35f,
                        Quaternion.Euler(0f, R(0f, 360f), 0f), R(0.45f, 0.75f), true).name = "Ploughed Dirt";
                }
        }

        // ---------- Around the hull ----------

        // Cargo burst out of the port side: crates and barrels tumbling away in a line, the far ones lightest.
        private void CargoSpill()
        {
            const float u = -3f;
            Piece("Crate", u, -10f, R(-20f, 20f), tilt: 0f, sink: 0f);
            Piece("Crate", u + 1.2f, -12.4f, 40f + R(-10f, 10f), tilt: 80f, sink: 0.15f); // on its side
            Piece("Barrel", u - 1.4f, -11.6f, R(0f, 360f), tilt: 90f, sink: 0.3f);        // rolled over
            Piece("Barrel", u + 2.4f, -14.2f, R(0f, 360f), tilt: 90f, sink: 0.3f);
            Piece("Seat", u - 0.4f, -15.6f, R(-60f, 60f), tilt: 75f, sink: 0.1f);          // pitched on its back
            Piece("CableBundle", u + 0.6f, -8.6f, 90f, tilt: 0f, sink: 0f);
            Piece("HullPanelSmall", u + 3.4f, -17.4f, R(0f, 360f), tilt: 10f, sink: 0.08f);
        }

        // Frame pieces ripped off the hull lie along its starboard side.
        private void HullFrame()
        {
            Piece("Rib", -9f, 9.6f, 90f + R(-10f, 10f), tilt: 8f, sink: 0.2f);
            Piece("Strut", 1f, 10.4f, R(-20f, 20f), tilt: 5f, sink: 0.1f);
            Piece("HullPanel", 7f, 9.8f, R(-25f, 25f), tilt: 15f, sink: 0.2f);
        }

        // When the nose dug in and stopped, light junk kept going: a fan ahead of the ship, past the ploughed berm.
        private void ForwardThrow()
        {
            string[] light = { "HullPanelSmall", "HullPanelSmall", "Gear", "CableBundle", "Pipe", "Strut" };
            for (int i = 0, tries = 0; i < 8 && tries < 60; tries++)
            {
                float angle = R(-38f, 38f) * Mathf.Deg2Rad, distance = R(22f, 31f);
                float u = Mathf.Cos(angle) * distance, v = Mathf.Sin(angle) * distance;
                // Thrown pieces point the way they flew.
                float heading = Mathf.Atan2(v, u) * Mathf.Rad2Deg;
                if (Piece(light[random.Next(light.Length)], u, v, -heading + R(-25f, 25f), tilt: R(4f, 20f), sink: 0.08f)) i++;
            }
        }

        // ---------- Helpers ----------

        private Vector2 ShipPoint(float u, float v) => ship + forward * u + side * v;

        private float R(float min, float max) => min + (float)random.NextDouble() * (max - min);

        private bool Blocked(Vector2 at, float radius)
        {
            foreach (Vector3 other in taken)
                if ((at - new Vector2(other.x, other.y)).magnitude < radius + other.z) return true;
            return false;
        }

        /// <summary>
        /// One piece: <paramref name="yaw"/> is relative to the ship's travel (0 = pointing the way it slid),
        /// <paramref name="tilt"/> pitches it nose-down into the ground and <paramref name="sink"/> buries it a little.
        /// Returns false (and places nothing) if it would land on another piece.
        /// </summary>
        private bool Piece(string name, float u, float v, float yaw, float tilt, float sink)
        {
            Vector2 at = ShipPoint(u, v);
            float radius = Footprint.TryGetValue(name, out float r) ? r : 0.8f;
            if (Blocked(at, radius * 0.85f)) return false;
            Reserve(at, radius);
            Quaternion rotation = Quaternion.Euler(0f, travelYaw + yaw, 0f) * Quaternion.Euler(tilt, 0f, R(-6f, 6f));
            place(CrashSiteWreckBuilder.DebrisPath(name), parent, ground(at) + Vector3.down * sink, rotation, R(0.9f, 1.15f), true);
            return true;
        }

        // A scorch decal laid along the trench (its long axis down the line of travel).
        private void Scorch(float u, float v, float scale)
        {
            Vector2 at = ShipPoint(u, v);
            Quaternion rotation = Quaternion.Euler(0f, travelYaw + 90f + R(-10f, 10f), 0f);
            place(CrashSiteWreckBuilder.DebrisPath("ScorchMark"), parent, ground(at) + Vector3.down * 0.02f, rotation, scale, true);
        }
    }
}
