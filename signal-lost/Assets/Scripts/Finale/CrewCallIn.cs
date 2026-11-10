using SignalLost.Core;
using SignalLost.Player;
using SignalLost.UI;
using UnityEngine;

namespace SignalLost.Finale
{
    /// <summary>
    /// The final fight's call-in (Adam, 11-09): press the "call_crew" key (G by default, rebindable) and Eugene charges
    /// in with three of his cousins for <see cref="Duration"/> seconds. They draw some of the mech's fire, pinch it
    /// during damage windows and clear the adds during shield tasks. They can't be hurt. Then a cooldown.
    /// Only works while the fight is on (<see cref="FinaleEncounter"/> turns it on and off).
    /// </summary>
    public sealed class CrewCallIn : MonoBehaviour
    {
        public const float Duration = 15f, Cooldown = 60f;
        private const float HitAdds = 14f, HitMech = 9f;

        private static readonly string[] EugeneLines =
        {
            "Cavendish! We shared a LOG! Get him, boys!",
            "I put the kettle on for you every night, you rotten rodent!",
            "Bucket Brothers! Pinch that tin can!",
            "Safe keeping?! SAFE KEEPING?!"
        };

        private FinaleEncounter encounter;
        private float readyAt, activeUntil;
        private int line;

        public bool Enabled { get; set; }
        public bool Active => Time.time < activeUntil;
        public float ActiveRemaining => Mathf.Max(0f, activeUntil - Time.time);
        public float CooldownRemaining => Mathf.Max(0f, readyAt - Time.time);
        public bool Ready => Enabled && !Active && CooldownRemaining <= 0f;

        public static CrewCallIn Ensure(GameObject player, FinaleEncounter encounter)
        {
            var call = player.GetComponent<CrewCallIn>();
            if (call == null) call = player.AddComponent<CrewCallIn>();
            call.encounter = encounter;
            return call;
        }

        private void Update()
        {
            if (Enabled && KeyBindings.WasPressed("call_crew") && !UiMenus.AnyOpen) Call();
        }

        /// <summary>Calls the crew in if it's ready. Returns false (with a reason on screen) if not.</summary>
        public bool Call()
        {
            if (!Enabled || encounter == null) return false;
            if (Active || CooldownRemaining > 0f)
            {
                DialogueUi.Instance?.Subtitle("Eugene", Active ? "We're already here, pal!" : $"Catching our breath! ({CooldownRemaining:0} s)", 1.5f);
                return false;
            }
            activeUntil = Time.time + Duration;
            readyAt = activeUntil + Cooldown;
            Transform view = GetComponentInChildren<Camera>() != null ? GetComponentInChildren<Camera>().transform : transform;
            Vector3 back = -Vector3.ProjectOnPlane(view.forward, Vector3.up).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, back);
            GameObject[] crew = encounter.CrewPrefabs;
            for (int i = 0; i < crew.Length; i++)
            {
                if (crew[i] == null) continue;
                Vector3 at = transform.position + back * (3f + (i % 2) * 1.5f) + side * ((i - (crew.Length - 1) * 0.5f) * 2.4f);
                AllyCrab.Spawn(crew[i], i == 0 ? "Eugene" : crew[i].name, at, Quaternion.LookRotation(-back), Duration, HitAdds, HitMech, encounter.Mech);
            }
            DialogueUi.Instance?.Subtitle("Eugene", EugeneLines[line++ % EugeneLines.Length], 3f);
            return true;
        }

        /// <summary>Sends the crew home and makes the call-in ready again (a phase retry).</summary>
        public void ResetCall()
        {
            foreach (AllyCrab crab in AllyCrab.Live.ToArray())
                if (crab != null) Destroy(crab.gameObject);
            activeUntil = 0f;
            readyAt = 0f;
        }
    }
}
