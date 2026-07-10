# Urban Hybrid Terrain with RealBlend Implementation Plan

## Objective

Build the approved Layered Florence urban terrain family for Mercato Vecchio, Ponte Vecchio, Duomo, Piazza della Signoria, and Via Calimala. Use RealBlend for vertex-color authoring and mesh baking, preserve the existing hybrid exploration/battle and persistent-weather contracts, and prove the workflow in a ToolingSandbox Piazza sample before touching production scenes.

Design source: `Docs/superpowers/specs/2026-07-10-urban-hybrid-terrain-design.md`

## Safety Boundaries

- Preserve all unrelated dirty worktree changes documented in `Docs/MASTER_PROJECT_MEMORY.md`.
- Stage only explicit paths from this plan and Unity-generated meta files for those paths.
- Include `Assets/RealBlend.meta` and `Assets/RealBlend/**` only because the user explicitly approved the imported RealBlend package for commit scope.
- Do not edit RealBlend package source, shaders, examples, or documentation.
- Do not rebuild or reposition urban buildings, props, routes, boundaries, colliders, travel markers, battle grids, spawns, cover, or elevations.
- Keep Rose Garden and Fiesole on the existing natural control-texture path.
- Keep wetness controlled by the project weather authority rather than consuming a RealBlend vertex layer.
- Abort before saving a production scene if its ground renderer cannot be identified unambiguously.

## Task 1: Capture urban and RealBlend baselines

### Files and scenes

- Inspect `Assets/RealBlend/Art/Shaders/RealBlendVariationURP.shadergraph`.
- Inspect `Assets/RealBlend/Scripts/VertexPaintStorage.cs`.
- Inspect the five approved urban scenes without saving.
- Inspect `Assets/Editor/PiazzaSignoriaSceneBuilder.cs` and any scene-specific ground tooling found during the audit.

### Implementation

1. Refresh Unity and allow the RealBlend import and Shader Graph compilation to finish.
2. Confirm the RealBlend editor window opens and its URP variation material renders without magenta fallback.
3. Inventory every renderer under each scene's ground roots, recording mesh asset, material, collider, bounds, vertex count, vertex-color availability, and shared-mesh usage.
4. Record exploration routes, travel markers, camera framing, weather components, and hybrid combat classification for regression comparison.
5. Identify which ground objects can be migrated directly and which require a new dense baked mesh while preserving their world bounds.

### Verification

- Unity console contains zero RealBlend import or shader compilation errors.
- Every urban scene has an explicit ground inventory.
- No scene or package file is modified during baseline capture.

## Task 2: Build the Piazza ToolingSandbox proof

### Files

- Create `Assets/Editor/UrbanHybridTerrainSandboxBuilder.cs` and meta file.
- Create `Assets/ToolingSandbox/UrbanHybridTerrainSandbox.unity` and meta file.
- Create proof assets under `Assets/ToolingSandbox/UrbanHybridTerrain/`.

### Implementation

1. Duplicate the dimensions and transform behavior of Piazza's `Floor_Piazza` into the sandbox; do not reference or modify the production mesh instance.
2. Use RealBlend's floor/topology workflow to create an evenly subdivided mesh with a conservative vertices-per-metre setting.
3. Paint or deterministically seed three blend states:
   - Base/default: warm Florentine stone.
   - Vertex red: terracotta repairs or alternate paving.
   - Vertex green: dirt, grime, and edge wear.
4. Bake the proof mesh to a standalone asset and confirm the baked mesh contains colors matching its vertex count.
5. Configure a RealBlend URP proof material with representative stone, terracotta, and grime textures.
6. Preserve a side-by-side copy using the current urban material for visual and performance comparison.

### Verification gate

- The baked mesh survives scene reload without `VertexPaintStorage`.
- Vertex colors remain present after reload.
- Lighting, shadows, fog, and depth rendering work in URP 17.4.
- The proof has no obvious stretching, blend seams, or excessive vertex density.
- Do not proceed to production shader changes until this gate passes.

## Task 3: Add the urban vertex-color path to the hybrid shader

### Files

- Modify `Assets/Art/Shaders/HybridZoneTerrain.shader`.
- Modify `Assets/Scripts/Environment/HybridZoneTerrainProfile.cs`.

### Implementation

1. Add vertex color to the mesh shader input and varying structures.
2. Add a material-controlled blend-source mode:
   - Natural mode reads Unity Terrain `_Control` RGBA exactly as today.
   - Urban mode derives normalized base/layer-one/layer-two weights from baked vertex colors.
3. Keep the existing macro variation, ambient response, fog, main-light shadows, and global `_GrassWetness` behavior shared by both modes.
4. Add urban profile settings for blend-source selection, stone/repair/grime tints, per-layer wet affinity, and conservative blend contrast.
5. Preserve safe defaults so existing Rose Garden materials serialize and render unchanged.
6. Keep corruption influence neutral and keep tactical overlays outside the terrain shader.

### Verification

- Shader compiles with zero errors for the active PC URP renderer.
- Rose Garden remains visually and materially unchanged in natural mode.
- The sandbox mesh reads its baked red/green vertex channels correctly in urban mode.
- Setting global wetness affects the three urban layers without altering painted weights.

## Task 4: Add deterministic urban profile, material, and mesh generation

### Files

- Create `Assets/Editor/UrbanHybridTerrainBuilder.cs` and meta file.
- Create profiles under `Assets/Art/Environment/HybridZones/Profiles/Urban/`.
- Create materials under `Assets/Art/Environment/HybridZones/Materials/Urban/`.
- Create baked meshes under `Assets/Art/Environment/HybridZones/Meshes/Urban/`.

### Implementation

1. Add an explicit allowlist for the five approved scene paths and exact accepted ground renderer names discovered in Task 1.
2. Create/update five profile assets with the approved district tuning.
3. Create/update five material instances using `InfernosCurse/HybridZoneTerrain` in urban mode.
4. For ground requiring more density, create a new grid mesh matching the source world bounds and collider footprint; never mutate Unity primitives or shared imported meshes.
5. Seed deterministic vertex colors from authored regions, boundaries, repair bands, and low-amplitude noise so rebuilding does not erase the approved look.
6. Allow later RealBlend hand-painting to refine the baked colors without changing shader or runtime contracts.
7. Save only after profile, material, mesh colors, collider compatibility, and scene authoring validate.

### District tuning

- Mercato Vecchio: warm, worn, repair-heavy, and locally stained.
- Ponte Vecchio: cooler traffic-worn stone with restrained damp edges.
- Duomo: pale dressed stone with sparse repairs.
- Piazza della Signoria: balanced warm-gray slabs and strongest tactical boundaries.
- Via Calimala: darker compact paving with directional wear and narrow repairs.

### Verification

- Run the builder twice and confirm deterministic assets and no duplicate materials, profiles, or meshes.
- Every baked urban mesh has a non-empty color array equal to vertex count.
- Source mesh assets remain byte-for-byte untouched.

## Task 5: Extend urban validation and migrate the two extremes

### Files

- Modify `Assets/Editor/HybridZoneStandardValidator.cs`.
- Update `Assets/Scenes/PiazzaDellaSignoria.unity`.
- Update `Assets/Scenes/MercatoVecchio.unity`.
- Modify `Assets/Editor/PiazzaSignoriaSceneBuilder.cs` so rebuilding Piazza preserves or reproduces its approved urban terrain output.

### Implementation

1. Validate urban scenes for surface family, urban blend mode, profile/material ownership, baked vertex colors, ground coverage, collider preservation, and valid hybrid-zone authoring.
2. Migrate Piazza first as the open-space readability reference.
3. Migrate Mercato second as the dense prop and occlusion stress case.
4. Ensure battle startup does not swap the terrain material or mesh.
5. Ensure scene builders do not overwrite the migration with legacy flat-color materials.

### Runtime verification gate

For both scenes verify:

- Exploration movement and collision.
- Uniform locked camera behavior.
- Weather and wetness response.
- In-place battle startup and tactical readability.
- Victory cleanup and exploration-camera restoration.
- No terrain material or mesh identity swap at battle boundaries.
- Zero new Unity errors or warnings.

Do not migrate the remaining three scenes until both extremes pass.

## Task 6: Migrate Ponte Vecchio, Duomo, and Via Calimala

### Files

- Update `Assets/Scenes/PonteVecchio.unity`.
- Update `Assets/Scenes/Duomo.unity`.
- Update `Assets/Scenes/ViaCalimala.unity`.
- Modify any authoritative scene-specific ground builder discovered in Task 1.

### Implementation

1. Apply the shared urban workflow using each approved district profile.
2. Preserve scene geometry, paths, colliders, props, boundaries, travel wiring, and battle authoring.
3. Rebuild any authoritative builder twice to confirm it reproduces the new ground output.
4. Leave unsupported renderers unchanged and report them rather than guessing.

### Verification

- Each scene passes the urban hybrid validator.
- Exploration routes and collision probes match baseline.
- One encounter transition per scene preserves terrain and camera state.
- Districts read as one Florence family without becoming visually identical.

## Task 7: Full regression and performance pass

### Validators

1. Run `HybridZoneStandardValidator` across all enabled scenes.
2. Run `BattleTerrainStandardValidator`.
3. Run `WeatherSurfaceStandardValidator`.

### Runtime matrix

- Piazza: clear exploration through victory.
- Mercato: rain exploration through victory.
- Ponte Vecchio: wet edge and camera-boundary check.
- Duomo: pale-surface tactical-overlay check.
- Via Calimala: narrow-street battle readability check.
- Rose Garden: natural control-texture regression.

### Performance checks

- Compare sandbox current-material and urban-vertex-material GPU/frame timing under the same camera.
- Record mesh vertex/triangle counts before and after migration.
- Reduce topology density if the blend improvement is not visible at the locked HD-2D camera distance.
- Confirm shader variant count and build logs do not show an unexpected explosion.

### Completion threshold

- All validators pass.
- All six runtime scenes complete their assigned probe.
- Unity console contains zero new errors or warnings attributable to the pass.
- No material swaps, duplicate battle rigs, lost camera state, or collider regressions occur.

## Task 8: Commit discipline and handoff

1. Review `git status --short` and preserve all unrelated existing changes.
2. Stage the RealBlend package explicitly as `Assets/RealBlend.meta` and `Assets/RealBlend/**`; do not stage other Asset Store or tooling packages.
3. Stage the implementation scripts, shader, profiles, materials, baked meshes, approved scene outputs, builder updates, and their meta files explicitly.
4. Run `git diff --cached --check`, separating harmless Unity serializer output from hand-authored whitespace failures.
5. Verify the staged diff contains no credentials, installers, WSL images, fonts, package-manifest changes, or unrelated BattleArena edits.
6. Commit the verified implementation with a focused message such as `Build RealBlend urban hybrid terrain`.
7. Do not push unless explicitly requested.

## Completion Criteria

- RealBlend is imported as an approved, unmodified editor dependency.
- Five urban zones use baked vertex colors for stone, repair, and grime blending.
- Natural zones retain their control-texture path.
- Persistent weather remains authoritative for wetness.
- Exploration and battle share the same visible urban ground.
- The locked exploration camera hands off to tactical combat and restores reliably.
- All scene geometry, paths, colliders, props, and tactical authoring remain intact.
- The full validation matrix passes with zero new Unity errors attributable to the implementation.

