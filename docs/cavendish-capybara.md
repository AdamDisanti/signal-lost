# Cavendish as a capybara (11-10)

Lawson asked for Cavendish redesigned to look like a normal capybara wearing a space suit: low poly, with a clear space helmet, made in Blender. He is a new prefab, standing in the Testing Sandbox to look at. The story scenes still use the old upright `GrayboxProps/CapybaraAstronaut`.

![Cavendish](../art/cavendish-capybara/CavendishCapybara.png)

## The model

- **The capybara:** on four short legs, with a heavy barrel body and a tall, blunt, brick-like head whose top slopes down to the nose. He has small round ears, eyes high on the head and a dark nose pad with nostrils on top. His fur is reddish brown, darker on top and greyer-brown low on the muzzle. It was modelled from Lawson's two reference photos.
  - The legs are tucked under the body and rise well into it, so there is no gap where they join (11-10, after Lawson saw them floating at the body's edge).
- **His face** (11-10, "make him more expressive"):
  - Big dark eyes with a white shine, under relaxed half-lids: the calm, content capybara look.
  - A small smile, whiskers, and ears with dark insides.
- **He moves** (`CavendishIdle`), all visual:
  - His head turns inside the helmet to watch the player within 14 m, up to 18° each way, and gazes about slowly when nobody is near. Every few seconds he gives a curious tilt of the head.
  - He blinks (now and then twice), flicks one or both ears, breathes, and his antenna light pulses.
- **The suit:** white, with an orange band, a blue shoulder patch on each side, grey knee joints, dark boots and a chest panel with a light.
  - A backpack with two orange tanks and an antenna with a red light.
- **The helmet:** a clear faceted bubble over the head, sealed to a metal collar ring at the neck. The glass is see-through and casts no shadow.
- **Size:** about 2.1 m long and 1.6 m to the top of the helmet: bigger than a real capybara, so he reads as a character. He faces +Z.
- Low poly and flat shaded, with no textures: each part has a flat colour (the FBX is 54 KB).

## Files

- `tools/blender/cavendish_capybara.py`: builds the model. Run it in Blender, through the Blender MCP or with `blender --background --python tools/blender/cavendish_capybara.py`.
  - It saves `art/cavendish-capybara/CavendishCapybara.blend` and the preview render beside it.
  - It exports `Assets/Prefabs/NPC/Cavendish/Models/CavendishCapybara.fbx`, in parts that move: the body, the head (pivoted at the neck), each ear and eye, the antenna light, and the helmet glass.
- `Assets/Scripts/NPC/CavendishIdle.cs`: the idle animation.
- `Assets/Editor/CavendishCapybaraBuilder.cs`:
  - **Signal Lost > NPC > Build Cavendish Capybara** makes the URP materials (`Prefabs/NPC/Cavendish/Materials`) and `Prefabs/NPC/Cavendish/CavendishCapybara.prefab`. The prefab has a box collider and a `DialogueAnchor` above the helmet, like the old prefab.
  - **Signal Lost > NPC > Place Cavendish Capybara in Testing Sandbox** puts him 3.5 m to the right of the old Cavendish, in front of the player's start, turned three-quarters toward the player.

## Checked

- The model was built in Blender 5.2.3 and its preview render reviewed.
- The prefab was built and placed by the builder on a disposable copy of the project (Unity 6000.6.2f1, batch mode). Renders of him in the sandbox were reviewed from the player's start and from four sides.
- The batch run checked the idle script is wired to the head, body, ears, eyes and antenna light, with the ears and eyes riding on the head. It also ran the script's start-up and one frame without errors.
- Not done: Play mode. The head-turning, blinking and ear flicks have not been watched moving, and nobody has walked up to him.

## Notes

- The Blender MCP add-on connected but never answered requests, so the model was built by running the same script in Blender from the command line.
- From the player's start the sandbox sun is behind him, so his face is in shadow until you walk round to his lit side.
- He now replaces `CapybaraAstronaut` everywhere. **Signal Lost > NPC > Put New Cavendish In Every Scene** (`Editor/CavendishEverywhere.cs`) rebuilds `CapybaraAstronaut.prefab` around this model, keeping its `DialogueAnchor`, and updates Tutorial Demo, DesertDunes, CrystalCaves, Mudlands, Wasteland and the Testing Sandbox (the separate sandbox copy is removed). Run on 2026-11-21: 5 Cavendishes in 6 scenes.
- `Scripts/NPC/CavendishPose.cs` makes the four-legged model work with the scripts written for the old upright one. When they seat him (lowering `Visual`), he lies on the ground just behind his log instead; on the boat he stands a little forward of his seat. It keeps his collider and speech bubbles over him. The tutorial's pull-out and drag (`TutorialStory`), the mech cockpit (`MechModel.SeatPilot`) and the desert outfit (`Outfits.DressCapybara`: sun hat and sunglasses, helmet hidden) are adjusted for his shape.
- **Signal Lost > NPC > Cavendish Check Shots** saves pictures of every Cavendish, the boat and the cockpit to `signal-lost/Logs/CavendishShots`.
- Nicole's older `feature/cavendish-model` branch (a script-built capybara) is superseded and should not be merged; it also has a different `CavendishPose.cs`.
