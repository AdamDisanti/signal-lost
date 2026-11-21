using System;
using System.Collections.Generic;
using SignalLost.Effects;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace SignalLost.Mudlands
{
    /// <summary>
    /// Lots of scenery (trees, rocks, cattails, lily pads, mushrooms...) drawn as a few combined meshes per area, built
    /// from code on enable (in the editor too) and never saved: the same idea as the meadow's FoliageScatter. The level
    /// builder fills <see cref="Place"/> entries; colliders for trees and rocks are separate saved objects.
    /// </summary>
    [ExecuteAlways]
    public sealed class MudScenery : MonoBehaviour
    {
        [Serializable]
        public struct Entry
        {
            public MudModels.Kind kind;
            public int variant;
            public Vector3 position;
            public float yaw, scale;
            // The ground's slope under it (11-20): metres of rise per metre along world x and z. The bottom of the model
            // (a tree's flared base and knees, a rock, a log) is draped onto that slope instead of standing level on it,
            // so nothing hangs in the air on a bank; the trunk above stays upright. Zero: level ground.
            public Vector2 slope;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();
        [SerializeField, Min(10f)] private float chunkSize = 60f;

        private Transform generated;
        private readonly List<Mesh> built = new List<Mesh>();

        public void Place(MudModels.Kind kind, int variant, Vector3 position, float yaw, float scale, Vector2 slope = default) =>
            entries.Add(new Entry { kind = kind, variant = variant, position = position, yaw = yaw, scale = scale, slope = slope });

        public void Clear() => entries.Clear();

        private void OnEnable() => Generate();
        private void OnDisable() => Release();

        [ContextMenu("Regenerate")]
        public void Generate()
        {
            Release();
            generated = new GameObject("Generated Scenery") { hideFlags = HideFlags.DontSave }.transform;
            generated.SetParent(transform, false);
            var chunks = new Dictionary<(Vector2Int, Material), List<CombineInstance>>();
            foreach (Entry entry in entries)
            {
                (Mesh mesh, Material[] materials) = MudArt.Cached($"scenery_{entry.kind}_{entry.variant % 4}", k => MudModels.Build(k, entry.kind, entry.variant % 4));
                if (entry.slope.sqrMagnitude > 1e-6f) mesh = Draped(mesh, entry);
                Matrix4x4 pose = transform.worldToLocalMatrix * Matrix4x4.TRS(entry.position, Quaternion.Euler(0f, entry.yaw, 0f), Vector3.one * (entry.scale <= 0f ? 1f : entry.scale));
                var cell = new Vector2Int(Mathf.FloorToInt(entry.position.x / chunkSize), Mathf.FloorToInt(entry.position.z / chunkSize));
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    var key = (cell, materials[sub]);
                    if (!chunks.TryGetValue(key, out List<CombineInstance> list)) chunks[key] = list = new List<CombineInstance>();
                    list.Add(new CombineInstance { mesh = mesh, subMeshIndex = sub, transform = pose });
                }
            }
            foreach (var pair in chunks)
            {
                var mesh = new Mesh { name = "Scenery " + pair.Key.Item1, indexFormat = IndexFormat.UInt32, hideFlags = HideFlags.DontSave };
                mesh.CombineMeshes(pair.Value.ToArray(), true, true);
                built.Add(mesh);
                var chunk = new GameObject("Chunk " + pair.Key.Item1) { hideFlags = HideFlags.DontSave };
                chunk.transform.SetParent(generated, false);
                chunk.AddComponent<MeshFilter>().sharedMesh = mesh;
                chunk.AddComponent<MeshRenderer>().sharedMaterial = pair.Key.Item2;
            }
        }

        // How high (model metres) the drape is full and where it has faded out: a tree's flare and knees follow the slope
        // and its trunk stays upright; rocks, stumps and logs follow it whole.
        private static (float full, float none) DrapeBand(MudModels.Kind kind) => kind switch
        {
            MudModels.Kind.Cypress => (0.95f, 2.6f),
            MudModels.Kind.Willow => (0.6f, 2.4f),
            MudModels.Kind.DeadTree => (0.5f, 2f),
            _ => (3f, 4f)
        };

        // A copy of the model with its bottom moved up or down with the ground's slope (by each vertex's position
        // across the slope), fading to unchanged by DrapeBand's top.
        private Mesh Draped(Mesh source, Entry entry)
        {
            Vector3[] vertices = source.vertices;
            (float full, float none) = DrapeBand(entry.kind);
            float scale = entry.scale <= 0f ? 1f : entry.scale;
            Quaternion yaw = Quaternion.Euler(0f, entry.yaw, 0f);
            for (int i = 0; i < vertices.Length; i++)
            {
                float y = vertices[i].y;
                float weight = y <= full ? 1f : y >= none ? 0f : 1f - Mathf.SmoothStep(0f, 1f, (y - full) / (none - full));
                if (weight <= 0f) continue;
                Vector3 world = yaw * vertices[i] * scale;
                vertices[i].y += weight * (entry.slope.x * world.x + entry.slope.y * world.z) / scale;
            }
            Mesh draped = Instantiate(source);
            draped.hideFlags = HideFlags.DontSave;
            draped.vertices = vertices;
            draped.RecalculateBounds();
            built.Add(draped);
            return draped;
        }

        private void Release()
        {
            if (generated != null)
            {
                if (Application.isPlaying) Destroy(generated.gameObject);
                else DestroyImmediate(generated.gameObject);
            }
            generated = null;
            foreach (Mesh mesh in built)
            {
                if (mesh == null) continue;
                if (Application.isPlaying) Destroy(mesh);
                else DestroyImmediate(mesh);
            }
            built.Clear();
        }
    }
}
