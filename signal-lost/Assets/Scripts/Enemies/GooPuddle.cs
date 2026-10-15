using UnityEngine;
using UnityEngine.Rendering;

namespace SignalLost.Enemies
{
    /// <summary>A flat splat of goo left where a blob lands. Purely visual: it shrinks away after a few seconds.</summary>
    public sealed class GooPuddle : MonoBehaviour
    {
        private static readonly RaycastHit[] GroundHits = new RaycastHit[8];
        private float size, age, life;

        public static void Spawn(Material material, Vector3 point, float radius, float life = 3f)
        {
            var puddle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            puddle.name = "Goo Puddle";
            Collider collider = puddle.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            var renderer = puddle.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;

            // Lay it flat on the ground below the splash. A blob that hits the player (or an enemy) mid-air
            // splashes on them, so look straight down past anything with Health to the floor underneath.
            if (!FindGround(point, out Vector3 ground))
            {
                Destroy(puddle);
                return;
            }
            puddle.transform.SetPositionAndRotation(ground + Vector3.up * 0.015f, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            var component = puddle.AddComponent<GooPuddle>();
            component.size = radius * 1.6f;
            component.life = life;
            puddle.transform.localScale = new Vector3(component.size, 0.01f, component.size * Random.Range(0.75f, 1f));
        }

        private static bool FindGround(Vector3 point, out Vector3 ground)
        {
            ground = point;
            int count = Physics.RaycastNonAlloc(point + Vector3.up * 0.3f, Vector3.down, GroundHits, 8f, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                Collider other = GroundHits[i].collider;
                if (other is CharacterController || other.GetComponentInParent<SignalLost.Combat.Health>() != null) continue;
                if (GroundHits[i].distance >= nearest) continue;
                nearest = GroundHits[i].distance;
                ground = GroundHits[i].point;
                found = true;
            }
            return found;
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (age >= life)
            {
                Destroy(gameObject);
                return;
            }
            float fade = Mathf.Clamp01((life - age) / 0.8f);
            Vector3 scale = transform.localScale;
            transform.localScale = new Vector3(size * fade, scale.y, size * fade * 0.9f);
        }
    }
}
