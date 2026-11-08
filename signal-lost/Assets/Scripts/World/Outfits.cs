using System.Collections.Generic;
using SignalLost.NPC;
using SignalLost.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace SignalLost.World
{
    /// <summary>What the mechanic wears on an island: colours for the suit and kit, and the hat.</summary>
    public struct MechanicLook
    {
        public Color Suit, Trim, Glove, Skin, Boot, Belt, Patch;
        public Color Hat, HatBand, Lens, Scarf;
        /// <summary>A wide-brimmed bush hat with goggles on the band; otherwise the orange work cap.</summary>
        public bool BushHat;
        public bool Neckerchief;
    }

    /// <summary>
    /// Island outfits (Lawson, 11-08): the mechanic, Cavendish and Eugene dress for the island they are on
    /// (<see cref="StageTheme"/>).
    /// <list type="bullet">
    /// <item><b>Crash Meadow</b> is how everyone starts: the orange jumpsuit and cap, Cavendish's ivory-and-teal
    /// space suit, Eugene's plain trading bucket. Nothing is added.</item>
    /// <item><b>Desert Dunes:</b> the mechanic wears a khaki jumpsuit with rust trim, suede gloves, a bush hat with
    /// goggles on the band and a neckerchief. Cavendish's suit turns sand and terracotta; he leaves off the helmet frame
    /// and lamp for a straw sun hat (ears through the brim), sunglasses and a neckerchief. Eugene plants a striped beach
    /// parasol in his bucket, and wears goggles on his eye stalks and a bandana.</item>
    /// </list>
    /// The extra pieces are low-poly shapes built at runtime and ride on the models' own parts, so they follow the
    /// seat drop, the crab's eye stalks and so on. Only the looks change; nobody's collisions, size or behaviour does.
    /// </summary>
    public static class Outfits
    {
        private const string Marker = "Island Outfit";

        private static readonly Color Khaki = new Color(0.8f, 0.68f, 0.47f);
        private static readonly Color Sand = new Color(0.86f, 0.76f, 0.56f);
        private static readonly Color Rust = new Color(0.72f, 0.3f, 0.2f);
        private static readonly Color Terracotta = new Color(0.7f, 0.37f, 0.23f);
        private static readonly Color Straw = new Color(0.88f, 0.75f, 0.45f);
        private static readonly Color Cream = new Color(0.95f, 0.91f, 0.82f);
        private static readonly Color Shade = new Color(0.07f, 0.08f, 0.1f);
        private static readonly Color Brass = new Color(0.82f, 0.63f, 0.28f);
        private static readonly Color Pole = new Color(0.45f, 0.3f, 0.18f);

        private static readonly Dictionary<Color, Material> Paints = new Dictionary<Color, Material>();
        private static readonly Dictionary<(Material, Color), Material> Recoloured = new Dictionary<(Material, Color), Material>();

        // ---------- The mechanic ----------

        public static MechanicLook Mechanic(IslandLook look)
        {
            // The usual look is the mechanic model's own colours.
            var meadow = new MechanicLook
            {
                Suit = MechanicModel.Suit, Trim = MechanicModel.Cream, Glove = MechanicModel.Glove, Skin = MechanicModel.Skin,
                Boot = MechanicModel.Boot, Belt = MechanicModel.Belt, Patch = MechanicModel.Teal, Hat = MechanicModel.Suit
            };
            if (look != IslandLook.Desert) return meadow;
            MechanicLook desert = meadow;
            desert.Suit = Khaki;
            desert.Trim = Rust;
            desert.Glove = new Color(0.78f, 0.62f, 0.42f);
            desert.Boot = new Color(0.52f, 0.36f, 0.22f);
            desert.Hat = new Color(0.64f, 0.56f, 0.36f);
            desert.HatBand = Rust;
            desert.Lens = Shade;
            desert.Scarf = Rust;
            desert.BushHat = true;
            desert.Neckerchief = true;
            return desert;
        }

        // ---------- Cavendish and Eugene ----------

        /// <summary>Dresses an NPC for <paramref name="look"/> (once). The meadow look is the models as they are.</summary>
        public static void Dress(NpcActor actor, IslandLook look)
        {
            if (actor == null || look == IslandLook.Meadow || actor.transform.Find(Marker) != null) return;
            new GameObject(Marker).transform.SetParent(actor.transform, false);
            if (actor.Kind == NpcActor.Role.Cavendish) DressCavendish(actor.transform);
            else DressMerchant(actor.transform);
        }

        private static void DressCavendish(Transform root)
        {
            Transform visual = Find(root, "Visual") ?? root;
            Recolour(visual, "SuitIvory", Sand);
            Recolour(visual, "SuitTeal", Terracotta);
            foreach (string part in new[] { "Helmet lamp", "Lamp lens", "Raised helmet frame" })
            {
                Transform hidden = Find(visual, part);
                if (hidden != null) hidden.gameObject.SetActive(false);
            }
            Renderer headPart = Part(visual, "Capybara head");
            if (headPart == null) return;
            Bounds head = LocalBounds(visual, headPart);

            // A wide straw sun hat, tipped back; his round ears poke up through the brim.
            Transform hat = Empty(visual, "Sun Hat", new Vector3(head.center.x, head.max.y - 0.08f, head.center.z - 0.05f), Quaternion.Euler(-8f, 0f, 0f));
            float crown = head.extents.x * 0.8f, brim = head.extents.x * 1.6f;
            AddMesh(hat, "Brim", Lathe(new[] { new Vector2(crown, -0.01f), new Vector2(brim, -0.035f), new Vector2(brim + 0.01f, 0f), new Vector2(crown, 0.025f) }, 10, true), Straw);
            AddMesh(hat, "Crown", Lathe(new[] { new Vector2(crown, 0f), new Vector2(crown * 0.97f, 0.17f), new Vector2(crown * 0.8f, 0.25f), new Vector2(crown * 0.38f, 0.27f), new Vector2(0f, 0.255f) }, 10, false), Straw);
            AddMesh(hat, "Band", Lathe(new[] { new Vector2(crown * 1.02f, 0.015f), new Vector2(crown * 1.02f, 0.075f) }, 10, false), Rust);

            // Sunglasses over the eyes, with a bridge over the top of the muzzle and arms back toward the ears.
            Renderer right = Part(visual, "Eye"), left = Part(visual, "Eye.001");
            if (right != null && left != null)
            {
                Bounds r = LocalBounds(visual, right), l = LocalBounds(visual, left);
                float front = Mathf.Max(r.max.z, l.max.z) + 0.015f;
                foreach (Bounds eye in new[] { r, l })
                {
                    float side = Mathf.Sign(eye.center.x - head.center.x);
                    Box(visual, "Sunglass Lens", Shade, new Vector3(eye.center.x, eye.center.y, front), new Vector3(0.16f, 0.1f, 0.025f), Quaternion.identity);
                    Box(visual, "Sunglass Arm", Shade, new Vector3(eye.center.x + side * 0.08f, eye.center.y + 0.015f, (front + head.center.z) * 0.5f),
                        new Vector3(0.018f, 0.018f, Mathf.Max(0.05f, front - head.center.z)), Quaternion.identity);
                }
                float gap = Mathf.Abs(r.center.x - l.center.x);
                Box(visual, "Sunglass Bridge", Shade, new Vector3((r.center.x + l.center.x) * 0.5f, (r.center.y + l.center.y) * 0.5f + 0.025f, front),
                    new Vector3(Mathf.Max(0.05f, gap - 0.14f), 0.022f, 0.022f), Quaternion.identity);
            }

            // A neckerchief tied over the suit's collar, its point hanging down the front.
            Renderer collarPart = Part(visual, "Collar rim");
            if (collarPart != null)
            {
                Bounds collar = LocalBounds(visual, collarPart);
                float radius = collar.extents.x + 0.02f;
                Transform scarf = Empty(visual, "Neckerchief", new Vector3(collar.center.x, collar.min.y - 0.02f, collar.center.z), Quaternion.identity);
                AddMesh(scarf, "Knot", Lathe(new[] { new Vector2(radius, -0.05f), new Vector2(radius + 0.02f, 0f), new Vector2(radius, 0.05f) }, 10, false), Rust);
                AddMesh(scarf, "Point", Triangle(new Vector3(-0.18f, 0.02f, radius - 0.02f), new Vector3(0.18f, 0.02f, radius - 0.02f), new Vector3(0f, -0.22f, radius + 0.03f)), Rust);
            }
        }

        private static void DressMerchant(Transform root)
        {
            // A striped beach parasol planted in the bucket, leaning back over the shell.
            Transform socket = Find(root, "Shell Socket");
            if (socket != null)
            {
                var shell = new List<Renderer>(socket.GetComponentsInChildren<Renderer>(true));
                shell.RemoveAll(r => r is ParticleSystemRenderer || !r.gameObject.activeInHierarchy);
                if (shell.Count > 0)
                {
                    Bounds bucket = LocalBounds(root, shell[0]);
                    foreach (Renderer part in shell) bucket.Encapsulate(LocalBounds(root, part));
                    float height = bucket.size.y * 0.8f, spread = Mathf.Max(bucket.size.x, bucket.size.z) * 0.55f;
                    Transform parasol = Empty(root, "Beach Parasol", new Vector3(bucket.center.x, bucket.max.y - bucket.size.y * 0.25f, bucket.center.z),
                        Quaternion.Euler(-14f, 0f, 9f));
                    Box(parasol, "Pole", Pole, new Vector3(0f, height * 0.5f, 0f), new Vector3(0.035f, height, 0.035f), Quaternion.identity);
                    Transform canopy = Empty(parasol, "Canopy", new Vector3(0f, height, 0f), Quaternion.identity);
                    for (int stripe = 0; stripe < 2; stripe++)
                    {
                        Vector2[] top = { new Vector2(spread, -0.02f), new Vector2(0f, spread * 0.36f) };
                        Vector2[] under = { new Vector2(0f, spread * 0.34f), new Vector2(spread, -0.03f) };
                        AddMesh(canopy, "Canopy " + stripe, Lathe(top, 10, false, stripe), stripe == 0 ? Terracotta : Cream);
                        AddMesh(canopy, "Canopy Under " + stripe, Lathe(under, 10, false, stripe), stripe == 0 ? Terracotta * 0.85f : Cream * 0.85f);
                    }
                    Box(canopy, "Finial", Pole, new Vector3(0f, spread * 0.38f, 0f), Vector3.one * 0.06f, Quaternion.identity);
                    parasol.SetParent(socket, true);

                    // Its speech bubbles float above the parasol rather than in it.
                    NpcDialogue voice = root.GetComponentInChildren<NpcDialogue>(true);
                    if (voice != null) voice.transform.position += Vector3.up * (height * root.lossyScale.y * 0.6f);
                }
            }

            // Goggles on the eye stalks: a dark lens in a brass rim in front of each eye.
            foreach (Transform eye in All(root, "Eye"))
            {
                var renderer = eye.GetComponent<Renderer>();
                if (renderer == null) continue;
                Bounds bounds = LocalBounds(root, renderer);
                float size = Mathf.Max(bounds.size.x, bounds.size.y);
                Transform goggle = Empty(root, "Goggle", new Vector3(bounds.center.x, bounds.center.y, bounds.max.z + size * 0.05f), Quaternion.identity);
                Box(goggle, "Rim", Brass, Vector3.zero, new Vector3(size * 1.12f, size * 1.12f, size * 0.12f), Quaternion.identity);
                Box(goggle, "Lens", Shade, new Vector3(0f, 0f, size * 0.05f), new Vector3(size * 0.86f, size * 0.86f, size * 0.1f), Quaternion.identity);
                goggle.SetParent(eye, true);
            }

            // A bandana knotted round the front of its body.
            Transform body = Find(root, "Body");
            var bodyRenderer = body != null ? body.GetComponent<Renderer>() : null;
            if (bodyRenderer != null)
            {
                Bounds b = LocalBounds(root, bodyRenderer);
                float y = b.max.y - b.size.y * 0.2f, z = b.max.z + 0.01f, half = b.extents.x * 0.85f;
                Transform bandana = Empty(root, "Bandana", Vector3.zero, Quaternion.identity);
                AddMesh(bandana, "Cloth", Triangle(new Vector3(b.center.x - half, y, z - 0.04f), new Vector3(b.center.x + half, y, z - 0.04f),
                    new Vector3(b.center.x, y - b.size.y * 0.45f, z + 0.02f)), Rust);
                bandana.SetParent(body, true);
            }
        }

        // ---------- Building blocks ----------

        /// <summary>A copy of each <paramref name="materialName"/> material under <paramref name="root"/> in a new colour.</summary>
        private static void Recolour(Transform root, string materialName, Color color)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null || !materials[i].name.StartsWith(materialName)) continue;
                    if (!Recoloured.TryGetValue((materials[i], color), out Material copy) || copy == null)
                    {
                        copy = new Material(materials[i]) { name = materials[i].name + " (island)" };
                        if (copy.HasProperty("_BaseColor")) copy.SetColor("_BaseColor", color);
                        if (copy.HasProperty("_Color")) copy.SetColor("_Color", color);
                        Recoloured[(materials[i], color)] = copy;
                    }
                    materials[i] = copy;
                    changed = true;
                }
                if (changed) renderer.sharedMaterials = materials;
            }
        }

        public static Material Paint(Color color)
        {
            if (Paints.TryGetValue(color, out Material material) && material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Outfit " + ColorUtility.ToHtmlStringRGB(color) };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.2f);
            Paints[color] = material;
            return material;
        }

        private static Transform Empty(Transform parent, string name, Vector3 localPosition, Quaternion localRotation)
        {
            var piece = new GameObject(name).transform;
            piece.SetParent(parent, false);
            piece.SetLocalPositionAndRotation(localPosition, localRotation);
            return piece;
        }

        private static Transform Box(Transform parent, string name, Color color, Vector3 localPosition, Vector3 size, Quaternion localRotation)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            Object.DestroyImmediate(box.GetComponent<Collider>());
            box.transform.SetParent(parent, false);
            box.transform.SetLocalPositionAndRotation(localPosition, localRotation);
            box.transform.localScale = size;
            box.GetComponent<Renderer>().sharedMaterial = Paint(color);
            return box.transform;
        }

        private static void AddMesh(Transform parent, string name, Mesh mesh, Color color)
        {
            var piece = new GameObject(name);
            piece.transform.SetParent(parent, false);
            piece.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = piece.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Paint(color);
            renderer.shadowCastingMode = ShadowCastingMode.On;
        }

        /// <summary>
        /// A faceted solid of revolution: <paramref name="profile"/> is (radius, height) points, listed so the solid's
        /// inside is on their left (up the outside, then in across the top), which makes the faces point outward.
        /// With <paramref name="onlyParity"/> 0 or 1, only every other segment is built (for stripes).
        /// </summary>
        public static Mesh Lathe(Vector2[] profile, int sides, bool closed, int onlyParity = -1)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            int edges = closed ? profile.Length : profile.Length - 1;
            for (int k = 0; k < edges; k++)
            {
                Vector2 a = profile[k], b = profile[(k + 1) % profile.Length];
                for (int s = 0; s < sides; s++)
                {
                    if (onlyParity >= 0 && s % 2 != onlyParity) continue;
                    float a0 = s * Mathf.PI * 2f / sides, a1 = (s + 1) * Mathf.PI * 2f / sides;
                    Vector3 p00 = new Vector3(Mathf.Cos(a0) * a.x, a.y, Mathf.Sin(a0) * a.x), p01 = new Vector3(Mathf.Cos(a1) * a.x, a.y, Mathf.Sin(a1) * a.x);
                    Vector3 p10 = new Vector3(Mathf.Cos(a0) * b.x, b.y, Mathf.Sin(a0) * b.x), p11 = new Vector3(Mathf.Cos(a1) * b.x, b.y, Mathf.Sin(a1) * b.x);
                    int i = vertices.Count;
                    vertices.AddRange(new[] { p00, p10, p11, p00, p11, p01 });
                    for (int t = 0; t < 6; t++) triangles.Add(i + t);
                }
            }
            var mesh = new Mesh { name = "Outfit Lathe" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A two-sided triangle (a hanging scarf or bandana point).</summary>
        private static Mesh Triangle(Vector3 a, Vector3 b, Vector3 c)
        {
            var mesh = new Mesh { name = "Outfit Cloth" };
            mesh.SetVertices(new[] { a, b, c, a, c, b });
            mesh.SetTriangles(new[] { 0, 1, 2, 3, 4, 5 }, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Transform Find(Transform root, string name)
        {
            foreach (Transform part in root.GetComponentsInChildren<Transform>(true))
                if (part.name == name) return part;
            return null;
        }

        private static IEnumerable<Transform> All(Transform root, string name)
        {
            foreach (Transform part in root.GetComponentsInChildren<Transform>(true))
                if (part.name == name) yield return part;
        }

        private static Renderer Part(Transform root, string name)
        {
            Transform part = Find(root, name);
            return part != null ? part.GetComponent<Renderer>() : null;
        }

        /// <summary>A part's mesh bounds in <paramref name="space"/>'s own coordinates, whichever way it is turned.</summary>
        private static Bounds LocalBounds(Transform space, Renderer part)
        {
            var filter = part.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
                return new Bounds(space.InverseTransformPoint(part.bounds.center), space.InverseTransformVector(part.bounds.size));
            Matrix4x4 toSpace = space.worldToLocalMatrix * part.transform.localToWorldMatrix;
            Bounds mesh = filter.sharedMesh.bounds;
            Bounds result = new Bounds(toSpace.MultiplyPoint3x4(mesh.min), Vector3.zero);
            for (int corner = 0; corner < 8; corner++)
                result.Encapsulate(toSpace.MultiplyPoint3x4(mesh.center + Vector3.Scale(mesh.extents,
                    new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1))));
            return result;
        }
    }
}
