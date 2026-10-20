# Tutorial impact site

The current tutorial is a **480 x 480-meter, moon-inspired crash site**: rolling regolith, overlapping bowl-shaped craters, low rounded hills and scattered grey stones. It follows Lawson's supplied lunar reference, not the earlier mine-like mesa/catwalk direction. Width/length remain 1.5x the old 320-meter map; the player, ship and props are not rescaled.

## Open and edit

Open **`signal-lost/Assets/Scenes/Tutorial.unity`** in Unity **6000.6.2f1**. The scene is already authored; **do not rerun creation tools**. Reopen from disk rather than saving a stale in-memory scene over the revision.

Under **Tutorial Impact Site**:

- **Rocky Crater Terrain** remains native, sculptable Unity terrain, 480 meters square with 513 height samples per side. The 96-meter TerrainData authoring range is unchanged; actual exploration relief is much gentler.
- **Lunar Landscape** is a linked prefab of grey stone dressing. Its 10,000 stones include **215 solid boulders** and **9,785 decorative pebbles**. Tiny pebbles deliberately have no collision, avoiding constant controller snagging. Visual geometry is combined into three material batches, with lower-detail meshes for tiny stones and no added lights.
- **Basalt Rocks** retains the 96 original rocks at their original scales/rotations, regrounded and given scene/prefab-only neutral material overrides. Outlying pieces may move aside to clear walking circuits; their source meshes/materials/prefabs remain intact.
- **Ship Debris** retains the existing 30 panels/struts, regrounded without rescaling or new mining installations. These and the central ship provide the sparse crash-wreckage accent.
- **Perimeter Cliffs** retains the existing continuous non-convex mesh collision, now substantially lower than the extreme traversal pass and blended into a rounded outer terrain rim. The outer containment band is intentionally steeper than the exploration interior. It is not a gate-unlock mechanic or a new invisible box boundary.

The six added traversal installations/arches have been removed from the scene and reusable environment: no relay causeway, summit observatory, rib tunnel, stepping-column course or fallen-mast catwalk. Their original assets and GUIDs remain available, **inactive**, under `TutorialCrashSite/TraversalLandscape/`; they were not deleted or silently overwritten.

## Lunar terrain direction

Lawson selected **natural terrain plus sparse wreckage**, then supplied a reference showing craters, continuous hills, grey regolith and a dark sky. This revision spreads variation across the landscape rather than leaving broad flat fields between isolated dramatic features.

- **57 impact craters**, from small depressions to broad overlapping bowls. Their rims are rounded and slightly irregular, with restrained pale ejecta/darker floor paint.
- Several overlapping scales of rolling relief extend throughout the exploration area. There are **no terrain-carved straight roads, deep linear trenches, flat mesa pads or terraced switchbacks**.
- Eight open walking routes pass through western twin impacts, northern bowls, southern basins and eastern ejecta fields. These are test/clearance routes, not visibly excavated roads. Explore freely rather than follow a marked industrial course.
- Three neutral terrain layers: **GreyRegolith**, **PaleEjecta** and **DarkImpactDust**, with fine-grained original project textures. The original crash clearing remains darker/scorched.
- A black, atmosphere-free procedural sky, low neutral directional light and restrained cool shadow fill replace the brown/blue mining-landscape atmosphere. Distance fog is disabled. No Earth image, imported sky pack, stars, vegetation, low-gravity physics or oxygen mechanics were added; the reference guides appearance, not a literal change of setting or mechanics.

### Measured exploration relief

The distribution check excludes the preserved central clearing and the outer containment band. It samples the interior at 3-meter spacing and checks 132 separate 24-meter patches:

| Measurement | Earlier extreme pass | Current lunar revision |
| --- | --- | --- |
| Sampled exploration elevations | -9.27 to 52.00 m | **-3.81 to 13.05 m** |
| 95th-percentile slope | 64.15 degrees | **20.95 degrees** |
| Samples steeper than 30 degrees | 31.06% | **0.48%** |
| 24 m patches with at least 1.1 m local relief | 122 / 132 | **132 / 132** |
| Least local patch relief | 0.00 m | **1.67 m** |

These are sampled distribution/gradient checks, not claims that every square meter or every arbitrary path is identical or guaranteed walkable. The small central tutorial floor intentionally remains clearer/flatter. The outer rim is not included in the interior elevation/slope figures.

## Preserved ship, props and gameplay

**Enhanced Crashed Rocket** remains a linked reusable prefab with the original rocket mesh/GUID, pose and scale; blue side porthole, three recessed engine nozzles and nine mesh colliders. Its buried nose and 40 contact rocks/40 partly buried hull chips remain. Tiny contact dressing is decorative, not collectible scrap.

**BurntRockBasin > MiningLaserAnchor > Mining Laser** remains the orange/graphite industrial tool, fitted just above the basin floor, with its pickup binding, original combat model, collider and anchors. The existing player, capybara, flask, basin, gate, tutorial HUD, pad and targets retain their saved transforms and working roles. The checked core floor within 38 meters is **unchanged**, with zero measured height delta from the latest saved pre-revision terrain.

The existing [tutorial gameplay](tutorial-gameplay.md) is unchanged: unarmed start, E pickups, original laser melee/charged bolts, health/HUD, F flask healing/intoxication, blackout/death and gear/target reset. Movement remains **4 m/s walking, 7 m/s sprinting, 1.2-meter jump and gravity 20**. There is no new fall damage, climbing, inventory, quest, reward or checkpoint system.

The prior eight-jump industrial course is deliberately no longer part of this layout. Natural slopes and stones supply traversal; the eight tested exploration circuits and the original prop/gate approaches are walkable without jumping. The gate remains an open visual frame inside the closed perimeter, including 50 meters beyond it. Its eventual transition/unlock behavior is still unspecified.

## Reusable assets and sources

Current lunar assets:

`signal-lost/Assets/Prefabs/Enviornment objects/TutorialCrashSite/LunarLandscape/`

- `LunarLandscape.prefab`: linked stone-field dressing, combined visual meshes and individual matching boulder colliders.
- `Meshes/`: original project-created breccia forms and combined stone-field meshes.
- `Materials/`: three matte grey rock palettes and the airless procedural sky.
- `Terrain/`: three original texture maps and native TerrainLayer assets.

The existing `TutorialCrashSite/CraterEnvironment.prefab`, `Terrain/ImpactCrater.asset` and scene are updated **in place with their GUIDs preserved**. The reusable environment contains terrain, lunar stone field, original rocks/debris and perimeter, not the ship, player or scene lighting. Other scenes, runtime scripts, shared player/scanner/HUD prefabs and source models are not modified.

[LunarTutorialBuilder.cs](../signal-lost/Assets/Editor/LunarTutorialBuilder.cs) is the protected one-shot authoring recipe, reusing the existing `LowPolyMesh` helper. It requires the 480-meter traversal baseline, backs up its saved scene/terrain and refuses to overwrite an existing lunar revision. Delivered content is static/editor-authored, not runtime generation. Edit through Unity normally; duplicated environment instances share TerrainData, so duplicate that asset before making an independent terrain copy.

Preserve every `.meta`/GUID. Native TerrainData is binary and uses the existing exact-path LFS rule; PNG previews/textures use existing LFS rules. Unity text scenes, prefabs, materials and mesh assets remain ordinary Git. No dependencies, package versions, source-model imports or third-party asset packs changed. See [asset attribution](assets.md).

## Actual Unity previews

Captured from the final disposable Play Mode scene using standard URP render requests, not mockups:

- [Whole-map lunar overview](../art/tutorial-impact-site/Previews/LunarOverview.png)
- [Spawn-side lunar horizon](../art/tutorial-impact-site/Previews/LunarSpawnHorizon.png)
- [Western crater field](../art/tutorial-impact-site/Previews/LunarWesternCraters.png)
- [Northern hills](../art/tutorial-impact-site/Previews/LunarNorthernHills.png)
- [Southern basin](../art/tutorial-impact-site/Previews/LunarSouthernBasin.png)
- [Eastern ejecta and stones](../art/tutorial-impact-site/Previews/LunarEjectaField.png)

Earlier `Traversal*.png` previews depict the superseded industrial layout, not the current scene. Existing rocket/laser detail previews remain useful for the unchanged props. The user-supplied lunar reference was not imported or redistributed as a game asset.

## Validation and safe reproduction

Unity **6000.6.2f1** compilation and rendered Direct3D 11 Play Mode checks passed:

- Distribution regression intentionally failed on the extreme baseline, then passed all interior elevation/slope and local-relief checks above.
- Saved-state comparison: unchanged core floor; full saved ship/core-actor transforms; retained original dressing counts/scales/rotations. All 57 crater definitions, three terrain layers, combined stone geometry, linked prefab, grounded boulders, static collision, sky/fog settings and scene references checked.
- Eight walk-only circuits across the hills/crater fields with the authored CharacterController and existing movement settings. Original spawn-to-prop/gate routes and 50 meters beyond the gate also passed.
- Ship collision/burial, one active gameplay camera/listener, 256 radial perimeter mesh rays, and 64 sprint/repeated-jump containment tests. Loose-rock colliders were disabled for containment testing.
- Full rendered gameplay regression: pickups, HUD/health, pad damage, flask animation/heal cap/cooldown, intoxication/recovery, all original laser attacks, blackout/death/retained gear/target reset and the unchanged combat demo. Actual intoxication-image comparison passed. The previously documented narrowly excluded Editor SearchDatabase startup exception remains unrelated to gameplay.
- Final preview review, **647 original asset files preserved byte-for-byte**, 55 matching tested delivery files, 380 unique project GUIDs, six matching previews, LFS/ignore checks and delivery hashes. All delivered GUID references resolve. The broader scan also records **12 pre-existing unresolved references** in the unchanged `DefaultVolumeProfile.asset`, `PC_Renderer.asset` and `TMP Settings.asset`; those unrelated settings are not repaired or claimed clean by this pass.

[CrashSiteSmoke.cs](../tools/unity/CrashSiteSmoke.cs) selects the layout's matching checks. Install it together with [LunarTutorialSmoke.cs](../tools/unity/LunarTutorialSmoke.cs) and the historical [TutorialTraversalSmoke.cs](../tools/unity/TutorialTraversalSmoke.cs) **only in a disposable project's Assets/Editor**. Use matching assets/packages/settings under the system temporary directory; never install test fixtures in the real project. The saved-state check expects the matching pre-lunar scene/terrain in that temporary project's `Logs/BeforeLunarPass/`.

```text
Unity.exe -batchmode -force-d3d11 -projectPath <temporary-project> -executeMethod CrashSiteSmoke.Start -logFile <log-path>
```

Omit `-quit`; the Play Mode harness exits itself. `-nographics` skips rendered views. `LunarTutorialSmoke.CheckDistributionOnly` is a static entry point and uses `-quit`. Run [TutorialGameplaySmoke.cs](../tools/unity/TutorialGameplaySmoke.cs) separately for the complete gameplay suite.

Latest saved pre-revision assets/docs are backed up locally under ignored `signal-lost/Logs/BeforeLunarDelivery/`. Reports and delivery hashes are under `Logs/LunarValidation/`. The initial lunar delivery was uncommitted; its authored assets and the later desert-enemy integration are now preserved together on the `desert-enemies` publication branch. Physical-input feel, final art acceptance and target-hardware performance remain human playtest/profile checks; automated traversal is not that proof.
