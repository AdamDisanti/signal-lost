using System;
using System.Collections;
using SignalLost.Combat;
using SignalLost.Enemies;
using SignalLost.Enemies.Desert;
using SignalLost.Inventory;
using SignalLost.Player;
using SignalLost.Progression;
using SignalLost.UI;
using UnityEngine;

namespace SignalLost.Story
{
    // Save and load for the tutorial (Christian, 10-27). This is the save-only part of TutorialStory; the story itself
    // is in Story/TutorialStory.cs, which only calls LoadSaveInstead() from Start.
    //
    // It autosaves at every stage, purchase and pickup, and the pause menu saves anywhere outside combat. A loaded save
    // puts the mechanic back where they stood, in the saved stage, with the enemies already beaten in it still beaten.
    // The per-frame and event work runs through a small helper component (TutorialSaveWatcher) added at Start, so
    // TutorialStory's own Update, OnEnable and OnDisable stay untouched.
    public sealed partial class TutorialStory : ISaveableLevel
    {
        private static readonly string[] LoadMumble = { "Mmh... must've dozed off.", "...right. Where was I?" };
        private const string PartGivenFlag = "partGiven";
        private const string CrabGaveUpFlag = "crabGaveUp";
        private const string SaveCrownId = "chief_toad_crown";
        // Being hit this recently counts as being in a fight, even with nothing inside its fight radius (a frog's goo).
        private const float HurtCombatSeconds = 6f;

        private bool restoring;
        private float lastHurt = float.NegativeInfinity;
        private float groundedAt = float.NegativeInfinity;
        private Health saveHealth;
        private int lastPartCount = -1;
        private bool lastPartGiven;

        /// <summary>
        /// Called once from Start. Sets up autosaving, and if the scene was opened from a save (Continue or Load
        /// Game), restores it and returns true so Start skips the opening.
        /// </summary>
        private bool LoadSaveInstead()
        {
            if (!TryGetComponent(out TutorialSaveWatcher _)) gameObject.AddComponent<TutorialSaveWatcher>();
            SaveData save = SaveGame.TakePending();
            if (save == null) return false;
            StartCoroutine(Restore(save));
            return true;
        }

        // ---------- Hooks (called by TutorialSaveWatcher) ----------

        internal void SaveHooks(bool on)
        {
            if (saveHealth == null && player != null) saveHealth = player.GetComponent<Health>();
            if (on)
            {
                SaveGame.Register(this);
                StageChanged += AutosaveOnStage;
                if (gear != null) gear.Collected += AutosaveOnPickup;
                if (pockets != null) pockets.Purchased += AutosaveOnPurchase;
                TrophyPickup.PickedUp += AutosaveOnLoot;
                TrophyStand.Placed += AutosaveOnLoot;
                if (saveHealth != null) saveHealth.Damaged += OnPlayerHurt;
            }
            else
            {
                SaveGame.Unregister(this);
                StageChanged -= AutosaveOnStage;
                if (gear != null) gear.Collected -= AutosaveOnPickup;
                if (pockets != null) pockets.Purchased -= AutosaveOnPurchase;
                TrophyPickup.PickedUp -= AutosaveOnLoot;
                TrophyStand.Placed -= AutosaveOnLoot;
                if (saveHealth != null) saveHealth.Damaged -= OnPlayerHurt;
            }
        }

        internal void SaveTick()
        {
            // CharacterController.isGrounded only holds while the player moves into the ground, so it reads false
            // whenever the game is paused (time stops, the move is zero). Remember the last real touch instead.
            if (Time.deltaTime > 0f && body != null && body.isGrounded) groundedAt = Time.time;
            SaveGame.Tick(); // an autosave that fell during a fight happens once it is over

            // The cooling system is picked up by walking over it, and handed to Cavendish in a conversation; neither
            // raises an event, so watch for them.
            int parts = ShipPartDrop.CountFor(StageProgress.Tutorial);
            bool changed = (lastPartCount >= 0 && parts != lastPartCount) || partGiven != lastPartGiven;
            lastPartCount = parts;
            lastPartGiven = partGiven;
            if (changed) AutosaveNow();
        }

        private void AutosaveOnStage(Stage next) { if (next != Stage.Opening) AutosaveNow(); }
        private void AutosaveOnPickup(PlayerItem item) => AutosaveNow();
        private void AutosaveOnPurchase(Upgrade upgrade) => AutosaveNow(); // keep what was just bought
        private void AutosaveOnLoot(string id) => AutosaveNow();

        private void AutosaveNow()
        {
            if (restoring || stage == Stage.Opening) return;
            SaveGame.Autosave();
        }

        private void OnPlayerHurt(Health _, float amount, GameObject source) => lastHurt = Time.time;

        // ---------- ISaveableLevel ----------

        public bool CanSave(out string reason)
        {
            reason = null;
            if (restoring || stage == Stage.Opening) reason = "You can save once you're up and about.";
            else if (player == null || player.IsRespawning || player.IsDead) reason = "You can't save while you're knocked out.";
            else if (toad != null && toad.Current == ChiefToadEncounter.State.Cutscene) reason = "You can't save during a cutscene.";
            else if (DialogueUi.Instance.PopupOpen) reason = "Finish the conversation first.";
            else if (body != null && Time.time - groundedAt > 0.25f) reason = "Land first, then save.";
            else if (Time.time - lastHurt < HurtCombatSeconds || merchant.AmbushActive || CombatCheck.EnemyEngaging(player.transform.position))
                reason = "You can't save with enemies nearby. Get clear of the fight first.";
            return reason == null;
        }

        public void Capture(SaveData data)
        {
            data.scene = gameObject.scene.name;
            data.stage = stage.ToString();
            data.label = QuestLabel(stage);
            data.SetFlag(PartGivenFlag, partGiven);
            data.playSeconds = Timer.Elapsed;
            data.deaths = Timer.Deaths;
            data.hasPosition = true;
            data.position = player.transform.position;
            data.yaw = player.transform.eulerAngles.y;
            // Progress inside the step, so a load never brings back (and re-drops) what was already beaten.
            if (stage == Stage.Fight)
            {
                foreach (var cousin in merchant.Cousins) if (cousin != null && cousin.IsDown) data.downed.Add(cousin.name);
                data.SetFlag(CrabGaveUpFlag, merchant.IsDefeated);
            }
            if (stage == Stage.Frogs && frogs != null) data.count = frogs.Defeated;
        }

        private static string QuestLabel(Stage at)
        {
            switch (at)
            {
                case Stage.Opening:
                case Stage.Greeting: return "A Rude Awakening";
                case Stage.SearchGear: return SearchTitle;
                case Stage.Fight:
                case Stage.MerchantComing:
                case Stage.MerchantIntro: return CrabTitle;
                case Stage.FirstUpgrade:
                case Stage.ShowCavendish: return GearUpTitle;
                case Stage.Frogs:
                case Stage.FrogsDone: return PondTitle;
                case Stage.DemoComplete: return "Demo complete";
                default: return ToadTitle;
            }
        }

        // Steps that can't be picked up halfway: the crab's walk home ends with it already home, and the Chief Toad
        // (who resets when the player leaves) waits under the pond again.
        private static Stage ResumeStage(Stage saved)
        {
            switch (saved)
            {
                case Stage.Opening: return Stage.Greeting;
                case Stage.MerchantComing: return Stage.MerchantIntro;
                case Stage.ToadFight: return Stage.ChiefToad;
                default: return saved;
            }
        }

        // ---------- Loading ----------

        /// <summary>
        /// Puts a loaded save back: the pockets, the run timer, the world for the saved quest step (the crab friendly
        /// at camp, the toad beaten, and so on) with the enemies already beaten in it still beaten, and the mechanic
        /// where they stood, with the health and drinks they had. Scrap left lying is back where it lay. Enemies still
        /// standing are back home at full health (saving needs the player clear of them, so they would be anyway).
        /// The crown and cooling system, if never picked up, come back in the pockets.
        /// </summary>
        private IEnumerator Restore(SaveData save)
        {
            restoring = true;
            if (!Enum.TryParse(save.stage, out Stage saved)) saved = Stage.Greeting;
            Stage resume = ResumeStage(saved);

            // Pockets first, so upgrades, health and the gear in hand match the save.
            SaveGame.RestorePockets(save, pockets);
            if (resume >= Stage.MerchantIntro) pockets.Add(PlayerItem.Flask); // the crab hands it over before the shop
            gear.Sync();
            if (player.TryGetComponent(out PlayerUpgrades upgrades)) upgrades.Apply();
            player.Health.Restore();
            SaveGame.RestorePlayerState(save, pockets); // health and drinks as they were
            SaveGame.RestoreDroppedScrap(save);
            Timer.Resume(save.playSeconds, save.deaths);
            partGiven = save.HasFlag(PartGivenFlag);
            crownPlaced = CampSave.IsPlaced(SaveCrownId);

            // The tools already found are gone from the wreck.
            if (laserPickup != null && gear.Has(PlayerItem.MiningLaser)) laserPickup.gameObject.SetActive(false);
            if (scannerPickup != null && gear.Has(PlayerItem.Scanner)) scannerPickup.gameObject.SetActive(false);

            // The world for that step.
            if (resume >= Stage.MerchantIntro) merchant.RestoreFriendly(resume > Stage.MerchantIntro);
            if (resume >= Stage.ToadBeaten && toad != null) toad.RestoreDefeated();

            // Back where the game was saved (old saves without a position: by the campfire).
            Transform spawn = PlayerSpawnPoint.Current != null ? PlayerSpawnPoint.Current.transform : null;
            if (save.hasPosition || spawn != null)
            {
                body.enabled = false;
                if (save.hasPosition) player.transform.SetPositionAndRotation(save.position, Quaternion.Euler(0f, save.yaw, 0f));
                else player.transform.SetPositionAndRotation(spawn.position, Quaternion.Euler(0f, spawn.eulerAngles.y, 0f));
                body.enabled = true;
            }
            view.localRotation = Quaternion.identity;
            movement.ResetMotion();
            // The full wake-up from the tutorial's opening (eyes opening, lying back, sitting up), with its own mumble.
            if (wake != null)
            {
                wake.Play(true, LoadMumble);
                yield return new WaitUntil(() => !wake.Playing);
            }

            switch (resume)
            {
                case Stage.Greeting:
                    BeginGreeting();
                    break;
                case Stage.SearchGear:
                    EnterSearch();
                    break;
                case Stage.Fight:
                    EnterFight();
                    // Cousins already beaten stay gone; a crab that already gave up waits for the rest of the brawl.
                    foreach (var cousin in merchant.Cousins)
                        if (cousin != null && save.downed.Contains(cousin.name)) cousin.Despawn();
                    if (save.HasFlag(CrabGaveUpFlag)) merchant.RestoreSurrendered(gear.Has(PlayerItem.Flask));
                    UpdateFightObjective();
                    break;
                case Stage.MerchantIntro:
                    SetStage(Stage.MerchantIntro);
                    Signal(merchant, true);
                    GameplayOverlay.Instance.SetQuest(CrabTitle, "Talk to the crab (Eugene) at camp.");
                    break;
                case Stage.FirstUpgrade when pockets.Upgrades.Count > 0:
                case Stage.ShowCavendish:
                    EnterShowCavendish();
                    break;
                case Stage.FirstUpgrade:
                    SetStage(Stage.FirstUpgrade);
                    Signal(merchant, true);
                    GameplayOverlay.Instance.SetQuest(GearUpTitle, "Buy your first upgrade from Eugene at Bucket & Barter.");
                    break;
                case Stage.Frogs:
                    // Same as EnterFrogs, but the waves carry on from the frogs already beaten.
                    SetStage(Stage.Frogs);
                    Signal(cavendish, false);
                    if (frogs != null)
                    {
                        if (saved == Stage.Frogs && save.count > 0) frogs.Resume(save.count);
                        else frogs.Begin();
                    }
                    UpdateFrogObjective();
                    break;
                case Stage.FrogsDone:
                    SetStage(Stage.FrogsDone);
                    Signal(cavendish, true);
                    GameplayOverlay.Instance.SetQuest(PondTitle, $"All {(frogs != null ? frogs.Total : 20)} frogs cleared. Report back to Cavendish.");
                    break;
                case Stage.ChiefToad:
                    EnterChiefToad();
                    break;
                case Stage.ToadBeaten:
                case Stage.TrophyPlaced:
                    if (!crownPlaced && !pockets.HasKeyItem(SaveCrownId)) pockets.AddKeyItem(SaveCrownId);
                    if (!partGiven && ShipPartDrop.CountFor(StageProgress.Tutorial) == 0)
                        ShipPartDrop.RecordRecovered(toad != null && toad.Toad != null ? toad.Toad.PartName : "Cooling system", StageProgress.Tutorial);
                    partSeen = ShipPartDrop.CountFor(StageProgress.Tutorial) > 0;
                    SetStage(Stage.ToadBeaten);
                    FinishLootOrUpdate(); // moves on to TrophyPlaced when both jobs were done
                    break;
                case Stage.DemoComplete:
                    SetStage(Stage.DemoComplete);
                    Signal(cavendish, false);
                    GameplayOverlay.Instance.SetQuest(ToadTitle, "Chief Toad defeated. Thanks for playing the demo!", true);
                    break;
                default:
                    BeginGreeting();
                    break;
            }
            // Watch the loot from here, without an autosave for what the load itself put back.
            lastPartCount = ShipPartDrop.CountFor(StageProgress.Tutorial);
            lastPartGiven = partGiven;
            restoring = false;
        }
    }
}
