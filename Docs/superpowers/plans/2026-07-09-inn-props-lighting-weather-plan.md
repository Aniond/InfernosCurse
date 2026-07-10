# Inn Props, Lighting, and Weather Implementation Plan

## Goal

Replace the first-floor inn's primitive prop placeholders with a fully dressed hybrid prop kit, add an animated courtyard fountain, prepare reusable upstairs bed/rest behavior, and implement a shared window-environment system so the inn and every applicable existing building reflect the same persistent world time and weather without changing approved layouts.

## Phase 1: Runtime-System and Scene Audit

1. Inspect `Assets/Editor/FlorentineInnFloor1Builder.cs` and `Assets/Scenes/FlorentineInnFloor1.unity` for every prop placeholder, transform, collider, and room parent.
2. Capture baseline top, isometric, and gameplay-camera images.
3. Locate the existing persistent world clock, time-of-day, weather, rest-benefit, scene-transition, save-state, and player-placement systems.
4. Record their public access patterns, state enums, events, save keys, and scene lifecycle behavior.
5. Identify existing window, rain, storm-lightning, fountain-water, particle, and ambient-audio implementations that can be reused.
6. Verify the current entrance, counter, doorway, courtyard, dining, and service routes before modifying props.
7. Inventory scenes and reusable building prefabs with window surfaces, beginning with Ponte Vecchio, Mercato Vecchio, Fiesole, Piazza della Signoria, the street-template building set, Salone delle Arti, Duomo-related interiors, and gardener or service buildings.
8. For each candidate, record whether the correct integration point is a shared prefab, a scene-local window group, or a documented skip because no usable window surface exists.

Verification: a written asset/runtime inventory exists, world-state integration points are identified, every applicable building has a proposed integration or skip reason, and Unity reports zero baseline errors.

## Phase 2: Asset Inventory Search and Selection

1. Confirm Asset Inventory indexing status and custom MCP tool availability after restart.
2. Search indexed packages and current project assets for period-appropriate tables, chairs, benches, counter pieces, shelves, bookcases, chests, barrels, crates, rugs, beds, linens, cookware, ceramics, candles, books, luggage, plants, buckets, planters, and fountain components.
3. Reject modern, ornate late-Renaissance, high-fantasy, or stylistically inconsistent assets.
4. Evaluate candidate scale, URP compatibility, polycount, texture quality, collider suitability, and license/source metadata.
5. Import selected candidates into an isolated staging folder or tooling sandbox.
6. Produce a room-by-room keep/create list for user review before paid generation.

Verification: every required prop category has either an approved existing asset or a documented custom-creation requirement.

## Phase 3: Hybrid Prop Kit Production

1. Create `Assets/Environment/FlorentineInnFloor1/PropKit` with `Meshes`, `Prefabs`, `Materials`, `Textures`, `Audio`, and `VFX` subfolders.
2. Normalize selected assets to Unity metres, consistent forward axes, sensible bottom-centre pivots, and simple colliders.
3. Create missing signature assets through 3D AI Studio or UModeler X, gating generated previews with user approval.
4. Build reusable prefab families for furniture, storage, tabletop dressing, kitchen dressing, office dressing, plants, luggage, and courtyard dressing.
5. Create two to four deterministic tabletop and shelf-dressing variants to avoid visible repetition.
6. Standardize materials to the approved dark chestnut, terracotta, ceramic, linen, iron, brass, leather, parchment, and plant palette.
7. Register approved prefabs in a GSpawn library named `Florentine Inn Prop Kit` while the initialized tooling sandbox is active.

Verification: prop prefabs render correctly under URP, have appropriate colliders, and can be placed without manual scale correction.

## Phase 4: Room Dressing in the Sandbox

1. Recreate representative reception, salon, dining, kitchen, office, and courtyard bays in the tooling sandbox.
2. Validate camera readability, silhouette variety, material cohesion, and prop density at the gameplay-camera distance.
3. Check that small items remain visible without producing visual clutter.
4. Confirm every interactive route has conservative clearance around furniture and dressing.
5. Capture comparison views and obtain user approval before changing the production builder.

Verification: each room bay is approved and has a deterministic placement list.

## Phase 5: Rest Bed Prefab and State Logic

1. Build a reusable upstairs bedroom kit containing a period bed, bedside chest, wash basin, linens, and a named wake-point transform.
2. Add `InnBedInteraction` using the project's existing `WorldInteractable` pattern.
3. Integrate with the existing persistent rest-benefit state rather than storing eligibility only on the bed instance.
4. Ensure a valid rest grants benefits once per eligible cycle.
5. Ensure repeated bed use may play the rest presentation but cannot duplicate benefits.
6. Add assigned-bed and wake-point support to the rest flow without activating it on floor 1.
7. Preserve the existing counter rest/wake behavior until the second-floor scene is built.

Verification: edit/runtime tests prove benefit consumption persists across scene reload and future wake placement resolves to the assigned bed when enabled.

## Phase 6: Animated Fountain

1. Build the upgraded fountain prefab while preserving its existing footprint.
2. Create an animated water material with restrained UV movement.
3. Add falling-water meshes or particles, impact ripples/foam, and spatial looping water audio.
4. Prevent fountain particles from entering rooms or blocking gameplay.
5. Reduce or pause expensive effects while offscreen when supported by the existing VFX pattern.

Verification: surface, flow, ripple, and audio loop seamlessly; the courtyard route remains clear; no runtime allocations or errors accumulate during repeated observation.

## Phase 7: Shared Windows and Persistent Time

1. Add dimensionally appropriate window prefabs to approved exterior wall positions without changing doorway or collider layouts.
2. Create exterior light cards, sky treatment, or limited exterior geometry so windows never reveal empty space.
3. Implement a reusable `WorldWindowEnvironment` adapter that reads, but does not advance, the persistent world clock.
4. Create a per-building configuration component for window renderers, exterior-effect anchors, optional interior lights, and profile overrides.
5. Drive the inn courtyard sun direction, intensity, color, shadows, ambient light, window exterior presentation, and interior lamp intensity from that state.
6. Add time-driven emissive and exterior-view response to audited exterior building windows and playable interiors, preferring shared-prefab integration where it correctly propagates to multiple maps.
7. Tune dawn, noon, sunset, and night profiles for readability and continuity with outdoor maps.
8. Confirm scene entry and exit preserve the current hour and that no building creates an independent clock.

Verification: forced dawn/noon/sunset/night states produce distinct correct visuals in the inn and representative shared/exterior building windows, and the state remains unchanged across scene transitions.

## Phase 8: Persistent Weather Through Windows

1. Extend `WorldWindowEnvironment` with the global weather state and events rather than creating a second weather owner.
2. Support clear, cloudy, rain, storm, and fog presentations.
3. Add exterior-only rain particles, glass streak/droplet treatment, weather-aware ambient color, and weather audio routing.
4. Synchronize storm lightning window flashes and temporary interior illumination across all configured buildings to the outdoor lightning event/state.
5. Ensure precipitation is never visible inside rooms.
6. Degrade safely to clear daytime when the persistent services are unavailable, logging at most one actionable warning.
7. Apply weather response to every audited usable window integration, again preferring shared building prefabs where appropriate.
8. Record concrete skip reasons for buildings that do not expose a usable window surface; do not remodel them merely to add the effect.

Verification: weather changes update without scene reload; exterior and interior windows agree on the current state; lightning flashes remain synchronized across scenes and buildings; entering/leaving preserves weather; no indoor precipitation appears.

## Phase 9: Builder Integration

1. Add focused prop-prefab loading and deterministic placement helpers to `FlorentineInnFloor1Builder.cs`.
2. Replace primitive helpers for approved furniture and prop categories with prefab instances.
3. Retain approved transforms where they preserve layout and interaction clearances.
4. Add explicitly authored small-dressing variants rather than uncontrolled random placement.
5. Instantiate the fountain, windows, shared window-environment adapter, and inn configuration through the builder.
6. Keep second-floor bedroom assets out of the production first-floor hierarchy.
7. Rebuild the inn twice and compare the resulting prop hierarchy and placements.
8. Apply configuration to audited building prefabs and scene-local window groups without changing their structural geometry.
9. Rebuild or resave affected builder-authored scenes through their authoritative builders so integrations survive regeneration.

Verification: repeat builds are deterministic, no placeholders return, all references resolve, and shared-prefab integrations remain present in every consuming scene.

## Phase 10: Runtime and Visual Validation

1. Playtest entrance-to-counter, counter interaction, all public doorways, dining/service routes, courtyard routes, camera fading, and locked landing.
2. Probe prop collisions with normal traversal and route-centre physics checks.
3. Test fountain animation/audio over multiple loops.
4. Test dawn, noon, sunset, and night profiles.
5. Test clear, cloudy, rain, storm, and fog profiles, including repeated state changes.
6. Test storm-lightning synchronization.
7. Test persistent time/weather across inn entry and exit.
8. Test bed-benefit state and future wake placement through direct runtime harnesses without opening floor 2.
9. Test representative exterior building windows and every playable interior with forced dawn, noon, sunset, night, clear, rain, storm, and fog states.
10. Confirm a single storm-lightning event reaches all loaded configured windows without duplicate random flashes.
11. Verify every audited building has either a working integration or a documented no-usable-window skip reason.
12. Capture final top, isometric, gameplay-camera, inn comparison, and cross-building time/weather images.
13. Confirm zero Unity console errors and warnings after a complete playthrough.

## Phase 11: Delivery

1. Review the dressed inn and time/weather comparisons with the user.
2. Apply approved corrections.
3. Update `Docs/MASTER_PROJECT_MEMORY.md` with the completed pass and commit IDs.
4. Commit only the prop kit, approved generated/imported assets, shared environment scripts and profiles, building configurations, builder changes, rebuilt scenes, and required configuration data.
5. Push only when explicitly requested.

## Safety and Scope Controls

- Do not alter the approved structural layout or floor-plan coordinates.
- Do not build or open the second floor.
- Do not replace NPC capsules in this pass.
- Do not add loot, quests, or interactions to decorative props.
- Do not remodel unrelated buildings or invent windows solely to include them in the environment pass.
- Do not add scene-local clocks, weather generators, or independent lightning timers.
- Do not store API keys in files, Unity assets, logs, or source control.
- Preserve unrelated dirty working-tree changes and stage explicit paths only.
- Obtain user approval before paid asset generation or importing a visually significant asset set.
