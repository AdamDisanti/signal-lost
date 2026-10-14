using UnityEngine;

namespace SignalLost.Combat
{
    /// <summary>
    /// A short-lived glowing burst where a melee strike or bolt lands. It pops in at full size and shrinks
    /// away. Placeholder effect until real particles are made.
    /// </summary>
    public sealed class ImpactFlash : MonoBehaviour
    {
        private float size, lifetime, age;

        public static void Spawn(Material material, Vector3 position, float size, float lifetime = 0.12f)
        {
            GameObject flash = MiningLaser.VisualPrimitive(PrimitiveType.Sphere, "Impact Flash", material);
            flash.transform.position = position;
            flash.transform.localScale = Vector3.one * size;
            var effect = flash.AddComponent<ImpactFlash>();
            effect.size = size;
            effect.lifetime = Mathf.Max(0.01f, lifetime);
        }

        private void Update()
        {
            age += Time.deltaTime;
            float k = age / lifetime;
            if (k >= 1f)
            {
                Destroy(gameObject);
                return;
            }
            transform.localScale = Vector3.one * (size * (1f - k * k));
        }
    }
}
