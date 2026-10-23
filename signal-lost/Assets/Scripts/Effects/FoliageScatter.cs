using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SignalLost.Effects
{
    /// <summary>
    /// Covers the ground with thousands of small, collider-free plants (grass tufts, flowers) without saving them in
    /// the scene: on enable it scatters the <see cref="items"/> deterministically from <see cref="seed"/> across a
    /// circle, drops each onto whatever collider is below, and combines them into a few meshes per area chunk.
    /// Works in the editor too (generated objects are not saved), so level designers see the result.
    /// Keep plants out of places with child objects named "Clear" (a sphere collider's radius sets the size).
    /// </summary>
    [ExecuteAlways]
    public sealed class FoliageScatter : MonoBehaviour
    {
        [Serializable]
        public struct Item
        {
            public Mesh mesh;
            public Material[] materials;
            [Min(0)] public int count;
            public Vector2 scale;
        }

        [Tooltip("Plants only grow on this collider (the terrain). Empty: whatever is hit first.")]
        [SerializeField] private Collider ground;
        [SerializeField] private Item[] items = Array.Empty<Item>();
        [SerializeField, Min(1f)] private float radius = 110f;
        [SerializeField, Min(4f)] private float chunkSize = 40f;
        [SerializeField] private int seed = 20261022;
        [Tooltip("Only places plants on surfaces flatter than this (degrees).")]
        [SerializeField, Range(0f, 90f)] private float maxSlope = 32f;

        private Transform generated;
        private readonly List<Mesh> meshes = new List<Mesh>();

        private void OnEnable() => Generate();

        private void OnDisable() => Clear();

        /// <summary>Rebuilds the scattered plants (for example after editing the ground).</summary>
        [ContextMenu("Regenerate")]
        public void Generate()
        {
            Clear();
            generated = new GameObject("Generated Foliage") { hideFlags = HideFlags.DontSave }.transform;
            generated.SetParent(transform, false);
            var clear = new List<(Vector3, float)>();
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
                if (child.name.StartsWith("Clear", StringComparison.Ordinal))
                {
                    var sphere = child.GetComponent<SphereCollider>();
                    clear.Add((child.position, sphere != null ? sphere.radius * child.lossyScale.x : 5f));
                    if (sphere != null) sphere.enabled = false; // markers only, never solid
                }

            var random = new System.Random(seed);
            var chunks = new Dictionary<(Vector2Int, Material), List<CombineInstance>>();
            float cosMax = Mathf.Cos(maxSlope * Mathf.Deg2Rad);
            foreach (Item item in items)
            {
                if (item.mesh == null || item.materials == null) continue;
                for (int i = 0; i < item.count; i++)
                {
                    float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                    float distance = Mathf.Sqrt((float)random.NextDouble()) * radius;
                    Vector3 point = transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
                    float scale = Mathf.Lerp(item.scale.x <= 0f ? 1f : item.scale.x, item.scale.y <= 0f ? 1f : item.scale.y, (float)random.NextDouble());
                    float yaw = (float)random.NextDouble() * 360f;
                    var ray = new Ray(point + Vector3.up * 200f, Vector3.down);
                    RaycastHit hit;
                    bool found = ground != null ? ground.Raycast(ray, out hit, 400f) : Physics.Raycast(ray, out hit, 400f, ~0, QueryTriggerInteraction.Ignore);
                    if (!found) continue;
                    if (hit.normal.y < cosMax || IsCleared(hit.point, clear)) continue;
                    Matrix4x4 pose = Matrix4x4.TRS(hit.point, Quaternion.FromToRotation(Vector3.up, hit.normal) * Quaternion.Euler(0f, yaw, 0f), Vector3.one * scale);
                    var cell = new Vector2Int(Mathf.FloorToInt(hit.point.x / chunkSize), Mathf.FloorToInt(hit.point.z / chunkSize));
                    for (int sub = 0; sub < item.mesh.subMeshCount && sub < item.materials.Length; sub++)
                    {
                        var key = (cell, item.materials[sub]);
                        if (!chunks.TryGetValue(key, out List<CombineInstance> list)) chunks[key] = list = new List<CombineInstance>();
                        list.Add(new CombineInstance { mesh = item.mesh, subMeshIndex = sub, transform = transform.worldToLocalMatrix * pose });
                    }
                }
            }

            foreach (var pair in chunks)
            {
                var mesh = new Mesh { name = "Foliage " + pair.Key.Item1, indexFormat = IndexFormat.UInt32, hideFlags = HideFlags.DontSave };
                mesh.CombineMeshes(pair.Value.ToArray(), true, true);
                meshes.Add(mesh);
                var chunk = new GameObject(pair.Key.Item2.name + " " + pair.Key.Item1) { hideFlags = HideFlags.DontSave };
                chunk.transform.SetParent(generated, false);
                chunk.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = chunk.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = pair.Key.Item2;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        private static bool IsCleared(Vector3 point, List<(Vector3 centre, float radius)> clear)
        {
            foreach ((Vector3 centre, float r) in clear)
            {
                Vector3 offset = point - centre;
                offset.y = 0f;
                if (offset.sqrMagnitude < r * r) return true;
            }
            return false;
        }

        private void Clear()
        {
            if (generated != null)
            {
                if (Application.isPlaying) Destroy(generated.gameObject);
                else DestroyImmediate(generated.gameObject);
            }
            foreach (Mesh mesh in meshes)
            {
                if (Application.isPlaying) Destroy(mesh);
                else DestroyImmediate(mesh);
            }
            meshes.Clear();
            generated = null;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.5f, 0.9f, 0.3f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
