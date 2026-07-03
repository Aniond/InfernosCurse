# Weather & Time System

COZY: Stylized Weather 3 drives sky, sun, fog, and weather. A thin project layer
on top picks historically-real Florence weather and feeds every game system
through one clock facade. Migrated 2026-07-03 (commit `0c1acc9`), replacing the
hand-written `DayNightCycle`/`WeatherSystem` (deleted).

## Architecture

```
COZY (Packages/com.distantlands.cozy.core, embedded UPM package)
  └─ "Cozy Weather Sphere" — nested inside the GameSystems prefab
     (Resources/GameSystems.prefab), so weather/time are GLOBAL and
     survive scene loads. Bootstrap spawns it; nothing per-scene.

GameClock  (Assets/Scripts/Environment/GameClock.cs)          [static facade]
  The ONLY way game code asks "what hour is it?" (0–24 float).
  Backed by CozyWeather.instance.timeModule. Consumers: GameCalendar,
  TorchFlicker, LightShaft, SaveSystem. GameClock.Describe() dumps
  backend state for debugging.

GameCalendar  — stile fiorentino calendar (unchanged); advances one day
  when GameClock.Hour wraps past midnight.

FlorenceWeather  (on GameSystems)  — the daily weather picker.
  Reads Assets/Data/Weather/FlorenceClimate.json (v2: research-backed —
  1991-2020 NOAA normals for rain days/temps, verified storm/hail seasonality;
  fog & wind columns are flagged design judgment; see the JSON's _sources).
  Rolls ONE condition per in-game day, SEEDED off the date (year*1000 +
  dayOfYear) so a given day is reproducible. Applies it via COZY's ecosystem
  in manual mode with a 20s transition.

  Era layer (chronicle-verified: Villani's floods of 1269/1280s, the 1302-03
  famine rains, 4 Nov 1333): in Sep/Oct/Nov a wet day can CHAIN into the next
  (rainSpell block in the JSON; chainProb 0.45, max 4 days). The chain is a
  pure function of the date (seeded, bounded backward replay) — save-proof.
  Spell day >= 2 renders Heavy Rain; day >= 3 sets FlorenceWeather.
  FloodRiskToday (hook for WeatherEffects/story). Statistically verified over
  2000 simulated years: autumn wet frequency amplifies ~1.4x, ~1.4 flood-risk
  days/year, full 4-day episodes every ~2.6 years.

  Morning fog: fog days apply Dense Fog, then burn off to Mostly Clear at
  fogBurnOffHour (10.5) with a slow 60s dissolve — Arno fog is a radiation/
  morning phenomenon, not an all-day soup.

CozySceneAdapter  (on GameSystems) — on every scene load, disables
  scene-authored directional lights so COZY's sun is the only sun at
  runtime. Scenes KEEP their authored suns for edit-mode lighting.
```

## Condition → COZY profile mapping

fog → Dense Fog · rain → Light Rain (30% Heavy Rain) · storm → Thunder Storm ·
wind → Mostly Cloudy · snow → Light Snow · sleet → Light Precipitation ·
hail → Hail Storm · clear → Clear / Mostly Clear / Partly Cloudy (variety).
Profiles load by name from the package's Resources
(`Profiles/Weather Profiles/<name>`).

## Time pacing

COZY perennial profile `timeMovementSpeed = 1` → **1 in-game minute per real
second** (24-minute full day) — same pace the old system intended. Save games
store `timeOfDay` (via GameClock) and the COZY profile name; load restores both.

## Gotchas (learned the hard way)

- **"Time is frozen"** → check `EditorApplication.isPaused` FIRST. The editor
  pause button latches across play sessions and caused every frozen-clock
  mystery during the migration.
- Editor **RunCommand scripts cannot reference `DistantLands.Cozy`** (asmdef not
  visible to the dynamic assembly) — probe through project types
  (`GameClock.Describe()` etc.).
- COZY **modules are ScriptableObjects** — runtime writes to time can persist in
  editor memory for the session; don't be surprised by carried-over hours
  between plays in one editor session.
- Mid-play recompiles wipe statics without re-running Awake — GameCalendar and
  FlorenceWeather singletons lazy-resolve for this reason; keep that pattern.
- The old scenes' Directional Lights are edit-mode only now. Don't fight the
  adapter; tune play-mode looks through COZY's atmosphere instead.

## Still to build

- **WeatherEffects** gameplay layer: fog → vision/detection radius, rain →
  movement/audio, storm flash, curse-forced gloom (see cozy plan memory /
  `FlorenceClimate.json` header).
- Morning-weighted fog (Florence fog is a dawn thing) — currently a whole-day
  condition.
- COZY add-ons owned but not installed: Plume (volumetric clouds), Blocks
  (atmosphere presets).
