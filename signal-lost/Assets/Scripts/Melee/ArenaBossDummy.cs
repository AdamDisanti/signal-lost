using System.Collections.Generic;
using SignalLost.Combat;
using SignalLost.Finale;
using SignalLost.Mudlands;
using TMPro;
using UnityEngine;

namespace SignalLost.Melee
{
    /// <summary>
    /// Testing aid for the melee arena: a hulking robot target with boss-sized health that never fights back, so the
    /// damage the player does to a boss (and how rage raises it) can be measured. A sign over it shows the damage per
    /// second over the last few seconds, the biggest hit and the current rage bonus. It heals back to full when it gets
    /// low. Counts as a big enemy: the magnet pull drags the player to it, and it can't be finished.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class ArenaBossDummy : MonoBehaviour, IStunnable
    {
        private const float Window = 5f;

        private Health health;
        private TextMeshPro sign;
        private readonly Queue<(float time, float amount)> recent = new Queue<(float, float)>();
        private float biggest, total;

        private void Awake()
        {
            health = GetComponent<Health>();
            health.Damaged += OnDamaged;
            Build();
        }

        private void OnDestroy()
        {
            if (health != null) health.Damaged -= OnDamaged;
        }

        public void Stun(float seconds) { } // a dummy: nothing to stun

        private void OnDamaged(Health _, float amount, GameObject source)
        {
            recent.Enqueue((Time.time, amount));
            total += amount;
            biggest = Mathf.Max(biggest, amount);
            if (health.Normalized < 0.05f) health.Restore();
        }

        private void Update()
        {
            while (recent.Count > 0 && Time.time - recent.Peek().time > Window) recent.Dequeue();
            float sum = 0f;
            foreach ((float _, float amount) in recent) sum += amount;
            RageMeter rage = RageMeter.Instance;
            sign.text = $"<b>BOSS DUMMY</b>\nDPS <b>{sum / Window:0}</b>   biggest hit {biggest:0}   total {total:0}\n" +
                        $"<color=#66E6FF>rage x{(rage != null ? rage.DamageMultiplier : 1f):0.00}</color>";
            Camera view = Camera.main;
            if (view != null) sign.transform.rotation = Quaternion.LookRotation(sign.transform.position - view.transform.position);
        }

        // A big boxy robot in the finale's robot colours: a barrel chest, a visor and an antenna, on stubby legs.
        private void Build()
        {
            var capsule = gameObject.AddComponent<CapsuleCollider>();
            capsule.radius = 1.1f;
            capsule.height = 4.2f;
            capsule.center = new Vector3(0f, 2.1f, 0f);
            MudArt.Part(transform, "Dummy Body", Vector3.zero, Quaternion.identity, "MeleeArenaBossDummy", kit =>
            {
                Color steel = FinalePalette.Steel, dark = FinalePalette.Frame;
                kit.Box(new Vector3(-0.5f, 0.6f, 0f), new Vector3(0.6f, 1.2f, 0.7f), dark);
                kit.Box(new Vector3(0.5f, 0.6f, 0f), new Vector3(0.6f, 1.2f, 0.7f), dark);
                kit.Box(new Vector3(0f, 2.05f, 0f), new Vector3(2f, 1.8f, 1.4f), steel);
                kit.Box(new Vector3(0f, 2.05f, 0.71f), new Vector3(1.2f, 0.9f, 0.05f), dark);
                kit.Ball(new Vector3(0f, 2.05f, 0.75f), new Vector3(0.3f, 0.3f, 0.12f), Quaternion.identity, GaussFx.Energy, 1, 2.2f);
                kit.Box(new Vector3(-1.25f, 2.2f, 0f), new Vector3(0.5f, 1.4f, 0.6f), dark);
                kit.Box(new Vector3(1.25f, 2.2f, 0f), new Vector3(0.5f, 1.4f, 0.6f), dark);
                kit.Box(new Vector3(0f, 3.3f, 0f), new Vector3(1.1f, 0.7f, 0.9f), steel);
                kit.Box(new Vector3(0f, 3.35f, 0.46f), new Vector3(0.9f, 0.18f, 0.04f), GaussFx.Energy, 2.4f);
                kit.Rod(new Vector3(0.3f, 3.65f, 0f), new Vector3(0.3f, 4.2f, 0f), 0.04f, 6, dark);
                kit.Ball(new Vector3(0.3f, 4.25f, 0f), 0.09f, FinalePalette.EngineGlow, 1, 2.4f);
            });
            sign = new GameObject("Dummy Sign").AddComponent<TextMeshPro>();
            sign.transform.SetParent(transform, false);
            sign.transform.localPosition = new Vector3(0f, 5.1f, 0f);
            sign.alignment = TextAlignmentOptions.Center;
            sign.fontSize = 4f;
            sign.textWrappingMode = TextWrappingModes.NoWrap;
            sign.outlineWidth = 0.2f;
            sign.outlineColor = new Color32(10, 14, 30, 255);
        }
    }
}
