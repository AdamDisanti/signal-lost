using UnityEngine;
using UnityEngine.Rendering;

namespace SignalLost.Effects
{
    /// <summary>
    /// A flat ring that grows outward and thins away: the beetle blast wave and the meerkat's shriek.
    /// Placeholder effect built from a LineRenderer.
    /// </summary>
    public sealed class ExpandingRing : MonoBehaviour
    {
        private const int Points = 32;

        private LineRenderer line;
        private float radius, lifetime, width, age;
        private bool vertical;

        /// <param name="vertical">Stand the ring up facing the camera-ish direction (shriek) instead of lying flat.</param>
        public static ExpandingRing Spawn(Material material, Vector3 position, float radius, float lifetime, float width,
            bool vertical = false, Quaternion facing = default)
        {
            var go = new GameObject(vertical ? "Shriek Ring" : "Blast Ring");
            go.transform.position = position;
            if (vertical) go.transform.rotation = facing;
            var ring = go.AddComponent<ExpandingRing>();
            ring.line = go.AddComponent<LineRenderer>();
            ring.line.sharedMaterial = material;
            ring.line.useWorldSpace = false;
            ring.line.loop = true;
            ring.line.positionCount = Points;
            ring.line.shadowCastingMode = ShadowCastingMode.Off;
            ring.line.receiveShadows = false;
            ring.radius = radius;
            ring.lifetime = Mathf.Max(0.05f, lifetime);
            ring.width = width;
            ring.vertical = vertical;
            ring.Draw(0f);
            return ring;
        }

        private void Update()
        {
            age += Time.deltaTime;
            float k = age / lifetime;
            if (k >= 1f)
            {
                Destroy(gameObject);
                return;
            }
            Draw(k);
        }

        private void Draw(float k)
        {
            float r = radius * (1f - (1f - k) * (1f - k)); // fast out, slowing down
            for (int i = 0; i < Points; i++)
            {
                float a = i * Mathf.PI * 2f / Points;
                line.SetPosition(i, vertical
                    ? new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f)
                    : new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
            }
            line.startWidth = line.endWidth = width * (1f - k);
        }
    }
}
