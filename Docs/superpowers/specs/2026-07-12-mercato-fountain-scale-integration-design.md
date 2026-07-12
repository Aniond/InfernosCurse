# Mercato Fountain Scale Integration Design

Date: 2026-07-12
Status: Approved

## Goal

Scale the authored Mercato fountain so its stone basin contains the existing weather-responsive water surface and the two read as one physical fountain from the locked HD-2D gameplay camera.

## Approved Direction

Option B, the natural basin fit, is approved:

- Preserve the existing 4.3-metre water-surface diameter.
- Scale the authored fountain model to a 4.9-metre horizontal outer diameter.
- Leave approximately 0.30 metres of visible stone rim around the water.
- Preserve the existing 9.6-metre plaza base, fountain location, water height, weather registration, rain impacts, navigation, and battle geometry.
- Scale the complete authored model uniformly so its proportions remain intact.
- Ground and centre the scaled model from measured renderer bounds rather than relying on a magic vertical offset.

## Implementation

`MercatoVecchioProductionKitBuilder.BuildFountain` will normalize the imported `Fountain.glb` instance from renderer bounds:

1. Measure the instantiated model's combined renderer bounds.
2. Apply a uniform scale that makes the larger horizontal dimension 4.9 metres.
3. Re-measure and translate the model so its horizontal centre matches the prefab origin and its lowest point rests on the plaza base.
4. Keep `Fountain_WaterSurface` at its existing 4.3-metre diameter and existing vertical position.

The production-kit validator will reject a missing authored model or a basin diameter outside a small tolerance around 4.9 metres. This prevents future deterministic rebuilds from restoring the undersized 1.45-scale model.

## Validation

- Rebuild the Mercato production kit and scene.
- Confirm the commerce, Mercato scene, weather-surface, and hybrid-zone validators still pass.
- Review the fountain in Play Mode at the standard gameplay camera.
- Confirm the water remains inside the stone lip without clipping through it.
- Confirm Unity is left out of Play Mode and unrelated working-tree changes remain unstaged.

## Scope

This pass changes only the Mercato fountain model scale/grounding and its validation. It does not redesign the model, resize the plaza, replace the water material, alter fountain animation, or change nearby routes.
