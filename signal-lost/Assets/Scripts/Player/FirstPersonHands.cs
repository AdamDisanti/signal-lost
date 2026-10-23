using UnityEngine;
using UnityEngine.Rendering;

namespace SignalLost.Player
{
    /// <summary>
    /// The mechanic's hands, always on screen (meeting notes 10-22): the right hand holds whatever
    /// <see cref="PlayerGear"/> has out (the mining laser or the scanner) and moves with it, so laser swings carry the
    /// hand along; with nothing out it rests as a fist. The left hand rests at the bottom left and, when
    /// <see cref="PlayerFlask"/> takes a sip, grabs the flask, drinks quickly and puts it away.
    /// The hands are chunky low-poly gloves and orange suit sleeves built at runtime from boxes, so no art assets are
    /// needed; tweak the poses and colours below.
    /// </summary>
    [RequireComponent(typeof(PlayerGear))]
    public sealed class FirstPersonHands : MonoBehaviour
    {
        [Tooltip("Model shown in the left hand while drinking (the AlcoholFlask prefab). Its colliders are removed.")]
        [SerializeField] private GameObject flaskModel;
        [SerializeField] private Color gloveColor = new Color(0.62f, 0.44f, 0.27f); // tan work leather
        [SerializeField] private Color sleeveColor = new Color(0.93f, 0.48f, 0.15f);
        [SerializeField] private Color cuffColor = new Color(0.86f, 0.84f, 0.79f);
        [Tooltip("Size of the hands; the held tools are drawn a little larger than life, so the hands are too.")]
        [SerializeField, Min(0.1f)] private float handScale = 1.2f;

        // Poses in camera space (metres / degrees). They sit just above the HUD status bar so they stay in view.
        private static readonly Vector3 RightRest = new Vector3(0.26f, -0.23f, 0.45f);
        private static readonly Vector3 RightRestAngles = new Vector3(-18f, -16f, 10f);
        private static readonly Vector3 LeftRest = new Vector3(-0.27f, -0.23f, 0.45f);
        private static readonly Vector3 LeftRestAngles = new Vector3(-18f, 18f, -12f);
        private static readonly Vector3 LeftDrink = new Vector3(-0.07f, -0.15f, 0.3f);
        private static readonly Vector3 LeftHip = new Vector3(-0.32f, -0.62f, 0.3f); // below the bottom of the screen
        private static readonly Vector3 LeftHipAngles = new Vector3(20f, 25f, -20f);
        private static readonly Vector3 LeftDrinkAngles = new Vector3(-70f, 10f, -15f);
        // Where the right hand sits on each tool (relative to the laser's grip / the scanner's visual root).
        // The laser's real grip sits below the screen edge, so the hand holds the body further forward, on its inner
        // side (toward the screen centre), where it can be seen.
        private static readonly Vector3 LaserGrip = new Vector3(-0.06f, 0.04f, 0.15f);
        private static readonly Vector3 LaserGripAngles = new Vector3(0f, 0f, -75f);
        private static readonly Vector3 ScannerGrip = new Vector3(0f, -0.06f, -0.02f);
        private static readonly Vector3 ScannerGripAngles = new Vector3(-5f, 0f, 0f);

        private PlayerGear gear;
        private PlayerFlask flask;
        private Transform view, rightHand, leftHand, heldFlask;
        private Transform rightAnchor;
        private Material glove, sleeve, cuff;

        public Transform RightHand => rightHand;
        /// <summary>The flask model prefab (the pockets menu shows a copy).</summary>
        public GameObject FlaskModel => flaskModel;
        public Transform LeftHand => leftHand;

        private void Awake()
        {
            gear = GetComponent<PlayerGear>();
            flask = GetComponent<PlayerFlask>();
            Camera camera = GetComponentInChildren<Camera>();
            view = camera != null ? camera.transform : transform;
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            glove = MakeMaterial(lit, "Glove", gloveColor, 0.25f);
            sleeve = MakeMaterial(lit, "Sleeve", sleeveColor, 0.3f);
            cuff = MakeMaterial(lit, "Cuff", cuffColor, 0.35f);
            rightHand = BuildHand("Right Hand", 1f, true);
            leftHand = BuildHand("Left Hand", -1f, false);
            leftHand.SetParent(view, false);
            leftHand.localScale = Vector3.one * handScale;
            if (flaskModel != null)
            {
                heldFlask = Instantiate(flaskModel, leftHand, false).transform;
                heldFlask.name = "Held Flask";
                heldFlask.localPosition = new Vector3(0f, 0.02f, 0.03f);
                heldFlask.localRotation = Quaternion.Euler(0f, 90f, 0f);
                heldFlask.localScale = Vector3.one * 0.45f; // a hip flask, not the display-sized prop
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
            // Right hand: on the tool that is out, or resting.
            Transform anchor = view;
            Vector3 position = RightRest, angles = RightRestAngles;
            if (gear.InHand == PlayerGear.Held.Laser && gear.Laser != null && gear.Laser.HeldTool != null)
            {
                anchor = gear.Laser.HeldTool;
                position = LaserGrip;
                angles = LaserGripAngles;
            }
            else if (gear.InHand == PlayerGear.Held.Scanner && gear.Scanner != null && gear.Scanner.Visual != null)
            {
                anchor = gear.Scanner.Visual.transform;
                position = ScannerGrip;
                angles = ScannerGripAngles;
            }
            if (anchor != rightAnchor)
            {
                rightAnchor = anchor;
                rightHand.SetParent(anchor, false);
                rightHand.localScale = Vector3.one * handScale / Mathf.Max(0.001f, anchor.lossyScale.x / view.lossyScale.x);
            }
            rightHand.localPosition = position;
            rightHand.localRotation = Quaternion.Euler(angles);

            // Left hand (meeting notes 10-23): reach down to the hip for the flask, snap it up to the mouth, tip it
            // back for a swig (the heal lands here), then drop it back down out of view and come back up empty.
            float t = flask != null ? flask.DrinkProgress : 0f;
            Vector3 leftPosition = LeftRest, leftAngles = LeftRestAngles;
            float tip = 0f;
            if (t > 0f && t < 0.25f) // reach down
            {
                float k = Mathf.SmoothStep(0f, 1f, t / 0.25f);
                leftPosition = Vector3.Lerp(LeftRest, LeftHip, k);
                leftAngles = Vector3.Lerp(LeftRestAngles, LeftHipAngles, k);
            }
            else if (t >= 0.25f && t < 0.4f) // aggressive snap up, overshooting a little
            {
                float k = OvershootOut((t - 0.25f) / 0.15f);
                leftPosition = Vector3.LerpUnclamped(LeftHip, LeftDrink, k);
                leftAngles = Vector3.LerpUnclamped(LeftHipAngles, LeftDrinkAngles, k);
            }
            else if (t >= 0.4f && t < 0.75f) // swig
            {
                leftPosition = LeftDrink;
                leftAngles = LeftDrinkAngles;
                tip = Mathf.Sin((t - 0.4f) / 0.35f * Mathf.PI);
            }
            else if (t >= 0.75f && t < 0.9f) // stow it back at the hip
            {
                float k = Mathf.SmoothStep(0f, 1f, (t - 0.75f) / 0.15f);
                leftPosition = Vector3.Lerp(LeftDrink, LeftHip, k);
                leftAngles = Vector3.Lerp(LeftDrinkAngles, LeftHipAngles, k);
            }
            else if (t >= 0.9f) // empty hand back up
            {
                float k = Mathf.SmoothStep(0f, 1f, (t - 0.9f) / 0.1f);
                leftPosition = Vector3.Lerp(LeftHip, LeftRest, k);
                leftAngles = Vector3.Lerp(LeftHipAngles, LeftRestAngles, k);
            }
            leftHand.localPosition = leftPosition;
            leftHand.localRotation = Quaternion.Euler(leftAngles) * Quaternion.Euler(-40f * tip, 0f, 0f);
            // The flask is picked up and put away while the hand is down out of view.
            if (heldFlask != null) heldFlask.gameObject.SetActive(t > 0.2f && t < 0.88f);
        }

        // Eases out past 1 and settles back: a quick, forceful motion.
        private static float OvershootOut(float x)
        {
            x = Mathf.Clamp01(x) - 1f;
            const float s = 1.6f;
            return 1f + x * x * ((s + 1f) * x + s);
        }

        // ---------- The hand model ----------

        // A mitten-like low-poly glove: palm, four fingers, thumb, cuff and a sleeve running off the bottom of the screen.
        private Transform BuildHand(string name, float side, bool gripping)
        {
            var root = new GameObject(name).transform;
            root.SetParent(view, false);
            Part(root, "Palm", glove, new Vector3(0f, -0.005f, -0.02f), new Vector3(0.075f, 0.04f, 0.085f), Vector3.zero);
            for (int i = 0; i < 4; i++)
            {
                float x = (i - 1.5f) * 0.019f;
                // Gripping fingers curl down around the handle; resting ones stay half open.
                Part(root, "Finger " + (i + 1), glove, new Vector3(x, gripping ? -0.03f : -0.012f, gripping ? 0.03f : 0.042f),
                    new Vector3(0.017f, 0.022f, gripping ? 0.03f : 0.045f), new Vector3(gripping ? 70f : 25f, 0f, 0f));
            }
            Part(root, "Thumb", glove, new Vector3(-0.045f * side, 0.012f, 0.012f), new Vector3(0.02f, 0.02f, 0.045f), new Vector3(10f, 25f * side, 0f));
            Part(root, "Cuff", cuff, new Vector3(0f, 0f, -0.085f), new Vector3(0.088f, 0.058f, 0.05f), Vector3.zero);
            Part(root, "Sleeve", sleeve, new Vector3(0.015f * side, -0.05f, -0.3f), new Vector3(0.1f, 0.075f, 0.42f), new Vector3(-12f, 4f * side, 0f));
            Part(root, "Sleeve Stripe", cuff, new Vector3(0.015f * side, -0.012f, -0.24f), new Vector3(0.102f, 0.012f, 0.06f), new Vector3(-12f, 4f * side, 0f));
            return root;
        }

        private static void Part(Transform parent, string name, Material material, Vector3 position, Vector3 size, Vector3 angles)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            Destroy(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localRotation = Quaternion.Euler(angles);
            part.transform.localScale = size;
            var renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
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
