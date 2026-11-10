using SignalLost.Player;
using SignalLost.Scanner;
using Unity.Hierarchy;
using UnityEngine;

namespace SignalLost.Enemies
{
    [DisallowMultipleComponent]
    public sealed class BurrowEmerge : MonoBehaviour
    {
        private enum Phase
        {
            Buried,
            Emerging,
            Grace,
            Aggressive
        }

        [SerializeField] private Transform visualRoot;
        [SerializeField] private EnemySignalSource signalSource;
        [SerializeField] private Collider[] bodyColliders = new Collider[0];

        [Tooltip("Rise when the player comes within Trigger Radius. Untick for enemies that only rise when something calls Emerge() (the crystal chameleon, revealed by the scanner).")]
        [SerializeField] private bool emergeOnProximity = true;
        [SerializeField, Min(0f)] private float triggerRadius = 3f;
        [SerializeField, Min(0f)] private float emergenceDuration = 0.8f;
        [SerializeField, Min(0f)] private float burrowDepth = 1.25f;
        [SerializeField, Min(0f)] private float postEmergenceGrace = 1.25f;

        private Transform player;
        private IBurrowOffset burrowReceiver;
        private Vector3 surfaceLocalPosition;
        private float emergenceStartOffset;
        private float phaseTime;
        private Phase phase = Phase.Buried;

        public bool IsBuried => phase == Phase.Buried || phase == Phase.Emerging;
        public bool CanTargetPlayer => phase == Phase.Grace ||  phase == Phase.Aggressive;
        public bool CanAttack => phase == Phase.Aggressive;

        private void Awake()
        {
            if(visualRoot == null)
            {
                Debug.LogError("Visual root is not assigned.", this);
                enabled = false;
                return;
            }

            if(signalSource == null)
            {
                signalSource = GetComponent<EnemySignalSource>();
            }

            if(signalSource == null)
            {
                Debug.LogError("BurrowEmerge requires an EnemySignalSource.", this);
                enabled = false;
                return;
            }

            burrowReceiver = GetComponent<IBurrowOffset>();

            if (burrowReceiver != null)
            {
                burrowReceiver.BurrowOffset = -burrowDepth;
            }

            SetBodyColliders(false);
            signalSource.SetBuried(true);
        }

        private void Start()
        {
            FirstPersonController controller = FindAnyObjectByType<FirstPersonController>();

            if(controller == null)
            {
                Debug.LogError("BurrowEmerge requires a FirstPersonController.", this);
                enabled = false;
                return;
            }

            player = controller.transform;
        }

        private void Update()
        {
            switch (phase)
            {
                case Phase.Buried:
                    CheckTriggerRadius();
                    break;
                case Phase.Emerging:
                    UpdateEmergence();
                    break;
                case Phase.Grace:
                    phaseTime += Time.deltaTime;
                    if(phaseTime >= postEmergenceGrace)
                    {
                        phase = Phase.Aggressive;
                    }
                    break;
            }
        }

        private void CheckTriggerRadius()
        {
            if(player == null)
            {
                return;
            }

            Vector3 offset = player.position - transform.position;
            offset.y = 0f;

            if(emergeOnProximity && offset.sqrMagnitude <= triggerRadius * triggerRadius)
            {
                Emerge();
            }
        }

        /// <summary>Starts rising now, whatever the player's distance. Does nothing unless it is still buried.</summary>
        public void Emerge()
        {
            if(!enabled || phase != Phase.Buried)
            {
                return;
            }

            phase = Phase.Emerging;
            phaseTime = 0f;

            if(burrowReceiver != null)
            {
                emergenceStartOffset = burrowReceiver.BurrowOffset;
            }
            else
            {
                emergenceStartOffset = -burrowDepth;
            }
        }

        private void UpdateEmergence()
        {
            phaseTime += Time.deltaTime;
            float t;
            if(emergenceDuration <= 0f)
            {
                t = 1f;
            }
            else
            {
                t = Mathf.Clamp01(phaseTime / emergenceDuration);
            }

            float easedT = t * t * (3f - 2f * t);
            if(burrowReceiver != null)
            {
                burrowReceiver.BurrowOffset = Mathf.Lerp(emergenceStartOffset, 0f, easedT);
            }

            if(t < 1f)
            {
                return;
            }

            signalSource.SetBuried(false);
            SetBodyColliders(true);
            phase = Phase.Grace;
            phaseTime = 0f;
        }

        private void SetBodyColliders(bool enabledState)
        {
            foreach(Collider bodyCollider in bodyColliders)
            {
                if(bodyCollider != null)
                {
                    bodyCollider.enabled = enabledState;
                }
            }
        }
    }
}