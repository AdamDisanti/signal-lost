using System;
using SignalLost.Combat;
using SignalLost.Effects;
using SignalLost.Mudlands;
using SignalLost.Scanner;
using UnityEngine;

namespace SignalLost.Finale
{
    /// <summary>
    /// Phase 1's shield task: a power conduit tucked into the scrap around the arena, feeding the mech's shield. It sits
    /// dark and can't be hurt until the shield goes up; then it hums, sparks and shows on the scanner (a quest signal),
    /// and the player has to find it and break it. When every live conduit is broken the shield fails
    /// (<see cref="FinaleEncounter"/>). <see cref="ResetConduit"/> puts it back for a retry.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class ShieldConduit : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float toughness = 70f;

        public event Action<ShieldConduit> Broken;

        public bool Live { get; private set; }
        public bool IsBroken { get; private set; }

        private Health health;
        private QuestSignalSource signal;
        private Transform coil, wreck;
        private Light glow;
        private float nextSpark;

        private void Awake()
        {
            health = GetComponent<Health>();
            health.SetMax(toughness);
            signal = GetComponent<QuestSignalSource>();
            if (signal == null) signal = gameObject.AddComponent<QuestSignalSource>();
            if (transform.Find("Model") == null) BuildModel();
            coil = transform.Find("Model/Coil");
            wreck = transform.Find("Model/Broken");
            glow = GetComponentInChildren<Light>(true);
            SetLive(false);
        }

        private void OnEnable() => health.Died += OnDied;
        private void OnDisable() => health.Died -= OnDied;

        /// <summary>Powers it up (the shield is up) or down.</summary>
        public void SetLive(bool live)
        {
            Live = live && !IsBroken;
            health.Invulnerable = !Live;
            signal.enabled = Live;
            if (coil != null) coil.gameObject.SetActive(!IsBroken);
            if (glow != null) glow.intensity = Live ? 2.5f : 0f;
        }

        public void ResetConduit()
        {
            IsBroken = false;
            health.Restore();
            if (wreck != null) wreck.gameObject.SetActive(false);
            SetLive(false);
        }

        private void OnDied(Health _)
        {
            IsBroken = true;
            SetLive(false);
            if (wreck != null) wreck.gameObject.SetActive(true);
            ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, transform.position + Vector3.up * 1.4f, Vector3.up, 90, 0.2f, 10f);
            ParticleBurst.Spawn(ParticleBurst.Kind.Smoke, transform.position + Vector3.up, Vector3.up, 30, 1.2f, 2.5f);
            CameraShake.Add(0.2f);
            Broken?.Invoke(this);
        }

        private void Update()
        {
            if (!Live) return;
            if (coil != null) coil.localRotation = Quaternion.Euler(0f, Time.time * 120f, 0f);
            if (glow != null) glow.intensity = 2f + Mathf.Sin(Time.time * 9f) * 0.8f;
            if (Time.time >= nextSpark)
            {
                nextSpark = Time.time + UnityEngine.Random.Range(0.4f, 1.1f);
                ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, transform.position + Vector3.up * 1.8f, Vector3.up, 10, 0.12f, 5f);
            }
        }

        // A squat power box with a spinning coil on top, cables into the ground, and a broken version for afterwards.
        private void BuildModel()
        {
            var model = new GameObject("Model").transform;
            model.SetParent(transform, false);
            MudArt.Part(model, "Base", Vector3.zero, Quaternion.identity, "finale_conduit_base", k =>
            {
                k.Box(new Vector3(0f, 0.45f, 0f), new Vector3(1.3f, 0.9f, 1.1f), FinalePalette.Frame);
                k.Box(new Vector3(0f, 0.92f, 0f), new Vector3(1.0f, 0.12f, 0.85f), FinalePalette.Hazard);
                k.Box(new Vector3(0f, 0.5f, 0.56f), new Vector3(0.6f, 0.35f, 0.04f), FinalePalette.Teal, 1.5f);
                k.Prism(new Vector3(0f, 0.98f, 0f), Quaternion.identity, 0.18f, 0.14f, 0.5f, 8, FinalePalette.Steel);
                k.Tube(new[] { new Vector3(-0.55f, 0.3f, -0.3f), new Vector3(-1.1f, 0.15f, -0.5f), new Vector3(-1.7f, -0.2f, -0.4f) }, 0.1f, 6, FinalePalette.DarkFrame);
                k.Tube(new[] { new Vector3(0.55f, 0.3f, -0.3f), new Vector3(1.0f, 0.12f, -0.9f), new Vector3(1.2f, -0.2f, -1.6f) }, 0.1f, 6, FinalePalette.DarkFrame);
            });
            Transform coilPart = MudArt.Part(model, "Coil", new Vector3(0f, 1.6f, 0f), Quaternion.identity, "finale_conduit_coil", k =>
            {
                for (int i = 0; i < 5; i++) k.Prism(new Vector3(0f, -0.12f + i * 0.16f, 0f), Quaternion.Euler(0f, i * 18f, 0f), 0.42f - i * 0.05f, 0.42f - i * 0.05f, 0.08f, 8, FinalePalette.Teal, 1.3f);
                k.Ball(new Vector3(0f, 0.75f, 0f), 0.2f, FinalePalette.Teal, 1, 1.6f);
            });
            var light = new GameObject("Glow").AddComponent<Light>();
            light.transform.SetParent(coilPart, false);
            light.type = LightType.Point;
            light.color = FinalePalette.Teal;
            light.range = 7f;
            light.shadows = LightShadows.None;
            Transform broken = MudArt.Part(model, "Broken", new Vector3(0f, 1.05f, 0f), Quaternion.identity, "finale_conduit_broken", k =>
            {
                k.Box(new Vector3(0.1f, 0.05f, 0f), new Vector3(0.5f, 0.1f, 0.4f), Quaternion.Euler(0f, 30f, 12f), FinalePalette.DarkFrame);
                k.Box(new Vector3(-0.2f, 0.08f, 0.15f), new Vector3(0.3f, 0.12f, 0.25f), Quaternion.Euler(20f, -15f, 0f), FinalePalette.Rust);
            });
            broken.gameObject.SetActive(false);
            var box = gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 1.1f, 0f);
            box.size = new Vector3(1.4f, 2.2f, 1.2f);
        }
    }
}
