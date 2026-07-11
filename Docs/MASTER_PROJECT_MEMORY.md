# Inferno's Curse Master Project Memory

Last updated: 2026-07-10

## Purpose

This is the restart-safe handoff document for continuing work in `C:\UnityGames\InfernosCurse`. Read this file before making project changes after a Codex or Unity restart.

## Project Baseline

- Engine: Unity 6.4, editor `6000.4.11f1`.
- Render pipeline: URP 17.4.
- Repository branch: `main`.
- Current local HEAD: `1354b6a Build RealBlend urban hybrid terrain`.
- `origin/main` was still at `b494bf8` when this handoff was updated; the later local commits have not been pushed.
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
- Giardino delle Rose is the current valid production hybrid zone. It uses a 32x32 authored grid and the natural control-texture path of `InfernosCurse/HybridZoneTerrain`.
- The five urban scenes currently have the exploration camera and migrated terrain, but still classify as `Migration candidate`; they do not yet contain active `ZoneBattleAuthoring` encounter setups.
- Latest verification: `[HybridZoneValidator] Validation passed: 10 scene(s) classified, 0 invalid.` This means configuration is internally consistent; it does not mean every migration candidate already supports battles.

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
- The urban scenes cannot yet complete an exploration-to-battle runtime probe because their encounter authoring has not been migrated. This is a known remaining task, not a terrain failure.

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
- `Docs/MASTER_PROJECT_MEMORY.md` itself remains untracked until the user asks to commit this handoff update.
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

The local project is complete through commit `1354b6a`, but the commits after `origin/main` have not been pushed. Confirm the desired next action after restart.

Likely sequence:

1. Open Unity and allow imports/compilation to settle.
2. Confirm Unity MCP is connected and the console is clean.
3. Migrate approved urban scenes from `Migration candidate` to active hybrid battle zones, starting with Piazza and Mercato as the open/dense extremes.
4. For each migrated urban zone, verify exploration collision, encounter startup, terrain identity preservation, tactical readability, victory cleanup, and restoration of the locked exploration camera.
5. Run the full urban, hybrid-zone, battle-terrain, and weather validators after each scene batch.
6. Commit this master-document update only when requested.
7. Push local commits only if the user asks to push.
8. Review the completed first-floor prop pass at gameplay-camera distance and commit it when requested.
9. Keep NPC capsule replacement, live upstairs bed activation, and the second inn floor outside scope until explicitly approved.

## Git Safety

- Never use `git reset --hard` or discard unrelated changes.
- Stage explicit paths only.
- The last structural commit is large because it includes the complete GSpawn Asset Store package and configured library data.
- The RealBlend urban terrain commit is also large because it includes the complete approved RealBlend package.
- Current local HEAD is `1354b6a`; no push was performed for the later local commit series during this session.
