# Refined Battle Terrain Shader Implementation Plan

## Objective

Implement the approved Refined Painterly Diorama treatment on the test plains battle map, make it the shared standard for generated 3D battle maps, connect it to persistent weather, and place the corruption system behind one reversible disabled feature setting without disturbing tactical fog or combat authoring.

Design source: `Docs/superpowers/specs/2026-07-09-refined-battle-terrain-shader-design.md`

## Safety Boundaries

- Preserve the unrelated dirty worktree documented in `Docs/MASTER_PROJECT_MEMORY.md`.
- Stage only files listed in this plan or files Unity deterministically regenerates from the approved builders.
- Do not edit imported Asset Store package source.
- Do not alter battle grids, elevations, spawns, paths, obstacles, cover, or line-of-sight rules.
- Preserve corruption data, assets, save fields, orb refinement metadata, and the `Cursed` combat status. The feature switch makes the world-corruption economy and its presentation dormant; it does not delete future design work.
- Keep `BattleTerrainFog` and `ZoneFogOfWar` active and independent.

## Task 1: Add a centralized corruption feature setting

### Files

- Create `Assets/Scripts/Config/GameFeatureSettings.cs` and its Unity meta file.
- Create `Assets/Resources/GameFeatureSettings.asset` and its Unity meta file.

### Implementation

1. Add a `GameFeatureSettings` `ScriptableObject` with a serialized `corruptionEnabled` field that defaults to `false`.
2. Add a small static `GameFeatures` access layer that loads `Resources/GameFeatureSettings` once and fails closed (`false`) when the asset is missing.
3. Expose `GameFeatures.CorruptionEnabled` as the only runtime decision point.
4. Keep the setting independently testable and avoid coupling it to `GameSystems` scene lifetime.

### Verification

- Refresh Unity and confirm zero compile errors.
- Use a Unity editor probe to log the loaded asset path and `CorruptionEnabled=False`.
- Temporarily inspect the missing-asset fallback in an editor-only probe without moving or deleting the production asset.

## Task 2: Make corruption presentation and terrain input neutral

### Files

- Modify `Assets/Scripts/Curse/InsanityPresenter.cs`.
- Modify `Assets/Scripts/Curse/CurseOverlay.cs`.
- Modify `Assets/Scripts/Battle/BattleTerrainCurse.cs`.

### Implementation

1. Make `InsanityState.Personal`, `WorldCorruption`, `Total`, and `Current` return neutral values while corruption is disabled.
2. Make `InsanityPresenter` skip vignette creation, water material instancing/tinting, and whisper setup while disabled.
3. Ensure disabling the feature at startup leaves no `InsanityVignette` object, no whisper playback, and no altered water material instances.
4. Make `CurseOverlay` avoid event subscriptions, sprite creation, and terrain-mask forwarding while disabled.
5. Make `BattleTerrainCurse` initialize and retain a black mask while disabled; `SetDensity` becomes a no-op.
6. Keep all existing behavior intact behind the enabled branch for later tuning.

### Verification

- Play an exploration scene containing water and confirm no vignette or runtime corruption-tinted water materials are created.
- Play `BattleArena` and confirm no `CurseOverlay_*` objects appear and the terrain curse mask remains black.

## Task 3: Stop corruption economy and battle mutation without breaking AI

### Files

- Modify `Assets/Scripts/Curse/DailyCurseDrift.cs`.
- Modify `Assets/Scripts/Curse/HubMap.cs`.
- Modify `Assets/Scripts/Rest/RestSystem.cs`.
- Modify `Assets/Scripts/Battle/PendingEncounter.cs`.
- Modify `Assets/Scripts/Battle/ZoneEncounterTrigger.cs`.
- Modify `Assets/Scripts/Battle/EncounterBootstrap.cs`.
- Modify `Assets/Scripts/Battle/BattleManager.cs`.
- Modify `Assets/Scripts/Curse/BattleCurseAutomata.cs`.
- Modify `Assets/Scripts/Battle/AI/EnemyAI.cs`.

### Implementation

1. Stop daily drift and rest costs from changing hub corruption while disabled.
2. Make hub corruption reads used for presentation, encounters, and battle seeding return neutral values without erasing stored save data.
3. Remove insanity/curse multipliers from encounter chance, enemy count, and zone detection while disabled.
4. Prevent defeat outcomes from adding corruption while disabled.
5. Make `BattleManager` skip curse seeding, per-turn steps, and cleanse calls while disabled.
6. Make all public `BattleCurseAutomata` mutation methods no-op while disabled and keep its singleton lifecycle safe.
7. Exclude `SpreadCurse` and `RetreatAndLure` from enemy intent selection while disabled. Other tactical intents continue scoring normally.
8. Keep `InfernalWorldState` structurally available because non-corruption AI influence, belief, and threat systems still use it.

### Verification

- Advance an in-game day and rest once; compare hub node corruption snapshots before and after and confirm no changes.
- Start a battle with a nonzero stored hub value and confirm battle curse density and global curse remain zero.
- Run multiple enemy turns and confirm no curse intent is selected and no enemy stalls or passes because of the disabled feature.

## Task 4: Extend the biome style profile for painterly terrain

### Files

- Modify `Assets/Scripts/Battle/BattleMapStyle3D.cs`.
- Modify `Assets/Prefabs/Battle/Maps/Styles/BattleMap_Plains_Style3D.asset`.

### Implementation

1. Add grouped, documented style fields for:
   - Material transition softness.
   - Macro variation scale and strength.
   - Warm exposed-surface tint.
   - Cool recess tint.
   - Fold/cavity strength.
   - Elevation tint strength.
   - Wet darkening and wet highlight strength.
2. Choose conservative defaults that preserve legacy maps if a new profile has not been tuned.
3. Tune the plains profile to the approved earthy olive/moss, ochre earth, neutral stone, and restrained path palette.
4. Keep arena light fields available, but do not make the profile override persistent world time or weather when a live world light rig is present.

### Verification

- Inspect serialization after Unity refresh and confirm all new values persist.
- Confirm a newly created style asset receives safe painterly defaults.

## Task 5: Refine the shared battle terrain shader

### Files

- Modify `Assets/Art/Shaders/BattleTerrainSplat.shader`.

### Implementation

1. Preserve grass/dirt/rock/path inputs and triplanar dirt/rock projection.
2. Normalize softened material weights using a profile-controlled transition exponent.
3. Add deterministic world-space macro variation without requiring a new texture asset.
4. Read UV2 X as the existing slope shade and UV2 Y as a new authored fold/cavity channel.
5. Apply subtle warm tint to exposed/high surfaces and cool tint to recesses.
6. Read the existing global `_GrassWetness`; darken dirt, path, and rock more strongly than grass.
7. Add a restrained view-dependent wet highlight that uses the URP main light and never resembles plastic gloss.
8. Keep shadow casting and SH ambient support.
9. Retain curse properties for compatibility, but multiply their contribution by a global `_CorruptionEnabled` value that is zero while the feature is disabled.

### Verification

- Confirm shader compilation for the PC URP renderer with zero errors.
- Probe the plains material to verify every expected property exists and receives the profile value.
- Confirm `_GrassWetness` changes terrain response in Play mode without affecting dry base color when zero.

## Task 6: Update mesh authoring and deterministic map generation

### Files

- Modify `Assets/Editor/BattleMapMeshBuilder.cs`.
- Modify generated assets:
  - `Assets/Prefabs/Battle/Maps/Meshes/BattleMap_Plains3D.asset`
  - `Assets/Prefabs/Battle/Maps/Materials/BattleMap_Plains3D_Terrain.mat`
  - `Assets/Prefabs/Battle/Maps/BattleMap_Plains3D.prefab`
  - `Assets/Scenes/BattleArena.unity` only if deterministic arena wiring changes it.

### Implementation

1. Calculate a normalized fold/cavity value from the existing local height Laplacian and write it to UV2 Y.
2. Continue writing slope shading to UV2 X and material/path weights to vertex colors.
3. Calculate generated mesh minimum/maximum height and send the range to the material for elevation tinting.
4. Copy every new profile field into the generated material.
5. Add validation before overwriting output:
   - Shared shader exists.
   - Source `BattleMapAuthoring` exists.
   - Required style profile and layer textures exist.
6. Abort generation with an actionable error before replacing the last valid prefab when required inputs are invalid.
7. Rebuild plains through `InfernosCurse/Templates/8. Build 3D Plains Diorama`, then rewire the arena only if the generated prefab reference changes.

### Verification

- Record grid dimensions, spawn lists, plateau data, obstacle cells, path cells, and sampled terrain heights before rebuild.
- Rebuild and compare those values after rebuild; all gameplay-authoring values must match.
- Run the same-camera visual capture and confirm the approved painterly direction.

## Task 7: Add audit tooling and prepare the other-map rollout

### Files

- Create `Assets/Editor/BattleTerrainStandardValidator.cs` and its Unity meta file.
- Create profile/output assets only for battle source maps that already have valid `BattleMapAuthoring` and can be rebuilt safely.

### Implementation

1. Add an editor validation menu that inventories all `BattleMap_*3D.prefab` assets.
2. For each map, report:
   - Shared terrain shader assignment.
   - Valid `BattleMapStyle3D` profile.
   - Required textures and material properties.
   - `BattleTerrainFog` and `ZoneFogOfWar` presence.
   - Neutral corruption state.
3. Inventory source prefabs (`BattleMap_Plains`, `BattleMap_Field`, `BattleMap_Ruins`, and any later authored maps) separately from generated 3D outputs.
4. Do not silently create production 3D variants from incomplete source maps. Report migration candidates and rebuild them only after the plains result is accepted in-engine.
5. Make the shared builder/profile path the standard for every subsequently approved map.

### Verification

- Run the validator and confirm plains passes.
- Confirm source maps without approved 3D output are listed as migration candidates rather than false failures.

## Task 8: Runtime and regression validation

### Test map matrix

1. `BattleArena` in dry clear daylight.
2. `BattleArena` at nighttime.
3. `BattleArena` in heavy rain after wetness reaches the expected target.
4. `BattleArena` through at least three player/enemy turns with tactical fog active.
5. One additional approved 3D map after the plains shader is accepted and its profile is migrated.

### Runtime probes

- Terrain shader is `InfernosCurse/BattleTerrainSplat`.
- `_GrassWetness` transitions from dry to the expected rain value and back.
- `_CorruptionEnabled` remains zero.
- No `InsanityVignette` or `CurseOverlay_*` objects exist.
- Battle curse density remains zero.
- Tactical fog mask updates and enemies hide/reveal correctly.
- Enemy AI completes turns and does not choose corruption-only intents.
- Movement and targeting highlights remain readable.
- Unity console contains zero errors.

### Visual evidence

- Capture before/after images from the same battle camera.
- Capture clear, heavy-rain, and nighttime variants of the accepted test map.
- Inspect unit silhouettes, cliff depth, path contrast, grass density, and highlight readability.

## Task 9: Commit discipline and handoff

1. Run Unity refresh and confirm zero compile errors.
2. Run the battle-terrain validator.
3. Complete the Play-mode matrix and record evidence.
4. Run `git diff --check` on hand-authored text files; treat Unity serializer blank-value whitespace as generated output rather than manually rewriting YAML.
5. Review `git status --short` and stage only the implementation files and deterministic generated assets.
6. Commit the verified implementation with a focused message such as `Refine battle terrain rendering`.
7. Do not push unless explicitly requested.

## Completion Criteria

- The plains test map visibly matches the approved Refined Painterly Diorama direction.
- Persistent rain and time of day affect battle terrain naturally.
- Corruption presentation, economy, encounters, battle propagation, and AI intents remain dormant behind one disabled setting.
- Tactical fog remains functional.
- Gameplay-authoring data is unchanged.
- The validator passes for every approved 3D battle-map output.
- The test plains map and at least one additional approved migrated map pass Play mode with zero Unity errors.
