# Uniform HD-2D Exploration Camera Design

Date: 2026-07-10

## Goal

Standardize every exploration scene on one locked HD-2D camera composition inspired by Octopath Traveler and similar diorama-style RPGs. The result must preserve the project's existing perspective, depth-of-field, and occlusion systems while eliminating scene-dependent zoom changes.

The separate battle camera is outside this pass and must remain unchanged.

## Approved Camera Standard

All exploration scenes use the shared `Assets/Prefabs/HD2D_CameraKit.prefab` with these settings:

- Perspective projection.
- Field of view: 36 degrees.
- Camera-rig pitch: 40 degrees.
- Camera-rig yaw: 0 degrees.
- Cinemachine follow offset: `(0, 13, -12)`.
- Cinemachine position damping: `(0.4, 0.4, 0.4)`.
- No adaptive clearance zoom.
- No radial or scene-center zoom.
- No exploration-scene lens or offset overrides.
- Cinemachine Confiner 2D remains disabled.

The locked offset intentionally replaces the current wide-to-close range. It provides one consistent character scale and room framing across interiors, streets, and plazas.

## Retained HD-2D Treatment

The following parts of the existing camera stack remain active:

- Player-tracked depth-of-field focus.
- Bokeh depth of field from the shared volume profile.
- Bloom and restrained vignette.
- Motion blur disabled to avoid pixel-sprite smearing.
- Existing sprite billboard behavior.
- Existing camera shake/impulse support.
- Scene-specific `CameraOcclusionFader` configuration, including the Florentine inn wall and lintel prefixes.
- The current runtime bottom-edge push is removed with `DynamicZoom`, because it changes the follow offset. The fixed composition must keep the player visible without runtime offset changes.

## Architecture

The shared prefab is the single source of truth for exploration composition. `DynamicZoom` must no longer drive `CinemachineFollow.FollowOffset` in exploration scenes. The preferred implementation is to remove or disable `DynamicZoom` on the shared prefab and author the approved fixed offset directly on `CinemachineFollow`.

Scene builders may add scene-specific occlusion-fader prefixes, but they may not change lens, pitch, yaw, follow offset, damping, or zoom behavior. Existing scene instances must be normalized so prefab overrides cannot preserve old adaptive settings.

## Scope

In scope:

- The shared HD-2D exploration camera prefab.
- Exploration scenes and builder code that override shared camera composition.
- Camera documentation describing the shared standard.
- Runtime verification in representative indoor, corridor, and open-area scenes.

Out of scope:

- `BattleCameraRig` and battle-scene framing.
- Cutscene cameras.
- Level geometry, colliders, room dimensions, or navigation.
- Lighting, materials, props, NPCs, and gameplay systems.

## Verification

The pass is complete only after all of the following are verified:

1. The shared prefab reports perspective projection, 36-degree FOV, 40-degree pitch, 0-degree yaw, fixed `(0, 13, -12)` follow offset, and `0.4` position damping.
2. `DynamicZoom` does not modify the exploration follow offset at runtime.
3. At least one interior, one corridor/street, and one open-area exploration scene use the same framing values.
4. Walking in all four directions does not clip the player at the screen edge.
5. Depth of field continues to keep the player on the sharp focal plane.
6. Florentine inn wall and lintel occlusion fading still works.
7. The battle camera and battle-scene framing are unchanged.
8. Unity reports no new console errors or warnings.

## Implementation Safety

- Preserve unrelated dirty worktree changes.
- Stage only camera-pass files.
- Do not rebuild or alter the Florentine inn floor plan.
- Show the proposed implementation diff before applying camera changes.
- If the fixed composition fails the player-visibility test, stop and present the runtime evidence for design review instead of adding adaptive camera movement.
