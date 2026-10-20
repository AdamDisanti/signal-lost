using System.Collections.Generic;
using SignalLost.Combat;
using UnityEngine;
using UnityEngine.Rendering;

namespace SignalLost.Enemies.Desert
{
    /// <summary>
    /// A puddle of ship fuel on the sand, sprayed by Sludge. Standing in it slows the player (<see cref="PlayerSlowReceiver"/>).
    /// Fire turns it into a <see cref="FireZone"/> of the same size: Sludge's igniter, a neighbouring burning slick, a
    /// beetle blast, or the player shooting it (it is an <see cref="IBoltReflector"/> that never reflects, only catches
    /// the spark). Fires Sludge starts itself don't hurt Sludge; fires anything else starts do, so luring it onto oil
    /// and shooting the oil is a way to hurt it.
    ///
    /// It has a paper-thin box collider so bolts and blasts can find it; it doesn't block walking.
    /// </summary>
    public sealed class OilSlick : MonoBehaviour, IIgnitable, IBoltReflector
    {
        private static readonly List<OilSlick> Active = new List<OilSlick>();

        private float radius, life, age, slow;
        private GameObject owner;
        private FireStyle fire;
        private bool burning;
        private Vector3 fullScale;

        public float Slow => burning ? 0f : slow;
        public GameObject Owner => owner;
        public float Radius => radius;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Active.Clear();

        /// <summary>The strongest slow from any slick the point is standing in (0 to 1).</summary>
        public static float SlowAt(Vector3 feet)
        {
            float strongest = 0f;
            foreach (OilSlick slick in Active)
                if (slick != null && slick.Contains(feet)) strongest = Mathf.Max(strongest, slick.Slow);
            return strongest;
        }

        /// <summary>Live slicks, for Sludge choosing one to ignite.</summary>
        public static IReadOnlyList<OilSlick> All => Active;

        /// <param name="slow">Fraction of the player's movement taken away while standing in it (0 to 1).</param>
        public static OilSlick Spawn(Material oil, FireStyle fire, Vector3 ground, float radius, float life, float slow, GameObject owner)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Oil Slick";
            Object.Destroy(go.GetComponent<Collider>()); // the capsule collider of a cylinder; replaced by a flat box
            go.transform.SetPositionAndRotation(ground + Vector3.up * 0.015f, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            go.transform.localScale = new Vector3(radius * 2f, 0.01f, radius * 2f * Random.Range(0.8f, 1f));
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(0.9f, 2f, 0.9f); // the cylinder mesh is 1 wide and 2 tall before scaling
            var renderer = go.GetComponent<Renderer>();
            if (oil != null) renderer.sharedMaterial = oil;
            renderer.shadowCastingMode = ShadowCastingMode.Off;

            var slick = go.AddComponent<OilSlick>();
            slick.radius = radius;
            slick.life = life;
            slick.slow = Mathf.Clamp01(slow);
            slick.owner = owner;
            slick.fire = fire;
            slick.fullScale = go.transform.localScale;
            PlayerSlowReceiver.Ensure();
            return slick;
        }

        private void OnEnable() => Active.Add(this);
        private void OnDisable() => Active.Remove(this);

        private bool Contains(Vector3 point)
        {
            Vector3 offset = point - transform.position;
            if (Mathf.Abs(offset.y) > 0.6f) return false;
            offset.y = 0f;
            return offset.magnitude <= radius * 0.95f;
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (age >= life)
            {
                Destroy(gameObject);
                return;
            }
            float shrink = Mathf.Clamp01((life - age) / 1.5f);
            transform.localScale = new Vector3(fullScale.x * shrink, fullScale.y, fullScale.z * shrink);
        }

        /// <summary>IIgnitable: burst into flame. Fires started by its owner (Sludge) don't hurt the owner.</summary>
        public void Ignite(GameObject source)
        {
            if (burning || fire == null) return;
            burning = true;
            Transform immune = source != null && owner != null && source == owner ? owner.transform : null;
            float remaining = Mathf.Max(1.5f, fire.duration);
            FireZone.Spawn(fire, transform.position, radius, source, immune, remaining);
            Destroy(gameObject);
        }

        /// <summary>IBoltReflector: a laser bolt landing in the oil sets it alight. The bolt itself stops as usual.</summary>
        public bool TryReflect(Vector3 hitPoint, Vector3 incoming, Transform shooter, out Vector3 newDirection, out float damageMultiplier)
        {
            newDirection = incoming;
            damageMultiplier = 1f;
            Ignite(shooter != null ? shooter.gameObject : null);
            return false;
        }
    }
}
