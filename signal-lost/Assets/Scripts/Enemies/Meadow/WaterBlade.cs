using SignalLost.Combat;
using SignalLost.Effects;
using SignalLost.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace SignalLost.Enemies.Meadow
{
    /// <summary>
    /// A crescent of high-pressure water the Chief Toad slings: it flies flat and fast, trailing spray (and kicking up
    /// a wake when it skims the ground or the pond), and bursts on the first solid thing it meets. It hurts the player
    /// and shoves them; it passes through other creatures (enemies never hurt each other), and cover stops it.
    /// Made entirely in code, so it works anywhere, in or out of the water.
    /// </summary>
    public sealed class WaterBlade : MonoBehaviour
    {
        private const float Radius = 0.5f;
        private const float Lifetime = 2.2f;

        private static Mesh crescent;

        private Vector3 velocity;
        private float damage, age, nextSpray;
        private GameObject owner;
        private Transform shape;

        /// <param name="material">The water look for the crescent (the toad's water material); null uses a soft glow.</param>
        /// <param name="width">Width of the crescent, tip to tip, in metres.</param>
        public static WaterBlade Launch(Material material, Vector3 from, Vector3 direction, float speed, float damage, GameObject owner, float width = 1.8f)
        {
            if (direction.sqrMagnitude < 0.0001f) direction = Vector3.forward;
            direction.Normalize();
            var go = new GameObject("Water Blade");
            go.transform.SetPositionAndRotation(from, Quaternion.LookRotation(direction));
            var blade = go.AddComponent<WaterBlade>();
            blade.velocity = direction * speed;
            blade.damage = damage;
            blade.owner = owner;

            var visual = new GameObject("Crescent");
            visual.transform.SetParent(go.transform, false);
            visual.transform.localScale = new Vector3(width / 1.73f, 1f, width / 1.73f);
            visual.AddComponent<MeshFilter>().sharedMesh = Crescent();
            var renderer = visual.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material != null ? material : EffectMaterials.AlphaBlended(EffectMaterials.Look.Glow);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            blade.shape = visual.transform;

            var trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = EffectMaterials.AlphaBlended(EffectMaterials.Look.Beam);
            trail.time = 0.22f;
            trail.startWidth = width * 0.7f;
            trail.endWidth = 0.05f;
            trail.startColor = new Color(0.75f, 0.93f, 1f, 0.55f);
            trail.endColor = new Color(0.5f, 0.8f, 1f, 0f);
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            ParticleBurst.Spawn(ParticleBurst.Kind.Mist, from, direction, 3, 0.7f, 2f, 30f);
            return blade;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            if (age >= Lifetime)
            {
                Burst(transform.position, 8);
                return;
            }

            Vector3 step = velocity * dt;
            float distance = step.magnitude;
            if (distance > 0.0001f)
            {
                RaycastHit[] hits = Physics.SphereCastAll(transform.position, Radius, step / distance, distance, ~0, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                foreach (RaycastHit hit in hits)
                {
                    GameObject other = hit.collider.gameObject;
                    if (owner != null && other.transform.IsChildOf(owner.transform)) continue;
                    if (EnemyAllegiance.IsEnemy(other)) continue; // sails through the toad's fellow creatures
                    if (hit.collider.GetComponentInParent<FirstPersonController>() != null)
                    {
                        Health health = hit.collider.GetComponentInParent<Health>();
                        if (health != null && EnemyAllegiance.CanHurt(owner, health)) health.TakeDamage(damage, owner);
                        var body = hit.collider.GetComponentInParent<CharacterController>();
                        Vector3 push = velocity;
                        push.y = 0f;
                        if (body != null && body.enabled && push.sqrMagnitude > 0.01f) body.Move(push.normalized * 0.7f);
                        CameraShake.Add(0.2f);
                        Burst(hit.point == Vector3.zero ? transform.position : hit.point, 22);
                        return;
                    }
                    if (hit.collider.GetComponentInParent<Health>() != null) continue; // props with health (dummies) don't stop it
                    Burst(hit.point == Vector3.zero ? transform.position : hit.point, 22);
                    return;
                }
            }
            transform.position += step;

            // A gentle wobble, and spray peeling off its tips.
            if (shape != null) shape.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(age * 30f) * 6f);
            if (Time.time < nextSpray) return;
            nextSpray = Time.time + 0.05f;
            Vector3 side = transform.right * Random.Range(-0.8f, 0.8f);
            ParticleBurst.Spawn(ParticleBurst.Kind.Water, transform.position + side, -velocity + Vector3.up * 3f, 2, 0.14f, 3f, 35f);
            // Skimming low over the ground or the water: a wake.
            if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit below, 1.6f, ~0, QueryTriggerInteraction.Ignore))
                ParticleBurst.Spawn(ParticleBurst.Kind.Mist, below.point, Vector3.up, 1, 0.8f, 1.5f, 40f);
        }

        private void Burst(Vector3 at, int droplets)
        {
            ParticleBurst.Spawn(ParticleBurst.Kind.Water, at, Vector3.up - velocity.normalized * 0.6f, droplets, 0.18f, 6f, 65f);
            ParticleBurst.Spawn(ParticleBurst.Kind.Mist, at, Vector3.up, 4, 1f, 1.8f, 50f);
            Destroy(gameObject);
        }

        // A flat, slightly thick crescent in XZ, its curve bulging toward +Z, about 1.73 m tip to tip at scale 1.
        private static Mesh Crescent()
        {
            if (crescent != null) return crescent;
            const int segments = 12;
            var vertices = new System.Collections.Generic.List<Vector3>();
            var triangles = new System.Collections.Generic.List<int>();
            // Wound so the quad faces the given way, whatever order the corners come in.
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 facing)
            {
                int i = vertices.Count;
                vertices.AddRange(new[] { a, b, c, d });
                bool forward = Vector3.Dot(Vector3.Cross(b - a, c - a), facing) >= 0f;
                triangles.AddRange(forward ? new[] { i, i + 1, i + 2, i, i + 2, i + 3 } : new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
            }
            var outer = new Vector3[segments + 1];
            var inner = new Vector3[segments + 1];
            for (int s = 0; s <= segments; s++)
            {
                float t = s / (float)segments;
                float angle = Mathf.Lerp(-60f, 60f, t) * Mathf.Deg2Rad;
                float thickness = Mathf.Lerp(0.03f, 0.3f, Mathf.Sin(t * Mathf.PI));
                outer[s] = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle) - 0.75f);
                inner[s] = new Vector3(Mathf.Sin(angle) * (1f - thickness), 0f, Mathf.Cos(angle) * (1f - thickness) - 0.75f);
            }
            Vector3 up = Vector3.up * 0.04f;
            for (int s = 0; s < segments; s++)
            {
                Vector3 bulge = (outer[s] + outer[s + 1]) * 0.5f - (inner[s] + inner[s + 1]) * 0.5f;
                Quad(inner[s] + up, inner[s + 1] + up, outer[s + 1] + up, outer[s] + up, Vector3.up);
                Quad(inner[s] - up, inner[s + 1] - up, outer[s + 1] - up, outer[s] - up, Vector3.down);
                Quad(outer[s] + up, outer[s + 1] + up, outer[s + 1] - up, outer[s] - up, bulge);    // leading edge
                Quad(inner[s] + up, inner[s + 1] + up, inner[s + 1] - up, inner[s] - up, -bulge);   // trailing edge
            }
            crescent = new Mesh { name = "Water Blade" };
            crescent.SetVertices(vertices);
            crescent.SetTriangles(triangles, 0);
            crescent.RecalculateNormals();
            crescent.RecalculateBounds();
            return crescent;
        }
    }
}
