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
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();
        [SerializeField, Min(10f)] private float chunkSize = 60f;

        private Transform generated;
        private readonly List<Mesh> built = new List<Mesh>();

        public void Place(MudModels.Kind kind, int variant, Vector3 position, float yaw, float scale) =>
            entries.Add(new Entry { kind = kind, variant = variant, position = position, yaw = yaw, scale = scale });

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
