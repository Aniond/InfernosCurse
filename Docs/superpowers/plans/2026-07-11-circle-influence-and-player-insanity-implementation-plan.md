# Circle Influence and Player Insanity Separation Implementation Plan

Date: 2026-07-11

Design: `Docs/superpowers/specs/2026-07-11-circle-influence-and-player-insanity-design.md`

## Objective

Replace the current blended corruption/Insanity behavior with two runtime domains that cannot influence one another:

- Hidden, territory-owned Circle Influence drives world simulation, warnings, NPC response, encounters, environmental state, and permanent story consequences.
- Hidden, loadout-derived Insanity drives only Benidito's audiovisual perception and deterministic combat penalties.

The implementation reuses the existing independent Circle ledger, World Event Ledger, Campaign Chronicle, persistent Crier/NPC records, discovery system, and Gugol route graph. It does not implement the full Florist quest, the remaining eight Circle expression profiles, or The Name Forgotten opening content.

## Safety Boundaries

- Read `Docs/MASTER_PROJECT_MEMORY.md` and inspect `git status` before every checkpoint that changes files.
- Preserve every unrelated dirty-tree change. Never reset, clean, discard, or broadly stage the workspace.
- Before editing overlapping Limbo files, create an explicit-path baseline commit of the already verified uncommitted Limbo implementation. Exclude unrelated inn, COZY, font, package, tooling, executable, WSL-image, and imported Asset Store churn.
- `Assets/Resources/GameSystems.prefab` contains unrelated serialized changes. Modify it only through an idempotent editor setup method, inspect the resulting YAML diff, and stage it only when the diff contains the intended component fields.
- Keep append-only serialized enums and effect IDs append-only.
- Keep Gemini credentials and all private configuration out of assets, logs, fixtures, prompts, saves, and commits.
- Do not expose either hidden value through player-facing numbers, bars, tier labels, map overlays, icons, or debug text.
- Do not start The Name Forgotten or production Florist quest implementation in this plan.

## Checkpoint 0: Stabilize the Existing Limbo Baseline

### Actions

1. Re-read the master handoff and classify every dirty file as existing Limbo implementation, approved design documentation, or unrelated user/tool/package work.
2. Confirm the current Limbo validators and Play-mode probe still describe the same implementation recorded in the master handoff.
3. Stage only the verified existing Limbo implementation paths. If an overlapping serialized file contains unrelated changes that cannot be isolated safely, leave it unstaged and document the dependency instead of forcing a mixed commit.
4. Commit the isolated baseline before changing shared Circle, save, battle, or narrative files.
5. Record the resulting baseline commit in `Docs/MASTER_PROJECT_MEMORY.md` only after implementation begins and the commit succeeds.

### Verification

- `dotnet build Assembly-CSharp.csproj --nologo -v:minimal`
- Unity menu: `InfernosCurse/Validation/Validate Complete Limbo Crier Stack`
- Confirm the staged path list contains no package, font, COZY, inn, executable, WSL-image, AssetInventory, or UModelerX paths.

## Checkpoint 1: Split Runtime Feature Controls

### Files

- Modify `Assets/Scripts/Config/GameFeatureSettings.cs`.
- Modify `Assets/Resources/GameFeatureSettings.asset`.
- Create `Assets/Editor/CircleInsanitySetup.cs` and its Unity meta file.
- Modify `Assets/Resources/GameSystems.prefab` only through `CircleInsanitySetup`.

### Implementation

1. Add independent serialized controls:
   - `circleWorldEnabled`.
   - `playerInsanityEnabled`.
   - `insanityPresentationEnabled`.
   - `circleBattlePresentationEnabled`.
2. Add matching fail-closed `GameFeatures` properties and retain `ReloadForEditorTests`.
3. Keep the serialized legacy `corruptionEnabled` field for one migration cycle, but stop using it as a runtime decision point once all callers move.
4. Default Circle simulation and Insanity calculation to enabled in the production asset.
5. Leave both presentation switches independently authorable. The Insanity presentation switch is enabled only after the visual tier review passes; Circle battle presentation retains its currently parked setting.
6. Make the editor setup method idempotently wire component values without rebuilding unrelated `GameSystems` content.

### Verification

- Missing settings asset returns `false` for every feature.
- Enabling or disabling any switch leaves the other three unchanged.
- Search first-party code for `GameFeatures.CorruptionEnabled`; the count must reach zero by Checkpoint 10.
- Compile with zero new first-party warnings.

## Checkpoint 2: Extract the Player-Only Insanity Domain

### Files

- Create `Assets/Scripts/Curse/PlayerInsanityState.cs` and its Unity meta file.
- Create `Assets/Scripts/Curse/PlayerInsanityModifiers.cs` and its Unity meta file.
- Modify `Assets/Scripts/Curse/InsanityPresenter.cs`.
- Modify `Assets/Scripts/Data/CombatantData.cs` only if a pure loadout enumeration helper is required.
- Modify `Assets/Scripts/Battle/BattleUnit.cs`.
- Modify `Assets/Scripts/Battle/BattleFormulas.cs`.
- Modify `Assets/Resources/GameSystems.prefab` through `CircleInsanitySetup`.

### Implementation

1. Move `InsanityState` out of the presenter and replace it with `PlayerInsanityState`.
2. Provide a pure `Calculate(CombatantData benidito)` method for deterministic validation and a runtime `Current` accessor that locates the Benidito-role party member.
3. Calculate `sum(GetInsanityCost())` only across equipped absorbed slots. Banked and unequipped orbs contribute zero; refined instances already return zero.
4. Clamp the result to 0-100 and map it to the approved tiers:
   - 0-14: clear.
   - 15-34: warning presentation only.
   - 35-54: Perception -2.
   - 55-74: Perception -3 and Faith -2.
   - 75-100: Perception -4, Faith -4, and CT gain x0.90.
5. Apply stat penalties after equipment and orb bonuses in `BattleUnit.GetEffectiveStats` for Benidito only.
6. Apply the CT multiplier in `BattleFormulas.CTGainPerTick` for Benidito only.
7. Make `InsanityPresenter` read `PlayerInsanityState.Current`, never HubMap or Circle state.
8. Restore production presentation thresholds on the persistent prefab to 15, 35, 55, and 85; the current serialized 3, 6, 9, and 15 values are debug-scale and must not ship.
9. Keep hallucinations cosmetic and non-interactive. Do not create fake targets, choices, forecasts, objectives, or interactables.

### Verification

- Validate every boundary value from 0 through 100.
- Equip, level, refine, and unequip representative corrupted skills and confirm immediate recalculation.
- Confirm rest and game-day advancement do not change the value.
- Confirm non-Benidito units receive no modifier.
- Confirm 100 Insanity does not block control, actions, or saving.

## Checkpoint 3: Remove Every Cross-Domain Coupling and Map Leak

### Files

- Modify `Assets/Scripts/Battle/PendingEncounter.cs`.
- Modify `Assets/Scripts/Battle/ZoneEncounterTrigger.cs`.
- Modify `Assets/Scripts/Rest/RestSystem.cs`.
- Modify `Assets/Scripts/Map/GugolMapPin.cs`.
- Modify `Assets/Scripts/Map/GugolUi.cs`.
- Modify `Assets/Scripts/Map/MapNodeView.cs`.
- Modify any additional first-party callers found by repository search.

### Implementation

1. Delete the road-encounter `InsanityPull` term. Encounter chance may read Circle World State, never Insanity.
2. Delete the zone-detection `insanityScent` multiplier. Enemy exploration detection may read authored senses, weather, and world conditions only.
3. Remove direct Circle additions and cleansing from ordinary inn rest and camping. Rest still heals, spends florins where applicable, advances the day, and allows unresolved world sources and deadlines to process.
4. Preserve explicit sanctuary or remembrance actions as registered world events rather than passive rest side effects.
5. Remove Circle-derived pin tinting, the `666` review tell, and any other Gugol-map indication of hidden pressure.
6. Preserve ordinary discovery, routing, travel access, previews, and non-Circle map presentation.
7. Remove `InsanityState.WorldCorruption`, `Total`, and the old `Current` compatibility path after all callers compile against `PlayerInsanityState`.

### Verification

- Repository search finds no Insanity reference in map, NPC, quest, world-event, territory, or encounter-generation code.
- Repository search finds no Circle or HubMap reference in `PlayerInsanityState`, `PlayerInsanityModifiers`, or `InsanityPresenter`.
- Changing Insanity between 0 and 100 does not change deterministic road rolls or zone sight radii.
- Resting changes neither Circle Influence nor Insanity directly.
- Gugol map screenshots at low and high test influence are visually identical except for ordinary event/discovery content.

## Checkpoint 4: Add Explicit Territory Ownership to the Gugol Graph

### Files

- Create `Assets/Scripts/Curse/CircleTerritory.cs` and its Unity meta file.
- Modify `Assets/Scripts/Curse/HubNode.cs`.
- Modify `Assets/Scripts/Curse/HubMap.cs`.
- Modify `Assets/Scripts/Curse/CircleInfluence.cs` only for territory-safe helpers.
- Modify `Assets/Editor/GugolMappeSetup.cs`.
- Modify `Assets/Resources/GameSystems.prefab` through the Gugol setup method.

### Implementation

1. Add an append-only `TerritoryKind` enum for state owners: City, Town, Village, Estate, Winery, Monastery, and Landmark.
2. Extend `HubNodeData` and runtime `HubNode` with:
   - `influenceTerritoryId` for site-to-owner resolution.
   - `parentRegionId` for state-owning territories.
   - `territoryKind`.
   - `regionalWeight` greater than zero for state owners.
   - A boolean or equivalent contract identifying aggregate-only and non-state nodes.
   - Route-strength overrides keyed by existing neighbor IDs.
3. Keep existing `neighborIds` as the travel graph. Route-strength overrides affect Circle propagation only and default to 1.0 when absent.
4. Add `HubMap.ResolveInfluenceTerritoryId(locationId)` and route all Circle reads and writes through it.
5. Export influence only for state-owning territories. Sites may retain permanent outcome state but cannot retain a separate numeric ledger.
6. Add `HubMap.GetRegionalInfluence(regionId, circle)` using the approved weighted mean. Regions remain read-only and are never serialized as mutable influence owners.
7. Keep compatibility methods temporarily, but mark broad global writes such as `NudgeGlobalInfluence` obsolete and remove their callers.

### Florence Authoring

1. Configure `firenze` as Florence's sole citywide influence owner, parented to `toscana`, with regional weight 8.
2. Configure `fiesole` as its own town territory, parented to `toscana`, with regional weight 1.
3. Configure `toscana` as aggregate-only.
4. Point Duomo, Mercato, Signoria, Ponte Vecchio, Oltrarno, Santa Croce, San Lorenzo, Giardino, Salone delle Arti, Via Calimala, and other Florence sites at `firenze` through `influenceTerritoryId`.
5. Keep waypoints non-state-bearing.
6. Future villages, wineries, monasteries, and estates receive their own owner records and explicit regional weights when authored.

### Verification

- Every build-scene location resolves to exactly one state owner or an explicitly non-state node.
- Writing Limbo through `mercato` changes only `firenze`.
- Giardino has no independent Circle entry.
- Tuscany equals the exact weighted result of Florence and Fiesole in the initial graph.
- Map routing and zoom behavior remain unchanged.

## Checkpoint 5: Collapse Florence Baselines and Migrate Saves to v6

### Files

- Modify `Assets/Scripts/Narrative/FlorenceOpeningBaseline.cs`.
- Modify `Assets/Scripts/UI/SaveSystem.cs`.
- Modify `Assets/Scripts/Curse/HubMap.cs`.
- Create a focused migration helper under `Assets/Scripts/Curse/CircleTerritoryMigration.cs` and its meta file.
- Modify `Assets/Editor/CircleInfluenceValidator.cs`.

### Implementation

1. Advance `SaveSystem.CURRENT_VERSION` from 5 to 6.
2. New campaigns seed Florence Limbo at 0.08 on `firenze` and Fiesole Limbo at 0.05 on `fiesole`. Do not seed mutable state on `toscana` or any Florence site.
3. Export only owner-territory Circle arrays.
4. When v6 owner data is absent, collapse known legacy Florence site and district entries into `firenze` through a fixed migration-weight table stored in code or validated data.
5. Compute the legacy Florence value from that table whenever at least one mapped Florence child entry is present. Use the legacy `firenze` gateway value only as a fallback when no mapped child entry exists; never combine both representations or sum percentages.
6. Migrate non-Florence owner-capable locations directly.
7. Keep legacy `curseNodeIds`, `curseLevels`, `curseSanctity`, `driftDayKey`, and `worldSimulationDayKey` as guarded read paths for one version; stop writing obsolete district influence in new saves.
8. Do not add an Insanity field. The saved absorbed/equipped loadout remains its only persistence source.

### Verification

- Round-trip multiple Circles on Florence and Fiesole.
- Load representative v2-v5 fixtures and confirm one-time deterministic migration.
- Save migrated data and reload without drift or duplicated entries.
- Reject malformed parallel arrays and invalid territory IDs.
- Confirm no world value enters the Insanity calculation during migration.

## Checkpoint 6: Replace Daily Drift with the Territory Simulation Director

### Files

- Create `Assets/Scripts/Curse/CirclePropagationMath.cs` and its meta file.
- Replace or rename `Assets/Scripts/Curse/DailyCurseDrift.cs` as `CircleWorldSimulationDirector.cs`, preserving its Unity meta identity if the file is renamed.
- Modify `Assets/Scripts/Curse/CurseDefinition.cs`.
- Modify `Assets/Scripts/Curse/HubMap.cs`.
- Modify `Assets/Resources/GameSystems.prefab` through `CircleInsanitySetup`.
- Modify `Assets/Scripts/UI/SaveSystem.cs` for the new applied-day key.

### Implementation

1. Keep the existing date subscription and first-seen baseline behavior, but rename the concept from drift to explicit world simulation.
2. Move bleed math into a pure helper implementing the approved threshold, gradient, route, resistance, and cap rules.
3. Take one immutable territory snapshot per day and batch every incoming result so no same-day propagation cascade is possible.
4. Use authored route-strength overrides rather than assuming every edge is 1.0.
5. Apply unresolved source contributions through stable source IDs and date keys.
6. Remove the obsolete rest-policy simulation and all passive-decay fields from active logic. Retain serialized legacy fields hidden only where asset compatibility requires them.
7. Recalculate regions after batch application; never save or directly mutate a region.
8. Persist the director's applied-day key as `circleSimulationDayKey`, with migration from the older keys.

### Verification

- Exact formula cases at 0%, 70%, 85%, and 100% source influence.
- Every route strength and resistance boundary.
- Multiple incoming sources respect the 0.015 target cap.
- A chain A-B-C cannot influence C from A during the same day when only B receives that day's first incoming value.
- Thirty-, sixty-, and one-hundred-twenty-day simulations demonstrate serious neglect without all-map runaway.
- Thirty days without sources leave every value unchanged.

## Checkpoint 7: Add Warning, Deadline, and Site-Outcome State

### Files

- Create `Assets/Scripts/Narrative/CircleWarningDefinition.cs` and its meta file.
- Create `Assets/Scripts/Narrative/CircleWarningLedger.cs` and its meta file.
- Create `Assets/Scripts/Narrative/SiteOutcomeState.cs` and its meta file.
- Create `Assets/Scripts/Narrative/CircleExpressionProfile.cs` and its meta file.
- Modify `Assets/Scripts/Narrative/WorldEventLedger.cs`.
- Modify `Assets/Scripts/Narrative/WorldEventDefinition.cs` only if authored event references need explicit warning metadata.
- Modify `Assets/Scripts/UI/SaveSystem.cs`.

### Definition Contracts

1. `CircleWarningDefinition` owns stable IDs, territory/Circle filters, severity, prerequisites, opening symptom, stage schedule, internal deadline, response event IDs, expiration event ID, and permanence classification.
2. `CircleWarningRecord` owns event instance ID, definition ID, territory, Circle, opened day, current stage, deadline, resolved/expired state, and last processed day.
3. `SiteOutcomeRecord` owns site ID, stable outcome ID, source event instance ID, permanence, activation date, and optional recovery/remembered state. It does not own Circle Influence.
4. `CircleExpressionProfile` owns symptom thresholds, event tags, NPC overlay vocabulary, environmental presentation IDs, encounter tags, and propagation tuning references.

### Runtime Rules

1. Evaluate thresholds only after daily source and propagation processing.
2. Open at most one new major warning per territory per day.
3. Advance active warning stages and deadlines idempotently by day key.
4. Never execute an irreversible outcome from a missing warning definition.
5. Commit Campaign-Permanent expiration outcomes to the Chronicle before changing site or NPC presentation.
6. Add append-only registered world effects for site-outcome state and authored remembrance. Preserve all existing effect IDs.
7. Make site-outcome and warning imports transactional with the existing batch consequence sink.
8. Provide deterministic authored text IDs; Gemini receives them as allowed context but cannot add alternatives.

### Verification

- Threshold crossing opens a warning and does not commit its expiration outcome.
- Stage escalation survives save/load and cannot double-advance in one day.
- Explicit success closes the warning and blocks expiration.
- Expiration commits once, even after loading an older slot.
- Missing or invalid definitions report an authoring error and leave permanent state unchanged.
- Site outcomes never create sub-city influence ledgers.

## Checkpoint 8: Integrate Limbo Agents and NPC Expression with Citywide Florence

### Files

- Modify `Assets/Scripts/Narrative/LimboWorldSimulation.cs`.
- Modify `Assets/Scripts/Narrative/NpcUnmooringOverlay.cs`.
- Modify `Assets/Scripts/Narrative/NpcMemoryDefinition.cs`.
- Modify `Assets/Scripts/Narrative/WorldAgentDefinition.cs` only for explicit site/territory separation.
- Modify `Assets/Scripts/Narrative/GeminiNarrativeDirector.cs`.
- Modify `Assets/Editor/FlorenceLimboWorldAuthoringBuilder.cs`.
- Create the Limbo expression-profile asset under `Assets/Resources/CircleProfiles/Limbo/` with its meta files.
- Modify existing NPC-memory and Crier assets only through the idempotent authoring builder.

### Implementation

1. Preserve authored site IDs such as Mercato preaching sites for schedules, overlap, materialization, and local presentation.
2. Resolve every Crier influence contribution through the site's owner territory so Mercato activity changes `firenze`.
3. Keep NPC exposure and individual Unmooring records distinct from citywide Circle Influence.
4. Retain non-destructive Grounded, Distracted, Unmoored, and Forgotten overlays.
5. Use the approved relevance layers: direct relationships, local witnesses, territory symptoms, and derived regional events.
6. Allow ordinary recoverable NPC overlays to recede after Limbo is meaningfully reduced and sources are resolved. Require an authored remembrance outcome to restore facts erased by a permanent site loss.
7. Preserve essential-service and quest-critical safeguards.
8. Feed Gemini canonical territory, warning, site-outcome, relevance, and Chronicle facts without numeric values.
9. Update the opening authoring so the active Mercato Crier creates a troubled local experience without a separate Mercato meter.

### Verification

- Loaded and unloaded Crier processing produces identical Florence influence and NPC exposure.
- Crier disruption, escape, and defeat persist across scenes and saves.
- NPC overlays restore or remain permanent according to Chronicle/site-outcome state.
- Gemini online and fallback paths produce identical state.
- No prompt or response exposes numeric Circle or Insanity values.

## Checkpoint 9: Add a Florist-Shaped Validation Fixture Without Building the Quest

### Files

- Add test-only definitions or in-memory fixtures to `Assets/Editor/CircleInsanitySeparationValidator.cs`.
- Create `Assets/Scripts/Validation/CircleInsanityPlayModeProbe.cs` and its meta file.
- Create an editor verifier wrapper under `Assets/Editor/` if automated Play-mode launch is required.
- Do not modify the production Giardino scene or author production Florist dialogue in this checkpoint.

### Implementation

1. Create an in-memory warning definition shaped like the approved Florist flow: opening plea, escalating deterioration, expiration, permanent recruit death, permanent garden loss, grief, Limbo memory erosion, stabilization, and remembrance.
2. Use stable validator-only IDs that can never collide with production event IDs.
3. Prove the sequence through the real warning ledger, World Event Ledger, Chronicle sink, NPC overlays, and site-outcome store.
4. Confirm remembrance restores canonical memory/grief but not the NPC or original site state.

### Verification

- Success before expiration leaves the recruit and garden available in the fixture.
- Expiration commits death and site loss once.
- Later Limbo escalation erodes witness memory.
- Reducing Limbo stops erosion but does not restore facts automatically.
- Authored remembrance restores memory and grief only.
- Loading an older slot cannot undo the death or garden loss.

## Checkpoint 10: Update Battle and Presentation Callers to the New Gates

### Files

- Modify `Assets/Scripts/Battle/BattleManager.cs`.
- Modify `Assets/Scripts/Battle/BattleTerrainCurse.cs`.
- Modify `Assets/Scripts/Curse/BattleCurseAutomata.cs`.
- Modify `Assets/Scripts/Curse/CurseOverlay.cs`.
- Modify `Assets/Scripts/Battle/AI/EnemyAI.cs`.
- Modify `Assets/Scripts/Battle/EncounterBootstrap.cs`.
- Modify any remaining `GameFeatures.CorruptionEnabled` callers found by repository search.

### Implementation

1. Route world encounter composition through Circle World State only.
2. Gate Circle-specific battle terrain and automata through the independent Circle battle control.
3. Keep tactical fog independent.
4. Keep Crier skill-specific Limbo Stain behavior independent from Benidito's Insanity.
5. Ensure disabling Circle battle presentation does not disable world warnings, NPC effects, or territory simulation.
6. Ensure disabling Insanity presentation does not disable calculation or combat penalties.
7. Remove the legacy monolithic gate after the final caller migrates, while preserving serialized migration data in the settings asset for one version if required.

### Verification

- Four-switch truth table passes for world simulation, Insanity calculation, Insanity presentation, and Circle battle presentation.
- Tactical fog behaves identically in every switch combination.
- Circle world events continue when battle presentation is off.
- Insanity penalties continue when audiovisual presentation is off.
- Repository search reports zero runtime uses of `GameFeatures.CorruptionEnabled`.

## Checkpoint 11: Production Validators and Runtime Proof

### Files

- Create or complete `Assets/Editor/CircleInsanitySeparationValidator.cs` and its meta file.
- Modify `Assets/Editor/CircleInfluenceValidator.cs`.
- Modify `Assets/Editor/LimboWorldSimulationValidator.cs`.
- Modify `Assets/Editor/CampaignChronicleValidator.cs`.
- Modify `Assets/Editor/GeminiNarrativeValidator.cs` where new context fields require coverage.
- Modify `Assets/Editor/LimboCrierFinalValidator.cs` to include the new validators.
- Complete `Assets/Scripts/Validation/CircleInsanityPlayModeProbe.cs`.

### Static Validation Matrix

1. Feature-switch independence and missing-asset fail-closed behavior.
2. Insanity calculations, tier boundaries, stat modifiers, CT modifier, refinement, and immediate unequip recovery.
3. Forbidden dependency scan across known world and player-domain source paths.
4. Territory ownership, city-site resolution, regional weights, and aggregate-only nodes.
5. Save v6 shape and v2-v5 migration fixtures.
6. Propagation threshold, gradient, route strength, resistance, cap, and simultaneous batch behavior.
7. Warning stages, deadlines, missing-definition safety, and at-most-one-major-warning rule.
8. Site outcomes, remembrance, Chronicle reconciliation, and duplicate prevention.
9. Hidden-map contract: no Circle-derived ratings, tints, review values, tier labels, or numeric strings.
10. Gemini state invariance and numeric-value redaction.

### Runtime Matrix

1. Clean Florence opening with citywide state and no visible numeric UI.
2. Active Crier raises Florence while Mercato remains the visible local source.
3. Travel and rest advance the source and warning clocks without direct Circle deltas.
4. Existing hybrid Crier encounter still starts, resolves, and persists victory.
5. Insanity transitions at 0, 15, 35, 55, 75, and 100 in an exploration scene and battle.
6. Insanity never changes road rolls, zone sight, NPC behavior, warnings, or Circle state.
7. Florist-shaped fixture covers warning through remembrance.
8. Multiple Circles coexist and propagate independently.
9. Final console contains no new first-party errors or warnings.

## Checkpoint 12: Documentation, Visual Review, and Commit Boundaries

### Documentation

- Update `Docs/MASTER_PROJECT_MEMORY.md` with the new state boundaries, save v6, territory ownership, feature switches, propagation order, warning records, site outcomes, and verification evidence.
- Update `Docs/AI_HANDOVER_BLUEPRINT.md` to remove the `Personal + WorldCorruption` model, Insanity encounter beacon behavior, rest-driven corruption, numeric map tells, and district-owned Florence values.
- Document how future Gugol territories assign a parent region, native Circle, regional weight, owner mapping, and route strength.
- Document how future Circles add expression profiles without changing the shared state engine.

### Visual Review Gate

1. Preview Insanity presentation at the six required boundary values in an existing exploration scene containing water and indoor/outdoor lighting.
2. Confirm no effect resembles territory corruption or exposes an actionable falsehood.
3. Obtain user approval before enabling `insanityPresentationEnabled` in the production settings asset.

### Commit Sequence

1. `Separate player Insanity from world influence`.
2. `Define Gugol Circle territories and regional pressure`.
3. `Add Circle warnings and permanent site outcomes`.
4. `Migrate Limbo world simulation to citywide Florence`.
5. `Validate Circle and Insanity isolation`.
6. `Document separated Circle and Insanity systems`.

For every commit, stage explicit paths only and inspect `git diff --cached --name-only` plus `git diff --cached --check` before committing.

## Completion Criteria

- The project compiles with zero new first-party errors or warnings.
- Circle Influence and Insanity have separate feature controls, state services, inputs, outputs, and validation.
- Florence owns one citywide Circle ledger; sites resolve to it and Tuscany is derived from weighted territory owners.
- Hidden Circle pressure can trigger fair, timed warnings and permanent site outcomes without exposing a number.
- Circle pressure grows only through registered sources and propagation and falls only through meaningful registered action.
- Insanity derives only from equipped unrefined Corrupted Skills and applies the approved personal tiers.
- Insanity cannot affect world state, encounters, NPCs, quests, or facts; world state cannot affect Insanity.
- Save v6 migrates earlier saves without drift and the Campaign Chronicle preserves irreversible outcomes across older-slot loads.
- Existing Crier combat, hybrid-zone behavior, discovery, Gemini fallback, and tactical fog remain functional.
- The Florist-shaped validation fixture proves loss, grief, erasure, stabilization, and remembrance without prematurely implementing the production quest.
- The master handoff is current, and every implementation commit excludes unrelated dirty-tree changes.

## Work After This Plan

After the foundation is implemented and verified:

1. Resume The Name Forgotten brainstorming against the approved citywide Limbo and warning-event contracts.
2. Design the production Florist quest as its own spec, including the recruitable NPC, exact warning duration, exact influence consequences, rewards, dialogue, art, and Giardino scene variants.
3. Add further Circle expression profiles only when their native territories enter production.
