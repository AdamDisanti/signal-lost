using SignalLost.Combat;
using SignalLost.UI;
using UnityEngine;

namespace SignalLost.Enemies
{
    /// <summary>
    /// One of the merchant crab's ten cousins, the Bucket Brothers (meeting notes 10-22): an ordinary hermit crab
    /// with its own name (the GameObject's, so the HUD target readout shows it), shell, colours and lines. It barks
    /// when it first gets hit, says a last line when it goes down, and then despawns instead of leaving a corpse.
    /// Cousins only come back when the angry merchant calls them (<see cref="Summon"/>), always the same ten, and
    /// then drop no scrap.
    /// Their looks are baked into each cousin's prefab by CrabCousinBuilder.
    /// </summary>
    [RequireComponent(typeof(HermitCrab))]
    public sealed class CrabCousin : MonoBehaviour
    {
        [SerializeField] private string[] barks = { "Get 'im!" };
        [SerializeField] private string lastWords = "Ow.";
        [Tooltip("Seconds after going down before it disappears (the flip-over and sink play first).")]
        [SerializeField, Min(0.5f)] private float despawnDelay = 2.5f;
        [Tooltip("Lines are subtitled only while the player is this close.")]
        [SerializeField, Min(1f)] private float hearingDistance = 22f;

        // All cousins share one voice slot, so ten crabs charging at once do not talk over each other.
        private static float nextLine;

        private HermitCrab crab;
        private Health health;
        private Camera viewer;
        private bool barked;
        private float despawnAt = -1f;

        public HermitCrab Crab => crab != null ? crab : crab = GetComponent<HermitCrab>();
        public string DisplayName => name;
        /// <summary>True while it is out of the fight: defeated (or despawned) and not yet summoned again.</summary>
        public bool IsDown => !gameObject.activeInHierarchy || Crab.IsDead;

        private void Awake()
        {
            crab = GetComponent<HermitCrab>();
            health = GetComponent<Health>();
        }

        private void OnEnable()
        {
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }

        private void OnDisable()
        {
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
        }

        private void Update()
        {
            if (despawnAt >= 0f && Time.time >= despawnAt)
            {
                despawnAt = -1f;
                gameObject.SetActive(false);
            }
        }

        /// <summary>Brings the cousin back at full health at <paramref name="position"/>, ready to fight there.</summary>
        public void Summon(Vector3 position, Quaternion rotation)
        {
            despawnAt = -1f;
            barked = false;
            Crab.Territory.Zone = null; // it now defends the spot it was called to, not its old hangout
            // Called-back cousins drop nothing (meeting notes 10-23): otherwise the angry merchant is a scrap farm.
            if (TryGetComponent(out SignalLost.Inventory.ScrapDropper dropper)) dropper.enabled = false;
            gameObject.SetActive(true);
            Crab.enabled = true;
            Crab.Relocate(position, rotation);
        }

        /// <summary>Removes the cousin from the world straight away (it can still be summoned again).</summary>
        public void Despawn()
        {
            despawnAt = -1f;
            gameObject.SetActive(false);
        }

        private void OnDamaged(Health _, float amount, GameObject source)
        {
            if (barked || health.IsDead || barks.Length == 0) return;
            barked = true;
            Say(barks[Random.Range(0, barks.Length)]);
        }

        private void OnDied(Health _)
        {
            Say(lastWords, true);
            despawnAt = Time.time + despawnDelay;
        }

        private void Say(string line, bool always = false)
        {
            if (string.IsNullOrEmpty(line) || (!always && Time.time < nextLine)) return;
            if (viewer == null || !viewer.isActiveAndEnabled) viewer = Camera.main;
            if (viewer == null || Vector3.Distance(viewer.transform.position, transform.position) > hearingDistance) return;
            DialogueUi ui = DialogueUi.Current;
            if (ui == null) return;
            nextLine = Time.time + 2.5f;
            ui.Subtitle(name, line, 2.4f);
        }

        // Domain reload may be disabled in the editor; start every Play session with a free voice slot.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => nextLine = 0f;
    }
}
