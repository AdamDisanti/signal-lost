using System.Collections.Generic;
using SignalLost.Effects;
using SignalLost.Enemies;
using SignalLost.Enemies.Meadow;
using SignalLost.NPC;
using SignalLost.Player;
using SignalLost.Progression;
using SignalLost.Scanner;
using SignalLost.Story;
using SignalLost.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SignalLost.Audio
{
    /// <summary>
    /// Starts itself when the game runs (no scene setup needed) and adds sound to whatever is in the current scene:
    /// <list type="bullet">
    /// <item>a background ambience loop chosen by scene name;</item>
    /// <item><see cref="PlayerAudio"/> on the player and <see cref="CreatureAudio"/> on every hermit crab;</item>
    /// <item>crackling loops on fires, a fizzing loop on electrical fires and sparks, lapping water at the pond;</item>
    /// <item>hover/click sounds on every UI button, open/close sounds for menus, a cha-ching when the shop opens;</item>
    /// <item>a quest chime on each tutorial stage and a fanfare when a main ship part is recovered;</item>
    /// <item>a nicer ping for the scanner's beeps.</item>
    /// </list>
    /// Scenes are re-checked every second, so things spawned later (cousins, buttons, fires) get sound too.
    /// Doesn't run in batch mode, so automated smoke tests are unaffected.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class SoundDirector : MonoBehaviour
    {
        public static SoundDirector Instance { get; private set; }

        private const float AmbienceVolume = 0.22f;

        private SfxLoop ambience;
        private float sceneStarted, nextScan;
        private bool menusWereOpen, shopWasOpen;
        private TutorialStory story;
        private readonly HashSet<Object> hooked = new HashSet<Object>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (Application.isBatchMode || Instance != null) return;
            var go = new GameObject("Signal Lost Audio");
            DontDestroyOnLoad(go);
            go.AddComponent<SoundDirector>();
        }

        private void Awake()
        {
            Instance = this;
            Sfx.SetRoot(transform);
            ambience = Sfx.AddLoop(gameObject, "amb_meadow", AmbienceVolume, false, bus: SoundBus.Ambience, childName: "Ambience");
            SceneManager.sceneLoaded += OnSceneLoaded;
            StageProgress.MainPartRecovered += OnMainPartRecovered;
            SetUpScene(SceneManager.GetActiveScene());
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            StageProgress.MainPartRecovered -= OnMainPartRecovered;
            if (story != null) story.StageChanged -= OnStageChanged;
            if (Instance == this) Instance = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single) SetUpScene(scene);
        }

        private void SetUpScene(Scene scene)
        {
            sceneStarted = Time.unscaledTime;
            nextScan = 0f;
            hooked.Clear();
            if (story != null) story.StageChanged -= OnStageChanged;
            story = null;

            string bed = AmbienceFor(scene.name);
            if (ambience != null)
            {
                ambience.gameObject.SetActive(bed != null);
                if (bed != null) ambience.SetClip(bed);
                ambience.Level = 0f; // fades in
            }
        }

        /// <summary>Background loop per scene. Add a line here for new levels; test scenes stay quiet.</summary>
        private static string AmbienceFor(string sceneName)
        {
            string name = sceneName.ToLowerInvariant();
            if (name.Contains("desert")) return "amb_desert";
            if (name.Contains("tutorial") || name.Contains("mainmenu")) return "amb_meadow";
            if (name.Contains("sandbox")) return "amb_wind";
            return null;
        }

        private void Update()
        {
            if (ambience != null) ambience.Level = Mathf.MoveTowards(ambience.Level, 1f, Time.unscaledDeltaTime / 2.5f);
            if (Time.unscaledTime >= nextScan)
            {
                // Every second at first, then every 3 s (scanning is cheap but not free in big scenes).
                nextScan = Time.unscaledTime + (Time.unscaledTime - sceneStarted < 6f ? 1f : 3f);
                Scan();
            }
            UpdateMenus();
        }

        // ---------- Finding things to add sound to ----------

        private void Scan()
        {
            var player = FindAnyObjectByType<FirstPersonController>();
            if (player != null && player.GetComponent<PlayerAudio>() == null) player.gameObject.AddComponent<PlayerAudio>();

            foreach (HermitCrab crab in FindObjectsByType<HermitCrab>(FindObjectsInactive.Include))
                if (crab.GetComponent<CreatureAudio>() == null) crab.gameObject.AddComponent<CreatureAudio>();

            foreach (ChiefToad toad in FindObjectsByType<ChiefToad>(FindObjectsInactive.Include))
                if (toad.GetComponent<ToadAudio>() == null) toad.gameObject.AddComponent<ToadAudio>();

            foreach (MerchantCrab merchant in FindObjectsByType<MerchantCrab>(FindObjectsInactive.Include))
                if (Hook(merchant)) merchant.ArrivedHome += () => Sfx.PlayAt("shop_bell", merchant.transform.position, 0.7f, 1f, 30f);

            if (story == null)
            {
                story = FindAnyObjectByType<TutorialStory>();
                if (story != null) story.StageChanged += OnStageChanged;
            }

            AddFireSounds();
            AddWaterSounds();

            // The scanner's own beep is a plain generated tone; swap in the ping. Its timing, pitch and volume logic stay.
            foreach (ScannerAudioFeedback feedback in FindObjectsByType<ScannerAudioFeedback>(FindObjectsInactive.Include))
            {
                var source = feedback.GetComponent<AudioSource>();
                if (source != null && source.clip != null && source.clip.name == "ScannerBeep") source.clip = Sfx.Clip("scanner_ping");
            }

            foreach (Selectable selectable in FindObjectsByType<Selectable>(FindObjectsInactive.Include))
                if (selectable.GetComponent<UiSoundHook>() == null) selectable.gameObject.AddComponent<UiSoundHook>();
        }

        /// <summary>True the first time an object is seen this scene.</summary>
        private bool Hook(Object target) => target != null && hooked.Add(target);

        private void AddFireSounds()
        {
            // Particle fires placed by the level builders: "Campfire", "Reactor Bay Fire", "Trench Fire 3", "Cockpit Sparks"...
            foreach (ParticleSystem particles in FindObjectsByType<ParticleSystem>())
            {
                Transform effect = particles.transform.parent;
                if (effect == null) continue;
                string name = effect.name;
                if (effect.GetComponent<FireEffect>() != null) continue; // handled below
                bool fire = name.Contains("Fire") || name.Contains("fire");
                bool sparks = name.Contains("Spark");
                if ((!fire && !sparks) || !Hook(effect)) continue;

                bool campfire = name.Contains("Campfire");
                bool small = name.Contains("Trench"); // the little fires along the crash trench
                // Only the main fires fizz, so a cluster of fires doesn't stack into a wall of zaps.
                bool fizz = sparks || name.Contains("Bay");
                if (!sparks)
                    Sfx.AddLoop(effect.gameObject, "fire_crackle", campfire ? 0.5f : small ? 0.25f : 0.4f, true, 1f,
                        campfire ? 20f : small ? 18f : 32f, childName: "Fire Sound");
                if (fizz) Sfx.AddLoop(effect.gameObject, "electric_sizzle", 0.18f, true, 1f, 18f, childName: "Sizzle Sound");
            }

            // Fires spawned in fights (Burnout's thruster trails, Sludge's burning oil).
            foreach (FireEffect fire in FindObjectsByType<FireEffect>())
                if (Hook(fire) && fire.transform.Find("Fire Sound") == null) Sfx.AddLoop(fire.gameObject, "fire_crackle", 0.45f, true, 1f, Mathf.Max(14f, fire.Radius * 6f), childName: "Fire Sound");
        }

        private void AddWaterSounds()
        {
            foreach (Renderer renderer in FindObjectsByType<Renderer>())
            {
                if (!renderer.name.Contains("Pond Water") || !Hook(renderer)) continue;
                var anchor = new GameObject("Water Sound");
                anchor.transform.SetParent(renderer.transform, true);
                anchor.transform.position = renderer.bounds.center;
                float reach = Mathf.Max(renderer.bounds.extents.x, renderer.bounds.extents.z);
                Sfx.AddLoop(anchor, "water_lap", 0.5f, true, reach * 0.6f, reach + 25f, childName: "Lapping");
            }
        }

        // ---------- Menus ----------

        private void UpdateMenus()
        {
            bool shop = MerchantMenuUi.Current != null && MerchantMenuUi.Current.IsOpen;
            if (shop && !shopWasOpen) Sfx.Play("shop_chaching", 0.7f, 1f, SoundBus.Interface);
            shopWasOpen = shop;

            bool menus = UiMenus.AnyOpen;
            if (menus != menusWereOpen && Time.unscaledTime - sceneStarted > 0.5f && !(shop && menus))
                Sfx.Play(menus ? "ui_open" : "ui_close", 0.45f, 1f, SoundBus.Interface);
            menusWereOpen = menus;
        }

        // ---------- Story ----------

        private void OnStageChanged(TutorialStory.Stage stage)
        {
            if (Time.unscaledTime - sceneStarted > 2f) Sfx.Play("quest_chime", 0.6f, 1f, SoundBus.Interface);
        }

        private void OnMainPartRecovered(string part) => Sfx.Play("part_fanfare", 0.85f, 1f, SoundBus.Interface);
    }
}
