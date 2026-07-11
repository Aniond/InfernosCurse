# Limbo Crier Humanoid Enemy Design

## Summary

The Limbo Crier is the common humanoid cultist of Limbo. Criers infiltrate Florence as wandering penitents and public doomsayers, preach that the city is already damned, inflame existing grievances, provoke civic unrest, and make populated districts more receptive to supernatural corruption.

The Crier is a recurring support-controller rather than an elite. One Crier is manageable. Several Criers protected by melee enemies can destabilize an encounter through area debuffs, allied empowerment, forced movement, and temporary corrupted terrain.

This production pass includes the complete enemy, a reusable humanoid battle-visual profile, all required directional animation, armor and weapon data, a portrait, status effects and icons, temporary terrain presentation, AI behavior, absorbable skills, deterministic tooling, and runtime validation.

## Narrative Role

Limbo does not spread only through monsters. Its human servants enter markets, plazas, alleys, church crowds, and tense civic gatherings in the clothing of penitents. They turn hunger, factional resentment, disease, and fear into sermons about inevitable ruin. Their purpose is to make surrender feel reasonable before supernatural corruption arrives.

Criers are ordinary recruits rather than ordained cult officers. Their equipment is practical, concealed, and repeatable. Their visual authority comes from ritual posture, the cracked bell, and the calm iron mask rather than wealth or elaborate armor.

The production display name is **Limbo Crier**. `Cultist of Limbo` is the enemy family and narrative classification.

## Visual Identity

The Crier has a lean, ordinary human build and a readable armed-preacher silhouette.

- Soot-black pointed hood with a ragged shoulder mantle.
- Dull iron half-mask shaped into a calm, expressionless human face.
- Patched charcoal penitential robe over a quilted gambeson.
- Mismatched medieval leather gloves, boots, belts, and pouches.
- Counterfeit relic chains and scraps of condemned proclamations.
- A false reliquary carried at the belt.
- Subtle violet-red corruption visible only through mask vents, bell cracks, the reliquary seam, and stitched robe symbols.
- No ornate plate armor, oversized pauldrons, modern clothing, firearms, or clean ceremonial luxury.

The palette uses charcoal, soot brown, dull iron, old parchment, dried blood red, and restrained violet corruption. Hard dark outlines and three-tone pixel shading must match the project's HD-2D character presentation.

## Equipment

### Bell-Hook Staff

An iron-shod preaching staff combining a shepherd's hook, short polearm, and cracked handbell. It is the Crier's visual and mechanical signature.

- Slot: Weapon.
- Basic attacks become Dark-aligned.
- Strength: +1.
- Perception: +1.
- No range increase for ordinary basic attacks.
- Production icon: transparent 32 by 32 pixel art.

### Crier's Jack

A patched quilted gambeson concealed beneath the penitential robe.

- Slot: Armor.
- Constitution: +2.
- Dark damage received: -10% through the optional equipment damage-modifier contract defined below.
- Production icon: transparent 32 by 32 pixel art.

### False Reliquary

A counterfeit holy container holding a sliver of Limbo-corrupted material.

- Slot: Accessory.
- Creativity: +1.
- Faith: +1.
- Holy damage received: +10% through the optional equipment damage-modifier contract defined below.
- Production icon: transparent 32 by 32 pixel art.

All three pieces are assigned to `Enemy_LimboCrier`. The first sprite set has the equipment baked into every frame for visual consistency. The data assets remain genuine equipment definitions so they may later become uncommon drops without requiring new art.

## Combat Role and Baseline

The Limbo Crier is a fragile common support-controller.

- Role: Enemy.
- Level: 2.
- Intelligence: 5. The Crier understands formations and ally value but is not an elite tactician.
- HP maximum: 72.
- SP maximum: 36.
- Strength: 7.
- Dexterity: 8.
- Constitution: 7 before equipment.
- Creativity: 11 before equipment.
- Faith: 10 before equipment.
- Perception: 10 before equipment.
- Speed: 8.
- Sight range: 12 grid cells.
- Eye height: 2 elevation half-units.

The exact totals include equipped gear through the existing stat pipeline. The Crier is intentionally easier to kill than the Loamkeeper and most frontline humanoids.

## Skill: Bell-Hook Jab

Bell-Hook Jab is the Crier's basic adjacent fallback attack.

- Type: active/basic weapon action.
- Damage: Dark because of the equipped Bell-Hook Staff.
- Target: one hostile unit.
- Minimum range: 1.
- Maximum range: 1.
- Area: 0.
- Base power: 8.
- Base hit: 0.90.
- SP cost: 0.
- Charge: instant.
- Not independently absorbable; Benidito obtains the ordinary weapon attack through equipment rules rather than an orb.

## Skill: Knell of Dread

The Crier rings the cracked bell and makes nearby enemies feel that the end is inevitable.

- Type: active utility.
- Damage: none.
- Center: caster.
- Radius: 2 grid cells.
- Base hit: automatic against ordinary targets.
- SP cost: 9.
- Charge: 12 ticks.
- Applies Dread for two affected-unit turns.
- Requires no line of sight from the caster to individual targets after the audible pulse is released.
- Absorbable by Benidito.
- Maximum absorbed level: 5.
- Equipped absorbed bonus: Faith, +1 per slotted orb level.
- Insanity cost: 2 percentage points per slotted orb level.
- Higher orb levels improve the Faith penalty at levels 3 and 5; radius never increases.

Its future Holy counterpart is **Bell of Vigilance**. The Holy form removes Dread from allies in the same radius and grants +10% CT recovery for two turns.

## Skill: Crooked Benediction

The Crier blesses an allied combatant with a doctrine that converts pain and violence into spreading corruption.

- Type: active utility.
- Damage: none.
- Target: one allied unit other than the caster.
- Minimum range: 1.
- Maximum range: 3.
- Base hit: automatic.
- SP cost: 8.
- Charge: 8 ticks.
- Applies False Zeal for two affected-unit turns.
- Requires line of sight.
- Enemy-only innate doctrine; not absorbable.

The Crier prioritizes the allied unit with the highest expected damage that does not already have False Zeal.

## Skill: Pilgrim's Hook

The Crier catches a target with the hooked staff and drags them out of formation.

- Type: active.
- Damage: physical.
- Target: one hostile unit.
- Minimum range: 2.
- Maximum range: 2.
- Area: 0.
- Base power: 7.
- Base hit: 0.82.
- SP cost: 5.
- Charge: instant.
- Requires line of sight.
- On a successful hit, attempts to pull the target one cell toward the Crier.
- Absorbable by Benidito.
- Maximum absorbed level: 5.
- Equipped absorbed bonus: Perception, +1 per slotted orb level.
- Insanity cost: 2 percentage points per slotted orb level.
- Higher orb levels improve damage only; pull distance remains one cell.

The future Holy counterpart is **Pilgrim's Rescue**. It targets an ally, pulls them one valid cell toward the caster, and applies a one-turn Protect effect instead of dealing damage.

## Status: Dread

Dread is a first-class append-only status effect.

- Duration: two affected-unit turns.
- Faith: -3 at base Knell level, -4 at absorbed level 3, and -5 at absorbed level 5.
- CT recovery: -15%.
- Reapplication refreshes duration and does not stack.
- Holy cleansing removes it.
- Bosses and explicitly steady units may resist the CT penalty while retaining the Faith reduction.
- Tooltip: `The end feels near. Faith and turn speed are reduced.`
- Icon: transparent 32 by 32 cracked black handbell emitting violet rings.
- Battlefield feedback: one dark bell-wave on application and a restrained trembling shadow beneath the affected unit.

## Status: False Zeal

False Zeal is a first-class append-only status effect.

- Duration: two affected-unit turns.
- Outgoing damage: +20%.
- Holy damage received: +15%.
- After every completed action, the unit places Limbo Stain on its current eligible tile.
- Reapplication refreshes duration and does not stack.
- Removing False Zeal prevents future stains but does not remove stains already placed.
- Tooltip: `Limbo lends strength at the cost of spreading its stain.`
- Icon: transparent 32 by 32 broken golden halo sewn together with red-black thread.
- Battlefield feedback: a crooked halo flickers behind the unit and releases dark motes after actions.

## Temporary Terrain: Limbo Stain

Limbo Stain is a one-round temporary terrain effect.

- Deals Dark damage equal to 4% maximum HP to a hostile unit ending its turn on the tile.
- Applies Dread for one affected-unit turn to that hostile unit.
- Does not affect Limbo-aligned combatants.
- Reapplication refreshes duration and does not stack damage or create nested snapshots.
- Cannot replace objectives, permanent obstacles, protected terrain, or a stronger authored corruption state.
- Restores the original authored terrain at expiry or when combat ends.
- Visual: thin violet cracks and an incomplete circular sermon-script sigil with a restrained pulse.

## AI Priorities

The common Crier uses deterministic low-cost priorities:

1. Use Crooked Benediction on the highest-damage valid ally without False Zeal.
2. Use Knell of Dread when it can affect at least two hostile units.
3. Use Pilgrim's Hook when it can displace a protected, ranged, charging, or elevated target.
4. Use Bell-Hook Jab when adjacent.
5. Move toward allied protection or a position that enables the next support action.
6. Retreat rather than advance alone when no allied unit is within four cells.

The Crier does not receive bespoke decision-making outside existing EnemyAI scoring hooks.

## Complete Character Art Package

The Limbo Crier is generated as one consistent PixelLab humanoid character. Armor, mask, staff, bell, and reliquary remain visually identical in every rotation and frame.

Required assets:

- Eight static directional rotations.
- Four-direction walking cycle, six frames per direction: 24 frames.
- Four-direction Bell-Hook Jab, nine frames per direction: 36 frames.
- Four-direction Knell of Dread, nine frames per direction: 36 frames.
- Four-direction Crooked Benediction, nine frames per direction: 36 frames.
- Four-direction Pilgrim's Hook, nine frames per direction: 36 frames.
- Four-direction hurt reaction, four frames per direction: 16 frames.
- Four-direction death/fall animation, nine frames per direction: 36 frames.

The package contains 220 animation frames plus eight rotations, for 228 character images total. A separate breathing idle is excluded; static rotations provide an appropriately controlled, unsettling idle.

## Portrait and Supporting Art

- `Assets/Art/Portraits/portrait-limbo-crier.png`: transparent 128 by 128 pixel-art bust matching the Benidito, Rosekin, and Loamkeeper portrait standard.
- `Assets/UI/StatusIcons/status-dread.png`: transparent 32 by 32 icon.
- `Assets/UI/StatusIcons/status-false-zeal.png`: transparent 32 by 32 icon.
- Equipment icons: transparent 32 by 32 Bell-Hook Staff, Crier's Jack, and False Reliquary.
- Limbo Stain decal and pulse material/VFX sized for one tactical grid cell.

The portrait emphasizes the iron half-mask, soot hood, cracked bell, false reliquary, and restrained corruption light. It contains no background, text, frame, or modern detail.

## Reusable Humanoid Battle Visual Profile

The implementation adds an optional `HumanoidBattleVisualProfile` ScriptableObject rather than a separate combatant hierarchy.

The profile owns:

- Eight static rotations.
- Four cardinal walking arrays.
- Four cardinal hurt arrays.
- Four cardinal death arrays.
- Skill-keyed four-direction action arrays.
- Per-sequence frame rate and expected frame counts.
- Source-generation provider, character ID, animation group IDs, and generation prompt metadata without credentials.

`CombatantData` receives one optional humanoid visual-profile reference. `BattleUnit` queries the profile first for humanoid animation and falls back to its existing inline animation fields when no profile is assigned. Rosekin and other current enemies remain unchanged.

The profile is an animation-data boundary only. Stats, skills, AI, equipment, portrait, drops, alignment, and status behavior remain on existing domain objects.

## Equipment Damage Modifiers

`EquipmentDefinition` receives an optional append-only collection of damage-received modifiers. Each entry pairs one `DamageType` with a signed percentage adjustment. Empty collections preserve every existing item unchanged. Battle damage folds equipped modifiers after ordinary damage calculation and before HP loss, clamping the final multiplier to a safe non-negative value.

The Crier's Jack contributes `Dark: -10%`; the False Reliquary contributes `Holy: +10%`. This narrow contract supports the approved gear without introducing a separate resistance subsystem or changing base character stats.

## Shared Mechanical Services

Pilgrim's Hook and the Loamkeeper's Root-Hook use one forced-movement service. It validates grid bounds, occupancy, reservations, walkability, elevation, impassable edges, protected actors, objectives, and immovable targets. Damage remains resolved when a pull cannot move its target.

Limbo Stain and Grave Mulch use one temporary-terrain snapshot/restore service. It preserves the original cell, rejects protected authored terrain, avoids nested-snapshot corruption, expires deterministically, and restores every temporary change when combat ends.

These are narrow reusable contracts, not an arbitrary skill scripting language.

## Absorption and Refinement

The Crier can drop Knell of Dread or Pilgrim's Hook for Benidito through the existing random learnable-skill selection. Base skill-drop chance is 0.25 per defeated Crier because the enemy is common and may appear in groups.

Crooked Benediction remains enemy-only. Bell-Hook Jab comes from weapon behavior and is not an orb.

Both absorbable skills have complete Holy counterparts at implementation time so Church refinement cannot remain blocked by empty `holyVersion` references.

## Encounter Use

Criers appear in populated or politically tense locations: markets, plazas, alleys, gates, church approaches, guild unrest, and corrupted public gatherings. They may also accompany Limbo-aligned forces in garden encounters but are primarily urban agitators.

Initial encounter composition uses one Crier protected by two ordinary frontline enemies. Later groups may use two Criers, but Knell of Dread timing and SP prevent permanent debuff loops.

## Deterministic Tooling

A production builder:

- Imports or refreshes the approved source frames at stable paths.
- Enforces transparent Sprite imports, nearest filtering, no mipmaps, no compression, and project-standard pixels per unit.
- Creates the humanoid visual profile.
- Creates or refreshes the three equipment definitions.
- Creates or refreshes all skills, Holy counterparts, status definitions/icons, and the Crier combatant.
- Assigns portrait, equipment, animations, skills, learnable skills, alignment, and AI values.
- Registers new definitions with `AssetRegistry` through existing deterministic setup paths.
- Is idempotent and never duplicates production assets or references.

Credentials and private tokens remain outside tracked files.

## Failure Handling

- Missing source rotations, directions, or frames fail validation with the exact sequence named.
- A missing humanoid profile falls back to existing inline combatant visuals rather than breaking battle initialization.
- A blocked Pilgrim's Hook preserves resolved damage and grid occupancy.
- Missing temporary-terrain support suppresses Limbo Stain with one clear warning rather than breaking the affected action.
- Protected terrain remains unchanged.
- Missing status icons do not break effect resolution but fail production validation.
- Rebuilding must not overwrite unrelated characters, equipment, skills, scenes, or dirty-tree changes.

## Verification

Static and editor validation confirms:

- 228 required character images are present.
- All eight rotations exist.
- Walking contains four arrays of six frames.
- Bell-Hook Jab, Knell of Dread, Crooked Benediction, Pilgrim's Hook, and death each contain four arrays of nine frames.
- Hurt contains four arrays of four frames.
- Every frame uses the approved character identity and equipment silhouette.
- Portrait and five required icons use approved dimensions and import settings.
- The humanoid profile maps every action and direction correctly.
- Skills, statuses, equipment, Holy counterparts, drop rules, and registry entries match this design.

Play-mode verification confirms:

- Cardinal idle and walking visuals are correct.
- Every action plays its matching animation in all four directions.
- Hurt and death interrupt or complete according to battle-state rules without leaving stale sprites.
- Dread applies, refreshes, affects Faith and CT, respects boss resistance, and cleanses correctly.
- False Zeal modifies outgoing and incoming damage, places Limbo Stain after actions, refreshes, and stops placing stains after removal.
- Limbo Stain damages and Dreads hostile units, ignores Limbo-aligned units, expires after one round, and restores authored terrain.
- Pilgrim's Hook succeeds and fails safely across valid, occupied, blocked, elevated, protected, immovable, and edge-of-grid cases.
- AI follows the approved priorities without spamming unaffordable actions.
- Benidito absorbs, equips, levels, casts, and refines Knell of Dread and Pilgrim's Hook.
- Portrait, status icons, and equipment icons render cleanly in their production UI surfaces.
- Multiple Criers can battle simultaneously without animation, occupancy, status, or temporary-terrain corruption.
- The final Unity console contains no Crier-related errors or warnings.

## Out of Scope

- Elite Crier officers, named prophets, or boss variants.
- Modular runtime sprite-layer swapping for alternate equipment.
- Civilian riot AI or a complete crowd simulation.
- Dialogue trees, quests, or voiced sermons.
- Broad combat-AI refactoring.
- A universal arbitrary skill-effect language.
