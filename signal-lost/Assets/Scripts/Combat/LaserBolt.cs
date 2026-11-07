using System;
using System.Collections.Generic;
using SignalLost.Effects;
using UnityEngine;
using UnityEngine.Rendering;

namespace SignalLost.Combat
{
    /// <summary>
    /// The mining laser's charged shot: a glowing bolt that flies straight, damages the first thing with
    /// Health it touches, and disappears on any hit or after travelling its range. It moves by sphere-casting
    /// each frame instead of using physics, so fast bolts cannot pass through thin walls. Something with an
    /// <see cref="IBoltReflector"/> can bounce it back instead; the bounced bolt can then hit whoever fired it.
    /// A bolt given a blast (<see cref="SetBlast"/>, the full-charge shot) also bursts on impact (<see cref="AreaBlast"/>).
    /// Looks: a white-hot stretched core in a camera-facing glow, a two-layer fading streak, a small light, and sparks
    /// shed along the way; on impact a flash, a spray of sparks off the surface and a brief burst of light.
    /// </summary>
    public sealed class LaserBolt : MonoBehaviour
    {
        private static readonly RaycastHit[] Hits = new RaycastHit[16];
        private static readonly Dictionary<Material, Material> Cores = new Dictionary<Material, Material>();
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private Material material;
        private Color color;
        private Transform halo;
        private TrailRenderer glowTrail, coreTrail;
        private Light glow;
        private float shedDistance;

        private Transform owner;
        private Action<Health> onHit;
        private Vector3 direction, previous;
        private float damage, speed, radius, remaining, range;
        private int reflections;
        private float blastRadius, blastEdge, blastKnockback;

        private const int MaxReflections = 3; // stops bolts bouncing between two reflectors forever
        private LayerMask mask;

        /// <summary>
        /// Creates and launches a bolt. <paramref name="castFrom"/> is where collision checks begin on the first
        /// frame (the camera), so a bolt fired with the tool tip inside a wall still hits that wall.
        /// </summary>
        public static LaserBolt Spawn(Material material, Vector3 position, Vector3 direction, Vector3 castFrom,
            float damage, float speed, float radius, float range, LayerMask mask, Transform owner, Action<Health> onHit)
        {
            Color color = Tint(material);

            // A white-hot core stretched along its flight, inside a soft glow, trailing a fading streak.
            var body = new GameObject("Laser Bolt");
            body.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction));
            GameObject core = MiningLaser.VisualPrimitive(PrimitiveType.Sphere, "Core", CoreMaterial(material, color));
            core.transform.SetParent(body.transform, false);
            core.transform.localScale = new Vector3(radius * 1.3f, radius * 1.3f, radius * 3.4f);

            var bolt = body.AddComponent<LaserBolt>();
            bolt.material = material;
            bolt.color = color;
            bolt.halo = Halo(body.transform, color, radius);
            bolt.glowTrail = Trail("Glow Trail", body.transform, EffectMaterials.Additive(EffectMaterials.Look.Beam),
                radius * 3.2f, 0.16f, color, 0.85f);
            bolt.coreTrail = Trail("Core Trail", body.transform, EffectMaterials.Additive(EffectMaterials.Look.Beam),
                radius * 1.1f, 0.07f, Color.Lerp(color, Color.white, 0.75f), 1f);

            var lightObject = new GameObject("Bolt Light");
            lightObject.transform.SetParent(body.transform, false);
            bolt.glow = lightObject.AddComponent<Light>();
            bolt.glow.type = LightType.Point;
            bolt.glow.color = color;
            bolt.glow.range = 2.5f + radius * 12f;
            bolt.glow.intensity = 1.5f + radius * 10f;
            bolt.glow.shadows = LightShadows.None;

            bolt.owner = owner;
            bolt.onHit = onHit;
            bolt.direction = direction.normalized;
            bolt.previous = castFrom;
            bolt.damage = damage;
            bolt.speed = speed;
            bolt.radius = radius;
            bolt.remaining = range;
            bolt.range = range;
            bolt.mask = mask;
            return bolt;
        }

        /// <summary>Makes the bolt burst on impact, hurting everything within <paramref name="radius"/>.</summary>
        public void SetBlast(float radius, float edgeMultiplier, float knockback)
        {
            blastRadius = radius;
            blastEdge = edgeMultiplier;
            blastKnockback = knockback;
        }

        private void Update()
        {
            float step = Mathf.Min(speed * Time.deltaTime, remaining);
            Vector3 next = transform.position + direction * step;
            remaining -= step;

            Vector3 path = next - previous;
            float length = path.magnitude;
            if (length > 0f && TryHit(previous, path / length, length, out RaycastHit hit))
            {
                Impact(hit);
                return;
            }

            previous = next;
            transform.position = next;

            // Shed a spark every so often, drifting off behind the bolt.
            shedDistance += step;
            if (shedDistance > 0.6f)
            {
                shedDistance = 0f;
                ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, next, -direction, 1, radius * 0.6f, 1.5f, 70f, Color.Lerp(color, Color.white, 0.4f));
            }

            if (remaining <= 0f)
            {
                // Fizzle out at the end of its range rather than vanishing.
                ImpactFlash.Spawn(CoreMaterial(material, color), next, radius * 2f, 0.08f);
                Remove();
            }
        }

        private void LateUpdate()
        {
            // The glow is a flat card; keep it facing the camera.
            Camera view = Camera.main;
            if (halo != null && view != null) halo.rotation = view.transform.rotation;
        }

        private bool TryHit(Vector3 from, Vector3 along, float length, out RaycastHit nearest)
        {
            nearest = default;
            bool found = false;
            int count = Physics.SphereCastNonAlloc(from, radius, along, Hits, length, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (owner != null && Hits[i].collider.transform.IsChildOf(owner)) continue;
                if (found && Hits[i].distance >= nearest.distance) continue;
                nearest = Hits[i];
                found = true;
            }
            return found;
        }

        private void Impact(RaycastHit hit)
        {
            // A sphere cast that starts overlapping something reports no contact point; use the bolt's position.
            Vector3 point = hit.distance > 0f ? hit.point : transform.position;

            var reflector = hit.collider.GetComponentInParent<IBoltReflector>();
            if (reflector != null && reflections < MaxReflections &&
                reflector.TryReflect(point, direction, owner, out Vector3 bounce, out float damageMultiplier))
            {
                Reflect(point, bounce, ((Component)reflector).transform, damageMultiplier);
                return;
            }

            Health target = hit.collider.GetComponentInParent<Health>();
            GameObject source = owner != null ? owner.gameObject : null;
            if (target != null && target.TakeDamage(damage, source))
                onHit?.Invoke(target);

            if (blastRadius > 0f && AreaBlast.Explode(point, blastRadius, damage, blastEdge, blastKnockback, source, target, material) > 0)
                onHit?.Invoke(null);

            Vector3 normal = hit.distance > 0f && hit.normal.sqrMagnitude > 0.01f ? hit.normal : -direction;
            ImpactFlash.Spawn(CoreMaterial(material, color), point, radius * 4f);
            ImpactFlash.Spawn(material, point, radius * 6f, 0.18f);
            ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, point + normal * 0.02f, normal, Mathf.RoundToInt(10f + radius * 60f),
                Mathf.Max(0.05f, radius * 0.7f), 4f + radius * 12f, 65f, Color.Lerp(color, Color.white, 0.35f));
            ImpactLight(point + normal * 0.15f, color, 2f + radius * 14f, 2f + radius * 20f);
            Remove();
        }

        /// <summary>Destroys the bolt, letting its streaks fade out where they are instead of vanishing with it.</summary>
        private void Remove()
        {
            foreach (TrailRenderer trail in new[] { glowTrail, coreTrail })
            {
                if (trail == null) continue;
                trail.transform.SetParent(null, true);
                trail.emitting = false;
                trail.autodestruct = true;
                Destroy(trail.gameObject, trail.time + 0.1f); // in case it never empties
            }
            Destroy(gameObject);
        }

        // ---------- Looks ----------

        private static Color Tint(Material source)
        {
            if (source != null && source.HasProperty(BaseColorId)) return source.GetColor(BaseColorId);
            return new Color(0.4f, 0.9f, 1f);
        }

        /// <summary>A hotter, whiter copy of the energy material for the core, made once per energy material.</summary>
        private static Material CoreMaterial(Material source, Color color)
        {
            if (source == null) return null;
            if (Cores.TryGetValue(source, out Material core) && core != null) return core;
            core = new Material(source) { name = source.name + " Core", hideFlags = HideFlags.DontSave };
            if (core.HasProperty(BaseColorId)) core.SetColor(BaseColorId, Color.Lerp(color, Color.white, 0.7f));
            Cores[source] = core;
            return core;
        }

        private static Transform Halo(Transform body, Color color, float radius)
        {
            var card = new GameObject("Glow");
            card.transform.SetParent(body, false);
            card.transform.localScale = Vector3.one * radius * 7f;
            card.AddComponent<MeshFilter>().sharedMesh = QuadMesh();
            var renderer = card.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = EffectMaterials.Additive(EffectMaterials.Look.Glow);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, new Color(color.r, color.g, color.b, 0.9f));
            renderer.SetPropertyBlock(block);
            return card.transform;
        }

        private static Mesh quad;

        private static Mesh QuadMesh()
        {
            if (quad != null) return quad;
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad = temp.GetComponent<MeshFilter>().sharedMesh;
            Destroy(temp);
            return quad;
        }

        private static TrailRenderer Trail(string name, Transform body, Material material, float width, float time, Color color, float alpha)
        {
            var holder = new GameObject(name);
            holder.transform.SetParent(body, false);
            var trail = holder.AddComponent<TrailRenderer>();
            trail.sharedMaterial = material;
            trail.time = time;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            trail.widthMultiplier = width;
            trail.minVertexDistance = 0.04f;
            trail.numCapVertices = 2;
            trail.alignment = LineAlignment.View;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(alpha, 0f), new GradientAlphaKey(alpha * 0.5f, 0.4f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = gradient;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            return trail;
        }

        /// <summary>A short-lived light that flares where the bolt lands.</summary>
        private static void ImpactLight(Vector3 position, Color color, float intensity, float range)
        {
            var go = new GameObject("Bolt Impact Light");
            go.transform.position = position;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            go.AddComponent<FadeLight>().Begin(intensity, 0.18f);
        }

        /// <summary>Fades a light out and removes it.</summary>
        private sealed class FadeLight : MonoBehaviour
        {
            private Light target;
            private float start, duration, age;

            public void Begin(float intensity, float seconds)
            {
                target = GetComponent<Light>();
                start = intensity;
                duration = Mathf.Max(0.01f, seconds);
            }

            private void Update()
            {
                age += Time.deltaTime;
                float k = age / duration;
                if (k >= 1f || target == null) { Destroy(gameObject); return; }
                target.intensity = start * (1f - k) * (1f - k);
            }
        }

        /// <summary>
        /// Sends the bolt off in a new direction, owned by the reflector so it ignores the reflector and can hit the
        /// original shooter. It no longer reports hits to the shooter's laser (no hit marker for being hit yourself).
        /// </summary>
        private void Reflect(Vector3 point, Vector3 newDirection, Transform newOwner, float damageMultiplier)
        {
            reflections++;
            blastRadius = 0f; // a bounced bolt never blasts the player
            owner = newOwner;
            onHit = null;
            damage *= damageMultiplier;
            direction = newDirection.sqrMagnitude > 0.0001f ? newDirection.normalized : -direction;
            remaining = range;
            previous = point;
            transform.SetPositionAndRotation(point, Quaternion.LookRotation(direction));
            ImpactFlash.Spawn(material, point, radius * 3f, 0.08f);
            ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, point, direction, 8, Mathf.Max(0.04f, radius * 0.5f), 4f, 50f, Color.Lerp(color, Color.white, 0.35f));
        }
    }
}
