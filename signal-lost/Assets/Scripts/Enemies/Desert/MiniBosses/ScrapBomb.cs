using SignalLost.Combat;
using UnityEngine;

namespace SignalLost.Enemies.Desert
{
    /// <summary>
    /// A chunk of hull scrap Old Buzzard drops on its bombing run. It falls straight down, tumbling, onto a ring that
    /// marks where it will land, and bursts on impact, hurting everything in a small radius (fodder too) and setting off
    /// anything <see cref="IIgnitable"/>.
    /// </summary>
    public sealed class ScrapBomb : MonoBehaviour
    {
        private const float Gravity = 22f;

        private Vector3 velocity, landing, spin;
        private float radius, damage;
        private GameObject source;
        private Transform ignore;
        private Material blast;
        private LayerMask mask;
        private GroundMarker marker;

        public static ScrapBomb Drop(Material scrap, Material markerMaterial, Material blastMaterial, Vector3 from, Vector3 ground,
            float radius, float damage, GameObject source, Transform ignore, LayerMask mask)
        {
            GameObject body = MiningLaser.VisualPrimitive(PrimitiveType.Cube, "Scrap Bomb", scrap);
            body.transform.SetPositionAndRotation(from, Random.rotation);
            body.transform.localScale = new Vector3(0.55f, 0.12f, 0.4f);
            var bomb = body.AddComponent<ScrapBomb>();
            bomb.landing = ground;
            bomb.radius = radius;
            bomb.damage = damage;
            bomb.source = source;
            bomb.ignore = ignore;
            bomb.blast = blastMaterial;
            bomb.mask = mask;
            bomb.spin = Random.onUnitSphere * 540f;
            bomb.marker = GroundMarker.Spawn(markerMaterial, null, ground, radius);
            return bomb;
        }

        private void Update()
        {
            velocity += Vector3.down * Gravity * Time.deltaTime;
            transform.position += velocity * Time.deltaTime;
            transform.Rotate(spin * Time.deltaTime, Space.World);
            if (marker != null)
            {
                float fall = Mathf.Max(0.01f, transform.position.y - landing.y);
                marker.Progress = 1f - Mathf.Clamp01(fall / 8f);
            }
            if (transform.position.y > landing.y + 0.1f) return;

            FodderExplosion.Explode(landing, radius, damage, 0.4f, source, ignore, blast, mask);
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (marker != null) Destroy(marker.gameObject);
        }
    }
}
