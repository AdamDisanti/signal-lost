using System.Collections.Generic;
using UnityEngine;

namespace SignalLost.Inventory
{
    /// <summary>
    /// One chunk of scrap dropped by a defeated enemy (meeting notes 2026-10-20). It pops out, lands, bobs and
    /// spins, and once the player walks within <see cref="MagnetRadius"/> it is sucked into their pockets and
    /// sorted into the inventory. Built at runtime from primitives, so no prefab is needed; shape and colour
    /// tell the four types apart.
    /// </summary>
    public sealed class ScrapPickup : MonoBehaviour
    {
        public const float MagnetRadius = 3.5f;
        private const float Gravity = 18f, Lifetime = 120f;
        private static readonly Dictionary<ScrapType, Material> Materials = new Dictionary<ScrapType, Material>();
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        private ScrapType type;
        private Vector3 velocity;
        private float born, restY, spin;
        private bool landed, pulled;

        public ScrapType Type => type;

        /// <summary>Spawns one chunk at <paramref name="position"/>, thrown with <paramref name="launch"/>.</summary>
        public static ScrapPickup Spawn(ScrapType type, Vector3 position, Vector3 launch)
        {
            PrimitiveType shape = type == ScrapType.Wiring ? PrimitiveType.Cylinder :
                type == ScrapType.FuelCells ? PrimitiveType.Capsule : PrimitiveType.Cube;
            GameObject chunk = GameObject.CreatePrimitive(shape);
            chunk.name = ScrapInfo.Name(type) + " Scrap";
            Destroy(chunk.GetComponent<Collider>()); // walked through, never blocks or catches bolts
            chunk.transform.position = position;
            chunk.transform.rotation = Random.rotation;
            chunk.transform.localScale = 1.4f * (type == ScrapType.Wiring ? new Vector3(0.08f, 0.22f, 0.08f) :
                type == ScrapType.FuelCells ? new Vector3(0.16f, 0.14f, 0.16f) :
                type == ScrapType.CircuitBoards ? new Vector3(0.3f, 0.04f, 0.22f) : new Vector3(0.3f, 0.07f, 0.26f));
            var renderer = chunk.GetComponent<Renderer>();
            renderer.sharedMaterial = MaterialFor(type);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var pickup = chunk.AddComponent<ScrapPickup>();
            pickup.type = type;
            pickup.velocity = launch;
            pickup.born = Time.time;
            pickup.spin = Random.Range(90f, 200f) * (Random.value < 0.5f ? -1f : 1f);
            return pickup;
        }

        private static Material MaterialFor(ScrapType type)
        {
            if (Materials.TryGetValue(type, out Material material) && material != null) return material;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            material = new Material(shader) { name = ScrapInfo.Name(type) + " (runtime)" };
            Color color = ScrapInfo.Color(type);
            material.SetColor(BaseColorId, color);
            material.EnableKeyword("_EMISSION");
            material.SetColor(EmissionId, color * 0.6f); // a faint glow so scrap reads against grey regolith
            Materials[type] = material;
            return material;
        }

        private void Update()
        {
            PlayerInventory inventory = PlayerInventory.Current;
            float age = Time.time - born;
            if (age > Lifetime) { Destroy(gameObject); return; }

            Vector3 chest = inventory != null ? inventory.transform.position + Vector3.up * 1f : transform.position;
            float distance = Vector3.Distance(transform.position, chest);
            if (!pulled && inventory != null && age > 0.6f && distance <= MagnetRadius) pulled = true;

            if (pulled)
            {
                // Sucked in: accelerates toward the player and shrinks as it arrives.
                float speed = Mathf.Lerp(6f, 22f, Mathf.Clamp01(age));
                transform.position = Vector3.MoveTowards(transform.position, chest, speed * Time.deltaTime);
                transform.localScale *= 1f - 2f * Time.deltaTime;
                if (Vector3.Distance(transform.position, chest) < 0.35f)
                {
                    inventory.AddScrap(type, 1);
                    Destroy(gameObject);
                }
                return;
            }

            if (!landed)
            {
                velocity.y -= Gravity * Time.deltaTime;
                Vector3 step = velocity * Time.deltaTime;
                if (velocity.y < 0f && Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, out RaycastHit ground,
                        0.25f - step.y, ~0, QueryTriggerInteraction.Ignore) && !ground.collider.GetComponentInParent<PlayerInventory>())
                {
                    landed = true;
                    restY = ground.point.y + 0.2f;
                }
                else transform.position += step;
                transform.Rotate(velocity.magnitude * 40f * Time.deltaTime, spin * Time.deltaTime, 0f, Space.World);
                return;
            }

            // Resting: hover, bob and spin so it is easy to spot.
            Vector3 position = transform.position;
            position.y = Mathf.Lerp(position.y, restY + Mathf.Sin((Time.time + born) * 3f) * 0.06f, 8f * Time.deltaTime);
            transform.position = position;
            transform.Rotate(0f, spin * Time.deltaTime, 0f, Space.World);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Materials.Clear();
    }
}
