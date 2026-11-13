using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SignalLost.Mudlands
{
    /// <summary>
    /// The third-person camera while driving: it swings in from the mechanic's eyes, then hangs behind and above the
    /// car, following where it is going rather than where it points (so drifts read), widening its field of view with
    /// speed. It is the main camera while it exists, so CameraShake shakes it. Mouse look swings it round a little and
    /// it drifts back.
    /// </summary>
    public sealed class ChaseCamera : MonoBehaviour
    {
        private DrunkCar car;
        private Camera cam;
        private Vector3 heading;
        private float born, orbit, baseFov;
        private Vector3 fromPosition;
        private Quaternion fromRotation;

        public static ChaseCamera Create(Camera playerView, DrunkCar car)
        {
            var go = new GameObject("Drunk Driving Camera");
            var chase = go.AddComponent<ChaseCamera>();
            chase.cam = go.AddComponent<Camera>();
            chase.cam.CopyFrom(playerView);
            chase.cam.nearClipPlane = 0.1f;
            go.tag = "MainCamera";
            var data = chase.cam.GetUniversalAdditionalCameraData();
            var source = playerView.GetUniversalAdditionalCameraData();
            if (data != null && source != null) data.renderPostProcessing = source.renderPostProcessing;
            chase.car = car;
            chase.baseFov = playerView.fieldOfView;
            chase.fromPosition = playerView.transform.position;
            chase.fromRotation = playerView.transform.rotation;
            go.transform.SetPositionAndRotation(chase.fromPosition, chase.fromRotation);
            chase.heading = car.transform.forward;
            chase.born = Time.time;
            return chase;
        }

        /// <summary>The ride is over: hand back to the player's eyes.</summary>
        public void Release() => Destroy(gameObject);

        private void LateUpdate()
        {
            if (car == null) return;
            Vector3 velocity = car.Velocity;
            velocity.y = 0f;
            Vector3 facing = car.transform.forward;
            facing.y = 0f;
            Vector3 want = velocity.magnitude > 3f && Vector3.Dot(velocity, facing) > 0f ? velocity.normalized : facing.normalized;
            heading = Vector3.Slerp(heading, want, Time.deltaTime * 3.5f);

            if (UnityEngine.InputSystem.Mouse.current != null && Cursor.lockState == CursorLockMode.Locked)
                orbit += UnityEngine.InputSystem.Mouse.current.delta.ReadValue().x * 0.15f;
            orbit = Mathf.Clamp(Mathf.Lerp(orbit, 0f, Time.deltaTime * 1.2f), -110f, 110f);

            Vector3 back = Quaternion.Euler(0f, orbit, 0f) * -heading;
            float speed01 = Mathf.Clamp01(car.Speed / 24f);
            Vector3 target = car.transform.position + back * Mathf.Lerp(6.5f, 8f, speed01) + Vector3.up * Mathf.Lerp(2.7f, 3.1f, speed01);
            // Don't let scenery come between the camera and the car.
            Vector3 focus = car.transform.position + Vector3.up * 1.3f;
            if (Physics.SphereCast(focus, 0.3f, (target - focus).normalized, out RaycastHit hit, (target - focus).magnitude, ~0, QueryTriggerInteraction.Ignore) &&
                !hit.collider.transform.IsChildOf(car.transform) && hit.collider.GetComponentInParent<SignalLost.Combat.Health>() == null // creatures move too much to block it
                && hit.collider.GetComponentInParent<Destructible>() == null)
                target = hit.point + hit.normal * 0.3f;
            Quaternion look = Quaternion.LookRotation(focus + heading * 3f - target);

            float swing = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.time - born) / 0.45f));
            Vector3 position = Vector3.Lerp(fromPosition, target, swing);
            transform.SetPositionAndRotation(Vector3.Lerp(transform.position, position, swing < 1f ? 1f : Time.deltaTime * 10f),
                Quaternion.Slerp(fromRotation, look, swing));
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, baseFov + 16f * speed01, Time.deltaTime * 3f);
        }
    }
}
