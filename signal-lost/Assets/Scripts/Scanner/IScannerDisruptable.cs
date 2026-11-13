using UnityEngine;

namespace SignalLost.Scanner
{
    /// <summary>
    /// An enemy that hides as a crystal and is revealed by the Crystal Disruptor scanner upgrade (the crystal
    /// chameleon; the cave axolotl later). Put it on the enemy with an <see cref="EnemySignalSource"/> on the same
    /// object or a child. Without the upgrade the scanner ignores it while <see cref="IsDisguised"/>; with the upgrade
    /// it beeps at it, and raising the scanner within <see cref="RevealRadius"/> of <see cref="DisguisePosition"/>
    /// calls <see cref="Disrupt"/>. IsDisguised may turn true again later (an enemy that re-burrows).
    /// </summary>
    public interface IScannerDisruptable
    {
        bool IsDisguised { get; }
        /// <summary>The fake crystal the player has to get close to.</summary>
        Vector3 DisguisePosition { get; }
        float RevealRadius { get; }
        /// <summary>Drop the disguise and come out fighting.</summary>
        void Disrupt();
    }
}
