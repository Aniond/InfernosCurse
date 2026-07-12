# Mercato Inn Player-Visibility Camera Design

**Status:** Approved design, pending written-spec review

**Date:** 2026-07-12

**Scope:** Albergo Fiorentino seamless entry in `MercatoVecchio`

## Problem

The Albergo Fiorentino threshold correctly activates its seamless-interior state and temporary camera profile, but the player can no longer be seen after entering. The current automated probe only proves that the camera offset moves toward the authored profile; it does not prove that the player remains visible in the rendered frame.

The inn facade is instantiated separately from the seamless interior module. It is created after the camera zone is configured, its renderer names do not match the zone's approved interior-wall prefixes, and its renderers are not included in the zone's explicit occluder list. Consequently, facade geometry can remain between the shared camera and the player after entry.

## Approved Behavior

- Continue using the existing shared Cinemachine exploration camera.
- Keep the camera following the player continuously before, during, and after the threshold crossing.
- Do not add a fixed room camera, a second Main Camera, or a room-center follow target.
- On interior entry, activate the existing temporary inn camera profile and allow approved facade, roof, and interior-wall renderers that block the player sightline to switch to shadows-only rendering.
- Preserve non-blocking architecture so the inn retains its dollhouse presentation.
- On exit, restore the live exterior camera profile and every renderer's original state.

## Authoring and Runtime Design

`MercatoVecchioProductionBuilder.BuildInn` will instantiate the facade before configuring the inn's `SeamlessInteriorCameraZone`. The builder will gather the facade's renderers together with the existing approved roof and interior occluders, then configure the zone once with the complete deterministic set.

`SeamlessInteriorCameraZone` remains the sole owner of the temporary `DynamicZoom.CameraOverrideProfile` and the temporary occluder configuration. `DynamicZoom` remains player-following and continues blending between the live exterior result and the inn profile. `CameraOcclusionFader` remains the sightline authority and preserves its shadows-only behavior.

The change must be idempotent when the Mercato production scene is rebuilt. It must not create duplicate cameras, camera adapters, facade instances, or override owners.

## Validation

The static Mercato production validator will require the seamless inn camera zone to include the complete facade renderer set in its explicit occluders. It will reject missing facade coverage and duplicate camera authority.

The Mercato seamless Play Mode probe will continue checking entry/exit state, registry state, local lighting, battle protection, override ownership, and exterior restoration. It will additionally verify after interior entry that:

- the player projects in front of the camera;
- the player's viewport position stays inside the authored safe frame;
- no registered facade renderer remains as an opaque sightline blocker;
- the same player transform remains the active camera follow target.

Manual Game-view review will confirm that the player remains visible while crossing the threshold and walking immediately inside the inn, with a readable amount of nearby room context. Unity must be left out of Play Mode after verification.

## Non-Goals

- Rebuilding or rearranging the inn interior.
- Adding per-room cameras or room-center anchors.
- Changing battle-camera behavior.
- Retuning unrelated Mercato exploration framing.
- Altering the seamless-interior workflow for other buildings in this pass.
