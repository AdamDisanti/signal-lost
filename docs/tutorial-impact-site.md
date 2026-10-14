# Tutorial impact site

A dark, rocky low-poly tutorial crash site. The latest art upgrade adds close-in wreck dressing, a side window and three engines, a mining-laser prop in the basin, a doubled terrain footprint, and impassable perimeter cliffs.

## Open the scene

Open `signal-lost/Assets/Scenes/Tutorial.unity` in Unity **6000.6.2f1**. If an older copy is already open, save any new edits and reopen the scene from the Project window. Do not overwrite the updated disk file with a stale in-memory scene.

Under **Tutorial Impact Site**:

- **Rocky Crater Terrain** is native, sculptable Unity terrain, now **320 x 320 meters** instead of 160 x 160. Width and length are doubled; area is four times larger. The broad crater is approximately 172 meters across. All approved height samples are unchanged; elevations remain approximately -0.05 to 6.57 meters. Widening the terrain naturally reduces physical slopes: the maximum sampled slope is now 26.27 degrees. No smoothing or vertical rescaling was applied.
- **Enhanced Crashed Rocket** is a linked reusable prefab containing **Crashed Ship** and **Ground Contact Debris**. The original rocket's mesh dimensions, scale and rotation are unchanged. It is embedded an additional 0.35 meters, with 40 small wedged basalt pieces and 40 partly buried hull chips dressing the contact edges. Tiny contact pieces are decorative, not route-blocking collision or collectible scrap.
- The ship has a bolted **blue side porthole** and **three recessed engine nozzles** at its base. Its original FBX/GUID remain unchanged. `BuriedNoseMarker` is approximately 1.05 meters below the central ground, and the ship assembly has nine static mesh colliders.
- **Basalt Rocks** and **Ship Debris** retain all 96 existing rocks and 30 existing panels/struts at their original sizes and rotations. Their horizontal distribution was widened by two, not their geometry.
- **Perimeter Cliffs** is an irregular, continuous low-poly basalt formation with fractured strata, overhangs, an uneven skyline, and a static non-convex MeshCollider. The steep faces are far taller than the player's 1.2-meter jump. The visible cliff mesh itself blocks passage; no disconnected invisible boundary boxes are used.

## Basin laser and preserved content

**BurntRockBasin > MiningLaserAnchor > Mining Laser** contains the new orange/graphite industrial laser gun. It lies on its side, at 0.86 of the reusable prefab's authored meter scale. Its bounds sit at local Y 0.65 meters, just above the basin floor at 0.64 meters, without sealing the recess. The laser prefab has a static BoxCollider, `MuzzleAnchor`, and `GripAnchor`.

The player, capybara, basin, flask and gate retain their saved XZ positions, rotations and scales; only their heights were regrounded on the expanded terrain. Their source prefabs are unchanged. Routes from spawn to these props, through the gate, and for 50 meters beyond it remain traversable without jumping.

The laser is a **placed visual prop**, not a new pickup/equip or firing system. No dialogue, healing, scrap collection, quest progression, exit unlocking or scene-transition rules were invented. The requested cliff perimeter is closed, including beyond the gate; the gate remains an open visual frame. Its eventual progression/transition behavior needs team confirmation.

The extra authoring camera/listener and duplicate directional light remain disabled. Exactly one gameplay camera/listener is active. The scene retains the existing URP setup, dark rough materials and cool lighting; distance fog was extended for the larger map. No dependencies or third-party asset packs were added.

## Prefabs and editable sources

All new or updated Unity models are in the existing prefab section, preserving its folder spelling:

`signal-lost/Assets/Prefabs/Enviornment objects/TutorialCrashSite/Upgrades/`

| Prefab | Contents |
| --- | --- |
| `EnhancedCrashedRocket.prefab` | Original rocket, crashed pose/materials/collision, window, three engines and close-in contact debris |
| `RocketWindow.prefab` | Bolted metal porthole, backing and blue glass |
| `RocketEngine.prefab` | Mount, chamber, hollow recessed nozzle and cooling ribs |
| `MiningLaser.prefab` | Imported laser model, URP material overrides, box collider and placement anchors |
| `ContactRock.prefab` | Reusable basalt contact piece; tiny scene pieces disable its collider |
| `ContactHullShard.prefab` | Reusable decorative torn-metal chip |
| `NaturalCliffPerimeter.prefab` | Continuous textured cliff formation and matching static mesh collision |

The laser FBX/palette are in `Upgrades/Models/`; procedural meshes and materials are in `Upgrades/Meshes/` and `Upgrades/Materials/`. The editable laser source is [MiningLaser.blend](../art/mining-laser/Blender/MiningLaser.blend), outside Unity Assets so teammates do not need Blender to import the project. See the [Blender mining-laser preview](../art/mining-laser/Previews/MiningLaser.png).

The existing `TutorialCrashSite/CraterEnvironment.prefab` was updated in place with its existing GUID. It contains the expanded terrain, scattered rocks/debris and perimeter cliffs, not the ship, player, tutorial props or scene lighting. The scene's environment root is directly editable and unscaled; the upgraded rocket, laser and cliffs are prefab instances.

- Sculpt/paint **Rocky Crater Terrain** using Unity Terrain tools. Keep the core tutorial routes clear.
- Duplicated environment prefabs share TerrainData. Duplicate that asset through Unity before sculpting an independent copy.
- Edit prefabs through Unity; preserve `.meta` files/GUIDs when moving or renaming assets.
- Native TerrainData remains binary under Force Text. The exact `Terrain/ImpactCrater.asset` path has a Git LFS rule; other Unity text assets remain ordinary Git. FBX, Blender sources and PNG images use existing LFS rules.

## Actual Unity previews

Rendered in the isolated Play Mode validation project, not artist mockups. Only the bird's-eye overview temporarily disables fog for inspection; the delivered gameplay fog remains enabled.

- [Expanded impact-site overview](../art/tutorial-impact-site/Previews/ExpandedImpactSiteOverview.png)
- [Rocket window, burial and debris](../art/tutorial-impact-site/Previews/CrashedRocket.png)
- [Three base engines](../art/tutorial-impact-site/Previews/RocketEngines.png)
- [Mining laser in the basin](../art/tutorial-impact-site/Previews/MiningLaserInBasin.png)
- [Fractured perimeter cliffs](../art/tutorial-impact-site/Previews/PerimeterCliffs.png)

![Mining laser placed in the basin](../art/tutorial-impact-site/Previews/MiningLaserInBasin.png)

## Tools and validation

- Original one-shot conversion: [TutorialCrashSiteBuilder.cs](../signal-lost/Assets/Editor/TutorialCrashSiteBuilder.cs)
- Ship/laser upgrades: [CrashSiteUpgradeBuilder.cs](../signal-lost/Assets/Editor/CrashSiteUpgradeBuilder.cs)
- Terrain/cliff generation: [CrashSiteExpansionBuilder.cs](../signal-lost/Assets/Editor/CrashSiteExpansionBuilder.cs)
- Upgrade checks: [CrashSiteUpgradeValidation.cs](../signal-lost/Assets/Editor/CrashSiteUpgradeValidation.cs)
- Blender laser generator: [create_mining_laser.py](../tools/blender/create_mining_laser.py)
- Isolated Play Mode harness: [CrashSiteSmoke.cs](../tools/unity/CrashSiteSmoke.cs), intentionally outside Unity Assets
- Attribution: [assets.md](assets.md)

**Signal Lost > Tutorial Crash Site > Validate Upgraded Scene** checks dimensions, references, missing scripts, burial, window/engine placement, basin fit, prefab assets, materials and 256 radial cliff-collision probes. The original **Validate Scene** also understands the updated hierarchy. Creation/expansion menu operations refuse to rerun over an already-upgraded scene.

Unity 6000.6.2f1 compilation and isolated Play Mode checks **passed** after the final cliff art pass:

- FirstPersonController initialized with valid input/camera references and grounded at spawn.
- The authored CharacterController traversed all four prop approaches and the gate route without jumping.
- Ship collision and buried nose checks passed; exactly one gameplay camera/listener was active.
- All 256 radial boundary probes hit the cliff collider. Sprinting and repeated standard-height jumps from 64 directions, including mesh seams, could not escape the perimeter. Loose-rock collisions were disabled for that test so they could not hide a cliff gap.
- Saved pre-upgrade snapshots confirmed unchanged original rocket renderer dimensions/scale/orientation; unchanged rock/debris scales and rotations; doubled dressing distribution; preserved player/prop XZ, rotation and scale; and identical approved heightmap samples.
- URP screenshots were rendered and visually inspected. Asset/meta pairs, GUIDs, references and Git attributes were checked.

Run the harness with `-batchmode -projectPath <temporary-project> -executeMethod CrashSiteSmoke.Start -logFile <log-path>`; omit `-quit` because it exits after checks. It requires matching assets/settings/packages and terrain, physics and audio modules. `-nographics` skips rendering. It refuses to run outside the system temp directory. Optional `Assets/TestBaseline.unity` and `Assets/TestBaselineTerrain.asset` enable snapshot comparisons; never install those test snapshots in the real project.

Ignored local safety copies/reports are under `signal-lost/Logs/`, including `TutorialBeforeShipUpgrades.unity`, `TutorialBeforeMapExpansion.unity`, `ImpactCraterBeforeExpansion.asset`, and `CrashSiteUpgradeValidation.txt`. The TerrainData file itself changed for the doubled dimensions, although its height samples did not. Nothing was staged, committed or pushed.

The harness drives CharacterController collision directly after checking FirstPersonController initialization; it does not simulate keyboard/mouse input. Interactive movement feel, final art direction and gameplay integration still need team playtesting.
