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
    /// Quest line 2, "Three Gongs": a spirit gong on an island in Sinkmire Bog. E rings it (a long, deep note and a
    /// ripple), and wakes its guards (a group switched on when it rings).
    /// </summary>
    public sealed class SpiritGong : MonoBehaviour, IInteractable
    {
        [SerializeField] private GameObject guards;
        [SerializeField] private Transform disc;
        private float rungAt = -10f;

        public bool Ready { get; set; }
        public bool Rung { get; private set; }
        public event Action<SpiritGong> RungEvent;

        public void Configure(GameObject guardGroup, Transform gongDisc)
        {
            guards = guardGroup;
            disc = gongDisc;
            if (guards != null) guards.SetActive(false);
        }

        public string Prompt => Ready && !Rung ? "Ring the gong" : "";
        public bool CanInteract(PlayerInteractor player) => isActiveAndEnabled && Ready && !Rung;

        public bool Interact(PlayerInteractor player)
        {
            if (!CanInteract(player)) return false;
            Rung = true;
            rungAt = Time.time;
            Sfx.PlayAt("gong", transform.position, 1f, Random.Range(0.9f, 1.05f), 120f);
            ExpandingRing.Spawn(EffectMaterials.Additive(EffectMaterials.Look.Glow), transform.position + Vector3.up * 0.3f, 14f, 1.2f, 0.4f);
            CameraShake.Add(0.2f);
            if (guards != null) guards.SetActive(true);
            RungEvent?.Invoke(this);
            return true;
        }

        private void Update()
        {
            float t = Time.time - rungAt;
            if (disc != null) disc.localRotation = Quaternion.Euler(Mathf.Sin(t * 18f) * 12f * Mathf.Exp(-t * 1.5f), 0f, 0f);
        }
    }
}
