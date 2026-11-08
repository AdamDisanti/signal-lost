using SignalLost.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace SignalLost.Player
{
    /// <summary>
    /// The mechanic's hands, always on screen (meeting notes 10-22): the right hand holds whatever
    /// <see cref="PlayerGear"/> has out (the mining laser or the scanner) and moves with it, so laser swings carry the
    /// hand along; with nothing out it rests as a fist. The left hand rests at the bottom left and, when
    /// <see cref="PlayerFlask"/> takes a sip, fetches the flask from the hip, raises it to the mouth and takes a big
    /// swig with the bottom tipped up and the head tipped back, then puts it away.
    /// The hands are pudgy gloves with orange suit sleeves, built at runtime by <see cref="MechanicModel"/> (the same
    /// hands the pockets menu's mechanic wears), so no art assets are needed. Held things sit in the fist: the fingers
    /// wrap the laser's pistol grip, the scanner's side rail and the flask (<see cref="MechanicModel.Hand.Wrap"/>).
    /// </summary>
    [RequireComponent(typeof(PlayerGear))]
    public sealed class FirstPersonHands : MonoBehaviour
    {
        [Tooltip("Model shown in the left hand while drinking (the AlcoholFlask prefab). Its colliders are removed.")]
        [SerializeField] private GameObject flaskModel;
        [SerializeField] private Color gloveColor = new Color(0.62f, 0.44f, 0.27f); // tan work leather
        [SerializeField] private Color sleeveColor = new Color(0.93f, 0.48f, 0.15f);
        [SerializeField] private Color cuffColor = new Color(0.86f, 0.84f, 0.79f);
        [Tooltip("Size of the hands. The tool grips (MechanicModel.LaserHold / ScannerHold) are measured for 1.2.")]
        [SerializeField, Min(0.1f)] private float handScale = 1.2f;
        [Tooltip("How fast the fingers open and close (full grip per second).")]
        [SerializeField, Min(0.1f)] private float curlSpeed = 6f;
        [Tooltip("How far the head tips back (degrees) at the height of a swig. 0 turns it off.")]
        [SerializeField, Range(0f, 25f)] private float swigHeadTilt = 10f;

        // How far the fingers close (0 = relaxed, 1 = fist).
        private const float FistGrip = 1f, RestGrip = 0.18f;

        // Rest poses in camera space (metres / degrees), just above the HUD status bar so they stay in view.
        private static readonly Vector3 RightRest = new Vector3(0.26f, -0.23f, 0.45f);
        private static readonly Vector3 RightRestAngles = new Vector3(-18f, -16f, 10f);
        private static readonly Vector3 LeftRest = new Vector3(-0.35f, -0.24f, 0.45f);
        private static readonly Vector3 LeftRestAngles = new Vector3(-18f, 18f, -12f);
        private static readonly Vector3 LeftHip = new Vector3(-0.32f, -0.62f, 0.3f); // below the bottom of the screen
        private static readonly Vector3 LeftHipAngles = new Vector3(20f, 25f, -20f);

        // ---------- The flask ----------
        // The flask (prefab units: it stands 0.32 tall, GripAnchor 0.13 up, the neck's screw cap at (-0.037, 0.31))
        // sits in the left fist with its flat face on the palm, the fingers round its edge and its neck out of the
        // thumb side. The whole drink is posed by moving the flask: the hand follows it.
        private const float FlaskScale = 0.45f; // a hip flask, not the display-sized prop
        private const float FlaskWrapRadius = 0.022f, FlaskThumb = 0.7f;
        private static readonly Vector3 FlaskGripPoint = new Vector3(0f, 0.13f, 0f);
        private static readonly Vector3 FlaskNeck = new Vector3(-0.037f, 0.312f, 0f);
        // The flask's axes in the left hand: its length runs toward the thumb, its flat face lies on the palm.
        private static readonly Quaternion FlaskInHand = Quaternion.LookRotation(Vector3.up, Vector3.right);
        private static readonly Vector3 FlaskGripInHand =
            MechanicModel.Hand.WrapCentre(FlaskWrapRadius) + new Vector3(0f, 0f, -FlaskWrapRadius);

        // The drink, in camera space: where the flask's neck travels (hip, past the chest, to the mouth, which sits just
        // below the bottom of the screen) and how far the bottom tips up (degrees above level).
        private static readonly Vector3 NeckAtHip = new Vector3(-0.3f, -0.62f, 0.28f);
        private static readonly Vector3 NeckPastChest = new Vector3(-0.16f, -0.3f, 0.3f);
        private static readonly Vector3 NeckAtMouth = new Vector3(-0.1f, -0.16f, 0.19f);
        // FlaskTurn turns flask and hand about the flask's length so the back of the left fist faces you, on the left.
        private const float TipAtHip = -70f, TipSwig = 48f, TipGulp = 8f, FlaskTurn = 60f;

        // The sip's timeline (fractions of PlayerFlask.DrinkProgress): reach to the hip, raise, swig, lower, hand back.
        private const float ReachEnd = 0.15f, RaiseEnd = 0.38f, SwigEnd = 0.8f, LowerEnd = 0.92f;
        private const float GulpsPerSip = 2f;

        private PlayerGear gear;
        private PlayerFlask flask;
        private FirstPersonController runner;
        private float runWeight;
        private Transform view, rightHand, leftHand, heldFlask;
        private Transform rightAnchor, rightSleeve;
        private Vector3 sleeveRestPosition, sleeveFrontInHand, elbowInHolder, anchorRestPosition;
        private Quaternion sleeveRestRotation;
        private float sleeveHalfLength;
        private MechanicModel.Hand right, left;
        private Material glove, sleeve, cuff;
        private bool leftHolding;
        private Quaternion tiltedView, untiltedView;
        private bool viewTilted;

        public Transform RightHand => rightHand;
        /// <summary>The flask model prefab (the pockets menu shows a copy).</summary>
        public GameObject FlaskModel => flaskModel;
        public Transform LeftHand => leftHand;

        private void Awake()
        {
            gear = GetComponent<PlayerGear>();
            flask = GetComponent<PlayerFlask>();
            runner = GetComponentInParent<FirstPersonController>();
            Camera camera = GetComponentInChildren<Camera>();
            view = camera != null ? camera.transform : transform;
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            // Other islands have their own outfit (11-08); Crash Meadow keeps the colours set here.
            IslandLook island = StageTheme.Current;
            if (island != IslandLook.Meadow)
            {
                MechanicLook outfit = Outfits.Mechanic(island);
                gloveColor = outfit.Glove;
                sleeveColor = outfit.Suit;
                cuffColor = outfit.Trim;
            }
            glove = MakeMaterial(lit, "Glove", gloveColor, 0.25f);
            sleeve = MakeMaterial(lit, "Sleeve", sleeveColor, 0.3f);
            cuff = MakeMaterial(lit, "Cuff", cuffColor, 0.35f);
            right = MechanicModel.BuildHand(view, "Right Hand", 1f, glove, cuff, sleeve);
            left = MechanicModel.BuildHand(view, "Left Hand", -1f, glove, cuff, sleeve);
            right.Curl(FistGrip);
            left.Curl(RestGrip);
            rightHand = right.Root;
            rightSleeve = rightHand.Find("Sleeve");
            if (rightSleeve != null)
            {
                sleeveRestPosition = rightSleeve.localPosition;
                sleeveRestRotation = rightSleeve.localRotation;
                sleeveHalfLength = rightSleeve.localScale.z * 0.5f;
                sleeveFrontInHand = sleeveRestPosition + sleeveRestRotation * new Vector3(0f, 0f, sleeveHalfLength);
            }
            leftHand = left.Root;
            leftHand.localScale = Vector3.one * handScale;
            if (flaskModel != null)
            {
                heldFlask = Instantiate(flaskModel, leftHand, false).transform;
                heldFlask.name = "Held Flask";
                heldFlask.localRotation = FlaskInHand;
                heldFlask.localScale = Vector3.one * FlaskScale;
                heldFlask.localPosition = FlaskGripInHand - FlaskInHand * (FlaskScale * FlaskGripPoint);
                foreach (Collider part in heldFlask.GetComponentsInChildren<Collider>(true)) Destroy(part);
                foreach (MonoBehaviour behaviour in heldFlask.GetComponentsInChildren<MonoBehaviour>(true)) Destroy(behaviour);
                Unshadow(heldFlask);
                heldFlask.gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            Destroy(glove);
            Destroy(sleeve);
            Destroy(cuff);
        }

        private void LateUpdate()
        {
            // Sprinting pumps the arms, except with the scanner up or while the laser charges or swings.
            var laser = gear.InHand == PlayerGear.Held.Laser ? gear.Laser : null;
            bool running = runner != null && runner.IsRunning && gear.InHand != PlayerGear.Held.Scanner &&
                           (laser == null || (!laser.IsCharging && !laser.IsSwinging));
            runWeight = Mathf.MoveTowards(runWeight, running ? 1f : 0f, Time.deltaTime * (running ? 4f : 7f));
            PoseRightHand();
            float swig = PoseLeftHand(flask != null ? flask.DrinkProgress : 0f);
            TiltHead(swig * swigHeadTilt);
        }

        // ---------- Right hand ----------

        // On the tool that is out (wrapped round its grip), or resting as a fist.
        private void PoseRightHand()
        {
            Transform anchor = view;
            MechanicModel.ToolHold hold = default;
            if (gear.InHand == PlayerGear.Held.Laser && gear.Laser != null && gear.Laser.HeldTool != null)
            {
                anchor = gear.Laser.HeldTool;
                hold = MechanicModel.LaserHold;
            }
            else if (gear.InHand == PlayerGear.Held.Scanner && gear.Scanner != null && gear.Scanner.Visual != null)
            {
                anchor = gear.Scanner.Visual.transform;
                hold = MechanicModel.ScannerHold;
            }
            if (anchor != rightAnchor)
            {
                rightAnchor = anchor;
                rightHand.SetParent(anchor, false);
                rightHand.localScale = Vector3.one * handScale / Mathf.Max(0.001f, anchor.lossyScale.x / view.lossyScale.x);
                if (anchor != view) right.Wrap(hold.Radius, hold.Thumb);
            }
            if (anchor == view)
            {
                RunPose(1f, RightRest, Quaternion.Euler(RightRestAngles), out Vector3 at, out Quaternion turn);
                rightHand.localPosition = at;
                rightHand.localRotation = turn;
                right.Curl(Mathf.MoveTowards(right.Grip < 0f ? 0.6f : right.Grip, FistGrip, curlSpeed * Time.deltaTime));
            }
            else
            {
                rightHand.localPosition = hold.HandPosition;
                rightHand.localRotation = hold.HandRotation;
            }
            PoseRightSleeve(anchor);
        }

        // The forearm sleeve stays with the wrist; its elbow end follows the tool's movement but not its turning, so in
        // swings and slams the arm lifts and drives the laser while only the wrist turns it.
        private void PoseRightSleeve(Transform anchor)
        {
            if (rightSleeve == null) return;
            rightSleeve.localPosition = sleeveRestPosition;
            rightSleeve.localRotation = sleeveRestRotation;
            Transform holder = anchor != view ? anchor.parent : null;
            if (holder == null) { pinnedFor = null; return; }
            Vector3 front = rightHand.TransformPoint(sleeveFrontInHand);
            if (anchor != pinnedFor)
            {
                // Pinned when the tool comes out (it is at rest then; swings start later).
                pinnedFor = anchor;
                Vector3 back = rightHand.TransformPoint(sleeveRestPosition - sleeveRestRotation * new Vector3(0f, 0f, sleeveHalfLength));
                elbowInHolder = holder.InverseTransformPoint(back);
                anchorRestPosition = anchor.localPosition;
            }
            // In the slam the elbow rides along with the tool's lift and thrust so the whole arm drives it; in the lighter
            // swings it stays pinned and the forearm pivots from it.
            var laser = gear != null ? gear.Laser : null;
            bool slamming = laser != null && laser.IsSwinging && laser.ComboLength > 1 && laser.ComboStep == laser.ComboLength;
            slamFollow = slamming ? 1f : Mathf.MoveTowards(slamFollow, 0f, 4f * Time.deltaTime);
            // Sprinting pumps the whole arm too, so the elbow rides along with that as well.
            float follow = Mathf.Max(slamFollow, laser != null ? laser.RunWeight : 0f);
            Vector3 elbow = holder.TransformPoint(elbowInHolder + follow * (anchor.localPosition - anchorRestPosition));
            Vector3 along = front - elbow;
            if (along.sqrMagnitude < 1e-6f) return;
            float half = sleeveHalfLength * rightHand.lossyScale.z;
            rightSleeve.rotation = Quaternion.LookRotation(along, rightHand.up);
            rightSleeve.position = front - along.normalized * half;
        }

        private Transform pinnedFor;
        private float slamFollow;

        // Running: the empty hand (side 1 right, -1 left) swings with the whole arm about its shoulder.
        private void RunPose(float side, Vector3 rest, Quaternion restRotation, out Vector3 at, out Quaternion turn)
        {
            at = rest;
            turn = restRotation;
            if (runWeight <= 0f || runner == null) return;
            Vector3 shoulder = FirstPersonController.RightShoulder;
            shoulder.x *= side;
            Quaternion arc = Quaternion.Slerp(Quaternion.identity, runner.ArmSwing(side), Mathf.SmoothStep(0f, 1f, runWeight));
            at = shoulder + arc * (rest - shoulder);
            turn = arc * restRotation;
        }

        // ---------- Left hand and the drink ----------

        // Left hand (meeting notes 10-23, reworked 11-07): reach down to the hip for the flask, raise it to the mouth,
        // take a big swig with the bottom tipped up (the heal lands here), lower it out of view and come back up empty.
        // Returns how far into the swig the drinker is (0-1), for the head tilt.
        private float PoseLeftHand(float t)
        {
            if (t <= 0f || t >= 1f) // resting
            {
                SetLeftHolding(false);
                RunPose(-1f, LeftRest, Quaternion.Euler(LeftRestAngles), out Vector3 at, out Quaternion turn);
                PoseLeftEmpty(at, turn, RestGrip);
                return 0f;
            }
            if (t < ReachEnd) // reach down to the hip
            {
                SetLeftHolding(false);
                float k = Mathf.SmoothStep(0f, 1f, t / ReachEnd);
                PoseLeftEmpty(Vector3.Lerp(LeftRest, LeftHip, k), Vector3.Lerp(LeftRestAngles, LeftHipAngles, k), RestGrip);
                return 0f;
            }
            if (t >= LowerEnd) // empty hand back up
            {
                SetLeftHolding(false);
                float k = Mathf.SmoothStep(0f, 1f, (t - LowerEnd) / (1f - LowerEnd));
                PoseLeftEmpty(Vector3.Lerp(LeftHip, LeftRest, k), Vector3.Lerp(LeftHipAngles, LeftRestAngles, k), RestGrip);
                return 0f;
            }

            SetLeftHolding(true);
            float raise, gulps = 0f;
            if (t < RaiseEnd) raise = (t - ReachEnd) / (RaiseEnd - ReachEnd);
            else if (t < SwigEnd) { raise = 1f; gulps = GulpsPerSip * (t - RaiseEnd) / (SwigEnd - RaiseEnd); }
            else raise = 1f - (t - SwigEnd) / (LowerEnd - SwigEnd);
            return PoseDrink(raise, gulps);
        }

        /// <summary>
        /// The drinking pose from two numbers, so something other than the timed sip can drive it (a hold-to-chug
        /// would keep <paramref name="raise"/> at 1 for as long as the button is held, counting up
        /// <paramref name="gulps"/>): <paramref name="raise"/> brings the flask from the hip (0) to the mouth (1), and
        /// each whole number of <paramref name="gulps"/> is one swallow, tipping the bottom up a little further and
        /// back. Returns how far the head is tipped back (0-1).
        /// </summary>
        private float PoseDrink(float raise, float gulps)
        {
            float e = Mathf.SmoothStep(0f, 1f, raise);
            // Along a curve from the hip, past the chest, to the mouth.
            Vector3 neck = (1f - e) * (1f - e) * NeckAtHip + 2f * (1f - e) * e * NeckPastChest + e * e * NeckAtMouth;
            float gulp = 0.5f - 0.5f * Mathf.Cos(gulps * 2f * Mathf.PI);
            float tip = Mathf.LerpUnclamped(TipAtHip, TipSwig, e) + TipGulp * gulp * e;
            neck += new Vector3(0f, 0.006f, -0.004f) * gulp * e;
            HoldFlask(neck, tip);
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 1f, raise));
        }

        // Puts the left hand wherever it must be for the flask's neck to be at 'neck' (camera space) with its bottom
        // tipped up 'tip' degrees above level.
        private void HoldFlask(Vector3 neck, float tip)
        {
            float a = tip * Mathf.Deg2Rad;
            var length = new Vector3(0f, -Mathf.Sin(a), -Mathf.Cos(a)); // bottom to neck: down and back toward the mouth
            Quaternion flaskRotation = Quaternion.AngleAxis(FlaskTurn, length) * Quaternion.LookRotation(Vector3.left, length);
            float flaskScale = FlaskScale * handScale;
            Vector3 flaskRoot = neck - flaskRotation * (flaskScale * FlaskNeck);
            Quaternion handRotation = flaskRotation * Quaternion.Inverse(FlaskInHand);
            Vector3 flaskInHand = FlaskGripInHand - FlaskInHand * (FlaskScale * FlaskGripPoint);
            leftHand.localRotation = handRotation;
            leftHand.localPosition = flaskRoot - handRotation * (handScale * flaskInHand);
        }

        private void PoseLeftEmpty(Vector3 position, Vector3 angles, float grip) =>
            PoseLeftEmpty(position, Quaternion.Euler(angles), grip);

        private void PoseLeftEmpty(Vector3 position, Quaternion rotation, float grip)
        {
            leftHand.localPosition = position;
            leftHand.localRotation = rotation;
            left.Curl(Mathf.MoveTowards(left.Grip < 0f ? grip : left.Grip, grip, curlSpeed * Time.deltaTime));
        }

        // The flask is picked up and put away at the hip, out of view.
        private void SetLeftHolding(bool holding)
        {
            if (holding == leftHolding) return;
            leftHolding = holding;
            if (heldFlask != null) heldFlask.gameObject.SetActive(holding);
            if (holding) left.Wrap(FlaskWrapRadius, FlaskThumb);
            else left.Curl(RestGrip);
        }

        // Tips the camera back by 'degrees' on top of wherever the player is looking, and takes it off again
        // afterwards (only if nothing else has turned the camera in between, so mouse look is never fought).
        private void TiltHead(float degrees)
        {
            if (viewTilted && view.localRotation == tiltedView) view.localRotation = untiltedView;
            viewTilted = degrees > 0.01f && view != transform;
            if (!viewTilted) return;
            untiltedView = view.localRotation;
            tiltedView = untiltedView * Quaternion.Euler(-degrees, 0f, 0f);
            view.localRotation = tiltedView;
        }

        private static void Unshadow(Transform root)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        private static Material MakeMaterial(Shader shader, string name, Color color, float smoothness)
        {
            var material = new Material(shader) { name = name + " (hands)" };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            return material;
        }
    }
}
