using System;
using System.Collections.Generic;
using System.IO;
using SignalLost.Effects;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SignalLost.Editor
{
    /// <summary>
    /// Generates the tutorial meadow's art (meeting notes 10-22): low-poly pines and golden-green broadleaf trees,
    /// bushes, mossy rocks and boulders, grass tufts and flowers, Cavendish's campsite props, and the fire and smoke
    /// effects (campfire, electrical fire, smoke plume). Original geometry, URP materials and particle systems; no
    /// imported assets. Re-running overwrites the generated assets in place, keeping their GUIDs.
    /// Menu: Signal Lost > Tutorial > Build Meadow Assets. TutorialLevelBuilder scatters them.
    /// </summary>
    internal static class MeadowAssetBuilder
    {
        public const string Folder = "Assets/Prefabs/Enviornment objects/TutorialMeadow";

        public enum M
        {
            Bark, Needles, NeedleTips, LeafGreen, LeafGold, LeafYellow, Bush, BushDark, Rock, RockDark, Moss,
            Grass, GrassDry, FlowerYellow, FlowerWhite, FlowerPink, TentOrange, TentTeal, Canvas, Log, Stone, Ash
        }

        private static readonly Dictionary<M, Material> Materials = new Dictionary<M, Material>();

        public static readonly string[] Trees = { "PineA", "PineB", "PineC", "BroadleafA", "BroadleafB", "BroadleafGold" };
        public static readonly string[] Bushes = { "BushA", "BushB" };
        public static readonly string[] Rocks = { "RockA", "RockB", "RockC", "Boulder", "BoulderTall" };
        public static readonly string[] Ground = { "GrassTuft", "GrassTuftDry", "FlowersYellow", "FlowersWhite", "FlowersPink" };

        [MenuItem("Signal Lost/Tutorial/Build Meadow Assets")]
        public static void Build()
        {
            EnsureFolder(Folder);
            EnsureFolder(Folder + "/Meshes");
            EnsureFolder(Folder + "/Materials");
            EnsureFolder(Folder + "/Effects");
            BuildMaterials();
            for (int v = 0; v < 3; v++) Prefab(Trees[v], Pine(v), Collider.Trunk);
            Prefab("BroadleafA", Broadleaf(0, M.LeafGreen), Collider.Trunk);
            Prefab("BroadleafB", Broadleaf(1, M.LeafGreen), Collider.Trunk);
            Prefab("BroadleafGold", Broadleaf(2, M.LeafGold), Collider.Trunk);
            Prefab("BushA", Bush(0), Collider.None);
            Prefab("BushB", Bush(1), Collider.None);
            Prefab("RockA", Rock(0, new Vector3(0.9f, 0.6f, 0.8f)), Collider.None);
            Prefab("RockB", Rock(1, new Vector3(0.7f, 0.45f, 1f)), Collider.None);
            Prefab("RockC", Rock(2, new Vector3(1.1f, 0.5f, 0.9f)), Collider.None);
            Prefab("Boulder", Rock(3, new Vector3(2.6f, 1.8f, 2.2f)), Collider.Hull);
            Prefab("BoulderTall", Rock(4, new Vector3(2.2f, 3.6f, 2f)), Collider.Hull);
            Prefab("GrassTuft", Grass(0, M.Grass), Collider.None);
            Prefab("GrassTuftDry", Grass(1, M.GrassDry), Collider.None);
            Prefab("FlowersYellow", Flowers(0, M.FlowerYellow), Collider.None);
            Prefab("FlowersWhite", Flowers(1, M.FlowerWhite), Collider.None);
            Prefab("FlowersPink", Flowers(2, M.FlowerPink), Collider.None);
            Prefab("Tent", Tent(), Collider.Hull);
            Prefab("LogSeat", LogSeat(), Collider.Box);
            Prefab("FirePit", FirePit(), Collider.None);
            BuildEffects();
            AssetDatabase.SaveAssets();
            Debug.Log("MEADOW_ASSETS_BUILT");
        }

        public static string PrefabPath(string name) => Folder + "/" + name + ".prefab";
        public static string EffectPath(string name) => Folder + "/Effects/" + name + ".prefab";
        public static Material MaterialFor(M key) => Materials[key];

        // ---------- Materials ----------

        private static void BuildMaterials()
        {
            Mat(M.Bark, new Color(0.36f, 0.24f, 0.15f));
            Mat(M.Needles, new Color(0.13f, 0.33f, 0.19f));
            Mat(M.NeedleTips, new Color(0.22f, 0.46f, 0.24f));
            Mat(M.LeafGreen, new Color(0.45f, 0.62f, 0.22f));
            Mat(M.LeafGold, new Color(0.86f, 0.68f, 0.2f));
            Mat(M.LeafYellow, new Color(0.93f, 0.85f, 0.3f));
            Mat(M.Bush, new Color(0.3f, 0.55f, 0.22f));
            Mat(M.BushDark, new Color(0.2f, 0.4f, 0.17f));
            Mat(M.Rock, new Color(0.56f, 0.54f, 0.5f));
            Mat(M.RockDark, new Color(0.4f, 0.38f, 0.36f));
            Mat(M.Moss, new Color(0.38f, 0.52f, 0.22f));
            Mat(M.Grass, new Color(0.42f, 0.65f, 0.23f));
            Mat(M.GrassDry, new Color(0.72f, 0.7f, 0.3f));
            Mat(M.FlowerYellow, new Color(1f, 0.85f, 0.2f));
            Mat(M.FlowerWhite, new Color(0.96f, 0.95f, 0.9f));
            Mat(M.FlowerPink, new Color(0.95f, 0.45f, 0.62f));
            Mat(M.TentOrange, new Color(0.93f, 0.5f, 0.16f));
            Mat(M.TentTeal, new Color(0.26f, 0.6f, 0.58f));
            Mat(M.Canvas, new Color(0.78f, 0.72f, 0.58f));
            Mat(M.Log, new Color(0.45f, 0.3f, 0.18f));
            Mat(M.Stone, new Color(0.48f, 0.46f, 0.44f));
            Mat(M.Ash, new Color(0.12f, 0.1f, 0.09f));
        }

        private static void Mat(M key, Color color)
        {
            string path = Folder + "/Materials/Meadow" + key + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.12f);
            material.SetFloat("_Metallic", 0f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            Materials[key] = material;
        }

        // ---------- Shapes ----------

        private static FlatMeshKit<M> Pine(int variant)
        {
            var kit = new FlatMeshKit<M>();
            float height = 7f + variant * 1.6f;
            Trunk(kit, 0.32f, height * 0.35f, variant);
            int tiers = 3 + variant % 2 + (variant == 2 ? 1 : 0);
            for (int t = 0; t < tiers; t++)
            {
                float k = t / (float)tiers;
                float radius = Mathf.Lerp(2.4f + variant * 0.25f, 0.9f, k);
                float baseY = height * (0.2f + k * 0.62f);
                Mesh cone = LowPolyMesh.Lathe("Tier", new[] { new Vector2(0.01f, 0f), new Vector2(radius, 0f), new Vector2(0.01f, radius * 1.25f) }, 7);
                Displace(cone, 0.12f, variant * 13 + t);
                kit.Add(cone, Matrix4x4.TRS(new Vector3(0f, baseY, 0f), Quaternion.Euler(0f, t * 23f + variant * 11f, 0f), Vector3.one), t == tiers - 1 ? M.NeedleTips : M.Needles);
            }
            return kit;
        }

        private static FlatMeshKit<M> Broadleaf(int variant, M leaves)
        {
            var kit = new FlatMeshKit<M>();
            float height = 4.2f + variant * 0.7f;
            Trunk(kit, 0.34f, height, variant);
            // Two crooked branches, then a cluster of lumpy canopy blobs.
            kit.Add(LowPolyMesh.TaperedPrism("Branch", 5, 0.4f), Matrix4x4.TRS(new Vector3(0f, height * 0.7f, 0f), Quaternion.Euler(-50f, 40f + variant * 30f, 0f), new Vector3(0.12f, 0.12f, 1.5f)), M.Bark);
            kit.Add(LowPolyMesh.TaperedPrism("Branch", 5, 0.4f), Matrix4x4.TRS(new Vector3(0f, height * 0.75f, 0f), Quaternion.Euler(-45f, 220f + variant * 25f, 0f), new Vector3(0.11f, 0.11f, 1.3f)), M.Bark);
            var random = new System.Random(variant * 7 + 3);
            int blobs = 4 + variant;
            for (int b = 0; b < blobs; b++)
            {
                float angle = b * Mathf.PI * 2f / blobs + (float)random.NextDouble();
                float spread = b == 0 ? 0f : 1.4f + (float)random.NextDouble() * 0.6f;
                Vector3 centre = new Vector3(Mathf.Cos(angle) * spread, height + 0.9f + (float)random.NextDouble() * 1.1f, Mathf.Sin(angle) * spread);
                float size = b == 0 ? 2.3f : 1.5f + (float)random.NextDouble() * 0.5f;
                Mesh blob = LowPolyMesh.Icosphere("Canopy", 1);
                Displace(blob, 0.16f, variant * 31 + b);
                M shade = leaves == M.LeafGold && b % 2 == 1 ? M.LeafYellow : leaves;
                kit.Add(blob, Matrix4x4.TRS(centre, Quaternion.Euler(0f, b * 40f, 0f), new Vector3(size, size * 0.8f, size)), shade);
            }
            return kit;
        }

        private static void Trunk(FlatMeshKit<M> kit, float radius, float height, int variant)
        {
            Mesh trunk = LowPolyMesh.TaperedPrism("Trunk", 6, 0.55f);
            kit.Add(trunk, Matrix4x4.TRS(new Vector3(0f, -0.3f, 0f), Quaternion.Euler(-90f, variant * 17f, 0f), new Vector3(radius, radius, height + 0.3f)), M.Bark);
        }

        private static FlatMeshKit<M> Bush(int variant)
        {
            var kit = new FlatMeshKit<M>();
            for (int b = 0; b < 3 + variant; b++)
            {
                float angle = b * 2.1f + variant;
                Vector3 centre = new Vector3(Mathf.Cos(angle) * 0.55f, 0.45f + (b % 2) * 0.15f, Mathf.Sin(angle) * 0.55f) * (b == 0 ? 0f : 1f) + Vector3.up * (b == 0 ? 0.55f : 0f);
                Mesh blob = LowPolyMesh.Icosphere("Bush", 1);
                Displace(blob, 0.14f, variant * 17 + b);
                float size = b == 0 ? 1.1f : 0.75f;
                kit.Add(blob, Matrix4x4.TRS(centre, Quaternion.identity, new Vector3(size, size * 0.8f, size)), b % 2 == 0 ? M.Bush : M.BushDark);
            }
            return kit;
        }

        private static FlatMeshKit<M> Rock(int variant, Vector3 size)
        {
            var kit = new FlatMeshKit<M>();
            Mesh rock = LowPolyMesh.Icosphere("Rock", 1);
            Displace(rock, 0.22f, variant * 41 + 5);
            Matrix4x4 pose = Matrix4x4.TRS(new Vector3(0f, size.y * 0.32f, 0f), Quaternion.Euler(0f, variant * 50f, 0f), size);
            // Faces pointing up get moss; the rest is stone in two tones.
            Vector3[] vertices = rock.vertices;
            int[] triangles = rock.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = pose.MultiplyPoint3x4(vertices[triangles[i]]), b = pose.MultiplyPoint3x4(vertices[triangles[i + 1]]), c = pose.MultiplyPoint3x4(vertices[triangles[i + 2]]);
                Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                M tone = normal.y > 0.72f && variant != 1 ? M.Moss : (i / 3) % 3 == 0 ? M.RockDark : M.Rock;
                kit.Tri(a, b, c, normal, tone);
            }
            UnityEngine.Object.DestroyImmediate(rock);
            return kit;
        }

        // Thin double-sided blades fanned out from a point.
        private static FlatMeshKit<M> Grass(int variant, M color)
        {
            var kit = new FlatMeshKit<M>();
            var random = new System.Random(variant + 11);
            for (int i = 0; i < 9; i++)
            {
                float angle = i * 0.7f + (float)random.NextDouble();
                Vector3 root = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (float)random.NextDouble() * 0.25f;
                Vector3 lean = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.18f;
                float height = 0.35f + (float)random.NextDouble() * 0.35f;
                Vector3 side = Vector3.Cross(Vector3.up, lean.normalized) * 0.045f;
                Vector3 tip = root + lean + Vector3.up * height;
                kit.Tri(root - side, root + side, tip, lean + Vector3.up * 0.01f, color);
                kit.Tri(root + side, root - side, tip, -lean - Vector3.up * 0.01f, color);
            }
            return kit;
        }

        private static FlatMeshKit<M> Flowers(int variant, M petals)
        {
            FlatMeshKit<M> kit = Grass(variant + 5, M.Grass);
            for (int i = 0; i < 4; i++)
            {
                float angle = i * 1.6f + variant;
                Vector3 head = new Vector3(Mathf.Cos(angle) * 0.18f, 0.45f + (i % 2) * 0.12f, Mathf.Sin(angle) * 0.18f);
                kit.Box(head - Vector3.up * 0.22f, new Vector3(0.02f, 0.44f, 0.02f), Quaternion.identity, M.Grass);
                kit.Box(head, new Vector3(0.11f, 0.05f, 0.11f), Quaternion.Euler(0f, i * 30f, 0f), petals);
                kit.Box(head + Vector3.up * 0.03f, new Vector3(0.05f, 0.03f, 0.05f), Quaternion.identity, M.FlowerYellow);
            }
            return kit;
        }

        private static FlatMeshKit<M> Tent()
        {
            var kit = new FlatMeshKit<M>();
            Vector3 ridgeFront = new Vector3(0f, 1.7f, 1.4f), ridgeBack = new Vector3(0f, 1.7f, -1.4f);
            Vector3 leftFront = new Vector3(-1.4f, 0f, 1.4f), leftBack = new Vector3(-1.4f, 0f, -1.4f);
            Vector3 rightFront = new Vector3(1.4f, 0f, 1.4f), rightBack = new Vector3(1.4f, 0f, -1.4f);
            kit.Quad(leftFront, ridgeFront, ridgeBack, leftBack, new Vector3(-1f, 0.6f, 0f), M.TentOrange);
            kit.Quad(rightBack, ridgeBack, ridgeFront, rightFront, new Vector3(1f, 0.6f, 0f), M.TentOrange);
            kit.Quad(leftFront, leftBack, ridgeBack, ridgeFront, new Vector3(1f, -0.6f, 0f), M.Canvas); // inside faces
            kit.Quad(rightBack, rightFront, ridgeFront, ridgeBack, new Vector3(-1f, -0.6f, 0f), M.Canvas);
            kit.Tri(leftBack, rightBack, ridgeBack, Vector3.back, M.TentTeal);
            kit.Tri(leftFront, ridgeFront, new Vector3(-0.35f, 0f, 1.6f), Vector3.forward, M.TentTeal); // one flap tied open
            kit.Box(new Vector3(0f, 0.02f, 0f), new Vector3(3f, 0.04f, 3.3f), Quaternion.identity, M.Canvas);
            kit.Box(new Vector3(0f, 0.9f, 1.45f), new Vector3(0.06f, 1.8f, 0.06f), Quaternion.identity, M.Log);
            kit.Box(new Vector3(0f, 0.9f, -1.45f), new Vector3(0.06f, 1.8f, 0.06f), Quaternion.identity, M.Log);
            return kit;
        }

        private static FlatMeshKit<M> LogSeat()
        {
            var kit = new FlatMeshKit<M>();
            Mesh log = LowPolyMesh.Lathe("Log", new[] { new Vector2(0.01f, 0f), new Vector2(0.28f, 0f), new Vector2(0.3f, 1.8f), new Vector2(0.01f, 1.8f) }, 7);
            kit.Add(log, Matrix4x4.TRS(new Vector3(-0.9f, 0.26f, 0f), Quaternion.Euler(0f, 0f, -90f), Vector3.one), M.Log);
            return kit;
        }

        private static FlatMeshKit<M> FirePit()
        {
            var kit = new FlatMeshKit<M>();
            for (int i = 0; i < 9; i++)
            {
                float angle = i * Mathf.PI * 2f / 9f;
                Mesh stone = LowPolyMesh.Icosphere("Stone", 0);
                Displace(stone, 0.2f, i);
                kit.Add(stone, Matrix4x4.TRS(new Vector3(Mathf.Cos(angle) * 0.75f, 0.12f, Mathf.Sin(angle) * 0.75f), Quaternion.Euler(0f, i * 40f, 0f), new Vector3(0.28f, 0.2f, 0.24f)), M.Stone);
            }
            kit.Box(new Vector3(0f, 0.02f, 0f), new Vector3(1.2f, 0.04f, 1.2f), Quaternion.Euler(0f, 20f, 0f), M.Ash);
            for (int i = 0; i < 4; i++)
                kit.Add(LowPolyMesh.TaperedPrism("Firewood", 5, 0.8f), Matrix4x4.TRS(new Vector3(0f, 0.08f, 0f), Quaternion.Euler(-28f, i * 90f + 20f, 0f) * Quaternion.Euler(0f, 0f, 0f), new Vector3(0.07f, 0.07f, 0.65f)), M.Log);
            return kit;
        }

        // Moves each vertex by a smooth pseudo-random amount based on its position, keeping shared corners together.
        private static void Displace(Mesh mesh, float amount, int seed)
        {
            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 p = vertices[i];
                float n = Mathf.PerlinNoise(p.x * 3.1f + seed * 1.37f, p.z * 3.1f + p.y * 2.3f + seed * 0.71f) - 0.5f;
                vertices[i] = p * (1f + n * amount * 2f);
            }
            mesh.vertices = vertices;
        }

        // ---------- Effects ----------

        private static void BuildEffects()
        {
            Material fire = ParticleMaterial("ParticleFire", true, new Color(1f, 0.75f, 0.35f));
            Material smoke = ParticleMaterial("ParticleSmoke", false, new Color(1f, 1f, 1f));
            Material spark = ParticleMaterial("ParticleSpark", true, new Color(0.8f, 0.95f, 1f));

            Effect("Campfire", root =>
            {
                Flames(root, fire, 0.3f, 32f, 0.55f, new Color(1f, 0.85f, 0.4f), new Color(1f, 0.35f, 0.08f));
                Embers(root, fire);
                Smoke(root, smoke, "Wisps", 3f, 0.4f, 2.4f, 5f, new Color(0.45f, 0.43f, 0.42f, 0.35f), 0.8f);
                Glow(root, new Color(1f, 0.62f, 0.3f), 3.2f, 9f, false);
            });
            Effect("ElectricalFire", root =>
            {
                Flames(root, fire, 0.4f, 22f, 0.45f, new Color(0.75f, 0.9f, 1f), new Color(1f, 0.45f, 0.1f));
                Sparks(root, spark);
                Smoke(root, smoke, "Smoke", 4f, 0.8f, 4f, 7f, new Color(0.2f, 0.19f, 0.19f, 0.5f), 1.6f);
                Glow(root, new Color(0.6f, 0.85f, 1f), 2.5f, 7f, true);
            });
            Effect("SmokePlume", root => Smoke(root, smoke, "Plume", 7f, 1.6f, 9f, 11f, new Color(0.16f, 0.15f, 0.15f, 0.55f), 2.4f));
        }

        private static Material ParticleMaterial(string name, bool additive, Color tint)
        {
            string path = Folder + "/Effects/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetTexture("_BaseMap", AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd"));
            material.SetColor("_BaseColor", tint);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", additive ? 2f : 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void Effect(string name, Action<Transform> build)
        {
            var root = new GameObject(name);
            build(root.transform);
            PrefabUtility.SaveAsPrefabAsset(root, EffectPath(name));
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static ParticleSystem Emitter(Transform parent, string name, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // emit upward
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var main = system.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 300;
            return system;
        }

        private static void Flames(Transform root, Material material, float radius, float rate, float size, Color hot, Color cool)
        {
            ParticleSystem flames = Emitter(root, "Flames", material);
            var main = flames.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.7f, 1.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var emission = flames.emission;
            emission.rateOverTime = rate;
            var shape = flames.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 8f;
            shape.radius = radius;
            Fade(flames, hot, cool, 0.9f);
            var size01 = flames.sizeOverLifetime;
            size01.enabled = true;
            size01.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
        }

        private static void Embers(Transform root, Material material)
        {
            ParticleSystem embers = Emitter(root, "Embers", material);
            var main = embers.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 2.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
            main.gravityModifier = -0.05f;
            var emission = embers.emission;
            emission.rateOverTime = 7f;
            var shape = embers.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 18f;
            shape.radius = 0.3f;
            var noise = embers.noise;
            noise.enabled = true;
            noise.strength = 0.6f;
            noise.frequency = 0.8f;
            Fade(embers, new Color(1f, 0.8f, 0.35f), new Color(1f, 0.3f, 0.05f), 1f);
        }

        private static void Sparks(Transform root, Material material)
        {
            ParticleSystem sparks = Emitter(root, "Sparks", material);
            var main = sparks.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
            main.gravityModifier = 1.2f;
            var emission = sparks.emission;
            emission.rateOverTime = 2f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 12, 26, 0, 0.9f) });
            var shape = sparks.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 55f;
            shape.radius = 0.1f;
            var renderer = sparks.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.06f;
            renderer.lengthScale = 1.5f;
            Fade(sparks, new Color(0.85f, 0.97f, 1f), new Color(1f, 0.55f, 0.15f), 1f);
        }

        private static void Smoke(Transform root, Material material, string name, float rate, float startSize, float endSize, float lifetime, Color color, float radius)
        {
            ParticleSystem smoke = Emitter(root, name, material);
            smoke.transform.localPosition = Vector3.up * 0.6f;
            var main = smoke.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.8f, lifetime * 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(startSize * 0.8f, startSize * 1.2f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var emission = smoke.emission;
            emission.rateOverTime = rate;
            var shape = smoke.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 10f;
            shape.radius = radius;
            var size01 = smoke.sizeOverLifetime;
            size01.enabled = true;
            size01.size = new ParticleSystem.MinMaxCurve(endSize / startSize, AnimationCurve.EaseInOut(0f, startSize / endSize, 1f, 1f));
            var drift = smoke.velocityOverLifetime;
            drift.enabled = true;
            drift.space = ParticleSystemSimulationSpace.World;
            drift.x = new ParticleSystem.MinMaxCurve(0.4f, 0.9f); // a light breeze
            drift.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            drift.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            var rotation = smoke.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
            Fade(smoke, color, new Color(color.r * 1.3f, color.g * 1.3f, color.b * 1.3f, 0f), color.a);
        }

        // Colour from hot to cool over each particle's life, fading in quickly and out at the end.
        private static void Fade(ParticleSystem system, Color from, Color to, float alpha)
        {
            var color = system.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(from, 0f), new GradientColorKey(to, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(alpha, 0.12f), new GradientAlphaKey(alpha * 0.7f, 0.6f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
        }

        private static void Glow(Transform root, Color color, float intensity, float range, bool electrical)
        {
            var go = new GameObject("Glow");
            go.transform.SetParent(root, false);
            go.transform.localPosition = Vector3.up * 0.6f;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            var flicker = go.AddComponent<FlickerLight>();
            var data = new SerializedObject(flicker);
            data.FindProperty("electrical").boolValue = electrical;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- Assets ----------

        private enum Collider { None, Trunk, Hull, Box }

        private static void Prefab(string name, FlatMeshKit<M> kit, Collider collider)
        {
            Mesh mesh = SaveMesh("Meadow" + name, kit.Build("Meadow" + name));
            var root = new GameObject(name);
            var renderer = root.AddComponent<MeshRenderer>();
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            var shared = new Material[mesh.subMeshCount];
            for (int i = 0; i < shared.Length; i++) shared[i] = Materials[FlatMeshKit<M>.MaterialOf(mesh, i)];
            renderer.sharedMaterials = shared;
            if (collider == Collider.Trunk)
            {
                var trunk = root.AddComponent<CapsuleCollider>();
                trunk.radius = 0.4f;
                trunk.height = 4f;
                trunk.center = new Vector3(0f, 2f, 0f);
            }
            else if (collider == Collider.Hull)
            {
                var hull = root.AddComponent<MeshCollider>();
                hull.sharedMesh = mesh;
                hull.convex = true;
            }
            else if (collider == Collider.Box)
            {
                var box = root.AddComponent<BoxCollider>();
                box.center = mesh.bounds.center;
                box.size = mesh.bounds.size;
            }
            root.isStatic = true;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(name));
            UnityEngine.Object.DestroyImmediate(root);
        }

        public static Mesh SaveMesh(string name, Mesh mesh)
        {
            string path = Folder + "/Meshes/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            string order = FlatMeshKit<M>.Order(mesh);
            EditorUtility.CopySerialized(mesh, existing);
            existing.name = name;
            FlatMeshKit<M>.Remember(existing, order);
            EditorUtility.SetDirty(existing);
            UnityEngine.Object.DestroyImmediate(mesh);
            return existing;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
