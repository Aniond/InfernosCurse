# Limbo Crier, World AI, and Multi-Circle Implementation Plan

Date: 2026-07-10

Design: `Docs/superpowers/specs/2026-07-10-limbo-crier-humanoid-enemy-design.md`

## Delivery Strategy

This pass crosses save permanence, world simulation, AI narrative, combat mechanics, and a large visual package. Implement it through the checkpoints below in order. Each checkpoint must compile and preserve legacy saves before the next layer begins. Do not batch unrelated dirty workspace files into any commit.

## Checkpoint 1: Establish Test Fixtures and Migration Baseline

1. Capture representative legacy save JSON for pre-Circle and pre-chronicle formats without including credentials or personal filesystem data.
2. Add deterministic edit-mode fixtures for the current Florence node graph, calendar day keys, influence values, and existing save import/export.
3. Record the current global and per-node curse values produced by a short simulation so migration behavior can be compared.
4. Confirm `Assets/Scripts/Config/GeminiConfig.json` remains ignored and never appears in a diff, log, fixture, prompt snapshot, or test artifact.
5. Add focused test helpers for advancing game days without loading production scenes.

## Checkpoint 2: Replace the Single Curse Value with Circle Influence Ledgers

1. Add an append-only `CircleId` representation and a `CircleInfluenceState` value object.
2. Extend or replace `CurseDefinition` with reusable Circle identity, sanctity resistance, local-source tuning, bleed tuning, presentation, battle, NPC-affliction, and event tags while preserving compatibility during migration.
3. Give every `HubNode` a validated collection of independent Circle values and a native-Circle reference.
4. Add APIs to read, set, add, cleanse, enumerate, and identify the dominant Circle without requiring values to sum to one.
5. Route Florence's current corruption callers to the Limbo entry.
6. Preserve `GameFeatures.CorruptionEnabled` as a temporary compatibility gate.
7. Update battle seeding and presentation callers to read the dominant Circle or an encounter-authored override.
8. Remove universal time-only growth; leave influence unchanged on source-free exploration days.
9. Add formula tests for independent meters, dominant selection, sanctity resistance, zero-source stability, and bounds.

## Checkpoint 3: Migrate and Round-Trip Saves

1. Extend `SaveData` with `influenceLocationIds`, `influenceCircleIds`, and `influenceValues` parallel arrays.
2. Validate identical lengths, known location IDs, known Circle IDs, finite values, and clamped ranges during import.
3. Migrate legacy `curseNodeIds` and `curseLevels` into Florence Limbo entries when new arrays are absent.
4. Preserve legacy sanctity values.
5. Round-trip multiple Circle entries on one node and one Circle across multiple nodes.
6. Prove legacy saves load, migrate once, save in the new format, and reload without drift.

## Checkpoint 4: Implement the Campaign-Permanent Chronicle

1. Add a `CampaignChronicle` service stored separately from save slots under `Application.persistentDataPath`.
2. Create a new random campaign ID on New Game and persist it before the first permanent choice.
3. Define immutable chronicle entries with sequence, campaign ID, event-instance ID, event/choice IDs, date, location, registered consequence payload, previous hash, and current hash.
4. Serialize with deterministic field ordering required by the hash computation.
5. Commit through temporary-file write, flush, validation, atomic replacement, and rolling backup.
6. Add `campaignId` and `chronicleSequence` to ordinary saves.
7. Reconcile every later entry when an older slot from the same campaign loads.
8. Apply consequences idempotently by event-instance and registered effect IDs.
9. Prevent committed choice UI from reopening; route directly to established aftermath.
10. Keep chronicles when individual slots are deleted or overwritten.
11. Create a new independent chronicle only through New Game.
12. Restore a valid rolling backup when the primary fails validation; block load with a clear recovery error when both fail.
13. Show the full permanence warning on the first permanent decision and the compact marker thereafter.
14. Test old-slot reconciliation, duplicate loads, slot deletion, New Game isolation, sequence gaps, hash mismatch, partial writes, backup restore, and double-effect prevention.

## Checkpoint 5: Add the Authoritative World Event Ledger

1. Define registered event types, choices, consequence effects, semantic tags, and unlock references.
2. Append canonical `WorldEventRecord` data for every consequential choice.
3. Retain the full structured ledger for the first implementation; do not destructively compact it.
4. Add indexed queries by NPC, world agent, location, Circle, tag, event type, and recency.
5. Build derived permanent facts and summaries as replaceable caches.
6. Integrate event records with campaign-chronicle commitment for Campaign-Permanent choices.
7. Implement the exact default Limbo consequence values from the design through registered effects.
8. Test event idempotence, unlock idempotence, canonical-fact preservation, and derived-cache rebuild.

## Checkpoint 6: Implement Bounded Circle Bleed

1. Add route-strength metadata to hub connections with defaults for direct, regional, and weak links.
2. Run cross-location propagation once per game day through one idempotent director.
3. Implement the approved pressure, gradient, sanctity, and route formula.
4. Enforce the 70% source threshold and 1.5-point incoming cap per Circle and target per day.
5. Ensure export never drains the source.
6. Permit multiple incoming Circles without normalization or overwrite.
7. Add deterministic table tests for 0%, 70%, 85%, and 100% sources; equal source/target levels; full and zero sanctity; every route strength; multiple sources; and caps.
8. Add a long-horizon simulation proving serious regional bleed without an unstoppable all-map cascade.

## Checkpoint 7: Implement Persistent Crier World Agents

1. Define persistent world-agent records with stable IDs, district/site IDs, activity, discovered, defeated, disruption day, and last processed day.
2. Author scene-local preaching, travel, relocation, and hiding sites through stable IDs.
3. Implement unloaded daily simulation without hidden GameObjects.
4. Apply cautious undiscovered Crier influence at 0.25 points and discovered influence at 0.75 points, capped at 2 points per district daily before sanctity reduction.
5. Materialize loaded agents at their persistent authored sites.
6. Implement the `Travel -> Preach -> Relocate/Evade -> Hide` visible state machine.
7. Connect sermon disruption, hybrid battle confrontation, withdrawal, escape, and defeat to persistent outcomes.
8. Verify every daily contribution is idempotent by agent and date.
9. Test loaded/unloaded parity, relocation, disruption, escape, permanent defeat, save/load, and domain reload.

## Checkpoint 8: Add NPC Memory State and the Unmooring Cascade

1. Define stable NPC memory records with district, susceptibility, exposure, stage, day key, original schedule/relationship references, safeguards, and Forgotten-pool state.
2. Author schedule-to-preaching-site overlap IDs for the first Florence NPC cohort.
3. Implement the approved exposure formula, cautious-Crier half weight, sanctity reduction, and 24-point daily cap.
4. Add non-destructive overlays for Grounded, Distracted, Unmoored, and Forgotten behavior.
5. Preserve original dialogue, schedule, relationship, quest, and service data beneath overlays.
6. Cap critical NPCs at 79 and provide explicit backup access for essential services.
7. Move Forgotten NPCs into a protected pool without deleting their identities or relationships.
8. Implement cleanse, passive recovery, sanctuary recovery, high-Limbo recovery block, rescue, next-dawn return, and threshold-based restoration.
9. Test stage boundaries, loaded/unloaded parity, critical caps, duplicate spawning, pool recovery, name restoration, schedule restoration, and full save round-trip.

## Checkpoint 9: Build the Authored Florence Opening

1. Author baseline familiarity lines that establish Benidito as a lifelong resident known throughout his neighborhood.
2. Create the guaranteed neighbor-forgetting event and make it Campaign-Permanent.
3. Add the ordered escalation events for usual orders, repeated conversations, lost homes, missing records, disputed relationships, empty workplaces, and erased citizens.
4. Keep Limbo terminology and Circle UI hidden before the first Crier confrontation.
5. Unlock the Limbo journal vocabulary and active-world rules at the first face-to-face Crier event.
6. Verify the sequence cannot repeat, skip its commitment, or change after loading an older save.

## Checkpoint 10: Add Rumor and Hidden-POI Discovery

1. Define stable rumor, search-area, route, POI, and discovery IDs.
2. Implement `Rumored -> Located -> Discovered` as monotonic unlock stages.
3. Allow authored events and validated Gemini follow-ups to unlock only registered IDs.
4. Add initial Florence rumors for Crier sermons, a lost person, and one culturally significant hidden POI.
5. Keep undiscovered POIs absent from exact map/travel UI while retaining their simulation records.
6. Persist discoveries across all slots in the campaign when their source choice is Campaign-Permanent.
7. Test duplicate rumors, out-of-order evidence, older-save reconciliation, travel unlock, and hidden-map presentation.

## Checkpoint 11: Build the Gemini Narrative Director

1. Keep the existing `GeminiClient` as the provider boundary and add no credential data to tracked files.
2. Define a strict response schema containing prose, reaction metadata, selected registered follow-up ID, and derived summary only.
3. Build bounded context from canonical facts, location Circle ledger, current NPC memory/relationship state, permanent facts, up to 20 recent relevant records, current rumors, and allowed IDs.
4. Treat all model output as untrusted data; reject unknown IDs, schema violations, contradictions, and oversized responses.
5. Prevent the model from emitting or applying arbitrary state patches.
6. Add deterministic authored fallback dialogue, rumors, and follow-up selection for timeout, quota, malformed output, network absence, and provider failure.
7. Cache safe responses by canonical context hash where repetition would otherwise incur unnecessary calls.
8. Redact credentials and avoid logging full prompts containing future spoiler state.
9. Test continuity across sessions, old-slot reconciliation, relevance filtering, unknown IDs, contradiction, timeout, offline behavior, and identical deterministic consequences with or without Gemini.

## Checkpoint 12: Add Shared Combat Effect Services

1. Implement the one-cell forced-movement service shared by Pilgrim's Hook and Root-Hook.
2. Validate bounds, occupancy, reservation, walkability, elevations, edges, objectives, protected actors, and immovable targets.
3. Preserve resolved damage when movement fails and keep grid occupancy authoritative.
4. Implement the temporary-terrain snapshot/restore service shared by Limbo Stain and Grave Mulch.
5. Reject protected terrain, avoid nested snapshots, expire deterministically, and restore all temporary terrain when combat ends.
6. Add exhaustive edit-mode and Play-mode tests before wiring either enemy.

## Checkpoint 13: Add Status and Equipment Foundations

1. Append Dread and False Zeal without reordering existing status values.
2. Implement exact duration, stat, CT, damage, refresh, cleanse, boss-resistance, and tile-stain behavior.
3. Add optional signed damage-received modifiers to `EquipmentDefinition`; empty collections preserve existing items.
4. Fold equipped modifiers after ordinary damage computation and before HP loss with a non-negative multiplier clamp.
5. Create Bell-Hook Staff, Crier's Jack, and False Reliquary data with exact approved values.
6. Create Knell of Dread, Crooked Benediction, Pilgrim's Hook, Bell of Vigilance, and Pilgrim's Rescue skill assets.
7. Assign complete Holy counterpart references.
8. Register all new definitions deterministically.

## Checkpoint 14: Generate and Approve the Limbo Crier Visual Anchor

1. Create one final full-body south-facing PixelLab-compatible character anchor from the approved identity, equipment, palette, and high top-down view.
2. Present the actual anchor to David before spending animation-generation credits.
3. Reject inconsistent mask, hood, staff, bell, reliquary, silhouette, period, or palette details rather than patching them across hundreds of frames.
4. Create the PixelLab character only after anchor approval and record the character ID without credentials.
5. Generate and approve all eight static rotations.

## Checkpoint 15: Generate the Complete Animation Package

1. Generate four-direction walking with six frames per direction.
2. Generate four-direction Bell-Hook Jab with nine frames per direction.
3. Generate four-direction Knell of Dread with nine frames per direction.
4. Generate four-direction Crooked Benediction with nine frames per direction.
5. Generate four-direction Pilgrim's Hook with nine frames per direction.
6. Generate four-direction hurt reaction with four frames per direction.
7. Generate four-direction death/fall with nine frames per direction.
8. Poll every job to completion and verify 220 animation frames plus eight rotations.
9. Reject missing directions, duplicate frames, identity drift, equipment drift, cropped weapons, or inconsistent scale before import.
10. Retain provider character and animation group IDs in tracked source metadata without URLs containing credentials.

## Checkpoint 16: Create Portrait, Icons, and Limbo Stain Presentation

1. Generate a transparent 128 by 128 Crier portrait matching the project portrait anchor and approved character identity.
2. Generate transparent 32 by 32 Dread and False Zeal icons.
3. Generate transparent 32 by 32 icons for the Bell-Hook Staff, Crier's Jack, and False Reliquary.
4. Create the one-cell Limbo Stain decal and restrained pulse material/VFX without baked text or high-intensity bloom.
5. Present the portrait and icon sheet for approval before production wiring.
6. Apply nearest filtering, no mipmaps, no compression, transparency, and correct Sprite import settings.

## Checkpoint 17: Build the Reusable Humanoid Visual Profile

1. Add `HumanoidBattleVisualProfile` with rotations, cardinal locomotion, hurt, death, skill-keyed actions, frame rates, counts, and provider metadata.
2. Add one optional profile reference to `CombatantData`.
3. Query the profile first in battle presentation and preserve every existing inline animation fallback.
4. Build an idempotent Limbo Crier importer/builder that copies validated source frames into stable production paths and wires the profile.
5. Create `Enemy_LimboCrier` with exact stats, equipment, portrait, skills, learnable drops, 0.25 drop chance, alignment, and profile.
6. Verify no Rosekin, Loamkeeper, or Benidito visual reference leaks into the Crier.

## Checkpoint 18: Wire Combat AI and Encounters

1. Add narrow scoring support for Crooked Benediction, multi-target Knell, and tactical one-cell pulls.
2. Preserve the existing intelligence-tier architecture and avoid a bespoke Crier controller.
3. Verify the approved priority order, SP affordability, charge behavior, retreat, and ally protection.
4. Add one Crier protected by two frontline enemies to an existing Florence hybrid encounter.
5. Preserve exploration terrain, camera, grid, routes, props, and unrelated encounter actors.
6. Validate Benidito absorption, duplicate leveling, insanity, casting, and Church refinement for both learnable Crier skills.

## Checkpoint 19: Production Validators and Runtime Proof

1. Validate all 228 character images, expected dimensions, directions, counts, import settings, IDs, profile mappings, equipment, skills, statuses, icons, portrait, registry entries, world-agent records, and scene bindings.
2. Rebuild twice and confirm the second pass produces no duplicate assets or reference drift.
3. Compile editor and runtime assemblies with zero new errors.
4. Run the complete battle matrix for four-direction movement, actions, hurt, death, statuses, forced movement, terrain restoration, simultaneous Criers, absorption, and refinement.
5. Run the complete world matrix for opening events, cautious/full Criers, disruption, escape, defeat, Unmooring, recovery, rumors, POIs, event permanence, Circle coexistence, and bleed.
6. Run Gemini online and deterministic offline paths against the same canonical event sequence and confirm identical state outcomes.
7. Load older slots after permanent decisions and prove the chronicle reconciles forward without duplicate rewards or consequences.
8. Run 30-, 60-, and 120-day source/no-source simulations to validate exploration freedom and regional contagion bounds.
9. Inspect the final Unity console and resolve every Crier/world-AI error or new warning.

## Checkpoint 20: Documentation and Commit Boundaries

1. Update `Docs/MASTER_PROJECT_MEMORY.md` with architecture, migration, source IDs, production paths, exact formulas, AI fallback, save permanence, and verification evidence.
2. Document how future cities register native Circles and NPC-affliction profiles without implementing the other eight profiles.
3. Document secure Gemini configuration and recovery without recording the token.
4. Keep generated staging files, raw provider responses, prompt experiments, and secret-bearing config out of commits.
5. Split implementation commits by verified subsystem rather than one monolithic commit.
6. Stage only explicit paths and preserve all unrelated dirty workspace changes.
