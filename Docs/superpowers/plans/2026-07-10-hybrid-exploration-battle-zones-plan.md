# Hybrid Exploration and Battle Zones Implementation Plan

## Objective

Implement the approved hybrid-zone foundation and prove it in `GiardinoDelleRose`: exploration and tactical combat occur in the same scene on the same visible environment; the Rose Garden receives a Unity Terrain-compatible form of the refined painterly terrain treatment; encounter startup and teardown are transactional; and the scene builder deterministically reproduces all required combat authoring.

Design source: `Docs/superpowers/specs/2026-07-10-hybrid-exploration-battle-zones-design.md`

## Safety Boundaries

- Preserve the unrelated dirty worktree documented in `Docs/MASTER_PROJECT_MEMORY.md`.
- Stage explicit implementation paths only.
- Do not edit imported Asset Store package source.
- Preserve the Rose Garden layout, paths, walls, rose beds, fountain, terrace, travel markers, props, weather surfaces, and exploration routes.
- Preserve tactical dimensions, obstacle cells, elevations, cover behavior, and encounter composition unless runtime verification proves an existing mismatch.
- Keep the Florentine Inn, Salone delle Arti, world map, road encounters, and standalone BattleArena outside this implementation scope.
- Keep corruption disabled and tactical fog independent.
- Do not migrate additional zones until the Rose Garden pilot passes.

## Task 1: Capture the Rose Garden baseline

### Files

- Read `Assets/Scenes/GiardinoDelleRose.unity` through Unity.
- Read `Assets/Editor/GiardinoWalledGardenBuilder.cs`.
- Read `Assets/Scripts/Battle/ZoneEncounterTrigger.cs`.
- Read `Assets/Prefabs/Battle/BattleKit.prefab`.

### Implementation

1. Load `GiardinoDelleRose` without saving and inventory:
   - Terrain data, terrain layers, material template, and weather components.
   - `BattleMapAuthoring`, `BattleTerrainHeights`, fog, curse, and weather components.
   - `ZoneEncounterTrigger`, BattleKit reference, and staged enemy objects.
   - Player, exploration camera, entry points, exits, interaction objects, and safe-state defaults.
2. Record grid size, cell size, plateau data, obstacle cells, party/enemy spawns, and representative height samples.
3. Capture the normal exploration camera view under clear daylight.
4. Verify the current in-place encounter once before modification if the scene contains a complete staged encounter.
5. Restore the original editor scene and do not save baseline inspection changes.

### Verification

- Produce a baseline inventory sufficient to compare deterministic rebuild output.
- Confirm whether the current scene contains runtime wiring that the builder does not reproduce.
- Confirm Unity has no compile errors before implementation begins.

## Task 2: Add a reusable hybrid-zone terrain profile

### Files

- Create `Assets/Scripts/Environment/HybridZoneTerrainProfile.cs` and its Unity meta file.
- Create `Assets/Art/Environment/HybridZones/Profiles/GiardinoDelleRose_Terrain.asset` and its Unity meta file.

### Implementation

1. Add a `ScriptableObject` profile containing presentation-only settings:
   - Surface family (`Natural` initially; reserve `Urban` for the later phase).
   - Material transition softness.
   - Macro variation scale and strength.
   - Exposed warm tint and recess cool tint.
   - Slope/recess strength and elevation tint strength.
   - Wet darkening and wet-highlight strength.
   - Ambient/readability adjustment.
2. Give all fields conservative defaults matching the accepted `BattleTerrainSplat` treatment.
3. Keep textures and terrain paint distribution owned by `TerrainLayer` assets; the profile must not duplicate layer authoring.
4. Tune the Rose Garden profile for restrained meadow green, warm gravel, muted dry grass, soft slope depth, and non-plastic wetness.

### Verification

- Validate the script with Unity standard diagnostics.
- Confirm the profile serializes and reloads with the expected values.
- Confirm missing profiles can be detected before a builder overwrites a valid material.

## Task 3: Add the terrain-compatible painterly shader

### Files

- Create `Assets/Art/Shaders/HybridZoneTerrain.shader` and its Unity meta file.
- Create `Assets/Art/Environment/HybridZones/Materials/GiardinoDelleRose_Terrain.mat` and its Unity meta file.

### Implementation

1. Add `InfernosCurse/HybridZoneTerrain`, a URP terrain shader that consumes Unity Terrain control maps and the first four terrain layers.
2. Preserve the Rose Garden's existing grass, gravel, and dry-grass splat distribution and per-layer tiling.
3. Port the approved painterly technology from `BattleTerrainSplat`:
   - Deterministic world-space macro variation.
   - Soft, normalized layer transitions.
   - Normal-derived slope and recess darkening.
   - Warm exposed/high-surface tint and cool recessed-surface tint.
   - Global `_GrassWetness` response with stronger darkening on gravel/soil than grass.
   - Restrained main-light wet highlights.
4. Support URP main light, shadows, spherical-harmonic ambient light, fog, and terrain instancing requirements used by the project.
5. Provide a deliberate dry fallback when no persistent weather authority exists.
6. Keep corruption influence neutral; do not add curse color or glow to the hybrid-zone shader.
7. Create/update the Rose Garden material from the profile rather than hand-tuning scene-only values.

### Verification

- Confirm shader compilation for the active PC URP renderer with zero errors.
- Verify the material exposes and receives every profile property.
- Compare clear, wet, and night views for terrain continuity and tactical readability.
- Confirm no magenta fallback appears at normal or distant terrain rendering ranges.

## Task 4: Add explicit zone combat authorization and validation

### Files

- Create `Assets/Scripts/Battle/ZoneBattleAuthoring.cs` and its Unity meta file.
- Create `Assets/Editor/HybridZoneStandardValidator.cs` and its Unity meta file.

### Implementation

1. Add `ZoneBattleAuthoring` as the scene-level contract for hybrid combat:
   - Explicit `combatAllowed` authorization, defaulting to false.
   - References to `BattleMapAuthoring`, `BattleTerrainHeights`, `ZoneEncounterTrigger`, and the BattleKit prefab.
   - Optional exploration-only and battle-only object roots.
   - Optional camera framing bounds or padding.
2. Add a validation method returning actionable findings without changing scene state.
3. Ensure ordinary encounter startup requires explicit authorization; enemies alone cannot turn a safe scene into a battlefield.
4. Add an editor validator that inventories explorable scenes and classifies them as:
   - Valid hybrid zone.
   - Explicit safe/noncombat zone.
   - Migration candidate.
   - Invalid partial configuration.
5. Validate visible/tactical essentials: grid and height data, trigger, BattleKit, spawn cells, exploration camera, Player tag, and terrain profile/material where applicable.

### Verification

- Validate the new scripts with Unity standard diagnostics.
- Confirm the Rose Garden reports as a valid hybrid-zone candidate after builder integration.
- Confirm the Inn and Salone report as explicit noncombat scenes rather than failures.
- Confirm incomplete scenes receive actionable findings without automatic modification.

## Task 5: Make encounter startup and teardown transactional

### Files

- Modify `Assets/Scripts/Battle/ZoneEncounterTrigger.cs`.

### Implementation

1. Resolve and validate `ZoneBattleAuthoring`, `BattleMapAuthoring`, height data, BattleKit, main camera, player, staged enemies, and usable spawn cells before suspending exploration.
2. Refuse startup when `combatAllowed` is false.
3. Capture all exploration state that the trigger changes:
   - Player active state and transform.
   - Cinemachine brain enabled state.
   - Exploration-only/battle-only root states.
   - Interaction/exit state controlled by the authoring component.
4. Encapsulate suspension and restoration so every exit path uses the same teardown routine.
5. If any operation fails after suspension, destroy partial battle objects, restore the camera and player, restore object groups, clear subscriptions, and return to exploration.
6. Prevent duplicate EventSystems, battle rigs, grids, units, and event subscriptions across repeated encounters.
7. Preserve existing ambush detection, weather/insanity feature gating, staged-enemy concealment, belief seeding, tactical fog reveal, and victory/defeat behavior.
8. Restore the exploration player to the captured location unless an explicit outcome overrides it.

### Verification

- Validate the script with Unity standard diagnostics.
- Probe successful startup and teardown.
- Probe at least three controlled failures: missing BattleKit, no valid spawn cells, and missing camera support.
- Confirm every failure leaves the exploration player visible, mobile, and under the locked HD-2D camera.

## Task 6: Make the Rose Garden builder authoritative

### Files

- Modify `Assets/Editor/GiardinoWalledGardenBuilder.cs`.
- Modify `Assets/Editor/ZoneBattleKitMaker.cs` only if BattleKit generation needs a stable reusable lookup API.
- Deterministically update `Assets/Scenes/GiardinoDelleRose.unity`.
- Deterministically update Rose Garden terrain-layer, material, and profile assets created by the builder.

### Implementation

1. Make terrain-layer creation idempotent: update existing assets instead of failing or duplicating them on rebuild.
2. Create/update the hybrid-zone terrain profile and material, assign the material template to `Terrain_Giardino`, and retain the existing control-map paint.
3. Add `ZoneBattleAuthoring` to `[BattleGridData]` with `combatAllowed=true` and all required references.
4. Add and configure `ZoneEncounterTrigger` on the same authoring root.
5. Load and assign `Assets/Prefabs/Battle/BattleKit.prefab`; fail the rebuild with an actionable error before saving if it is missing.
6. Deterministically stage the approved Rosekin encounter from `Assets/Data/Combatants/Enemy_Rosekin.asset` on valid authored cells, with the runtime components required by the existing trigger.
7. Preserve the existing 32x32 grid, terrace plateau, fence/tree/rose-bed/fountain obstacle authoring, party spawns, enemy spawns, fog, weather director, travel markers, and exploration camera kit.
8. Add explicit exploration-only and battle-only roots only where a lifecycle distinction is required; do not duplicate the environment.
9. Save only after all required assets and references validate.

### Verification

- Rebuild the scene twice and confirm deterministic output and no duplicate assets/components.
- Compare the Task 1 grid, obstacle, spawn, and height baseline; gameplay-authoring values must match.
- Confirm the builder reproduces the complete encounter without manual scene edits.
- Run the hybrid-zone validator and require a clean Rose Garden result.

## Task 7: Verify seamless presentation and camera switching

### Test matrix

1. Clear daylight exploration.
2. Clear daylight encounter through victory.
3. Heavy rain exploration into battle after wetness reaches its target.
4. Night exploration into battle.
5. Controlled startup failure followed by continued exploration.
6. A second encounter in the same Play session to detect leaked state.

### Runtime probes

- Active scene remains `GiardinoDelleRose` throughout the encounter.
- Exploration begins with the Cinemachine brain enabled and no battle camera rig.
- Combat begins with the Cinemachine brain suspended and one battle camera rig.
- Terrain renderer/material identity does not change at transition.
- `_GrassWetness`, time, weather, lighting, and post-processing remain continuous.
- Player world location is captured and restored.
- Tactical grid dimensions, heights, walkability, obstacles, and spawns match authored values.
- Movement and targeting highlights remain readable over grass, gravel, dry grass, and wet surfaces.
- Victory removes battle units, grid/runtime kit, battle rig, and subscriptions.
- Failure recovery restores exploration completely.
- No duplicate EventSystem, camera rig, BattleManager, or battle kit survives teardown.
- Unity console contains zero new errors or warnings attributable to the pilot.

### Visual evidence

- Capture exploration and battle views of the same garden area under identical clear conditions.
- Capture the wet/rain transition and a night battle view.
- Inspect terrain color continuity, unit silhouettes, path contrast, grid readability, garden boundaries, and camera framing.

## Task 8: Audit later migration candidates without converting them

### Files

- Generate `Docs/2026-07-10-hybrid-zone-migration-audit.md` from the validator findings.

### Implementation

1. Inventory Fiesole, Ponte Vecchio, Mercato Vecchio, Duomo, Piazza della Signoria, and street scenes.
2. Record terrain/surface family, available encounter space, boundary-camera risk, existing tactical authoring, and required migration work.
3. Mark safe hubs/interiors and dedicated battle scenes as explicit exceptions.
4. Rank migration candidates but do not edit their production scenes in this pilot.

### Verification

- Every explorable scene is classified exactly once.
- Recommendations distinguish natural-terrain, urban-surface, safe/noncombat, and dedicated-arena cases.
- No additional scene becomes combat-capable during the audit.

## Task 9: Commit discipline and handoff

1. Refresh Unity and confirm zero compile errors.
2. Run standard diagnostics on every new or modified C# file.
3. Run the hybrid-zone validator.
4. Complete the Play-mode matrix and inspect visual captures.
5. Run `git diff --check` on hand-authored text files.
6. Review `git status --short` and stage only the approved implementation, deterministic Rose Garden outputs, migration audit, and their meta files.
7. Commit the verified pilot with a focused message such as `Build hybrid Rose Garden battle zone`.
8. Do not push unless explicitly requested.

## Completion Criteria

- `GiardinoDelleRose` uses the terrain-compatible painterly presentation in exploration and combat.
- Combat starts and resolves in the same scene without terrain, lighting, time, or weather discontinuity.
- The locked exploration camera hands off to the tactical camera and returns reliably.
- The Rose Garden builder reproduces the full hybrid encounter deterministically.
- Explicit combat authorization prevents ordinary encounters in safe scenes.
- Failure recovery cannot leave the player hidden, frozen, or without camera control.
- Tactical data still matches visible garden geometry.
- The migration audit classifies all existing exploration scenes.
- Runtime validation completes with zero new Unity errors attributable to the implementation.
