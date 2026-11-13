using System;
using UnityEngine;
using SignalLost.Combat;

namespace SignalLost.Scanner
{
    public sealed class ScannerController : MonoBehaviour
    {
        [SerializeField] private ScannerToggle scannerToggle;
        [SerializeField] private ScannerConfig config;
        [SerializeField] private Transform scanOrigin;
        [SerializeField, Min(0f)] private float targetReachedDistance = 2.5f;
        [SerializeField, Min(0f)] private float levelRangeOverride;

        [Header("Crystal Disruptor upgrade")]
        [Tooltip("The scanner model shown without the upgrade (under the visual root). Leave empty to never swap.")]
        [SerializeField] private GameObject standardModel;
        [Tooltip("The upgraded scanner model, shown once the upgrade is owned. Leave empty to keep the standard model.")]
        [SerializeField] private GameObject upgradedModel;

        [Header("Runtime")]
        [SerializeField] private TrackableTarget currentTarget;
        [SerializeField, Range(0f, 1f)] private float signalStrength01;
        [SerializeField] private Vector3 directionToTarget;

        private float nextScanTime;
        private bool reachedCurrentTarget;

        public TrackableTarget CurrentTarget => currentTarget;
        public float SignalStrength01 => signalStrength01;
        public Vector3 DirectionToTarget => directionToTarget;
        /// <summary>How far away creatures are detected (quest objectives are tracked at any distance).</summary>
        public float Range => config != null ? (levelRangeOverride > 0f ? levelRangeOverride * config.RangeUpgradeMultiplier : config.MaxRange) + RangeBonus : 0f;

        /// <summary>Extra critter range from upgrades (Long-Nose Antenna).</summary>
        public float RangeBonus { get; set; }

        /// <summary>Crystal Disruptor owned: disguised enemies show up, and get revealed when the raised scanner is near.</summary>
        public bool DisruptsCamouflage
        {
            get => disruptsCamouflage;
            set
            {
                disruptsCamouflage = value;
                if (upgradedModel == null) return;
                upgradedModel.SetActive(value);
                if (standardModel != null) standardModel.SetActive(!value);
            }
        }
        private bool disruptsCamouflage;

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
                if (disruptsCamouflage) DisruptNearby();
                ScanForNearestTarget();
            }

            UpdateSignal();
        }

        private static bool IsDefeated(EnemySignalSource enemy)
        {
            Health health = enemy.GetComponentInParent<Health>();
            return health != null && health.IsDead;
        }

        private static IScannerDisruptable Disguised(EnemySignalSource enemy)
        {
            IScannerDisruptable hider = enemy.GetComponentInParent<IScannerDisruptable>();
            return hider != null && hider.IsDisguised ? hider : null;
        }

        // Every disguised enemy whose fake crystal is within its reveal radius of the raised scanner drops the act.
        private void DisruptNearby()
        {
            foreach (EnemySignalSource candidate in FindObjectsByType<EnemySignalSource>())
            {
                IScannerDisruptable hider = Disguised(candidate);
                if (hider != null && !IsDefeated(candidate) &&
                    (hider.DisguisePosition - scanOrigin.position).sqrMagnitude <= hider.RevealRadius * hider.RevealRadius)
                    hider.Disrupt();
            }
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
            float maxRange = Range;
            float nearestDistanceSquared = maxRange * maxRange;

            foreach (EnemySignalSource candidate in FindObjectsByType<EnemySignalSource>())
            {
                if (!candidate.IsActive || IsDefeated(candidate) || (!disruptsCamouflage && Disguised(candidate) != null))
                {
                    continue;
                }

                if(candidate.IsBuried && candidate.SignalType != SignalType.UnderGround)
                {
                    continue;
                }

                if(!candidate.IsBuried && candidate.SignalType != SignalType.AboveGround && candidate.SignalType != SignalType.UnderGround)
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
            EnemySignalSource currentEnemy = currentTarget as EnemySignalSource;
            if (currentEnemy != null && IsDefeated(currentEnemy))
            {
                ScanForNearestTarget();
            }

            if (currentTarget == null || !currentTarget.IsActive)
            {
                SetCurrentTarget(null);
                signalStrength01 = 0f;
                directionToTarget = Vector3.zero;
                return;
            }

            Vector3 offset = currentTarget.transform.position - scanOrigin.position;
            float distance = offset.magnitude;
            float maxRange = Range;

            if (distance > maxRange && !(currentTarget is QuestSignalSource))
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

            signalStrength01 = Mathf.Clamp01(1f - (distance / maxRange) * currentTarget.SignalStrengthMultiplier * config.StrengthUpgradeMultiplier);

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