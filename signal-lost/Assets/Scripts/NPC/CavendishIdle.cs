using UnityEngine;

namespace SignalLost.NPC
{
    /// <summary>
    /// Brings the capybara Cavendish to life: he turns his head to watch the player inside his helmet (with a curious
    /// tilt now and then), blinks, flicks his ears, breathes, and his antenna light pulses. Purely visual; the prefab
    /// built by CavendishCapybaraBuilder wires the parts.
    /// </summary>
    public sealed class CavendishIdle : MonoBehaviour
    {
        [SerializeField] private Transform head;
        [SerializeField] private Transform body;
        [SerializeField] private Transform[] ears = new Transform[2];  // left, right
        [SerializeField] private Transform[] eyes = new Transform[2];
        [SerializeField] private Transform antennaLight;
        [SerializeField, Tooltip("How far away he notices the player, metres.")] private float watchRange = 14f;
        [SerializeField, Tooltip("Furthest he turns his head inside the helmet, degrees.")] private float maxYaw = 18f;
        [SerializeField] private float maxPitch = 10f;

        private Quaternion headBase;
        private Quaternion[] earBase;
        private Vector3[] eyeBase;
        private Vector3 bodyScale, lightScale;
        private float yaw, pitch, roll, yawVelocity, pitchVelocity, rollVelocity;
        private float nextBlink, blinkStart = -1f, nextTilt, tiltUntil, tiltSign = 1f;
        private float[] earTwitch = { -1f, -1f };
        private float nextEar;
        private Transform viewer;

        private void Awake()
        {
            if (head != null) headBase = Quaternion.Inverse(transform.rotation) * head.rotation;
            if (body != null) bodyScale = body.localScale;
            earBase = new Quaternion[ears.Length];
            for (int i = 0; i < ears.Length; i++) if (ears[i] != null) earBase[i] = ears[i].localRotation;
            eyeBase = new Vector3[eyes.Length];
            for (int i = 0; i < eyes.Length; i++) if (eyes[i] != null) eyeBase[i] = eyes[i].localScale;
            if (antennaLight != null) lightScale = antennaLight.localScale;
            float now = Time.time;
            nextBlink = now + Random.Range(1f, 3f);
            nextTilt = now + Random.Range(3f, 6f);
            nextEar = now + Random.Range(2f, 4f);
        }

        private void LateUpdate()
        {
            float now = Time.time;
            if (viewer == null && Camera.main != null) viewer = Camera.main.transform;

            // Watch the player, or gaze about slowly when there is nobody near.
            float wantYaw = Mathf.Sin(now * 0.23f) * maxYaw * 0.5f, wantPitch = Mathf.Sin(now * 0.31f + 1f) * 3f;
            if (head != null && viewer != null && (viewer.position - head.position).sqrMagnitude < watchRange * watchRange)
            {
                Vector3 local = transform.InverseTransformDirection(viewer.position - head.position);
                wantYaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -maxYaw, maxYaw);
                wantPitch = Mathf.Clamp(-Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg, -maxPitch, maxPitch);
            }
            // A curious tilt of the head every so often.
            if (now > nextTilt)
            {
                tiltUntil = now + Random.Range(1.2f, 2.4f);
                tiltSign = Random.value < 0.5f ? -1f : 1f;
                nextTilt = now + Random.Range(4f, 8f);
            }
            float wantRoll = now < tiltUntil ? 11f * tiltSign : 0f;
            yaw = Mathf.SmoothDamp(yaw, wantYaw, ref yawVelocity, 0.35f);
            pitch = Mathf.SmoothDamp(pitch, wantPitch, ref pitchVelocity, 0.4f);
            roll = Mathf.SmoothDamp(roll, wantRoll, ref rollVelocity, 0.3f);
            float breath = Mathf.Sin(now * 1.6f);
            if (head != null)
                head.rotation = transform.rotation * Quaternion.Euler(pitch + breath * 0.8f, yaw, roll) * headBase;

            // Breathing.
            if (body != null) body.localScale = Scaled(bodyScale, UpAxis(body), 1f + breath * 0.012f);

            // Blink: a quick squash of both eyes, sometimes twice.
            if (now > nextBlink && blinkStart < 0f) blinkStart = now;
            float squash = 1f;
            if (blinkStart >= 0f)
            {
                float t = (now - blinkStart) / 0.16f;
                squash = t < 1f ? Mathf.Lerp(1f, 0.1f, 1f - Mathf.Abs(t * 2f - 1f)) : 1f;
                if (t >= 1f)
                {
                    blinkStart = -1f;
                    nextBlink = now + (Random.value < 0.2f ? 0.18f : Random.Range(2.2f, 5.5f));
                }
            }
            for (int i = 0; i < eyes.Length; i++)
                if (eyes[i] != null) eyes[i].localScale = Scaled(eyeBase[i], UpAxis(eyes[i]), squash);

            // Ear flicks: one ear, or both, flick out and back.
            if (now > nextEar)
            {
                int which = Random.Range(0, 3);
                for (int i = 0; i < 2; i++) if (which == 2 || which == i) earTwitch[i] = now;
                nextEar = now + Random.Range(2.5f, 6.5f);
            }
            for (int i = 0; i < ears.Length && i < 2; i++)
            {
                if (ears[i] == null) continue;
                float a = 0f;
                if (earTwitch[i] >= 0f)
                {
                    float t = (now - earTwitch[i]) / 0.3f;
                    if (t >= 1f) earTwitch[i] = -1f;
                    else a = Mathf.Sin(t * Mathf.PI) * 28f;
                }
                Vector3 axis = ears[i].parent.InverseTransformDirection(transform.forward);
                ears[i].localRotation = Quaternion.AngleAxis(a * (i == 0 ? 1f : -1f), axis) * earBase[i];
            }

            // The antenna light pulses.

            if (antennaLight != null) antennaLight.localScale = lightScale * (1f + 0.35f * Mathf.Max(0f, Mathf.Sin(now * 4f)));
        }

        // Which of a part's own axes points up (the model's parts keep the axes they had in Blender).
        private int UpAxis(Transform part)
        {
            Vector3 up = part.InverseTransformDirection(transform.up);
            up = new Vector3(Mathf.Abs(up.x), Mathf.Abs(up.y), Mathf.Abs(up.z));
            return up.x > up.y && up.x > up.z ? 0 : up.y > up.z ? 1 : 2;
        }

        private static Vector3 Scaled(Vector3 scale, int axis, float factor)
        {
            scale[axis] *= factor;
            return scale;
        }
    }
}
