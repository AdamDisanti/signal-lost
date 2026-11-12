using UnityEngine;

namespace SignalLost.Scanner
{
    public sealed class EnemySignalSource : TrackableTarget
    {
        [SerializeField] private bool isBuried = false;
        public bool IsBuried => isBuried;

        public void SetBuried(bool buried)
        {
            isBuried = buried;
            if (buried)
            {
                SetSignalType(SignalType.UnderGround);
            }
            else
            {
                SetSignalType(SignalType.AboveGround);
            }

        }
    }
}
