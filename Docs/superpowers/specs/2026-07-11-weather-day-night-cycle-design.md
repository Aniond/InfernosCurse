# Weather and Day/Night Cycle Design

**Date:** 2026-07-11
**Status:** Approved design, awaiting written-spec review
**Project:** Inferno's Curse — Unity 6.4 / URP

## Purpose

Build a continuous, Palworld-style world clock and realistic weather simulation for exploration while preserving the project's deterministic narrative state, locked HD-2D presentation, tactical battles, seamless interiors, and save guarantees.

The player should experience time as a natural part of Florence rather than a countdown. Sunsets should happen during exploration, night should expose different activity without overstaying its welcome, and weather should arrive as a believable front rather than a random visual toggle.

Palworld is the pacing reference because it advances time continuously and exposes independent day and night speed controls. The implementation remains an original system designed for this project's RPG structure.

## Approved Architecture

The system uses an authoritative project-owned environment simulation over COZY.

1. `WorldEnvironmentState` owns logical time phase, forecast state, weather category, intensity, temperature, wind, visibility, wetness, and flood risk.
2. `WorldClock` advances continuously during exploration and exposes separately tunable day and night rates.
3. `WorldForecast` deterministically generates realistic fronts from campaign seed, Florentine date, season, and climate data.
4. District microclimates modify the city-wide front without creating unrelated local weather.
5. `CozyEnvironmentPresenter` translates typed project state into COZY sun, moon, sky, cloud, fog, precipitation, wind, and lightning presentation.
6. Gameplay consumers read typed project state. They never parse COZY profile names.
7. Battle presentation receives a snapshot and an optional battle-local weather transition without rewriting the world forecast.

COZY remains the visual authority for the outdoor sky and directional celestial lights. It does not own gameplay state, generate the canonical forecast, or serialize runtime forecast data into project assets.

## Continuous Time

### Default pacing

- One default full cycle lasts approximately 40 real minutes.
- Dawn: 05:00–07:00.
- Day: 07:00–18:00.
- Dusk: 18:00–20:00.
- Night: 20:00–05:00.
- Night advances at approximately 1.75 times the daytime rate.
- All phase boundaries and rates live in a designer-authored profile and are not hard-coded into consumers.

The target duration is a default tuning value, not a save invariant. Changing the profile changes future clock progression without invalidating saved absolute time.

### Clock behavior

The clock advances during:

- ordinary exploration;
- ordinary NPC conversation;
- nonblocking environmental interactions.

The clock pauses during:

- tactical battles;
- authored cutscenes;
- pause and inventory interfaces;
- Gugol Mappe;
- any modal interface explicitly marked as world-pausing.

Travel advances the clock by authored route duration. Rest advances the date by one day and sets time to 07:00. Date rollover remains historically Florentine and continues to use the project's compressed calendar.

Every backward time assignment must resynchronize the calendar wrap detector. Forward travel across midnight must advance the calendar naturally or explicitly account for every crossed day.

## Realistic Weather Fronts

### Forecast structure

Each day contains two to four forecast periods. A period specifies:

- start and end time;
- typed weather category;
- precipitation intensity;
- cloud coverage;
- fog density;
- wind strength;
- temperature;
- transition duration.

The forecast is a pure function of campaign seed, year, day of year, seasonal climate data, and permitted authored world-event biases. Saving and loading the same state reproduces the same front sequence.

Transitions blend over roughly 10–30 in-game minutes, with slower transitions for large fronts and fog burn-off. The renderer must not snap between conditions.

### Seasonal behavior

The existing Florence climate data remains the baseline. Temperature and monthly probability determine whether precipitation manifests as rain, sleet, snow, or hail. Autumn rain spells can persist across multiple days, strengthen into heavy rain or storms, and raise Arno flood risk.

Morning valley fog burns off gradually when temperature, wind, and cloud conditions allow it. It is not replaced by an unconditional fixed-hour toggle.

### District microclimates

All districts receive the same city-wide front, modified by authored microclimate:

- **Riverside:** thicker radiation fog, heavier rain response, stronger flood presentation.
- **Open piazza:** stronger wind and fully exposed precipitation.
- **Sheltered streets:** reduced wind and precipitation under authored cover.
- **Hilltop:** reduced valley fog, stronger wind, and earlier clearing.

Crossing districts blends toward the local variant. It does not reroll the day or abruptly replace the front.

### Persistent surface response

Wetness accumulates from precipitation and dries gradually according to temperature, wind, exposure, and material response. Stone, soil, grass, rivers, ponds, fountains, and battle water receive appropriate typed response. Indoor surfaces remain dry unless an authored leak or open courtyard explicitly exposes them.

Puddles, gloss, rain impacts, ripples, grass wetness, and river response share global budgets and avoid duplicate precipitation emitters.

## HD-2D Visual Direction

The locked exploration camera remains unchanged. Environment presentation supports the character and navigation silhouette at every phase.

- **Dawn:** low warm sun, long soft shadows, river mist, and cool ambient fill.
- **Day:** clean natural contrast, readable façades, restrained bloom, and restrained atmospheric haze.
- **Dusk:** amber rim light, deepening blue shadow, and lamps awakening in a controlled sequence.
- **Night:** moonlit blue ambient, warm windows, localized pools of safety, and deeper distance contrast.

Cloud cover softens shadows. Rain darkens stone and increases controlled specular response. Storms reduce exposure without making navigation unreadable. Fog reduces depth contrast instead of applying a flat white wash. Lightning is sourced from one authoritative event and reaches all loaded window and interior listeners in the same frame.

A subtle camera-relative character readability light may protect sprite readability, but it must not flatten world lighting or behave as an obvious spotlight.

### Interior behavior

Seamless interiors use local volumes, local lights, window spill, and authored exterior openings. They do not write global `RenderSettings` or own another celestial light. Walls and roofs must visibly block outdoor contribution. Courtyards and open windows continue to display world weather.

Standalone interior scenes may use a dedicated global ambient guard only while they are the active scene. Embedded modules must not carry that guard.

## Gameplay Contract

NPCs, shops, crowds, lamps, encounters, ambient audio, and secrets consume shared time phase and weather state. Severe weather can send civilians toward shelter, reduce crowds, alter sight, or enable authored activity. Night can change schedules, encounters, access, and rumors.

Weather does not directly change Circle corruption or player sanity. An authored world event may react to weather, but world corruption and player sanity remain isolated systems.

### Battle behavior

Entering tactical battle pauses the world clock and captures the world environment state. Battle lighting and visibility begin from that snapshot.

The battle director may transition to a battle-local weather state for drama. That transition can affect battle sight, VFX, audio, and terrain response, but it does not mutate Florence's deterministic forecast. Returning to exploration restores the exact paused world time and its expected forecast period.

## Persistence

Save data records:

- exact absolute time and Florentine date;
- campaign forecast seed or identity;
- active forecast period and transition progress;
- current district;
- accumulated wetness state;
- active flood-risk state;
- battle-local weather only when a future battle-save design explicitly permits it.

Loading applies date, time, district, forecast, and accumulated presentation state before player control returns. A load must reproduce the same sky, lighting phase, weather category, wetness, and gameplay modifiers.

Missing COZY presentation falls back to clear daylight without invalidating logical environment state. Each missing-authority condition logs at most one actionable warning per system instance or scene.

## Migration of Existing Systems

The implementation consolidates current consumers rather than running a second environment stack.

- `GameClock` becomes or delegates to the continuous world-clock service.
- `FlorenceWeather` becomes or delegates to deterministic `WorldForecast` and typed environment state.
- `WorldWindowEnvironment`, `WeatherSurfaceController`, `WeatherVision`, `LightShaft`, map weather, NPC schedules, and torch behavior consume typed state.
- `BattleWeatherDirector` writes only battle-local weather while COZY is suspended.
- `CozySceneAdapter` remains responsible for celestial-light ownership and scene presentation suspension, not logical time or weather ownership.
- Generated COZY `currentForecast` data is removed from the persistent `GameSystems` prefab and prevented from dirtying it again.
- Existing serialized fields and save data receive migration defaults so old saves load safely.

## Validation and Completion Gates

### Deterministic and clock tests

- Identical campaign seed and date reproduce identical forecast periods, district variants, and flood risk.
- Different permitted seeds produce valid but different sequences.
- Midnight rollover, faster night rate, pause nesting, travel, rest, battle entry/exit, and save restoration behave correctly.
- Backward time assignment cannot create a duplicate day rollover.

### Classification tests

Every installed weather profile maps through the typed adapter, including clear, partly cloudy, overcast, wind, fog, haze, mist, drizzle, light precipitation, light rain, heavy rain, thunderstorm, hail, sleet, light snow, and any supported package aliases.

No gameplay or presentation consumer may classify weather by substring after migration.

### Scene and runtime matrix

Capture and inspect dawn, noon, dusk, and night under clear, cloudy, fog, rain, and storm conditions in:

- Mercato Vecchio;
- the seamless Florentine inn;
- Giardino delle Rose;
- one major standalone interior;
- BattleArena.

The matrix verifies:

- one enabled celestial directional-light authority;
- smooth lighting and weather transitions;
- synchronized windows, lamps, lightning, audio, and surfaces;
- no indoor precipitation or global sky flooding through walls;
- readable player and navigation silhouettes;
- correct pause and resume behavior;
- battle-local weather isolation;
- exact save/load reproduction.

### Surface validation

The weather-surface validator must distinguish actual water from structural objects whose names contain `river` or `water`. Every true surface is either registered with correct exposure and response or documented as an intentional visual-only submesh. Mercato Vecchio and the inn must finish with zero unexplained findings.

### Source-control cleanliness

Entering and leaving play mode cannot dirty the persistent prefab through generated COZY forecast state. Final verification includes:

- clean Unity compilation;
- passing environment validators;
- passing automated tests;
- runtime matrix evidence;
- save/load evidence;
- a final dirty-tree audit that distinguishes this work from pre-existing user changes.

## Out of Scope

- Replacing COZY with a custom sky renderer.
- Changing the approved locked exploration camera.
- Merging weather with Circle corruption or player sanity.
- Building individual quests, NPC schedules, or weather events beyond the shared hooks required by this system.
- Reauthoring unrelated scene geometry merely to participate in weather presentation.
