using UnityEngine;

namespace SignalLost.Scanner
{
    public sealed class EnemySignalSource : TrackableTarget
    {
        [SerializeField] private bool isBuried = false;
        public bool IsBuried => isBuried;
    }
}
