using SignalLost.Combat;
using SignalLost.Inventory;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace SignalLost.Player
{
    /// <summary>
    /// The Scotch flask for fights outside the tutorial (the tutorial has its own in TutorialPlayer), with the same
    /// rules: F (or d-pad up) drinks; each sip heals 35, halved for every drunk stack; a stack wears off every 15 s;
    /// drinking makes the player stagger; the sip after 5 stacks is a blackout, which counts as going down (the normal
    /// death and respawn). The laser is put away while drinking.
    ///
    /// It is usable once the player owns the flask (<see cref="PlayerInventory"/>) or once <see cref="Grant"/> is
    /// called; the desert stage exit grants it when the Core Worm fight starts. With no flask model assigned it makes a
    /// simple placeholder flask. A small IMGUI line shows the next sip and how drunk the player is.
    /// </summary>
    [RequireComponent(typeof(Health), typeof(FirstPersonController))]
    public sealed class CombatFlask : MonoBehaviour
    {
        [Tooltip("Optional: the flask model held up to drink (the AlcoholFlask prop). Empty: a simple placeholder.")]
        [SerializeField] private GameObject flaskModel;
        [SerializeField, Min(1f)] private float healAmount = 35f;
        [SerializeField, Min(1)] private int blackoutAfterStacks = 5;
        [SerializeField, Min(0.1f)] private float drinkSeconds = 0.9f;
        [SerializeField, Min(0.1f)] private float drinkCooldown = 1.2f;
        [SerializeField, Min(0.1f)] private float secondsPerStack = 15f;

        private Health health;
        private FirstPersonController movement;
        private MiningLaser laser;
        private PlayerInventory inventory;
        private Transform held;
        private Vector3 heldRest;
        private Quaternion heldRotation;
        private bool granted, drinking, sipApplied, laserWasOn;
        private float drinkStarted, nextDrink, nextSober;
        private GUIStyle style;

        public int DrunkStacks { get; private set; }
        public float NextSipHeal => healAmount * Mathf.Pow(0.5f, DrunkStacks);
        public bool Available => granted || (inventory != null && inventory.Has(PlayerItem.Flask));
        public bool IsDrinking => drinking;

        /// <summary>Sets the flask model before it is first shown (for a flask added at runtime).</summary>
        public void UseModel(GameObject model)
        {
            if (held == null) flaskModel = model;
        }

        /// <summary>Gives the player the flask (and records it in their inventory if they have one).</summary>
        public void Grant()
        {
            granted = true;
            if (inventory != null) inventory.Add(PlayerItem.Flask);
        }

        private void Awake()
        {
            health = GetComponent<Health>();
            movement = GetComponent<FirstPersonController>();
            laser = GetComponent<MiningLaser>();
            inventory = GetComponent<PlayerInventory>();
            health.Died += OnDied;
        }

        private void OnDestroy()
        {
            if (health != null) health.Died -= OnDied;
        }

        private void Start() => BuildHeldFlask();

        private void BuildHeldFlask()
        {
            Camera view = GetComponentInChildren<Camera>();
            if (view == null) return;
            GameObject model;
            if (flaskModel != null)
            {
                model = Instantiate(flaskModel);
                foreach (Collider item in model.GetComponentsInChildren<Collider>()) Destroy(item);
                Bounds bounds = new Bounds(model.transform.position, Vector3.zero);
                bool first = true;
                foreach (Renderer item in model.GetComponentsInChildren<Renderer>())
                {
                    if (first) bounds = item.bounds;
                    else bounds.Encapsulate(item.bounds);
                    first = false;
                }
                if (bounds.size.y > 0.001f) model.transform.localScale *= 0.26f / bounds.size.y;
            }
            else model = PlaceholderFlask();
            model.name = "Held Scotch Flask";
            foreach (Renderer item in model.GetComponentsInChildren<Renderer>())
            {
                item.shadowCastingMode = ShadowCastingMode.Off;
                item.receiveShadows = false;
            }
            held = model.transform;
            held.SetParent(view.transform, false);
            held.localPosition = new Vector3(-0.24f, -0.36f, 0.55f);
            heldRest = held.localPosition;
            heldRotation = held.localRotation;
            model.SetActive(false);
        }

        // A hip flask from primitives: a flat steel body, a leather band and a screw cap.
        private static GameObject PlaceholderFlask()
        {
            var root = new GameObject("Flask");
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            Material steel = new Material(lit) { name = "Flask Steel" };
            steel.SetColor("_BaseColor", new Color(0.72f, 0.72f, 0.75f));
            steel.SetFloat("_Metallic", 0.9f);
            steel.SetFloat("_Smoothness", 0.75f);
            Material leather = new Material(lit) { name = "Flask Leather" };
            leather.SetColor("_BaseColor", new Color(0.36f, 0.2f, 0.1f));
            leather.SetFloat("_Smoothness", 0.3f);
            Piece(PrimitiveType.Cube, root.transform, steel, new Vector3(0f, 0f, 0f), new Vector3(0.11f, 0.15f, 0.035f));
            Piece(PrimitiveType.Cube, root.transform, leather, new Vector3(0f, -0.02f, 0f), new Vector3(0.115f, 0.07f, 0.04f));
            Piece(PrimitiveType.Cylinder, root.transform, steel, new Vector3(0f, 0.09f, 0f), new Vector3(0.035f, 0.015f, 0.035f));
            Piece(PrimitiveType.Cylinder, root.transform, steel, new Vector3(0f, 0.11f, 0f), new Vector3(0.045f, 0.012f, 0.045f));
            return root;
        }

        private static void Piece(PrimitiveType type, Transform parent, Material material, Vector3 position, Vector3 scale)
        {
            GameObject piece = GameObject.CreatePrimitive(type);
            Destroy(piece.GetComponent<Collider>());
            piece.transform.SetParent(parent, false);
            piece.transform.localPosition = position;
            piece.transform.localScale = scale;
            piece.GetComponent<Renderer>().sharedMaterial = material;
        }

        private void Update()
        {
            if (!Available || health.IsDead) return;
            bool wantsDrink = Keyboard.current?.fKey.wasPressedThisFrame == true || Gamepad.current?.dpad.up.wasPressedThisFrame == true;
            if (wantsDrink && movement.enabled && Cursor.lockState == CursorLockMode.Locked) TryDrink();

            if (drinking)
            {
                float t = Mathf.Clamp01((Time.time - drinkStarted) / drinkSeconds);
                float lift = Mathf.Sin(t * Mathf.PI);
                if (held != null)
                {
                    held.localPosition = Vector3.Lerp(heldRest, new Vector3(-0.09f, -0.12f, 0.28f), lift);
                    held.localRotation = heldRotation * Quaternion.Euler(-105f * lift, 0f, -15f * lift);
                }
                if (!sipApplied && t >= 0.5f)
                {
                    sipApplied = true;
                    health.Heal(NextSipHeal);
                    DrunkStacks++;
                    nextSober = Time.time + secondsPerStack;
                    if (DrunkStacks > blackoutAfterStacks)
                    {
                        // Out cold: the same as going down in the fight.
                        EndDrink();
                        SignalLost.Enemies.Desert.MiniBossHud.Message("BLACKOUT: too much Scotch", new Color(1f, 0.6f, 0.4f), 3f);
                        health.TakeDamage(health.Current + 1f, gameObject);
                        return;
                    }
                }
                if (t >= 1f) EndDrink();
            }

            if (DrunkStacks > 0 && Time.time >= nextSober)
            {
                DrunkStacks--;
                nextSober = Time.time + secondsPerStack;
            }
            movement.Impairment = Mathf.Clamp01(DrunkStacks / (float)blackoutAfterStacks);
        }

        public bool TryDrink()
        {
            if (!Available || health.IsDead || drinking || Time.time < nextDrink) return false;
            drinking = true;
            sipApplied = false;
            drinkStarted = Time.time;
            nextDrink = Time.time + drinkCooldown;
            laserWasOn = laser != null && laser.enabled;
            if (laser != null) laser.enabled = false;
            if (held != null)
            {
                held.localPosition = heldRest;
                held.localRotation = heldRotation;
                held.gameObject.SetActive(true);
            }
            return true;
        }

        private void EndDrink()
        {
            drinking = false;
            if (held != null) held.gameObject.SetActive(false);
            // Only bring the laser back if nothing else (a cutscene, a respawn) has taken control away meanwhile.
            if (laser != null && laserWasOn && movement.enabled && !health.IsDead) laser.enabled = true;
        }

        private void OnDied(Health _)
        {
            if (drinking) EndDrink();
            DrunkStacks = 0;
            movement.Impairment = 0f;
        }

        private void OnGUI()
        {
            if (!Available || health.IsDead) return;
            style ??= new GUIStyle(GUI.skin.label) { fontSize = 17, alignment = TextAnchor.MiddleCenter };
            string text = DrunkStacks > 0
                ? $"F: drink Scotch (+{NextSipHeal:0.#} HP)    DRUNK x{DrunkStacks} / {blackoutAfterStacks}"
                : $"F: drink Scotch (+{NextSipHeal:0.#} HP)";
            var rect = new Rect(0f, Screen.height - 64f, Screen.width, 26f);
            GUI.color = new Color(0f, 0f, 0f, 0.8f);
            GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, style);
            GUI.color = DrunkStacks >= blackoutAfterStacks ? new Color(1f, 0.5f, 0.4f) : new Color(1f, 0.85f, 0.55f);
            GUI.Label(rect, text, style);
            GUI.color = Color.white;
        }
    }
}
