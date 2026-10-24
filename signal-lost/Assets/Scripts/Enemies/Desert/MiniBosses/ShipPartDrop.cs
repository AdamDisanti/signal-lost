using System.Collections.Generic;
using SignalLost.Combat;
using SignalLost.Player;
using SignalLost.Progression;
using UnityEngine;

namespace SignalLost.Enemies.Desert
{
    /// <summary>
    /// The stolen ship part a defeated mini-boss drops: it falls to the ground, then floats, spins and glows until the
    /// player walks up to it. Recovering it shows a message and adds it to <see cref="Recovered"/>, under the stage it
    /// belongs to (the desert's three, or another stage's, such as the tutorial's cooling system).
    ///
    /// PLACEHOLDER reward: there is no inventory or ship-repair system yet, so recovering a part only records its
    /// name. Whether these parts are progression items is an open design question.
    /// </summary>
    public sealed class ShipPartDrop : MonoBehaviour
    {
        /// <summary>Names of the parts recovered since Play started.</summary>
        public static readonly List<string> Recovered = new List<string>();
        private static readonly List<string> RecoveredStages = new List<string>();

        [SerializeField] private string partName = "Ship part";
        [Tooltip("The stage whose part count it adds to.")]
        [SerializeField] private string stage = StageProgress.Desert;
        [SerializeField, Min(0.5f)] private float pickupRadius = 2.4f;

        private Transform player;
        private Health playerHealth;
        private Vector3 from, rest;
        private Quaternion fromRotation;
        private float born;
        private Light glow;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Recovered.Clear();
            RecoveredStages.Clear();
        }

        /// <summary>How many of a stage's parts have been recovered since Play started.</summary>
        public static int CountFor(string stage)
        {
            int count = 0;
            foreach (string item in RecoveredStages) if (item == stage) count++;
            return count;
        }

        /// <summary>Copies the recovered parts (and the stage each belongs to) into a save slot's lists.</summary>
        public static void CaptureRecovered(List<string> names, List<string> stages)
        {
            names.AddRange(Recovered);
            stages.AddRange(RecoveredStages);
        }

        /// <summary>Loading a save slot (or New Game, with nulls): replaces the recovered parts quietly.</summary>
        public static void RestoreRecovered(IList<string> names, IList<string> stages)
        {
            Recovered.Clear();
            RecoveredStages.Clear();
            if (names == null || stages == null) return;
            for (int i = 0; i < names.Count && i < stages.Count; i++) RecordRecovered(names[i], stages[i]);
        }

        /// <summary>Counts a part as recovered without a pickup (a loaded save whose loot was never picked up).</summary>
        public static void RecordRecovered(string name, string stage)
        {
            Recovered.Add(name);
            RecoveredStages.Add(stage);
        }

        /// <summary>Detaches <paramref name="part"/> from its owner and turns it into a pickup near <paramref name="near"/>.</summary>
        public static ShipPartDrop Create(Transform part, string name, Vector3 near, string stage = StageProgress.Desert)
        {
            part.SetParent(null, true);
            foreach (Collider collider in part.GetComponentsInChildren<Collider>(true)) Destroy(collider);
            foreach (Health health in part.GetComponentsInChildren<Health>(true)) Destroy(health);
            var drop = part.gameObject.AddComponent<ShipPartDrop>();
            drop.partName = name;
            drop.stage = stage;
            return drop;
        }

        private void Start()
        {
            born = Time.time;
            from = transform.position;
            fromRotation = transform.rotation;
            rest = FindGround(transform.position) + Vector3.up * 1f;
            var controller = FindAnyObjectByType<FirstPersonController>();
            if (controller != null)
            {
                player = controller.transform;
                playerHealth = controller.GetComponent<Health>();
            }

            var lightObject = new GameObject("Part Glow");
            lightObject.transform.SetParent(transform, false);
            glow = lightObject.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.75f, 0.35f);
            glow.range = 5f;
            glow.intensity = 2.5f;
        }

        private void Update()
        {
            float t = Time.time - born;
            if (t < 0.7f)
            {
                float k = t / 0.7f;
                Vector3 p = Vector3.Lerp(from, rest, k);
                p.y += Mathf.Sin(k * Mathf.PI) * 1.2f;
                transform.position = p;
                transform.rotation = Quaternion.Slerp(fromRotation, Quaternion.identity, k);
                return;
            }
            transform.position = rest + Vector3.up * (Mathf.Sin(t * 2f) * 0.15f);
            transform.rotation = Quaternion.Euler(0f, t * 60f, 0f);
            if (glow != null) glow.intensity = 2f + Mathf.Sin(t * 4f) * 0.8f;

            if (player == null || (playerHealth != null && playerHealth.IsDead)) return;
            Vector3 offset = player.position - transform.position;
            offset.y = 0f;
            if (offset.magnitude > pickupRadius) return;

            Recovered.Add(partName);
            RecoveredStages.Add(stage);
            string tally = stage == StageProgress.Desert ? $"  ({CountFor(stage)}/{DesertMiniBoss.MiniBossesPerBiome})" : "";
            MiniBossHud.Message($"RECOVERED: {partName.ToUpperInvariant()}{tally}", new Color(0.55f, 1f, 0.6f));
            Destroy(gameObject);
        }

        private static Vector3 FindGround(Vector3 point)
        {
            RaycastHit[] hits = Physics.RaycastAll(point + Vector3.up * 2f, Vector3.down, 60f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            Vector3 ground = point;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider is CharacterController || hit.collider.GetComponentInParent<Health>() != null) continue;
                if (hit.distance >= best) continue;
                best = hit.distance;
                ground = hit.point;
            }
            return ground;
        }
    }
}
