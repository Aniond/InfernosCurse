# Inn Structural Placeholder Replacement Implementation Plan

## Goal

Replace the Florentine inn's primitive walls, floors, stairs, columns, lintels, and arcade pieces with a reusable hybrid structural kit while preserving the approved layout, collisions, navigation, camera behavior, and counter interaction.

## Phase 1: Baseline and Asset Audit

1. Open `Assets/Scenes/FlorentineInnFloor1.unity` and record the active hierarchy, structural transforms, collider dimensions, and material assignments.
2. Capture top, isometric, and gameplay-camera baseline images.
3. Playtest entrance-to-counter movement, all doorway routes, the locked landing, counter interaction, and wall fading.
4. Search Asset Inventory for usable period-appropriate structural references before generating new content.
5. Record the exact generated texture requirements and the current real-world scale of each surface.

Verification: baseline captures exist, structural transforms are recorded, and Unity reports zero errors.

## Phase 2: 3DAI Studio Material Generation

1. Generate seamless source textures for aged lime plaster, pietra serena, public tile, service terracotta, courtyard pavers, and dark structural timber.
2. Review generated previews before importing final texture sets.
3. Import approved albedo, normal, roughness or smoothness, and ambient-occlusion maps under `Assets/Environment/FlorentineInnFloor1/StructuralKit/Textures`.
4. Create URP materials under `StructuralKit/Materials` with consistent texel density and conservative normal strength.
5. Validate tiling and repetition on large sandbox planes at gameplay-camera distance.

Verification: every material tiles without seams, matches the approved period direction, and has a documented scale.

## Phase 3: UModeler Modular Geometry

1. Create the `StructuralKit/Meshes` and `StructuralKit/Prefabs` folders.
2. Build straight wall, wall-end, inside-corner, outside-corner, and doorway modules matching the existing wall height and thickness.
3. Build stone base, doorway surround, timber header, floor, stair slab, column, capital, base, lintel, and shallow arch modules.
4. Keep decorative geometry separate from simplified collision geometry.
5. Establish predictable prefab names compatible with the current wall-fading prefixes.

Verification: module bounds align to the 1m by 0.25m grid, pivots are consistent, and adjacent pieces show no seams or overlap.

## Phase 4: GSpawn Kit Configuration

1. Register the structural prefabs in a dedicated Florentine Inn GSpawn library.
2. Configure wall, corner, doorway, floor, column, and arcade groups.
3. Validate snapping, rotation, replacement, and repeated placement in `Assets/ToolingSandbox/LevelDesignToolsSandbox.unity`.
4. Create a representative test bay containing one doorway, one corner, two floor types, a column, and an arch.

Verification: the test bay can be assembled without manual transform correction and produces no console errors.

## Phase 5: Builder Integration

1. Add a focused structural-prefab loading and instantiation layer to `Assets/Editor/FlorentineInnFloor1Builder.cs`.
2. Preserve all current layout coordinates, dimensions, names, and gameplay collider footprints.
3. Replace primitive creation only for the approved structural categories.
4. Preserve the `InnWall_` and `CourtyardLintel_` naming conventions used by `CameraOcclusionFader`.
5. Rebuild `Assets/Scenes/FlorentineInnFloor1.unity` from the updated builder.
6. Confirm that rebuilding is deterministic and does not restore structural placeholders.

Verification: two consecutive rebuilds produce equivalent structural hierarchies and no missing-prefab or missing-material errors.

## Phase 6: Gameplay and Visual Validation

1. Run a full traversal through every existing doorway and room route.
2. Verify the entrance-to-counter route and keyboard, gamepad, and mouse counter interaction.
3. Verify the locked upper landing remains inaccessible.
4. Verify camera clearance, occlusion, and wall fading from all public rooms.
5. Inspect for gaps, z-fighting, floating modules, visible primitive structural pieces, inconsistent tiling, and collider mismatches.
6. Capture final top, isometric, and gameplay-camera images for comparison with the baseline.
7. Confirm zero Unity console errors after rebuilding and playtesting.

## Phase 7: Delivery

1. Review the visual comparison with the user.
2. Apply any approved structural corrections.
3. Commit only the structural kit, generated materials, builder integration, and rebuilt scene changes.
4. Leave furniture, NPC, lighting-fixture, and decorative-prop replacement for later passes.

## Safety and Rollback

- Work in the tooling sandbox until the material and modular kit are approved.
- Do not modify the approved floor-plan coordinates.
- Do not overwrite unrelated dirty worktree changes.
- Keep generated source textures and Unity materials in dedicated folders.
- The existing builder coordinates remain the rollback reference for every structural placement.
