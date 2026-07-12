# Inferno's Curse Master Project Memory

Last updated: 2026-07-11

## Purpose

This is the restart-safe handoff document for continuing work in `C:\UnityGames\InfernosCurse`. Read this file before making project changes after a Codex or Unity restart.

## Project Baseline

- Engine: Unity 6.4, editor `6000.4.11f1`.
- Render pipeline: URP 17.4.
- Repository branch: `main`.
- Local implementation baseline: the verified Circle-territory, seamless Mercato, and refined Gugol Mappe commit immediately follows `8c0574f1 Plan Gugol Mappe refinement and Street View`.
- `origin/main` is at `365e1153`; the later local implementation and design/plan commits have not been pushed.
- Primary project root: `C:\UnityGames\InfernosCurse`.
- The project contains unrelated dirty working-tree changes. Never reset, clean, or discard them without explicit user approval.
- Keep API keys and tokens out of tracked files. The user has provided PixelLab and 3D AI Studio credentials in conversation, but they are intentionally absent from this document and repository.

## Current Inn Direction

Scene: `Assets/Scenes/FlorentineInnFloor1.unity`

Builder: `Assets/Editor/FlorentineInnFloor1Builder.cs`

The inn is the approved Option-B adaptation of `Refrences/maps/inn.png`. Floor 1 is a safe social hub. Floor 2 will be added later and must remain blocked for now.

Approved constraints:

- Preserve the floor plan, doorway widths, collider footprints, and room dimensions.
- Preserve the clear entrance-to-reception-counter route.
- Inn services trigger only by interacting with the reception counter.
- Supported counter input: keyboard interaction, gamepad south button, and mouse click.
- Do not add modern objects. Bookcases are period furniture; televisions are not.
- Current structural art direction is restrained thirteenth-century Florentine: lime plaster, pietra serena, handmade terracotta, courtyard pavers, and dark chestnut timber.

### Seamless accessible-building workflow

Approved on 2026-07-11 as part of the Mercato Vecchio production rebuild:

- Small and medium accessible interiors such as inns, shops, homes, workshops, and guild rooms should normally be embedded or additively streamed within their surrounding zone.
- Entering these buildings should be seamless: no visible scene load, with threshold-driven camera framing, wall and roof occlusion, contained interior lighting, audio blending, NPC activation, and save restoration.
- Interiors remain separately authorable modules even when they share the exterior zone's coordinate space.
- Major landmarks with substantially different scale or presentation remain dedicated zones. The Duomo is the explicit first example.
- The Albergo Fiorentino is the pilot: preserve the completed `FlorentineInnFloor1` work as a reusable interior module inside the rebuilt `MercatoVecchio` zone.
- Ordinary outdoor encounters must not route into protected social interiors; story-authored interior combat requires explicit authorization.
- Full design: `Docs/superpowers/specs/2026-07-11-mercato-vecchio-and-seamless-interiors-design.md`.

Verified implementation state on 2026-07-11:

- `Assets/Editor/MercatoVecchioProductionBuilder.cs` deterministically rebuilds the production square with the Loggia north, fountain plaza center, organized stall rows, west-side Albergo Fiorentino, southern Arno edge, eastern Ponte route, and northwestern Signoria route.
- `Assets/Editor/MercatoVecchioProductionKitBuilder.cs` owns seven reusable production prefabs under `Assets/Environment/MercatoVecchio/ProductionKit`.
- `Assets/Environment/FlorentineInnFloor1/Prefabs/FlorentineInnFloor1_Module.prefab` is the reusable inn module. It has stable IDs `albergo_fiorentino` / `albergo_fiorentino_floor1`, threshold anchors, local lighting and camera control, shadow-preserving occlusion, and protected battle access.
- The production Mercato embeds that module behind the west facade. Entry and exit occur in the active `MercatoVecchio` scene; there is no scene-load handoff.
- Save format v7 records player facing and `subLocationId`. Pre-v7 saves naming `FlorentineInnFloor1` migrate to `MercatoVecchio` plus `albergo_fiorentino_floor1`, with the old inn position remapped through the embedded module transform.
- `InfernosCurse/Validation/Run Mercato Seamless Play Mode Probe` passed five repeated crossings, camera/light state changes, protected battle locking, legacy-save migration, missing-ID recovery, and duplicate-authority checks.
- `InfernosCurse/Validation/Run Limbo Crier Interactive Play Mode Probe` passed discovery, player interaction, the three-enemy in-place battle, duplicate-input protection, temporary Limbo terrain, protected-inn locking, permanent victory, and exploration/portal restoration.

## Completed Inn Work

### Counter interaction

- Removed the automatic walk-in inn trigger.
- Added `Assets/Scripts/Interaction/InnCounterInteraction.cs`.
- Interaction range is 2.25 metres and respects obstacle blocking.
- Mouse clicking the counter works.
- Commit: `d139d5d Make inn counter interactive`.

### Structural replacement design and plan

- Design: `Docs/superpowers/specs/2026-07-09-inn-structural-placeholder-replacement-design.md`.
- Design commit: `afe4ecc Design inn structural replacement pass`.
- Implementation plan: `Docs/superpowers/plans/2026-07-09-inn-structural-placeholder-replacement-plan.md`.
- Plan commit: `74e9c1c Plan inn structural replacement pass`.

### Structural replacement implementation

- Six approved 2K material sources were generated through the 3D AI Studio Gemini 3.1 Flash image endpoint:
  - Aged lime plaster.
  - Pietra serena.
  - Public-room mixed terracotta and pale stone tile.
  - Service-room terracotta.
  - Courtyard terracotta and gray-stone pavers.
  - Dark structural timber.
- Imported Unity assets live under `Assets/Environment/FlorentineInnFloor1/StructuralKit`.
- Seven reusable prefabs were created:
  - `InnWall_Straight_2m`
  - `InnWall_Doorway_2m`
  - `InnFloor_Public_2m`
  - `InnFloor_Service_2m`
  - `InnFloor_Courtyard_2m`
  - `InnArcade_TuscanColumn`
  - `InnArcade_Lintel_2m`
- The inn builder now uses the approved structural materials and prefab-based walls, columns, and lintels while retaining its original coordinates.
- Floors retain simple collider geometry but use the approved room-specific materials.
- GSpawn contains a seven-prefab library named `Florentine Inn Structural Kit`.
- Commit: `45a3e70 Replace inn structural placeholders`.

### First-floor furniture and prop implementation

- Approved design: `Docs/superpowers/specs/2026-07-09-inn-props-lighting-weather-design.md`.
- Approved plan: `Docs/superpowers/plans/2026-07-09-inn-props-lighting-weather-plan.md`.
- `Assets/Editor/FlorentineInnPropKitBuilder.cs` now generates and validates a reusable, deterministic first-floor prop kit.
- The kit contains 29 prefabs and 14 restrained URP material families under `Assets/Environment/FlorentineInnFloor1/PropKit`.
- Reception now includes the finished counter face, returns, foot rail, key cubbies and keys, ledger, quill, candle, bench, luggage, and plants while retaining the existing counter interaction.
- Salon now includes the woven rug, period tea table and chairs, stocked bookcase, tea service, pitcher, candle, and plant.
- Dining now includes six finished tables, twelve period chairs, deterministic place settings, bread boards, candles, a sideboard, ceramics, and pitcher dressing.
- Kitchen and service rooms now include finished counters, island, hearth, cookware, bread board, pitcher, ingredient set, worktable, stocked pantry shelves, five banded barrels, and storage crates.
- Office now includes the finished desk, chair, stocked bookcase, iron-banded chest, ledger, quill, documents, and candle.
- Courtyard now includes the upgraded fountain, two stone benches, bucket, and four potted plants without changing the approved arcade or route footprint.
- The courtyard fountain was subsequently enlarged to a true 3.2-metre basin, with a 2.7-metre animated surface, four pulsing falling-water streams, and two expanding ripple layers driven by `InnFountainAnimator`.
- Falling streams and ripples use a dedicated transparent URP material; the shared weather-responsive water shader remains limited to the horizontal basin surface to avoid projection artifacts.
- The primitive fountain structure has now been replaced by the approved 3D AI Studio octagonal lion-spout fountain. The approved design and plan are `Docs/superpowers/specs/2026-07-10-3d-ai-octagonal-inn-fountain-design.md` and `Docs/superpowers/plans/2026-07-10-3d-ai-octagonal-inn-fountain-plan.md`.
- Prism 3.1 task `cc44b999-d678-47d5-b846-bdde2af9961f` generated the standard textured PBR source. The raw 1,429,277-triangle GLB was retained in the ignored workbench output; the production model was reproducibly reduced with glTF Transform 4.4.1 to 92,903 triangles before import.
- The production model is `Assets/Environment/FlorentineInnFloor1/PropKit/Models/FlorentineInnOctagonalLionFountain.glb`. `FlorentineInnPropKitBuilder` owns its import contract, scales its renderer bounds to exactly 3.2 by 2.1 by 3.2 metres, recentres and grounds it, adds the simple basin collider, and places four diagonal mouth-fed water streams plus the existing two ripple layers.
- `FlorentineInnPropValidator` now rejects a missing authored structure, retired primitive fountain parts, incorrect bounds, more than 120,000 production triangles, missing stream/ripple counts, missing animation, or missing simple collision.
- `FlorentineInnFloor1Builder` instantiates these prefabs deterministically, so rebuilding the scene does not restore primitive furniture placeholders.
- This implementation remains uncommitted as of this handoff update.

### Persistent exploration facing

- Root cause: Benidito's exploration animator sent every walking direction to one shared south-facing `Idle` state even though `PlayerController` retained the last `MoveX` and `MoveY` values.
- `Assets/Editor/BeniditoDirectionalIdleBuilder.cs` now generates North, South, East, and West static idle clips from the approved directional walking artwork and deterministically rewires the controller transitions.
- `PlayerController.SetFacing` now enters the matching directional idle state when stationary, so scene-entry and fast-travel facing also remain correct.
- The pre-fix Play-mode regression was captured: north movement used `Benidito_Walking_north`, then release changed to `Benidito_Breathing_Idle_south`.
- The post-fix Play-mode probe passed all four press/release directions plus explicit north `SetFacing`.

## Verified Runtime Behavior

The structural pass was rebuilt twice and checked in Play mode. The following probes passed:

- Entrance-to-counter route clear.
- Salon doorway clear.
- Courtyard-to-dining opening clear.
- Courtyard-to-service opening clear.
- Counter interaction opens `RestMenuUI`.
- `CameraOcclusionFader` retains wall and lintel prefixes.
- The upper landing barrier retains its collider.
- Final Unity console contained zero errors and warnings.

Important testing nuance: two initial straight-line route probes hit existing props (`KeyCubbies`, a courtyard column, and `Stair_12`). These were bad probe paths rather than new structural regressions. The corrected doorway-centre probes passed.

The first-floor prop pass was freshly rebuilt and verified on 2026-07-10:

- Unity compiled and executed the prop-kit generator, production builder, and validator successfully.
- `[FlorentineInnPropKit] Rebuilt 29 reusable prefabs.`
- `[FlorentineInnProps] Validation passed: 29 prefabs and 19 production anchors present.`
- Multi-angle scene inspection confirmed the entrance-to-counter lane, dining circulation, courtyard circulation, room silhouettes, and locked upper floor remain readable.
- In Play mode, the counter correctly rejected interaction at the entrance spawn distance.
- At valid range, the counter opened the inn-rest UI five consecutive times with a close between each attempt.
- The final fountain/facing refinement probe confirmed a 3.2-metre basin, four animated streams, two animated ripple layers, and correct North/South/East/West idle retention.
- The authored 3D AI fountain pass rebuilt all 29 prop prefabs and the production inn scene, then passed the strengthened validator.
- The authored fountain runtime contract reported exact bounds `3.200, 2.100, 3.200`, 92,903 triangles, four streams, two ripples, and a `3.02 by 0.72 by 3.02` simple collider.
- Two Play-mode samples at `Time.time` 112.791 and 114.180 showed different stream scales and ripple scales, proving `InnFountainAnimator` was actively updating the replacement fountain.
- The locked-camera Play-mode capture is `Temp/FlorentineInnFountain_PlayMode.png`; it shows the octagonal basin, carved lion masks, faceted pedestal, and mouth-fed water without the previous stacked-cylinder silhouette or shader projection artifacts.
- `dotnet build Assembly-CSharp-Editor.csproj --no-restore --nologo` completed with 32 existing deprecation warnings and zero errors. The installed .NET 8 SDK cannot build Unity's `.slnx` container directly, so the generated editor project is the external build target.
- The COZY inn-light flooding regression was traced to five unshadowed point lights with 5-7 metre ranges plus unrestricted global sky ambient. `IndoorWeatherLightingGuard` now keeps the mixed indoor/courtyard scene on flat ambient `(0.20, 0.18, 0.15)` and reflection intensity `0.45` while leaving COZY in control of time, weather, fog, sky, and the directional sun.
- All five room lights now use soft shadows, ranges of 3.8-4.8 metres, reduced night intensities of 2.6-3.4, and explicitly seeded daylight values so a direct Play-mode start cannot begin at full night strength.
- The live `Mostly Cloudy` regression probe deliberately injected white skybox ambient at intensity `4.0` and reflection `1.0`; the inn restored its flat ambient and reflection `0.45` on the next frame while all five room lights remained soft-shadowed. The fixed locked-camera capture is `Temp/FlorentineInn_COZYLightingFixed.png`.
- The post-lighting external build completed with 80 existing warnings and zero errors. The final Play-mode console contained zero errors and the same unrelated Asset Inventory editor-only assembly warning.
- No runtime errors were reported. Unity emitted one unrelated Asset Inventory editor-only assembly warning involving `System.Runtime.CompilerServices.Unsafe.dll`.

## Completed World Presentation Work

### Persistent building-window environments

- Building windows now share persistent world-aware fog, glow, and rain behavior instead of relying on scene-local one-offs.
- The pass covers the inn and exterior building scenes through `BuildingWindowEnvironmentInstaller`, `ExteriorWindowFacade`, and `WorldWindowEnvironment`.
- The inn additionally owns `IndoorWeatherLightingGuard`; this isolates indoor ambient illumination from COZY's non-occluded global sky contribution without disabling weather presentation in the open courtyard or through windows.
- Key commits:
  - `1744c8b Expand window environment pass to all buildings`
  - `473c703 Add persistent world window environments`

### Shared grass, water, and weather surfaces

- A shared weather-responsive surface family now owns supported grass, ponds, fountains, rivers, shallow battle water, and rain splash materials.
- Scene builders and battle-map builders reproduce the approved materials instead of returning to legacy grass or water shaders.
- Design: `f8a993e Design stylized weather surface standard`.
- Plan: `77ce741 Plan stylized weather surface standard`.
- Implementation: `26fbd00 Standardize grass and water weather surfaces`.
- Latest verification: `[WeatherSurfaceStandard] Validation passed: no unexplained legacy grass or water surfaces.`

### Refined battle terrain

- The three approved 3D battle maps use the refined shared terrain shader/material workflow.
- The presentation retains tactical fog while pausing the earlier corruption treatment.
- Design: `5bba59d Design refined battle terrain shader`.
- Plan: `4b664ba Plan refined battle terrain rendering`.
- Implementation: `b494bf8 Refine battle terrain and pause corruption`.
- Latest verification: `[BattleTerrainStandard] Validation passed for 3 approved 3D map(s); corruption disabled, tactical fog retained.`

## Completed Authoritative Weather and Day/Night Runtime

The approved project-owned weather and time architecture is implemented and runtime-verified as of 2026-07-11.

- Design: `Docs/superpowers/specs/2026-07-11-weather-day-night-cycle-design.md` (`f9f28f00`).
- Plan: `Docs/superpowers/plans/2026-07-11-weather-day-night-cycle-plan.md` (`5cc60d25`).
- Typed contract: `fd92f5d5`.
- Continuous world clock: `7f0fbd92`.
- Seasonal deterministic fronts: `56b4225c`.
- Persistent runtime/profile/save integration: `6a37493f`.
- Typed consumers, battle isolation, and core water correction: `7eca8a32`.

### Ownership and pacing

- `WorldEnvironmentDirector` is the authoritative persistent clock and typed environment snapshot. It attaches to the existing runtime `GameSystems` object without serializing another component into the already-dirty prefab.
- COZY is presentation-only. Its time module is disabled while the director is active, and the authoritative hour is mirrored into it every frame for sun, moon, sky, fog, cloud, wind, precipitation, and lightning rendering.
- Default full-day duration is 40 real minutes. Night advances at 1.75 times daytime speed while the integrated full cycle remains exactly 2,400 real seconds.
- Canonical phases are Dawn 05:00-07:00, Day 07:00-18:00, Dusk 18:00-20:00, and Night 20:00-05:00.
- The clock advances during exploration and ordinary conversations. It pauses under `Time.timeScale == 0`, explicit nested pause reasons, and blocked tactical scenes (`Battle`, `BattleArena`). Travel and rest continue to advance time explicitly; rest still advances one date and lands at 07:00.

### Forecasts and typed consumers

- `WorldForecastGenerator` creates two to four deterministic, gap-free fronts per day from `FlorenceClimate.json`, the Florentine calendar, the current district microclimate, and stable seeded rolls.
- Fronts carry typed condition, precipitation, clouds, fog, wind, temperature, visibility, wetness target, lightning, and flood-risk state. Autumn multi-day Arno rain spells and deterministic flood-risk dates are preserved.
- `FlorenceWeather` now selects the active front and presents its exact installed COZY profile; it no longer owns a separate daily string roll.
- Tactical vision, world windows, light shafts, weather surfaces, and battle weather read typed `WorldWeatherState`. Legacy profile strings remain only at COZY/save compatibility boundaries and are centralized through `WorldWeatherClassifier`.
- Battle weather is local and temporary. It may alter tactical visibility or Gemini-authored battle drama, but it cannot mutate the world forecast, persistent wetness, or future story weather.
- Accumulated wetness rises and dries gradually and is persisted in save version 9. Existing earlier saves remain additive-compatible.

### Surface and runtime proof

- The Arno and the two main fountain basins use the shared Stylized Water 3 weather standard with rain impacts. Authored fountain streams/ripples remain decorative animation meshes, and structural river walls/walkways are explicitly excluded from water-surface detection.
- Static validators passed for the typed contract, exact clock pacing, all installed COZY profile mappings, 48 deterministic forecast days, and all exploration weather surfaces.
- The Mercato Play Mode probe passed after the final consumer migration: runtime authority attached to `GameSystems`; the world clock advanced and mirrored COZY; nested and `Time.timeScale` pauses froze it; current fronts were live; battle-local fog reduced tactical sight without changing the world forecast; and wetness survived save serialization.
- The unrelated generated COZY forecast array churn in `Assets/Resources/GameSystems.prefab` remains intentionally uncommitted. Do not bundle it with this system.

## Completed Camera and Hybrid-Zone Work

### Uniform locked HD-2D exploration camera

- Shared exploration scenes now use one locked, uniform HD-2D camera direction inspired by Octopath-style presentation.
- Per-zone dynamic zoom and ad-hoc camera bearing differences were removed from the shared exploration rig.
- The camera remains an exploration camera; tactical combat takes ownership through the battle camera rig when a hybrid encounter starts.
- Design: `45e67fd Design uniform HD-2D exploration camera`.
- Implementation: `faaee71 Implement uniform HD-2D exploration camera`.
- Primary documentation: `Docs/HD2D_Camera.md`.

### Shared exploration-to-battle architecture

- Hybrid zones keep the visible environment in place when combat starts; they do not swap to a separate battle scene or replace the visible terrain.
- `ZoneBattleAuthoring` owns the zone contract and `ZoneEncounterTrigger` suspends exploration, applies the authored grid, transfers camera ownership, and restores exploration after the encounter.
- Design: `40016c8 Design hybrid exploration and battle zones`.
- Plan: `8b704d9 Plan hybrid Rose Garden battle zone`.
- First production implementation: `1ceae4c Build hybrid Rose Garden battle zone`.
- Giardino delle Rose remains a valid production hybrid zone. It uses a 32x32 authored grid and the natural control-texture path of `InfernosCurse/HybridZoneTerrain`.
- Mercato Vecchio is now the first active urban hybrid zone. Its production 90x64 grid uses world origin `(-50,-32)`, collider-derived obstruction/cover authoring, protected seamless interiors, and the shared locked exploration-to-battle camera handoff.
- Ponte Vecchio, Duomo, Piazza della Signoria, and Via Calimala retain migrated urban terrain but remain encounter-authoring migration candidates.
- The latest Crier encounter validator confirms the Mercato hybrid references, obstruction grid, player interaction path, in-place battle startup, and restoration lifecycle.

## Completed RealBlend Urban Terrain Pass

### Design and implementation record

- Base urban-family design: `14bb8e8 Design urban hybrid terrain family`.
- RealBlend integration amendment: `a654091 Add RealBlend to urban terrain design`.
- Implementation plan: `57bb339 Plan RealBlend urban hybrid terrain`.
- Production implementation: `1354b6a Build RealBlend urban hybrid terrain`.
- Design document: `Docs/superpowers/specs/2026-07-10-urban-hybrid-terrain-design.md`.
- Plan document: `Docs/superpowers/plans/2026-07-10-urban-hybrid-terrain-realblend-plan.md`.

### Architecture

- RealBlend is committed under `Assets/RealBlend` as an approved, unmodified editor dependency.
- RealBlend supplies the vertex-paint authoring convention; the final game presentation uses the project-owned `InfernosCurse/HybridZoneTerrain` shader.
- The shader supports both approved inputs:
  - Natural zones continue to use terrain control textures.
  - Urban zones use baked vertex colors.
- Urban channel convention:
  - Blue = base stone.
  - Red = repairs or warmer infill.
  - Green = grime/wear.
  - Alpha mirrors grime for RealBlend authoring compatibility.
- Persistent weather remains authoritative for wetness through the existing `_GrassWetness` integration.

### Generated urban family

Five district profiles, materials, generic proof meshes, and production meshes live under `Assets/Art/Environment/HybridZones`:

- Mercato Vecchio: warm, repair-heavy market stone.
- Ponte Vecchio: cooler bridge stone with restrained damp-edge wear.
- Duomo: pale dressed stone with sparse repairs.
- Piazza della Signoria: balanced warm-gray civic paving.
- Via Calimala: darker compact street paving with directional wear.

Production renderer migrations:

- `MercatoVecchio/[MarketSquare]/Floor_Cobblestone`
- `PiazzaDellaSignoria/[Ground]/Floor_Piazza`
- `PonteVecchio/[BridgeDeck]/Deck`
- `Duomo/[Floors]/Floor_Octagon`
- `ViaCalimala/Street_EW_Template/[Ground]/Street_Paving`

The migration preserves object transforms, colliders, overlays, paths, props, travel wiring, exploration cameras, and existing authoring. Piazza's yard, Uberti waste, and outskirts remain specialized surfaces. Ponte walkway overlays, Duomo adjoining marble pieces, and Via's structural outskirts also remain unchanged.

### Rebuild and validation tooling

- `Assets/Editor/UrbanHybridTerrainBuilder.cs` deterministically builds the five urban profiles, materials, and generic baked meshes.
- `Assets/Editor/UrbanHybridTerrainSceneMigrator.cs` creates transform-safe production top meshes and assigns them to the five approved renderers.
- `Assets/Editor/UrbanHybridTerrainSandboxBuilder.cs` maintains the side-by-side proof scene at `Assets/ToolingSandbox/UrbanHybridTerrainSandbox.unity`.
- `PiazzaSignoriaSceneBuilder` and `DuomoTilePass` reapply their hybrid primary surfaces so later builder passes do not erase them.
- `HybridZoneStandardValidator` validates all five asset sets and all five production assignments, including baked colors and retained colliders.
- Production-scene validation uses Unity preview scenes. This avoids a Unity 6.4 GPU Resident Drawer cache error that occurred when five rendered scenes were repeatedly opened and restored additively.

### Verification completed

- Builder output was regenerated deterministically after the Unity restart/update.
- All five generic meshes and all five production meshes retain a color array equal to vertex count.
- Visual editor checks passed for all five migrated districts.
- Piazza della Signoria and Via Calimala were checked in Play mode for exploration camera framing, visible terrain, and collision.
- Latest urban verification:
  - `[UrbanTerrainValidator] Validation passed for 5 urban profile/material/mesh sets.`
  - `[UrbanTerrainValidator] Production validation passed for 5 urban surfaces; colliders retained.`
- Mercato has now completed an exploration-to-battle runtime probe through the Limbo Crier encounter. The other four urban scenes remain encounter-authoring migrations.

## Completed Limbo World, Memory, and Crier Stack

### Circle influence and permanent event memory

- Save format is now v8 and carries the persistent Circle state, Crier records, NPC memory records, discovery records, world-event ledger, campaign ID, Chronicle sequence, player facing, seamless-interior sub-location, and player-known NPC map sightings. Older saves initialize a valid empty sighting ledger and fall back to authored usual locations.
- Limbo is a named circle influence rather than generic container corruption. It grows locally, can bleed into connected districts when neglected, and remains bounded by deterministic caps and idempotent daily processing.
- The campaign Chronicle is append-only, hash-chained, mirrored to a rolling backup, and reconciled into the indexed `WorldEventLedger`. Save slots reference permanent history rather than replacing it, so loading an older slot cannot rewrite decisions already committed to the campaign.
- The Windows atomic replacement path now retries the same durable replace for a bounded period when antivirus, indexing, or sync briefly locks the destination. The validator includes a deterministic transient-lock regression that failed before the fix and passes afterward.
- Gemini is a bounded narrative director only. It receives canonical facts, approved IDs, vocabulary, and state summaries, returns strict JSON, and cannot directly mutate authoritative world state. Invalid or unavailable AI output uses deterministic fallback events.

### Persistent Limbo simulation and NPC Unmooring

- One cautious opening Crier record begins in Mercato and remains undiscovered until an approved rumor/event consequence reveals it.
- The Crier travels among three preaching sites and one hide site, respects curfew state, raises Limbo influence during unresolved daily simulation, and relocates after disruption.
- Five Mercato NPC memory definitions track susceptibility, schedule overlap, relationship identity, exposure, and the Grounded -> Distracted -> Unmoored -> Forgotten progression.
- Essential-service and quest-critical safeguards prevent the innkeeper or record clerk from becoming irrecoverably unavailable. Forgotten NPCs require authored rescue rather than passive offscreen recovery.
- Discoveries include the strange-bell rumor, missing-neighbor rumor, lost-neighbor POI, Roman Florentia stone POI, and the corroborated Mercato Crier route.

### Limbo Crier combat package

- Production combatant: level-2 `Limbo Crier`, a common Florentine doomsayer in a soot-dark robe, iron half-mask, false reliquary, and bell-hook staff.
- Specialized deterministic `LimboCrierAI` prioritizes formation support, Dread pressure, pulls, and legal fallbacks instead of using a flat generic enemy turn.
- Active kit:
  - `Bell-Hook Jab`: equipped-weapon melee.
  - `Knell of Dread`: charged caster-centered Dread field; absorbable and level-scaled.
  - `Crooked Benediction`: applies False Zeal to an ally.
  - `Pilgrim's Hook`: ranged one-cell pull; absorbable.
  - Holy/refined counterparts: `Bell of Vigilance` and `Pilgrim's Rescue`.
- Dread and False Zeal were appended to the serialized status enum. Both use exact two-affected-turn timing, refresh without stacking, and expose UI icons/tooltips through `StatusEffectPresentationCatalog`.
- Equipment is fully wired: `Bell-Hook Staff`, `Crier's Jack`, and `False Reliquary`, including signed Dark/Holy damage-received modifiers.
- Shared forced movement rejects blocked, occupied, reserved, objective, protected, or illegal-edge destinations.
- Temporary terrain restores every authored cell field on expiry, victory, defeat, teardown, and test reset. Limbo Stain damages hostile non-Limbo occupants, applies Dread, and cannot overwrite protected objectives or stronger authored corruption.

### Production visual package and source metadata

- PixelLab Pro source character: `7a07c91f-65dc-4501-a80f-a154619092bc`.
- Imported production package contains 228 unique 196x196 character images: eight rotations plus four-direction walking, Bell-Hook Jab, Knell of Dread, Crooked Benediction, Pilgrim's Hook, hurt, and death sequences.
- Portrait is 128x128. Dread, False Zeal, Bell-Hook Staff, Crier's Jack, and False Reliquary icons are 32x32.
- All sprite imports use point filtering, no mipmaps, and uncompressed textures. Source IDs, prompts, group IDs, expected frame counts, and import contracts are recorded in `Assets/Characters/LimboCrier/Source/pixellab-source.json` and the humanoid visual profile; credentials are never stored.
- Raw recoverable generation output is kept only under ignored `GeneratedAssets/PixelLab/LimboCrier`. Deterministic scripts live under `Tools/pixellab`.
- The Limbo Stain texture is project-authored procedural art: disconnected violet/charcoal cracks and arcs, not a stock magic circle. `TemporaryTerrainPresenter` mirrors authoritative apply/restore events and pulses the visual without modifying the authored scene.

### Exploration-to-battle encounter

- `Assets/Prefabs/Narrative/LimboCrierWorld.prefab` is linked to `LimboCrier_Mercato_01`. It reuses the production directional profile for exploration walking/idle animation and never doubles as a staged `BattleUnit`.
- Before discovery, the world actor is invisible, non-blocking, and not interactable. After discovery, the shared `PlayerWorldInteractor` exposes `Confront the Limbo Crier`.
- The Mercato confrontation starts in place with one Crier behind two level-1 Cursebearer frontliners. Repeated interaction while the handoff is active cannot create duplicate managers or enemies.
- Defeat restores the same persistent Crier. Victory calls `PersistentLimboWorldState.DefeatAgent`, removes it from daily Limbo simulation, destroys the world actor, and restores exploration/camera ownership.
- The 2026-07-10 batch Play-mode walkthrough passed the complete golden path: hidden discovery gate -> player interaction -> one Crier plus two frontliners -> Limbo Stain apply/refresh/restore -> persistent victory cleanup. Ten repeated confrontation attempts did not duplicate the encounter.

### Final validation evidence

- `[CampaignChronicleValidator]` passed hash chain, atomic mirror, transient-lock recovery, backup recovery, save envelope, indexed ledger, and idempotent reconciliation.
- `[LimboWorldSimulationValidator]` passed daily caps/idempotence, Unmooring/recovery, loaded-unloaded parity, discovery monotonicity, and v5 save state.
- `[GeminiNarrativeValidator]` passed bounded context, strict schema/IDs, locked vocabulary, deterministic fallback, and state-invariant AI output.
- `[FlorenceLimboWorldAuthoringValidator]` passed one Crier, five safeguarded NPC records, five discoveries, and four Mercato sites.
- `[LimboCrierCombatValidator]` passed exact data, status timing, targeting, forced movement, temporary-terrain restore, equipment modifiers, humanoid fallback, 228 images, portrait/icons, source metadata, and import settings.
- `[LimboCrierEncounterValidator]` passed the discovery-gated prefab, three-enemy formation, Mercato hybrid grid, persistent victory, player interaction path, and stain lifecycle.
- `[LimboCrierFinalValidator]` ran the complete stack in one Unity batch and exited successfully.

## Gugol Mappe Refined Browsing and Street View

- The browsing hierarchy is now masthead-free and map-first. Search plus direct `City`, `Tuscany`, and `Italy` controls stay in a fitted chrome layer while the parchment map remains dominant.
- `GugolMapUI` remains the open/close, pause, travel, and input coordinator. Layer fitting/transitions, feature presentation, knowledge snapshots, weather treatment, selection state, context cards, and search are split into project-owned map presenters and services under `Assets/Scripts/Map`.
- Street View is a new append-only presentation state; the serialized `MapLevel` values were not changed. A street can use dedicated art or safely fall back to a magnified city-map crop.
- Street, venue, NPC, presentation, and world-expression contracts live under `Assets/Resources/GugolMap`. `Assets/Editor/GugolMappeRefinedAuthoringBuilder.cs` deterministically authors the initial profile, two streets, three venues, and five NPC definitions.
- The Mercato pilot includes Mercato Vecchio and Via Calimala. Mercato Street View exposes The Florentine Inn, Mercato Public Stalls, Mercato Records Desk, and five known NPC markers. Venue labels remain visible; NPC names are resolved through click or mixed search to avoid label clutter.
- The Florentine Inn map record targets the seamless `albergo_fiorentino` / `albergo_fiorentino_floor1` contract rather than loading a legacy standalone inn scene.
- Search indexes only the immutable player-knowledge snapshot. Exact proper-name matches take priority, so `Mercato Vecchio` opens the street even when NPC last-known text contains the same words.
- NPC map knowledge stores authored usual locations or player-known last sightings only. It never reads a live transform or simulation-only position, and it round-trips in save v8.
- Hidden Circle numbers and player Sanity are isolated from map presentation. The authoring validator rejects forbidden raw influence, Circle evaluation, and Insanity dependencies in the map scripts.
- Weather and authored world-state expression IDs may change map presentation; numeric Circle changes alone may not.
- Latest verification on 2026-07-11:
  - `dotnet build Assembly-CSharp-Editor.csproj --no-restore --nologo`: 0 errors; existing project/package warnings remain.
  - `[GugolMapValidator] Refined authoring validation passed after reload.`
  - `[GugolMapPlayModeVerifier] PASS`: masthead-free City map -> exact street search -> Mercato Street View -> three venues plus five known NPC markers -> venue card -> Tuscany/Italy navigation -> close/reopen without duplicate canvases.
  - Manual runtime review confirmed compact zoom-independent markers, readable venue labels, the Florentine Inn service card, and its Street View directions route.

### Adding another Street View area

1. Create stable `GugolStreetDefinition`, `GugolVenueDefinition`, and optional `GugolNpcMapDefinition` assets in the matching `Assets/Resources/GugolMap` folders.
2. Author normalized city centerlines, forgiving hit widths, Street View bounds or dedicated background art, venue anchors, discovery contracts, and route fallbacks.
3. Link entrances through stable owning-zone, building, and sub-location IDs. Small shops/inns remain seamless parts of their exterior zone; major landmarks may use dedicated scenes.
4. Add explicit `GugolNpcSightingReporter` hooks only where a player observation or authored interaction should update last-known knowledge.
5. Run `InfernosCurse/Validation/Validate Gugol Mappe Refined Authoring`, then `Run Gugol Mappe Play Mode Probe` before treating the area as production-ready.

## Tooling State

The following Asset Store tools are currently imported locally:

- Asset Inventory 4, version 4.5.1.
- GSpawn - Level Designer.
- UModeler X, version 1.2.1 f3.
- RealBlend - Mesh Painting & Creation.

### Asset Inventory

- Setup wizard was completed.
- It discovered roughly 193 packages and began background indexing.
- Live project search was verified by finding `Assets/ToolingSandbox/Prefabs/Counter_2m.prefab`.
- Its package MCP functions compile, but custom `AssetInventory_*` tools did not appear in the negotiated Codex tool list during this session. Direct calls inside Unity worked.
- After restart, check whether the custom MCP tools are exposed before using the reflection/direct-call fallback.
- Do not edit package internals. `Assets/AssetInventory/AGENTS.md` contains package-specific instructions.

### GSpawn

- Initialized in `Assets/ToolingSandbox/LevelDesignToolsSandbox.unity`, not in the production inn scene.
- Grid-friendly Unity snapping was configured to XZ 1 metre, Y 0.25 metre, rotation 15 degrees.
- The `Florentine Inn Structural Kit` library contains seven prefabs.
- GSpawn library registration must run while the initialized sandbox scene is active because its preview factory otherwise throws a null reference.
- The GSpawn package and configured data were included in commit `45a3e70`.

### UModeler X

- An editable test object exists in the tooling sandbox.
- The production structural prefabs are standard Unity prefab geometry and remain editable/replaceable. The sandbox and UModeler package are not part of commit `45a3e70`.

### RealBlend

- Imported package is tracked in commit `1354b6a`.
- Do not edit package internals unless a package-specific fix is explicitly approved.
- Project-owned builders use `RealBlend.VertexPaintStorage` during authoring, then bake colors into ordinary Unity mesh assets so production scenes do not depend on an authoring component at runtime.
- RealBlend's sample shader was useful as an authoring proof but was too dark/contrast-heavy for the game. The project-owned hybrid shader controls the final restrained presentation.

## Current Working Tree Boundaries

Commit `1354b6a` intentionally included only RealBlend, the urban terrain implementation, generated urban assets, the five approved production scenes, and the urban proof subset of `Assets/ToolingSandbox`.

The following remain intentionally uncommitted and must not be reset, cleaned, or bundled into another commit without inspection:

- `.superpowers/brainstorm/floor1-1783634009/content/floor1-approaches.html`
- `.superpowers/brainstorm/floor1-1783634009/content/floor1-layout-approved-direction.html`
- `.superpowers/brainstorm/battle-terrain-shader-20260709/`
- `.superpowers/brainstorm/inn-props-20260709/`
- `.superpowers/brainstorm/inn-structural-materials/`
- `.superpowers/brainstorm/urban-hybrid-terrain-20260710/`
- `Assets/Scenes/BattleArena.unity`
- `Assets/UI/Fonts/Resources/UIFonts/Cinzel SDF.asset`
- `Assets/UI/Fonts/Resources/UIFonts/EBGaramond SDF.asset`
- `InfernosCurse.slnx`
- `Packages/manifest.json`
- `Packages/packages-lock.json`
- `Packages/xyz.staggart-creations.stylized-grass/_Demo/DemoAssets/Models/BackdropMountains.obj.meta`
- `ProjectSettings/ProjectSettings.asset`
- `Assets/AssetInventory/` and its root meta file.
- The unrelated `Assets/ToolingSandbox/LevelDesignToolsSandbox.unity`, `Materials/`, and `Prefabs/` contents. The root meta and urban terrain proof subset are now tracked.
- `Assets/UModelerX/`, `Assets/UModelerXData/`, and their root meta files.
- The approved inn prop pass: `Assets/Editor/FlorentineInnPropKitBuilder.cs`, its meta file, `Assets/Environment/FlorentineInnFloor1/PropKit/`, `Assets/Editor/FlorentineInnFloor1Builder.cs`, and `Assets/Scenes/FlorentineInnFloor1.unity`.
- The approved fountain/facing refinement: `Assets/Scripts/Environment/InnFountainAnimator.cs`, `Assets/Editor/BeniditoDirectionalIdleBuilder.cs`, the generated directional idle clips, `Assets/Characters/Benidito/Benidito.controller`, and `Assets/Scripts/Player/PlayerController.cs`.
- The approved authored-fountain pass: `Docs/superpowers/specs/2026-07-10-3d-ai-octagonal-inn-fountain-design.md`, `Docs/superpowers/plans/2026-07-10-3d-ai-octagonal-inn-fountain-plan.md`, `Assets/Environment/FlorentineInnFloor1/PropKit/Models/`, the rebuilt fountain prefab and scene, and the authored-fountain changes in `Assets/Editor/FlorentineInnPropKitBuilder.cs`.
- The approved COZY inn-light isolation fix: `Assets/Scripts/Environment/IndoorWeatherLightingGuard.cs`, its meta file, the lighting changes in `Assets/Editor/FlorentineInnFloor1Builder.cs`, strengthened validation in `Assets/Editor/FlorentineInnPropKitBuilder.cs`, and the rebuilt inn scene.
- The verified Limbo/Crier baseline is committed in `7383acd6`. Any later dirty changes touching its Circle/Chronicle/Gemini/NPC/discovery scripts, validators, prefab, Mercato authoring, or data remain user-owned follow-up work and must be inspected before staging; the raw PixelLab cache under `GeneratedAssets/PixelLab/LimboCrier` remains ignored.
- The approved Gugol Mappe implementation is included in the local implementation commit immediately after `8c0574f1`: runtime contracts/presenters/services, NPC map knowledge (now carried by save v9), deterministic refined authoring, `Assets/Resources/GugolMap`, and the authoring and Play-mode validators.
- Do not accidentally bundle the unrelated COZY forecast churn in `Assets/Resources/GameSystems.prefab`; the Limbo skill registry uses Resources-backed assets and does not require that prefab change.
- `Refrences/maps/ubuntu-24.04.4-wsl-amd64.wsl`
- `Refrences/maps/wsl.2.7.10.0.x64.msi`

Treat all of these as user-owned or session-generated changes. Inspect them individually before deciding whether to commit, ignore, or remove anything.

## Local WSL Environment

- WSL 2.7.10.0 is installed and operational.
- Installed kernel reported `6.18.33.2-2`.
- Distribution `Ubuntu-24.04` is installed, running under WSL 2, and uses Unix account `david`.
- The Microsoft Store/download path returned `Wsl/InstallDistro/E_ACCESSDENIED`; importing the local Ubuntu `.wsl` package succeeded.
- WSL 1 support is not required for this project.
- The local Ubuntu package and WSL installer under `Refrences/maps` are intentionally untracked.

## API Workflows

### 3D AI Studio

- Documentation: `https://www.3daistudio.com/Platform/API/Documentation`.
- Image endpoint used: `POST https://api.3daistudio.com/v1/images/gemini/3.1/flash/generate/`.
- Production prop models use Prism 3.1 through `POST https://api.3daistudio.com/v1/3d-models/tripo/image-to-3d/3.1/` with standard textures and PBR enabled.
- Status endpoint: `GET https://api.3daistudio.com/v1/generation-request/<task_id>/status/`.
- Authentication uses `Authorization: Bearer <API_KEY>`.
- Default documented rate limit was three requests per minute, so requests were spaced approximately 21 seconds apart.
- Never place the key in this document, source control, Unity assets, or shell scripts saved on disk.

### PixelLab

- PixelLab environment variables existed locally as `PIXELLAB_API_TOKEN` and `PIXELLAB_SECRET`.
- Prefer PixelLab for project pixel-art sprites or icons when suitable.
- Never print or commit the token values.

## Recommended Next Work

The refined Gugol Mappe, Circle-territory foundation, seamless Mercato implementation, and authoritative weather/day-night runtime are verified and committed locally. They have not been pushed. Unrelated inn, package, font, BattleArena, tooling, and COZY changes remain dirty and intentionally excluded.

Likely sequence:

1. Open Unity and allow imports/compilation to settle; rerun the World Environment static suite and Play Mode probe after changing clock, climate, COZY profiles, pause behavior, save data, or weather consumers.
2. Run `InfernosCurse/Validation/Validate Gugol Mappe Refined Authoring` and `Run Gugol Mappe Play Mode Probe` after changing map definitions, search, save knowledge, Street View geometry, or map presentation.
3. Review the Crier visually in Mercato at gameplay-camera distance when local, then tune only presentation if needed; the functional encounter already passes Play mode.
4. Migrate the next approved urban scene from `Migration candidate` to an active hybrid battle zone, with Piazza della Signoria as the open-space counterpart to dense Mercato.
5. For each migrated urban zone, verify exploration collision, encounter startup, terrain identity preservation, tactical readability, victory cleanup, and restoration of the locked exploration camera.
6. Run the full urban, hybrid-zone, battle-terrain, weather, and Limbo validators after each scene batch.
7. Inspect and commit any remaining approved inn or BattleArena work separately; do not bundle package, font, tooling, or COZY churn without review.
8. Push local commits only if the user asks to push.
9. Return to the inn when the user is local; keep NPC capsule replacement, live upstairs bed activation, and the second floor outside scope until explicitly approved.

## Git Safety

- Never use `git reset --hard` or discard unrelated changes.
- Stage explicit paths only.
- The last structural commit is large because it includes the complete GSpawn Asset Store package and configured library data.
- The RealBlend urban terrain commit is also large because it includes the complete approved RealBlend package.
- The local implementation commit follows `8c0574f1`; `origin/main` remains `365e1153`. No push was performed in this pass.
