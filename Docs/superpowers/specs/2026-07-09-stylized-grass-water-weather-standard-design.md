# Stylized Grass, Water, and Weather Surface Standard

## Objective

Make Stylized Grass and Stylized Water 3 the project standards everywhere grass or water is visibly represented, convert every existing applicable exploration and battle scene, and make those surfaces respond consistently to the persistent `FlorenceWeather`/COZY state.

The standard must cover authored scenes, generated battle maps, reusable prefabs, and future scene builders. It must not create a second clock, weather simulation, or rain state.

## Approved Direction

Use a project-owned shared weather-surface layer on top of the licensed Stylized Grass and Stylized Water 3 packages.

The project layer owns configuration, weather-state adaptation, pooled rain effects, conversion tools, and validation. The Asset Store package source remains unmodified so upgrades cannot overwrite project behavior.

A scene-by-scene material swap was rejected because rebuilding a scene could restore legacy materials and future maps could drift from the standard. Editing the package source was rejected because it would make upgrades fragile and mix game-specific behavior into vendor code.

## Current Coverage Baseline

The July 9 audit found:

- `GiardinoDelleRose` is the only authored scene currently using a Stylized Water 3 surface and `WaterObject`.
- `FlorentineInnFloor1`, `MercatoVecchio`, and `PonteVecchio` still contain custom or legacy water materials.
- No authored scene currently renders grass with the Stylized Grass shader.
- `Fiesole` presents a grass-colored meadow mesh without the Stylized Grass system.
- `GiardinoDelleRose` uses painted terrain layers for meadow and dry grass but has no Stylized Grass detail coverage.
- Existing authored outdoor water surfaces have no weather-driven rain splash or ripple emitters.
- Generated battle maps can use grass textures and optional water planes, but their builders do not currently apply the shared packages or weather effects.

The conversion pass must repeat this audit after implementation rather than assuming that file names alone identify every surface.

## Shared Runtime Architecture

### Weather Surface Controller

A single project-owned `WeatherSurfaceController` reads the current persistent weather presentation state and drives all registered surfaces. It must:

- Read `FlorenceWeather.CurrentProfileName` and the existing COZY wind state.
- React to weather changes without requiring a scene reload.
- Map clear, cloudy, fog, light rain, heavy rain, and storm profiles to normalized wetness, wind response, and precipitation intensity.
- Ease wetness and effect intensity in and out so transitions do not visibly pop.
- Continue reading the persistent profile in battle scenes even when general COZY scene effects are suspended.
- Fall back to clear weather when opened outside the normal bootstrap and issue no repeated warning spam.

Weather classification should be shared with the existing window-environment presentation where practical so windows, grass, and water cannot interpret the same profile differently. This layer is read-only with respect to world weather.

### Explicit Surface Registration

Applicable surfaces receive a project-owned marker/configuration component rather than relying on runtime name searches. Each registration declares:

- Surface family: grass or water.
- Surface form: terrain detail, grass patch, river, pond, fountain, or shallow battle water.
- Exposure: outdoor, sheltered, or indoor.
- Renderer, terrain, or bounded emission region.
- Optional density, scale, flow, and weather-response overrides.

Outdoor surfaces receive persistent weather. Sheltered surfaces may receive reduced weather. Indoor surfaces receive no rain impacts. Decorative fountain flow and fountain-generated splashes remain independent of rain exposure.

### Asset Organization

Project-controlled assets live under:

- `Assets/Art/Environment/WeatherSurfaces/Grass`
- `Assets/Art/Environment/WeatherSurfaces/Water`
- `Assets/Art/Environment/WeatherSurfaces/VFX`
- `Assets/Prefabs/Environment/WeatherSurfaces`
- `Assets/Scripts/Environment/WeatherSurfaces`

Vendor materials and textures may be referenced or duplicated into configured project variants as permitted, but project scenes and builders must reference the project-controlled variants rather than demo-scene assets.

## Stylized Grass Standard

Grass-colored ground remains as the soil/base layer. The Stylized Grass shader is added through actual blade or clump geometry so grass reads as vegetation rather than green paint.

### Terrain Zones

Terrain-based zones use a deterministic Stylized Grass detail prototype or equivalent authored patch system:

- Meadow and dry-grass masks determine placement and density.
- Paths, stone, water, walls, doors, and important traversal edges remain clear.
- Dry and healthy variants share the same weather response but retain distinct base colors.
- Detail distances and density scale with the existing quality settings.
- Grass renderers have no gameplay colliders.

### Mesh and Authored Zones

Mesh-based zones use reusable patch prefabs with the Stylized Grass material. Placement remains deterministic so scene rebuilds produce the same result.

### Battle Maps

`BattleMapMeshBuilder` keeps its readable terrain splat as the base and adds restrained Stylized Grass patches over cells designated as grassy. Patches must:

- Avoid water, paths, rocks, obstacles, spawn points, and unwalkable cells.
- Remain short enough that units, grid highlights, and tactical elevation stay readable.
- Use deterministic placement based on map data.
- Carry no colliders and have no effect on movement or line of sight.

### Weather Response

The package `WeatherShadingController` provides shader wetness, driven by the shared project controller. The package `WindController` uses COZY wind integration when available, with a project fallback to the active wind values when required.

Grass response by state:

- Clear/cloudy/fog: dry or slowly recovering wetness, normal wind response.
- Light rain: gradual darkening and wetness with restrained motion.
- Heavy rain: stronger wetness and gust response.
- Storm: maximum wetness with stronger, clamped gust movement.

Sparse close-range rain impacts may be shown over exposed grass, but water splashes remain the visually dominant precipitation response. Grass impacts must not become a field of distracting particles.

## Stylized Water Standard

All visible water surfaces use Stylized Water 3 and carry a valid `WaterObject` or the appropriate package component.

Project material variants cover:

- Arno river water with directional flow and broader wave scale.
- Pond and garden water with calmer waves and clearer depth.
- Fountain basin water with small-scale ripples.
- Shallow battle water with restrained transparency and tactical readability.

Legacy scrolling or custom water behavior is retired on converted surfaces when the Stylized Water material already provides the equivalent motion. Boat alignment and other water-aware objects continue to work through the package height/position facilities or an explicit stable-water fallback where wave sampling is unnecessary.

## Rain Splash and Ripple System

Rain impacts use a shared pooled effect system built from the existing Stylized Water splash/ripple textures and materials. It must not instantiate and destroy particles continuously.

Each exposed water surface supplies an emission region. The pool emits impacts at randomized valid points within visible surface bounds, with camera-distance and visibility culling.

Response by state:

- Clear, cloudy, and fog: no rain impacts.
- Light rain: sparse small ripples.
- Heavy rain: denser ripples with occasional upward splashes.
- Storm: dense but globally capped ripples and splashes.

Emission density scales by visible surface area but is constrained by a global budget. The Arno must not generate an unbounded number of particles simply because its surface is large. Fountain-generated flow, foam, and basin impacts continue even when the fountain is indoors; only weather-generated impacts honor the exposure setting.

## Existing Scene Conversion

The first standardization pass covers all applicable existing content, including:

- `Fiesole`: replace the grass-painted meadow presentation with the shared base-plus-Stylized-Grass treatment.
- `GiardinoDelleRose`: retain the terrain color layers, add Stylized Grass coverage, standardize pond and fountain water, and add exposed rain impacts.
- `PonteVecchio`: convert the Arno and any other visible water surfaces to the river standard and add bounded rain impacts.
- `MercatoVecchio`: convert river/fountain water surfaces to the correct variants and add exposure-aware rain impacts.
- `FlorentineInnFloor1`: convert the courtyard fountain basin to the fountain-water standard while keeping it sheltered from outdoor rain.
- Existing generated 3D battle-map prefabs and styles: apply standard grass patches and water components wherever their map data declares grass or water.

Scenes without visible grass or water receive no placeholder surface solely to participate in the system. Any apparent surface that cannot be converted safely must be recorded by the validator with a concrete skip reason.

## Builder and Future-Zone Standard

Existing builders become authoritative users of a shared editor factory or installation utility. Rebuilding a converted scene must reproduce the standard materials, components, exposure settings, and deterministic grass placement.

Future zone rules are:

- Water must be created through the shared water factory and assigned a river, pond, fountain, or battle-shallow profile.
- Grass must use a shared terrain-detail or patch profile in addition to its base ground layer.
- Every surface must declare exposure.
- Rain effects come from the shared pool, never a new scene-local weather detector.
- Builders must never reference vendor demo-scene objects directly.

An editor validation command scans build scenes, environment prefabs, battle-map prefabs, and builder outputs. It reports:

- Water-like renderers that do not use Stylized Water 3.
- Stylized Water renderers missing the required water and project registration components.
- Grass-designated terrain or mesh areas without Stylized Grass coverage.
- Registered outdoor water missing a valid rain-impact region.
- Indoor surfaces incorrectly configured to receive rain.
- Builder references that would restore legacy materials.

## Compatibility and Performance

- Package source under `Assets/Stylized Water 3` and `Packages/xyz.staggart-creations.stylized-grass` remains unedited.
- Weather state remains owned by `FlorenceWeather` and COZY.
- Material changes must coexist with `InsanityPresenter`; weather response uses global shader values, material property blocks, or companion effects instead of replacing its runtime material instances.
- Pooled precipitation performs no steady-state instantiate/destroy loop and avoids per-frame garbage allocation.
- Offscreen and distant surfaces reduce or disable impact emission.
- Grass density, draw distance, and water effect budgets remain tunable for quality levels.
- Surface effects remain cosmetic and cannot alter combat occupancy, navigation, cover, line of sight, or physics.

## Validation and Acceptance

The pass is accepted when:

- Every existing visible grass area is either rendered with the Stylized Grass standard or has a documented, approved skip reason.
- Every existing visible water surface uses a project Stylized Water 3 variant and the proper package component.
- Fiesole and Giardino show actual grass vegetation rather than only grass-colored ground.
- Ponte Vecchio and Mercato Vecchio show coherent Arno flow with rain ripples during precipitation.
- Giardino's outdoor pond/fountain receive rain impacts; the inn's indoor/sheltered fountain does not receive weather rain.
- Light rain, heavy rain, storm, clear, cloudy, and fog transitions update surfaces without reloading the scene.
- Grass wetness and wind visibly respond to weather without excessive bending or particle clutter.
- Generated battle maps inherit the persistent active weather and retain tactical readability.
- Clear weather produces no stale precipitation effects after the transition completes.
- Curse/insanity presentation remains visible and does not permanently replace standard materials.
- Rebuilding each converted scene twice produces equivalent surface configuration and deterministic grass placement.
- The editor validator reports zero unexplained legacy or unregistered grass/water surfaces.
- Unity reports zero console errors after scene conversion, rebuild checks, and runtime weather tests.

Runtime verification includes at least Fiesole, GiardinoDelleRose, PonteVecchio or MercatoVecchio, FlorentineInnFloor1, and one grassy/water-capable 3D battle map. Each applicable scene is exercised under clear, light-rain, and heavy-rain or storm profiles.

## Delivery Boundary

This pass standardizes visible grass, water, their weather shading, and rain impacts. It does not add flooding simulation, mud gameplay, wet footprints, swimming, buoyancy, new weather profiles, new map layouts, vegetation harvesting, or changes to tactical terrain rules.
