# Gugol Mappe Refinement and Street View Implementation Plan

## Objective

Refactor and refine the existing Gugol Mappe overlay into the approved masthead-free, map-first interface; preserve current city, regional, world, routing, travel, weather, and encounter behavior; add knowledge-aware search and authored world-state presentation; and introduce a scalable Street View information layer where the player can select a named street to find known shops, entrances, services, and NPCs last known there.

Design source: `Docs/superpowers/specs/2026-07-11-gugol-mappe-refinement-and-street-view-design.md`

## Safety Boundaries

- Preserve the unrelated dirty worktree documented in `Docs/MASTER_PROJECT_MEMORY.md`.
- The current working tree already modifies `Assets/Editor/GugolMappeSetup.cs`, `Assets/Scripts/Map/GugolMapPin.cs`, `Assets/Scripts/Map/GugolUi.cs`, `Assets/Scripts/Curse/HubMap.cs`, `Assets/Scripts/Curse/HubNode.cs`, `Assets/Scripts/UI/SaveSystem.cs`, and related Limbo/world-state files. Inspect every overlap before editing and stage explicit paths only.
- Preserve `HubMap`, `MapRouting`, `DistrictTracker`, `TravelIntent`, `FlorenceWeather`, `EncounterRoll`, `PendingEncounter`, and `ZoneEntryPoint` as the current travel authorities.
- Preserve current City, Region, and World serialized `MapLevel` values. Street View uses a new map-view state and must not insert a value into the existing serialized enum.
- Preserve the current hidden-state contract: no numeric Circle value, Circle tier, derived rating, tint, label, review count, search rank, or route treatment may reveal hidden influence.
- Keep player Sanity completely outside all map assemblies, data contracts, snapshots, presenters, validators, and art selection.
- Do not expose live NPC transforms or simulation-only positions. Street View reports only player-known, last-known, or authored usual locations.
- Do not add 360-degree panoramas, live scene cameras, a duplicate 3D city, or real web-map service integration.
- Do not edit imported Asset Store package source.
- Do not push unless explicitly requested.

## Task 1: Capture the current map baseline

### Files and systems

- Read the complete current versions of:
  - `Assets/Scripts/Map/GugolMapUI.cs`
  - `Assets/Scripts/Map/GugolMapPin.cs`
  - `Assets/Scripts/Map/GugolDirectionsCard.cs`
  - `Assets/Scripts/Map/GugolRouteRenderer.cs`
  - `Assets/Scripts/Map/GugolUi.cs`
  - `Assets/Scripts/Map/MapRouting.cs`
  - `Assets/Scripts/Curse/HubMap.cs`
  - `Assets/Scripts/Curse/HubNode.cs`
  - `Assets/Editor/GugolMappeSetup.cs`
- Inspect the generated `GugolMapUI` component on `Assets/Resources/GameSystems.prefab` without saving unrelated prefab churn.
- Inspect the current assets under `Assets/UI/Map` and `Assets/Art/Map`.

### Implementation procedure

1. Record every serialized field on the existing `GugolMapUI` component before refactoring.
2. Record the current hierarchy produced by `BuildUI`, including the map rect, route layer, pin layer, search bar, wordmark, zoom control, attribution, and directions-card host.
3. Record the current City, Region, and World backgrounds, pin positions, visible nodes, search behavior, weather glyphs, player marker, route rendering, fare calculations, in-zone jumps, and road encounter behavior.
4. Capture screenshots at the supported reference resolution and representative 16:9, 16:10, ultrawide, and safe-area-constrained resolutions.
5. Run the existing map flow from Mercato Vecchio: open, select current district, route within Florence, zoom to Tuscany and Italy, close, reopen, and cancel from a `ZoneExit`.
6. Record pre-existing console errors or warnings before implementation.

### Verification

- A written baseline identifies behavior that must survive the presentation refactor.
- Current serialized references are sufficient to create an explicit migration or compatibility path.
- No inspection step saves a scene, prefab, or package asset.

## Task 2: Add stable Street View and venue authoring contracts

### Files

- Create `Assets/Scripts/Map/GugolMapViewKind.cs` and its meta file.
- Create `Assets/Scripts/Map/GugolStreetDefinition.cs` and its meta file.
- Create `Assets/Scripts/Map/GugolVenueDefinition.cs` and its meta file.
- Create `Assets/Scripts/Map/GugolNpcMapDefinition.cs` and its meta file.
- Create `Assets/Scripts/Map/GugolMapPresentationProfile.cs` and its meta file.
- Create `Assets/Scripts/Map/GugolMapWorldExpressionDefinition.cs` and its meta file.
- Create `Assets/Editor/GugolMapAuthoringValidator.cs` and its meta file.

### Contracts

1. Add a new append-only `GugolMapViewKind` with City, Region, World, and Street values. Convert to and from the existing `MapLevel` without modifying `MapLevel` serialization.
2. `GugolStreetDefinition` owns:
   - Stable street ID, display name, parent city ID, and optional district IDs.
   - A discovery ID and minimum visible stage.
   - Normalized City-layer centerline and selectable hit width or polygon.
   - Street View framing bounds and optional high-detail background.
   - Ordered venue IDs, route fallback point, and authored label priority.
3. `GugolVenueDefinition` owns:
   - Stable venue ID, display name, category, street ID, and discovery contract.
   - Normalized Street View anchor and optional frontage/entrance art.
   - Scene, sub-location, and `ZoneEntryPoint` destination references where applicable.
   - Authored services, opening-hours presentation, site ID, and optional outcome-expression references.
4. `GugolNpcMapDefinition` owns stable NPC identity, display name, optional portrait, discovery contract, usual street or venue, and authored schedule phrases. It does not own a live transform.
5. `GugolMapPresentationProfile` owns map backgrounds, scale-control art, parchment palette, route/selection colors, label priorities, reduced-motion settings, and safe fallback sprites.
6. `GugolMapWorldExpressionDefinition` maps approved warning, environmental-presentation, site-outcome, remembered, and weather IDs to map art, label treatment, and feature visibility. It contains no numeric Circle threshold.
7. Put project-owned definition assets under `Assets/Resources/GugolMap/Streets`, `Venues`, `Npcs`, `Presentation`, and `WorldExpressions` with required folder meta files.

### Verification

- Validator rejects empty or duplicate IDs, invalid normalized geometry, missing parent records, duplicate venue frontage, invalid discovery references, missing route fallbacks, and missing fallback art.
- Validator rejects a world-expression definition that names a numeric Circle field, player Sanity type, or unsupported expression ID.
- Loading definitions twice produces the same registry without duplicate records.

## Task 3: Add persistent last-known NPC map knowledge

### Files

- Create `Assets/Scripts/Map/GugolNpcMapKnowledge.cs` and its meta file.
- Create `Assets/Scripts/Map/GugolNpcSightingReporter.cs` and its meta file.
- Modify `Assets/Scripts/UI/SaveSystem.cs`.
- Modify `Assets/Scripts/Narrative/WorldEventLedger.cs` only if authored consequences require an append-only NPC-map-knowledge effect.
- Create `Assets/Editor/GugolNpcMapKnowledgeValidator.cs` and its meta file.

### Data contract

1. Add `GugolNpcMapKnowledgeRecord` with:
   - Stable NPC ID.
   - Last-known street ID and optional venue ID.
   - In-world observation day key and time band.
   - Knowledge source type and stable source ID.
   - Whether the record is a direct sighting, conversation, authored rumor, or usual-location fallback.
2. Add `GugolNpcMapKnowledgeLedger` with validated register, record, query, export, import, and reset operations.
3. Store only player knowledge. Never query an NPC GameObject, world-agent record, schedule service, or scene transform while building a map result.
4. New direct sightings update the record through an explicit `GugolNpcSightingReporter` attached to an authored NPC or interaction. The reporter requires a stable street ID and does not infer a street from world coordinates.
5. Conversations and events can update knowledge through one explicit adapter or append-only World Event effect. Keep the World Event batch transactional if an effect is added.
6. Format recency as authored in-world phrases such as `seen this morning`, `seen yesterday`, or `usually works here`. Do not show technical timestamps.
7. Increment save data from v7 to v8 and add the NPC map-knowledge array. Earlier saves initialize an empty ledger and continue to use authored usual locations where discovered.
8. Last-known knowledge is save-persistent but not Campaign-Permanent. Loading a save may restore that save's knowledge without rewriting irreversible Chronicle facts.

### Verification

- Direct sighting, conversation, rumor, overwrite, stale record, export/import, reset, and malformed-record cases pass.
- An older save loads with an empty valid knowledge ledger.
- A v8 save round-trips every field without duplicate NPC records.
- Repository validation proves the map-knowledge ledger never reads `Transform`, `PersistentWorldAgentRecord.currentSiteId`, hidden Circle values, or player Sanity.

## Task 4: Build a read-only map knowledge snapshot

### Files

- Create `Assets/Scripts/Map/GugolMapKnowledgeSnapshot.cs` and its meta file.
- Create `Assets/Scripts/Map/GugolMapKnowledgeService.cs` and its meta file.
- Create `Assets/Scripts/Map/GugolMapSearchIndex.cs` and its meta file.
- Modify `Assets/Scripts/Narrative/ExplorationDiscovery.cs` only if a read-only record query is missing.
- Modify `Assets/Scripts/Narrative/CircleWarningLedger.cs` only if a read-only warning query is missing.

### Implementation

1. Build one immutable snapshot when the map opens and rebuild it only after an authoritative knowledge, outcome, warning, weather, or day change.
2. The snapshot may consume:
   - `ExplorationDiscoveryLedger` stages and authored discovery presentation.
   - `SiteOutcomeState` records.
   - Visible `CircleWarningLedger` stages and authored symptom IDs.
   - `FlorenceWeather` condition and flood-risk presentation.
   - `GugolNpcMapKnowledgeLedger` records.
   - Authored venue schedules and stable travel data.
3. The snapshot must not consume `HubMap.GetInfluence`, `HubNode.circleInfluence`, `CircleExpressionProfile.Evaluate`, Insanity state, or any numeric corruption representation.
4. Resolve every feature into Hidden, Rumored, Known, LastKnown, Lost, Forgotten, or RememberedLoss presentation.
5. Build the search index only from records the player is allowed to know. Rumored content uses its authored clue label rather than a hidden proper name.
6. Include stable search-result IDs and types so street, venue, NPC, and location results can select the correct view and focus target.
7. Return neutral fallback presentation when optional art or expression data is absent.

### Verification

- Snapshot tests cover all seven knowledge/presentation states.
- Hidden names and IDs never appear in search results, placeholder text, tooltips, or debug-facing UI strings.
- Changing numeric Circle influence alone produces an identical snapshot.
- Adding an approved warning or site outcome changes only the authored presentation records.
- Player Sanity changes produce byte-for-byte identical map snapshots.

## Task 5: Refactor `GugolMapUI` into a coordinator

### Files

- Modify `Assets/Scripts/Map/GugolMapUI.cs`.
- Create `Assets/Scripts/Map/GugolMapLayerPresenter.cs` and its meta file.
- Create `Assets/Scripts/Map/GugolMapFeaturePresenter.cs` and its meta file.
- Create `Assets/Scripts/Map/GugolMapSelectionState.cs` and its meta file.
- Create `Assets/Scripts/Map/GugolMapCardHost.cs` and its meta file.
- Modify `Assets/Scripts/Map/GugolDirectionsCard.cs` only to make it a card-host client.
- Modify `Assets/Scripts/Map/GugolRouteRenderer.cs` only behind a stable route-presenter interface.

### Implementation

1. Keep `GugolMapUI` authoritative for open, close, pause, input ownership, travel lock, selected view, and coordinator lifecycle.
2. Move background selection, rect fitting, active layer, transition state, and focus bounds to `GugolMapLayerPresenter`.
3. Move pin, waypoint, street, venue, NPC, player marker, weather glyph, and pooled feature lifecycle to `GugolMapFeaturePresenter`.
4. Represent selected view, focused street, selected feature, and back-navigation as plain `GugolMapSelectionState` data.
5. Use `GugolMapCardHost` to enforce one visible card. Existing travel/current-area content remains in `GugolDirectionsCard`; street, venue, and NPC content receives focused presenters without nesting panels.
6. Preserve `MapRouting`, travel timing, fares, `EncounterRoll`, `PendingEncounter`, in-zone jumps, and scene-load behavior unchanged during this task.
7. Add a temporary compatibility path that can render the current UI from the new presenters before visual changes begin.
8. Unsubscribe every event and stop every transition when the map closes, the component disables, or the GameSystems object is destroyed.

### Verification

- The compatibility presentation reproduces every baseline behavior from Task 1.
- Open/close cycles do not duplicate canvases, event systems, pins, routes, listeners, or coroutines.
- Region road encounters, fare failures, destination travel, `ZoneExit` cancel rearming, and time-scale restoration remain unchanged.
- `GugolMapUI.cs` no longer directly builds every feature and card element.

## Task 6: Apply the approved map-first visual system

### Files and assets

- Modify `Assets/Scripts/Map/GugolMapUI.cs` and the new presenters.
- Modify `Assets/Scripts/Map/GugolUi.cs`.
- Modify `Assets/Scripts/Map/GugolMapPin.cs` or replace its presentation internals behind the same selection contract.
- Create refined UI art under `Assets/UI/Map/Refined` with meta files.
- Modify `Assets/Editor/GugolMappeSetup.cs` to assign the new presentation profile and art deterministically.

### Implementation

1. Remove `BuildWordmark` from the active browsing hierarchy. Do not replace it with another large title, crest, or decorative masthead.
2. Put search and compact City/Tuscany/Italy controls in the top safe area. The controls represent view state directly; retain plus/minus only if usability testing proves a separate accessibility need.
3. Give the map the remaining dominant area and keep one bottom context-card region.
4. Replace the hard-coded visual constants scattered through the UI with `GugolMapPresentationProfile` values and assigned sprites.
5. Preserve Cinzel for restrained headings and EB Garamond for body copy, with outline/backplate treatment sufficient for every supported map background.
6. Use restrained parchment, sepia, terracotta, olive, desaturated river blue, active-route blue, and wax-seal red. Do not copy a real Google logo or brand treatment.
7. Use category icon plus label or shape so meaning never depends on color alone.
8. Keep attribution small and noncompetitive with the map.
9. Add a safe-area container and resolution-aware card sizing without assuming the phone mockup is the shipping aspect ratio.

### Verification

- No `Wordmark` object exists in the generated browsing hierarchy.
- Search and scale controls fit within all tested safe areas.
- The map remains the largest visual region at 16:9, 16:10, ultrawide, and constrained resolutions.
- Every active label, route, marker, and card passes visual readability review from the normal gameplay viewing distance.

## Task 7: Add selectable City streets and Street View transitions

### Files

- Create `Assets/Scripts/Map/GugolStreetFeatureGraphic.cs` and its meta file.
- Create `Assets/Scripts/Map/GugolStreetViewPresenter.cs` and its meta file.
- Create `Assets/Scripts/Map/GugolMapTransitionController.cs` and its meta file.
- Modify `Assets/Scripts/Map/GugolMapLayerPresenter.cs`.
- Modify `Assets/Scripts/Map/GugolMapFeaturePresenter.cs`.

### Implementation

1. Render named selectable streets from normalized authored centerlines. Use a custom UI graphic or equivalent pooled geometry with a separate, forgiving hit width.
2. Label only streets allowed by the active density budget and current knowledge snapshot.
3. Mouse click selects a street directly. Keyboard/gamepad focus chooses the nearest valid street in the requested navigation direction and confirms through the standard submit action.
4. Selecting a street stores City focus, animates into its Street View bounds, and builds only that street's known frontage.
5. Back returns to the same City focus and selection. Back again follows the existing card-close/map-close hierarchy.
6. Street View uses its optional high-detail sheet when present and a magnified City crop plus neutral frontage overlay when absent.
7. Street View shows known venues, usable entrances, known NPC records, weather, and authored outcomes. It never instantiates or renders the gameplay scene.
8. Missing or invalid street geometry leaves the street visible but nonselectable and reports one actionable authoring error.

### Verification

- Thin visual streets remain easy to select without overlapping unrelated streets.
- Repeated City-to-Street-to-City transitions preserve focus and do not rebuild unrelated scales.
- Missing high-detail art uses the documented fallback.
- A missing street definition cannot expose hidden venues or crash selection.

## Task 8: Author the Mercato Vecchio Street View pilot

### Files and assets

- Create street, venue, NPC, presentation, and world-expression assets under `Assets/Resources/GugolMap`.
- Create Mercato Street View art under `Assets/UI/Map/Refined/Streets/MercatoVecchio` with meta files.
- Modify `Assets/Editor/GugolMappeSetup.cs` or create `Assets/Editor/GugolMappeMercatoAuthoringBuilder.cs` and its meta file.
- Read stable entrances and sub-location IDs from the production Mercato and seamless inn authoring; do not infer them from scene names.

### Pilot content

1. Author the Mercato square/perimeter and Via Calimala as distinct selectable street records only after reconciling their bounds with the production map and reference.
2. Add the Albergo Fiorentino as the first venue with:
   - Street frontage.
   - `albergo_fiorentino` building ID.
   - `albergo_fiorentino_floor1` sub-location ID.
   - Valid entrance or street fallback target.
   - Inn service and open/closed presentation.
3. Add representative known commerce and public services already present in Mercato authoring. Do not invent a production business merely to fill the map.
4. Add map definitions for representative authored NPCs such as the innkeeper, baker, stallholder, or record clerk only where stable identity and discovery records already exist.
5. Add explicit sighting reporters or interaction hooks for the selected pilot NPCs.
6. Ensure the Crier can appear only through discovered, authored knowledge and never through its live hidden simulation position.
7. Provide neutral, warning, lost, forgotten, and remembered-loss expression fixtures without triggering production story events.

### Verification

- Selecting the authored Mercato street shows the inn, known services, and only eligible NPC records.
- Selecting the inn highlights its frontage and provides a valid route to its entrance or street fallback.
- An undiscovered NPC and venue are absent from Street View and search.
- A rumor produces an approximate or clue presentation without revealing the proper destination.
- The embedded inn remains a seamless gameplay destination; the map does not load a separate legacy inn scene.

## Task 9: Expand search and context cards across feature types

### Files

- Modify `Assets/Scripts/Map/GugolMapSearchIndex.cs`.
- Create `Assets/Scripts/Map/GugolStreetCard.cs` and its meta file.
- Create `Assets/Scripts/Map/GugolVenueCard.cs` and its meta file.
- Create `Assets/Scripts/Map/GugolNpcCard.cs` and its meta file.
- Modify `Assets/Scripts/Map/GugolMapCardHost.cs`.
- Modify `Assets/Scripts/Map/GugolDirectionsCard.cs` where shared actions require it.

### Implementation

1. Search known locations, streets, venues, services, and NPC knowledge through one immutable index.
2. Keep nonmatching visible features dimmed where that preserves spatial context; never dim or render hidden features.
3. A street result opens its Street View.
4. A venue result opens its street, highlights the frontage, and offers Directions and Remember actions.
5. An NPC result opens the last-known street, highlights the associated venue or street area, and displays recency or usual-location wording.
6. A stale last-known record remains explicitly stale; it is never promoted to current.
7. An empty result reports that no known record matches and offers no hidden autocomplete.
8. Preserve current location travel cards, fare errors, weather glyphs, ratings, and route actions unless the approved design explicitly replaces their layout.

### Verification

- Feature-type search and selection tests pass for known, rumored, hidden, stale, lost, and forgotten records.
- Searching an undiscovered proper name returns no result.
- Keyboard/gamepad search submit focuses exactly one stable result.
- Only one card is visible at any time, and closing it clears only its route/selection state.

## Task 10: Add authored world-state and weather presentation

### Files

- Create `Assets/Scripts/Map/GugolMapWorldStatePresenter.cs` and its meta file.
- Create `Assets/Scripts/Map/GugolMapWeatherPresenter.cs` and its meta file.
- Modify `Assets/Scripts/Map/GugolMapFeaturePresenter.cs`.
- Create required overlay sprites/materials under `Assets/UI/Map/Refined/WorldState` with meta files.

### Implementation

1. Convert only snapshot expression IDs into visual treatment.
2. Support neutral, neglected, lost, forgotten, and remembered-loss treatments for locations and Street View frontage.
3. Forgotten treatment may suppress a label or illustration only when the snapshot explicitly authorizes it.
4. Remembered-loss treatment can restore the canonical name and remembrance copy while preserving irreversible damaged art.
5. Add localized rain stippling, mist, cloud shadow, flood notation, and clear-state presentation at the relevant scale.
6. Keep route, selection, player location, and essential labels above weather overlays.
7. Update overlays only when the snapshot or weather state changes; do not poll hidden world values every frame.
8. Add a source-code validator that forbids `HubMap.GetInfluence`, `CircleExpressionProfile.Evaluate`, `PlayerInsanityState`, `InsanityPresenter`, or equivalent raw-state dependencies in map presentation files.

### Verification

- Numeric Circle changes alone do not alter a map screenshot or serialized visual state.
- Approved warning/outcome records produce the exact authored overlays.
- Healthy, lost, forgotten, and remembered-but-still-lost Giardino fixtures remain visually distinct without showing a meter.
- Rain, mist, flood, and clear weather do not obscure active routes or selected entrances.

## Task 11: Add restrained motion, input completeness, and performance limits

### Files

- Modify `Assets/Scripts/Map/GugolMapTransitionController.cs`.
- Modify `Assets/Scripts/Map/GugolMapFeaturePresenter.cs`.
- Modify `Assets/Scripts/Map/GugolRouteRenderer.cs`.
- Modify `Assets/Scripts/Map/GugolMapUI.cs` for input routing.

### Implementation

1. Animate City, Region, World, and Street changes through position/scale/redraw transitions using unscaled time.
2. Animate route appearance as fresh ink along the authoritative path; retain skip-to-end behavior during travel.
3. Lift and shadow only the selected pin or venue marker.
4. Use localized, nonlooping reveal motion for irreversible world losses.
5. Add a reduced-motion path that uses short crossfades or immediate state changes while preserving selection feedback.
6. Pool pins, street graphics, labels, venue markers, NPC markers, weather glyphs, and route dots.
7. Keep only the active layer and its transition partner active.
8. Cap label density per scale and priority.
9. Ensure search focus, cancel hierarchy, scale controls, Street back-navigation, card actions, and map close work with mouse, keyboard, and gamepad.

### Verification

- Opening and browsing the map creates no recurring garbage spike after pools warm.
- One hundred repeated view transitions do not increase pooled object counts or subscriptions.
- Reduced-motion mode contains no continuous ornamental animation.
- Input parity passes for all supported devices.
- Route travel and time-scale restoration remain deterministic at `Time.timeScale == 0`.

## Task 12: Deterministic setup, migration, and production validation

### Files

- Modify `Assets/Editor/GugolMappeSetup.cs`.
- Modify `Assets/Resources/GameSystems.prefab` only through the deterministic setup path.
- Complete `Assets/Editor/GugolMapAuthoringValidator.cs`.
- Complete `Assets/Editor/GugolNpcMapKnowledgeValidator.cs`.
- Create `Assets/Editor/GugolMapFinalValidator.cs` and its meta file.
- Create `Assets/Scripts/Validation/GugolMapPlayModeProbe.cs` and its meta file.
- Create `Assets/Editor/GugolMapPlayModeVerifier.cs` and its meta file.

### Implementation

1. Make setup idempotently assign the presentation profile, refined map art, street/venue/NPC registries, fallback sprites, fonts, and travel tuning.
2. Preserve existing serialized travel and encounter values when adding new references.
3. Rebuild the setup twice and confirm no duplicate GameSystems components, assets, listeners, or changed IDs.
4. Add a final validator covering:
   - Stable IDs and graph references.
   - Search visibility and hidden-name exclusion.
   - NPC knowledge import/export and no-live-position rule.
   - Street geometry, view bounds, venue frontage, entrances, and fallbacks.
   - Presentation-profile and fallback-art completeness.
   - Forbidden Circle/Insanity dependencies.
   - Current City/Region/World travel compatibility.
5. Add Play-mode automation for the full Mercato golden path.

### Runtime matrix

1. Open and close from the M key and a `ZoneExit`.
2. Search and select a City location.
3. Zoom City to Tuscany to Italy and back.
4. Select a Mercato street and enter Street View.
5. Select the Florentine Inn, build a route, and resolve its entrance.
6. Find a known NPC by last-known street.
7. Confirm an undiscovered NPC and venue remain absent.
8. Show a rumored location without revealing its precise identity.
9. Exercise neutral, warning, lost, forgotten, and remembered-loss fixtures.
10. Exercise clear, rain, mist, and flood weather.
11. Complete city walking, region fare, road ambush, and in-zone jump flows.
12. Close and reopen after travel cancellation with correct time scale and `ZoneExit` state.

### Verification

- Unity compiles with zero new first-party errors or warnings.
- Every validator passes twice after a clean setup rebuild.
- The final Play-mode console contains no new first-party errors or warnings.
- The final screenshots match the approved masthead-free map-first direction.

## Task 13: Documentation and commit boundaries

### Documentation

- Update `Docs/MASTER_PROJECT_MEMORY.md` with the completed architecture, save v8, authoring paths, Mercato Street View pilot, fallback rules, and verification evidence.
- Update `Docs/AI_HANDOVER_BLUEPRINT.md` only where it describes Gugol Mappe, discovery, NPC knowledge, or hidden world-state presentation.
- Document how future districts add street, venue, NPC, discovery, expression, entrance, and validation records.

### Proposed implementation commits

1. `Refactor Gugol Mappe presentation architecture`.
2. `Add Street View and NPC map knowledge contracts`.
3. `Build Mercato Street View pilot`.
4. `Refine Gugol Mappe visual presentation`.
5. `Add authored map world-state expression`.
6. `Validate refined Gugol Mappe navigation`.
7. `Document Gugol Mappe and Street View workflow`.

For every commit:

- Stage explicit paths only.
- Run `git diff --cached --check`.
- Inspect `git diff --cached --name-only` against the task's intended file list.
- Never bundle unrelated inn, Limbo, package, font, scene, or tooling changes.
- Do not push unless explicitly requested.

## Completion Criteria

- The browsing screen contains no large Gugol Mappe masthead and keeps the map dominant.
- City, Tuscany, and Italy remain fully functional and preserve current routing, travel, fare, weather, and road-encounter behavior.
- A named City street can be selected and opened as a detailed Street View information layer.
- The Mercato pilot exposes the Florentine Inn, authored services, entrances, and eligible NPC last-known records.
- Street View never uses a panorama, live scene camera, duplicate 3D city, or live NPC transform.
- Search reveals only player-known locations, streets, venues, services, and NPC knowledge.
- Rumored content remains approximate; hidden content remains absent.
- Authored warnings, weather, site outcomes, forgetting, and remembrance visibly affect the map without displaying or deriving from numeric Circle values.
- Player Sanity has no dependency path into the map.
- Save v8 round-trips last-known NPC knowledge and migrates earlier saves safely.
- Mouse, keyboard, and gamepad input pass the complete navigation matrix.
- Missing art or authoring data invokes the documented safe fallbacks.
- Setup is deterministic, validators pass, Play mode is clean, and all commits exclude unrelated dirty-tree changes.
