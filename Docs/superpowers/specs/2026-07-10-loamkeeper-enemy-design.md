# Loamkeeper Enemy Design

## Summary

The Loamkeeper is a recurring low-level enemy for garden-like environments. He is the corpse of a groundskeeper reanimated by cursed soil and forced to continue tending the places where he died. His decayed straw hat, thorn-vine cloak, fused rusted shears, hunched posture, and workmanlike movements distinguish him from the plant-bodied Rosekin.

The Loamkeeper is designed for gardens, vineyards, orchards, cloisters, courtyards, monastery grounds, and overgrown estates. He is not unique to Giardino delle Rose. Encounters may use several Loamkeepers together, where their modest forced movement becomes more dangerous through coordination.

## Combat Role

The Loamkeeper is a low-level melee control grunt.

- Slow movement and low tactical intelligence.
- Moderate durability for an early enemy.
- Modest individual damage.
- Most dangerous when multiple Loamkeepers pull targets out of position.
- Prefers Root-Hook at exactly two tiles and Rusted Snip when adjacent.
- Advances toward the nearest reachable hostile when neither skill has a valid target.
- Leaves temporary corrupted soil behind when defeated.

He must remain clearly weaker than an elite, specialist, or miniboss. His interest comes from positioning and terrain consequences rather than inflated health or damage.

## Identity and Narrative

The cursed soil remembers the groundskeeper's labor but not his humanity. It raises his corpse whenever roots are disturbed and compels him to prune living flesh as though it were an unruly hedge. His shears have fused to one hand, while roots and garden refuse hold the body together beneath a thorn-vine cloak.

The display name is **Loamkeeper**. Variants may later use the same family name, such as Blighted Loamkeeper or Cloister Loamkeeper, without changing this base enemy's scope.

## PixelLab Source and Import Contract

- PixelLab character ID: `fdaea102-2e8f-44df-8bb6-d2257b10640f`
- Source name: `Hunched gardener, rusted shears fused`
- Source prompt: `Hunched gardener, rusted shears fused to hand, thorn-vine cloak, decayed straw hat`
- Source resolution: 244 by 244 pixels.
- View: high top-down.
- Template: mannequin.
- Rotations: eight static directions.

Production assets belong under `Assets/Characters/Loamkeeper/`. The source character ID and animation group IDs must be retained in import metadata or the deterministic builder so the asset can be refreshed without rediscovery.

Texture imports use transparency, nearest-neighbor filtering, no compression, no mipmaps, and a consistent pixels-per-unit value matching the existing HD-2D battle characters. The builder must normalize the visible sprite to the battle grid without cropping the shears or thorn cloak.

## Animation Contract

PixelLab currently exposes the following complete animation groups:

1. Crouched walking, group `a229603e-69be-4810-8fe8-e8db7532bab4`:
   - South, north, east, and west.
   - Six frames per direction.
2. Rusted Snip, group `b8051933-6f81-4ba0-bc73-07bc23cddbc6`:
   - South, north, east, and west.
   - Nine frames per direction.
3. Grave Mulch/death reaction, group `589c2c9d-44a5-403d-b649-8d0c2e4485d3`:
   - South, north, east, and west.
   - Nine frames per direction.
4. Root-Hook, group `fc552ee0-d093-47ec-97ee-cc94609fbf2e`:
   - South, north, east, and west.
   - Nine frames per direction.

All four cardinal arrays must be wired directly into `CombatantData`. No south-facing fallback is required for these groups. The eight static rotations provide idle presentation; the combat system continues to collapse visual facing to the four cardinal directions used by tactical animation.

The production import contains 132 animation frames plus eight idle rotations. A validator must reject missing directions, incorrect frame counts, absent skill-animation bindings, or a mismatched PixelLab source ID.

## Skill: Rusted Snip

Rusted Snip is an inexpensive adjacent attack using the fused shears.

- Type: active.
- Damage: physical.
- Target: one hostile unit.
- Minimum range: 1.
- Maximum range: 1.
- Area: 0.
- Base power: 9.
- Base hit: 0.90.
- SP cost: 2.
- Charge: instant.
- Requires line of sight.
- Absorbable by Benidito.
- Maximum absorbed level: 5.
- Equipped absorbed bonus: Dexterity, +1 per slotted orb level.
- Insanity cost: 1 percentage point per slotted orb level.

Duplicate drops follow the existing absorbed-orb rules. Each owned orb may be slotted up to the five-level cap, raising power, SP cost, Dexterity, and insanity through the established `AbsorbedSkillInstance` model.

The future Holy refinement is **Merciful Sever**. It keeps the adjacent attack shape and becomes Holy damage. This design records the counterpart so implementation can make the existing Church refinement path functional rather than leaving `holyVersion` empty.

## Skill: Root-Hook

Root-Hook is a light attack and early battlefield-control skill.

- Type: active.
- Damage: physical.
- Target: one hostile unit.
- Minimum range: 2.
- Maximum range: 2.
- Area: 0.
- Base power: 6.
- Base hit: 0.80.
- SP cost: 5.
- Charge: instant.
- Requires line of sight.
- Absorbable by Benidito.
- Maximum absorbed level: 5.
- Equipped absorbed bonus: Strength, +1 per slotted orb level.
- Insanity cost: 2 percentage points per slotted orb level.

On a successful hit, Root-Hook attempts to move the target one grid cell toward the caster. The pull succeeds only when the destination:

- Is inside the authored battle grid.
- Is walkable for the target.
- Is not occupied or reserved.
- Does not cross an impassable edge or violate elevation rules.
- Does not move an immune objective, fixed encounter actor, or explicitly immovable unit.

Damage still resolves when the pull destination is invalid. The failed pull must produce readable feedback without throwing an exception or corrupting grid occupancy.

The future Holy refinement is **Shepherd's Crook**. It retains the pull and changes its damage to Holy.

## Innate Trait: Grave Mulch

Grave Mulch is intrinsic to the Loamkeeper and is not absorbable.

When the Loamkeeper dies, his tile becomes temporary corrupted soil for two rounds. A unit that ends its turn on the tile receives Poison for two target turns at 5% maximum HP per tick. Re-entering or ending another turn on Grave Mulch refreshes this duration but does not stack its magnitude.

Grave Mulch must use terrain snapshot and restoration behavior:

- Preserve the authored terrain state before applying the temporary effect.
- Do not overwrite objectives, permanent obstacles, encounter-critical terrain, or protected authored tiles.
- Restore the original terrain after two rounds.
- Cleanly restore the terrain if combat ends early.
- Multiple applications may refresh duration but may not destroy the original snapshot.

## Enemy Data

A new `Enemy_Loamkeeper` `CombatantData` asset owns the production configuration.

- Role: Enemy.
- Intelligence: 2, reflecting a corpse following remembered labor rather than tactical reasoning.
- Level: 1.
- HP maximum: 80.
- SP maximum: 20.
- Strength: 9.
- Dexterity: 8.
- Constitution: 10.
- Creativity: 4.
- Faith: 2.
- Perception: 7.
- Speed: 6.
- Sight range: 9 grid cells.
- Eye height: 2 elevation half-units.
- Active skills: Rusted Snip and Root-Hook.
- Learnable skills: Rusted Snip and Root-Hook.
- Skill-drop chance: 0.30 per defeated Loamkeeper, selecting one of the two learnable skills through the existing random-drop behavior.
- Grave Mulch is configured as an innate death trait, not an active or learnable skill.

The asset must not reuse Rosekin identity, portrait, animations, or battle sprite references.

## Architecture

The implementation follows existing `CombatantData`, `BattleSkillAnimation`, `SkillDefinition`, `BattleUnit`, and `AbilityResolver` patterns.

Two focused extensions are required:

1. A skill-effect contract for forced movement. It runs after a successful damaging hit, validates the destination through the battle grid, and updates occupancy through existing grid APIs.
2. A death-trait contract for temporary terrain. It listens to the unit's resolved death, snapshots the tile, applies the authored temporary state, and restores it deterministically.

These extensions must be data-driven enough for later enemies and skills but must not become a general scripting framework. The Loamkeeper only requires one-cell pull and one temporary death tile.

A deterministic editor builder imports the PixelLab images, applies texture settings, creates or refreshes the skill assets and Holy counterparts, wires `Enemy_Loamkeeper`, and registers the character with the existing asset registry where required. A validator confirms the complete source, animation, skill, and combatant contract.

## Encounter Use

The Loamkeeper may appear in all garden-like zones. Initial production placement should use an existing hybrid garden encounter rather than creating a separate test-only battle map. Encounter composition should begin with one or two Loamkeepers alongside terrain that makes a one-cell pull meaningful.

The base enemy should not appear in ordinary streets, interiors without substantial planted grounds, or locations where the soil-and-gardener origin would be visually incoherent.

## Failure Handling

- Missing PixelLab images fail validation with the missing group and direction named.
- A blocked or invalid Root-Hook pull leaves the target in place while retaining resolved damage.
- A missing temporary-terrain service suppresses Grave Mulch with one clear warning rather than breaking death resolution.
- Protected terrain rejects Grave Mulch and remains unchanged.
- Rebuilding is idempotent and must not duplicate assets, animation references, registry entries, or encounter actors.
- PixelLab credentials remain outside tracked files.

## Verification

Static and editor verification must confirm:

- Eight idle rotations are imported.
- Walking has four directions with six frames each.
- Each custom animation has four directions with nine frames each.
- Rusted Snip and Root-Hook use their correct animation groups.
- Grave Mulch uses the approved death reaction.
- Both active skills are equipped and learnable.
- Both Holy counterparts are assigned.
- Texture settings match the HD-2D character standard.

Play-mode verification must confirm:

- Cardinal walking displays the correct direction.
- Rusted Snip plays correctly in all four directions and deals expected damage.
- Root-Hook plays correctly in all four directions.
- A valid Root-Hook pull updates position and occupancy by one tile.
- Blocked, occupied, edge-of-grid, elevation-invalid, and immovable pulls deal damage without moving the target.
- Defeat creates Grave Mulch on an eligible tile.
- Grave Mulch poisons a unit ending a turn on it.
- Grave Mulch expires after two rounds and restores the original tile.
- Combat ending early restores temporary terrain.
- Benidito can absorb both active skills and cast them from absorbed slots.
- Duplicate orbs follow the existing five-level growth and insanity rules.
- Church refinement produces the correct Holy counterpart.
- The final Unity console contains no new errors or warnings attributable to the Loamkeeper pass.

## Out of Scope

- Elite or boss Loamkeeper variants.
- Additional animation generation.
- Dialogue, quests, or a named individual version.
- Broad enemy-AI refactoring.
- A universal arbitrary skill-effect scripting language.
- Placement in non-garden environments.
