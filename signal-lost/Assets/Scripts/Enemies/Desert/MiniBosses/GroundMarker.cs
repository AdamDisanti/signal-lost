using SignalLost.Combat;
using UnityEngine;
using UnityEngine.Rendering;

namespace SignalLost.Enemies.Desert
{
    /// <summary>
    /// A warning on the ground where something is about to land: a ring that pulses faster as <see cref="Progress"/>
    /// goes from 0 to 1, with an optional dark "shadow" disc that grows (Old Buzzard's dive). The owner moves it,
    /// sets Progress and destroys it. Placeholder visuals.
    /// </summary>
    public sealed class GroundMarker : MonoBehaviour
    {
        private const int Points = 32;

        private LineRenderer ring;
        private Transform shadow;
        private float radius;

        /// <summary>0 when the warning starts, 1 at impact.</summary>
        public float Progress { get; set; }

        public static GroundMarker Spawn(Material ringMaterial, Material shadowMaterial, Vector3 point, float radius)
        {
            var go = new GameObject("Ground Marker");
            go.transform.position = point + Vector3.up * 0.04f;
            var marker = go.AddComponent<GroundMarker>();
            marker.radius = radius;
            marker.ring = go.AddComponent<LineRenderer>();
            marker.ring.sharedMaterial = ringMaterial;
            marker.ring.useWorldSpace = false;
            marker.ring.loop = true;
            marker.ring.positionCount = Points;
            marker.ring.shadowCastingMode = ShadowCastingMode.Off;
            marker.ring.receiveShadows = false;
            for (int i = 0; i < Points; i++)
            {
                float a = i * Mathf.PI * 2f / Points;
                marker.ring.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
            }
            if (shadowMaterial != null)
            {
                GameObject disc = MiningLaser.VisualPrimitive(PrimitiveType.Cylinder, "Shadow", shadowMaterial);
                marker.shadow = disc.transform;
                marker.shadow.SetParent(go.transform, false);
                marker.shadow.localScale = new Vector3(0.1f, 0.005f, 0.1f);
            }
            marker.Update();
            return marker;
        }

        /// <summary>Moves the marker to a new spot on the ground (a target that is still tracking the player).</summary>
        public void MoveTo(Vector3 point) => transform.position = point + Vector3.up * 0.04f;

        private void Update()
        {
            float p = Mathf.Clamp01(Progress);
            float pulse = 1f + 0.12f * Mathf.Sin(Time.time * Mathf.Lerp(8f, 30f, p));
            transform.localScale = new Vector3(pulse, 1f, pulse);
            ring.startWidth = ring.endWidth = Mathf.Lerp(0.05f, 0.12f, p);
            if (shadow != null)
            {
                float d = radius * 2f * Mathf.Lerp(0.15f, 1f, p);
                shadow.localScale = new Vector3(d, 0.005f, d);
            }
        }
    }
}
