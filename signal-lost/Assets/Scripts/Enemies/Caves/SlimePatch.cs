using System.Collections.Generic;
using SignalLost.Enemies.Desert;
using UnityEngine;

namespace SignalLost.Enemies.Caves
{
    /// <summary>
    /// One patch of the Prism Snail's glowing slime trail. While the player stands in it they are slowed, through the
    /// same <see cref="PlayerSlowReceiver.ExtraSlow"/> hook the final boss's ice uses, and hurt (all patches share one
    /// damage tick, so overlapping patches don't stack). It shrinks away over its life.
    /// </summary>
    public sealed class SlimePatch : MonoBehaviour
    {
        private static readonly List<SlimePatch> Active = new List<SlimePatch>();

        private const float HurtInterval = 0.5f;
        private static float nextHurt;

        private float fullRadius, radius, slow, damage, life, age;
        private GameObject owner;

        public static void Spawn(Material material, Vector3 position, float radius, float slow, float damage, GameObject owner, float life)
        {
            GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "Slime Patch";
            Destroy(disc.GetComponent<Collider>());
            disc.GetComponent<Renderer>().sharedMaterial = material;
            disc.transform.position = position;
            disc.transform.localScale = new Vector3(radius * 2f, 0.01f, radius * 2f);
            var patch = disc.AddComponent<SlimePatch>();
            patch.fullRadius = patch.radius = radius;
            patch.slow = slow;
            patch.damage = damage;
            patch.owner = owner;
            patch.life = Mathf.Max(0.5f, life);
            PlayerSlowReceiver.Ensure();
        }

        private void OnEnable()
        {
            if (Active.Count == 0) PlayerSlowReceiver.ExtraSlow += SlowAt;
            Active.Add(this);
        }

        private void OnDisable()
        {
            Active.Remove(this);
            if (Active.Count == 0) PlayerSlowReceiver.ExtraSlow -= SlowAt;
        }

        /// <summary>The strongest slow (0 to 1) of any patch under this point.</summary>
        public static float SlowAt(Vector3 point)
        {
            float best = 0f;
            foreach (SlimePatch patch in Active) best = Mathf.Max(best, SlowAt(point, patch));
            return best;
        }

        private static float SlowAt(Vector3 point, SlimePatch patch)
        {
            Vector3 offset = point - patch.transform.position;
            float height = offset.y;
            offset.y = 0f;
            return offset.magnitude <= patch.radius && height > -0.5f && height < 1.5f ? patch.slow : 0f;
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (age >= life)
            {
                Destroy(gameObject);
                return;
            }
            // Full size for most of its life, then it dries up.
            float fade = Mathf.Clamp01((life - age) / (life * 0.25f));
            radius = fullRadius * fade; // the slow shrinks with the patch
            transform.localScale = new Vector3(radius * 2f, 0.01f, radius * 2f);

            if (damage > 0f && Time.time >= nextHurt && BossFx.PlayerAlive && SlowAt(BossFx.PlayerFeet, this) > 0f)
            {
                nextHurt = Time.time + HurtInterval;
                BossFx.HurtPlayer(damage, owner);
            }
        }
    }
}
