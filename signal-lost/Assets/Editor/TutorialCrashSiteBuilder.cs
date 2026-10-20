using System;
using System.Collections.Generic;
using System.IO;
using SignalLost.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace SignalLost.Editor
{
    /// <summary>One-shot saved-scene conversion; never regenerates over authored crash-site edits.</summary>
    public static class TutorialCrashSiteBuilder
    {
        public const string ScenePath = "Assets/Scenes/Tutorial.unity";
        public const string AssetRoot = "Assets/Prefabs/Enviornment objects/TutorialCrashSite";
        public const string EnvironmentName = "Tutorial Impact Site";
        public const float TerrainSize = 160f;
        private const int Resolution = 257;
        private static Vector3 center;
        private static Vector2 gate;
        private static readonly List<Vector2> pads = new List<Vector2>();
        private static readonly List<Vector2[]> routes = new List<Vector2[]>();

        [MenuItem("Signal Lost/Tutorial Crash Site/Replace Saved Graybox")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode first.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject[] oldRoots = scene.GetRootGameObjects();
            foreach (GameObject root in oldRoots)
                if (root.name == EnvironmentName)
                    throw new InvalidOperationException("Crash site already exists. Edit it by hand; conversion will not overwrite it.");
            GameObject graybox = FindRoot(oldRoots, "graybox");
            GameObject rocket = FindRoot(oldRoots, "rocket");
            if (graybox == null || rocket == null)
                throw new InvalidOperationException("Expected saved graybox and rocket roots. No scene conversion performed.");
            // An interrupted conversion can resume while the saved scene is still the original graybox.
            // Once the scene contains EnvironmentName, the guard above always refuses regeneration.
            if (AssetDatabase.IsValidFolder(AssetRoot) &&
                AssetDatabase.LoadAssetAtPath<TerrainData>(AssetRoot + "/Terrain/ImpactCrater.asset") == null)
                throw new InvalidOperationException("Unknown pre-existing crash-site asset folder. Inspect it before conversion.");
            Directory.CreateDirectory("Logs");
            File.Copy(ScenePath, "Logs/TutorialBeforeImpactSite.unity", true);
            center = new Vector3(rocket.transform.position.x, 0, rocket.transform.position.z);
            SetLayout(oldRoots);
            EnsureFolder(AssetRoot + "/Materials");
            EnsureFolder(AssetRoot + "/Terrain");
            EnsureFolder(AssetRoot + "/Meshes");
            EnsureFolder(AssetRoot + "/Textures");
            Material basalt = Material("Basalt", new Color(0.16f, 0.18f, 0.19f), 0.02f, 0.12f);
            Material basaltLight = Material("BasaltAsh", new Color(0.25f, 0.26f, 0.25f), 0.01f, 0.10f);
            Material metal = Material("ScorchedHull", new Color(0.30f, 0.34f, 0.36f), 0.60f, 0.30f);
            Material soot = Material("BurntMetal", new Color(0.095f, 0.11f, 0.12f), 0.35f, 0.15f);
            Material warm = Material("OxidizedEdges", new Color(0.35f, 0.22f, 0.13f), 0.30f, 0.17f);
            Material hull = Material("DamagedRocketHull", new Color(0.54f, 0.56f, 0.54f), 0.45f, 0.24f);
            var environment = new GameObject(EnvironmentName);
            environment.transform.position = center;
            Terrain terrain = CreateTerrain(environment.transform);
            Mesh stone = CreateRockMesh();
            Mesh panel = CreateBentPanelMesh();
            BuildRocks(environment.transform, terrain, stone, basalt, basaltLight);
            BuildDebris(environment.transform, terrain, panel, metal, soot, warm);
            // Store a reusable environment without the author's ship or tutorial props.
            PrefabUtility.SaveAsPrefabAsset(environment, AssetRoot + "/CraterEnvironment.prefab");
            CrashRocket(environment.transform, rocket, hull, soot);
            GroundPreservedObjects(oldRoots, terrain);
            ConfigureLighting(oldRoots);
            DisableExtraViewpoints(oldRoots);
            // Replace a previously dragged-in preview of this generated environment, not unrelated authored roots.
            foreach (GameObject root in oldRoots)
                if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root) == AssetRoot + "/CraterEnvironment.prefab")
                    UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(graybox);
            Physics.SyncTransforms();
            Validate();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Could not save Tutorial scene.");
            AssetDatabase.SaveAssets();
            Debug.Log("TUTORIAL_IMPACT_SITE_CREATED_AND_VALIDATED");
        }

        private static GameObject FindRoot(GameObject[] roots, string name)
        {
            foreach (GameObject root in roots) if (root.name == name) return root;
            return null;
        }

        private static void SetLayout(GameObject[] roots)
        {
            pads.Clear(); routes.Clear();
            Vector2 player = LocalXZ(FindRoot(roots, "FirstPersonPlayer").transform.position);
            var points = new List<Vector2>();
            foreach (string name in new[] { "CapybaraAstronaut", "BurntRockBasin", "AlcoholFlask", "GrayboxExitGate" })
            {
                GameObject obj = FindRoot(roots, name);
                if (obj == null) continue;
                Vector2 p = LocalXZ(obj.transform.position);
                pads.Add(p);
                points.Add(p);
                if (name == "GrayboxExitGate") gate = p;
            }
            pads.Add(player);
            // Paths reserve space only; no quest order or progression requirement is introduced.
            foreach (Vector2 p in points) routes.Add(new[] { player, p });
            for (int i = 1; i < points.Count; i++) routes.Add(new[] { points[i - 1], points[i] });
            routes.Add(new[] { gate, gate + Vector2.up * 65f });
        }

        private static Vector2 LocalXZ(Vector3 p) { return new Vector2(p.x - center.x, p.z - center.z); }
        private static float Smooth(float lo, float hi, float value)
        { return Mathf.SmoothStep(0, 1, Mathf.InverseLerp(lo, hi, value)); }
        private static float Gaussian(Vector2 p, Vector2 at, float width)
        { return Mathf.Exp(-(p - at).sqrMagnitude / (width * width)); }
        private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 d = b - a;
            float t = d.sqrMagnitude > 0 ? Mathf.Clamp01(Vector2.Dot(p - a, d) / d.sqrMagnitude) : 0;
            return Vector2.Distance(p, a + t * d);
        }

        private static float Height(float x, float z)
        {
            Vector2 p = new Vector2(x, z);
            float r = p.magnitude;
            float unevenRadius = r + Mathf.Sin(Mathf.Atan2(z, x) * 5f) * 1.25f + Mathf.Sin(z * 0.12f) * 0.70f;
            float h = 1.45f * Smooth(24, 64, r);
            h += 3.80f * Mathf.Exp(-Mathf.Pow((unevenRadius - 43f) / 9.5f, 2)) * Smooth(23, 34, r);
            h += 2.0f * Gaussian(p, new Vector2(-34, 23), 10);
            h += 2.4f * Gaussian(p, new Vector2(31, -38), 11);
            h += 2.8f * Gaussian(p, new Vector2(-42, -30), 12);
            float micro = Mathf.PerlinNoise(x * 0.09f + 124, z * 0.09f + 332);
            h += (micro - 0.5f) * Mathf.Lerp(0.12f, 0.60f, Smooth(24, 42, r));
            // Wide level pads under existing authored props; gentle blending into the basin.
            foreach (Vector2 pad in pads) h *= Smooth(2.4f, 10f, Vector2.Distance(p, pad));
            // A broad, shallow saddle through the rim in the gate's existing +Z facing direction.
            float corridor = (1 - Smooth(4.2f, 20f, Mathf.Abs(x - gate.x))) * Smooth(gate.y - 3, gate.y + 7, z);
            float exitHeight = 1.5f * Smooth(gate.y + 16, gate.y + 57, z);
            h = Mathf.Lerp(h, exitHeight, corridor);
            return Mathf.Max(-0.08f, h);
        }

        private static Terrain CreateTerrain(Transform parent)
        {
            string dataPath = AssetRoot + "/Terrain/ImpactCrater.asset";
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(dataPath);
            // Lawson approved the existing relief. Reuse its heightmap exactly rather than reshaping the hills.
            if (data != null) return TerrainObject(parent, data);
            data = new TerrainData { name = "Dark Rocky Impact Crater" };
            data.heightmapResolution = Resolution;
            data.alphamapResolution = 256;
            data.baseMapResolution = 512;
            data.size = new Vector3(TerrainSize, 16, TerrainSize);
            float[,] heights = new float[Resolution, Resolution];
            for (int z = 0; z < Resolution; z++)
                for (int x = 0; x < Resolution; x++)
                    heights[z, x] = (2 + Height(x * TerrainSize / (Resolution - 1) - TerrainSize / 2,
                        z * TerrainSize / (Resolution - 1) - TerrainSize / 2)) / data.size.y;
            data.SetHeights(0, 0, heights);
            var layers = new TerrainLayer[3];
            foreach (int index in new[] { 0, 1, 2 })
            {
                string name = new[] { "FracturedBasalt", "ImpactSoot", "AshGravel" }[index];
                Texture2D diffuse = Texture(name, index, false);
                Texture2D normal = Texture(name + "Normal", index, true);
                string layerPath = AssetRoot + "/Terrain/" + name + ".terrainlayer";
                layers[index] = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
                if (layers[index] == null)
                {
                    layers[index] = new TerrainLayer { name = name, diffuseTexture = diffuse, normalMapTexture = normal,
                        tileSize = new Vector2(index == 1 ? 12 : 7, index == 1 ? 12 : 7), metallic = 0,
                        smoothness = 0.08f, normalScale = 0.55f };
                    AssetDatabase.CreateAsset(layers[index], layerPath);
                }
            }
            data.terrainLayers = layers;
            float[,,] alpha = new float[256, 256, 3];
            for (int z = 0; z < 256; z++)
                for (int x = 0; x < 256; x++)
                {
                    float px = x * TerrainSize / 255 - TerrainSize / 2;
                    float pz = z * TerrainSize / 255 - TerrainSize / 2;
                    float r = new Vector2(px, pz).magnitude;
                    float scorch = Mathf.Clamp01(1 - r / 23) * 0.75f;
                    float ash = Mathf.Exp(-Mathf.Pow((r - 40) / 11, 2)) * 0.36f +
                        Mathf.PerlinNoise(px * 0.055f + 71, pz * 0.055f + 6) * 0.16f;
                    alpha[z, x, 0] = 1 - scorch - ash;
                    alpha[z, x, 1] = scorch;
                    alpha[z, x, 2] = ash;
                }
            data.SetAlphamaps(0, 0, alpha);
            AssetDatabase.CreateAsset(data, dataPath);
            return TerrainObject(parent, data);
        }

        private static Terrain TerrainObject(Transform parent, TerrainData data)
        {
            GameObject obj = Terrain.CreateTerrainGameObject(data);
            obj.name = "Rocky Crater Terrain";
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = new Vector3(-TerrainSize / 2, -2, -TerrainSize / 2);
            Terrain terrain = obj.GetComponent<Terrain>();
            Shader shader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
            if (shader == null) throw new InvalidOperationException("URP terrain shader missing.");
            string materialPath = AssetRoot + "/Materials/DarkRockTerrain.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (mat == null)
            {
                mat = new Material(shader) { name = "DarkRockTerrain" };
                AssetDatabase.CreateAsset(mat, materialPath);
            }
            terrain.materialTemplate = mat;
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 3;
            terrain.basemapDistance = 200;
            terrain.Flush();
            return terrain;
        }

        private static float Grain(float u, float v)
        {
            float broad = Mathf.PerlinNoise(u * 8 + 41, v * 8 + 53);
            float fine = Mathf.PerlinNoise(u * 83 + 12, v * 83 + 19);
            float veins = Mathf.Abs(Mathf.PerlinNoise(u * 22 + 7, v * 22 + 9) - 0.5f);
            return Mathf.Clamp01(broad * 0.57f + fine * 0.43f - (veins < 0.025f ? 0.16f : 0));
        }

        private static Texture2D Texture(string name, int index, bool normal)
        {
            const int size = 512;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            Color tint = new[] { new Color(0.24f, 0.255f, 0.27f), new Color(0.13f, 0.14f, 0.15f),
                new Color(0.31f, 0.30f, 0.28f) }[index];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size, v = (float)y / size;
                    if (normal)
                    {
                        float dx = (Grain(u + 1f / size, v) - Grain(u - 1f / size, v)) * 1.7f;
                        float dy = (Grain(u, v + 1f / size) - Grain(u, v - 1f / size)) * 1.7f;
                        Vector3 n = new Vector3(-dx, -dy, 1).normalized;
                        pixels[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1);
                    }
                    else
                    {
                        float grain = Grain(u, v);
                        pixels[y * size + x] = tint * (0.48f + grain * 0.88f);
                        pixels[y * size + x].a = 1;
                    }
                }
            texture.SetPixels(pixels); texture.Apply();
            string path = AssetRoot + "/Textures/" + name + ".png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 512;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Material Material(string name, Color color, float metallic, float smoothness)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(AssetRoot + "/Materials/" + name + ".mat");
            if (existing != null) return existing;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader missing.");
            var mat = new Material(shader) { name = name };
            mat.SetColor("_BaseColor", color); mat.SetFloat("_Metallic", metallic); mat.SetFloat("_Smoothness", smoothness);
            AssetDatabase.CreateAsset(mat, AssetRoot + "/Materials/" + name + ".mat");
            return mat;
        }

        private static Mesh CreateRockMesh()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(AssetRoot + "/Meshes/FacetedBasalt.asset");
            if (existing != null) return existing;
            float t = (1 + Mathf.Sqrt(5)) / 2;
            Vector3[] points = { new Vector3(-1,t,0), new Vector3(1,t,0), new Vector3(-1,-t,0), new Vector3(1,-t,0),
                new Vector3(0,-1,t), new Vector3(0,1,t), new Vector3(0,-1,-t), new Vector3(0,1,-t),
                new Vector3(t,0,-1), new Vector3(t,0,1), new Vector3(-t,0,-1), new Vector3(-t,0,1) };
            int[] faces = {0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6,
                7,1,8, 3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1 };
            for (int i = 0; i < points.Length; i++) points[i] = points[i].normalized * (0.83f + 0.08f * (i % 4));
            var verts = new Vector3[faces.Length]; var indices = new int[faces.Length];
            for (int i = 0; i < faces.Length; i++) { verts[i] = points[faces[i]]; indices[i] = i; }
            var mesh = new Mesh { name = "Faceted Basalt" }; mesh.vertices = verts; mesh.triangles = indices;
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, AssetRoot + "/Meshes/FacetedBasalt.asset");
            return mesh;
        }

        private static Mesh CreateBentPanelMesh()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(AssetRoot + "/Meshes/BentShipPanel.asset");
            if (existing != null) return existing;
            Vector3[] verts = { new Vector3(-1,0,-0.6f), new Vector3(0,0.06f,-0.6f), new Vector3(1,0.34f,-0.6f),
                new Vector3(-1,0,0.6f), new Vector3(0,0.06f,0.6f), new Vector3(1,0.34f,0.6f),
                new Vector3(-1,-0.07f,-0.6f), new Vector3(0,-0.01f,-0.6f), new Vector3(1,0.27f,-0.6f),
                new Vector3(-1,-0.07f,0.6f), new Vector3(0,-0.01f,0.6f), new Vector3(1,0.27f,0.6f) };
            int[] indices = {0,3,4,0,4,1, 1,4,5,1,5,2, 6,7,10,6,10,9,7,8,11,7,11,10,
                0,1,7,0,7,6,1,2,8,1,8,7,3,9,10,3,10,4,4,10,11,4,11,5,0,6,9,0,9,3,2,5,11,2,11,8};
            var mesh = new Mesh { name = "Bent Ship Panel" }; mesh.vertices = verts; mesh.triangles = indices;
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, AssetRoot + "/Meshes/BentShipPanel.asset"); return mesh;
        }

        private static GameObject MeshPart(string name, Transform parent, Mesh mesh, Material material, bool collision)
        {
            var obj = new GameObject(name); obj.transform.SetParent(parent, false);
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            obj.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (collision) obj.AddComponent<MeshCollider>().sharedMesh = mesh;
            return obj;
        }

        private static bool ClearSpace(Vector2 p, float radius)
        {
            if (p.magnitude < 9 + radius) return false;
            foreach (Vector2 pad in pads) if (Vector2.Distance(p, pad) < 3 + radius) return false;
            foreach (Vector2[] route in routes)
                if (SegmentDistance(p, route[0], route[1]) < 2.8f + radius) return false;
            return true;
        }

        private static float SurfaceHeight(Terrain terrain, Vector2 local)
        {
            return terrain.SampleHeight(new Vector3(center.x + local.x, 0, center.z + local.y)) + terrain.transform.position.y;
        }

        private static void BuildRocks(Transform parent, Terrain terrain, Mesh mesh, Material dark, Material ash)
        {
            var group = new GameObject("Basalt Rocks"); group.transform.SetParent(parent, false);
            var rng = new System.Random(1803); int placed = 0;
            for (int attempt = 0; attempt < 700 && placed < 96; attempt++)
            {
                float angle = (float)rng.NextDouble() * Mathf.PI * 2;
                float radius = Mathf.Lerp(10, 68, Mathf.Sqrt((float)rng.NextDouble()));
                Vector2 p = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                float scale = Mathf.Lerp(0.35f, radius > 32 ? 2.4f : 1.15f, (float)rng.NextDouble());
                if (!ClearSpace(p, scale)) continue;
                GameObject obj = MeshPart("Basalt " + (++placed).ToString("00"), group.transform, mesh,
                    placed % 4 == 0 ? ash : dark, scale > 0.65f);
                obj.transform.localPosition = new Vector3(p.x, SurfaceHeight(terrain, p) + scale * 0.28f, p.y);
                obj.transform.localScale = new Vector3(scale * 1.2f, scale * 0.72f, scale);
                obj.transform.localRotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360, (float)rng.NextDouble() * 16 - 8);
            }
            if (placed < 60) throw new InvalidOperationException("Insufficient rock dressing.");
        }

        private static void BuildDebris(Transform parent, Terrain terrain, Mesh panel, Material metal, Material soot, Material rust)
        {
            var group = new GameObject("Ship Debris"); group.transform.SetParent(parent, false);
            var rng = new System.Random(1804); int placed = 0;
            for (int attempt = 0; attempt < 500 && placed < 30; attempt++)
            {
                float angle = (float)rng.NextDouble() * Mathf.PI * 2;
                float radius = Mathf.Lerp(9, 26, (float)rng.NextDouble());
                Vector2 p = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                if (!ClearSpace(p, 1.5f)) continue;
                bool plate = placed % 3 != 2;
                GameObject obj = plate ? MeshPart("Torn Hull Panel " + placed, group.transform, panel,
                    placed % 3 == 0 ? soot : metal, true) : GameObject.CreatePrimitive(PrimitiveType.Cube);
                if (!plate) { obj.name = "Broken Frame Strut " + placed; obj.transform.SetParent(group.transform, false);
                    obj.GetComponent<Renderer>().sharedMaterial = placed % 2 == 0 ? rust : metal; }
                obj.transform.localPosition = new Vector3(p.x, SurfaceHeight(terrain, p) + 0.12f, p.y);
                obj.transform.localScale = plate ? new Vector3(0.7f + (float)rng.NextDouble(), 1, 0.6f + (float)rng.NextDouble()) :
                    new Vector3(0.18f, 0.22f, 1.7f + (float)rng.NextDouble() * 1.3f);
                obj.transform.localRotation = Quaternion.Euler(-6 + (float)rng.NextDouble() * 12,
                    (float)rng.NextDouble() * 360, -5 + (float)rng.NextDouble() * 10);
                placed++;
            }
            if (placed < 20) throw new InvalidOperationException("Insufficient ship debris.");
        }

        private static void CrashRocket(Transform parent, GameObject rocket, Material hull, Material scorch)
        {
            var pivot = new GameObject("Crashed Ship"); pivot.transform.SetParent(parent, false);
            rocket.transform.SetParent(pivot.transform, false);
            rocket.transform.localPosition = Vector3.zero;
            rocket.transform.localRotation = Quaternion.identity;
            rocket.transform.localScale = Vector3.one;
            Renderer[] renderers = rocket.GetComponentsInChildren<Renderer>();
            bool hasBounds = false; Bounds bounds = default;
            foreach (Renderer renderer in renderers)
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null || filter.sharedMesh.vertexCount == 0) continue;
                if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; } else bounds.Encapsulate(renderer.bounds);
            }
            if (!hasBounds) throw new InvalidOperationException("Rocket has no renderable meshes.");
            Vector3 localCenter = pivot.transform.InverseTransformPoint(bounds.center);
            Vector3 localTip = pivot.transform.InverseTransformPoint(new Vector3(bounds.center.x, bounds.max.y, bounds.center.z));
            rocket.transform.localPosition = -localCenter;
            float scale = 12f / bounds.size.y;
            pivot.transform.localScale = Vector3.one * scale;
            pivot.transform.localRotation = Quaternion.Euler(112, -28, 11);
            Vector3 tipOffset = pivot.transform.localRotation * ((localTip - localCenter) * scale);
            pivot.transform.localPosition = new Vector3(0, -0.70f - tipOffset.y, 0);
            foreach (Renderer renderer in renderers)
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null || filter.sharedMesh.vertexCount == 0) continue;
                var mats = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = renderer.name.IndexOf("Cone", StringComparison.OrdinalIgnoreCase) >= 0 ? scorch : hull;
                renderer.sharedMaterials = mats;
                if (renderer.GetComponent<Collider>() == null) renderer.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
            }
            var tip = new GameObject("BuriedNoseMarker"); tip.transform.SetParent(pivot.transform, false);
            tip.transform.localPosition = localTip - localCenter;
        }

        private static void GroundPreservedObjects(GameObject[] roots, Terrain terrain)
        {
            foreach (GameObject obj in roots)
            {
                bool player = obj.name == "FirstPersonPlayer";
                bool prop = obj.name == "CapybaraAstronaut" || obj.name == "BurntRockBasin" ||
                    obj.name == "AlcoholFlask" || obj.name == "GrayboxExitGate";
                if (!player && !prop) continue;
                CharacterController cc = obj.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                Vector3 p = obj.transform.position;
                p.y = terrain.SampleHeight(p) + terrain.transform.position.y + (player ? 0.10f : 0.01f);
                obj.transform.position = p;
                if (cc != null) cc.enabled = true;
                PrefabUtility.RecordPrefabInstancePropertyModifications(obj.transform);
            }
        }

        private static void ConfigureLighting(GameObject[] roots)
        {
            Light primary = null;
            foreach (GameObject root in roots)
            {
                Light light = root.GetComponent<Light>();
                if (light == null || light.type != LightType.Directional) continue;
                if (primary == null)
                {
                    primary = light;
                    light.color = new Color(0.86f, 0.90f, 1f);
                    light.intensity = 1.35f;
                    light.shadows = LightShadows.Soft;
                    light.transform.rotation = Quaternion.Euler(42, -32, 0);
                }
                else light.enabled = false;
            }
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.31f, 0.35f, 0.42f);
            RenderSettings.ambientEquatorColor = new Color(0.19f, 0.22f, 0.26f);
            RenderSettings.ambientGroundColor = new Color(0.10f, 0.115f, 0.13f);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.19f, 0.22f, 0.25f);
            RenderSettings.fogStartDistance = 85; RenderSettings.fogEndDistance = 160;
            RenderSettings.sun = primary;
        }

        private static void DisableExtraViewpoints(GameObject[] roots)
        {
            // Preserve the extra authoring camera but avoid two active MainCamera/listener pairs.
            if (FindRoot(roots, "FirstPersonPlayer") == null) return;
            foreach (GameObject root in roots)
            {
                Camera camera = root.GetComponent<Camera>();
                if (camera == null) continue;
                camera.enabled = false;
                AudioListener listener = root.GetComponent<AudioListener>();
                if (listener != null) listener.enabled = false;
                root.tag = "Untagged";
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int i = path.LastIndexOf('/'); EnsureFolder(path.Substring(0, i));
            AssetDatabase.CreateFolder(path.Substring(0, i), path.Substring(i + 1));
        }

        [MenuItem("Signal Lost/Tutorial Crash Site/Validate Scene")]
        public static void Validate()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) throw new InvalidOperationException("Open the Tutorial scene first.");
            GameObject environment = FindRoot(scene.GetRootGameObjects(), EnvironmentName);
            if (environment == null) throw new InvalidOperationException("Impact site missing.");
            Terrain terrain = environment.GetComponentInChildren<Terrain>();
            if (terrain == null || terrain.GetComponent<TerrainCollider>().terrainData != terrain.terrainData)
                throw new InvalidOperationException("Terrain or collision data missing.");
            var lines = new List<string>(); float maximumSlope = 0, minimum = float.MaxValue, maximum = float.MinValue;
            for (int z = 0; z <= 100; z++)
                for (int x = 0; x <= 100; x++)
                {
                    float u = x / 100f, v = z / 100f;
                    maximumSlope = Mathf.Max(maximumSlope, terrain.terrainData.GetSteepness(u, v));
                    float h = terrain.terrainData.GetInterpolatedHeight(u, v) + terrain.transform.position.y;
                    minimum = Mathf.Min(minimum, h); maximum = Mathf.Max(maximum, h);
                }
            // The requested traversal redesign intentionally supersedes the original gentle-relief limit.
            bool traversal = environment.transform.Find("Traversal Landscape") != null;
            bool lunar = environment.transform.Find("Lunar Landscape") != null;
            if (maximum < 4 || maximum > (lunar ? 24 : traversal ? 60 : 8)) throw new InvalidOperationException("Crater relief out of range: " + maximum);
            int missing = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject);
            if (missing > 0) throw new InvalidOperationException("Missing scene scripts.");
            var player = UnityEngine.Object.FindAnyObjectByType<FirstPersonController>();
            if (player == null) throw new InvalidOperationException("Player missing.");
            var serialized = new SerializedObject(player);
            if (serialized.FindProperty("inputActions").objectReferenceValue == null ||
                serialized.FindProperty("cameraPivot").objectReferenceValue == null)
                throw new InvalidOperationException("Player input/camera reference missing.");
            Transform ship = environment.transform.Find("Crashed Ship") ??
                environment.transform.Find("Enhanced Crashed Rocket/Crashed Ship");
            if (ship == null || ship.GetComponentsInChildren<MeshCollider>().Length == 0)
                throw new InvalidOperationException("Crashed ship collision missing.");
            Vector3 nose = ship.Find("BuriedNoseMarker").position;
            if (nose.y > terrain.SampleHeight(nose) + terrain.transform.position.y)
                throw new InvalidOperationException("Ship nose is not buried.");
            if (FindRoot(scene.GetRootGameObjects(), "graybox") != null)
                throw new InvalidOperationException("Old graybox still present.");
            lines.Add("Terrain: " + terrain.terrainData.size.x.ToString("F0") + "m square, " +
                (lunar ? "rolling lunar layout" : traversal ? "traversal layout" : "broad ~" + (86f * terrain.terrainData.size.x / 160f).ToString("F0") + "m crater") + ", maximum slope " + maximumSlope.ToString("F2") +
                " degrees, elevation " + minimum.ToString("F2") + ".." + maximum.ToString("F2") + "m.");
            lines.Add("Crashed ship nose: " + nose + "; mesh colliders=" + ship.GetComponentsInChildren<MeshCollider>().Length);
            lines.Add("Player input/camera references and scene scripts validated. Existing tutorial props retained.");
            lines.Add(lunar || traversal ? "Authorized relief redesign; the core crash floor is retained." : "Existing author-approved terrain relief retained without regeneration; rim slope is informational.");
            lines.Add("Graybox removed; terrain collider and all generated assets referenced.");
            Directory.CreateDirectory("Logs"); File.WriteAllLines("Logs/TutorialImpactSiteValidation.txt", lines);
            Debug.Log("TUTORIAL_IMPACT_SITE_VALIDATED\n" + string.Join("\n", lines));
        }
    }
}
