# Urban Hybrid Terrain Design

## Purpose

Create a uniform urban terrain language for the five Florence city zones that support both exploration and in-place tactical battles. The pass extends the proven Rose Garden hybrid-zone technology while giving each district a controlled local identity.

## Approved Direction

The visual direction is **Layered Florence**: warm gray Florentine stone, worn paving, terracotta repair bands, restrained dirt, and moisture concentrated at boundaries. The terrain must remain rich during exploration and readable during tactical combat.

The implementation uses a shared urban profile family rather than one universal material or five unrelated shaders.

## Zone Scope

The urban family covers:

- Mercato Vecchio
- Ponte Vecchio
- Duomo
- Piazza della Signoria
- Via Calimala

Fiesole remains outside this pass because it needs a rural terrain family. Giardino delle Rose remains unchanged as the natural-zone reference implementation. Enemy placement, encounter tuning, prop replacement, and layout changes are outside scope.

## Architecture

- Extend the existing `InfernosCurse/HybridZoneTerrain` shader and `HybridZoneTerrainProfile` workflow.
- Add shared Layered Florence controls for stone color, secondary paving, terracotta repairs, wear, edge dirt, and moisture.
- Create one profile asset and one material instance per urban zone.
- Assign the zone-specific profile through `ZoneBattleAuthoring`.
- Preserve authored geometry, colliders, routes, props, and non-ground materials.
- Use the same terrain material in exploration and battle. Tactical cells, ranges, and targeting remain separate overlays.
- Extend `HybridZoneStandardValidator` so urban scenes require an approved shader, profile, material assignment, compatible ground coverage, and valid hybrid battle authoring.

## Zone Profiles

### Mercato Vecchio

Use warm, heavily worn paving with the strongest terracotta repair presence, market dirt, and localized staining. Retain enough value separation for characters and tactical overlays in dense prop areas.

### Ponte Vecchio

Use cooler stone, smoother traffic wear, and subtle dampness concentrated at edges. Avoid broad wet patches that would compete with silhouettes or weather effects.

### Duomo

Use paler dressed stone, restrained repairs, and a cleaner ceremonial character. The surface must still show age and hand-laid variation rather than appearing modern or perfectly uniform.

### Piazza della Signoria

Use balanced warm-gray slabs with the strongest natural surface boundaries. This is the primary open-space readability reference for the urban family.

### Via Calimala

Use darker compact paving, directional traffic wear, and frequent narrow repair bands. Keep the pattern quiet enough for combat in confined streets.

## Exploration and Battle Behavior

- Terrain appearance does not swap when battle starts.
- Exploration retains authored surface variation and weather response.
- Tactical grid, movement range, targeting, and spell indicators render above the environment.
- Profile contrast is capped so sprites, grid lines, and effects remain legible.
- Dirt and moisture collect near walls, seams, drains, and boundaries instead of covering the whole surface with noise.
- The pass adds no modern markings, mechanically perfect tiling, or fantasy glow.

## Migration Safety

- The editor migration operates on an explicit allowlist of the five approved scenes.
- Compatible ground renderers are identified deliberately; the tool does not replace every renderer using a floor-like material name.
- Unsupported or ambiguous renderers are skipped and reported for review.
- Original scene geometry and collider components are never rebuilt by the terrain migration.
- Existing material references remain recoverable through version control and are not deleted.
- A scene is saved only after its required profile, material, shader, and authoring assignments validate.

## Validation

After migration:

1. Run `HybridZoneStandardValidator` across all enabled scenes.
2. Run `BattleTerrainStandardValidator` to confirm dedicated battle-map standards remain intact.
3. Run `WeatherSurfaceStandardValidator` to detect legacy or unexplained surfaces.
4. Confirm the Unity console contains zero errors and zero warnings introduced by the pass.
5. Playtest Piazza della Signoria as the open-space extreme.
6. Playtest Mercato Vecchio as the dense-space extreme.

Each Play Mode test must verify:

- Exploration movement and collision remain correct.
- The uniform locked exploration camera behaves normally.
- An encounter starts an in-place battle without a scene or terrain-material swap.
- Tactical cells, ranges, targets, characters, and effects remain readable.
- Victory removes battle-only objects and restores exploration control and camera state.
- Weather response remains visually coherent before and after combat.

## Acceptance Criteria

- All five urban zones use `InfernosCurse/HybridZoneTerrain` through individual Layered Florence profile and material assets.
- Districts remain recognizably distinct while sharing one coherent material language.
- No authored geometry, routes, colliders, or props change as part of the terrain pass.
- The terrain does not visibly swap when combat starts or ends.
- All three project validators pass.
- Piazza della Signoria and Mercato Vecchio pass the full exploration-to-battle-to-exploration Play Mode probe.
- Unity reports no new errors or warnings.

