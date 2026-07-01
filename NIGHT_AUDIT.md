# End-of-Night Audit — InfernosCurse
Generated 2026-06-30 (end of the environment/world-map session).

Full read of all 53 scripts + asset/scene scan. Console is clean (0 errors, 0 warnings).
Nothing was edited — this is a report to go over tomorrow. Suggested order at the bottom.

Prior `BUG_AUDIT.md` (root) already resolved the Battle-layer issues; this pass targets the
newer **Map / Camera / Curse / Core** work that grew this session.

---

## CRITICAL
None. No crash / data-corruption bugs in the new code.

---

## HIGH

- [x] **CurseOverlay.Initialise() is never called** (`Curse/CurseOverlay.cs:34`).
  The overlay builds its sprite pool in `Initialise(world)`, but nothing calls it.
  Until a cell *changes*, no overlay sprites exist — so the **initial seeded curse
  density is invisible at battle start**. Fix: call `Initialise` from BattleManager
  when a battle begins, OR confirm the on-demand `CreateOverlay` path is intended and
  delete `Initialise`.

- [x] **HubMap.OnSurge fires but has zero subscribers** (`Curse/HubMap.cs:26,148`).
  `CurseDefinition.surgeThreshold`'s tooltip promises "AI buffs / new rituals spawn"
  on surge — that behavior is **not wired**. Fix: subscribe a listener, or mark it a
  documented TODO so the tooltip isn't mistaken for live behavior.

- [x] **ZoneExit re-arm gap on rapid scene reload** (`Map/ZoneExit.cs:44-49`).
  `OnEnable` does `Invoke(nameof(Arm), armDelay)`; fast disable/enable can stack Arm
  invokes. Low frequency but real. Fix: add `CancelInvoke()` at the top of `OnEnable`.

---

## MEDIUM

- [x] **Zone→zone round-trip is only half-wired** (`Map/ZoneExit.cs:23-27, 85-93`).
  `ZoneExit` has `entryPoint` + `entryId` fields but `Fire()` never calls
  `TravelIntent.SetEntry()` before loading. So travelling *out* of a zone doesn't tell
  the destination where to spawn. Only `WorldMapUI.TravelTo` sets the intent (map→zone
  works; zone→zone doesn't). Fix: call `TravelIntent.SetEntry(entryId)` in `ZoneExit.Fire`,
  or remove the two dead fields if zone→zone isn't planned.

- [x] **DynamicZoom queries Camera.main every LateUpdate** (`Camera/DynamicZoom.cs:84`).
  The bottom-edge clamp calls `Camera.main` each frame (a tagged FindObject under the hood).
  Prior audit fixed this exact pattern in SpriteBillboard/DepthOfFieldFocus; this one was
  missed. Fix: cache the camera like the other two.

- [x] **DynamicZoom AddComponent<CinemachineFollow> at runtime in Awake** (`Camera/DynamicZoom.cs:50`).
  If the camera already has a body/composer authored in the scene, silently adding a second
  Follow can conflict in ways that don't reproduce from the serialized scene. Fix: require
  the component in-editor rather than runtime-add, or log when auto-added.

- [x] **restrictToNeighbors defaults false, so adjacency gating is inert** (`Map/WorldMapUI.cs:19`).
  The whole IsReachable / currentNodeId / neighbor path never runs with the shipped default —
  so it's untested in practice. Fix: confirm intended default; test with it on.

---

## LOW

- [x] **ZoneExit ToWorldMap ignores targetScene, hardcodes "WorldMap"** (`Map/ZoneExit.cs:16 vs 74-77`).
  Confusing coupling. Use `targetScene` in both modes or comment why ToWorldMap is fixed.

- [x] **HubMap.Cleanse / ActivateRitual / GetBattleSeedCurse have no callers** (`Curse/HubMap.cs:162,172,191`).
  API built ahead of use (clerics/rituals not wired yet). Fine to keep — just flag so the
  "Called by..." comments aren't mistaken for live wiring. Only `GlobalCurseLevel()` is consumed.

- [x] **mapPosition stored/copied but never read** (`Curse/HubNode.cs:11`, `HubMap.cs:57,205`).
  Only `mapImagePosition` is used (pin placement). `mapPosition` is `{0,0}` on all 6 nodes.
  Remove, or repurpose for auto-layout.

- [x] **PlayerController.SetFacing has a dead null-check** (`Player/PlayerController.cs:64-71`).
  `[RequireComponent(typeof(Animator))]` guarantees `_anim`. Harmless. Optional cleanup.

---

## CLEANUP — orphaned assets/scripts (safe to delete, confirmed by GUID cross-reference)

- [x] **Camera/CameraZoneTrigger.cs** — referenced in NO scene/prefab. The abandoned
  river-camera-blend approach. Delete script + .meta.
- [x] **Camera/BottomFade.cs** — referenced in NO scene/prefab. Superseded by the ground/edge
  fade approach. Delete if the bottom-gradient idea is abandoned.
- [x] **Assets/Art/Map/EdgeFade.mat + EdgeFadeGradient.png** — the abandoned dark-fade edge
  (replaced by the Arno river). Nothing references either. Delete both + .meta.
- [x] **6 preview source .jpg files** in `Assets/Art/Previews/` — the transparent .png versions
  are what's used; the source JPGs (e.g. "Cathedral of Santa Maria del Fiore.jpg") linger.
  Deletable. (Also: "Basilica of Santa Maria Novella..." has a doubled filename.)

**Confirmed STILL USED (do NOT delete):** ZoneEntryPoint, ZoneEntryPlacer, ZoneExit,
DynamicZoom, SpriteBillboard, OcclusionFade — all referenced in MercatoVecchio.
`SouthExit` object is legit (holds ExitZone_South trigger + EntryPoint_South + WaterTile).

---

## Health notes (things that are GOOD)
- No missing script references in either scene.
- No event-subscription leaks in new code: WorldMapUI (OnNodeChanged) and CurseOverlay
  (OnCellCurseChanged) both unsubscribe correctly. Singletons (HubMap, CameraShake,
  BattleCurseAutomata) all guard the duplicate case.
- Build settings correct: MercatoVecchio + WorldMap, both enabled.
- Big GLB source refs (arch/ponte/sign, ~60MB each) are gitignored; imported 62MB Sign
  was removed pre-commit (re-import from Refrences when placing it).

---

## Suggested order for tomorrow
1. **CLEANUP first (zero risk, 5 min):** delete CameraZoneTrigger.cs, BottomFade.cs,
   EdgeFade.mat + .png, and the 6 preview JPGs. Instantly lighter, no behavior change.
2. **HIGH — CurseOverlay.Initialise:** decide wire-vs-delete so curse visuals show at
   battle start. (Blocks nothing today since battles aren't the current focus.)
3. **MEDIUM — ZoneExit → TravelIntent:** wire it (or strip dead fields) so zone→zone
   travel spawns the player correctly. Relevant once a second explorable zone exists.
4. The rest (Camera.main caching, OnSurge wiring, adjacency default) are polish — batch
   them whenever we next touch those systems.

Deferred by design (from BUG_AUDIT.md): MenuManager.OpenCharacterSheet index +
CharacterSheet placeholder stats — waiting on the Job/party data task.
