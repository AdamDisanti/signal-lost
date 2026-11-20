using UnityEngine;

namespace SignalLost.Melee
{
    /// <summary>
    /// Lets a big enemy be finished off (Adam, 11-10: the finale's robot mini-bosses). Only small critters can be
    /// finished normally; with this, V also finishes this one once it is at or below <see cref="Below"/> of its health,
    /// with a bigger slam (<see cref="PlayerFists"/>). Goes on the object with the enemy's Health.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Finishable : MonoBehaviour
    {
        [Tooltip("Share of its health at or below which it can be finished.")]
        [SerializeField, Range(0.05f, 1f)] private float below = 0.25f;

        private static readonly System.Collections.Generic.List<Finishable> Live = new System.Collections.Generic.List<Finishable>();

        /// <summary>Every one in play (found by this list, not by colliders: a flying witch's may be off).</summary>
        public static System.Collections.Generic.IReadOnlyList<Finishable> All => Live;

        public SignalLost.Combat.Health Health { get; private set; }

        public float Below
        {
            get => below;
            set => below = Mathf.Clamp(value, 0.05f, 1f);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Live.Clear();

        private void Awake() => Health = GetComponent<SignalLost.Combat.Health>();
        private void OnEnable() => Live.Add(this);
        private void OnDisable() => Live.Remove(this);
    }
}
