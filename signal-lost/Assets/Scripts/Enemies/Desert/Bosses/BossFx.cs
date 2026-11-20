using System;
using System.Collections.Generic;
using SignalLost.Audio;
using SignalLost.Combat;
using SignalLost.Effects;
using SignalLost.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace SignalLost.Enemies.Desert
{
    /// <summary>
    /// Shared pieces for the desert bosses' attacks (docs/desert-bosses.md): finding the player, low-poly props for
    /// effects, and the hazards (<see cref="BossPuff"/>, <see cref="ThornShot"/>, <see cref="GroundThorn"/>,
    /// <see cref="SpikeRing"/>, <see cref="FireRing"/>, <see cref="SmokeCloud"/>). Everything is built from the low-poly
    /// meshes and flat materials the boss prefab carries, to match the game's style.
    /// </summary>
    public static class BossFx
    {
        private static FirstPersonController player;
        private static Health playerHealth;
        private static CharacterController playerBody;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            player = null;
            playerHealth = null;
            playerBody = null;
        }

        public static FirstPersonController Player
        {
            get
            {
                if (player == null)
                {
                    player = UnityEngine.Object.FindAnyObjectByType<FirstPersonController>();
                    playerHealth = player != null ? player.GetComponentInParent<Health>() : null;
                    playerBody = player != null ? player.GetComponentInParent<CharacterController>() : null;
                }
                return player;
            }
        }

        public static Health PlayerHealth => Player != null ? playerHealth : null;
        public static CharacterController PlayerBody => Player != null ? playerBody : null;
        public static bool PlayerAlive => PlayerHealth != null && !PlayerHealth.IsDead;

        /// <summary>True while the player is standing (or crouching) on the ground: a jump clears ground waves.</summary>
        public static bool PlayerGrounded => PlayerBody != null && PlayerBody.isGrounded;

        /// <summary>The player's feet.</summary>
        public static Vector3 PlayerFeet => PlayerBody != null
            ? PlayerBody.transform.position + PlayerBody.center - Vector3.up * (PlayerBody.height * 0.5f)
            : Player != null ? Player.transform.position : Vector3.zero;

        /// <summary>The middle of the player's body.</summary>
        public static Vector3 PlayerChest => PlayerBody != null ? PlayerBody.transform.position + PlayerBody.center
            : Player != null ? Player.transform.position + Vector3.up : Vector3.zero;

        /// <summary>Distance from a point to the player's body (their capsule), 0 inside it.</summary>
        public static float DistanceToPlayer(Vector3 point)
        {
            if (PlayerBody == null) return Player != null ? Vector3.Distance(point, Player.transform.position) : float.MaxValue;
            Vector3 centre = PlayerBody.transform.position + PlayerBody.center;
            float half = Mathf.Max(0f, PlayerBody.height * 0.5f - PlayerBody.radius);
            Vector3 a = centre - Vector3.up * half, b = centre + Vector3.up * half;
            Vector3 ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            return Mathf.Max(0f, Vector3.Distance(point, a + ab * t) - PlayerBody.radius);
        }

        /// <summary>Flat distance from a point to the player's feet.</summary>
        public static float FlatDistanceToPlayer(Vector3 point)
        {
            Vector3 d = PlayerFeet - point;
            d.y = 0f;
            return d.magnitude;
        }

        /// <summary>Damages the player (dodge i-frames refuse it). Returns true if it landed.</summary>
        public static bool HurtPlayer(float damage, GameObject source) => PlayerAlive && PlayerHealth.TakeDamage(damage, source);

        /// <summary>A visual-only mesh object (no collider, no shadows unless asked).</summary>
        public static GameObject Prop(string name, Mesh mesh, Material material, Vector3 position, Quaternion rotation, Vector3 scale,
            bool shadows = false, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go;
        }

        /// <summary>The ground under a point (static world only: ignores anything with Health and trigger volumes).</summary>
        public static Vector3 Ground(Vector3 point, float up = 3f, float down = 40f)
        {
            RaycastHit[] hits = Physics.RaycastAll(point + Vector3.up * up, Vector3.down, up + down, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            Vector3 ground = point;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider is CharacterController || hit.collider.GetComponentInParent<Health>() != null) continue;
                if (hit.collider.GetComponent<FuelPool>() != null) continue;
                if (hit.distance < best)
                {
                    best = hit.distance;
                    ground = hit.point;
                }
            }
            return ground;
        }

        private static Mesh ring;

        /// <summary>An open, two-sided cylinder wall 1 m in radius and 1 m tall (the fire ring).</summary>
        public static Mesh RingWall
        {
            get
            {
                if (ring != null) return ring;
                const int n = 40;
                var verts = new List<Vector3>();
                var tris = new List<int>();
                for (int i = 0; i < n; i++)
                {
                    float a0 = i * Mathf.PI * 2f / n, a1 = (i + 1) * Mathf.PI * 2f / n;
                    var p0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                    var p1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                    float h0 = (i % 2 == 0) ? 1f : 0.7f, h1 = ((i + 1) % 2 == 0) ? 1f : 0.7f; // a jagged flame top
                    int b = verts.Count;
                    verts.Add(p0); verts.Add(p1); verts.Add(p1 + Vector3.up * h1); verts.Add(p0 + Vector3.up * h0);
                    tris.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2, b, b + 1, b + 2, b, b + 2, b + 3 });
                }
                ring = new Mesh { name = "BossRingWall" };
                ring.SetVertices(verts);
                ring.SetTriangles(tris, 0);
                ring.RecalculateNormals();
                ring.RecalculateBounds();
                return ring;
            }
        }
    }

    /// <summary>
    /// A puff of something thrown out of a boss (flame, smoke, fuel, sparks, rock chips): flies, falls, grows or
    /// shrinks, and optionally reports where it lands.
    /// </summary>
    public sealed class BossPuff : MonoBehaviour
    {
        private Vector3 velocity, spin, startScale, endScale;
        private float gravity, life, age, drag;
        private Action<Vector3> onLand;

        public static BossPuff Spawn(Mesh mesh, Material material, Vector3 position, Vector3 velocity, float life, float startSize,
            float endSize, float gravity = 0f, Action<Vector3> onLand = null, float drag = 0f)
        {
            GameObject go = BossFx.Prop("Boss Puff", mesh, material, position, UnityEngine.Random.rotation, Vector3.one * startSize);
            var puff = go.AddComponent<BossPuff>();
            puff.velocity = velocity;
            puff.life = Mathf.Max(0.05f, life);
            puff.startScale = Vector3.one * startSize;
            puff.endScale = Vector3.one * endSize;
            puff.gravity = gravity;
            puff.onLand = onLand;
            puff.drag = drag;
            puff.spin = UnityEngine.Random.insideUnitSphere * 360f;
            return puff;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            velocity += Vector3.down * (gravity * dt);
            velocity *= Mathf.Clamp01(1f - drag * dt);
            Vector3 step = velocity * dt;
            if (onLand != null && step.y < 0f &&
                Physics.Raycast(transform.position, step.normalized, out RaycastHit hit, step.magnitude + 0.05f, ~0, QueryTriggerInteraction.Ignore) &&
                hit.collider.GetComponentInParent<Health>() == null)
            {
                onLand(hit.point);
                Destroy(gameObject);
                return;
            }
            transform.position += step;
            transform.Rotate(spin * dt, Space.Self);
            float t = age / life;
            transform.localScale = Vector3.Lerp(startScale, endScale, t);
            if (age >= life) Destroy(gameObject);
        }
    }

    /// <summary>A thorn fired by Briarwing: flies straight, hurts the player on contact, and sticks in the ground.</summary>
    public sealed class ThornShot : MonoBehaviour
    {
        private Vector3 velocity;
        private float damage, age;
        private GameObject owner;
        private Mesh mesh;
        private Material material;
        private float lingerLife;

        public static ThornShot Fire(Mesh mesh, Material material, Vector3 from, Vector3 direction, float speed, float damage, GameObject owner,
            float lingerLife = 4f)
        {
            GameObject go = BossFx.Prop("Thorn", mesh, material, from, Quaternion.LookRotation(direction), new Vector3(0.18f, 0.18f, 1.1f));
            var shot = go.AddComponent<ThornShot>();
            shot.velocity = direction.normalized * speed;
            shot.damage = damage;
            shot.owner = owner;
            shot.mesh = mesh;
            shot.material = material;
            shot.lingerLife = lingerLife;
            return shot;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            Vector3 step = velocity * dt;
            if (BossFx.DistanceToPlayer(transform.position) < 0.35f || BossFx.DistanceToPlayer(transform.position + step * 0.5f) < 0.35f)
            {
                BossFx.HurtPlayer(damage, owner);
                Destroy(gameObject);
                return;
            }
            foreach (RaycastHit hit in Physics.RaycastAll(transform.position, velocity.normalized, step.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                if (owner != null && hit.collider.transform.IsChildOf(owner.transform)) continue;
                if (hit.collider is CharacterController) continue; // the player is checked above
                GroundThorn.Spawn(mesh, material, hit.point - velocity.normalized * 0.4f, velocity.normalized, lingerLife, damage * 0.4f, owner);
                Destroy(gameObject);
                return;
            }
            transform.position += step;
            if (age > 4f) Destroy(gameObject);
        }
    }

    /// <summary>A thorn stuck in the ground (missed barrage thorns, spinning-dive trails): stings anyone who touches it.</summary>
    public sealed class GroundThorn : MonoBehaviour
    {
        private float life, age, damage, nextSting;
        private GameObject owner;
        private Vector3 fullScale;

        public static GroundThorn Spawn(Mesh mesh, Material material, Vector3 point, Vector3 direction, float life, float damage,
            GameObject owner, float size = 1f)
        {
            if (direction.sqrMagnitude < 0.001f) direction = Vector3.down;
            var scale = new Vector3(0.22f, 0.22f, 1.2f) * size;
            GameObject go = BossFx.Prop("Ground Thorn", mesh, material, point, Quaternion.LookRotation(-direction), scale, true);
            var thorn = go.AddComponent<GroundThorn>();
            thorn.life = life;
            thorn.damage = damage;
            thorn.owner = owner;
            thorn.fullScale = scale;
            return thorn;
        }

        private void Update()
        {
            age += Time.deltaTime;
            float grow = Mathf.Clamp01(age / 0.12f) * Mathf.Clamp01((life - age) / 0.5f);
            transform.localScale = fullScale * grow;
            if (age >= life)
            {
                Destroy(gameObject);
                return;
            }
            if (Time.time < nextSting) return;
            if (BossFx.DistanceToPlayer(transform.position + transform.forward * 0.5f * fullScale.z) < 0.45f)
            {
                BossFx.HurtPlayer(damage, owner);
                nextSting = Time.time + 0.8f;
            }
        }
    }

    /// <summary>
    /// Briarwing's dive bomb: a wave of spikes bursting out of the ground in a widening ring. It hurts once if the
    /// wave passes the player while they're on the ground (jump or dodge through it). The last ring stays a moment
    /// as a briar wall.
    /// </summary>
    public sealed class SpikeRing : MonoBehaviour
    {
        private Mesh mesh;
        private Material material;
        private float radius, endRadius, speed, damage, spacing, linger, nextCircle, height;
        private bool hit;
        private GameObject owner;

        public static SpikeRing Spawn(Mesh mesh, Material material, Vector3 centre, float startRadius, float endRadius, float speed,
            float damage, GameObject owner, float spacing = 2.1f, float linger = 2.5f, float height = 1.6f)
        {
            var go = new GameObject("Spike Ring");
            go.transform.position = centre;
            var wave = go.AddComponent<SpikeRing>();
            wave.mesh = mesh;
            wave.material = material;
            wave.radius = wave.nextCircle = startRadius;
            wave.endRadius = endRadius;
            wave.speed = speed;
            wave.damage = damage;
            wave.owner = owner;
            wave.spacing = spacing;
            wave.linger = linger;
            wave.height = height;
            return wave;
        }

        private void Update()
        {
            radius += speed * Time.deltaTime;
            while (radius >= nextCircle && nextCircle <= endRadius)
            {
                bool last = nextCircle + spacing > endRadius;
                Circle(nextCircle, last ? linger : 0.55f);
                nextCircle += spacing;
            }
            if (!hit && BossFx.PlayerGrounded && Mathf.Abs(BossFx.FlatDistanceToPlayer(transform.position) - radius) < 0.9f)
            {
                hit = BossFx.HurtPlayer(damage, owner) || hit;
                if (hit) PlayerKnock(transform.position, 2.5f);
            }
            if (radius > endRadius + 1f) Destroy(gameObject);
        }

        private void Circle(float r, float life)
        {
            int count = Mathf.Max(6, Mathf.RoundToInt(2f * Mathf.PI * r / spacing));
            float offset = UnityEngine.Random.value * Mathf.PI * 2f;
            for (int i = 0; i < count; i++)
            {
                float a = offset + i * Mathf.PI * 2f / count;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 at = BossFx.Ground(transform.position + dir * r, 4f, 8f);
                Vector3 up = (Vector3.up + dir * 0.35f + UnityEngine.Random.insideUnitSphere * 0.15f).normalized;
                float size = UnityEngine.Random.Range(0.8f, 1.2f) * height;
                GameObject spike = BossFx.Prop("Ground Spike", mesh, material, at - up * 0.3f, Quaternion.LookRotation(up),
                    new Vector3(0.45f, 0.45f, size), true);
                spike.AddComponent<RiseAndSink>().Begin(life, life > 1f ? damage * 0.25f : 0f, owner);
            }
            Sfx.PlayAt("stone_rise", transform.position, 0.5f, UnityEngine.Random.Range(0.8f, 1.1f), 40f);
        }

        internal static void PlayerKnock(Vector3 from, float distance)
        {
            if (BossFx.PlayerBody == null) return;
            Vector3 away = BossFx.PlayerFeet - from;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = Vector3.forward;
            BossFx.PlayerBody.Move(away.normalized * Mathf.Min(distance, 1.2f) + Vector3.up * 0.3f);
        }
    }

    /// <summary>A spike that shoots up out of the ground, stays for a while (stinging anyone touching it), then sinks.</summary>
    public sealed class RiseAndSink : MonoBehaviour
    {
        private float life, age, damage, nextSting;
        private Vector3 full;
        private GameObject owner;

        public void Begin(float seconds, float stingDamage, GameObject source)
        {
            life = seconds;
            damage = stingDamage;
            owner = source;
            full = transform.localScale;
            transform.localScale = new Vector3(full.x, full.y, 0.01f);
        }

        private void Update()
        {
            age += Time.deltaTime;
            float up = Mathf.Clamp01(age / 0.09f), down = Mathf.Clamp01((life + 0.35f - age) / 0.35f);
            transform.localScale = new Vector3(full.x, full.y, Mathf.Max(0.01f, full.z * up * down));
            if (age >= life + 0.35f)
            {
                Destroy(gameObject);
                return;
            }
            if (damage <= 0f || Time.time < nextSting || age > life) return;
            if (BossFx.DistanceToPlayer(transform.position + transform.forward * full.z * 0.5f) < 0.5f)
            {
                BossFx.HurtPlayer(damage, owner);
                nextSting = Time.time + 0.8f;
            }
        }
    }

    /// <summary>
    /// Cindermaw's rear slam: a wall of flame rolling outward from where it lands. Hurts once if it passes the player
    /// while they're on the ground (jump it, or dodge through it).
    /// </summary>
    public sealed class FireRing : MonoBehaviour
    {
        private float radius, endRadius, speed, damage, height;
        private bool hit;
        private GameObject owner;

        public static FireRing Spawn(Material flame, Vector3 centre, float startRadius, float endRadius, float speed, float damage,
            float height, GameObject owner)
        {
            GameObject go = BossFx.Prop("Fire Ring", BossFx.RingWall, flame, centre, Quaternion.identity, Vector3.one * startRadius);
            var ring = go.AddComponent<FireRing>();
            ring.radius = startRadius;
            ring.endRadius = endRadius;
            ring.speed = speed;
            ring.damage = damage;
            ring.height = height;
            ring.owner = owner;
            Sfx.PlayAt("fire_crackle", centre, 0.9f, 0.8f, 40f);
            return ring;
        }

        private void Update()
        {
            radius += speed * Time.deltaTime;
            float fade = Mathf.Clamp01((endRadius - radius) / 3f);
            float h = height * (0.85f + 0.15f * Mathf.Sin(Time.time * 30f)) * Mathf.Max(0.15f, fade);
            transform.localScale = new Vector3(radius, h, radius);
            transform.Rotate(0f, 40f * Time.deltaTime, 0f);
            if (!hit && BossFx.PlayerGrounded && Mathf.Abs(BossFx.FlatDistanceToPlayer(transform.position) - radius) < 0.8f)
            {
                hit = BossFx.HurtPlayer(damage, owner);
                if (hit) SpikeRing.PlayerKnock(transform.position, 2f);
            }
            if (radius >= endRadius) Destroy(gameObject);
        }
    }

    /// <summary>
    /// A cloud of Cindermaw's thruster smoke (rebuilt 11-10 as real smoke): soft, churning particle smoke that rolls
    /// out over the ground, hugs it and the walls (the particles collide with the world), and swirls out of the way of
    /// anything moving through it, the drake charging out or the player running in. While the player is inside, their
    /// view greys out. <see cref="Contains"/> lets the drake hide in one; <see cref="Puff"/> adds smoke to it.
    /// </summary>
    public sealed class SmokeCloud : MonoBehaviour
    {
        private static readonly List<SmokeCloud> Active = new List<SmokeCloud>();
        private static readonly List<Transform> Stirrers = new List<Transform>();
        private ParticleSystem smoke;
        private ParticleSystem.Particle[] buffer;
        private float radius, life, age;

        public static IReadOnlyList<SmokeCloud> All => Active;
        public float Radius => radius;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Active.Clear();
            Stirrers.Clear();
        }

        /// <summary>Something that pushes smoke aside as it moves through (the drake).</summary>
        public static void AddStirrer(Transform mover)
        {
            if (mover != null && !Stirrers.Contains(mover)) Stirrers.Add(mover);
        }

        public static SmokeCloud Spawn(Vector3 ground, float radius, float life)
        {
            var go = new GameObject("Smoke Cloud");
            go.transform.position = ground;
            var cloud = go.AddComponent<SmokeCloud>();
            cloud.radius = radius;
            cloud.life = life;
            cloud.Build();
            return cloud;
        }

        private void Build()
        {
            smoke = gameObject.AddComponent<ParticleSystem>();
            smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = smoke.main;
            main.loop = true;
            main.duration = 5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(radius * 0.55f, radius * 1.05f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.2f, 0.19f, 0.18f, 0.9f), new Color(0.3f, 0.28f, 0.26f, 0.85f));
            main.gravityModifier = -0.01f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 160;
            var emission = smoke.emission;
            emission.rateOverTime = 10f + radius * 4f;
            var shape = smoke.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = radius * 0.7f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var velocity = smoke.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);
            velocity.y = new ParticleSystem.MinMaxCurve(-0.05f, 0.25f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);
            var size = smoke.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.45f), new Keyframe(0.3f, 0.95f), new Keyframe(1f, 1.4f)));
            var colour = smoke.colorOverLifetime;
            colour.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.9f, 0.7f), new GradientAlphaKey(0f, 1f) });
            colour.color = fade;
            var spin = smoke.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
            var noise = smoke.noise;
            noise.enabled = true;
            noise.strength = 0.5f;
            noise.frequency = 0.25f;
            noise.scrollSpeed = 0.2f;
            // It rolls over the ground and piles against walls instead of passing through them.
            var collision = smoke.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.dampen = 0.6f;
            collision.bounce = 0.1f;
            collision.lifetimeLoss = 0f;
            collision.radiusScale = 0.25f;
            collision.quality = ParticleSystemCollisionQuality.Medium;
            var renderer = GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = EffectMaterials.AlphaBlended(EffectMaterials.Look.Smoke);
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            buffer = new ParticleSystem.Particle[main.maxParticles];
            smoke.Play();
            // A thick first billow, so it doesn't fade in from nothing.
            Puff(transform.position + Vector3.up * radius * 0.3f, Mathf.RoundToInt(20 + radius * 6), radius * 0.6f);
        }

        /// <summary>Billows fresh smoke into the cloud at a point.</summary>
        public void Puff(Vector3 at, int count, float spread)
        {
            if (smoke == null) return;
            var emit = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                emit.position = at + UnityEngine.Random.insideUnitSphere * spread;
                emit.velocity = UnityEngine.Random.insideUnitSphere * 1.5f + Vector3.up * 0.3f;
                smoke.Emit(emit, 1);
            }
        }

        /// <summary>True if the point is inside the cloud (a squashed dome over its ground spot).</summary>
        public bool Contains(Vector3 point, float margin = 0f)
        {
            Vector3 d = point - transform.position;
            d.y = Mathf.Max(0f, d.y - radius * 0.3f) * 1.4f;
            return d.magnitude <= radius * 0.85f + margin;
        }

        /// <summary>Stops billowing and lets the smoke drift away (the boss's reset, the cloud's end).</summary>
        public void Clear()
        {
            life = Mathf.Min(life, age);
            if (smoke != null) smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private void OnEnable() => Active.Add(this);
        private void OnDisable() => Active.Remove(this);

        private void Update()
        {
            age += Time.deltaTime;
            if (age >= life && smoke != null && smoke.isEmitting) smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (age >= life && (smoke == null || smoke.particleCount == 0))
            {
                Destroy(gameObject);
                return;
            }
            Stir();
            bool thick = age < life && age > 0.4f;
            if (thick && BossFx.Player != null && Contains(BossFx.PlayerChest + Vector3.up * 0.6f))
                MiniBossHud.Haze(new Color(0.24f, 0.23f, 0.22f, 0.9f), 0.3f);
        }

        /// <summary>Swirls particles out of the way of the player and anything registered as a stirrer.</summary>
        private void Stir()
        {
            if (smoke == null || smoke.particleCount == 0) return;
            int count = smoke.GetParticles(buffer);
            bool changed = false;
            for (int s = -1; s < Stirrers.Count; s++)
            {
                Vector3 at;
                if (s < 0)
                {
                    if (BossFx.Player == null) continue;
                    at = BossFx.PlayerChest;
                }
                else
                {
                    if (Stirrers[s] == null) continue;
                    at = Stirrers[s].position + Vector3.up * 1.5f;
                }
                if ((at - transform.position).sqrMagnitude > (radius + 5f) * (radius + 5f)) continue;
                float reach = s < 0 ? 1.8f : 3.5f;
                for (int i = 0; i < count; i++)
                {
                    Vector3 d = buffer[i].position - at;
                    float dist = d.magnitude;
                    if (dist > reach || dist < 0.001f) continue;
                    // pushed outward and curled sideways: a swirl rather than a clean hole
                    Vector3 push = d / dist * (reach - dist) * 2.5f + Vector3.Cross(Vector3.up, d).normalized * 1.2f;
                    buffer[i].velocity += push * (Time.deltaTime * 4f);
                    changed = true;
                }
            }
            if (changed) smoke.SetParticles(buffer, count);
        }
    }


}
