using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SignalLost.Player
{
    /// <summary>
    /// The mechanic's look, built in code from soft, rounded shapes: a short, round-bellied build with a big head and
    /// pudgy hands, in the usual outfit (orange suit with a cream stripe, belt, teal patch, orange cap with a badge,
    /// tan work gloves) and the usual face (dark eyes, nose, stubble). Used by <see cref="FirstPersonHands"/> for the
    /// on-screen hands and by the pockets menu's 3D figure (<c>SignalLost.UI.GearFigure</c>), so the two always match.
    /// <para>
    /// Every part is a <see cref="Shape"/>: a superquadric that runs from a ball to a rounded box, generated once per
    /// shape and shared. Sizes are full widths in metres, like a scaled cube; children of a part are placed in that
    /// part's own units (-0.5 to 0.5), as with a scaled cube.
    /// </para>
    /// </summary>
    public static class MechanicModel
    {
        // ---------- Outfit and face ----------

        public static readonly Color Suit = new Color(0.93f, 0.48f, 0.15f);
        public static readonly Color Cream = new Color(0.9f, 0.86f, 0.78f);
        public static readonly Color Glove = new Color(0.62f, 0.44f, 0.27f);
        public static readonly Color Skin = new Color(0.93f, 0.74f, 0.58f);
        public static readonly Color Boot = new Color(0.25f, 0.18f, 0.13f);
        public static readonly Color Belt = new Color(0.18f, 0.16f, 0.15f);
        public static readonly Color Teal = new Color(0.42f, 0.9f, 0.86f);
        public static readonly Color Eyes = new Color(0.08f, 0.07f, 0.07f);

        // ---------- Shapes ----------

        /// <summary>
        /// A rounded solid. <see cref="Ends"/> rounds the profile along its main axis and <see cref="Around"/> the
        /// cross-section: 1 is round, smaller is boxier (0.2 is nearly a brick). The main axis is Y, or Z for
        /// <see cref="AlongZ"/> (fingers and sleeves, which point forward in a hand).
        /// </summary>
        public readonly struct Shape
        {
            public readonly float Ends, Around;
            public readonly bool AlongZ;

            public Shape(float ends, float around, bool alongZ = false)
            {
                Ends = Mathf.Clamp(ends, 0.1f, 1.5f);
                Around = Mathf.Clamp(around, 0.1f, 1.5f);
                AlongZ = alongZ;
            }

            public override string ToString() => $"Soft {Ends:0.00}/{Around:0.00}{(AlongZ ? " Z" : "")}";

            /// <summary>
            /// How far forward (+Z, or +Y for <see cref="AlongZ"/>) the surface is at a point across the face, all in
            /// the shape's half-widths (-1 to 1). Used to sit features such as eyes and badges on a curved surface.
            /// </summary>
            public float Front(float across, float up)
            {
                float rest = 1f - Mathf.Pow(Mathf.Abs(up), 2f / Ends);
                if (rest <= 0f) return 0f;
                float left = Mathf.Pow(rest, Ends / Around) - Mathf.Pow(Mathf.Abs(across), 2f / Around);
                return left > 0f ? Mathf.Pow(left, Around * 0.5f) : 0f;
            }

            /// <summary>How wide the cross-section is (0-1 of the full width) at a height along the main axis (-1 to 1).</summary>
            public float Girth(float along) => Front(0f, along);
        }

        public static readonly Shape Ball = new Shape(1f, 1f);
        public static readonly Shape Pillow = new Shape(0.7f, 0.6f);
        public static readonly Shape Brick = new Shape(0.3f, 0.3f);
        public static readonly Shape Barrel = new Shape(0.6f, 0.95f);
        public static readonly Shape Band = new Shape(0.2f, 0.95f);
        public static readonly Shape Sausage = new Shape(0.55f, 0.95f, true);
        public static readonly Shape Cuff = new Shape(0.3f, 0.9f, true);

        private static readonly Dictionary<string, Mesh> Meshes = new Dictionary<string, Mesh>();

        /// <summary>The shared mesh for a shape (unit size, centred).</summary>
        public static Mesh MeshFor(Shape shape)
        {
            string key = shape.ToString();
            if (Meshes.TryGetValue(key, out Mesh mesh) && mesh != null) return mesh;
            mesh = BuildMesh(shape);
            Meshes[key] = mesh;
            return mesh;
        }

        /// <summary>One rounded part. <paramref name="size"/> is its full width, height and depth.</summary>
        public static Transform Part(Transform parent, string name, Material material, Shape shape, Vector3 position,
            Vector3 size, Vector3 angles = default)
        {
            var part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localRotation = Quaternion.Euler(angles);
            part.transform.localScale = size;
            part.AddComponent<MeshFilter>().sharedMesh = MeshFor(shape);
            var renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return part.transform;
        }

        // A superquadric with analytic normals, so it shades smoothly with no seam.
        private static Mesh BuildMesh(Shape shape)
        {
            const int around = 40, along = 28;
            var vertices = new Vector3[(along + 1) * (around + 1)];
            var normals = new Vector3[vertices.Length];
            for (int i = 0; i <= along; i++)
            {
                float eta = -Mathf.PI * 0.5f + Mathf.PI * i / along;
                float ce = Mathf.Max(0f, Mathf.Cos(eta)), se = Mathf.Sin(eta);
                float ring = Mathf.Pow(ce, shape.Ends), ringNormal = Mathf.Pow(ce, 2f - shape.Ends);
                for (int j = 0; j <= around; j++)
                {
                    float omega = -Mathf.PI + 2f * Mathf.PI * j / around;
                    float co = Mathf.Cos(omega), so = Mathf.Sin(omega);
                    var point = new Vector3(ring * Signed(co, shape.Around), Signed(se, shape.Ends), ring * Signed(so, shape.Around)) * 0.5f;
                    var normal = new Vector3(ringNormal * Signed(co, 2f - shape.Around), Signed(se, 2f - shape.Ends),
                        ringNormal * Signed(so, 2f - shape.Around));
                    if (shape.AlongZ)
                    {
                        point = new Vector3(point.x, point.z, point.y);
                        normal = new Vector3(normal.x, normal.z, normal.y);
                    }
                    int index = i * (around + 1) + j;
                    vertices[index] = point;
                    normals[index] = normal.sqrMagnitude > 1e-12f ? normal.normalized : point.normalized;
                }
            }

            var triangles = new int[along * around * 6];
            int t = 0;
            for (int i = 0; i < along; i++)
            {
                for (int j = 0; j < around; j++)
                {
                    int a = i * (around + 1) + j, up = a + around + 1, next = a + 1, upNext = up + 1;
                    // Front faces wind so that Cross(b - a, c - a) points outward; swapping Y and Z mirrors the mesh.
                    if (shape.AlongZ)
                    {
                        triangles[t++] = a; triangles[t++] = next; triangles[t++] = up;
                        triangles[t++] = up; triangles[t++] = next; triangles[t++] = upNext;
                    }
                    else
                    {
                        triangles[t++] = a; triangles[t++] = up; triangles[t++] = next;
                        triangles[t++] = up; triangles[t++] = upNext; triangles[t++] = next;
                    }
                }
            }

            var mesh = new Mesh { name = shape.ToString(), hideFlags = HideFlags.DontUnloadUnusedAsset };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static float Signed(float value, float power) => Mathf.Sign(value) * Mathf.Pow(Mathf.Abs(value), power);

        // ---------- Pudgy hand ----------

        /// <summary>
        /// A pudgy gloved hand: one smooth, seamless skinned mesh (<see cref="SoftShape"/>) with a puffy palm, short
        /// thick two-jointed fingers with round tips and a chunky thumb, which bend smoothly at their joints.
        /// In its own space the fingers point +Z, the back of the hand faces +Y and the thumb is on the side toward
        /// -X for a right hand (+X for a left). <see cref="Curl"/> closes it from relaxed (0) to a tight fist (1).
        /// </summary>
        public sealed class Hand
        {
            public Transform Root { get; }
            public float Grip { get; private set; } = -1f;

            private readonly float side;
            private readonly Transform[] knuckles = new Transform[4], middles = new Transform[4];
            private Transform thumb, thumbTip;

            // Index to pinky. Relaxed fingers fall into a gentle cascade, the pinky curling most.
            private static readonly float[] Width = { 0.0235f, 0.0245f, 0.0235f, 0.021f };
            private static readonly float[] Length = { 0.05f, 0.055f, 0.05f, 0.04f };
            private static readonly float[] KnuckleZ = { 0.03f, 0.032f, 0.03f, 0.024f };
            private static readonly float[] KnuckleY = { 0.002f, 0.003f, 0.001f, -0.003f };
            private static readonly float[] Relaxed = { 12f, 15f, 19f, 24f };
            private static readonly float[] Splay = { -5f, -1.5f, 2f, 6f };
            // The glove is meshed once per side with the fingers spread, so they come out separate; every hand of
            // that side shares the mesh (bones are always laid out the same way).
            private const float BindSpread = 3.4f;
            internal static readonly Dictionary<int, Mesh> GloveMeshes = new Dictionary<int, Mesh>();

            internal Hand(Transform root, float side)
            {
                Root = root;
                this.side = side;
            }

            internal void Build(Material glove)
            {
                for (int i = 0; i < 4; i++)
                {
                    float x = -(1.5f - i) * 0.0205f * side;
                    knuckles[i] = Joint(Root, "Finger " + (i + 1), new Vector3(x, KnuckleY[i], KnuckleZ[i]));
                    middles[i] = Joint(knuckles[i], "Joint", new Vector3(0f, 0f, Length[i] * 0.52f));
                }
                thumb = Joint(Root, "Thumb", new Vector3(-0.04f * side, -0.008f, -0.012f));
                thumbTip = Joint(thumb, "Joint", new Vector3(0f, 0f, 0.032f));

                // Bones: the hand, each finger's two segments, the thumb's two.
                var bones = new Transform[11];
                bones[0] = Root;
                for (int i = 0; i < 4; i++) { bones[1 + i * 2] = knuckles[i]; bones[2 + i * 2] = middles[i]; }
                bones[9] = thumb;
                bones[10] = thumbTip;

                Pose(0f, BindSpread);
                int key = side > 0f ? 1 : -1;
                if (!GloveMeshes.TryGetValue(key, out Mesh mesh) || mesh == null)
                {
                    string meshName = key > 0 ? "Glove R" : "Glove L";
                    mesh = Baked(meshName);
                    if (mesh == null)
                    {
                        var poses = new UnityEngine.Pose[bones.Length];
                        for (int b = 0; b < bones.Length; b++) poses[b] = ToRoot(bones[b]);
                        mesh = GloveShape().Build(meshName, 0.0024f, poses, 0.0022f);
                    }
                    GloveMeshes[key] = mesh;
                }

                var skin = new GameObject("Glove");
                skin.transform.SetParent(Root, false);
                var renderer = skin.AddComponent<SkinnedMeshRenderer>();
                renderer.sharedMesh = mesh;
                renderer.bones = bones;
                renderer.rootBone = Root;
                renderer.localBounds = new Bounds(new Vector3(0f, -0.01f, 0.01f), new Vector3(0.2f, 0.16f, 0.26f));
                renderer.quality = SkinQuality.Bone4;
                renderer.sharedMaterial = glove;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                Curl(0f);
            }

            // The glove as rounded pieces on its bones (each in that bone's own space).
            private SoftShape GloveShape()
            {
                var shape = new SoftShape { CoreBlend = 0.014f, AttachBlend = 0.01f, JointBlend = 0.004f };
                // Core: a puffy palm, a soft rise over the back of the hand, the knuckle ridge, the thumb's heel and
                // the wrist running back into the cuff.
                shape.RoundBox(0, 0, new Vector3(0f, -0.003f, -0.014f), new Vector3(0.03f, 0.009f, 0.027f), 0.017f);
                shape.Ellipsoid(0, 0, new Vector3(0.002f * side, 0.007f, -0.018f), new Vector3(0.043f, 0.021f, 0.041f));
                shape.Ellipsoid(0, 0, new Vector3(0.003f * side, 0.004f, 0.022f), new Vector3(0.046f, 0.018f, 0.018f));
                shape.Ellipsoid(0, 0, new Vector3(-0.03f * side, -0.014f, -0.026f), new Vector3(0.023f, 0.02f, 0.031f));
                shape.Cone(0, 0, new Vector3(0f, -0.003f, -0.045f), 0.031f, new Vector3(0f, -0.003f, -0.066f), 0.032f);
                // Fingers: thick, slightly tapering, with round tips.
                for (int i = 0; i < 4; i++)
                {
                    int finger = shape.NewGroup();
                    float r = Width[i] * 0.6f, l1 = Length[i] * 0.52f, l2 = Length[i] * 0.48f;
                    shape.Cone(finger, 1 + i * 2, new Vector3(0f, 0f, -0.006f), r, new Vector3(0f, 0f, l1), r * 0.96f);
                    shape.Cone(finger, 2 + i * 2, Vector3.zero, r * 0.96f, new Vector3(0f, -0.0015f, l2 - r * 0.25f), r * 0.9f);
                }
                int thumbGroup = shape.NewGroup();
                shape.Cone(thumbGroup, 9, new Vector3(0f, 0f, -0.01f), 0.0185f, new Vector3(0f, 0f, 0.032f), 0.017f);
                shape.Cone(thumbGroup, 10, Vector3.zero, 0.017f, new Vector3(0f, -0.001f, 0.02f), 0.0155f);
                return shape;
            }

            // A bone's pose relative to the hand root (bones carry no scale).
            private UnityEngine.Pose ToRoot(Transform bone)
            {
                Vector3 position = Vector3.zero;
                Quaternion rotation = Quaternion.identity;
                for (Transform t = bone; t != Root; t = t.parent)
                {
                    position = t.localRotation * position + t.localPosition;
                    rotation = t.localRotation * rotation;
                }
                return new UnityEngine.Pose(position, rotation);
            }

            /// <summary>Where the underside of the palm is (hand space y): handles rest against it.</summary>
            public const float PalmUnderside = -0.027f;
            /// <summary>How far forward (hand space z) a wrapped handle sits: under the front of the palm.</summary>
            public const float WrapForward = 0.012f;

            /// <summary>
            /// Where the axis of a handle of this radius (hand units) passes when <see cref="Wrap"/> holds it: across the
            /// palm along the hand's X axis (index side to little finger), resting against the palm.
            /// </summary>
            public static Vector3 WrapCentre(float radius) => new Vector3(0f, PalmUnderside - radius, WrapForward);

            /// <summary>
            /// Closes the fingers round a handle of the given radius (hand units) lying across the palm, so a tool's grip
            /// can sit in the fist (at <see cref="WrapCentre"/>) instead of the fingers passing through it. Each finger
            /// bends at the knuckle and the middle joint so both segments end on the handle's surface.
            /// <paramref name="thumb"/> closes the thumb like <see cref="Curl"/> does (0 open, 1 folded over).
            /// </summary>
            public void Wrap(float radius, float thumb = 0.5f)
            {
                Grip = -1f; // so the next Curl applies again
                Vector2 centre = new Vector2(PalmUnderside - radius, WrapForward); // (y, z)
                for (int i = 0; i < 4; i++)
                {
                    float reach = radius + Width[i] * 0.55f;
                    var knuckle = new Vector2(KnuckleY[i], KnuckleZ[i]);
                    float l1 = Length[i] * 0.52f, l2 = Length[i] * 0.48f - Width[i] * 0.15f;
                    float bend = Land(knuckle, 0f, l1, centre, reach);
                    Vector2 joint = knuckle + Direction(bend) * l1;
                    float tip = Land(joint, bend, l2, centre, reach);
                    knuckles[i].localRotation = Quaternion.Euler(bend, Splay[i] * 0.3f * side, 0f);
                    middles[i].localRotation = Quaternion.Euler(Mathf.Clamp(tip - bend, 0f, 120f), 0f, 0f);
                }
                PoseThumb(thumb);
            }

            // The pitch (degrees, from start) at which a segment of this length from 'from' first reaches the circle.
            private static float Land(Vector2 from, float start, float length, Vector2 centre, float reach)
            {
                for (float pitch = start; pitch <= start + 170f; pitch += 0.5f)
                    if ((from + Direction(pitch) * length - centre).magnitude <= reach) return pitch;
                return start + 90f;
            }

            // A finger's direction in the hand's (y, z) plane after pitching down by the given angle from straight ahead.
            private static Vector2 Direction(float pitch) =>
                new Vector2(-Mathf.Sin(pitch * Mathf.Deg2Rad), Mathf.Cos(pitch * Mathf.Deg2Rad));

            /// <summary>0 = relaxed and half open, 1 = a tight fist (round things are held at about 0.7).</summary>
            public void Curl(float grip)
            {
                grip = Mathf.Clamp01(grip);
                if (Mathf.Abs(grip - Grip) < 0.0005f) return;
                Grip = grip;
                Pose(grip, 1f);
            }

            private void Pose(float grip, float spread)
            {
                for (int i = 0; i < 4; i++)
                {
                    knuckles[i].localRotation = Quaternion.Euler(Mathf.Lerp(Relaxed[i], 84f, grip), Splay[i] * spread * (1f - grip * 0.7f) * side, 0f);
                    middles[i].localRotation = Quaternion.Euler(Mathf.Lerp(16f + i * 3f, 100f, grip), 0f, 0f);
                }
                PoseThumb(grip);
            }

            // The thumb points forward and out; closing, it swings under the palm and folds over the fingers.
            private void PoseThumb(float grip)
            {
                thumb.localRotation = Quaternion.Euler(Mathf.Lerp(14f, 30f, grip), Mathf.Lerp(-38f, 40f, grip) * side, Mathf.Lerp(-10f, -25f, grip) * side);
                thumbTip.localRotation = Quaternion.Euler(Mathf.Lerp(10f, 28f, grip), 0f, 0f);
            }
        }

        // ---------- Holding tools ----------

        /// <summary>
        /// How the right hand holds a tool, measured against the real models: the hand's pose in the tool holder's space
        /// (the first-person holder: the laser's grip pivot or the scanner's visual root), its scale there, and how the
        /// fingers wrap the grip (<see cref="Hand.Wrap"/>). Also how the tool itself sits in that holder, so
        /// <see cref="Seat"/> can put a copy of the tool in any hand (the pockets menu's mechanic).
        /// </summary>
        public readonly struct ToolHold
        {
            public readonly Vector3 HandPosition, HandAngles, ToolAngles, ToolGripPoint;
            public readonly float HandScale, ToolScale, Radius, Thumb;

            public ToolHold(Vector3 handPosition, Vector3 handAngles, float handScale, float radius, float thumb,
                Vector3 toolAngles, float toolScale, Vector3 toolGripPoint)
            {
                HandPosition = handPosition;
                HandAngles = handAngles;
                HandScale = handScale;
                Radius = radius;
                Thumb = thumb;
                ToolAngles = toolAngles;
                ToolScale = toolScale;
                ToolGripPoint = toolGripPoint;
            }

            public Quaternion HandRotation => Quaternion.Euler(HandAngles);

            /// <summary>The hand's rotation in the holder's space, mirrored for a left hand.</summary>
            public Quaternion HandRotationFor(bool mirror) =>
                Quaternion.Euler(HandAngles.x, mirror ? -HandAngles.y : HandAngles.y, mirror ? -HandAngles.z : HandAngles.z);

            /// <summary>
            /// Parents <paramref name="tool"/> (a copy of the model, its root as in the holder) to the hand and places it
            /// as in first person, then wraps the fingers round its grip. <paramref name="mirror"/> is for a left hand.
            /// <paramref name="gripPoint"/> overrides <see cref="ToolGripPoint"/> (the tool point, in its own space,
            /// that sits at the holder's origin) when the copy has its own GripAnchor. <paramref name="shrink"/> scales the
            /// tool about the grip, for a smaller copy in a big hand.
            /// </summary>
            public void Seat(Transform tool, Hand hand, bool mirror, Vector3? gripPoint = null, float shrink = 1f)
            {
                Vector3 handPosition = mirror ? new Vector3(-HandPosition.x, HandPosition.y, HandPosition.z) : HandPosition;
                Quaternion handRotation = HandRotationFor(mirror);
                Quaternion toolRotation = Quaternion.Euler(ToolAngles.x, mirror ? -ToolAngles.y : ToolAngles.y, mirror ? -ToolAngles.z : ToolAngles.z);
                Vector3 toolPosition = -(toolRotation * (ToolScale * (gripPoint ?? ToolGripPoint)));
                Quaternion back = Quaternion.Inverse(handRotation);
                tool.SetParent(hand.Root, false);
                tool.localRotation = back * toolRotation;
                tool.localScale = Vector3.one * (ToolScale / HandScale);
                tool.localPosition = back * (toolPosition - handPosition) / HandScale;
                if (shrink != 1f)
                {
                    Vector3 grip = Hand.WrapCentre(Radius);
                    tool.localPosition = grip + (tool.localPosition - grip) * shrink;
                    tool.localScale *= shrink;
                }
                hand.Wrap(Radius, Thumb);
            }
        }

        /// <summary>
        /// The mining laser: the fist round its pistol grip (index finger under the trigger guard), relative to
        /// MiningLaser's grip pivot (HeldTool). The held model sits there turned (0, -3, 0) at scale 0.55.
        /// </summary>
        public static readonly ToolHold LaserHold = new ToolHold(new Vector3(0.0588f, 0.0098f, -0.0171f), new Vector3(13.97f, 0.57f, -89.14f),
            1.2f, 0.022f, 0.45f, new Vector3(0f, -3f, 0f), 0.55f, new Vector3(0f, 0.11f, -0.11f));

        /// <summary>
        /// The scanner: the fist round its right-hand side rail, wrist tipped so the arm comes up from below, relative to
        /// the scanner's visual root.
        /// </summary>
        public static readonly ToolHold ScannerHold = new ToolHold(new Vector3(0.1531f, -0.0283f, 0.0381f), new Vector3(-35f, -25f, -90f),
            1.2f, 0.02f, 0.45f, Vector3.zero, 1f, Vector3.zero);

        /// <summary>
        /// Builds a pudgy hand under <paramref name="parent"/>. <paramref name="side"/> is 1 for the right hand and -1
        /// for the left. With <paramref name="sleeve"/> it also gets a cuff and a suit sleeve running back off the
        /// screen (the first-person hands); without, it is just the glove (the figure's arms bring their own sleeves).
        /// </summary>
        public static Hand BuildHand(Transform parent, string name, float side, Material glove, Material cuff = null,
            Material sleeve = null, Material stripe = null)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            var hand = new Hand(root, side);
            hand.Build(glove);
            if (cuff != null) Part(root, "Cuff", cuff, Cuff, new Vector3(0f, -0.003f, -0.068f), new Vector3(0.098f, 0.074f, 0.05f));
            if (sleeve != null)
            {
                Transform arm = Part(root, "Sleeve", sleeve, Sausage, new Vector3(0.012f * side, -0.045f, -0.3f), new Vector3(0.118f, 0.095f, 0.46f),
                    new Vector3(-12f, 4f * side, 0f));
                if (stripe != null) Part(arm, "Sleeve Stripe", stripe, new Shape(0.2f, 0.95f, true), new Vector3(0f, 0f, 0.3f), new Vector3(1.04f, 1.04f, 0.13f));
            }
            return hand;
        }

        private static Transform Joint(Transform parent, string name, Vector3 position)
        {
            var joint = new GameObject(name).transform;
            joint.SetParent(parent, false);
            joint.localPosition = position;
            return joint;
        }

        // ---------- The whole mechanic ----------

        /// <summary>The figure's mounts, for holding tools and wearing upgrade parts.</summary>
        public sealed class Figure
        {
            public Transform Body, Head, Torso, KneeL, KneeR, ArmL, ArmR, Hip;
            public Hand HandL, HandR;
        }

        /// <summary>From the shoulder pivot down to the middle of the palm.</summary>
        public const float ArmReach = 0.47f;
        /// <summary>The figure's height to the top of the cap, in metres.</summary>
        public const float Height = 1.66f;

        // Body proportions (metres, standing at the origin, facing +Z).
        private static readonly Shape BellyShape = new Shape(1f, 1f);
        private static readonly Shape ChestShape = new Shape(1f, 1f);
        private static readonly Shape HeadShape = new Shape(0.92f, 0.9f);
        private static readonly Shape CapShape = new Shape(0.85f, 0.92f);
        private static readonly Shape Soft = new Shape(0.45f, 0.45f); // small flat details: eyes, patch, badge
        private static readonly Vector3 BellyAt = new Vector3(0f, 0.62f, 0.03f), BellySize = new Vector3(0.68f, 0.64f, 0.54f);
        private static readonly Vector3 ChestAt = new Vector3(0f, 0.9f, 0.01f), ChestSize = new Vector3(0.56f, 0.56f, 0.44f);
        private static readonly Vector3 HeadAt = new Vector3(0f, 1.37f, 0.02f), HeadSize = new Vector3(0.53f, 0.49f, 0.49f);

        /// <summary>
        /// Builds the whole mechanic under <paramref name="body"/>: short and stocky, with a round belly, stubby legs,
        /// short arms and a big head with no neck to speak of. <paramref name="paint"/> turns a colour into a material.
        /// Arms hang from the shoulders at the given angles; the hands close to <paramref name="gripR"/>/<paramref name="gripL"/>.
        /// </summary>
        /// <param name="look">What the mechanic wears (<see cref="SignalLost.World.Outfits.Mechanic"/>, 11-08: each island has
        /// its own outfit). Omitted: the usual orange suit and cap.</param>
        public static Figure BuildFigure(Transform body, System.Func<Color, Material> paint, Vector3 armAnglesR, Vector3 armAnglesL,
            float gripR = 0.75f, float gripL = 0.75f, SignalLost.World.MechanicLook? look = null)
        {
            SignalLost.World.MechanicLook wear = look ?? SignalLost.World.Outfits.Mechanic(SignalLost.World.IslandLook.Meadow);
            var figure = new Figure { Body = body };
            Material suit = paint(wear.Suit), cream = paint(wear.Trim);

            for (int s = -1; s <= 1; s += 2)
                Part(body, "Boot " + (s < 0 ? "L" : "R"), paint(wear.Boot), new Shape(0.7f, 0.8f), new Vector3(0.135f * s, 0.065f, 0.04f), new Vector3(0.23f, 0.14f, 0.31f));
            figure.KneeL = Joint(body, "Knee L", new Vector3(-0.135f, 0.23f, 0f));
            figure.KneeR = Joint(body, "Knee R", new Vector3(0.135f, 0.23f, 0f));

            // Legs, belly and chest as one seamless suit: an egg, widest at the belly, with sloping shoulders.
            Static(body, "Suit", suit, SuitMesh());
            Ring(body, "Belt", paint(wear.Belt), 0.44f, 0.065f);
            Ring(body, "Stripe", cream, 0.51f, 0.045f);
            figure.Torso = Joint(body, "Torso", new Vector3(0f, 0.62f, 0.11f)); // padding sits 0.17 in front of this
            figure.Hip = Joint(body, "Hip", new Vector3(0.33f, 0.44f, 0.04f));

            // The Maintenance Corps patch, turned to lie on the curve of the chest.
            Transform patch = OnSurface(body, "Patch", paint(wear.Patch), Soft, ChestShape, ChestAt, ChestSize, -0.12f, 1.01f, new Vector3(0.1f, 0.1f, 0.02f));
            Part(patch, "Patch Gear", cream, Soft, new Vector3(0f, 0f, 0.6f), new Vector3(0.5f, 0.5f, 0.4f));

            figure.Head = BuildHead(body, paint, wear);
            if (wear.Neckerchief)
            {
                // Tied round where the head meets the chest, its point hanging down the front.
                Part(body, "Neckerchief", paint(wear.Scarf), Band, new Vector3(0f, 1.15f, 0.02f), new Vector3(0.42f, 0.07f, 0.38f));
                Part(body, "Neckerchief Point", paint(wear.Scarf), Soft, new Vector3(0f, 1.08f, 0.2f), new Vector3(0.15f, 0.15f, 0.03f), new Vector3(-15f, 0f, 45f));
            }

            figure.ArmR = Arm(body, "Arm R", 1f, armAnglesR, suit, cream, paint(wear.Glove), gripR, out figure.HandR);
            figure.ArmL = Arm(body, "Arm L", -1f, armAnglesL, suit, cream, paint(wear.Glove), gripL, out figure.HandL);
            return figure;
        }

        // The face is the base game's: dark eyes, a nose and stubble, under the orange cap with its cream badge (or, on
        // islands where the outfit has one, a bush hat with goggles on the band).
        private static Transform BuildHead(Transform body, System.Func<Color, Material> paint, SignalLost.World.MechanicLook wear)
        {
            Transform head = Part(body, "Head", paint(wear.Skin), HeadShape, HeadAt, HeadSize);
            Material eyes = paint(Eyes);
            Part(head, "Eye L", eyes, Soft, new Vector3(-0.18f, -0.03f, FaceZ(-0.18f, -0.03f) - 0.006f), new Vector3(0.11f, 0.1f, 0.04f));
            Part(head, "Eye R", eyes, Soft, new Vector3(0.18f, -0.03f, FaceZ(0.18f, -0.03f) - 0.006f), new Vector3(0.11f, 0.1f, 0.04f));
            Part(head, "Nose", paint(wear.Skin * 0.92f), Ball, new Vector3(0f, -0.15f, FaceZ(0f, -0.15f) + 0.02f), new Vector3(0.11f, 0.13f, 0.1f));
            // A flat mouth and a light scatter of stubble on the chin and jaw, as on the HUD portrait.
            OnFace(head, "Mouth", paint(Eyes), new Shape(0.4f, 0.5f), 0f, -0.255f, new Vector3(0.17f, 0.03f, 0.03f), 0.004f);
            Material stubble = paint(StubbleDots);
            for (int i = 0; i < StubbleSpots.Length; i++)
                OnFace(head, "Stubble", stubble, Ball, StubbleSpots[i].x, StubbleSpots[i].y, new Vector3(0.019f, 0.019f, 0.01f), 0.003f);

            if (wear.BushHat)
            {
                // A wide-brimmed bush hat (head units), its band in the trim colour, goggles pushed up on the front.
                Part(head, "Brim", paint(wear.Hat), new Shape(0.25f, 0.95f), new Vector3(0f, 0.36f, 0.01f), new Vector3(1.55f, 0.07f, 1.5f), new Vector3(-6f, 0f, 0f));
                Part(head, "Crown", paint(wear.Hat), new Shape(0.7f, 0.9f), new Vector3(0f, 0.55f, -0.01f), new Vector3(0.9f, 0.42f, 0.86f));
                Part(head, "Crown Dent", paint(wear.Hat * 0.88f), new Shape(0.4f, 0.9f), new Vector3(0f, 0.75f, -0.01f), new Vector3(0.6f, 0.06f, 0.5f));
                Part(head, "Hat Band", paint(wear.HatBand), Band, new Vector3(0f, 0.42f, -0.01f), new Vector3(0.93f, 0.1f, 0.89f));
                for (int s = -1; s <= 1; s += 2)
                {
                    Transform rim = Part(head, "Goggle " + (s < 0 ? "L" : "R"), paint(new Color(0.82f, 0.63f, 0.28f)), Ball,
                        new Vector3(0.17f * s, 0.45f, 0.44f), new Vector3(0.24f, 0.18f, 0.08f), new Vector3(-20f, 0f, 0f));
                    Part(rim, "Lens", paint(wear.Lens), Ball, new Vector3(0f, 0f, 0.3f), new Vector3(0.75f, 0.7f, 0.6f));
                }
                return head;
            }

            Material cap = paint(wear.Hat);
            Vector3 capAt = new Vector3(0f, 0.25f, -0.01f), capSize = new Vector3(1.06f, 0.64f, 1.06f);
            Part(head, "Cap", cap, CapShape, capAt, capSize);
            Part(head, "Peak", paint(wear.Hat * 0.85f), new Shape(0.35f, 0.7f), new Vector3(0f, 0.08f, FaceZ(0f, 0.08f) + 0.11f),
                new Vector3(0.88f, 0.07f, 0.36f), new Vector3(10f, 0f, 0f));
            float badgeY = 0.27f;
            float badgeZ = capAt.z + capSize.z * 0.5f * CapShape.Front(0f, (badgeY - capAt.y) / (capSize.y * 0.5f));
            Part(head, "Badge", paint(wear.Trim), Soft, new Vector3(0f, badgeY, badgeZ - 0.005f), new Vector3(0.2f, 0.15f, 0.04f), new Vector3(-12f, 0f, 0f));
            return head;
        }

        // Stubble dots (head units, x across, y up): three loose rows under the mouth and a few up each jaw.
        private static readonly Vector2[] StubbleSpots =
        {
            new Vector2(-0.12f, -0.31f), new Vector2(-0.06f, -0.325f), new Vector2(0f, -0.315f), new Vector2(0.06f, -0.33f), new Vector2(0.12f, -0.31f),
            new Vector2(-0.09f, -0.37f), new Vector2(-0.03f, -0.38f), new Vector2(0.035f, -0.375f), new Vector2(0.095f, -0.365f),
            new Vector2(-0.045f, -0.42f), new Vector2(0.02f, -0.425f),
            new Vector2(-0.19f, -0.27f), new Vector2(-0.22f, -0.21f), new Vector2(0.19f, -0.275f), new Vector2(0.225f, -0.215f),
            new Vector2(-0.16f, -0.35f), new Vector2(0.165f, -0.345f),
        };
        private static readonly Color StubbleDots = new Color(0.47f, 0.34f, 0.27f);

        // A small detail lying on the face at (x, y) in head units, turned to follow the curve and sunk in by inset.
        private static void OnFace(Transform head, string name, Material material, Shape shape, float x, float y, Vector3 size, float inset)
        {
            const float d = 0.02f;
            float z = FaceZ(x, y);
            float yaw = Mathf.Atan((FaceZ(x + d, y) - FaceZ(x - d, y)) / (2f * d)) * Mathf.Rad2Deg;
            float pitch = Mathf.Atan((FaceZ(x, y + d) - FaceZ(x, y - d)) / (2f * d)) * Mathf.Rad2Deg;
            Part(head, name, material, shape, new Vector3(x, y, z - inset), size, new Vector3(pitch, -yaw, 0f));
        }

        // How far forward the face is at a point on the head (head units).
        private static float FaceZ(float x, float y) => 0.5f * HeadShape.Front(x / 0.5f, y / 0.5f);

        // A band around the belly at a height, just proud of its surface.
        private static void Ring(Transform body, string name, Material material, float y, float height)
        {
            float girth = BellyShape.Girth((y - BellyAt.y) / (BellySize.y * 0.5f));
            Part(body, name, material, Band, new Vector3(BellyAt.x, y, BellyAt.z),
                new Vector3(BellySize.x * girth + 0.016f, height, BellySize.z * girth + 0.016f));
        }

        // A flat detail sitting on the front of a rounded part, turned to follow its curve.
        private static Transform OnSurface(Transform parent, string name, Material material, Shape shape, Shape host, Vector3 hostAt,
            Vector3 hostSize, float x, float y, Vector3 size)
        {
            Vector2 half = new Vector2(hostSize.x * 0.5f, hostSize.y * 0.5f);
            float u = (x - hostAt.x) / half.x, v = (y - hostAt.y) / half.y;
            float z = hostAt.z + hostSize.z * 0.5f * host.Front(u, v);
            const float d = 0.05f;
            float slope = (host.Front(u + d, v) - host.Front(u - d, v)) * hostSize.z * 0.5f / (2f * d * half.x);
            float yaw = -Mathf.Atan(slope) * Mathf.Rad2Deg;
            return Part(parent, name, material, shape, new Vector3(x, y, z - size.z * 0.2f), size, new Vector3(0f, yaw, 0f));
        }

        // A short puffy arm hanging from the shoulder, ending in a pudgy hand whose palm faces the body.
        private static Transform Arm(Transform body, string name, float side, Vector3 angles, Material suit, Material cream, Material glove,
            float grip, out Hand hand)
        {
            Transform shoulder = Joint(body, name, new Vector3(0.28f * side, 1.03f, 0f));
            shoulder.localRotation = Quaternion.Euler(angles);
            Static(shoulder, "Sleeve", suit, ArmMesh(side));
            Part(shoulder, "Cuff", cream, Band, new Vector3(0f, -0.34f, 0f), new Vector3(0.155f, 0.05f, 0.165f));
            hand = BuildHand(shoulder, "Hand", side, glove);
            hand.Root.localPosition = new Vector3(0f, -0.44f, 0f);
            hand.Root.localRotation = Quaternion.Euler(90f, 90f * side, 0f); // fingers down the arm, palm inward, thumb forward
            hand.Root.localScale = Vector3.one * 1.75f;
            hand.Curl(grip);
            return shoulder;
        }

        // ---------- Seamless body parts (built once, shared) ----------

        // The seamless meshes take a moment to build, so they are baked into an asset in the editor
        // (Signal Lost > Models > Bake Mechanic Meshes) and loaded from there. If the asset is missing or out of date,
        // they are built at runtime instead (a short hitch on first use) and a warning says to bake.

        /// <summary>Bump whenever a seamless shape changes, so out-of-date baked meshes are ignored until rebaked.</summary>
        public const int MeshVersion = 1;
        /// <summary>The folder (under Resources) holding the baked meshes, one asset per mesh named "name vN".</summary>
        public const string BakedPath = "SignalLost/MechanicMeshes";
        private static Dictionary<string, Mesh> baked;
        private static bool ignoreBaked;
        private static readonly HashSet<string> Warned = new HashSet<string>();

        private static Mesh Baked(string key)
        {
            if (ignoreBaked) return null;
            string wanted = key + " v" + MeshVersion;
            if (baked == null || (baked.TryGetValue(wanted, out Mesh stale) && stale == null))
            {
                baked = new Dictionary<string, Mesh>();
                foreach (Mesh mesh in Resources.LoadAll<Mesh>(BakedPath)) baked[mesh.name] = mesh;
            }
            if (baked.TryGetValue(wanted, out Mesh found) && found != null) return found;
            if (Warned.Add(key))
                Debug.LogWarning($"The mechanic's '{key}' mesh isn't baked (version {MeshVersion}), so it is being built now, " +
                                 "which takes a moment. Bake it with Signal Lost > Models > Bake Mechanic Meshes.");
            return null;
        }

        /// <summary>
        /// Builds every seamless mesh from scratch, ignoring baked ones, for the editor's baker. Returned by name
        /// (without the version); the baker saves them as "name vN".
        /// </summary>
        public static Dictionary<string, Mesh> BuildMeshesForBaking()
        {
            ignoreBaked = true;
            Hand.GloveMeshes.Clear();
            SoftMeshes.Clear();
            var temporary = new GameObject("Mechanic bake (temporary)");
            try
            {
                BuildHand(temporary.transform, "Right", 1f, null);
                BuildHand(temporary.transform, "Left", -1f, null);
                return new Dictionary<string, Mesh>
                {
                    ["Glove R"] = Hand.GloveMeshes[1],
                    ["Glove L"] = Hand.GloveMeshes[-1],
                    ["Suit"] = SuitMesh(),
                    ["Arm R"] = ArmMesh(1f),
                    ["Arm L"] = ArmMesh(-1f),
                };
            }
            finally
            {
                ignoreBaked = false;
                Object.DestroyImmediate(temporary);
            }
        }

        private static readonly Dictionary<string, Mesh> SoftMeshes = new Dictionary<string, Mesh>();
        private static readonly UnityEngine.Pose[] NoBones = { new UnityEngine.Pose(Vector3.zero, Quaternion.identity) };

        private static Mesh SuitMesh()
        {
            if (SoftMeshes.TryGetValue("Suit", out Mesh mesh) && mesh != null) return mesh;
            mesh = Baked("Suit");
            if (mesh != null) return SoftMeshes["Suit"] = mesh;
            var shape = new SoftShape { CoreBlend = 0.09f, AttachBlend = 0.06f };
            shape.Ellipsoid(0, 0, BellyAt, BellySize * 0.5f);
            shape.Ellipsoid(0, 0, ChestAt, ChestSize * 0.5f);
            for (int s = -1; s <= 1; s += 2)
                shape.Cone(shape.NewGroup(), 0, new Vector3(0.135f * s, 0.46f, 0f), 0.125f, new Vector3(0.135f * s, 0.13f, 0.005f), 0.112f);
            mesh = shape.Build("Mechanic Suit", 0.016f, NoBones);
            SoftMeshes["Suit"] = mesh;
            return mesh;
        }

        // A short puffy arm in the shoulder's space: a round shoulder melting into a sleeve that tapers to the cuff.
        private static Mesh ArmMesh(float side)
        {
            string key = side > 0f ? "Arm R" : "Arm L";
            if (SoftMeshes.TryGetValue(key, out Mesh mesh) && mesh != null) return mesh;
            mesh = Baked(key);
            if (mesh != null) return SoftMeshes[key] = mesh;
            var shape = new SoftShape { CoreBlend = 0.05f };
            shape.Ellipsoid(0, 0, new Vector3(-0.02f * side, -0.035f, 0f), new Vector3(0.085f, 0.08f, 0.085f));
            shape.Cone(0, 0, new Vector3(0f, -0.06f, 0f), 0.08f, new Vector3(0f, -0.335f, 0.005f), 0.07f);
            mesh = shape.Build("Mechanic " + key, 0.011f, NoBones);
            SoftMeshes[key] = mesh;
            return mesh;
        }

        private static void Static(Transform parent, string name, Material material, Mesh mesh)
        {
            var part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }
}
