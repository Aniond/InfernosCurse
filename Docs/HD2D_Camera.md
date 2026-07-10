# Uniform HD-2D Exploration Camera — The Inferno's Curse

The shared exploration-camera rig is `Assets/Prefabs/HD2D_CameraKit.prefab`.
Use that prefab unchanged in every exploration scene. Battle and cutscene
cameras remain separate systems.

## Locked composition

The project uses a perspective diorama camera rather than an orthographic
isometric camera. Every exploration scene shares these values:

| Piece | Locked setting |
|---|---|
| Main Camera | Perspective, FOV 36 |
| Camera rig | Pitch 40 degrees, yaw 0 degrees |
| CinemachineFollow | WorldSpace, offset `(0, 13, -12)` |
| Position damping | `(0.4, 0.4, 0.4)` |
| CinemachineConfiner2D | Disabled |
| Dynamic zoom | None |
| Motion blur | Disabled |

Do not override the lens, rig rotation, follow offset, or damping per scene.
Interiors, corridors, streets, and plazas use the same character scale and
composition.

## Retained HD-2D stack

- Global Volume: tonemapping, bloom, restrained vignette, and bokeh depth of
  field.
- `DepthOfFieldFocus`: tracks the true 3D camera-to-player distance so the
  player remains on the sharp focal plane.
- `CameraController`: resolves the Player tag and assigns the locked
  Cinemachine follow target without changing composition.
- `SpriteBillboard`: preserves the approved sprite presentation.
- Cinemachine impulse support: retains camera shake for authored events.
- Scene-specific `CameraOcclusionFader`: may define wall-name prefixes and
  probe radius without changing composition.

## Scene checklist

1. Place `HD2D_CameraKit.prefab` without camera-setting overrides.
2. Tag the player `Player` so the follow and focus systems self-heal references.
3. Keep boundary geometry solid and preserve required backdrop coverage.
4. Configure only scene-specific occlusion prefixes when needed.
5. Playtest all four movement directions and confirm the player remains fully
   visible.
6. Confirm depth of field keeps the player sharp and Unity reports no new
   warnings or errors.

## Legacy scenes

Duomo, Mercato Vecchio, and Ponte Vecchio contain unpacked camera rigs rather
than shared-prefab instances. Their camera values must match this document and
must not contain `DynamicZoom`.

## Editor notes

- `VolumeProfile.Add<T>()` in editor scripts does not persist by itself; add the
  override to the asset with `AssetDatabase.AddObjectToAsset`, then save.
- Script default changes do not update already-open scene state across a domain
  reload. Reload the scene before validation.
- Unfocused editor play mode may barely advance frames. Prefer testing with the
  Unity editor focused.

## Approved 2026-07-10

Design: `Docs/superpowers/specs/2026-07-10-uniform-hd2d-exploration-camera-design.md`.
