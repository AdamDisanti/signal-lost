using SignalLost.Combat;
using SignalLost.Effects;
using UnityEngine;

namespace SignalLost.Enemies.Desert
{
    /// <summary>
    /// A glob of molten core slag the Coreworm spits in a high arc. A ring marks where it will land; it bursts there
    /// (a small blast) and leaves a patch of fire.
    /// </summary>
    public sealed class CoreSlag : MonoBehaviour
    {
        private Vector3 from, to;
        private float flight, age, height, radius, damage;
        private FireStyle fire;
        private GameObject owner;
        private Material blastMaterial;
        private LayerMask mask;
        private GroundMarker marker;
        private FireEffect trail;

        public static CoreSlag Launch(Material slag, Material markerMaterial, Material blastMaterial, FireStyle fire, Vector3 from,
            Vector3 ground, float flightTime, float radius, float damage, GameObject owner, LayerMask mask)
        {
            GameObject body = MiningLaser.VisualPrimitive(PrimitiveType.Sphere, "Core Slag", slag);
            body.transform.position = from;
            body.transform.localScale = Vector3.one * 0.6f;
            var glob = body.AddComponent<CoreSlag>();
            glob.from = from;
            glob.to = ground;
            glob.flight = Mathf.Max(0.3f, flightTime);
            glob.height = 4f + Vector3.Distance(from, ground) * 0.2f;
            glob.radius = radius;
            glob.damage = damage;
            glob.fire = fire;
            glob.owner = owner;
            glob.blastMaterial = blastMaterial;
            glob.mask = mask;
            glob.marker = GroundMarker.Spawn(markerMaterial, null, ground, radius);
            glob.trail = FireEffect.SpawnJet(body.transform, Vector3.zero, Quaternion.identity, 0.22f, 1.2f);
            return glob;
        }

        private void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / flight);
            Vector3 p = Vector3.Lerp(from, to, t);
            p.y += Mathf.Sin(t * Mathf.PI) * height;
            Vector3 travel = p - transform.position;
            transform.position = p;
            if (trail != null && travel.sqrMagnitude > 0.0001f) trail.transform.rotation = Quaternion.LookRotation(-travel);
            if (marker != null) marker.Progress = t;
            if (t < 1f) return;

            Transform ignore = owner != null ? owner.transform : null;
            FodderExplosion.Explode(to, radius, damage, 0.4f, owner, ignore, blastMaterial, mask);
            ParticleBurst.Explosion(to + Vector3.up * 0.3f, radius * 0.5f);
            FireZone.Spawn(fire, to, radius * 0.75f, owner, ignore);
            CameraShake.Add(0.08f);
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (marker != null) Destroy(marker.gameObject);
            if (trail != null)
            {
                trail.transform.SetParent(null, true);
                trail.Extinguish();
                Destroy(trail.gameObject, 2.5f);
            }
        }
    }
}
