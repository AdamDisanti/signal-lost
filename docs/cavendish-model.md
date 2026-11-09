# Cavendish's model

Nicole, 11-08: Cavendish is now a normal capybara on all fours wearing a spacesuit, instead of a standing astronaut. The model is low-poly and flat-shaded like the rest of the game. It uses the existing graybox suit colours (ivory, teal, orange, graphite) and adds a clear fishbowl helmet.

![Preview](../art/cavendish/CavendishPreview.png)

## Rebuild it

In Unity, choose **Signal Lost > Characters > Rebuild Cavendish Model**. It does three things:
- **Rebuilds the model inside the existing prefab** (`GrayboxProps/CapybaraAstronaut.prefab`), so every scene and builder that uses him keeps working. It replaces the old Blender model under `Visual`.
- **Updates every scene that has him.** It carries over how far each seated Cavendish was lowered onto his log, because the old value pointed at the replaced model. It also lowers his speech-bubble anchor to just above the helmet.
- **Saves the meshes and the helmet glass** in `GrayboxProps/Cavendish/`.

The Console prints `CAVENDISH_MODEL_REBUILT`.

## Poses

The model has no rig. `CavendishPose` (on `Visual`) poses it:
- **Seated:** the tutorial, island camps and boat still lower `Visual` to seat him (`IslandCamp.SetCavendishDrop`). The pose turns that into a capybara loaf: belly on the seat, front paws tucked forward, hind legs folded away.
- **Walking:** when his root moves along the ground, such as up a dock or backing away in the opening, his legs trot in diagonal pairs.
- **Outfits:** anything added straight onto `Visual`, such as the island outfits' sun hat and sunglasses, is moved onto the body so it follows the pose.

Poses only run in Play mode. In the Scene view, a seated Cavendish looks sunk into his log.

## Editing the model

The part list (position, size, material of every piece) comes from [cavendish_model.py](../tools/blender/cavendish_model.py), which also renders the preview. To change him:

```text
python3 tools/blender/cavendish_model.py art/cavendish/CavendishPreview.png --builder signal-lost/Assets/Editor/CavendishModelBuilder.cs
```

This needs Python with numpy and matplotlib. Then run the Unity menu above. Keep these part names, because the island outfits look for them: "Capybara head", "Eye", "Eye.001", "Collar rim", "Raised helmet frame" (the helmet group, hidden in the desert along with "Helmet lamp" and "Lamp lens"), and the `SuitIvory`/`SuitTeal` materials.

## Other changes

- `TutorialStoryInstaller` and `DesertDunesBuilder` place his dialogue anchor at the new height (`CavendishModelBuilder.DialogueHeight`).
- `GrayboxPropBuilder.Validate` expects the shorter model (1.3 to 1.7 m tall).
- The opening cutscene lines his face up with the cockpit window from his measured height, so it adapts on its own.

## Not changed

- The title screen draws its own little Cavendish in code (`MainMenuScreen`). He still stands upright there.
- In the desert outfit, the neckerchief is tied for an upright neck and may end up mostly hidden inside the suit.
