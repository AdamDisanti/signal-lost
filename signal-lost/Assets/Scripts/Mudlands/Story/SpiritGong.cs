using System;
using SignalLost.Audio;
using SignalLost.Combat;
using SignalLost.Effects;
using SignalLost.Player;
using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

namespace SignalLost.Mudlands
{
    /// <summary>
    /// Quest "Three Gongs": a spirit gong on an island in Sinkmire Bog, its post carved with a number (I, II, III). E
    /// rings it (a long, deep note and a ripple), wakes its guards (a group switched on when it rings) and leaves a
    /// teal spirit light standing over it; the story raises a section of the Spirit Road for each one. They ring in
    /// order: a gong out of turn only thuds, and says which one comes first.
    /// </summary>
    public sealed class SpiritGong : MonoBehaviour, IInteractable
    {
        [SerializeField] private GameObject guards;
        [SerializeField] private Transform disc;
        [Tooltip("Its number, 1-3: the gongs ring in this order.")]
        [SerializeField, Range(1, 3)] private int order = 1;
        private float rungAt = -10f;
        private QuestBeacon beacon;
        private TextMeshPro[] numerals = Array.Empty<TextMeshPro>();

        /// <summary>Its quest is on (the story sets it).</summary>
        public bool Ready { get; set; }
        /// <summary>It's the one to ring now (the story sets it).</summary>
        public bool Next { get; set; }
        public bool Rung { get; private set; }
        public int Order => order;
        public event Action<SpiritGong> RungEvent;

        public void Configure(GameObject guardGroup, Transform gongDisc, int number = 1)
        {
            guards = guardGroup;
            disc = gongDisc;
            order = Mathf.Clamp(number, 1, 3);
            if (guards != null) guards.SetActive(false);
        }

        public static string Numeral(int number) => number == 1 ? "I" : number == 2 ? "II" : "III";

        public string Prompt => !Ready || Rung ? "" : Next ? $"Ring gong {Numeral(order)}" : $"Gong {Numeral(order)}";
        public bool CanInteract(PlayerInteractor player) => isActiveAndEnabled && Ready && !Rung;

        public bool Interact(PlayerInteractor player)
        {
            if (!CanInteract(player)) return false;
            if (!Next)
            {
                // Out of turn: a dead thud, and the carving says which comes first.
                rungAt = Time.time - 0.7f;
                Sfx.PlayAt("crab_block", transform.position, 0.8f, 0.45f, 40f);
                MudHud.Toast($"It only thuds. This post is carved {Numeral(order)}: ring the gongs in order, I, II, III.", new Color(0.55f, 1f, 0.95f), 3f);
                return true;
            }
            Rung = true;
            rungAt = Time.time;
            Sfx.PlayAt("gong", transform.position, 1f, 1.1f - order * 0.08f, 160f); // each one a little lower
            ExpandingRing.Spawn(EffectMaterials.Additive(EffectMaterials.Look.Glow), transform.position + Vector3.up * 0.3f, 14f, 1.2f, 0.4f);
            ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, transform.position + Vector3.up * 2f, Vector3.up, 40, 0.08f, 4f, 80f, SpiritRoad.Spirit);
            CameraShake.Add(0.2f);
            if (guards != null) guards.SetActive(true);
            RungEvent?.Invoke(this);
            return true;
        }

        private void Start()
        {
            beacon = QuestBeacon.Attach(transform, Vector3.up * 3.2f, SpiritRoad.Spirit, 46f, 2.6f, 9f, 2.2f);
            // The carved number, glowing, on both faces of the frame's top bar.
            numerals = new TextMeshPro[2];
            for (int side = 0; side < 2; side++)
            {
                var go = new GameObject("Numeral") { hideFlags = HideFlags.DontSave };
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0f, 3.45f, side == 0 ? 0.12f : -0.12f);
                go.transform.localRotation = Quaternion.Euler(0f, side == 0 ? 180f : 0f, 0f);
                TextMeshPro text = go.AddComponent<TextMeshPro>();
                text.text = Numeral(order);
                text.fontSize = 5f;
                text.fontStyle = FontStyles.Bold;
                text.alignment = TextAlignmentOptions.Center;
                text.rectTransform.sizeDelta = new Vector2(2f, 1f);
                text.color = SpiritRoad.Spirit;
                numerals[side] = text;
            }
        }

        private void Update()
        {
            float t = Time.time - rungAt;
            if (disc != null) disc.localRotation = Quaternion.Euler(Mathf.Sin(t * 18f) * 12f * Mathf.Exp(-t * 1.5f), 0f, 0f);
            // The spirit light: faint over a gong waiting its turn, bright over the next one, steady once rung.
            if (beacon != null) beacon.Strength = Rung ? 0.8f : !Ready ? 0f : Next ? 1f : 0.35f;
            Color glow = Rung ? SpiritRoad.Spirit : Ready && Next ? Color.Lerp(SpiritRoad.Spirit, Color.white, 0.5f + 0.5f * Mathf.Sin(Time.time * 4f)) : SpiritRoad.Spirit * 0.55f;
            foreach (TextMeshPro text in numerals) if (text != null) text.color = glow;
        }
    }
}
