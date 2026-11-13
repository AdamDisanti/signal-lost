using System;
using SignalLost.Audio;
using SignalLost.Combat;
using SignalLost.Effects;
using SignalLost.Inventory;
using SignalLost.Player;
using SignalLost.UI;
using UnityEngine;
using Random = UnityEngine.Random;

namespace SignalLost.Mudlands
{
    /// <summary>
    /// Quest line 3, "Lights Out": a cursed lantern burning green on a pole. Shoot it out, or ram the pole with the car
    /// to topple it. It breaks with a shriek and releases the creatures it was holding (a group switched on).
    /// </summary>
    [RequireComponent(typeof(Destructible))]
    public sealed class CursedLantern : MonoBehaviour
    {
        [SerializeField] private GameObject released;
        private Destructible pole;

        public bool Ready { get; set; }
        public bool Out { get; private set; }
        public event Action<CursedLantern> PutOut;

        public void Configure(GameObject group)
        {
            released = group;
            if (released != null) released.SetActive(false);
        }

        private void Awake()
        {
            pole = GetComponent<Destructible>();
            pole.Broken += OnBroken;
        }

        private void OnBroken(Destructible _)
        {
            if (Out) return;
            Out = true;
            Vector3 top = transform.position + Vector3.up * 4f;
            ParticleBurst.Spawn(ParticleBurst.Kind.Mist, top, Vector3.zero, 50, 1f, 5f, 80f, new Color(0.5f, 1f, 0.4f, 0.8f));
            ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, top, Vector3.zero, 30, 0.1f, 6f, 80f, new Color(0.5f, 1f, 0.4f));
            Sfx.PlayAt("cackle", top, 1f, 1.6f, 60f);
            if (released != null)
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
            if (Out || Random.value > 0.25f) return;
            ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, transform.position + Vector3.up * 4.1f, Vector3.up, 1, 0.06f, 0.6f, 40f, new Color(0.5f, 1f, 0.4f));
        }
    }
}
