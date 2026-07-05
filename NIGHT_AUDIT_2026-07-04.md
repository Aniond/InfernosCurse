# Night Audit — 2026-07-04

Full-codebase audit run overnight by five parallel reviewers (battle core, map/travel/UI,
environment/simulation, persistence/data, project hygiene) after shipping the Gugol Maps
world map (`606ccb0`, `8070fb1`) and battle integration (`b553439`). Every finding below was
verified against current source, not comments. Nothing has been fixed yet — this is the
menu, ordered by how much each item matters.

---

## P0 — Fix first

**1. The entire combat-progression layer is not saved.** `SaveSystem` persists the world
(calendar, weather, florins, curse, guild rep, district) but nothing about the party:
roster HP/SP, job levels/XP/AP, AP-unlocked skills, and Benidito's absorbed/refined skills
are all runtime-only, rebuilt fresh by `PartyRoster` each session. Save→reload is a free
full heal and a total progression wipe. Suggested `SaveData` additions (following the
file's existing sentinel pattern, plus a proper `int saveVersion`): `partyHP[]`,
`partySP[]`, per-member `activeJobId/jobLevel/jobXP/jobAP`, `unlockedSkillIds[][]`,
`absorbedSkillIds/dupCounts/refined[]` keyed by stable asset names.

**2. `ApplySave` never loads the saved scene.** `MenuManager.LoadSlot` applies state into
whatever scene is currently open — loading a save made in Fiesole while standing in the
Duomo teleports the player to Fiesole coordinates inside the Duomo geometry. Fix: load
`data.sceneName` first, then apply positional/singleton state after the scene (and
GameSystems) is ready.

**3. Multi-day time advancement collapses the world simulation.** `DailyCurseDrift` (and
the flood-risk flag) tick by polling a day-key in `Update`; a 4-day journey or any
`AdvanceDay()×N` loop advances the calendar 4 days in one frame but charges only ONE day
of curse growth. This quietly breaks the game's core "time is the resource" economy.
Fix: subscribe drift to `GameCalendar.OnDayChanged` (fires once per `AdvanceDay`), and
compute flood risk per-day inside the tick via a pure `FloodRiskFor(year, dayOfYear)`.

**4. Charged skills can corrupt another unit's turn.** `BattleManager.ResolveQueuedAction`
mutates `_hasActed`/`_turnComplete` even when the resolving unit is not `_activeUnit` —
a player's charged skill that resolves during a later unit's turn force-ends that unit's
turn. Fix: only touch active-turn state when `unit == _activeUnit`.

**5. Saving mid-battle produces an unrecoverable slot.** The pause menu (and Save) is
reachable inside BattleArena; the save records `sceneName = "BattleArena"` but not the
encounter payload — loading it lands in the arena with no encounter, no exit path, and
possibly a test battle on top. Fix: block saving in battle scenes (grey the slots).

## P1 — High

- **Victory copy-back writes 0 HP for members who died in a won fight** — Piero can exit a
  *victory* permanently downed until rest. Clamp copy-back to a revive floor on victory
  (defeat already revives at half).
- **DoT kills award nothing** — poison/burn deaths bypass `AwardPostKill` entirely (rewards
  only flow through `AbilityResolver`): no XP, AP, florins, or absorb roll. Route DoT
  deaths through the reward path using the status's source.
- **Dead party members earn no XP from kills** (`AwardPostKill` filters `IsAlive`) — decide:
  FFT-authentic, or award to all members to avoid death-spiral starvation.
- **No SP re-validation at resolve time** — a queued action whose SP was drained in the
  interim silently no-ops and still consumes the turn. Re-check and refund the turn.
- **FlorenceWeather records "applied" state before its suspend guard** — entering a battle
  updates `_appliedDistrictId` while the actual COZY apply is skipped; returning to that
  same recorded district skips re-apply → stale sky. Bail out of `ApplyToday` before
  mutating `_applied*` when COZY is suspended. Same window: fog burn-off can consume its
  one-shot flag against a frozen clock.
- **`FloodRiskToday` goes stale across multi-day jumps and after `ApplySave`** (the raw
  profile apply sets the day-key, suppressing re-derivation for the loaded day).
- **Map soft-lock edges**: `_savedTimeScale` can be captured as 0 on re-entrant opens
  (permanent freeze on close); a refused destination load strands `_travelling = true`
  (map dead until scene change). Guard the capture; reset `_travelling` on load failure.
- **`TravelIntent` leaks on a failed `ToScene` ZoneExit** — the stale entry teleports the
  player at the next legitimate scene load. Set the intent only after
  `CanStreamedLevelBeLoaded` passes.
- **Aborted encounters mute the test arena for the session** — `PendingEncounter.Current`
  is never cleared if the arena unloads without an outcome. Call `Complete()` from
  `EncounterBootstrap.OnDestroy` when `!_outcomeHandled`.
- **`CameraShake` and `BattleCurseAutomata` are Awake-only singletons** — the last two
  components vulnerable to the mid-play-recompile static wipe (every other system already
  uses the lazy-resolve pattern). Convert both.
- **SaveSystem robustness**: no try/catch around JSON parse (corrupt file = throwing load
  button forever), non-atomic `File.WriteAllText` (crash mid-write corrupts the slot),
  no slot-range validation, no version field. Wrap, write-to-temp-then-replace, validate,
  version.

## P2 — Medium

- EnemyAI: a fully walled-in enemy "moves" to grid (0,0) — the empty-moveRange fallbacks
  return `Vector2Int.zero`. Skip the move instead.
- Confirming a single-target skill on an empty valid cell consumes the turn doing nothing.
  Reject confirm when the cell has no valid occupant.
- `GlobalCurseLevel()` now includes region gateway/city pins (population 1.0 teaser nodes),
  diluting the city corruption average that surge thresholds key off. Restrict to
  `NodeKind.District` (waypoints are already excluded).
- `GuildSystem.GetRep` mutates state (lazily inserts zero-rep rows) — save files accumulate
  phantom memberships. Make reads pure.
- Enemy assets: `baseStats.hp > hpMax` inconsistencies, copy-paste identity
  (`characterName: Dante, class: Warrior` on both enemies), and near-identical kits —
  Ash Wretch wants a distinguishing skill for encounter variety.
- `CharacterSheet`'s Tab hotkey ignores every overlay (opening it over the paused map
  un-pauses the game underneath via `BackToPause`). Broader issue: overlay mutual
  exclusion only exists against the map — R/G/Rest/Guild/Pause have no gate among
  themselves. Consider one shared "any overlay open" gate.
- Pin labels create a TMP material instance per pin per rebuild (`outlineWidth`) — material
  churn on every zoom. Share one outlined material preset.
- Search-submit can select dimmed, locked, or gateway pins (Enter on "fies" may zoom
  instead of opening the Fiesole card). Filter the submit loop.
- EnemyAI belief decay uses `Time.deltaTime` inside turn-based decisions — the fog-of-war
  layer is effectively inert. Decay per-turn instead.
- `HubMap`'s dormant real-time diffusion is only guarded by the component being disabled in
  the inspector — nothing in code prevents double-driving curse if re-enabled. Add an
  explicit flag.
- `SetLayer` doesn't unfocus the search field — mid-typing zoom leaves a focused empty box
  eating hotkeys and blocking ESC-close.
- `EncounterRoll._resolved` is session-memory only — a same-day save/load can re-trigger an
  already-fought waypoint (deterministically identical). Export/import in SaveData.
- Resting in a scene without a GameCalendar adds curse + heals without costing the day.
  Make rest no-op without a calendar.

## P3 — Low / cleanup sweep

- **Delete-ready legacy**: `WorldMapUI.cs`, `MapNodeView.cs`, `MapNodeDetailPanel.cs`,
  `Assets/Scenes/WorldMap.unity`, `Assets/Prefabs/Map/MapNodePin.prefab` (scene is out of
  Build Settings; every fallback path to it is unreachable). Decide `FastTravelMenu`'s
  fate: dead-in-practice, kept as a no-art fallback. Drop `ZoneExit`'s `ToWorldMap`
  LoadScene fallback branch (keep the overlay path).
- CT-queue ties resolve nondeterministically (unstable sort) — add a stable tiebreak.
- Walk animation clips through occupied tiles (cosmetic).
- `PlayerWait` is callable in any state — guard to player-turn states.
- `hpMax = 0` on a misauthored combatant NaNs the AI aggression math — guard the division.
- `FlorinWallet.SetBalance` fires `OnChanged` on no-op values.
- `DepthOfFieldFocus.Awake` throws if the Volume has no profile assigned.
- Camera-relative movement silently falls back to world axes with no MainCamera — warn once.
- Defeat overlay's displayed florin loss is computed separately from the applied loss —
  capture once, pass through.
- `GugolMapUI.OnDestroy` doesn't unsubscribe `HubMap.OnNodeChanged` (dangling delegate
  after teardown-while-open).
- `PlagueOfShadows.asset` predates the daily-drift fields — re-save it so inspector tuning
  actually persists (currently riding on code defaults).
- `Skill_NothingWasted` is a Passive with no passive-effect consumer anywhere — inert.
- Ergot Bloom flavor says "burn/Fire" but is typed Poison — confirm intent.

## Design questions for David (not bugs — your call)

1. **The 666 review count** on the Gugol card is a deliberate joke I added, but the auditor
   is right that it's a 1:1 readable tell of `curseLevel ≥ 0.5` — technically against the
   "curse is hidden" rule. Keep as an intentional vibe-tell, or make review counts pure
   hash noise?
2. **Church content is fully gated** (`Duomo_DISABLED_*` scene names park all Church
   interactions) — meaning the absorb→refine loop has no reachable in-game content. Fine
   until the events phase, but it now blocks a live mechanic (Benidito absorbs skills he
   can never refine).
3. **Dead-member XP starvation** (P1) — FFT-authentic punishment or quality-of-life fix?
4. **Baker is a dead-end job tree** (no `advancedJobs`) — fine solo, but `CanAdvance()` is
   permanently false; worth deciding the first advancement target when jobs expand.

## Positive findings (things that held up under audit)

Deterministic simulation discipline is genuinely strong — seeded `System.Random`
everywhere, FNV node salts, no wall-clock leakage; the date-before-hour + `ResyncClock`
save-restore ordering is correct; the skill library has no missing ranges/damage types;
`FlorenceClimate.json` matches its parser exactly; `GameSystems.prefab` has no stale or
duplicate components and every serialized ref resolves; Resources paths all resolve
(including COZY's nested profiles); and the clone-isolation and status-reentrancy scars
in the battle core show the hard bugs there were already found and killed.

## Suggested attack order

1. **Save v2** (P0 #1, #2, #5 + P1 robustness): progression persistence, scene-load-on-
   apply, battle-save guard, atomic writes. Biggest player-facing payoff; prerequisite for
   meaningful playtesting.
2. **Time-economy integrity** (P0 #3 + flood staleness): the drift refactor is small
   (subscribe vs poll) and protects the design's core resource.
3. **Battle turn-flow trio** (P0 #4 + DoT rewards + victory copy-back floor): three
   surgical fixes to the combat loop's correctness.
4. **Map exit-path hardening** (timeScale capture, `_travelling` strand, TravelIntent
   leak, encounter-abort Complete()): all small guards against soft-locks.
5. **The cheap sweep**: lazy-singleton conversions, one-line guards, enemy-asset touch-ups,
   legacy deletion. An afternoon of low-risk tidying.

## Project hygiene sweep

- **Compiler warnings in OUR code are just two lines**: `GugolMapUI.cs:904/910`
  `enableWordWrapping` → `textWrappingMode = TextWrappingModes.NoWrap`. Everything else in
  the warning spam is the vendored COZY package — leave it alone (and treat
  `Packages/com.distantlands.cozy.core` as permanently out of cleanup scope).
- **Confirmed delete-safe legacy set**: `WorldMapUI.cs` + `MapNodeView.cs` +
  `MapNodeDetailPanel.cs` + `WorldMap.unity` + `MapNodePin.prefab` (referenced only among
  themselves; the Gugol map fully supersedes them). `FastTravelMenu` is NOT blind-deletable
  — `MenuManager` still uses it as a fallback and two editor scripts patch it; retire in
  order (fallback → patches → file). `BattleTestStarter` is live infrastructure — keep.
- **`Assets/UOHD2D/` no longer exists** — the untracked folder seen at last night's commit
  time is gone (removed before the sweep ran). Nothing to review.
- **Repo health**: .gitignore is strong (Library/Temp/builds/.env/workbench outputs all
  covered); LFS patterns comprehensive (3D/audio/video/textures) — but the
  large-binary-not-in-LFS scan timed out: worth a manual `git lfs ls-files` sanity check.
  The `Refrences/` typo folder is coupled to a `.gitignore` rule — renaming is real risk
  for low value; leave it. Root `New folder/` and `tiles/` look like stray working dirs —
  glance and delete if junk.
- **Packages**: three Unity AI packages in the manifest (`ai.assistant` is a pre-release)
  plus visualscripting/collab-proxy — removal candidates if unused. COZY is embedded in
  git at ~1,856 files (accepted cost, but it's the bulk of the repo's source weight).
- **TODO census**: only three TODOs in our code, all still-relevant (CharacterSheet party
  data, HubMap surge/cleric wiring). No FIXME/HACK debris. No runtime `new Material`/
  `new Texture2D` leaks found in scripts; no per-frame Find/GetComponent smells in hot
  paths (camera scripts worth a spot-check for cached GetComponents).
- **Suggestion adopted-by-report**: add a short `CLAUDE.md` (build entrypoints, Gugol-vs-
  legacy map split, "never edit the COZY package") — cheap and prevents future churn.
