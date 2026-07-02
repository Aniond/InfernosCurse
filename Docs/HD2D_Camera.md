# HD-2D Camera System — The Inferno's Curse

The Octopath-style camera rig, verified in both scene shapes (open plaza +
narrow corridor). **Copy `Assets/Prefabs/HD2D_CameraKit.prefab` into every new
scene** — it contains the whole stack.

## Why this rig is NOT orthographic / 45°-yaw

Authentic Octopath Traveler is a **perspective tilt-shift camera facing north**
(yaw 0), not an orthographic 45° isometric. This project's rig matches that:
narrow FOV perspective + depth-of-field bokeh = the HD-2D "diorama" look.
Orthographic/wide attempts were tried early and kept exposing void past the
building line — do not revisit without reading this doc.

## The stack (all in HD2D_CameraKit.prefab)

| Piece | Setting | Why |
|---|---|---|
| Main Camera | Perspective, **FOV 36**, pitch **40°**, yaw **0°** | Narrow FOV = tilt-shift compression; 40° pitch reads sprites + facades together |
| CinemachineCamera (CM_PlayerCam) | `CinemachineFollow`, WorldSpace, damping 0.4 | Smooth follow, no jitter/overcorrection |
| CinemachineConfiner2D | **DISABLED** | Bounding comes from solid geometry, not a confiner — the confiner's view-inset kept clamping short. Component left for reference. |
| `DynamicZoom` | drives FollowOffset every LateUpdate | wide (0,15,−14.5) ↔ close (0,11,−9.5); NEVER hand-edit FollowOffset — this script overwrites it |
| `DynamicZoom` bottom clamp | bottomSafeMargin 0.25 | Fixes the south-walk cutoff: pushes camera back/up when the sprite nears the bottom edge |
| Global Volume | `SampleSceneProfile`: Tonemapping, Bloom, Vignette, **DepthOfField (Bokeh, 75mm, f/7)** | The DoF is the single biggest diorama-hider. MotionBlur is disabled (sprite smear). |
| `DepthOfFieldFocus` (on Global Volume) | focusSpeed 8 | Tracks true 3D camera→player distance into DoF focusDistance. Self-heals its player ref by tag. |
| `SpriteBillboard` (on player) | tiltFactor **0.75** | Full-pitch tilt clips the sprite's bottom edge at the south screen edge |

## DynamicZoom — pick the mode per scene

- **`useClearanceZoom = false` (script default) — zone-center/radial mode.**
  For open plazas with an authored center (Mercato: center (0,0), outer 8,
  inner 16). Distance from center → wide at center, close at edges.
- **`useClearanceZoom = true` — clearance mode.** For corridors and street
  scenes (Ponte Vecchio, the prefab default). 8 horizontal rays measure
  distance to the nearest *tall* collider: `wideClearance` 9 → wide,
  `closeClearance` 3 → close. `minArchitectureHeight` filters props:
  **6.5** counts only real buildings (stalls/fountains don't pump the zoom);
  lower it to **~2.5** in blockouts whose dividers are short (PV uses 2.5).

Why two modes: Mercato's plaza is small — its building ring is within ~7 of
center, so clearance mode can never read it as "open". The radial mode is the
tuned, shipped behavior there. Clearance mode generalizes everywhere else
without per-scene center tuning.

## Scene checklist for a new zone

1. Drop in `HD2D_CameraKit.prefab`. Tag the player **"Player"** — every script
   self-heals its target by tag.
2. Solid-collider the boundary: **every building/wall gets a BoxCollider.**
   The player being physically contained is what keeps the camera in bounds.
3. Backdrop planes anywhere the camera can see past geometry (camera looks
   north and down — usually north + below). DoF blurs them into atmosphere.
4. Floor at y=0; water/backdrops NOT coplanar with floor (z-fighting).
5. Pick the DynamicZoom mode (above) and set `minArchitectureHeight` for the
   scene's architecture scale.
6. Playtest all four walk directions — especially south (bottom clamp) — and
   any tight squeezes.

## Verified 2026-07-01

- **Ponte Vecchio (corridor)**: corridor t=0.83 (zooms close), terrace t=0.56,
  kiosk squeeze t=1.0 with player at viewport y 0.25; south parapet hug —
  player fully on screen (feet 0.27); DoF focus == true camera distance;
  parapet/walls block movement; exit gap passable.
- **Mercato (plaza)**: fresh load = radial mode with original tuned values
  (code-identical to the pre-v2 behavior that shipped); plaza point reads
  full wide. Scene file untouched by this work.

## Editor gotchas hit during this work

- `VolumeProfile.Add<T>()` in editor scripts does NOT persist — you must also
  `AssetDatabase.AddObjectToAsset(component, profile)` then save, or the
  override exists only in memory.
- Script-default changes don't affect an ALREADY-OPEN scene across domain
  reload (in-memory state survives); they apply on fresh scene load.
- Unfocused editor barely pumps play-mode frames; `EditorApplication.Step()`
  can recurse the PlayerLoop in heavy scenes — prefer testing with the editor
  focused.
