using SignalLost.Combat;
using UnityEngine;

namespace SignalLost.Enemies.Desert
{
    /// <summary>
    /// A blob of fuel Sludge sprays in a high arc. It does no damage itself; where it lands it leaves an
    /// <see cref="OilSlick"/>. A pulsing ring marks the landing spot.
    /// </summary>
    public sealed class OilGlob : MonoBehaviour
    {
        private Vector3 from, to;
        private float flight, age, height, slickRadius, slickLife, slow;
        private Material oil;
        private FireStyle fire;
        private GameObject owner;
        private GroundMarker marker;

        public static OilGlob Launch(Material oil, Material markerMaterial, FireStyle fire, Vector3 from, Vector3 ground, float flightTime,
            float slickRadius, float slickLife, float slow, GameObject owner)
        {
            GameObject body = MiningLaser.VisualPrimitive(PrimitiveType.Sphere, "Oil Glob", oil);
            body.transform.position = from;
            body.transform.localScale = Vector3.one * 0.35f;
            var glob = body.AddComponent<OilGlob>();
            glob.from = from;
            glob.to = ground;
            glob.flight = Mathf.Max(0.2f, flightTime);
            glob.height = 2.5f + Vector3.Distance(from, ground) * 0.15f;
            glob.oil = oil;
            glob.fire = fire;
            glob.slickRadius = slickRadius;
            glob.slickLife = slickLife;
            glob.slow = slow;
            glob.owner = owner;
            glob.marker = GroundMarker.Spawn(markerMaterial, null, ground, slickRadius * 0.8f);
            return glob;
        }

        private void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / flight);
            Vector3 p = Vector3.Lerp(from, to, t);
            p.y += Mathf.Sin(t * Mathf.PI) * height;
            transform.position = p;
            transform.localScale = new Vector3(0.35f, 0.35f * (1f + 0.3f * Mathf.Sin(Time.time * 25f)), 0.35f);
            if (marker != null) marker.Progress = t;
            if (t < 1f) return;

            OilSlick.Spawn(oil, fire, to, slickRadius, slickLife, slow, owner);
            if (marker != null) Destroy(marker.gameObject);
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (marker != null) Destroy(marker.gameObject);
        }
    }
}
