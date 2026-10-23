using SignalLost.Combat;
using SignalLost.Enemies.Desert;
using UnityEngine;

namespace SignalLost.Enemies
{
    /// <summary>
    /// Who is on the enemies' side, so enemy attacks never hurt enemies (their own, or each other). Fire, blasts,
    /// shrapnel and goo check <see cref="CanHurt"/> before dealing damage. Hazards the player sets off (shooting an oil
    /// slick, setting off a Jerry-Can Beetle) are credited to the player, so those still hurt enemies.
    /// </summary>
    public static class EnemyAllegiance
    {
        /// <summary>True if the object is, or belongs to, an enemy or an enemy-side hazard (like the Core Worm's arena fires).</summary>
        public static bool IsEnemy(GameObject item)
        {
            if (item == null) return false;
            return item.GetComponentInParent<DesertFodder>() != null || item.GetComponentInParent<Coreworm>() != null ||
                   item.GetComponentInParent<HermitCrab>() != null || item.GetComponentInParent<MortarFrog>() != null ||
                   item.GetComponentInParent<CorewormArena>() != null;
        }

        /// <summary>False when an enemy-side attack would hit an enemy.</summary>
        public static bool CanHurt(GameObject source, Health target)
        {
            if (target == null) return false;
            return !(IsEnemy(source) && IsEnemy(target.gameObject));
        }
    }
}
