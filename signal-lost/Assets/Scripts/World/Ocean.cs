using SignalLost.Player;
using UnityEngine;

namespace SignalLost.World
{
    /// <summary>
    /// The sea around a stage's island (Lawson, 11-07: each stage is its own island instead of being walled in by
    /// cliffs). This object's height is sea level; its water mesh sits on it. The player can't swim: they can wade out
    /// across the shallows, but once their feet are more than <see cref="WadeDepth"/> under the surface the
    /// <see cref="DeepWaterGuard"/> fades to black, costs them some health and puts them back on the nearest land.
    /// Spawn checks (<see cref="SignalLost.Enemies.SpawnSafety"/>) also refuse spots under the water. One per scene;
    /// the guard is added to the player when the scene starts.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Ocean : MonoBehaviour
    {
        public static Ocean Current { get; private set; }

        [Tooltip("How far under the surface the player's feet may go before it is too deep (they can't swim).")]
        [SerializeField, Min(0.3f)] private float wadeDepth = 1.3f;
        [Tooltip("Health lost each time the player has to be pulled out of deep water.")]
        [SerializeField, Min(0f)] private float drownDamage = 15f;
        [Tooltip("The middle of the island, in world space. The rescue turns the player to face inland.")]
        [SerializeField] private Vector3 islandCentre;

        // Which ground is open sea rather than a low hollow inland (the camp, the merchant's hollow, dune troughs): a
        // grid of bits baked by the builder from the water that connects to the open sea. Outside the grid is all sea.
        [SerializeField, HideInInspector] private Vector2 maskOrigin;
        [SerializeField, HideInInspector] private float maskCell;
        [SerializeField, HideInInspector] private int maskSize;
        [SerializeField, HideInInspector] private byte[] mask;

        public float SeaLevel => transform.position.y;
        public float WadeDepth => wadeDepth;
        public float DrownDamage => drownDamage;
        public Vector3 IslandCentre => islandCentre;

        /// <summary>True when feet at <paramref name="feet"/> would be out of their depth in the sea.</summary>
        public bool IsTooDeep(Vector3 feet) => feet.y < SeaLevel - wadeDepth && IsSea(feet.x, feet.z);

        /// <summary>True when <paramref name="point"/> is in the sea, under the surface by more than <paramref name="margin"/>.</summary>
        public bool IsUnderwater(Vector3 point, float margin = 0f) => point.y < SeaLevel - margin && IsSea(point.x, point.z);

        /// <summary>True where the sea reaches (not a hollow inland that happens to lie below sea level).</summary>
        public bool IsSea(float x, float z)
        {
            if (mask == null || mask.Length == 0 || maskSize <= 0 || maskCell <= 0f) return true;
            int i = Mathf.FloorToInt((x - maskOrigin.x) / maskCell), k = Mathf.FloorToInt((z - maskOrigin.y) / maskCell);
            if (i < 0 || k < 0 || i >= maskSize || k >= maskSize) return true;
            int bit = k * maskSize + i;
            return (mask[bit >> 3] & (1 << (bit & 7))) != 0;
        }

        /// <summary>Editor builders: stores which cells of a square grid are sea (row by row along x, then z).</summary>
        public void SetSeaMask(Vector2 origin, float cell, int size, bool[] sea)
        {
            maskOrigin = origin;
            maskCell = cell;
            maskSize = size;
            mask = new byte[(size * size + 7) / 8];
            for (int bit = 0; bit < sea.Length; bit++)
                if (sea[bit]) mask[bit >> 3] |= (byte)(1 << (bit & 7));
        }

        private void OnEnable()
        {
            if (Current == null) Current = this;
        }

        private void OnDisable()
        {
            if (Current == this) Current = null;
        }

        private void Start()
        {
            var player = FindAnyObjectByType<FirstPersonController>();
            if (player != null && player.GetComponent<DeepWaterGuard>() == null) player.gameObject.AddComponent<DeepWaterGuard>();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.7f, 0.9f);
            Gizmos.DrawWireCube(new Vector3(islandCentre.x, SeaLevel, islandCentre.z), new Vector3(300f, 0f, 300f));
            Gizmos.color = new Color(0.9f, 0.4f, 0.2f);
            Gizmos.DrawWireCube(new Vector3(islandCentre.x, SeaLevel - wadeDepth, islandCentre.z), new Vector3(300f, 0f, 300f));
        }
    }
}
