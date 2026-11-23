using UnityEngine;

namespace SignalLost.NPC
{
    /// <summary>
    /// Brings the desert's locals (Doris, Marshal Mo, Old Gus; 11-22) to life the way <see cref="CavendishIdle"/> does
    /// Cavendish: the head turns to watch the player and nods while they talk, the body breathes, the arms sway (and wave
    /// when talking), and Doris's wings give a flap now and then. Purely visual; DesertNpcBuilder wires the parts, which are
    /// separate objects from the Blender model, each pivoted where it moves.
    /// </summary>
    public sealed class LocalIdle : MonoBehaviour
    {
        [SerializeField] private Transform head;
        [SerializeField] private Transform body;
        [Tooltip("Arms (left, right) or wings: each swings about the NPC's sideways axis.")]
        [SerializeField] private Transform[] limbs = new Transform[0];
        [Tooltip("Wings flap outwards now and then instead of swinging.")]
        [SerializeField] private bool wings;
        [SerializeField] private float watchRange = 12f;
        [SerializeField] private float maxYaw = 50f;
        [SerializeField] private float maxPitch = 18f;

        private Quaternion headBase;
        private Quaternion[] limbBase;
        private Vector3 bodyScale;
        private float yaw, pitch, yawVelocity, pitchVelocity, nextFlap, flapStart = -10f;
        private Transform viewer;
        private NpcDialogue dialogue;

        private void Awake()
        {
            if (head != null) headBase = Quaternion.Inverse(transform.rotation) * head.rotation;
            if (body != null) bodyScale = body.localScale;
            limbBase = new Quaternion[limbs.Length];
            for (int i = 0; i < limbs.Length; i++) if (limbs[i] != null) limbBase[i] = Quaternion.Inverse(transform.rotation) * limbs[i].rotation;
            dialogue = GetComponentInChildren<NpcDialogue>(true);
            nextFlap = Time.time + Random.Range(3f, 7f);
        }

        private void LateUpdate()
        {
            float now = Time.time;
            if (viewer == null && Camera.main != null) viewer = Camera.main.transform;
            bool talking = dialogue != null && dialogue.IsSpeaking;

            float wantYaw = Mathf.Sin(now * 0.21f) * maxYaw * 0.3f, wantPitch = Mathf.Sin(now * 0.29f + 1f) * 4f;
            if (head != null && viewer != null && (viewer.position - head.position).sqrMagnitude < watchRange * watchRange)
            {
                Vector3 local = transform.InverseTransformDirection(viewer.position - head.position);
                wantYaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -maxYaw, maxYaw);
                wantPitch = Mathf.Clamp(-Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg, -maxPitch, maxPitch);
            }
            yaw = Mathf.SmoothDamp(yaw, wantYaw, ref yawVelocity, 0.4f);
            pitch = Mathf.SmoothDamp(pitch, wantPitch, ref pitchVelocity, 0.45f);
            float breath = Mathf.Sin(now * 1.5f);
            float nod = talking ? Mathf.Sin(now * 7f) * 5f : 0f;
            if (head != null) head.rotation = transform.rotation * Quaternion.Euler(pitch + nod + breath, yaw, 0f) * headBase;
            if (body != null) body.localScale = new Vector3(bodyScale.x * (1f + breath * 0.01f), bodyScale.y, bodyScale.z * (1f + breath * 0.01f));

            // A flap: both wings out and back, quickly.
            if (wings && now > nextFlap) { flapStart = now; nextFlap = now + Random.Range(talking ? 1.5f : 4f, talking ? 3f : 9f); }
            float flap = Mathf.Clamp01((now - flapStart) / 0.7f);
            float flapAngle = flap < 1f ? Mathf.Sin(flap * Mathf.PI * 2f) * 35f * (1f - flap) : 0f;

            for (int i = 0; i < limbs.Length; i++)
            {
                if (limbs[i] == null) continue;
                float side = i % 2 == 0 ? 1f : -1f;
                Quaternion move;
                if (wings) move = Quaternion.AngleAxis(flapAngle * side + breath * 2f * side, transform.forward);
                else
                {
                    float swing = Mathf.Sin(now * 1.3f + i * 1.7f) * 4f + (talking ? Mathf.Sin(now * 4.2f + i * 2.1f) * 14f : 0f);
                    move = Quaternion.AngleAxis(swing, transform.right);
                }
                limbs[i].rotation = move * transform.rotation * limbBase[i];
            }
        }
    }
}
