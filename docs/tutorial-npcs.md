# Tutorial NPCs and first quest

> **Superseded (10-22-26):** the tutorial is now a new scene, `Scenes/Tutorial/Tutorial.unity`; see [tutorial](tutorial.md). Testing Sandbox is a free-play sandbox on the [shared player](core-player.md). The tutorial-only player, quest flow and installers named below (`TutorialPlayer`, `TutorialQuestFlow`, `TutorialGameplayBuilder`, `TutorialCoreLoopInstaller`) and their smoke harnesses were removed. Their behaviour lives on in the shared components.

> **Partly superseded (10-20-26):** quests now run Gear Up, then Hair of the Crab. Dialogue uses subtitles, speech bubbles and code-built popups instead of the TutorialNpcWindows prefab (removed). The friendly crab leads the player home and opens the Bucket & Barter menu, and it calls an ambush if hit. See [tutorial core loop](tutorial-core-loop.md) for the current behaviour.

Open `Assets/Scenes/Testing Sandbox.unity` in Unity **6000.6.2f1**. Gameplay is already authored; do not rerun creation tools. This implements the design document's Cavendish quest giver and flask-crab-to-merchant introduction, with the project lead's interaction clarifications.

## Play the sequence

1. Start unarmed and without the flask. Walk up to **Cavendish**, aim at his body within 3 m, and press **E** (gamepad north button). His first quest opens a dedicated screen-space popup. Each E press advances one line; time alone never advances instructional dialogue. The final E accepts the quest and enables the merchant-crab fight. Closing with Escape or walking away before finishing leaves the quest unaccepted. Walking remains active and time does not pause. Screen-space popups free the cursor and suspend mouse look; closing restores capture when focused.
2. Cavendish explains the crash and asks you to recover your flask from the larger crab with the copper trading shell. After acceptance, **Recover Your Flask** appears in the HUD’s top-right tracker and updates through weapon pickup, crab defeat, flask collection and completion. Pick up the existing mining laser from the rock basin with E; collecting the laser before talking is also allowed.
3. Find **Merchant Crab**, initially near world position **(54.48, 0.09, 4.80)**. It is 1.6 times the original crab's scale, with a copper bucket, brass armor plates, teal cargo satchels, antenna and a visible flask on its back. Talking to Cavendish activates the fight. Before acceptance it is protected. No merchant interaction prompt appears before defeat, including during combat. Approaching within 9 m triggers a one-time overhead flavour greeting; combat taunts trigger once at 75%, 50%, and 25% remaining health and auto-advance overhead. These lines contain no quest instructions.
4. Defeat it using the existing melee/charged-shot combat. It uses the existing crab AI, shell defense and movement, with **500 HP**, **2.1 m attack range**, and **2.5 m attack reach** for its larger size. Other crab damage/behavior tuning is reused.
5. It surrenders through overhead flavour text and drops the existing flask as a one-time E pickup. Reward-collection and trading instructions are reserved for an interacted popup. The carried flask disappears. The old loose flask is unavailable before this defeat.
6. After a short recovery, the same crab walks along the NavMesh to stand beside Cavendish, near **(43.29, 0.09, 4.78)**. It is now friendly and protected from damage. Collect its dropped flask with E, then use F to drink/heal with the existing intoxication rules.
7. The first E on the friendly crab gives a short overhead greeting. Press E again to open the instructional popup: if the flask is still uncollected it explains how to recover and use it, followed by trading information. Each line waits for E. Finish those lines to open **Bucket & Barter**, a dedicated screen-space shop window on a separate canvas. It is not parented to, embedded in, or positioned by the crab model. Supplies, Upgrades and Trade Scrap buttons are disabled. Balance is shown as unavailable; no scrap is created, spent or tracked. E closes the shop even when you are not aiming at the crab; Escape or moving more than 6 m from the NPC also closes it. The window stays fixed on screen while you look or move. All purchase buttons remain disabled.

Talk to Cavendish again for quest reminders or completion instructions in an E-driven popup. After finishing his completion conversation, subsequent non-quest small talk appears above his head and waits for E. Combat banter and the crab’s immediate surrender chatter remain automatic. Dialogue is text only, with no audio recordings.

## Reset and persistence

Quest state lasts for the current Play session, not across save/load or restarting Play Mode. Before defeating the merchant, death/blackout retains the accepted quest and collected laser, and restores the merchant at its original fight position/full health. After defeat, the merchant stays friendly; the single dropped flask remains available until collected. Once collected, it remains owned through the existing tutorial respawn flow. The NPCs are outside the ordinary stage-enemy reset group so the friendly merchant does not become hostile again.

The existing three dummies and all regular crabs/frogs are preserved. No boss, shop economy, dialogue-choice system, new quest rewards or transitions to later levels are introduced. Both NPCs stay in this tutorial for now; their future appearances in other levels remain integration work.

## Unity-rendered previews

- [Merchant crab](../art/merchant-crab/Previews/MerchantCrabPreview.png)
- [Quest popup](../art/merchant-crab/Previews/QuestPopupPreview.png)
- [Top-right tracker](../art/merchant-crab/Previews/QuestTrackerPreview.png)
- [Screen-space shop](../art/merchant-crab/Previews/MerchantShopPreview.png)

## Assets and authoring

- `Assets/Prefabs/NPC/MerchantCrab.prefab`: variant of the existing crab; larger scale, carried flask, NPC interaction, shop controller and world-space speech. The old crab-attached shop panel has been removed.
- `Assets/Prefabs/NPC/MerchantShell.prefab`: decorated version of the existing bucket shell, with new native primitive details and matching materials under `NPC/Materials/`. No third-party assets were imported.
- Cavendish's interaction and dialogue are scene additions to the existing capybara instance. His shared art prefab is unchanged.
- `Assets/Scripts/NPC/`: quest flow, NPC interaction, manual/automatic world dialogue, merchant state and placeholder shop.
- `Assets/Prefabs/UI/TutorialNpcWindows.prefab`: independent screen-overlay quest/conversation window, shop window and top-right tracker. Assign its player and NPC controller references in the scene.
- E is routed to an open NPC window before checking new world interactions. Windows cancel on Escape, walking outside 6 m or player respawn; cancelled conversations do not run their completion callbacks.
- `TutorialPlayer` keeps using the existing Interact action and one 3 m interaction ray. Pickups and NPCs are resolved by the nearest collider, so talking through walls or at arbitrary distance is not allowed.
- The tutorial navigation volume is extended to 150 x 50 x 150 m to connect the fight and return point. Rebake **Tutorial Encounter Navigation** if you change terrain/collision. Its binary data keeps the existing exact-path LFS rule.
- `Health.Invulnerable` is runtime-only protection for the quest crab before acceptance and after surrender; all ordinary actors remain damageable by default.

The dedicated merchant prefab has scene reference slots for its return point and dropped flask; assign them when placing it in another scene. Quest and merchant state are tutorial-specific prototypes rather than a general persistent quest/inventory framework.

## Test checklist

- E near Cavendish opens the quest popup. Wait more than six seconds: the line must stay fixed. Advance with E, then finish to accept the quest and show its top-right tracker. Walking still works; mouse look pauses while a popup owns the cursor. Distant or blocked initial interaction fails.
- Before acceptance, the quest crab cannot be killed and the loose flask is unavailable. After acceptance, it navigates and fights normally. Its E interaction prompt must remain hidden until defeat.
- Defeat it: one flask drops, its carried flask disappears, surrender lines play, it recovers, walks back and stops beside Cavendish.
- Pick up the reward and heal. Repeated interaction/death must not duplicate the flask or reset the friendly merchant.
- Check player death and ninth-drink blackout both before and after surrender.
- Advance the merchant introduction with E, browse the screen-space shop, confirm every purchase row is disabled, then close it with E, Escape, or walking away. The shop stays fixed on screen with a free cursor. Non-quest Cavendish small talk remains above his head and advances only with E; quest reminders and completion instructions use popups.
- Check existing dummies, regular enemies and other scenes still behave normally.

## Validation

The original NPC integration passed Unity **6000.6.2f1** compilation and isolated Play Mode checks for ranged interaction gating, quest assignment/reminders, protected pre-quest crab, real crab AI combat, flask drop/collection without duplication, friendly return walk, disabled shop controls and walk-away close, death before/after surrender, ninth-drink blackout after completion, and preserved counts of existing actors. Rendered merchant and shop previews were inspected. Tests relocate the player, call existing pickup/drink methods, and apply lethal damage through the Health backend; they are not physical keyboard/gamepad or full weapon-balance tests. The known editor-only SearchDatabase startup exception is excluded; gameplay errors fail the run. Human playtesting should check dialogue pacing, encounter difficulty, E/Escape handling and readability while moving. Updated dialogue/UI validation additionally checks timed waits cannot advance instructions, cancellation cannot accept a quest, E advances screen and world conversations, combat chatter still auto-advances, the accepted quest tracker anchors top-right, the hostile merchant has no prompt, and all windows are on an independent overlay canvas. Updated renders cover the quest popup, tracker and shop.

The earlier `TutorialGameplaySmoke`/`TutorialEnemiesSmoke` harnesses describe the pre-quest tutorial sequence; they assume a loose flask at startup. Use the NPC-specific harness for the new sequence rather than interpreting that historical assumption as a regression.

Reproduction: copy [TutorialNpcSmoke.cs](../tools/unity/TutorialNpcSmoke.cs) and its [setup helper](../tools/unity/TutorialNpcSetup.cs), plus `TutorialEnemiesSetup.cs` for its temp-directory guard and [TutorialNpcUiSetup.cs](../tools/unity/TutorialNpcUiSetup.cs) for screen-space UI authoring, into `Assets/Editor` of a disposable matching project under the system temporary directory. Run `-batchmode -nographics -projectPath <temp-project> -executeMethod TutorialNpcSmoke.Start -logFile <log-path>` without `-quit`. Use `-force-metal -npcRender` without `-nographics` on macOS to capture the merchant and shop. `TutorialNpcSetup.Create` is only for a pre-NPC tutorial copy and refuses an already-integrated scene. Never install these test tools in the real project's Assets.

### Interaction recovery in an already-open editor

If E reaches Cavendish but the Console reports a missing reference in `TalkToCavendish`, an older in-memory scene may be missing the new NPC UI bindings. Exit Play Mode and use **Signal Lost > Tutorial > Repair NPC UI Bindings**. This adds the existing UI prefab only if missing and reconnects quest/player/shop/Cavendish references while preserving unsaved scene objects. It marks the scene dirty rather than overwriting it from disk; save after reviewing. The repair also runs after scripts reload and after exiting Play Mode in `Tutorial.unity`. Runtime reference resolution recovers missing links when the UI is already present.

[TutorialNpcInteractionSmoke.cs](../tools/unity/TutorialNpcInteractionSmoke.cs) recreates the stale scene/missing canvas, verifies preservation of an unsaved object and no duplicate UI on repeated repairs, then clears runtime references and tests the real player interaction ray through Cavendish's quest acceptance.

### Dialogue routing

Overhead text is for the crab's first approach greeting, combat taunts, surrender flavour and first friendly greeting. First approach and combat lines can auto-cycle; the first friendly greeting waits for E before opening the popup. Quest assignment, reminders, reward pickup/healing instructions and quest completion always require interaction and use screen-space popups. Hostile crab interaction prompts remain disabled until defeat. All existing quest tracker, reward, merchant return and reset behavior is retained.

### Merchant combat dialogue thresholds

Combat sections trigger on damage at **375, 250, and 125 HP** (75%, 50%, and 25% remaining). Each section plays once per attempt; hits crossing multiple thresholds queue sections in order. Defeat cancels queued combat lines and immediately starts surrender dialogue. Respawning before defeat resets the thresholds. The approach greeting remains proximity-triggered, and quest/shop instructions still require E. Edit the sections in `HealthDialogueLines` and their health fractions in `HealthDialogueThresholds` in `MerchantCrab.cs`.

Quest and shop popups release and show the cursor, suspend mouse look and weapon input, and prevent menu clicks from recapturing it. WASD movement remains active. E advances/closes the popup; Escape, walking out of range, respawning or disabling the UI closes it and restores mouse capture when focused. Overhead chatter and the HUD tracker do not release the cursor. Shop purchases remain disabled placeholders.

After surrender, aiming at the friendly merchant no longer displays combat health text, including after its health is restored.
