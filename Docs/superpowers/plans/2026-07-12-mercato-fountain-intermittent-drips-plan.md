# Mercato Fountain Intermittent Drips Implementation Plan

Date: 2026-07-12
Design: `Docs/superpowers/specs/2026-07-12-mercato-fountain-intermittent-drips-design.md`

## Objective

Add four staggered intermittent droplet emitters beneath the Mercato fountain's upper bowl and four subtle paired splash emitters inside the lower basin. Preserve the upright 4.90 m authored model, contained 3.55 m water surface, weather registration, and deterministic production-kit workflow.

## Phase 1 - Deterministic Particle Authoring

### Files

- Modify: `Assets/Editor/MercatoVecchioProductionKitBuilder.cs`
- Create: `Assets/Environment/MercatoVecchio/ProductionKit/Materials/Mercato_FountainDrips.mat`
- Rebuild: `Assets/Environment/MercatoVecchio/ProductionKit/Prefabs/Mercato_FountainPlaza.prefab`

### Tasks

1. Add a deterministic translucent URP particle material with restrained blue-gray color.
2. Add `Fountain_UpperDrips` to the fountain prefab with four cardinal drip/splash pairs.
3. Configure falling emitters for single irregular droplets, local simulation, gravity, stretched billboards, staggered deterministic seeds, and bounded particle counts.
4. Configure landing emitters for tiny delayed bursts at the lower-basin waterline using compact billboards.
5. Keep all particle objects free of colliders, lights, audio sources, and weather-surface components.

### Gate

- The rebuilt prefab contains exactly four drip and four splash emitters.
- The existing model transform and lower-basin water dimensions remain unchanged.

## Phase 2 - Validation and Visual Verification

### Files

- Modify: `Assets/Editor/MercatoVecchioProductionKitBuilder.cs`
- Modify: `Docs/MASTER_PROJECT_MEMORY.md`

### Tasks

1. Extend `ValidateFountain` to require the effect root, exact emitter counts, valid vertical placement, basin-contained splash positions, and forbidden-component absence.
2. Rebuild the production kit and run `MercatoProductionKitValidator`.
3. Compile `Assembly-CSharp-Editor.csproj` and require zero errors.
4. Run Mercato in Play Mode and visually confirm staggered droplets and restrained basin splashes from the exploration camera.
5. Stop Play Mode, update the master handoff, inspect the scoped diff, and commit only the fountain animation files.

### Gate

- Static validation passes for all seven production prefabs.
- Play Mode confirms intermittent rather than continuous flow, no synchronized pulsing, and no particles on the plaza.
- Unity is left out of Play Mode and unrelated dirty files remain untouched.

## Risk Controls

- Use deterministic seeds and explicit constants so rebuilds are stable.
- Keep particle counts and lifetimes low for mobile compatibility.
- Do not modify the source GLB or vendor assets.
- Do not register particle children as additional weather surfaces.
- Do not push unless separately requested.
