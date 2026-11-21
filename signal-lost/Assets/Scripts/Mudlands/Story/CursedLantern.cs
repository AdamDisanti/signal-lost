using System;
using SignalLost.Audio;
using SignalLost.Combat;
using SignalLost.Effects;
using UnityEngine;
using Random = UnityEngine.Random;

namespace SignalLost.Mudlands
{
    /// <summary>
    /// Quests "Lights Out": a cursed lantern burning green. The road ones stand on poles: shoot them out, or ram the
    /// pole with the car. The hex lanterns (the second quest) burn on rooftops and the windmill, out of the car's reach:
    /// they only appear when the curse jumps to them (<see cref="Reveal"/>), and have to be shot down. While its quest
    /// is on, each one throws a tall green beam over the trees. It breaks with a shriek and releases the creatures it
    /// was holding (a group switched on); a hex lantern's guards turn up with it instead.
    /// </summary>
    [RequireComponent(typeof(Destructible))]
    public sealed class CursedLantern : MonoBehaviour
    {
        [SerializeField] private GameObject released;
        [Tooltip("A rooftop hex lantern: hidden until revealed, bigger beam.")]
        [SerializeField] private bool hex;
        [Tooltip("Height of the flame above the pivot (the pole lantern's is 3.8 m).")]
        [SerializeField] private float flameHeight = 3.8f;
        private Destructible pole;
        private QuestBeacon beacon;

        public bool Ready { get; set; }
        public bool Out { get; private set; }
        public bool Hex => hex;
        /// <summary>Its quest is on: the beam shows.</summary>
        public bool Beacon { get; set; }
        public event Action<CursedLantern> PutOut;

        public void Configure(GameObject group, bool rooftop = false, float flame = 3.8f)
        {
            released = group;
            hex = rooftop;
            flameHeight = flame;
            if (released != null) released.SetActive(false);
        }

        private void Awake()
        {
            pole = GetComponent<Destructible>();
            pole.Broken += OnBroken;
        }

        private void Start()
        {
            Vector3 flame = transform.InverseTransformPoint(Flame);
            beacon = QuestBeacon.Attach(transform, flame, MudPalette.Curse, hex ? 70f : 40f, hex ? 3.4f : 2f, hex ? 14f : 0f, 3f, 0.25f);
        }

        private Vector3 Flame => transform.position + transform.up * flameHeight * transform.lossyScale.y;

        /// <summary>The curse jumps here (a hex lantern appears, burning).</summary>
        public void Reveal()
        {
            if (Out) return;
            gameObject.SetActive(true);
            Beacon = true;
            if (released != null) released.SetActive(true); // its guards turn up with it
            ParticleBurst.Spawn(ParticleBurst.Kind.Mist, Flame, Vector3.up, 60, 1.4f, 6f, 80f, new Color(0.5f, 1f, 0.4f, 0.8f));
            ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, Flame, Vector3.up, 50, 0.12f, 8f, 80f, MudPalette.Curse);
            Sfx.PlayAt("cackle", Flame, 1f, 1.2f, 200f);
        }

        private void OnBroken(Destructible _)
        {
            if (Out) return;
            Out = true;
            Vector3 top = Flame;
            ParticleBurst.Spawn(ParticleBurst.Kind.Mist, top, Vector3.zero, 50, 1f, 5f, 80f, new Color(0.5f, 1f, 0.4f, 0.8f));
            ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, top, Vector3.zero, 30, 0.1f, 6f, 80f, new Color(0.5f, 1f, 0.4f));
            Sfx.PlayAt("cackle", top, 1f, 1.6f, 60f);
            if (beacon != null)
            {
                // The beam is a child of the lantern, whose parts fly off as debris: keep it where it stands while it fades.
                beacon.transform.SetParent(null, true);
                beacon.Strength = 0f;
                Destroy(beacon.gameObject, 2f);
                beacon = null;
            }
            if (released != null && !hex)
            {
                released.SetActive(true);
                StartCoroutine(Burst());
            }
            PutOut?.Invoke(this);
        }

        // The freed creatures spring out of the ground in a puff of green, a frame after waking (once they have started).
        private System.Collections.IEnumerator Burst()
        {
            yield return null;
            foreach (MudCritter critter in released.GetComponentsInChildren<MudCritter>())
            {
                Vector3 away = critter.transform.position - transform.position;
                away.y = 0f;
                ParticleBurst.Spawn(ParticleBurst.Kind.Mist, critter.transform.position, Vector3.up, 14, 0.8f, 3f, 70f, new Color(0.5f, 1f, 0.4f, 0.6f));
                critter.Launch(away.normalized * 2.5f + Vector3.up * 6f, Random.Range(-360f, 360f));
            }
        }

        private void Update()
        {
            if (beacon != null) beacon.Strength = Beacon && !Out ? 1f : 0f;
            if (Out || Random.value > (hex ? 0.6f : 0.25f)) return;
            ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, Flame + Vector3.up * 0.3f, Vector3.up, 1, 0.06f, 0.6f, 40f, new Color(0.5f, 1f, 0.4f));
        }
    }
}
