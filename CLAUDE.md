# Inferno's Curse — Session Instructions

HD-2D Unity 6 RPG set in 1299 Florence. **Required reading before substantive
work: `Docs/AI_HANDOVER_BLUEPRINT.md`** — architecture map, 43 behavioral
restrictions (each learned from a real incident), bug protocol, tooling. Code
wins over docs; docs win over guesses.

## Hard rules (full list in blueprint §4)

- **No 3D-generation spend without David's explicit image approval** (concepts
  → ≤600px JPEG in chat → STOP → approval → Prism 3.1 batch).
- APPEND-ONLY serialized enums. No allocations in per-frame code. Curse is
  never shown as a number. UI chrome English, place names Italian.
- Scene builders are deterministic menu items (destructive full rebuilds,
  numbered execution order). After scripted saves: duplication canaries
  (0-1 EventSystem, sane line count, no `(1)` suffixes). Never full-scene
  MCP saves on large scenes.
- Authored suns are edit-mode only (COZY owns runtime light/fog/time).

## Reusable content templates (blueprint §6.3; spec `Docs/superpowers/specs/2026-07-06-content-templates.md`)

**New street**: menu `InfernosCurse/Templates/2` (E-W side-view street) or
`Templates/6` (N-S DEPTH street — the Persona-5 down-the-road view; with the
fixed yaw-0 camera, N-S streets recede into the screen and both sides get
full-height buildings — prefer this for shop streets) → rename the scene →
swap buildings under the `SLOT_*` anchors → set `ShopDoor.targetScene` per
entrance (empty = closed) → retarget the end `ZoneExit`s → Build Settings.
Prefab-INSTANCE component edits need
`PrefabUtility.RecordPrefabInstancePropertyModifications` or SaveScene drops
them. Keep prop colliders slim (capsules) and off door lanes.

**New battle map** (FFT): duplicate a prefab in
`Assets/Prefabs/Battle/Maps/` → move/add `BattleObstacle` props (right-click
→ Snap To Cell) → adjust `BattleMapAuthoring` plateaus. Elevation 2+ blocks
LoS. Keep spawn columns (x 1-2, 11-12) walkable.

**New zone from scratch**: follow an existing builder
(`PiazzaSignoriaSceneBuilder` is the most battle-tested outdoor recipe:
paving-under-ring, skyline quads Cull-Back facing inward hugging the
building line, frictionless colliders, marker-driven props, waypoint-driver
playtests).

## Working style

- Playtest walkability with an injected waypoint driver reporting via
  EditorPrefs; diagnose stalls with OverlapBox at the player's real bounds.
- Verify compiles landed before running builder menus after editing them
  (play mode defers compiles; check IsCompiling + a scene canary).
- Session memory lives in Claude's auto-memory; durable project facts belong
  in the blueprint (update contract in its §7).
