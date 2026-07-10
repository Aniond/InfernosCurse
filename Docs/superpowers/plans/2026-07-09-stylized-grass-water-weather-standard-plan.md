# Stylized Grass, Water, and Weather Surface Implementation Plan

## Goal

Convert every applicable exploration and generated battle scene to project-controlled Stylized Grass and Stylized Water 3 assets, connect those surfaces to the persistent `FlorenceWeather`/COZY state, add bounded rain impacts, and make the same integration mandatory for future builders.

## Phase 1: Baseline and Package Integration Audit

1. Record all authored scenes, environment prefabs, battle-map prefabs, builders, and materials that currently present grass or water.
2. Inspect Stylized Grass material, weather, wind, and render-feature requirements.
3. Inspect Stylized Water 3 material, `WaterObject`, mesh, wave, and render-feature requirements.
4. Confirm active URP renderer features, scripting defines, COZY integration, and baseline Unity console state.
5. Capture the relevant surface hierarchy and renderer/material assignments in Fiesole, GiardinoDelleRose, PonteVecchio, MercatoVecchio, FlorentineInnFloor1, and representative battle maps.

Verification: every candidate has an explicit conversion profile or documented skip reason, and baseline compile/runtime errors are recorded before edits.

## Phase 2: Shared Runtime Standard

1. Add project-owned surface profile and exposure enums.
2. Implement `WeatherSurfaceController` as the read-only adapter for `FlorenceWeather.CurrentProfileName` and the existing wind state.
3. Implement explicit `WeatherSurface` registration for grass/water kind, exposure, renderer/terrain, and effect bounds.
4. Drive global Stylized Grass wetness through the package-supported shader property/controller.
5. Install and configure the package wind/weather controllers through the persistent game-systems prefab without modifying package source.
6. Implement rain-intensity easing for clear, cloudy, fog, light rain, heavy rain, and storm.
7. Add allocation-free particle emission control for registered exposed surfaces.

Verification: a runtime harness can force each existing weather profile and report the expected normalized wetness, wind, and precipitation intensity with no duplicate weather owner.

## Phase 3: Project Surface Assets and Editor Factory

1. Create the project WeatherSurfaces folder structure.
2. Create river, pond, fountain, and shallow-battle variants based on Stylized Water 3.
3. Create healthy and dry grass variants based on the Stylized Grass shader.
4. Create a reusable project grass-clump mesh/prefab suitable for terrain details and deterministic fields.
5. Create water ripple/upward-splash and restrained grass-impact particle prefabs using existing licensed textures/materials.
6. Implement `WeatherSurfaceStandardBuilder` helpers for water configuration, grass field/detail configuration, exposure, deterministic placement, and effect bounds.
7. Implement an editor validator that reports legacy/missing surface integration in build scenes, relevant prefabs, and authoritative builders.

Verification: assets load by GUID/path, use the intended vendor shaders, contain no missing references, and a small sandbox instance responds to weather.

## Phase 4: Exploration Scene Conversion

1. Update `FiesoleSceneBuilder` and Fiesole scene output with a deterministic Stylized Grass field over the meadow surface.
2. Update both Giardino builders and Giardino scene output with Stylized Grass terrain/detail coverage and standardized fountain/pond water.
3. Convert PonteVecchio Arno water to the river profile, add `WaterObject`, surface registration, and bounded rain impacts.
4. Convert MercatoVecchio river/fountain water to the correct profiles with exposure-aware effects.
5. Convert FlorentineInnFloor1 fountain water through its authoritative builder, retaining its animated fountain presentation and sheltered rain configuration.
6. Confirm scenes without visible grass/water remain unchanged.

Verification: every converted renderer uses the project standard, rebuilds retain integration, and clear/light/heavy rain produce the expected surface response.

## Phase 5: Generated Battle-Map Conversion

1. Update `BattleMapMeshBuilder` so grass-designated terrain produces deterministic short Stylized Grass patches that do not affect collision, occupancy, cover, or line of sight.
2. Avoid paths, obstacles, unwalkable cells, water, and spawn areas when placing battle grass.
3. Replace optional battle water planes with configured Stylized Water 3 surfaces and bounded rain emitters.
4. Normalize existing battle style water references to the project shallow/river variants as appropriate.
5. Ensure battle surfaces continue using `FlorenceWeather.CurrentProfileName` when broader COZY scene effects are paused.
6. Rebuild representative grassy and water-capable 3D battle prefabs twice to prove deterministic output.

Verification: tactical highlights and units remain readable; terrain rules are unchanged; clear/light/storm weather transitions work in combat.

## Phase 6: Coverage, Runtime, and Performance Validation

1. Run the editor coverage validator across all build scenes and relevant prefabs.
2. Run Unity compilation and inspect the console for errors and actionable warnings.
3. Exercise Fiesole, GiardinoDelleRose, PonteVecchio or MercatoVecchio, FlorentineInnFloor1, and one 3D battle map under clear, light-rain, and heavy-rain/storm profiles.
4. Verify rain stops cleanly after returning to clear weather.
5. Verify the inn fountain receives fountain animation but no outdoor rain impacts.
6. Verify grass wetness and wind change without particle clutter or excessive bending.
7. Verify river emission is globally capped and distant/offscreen surfaces reduce emission.
8. Verify curse/insanity presentation still applies without permanently replacing the standard materials.
9. Profile for steady-state allocations and particle/renderer spikes at storm intensity.

Verification: zero unexplained coverage findings, zero Unity console errors, no steady-state instantiate/destroy loop, and no gameplay-rule changes.

## Phase 7: Documentation and Delivery

1. Record final scene coverage, material profiles, validator results, and any approved skips.
2. Update the authoritative master project memory with the implementation commit and runtime evidence.
3. Stage only the shared system, project surface assets, authoritative builders, converted scenes/prefabs, validation output, and documentation.
4. Commit the verified implementation; push only when explicitly requested.

## Safety and Scope Controls

- Do not edit vendor package source.
- Do not introduce a second weather or clock owner.
- Do not alter map layouts, navigation, combat occupancy, cover, line of sight, or physics.
- Do not add flooding, swimming, buoyancy, mud gameplay, or new weather profiles.
- Do not add grass colliders.
- Do not allow indoor or sheltered water to receive outdoor rain impacts.
- Do not replace runtime material instances used by curse/insanity presentation.
- Preserve unrelated dirty working-tree changes and stage explicit paths only.
