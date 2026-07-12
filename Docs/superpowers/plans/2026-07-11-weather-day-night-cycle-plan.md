# Weather and Day/Night Cycle Implementation Plan

**Date:** 2026-07-11
**Design:** `Docs/superpowers/specs/2026-07-11-weather-day-night-cycle-design.md`
**Target:** Unity 6.4, URP, COZY 3

## Goal

Deliver a project-owned continuous clock and deterministic realistic weather-front simulation, rendered by COZY and consumed consistently by exploration, interiors, maps, tactical battles, surfaces, NPC schedules, and saving.

The work is complete only after the full runtime matrix and dirty-tree audit in the approved design pass.

## Guardrails

- Preserve COZY as the outdoor sky, celestial light, cloud, precipitation, wind, fog, and lightning renderer.
- Do not introduce another runtime `RenderSettings` owner.
- Do not change the locked exploration camera.
- Do not connect weather directly to Circle corruption or player sanity.
- Keep existing saves readable and retain legacy serialized fields until migration is proven.
- Do not edit package source.
- Do not stage unrelated existing worktree changes.
- Scene and prefab changes must be deterministic and rerunnable through editor tooling.

## Phase 1 — Typed Environment Contract

### Task 1.1: Define project-owned environment types

Create:

- `Assets/Scripts/Environment/WorldEnvironmentTypes.cs`
- `Assets/Scripts/Environment/WorldEnvironmentProfile.cs`
- matching `.meta` files through Unity import

Define:

- `WorldTimePhase`: Dawn, Day, Dusk, Night.
- `WorldWeatherKind`: Clear, PartlyCloudy, Cloudy, Wind, Fog, Drizzle, Rain, HeavyRain, Storm, Hail, Sleet, Snow.
- `WorldWeatherState`: typed category plus precipitation, cloud, fog, wind, temperature, visibility, wetness target, flood risk, transition progress, and source profile name.
- `WorldEnvironmentSnapshot`: date key, hour, phase, district, weather, and accumulated wetness.
- `WorldEnvironmentProfile`: cycle duration, night multiplier, phase boundaries, transition limits, and fallback values.

The types must be serializable, clamp invalid values, and provide no COZY dependency.

### Task 1.2: Centralize weather aliases

Create `Assets/Scripts/Environment/WorldWeatherClassifier.cs`.

Map every currently installed COZY profile and legacy save value into `WorldWeatherKind`. Expose canonical COZY profile names for presentation. Unknown aliases return the clear fallback and one diagnostic result rather than silently inventing behavior.

Remove duplicated substring semantics from consumers only after compatibility tests cover every current alias.

### Task 1.3: Add deterministic editor validation

Create `Assets/Editor/WorldEnvironmentContractValidator.cs` with menu entry:

`InfernosCurse/Environment/World Environment/1. Validate Contract`

The validator must:

- enumerate installed COZY profiles;
- verify every profile maps to a typed category;
- verify all scalar ranges and phase boundaries;
- verify classification aliases expected by legacy saves;
- exit through a single PASS or actionable failure summary.

Verification:

- Unity compilation has zero C# errors.
- Contract validator passes.
- No scene or prefab changes occur in Phase 1.

## Phase 2 — Continuous World Clock

### Task 2.1: Implement the persistent clock director

Create `Assets/Scripts/Environment/WorldEnvironmentDirector.cs` and add it to `Assets/Resources/GameSystems.prefab` through the builder in Phase 5.

Responsibilities:

- advance absolute world time during unpaused exploration;
- derive phase from the shared profile;
- calculate a base clock rate that produces the configured full-cycle duration when the night multiplier is applied;
- accelerate only the Night phase;
- pause through an explicit nesting-safe pause-reason set and through blocked scenes;
- mirror its hour into the COZY time module without allowing COZY to advance a second clock;
- publish phase/hour changes without per-consumer polling requirements;
- remain valid when COZY is absent.

Use unscaled time only when the world is not explicitly paused. Do not let `Time.timeScale` alone define all pause behavior because battles run at normal scale.

### Task 2.2: Preserve the GameClock facade

Modify `Assets/Scripts/Environment/GameClock.cs`.

- Delegate `Hour`, `SetHour`, and debug description to `WorldEnvironmentDirector` when present.
- Retain the COZY fallback for isolated test scenes and migration safety.
- Add explicit pause/resume APIs using stable reason tokens.
- Keep the current backward-jump contract with `GameCalendar.ResyncClock()`.

### Task 2.3: Integrate scene and UI pause behavior

Modify:

- `Assets/Scripts/Environment/CozySceneAdapter.cs`
- modal UI owners that currently set `Time.timeScale`
- battle bootstrap/teardown where needed

Rules:

- exploration and ordinary dialogue continue;
- Battle and BattleArena pause world time;
- cutscenes, inventory, pause UI, and Gugol Mappe acquire/release named pause reasons;
- nested pause sources cannot resume a clock still paused by another source;
- scene transitions cannot leak pause tokens.

### Task 2.4: Validate clock behavior

Extend `WorldEnvironmentContractValidator` or create `WorldClockValidator.cs` to simulate:

- one complete 40-minute profile cycle without waiting in real time;
- phase boundaries and 1.75x night rate;
- midnight and Florentine date rollover;
- forward travel across one and multiple midnights;
- rest to next-day 07:00;
- backward restore plus calendar resynchronization;
- nested pause acquisition/release;
- battle entry/exit.

Verification:

- deterministic simulations report exact expected elapsed game time;
- existing travel, rest, and save paths compile and retain their date invariants;
- a short play-mode probe confirms the live hour advances only in permitted states.

## Phase 3 — Deterministic Realistic Weather Fronts

### Task 3.1: Extract reusable Florence climate data

Create `Assets/Scripts/Environment/FlorenceClimateData.cs` and migrate parsing from `FlorenceWeather` without changing `Assets/Data/Weather/FlorenceClimate.json` probabilities unless validation exposes an error.

Expose monthly temperature and condition probabilities plus the existing autumn rain-spell rules.

### Task 3.2: Implement pure front generation

Create `Assets/Scripts/Environment/WorldForecast.cs`.

Generate two to four periods per day as a pure function of:

- campaign seed;
- Florentine year and day of year;
- monthly climate row;
- prior-day spell state;
- allowed authored weather bias.

Each period includes start/end hour, typed weather state, and transition duration. Generation rules must produce plausible sequences such as fog to cloud to clear, cloud to rain to clearing, or rain to storm to drizzle. Disallow impossible discontinuities such as clear directly to heavy snow in a warm month.

Keep autumn multi-day rain and flood-risk behavior deterministic across multi-day travel.

### Task 3.3: Convert FlorenceWeather into the forecast service

Modify `Assets/Scripts/Environment/FlorenceWeather.cs`.

- Preserve its singleton and legacy `CurrentProfileName` compatibility surface.
- Replace one-condition-per-day application with active-period evaluation.
- Publish typed world weather to the director.
- Derive district variants from the active city-wide period.
- Blend district changes rather than rerolling them.
- Replace fixed-hour fog burn-off with a forecast transition governed by temperature, wind, and cloud state.
- Keep `ConditionForNode` and `ComputeFloodRisk` deterministic for unloaded map and world-simulation callers.
- Make legacy `Apply(string)` route to a controlled override API rather than directly owning COZY.

### Task 3.4: Implement wetness persistence

The director owns accumulated global wetness and updates it from precipitation, temperature, wind, and elapsed time. `WeatherSurfaceController` receives the typed value rather than integrating a separate conflicting wetness clock.

Provide deterministic catch-up for travel/rest and save restoration.

### Task 3.5: Validate forecasts

Create `Assets/Editor/WorldForecastValidator.cs` with menu entry:

`InfernosCurse/Environment/World Environment/2. Validate Forecasts`

Run a deterministic sweep across:

- all twelve Florentine months;
- multiple campaign seeds;
- all district microclimates;
- autumn spell and flood thresholds;
- period continuity and transition ranges;
- snow/sleet/hail temperature constraints;
- save/reload regeneration.

The validator reports seed/date context for every failure and a summary count.

## Phase 4 — COZY Presentation and Consumer Migration

### Task 4.1: Add a dedicated COZY presenter

Create `Assets/Scripts/Environment/CozyEnvironmentPresenter.cs`.

Responsibilities:

- translate `WorldWeatherState` to canonical COZY profiles;
- blend profiles once per logical transition;
- ensure the COZY ecosystem is manual at runtime;
- keep sun, moon, sky, clouds, fog, wind, precipitation, and thunder active only in permitted scenes;
- expose one synchronized lightning sample/event;
- never write gameplay state;
- never generate or serialize a canonical forecast.

Modify `CozySceneAdapter` so suspending the renderer does not invalidate logical time or weather state.

### Task 4.2: Migrate presentation consumers

Modify:

- `Assets/Scripts/Environment/WorldWindowEnvironment.cs`
- `Assets/Scripts/Environment/WeatherSurfaces/WeatherSurfaceController.cs`
- `Assets/Scripts/Environment/LightShaft.cs`
- `Assets/Scripts/Environment/TorchFlicker.cs`
- `Assets/Scripts/Map/GugolMapWeatherPresenter.cs`
- other current weather-profile readers found by repository search

Requirements:

- consume typed phase/weather/intensity values;
- remove local profile-name parsing;
- use one authoritative lightning value;
- preserve current material property blocks and particle budgets;
- use profile phase boundaries instead of local dawn/dusk constants where practical;
- retain safe clear-noon fallback in isolated scenes.

### Task 4.3: Migrate gameplay consumers

Modify:

- `Assets/Scripts/Battle/WeatherVision.cs`
- `Assets/Scripts/AI/BattleWeatherDirector.cs`
- `Assets/Scripts/Narrative/PersistentCrierActor.cs`
- Gugol time-band helpers and other schedule readers

Requirements:

- sight and schedule logic consume typed state;
- time bands share the central phase profile or a documented schedule mapping;
- BattleWeatherDirector creates only a battle-local override;
- leaving battle clears the override and restores the paused world snapshot;
- Gemini may choose only typed, battle-permitted categories and cannot alter the world forecast.

### Task 4.4: Extend save migration

Modify `Assets/Scripts/UI/SaveSystem.cs`.

Add a new save version containing:

- forecast seed/identity;
- active period identity and transition progress where needed;
- accumulated wetness;
- flood state;
- environment schema version.

Keep `weatherType` readable for legacy saves. Apply date, time, district, environment state, and wetness in a documented order before importing consumers that depend on them.

Add a save/load runtime verifier that captures a snapshot, perturbs time/weather/wetness, reloads, and compares every persisted field.

## Phase 5 — Deterministic Asset and Scene Migration

### Task 5.1: Build shared profile and prefab setup tooling

Create `Assets/Editor/WorldEnvironmentSetupBuilder.cs` with menu entry:

`InfernosCurse/Environment/World Environment/3. Ensure Shared Setup`

The builder must:

- create/update `Assets/Data/Weather/WorldEnvironmentProfile.asset`;
- attach/configure `WorldEnvironmentDirector` and `CozyEnvironmentPresenter` on `GameSystems.prefab`;
- set COZY presentation to manual without editing package source;
- remove generated `currentForecast` and runtime current-weather overrides from the prefab;
- preserve unrelated prefab components and user-authored overrides;
- be idempotent.

Run the builder once, inspect its exact prefab diff, and rerun it to prove a zero-diff second pass.

### Task 5.2: Correct weather-surface coverage

Modify `Assets/Editor/WeatherSurfaceStandardBuilder.cs`.

- Identify water using renderer/material/shader/component evidence rather than object-name substring alone.
- Exclude structural river walls, piers, caps, and walks from water findings.
- Treat fountain stream and ripple renderers as intentional visual children when driven by a registered fountain surface.
- Register the actual Arno water and Mercato fountain water with correct exposure, kind, renderer, and rain-impact behavior.
- Preserve particle budgets and avoid duplicate emitters in the embedded inn.

Run `Validate All Scenes` until Mercato Vecchio and FlorentineInnFloor1 produce zero unexplained findings.

### Task 5.3: Validate seamless interiors and celestial ownership

Audit Mercato plus its embedded inn, a standalone interior, Giardino delle Rose, and BattleArena for:

- exactly one enabled celestial directional authority where COZY is active;
- no embedded `IndoorWeatherLightingGuard`;
- local interior volumes/lights functioning at all phases;
- precipitation only at authored openings/courtyards;
- exterior weather visible through windows;
- no global ambient changes while entering the seamless inn.

Use deterministic installers/builders for any scene corrections and inspect every scene diff before keeping it.

## Phase 6 — Runtime Matrix and Completion Audit

### Task 6.1: Create the environment runtime verifier

Create `Assets/Editor/WorldEnvironmentPlayModeVerifier.cs` with menu entry:

`InfernosCurse/Environment/World Environment/4. Run Runtime Matrix`

The verifier must safely enter play mode and iterate:

- times: dawn, noon, dusk, night;
- weather: clear, cloudy, fog, rain, storm;
- scenes: Mercato Vecchio, seamless inn position, Giardino delle Rose, one standalone interior, BattleArena.

For each case capture:

- logical environment snapshot;
- COZY profile/presentation state when active;
- enabled directional-light count;
- registered surface counts and precipitation budget;
- window/light response;
- indoor exposure violations;
- screenshot path.

The verifier restores the original scene, time, weather, and play-mode state on completion or failure.

### Task 6.2: Run focused battle and persistence probes

Verify:

- world clock freezes during battle;
- battle-local weather changes sight and VFX;
- world forecast is unchanged after battle;
- save/load reproduces time, period, district, wetness, and flood state;
- travel/rest catch-up produces the same state as equivalent continuous simulation.

### Task 6.3: Run final source and runtime gates

Fresh evidence required:

1. Unity compilation: zero C# errors.
2. Contract validator: PASS.
3. Forecast validator: PASS.
4. Weather-surface validator: PASS.
5. Runtime matrix: every required scene/time/weather case captured and PASS.
6. Battle probe: PASS.
7. Save/load probe: PASS.
8. Enter and exit play mode, then confirm `GameSystems.prefab` has no generated forecast churn.
9. Inspect `git diff --check` and the complete scoped diff.
10. Update `Docs/MASTER_PROJECT_MEMORY.md` with architecture, validators, runtime evidence, and commit coverage.

## Commit Strategy

Use narrow commits so failures can be isolated without touching unrelated worktree changes:

1. `Add typed world environment contract`
2. `Implement continuous world clock and weather fronts`
3. `Migrate environment and battle consumers`
4. `Apply weather assets and surface standards`
5. `Verify weather and day-night runtime matrix`
6. `Document completed environment system`

Do not push unless explicitly requested.
