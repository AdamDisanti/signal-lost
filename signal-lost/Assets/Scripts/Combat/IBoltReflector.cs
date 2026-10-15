using UnityEngine;

namespace SignalLost.Combat
{
    /// <summary>
    /// Something that can bounce laser bolts instead of taking their damage, such as a hermit crab hiding in its
    /// shell. Put it on (or above) the collider the bolt hits. When it reflects, the bolt flies off in the new
    /// direction with the reflector as its owner, so it can hit whoever fired it.
    /// </summary>
    public interface IBoltReflector
    {
        /// <param name="hitPoint">Where the bolt struck.</param>
        /// <param name="incoming">The bolt's direction of travel.</param>
        /// <param name="shooter">Whoever owns the bolt (normally the player), or null.</param>
        /// <param name="newDirection">Where to send the bolt.</param>
        /// <param name="damageMultiplier">Scales the bolt's damage after the bounce.</param>
        /// <returns>True to reflect; false to let the bolt hit normally.</returns>
        bool TryReflect(Vector3 hitPoint, Vector3 incoming, Transform shooter, out Vector3 newDirection, out float damageMultiplier);
    }
}
