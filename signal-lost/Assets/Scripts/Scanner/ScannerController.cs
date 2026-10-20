using System;
using UnityEngine;

namespace SignalLost.Scanner
{
    public sealed class ScannerController : MonoBehaviour
    {
        [SerializeField] private ScannerToggle scannerToggle;
        [SerializeField] private ScannerConfig config;
        [SerializeField] private Transform scanOrigin;
        [SerializeField, Min(0f)] private float targetReachedDistance = 2.5f;

        [Header("Runtime")]
        [SerializeField] private TrackableTarget currentTarget;
        [SerializeField, Range(0f, 1f)] private float signalStrength01;
        [SerializeField] private Vector3 directionToTarget;

        private float nextScanTime;
        private bool reachedCurrentTarget;

        public TrackableTarget CurrentTarget => currentTarget;
        public float SignalStrength01 => signalStrength01;
        public Vector3 DirectionToTarget => directionToTarget;

        public event Action<TrackableTarget> OnTargetReached;

        public void Awake()
        {
            if (scannerToggle == null || config == null || scanOrigin == null)
            {
                Debug.LogError("ScannerController requires a toggle, config, and scan origin.", this);
                enabled = false;
            }
        }

        private void Update()
        {
            if (!scannerToggle.IsRaised)
            {
                SetCurrentTarget(null);
                signalStrength01 = 0f;
                directionToTarget = Vector3.zero;
                return;
            }

            if (Time.time >= nextScanTime)
            {
                nextScanTime = Time.time + config.UpdateInterval;
                ScanForNearestTarget();
            }

            UpdateSignal();
        }

        private void ScanForNearestTarget()
        {
            // Quest objectives outrank creatures and are tracked at any distance (meeting notes 2026-10-20).
            QuestSignalSource quest = QuestSignalSource.Nearest(scanOrigin.position);
            if (quest != null)
            {
                SetCurrentTarget(quest);
                return;
            }

            EnemySignalSource bestTarget = null;
            float maxRange = config.MaxRange;
            float nearestDistanceSquared = maxRange * maxRange;

            foreach (EnemySignalSource candidate in FindObjectsByType<EnemySignalSource>())
            {
                if (!candidate.IsActive || candidate.IsBuried || candidate.SignalType != SignalType.AboveGround)
                {
                    continue;
                }

                float distanceSquared = (candidate.transform.position - scanOrigin.position).sqrMagnitude;

                if (distanceSquared <= nearestDistanceSquared)
                {
                    nearestDistanceSquared = distanceSquared;
                    bestTarget = candidate;
                }
            }

            SetCurrentTarget(bestTarget);
        }

        private void UpdateSignal()
        {
            if (currentTarget == null || !currentTarget.IsActive)
            {
                SetCurrentTarget(null);
                signalStrength01 = 0f;
                directionToTarget = Vector3.zero;
                return;
            }

            Vector3 offset = currentTarget.transform.position - scanOrigin.position;
            float distance = offset.magnitude;
            float MaxRange = config.MaxRange;

            if (distance > MaxRange && !(currentTarget is QuestSignalSource))
            {
                SetCurrentTarget(null);
                signalStrength01 = 0f;
                directionToTarget = Vector3.zero;
                return;
            }

            directionToTarget = offset.normalized;
            if (distance > 0f)
            {
                directionToTarget = offset / distance;
            }
            else
            {
                directionToTarget = Vector3.zero;
            }

            signalStrength01 = Mathf.Clamp01(1f - (distance / MaxRange) * currentTarget.SignalStrengthMultiplier * config.StrengthUpgradeMultiplier);

            if (distance <= targetReachedDistance && !reachedCurrentTarget)
            {
                reachedCurrentTarget = true;
                OnTargetReached?.Invoke(currentTarget);
            }
            else if (distance > targetReachedDistance)
            {
                reachedCurrentTarget = false;
            }
        }

        private void SetCurrentTarget(TrackableTarget target)
        {
            if (currentTarget == target)
            {
                return;
            }

            if (currentTarget != null)
            {
                currentTarget.SetDetected(false);
            }

            currentTarget = target;
            reachedCurrentTarget = false;

            if (currentTarget != null)
            {
                currentTarget.SetDetected(true);
            }
        }

        private void OnDisable()
        {
            SetCurrentTarget(null);
        }
    }
}