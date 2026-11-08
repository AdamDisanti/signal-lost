using UnityEngine;

namespace SignalLost.Inventory
{
    /// <summary>
    /// Shows an E-to-pick-up item (gear, the flask, a trophy) the way every collectable is shown (<see cref="GroundItem"/>):
    /// its model, scaled to the shared size, floats above the ground where the item lies and slowly spins. A box around
    /// the floating model is what the crosshair finds; the player walks through it. The item's own colliders are
    /// switched off, since they still fit the model as it was placed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FloatingPickup : MonoBehaviour
    {
        [Tooltip("The model to float. Empty: every child with something visible in it.")]
        [SerializeField] private Transform model;

        private Transform pivot;
        private Vector3 rest;
        private float phase;

        /// <summary>The floating display, once built (on the first frame the item is active).</summary>
        public Transform Display => pivot;

        public void SetModel(Transform value) => model = value;

        private void Start()
        {
            phase = Random.value * 10f;
            Transform shown = model != null ? model : GatherModel();
            if (shown == null) return;

            foreach (Collider own in GetComponents<Collider>()) own.enabled = false;
            foreach (Collider own in shown.GetComponentsInChildren<Collider>()) own.enabled = false;

            pivot = GroundItem.Wrap(shown, transform);
            Vector3 ground = GroundItem.GroundBelow(transform.position, transform);
            rest = ground + Vector3.up * GroundItem.HoverHeight(ground, transform);
            pivot.position = rest;

            // What the interaction ray hits: a little bigger than the model, so it is easy to aim at.
            var box = pivot.gameObject.AddComponent<BoxCollider>();
            box.size = Vector3.one * GroundItem.Size * 1.25f;
            foreach (CharacterController walker in FindObjectsByType<CharacterController>())
                Physics.IgnoreCollision(box, walker, true);
        }

        private void Update()
        {
            if (pivot != null) GroundItem.Animate(pivot, rest, phase);
        }

        // Every direct child with a mesh in it (not lights or glints) goes into one holder, so it moves as one.
        private Transform GatherModel()
        {
            var holder = new GameObject("Model").transform;
            holder.SetParent(transform, false);
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child == holder) continue;
                bool visible = false;
                foreach (Renderer part in child.GetComponentsInChildren<Renderer>(true))
                    if (!(part is ParticleSystemRenderer)) { visible = true; break; }
                if (visible) child.SetParent(holder, true);
            }
            if (holder.childCount > 0) return holder;
            Destroy(holder.gameObject);
            return null;
        }
    }
}
