# Inn Structural Placeholder Replacement Design

## Objective

Replace the primitive structural placeholders in `FlorentineInnFloor1` with a coherent thirteenth-century Florentine inn kit while preserving the approved floor plan and all gameplay dimensions.

This pass covers walls, floors, stairs, courtyard columns, lintels, and arcade arches. Furniture, NPC markers, lighting fixtures, and decorative props remain unchanged.

## Non-Negotiable Constraints

- Preserve every existing wall footprint, doorway width, room dimension, collider boundary, and walkable route.
- Preserve the entrance-to-reception-counter axis and the counter interaction behavior.
- Preserve camera occlusion and wall-fading behavior.
- Keep `FlorentineInnFloor1Builder` authoritative. Rebuilding the scene must instantiate the finished structural prefabs instead of recreating primitive placeholders.
- Use historically restrained Florentine materials and forms. Avoid modern objects, polished marble, and ornate later-Renaissance detailing.
- Keep the current first-floor scope. Do not build the second floor.

## Production Approach

Use a hybrid pipeline:

- UModeler X creates dimension-locked modular geometry.
- GSpawn manages the modular prefab kit and grid-aligned placement.
- 3DAI Studio generates seamless surface textures and supporting material maps.
- Unity URP materials assemble the generated maps and enforce consistent real-world texture scale.

Asset-library-only replacement was rejected because arbitrary meshes are unlikely to match the approved footprint. Fully generated structural meshes were rejected because their dimensions and collision would be less predictable.

## Modular Structural Kit

Create reusable prefabs for:

- Straight plaster wall sections matching the existing wall thickness and height.
- Wall ends, inside corners, and outside corners.
- Doorway sections with plaster reveals, timber headers, and optional stone surrounds.
- Pietra-serena wall bases and restrained corner accents.
- Public-room floor modules.
- Service-room terracotta floor modules.
- Courtyard brick or paver modules.
- Pietra-serena stair and street-apron slabs.
- Tuscan courtyard columns with square bases and simple capitals.
- Shallow arcade arch and lintel sections that preserve existing clearances.

Modules use the configured one-metre horizontal grid, 0.25-metre vertical increments, and 15-degree rotational snapping. Smaller trim may use subdivisions without altering structural footprints.

## Material Set

3DAI Studio will produce coordinated, seamless materials for:

1. Warm aged lime plaster with subtle variation and restrained cracking.
2. Cool gray pietra serena with worn edges and low polish.
3. Worn public-room terracotta or pale stone tile.
4. Rougher kitchen and service-room terracotta.
5. Weathered courtyard brick or pavers.
6. Dark structural timber for doorway headers and limited accents.

Where supported, each material includes albedo, normal, roughness or smoothness, and ambient-occlusion maps. Unity materials use URP-compatible shaders. Texel density and tile scale remain consistent across modules, and repeated patterns must not become visually obvious at the gameplay camera distance.

## Scene and Builder Integration

Finished structural assets live under a dedicated `Assets/Environment/FlorentineInnFloor1/StructuralKit` hierarchy with separate `Meshes`, `Prefabs`, `Materials`, and `Textures` folders.

`FlorentineInnFloor1Builder` retains the existing layout coordinates but replaces primitive construction calls for approved structural categories with prefab instantiation. Structural prefab roots retain predictable names so `CameraOcclusionFader` can continue identifying walls and lintels. Gameplay colliders remain simple and dimensionally equivalent to the current primitives; decorative mesh detail does not define collision.

The existing scene is rebuilt from the updated builder only after the prefabs and materials have been validated in the tooling sandbox.

## Validation

The structural pass is accepted when all of the following are true:

- The player can traverse every existing doorway and room route.
- The path from the street entrance to the reception counter remains clear.
- Keyboard, gamepad, and mouse counter interaction still work.
- Camera occlusion and wall fading behave as before.
- The upper-floor landing remains blocked and clearly unfinished.
- Structural colliders introduce no invisible obstruction or unintended gaps.
- No wall seams, overlaps, floating pieces, z-fighting, or primitive structural placeholders remain visible.
- Top, isometric, and gameplay-camera captures show consistent material scale and alignment.
- Unity reports zero console errors after scene rebuild and playtesting.

## Delivery Boundary

This pass does not replace tables, chairs, counters, shelves, barrels, crates, plants, fountain dressing, NPC capsules, lamps, or other decorative props. Those will be handled in later replacement passes after the structural result is approved in-engine.
