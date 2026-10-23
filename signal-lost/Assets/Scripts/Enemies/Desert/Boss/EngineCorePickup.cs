using System;
using SignalLost.Player;
using SignalLost.Progression;
using UnityEngine;

namespace SignalLost.Enemies.Desert
{
    /// <summary>
    /// The Engine Core dropped by the Coreworm: it floats and glows until the player walks into it. Picking it up
    /// records it as a main ship part (<see cref="StageProgress"/>), clears the desert stage and unlocks the next one.
    /// </summary>
    public sealed class EngineCorePickup : MonoBehaviour
    {
        [SerializeField, Min(0.5f)] private float pickupRadius = 2.2f;

        private string partName = "Engine Core";
        private string unlocks;
        private Vector3 from, rest;
        private Quaternion fromRotation;
        private float born;
        private Transform player;
        private Light glow;

        /// <summary>Raised when the player picks it up, with the part's name.</summary>
        public static event Action<string> Recovered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Recovered = null;

        /// <summary>Turns an object into the floating pickup near a point.</summary>
        public static EngineCorePickup Create(Transform part, string name, Vector3 near, string unlocksStage = "NextStage")
        {
            var pickup = part.gameObject.AddComponent<EngineCorePickup>();
            pickup.partName = name;
            pickup.unlocks = unlocksStage;
            pickup.from = part.position;
            pickup.fromRotation = part.rotation;
            pickup.rest = near + Vector3.up * 1.4f;
            return pickup;
        }

        private void Start()
        {
            born = Time.time;
            var controller = FindAnyObjectByType<FirstPersonController>();
            if (controller != null) player = controller.transform;
            glow = GetComponentInChildren<Light>();
            if (glow == null)
            {
                glow = new GameObject("Core Glow").AddComponent<Light>();
                glow.transform.SetParent(transform, false);
            }
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.7f, 0.35f);
            glow.range = 9f;
        }

        private void Update()
        {
            float t = Time.time - born;
            if (t < 1f)
            {
                float k = t / 1f;
                Vector3 p = Vector3.Lerp(from, rest, k);
                p.y += Mathf.Sin(k * Mathf.PI) * 2f;
                transform.position = p;
                transform.rotation = Quaternion.Slerp(fromRotation, Quaternion.identity, k);
                return;
            }
            transform.position = rest + Vector3.up * Mathf.Sin(t * 1.8f) * 0.2f;
            transform.rotation = Quaternion.Euler(0f, t * 45f, Mathf.Sin(t) * 10f);
            if (glow != null) glow.intensity = 4f + Mathf.Sin(t * 5f) * 1.5f;

            if (player == null) return;
            Vector3 offset = player.position - transform.position;
            offset.y = 0f;
            if (offset.magnitude > pickupRadius) return;

            StageProgress.RecoverMainPart(partName);
            StageProgress.ClearStage(StageProgress.Desert, unlocks);
            MiniBossHud.Message($"RECOVERED: {partName.ToUpperInvariant()}  (main ship part)", new Color(0.55f, 1f, 0.6f), 4f);
            Recovered?.Invoke(partName);
            Destroy(gameObject);
        }
    }
}
