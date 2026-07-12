# Mercato Vecchio Stalls, Seamless Camera, and Map Card Implementation Plan

Date: 2026-07-12
Design: `Docs/superpowers/specs/2026-07-12-mercato-stalls-camera-map-card-design.md`

## Objective

Replace Mercato's visible stall blockouts with production commerce prefabs, add reusable automatic camera profiles for seamless interiors with the Florentine Inn as the first authored use, and make the Gugol Mappe location card fit desktop and phone-like viewports without overlap.

All work must preserve the current locked HD-2D camera angle, Mercato navigation and hybrid-battle geometry, one shared Main Camera, stable seamless-interior IDs, existing map callbacks, and unrelated dirty worktree files.

## Phase 1 — Source Asset Audit and Proof

### Files inspected

- `Assets/Environment/MarketSquare/Props/Florentine_Stall.glb`
- `Assets/Environment/GiardinoDelleRose/Props/florist-market-stall.glb`
- `Assets/Low-Poly Medieval Market/Prefabs/`
- `Assets/Environment/MercatoVecchio/ProductionKit/Prefabs/Mercato_Stall_*.prefab`
- `Assets/Editor/MercatoVecchioProductionKitBuilder.cs`
- `Assets/Editor/MercatoVecchioProductionBuilder.cs`

### Tasks

1. Add an editor-only source audit/preview command to `MercatoVecchioProductionKitBuilder` or a focused `MercatoCommercePolishBuilder`.
2. Load the 3D AI Studio Florentine stall and imported merchandise prefabs by exact asset path; fail with actionable errors when required sources are missing.
3. Instantiate the Florentine stall in an isolated preview or prefab-content context and measure renderer bounds, pivot, material count, triangle count, and collider state.
4. Normalize root scale and orientation in a project-owned wrapper rather than changing the source GLB importer or vendor prefabs.
5. Review the normalized source at the live Mercato camera distance and identify which trade roles need their own generated silhouette.
6. Run an intentional 3D AI Studio commerce batch for at least three high-visibility pieces: bakery, produce, and dry-goods/cloth. Generate additional carts, scales, racks, tables, or street furniture when they materially improve the locked-camera composition.
7. Record task IDs, prompts, generation settings, source images when used, and intended role in project-owned metadata without storing credentials.

### Gate

- Unity compiles with no new errors.
- Source audit identifies reusable supporting assets and the generated commerce batch completes or records a recoverable task state.
- No source/vendor asset is modified.

## Phase 2 — Project-Owned Commerce Prefabs

### Files

- Modify: `Assets/Editor/MercatoVecchioProductionKitBuilder.cs`
- Modify: `Assets/Editor/MercatoVecchioProductionBuilder.cs`
- Create: `Assets/Environment/MercatoVecchio/ProductionKit/Prefabs/Commerce/`
- Create as needed: project-owned material overrides under `Assets/Environment/MercatoVecchio/ProductionKit/Materials/Commerce/`
- Rebuild: `Assets/Scenes/MercatoVecchio.unity`

### Tasks

1. Add deterministic builder helpers for source-prefab instancing, normalized local bounds, simplified compound collision, and non-colliding merchandise children.
2. Build four wrapper prefabs:
   - `Mercato_Stall_Produce`
   - `Mercato_Stall_Bakery`
   - `Mercato_Stall_DryGoods`
   - `Mercato_Stall_General`
3. Mix the existing generated Florentine stall with the new trade-specific 3D AI Studio pieces so repeated commerce reads as one coherent market rather than cloned booths.
4. Populate variants with exact imported prefabs such as `apple_box`, `basket_01`, `basket_02`, `loaf_basket`, `bag`, wooden boxes, apples, and lanterns.
5. Author asymmetric deterministic clusters with small rotation/scale variations. Keep merchandise collision disabled.
6. Normalize any overly glossy or saturated materials through project-owned URP overrides while preserving useful authored texture detail.
7. Replace the current red/ochre/green blockout array in `BuildCommerce` with the four production roles. Preserve the current 16 placement anchors, facing, aisle widths, navigation, and battle-grid relationship.
8. Ensure stall bounds remain below DynamicZoom's architecture threshold and do not enter authored routes.
9. Add a validator that rejects remaining production `Stall_Goods_*` primitive renderers, missing sources, invalid colliders, duplicate commerce roots, or route intersections.

### Gate

- Production-kit validator passes.
- Mercato scene rebuild is deterministic.
- Gameplay-camera review confirms appropriate scale, material response, readable variety, and clear aisles.

### Suggested commit

`Replace Mercato blockout stalls with production commerce`

## Phase 3 — Reusable Seamless Interior Camera Profiles

### Files

- Modify: `Assets/Scripts/Camera/DynamicZoom.cs`
- Modify: `Assets/Scripts/Camera/SeamlessInteriorCameraZone.cs`
- Modify: `Assets/Editor/FlorentineInnSeamlessModuleBuilder.cs`
- Modify: `Assets/Editor/MercatoVecchioProductionBuilder.cs`
- Modify: `Assets/Scripts/Validation/MercatoSeamlessPlayModeProbe.cs`
- Rebuild: `Assets/Environment/FlorentineInnFloor1/Prefabs/FlorentineInnFloor1_Module.prefab`
- Rebuild: `Assets/Scenes/MercatoVecchio.unity`

### Tasks

1. Add a serializable camera-override profile to `DynamicZoom` with follow offset, composition/pan offset, blend duration, optional room bounds, and priority.
2. Preserve the existing exterior zoom calculation as the default. A temporary override changes only the target offset/composition and blends using unscaled or normal frame time appropriate to exploration.
3. Expose explicit `PushOverride(owner, profile)` and `RemoveOverride(owner)` APIs. Repeated calls from the same owner are idempotent; higher-priority future zones win deterministically.
4. Extend `SeamlessInteriorCameraZone` with the authored profile and one-warning resolution of the shared `DynamicZoom`/Cinemachine camera.
5. On entry, capture/apply the inn override while retaining the player follow target. On exit, remove the override and blend back to the live exterior profile rather than restoring a stale hard-coded offset.
6. Keep the existing occlusion-group switching in the same zone.
7. Author the Florentine Inn lobby/courtyard profile in the seamless-module builder. Use a tighter offset with modest lateral/inward composition; do not create a second camera.
8. Update the Mercato scene builder so the embedded module receives the same profile after rotation/placement.
9. Extend `MercatoSeamlessPlayModeProbe` to record the exterior offset, cross the portal, verify gradual movement toward the interior offset, cross back, and verify gradual restoration after five repetitions.
10. Validate that the Main Camera count, follow target, player control, occlusion, and protected battle lock remain unchanged.

### Gate

- Static seamless-module validation passes.
- Live repeated-crossing probe passes without camera duplication, snaps, lost follow target, or residual overrides.
- Manual gameplay-camera review confirms the room remains framed without exposing missing architecture.

### Suggested commit

`Add automatic camera profiles for seamless interiors`

## Phase 4 — Responsive Gugol Mappe Location Card

### Files

- Modify: `Assets/Scripts/Map/GugolDirectionsCard.cs`
- Modify: `Assets/Scripts/Validation/GugolMapPlayModeProbe.cs`
- Modify if contract checks require it: `Assets/Editor/GugolMapAuthoringValidator.cs`

### Tasks

1. Replace the unbounded root `ContentSizeFitter` layout with a bounded responsive panel containing:
   - a fixed header row;
   - a scroll viewport/content column;
   - clamped preview and description layout elements;
   - normal-flow status, action, Nearby header, and nearby rows.
2. Derive card width and maximum height from the parent canvas/viewport with desktop maxima and phone-safe margins. Recompute only when the parent dimensions change.
3. Remove fixed `300f` stats-label width. Allow the stats row to wrap/reflow within the content width.
4. Hide the stats row in `ShowCurrent`, removing the unexplained rating/review text for the player's current location.
5. Preserve route/time/fare/weather information for destination and region modes.
6. Rename the visible section label to `Nearby locations` and enforce a mobile-friendly minimum row height.
7. Keep all existing buttons and callbacks; layout changes may not alter travel, jump, close, or map-state behavior.
8. Extend the Gugol Play Mode probe to exercise current, district-destination, and region-destination cards at desktop and phone-like portrait canvas dimensions.
9. Assert that every visible child rectangle remains within the panel/viewport and that only one directions card exists after reopen.

### Gate

- Gugol authoring validation passes.
- Desktop and phone-like Play Mode probes pass.
- Manual phone-width screenshot shows no overflow or overlapping content.

### Suggested commit

`Make Gugol location cards responsive`

## Phase 5 — Final Integration and Handoff

### Files

- Modify: `Docs/MASTER_PROJECT_MEMORY.md`
- Modify/add focused validators only as required by failures found during integration.

### Tasks

1. Run fresh Unity compilation/import checks after all generated prefabs and scene rebuilds.
2. Run:
   - Mercato production/commerce validator;
   - Florentine Inn seamless-module validator;
   - Mercato seamless Play Mode probe;
   - refined Gugol Mappe authoring validator;
   - Gugol Mappe Play Mode probe;
   - hybrid-zone and weather-surface validators because the Mercato scene is rebuilt.
3. Capture gameplay-camera evidence of the polished stalls and inn camera entry/exit.
4. Inspect Git diff/status and stage only the approved files. Leave package, font, BattleArena, COZY forecast, tooling, and unrelated scene changes untouched.
5. Update the master handoff with asset sources/task IDs, generated wrapper paths, camera profile behavior, map-card rules, validation evidence, and commit IDs.

### Gate

- No new Unity C# errors.
- All focused static and Play Mode validators pass on the current tree.
- Play Mode is stopped at handoff.
- Approved changes are committed; no push occurs unless separately requested.

## Risk Controls

- Never edit or reserialize source GLBs/vendor prefabs when a wrapper prefab is sufficient.
- Do not rebuild all Mercato systems blindly; use deterministic focused builders and inspect scene diffs.
- Generated props must not bring API credentials or raw service responses into source control; only stable source/task metadata and production assets are tracked.
- Camera overrides must be owner-based and removable so nested/future interiors cannot leave global camera state behind.
- The responsive card must preserve callbacks and immutable player-knowledge behavior; only presentation changes.
- Avoid staging `.superpowers/` visual-companion output.
