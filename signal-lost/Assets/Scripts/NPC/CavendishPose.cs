using SignalLost.World;
using UnityEngine;

namespace SignalLost.NPC
{
    /// <summary>
    /// Lets the four-legged capybara Cavendish (built by CavendishCapybaraBuilder into CapybaraAstronaut.prefab) work with
    /// every scene and script written for the old upright, unrigged model. Those lower his "Visual" object to seat him on
    /// a log or a boat's thwart (IslandCamp.SetCavendishDrop, the tutorial, the voyage). His legs are part of his body
    /// mesh, so instead of sinking into the seat he lies down like a resting capybara:
    /// <list type="bullet">
    /// <item><b>At a camp:</b> he lies on the ground just behind his log, his head and helmet clear of it, facing the fire.
    /// His legs sink out of sight, so he rests on his belly.</item>
    /// <item><b>On the boat:</b> he stands on the floorboards a little forward of his thwart, clear of the mechanic at
    /// the tiller.</item>
    /// <item><b>Standing</b> (Visual not lowered): exactly as modelled.</item>
    /// </list>
    /// It also keeps his collider and his speech-bubble anchors ("DialogueAnchor", "Cavendish Dialogue") over him, wherever
    /// the pose puts him. All visual: the root never moves, so quests, seats and voyages are unchanged.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CavendishPose : MonoBehaviour
    {
        /// <summary>A Visual lowered by more than this counts as seated.</summary>
        public const float SeatedBelow = 0.25f;

        [SerializeField] private Transform visual;
        [Tooltip("The model, under Visual. The pose moves this, never Visual itself (other scripts own Visual).")]
        [SerializeField] private Transform model;
        [Tooltip("How far his legs sink into the ground when he lies down (from his feet up to just under his belly).")]
        [SerializeField, Min(0f)] private float legSink = 0.24f;
        [Tooltip("At a camp: how far behind his seat he lies, so his head and helmet clear the log.")]
        [SerializeField, Min(0f)] private float lieBehind = 1.55f;
        [Tooltip("On the boat: how far forward of his thwart he stands, clear of the mechanic at the tiller.")]
        [SerializeField] private float boatForward = 0.45f;
        [Tooltip("From his root to the front of his helmet, standing (for cutscenes that bring his face up to something).")]
        [SerializeField, Min(0f)] private float reach = 1.35f;
        [Tooltip("Where the speech bubble goes when standing: just above his helmet.")]
        [SerializeField] private Vector3 dialoguePoint = new Vector3(0f, 1.95f, -0.75f);

        private BoxCollider box;
        private Vector3 boxCentre;
        private Transform[] anchors;
        private Vector3 current, target;
        private bool started, wasSeated;
        private float groundFix, nextGroundCheck;

        /// <summary>From his root to the front of his helmet when standing, in metres (scaled with him).</summary>
        public float Reach => reach * transform.lossyScale.z;

        /// <summary>From Cavendish's root to the front of his face; 0 for a model without this component.</summary>
        public static float ReachOf(Component cavendish)
        {
            var pose = cavendish != null ? cavendish.GetComponentInChildren<CavendishPose>() : null;
            return pose != null ? pose.Reach : 0f;
        }

        private void Awake()
        {
            if (visual == null) visual = transform.Find("Visual");
            if (model == null && visual != null) model = visual.Find("Model");
            box = GetComponent<BoxCollider>();
            if (box != null) boxCentre = box.center;
            anchors = new[] { transform.Find("DialogueAnchor"), transform.Find("Cavendish Dialogue") };
        }

        private void OnEnable() => started = false;

        private void LateUpdate() => Apply(false);

        /// <summary>Poses him now, without easing (also used by the editor's check shots).</summary>
        public void ApplyNow()
        {
            if (visual == null || model == null || anchors == null) Awake();
            started = false;
            nextGroundCheck = 0f;
            Apply(true);
        }

        private void Apply(bool instant)
        {
            if (visual == null || model == null) return;
            float drop = -visual.localPosition.y;
            bool seated = drop > SeatedBelow;
            bool onBoat = GetComponentInParent<DockBoat>() != null;

            target = Vector3.zero;
            if (seated)
            {
                // Seated scripts put Visual's origin about at floor level (the old model's seat drop); lying down puts
                // his belly on that floor, with the legs out of sight below it.
                Vector3 shift = onBoat ? Vector3.forward * boatForward : Vector3.back * lieBehind;
                if (onBoat)
                {
                    // On the boat's floorboards: the old seat drop leaves his feet about on them, so he simply stands there.
                    target = shift;
                }
                else
                {
                    if (!started || seated != wasSeated || Time.time >= nextGroundCheck) groundFix = GroundFix(shift);
                    target = shift + Vector3.up * (groundFix - legSink);
                }
            }
            wasSeated = seated;

            // Getting up for the tutorial's opening, or sitting back down, eases over a moment rather than snapping.
            current = !started || instant ? target : Vector3.MoveTowards(current, target, Time.deltaTime * 3.5f);
            started = true;
            model.localPosition = current;

            Vector3 offset = visual.localPosition + visual.localRotation * Vector3.Scale(visual.localScale, current);
            if (box != null) box.center = boxCentre + offset;
            if (anchors == null) return;
            foreach (Transform anchor in anchors)
                if (anchor != null) anchor.localPosition = dialoguePoint + offset;
        }

        /// <summary>
        /// The ground under where he lies, relative to Visual's origin, so he rests on the real ground behind a log of any
        /// height (only trusted within half a metre of the old seat drop, so a tent or rock under him is ignored).
        /// </summary>
        private float GroundFix(Vector3 shift)
        {
            nextGroundCheck = Time.time + 2f;
            Vector3 spot = visual.TransformPoint(shift);
            float best = 0f, nearest = 0.5f;
            foreach (RaycastHit hit in Physics.RaycastAll(spot + Vector3.up * 1.5f, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(transform)) continue;
                float dy = hit.point.y - spot.y;
                if (Mathf.Abs(dy) < nearest)
                {
                    nearest = Mathf.Abs(dy);
                    best = dy;
                }
            }
            return best;
        }
    }
}
