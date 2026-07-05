# Giardino delle Rose — Zone Design

**Status:** Approved 2026-07-05. Nothing built yet — this spec precedes implementation.

## Purpose

A hillside rose garden overlooking Florence. It is the first zone built on the new
Terrain-based art pipeline (Stylized Water 3, Stylized Grass Shader, Vegetation
Spawner, Procedural Terrain Painter) rather than the hand-built flat-plane approach
used for Duomo, Mercato Vecchio, and Ponte Vecchio. It is also the path to unlocking
the Florist job — quest-gated, not freely walkable from the start.

This spec covers **only the zone**: its map placement, layout, scene structure, and
a placeholder unlock hook. The Florist job itself (skill tree, advanced tier) is
explicitly out of scope — see "Deferred" below and [[job_system_design]].

## Map placement

- **`NodeKind.District`**, **`MapLevel.City`** — sits alongside Duomo, Mercato
  Vecchio, and Ponte Vecchio on the City map sheet. Not a separate Region trip like
  Fiesole.
- Position: south-east of the city center, hillside above Piazza della Signoria,
  matching the real Giardino delle Rose's siting above the Arno.
- **Neighbors:** `signoria` (city approach) and `oltrarno` (riverside approach) —
  two natural approach routes onto the hillside.

## Unlock flow (placeholder — scoped to this zone only)

1. A quest NPC in an existing city district hands out a quest.
2. Completing/accepting the quest sets a flag that unlocks the `giardino_rose` pin
   on the Gugol Mappe (in addition to the scene existing in Build Settings, per
   the existing `MapRouting.IsUnlocked` pattern).
3. Player travels to the garden and completes the quest objective **inside** it.
4. Completion unlocks the Florist NPC (present in the scene from the start, but
   inactive/non-interactable until the flag is set).

The Florist job grant itself, the quest's actual objective, and the quest-giver
NPC's identity are **not designed here** — placeholder flag names and an inert
NPC object are enough to make the zone testable now. Follows the "one static NPC
per job" pattern from [[job_system_design]].

## HubNode data

| Field | Value | Notes |
|---|---|---|
| `id` | `giardino_rose` | stable id, never renamed |
| `displayName` | Giardino delle Rose | Italian place name (per [[ui_language_rule]]) |
| `kind` | `NodeKind.District` | |
| `mapLevel` | `MapLevel.City` | |
| `neighborIds` | `signoria`, `oltrarno` | |
| `sceneName` | `GiardinoDelleRose` | adding to Build Settings flips the pin live |
| `microClimate` | `MicroClimate.Hilltop` (reused) | no dedicated "Garden" value yet — defer a new enum entry until a second garden-type zone exists |
| `curseLevel` / `sanctity` | low / high (placeholder numbers) | gardens read as gentle, low-corruption spaces; exact tuning TBD in a follow-up balance pass |

## Zone layout

Three stepped terraces rising from the city-facing entrance to the Florist's
overlook:

- **Lower terrace (entrance):** gate/zone-entry point, rose beds flanking the path.
- **Mid terrace:** fountain centerpiece (Stylized Water 3), rose bed terraces on
  both sides framing the path upward.
- **Upper terrace:** Florist NPC + stall (inert until quest-complete), a city
  overlook viewpoint. Grass/vegetation density increases toward this tier.

A single path spine connects all three terraces, matching the FFT/HD-2D camera's
need for a legible, mostly-linear walk path (per the existing camera-containment
convention).

## Scene build order

Following the `FiesoleSceneBuilder` deterministic-editor-script convention — one
idempotent menu item — adapted for Terrain instead of primitive planes:

1. **Terrain object + heightmap** — three stepped terraces. Procedural Terrain
   Painter drives grass/dirt/path texturing by height and slope (no manual
   painting).
2. **Vegetation Spawner pass** — rule-based rose bed placement + background
   trees, slope/height-gated so beds land on the flat terrace tops, not the
   slopes between them.
3. **Stylized Grass Shader** — applied to the lawn terrain layer, wind-sway on.
4. **Fountain + water feature** — Stylized Water 3, mid-terrace centerpiece.
5. **Hero props via 3D AI Studio** — fountain basin details, garden furniture,
   pergola, Florist's stall. Goes through the existing Gemini-concept →
   David-approval → Prism 3.1 pipeline (`ASSET_PIPELINE.md`), same as Duomo's
   statues/paintings. Terrain, grass, and mass vegetation do **not** go through
   this pipeline — only hero/one-off props.
6. **Boundaries + camera kit** — Terrain collider + invisible walls at tier
   edges; `HD2D_CameraKit.prefab`; **clearance-mode `DynamicZoom`** (the
   heightmap variation needs the 8-raycast clearance checks, not the flat
   radial mode used by Mercato's plaza).
7. **Entry/exit + Florist NPC placeholder** — `ZoneEntryPoint` at the gate,
   `ZoneExit` (`ToWorldMap`) back to Gugol Mappe; Florist NPC object present
   but gated inactive until the quest-complete flag is set.
8. **Register scene + node** — add to Build Settings, wire `HubNodeData` via
   `GugolMappeSetup` (`AddTeaserNode`, following the exact idiom Fiesole used).
   This is the moment the pin goes live on the map.

## Gotchas carried forward from existing conventions

- **`GuildInteractionSpawner` sceneName trap:** if any guild/interaction zone
  entries are added for this scene, `sceneName` on the spawner entry must
  **exactly** equal the real scene name (`GiardinoDelleRose`). Do NOT append a
  disable-reason suffix to `sceneName` as an off-switch (the mistake found in
  the Duomo Church zones) — use a real enabled/disabled flag or omit the entry
  instead.
- **Camera clearance mode:** Fiesole/Ponte Vecchio use clearance-mode
  `DynamicZoom` (8 raycasts) rather than Mercato's radial mode — this zone's
  heightmap terrain makes clearance mode the only sensible choice; confirm
  `minArchitectureHeight` tuning once real props are in.
- **No per-district ScriptableObject exists** — district data is authored
  inline as `HubNodeData` on `GameSystems.prefab` via `GugolMappeSetup`, not a
  standalone asset. Follow that pattern, don't invent a new one.

## Deferred (explicitly out of scope this pass)

- Florist job design (skill tree, advanced tier, JobDefinition asset).
- The actual quest content/objective inside the garden.
- The quest-giver NPC's identity and placement.
- A dedicated `MicroClimate.Garden` enum value (reuse `Hilltop` until a second
  garden-type zone justifies a new value).
- Exact curse/sanctity numeric tuning.

## Related memory

[[gugol_mappe]], [[hd2d_scene_setup]], [[job_system_design]], [[asset_gen_workbench]],
[[asset_gen_approval_rule]], [[ui_language_rule]], [[duomo_scene]] (guild-zone
sceneName gotcha)
