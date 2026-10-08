using UnityEngine;
using UnityEngine.InputSystem;

namespace SignalLost.Combat
{
    /// <summary>
    /// The player's mining laser: a continuous hitscan beam fired while Attack is held.
    /// Firing drains charge; charge refills after a short pause. Emptying it overheats the laser,
    /// which locks firing until the charge has fully refilled. There are no magazines or reloads.
    /// All numbers are placeholders to tune in the Inspector during playtesting.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MiningLaser : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private InputActionAsset inputActions;
        [Tooltip("Where aim comes from; normally the player camera.")]
        [SerializeField] private Transform aimOrigin;
        [Tooltip("Where the beam is drawn from; a point slightly below and right of the camera.")]
        [SerializeField] private Transform muzzle;
        [SerializeField] private LineRenderer beam;

        [Header("Damage")]
        [SerializeField, Min(0f)] private float damagePerSecond = 30f;
        [SerializeField, Min(1f)] private float range = 25f;
        [SerializeField] private LayerMask hitMask = ~0;

        [Header("Charge (placeholder values)")]
        [SerializeField, Min(1f)] private float maxCharge = 100f;
        [Tooltip("Charge used per second of firing. 25 means four seconds of continuous fire from full.")]
        [SerializeField, Min(0f)] private float drainPerSecond = 25f;
        [SerializeField, Min(0f)] private float rechargePerSecond = 40f;
        [Tooltip("Seconds after releasing the trigger before charge starts refilling.")]
        [SerializeField, Min(0f)] private float rechargeDelay = 0.5f;
        [Tooltip("Seconds an overheat lasts. Charge refills from empty to full over this time.")]
        [SerializeField, Min(0.1f)] private float overheatDuration = 2f;

        private readonly RaycastHit[] hits = new RaycastHit[16];
        private InputActionAsset runtimeActions;
        private InputActionMap playerMap;
        private InputAction attack;
        private float charge;
        private float lastFiredTime = float.NegativeInfinity;
        private bool waitForRelease;

        public float Charge01 => charge / maxCharge;
        public bool IsFiring { get; private set; }
        public bool IsOverheated { get; private set; }
        public float Range => range;
        /// <summary>The damageable object currently under the beam, or null.</summary>
        public Health CurrentTarget { get; private set; }

        private void Awake()
        {
            if (inputActions == null || aimOrigin == null || muzzle == null || beam == null)
            {
                Debug.LogError("MiningLaser requires input actions, an aim origin, a muzzle, and a beam LineRenderer.", this);
                enabled = false;
                return;
            }

            // Same pattern as FirstPersonController: use a private copy so components never fight over the shared asset.
            runtimeActions = Instantiate(inputActions);
            playerMap = runtimeActions.FindActionMap("Player", true);
            playerMap.bindingMask = InputBinding.MaskByGroups("Keyboard&Mouse", "Gamepad");
            attack = playerMap.FindAction("Attack", true);

            charge = maxCharge;
            beam.positionCount = 2;
            beam.useWorldSpace = true;
            beam.enabled = false;
        }

        private void OnEnable() => playerMap?.Enable();

        private void OnDisable()
        {
            playerMap?.Disable();
            IsFiring = false;
            CurrentTarget = null;
            if (beam != null) beam.enabled = false;
        }

        private void OnDestroy()
        {
            if (runtimeActions != null) Destroy(runtimeActions);
        }

        // LateUpdate so the beam follows this frame's camera rotation from FirstPersonController.Update.
        private void LateUpdate()
        {
            bool pressed = attack.IsPressed();
            bool controllable = Application.isFocused && Cursor.lockState == CursorLockMode.Locked;

            // The left click that re-captures the cursor must not also fire. While the cursor is free,
            // require the button to be released before the laser will fire again.
            if (!controllable) waitForRelease = true;
            else if (waitForRelease && !pressed) waitForRelease = false;

            bool wantsToFire = controllable && !waitForRelease && pressed;
            float dt = Time.deltaTime;

            IsFiring = wantsToFire && !IsOverheated && charge > 0f;
            CurrentTarget = null;

            if (IsFiring)
            {
                lastFiredTime = Time.time;
                charge = Mathf.Max(0f, charge - drainPerSecond * dt);
                FireBeam(dt);
                if (charge <= 0f) IsOverheated = true;
            }
            else if (IsOverheated)
            {
                charge = Mathf.Min(maxCharge, charge + maxCharge / overheatDuration * dt);
                if (charge >= maxCharge) IsOverheated = false;
            }
            else if (Time.time - lastFiredTime >= rechargeDelay)
            {
                charge = Mathf.Min(maxCharge, charge + rechargePerSecond * dt);
            }

            beam.enabled = IsFiring;
        }

        private void FireBeam(float dt)
        {
            Vector3 origin = aimOrigin.position;
            Vector3 direction = aimOrigin.forward;
            Vector3 end = origin + direction * range;
            Collider struck = null;
            float nearest = float.PositiveInfinity;

            int count = Physics.RaycastNonAlloc(origin, direction, hits, range, hitMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                // Ignore the player's own colliders.
                if (hits[i].collider.transform.IsChildOf(transform)) continue;
                if (hits[i].distance >= nearest) continue;
                nearest = hits[i].distance;
                end = hits[i].point;
                struck = hits[i].collider;
            }

            if (struck != null)
            {
                Health target = struck.GetComponentInParent<Health>();
                if (target != null && !target.IsDead)
                {
                    target.TakeDamage(damagePerSecond * dt, gameObject);
                    CurrentTarget = target;
                }
            }

            beam.SetPosition(0, muzzle.position);
            beam.SetPosition(1, end);
        }
    }
}
