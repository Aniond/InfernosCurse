# Refined Battle Terrain Shader Standard

## Summary

Inferno's Curse will preserve the existing stylized battle-map diorama language and refine it into a reusable terrain-rendering standard. The approved direction is **Refined Painterly Diorama**: earthy color, restrained texture noise, soft material transitions, readable elevation, subtle atmospheric depth, and persistent-weather response.

The test plains map is the proving ground. After it passes visual and runtime validation, the same shader system will be applied to all existing generated 3D battle maps through biome-specific style profiles. Future battle maps will inherit the standard automatically from the builder.

The corruption system will remain in the project but will be disabled centrally. Corruption-driven vignette, red water, whispers, battle curse propagation, terrain tint, and curse glow must not affect normal play until the system is deliberately re-enabled for tuning.

## Goals

- Preserve the authored grass, dirt, rock, and path blending already present on 3D battle terrain.
- Replace the current saturated green presentation with an earthy olive, moss, ochre, and stone palette.
- Improve terrain depth without turning elevation bands into hard stripes.
- Reduce high-frequency visual noise so units, cover, paths, and movement highlights remain clear.
- Add broad painterly color variation that prevents texture repetition across large surfaces.
- Respond consistently to persistent time and weather, including rain wetness and nighttime lighting.
- Make the result reusable across existing and future generated battle maps.
- Preserve all gameplay-authoring data and tactical behavior.

## Non-Goals

- Do not replace the diorama terrain with Unity Terrain or a photorealistic PBR landscape system.
- Do not redesign battle layouts, grids, spawn positions, obstacles, cover, pathing, or elevation values.
- Do not combine tactical fog-of-war with corruption rendering.
- Do not remove corruption code or assets; the system must remain available for later tuning.
- Do not hand-author independent shader logic for every battle map.

## Approved Visual Direction

### Palette and material treatment

- Grass uses restrained olive and moss tones rather than neon green.
- Dirt uses warm, muted earth tones that remain distinct from paths.
- Rock uses neutral gray-brown tones with cooler shaded faces.
- Paths stay readable through hue and value contrast without becoming bright ribbons.
- Material boundaries remain soft enough to feel natural but sharp enough to communicate tactical terrain changes.

### Painterly depth

- Large-scale color variation breaks up tiling without introducing obvious procedural blotches.
- Exposed upper surfaces receive a subtle warm lift.
- Terrain folds, cliff bases, and recesses receive cooler, darker shading.
- Existing UV2 slope shading remains the authored foundation and is refined rather than discarded.
- Cliff faces retain triplanar texturing so vertical surfaces do not stretch.

### Readability

- Units must remain visually dominant over the ground.
- Movement and targeting highlights must remain clear over every material layer.
- Paths, cover edges, cliff faces, and elevation steps must be readable from the normal battle camera.
- Texture contrast and micro-detail must be restrained when they compete with combat information.

## Shared Architecture

### Battle terrain shader

`InfernosCurse/BattleTerrainSplat` remains the single shared shader for generated 3D battle terrain. It continues to consume the established authored channels:

- Vertex color red: grass weight.
- Vertex color green: dirt weight.
- Vertex color blue: rock weight.
- Vertex color alpha: path weight.
- UV2 X: authored slope and diorama-fold shading.

The refined shader adds shared controls for:

- Broad macro-color variation.
- Material transition softness.
- Slope and fold depth.
- Elevation-based warm/cool tinting.
- Persistent rain wetness.
- Restrained wet-surface highlights.
- Nighttime and low-light readability.

The shader must continue to use the active URP main light, shadows, and ambient lighting supplied by the world's time and weather systems.

### Biome style profiles

`BattleMapStyle3D` becomes the authoritative biome profile for terrain presentation. Each profile supplies:

- Grass, dirt, rock, and path textures.
- Material tint palette.
- Texture and macro-variation scale.
- Transition softness.
- Fold and recess shading strength.
- Elevation warmth and recess cooling.
- Wet tint and wet-highlight response.
- Water level where applicable.

Profiles contain presentation settings only. They do not own grid rules, obstacle placement, cover, pathing, spawns, or encounter data.

### Builder integration

`BattleMapMeshBuilder` remains responsible for producing terrain meshes and materials. It will copy all approved visual parameters from the selected `BattleMapStyle3D` profile into the generated material.

New maps receive the shared shader automatically. Existing generated maps are updated by rebuilding from their authored source and profile; production prefabs are not individually hand-patched unless a source map cannot be rebuilt safely.

### Weather flow

The terrain shader consumes the existing persistent weather state rather than creating a battle-only weather model.

1. `FlorenceWeather` remains the authority for current weather.
2. The shared weather-surface controller exposes the current global wetness.
3. Battle terrain reads the global wetness and applies profile-specific darkening and mild highlights.
4. Entering or leaving battle does not reset weather, wetness, or time of day.

Rain should darken soil, rock, and paths more strongly than grass. Wet highlights must remain subtle and must not make the board look plastic.

## Corruption Shutdown

Corruption is disabled through one centralized, reversible feature setting rather than by deleting components or scattering disabled checkboxes through scenes.

While disabled:

- `InsanityPresenter` creates no vignette, red-water tint, or whisper playback.
- Battle curse automata does not seed, spread, mutate tiles, or update world corruption.
- Curse overlays do not create sprites or feed terrain masks.
- `BattleTerrainCurse` contributes a zero mask.
- The terrain shader applies no corruption color or glow.
- Enemy AI must fail safely when curse influence is unavailable and continue using its other tactical inputs.

Tactical fog-of-war remains enabled and independent. Its visibility mask, unit concealment, weather visibility modifiers, and vision spells are outside the corruption shutdown.

## Rollout

### Phase 1: test plains map

- Refine the shared shader and plains biome profile.
- Rebuild `BattleMap_Plains3D` from its authored source.
- Compare the same camera framing before and after the refinement.
- Tune until terrain depth, paths, cliffs, grass, units, and highlights are simultaneously readable.

### Phase 2: existing maps

- Inventory all 3D battle-map prefabs and their style profiles.
- Add or update biome profiles as needed.
- Rebuild each map through the shared builder.
- Validate that every terrain renderer uses the shared shader and an approved profile.
- Preserve map-specific water and weather behavior.

### Phase 3: future-map standard

- Require a valid `BattleMapStyle3D` profile for every generated 3D map.
- Emit a clear editor error when required textures or profile data are missing.
- Keep safe profile defaults so incomplete authoring cannot produce magenta or invisible terrain.

## Failure Handling

- Missing shader: the builder reports an error and does not overwrite the last valid generated prefab.
- Missing required texture: the validator reports the profile and property name; the builder uses a deliberate neutral fallback only for preview generation.
- Missing style profile: generation stops with an actionable error identifying the source map.
- Unsupported legacy map: leave the prefab unchanged and report it for manual migration.
- Missing weather authority: terrain renders dry using its base profile.
- Disabled corruption system: all corruption consumers return neutral values and do not block battle startup or enemy AI.

## Validation

### Visual checks

- Capture the test plains map from the same battle camera before and after refinement.
- Confirm the palette is earthy and the texture does not overpower units.
- Confirm paths, cliffs, cover, grass, and elevation changes read immediately.
- Confirm the terrain retains its stylized diorama identity.
- Check clear daylight, nighttime, rain, and heavy rain.

### Tactical checks

- Movement and targeting highlights remain visible over every terrain material.
- Tactical fog reveals and conceals cells and enemies correctly.
- Grid dimensions, surface heights, spawns, obstacles, cover, paths, and line-of-sight behavior are unchanged.
- A complete battle can start, progress through turns, and end normally.

### Corruption checks

- No vignette is created.
- Water remains governed by its normal material and weather state.
- No curse overlay objects are created.
- Terrain curse-mask influence and glow remain zero.
- Curse automata does not mutate tiles or world state.
- Enemy AI still completes turns without curse inputs.

### Coverage checks

- Every existing generated 3D battle-map prefab uses `InfernosCurse/BattleTerrainSplat`.
- Every existing generated 3D battle map has a valid style profile.
- The test plains map and at least one additional rebuilt map pass Play mode testing.
- Unity reports zero errors after rebuild and runtime validation.

## Acceptance Criteria

The work is complete when the test plains map displays the approved Refined Painterly Diorama treatment, persistent weather affects the ground naturally, corruption is fully dormant, tactical fog remains functional, all existing 3D battle maps use the shared system, gameplay authoring is unchanged, and the verified runtime produces no Unity errors.
