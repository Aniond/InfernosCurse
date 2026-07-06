# Piazza della Signoria — Zone Design

**Status:** DRAFT — awaiting David's approval (design + plan sheet + construction concept).
**Sheets of record (once approved):** `Refrences/images/signoria-piazza-plan.png` (top-down
plan, Ferrerio engraving style) and `signoria-construction-concept.png` (palazzo
work-site mood reference). Workbench entries stay status `generated` (2D-only — NEVER
`approved`, so no floorplan can reach the Prism batch).

## Purpose

The civic heart of Florence and the hub junction of the world map — six zones touch the
`signoria` node (mercato, duomo, pontevecchio, santacroce, giardino_rose, salone_arti), and
until now it has been a pin with lore but no scene. First outdoor *civic* zone (Mercato is
commerce, Duomo is sacred): the seat of the Priori, proclamations, punishments, and the
city's raw ambition in scaffolding.

## The 1299 premise (why this piazza is special)

- **The Palazzo dei Priori is a construction site.** Arnolfo di Cambio broke ground in 1299
  — the year the game is set. No finished palazzo, no tower silhouette: rising rusticated
  pietra-forte walls (~3–6 wu on part of the footprint), the old **Foraboschi tower stump**
  swallowed by the new work, timber scaffolding, a treadwheel crane, stone piles, masons'
  lodge. (The map preview splash shows the finished palazzo — keep it: that's an
  aspirational vignette, not the in-scene state.)
- **The Uberti waste.** The piazza exists because the Ghibelline Uberti's houses were razed
  (1268) and the ground declared forever unbuildable — cursed ground by civic decree. In a
  curse game this is a gift: a bare, salt-sown patch in the paving where nothing grows and
  nothing stands. Dressing-only this pass; future event/ritual site. (Node
  startingCurseLevel 0.15 finally has a visible anchor.)
- **The comune's lions.** Florence kept live lions (the city's living Marzocco) caged near
  the civic center from the 13th century — Via dei Leoni is named for them. A barred lion
  den against the work-site fence is the zone's signature prop.
- **No Loggia dei Lanzi** (1376), no ringhiera (1323), no Neptune, no David. San Pier
  Scheraggio (the church the Uffizi later ate) stands at the southeast edge.

## Map/node wiring (data already exists — this zone just fills it in)

| Field | Value |
|---|---|
| id | `signoria` (existing node — do NOT create a new one) |
| sceneName | `PiazzaDellaSignoria` (fill the empty field) |
| entryId | `signoria_south` |
| everything else | keep as-is (blurb, pin 0.82/0.24 on city sheet, microClimate 2, curse 0.15, sanctity 0.2, neighbors) |

Pin's Travel button unlocks when the scene enters Build Settings (autohide rule — no code).
`cambio`'s homeNodeId is `signoria`, so any battle rep here already feeds Arte del Cambio.

## Zone layout & coordinate contract

Outdoor diorama per the Mercato recipe (HD2D_CameraKit, buildings scale 8.5, player 1.0,
solid-building boundaries, DoF backdrop melt). Entrance from the **south** (−Z), matching
every other zone.

- **Overall paved footprint ~44 × 34 wu** (X east-west 44, Z north-south 34), origin at
  piazza center. The walkable space reads as an **L** — the historic shape — because the
  construction site fences off the northeast quadrant.
- **NE quadrant: the work site** (~20 × 14 wu, fenced by timber-and-rope barrier, one gap
  for carts). Inside: partial palazzo walls (procedural blockout, rusticated stone material,
  stepped heights 2–6 wu), Foraboschi tower stump (~12 wu, weathered older stone), timber
  scaffolding (builder geometry: poles/planks/diagonals against the rising faces),
  treadwheel crane (hero GLB), stone-block piles, mortar troughs, masons' lodge lean-to.
  Site interior is walkable (duck under the rope at the cart gap) but is dressing, not quest
  space, this pass.
- **SE edge: San Pier Scheraggio** — reuse Mercato `Church1.glb` at 8.5, re-seated.
- **West side: the Salone delle Arti facade** — blockout exterior matching the Salone's
  porch/vestibule massing, with a **working door → loads `SaloneDelleArti` @ `salone_door`**
  (the Mercato↔PV direct-link pattern). The guildhall finally has a street address.
- **SW open corner: the Uberti waste** — paving gives way to bare scorched earth, broken
  foundation stubs, salt-white streaks, no props, nothing grows. Subtle — no VFX this pass.
- **Center-west: civic dressing** — herald's platform (hero GLB), comune flagpole pair with
  banner quads (red lily on white = Comune; red cross on white = Popolo — 2D magenta-key
  pipeline, 0 credits), reuse `stone-wellhead.glb`, stone benches, barrels, a cypress or two
  at the church.
- **North edge: tower houses** (reuse Mercato Apartment/Townhouse GLBs, vary rotation/tint)
  with a street mouth **exit north → world map** (toward mercato/duomo).
- **South edge: main entry** — street mouth with `ENTRY_signoria_south` + **exit south →
  world map**; Background_13thCentury backdrop beyond the roofline, DoF handles the rest.

## New hero props (Prism 3D — own approval round, concepts first)

~5 models ≈ **300 cr** (60 cr each, well within balance):

1. **Treadwheel crane** — the medieval great-wheel crane; work-site centerpiece.
2. **Lion den** — iron-barred cage, stone base, a maned lion resting inside (if the lion
   gens badly: empty cage + straw, lion as flavor text later).
3. **Masons' lodge** — open-sided timber lean-to, workbenches, hanging tools.
4. **Stone-block pile** — dressed pietra-forte ashlar stack with wooden sledge and ropes
   (place 2–3 instances, varied rotation).
5. **Herald's platform** — small wooden banditore stage with rail and step.

Everything else is reuse (Mercato buildings/props, Giardino wellhead/cypress, PV LightPost)
or builder geometry (palazzo walls, scaffold, fence).

## Scene build order (numbered menu items, `InfernosCurse/Piazza della Signoria/…`)

1. **Build Scene** — `PiazzaSignoriaSceneBuilder`: paving + Uberti waste, palazzo blockout
   + tower stump + scaffold + site fence, building ring (GLB reuse @8.5, re-seated,
   BoxColliders), Salone facade + door exit, church, MARKER_ drops for hero props,
   ENTRY/exit wiring, camera kit, edit-mode sun, save + Build Settings. Deterministic,
   destructive full rebuild.
2. *(external)* Hero-prop batch via workbench — David approves concepts AND spend first.
3. **Place Hero Props** — marker consumption, footprint-max grounding, AxisFix as needed.
4. **Wire Node** — fill `signoria.sceneName`/`entryId` on GameSystems.prefab (LoadPrefabContents
   pattern); verify Salone door round-trip + world-map exits.
5. **Material pass** — worn pietra-forte paving (wheel-rutted), rusticated new stone vs
   weathered tower stone, scorched-earth waste texture (workbench 2D tiles, no approval flip).

## Gotchas carried forward (do not relearn)

- Camera: perspective FOV 36, pitch 40, yaw 0; DynamicZoom **radial** mode (open plaza —
  Mercato's mode, NOT clearance; clearance never reads open in a plaza).
- Buildings scale 8.5 then **re-seat to y=0** (pivot scaling sinks/floats them).
- Every building/prop gets a BoxCollider (GLB imports have none) — boundaries are real
  geometry, not invisible walls; camera must never see past the building line.
- Banner quads: Unity Quad faces −Z; key against MEASURED Gemini pink (~210,30,115), never
  literal #FF00FF; marker ids kebab-case, `@`-free.
- Authored sun is edit-mode only (CozySceneAdapter kills it); this is an OUTDOOR zone —
  COZY weather/time runs live, torches ignite at dusk on their own.
- Scaffold/fence walkability: player must not wedge — keep pole spacing > 1.2 wu or fence
  the tight spots; MaxStepUp 0.45, slope ≤ 40°.
- MCP scene saves: File/Save menu via RunCommand guard ONLY (never full-scene MCP save).
- Scene canaries after every save: 0–1 EventSystem, sane line count, no `(1)` dup suffixes.
- Node edit is a PREFAB edit (GameSystems.prefab), not a scene edit — LoadPrefabContents.

## Deferred (explicitly out of scope this pass)

- Banditore/herald NPC + proclamation events; podestà justice events (pillory, etc.).
- Uberti waste ritual/event content (it's dressing with a curse number, nothing more yet).
- Any guild interaction zones (the Salone next door owns guild UI; adding an Albergatori
  inn on the piazza is a later call).
- Interior of the palazzo (it has no interior — it's a shell going up).
- Job NPC placement (no job maps cleanly to signoria yet).

## Related memory

[[hd2d_scene_setup]] (camera/scale/boundary recipe), [[pv_scene_layout]] (map→world
contract, GLB placement), [[salone_delle_arti]] (banner pipeline, builder patterns),
[[giardino_delle_rose]] (grounding, GameObject scatter), [[mcp_scene_save_corruption]]
(save guard), [[gugol_mappe]], [[guild_system]] (cambio home node)
