using System.Collections.Generic;
using SignalLost.Combat;
using SignalLost.Effects;
using UnityEngine;
using UnityEngine.Rendering;

namespace SignalLost.Enemies.Desert
{
    /// <summary>
    /// A droplet of Pitchclaw's fuel (docs/desert-bosses.md, 11-10). Droplets are thrown in streams and sprays and act
    /// like a liquid:
    /// - they fly under gravity, stretched along their motion;
    /// - landing on the ground, they pour their volume into a <see cref="FuelPool"/> there (joining a pool already there);
    /// - hitting a wall, they run down it and pool at its foot;
    /// - hitting the player, they splash: a sting of damage (rate-limited across all drops), and the fuel that hit them
    ///   pools at their feet;
    /// - burning drops (Pitchclaw's last phase) light the pools they make.
    /// </summary>
    public sealed class FuelDrop : MonoBehaviour
    {
        private static float nextSting;

        private Vector3 velocity;
        private float volume, age, damage, stretch;
        private bool burning, onWall;
        private Vector3 wallNormal;
        private GameObject owner;
        private Material poolMaterial;
        private FireStyle fire;
        private Vector3 size;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => nextSting = 0f;

        public static FuelDrop Throw(Mesh mesh, Material dropMaterial, Material poolMaterial, FireStyle fire, Vector3 from, Vector3 velocity,
            float volume, float damage, bool burning, GameObject owner)
        {
            float s = Mathf.Lerp(0.18f, 0.42f, Mathf.Clamp01(volume / 0.06f));
            GameObject go = BossFx.Prop(burning ? "Burning Fuel" : "Fuel", mesh, dropMaterial, from, Quaternion.LookRotation(velocity),
                Vector3.one * s);
            var drop = go.AddComponent<FuelDrop>();
            drop.velocity = velocity;
            drop.volume = volume;
            drop.damage = damage;
            drop.burning = burning;
            drop.owner = owner;
            drop.poolMaterial = poolMaterial;
            drop.fire = fire;
            drop.size = Vector3.one * s;
            return drop;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            if (age > 6f)
            {
                Destroy(gameObject);
                return;
            }
            if (onWall)
            {
                // Running down a wall: slides along it, slowing with drag, until it finds the floor.
                velocity = Vector3.ProjectOnPlane(velocity + Vector3.down * (9.8f * dt), wallNormal) * (1f - 2f * dt);
            }
            else velocity += Vector3.down * (14f * dt);
            Vector3 step = velocity * dt;
            float distance = step.magnitude;
            if (distance > 0.0001f)
            {
                if (BossFx.DistanceToPlayer(transform.position + step * 0.5f) < 0.25f)
                {
                    Splash(BossFx.Ground(BossFx.PlayerFeet, 1f, 3f), true);
                    return;
                }
                foreach (RaycastHit hit in Physics.RaycastAll(transform.position, step / distance, distance + 0.05f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (owner != null && hit.collider.transform.IsChildOf(owner.transform)) continue;
                    if (hit.collider is CharacterController || hit.collider.GetComponent<FuelPool>() != null) continue;
                    if (hit.normal.y > 0.55f)
                    {
                        Splash(hit.point, false);
                        return;
                    }
                    // A wall: stick to it and run down.
                    onWall = true;
                    wallNormal = hit.normal;
                    transform.position = hit.point + hit.normal * 0.05f;
                    velocity = Vector3.ProjectOnPlane(velocity, hit.normal) * 0.3f;
                    return;
                }
            }
            transform.position += step;
            // Stretched along its motion, like a falling drop.
            stretch = Mathf.Clamp(velocity.magnitude * 0.08f, 1f, 3f);
            if (velocity.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(velocity);
            transform.localScale = new Vector3(size.x / Mathf.Sqrt(stretch), size.y / Mathf.Sqrt(stretch), size.z * stretch);
        }

        private void Splash(Vector3 ground, bool onPlayer)
        {
            if (onPlayer && Time.time >= nextSting)
            {
                nextSting = Time.time + 0.2f;
                BossFx.HurtPlayer(damage, owner);
            }
            FuelPool pool = FuelPool.Deposit(poolMaterial, fire, ground, volume, owner);
            if (burning && pool != null) pool.Ignite(owner);
            if (Random.value < 0.25f)
                ParticleBurst.Spawn(burning ? ParticleBurst.Kind.Fireball : ParticleBurst.Kind.Mist, ground + Vector3.up * 0.1f, Vector3.up, 2,
                    0.35f, 2f, 50f, burning ? (Color?)null : new Color(0.05f, 0.04f, 0.04f, 0.8f));
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// A pool of spilled fuel that behaves like a liquid on the ground (docs/desert-bosses.md, 11-10):
    /// - it grows with the fuel poured into it and spreads out thin over a few seconds;
    /// - its edge drapes over the terrain, and on a slope the whole pool creeps downhill;
    /// - pools that touch run together into one;
    /// - it slows anyone wading through it (<see cref="SlowAt"/>, read by <see cref="PlayerSlowReceiver"/>);
    /// - it catches fire from any flame (FireZone, sparks, a bolt shot into it) and burns across its whole area, and
    ///   the fire spreads to pools touching it;
    /// - it slowly soaks into the sand.
    /// </summary>
    public sealed class FuelPool : MonoBehaviour, IIgnitable, IBoltReflector
    {
        private const int Rim = 28;
        private const float Depth = 0.035f;
        private static readonly List<FuelPool> Active = new List<FuelPool>();

        private float volume, radius, life = 18f, age, rebuild, seedA, seedB;
        private bool burning;
        private GameObject owner;
        private FireStyle fire;
        private Mesh mesh;
        private BoxCollider box;
        private Material material;
        private readonly Vector3[] verts = new Vector3[Rim + 1];

        public static IReadOnlyList<FuelPool> All => Active;
        public float Radius => radius;
        public bool Burning => burning;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Active.Clear();

        /// <summary>The strongest slow from any pool the point stands in (0 to 1).</summary>
        public static float SlowAt(Vector3 feet)
        {
            float strongest = 0f;
            foreach (FuelPool pool in Active)
            {
                if (pool == null || pool.burning) continue;
                Vector3 d = feet - pool.transform.position;
                if (Mathf.Abs(d.y) > 0.8f) continue;
                d.y = 0f;
                if (d.magnitude <= pool.radius * 0.95f) strongest = Mathf.Max(strongest, 0.45f);
            }
            return strongest;
        }

        /// <summary>Pours fuel onto the ground at a point: into the pool already there, or a new one.</summary>
        public static FuelPool Deposit(Material material, FireStyle fire, Vector3 ground, float volume, GameObject owner)
        {
            foreach (FuelPool pool in Active)
            {
                if (pool == null) continue;
                Vector3 d = ground - pool.transform.position;
                if (Mathf.Abs(d.y) > 1f) continue;
                d.y = 0f;
                if (d.magnitude <= pool.radius + 0.4f)
                {
                    // Fuel landing near the edge drags the pool's middle toward it a little.
                    float share = volume / (pool.volume + volume);
                    pool.transform.position = BossFx.Ground(pool.transform.position + d * share, 1.5f, 3f);
                    pool.volume += volume;
                    pool.life = Mathf.Max(pool.life, pool.age + 14f);
                    return pool;
                }
            }
            var go = new GameObject("Fuel Pool");
            go.transform.position = ground;
            var created = go.AddComponent<FuelPool>();
            created.volume = volume;
            created.radius = 0.2f;
            created.owner = owner;
            created.fire = fire;
            created.material = material;
            created.Build();
            PlayerSlowReceiver.Ensure();
            return created;
        }

        private void Build()
        {
            seedA = Random.value * 10f;
            seedB = Random.value * 10f;
            mesh = new Mesh { name = "Fuel Pool" };
            mesh.MarkDynamic();
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            box = gameObject.AddComponent<BoxCollider>();
            Reshape();
        }

        private void OnEnable() => Active.Add(this);
        private void OnDisable() => Active.Remove(this);

        private void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            // It soaks away: the last few seconds it drains.
            if (age > life) volume -= volume * dt * 0.8f + 0.002f * dt;
            float target = Mathf.Sqrt(Mathf.Max(0f, volume) / (Depth * Mathf.PI));
            radius = Mathf.MoveTowards(radius, target, (target > radius ? 2.4f : 0.6f) * dt);
            if (radius < 0.12f && age > 1f)
            {
                Destroy(gameObject);
                return;
            }
            // On a slope a thick pool creeps downhill.
            Vector3 c = transform.position;
            float r = Mathf.Max(0.5f, radius * 0.7f);
            float hx = BossFx.Ground(c + Vector3.right * r, 1.5f, 3f).y - BossFx.Ground(c - Vector3.right * r, 1.5f, 3f).y;
            float hz = BossFx.Ground(c + Vector3.forward * r, 1.5f, 3f).y - BossFx.Ground(c - Vector3.forward * r, 1.5f, 3f).y;
            var downhill = new Vector3(-hx, 0f, -hz) / (2f * r);
            if (downhill.sqrMagnitude > 0.0025f && !burning)
                transform.position = BossFx.Ground(c + downhill * (Mathf.Min(1.5f, volume * 6f) * dt), 1.5f, 3f);
            // Pools that touch run together.
            foreach (FuelPool other in Active)
            {
                if (other == null || other == this || other.volume > volume || other.burning != burning) continue;
                Vector3 d = other.transform.position - transform.position;
                d.y = 0f;
                if (d.magnitude < (radius + other.radius) * 0.75f)
                {
                    float share = other.volume / (volume + other.volume);
                    transform.position = BossFx.Ground(transform.position + d * share, 1.5f, 3f);
                    volume += other.volume;
                    other.volume = 0f;
                    Destroy(other.gameObject);
                    break;
                }
            }
            rebuild -= dt;
            if (rebuild <= 0f)
            {
                rebuild = 0.12f;
                Reshape();
            }
        }

        /// <summary>The pool's outline: a lobed, uneven edge draped over the ground (each rim point finds the sand below it).</summary>
        private void Reshape()
        {
            Vector3 c = transform.position;
            verts[0] = Vector3.up * 0.03f;
            for (int i = 0; i < Rim; i++)
            {
                float a = i * Mathf.PI * 2f / Rim;
                float lobe = 1f + 0.16f * Mathf.Sin(3f * a + seedA + age * 0.3f) + 0.09f * Mathf.Sin(5f * a + seedB);
                Vector3 p = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (radius * lobe);
                float y = BossFx.Ground(p, 1.5f, 3f).y;
                verts[i + 1] = new Vector3(p.x - c.x, y - c.y + 0.03f, p.z - c.z);
            }
            var tris = new int[Rim * 3];
            for (int i = 0; i < Rim; i++)
            {
                tris[i * 3] = 0;
                tris[i * 3 + 1] = 1 + (i + 1) % Rim;
                tris[i * 3 + 2] = 1 + i;
            }
            var uv = new Vector2[Rim + 1];
            for (int i = 1; i <= Rim; i++) uv[i] = new Vector2(radius, i / (float)Rim); // the oil shader's ripples run outward
            mesh.Clear();
            mesh.vertices = verts;
            mesh.uv = uv;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            box.center = Vector3.up * 0.03f;
            box.size = new Vector3(radius * 1.8f, 0.06f, radius * 1.8f);
        }

        /// <summary>IIgnitable: the pool bursts into flame across its whole area; touching pools catch from it.</summary>
        public void Ignite(GameObject source)
        {
            if (burning || fire == null) return;
            burning = true;
            Transform immune = source != null && owner != null && source == owner ? owner.transform : null;
            float duration = Mathf.Max(3f, fire.duration);
            int rings = Mathf.Clamp(Mathf.CeilToInt(radius / 2.2f), 1, 4);
            FireZone.Spawn(fire, transform.position, Mathf.Min(radius, 2.2f), source, immune, duration);
            for (int ring = 1; ring < rings; ring++)
            {
                float rr = radius * ring / rings;
                int count = 4 * ring + 2;
                for (int i = 0; i < count; i++)
                {
                    float a = i * Mathf.PI * 2f / count + ring;
                    Vector3 p = BossFx.Ground(transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * rr, 1.5f, 3f);
                    FireZone.Spawn(fire, p, Mathf.Min(2.2f, radius / rings + 0.4f), source, immune, duration);
                }
            }
            life = Mathf.Min(life, age + duration * 0.8f);
            // Spread to pools touching this one.
            foreach (FuelPool other in new List<FuelPool>(Active))
            {
                if (other == null || other == this || other.burning) continue;
                Vector3 d = other.transform.position - transform.position;
                d.y = 0f;
                if (d.magnitude < radius + other.radius + 0.5f) other.Ignite(source);
            }
        }

        /// <summary>IBoltReflector: a bolt fired into the fuel sets it alight (the bolt stops as usual).</summary>
        public bool TryReflect(Vector3 hitPoint, Vector3 incoming, Transform shooter, out Vector3 newDirection, out float damageMultiplier)
        {
            newDirection = incoming;
            damageMultiplier = 1f;
            Ignite(shooter != null ? shooter.gameObject : null);
            return false;
        }
    }
}
