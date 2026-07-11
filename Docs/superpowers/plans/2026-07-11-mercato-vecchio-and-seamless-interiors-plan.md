# Mercato Vecchio Production Rebuild and Seamless Interiors Implementation Plan

## Objective

Rebuild `MercatoVecchio` as a faithful, production-quality translation of `Refrences/maps/market square.png`; preserve and relocate its hybrid battle and Limbo/Crier systems; embed the completed Florentine Inn first floor as the pilot seamless interior; and establish reusable runtime, authoring, save, camera, lighting, and validation contracts for future shops, inns, homes, and comparable interiors.

Design source: `Docs/superpowers/specs/2026-07-11-mercato-vecchio-and-seamless-interiors-design.md`

## Safety Boundaries

- Preserve the unrelated dirty worktree documented in `Docs/MASTER_PROJECT_MEMORY.md`.
- Stage explicit implementation paths only; never reset or clean unrelated changes.
- Capture the current Mercato and Florentine Inn baselines before rebuilding either scene.
- Preserve the completed inn plan, doorway widths, service interaction, props, courtyard fountain, NPC markers, locked upper landing, and persistent facing behavior.
- Preserve the urban hybrid terrain technology, shared locked HD-2D camera standard, COZY weather ownership, persistent world state, and current Crier combat package.
- Keep the inn a protected social interior. Ordinary Mercato battles must not enter it.
- Keep the Duomo and other approved landmark-scale interiors as dedicated zones.
- Do not edit imported Asset Store package source. Build project-owned adapters and assets around imported tools.
- Do not regenerate or replace approved 3D assets unless inspection proves a production requirement cannot be met with the existing sources.
- Do not push unless explicitly requested.

## Task 1: Capture authoritative baselines

### Files and scenes

- Inspect `Assets/Scenes/MercatoVecchio.unity` through Unity without saving.
- Inspect `Assets/Scenes/FlorentineInnFloor1.unity` through Unity without saving.
- Read `Assets/Editor/FlorentineInnFloor1Builder.cs`.
- Read `Assets/Editor/FlorentineInnPropKitBuilder.cs`.
- Read `Assets/Editor/LimboCrierEncounterBuilder.cs`.
- Read `Assets/Editor/FlorenceLimboWorldAuthoringBuilder.cs`.
- Read `Assets/Editor/UrbanHybridTerrainSceneMigrator.cs`.
- Read `Assets/Editor/HybridZoneStandardValidator.cs`.

### Implementation

1. Record Mercato's current root hierarchy, bounds, player spawn, camera configuration, floor mesh/material, weather surfaces, exits, entry points, Crier sites, battle authoring, grid dimensions, blocked cells, party/enemy spawns, and scene-state objects.
2. Record the inn's root hierarchy, world bounds, entrances, circulation routes, colliders, interactions, NPC markers, lighting, weather windows, camera support, and every deterministic builder-owned anchor.
3. Capture gameplay-camera screenshots of the current Mercato and the inn from representative positions.
4. Record the supplied reference's landmark and route ratios: Loggia, fountain, west inn frontage, east shops, southern riverfront, Ponte route, Signoria route, primary aisles, and secondary aisles.
5. Verify Unity has no compile errors before implementation begins.
6. Restore the original editor scene setup and do not save inspection changes.

### Verification

- Produce a baseline inventory sufficient to compare rebuild output.
- Identify any scene-only configuration missing from current builders.
- Confirm existing Crier and inn validators pass or record their exact pre-existing failures.

## Task 2: Add the seamless-interior runtime contract

### Files

- Create `Assets/Scripts/Environment/SeamlessInteriorModule.cs` and its meta file.
- Create `Assets/Scripts/Environment/SeamlessInteriorPortal.cs` and its meta file.
- Create `Assets/Scripts/Environment/SeamlessInteriorRegistry.cs` and its meta file.
- Create `Assets/Scripts/Environment/SeamlessInteriorActivationGroup.cs` and its meta file only if module-level activation is insufficient.

### Implementation

1. Add `SeamlessInteriorModule` as the authoritative component for one accessible interior:
   - Stable `buildingId` and `subLocationId`.
   - Exterior and interior threshold anchors.
   - Interior content root and optional low-cost/sleep groups.
   - Camera, lighting, audio, weather, NPC, and battle-protection references.
   - Explicit protected/noncombat setting, safe by default.
2. Add `SeamlessInteriorPortal` as the threshold state machine:
   - Detect outside-to-inside and inside-to-outside crossings.
   - Reject ambiguous crossings and missing module references.
   - Expose current transition state and battle lock.
   - Avoid teleporting the player during ordinary threshold travel.
   - Require the module to be ready before allowing entry.
3. Add `SeamlessInteriorRegistry` to resolve stable IDs, expose the active sub-location, restore save state, and detect duplicate IDs.
4. Keep the runtime independent of a specific inn or scene name so future shops and homes can use it unchanged.
5. Support prefab-resident interiors immediately and leave an explicit extension point for additive scene streaming when a future medium building needs a larger memory boundary.
6. Ensure disabling or destroying a module safely clears registry and transition state.

### Verification

- Unit/editor probes cover registration, duplicate IDs, threshold direction, battle lock, missing-module rejection, and teardown.
- A missing module leaves the doorway safely blocked and logs one actionable error.
- Repeated entry/exit does not accumulate registrations, coroutines, or subscriptions.

## Task 3: Add seamless camera and shadow-preserving occlusion

### Files

- Create `Assets/Scripts/Camera/SeamlessInteriorCameraProfile.cs` and its meta file.
- Create `Assets/Scripts/Camera/SeamlessInteriorCameraZone.cs` and its meta file.
- Modify `Assets/Scripts/Camera/CameraOcclusionFader.cs`.
- Modify `Assets/Scripts/Camera/DynamicZoom.cs` only if a profile API cannot be added without it.

### Implementation

1. Add a project-owned camera profile containing interior follow offsets, zoom/clearance parameters, blend duration, occlusion prefixes/layers, and threshold hysteresis.
2. Blend the existing shared HD-2D rig between Mercato's plaza profile and the inn's interior profile; do not create a second camera philosophy or an uncontrolled camera stack.
3. Extend `CameraOcclusionFader` with a shadow-preserving hide mode:
   - Cache each renderer's original enabled and shadow-casting state.
   - Use `ShadowCastingMode.ShadowsOnly` for opaque architecture that must disappear from the camera while continuing to block sunlight.
   - Restore the exact original state when the line clears or the component disables.
   - Retain the current behavior as a selectable compatibility mode for existing scenes.
4. Support explicit roof and near-wall occluder groups instead of relying only on name prefixes.
5. Use hysteresis at the doorway so rapid threshold motion cannot pump the camera or repeatedly fade the facade.
6. Keep the player visible throughout the blend and prevent the camera from exposing unloaded space or the backs of exterior facades.

### Verification

- Enter and leave the inn repeatedly from cardinal and diagonal approaches.
- Confirm one camera rig remains active and the blend is smooth in both directions.
- Confirm hidden roofs/walls continue casting shadows.
- Confirm existing Duomo and standalone inn occlusion behavior does not regress.

## Task 4: Add local interior lighting, weather, and audio isolation

### Files

- Create `Assets/Scripts/Environment/SeamlessInteriorEnvironmentZone.cs` and its meta file.
- Modify `Assets/Scripts/Environment/IndoorWeatherLightingGuard.cs` only to make standalone ownership explicit and prevent accidental embedded use.
- Reuse `Assets/Scripts/Environment/WorldWindowEnvironment.cs` and existing weather-surface components where appropriate.
- Create project-owned URP volume/profile assets under `Assets/Environment/FlorentineInnFloor1/Environment` with meta files.

### Implementation

1. Replace the embedded inn's global ambient override with a local environment zone:
   - Physical roof/wall shadow casters remain active even when camera-hidden.
   - Interior point/area lights and emissive windows remain locally controlled.
   - A bounded URP volume handles restrained interior exposure/color treatment.
   - Exterior weather VFX remain outside or at authored windows/courtyard openings.
2. Retain `IndoorWeatherLightingGuard` only in the standalone inn test wrapper, where it can still own global render settings safely.
3. Add threshold-driven audio blending between market ambience and interior room tone; muffle exterior weather without muting important gameplay cues.
4. Preserve the open courtyard's connection to daylight and weather while preventing enclosed rooms from being flooded by the global sun.
5. Ensure local lights do not remain at full night intensity when the player is outside.
6. Keep COZY authoritative for time, sky, weather, fog, and the exterior directional sun.

### Verification

- Inspect clear day, overcast, heavy rain, lightning, dusk, and night.
- Confirm no direct exterior light ignores the inn's roof or walls.
- Confirm no global ambient or post-processing pop visibly alters Mercato when crossing the threshold.
- Confirm the courtyard still reads as open to the weather.
- Confirm market and inn audio blend without duplicate ambience players.

## Task 5: Extend save/load for owning zones and sub-locations

### Files

- Modify `Assets/Scripts/UI/SaveSystem.cs`.
- Modify `Assets/Scripts/Environment/SeamlessInteriorRegistry.cs`.
- Create or extend editor/play-mode verification for save migration.

### Implementation

1. Increment the save version and add backward-compatible fields for:
   - Owning zone scene.
   - Active `subLocationId`.
   - Player transform and facing inside that sub-location.
2. Save `MercatoVecchio` as the scene while the player is inside the inn.
3. On load, resolve and activate the required interior module before applying the saved transform.
4. If the saved sub-location is missing, place the player at the module's safe exterior fallback and report the recovery.
5. Preserve old saves that reference `FlorentineInnFloor1` by migrating them to `MercatoVecchio` plus the inn sub-location when the pilot is complete.
6. Preserve Campaign Chronicle, Circle, Limbo, guild, party, time, and weather state unchanged.

### Verification

- Load a pre-versioned save, a current Mercato exterior save, a new inn-interior save, and a deliberately missing-sub-location save.
- Confirm player position, facing, active sub-location, time, weather, and permanent world state restore correctly.
- Confirm recovery never places the player inside a collider or an unavailable module.

## Task 6: Refactor the Florentine Inn into module plus standalone wrapper

### Files

- Modify `Assets/Editor/FlorentineInnFloor1Builder.cs`.
- Modify `Assets/Editor/FlorentineInnPropKitBuilder.cs` only where module-safe references require it.
- Create `Assets/Environment/FlorentineInnFloor1/Prefabs/FlorentineInnFloor1_Module.prefab` and its meta file.
- Deterministically update `Assets/Scenes/FlorentineInnFloor1.unity` as a standalone test wrapper.
- Create `Assets/Editor/FlorentineInnSeamlessModuleValidator.cs` and its meta file.

### Implementation

1. Split the builder into two deterministic products:
   - A reusable content module containing architecture, props, NPCs, interactions, local environment, threshold anchors, occluders, and protected-interior metadata.
   - A standalone wrapper scene containing the player, shared camera kit, test apron, test travel, global standalone lighting guard, and one module instance.
2. Remove scene-loading travel components from the reusable module.
3. Preserve all approved room coordinates and current content anchors inside module-local space.
4. Preserve the reception-counter service interaction and locked upper landing.
5. Preserve the courtyard fountain and its animation, water material, streams, and ripples.
6. Add stable IDs such as `albergo_fiorentino` and `albergo_fiorentino_floor1`.
7. Add exterior/interior threshold anchors aligned to the existing four-metre south entrance.
8. Ensure the module carries no Player, Main Camera, EventSystem, COZY authority, scene exit, or global RenderSettings owner.
9. Keep the standalone scene in Build Settings during migration and validation; remove or repurpose it only after old-save migration is verified.

### Verification

- Rebuild the module and wrapper twice and confirm deterministic output.
- Run the existing inn prop, fountain, facing, interaction, route, and lighting checks in the wrapper.
- Require the new validator to reject duplicate players/cameras, scene exits, global lighting guards, missing IDs, missing anchors, missing occluders, or changed room bounds inside the module.

## Task 7: Build the reusable Mercato production kit

### Files

- Create `Assets/Editor/MercatoVecchioProductionKitBuilder.cs` and its meta file.
- Create project-owned assets under `Assets/Environment/MercatoVecchio/ProductionKit` with required folder meta files.
- Reuse approved materials and textures from `Assets/Environment/MarketSquare`, `Assets/Environment/FlorentineInnFloor1/StructuralKit`, and the urban hybrid terrain family.

### Implementation

1. Build reusable prefabs for:
   - Loggia bays, columns, roof, steps, and edge pieces.
   - Florentine townhouse and merchant-front modules.
   - Albergo Fiorentino exterior shell, doorway, sign bracket, and occluding facade/roof groups.
   - Stall frames, awning variants, counters, carts, crates, barrels, and merchandise groups.
   - Central fountain structure and animated water presentation.
   - River wall, overlook, stairs, drains, curbs, and Ponte-approach pieces.
2. Use controlled deterministic variants for plaster, shutters, signs, cloth, merchandise, rooflines, grime, dampness, and wear.
3. Reuse the approved urban vertex-blended surface technology for cobble, stone repair, dirt, and traffic wear.
4. Keep colliders simple and separate from decorative render geometry where practical.
5. Give tactical props explicit cover metadata or collider conventions that can be validated against the battle grid.
6. Enforce gameplay-scale bounds and reject placeholder primitives or missing production materials in final-facing modules.
7. Register the kit with GSpawn only through project-owned library data if useful; the deterministic builder remains authoritative.

### Verification

- Rebuild the kit twice with no duplicate assets.
- Validate prefab bounds, pivots, materials, colliders, LOD/renderer counts, shadow settings, and naming.
- Inspect representative modules at the locked exploration-camera distance before scene composition.

## Task 8: Create the authoritative Mercato production scene builder

### Files

- Create `Assets/Editor/MercatoVecchioProductionBuilder.cs` and its meta file.
- Deterministically rebuild `Assets/Scenes/MercatoVecchio.unity`.
- Preserve or regenerate the project-owned urban production mesh and material assets under `Assets/Art/Environment/HybridZones`.

### Implementation

1. Build a clear root contract, including:
   - `[MercatoEnvironment]` for ground, riverfront, and boundaries.
   - `[MercatoArchitecture]` for Loggia and perimeter buildings.
   - `[MercatoCommerce]` for stalls, carts, merchandise, and merchant frontage.
   - `[MercatoLandmarks]` for fountain, signs, and route anchors.
   - `[MercatoInteriors]` for the inn exterior and embedded module.
   - `[MercatoPopulation]` for civilians and authored NPC anchors.
   - `[MercatoWorldState]` for quest, Limbo, discovery, and Crier anchors.
   - `[MercatoBattleAuthoring]` for the hybrid combat contract.
   - `[MercatoTravel]`, player, camera, weather, and validation roots.
2. Compose the approved spatial hierarchy:
   - Loggia north.
   - Enlarged fountain plaza at center.
   - Albergo Fiorentino and merchant buildings west.
   - Weaponsmith/shop row east.
   - Walkable riverfront south.
   - Ponte route east and Signoria route northwest.
3. Match the reference's relative density and aisle hierarchy while tuning exact metre scale for the shared camera, player collider, and tactical cells.
4. Preserve clear primary routes and authored secondary lanes; do not scatter props randomly into circulation.
5. Place the existing player and shared HD-2D camera kit exactly once.
6. Derive camera confiner bounds from the production walkable area and test all edges.
7. Build physical boundary architecture so the camera never reveals a board edge or empty backdrop.
8. Save only after required assets, anchors, colliders, camera, player, weather, and route contracts validate.

### Verification

- Rebuild twice and compare hierarchy, object counts, anchor positions, and serialized output for deterministic ownership.
- Verify the Loggia, fountain, inn, riverfront, Ponte route, Signoria route, and market rows are recognizable from gameplay views.
- Walk every primary and secondary route without collision traps.

## Task 9: Embed the Florentine Inn pilot

### Files

- Modify `Assets/Editor/MercatoVecchioProductionBuilder.cs`.
- Use `Assets/Environment/FlorentineInnFloor1/Prefabs/FlorentineInnFloor1_Module.prefab`.
- Create `Assets/Environment/FlorentineInnFloor1/Environment/AlbergoFiorentino_Camera.asset` and any required local volume/audio assets with meta files.

### Implementation

1. Position the inn module behind the west facade with its entrance facing the fountain and main aisle.
2. Align the exterior door, street apron, threshold anchors, floor height, and player clearance without teleporting at entry.
3. Configure facade, roof, and near-wall occlusion groups for the shared camera.
4. Configure the interior camera, lighting, audio, weather, and activation profile.
5. Keep the inn module loaded for the pilot; sleep expensive NPC or VFX behavior while outside rather than risking a doorway hitch.
6. Add an exterior safe fallback point for failed restoration or unavailable content.
7. Add a battle-lock blocker that closes the threshold before outdoor tactical control begins.
8. Remove the old Mercato-to-inn `ZoneExit` and inn-to-Mercato scene-load path from the production experience.

### Verification

- Walk from the fountain into reception and back with no scene load, teleport, black frame, camera snap, or lighting leak.
- Interact with the counter, visit every first-floor room, inspect the courtyard, and confirm the upper landing remains locked.
- Save and load inside the inn, at the threshold, and outside in Mercato.

## Task 10: Author travel routes and future-facing contracts

### Files

- Modify `Assets/Editor/MercatoVecchioProductionBuilder.cs`.
- Modify `Assets/Resources/GameSystems.prefab` only if hub-node route data requires it.
- Deterministically update relevant destination entry points only when missing or incorrect.

### Implementation

1. Add a northwest Signoria exit targeting `PiazzaDellaSignoria` at its stable southern arrival.
2. Add an eastern Ponte exit targeting `PonteVecchio` at its stable western arrival.
3. Make the southern riverfront fully walkable and author a stable future Arno route anchor without loading a nonexistent scene.
4. Keep Gugol Mappe and fast-travel discovery behavior consistent with existing route contracts.
5. Give every entry/exit a unique stable ID, facing direction, safe arrival clearance, and anti-retrigger protection.

### Verification

- Travel Mercato to Signoria and back.
- Travel Mercato to Ponte and back.
- Confirm the future Arno anchor is discoverable/valid but cannot send the player to a missing scene.
- Confirm arrivals never overlap colliders or immediately retrigger exits.

## Task 11: Re-anchor Limbo, NPC memory, discoveries, and the Crier

### Files

- Modify `Assets/Editor/FlorenceLimboWorldAuthoringBuilder.cs`.
- Modify `Assets/Editor/LimboCrierEncounterBuilder.cs`.
- Modify related validators only where the new stable anchors require it.
- Preserve the existing `Assets/Resources/WorldAgents`, `Assets/Resources/NpcMemory`, `Assets/Resources/Discoveries`, and Circle profile data unless a stable anchor reference must change.

### Implementation

1. Replace fallback references to legacy names such as `Fountain`, `[Florentine_Stall_1]`, `EntryPoint_South`, and `GapFill_Barrel_7` with explicit production anchor IDs.
2. Place the three preaching sites at the fountain, west market rows, and southern riverfront/gate route; place the hide site in a believable service alley.
3. Preserve all site IDs so Campaign Chronicle, world-agent state, NPC memory, and discovery records remain compatible.
4. Relocate NPC anchors for the baker, record clerk, innkeeper, stallholder, and neighbor relationships without changing their narrative identity.
5. Add presentation-layer anchors for subtle and severe Limbo states without embedding hidden influence values in visible UI.
6. Keep irreversible site outcomes and grief/remembrance behavior intact.

### Verification

- Run the full Florence Limbo world-authoring validator.
- Confirm exactly four unique Crier sites with the approved three-preach/one-hide roles.
- Confirm all required NPC, discovery, and world-agent IDs still resolve.
- Verify the Crier can move, preach, hide, become discovered, and initiate the authored encounter from the new layout.

## Task 12: Rebuild tactical authoring from production geometry

### Files

- Modify `Assets/Editor/LimboCrierEncounterBuilder.cs`.
- Modify `Assets/Scripts/Battle/ZoneBattleAuthoring.cs`.
- Modify `Assets/Scripts/Battle/ZoneEncounterTrigger.cs` only where protected-interior lifecycle support is required.
- Deterministically update `Assets/Scenes/MercatoVecchio.unity`.

### Implementation

1. Make the production scene builder own Mercato bounds, origin, walkability, height, and named tactical anchors.
2. Remove fixed legacy `53x36`/`(-31,-19)` assumptions from the Crier builder; read the existing production `BattleMapAuthoring` instead of replacing it.
3. Recompute blocked cells and cover from production colliders using explicit ignore/terrain/cover conventions.
4. Preserve open cells through primary aisles, around the fountain, at Crier sites, and at valid party/enemy spawn regions.
5. Add protected seamless interiors to `ZoneBattleAuthoring` and lock their portals before exploration control is suspended.
6. Exclude the inn footprint and threshold from outdoor tactical movement and camera framing.
7. Keep stalls, carts, crates, fountain edges, Loggia columns, walls, and steps in place during battle and ensure tactical rules match their visible geometry.
8. Preserve civilian evacuation and encounter teardown behavior; restore building access only after battle cleanup completes.

### Verification

- Validate visible geometry against blocked cells, cover, elevation, and line of sight.
- Start the Crier battle from multiple approach directions.
- Confirm no combatant enters the inn and the tactical camera never exposes its protected interior.
- Complete victory and controlled startup failure paths; both must restore exploration and portal access correctly.

## Task 13: Add production validators

### Files

- Create `Assets/Editor/MercatoVecchioProductionValidator.cs` and its meta file.
- Create `Assets/Editor/SeamlessInteriorStandardValidator.cs` and its meta file.
- Modify `Assets/Editor/HybridZoneStandardValidator.cs`.
- Extend existing inn, urban terrain, weather, and Limbo validators only where integration checks belong there.

### Implementation

1. Validate required landmarks, roots, anchors, routes, player, camera, confiner, weather, surface, river boundary, and production materials.
2. Validate aisle clearance through sampled paths and doorway-width probes.
3. Validate seamless-interior IDs, anchors, module readiness, forbidden duplicate systems, camera profile, occlusion groups, shadow casters, lighting volume, audio blend, save fallback, and battle blocker.
4. Validate no protected interior is included in ordinary outdoor combat cells or spawn regions.
5. Validate Crier sites and NPC/world-state anchors against production IDs.
6. Validate rebuild idempotence and reject legacy test-zone roots or retired scene-load inn exits.
7. Update hybrid-zone classification so the embedded safe inn is a protected sub-location inside a valid hybrid exterior, not an invalid combat configuration.

### Verification

- Run every validator after two consecutive rebuilds.
- Deliberately break one required anchor, one portal ID, one shadow caster, one route, and one tactical blocker; require actionable failures.
- Restore the authored state and require a clean validation pass.

## Task 14: Runtime playtest matrix and visual review

### Exploration matrix

1. Enter from Signoria and walk to the Loggia, fountain, inn, riverfront, and Ponte exit.
2. Enter from Ponte and traverse primary and secondary market aisles.
3. Enter and exit the inn at least ten times, including stopping and reversing inside the threshold.
4. Use the inn counter and traverse every first-floor room.
5. Save/load outside, inside, and at the threshold.
6. Test clear day, rain, storm/lightning, dusk, and night.
7. Activate representative early and severe Limbo presentation layers.

### Battle matrix

1. Trigger the Crier encounter near the fountain.
2. Trigger it from the west stalls and southern route.
3. Confirm civilians evacuate and the inn portal locks.
4. Exercise movement, cover, line of sight, height, and camera framing around stalls, fountain, Loggia, buildings, and river wall.
5. Complete victory and verify full restoration.
6. Probe controlled failure after validation but before battle activation.
7. Start a second encounter in the same Play session to detect leaked state.

### Visual evidence

- Capture matched exploration views against the reference composition.
- Capture exterior-to-interior doorway sequences.
- Capture clear, rainy, night, and lightning views inside and outside the inn.
- Capture exploration and tactical views of the same market terrain.
- Review silhouette clarity, market density, route readability, prop repetition, roof/wall fading, light containment, water animation, tactical cover, and map-edge concealment.

### Runtime acceptance

- Unity reports zero new errors or warnings attributable to the implementation.
- No duplicate Player, Main Camera, EventSystem, weather authority, battle kit, portal registry, or ambience player exists.
- Frame-time and renderer counts remain within the project's current urban-zone budget; profile before optimizing if the production scene regresses performance.

## Task 15: Documentation, commit discipline, and handoff

### Files

- Update `Docs/MASTER_PROJECT_MEMORY.md` with verified implementation facts only.
- Record final asset paths, menu commands, validators, runtime evidence, scene ownership, save-version behavior, and known follow-up work.

### Implementation

1. Refresh Unity and allow all imports and compilation to settle.
2. Run standard diagnostics on every new or modified C# file.
3. Run the Mercato, seamless-interior, inn, hybrid-zone, urban terrain, weather, Limbo, Crier, save, and camera validators.
4. Complete the playtest matrix and review visual evidence.
5. Run `git diff --check` on hand-authored text files.
6. Review `git status --short` and stage only approved implementation files, deterministic generated outputs, documentation, and their meta files.
7. Commit the verified implementation with a focused message such as `Rebuild Mercato with seamless Florentine inn`.
8. Do not push unless explicitly requested.

## Completion Criteria

- Mercato is a faithful production translation of the approved reference rather than a dressed test zone.
- The Loggia, fountain, market rows, inn, riverfront, Ponte route, and Signoria route are recognizable, walkable, and camera-safe.
- The production kit and scene rebuild deterministically without duplicate assets or manual scene repair.
- The completed Florentine Inn first floor is preserved as a reusable module and can be entered and exited without loading another scene.
- Camera, wall/roof occlusion, shadow casting, lighting, weather, audio, save/load, NPC activation, and threshold recovery work across the seamless transition.
- Small/medium accessible buildings now have a reusable project standard; the Duomo remains a dedicated-zone exception.
- Mercato exploration and tactical combat use the same visible geometry, and visible props agree with tactical movement, cover, height, and line-of-sight rules.
- Civilians evacuate, the inn is protected during outdoor battle, and victory/failure restore exploration completely.
- Existing Circle, Limbo, Crier, NPC memory, discovery, and irreversible world-state records remain compatible.
- Runtime verification and all required validators complete with zero new attributable errors or warnings.
