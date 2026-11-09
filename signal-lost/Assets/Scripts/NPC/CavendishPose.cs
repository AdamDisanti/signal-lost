using UnityEngine;

namespace SignalLost.NPC
{
    /// <summary>
    /// Poses Cavendish's four-legged model (built by CavendishModelBuilder). It sits on his "Visual" object, which the
    /// tutorial, camps and boat already lower to seat him (IslandCamp.SetCavendishDrop). Rather than sinking the body
    /// into the log, a lowered Visual becomes a capybara loaf: the body rests on the seat, the front paws tuck forward
    /// and the hind legs fold away. When his root moves along the ground (walking up a dock, backing away in the
    /// opening), the legs trot in diagonal pairs.
    ///
    /// Anything other code adds straight onto Visual (the island outfits' sun hat and sunglasses) is moved onto the
    /// body, so it follows the pose.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CavendishPose : MonoBehaviour
    {
        /// <summary>A Visual lowered by more than this counts as seated.</summary>
        public const float SeatedBelow = 0.25f;
        /// <summary>How far the body drops from standing to lying on its belly.</summary>
        public const float LoafSink = 0.24f;

        [SerializeField] private Transform body;
        [SerializeField] private Transform[] frontLegs = new Transform[0];
        [SerializeField] private Transform[] hindLegs = new Transform[0];
        [SerializeField, Min(0.1f)] private float stride = 0.75f;
        [SerializeField, Range(0f, 45f)] private float swing = 24f;

        private Vector3 bodyRest;
        private Quaternion[] frontRest, hindRest;
        private Vector3[] frontScale, hindScale;
        private Vector3 lastRoot;
        private float seat, walk, phase;
        private bool started;

        private void Awake()
        {
            if (body == null) body = transform.Find("Body");
            if (body == null) { enabled = false; return; }
            bodyRest = body.localPosition;
            Cache(frontLegs, out frontRest, out frontScale);
            Cache(hindLegs, out hindRest, out hindScale);
        }

        private static void Cache(Transform[] legs, out Quaternion[] rest, out Vector3[] scale)
        {
            rest = new Quaternion[legs.Length];
            scale = new Vector3[legs.Length];
            for (int i = 0; i < legs.Length; i++)
            {
                if (legs[i] == null) continue;
                rest[i] = legs[i].localRotation;
                scale[i] = legs[i].localScale;
            }
        }

        private void LateUpdate()
        {
            AdoptAddOns();

            float drop = -transform.localPosition.y;
            bool seated = drop > SeatedBelow;
            Transform root = transform.parent != null ? transform.parent : transform;
            Vector3 position = root.position;
            float travelled = started ? Vector3.ProjectOnPlane(position - lastRoot, Vector3.up).magnitude : 0f;
            if (travelled > 1.5f) travelled = 0f; // teleported (a cut in a cutscene), not walking
            lastRoot = position;

            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            float speed = travelled / dt;
            if (!started) { seat = seated ? 1f : 0f; started = true; }
            seat = Mathf.MoveTowards(seat, seated ? 1f : 0f, dt * 4f);
            walk = Mathf.MoveTowards(walk, !seated && speed > 0.15f ? 1f : 0f, dt * 5f);
            phase += travelled / stride * Mathf.PI * 2f;

            // Seated: raise the body back up from the lowered Visual so the belly rests on the seat.
            body.localPosition = bodyRest + Vector3.up * (seat * Mathf.Max(0f, drop - LoafSink));

            float trot = Mathf.Sin(phase) * swing * walk;
            for (int i = 0; i < frontLegs.Length; i++)
            {
                if (frontLegs[i] == null) continue;
                float pair = i == 0 ? trot : -trot; // front right with hind left, front left with hind right
                frontLegs[i].localRotation = frontRest[i] * Quaternion.Euler(-75f * seat + pair, 0f, 0f);
                frontLegs[i].localScale = Vector3.Scale(frontScale[i], new Vector3(1f, Mathf.Lerp(1f, 0.7f, seat), 1f));
            }
            for (int i = 0; i < hindLegs.Length; i++)
            {
                if (hindLegs[i] == null) continue;
                float pair = i == 0 ? -trot : trot;
                hindLegs[i].localRotation = hindRest[i] * Quaternion.Euler(pair * (1f - seat), 0f, 0f);
                hindLegs[i].localScale = Vector3.Scale(hindScale[i], new Vector3(1f, Mathf.Lerp(1f, 0.2f, seat), 1f));
            }
        }

        /// <summary>Moves anything added straight onto Visual (outfit pieces) onto the body, keeping where it is.</summary>
        private void AdoptAddOns()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child != body) child.SetParent(body, true);
            }
        }
    }
}
