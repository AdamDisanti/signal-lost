using UnityEngine;

namespace SignalLost.Enemies
{
    /// <summary>
    /// An area a group of enemies defends together (for example the merchant crab and its cousins). Put it at the
    /// centre of the group and point each enemy's <see cref="Territory"/> at it: they all attack as soon as the
    /// player steps inside <see cref="Radius"/>, and all give up once the player is twice that far from the centre.
    /// Enemies without a zone defend a circle around where they start instead.
    /// </summary>
    public sealed class CombatZone : MonoBehaviour
    {
        [Tooltip("Enemies attack anyone inside this radius. They chase until the player is Leash Multiplier times as far away.")]
        [SerializeField, Min(1f)] private float radius = 14f;

        public float Radius
        {
            get => radius;
            set => radius = Mathf.Max(1f, value);
        }

        public Vector3 Centre => transform.position;

        private void OnDrawGizmos() => Territory.DrawRings(Centre, radius, new Color(1f, 0.55f, 0.15f, 0.9f));
    }
}
