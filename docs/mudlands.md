# The Mudlands

Christian's request (11-08): a new biome, **The Mudlands**, built around a new ability, **Drunk Driving**, following the same loop as earlier areas: base camp, Cavendish's quests, three mini-bosses, then a stage boss. Everything is generated in code and editor builders, like the tutorial and the desert; there are no imported assets.

- Scene: `Assets/Scenes/Large Areas/Mudlands.unity`, built by **Signal Lost > Mudlands > Build Mudlands Level** (`Editor/MudlandsBuilder.cs`).
- Code: `Assets/Scripts/Mudlands/` (Driving, Enemies, Bosses, Story, Art).
- Branch: `feature/mudlands`.

## Status (11-08)

Built and run in Unity 6000.6.2f1 on Christian's PC (build menu, then Play in the scene, with a private debug menu for teleports and boss damage). **Seen working in Play mode:**

- The level builds in about 15 s with no errors: terrain, roads, creek and bog water, buildings, about 900 trees, rocks and plants, grass, lighting and fog, the NavMesh, the camp, Cavendish and Eugene, all quest objects, creatures and bosses.
- Cavendish's greeting and the three-line tracker; the driving-school quiz (every answer passes) and the permit card; reporting back; the practical's barricades and still; directions to Kart Kong; the second line's report.
- Drunk Driving: hold F to chug (the scotch flask in the left hand, chug bar, head tilt), the car drops in, the chase camera, the in-car panel (flask, car health, speed), driving at up to 21 m/s, a head-on crash that blows the car up and throws you clear, the cooldown pip.
- All four boss entrances with their name cards: Kart Kong (traffic light and lap), Silt (tea rock), Granny Drowse (lanterns go out, "*yawn*", cackle), Clutch McCheddar (neon, ride-in, the Akira slide, gang).
- Kart Kong: a heavy hit breaks the kart ("MY GO-KART!!!", wheels fly off, "enraged"), the phase names, his death and drops (ship part, Kart Wheel, scrap).
- Silt raising an earth wall in front of the car. Granny Drowse hovering and throwing hex orbs. Clutch's gang and "YEEHAW"s.
- Clutch's getaway at 15% ("HE'S GETTING AWAY! Hold F and RAM HIM!", free car, chase bar), riding on the ground along the causeway, and ramming him to finish the fight. If he reaches the gate he heals to 33% and comes back for round two.
- The full story from a fresh start to the end: all three lines' reports (lines whose jobs were done early catch up when you talk to him), all four bosses killed, every drop (ship parts, Kart Wheel, Mudstone Bead, Witch Ember, Antigrav Coil, Landing Gear, Clutch's Aviators) on the ground where it can be picked up, the Landing Gear handed to Cavendish, and "QUEST COMPLETE: The Mudlands is clear".
- The dam and the flood draining at the chapel; breaking a cursed lantern releases its critters (they burst out); the creature models (toad, hog, possum, turtle, mudpuppy); the shop's Car tab with the new upgrade icons, and buying the Reinforced Bumper; dying mid-fight resets the boss and respawns you at camp.

**Fixed in the polish pass (11-08 afternoon):** components saved in the scene must sit in a file of the same name (the quest objects, scenery, boss encounters and no-parking zones were split into their own files); a private field named like one in the base class broke Clutch's serialization; switching off the walking controller in the car freed the mouse, so the car ignored input; the driving HUD only appeared after the quiz; boss entrances now trigger inside the arena so the fight starts when they end; swamp water rendered as white foam; Clutch's getaway was uncatchable (he now idles at 9 m/s until you are in a car, then rubber-bands between 11 and 18 m/s; the chase car does 26); the chase car now works even without the Learner's Permit; a free story ride no longer leaves rides free afterwards, and the bottomless chase car no longer starts the cooldown; boss drops and dropped props (barrels, banana peels, telegraph rings) snap to the ground under them, never onto a creature, the car or a treetop, and key items land on the NavMesh where you can walk to them; Cavendish catches up on jobs done before he asked; agents re-enabled off the NavMesh gave warnings; symbols the font lacks; the held scotch bottle was too big; the upgrade shop had no icons for the ten Mudlands upgrades (`Editor/MudlandsUpgradeIcons.cs`, also merged by the existing **Build Upgrade Assets** menu through a one-line hook in `UpgradeAssetBuilder.cs`).

**Second round (11-08 evening, from Christian's play-through):** the player no longer pokes out of the car (hidden while driving; the cabin got side windows); the chug drains the scotch flask instead of a bottle; all four bosses are rebuilt smoother (smooth-shaded, finer round parts, `MeshKit.Smooth`) and have faces that act (`Bosses/BossFace.cs`: brows, eyes or eyelids and an opening mouth, blinking, eyes following the player or the entrance camera, a mood per line they say, a pained flinch and a squash of the body when hit, breathing); Granny Drowse rises out of an open cauldron through a column of green steam, and her broom (longer than the pot is wide) shoots up beside her on end and flips in under her; Clutch's slide is shot from the road in front of him, like the reference frame; his donuts start from where he is (he used to jump to the ring), and his moves turn and lean into their poses instead of snapping; a boss is no longer moved back to its spot when its entrance ends (the witch used to drop from the air to the ground). The glowing mushrooms flashed a blinding white blob as the camera moved: the tip of their caps was a sliver (a ring of radius 0.0001 closed with quads), which the GPU can't interpolate across, so it wrote NaN pixels that the bloom smeared over the screen. `MeshKit.Lathe` now closes any ring under 2 mm with single triangles (this also fixed the cauldron, the witch's cloak and Clutch's snout); the mushrooms keep their glow. Glowing materials are also matte now, and see-through ones less glossy.

**Clutch rework (11-09, from Christian: "a true akira slide... make the animation way better and make the model more detailed... the most grand one in the mudlands"):** the rig, model, entrance and fight animation above. Seen in his Unity (6000.6.2f1, 0 errors): the whole entrance, frame by frame from the cutscene camera (pass-by, tracking shot, the slide away from the lens in slow motion, kick-up, aviators, the climax with the gang and the card); photos of the model from 9 angles; each move forced with the debug menu (ram, donuts, rooster, gang, whip, ramp jump with the no-footer, nitro), the phase-2 chain draw and rally, a wipe-out to the remount, death in the saddle, and over the handlebars (forced, not with a real car chase). Debug menu (git-ignored): **Ctrl+Shift+Alt+F4** saves frames from whatever camera is showing (cutscenes), **F10** wipes him out, **F11** forces his next move, **F12** rams him off his bike, **F3** photos.

**White flashes on the witch and Clutch (11-09 evening):** the same blinding bloom flash the mushrooms had. Cause: `MeshKit.Tri` used `Vector3.Normalize`, which returns a zero vector for anything shorter than 1e-5, so every triangle under about 4 mm across (rivets, studs, whiskers, an earring, the witch's wand and hat details) got a zero normal; the shader turns a zero normal into NaN pixels and the unclamped bloom smears them across the screen. `Tri` now divides by the length itself, and smoothed normals fall back to the face normal instead of ever being zero. A debug check (**Ctrl+Shift+Alt+Home**, Check Mesh Health) scans every cached Mudlands mesh for NaN or zero normals and sliver triangles: 9 meshes flagged before (all witch and Clutch parts), 0 after.

**Not yet checked by hand** (the code is written and compiles; needs a real play-through): every enemy's moves one by one, Silt's and the witch's full move sets, the clone beam, the witch's ragdoll fall, catching Clutch with ordinary driving (the ram was tested with the debug menu placing the car), Clutch's donuts since the fix (not caught on camera), the witch's face up close in a fight (she was only seen from below and in her entrance), buying each upgrade other than the bumper, and the overall difficulty.

**Placeholders, on purpose:** the driving HUD, boss title cards, quiz and chase bar are drawn with Unity's immediate-mode GUI, not the game's HUD style; the 23 new sounds are synthesized; all numbers are first pass; saving waits for the save branch (see 8).

The Mudlands is not in the design document's biome list (Desert, Fog Forest, Crystal Caverns). It is treated as an extra biome after the desert; see [Open questions](#open-questions).

## 1. What already exists, and what the Mudlands reuses

| System | Where | How the Mudlands uses it |
| --- | --- | --- |
| Shared player | `Prefabs/Core/Player.prefab` | Unchanged. Drunk Driving attaches itself to the player at runtime (`DrunkDriving.Ensure`), so no prefab edit is needed and the ability works in every scene once the permit is owned. |
| Movement | `FirstPersonController` | Walk 4, sprint 7, jump 1.2 m, crouch, slide, slide-jump, jump smash. Enemies are tuned against these (sidestep windows, jumpable rolling barrels, crouch-quiet for the otter). |
| Laser | `MiningLaser`, `LaserBolt`, `AreaBlast` | 3-hit combo (finisher stuns small enemies 1 s), charged bolt 10-70 with a 1-6 s cooldown, 4 m full-charge burst. Bosses read "charged" as a non-melee hit of 20+ and "full charge" as 95% of `MaxShotDamage`, so the laser itself is untouched. |
| Flask | `PlayerFlask` | Drunk stacks are the ability's cost. Two small hooks were added: `CancelSip` (a hold turns into a chug before the sip lands) and `AddDrinks` (the chug adds drinks without healing). |
| Hands | `FirstPersonHands` | The chug reuses `PoseDrink(raise, gulps)`, the hook Christian left for hold-to-chug, with the same scotch flask the sip uses. |
| Enemy base | `DesertFodder` | Every Mudlands creature derives from it, so the fighting radius, stun rule, hit flash, spawners and no-friendly-fire rule (`EnemyAllegiance`) all apply unchanged. |
| Mini-boss base | `DesertMiniBoss`, `MiniBossHud`, `ShipPartDrop` | All four bosses derive from it: patrol, engage, boss bar with phase notches, leash/reset, part drop. |
| Boss cutscenes | `ChiefToadEncounter` | Same pattern (a temporary camera, HUD steps aside, Space skips), generalised into `BossEntrance`. |
| Story | `TutorialStory`, `INpcStory`, `DialogueUi`, `GameplayOverlay`, `QuestSignalSource` | `MudlandsStory` follows the same pattern: Cavendish's popups, the top-right tracker, scanner quest signals. |
| Shop | `UpgradeCatalog`, `PlayerUpgrades`, `MerchantMenuUi` | New region `Mudlands` and a new slot `Car`. Eugene is at camp, already friendly. Key-item recipes (already supported) tie three car upgrades to mini-boss drops. |
| Camp | `BaseCamp.prefab`, `CampSave`, trophies | The same camp; the stage boss leaves a trophy. |
| Fodder waves | `FodderSpawner` | Boss adds and roadside ambushes. |
| Effects and sound | `ParticleBurst`, `FireEffect`, `CameraShake`, `GroundMarker`, `Sfx` | Reused. New sounds (engine, horn, tyre skid, crash, kart horn, banjo) are generated WAVs in `Resources/SignalLostAudio`. |
| Saving | `CampSave` (main); full saves on `feature/save-load` | See [Saving](#7-saving). |

## 2. Layout

400 x 400 m of low-poly faceted bayou (+Z north), closed by a ring of tall, unclimbable cypress ridges. Teal-blue dusk with green-grey fog, warm torches and lanterns as the accent, purple water lilies, hanging moss (the two reference images). One continuous **ring road** of packed dirt links every region, so the car always has a way round; narrow spurs, bridges and barricades make it interesting.

| Place | Where (x, z) | What it is |
| --- | --- | --- |
| **Stilt Camp** (base camp) | (0, -140) | The base camp prefab on a dry knoll, Cavendish, Eugene's shop, the spawn point. Boardwalk north to the road. No-parking zone. |
| **Gumbo Flats** | (0, -45) | Wide open mud plain: the car's playground. Fence lines, hay bales, a broken windmill. Roadside ambush of Mudpuppies and a Hog Brute. |
| **Rotwood** (quest line 1) | (-110, -40) | Abandoned logging settlement: stilt shacks, a sawmill, the **Bayou Driving School** shack, and the **haul road** with four barricades and a moonshine still. |
| **Banana Speedway** (Kart Kong) | (-120, 75) | A muddy oval go-kart track with tyre walls, a grandstand, a barrel pile and a banana stand. |
| **Sinkmire Bog** (quest line 2) | (100, -45) | Islands in shallow bog water joined by sinking stepping stones; three spirit gongs; the mud dam. |
| **Still Water Dojo** (the otter) | (125, 50) | A flat rock-and-mud island with stone lanterns and a wooden gate, ringed by mud pits. |
| **Lantern Road** (quest line 3) | (0, 0) to (60, 105) | The road north, lined with lantern poles; five are cursed. |
| **Hag's Clearing** (Sloth Witch) | (75, 125) | A round bog clearing with dead trees, her crooked hut and a cauldron in the middle. |
| **The Greasy Spoon** (stage boss) | (-40, 125) | A roadhouse with a big muddy lot, gas pumps, hay bales, a dirt jump ramp. Gated until the three mini-bosses are down. |
| **Gator Causeway** | lot to (-165, -10) | The road the stage boss flees along, ending at a "COUNTY LINE" gate. |

**Hazards** (all affect both the player on foot and the car):

- **Mud** (dark wet patches): 45% slower on foot, and the car loses grip and speed. Swamp Waders and Mud Tires remove these.
- **Bog water**: wading speed on foot; the car stalls and fizzles out (it explodes, as every ride does).
- **Bog gas vents**: bubbling vents that burst for 25 in 3.5 m when a full-charge bolt, a fire or a car explosion touches them (player-credited, so they hurt enemies).
- **Moonshine barrels**: shoot or ram them; they explode (30 in 4 m) and leave a short fire.
- **Destructibles**: wooden barricades, fences, crates, hay bales, outhouses and the moonshine still. The car smashes through them; the laser breaks them too, it just takes longer.

## 3. Drunk Driving

### Unlock: the Learner's Permit

A key item (`learners_permit`) from quest line 1's first quest. Until it is owned, holding F is just a normal sip. Once owned it is permanent, everywhere (it lives in the pockets, which the save branch already saves).

### How it works

1. **Hold F** (the existing drink button). A tap is still a normal sip. Held past 0.3 s, the sip turns into a chug before it lands: the left fist tips the **scotch flask** bottoms-up and drains it in three big gulps, the head tips back and the drunk blur swells. The chug takes **1.6 s**. You can still walk (half speed). A hit of 15 or more spills it ("Spilled it!") and costs nothing but the attempt.
2. Flask drained, with a puff of smoke a **beaten-up muscle car** drops out of the sky around you, bounces on its suspension, and the view pulls back to a chase camera. You are inside it: the walking player is hidden for the ride, and the cabin has glass all round.
3. **Drive:** W/S throttle and brake/reverse, A/D steer, Space handbrake (drift), left click horn, **F to bail out** (blows it up where it is). Gamepad: left stick, A handbrake, X horn, d-pad up to bail.
4. **Run over small enemies** above 6 m/s: they are launched (each has its own reaction). Smash destructibles. Scrape walls at an angle for sparks.
5. **Crash** head-on (above 9 m/s) into a large enemy, a mini-boss, a boss or anything unbreakable, and the car **explodes**.
6. **The explosion**: 60 damage at the centre, 30 at the edge, 6 m radius, a shove, and a **2.5 s stun** on everything caught. It never hurts the player.
7. The mechanic is thrown out in a comic tumble, lands unhurt, and is back on foot.

Every ride ends in an explosion: the "fuel" is the flask, a **14 s** meter. When it runs dry the engine sputters and the car blows. The car has **80 HP** of its own; enemy attacks hit the car instead of you, and at 0 it blows.

### Feel

Arcade raycast car: four suspension rays, ~22 m/s top speed (three times a sprint), 0-20 in about 2.5 s, speed-sensitive steering, a handbrake that kicks the rear out, lower sideways grip on grass and much lower in mud (it slides), body roll and pitch, wheels that spin and steer. Drunk stacks add a gentle steering wobble. The chase camera widens its field of view with speed and shakes on impacts; mud and sparks spray from the tyres.

### Balance

The existing "resource" in the game is drinks before a blackout, so that is the cost:

- **Cost:** the chug adds **2 drinks** and heals nothing. Your next sips heal less and you are more impaired after the ride. Using the car when you will need to heal is a real choice.
- **Can't chug** if the 2 drinks would black you out ("Too drunk to drive. Wait it off."), so the ability never blacks you out.
- **Cooldown: 30 s** after the explosion, shown on the car pip above the status bar.
- **Spill on hit** (15+) means you need a safe moment to chug.
- **Not usable:** without the permit, while drinking, stunned, respawning, in menus or cutscenes, in the camp's no-parking zone, or without room for the car.
- **Softlock guards:** a car upside down for 2 s, stuck for 3 s, in deep water or below the map blows up; the player is always put back on solid ground near the car; a cutscene starting mid-ride parks the car without an explosion; the boss chase hands out a free car.

### Enemies and bosses

`IVehicleReactive` lets each enemy answer a hit: **run over**, **bounce** (shoved, no explosion) or **explode** (it is too big). Enemies without it: mini-bosses and the stage boss explode the car; desert fodder, frogs and crabs are run over. Explosions reach mini-bosses through `ICarBlastReactive`, so each boss decides what a blast does to it (the kart breaks, the witch falls off her broom, and so on).

## 4. Quest lines

At camp Cavendish offers all three lines at once; take them in any order and work on several at a time. The tracker lists every active line, and the scanner points at the nearest live objective. Each line ends the same way: report back, Cavendish gives directions to its mini-boss, the mini-boss has its own entrance, you beat it, its ship part goes to Cavendish "for safe keeping".

| Line | Quests | Teaches | Mini-boss |
| --- | --- | --- | --- |
| **1. Learner's Permit** | **The Written Test**, then **The Practical** | Drunk Driving | Kart Kong |
| **2. Still Waters** | **Three Gongs**, then **Break the Dam** | Movement, positioning, the environment | Silt the Mudbender |
| **3. Lights Out** | **Cursed Lanterns** | Aiming high, ramming poles | The Sloth Witch |

**1a. The Written Test.** The Bayou Driving School in Rotwood is overrun with Mudpuppies, and a Jug Possum is lobbing moonshine from its roof. Inside, the old test kiosk (E) gives a three-question comedy quiz; every answer passes. The kiosk stamps your **Learner's Permit**, and an item card explains Drunk Driving.

**1b. The Practical.** "PROPERTY OF KART KONG RACEWAY": four plank barricades close the Rotwood haul road, with a Roadblock Turtle and possums behind them, and a moonshine still at the end. Break the barricades and blow up the still. The car does it in one pass, but each barricade has health, so the laser works too.

**2a. Three Gongs.** Three spirit gongs stand on islands in Sinkmire Bog, reached over stepping stones that sink if you stand on them for 1.5 s. Ring each one (E). Each gong wakes its guards (a Hog Brute, a Banjo Toad with Mudpuppies, a Roadblock Turtle).

**2b. Break the Dam.** The otter's mud dam has flooded the old bog chapel. The flood water sits in a hollow 0.6 m deep with a low bank round its rim (`Height`, `FloodRadii`), so its edge never floats where the land dips; the road dips into it. Blow it with the stacked moonshine barrels at its foot (a bolt, or ram them with the car). The water drains and the dojo path opens.

**3. Cursed Lanterns.** Five green-burning lanterns on poles and roofs along the Lantern Road. Shoot them out with bolts (they are high up), or ram a pole with the car to topple it. Each one breaks with a shriek that releases a small cursed group.

**Stage boss.** With all three mini-bosses down, Cavendish points you at the Greasy Spoon; its gate swings open. Beaten, the stage boss leaves the **Landing Gear** (the stage's main ship part, for Cavendish) and a trophy, **Clutch's Aviators**, for the camp's stands.

## 5. Enemies

Five creatures plus the boss's biker mice. Each has a job in an encounter, a personality that shows in its animation, and its own answer to the car.

| Enemy | Role | HP | Behaviour | Counter | Car |
| --- | --- | --- | --- | --- | --- |
| **Mudpuppy** | Weak swarm | 20 | Salamander pups that lurk in mud with only their eyes showing (bolts pass over them), pop up in a pack when you come close, zig-zag hop in and nibble (5), then dive back. Sometimes plays dead after a hit, then jumps up and bites when you walk past. | Bait them up, then a full-charge burst or the combo. | Panics: scatters screaming with arms flailing and sometimes runs into another pup. Run over: launched with a squeak. |
| **Hog Brute** | Pressure | 110 | A boar in overalls with a fence-post club. Paws the ground and snorts steam (1 s), then shoulder-charges in a line (22); an overhead club slam with a ring (18). Charging into mud gets him stuck for 2 s. | Sidestep the charge, bait it into mud, hit him while stuck. | Too big: plants his feet and flexes. The car explodes on him (with Reinforced Bumper he is flattened instead). |
| **Jug Possum** | Ranged, uses buildings | 45 | Lives on porches and roofs, lobs flaming moonshine jugs in arcs (12 plus a short fire). Ducks behind cover while you charge a bolt. Shot down, it plays dead for 2 s, then scurries back up. | Bolts from an angle, or catch it while it plays dead. | Lies flat and plays dead: too flat to run over. Ramming its shack drops it off the roof. |
| **Banjo Toad** | Support | 50 | Strums a tune that speeds up nearby allies by 25% and heals them 3 a second (music rings, notes). Keeps its distance and hides behind brutes; up close it twangs a shockwave (6 and a shove). Allies boo when it dies. | Kill it first. | Runs off clutching its banjo; run over, launched with a twang. |
| **Roadblock Turtle** | Heavy, area denial | 160 | A grumpy snapping turtle wearing a plank barricade and road signs as a shell. Parks on roads, blows a whistle, waves a STOP sign. Bolts glance off the front of its shell; a slow lunge bite with a clear wind-up (25). A full-charge burst or jump smash at its feet flips it onto its back for 3 s. | Flank it, flip it, hit the belly. | Head-on, it hides in its shell and the car explodes. Rammed from the side or behind, it is flipped. |
| **Biker Mouse** | Boss adds | 35 | Small mice on mini bikes (choppers, dirt bikes, scooters; different jackets and helmets). Circle at speed, then swerve in for a chain swipe (8). Knocked off, they run after the bike, shaking a fist. | Hit them on the swing-in. | Swerve to dodge if they see it coming; run over, launched with the bike. |

Shared personality (`MudCritter`): a **"!"** pop and a startle hop when they first notice you, cheers when you take a hit near them, gasps or mourning when an ally dies close by, panic at a horn or an approaching car, idle fidgets, and a squash-and-stretch on every hop and landing. Each one has its own death (puppies curl up, the brute topples like a tree, the possum keeps "playing dead", the toad's banjo string snaps, the turtle spins on its shell).

**Encounters.** Mudpuppy dens on the Flats with a Hog Brute (the car's first playground); possums on the driving school roof over puppies; the haul road's barricades with a turtle and possums behind; gong guards in the bog; a brute pair arguing at a bridge (they stop and both charge); cursed lanterns release mixed groups with a Banjo Toad. There is no ambient respawning outside the fodder spawners.

## 6. Bosses

All four derive from `DesertMiniBoss` and have a cutscene entrance (`BossEntrance`: a moving camera, the HUD hidden, a name card, Space skips). Numbers are a first pass.

### Kart Kong (line 1, Banana Speedway), 520 HP

A huge, grinning silverback squashed into a comically small go-kart (an original design inspired by Christian's reference). The kart stage is the happy, nearly harmless one; once the kart breaks it never comes back mid-fight (only a full reset, which also restores his health), and everything he does after that is on foot.

- **Look (11-09 rework):** a posable rig (hips, spine, head, two-bone arms and legs placed by IK every frame). Barrel chest, silver saddle, pot belly, a self-awarded champion's belt with a gold "1" plate, a gold medal on a red ribbon, a checkered scarf with a flapping tail, the starter motor strapped on like a backpack, sagittal crest, heavy brow, flared nostrils, a too-small racing helmet with goggles on the brim, fingerless red racing gloves on knuckly fists, grabbing feet. The kart: tube frame, chrome nerf bars and bumpers, yellow side pods with a red stripe and checkers, a red nose with a "1" roundel, a tiny bucket seat, an engine with an exhaust that puffs smoke, a bicycle horn, a banana air freshener, a whip antenna with a springy checkered flag, treaded wheels that spin (the front ones steer) and a steering wheel that turns.
- **Entrance:** the start-line traffic light counts down red, red, green; he tears round the bend honking, waving one arm ("WHEEE!"), skid-stops on the line, waves, honks the horn bulb twice and gives a thumbs-up.
- **Happy phase (in the kart):** drives laps, steering and leaning into the turns, and shows off: waves, thumbs-up, fist pumps ("LAP 2!"), "VROOM!" hunches and one-armed "WHEEE!"s. Two goofy moves, both barely a scratch: **rev and ram** (stops, revs with smoking back tyres for 0.8 s, then charges nearly straight at you: a "BONK!" for 4 and a small shove; a miss skids out) and the **donut** (if you're within about 6 m: a ring warning, then he spins on the spot flinging mud: 3 inside 4 m, and it leaves a slowing mud patch). The kart absorbs 75% of damage.
- **Break the kart:** a charged bolt (20+), the combo finisher, a jump smash of 20+ or the car (crash or explosion). If he reaches half health still driving, the kart gives out then. The kart spins out and falls apart, he's thrown up and back flailing, lands on his backside holding the steering wheel, stares at it and sniffles, drops it, stands, beats his chest and screams **"MY GO-KART!!!"** (shake, roar), then pounds both fists into the ground. Enraged, on his knuckles (a real knuckle-walk; a gallop when charging, with thuds and dust on every hand plant):
  - **Barrel toss:** reaches behind his back and produces a barrel, hoists it overhead, hurls it with a step ("CATCH!"); it rolls at you (18). Jump it or break it. Red ones explode, hurting him if they go off near him.
  - **Swipe:** rears back with his arm cocked behind him (0.6 s), then a huge backhand across the front, 3.5 m (20, a shove).
  - **Ground pound:** when you're 3.6 to 7.5 m away: a ring on the ground, he rears up on his legs with both fists overhead and hops (0.85 s), then slams: 18 inside 5 m unless you're in the air ("Jumped it!").
  - **Charge rush:** stands tall drumming his chest ("HOO HOO HOO!", 1 s), then gallops in a straight line (28). Into a tyre wall: sits dazed with stars round his head (2 s).
  - **Banana snack:** below 70%, backs off on his knuckles, plops onto his backside, pulls out a banana, peels it in three pulls and munches (2.8 s, with a bar over his head). A charged hit or the car interrupts it (he chokes, fists at his throat, 1.5 s). If he finishes, he burps, heals 12% and drops the peel; charging over a peel slips him onto his back, legs kicking (2.5 s).
- **Furious** (half health): he stands, beats his chest and pounds the ground; wind-ups are a quarter quicker and he throws two barrels.
- **Defeat:** staggers, sits, flops onto his back; the kart horn bleats sadly (if somehow still in the kart, he slumps over the wheel). Drops the **Auxiliary Starter Motor** (ship part) and the **Kart Wheel** (Mud Tires recipe).

### Silt the Mudbender (line 2, Still Water Dojo), 480 HP

A blindfolded otter martial artist in loose green robes with a cream sash and wide sleeves, barefoot, fighting from a low horse stance (an original design from Christian's idea and reference).

- **Feels vibrations:** he aims at where you last made noise. Sprinting, landing, swinging and the car are loud; walking is quiet; **crouching** makes you nearly silent, so his throws land wide.
- **Entrance:** sipping tea cross-legged on a rock 3 m behind his fighting spot; a close-up on the blindfold; he sets the cup down, stamps, and a stone column pushes the rock up with mud cascading off. He stands, front-flips off it (spinning about his middle, so he clears the rock) and lands on his spot, then bends the rock: the column crumbles away and the boulder floats up over his head ("Catch!").
- **Opening throw:** the fight starts with the boulder overhead; he draws it back (0.8 s) and hurls it at you (22 in a 2.8 m ring, shove, bursts into rubble). If a car wall interrupts him, he throws it next. Skipping the entrance still leaves him holding it.
- **Stays on his dojo:** every spot he walks, hops or leaps to is kept within 11 m of the dojo's centre (inside the stone lanterns), so chasing him never drags the fight out past the leash. Too close, he hops back at most every 3.5-5 s (sideways along the edge when his back is to it); otherwise he **stamps** (0.55 s crouch with a 3.4 m ring, then 10 damage and a big shove), every 4-6 s.
- **Walk:** a stride cycle driven by distance (a step every 0.7 m): legs swing opposite each other along his travel direction (sidesteps shuffle), arms counter-swing, hips dip and roll each step, shoulders twist, tail swishes; he shuffles his feet turning on the spot.
- **Mud balls:** stamps and pulls a mud boulder from the ground (0.7 s), throws it (14); the splat leaves a mud patch. Phase 2: three in a fan.
- **Earth wall:** raises a wall between you; it blocks bolts and lasts 6 s, which is also cover for you. Against the car he raises it in the car's path at once and the car crashes into it, unless he is mid-throw. A full-charge bolt or jump smash breaks a wall.
- **Earth rush:** punches the ground; a line of rock spikes travels at you over cracking ground (20, launch). Jump or sidestep. Winded for 1.2 s afterwards.
- **Phase 2:** **pillar leap** (a pillar raises him, then an elbow-drop ring, 24) and **listening stance**: melee from the front is parried with a palm strike (12, big shove); from the side or behind, it lands.
- **Drops** the **Gyro Stabilizer** and a **Mudstone Bead** (Shockwave Shocks recipe).

### The Sloth Witch, Granny Drowse (line 3, Hag's Clearing), 420 HP

A sloth in a crooked witch hat on a flying broom: everything she does is lazy and slow, except her magic. **She can only fly on her broom:** off it, she falls and walks.

- **Look (11-09 rework):** hunched granny build with a posable rig (hips, spine, head, two-bone arms and legs placed by IK every frame). Wavy drooping hat in three springy sections with a band, buckle, stitched patch, toadstool and a lucky star on a thread; round spectacles, a wart, chin whiskers, grey wisps under the brim; mustard knitted shawl with a fringe and glowing brooch; purple robe with a ragged hem, bell sleeves and a three-piece tattered cape that trails behind her; belt with a spore pouch, three different potion bottles and a stirring spoon; striped stockings and long hooked claws; a twig wand with a glowing crystal. The broom has a gnarled handle that curls up at the front with a swinging green lantern, a knitted seat cushion, twine bindings, a fanned bristle bundle and the copper **Antigrav Coil** clamped underneath.
- **Entrance:** the clearing's lanterns go out one by one; the cauldron bubbles and erupts; **her broom shoots out first**, spinning, flips and settles across the pot. Two claws hook over the handle and she hangs from it like a sloth from a branch (wand in her teeth), yawns, does a slow pull-up, swings a leg over and plonks down (the broom sags), blinks very slowly, then cackles at full speed with both arms up and the lanterns come back green. Only then does she lift off.
- **Hovering** 6-9 m up, out of melee reach, banking into her drift with the cape and hat trailing. **Hex orbs** (three slow homing orbs off her wand tip, with a wind-up and a flick for each; 10 each; bolts pop them), **sleepy spores** (a pod from her belt pouch, tossed underarm: a purple cloud, half speed and blur while inside), **cauldron rain** (she stirs the air with her wand toward the pot; five potion globs onto marked rings).
- **Clone beam:** she splits into 3 (5 in phase 2) around you; all raise a claw and level their wands for 3 s with converging rings (the clones copy her every move). **Only the real one wears the hat**; the clones can't be hurt. Hit the real one to interrupt (she spins dizzily, stars round her head). Otherwise each beam fires from the wand tips at where you stood when the aim locks (at 80%), 15 each.
- **Full-charge bolt:** knocks her off the broom. She tumbles end over end, flailing, and **bounces off anything that isn't ground** (the hut roof, treetops, lantern posts, the cauldron rim) rather than landing on it. She lies sprawled with stars circling, sits up, gets up (slowly), and waddles back. The broom spins away and catches itself, hovering at her hip height over **open floor she can walk to** (never on a roof, under the hut eaves, against a trunk or in the cauldron). She grabs the handle, it slides in under her, lifts her off her feet, sags, and takes off. She takes 1.5x damage the whole time she's on the ground. **Hitting the broom** knocks it further away (it skitters off, then flies to a new open spot). If she can't reach it (no full path, or stuck for 2.5 s) she whistles and it flies over to her. If her fall ever tangles for 4 s she puffs onto the ground nearby.
- **Car:** can't reach her in the air; on the ground, ram her: the car explodes, she's launched (no broom, so straight back down) and stays down longer.
- **Death:** she falls out of the air (no flying without the broom; the broom drops separately), curls up on her side and snores until she vanishes in a puff of smoke.
- **Drops** the **Antigrav Coil** (her broom's core) and a **Witch Ember** (Bigger Boom recipe).

### Clutch McCheddar, president of the Swamp Rats MC (stage boss, the Greasy Spoon), 1100 HP

A big mouse biker on a long purple chopper whose back wheel is the ship's landing gear. Since 11-09 he is a posable rig like Kart Kong and the witch (`Bosses/ClutchMcCheddar.cs`): hips, spine and head with two-bone IK arms and legs that reach the grips, pegs or ground, springy ears, bandana tails and a four-piece tail. **Model:** red dotted bandana, gold teardrop aviators (also the trophy), toothpick, gold hoop in his left ear, whiskers, sideburns; a black biker jacket over a red shirt with lapels, studded epaulets, an upturned collar, a zip and the club patch on the back (gold rockers and a winged cheese wedge); a cheese-wedge belt buckle and wallet chain; fingerless gloves; jeans and buckled engineer boots. **Chopper:** springer forks with springs, ape-hanger bars with tassels and mirrors, a headlight that is a real spot light, a teardrop tank with painted flames, a speedo and a flip-cap nitro button, a finned V-twin with a cheese-wedge air cleaner, drag pipes with fishtails, a belt drive, a tuck-and-roll saddle with a sissy bar, studded saddlebags with fringe, a laced front wheel with a mouse-skull ornament, the fat landing-gear tyre on a glowing hub with an oleo strut, and a tail light with a red light-streak. It leans into turns (lean from speed and turn rate), steers, and its wheels roll.

- **Entrance (the Akira slide):** the roadhouse sign flickers on; an engine echoes and a headlight comes out of the fog up the causeway; he blasts past a low camera that whips round after him; a tracking shot alongside at wheel height, pipes popping flame; then **the Akira slide, shot from behind on the road**: he throws the chopper sideways and slides *away* from the lens in slow motion (the whole game slows, sparks and smoke too), the bike broadside (front wheel to the left of frame) and laid over toward the camera, his left boot down braking in sparks, smoke and skid marks trailing from both tyres and the boot, and he turns his head to look back at us. In Akira, Kaneda power-slides away from the viewer braking with his left foot; until 11-09 Clutch slid toward the camera, which was the wrong way round. He kicks the bike up off his boot; close-up: a finger tips the aviators down his snout ("You're in my lane, pal."), the toothpick rolls across; then a low shot from behind (his back patch, the roadhouse beyond): fist in the air, "YEEEHAW!", a big rev and flames, the neon flares, the gang pours out pulling wheelies as the camera cranes up and back, and his name card comes up then (`MudBoss.ShowsOwnTitle`). Space skips; a skip still sets him, the gang and the card up.
- **Phase 1:** **ride-by ram** (stops with both boots down, a smoking burnout and a flashing high beam for 1 s, then launches front-wheel-up and charges through you, 25), **donuts** (inside boot planted, bike leaned in, opposite lock, chain twirling overhead; a 4.5 m spinning ring for 3 s, flinging mud patches), **mud rooster** (turns away, twists round to wave "Bye-bye!" over his shoulder, rear tyre spins 1.2 s, then a cone of mud behind him, 10 and mud in your eyes), **gang call** (two fingers to his snout to whistle, fist pumping: 4 biker mice). While circling he points at you, beckons ("C'mon!") or pumps a fist.
- **Phase 2 (66%):** he plants his boots, unhooks the chain from his belt, whirls it up and smacks it into his palm ("Alright, no more Mr. Nice Mouse."), then a fist-pumping rev in a sheet of flame; the chain hangs from his fist from then on. Adds a **ramp jump** onto a marked shadow (up on the pegs, a no-footer in the air, "WOOO!", a squash on landing; 30 in a ring) and a **chain whip** (stands on the pegs twirling it overhead 0.8 s, then cracks it out at you: a 5 m circle, 16); 6 mice; faster rams.
- **Phase 3 (33%):** **nitro**: he stops, flips the cap off the red button on his tank and slaps it ("NITRO!"): purple flames from the pipes, a purple underglow, a wheelie launch; double rams; donuts leave a ring of fire.
- **Wipe-outs:** a ram into the gas pumps or the ramp's side, a full-charge bolt during a ram, or the car: the bike goes down on its side and skids on, he is thrown off and tumbles, sits in the mud dazed (@ @), gets up, dusts off his jacket, heaves the bike up and swings back on: **3 s at 1.5x damage** (was 2 s before the animation; balance note).
- **The getaway:** at 15% he stops taking damage, says his line ("Catch me if you can, cheese-for-brains!") and pulls a wheelie up the Gator Causeway. Until you are in a car he idles along at 6 m/s taunting ("C'mon, slowpoke!", waving); once you are driving he rubber-bands, 13 m/s when you are close, 7 m/s when you are far behind, and only 4 m/s if you fall more than 60 m back (the chase car tops out at 26 m/s), so a clean run always catches him (at the old 11-18 m/s he always got away). **"HE'S GETTING AWAY! Hold F and RAM HIM!"** The chase gives you a free car (no cost, no cooldown, a little faster, works even without the Learner's Permit), and a new one if you crash. He weaves and tosses an oil barrel off the back every 3.5 s. His way out is a **jump ramp** on the road just short of the County Line (built when he first runs, it stays for round two): **ram him before the ramp** and he goes over the handlebars, defeated. If he reaches it he rides up and launches ("SO LONG, SUCKER!", "HE JUMPED IT!") in a 40 m, 10 m-high no-footer over the County Line, gets away, heals phase 3 back to 33%, and roars back into the lot for another go.
- **Defeat:** the chopper stands on its nose and he sails over the bars, arms and legs everywhere, while the bike cartwheels off on its own and lands on its side (it stays as a wreck); he lands face down and slides through the mud, his aviators fly off (the trophy), one hand comes up out of the mud, trembles, and drops; the gang scatters. He drops the **Landing Gear**. (Killed any other way, the bike topples over and he spills off face down beside it.)

### Silt the Mudbender (line 2, Still Water Dojo), 480 HP

A blindfolded otter martial artist in loose green robes with a cream sash and wide sleeves, barefoot, fighting from a low horse stance (an original design from Christian's idea and reference).

- **Feels vibrations:** he aims at where you last made noise. Sprinting, landing, swinging and the car are loud; walking is quiet; **crouching** makes you nearly silent, so his throws land wide.
- **Entrance:** sipping tea cross-legged on a rock 3 m behind his fighting spot; a close-up on the blindfold; he sets the cup down, stamps, and a stone column pushes the rock up with mud cascading off. He stands, front-flips off it (spinning about his middle, so he clears the rock) and lands on his spot, then bends the rock: the column crumbles away and the boulder floats up over his head ("Catch!").
- **Opening throw:** the fight starts with the boulder overhead; he draws it back (0.8 s) and hurls it at you (22 in a 2.8 m ring, shove, bursts into rubble). If a car wall interrupts him, he throws it next. Skipping the entrance still leaves him holding it.
- **Stays on his dojo:** every spot he walks, hops or leaps to is kept within 11 m of the dojo's centre (inside the stone lanterns), so chasing him never drags the fight out past the leash. Too close, he hops back at most every 3.5-5 s (sideways along the edge when his back is to it); otherwise he **stamps** (0.55 s crouch with a 3.4 m ring, then 10 damage and a big shove), every 4-6 s.
- **Walk:** a stride cycle driven by distance (a step every 0.7 m): legs swing opposite each other along his travel direction (sidesteps shuffle), arms counter-swing, hips dip and roll each step, shoulders twist, tail swishes; he shuffles his feet turning on the spot.
- **Mud balls:** stamps and pulls a mud boulder from the ground (0.7 s), throws it (14); the splat leaves a mud patch. Phase 2: three in a fan.
- **Earth wall:** raises a wall between you; it blocks bolts and lasts 6 s, which is also cover for you. Against the car he raises it in the car's path at once and the car crashes into it, unless he is mid-throw. A full-charge bolt or jump smash breaks a wall.
- **Earth rush:** punches the ground; a line of rock spikes travels at you over cracking ground (20, launch). Jump or sidestep. Winded for 1.2 s afterwards.
- **Phase 2:** **pillar leap** (a pillar raises him, then an elbow-drop ring, 24) and **listening stance**: melee from the front is parried with a palm strike (12, big shove); from the side or behind, it lands.
- **Drops** the **Gyro Stabilizer** and a **Mudstone Bead** (Shockwave Shocks recipe).

### The Sloth Witch, Granny Drowse (line 3, Hag's Clearing), 420 HP

A sloth in a crooked witch hat on a flying broom: everything she does is lazy and slow, except her magic. **She can only fly on her broom:** off it, she falls and walks.

- **Look (11-09 rework):** hunched granny build with a posable rig (hips, spine, head, two-bone arms and legs placed by IK every frame). Wavy drooping hat in three springy sections with a band, buckle, stitched patch, toadstool and a lucky star on a thread; round spectacles, a wart, chin whiskers, grey wisps under the brim; mustard knitted shawl with a fringe and glowing brooch; purple robe with a ragged hem, bell sleeves and a three-piece tattered cape that trails behind her; belt with a spore pouch, three different potion bottles and a stirring spoon; striped stockings and long hooked claws; a twig wand with a glowing crystal. The broom has a gnarled handle that curls up at the front with a swinging green lantern, a knitted seat cushion, twine bindings, a fanned bristle bundle and the copper **Antigrav Coil** clamped underneath.
- **Entrance:** the clearing's lanterns go out one by one; the cauldron bubbles and erupts; **her broom shoots out first**, spinning, flips and settles across the pot. Two claws hook over the handle and she hangs from it like a sloth from a branch (wand in her teeth), yawns, does a slow pull-up, swings a leg over and plonks down (the broom sags), blinks very slowly, then cackles at full speed with both arms up and the lanterns come back green. Only then does she lift off.
- **Hovering** 6-9 m up, out of melee reach, banking into her drift with the cape and hat trailing. **Hex orbs** (three slow homing orbs off her wand tip, with a wind-up and a flick for each; 10 each; bolts pop them), **sleepy spores** (a pod from her belt pouch, tossed underarm: a purple cloud, half speed and blur while inside), **cauldron rain** (she stirs the air with her wand toward the pot; five potion globs onto marked rings).
- **Clone beam:** she splits into 3 (5 in phase 2) around you; all raise a claw and level their wands for 3 s with converging rings (the clones copy her every move). **Only the real one wears the hat**; the clones can't be hurt. Hit the real one to interrupt (she spins dizzily, stars round her head). Otherwise each beam fires from the wand tips at where you stood when the aim locks (at 80%), 15 each.
- **Full-charge bolt:** knocks her off the broom. She tumbles end over end, flailing, and **bounces off anything that isn't ground** (the hut roof, treetops, lantern posts, the cauldron rim) rather than landing on it. She lies sprawled with stars circling, sits up, gets up (slowly), and waddles back. The broom spins away and catches itself, hovering at her hip height over **open floor she can walk to** (never on a roof, under the hut eaves, against a trunk or in the cauldron). She grabs the handle, it slides in under her, lifts her off her feet, sags, and takes off. She takes 1.5x damage the whole time she's on the ground. **Hitting the broom** knocks it further away (it skitters off, then flies to a new open spot). If she can't reach it (no full path, or stuck for 2.5 s) she whistles and it flies over to her. If her fall ever tangles for 4 s she puffs onto the ground nearby.
- **Car:** can't reach her in the air; on the ground, ram her: the car explodes, she's launched (no broom, so straight back down) and stays down longer.
- **Death:** she falls out of the air (no flying without the broom; the broom drops separately), curls up on her side and snores until she vanishes in a puff of smoke.
- **Drops** the **Antigrav Coil** (her broom's core) and a **Witch Ember** (Bigger Boom recipe).

### Clutch McCheddar, president of the Swamp Rats MC (stage boss, the Greasy Spoon), 1100 HP

A low-poly mouse in a leather jacket and aviators on a long chopper whose back wheel is the ship's landing gear.

- **Entrance:** the roadhouse sign flickers on, an engine echoes down the causeway, a headlight comes out of the fog; he tears into the lot and does the long **sideways slide**: the camera is down on the road in front of him, looking back up the causeway, and he comes at it broadside in slow motion, bike laid over, one boot skating on the ground in a fan of sparks, mud spraying and skid marks left behind, and stops just short of the lens; lowers his glasses ("You're in my lane, pal."), revs, and his gang rides out doing wheelies. Name card.
- **Phase 1:** **ride-by ram** (headlight flash and rev, 1 s, then a charge through you, 25), **donuts** (rear wheel smokes, a 4.5 m spinning ring for 3 s, flinging mud patches), **mud rooster** (turns away, rear tyre spins 1.2 s, then a cone of mud behind him, 10 and mud in your eyes), **gang call** (4 biker mice).
- **Phase 2 (66%):** adds a **ramp jump** onto a marked shadow (30 in a ring) and a **chain whip** (twirl overhead 0.8 s, then a 5 m circle, 16); 6 mice; faster rams.
- **Phase 3 (33%):** **nitro** (purple exhaust, double rams), donuts leave a ring of fire.
- **Wipe-outs:** a ram into the gas pumps or the ramp's side, or a full-charge bolt during a ram, throws him off the bike for 2 s at 1.5x damage. The car crashing into him does the same.
- **The getaway:** at 15% he stops taking damage, says his line ("Catch me if you can, cheese-for-brains!") and flees up the Gator Causeway. Until you are in a car he idles along at 9 m/s taunting ("C'mon, slowpoke!"); once you are driving he rubber-bands, 18 m/s when you are close and 11 m/s when you are far behind (the chase car tops out at 26 m/s), so the chase is always winnable but needs clean driving. **"HE'S GETTING AWAY! Hold F and RAM HIM!"** The chase gives you a free car (no cost, no cooldown, a little faster, works even without the Learner's Permit), and a new one if you crash. He weaves and drops oil barrels. **Ram him** before the County Line gate and he is thrown over the handlebars: defeated. If he reaches the gate he gets away, heals phase 3 back to 33%, and roars back into the lot for another go.
- **Defeat:** flies, lands face down in the mud, the bike tumbles, his glasses come off (the trophy), the gang scatters. He drops the **Landing Gear**.

## 7. Upgrades

New region **Mudlands** and new slot **Car** (a Car filter in Bucket & Barter, a Car group in the pockets). Costs are first-pass, in the same range as the tutorial's.

| Upgrade | Slot | Effect | Cost |
| --- | --- | --- | --- |
| Swamp Waders | Suit | No slowdown in mud or bog water on foot; slides 20% further | 10 metal, 4 wiring |
| Bootleg Capacitor | Laser | Bolts hit 15% harder and cool down 20% faster | 8 metal, 3 boards, 1 fuel cell |
| Moonshine Distiller | Flask | One more sip before a blackout, and a chug costs 1 drink instead of 2 | 6 wiring, 2 boards, 2 fuel cells |
| Reinforced Bumper | Car | Run-overs hit 60% harder; Hog Brutes and biker gangs are flattened instead of blowing the car | 14 metal, 2 boards |
| Bigger Boom | Car | Explosion radius 6 to 8.5 m | 6 wiring, 2 fuel cells, Witch Ember |
| Extra Proof | Car | Explosion damage +50% | 8 metal, 3 fuel cells |
| Shockwave Shocks | Car | Explosion stun 2.5 to 4 s | 4 boards, 2 fuel cells, Mudstone Bead |
| Mud Tires | Car | No mud drag, 30% more grip everywhere | 10 metal, 4 wiring, Kart Wheel |
| Quick Chug | Car | Chug 1.6 to 0.9 s, and hits no longer spill it | 6 wiring, 2 boards |
| Spare Keys | Car | Cooldown 30 to 18 s | 8 metal, 4 wiring, 1 board |

The Learner's Permit is not a shop item: it is the unlock, a key item.

## 8. Saving

On `main`, the only thing saved to disk is the camp's trophies (`CampSave`); the Mudlands trophy uses it. Everything else (scrap, upgrades, quests) is session-only, like the rest of `main`. The full save system is Christian's `feature/save-load` branch, kept separate on purpose. The Mudlands is ready for it:

- The **permit** and the mini-boss **key items** are ordinary pockets key items, and upgrades are ordinary upgrade Ids, which that branch already saves.
- `MudlandsStory` keeps its whole progress as flags (`CaptureFlags` / `RestoreFlags`), the shape that branch's `SaveData.flags` uses. After both branches are merged, a `MudlandsStory.Save.cs` partial implementing `ISaveableLevel` is a short job.

## 9. Test checklist

1. Open `Mudlands.unity`, press Play. No Console errors. You start by the fire at Stilt Camp; Cavendish and Eugene are there; the shop shows the Mudlands upgrades and a Car filter.
2. Hold F before the permit: a normal sip. Cavendish offers all three lines; the tracker lists them.
3. Line 1: the school, the kiosk quiz, the permit card. Hold F: chug the flask, car drops in, chase cam. Drive, drift, horn, bail with F. Run over pups; crash into a tree head-on (explodes) and scrape one (sparks). Cooldown pip. Too-drunk refusal. No parking at camp.
4. The haul road: barricades break by car and by laser; the still blows; back to Cavendish; Kart Kong's entrance; kart breaks on a charged bolt; "MY GO-KART!!!"; banana interrupt; the peel slip; defeat and drops.
5. Line 2: sinking stones, three gongs, the dam; Silt's entrance; crouch makes his throws miss; the wall against the car; listening stance.
6. Line 3: lanterns by bolt and by toppling a pole; the witch's entrance; clone beam interrupt on the hatted one; full charge knocks her down; she walks back to the broom.
7. All three down: the roadhouse gate opens; Clutch's entrance slide; phases; the getaway at 15%; the chase; ram him; the trophy and the Landing Gear; Cavendish's wrap-up. Let him escape once: phase 3 heals back and he returns.
8. Die mid-fight: the boss resets (the shared rule); a car mid-ride is parked safely.

## Open questions

- Where the Mudlands sits in the stage order (it is not in the design document). The desert's exit can point at it with **Signal Lost > Mudlands > Link Desert Exit To Mudlands** (sets `DesertStageExit.nextStageScene` in the desert scene; not run, since it changes the desert scene).
- A single hit bigger than 15% of Clutch's health (165) could skip the getaway. No player attack comes close (the strongest is about 100), so it is left as is.
- Drunk Driving makes the car explosion a second source of stuns, and on mini-bosses: an intentional exception to the 10-22 "only the combo finisher stuns" rule, because the bosses' designs ask for it.
- The stage boss's getaway needs the car by design. The car is free and guaranteed during the chase, so the ability is never locked out there.
- All numbers are a first pass for play-testing.
