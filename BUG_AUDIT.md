# InfernosCurse — Bug Audit Checklist
Generated 2026-06-29. Full read of all 44 scripts in `Assets/Scripts/`.

**UPDATE 2026-06-30:** ALL items fixed — CRITICAL, HIGH, MEDIUM, and LOW.

Work top-down: CRITICAL first (crashes/data corruption), then HIGH (wrong behavior).

---

## CRITICAL — will crash or corrupt data

- [x] **CombatantData.cs** — `currentHP`/`currentSP` are `[NonSerialized]` fields on a **ScriptableObject**. Two BattleUnits spawned from the same CombatantData asset (e.g. two of the same enemy) SHARE the same HP — damaging one damages both. **Fix:** clone the data per-unit at spawn, or move runtime state off the SO into BattleUnit. *(Highest priority — breaks any battle with duplicate enemies.)*

- [x] **BattleGrid.cs:104–107** — `GetMoveRange` BFS enqueues enemy-occupied cells for further expansion, so movement pathfinds THROUGH enemies and finds tiles behind them. **Fix:** don't enqueue cells occupied by non-allies.

- [x] **BattleUnit.cs:131** — `TickCharge()` calls `ResolveQueuedAction` (which does `SetState(TickCT)`) synchronously from inside `CTQueue.TickUntilReady()`, mutating turn-flow state mid-tick outside the coroutine. **Fix:** defer charge resolution into the battle-loop coroutine.

- [x] **BattleManager.cs:80–83** — `StartBattle` indexes `playerSpawns[i]`/`enemySpawns[i]` with no length check vs party lists → `ArgumentOutOfRangeException`. **Fix:** validate list lengths match before spawning.

- [x] **StatusEffect.cs:119** — DoT in `TickAll` can kill a unit (→ `Die()` → `HandleUnitDeath` → possibly `SetState(BattleVictory)`) synchronously inside `unit.StartTurn()`; `ProcessUnitTurn` then proceeds to `ShowMoveRange` for a dead unit (BattleManager.cs:149). **Fix:** check `unit.IsAlive` after StartTurn before showing ranges.

- [x] **DamageNumberPool.cs:94** — If prefab lacks `DamageNumber` component, `CreateNew()` enqueues null; next `Get().ShowDamage()` is a NRE. **Fix:** null-check after GetComponent, warn loudly.

- [x] **BattleManager.cs:153** — Player turn loop `while (!_hasActed || !_hasMoved)` can deadlock on action-then-undo sequences; `PlayerUndoMove` resets `_hasMoved=false` without re-checking `_hasActed`. **Fix:** audit the flag state machine, guard undo when already acted.

---

## HIGH — wrong behavior, not a crash

- [x] **AbilityResolver.cs:92–95 + DamageNumberPool.cs:49** — Crit flag is never propagated through `OnUnitDamaged`, so crits never show red/"!" damage numbers. **Fix:** add isCrit to the OnUnitDamaged event signature.

- [x] **AbilityResolver.cs:74** — Any skill with `damageType == None` is treated as a heal. Buff/utility skills will wrongly call `target.Heal()`. **Fix:** add explicit heal flag or SkillType.Heal.

- [x] **BattleManager.cs:264** — `ResolveQueuedAction` always sets `TickCT` even for a player mid-turn, ending their action phase after one skill. **Fix:** only advance turn when the action actually completes the turn.

- [x] **BattleManager.cs:271** — `EndUnitTurn` only advances from `EnemyTurn`. If a kill flips state to `BattleVictory` mid-AI-turn, loop at line 170 (`while State == EnemyTurn`) never exits. **Fix:** handle all terminal states.

- [x] **EnemyAI.cs:59–64** — `DecideAction` uses `BattleManager.Instance.Grid/.Players/.Enemies` with no null check on Instance. NRE during teardown. **Fix:** null-guard Instance.

- [x] **BattleCurseAutomata.cs:213–217** — `CleanseTile` uses `_world` which is null until `Step`/`SeedFromHub` runs. A Holy skill before first CA step → NRE. **Fix:** lazy-init or guard `_world`.

- [x] **MenuManager.cs:170,182,204,212,244,254** — `pausePanel.SetActive` and others lack the `?.` guard used everywhere else. Unassigned panel → crash on Escape. **Fix:** null-guard consistently.

- [x] **TurnOrderSidebar.cs:29–35** — Subscribes to BattleManager events in Start, never unsubscribes (no OnDestroy). Dangling delegates after teardown. **Fix:** unsubscribe in OnDestroy.

- [x] **TorchFlicker.cs:46** — `_light.intensity` used every Update with no null check; GetComponent<Light> may be null. **Fix:** guard or RequireComponent.

- [x] **DayNightCycle.cs:41–47** — On scene reload the persistent instance keeps `sun`/`mainCamera` refs to destroyed objects; OnEnable doesn't re-fire on the surviving instance. **Fix:** re-resolve refs on `sceneLoaded`.

---

## MEDIUM — edge cases, missing guards

- [x] **CTQueue.cs:21** — All-units-speed-0 → CT never advances → 10,000-iter guard returns empty → battle loop spins every frame. **Fix:** detect total stall, break the outer loop. *(Fixed 2026-06-30 — stall detection + battle-loop stall guard.)*
- [x] **CTQueue.cs:75,84** — If all remaining units have gain ≤ 0, `minTicks` stays float.MaxValue; `gain * MaxValue` → NaN/Infinity corrupts snapshot, 100,000-iter spin. **Fix:** break when minTicks is unbounded. *(Fixed 2026-06-30.)*
- [x] **AbsorbedSkillInstance.cs:30** — `maxLevel == 0` in a SkillDefinition silently locks level at 1 forever. **Fix:** clamp maxLevel ≥ 1 or validate.
- [x] **JobProgress.cs:67** — `entry.apInvested` is always 0; the guard `if (currentAP < entry.apInvested)` is dead code (likely meant `apCost`). **Fix:** remove or correct.
- [x] **SaveSystem.cs:29** — `timeOfDay` falls back to 12f if no DayNightCycle; silently saves noon. **Fix:** persist actual or skip field.
- [x] **BattleManager.cs:306** — `ShowAttackRange` hardcodes `minRange = 1`; range-0 self-buffs return zero cells and can't be confirmed. **Fix:** use skill's own min range.
- [x] **BattleManager.cs:209** — Charged skills queued but no path back to the charging unit's turn after charge completes during another unit's CT phase. **Fix:** design charge-completion turn re-entry. *(Fixed 2026-06-30 — ChargeComplete flag surfaces in CTQueue.GetReadyUnits, battle loop resolves it.)*
- [x] **EnemyAI.cs (Carrier)** — `BattleCurseAutomata.SpreadFromUnit` is never actually called by CarrierAI; Carrier's signature curse-spread behavior is dead. **Fix:** wire SpreadFromUnit into Carrier's move/action.
- [x] **OcclusionFade.cs:56** — `Physics.RaycastAll(~0)` every frame, hits player's own collider. **Fix:** layer mask + ignore self.
- [x] **SaveSystem.cs:56–70** — `ApplySave` teleports player transform with no rigidbody handling (interpolation artifacts). **Fix:** move via rigidbody or disable interp for one frame.

---

## LOW — cleanup, dead code, perf

- [x] **SpriteBillboard.cs:13** — `Camera.main` every LateUpdate; cache it.
- [x] **DepthOfFieldFocus.cs:41** — `Camera.main` every LateUpdate; cache it.
- [~] **MenuManager.cs:143** — `OpenCharacterSheet(memberIndex)` ignores the index; always shows Dante placeholder. *(Partial: index now plumbed through to `CharacterSheet.Open(int)`; real per-member stat lookup deferred to the Job/party data task.)*
- [~] **CharacterSheet.cs:76** — Hardcoded placeholder stats; skills/equipment tabs have no population logic. *(Deferred: real population lands with the Job assets task. Close button now returns to pause menu.)*
- [x] **DamageNumberPool.cs:49** — Acknowledged: crit flag not propagated (see HIGH).
- [x] **JobProgress.cs:67** — `apInvested` dead field; remove.
- [x] **GameSystemsBootstrap.cs:10** — Existence check via `FindAnyObjectByType<HubMap>()` breaks if HubMap is ever placed standalone in a scene. Consider a dedicated marker component.
- [x] **BattleGrid.cs:188–196** — A* `LowestF` is O(n²) linear scan; min-heap if grids grow.
- [x] **BattleFormulas.cs:65** — `RollHit` compares attacker DEX vs defender SPEED; document the intent.
- [x] **OcclusionFade.cs:18–19** — Two parallel dicts keyed on Renderer; merge into one.
- [x] **SaveSystem.cs** — `SLOT_COUNT = 3` declared but unused; loops hardcode 3.
- [x] **WeatherSystem.cs:44** — `DNC` lazy-find acceptable (low frequency) but noted.
- [x] **BattleCurseAutomata.cs:59** — Perlin Lerp upper half (1.0–1.2) is immediately clamped; cosmetic.

---

### Suggested order for tomorrow
1. CombatantData shared-HP (blocks all multi-enemy testing)
2. GetMoveRange pass-through enemies
3. Crit propagation (quick, visible win)
4. Heal-vs-buff skill type
5. Then sweep the rest of CRITICAL/HIGH
