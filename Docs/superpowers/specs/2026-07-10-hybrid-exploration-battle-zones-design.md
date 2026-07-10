# Hybrid Exploration and Battle Zone Standard

## Summary

Inferno's Curse will treat most combat-capable locations as hybrid zones: the player explores the location using the shared locked HD-2D camera, and battles occur in place on the same geometry when an encounter begins. Terrain, architecture, props, lighting, time, and weather remain visually continuous. Only control mode, camera presentation, tactical overlays, and battle actors change.

`GiardinoDelleRose` is the pilot for this standard. Its existing zones-equal-battlemaps architecture will be completed with terrain presentation derived from the refined painterly battle-terrain technology. Once the reusable foundation is verified there, compatible natural and urban zones will be migrated in phases.

## Goals

- Make exploration and tactical combat two modes of the same authored zone.
- Preserve the exact visible environment when combat starts.
- Reuse the painterly material blending, macro variation, slope depth, elevation tint, and weather response established by `InfernosCurse/BattleTerrainSplat`.
- Give natural and urban zones appropriate terrain-presentation variants without duplicating the transition architecture.
- Ensure visible geometry and tactical rules agree on height, walkability, cover, concealment, and line of sight.
- Return safely to exploration after victory or defeat without reloading the zone.
- Make future combat-capable zones inherit a clear authoring and validation contract.

## Non-Goals

- Do not make every scene combat-capable.
- Do not enable ordinary encounters in safe hubs, social interiors, the world map, or other explicitly protected spaces.
- Do not replace dedicated road encounters, scripted arenas, or the standalone battle test harness.
- Do not convert stable Unity Terrain to a custom mesh only to gain vertex-color channels.
- Do not redesign existing layouts, navigation routes, encounter compositions, quests, or environmental dressing during migration.
- Do not re-enable corruption rendering as part of this work.

## Approved Direction

### Seamless in-place combat

A combat-capable zone remains one Unity scene and one physical environment. Exploration and battle share:

- Terrain and architecture.
- Props and collision.
- Lighting, shadows, post-processing, time, and weather.
- Elevation and obstacle placement.
- Persistent scene and quest state.

Combat startup changes only the active mode. The exploration controller and Cinemachine brain are suspended, a tactical camera frames the encounter, the zone's authored tactical data is stamped into the battle grid, and battle actors and UI are activated. The environment does not swap materials or load a replacement arena.

### Terrain presentation family

The refined battle-terrain work defines a presentation technology rather than a requirement that every surface use the exact same shader implementation.

- Generated mesh battle maps continue using `InfernosCurse/BattleTerrainSplat`, vertex-color material weights, and UV2 slope or fold shading.
- Unity Terrain zones use a terrain-compatible sibling that reads their existing terrain control maps and applies the same painterly macro variation, slope depth, warm and cool elevation treatment, and persistent-weather response.
- Urban combat zones receive a related stone, cobble, dirt, and wear blend designed for modular hard-surface environments.
- All variants share compatible palette, macro-variation, wetness, and readability controls through presentation profiles.

The ground must retain the same appearance across the exploration-to-battle transition.

## Zone Contract

Every combat-capable zone provides one authoritative authoring root containing or referencing:

- Grid bounds and cell size.
- Walkability and elevation data.
- Cover, concealment, obstacle, and line-of-sight data.
- Valid party and enemy spawn regions.
- A terrain-presentation profile.
- Encounter detection and battle-kit configuration.
- A battle camera anchor or rules for deriving one from the encounter location.
- Explicit exploration-only and battle-only object groups where needed.

Visible geometry remains the visual authority. Tactical data must be authored from or validated against that geometry so a visible wall cannot be tactically empty and an open path cannot be silently blocked.

Safe hubs and interiors reject ordinary encounters. A story event may override that protection only through an explicit scene-level combat authorization; adding enemies alone must not silently convert a safe scene into a battlefield.

## Transition Flow

1. An encounter begins at the player's current location.
2. The system validates the zone authoring root, battle kit, camera, grid, and usable spawn cells.
3. Exploration movement, interactions, exits, and Cinemachine control are suspended.
4. The current player location and relevant exploration state are captured.
5. The zone's authored tactical data is applied to the battle grid.
6. The tactical camera frames the local encounter area without exposing map boundaries.
7. Party members and enemies spawn on nearby valid cells.
8. Battle UI, grid highlights, fog, and tactical systems activate.
9. Terrain, lighting, weather, props, and architecture remain unchanged throughout combat.
10. Victory or defeat removes battle-only actors and systems, restores exploration state and camera control, and returns the player to the zone without a scene reload.

## Failure Handling

Encounter startup is transactional. Validation occurs before exploration is suspended whenever possible.

- Missing zone authoring: log an actionable error and do not start combat.
- Missing battle kit or tactical camera support: abort and leave exploration active.
- No valid spawn cells: abort and identify the rejected encounter location.
- Failure after suspension: tear down any partial battle objects, restore the Cinemachine brain, reactivate the exploration player, and re-enable interactions and exits.
- Unsupported legacy zone: retain its current behavior and report it for migration rather than applying a partial conversion.
- Missing terrain presentation profile: render the last valid material and block destructive rebuilds.

No failure path may leave the player hidden, frozen, without camera control, or trapped in an incomplete battle state.

## Rollout

### Phase 1: reusable foundation and Rose Garden pilot

- Formalize the shared hybrid-zone authoring and transition contract around the existing `ZoneEncounterTrigger` flow.
- Add the terrain-compatible painterly presentation variant.
- Apply it to `GiardinoDelleRose` while preserving its painted meadow, path, soil, and stone distribution.
- Verify exploration, in-place battle startup, battle completion, and exploration restoration in the same scene.

### Phase 2: natural outdoor zones

- Audit natural outdoor scenes for tactical-grid compatibility.
- Migrate Fiesole after adding authoritative grid, height, obstacle, and spawn authoring.
- Migrate other compatible natural zones individually after the pilot foundation passes.

### Phase 3: urban combat zones

- Add the urban stone, cobble, dirt, and wear presentation variant.
- Audit Ponte Vecchio, Mercato Vecchio, Duomo, Piazza della Signoria, and street scenes for encounter space, boundary framing, and tactical authoring.
- Migrate only zones that can support readable in-place combat without changing their exploration layout.

### Explicit exceptions

- Florentine Inn and similar safe social hubs.
- Salone delle Arti and protected interiors unless a story event explicitly enables combat.
- World map.
- Dedicated road-encounter boards.
- Scripted arenas and the standalone BattleArena test harness.

## Validation

Each migrated zone must pass all applicable checks:

### Exploration

- Locked HD-2D exploration camera follows correctly.
- Free movement, interactions, exits, and occlusion behavior remain functional.
- Terrain and surface presentation remain readable under the normal exploration camera.

### Transition

- Combat begins without a scene reload.
- Ground, lighting, props, time, and weather do not pop or reset.
- Exploration input and Cinemachine control are fully suspended before the tactical camera takes control.
- The tactical camera frames the encounter without showing invalid map edges.

### Tactical behavior

- Grid heights match visible surfaces.
- Walkability, obstacles, cover, concealment, and line of sight match visible geometry.
- Party and enemy spawns resolve to valid, reachable cells.
- Movement and targeting highlights remain readable over every surface material.
- A complete battle can progress through turns and resolve normally.

### Restoration and failure recovery

- Victory restores exploration control and scene state.
- Defeat follows its designed outcome and does not leave partial battle objects behind.
- Invalid startup data aborts safely without hiding or freezing the player.
- Repeated encounter transitions do not accumulate cameras, grids, event systems, units, or subscriptions.
- Unity reports zero new runtime errors or warnings attributable to the hybrid-zone system.

## Acceptance Criteria

The first implementation pass is complete when the reusable hybrid-zone foundation and terrain-compatible painterly presentation are active in `GiardinoDelleRose`; exploration and an in-place tactical battle use the same visible environment; victory and failure recovery both restore exploration safely; weather, time, and terrain appearance remain continuous; the zone's visible geometry agrees with its tactical data; and Unity runtime verification reports no new errors. Additional zone migrations begin only after this pilot passes.
