# Inferno's Curse - Master Completion Register

Last updated: 2026-07-13

## Purpose

This is the project's living reference for completed work. It records only work that is either:

1. explicitly marked complete in an existing project tracker; or
2. completed and verified during a development task.

Planned work, open audit findings, and unverified assumptions do not belong here.

## Status meanings

- **Verified current** - exercised or validated against the current workspace on the recorded date.
- **Recorded complete** - explicitly checked off in a source tracker, but not fully re-audited while creating this register.
- **Verified removed** - the completed cleanup target is absent from the current workspace.

## Currently verified completions

### MC-2026-07-13-001 - Florentine inn west-entry camera cutaway

- **Status:** Verified current
- **Area:** Mercato Vecchio / Albergo Fiorentino / camera visibility
- **Completed:** 2026-07-13
- **Result:** Entering the west-facing inn now opens a dollhouse cutaway. The roof, exterior facade, and camera-facing wall set are hidden while the player is inside, leaving the player and inn interior visible. Exterior rendering is restored on exit.
- **Implementation:**
  - `Assets/Scripts/Camera/CameraOcclusionFader.cs`
  - `Assets/Scripts/Camera/SeamlessInteriorCameraZone.cs`
  - `Assets/Editor/FlorentineInnSeamlessModuleBuilder.cs`
  - `Assets/Editor/MercatoVecchioProductionBuilder.cs`
  - `Assets/Scenes/MercatoVecchio.unity`
- **Regression coverage:** `Assets/Scripts/Validation/MercatoSeamlessPlayModeProbe.cs`
- **Verification evidence:**
  - Stable Game-view inspection showed the unobstructed dollhouse interior.
  - Mercato production-scene validation passed after rebuilding the scene.
  - The clean play-mode verifier passed five exterior-to-interior-to-exterior crossings.
  - The probe checks the roof, facade, camera-facing walls, player framing, camera blend, lighting, registry ownership, battle lock, and exterior restoration.
  - Scoped C# `git diff --check` passed and temporary diagnostics were removed.

### MC-2026-07-13-002 - Florentine inn floor Z-fighting removal

- **Status:** Verified current
- **Area:** Mercato Vecchio / Albergo Fiorentino / floor rendering
- **Completed:** 2026-07-13
- **Result:** The inn's stone floor remains stable while the camera follows the player north and south; the continuous city surface no longer flickers through it.
- **Root cause:** Ten inn finish-floor meshes and the continuous `Floor_Cobblestone` surface shared the same world-space top plane at Y = 0, so the GPU alternated between the two surfaces.
- **Implementation:** `Assets/Editor/MercatoVecchioProductionBuilder.cs` raises the seamless Mercato inn's `Floors` group by 0.025 metres. This integration-only adjustment leaves the standalone inn scene and the rest of the module unchanged.
- **Regression coverage:** The Mercato production validator rejects any inn finish floor whose top is less than 0.01 metres above the city floor.
- **Verification evidence:**
  - The regression check failed before the fix and identified all ten overlapping room floors.
  - Mercato production-scene validation passed after rebuilding the scene.
  - The play-mode verifier passed five exterior-to-inn-to-exterior crossings, exercising repeated north/south traversal.
  - Live Game-view inspection showed the tiled inn floor remaining visible without city-floor patches replacing it.

### MC-2026-07-13-003 - Albergo Fiorentino medallion sign

- **Status:** Verified current
- **Area:** Mercato Vecchio / Albergo Fiorentino / environmental signage
- **Completed:** 2026-07-13
- **Result:** The inn now has a readable, double-sided circular medallion sign with raised `ALBERGO FIORENTINO` lettering, a Florentine lily, worn brass trim, and an iron wall bracket. It replaces the former blank rectangular placeholder.
- **Art source:**
  - `Tools/Blender/build_albergo_fiorentino_sign.py`
  - `ArtSource/Blender/AlbergoFiorentinoSign.blend`
  - `Assets/Environment/MercatoVecchio/ProductionKit/Models/AlbergoFiorentinoSign.glb`
- **Implementation:** `Assets/Editor/MercatoVecchioProductionKitBuilder.cs` instances and positions the authored sign in the generated inn facade.
- **Regression coverage:** The production-kit validator requires the medallion, rim, bracket, hangers, north/south lily faces, and north/south text faces; rejects the legacy rectangular slab; and verifies the bracket mounts toward the facade.
- **Verification evidence:**
  - The structural regression failed before integration with nine expected missing/legacy-part errors, then passed after the authored model replaced the placeholder.
  - A mounting-direction regression failed before the assembly rotation and passed after the bracket was corrected toward the facade.
  - Blender front and back renders confirmed upright, readable lettering on both faces; the final exported model contains 15,570 triangles.
  - Mercato production-kit and production-scene validators passed after rebuilding their generated assets.
  - Live Game-view inspection was approved by the user.
  - The clean play-mode verifier passed five exterior-to-inn-to-exterior crossings with one camera override owner, one registry module, active local lighting, protected battle lock, and no duplicate runtime authorities.

## Imported completion history

The following entries were imported from [`NIGHT_AUDIT.md`](../NIGHT_AUDIT.md), where they are explicitly marked `[x]`. They are preserved as **Recorded complete** unless a narrower current-workspace check is stated. The later [`NIGHT_AUDIT_2026-07-04.md`](../NIGHT_AUDIT_2026-07-04.md) says its findings were not fixed at the time of that audit, so none of those open findings are included here.

### Map, camera, curse, and player systems

1. **MC-HIST-001 - Initial curse overlay population**
   - **Status:** Recorded complete
   - **Original finding:** `CurseOverlay.Initialise()` was never called, leaving the initially seeded curse density invisible.
   - **Current supporting evidence:** `BattleCurseAutomata` calls `overlay.Initialise(world)`.

2. **MC-HIST-002 - Hub surge event with no consumer**
   - **Status:** Recorded complete
   - **Original finding:** `HubMap.OnSurge` fired without subscribers even though the tooltip promised surge behavior.

3. **MC-HIST-003 - ZoneExit rapid-reload re-arm safety**
   - **Status:** Recorded complete
   - **Original finding:** rapid disable/enable cycles could stack delayed `Arm` invokes.
   - **Current supporting evidence:** `ZoneExit.OnEnable()` cancels the pending `Arm` invoke before scheduling it, and `OnDisable()` cancels it again.

4. **MC-HIST-004 - Zone-to-zone spawn intent**
   - **Status:** Recorded complete
   - **Original finding:** zone-to-zone travel did not pass its destination entry point.
   - **Current supporting evidence:** the `ToScene` path calls `TravelIntent.SetEntry(targetEntryId)` before loading `targetScene`.

5. **MC-HIST-005 - DynamicZoom Main Camera caching**
   - **Status:** Recorded complete
   - **Original finding:** `DynamicZoom` queried `Camera.main` every `LateUpdate`.
   - **Current supporting evidence:** the Main Camera is cached in `_mainCam` and only re-resolved when the cached reference is missing.

6. **MC-HIST-006 - DynamicZoom Cinemachine Follow handling**
   - **Status:** Recorded complete
   - **Original finding:** `DynamicZoom` silently added `CinemachineFollow` at runtime.
   - **Current note:** the compatibility fallback remains, but it now emits an explicit warning instructing scene authors to provide the component.

7. **MC-HIST-007 - World-map adjacency default clarified**
   - **Status:** Recorded complete
   - **Original finding:** `restrictToNeighbors` defaulted to false, leaving adjacency gating inactive.
   - **Current note:** false is documented as the intentional early-game default until node discovery drives `currentNodeId`.

8. **MC-HIST-008 - World-map exit destination behavior clarified**
   - **Status:** Recorded complete
   - **Original finding:** the `ToWorldMap` path ignored `targetScene` and used `WorldMap` directly.
   - **Current note:** the Gugol Mappe overlay is the primary in-place path; the legacy `WorldMap` scene is explicitly documented as its fallback.

9. **MC-HIST-009 - Ahead-of-use HubMap APIs identified**
   - **Status:** Recorded complete
   - **Original finding:** `Cleanse`, `ActivateRitual`, and `GetBattleSeedCurse` had no callers.
   - **Current note:** the ahead-of-use API section is documented in `HubMap`; `GetBattleSeedCurse` is now consumed by `BattleManager`.

10. **MC-HIST-010 - Hub node `mapPosition` finding recorded**
    - **Status:** Recorded complete
    - **Original finding:** `mapPosition` was stored but unread while `mapImagePosition` drove pin placement.

11. **MC-HIST-011 - Player facing null-check cleanup**
    - **Status:** Recorded complete
    - **Original finding:** `PlayerController.SetFacing` contained a dead Animator null-check despite its required component.
    - **Current supporting evidence:** `SetFacing` uses the required Animator directly and documents that guarantee.

### Cleanup and removed legacy assets

12. **MC-HIST-012 - Removed `CameraZoneTrigger.cs`**
    - **Status:** Verified removed
    - **Reason:** abandoned river-camera blending approach with no scene or prefab references.

13. **MC-HIST-013 - Removed `BottomFade.cs`**
    - **Status:** Verified removed
    - **Reason:** superseded bottom-gradient camera treatment with no scene or prefab references.

14. **MC-HIST-014 - Removed legacy edge-fade assets**
    - **Status:** Verified removed
    - **Assets:** `Assets/Art/Map/EdgeFade.mat` and `Assets/Art/Map/EdgeFadeGradient.png`.

15. **MC-HIST-015 - Removed preview-source JPG files**
    - **Status:** Verified removed
    - **Reason:** the transparent PNG location previews are the live assets; the six source JPG copies were redundant.
    - **Current supporting evidence:** `Assets/Art/Previews/` contains the six PNG previews and their metadata, with no JPG files.

## How to add future completions

Add a new entry under **Currently verified completions** using this shape:

```text
### MC-YYYY-MM-DD-NNN - Short completion title

- Status: Verified current
- Area: Scene, system, or feature
- Completed: YYYY-MM-DD
- Result: What is now true for the player or developer
- Implementation: Important files or assets
- Verification evidence: Tests, validators, and live checks that passed
- Source: Task, audit, issue, or decision record
```

If an old tracker item is merely checked off but has not been re-tested, import it as **Recorded complete**. Promote it to **Verified current** only after checking the present workspace.
