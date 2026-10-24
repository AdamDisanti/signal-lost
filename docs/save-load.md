# Save and load

Three save slots, autosaves as you play, and manual saves from the pause menu anywhere outside combat (Christian, 10-24). Loading gives you back **the game you saved**: where you stood, your health and drinks, everything in your pockets, the scrap on the ground, and the enemies you'd already beaten still beaten.

## How it plays

- **Title screen:**
  - **New Game** opens the three slots. Pick an empty slot, or click a used one twice to erase it and start over there.
  - **Continue** loads the most recent save. The note beside it shows that save's quest.
  - **Load Game** opens the slots to pick any save.
- **Autosave** writes to the game's slot whenever the quest moves on, after every purchase, after picking up gear, and after handing over the cooling system or placing the crown. A small "Autosaved (slot N)" note fades in at the bottom right. An autosave that comes up during a fight waits until the fight is over.
- **Pause menu > Save Game** opens the slots. Saving into the game's own slot is one click; saving over a different save needs a second click.
- **When you can't save** (the pause menu says why):
  - during the opening or the Chief Toad's cutscene;
  - while knocked out, or in the middle of a conversation;
  - in mid-air;
  - in a fight: an enemy has you inside its fight radius, a boss is engaged, the angry merchant's cousins are out, or you were hit in the last 6 seconds.
- **Loading** puts you where you stood when the save was made, whether you saved by hand or it autosaved (an autosave that waited for a fight records where you were once it saved). Enemies still standing are back home at full health, which is where they'd be anyway: you can only save once you're clear of them. You come to with the tutorial's full wake-up (about 7 seconds: eyes opening, lying back, sitting up) and a mumble: "Mmh... must've dozed off." / "...right. Where was I?"

## What a save remembers

| Kept | Where |
| --- | --- |
| Where you stood and which way you faced | `TutorialStory.Capture` |
| Health, drinks in your system and how long until the current one wears off | `SaveGame.RestorePlayerState` (`Health.SetCurrent`, `PlayerFlask.RestoreStacks`) |
| Scrap lying on the ground, where it lay (its 2-minute timer starts again) | `SaveGame.RestoreDroppedScrap` |
| The tutorial's story stage, and whether the cooling system was handed over | `TutorialStory.Capture` |
| Progress inside the step: which Bucket Brothers are down, whether the big crab gave up, how many frogs are beaten | `TutorialStory.Capture` |
| Time played and knockouts (the run timer carries on) | `TutorialRunTimer.Resume` |
| Gear, scrap, upgrades, supplies, key items (the crown) | `PlayerInventory.Restore` |
| Recovered ship parts, cleared and unlocked stages, main parts | `ShipPartDrop`, `StageProgress` |
| The camp's trophies | `CampSave` (each slot has its own copy) |

Not kept: wounded enemies' health (they heal when you leave them anyway) and the merchant's anger.

Because beaten enemies stay beaten, a load never hands out the same loot twice: saving with ten cousins' scrap and loading again doesn't bring the cousins back.

## Where you pick up

| Saved during | Loads at |
| --- | --- |
| The greeting | Cavendish's greeting |
| Gear hunt | Gear hunt; tools already found stay in the pockets and are gone from the wreck |
| Big crab fight | The fight, minus the cousins already beaten. If the crab had given up, it waits for the rest and the flask lies beside it (unless you'd picked it up) |
| Following the crab home, or its introduction | Eugene already home at camp, ready to introduce the shop; the flask is in the pockets |
| First upgrade, Show Cavendish | That step |
| Pond frogs | The wave you'd reached, with only the frogs you hadn't beaten |
| Frogs done | Report to Cavendish |
| Chief Toad quest or fight | The quest: he waits under the pond again (he resets when you leave his fight anyway) |
| After the fight | The loot jobs; a crown or cooling system never picked up is in the pockets |
| Trophy placed, or the demo's end | Talk to Cavendish, or free roam after the end |

## Files

- Slots: `signal_lost_save_1.json` to `_3.json` in the game's data folder (`Application.persistentDataPath`, the same folder as `tutorial-times.csv`). They are plain JSON, so they can be read or deleted by hand.
- `Scripts/Progression/SaveGame.cs`: `SaveData` (what a slot holds), `ISaveableLevel`, and `SaveGame` (slots, autosaves and the wait for a fight to end, New Game, loading).
- `Scripts/Enemies/Common/CombatCheck.cs`: is any enemy fighting the player? (alive, with the player inside its fight radius, or a boss engaged).
- `Scripts/UI/SaveSlotPanel.cs`: the slot picker used by both menus.
- `Scripts/UI/SaveIndicator.cs`: the "Autosaved" note (created by `GameSystems`).
- Changed:
  - `TutorialStory` (implements `ISaveableLevel`);
  - `MerchantCrab.RestoreFriendly` and `RestoreSurrendered` (the flask drop moved into `DropFlask`);
  - `FrogWaves.Resume`, `ChiefToadEncounter.RestoreDefeated`, `PlayerGear.Sync`;
  - capture/restore methods on `PlayerInventory`, `CampSave`, `StageProgress`, `ShipPartDrop` and `TutorialRunTimer`;
  - `Health.SetCurrent` and `PlayerFlask.RestoreStacks` / `StackSecondsLeft`;
  - `PauseMenu` and `MainMenuScreen` for the menus.

## Adding a level (the desert, later)

1. Have the level's story script implement `ISaveableLevel`: call `SaveGame.Register(this)` in `OnEnable`, `SaveGame.Unregister(this)` in `OnDisable`, and `SaveGame.Tick()` in `Update`.
2. In `CanSave`, refuse during cutscenes and fights (`CombatCheck.EnemyEngaging`).
3. In `Capture`, set `data.scene`, a `data.stage` of your own, a `data.label` for the slot list, the player's position, and any progress inside the step (`downed`, `count`, `flags`).
4. In `Start`, call `SaveGame.TakePending()`. If it returns a save, call `SaveGame.RestorePockets`, refresh the gear (`PlayerGear.Sync`) and upgrades (`PlayerUpgrades.Apply`), then `SaveGame.RestorePlayerState` and `SaveGame.RestoreDroppedScrap`, put the player at `data.position`, and set the world up for that stage instead of starting fresh.
5. Call `SaveGame.Autosave()` at the level's milestones.

Pockets, health, drinks, scrap on the ground, parts, stage progress and trophies are saved for every level automatically.

## Notes

- Pressing Play straight in a level (not from the title screen) has no slot yet, so nothing autosaves until you pick one with Save Game.
- A loaded run's row in `tutorial-times.csv` only has split times for stages reached after the load.
- `SaveData.version` is 1. Bump it if a change would make old saves load wrongly.

## Test checklist

1. Title screen with no saves: Continue and Load Game are greyed out and the note reads "(no save yet)".
2. New Game > slot 1. After the opening, "Autosaved (slot 1)" appears. Find one tool, walk somewhere, save from the pause menu, quit to the title screen, Continue: you're standing where you saved, the tool is in hand, and it is gone from the wreck.
3. In the big crab fight, beat four cousins, run well clear, then save. Try saving up close first: the pause menu says enemies are nearby. Load: only the six other cousins are there, and the scrap from the four is still in your pockets.
4. Beat the crab before the cousins, save once clear, load: the crab waits beside the flask, and leads you home when the last cousin is down.
5. Buy an upgrade, quit, Continue: the upgrade is installed and the scrap spent.
6. Take some damage, have two drinks, get clear and save. Load: same health, two drunk pips, and the top pip still draining.
7. Beat a cousin, run off before walking over its scrap, save, load: the scrap is lying where it dropped.
8. Beat 7 frogs (into wave 2), get clear, save, load: wave 2 carries on with 5 frogs and the tracker reads 7 / 20.
9. Get hit by a frog and open the pause menu straight away: saving is refused for a few seconds. Start the Chief Toad fight: its autosave shows "Autosaved" only once you've got clear of him (he resets) or beaten him.
10. Beat the Chief Toad without picking anything up, quit, Continue: the crown and cooling system are in your pockets, and the loot steps can be finished.
11. Place the crown, then New Game in slot 2: the trophy stand is empty. Load slot 1: the crown is back on its stand.
12. Pause > Save Game into slot 3 (two clicks if it has a save), then Load Game shows all three slots with their quest, time played and date.
13. Try to save during the opening, the toad's rise, a conversation, and mid-jump: each is refused with a reason.
