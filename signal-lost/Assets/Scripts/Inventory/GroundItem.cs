using SignalLost.Combat;
using UnityEngine;

namespace SignalLost.Inventory
{
    /// <summary>
    /// One look for everything the player can collect from the ground (Lawson, 11-07): gear, the flask, scrap, trophies
    /// and ship parts are all shown the same size, floating at the same height and slowly spinning, so it is obvious
    /// they are there to be picked up. <see cref="Wrap"/> puts a model in a display pivot of that size;
    /// <see cref="Animate"/> bobs and turns it. <see cref="FloatingPickup"/> does both for E-to-pick-up items.
    /// </summary>
    public static class GroundItem
    {
        /// <summary>The longest side of every collectable lying in the world, in metres.</summary>
        public const float Size = 0.55f;
        /// <summary>Height of its middle above the ground (lowered under a low ceiling, see <see cref="HoverHeight"/>).</summary>
        public const float Hover = 0.8f;
        public const float BobHeight = 0.07f, BobSpeed = 2.2f, SpinSpeed = 70f;

        /// <summary>
        /// Puts <paramref name="model"/> in a new pivot under <paramref name="parent"/>: upright, its middle on the pivot
        /// and scaled so its longest side is <see cref="Size"/>. Returns the pivot, which the caller moves and spins.
        /// </summary>
        public static Transform Wrap(Transform model, Transform parent, string name = "Floating Display")
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            pivot.SetPositionAndRotation(model.position, Quaternion.identity);
            pivot.localScale = Vector3.one;
            model.SetParent(pivot, true);
            model.localRotation = Quaternion.identity; // the model's own upright pose

            Bounds bounds = VisibleBounds(model);
            float longest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            if (longest > 0.0001f) model.localScale *= Size / longest;
            bounds = VisibleBounds(model);
            model.position += pivot.position - bounds.center;
            return pivot;
        }

        /// <summary>World bounds of what can be seen (meshes; not particles, lines or trails).</summary>
        public static Bounds VisibleBounds(Transform root)
        {
            var bounds = new Bounds(root.position, Vector3.zero);
            bool first = true;
            foreach (Renderer part in root.GetComponentsInChildren<Renderer>())
            {
                if (part is ParticleSystemRenderer || part is LineRenderer || part is TrailRenderer || !part.enabled) continue;
                if (first) { bounds = part.bounds; first = false; }
                else bounds.Encapsulate(part.bounds);
            }
            return bounds;
        }

        /// <summary>The ground under <paramref name="point"/>: the nearest solid surface, ignoring creatures, the player and <paramref name="ignore"/>.</summary>
        public static Vector3 GroundBelow(Vector3 point, Transform ignore)
        {
            Vector3 best = point;
            float nearest = float.MaxValue;
            foreach (RaycastHit hit in Physics.RaycastAll(point + Vector3.up * 0.6f, Vector3.down, 40f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.distance >= nearest || Skip(hit.collider, ignore)) continue;
                nearest = hit.distance;
                best = hit.point;
            }
            return best;
        }

        /// <summary>
        /// How high above <paramref name="ground"/> the middle floats: <see cref="Hover"/>, or lower if something is
        /// overhead (an item hidden under a fallen panel or a hull rib), but always clear of the ground.
        /// </summary>
        public static float HoverHeight(Vector3 ground, Transform ignore)
        {
            float lowest = Size * 0.5f + 0.06f, wanted = Hover;
            float roof = float.MaxValue;
            foreach (RaycastHit hit in Physics.RaycastAll(ground + Vector3.up * 0.05f, Vector3.up, Hover + Size, ~0, QueryTriggerInteraction.Ignore))
                if (!Skip(hit.collider, ignore)) roof = Mathf.Min(roof, hit.distance + 0.05f);
            if (roof < float.MaxValue) wanted = Mathf.Min(wanted, roof - Size * 0.5f - BobHeight - 0.05f);
            return Mathf.Max(lowest, wanted);
        }

        /// <summary>Bobs and turns a display pivot about its resting point.</summary>
        public static void Animate(Transform pivot, Vector3 rest, float phase)
        {
            float t = Time.time + phase;
            pivot.position = rest + Vector3.up * (Mathf.Sin(t * BobSpeed) * BobHeight);
            pivot.rotation = Quaternion.Euler(0f, t * SpinSpeed, 0f);
        }

        private static bool Skip(Collider collider, Transform ignore) =>
            (ignore != null && collider.transform.IsChildOf(ignore)) || collider is CharacterController ||
            collider.GetComponentInParent<Health>() != null;
    }
}
