# Salone delle Arti — Zone Design

**Status:** Approved by David 2026-07-05 (design + both floorplan sheets). Nothing built yet.
**Floorplans of record:** `Refrences/images/salone-floorplan-terra.png` (ground floor) and
`salone-floorplan-galleria.png` (gallery level) — Gemini-generated in the Ferrerio engraving
style David supplied as reference; approved in chat 7/05. Workbench entries deliberately left
at status `generated` (NOT `approved`) so no 3D batch can ever submit a floorplan to Prism.

## Purpose

The central guildhall of Florence: one grand multi-level interior where all the Arti
Maggiori live. Consolidates the guild gameplay (standings, donations, joining, rep) that is
currently scattered across street-level interaction zones, and gives the guild system a
home stage for future faction content (elections, disputes, commissions). First true
multi-floor interior in the game.

## Map placement

Enter from `signoria` — the hall stands beside the rising Palazzo dei Priori (begun 1299,
a nice period rhyme). District pin on the City sheet of Gugol Mappe.

## HubNode data (via GugolMappeSetup.WireNodes / AddTeaserNode — the standard idiom)

| Field | Value |
|---|---|
| id | `salone_arti` |
| displayName | Salone delle Arti |
| sceneName | `SaloneDelleArti` |
| entryId | `salone_door` |
| kind | `NodeKind.District` |
| mapLevel | `MapLevel.City` |
| neighbors | `signoria` |
| microclimate | same value as the Duomo node (interior precedent — read it off the prefab node data in the setup step; interiors keep COZY running, only battle scenes block it) |
| curse/sanctity | placeholder mid/low; the hall is civic, not sacred |

Pin unlocks when `SaloneDelleArti.unity` enters Build Settings (autohide rule — no code).

## Zone layout & coordinate contract

Both sheets are read **rotated 180° from their printed orientation** so that:

- **South (world −Z):** entrance vestibule + porch (the sheet-top projecting porch) — `ENTRY_salone_door`.
- **South corners:** twin spiral stair turrets (present on BOTH sheets in matching corners — this is what makes the levels stack).
- **East/West walls:** eight semicircular alcove bays (sheet shows 4+4). Assignment: **seven Arti Maggiori + one Albergatori bay** (the minor inn-guild gets the humblest bay, nearest the door).
- **North (world +Z):** the apsed niche + broad steps = the **council dais**.
- **Hall center:** column dots in a cross pattern → columns as placed props (LoS/occlusion interest).
- **Gallery level (+5 wu):** rectangular walkway ring between outer wall and balustrade, open void over the hall; **chapel alcove at the north end of the ring** (Church presence, above the dais). Reached only by the two south spiral stairs.

**Scale contract:** hall interior rectangle (inside face of poché walls on the terra sheet)
maps to **36 × 16 wu** (world X = east-west 16, world Z = north-south 36). Measure the sheet's
interior-rectangle pixel bounds at build time and derive px→wu from that rectangle — do NOT
assume a fixed px/wu constant (Gemini sheets have arbitrary margins; the Duomo "14px = 1wu"
constant was specific to its map). Origin = hall center. Walls H = 10, T = 0.6 (Duomo values).
Gallery floor at +5, balustrade H = 1.1, gallery ring width from the galleria sheet (~2.5 wu).

## Guild data (7 new GuildDefinition assets)

Ids and display names (APPEND to existing `albergatori`, `church` — never rename those):
`calimala` (Arte di Calimala), `lana` (Arte della Lana), `cambio` (Arte del Cambio),
`giudici_notai` (Giudici e Notai), `por_santa_maria` (Por Santa Maria), `medici_speziali`
(Medici e Speziali), `vaiai_pellicciai` (Vaiai e Pellicciai).

- Italian rank names per guild (follow `Guild_Albergatori`'s ladder shape; distinct flavor per Arte).
- Perks use the EXISTING `GuildPerkType` values only (new perk types = code change — out of
  scope this pass). Guild perks bend the corruption economy, never zero it (house rule).
- `homeNodeId`: assign each Arte a sensible existing district (lana→oltrarno, cambio→mercato,
  por_santa_maria→ponte area, etc. — final mapping at implementation; battle rep-awards key
  off these).
- All 7 wired into `GuildSystem.guilds` on GameSystems.prefab via the editor setup step.
- Rep/donation flavor text: words, never numbers (house rule).

## Interactions (GuildInteractionSpawner — no scene-file surgery)

Per Arte bay: one `Donation` zone + one `Join` zone (`GuildPanelUI.OpenJoin` takes a single
guildId, so joining happens at the guild's own bay — the pledge kneel faces the bay banner).
One shared `Standings` hotspot near the dais. Church chapel on the gallery: `Donation`
(Church). All zones are `GuildInteractionSpawner` data entries — cheap. Transmute stays
Church-only and stays in the Duomo scope.

## Multi-level tech

- Stairs: the two spiral turrets are ramps in disguise — helical or switchback ramp colliders
  ≤ 40° (SnapToGround's `maxWalkableSlope`; ramp-climb verified in Giardino 7/05).
- **`LevelVisibilityFader`** (new, small): toggles renderers by GameObject name prefix
  (`Level2_`) based on player Y threshold — the Duomo dollhouse idiom generalized from
  camera-ray to player-height. When player is on the hall floor, `Level2_*` hides so the
  camera sees down into the hall; on the gallery, everything shows (camera looks across the void).
- Gallery floor gets a hole (the void) — build the ring floor as 4 rectangles, not a plane
  with a hole.
- HD2D_CameraKit as-is; DynamicZoom **clearance mode** (interior corridors — HD2D_Camera.md).
- Fall prevention at the void: balustrade colliders all the way around (every renderer gets
  a collider — project census rule).

## Scene build order (numbered menu items, `InfernosCurse/Salone delle Arti/…`)

1. **Build Scene** — `SaloneDelleArtiSceneBuilder`: shell from the coordinate contract
   (floor, poché walls, bays, dais + steps, columns, vestibule, stair turrets w/ ramp
   colliders, gallery ring + balustrade + chapel alcove, `Level2_` prefixes), MARKER_ drops
   for hero props, ENTRY/exit wiring, boundaries, edit-mode sun, player copy, camera kit,
   save + Build Settings. Deterministic; destructive full rebuild.
2. *(external)* Hero-prop batch via workbench — **its own David-approval round**: dais
   throne-bench, guild banner ×8 (per-Arte heraldry), bay bench + strongbox, notary lectern,
   chapel altar, iron chandelier. (~8–10 models ≈ 500–600 cr — get approval on concepts AND
   on the spend.)
3. **Place Hero Props** — marker consumption, footprint-max grounding (interior floors are
   flat; grounding trivial), AxisFix table as models land.
4. **Setup Guilds** — creates the 7 GuildDefinition assets (skip-if-exists), wires
   GuildSystem.guilds + GuildInteractionSpawner entries on GameSystems.prefab
   (LoadPrefabContents pattern), registers the `salone_arti` node via WireNodes-style
   AddTeaserNode, re-runs AssetRegistry population if needed.
5. **Floor/wall material pass** — DuomoTilePass pattern: world-aligned generated URP Lit
   materials (terracotta floor, pietra serena walls) — tiles generated via workbench
   (2D, no approval-status flip).

## Gotchas carried forward (do not relearn)

- Coordinate contract measured off the sheet's interior rectangle, not assumed px/wu.
- `Level2_` name prefix is LOAD-BEARING for the fader (Duomo name-coupling lesson — document
  in the scene doc).
- Interior scene ⇒ authored lights are edit-mode only (CozySceneAdapter kills suns); interior
  mood comes from TorchFlicker spots (Spot type for cookies) + LightShaft from high windows.
- GuildInteractionSpawner `sceneName` must EXACTLY equal `SaloneDelleArti` (the
  Duomo_DISABLED_* suffix mistake).
- Enums APPEND ONLY; new guild ids are new DATA, not new enum members.
- Marker ids stay kebab-case and `@`-free; heights `{h:0.##}` InvariantCulture.
- EventSystem: panels self-build — GuildPanelUI already guards; scene needs no authored canvas.
- Spiral stair ramps must be tested with the SnapToGround MaxStepUp=0.45 rule (step risers,
  if modeled, must be ≤0.45 or built as smooth ramp colliders).

## Deferred (explicitly out of scope this pass)

- Guild elections / disputes / commissions gameplay; per-Arte quests.
- New GuildPerkType values (code change — own task).
- Undercroft level (option we declined — door can be hinted in dressing).
- Transmute relocation; Duomo zone re-enable.
- Per-guild NPC masters (job-system lineage tie-in — see job_system_design memory).

## Related memory

[[guild_system]], [[duomo_scene]], [[hd2d_scene_setup]], [[giardino_delle_rose]] (grounding
patterns), [[asset_gen_approval_rule]], [[gugol_mappe]] (WireNodes idiom)
