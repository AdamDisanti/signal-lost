using SignalLost.Combat;
using UnityEngine;
using UnityEngine.Rendering;

namespace SignalLost.Melee
{
    /// <summary>
    /// Shows that a staggered enemy can be finished (Gaussian Brass Knuckles III): a spinning, pulsing diamond over its
    /// head and a flickering ring at its feet, Doom-style, so the player can spot the ones worth a finisher in a crowd.
    /// Added and removed by <see cref="PlayerFists"/> as enemies drop low or get out of range.
    /// </summary>
    public sealed class FinisherGlow : MonoBehaviour
    {
        private const int RingPoints = 24;

        private Transform diamond;
        private LineRenderer ring;
        private float top, radius;

        private void Awake()
        {
            Bounds bounds = new Bounds(transform.position + Vector3.up * 0.5f, Vector3.one);
            var colliders = GetComponentsInChildren<Collider>();
            if (colliders.Length > 0)
            {
                bounds = colliders[0].bounds;
                foreach (Collider c in colliders) bounds.Encapsulate(c.bounds);
            }
            top = bounds.max.y - transform.position.y + 0.55f;
            radius = Mathf.Clamp(Mathf.Max(bounds.extents.x, bounds.extents.z) + 0.35f, 0.6f, 3f);

            GameObject gem = MiningLaser.VisualPrimitive(PrimitiveType.Cube, "Finisher Diamond", GaussFx.HotMaterial);
            diamond = gem.transform;
            diamond.SetParent(null, false); // kept upright and unscaled; follows in LateUpdate

            ring = new GameObject("Finisher Ring").AddComponent<LineRenderer>();
            ring.sharedMaterial = GaussFx.EnergyMaterial;
            ring.useWorldSpace = true;
            ring.loop = true;
            ring.positionCount = RingPoints;
            ring.shadowCastingMode = ShadowCastingMode.Off;
            ring.receiveShadows = false;
        }

        private void OnDestroy()
        {
            if (diamond != null) Destroy(diamond.gameObject);
            if (ring != null) Destroy(ring.gameObject);
        }

        private void LateUpdate()
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 14f);
            Vector3 feet = transform.position;
            diamond.position = feet + Vector3.up * (top + Mathf.Sin(Time.time * 4f) * 0.08f);
            diamond.rotation = Quaternion.Euler(45f, Time.time * 220f, 45f);
            diamond.localScale = Vector3.one * (0.22f + 0.07f * pulse);
            for (int i = 0; i < RingPoints; i++)
            {
                float a = i / (float)RingPoints * Mathf.PI * 2f;
                ring.SetPosition(i, feet + new Vector3(Mathf.Cos(a) * radius, 0.08f, Mathf.Sin(a) * radius));
            }
            ring.widthMultiplier = 0.05f + 0.06f * pulse;
        }
    }
}
