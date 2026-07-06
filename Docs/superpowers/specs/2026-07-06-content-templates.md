# Content Templates — Street Kit + Battle Map Kit

**Status:** Direction from David 7/06: "create prefabs for 2 types of content" —
(1) Persona-5-style straight east-west streets with shop entrances on both sides,
(2) FFT-style battle map prefabs — duplicate, add obstacles/props, new map.
Both are DUPLICATE-AND-DRESS workflows: a base prefab plus tiny authoring
components, so new content is a copy + prop swap, not a new build script.

## 1. Street Kit (`Street_EW`)

Persona 5 street idiom translated to the HD-2D rig: a straight east-west
corridor, tall shop-houses on the NORTH side (camera-facing facades, doors at
street level), LOW botteghe on the SOUTH side (≤4.2 wu — the pitch-40 camera
sees over them; the PV corridor proved 5 wu works), skyline caps at both ends.

**Coordinate contract:** street runs along X, −30..30. Walkable Z −5..+5.
Paving 64×14 top y=0 over a lower outskirt apron. Entries at x ∓27 facing
inward; ExitZones at x ∓29 (ToWorldMap by default — retarget per street to
door-link zones, the Salone↔Signoria pattern).

**Template prefab:** `Assets/Prefabs/Templates/Street_EW_Template.prefab`
- `[Ground]` paving + apron (frictionless colliders).
- `[NorthRow]` 5 slots `SLOT_N1..N5` (x −24,−12,0,12,24) each holding a
  default Mercato building (Apartment_NE@180 / Apartment1@270 alternating,
  wall fronts flush at z ≈ +5.6). Swap the child under a slot to re-dress.
- `[SouthRow]` 5 low bottega blockouts (10×4.2×4, faces z −5.5) with awning
  slabs + door recesses.
- `DOOR_N1..N5` / `DOOR_S1..S5` anchors, each with a **ShopDoor** trigger:
  set `targetScene`/`targetEntryId` to make it a real entrance (interior scene
  or existing zone); left empty it logs "shop closed" (dwell-trigger pattern,
  armDelay like ZoneExit so arrivals don't insta-fire).
- `[Backdrops]` Gen_Backdrop_Skyline quads hugging all four sides (E/W ±34,
  N +15, S −13, y 15 — the Signoria edge recipe).
- `[Boundaries]` + ENTRY/Exit wiring.

**Menu:** `InfernosCurse/Templates/1. Build Street Template Prefab` (destructive
rebuild of the prefab) and `2. New Street Scene From Template` — fresh scene,
template instance, player copy (from SaloneDelleArti), HD2D kit in CLEARANCE
zoom (close 3 / wide 9 / minArchitectureHeight 2.5 — the PV corridor mode),
edit sun, saved as `Assets/Scenes/NewStreet.unity` (rename after) + Build
Settings. Workflow: run menu 2 → rename scene → swap slot buildings/props →
set ShopDoor targets → wire exits → done, new street.

## 2. Battle Map Kit (FFT maps)

The battle system is ALREADY FFT-isometric: `BattleGrid.GridToWorld` is a
diamond projection on the XY sprite plane and `GridCell` carries
`elevation` (half-units) + `walkable`. What's missing is authored MAPS — the
arena builds a flat inline checkerboard. The kit makes maps prefabs.

**Runtime components (Assets/Scripts/Battle/):**
- `BattleMapAuthoring` (prefab root): grid `width/height`, `tileWidth/Height`
  (must match BattleGrid: 1 / 0.5), serializable `plateaus` list
  (rect + elevation), `partySpawns` / `enemySpawns` cell lists.
  `Apply(BattleGrid)` = Initialize + stamp plateau elevations + stamp child
  obstacles. `applyOnAwake` finds the scene grid and applies before battle
  start (grid Awake self-initializes; authoring re-initializes with map data).
  Elevation 2+ blocks LoS via the existing Bresenham rule — walls happen for free.
- `BattleObstacle` (any prop in the map): `cell`, `makeUnwalkable`,
  `addedElevation` (for crates you can stand ON, set walkable + elevation
  instead). ContextMenu "Snap To Cell" positions the prop sprite at the iso
  point of its cell.

**Authoring workflow (the ask):** duplicate `BattleMap_Field.prefab`, move /
add obstacle sprites, set their cells (or snap), adjust plateaus → new map.
No code. Callers keep passing spawns to `StartBattle`; map prefabs expose
suggested spawns for future encounter-table use. RULE: keep the default
spawn columns (x 1-2 and width−2..width−1) walkable in every map.

**Prefabs (menu `InfernosCurse/Templates/3. Build Battle Map Prefabs`):**
- `Assets/Prefabs/Battle/Maps/BattleMap_Field.prefab` — 14×12 flat, tile
  sprites (parchment-earth tones, elevation-tinted), a few crate/rock
  obstacles mid-field.
- `BattleMap_Ruins.prefab` — same bones + a 2-elevation plateau (LoS-blocking
  high ground) + broken-pillar obstacles. Proof of duplicate-and-dress.
- Tiles: flat diamonds, sortingOrder 0 (elevation = Y offset + lighter tint +
  dark skirt at order −1). Proper per-unit iso sorting = later art pass.

**Arena integration:** BattleArena.unity swaps its inline checkerboard for a
`BattleMap_Field` instance (visuals + authoring apply). TestStarter spawns
unchanged (kept walkable per the rule). Road encounters keep working — the
map only changes terrain, not flow. Per-node map selection = future work
(PendingEncounter carries a map id → EncounterBootstrap instantiates).

## Deferred
- Shop interiors / shop UI behind ShopDoor (doors log until targets exist).
- Street variants (N-S orientation, corners, plazas) — copy the builder.
- Battle map iso-sorting art pass + real tile/prop sprites via workbench.
- Encounter-table → map-prefab mapping per district/waypoint.

## Related memory
[[pv_scene_layout]] (corridor camera), [[piazza_signoria]] (edge recipe, GLB
facings, wedge gotchas), [[battle_system_state]] (arena conventions, spawn
flow), [[hd2d_scene_setup]]
