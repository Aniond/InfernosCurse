# Loamkeeper Enemy Implementation Plan

Date: 2026-07-10

Design: `Docs/superpowers/specs/2026-07-10-loamkeeper-enemy-design.md`

## Phase 1: Capture and Validate PixelLab Source

1. Query PixelLab character `fdaea102-2e8f-44df-8bb6-d2257b10640f` through the authenticated character endpoint.
2. Record the eight rotation URLs and all four animation group IDs in a reproducible import manifest without storing credentials.
3. Download the eight rotations, the four-direction six-frame crouched walk, and the three four-direction nine-frame custom animations into an ignored staging directory.
4. Verify the expected total of eight rotations and 132 animation frames before copying anything into `Assets`.
5. Inspect every direction for missing, duplicated, corrupt, or visually inconsistent frames.
6. Map the custom groups exactly as approved: Rusted Snip, Grave Mulch/death reaction, and Root-Hook.

## Phase 2: Create and Approve the Portrait

1. Use the PixelLab south rotation and existing 128 by 128 Benidito and Rosekin portraits as visual references.
2. Generate one transparent 128 by 128 pixel-art bust preserving the straw hat, dead obscured face, thorn-vine cloak, work clothes, and fused rusted shears.
3. Keep hard pixel edges, restrained three-tone shading, no background, no text, no frame, and no modern details.
4. Present the portrait to David for visual approval before wiring it into production data.
5. Save the approved image as `Assets/Art/Portraits/portrait-loamkeeper.png` and apply the established portrait import settings.

## Phase 3: Import the Production Character Set

1. Create the production hierarchy under `Assets/Characters/Loamkeeper/` for rotations, walking, Rusted Snip, Root-Hook, Grave Mulch, and source metadata.
2. Copy only validated PNGs from staging into their deterministic production paths.
3. Add an editor builder that enforces Sprite import mode, transparency, nearest filtering, no mipmaps, no compression, and the project-standard pixels per unit.
4. Preserve source direction and frame order in stable filenames.
5. Make the builder idempotent so rerunning it refreshes references without duplicating assets.

## Phase 4: Add Skill and Holy-Counterpart Assets

1. Create or update `Skill_RustedSnip` with the approved physical, range, power, hit, SP, absorbed growth, Dexterity, and insanity values.
2. Create or update `Skill_RootHook` with the approved physical, exact two-cell range, power, hit, SP, absorbed growth, Strength, and insanity values.
3. Create `Skill_MercifulSever` as Rusted Snip's Holy counterpart.
4. Create `Skill_ShepherdsCrook` as Root-Hook's Holy counterpart.
5. Assign both `holyVersion` references so Church purification is functional.
6. Register all four skills with `AssetRegistry` through its existing deterministic setup path.

## Phase 5: Implement Forced Movement

1. Add the smallest data-driven skill-effect field needed to identify a one-cell pull after a successful hit.
2. Resolve the effect after damage through `AbilityResolver` without bypassing normal hit, death, or status resolution.
3. Add a grid helper that computes the destination one cell toward the caster.
4. Reject out-of-bounds, occupied, reserved, protected, unwalkable, elevation-invalid, impassable-edge, objective, and immovable destinations.
5. Update unit transform, grid occupancy, facing, and battle presentation through existing movement/occupancy APIs when the pull succeeds.
6. Preserve damage and display blocked-pull feedback when movement fails.
7. Ensure Benidito's absorbed Root-Hook uses the same effect path.

## Phase 6: Implement Grave Mulch

1. Add a focused death-trait contract to combatant data rather than encoding Grave Mulch as an active or absorbable skill.
2. Subscribe to resolved Loamkeeper death without changing unrelated enemy death behavior.
3. Snapshot the authored cell before applying temporary corrupted soil.
4. Refuse protected objectives, permanent obstacles, and encounter-critical terrain.
5. Apply a two-round temporary tile that gives Poison for two target turns at 5% maximum HP per tick when a unit ends a turn there.
6. Refresh duration without stacking magnitude when the tile is applied or triggered again.
7. Restore the original terrain after expiry and whenever combat ends early.
8. Reuse the existing temporary-terrain snapshot/restore conventions where possible.

## Phase 7: Build Enemy Data and Animation Wiring

1. Create or update `Assets/Data/Combatants/Enemy_Loamkeeper.asset` through the deterministic builder.
2. Assign the approved identity, portrait, role, level-one stats, intelligence, sight, and eye-height values.
3. Assign all four cardinal idle rotations and crouched-walk arrays.
4. Assign Rusted Snip, Root-Hook, and Grave Mulch animation arrays in all four directions with nine frames each.
5. Equip Rusted Snip and Root-Hook as active skills and list both as learnable skills.
6. Set the skill-drop chance to 0.30.
7. Assign Grave Mulch as the innate death trait.
8. Ensure the combatant has no Rosekin portrait, sprite, animation, or identity references.

## Phase 8: AI and Encounter Integration

1. Keep intelligence at 2 and use the existing low-intelligence AI path.
2. Confirm the AI prefers Root-Hook at exactly two cells, Rusted Snip when adjacent, and movement otherwise.
3. Add only the narrow AI scoring hook required for Root-Hook's forced-movement value; avoid broad AI refactoring.
4. Add one or two Loamkeepers to an existing garden-like hybrid encounter for production verification.
5. Preserve the authored map, exploration camera, battle grid, paths, and unrelated encounter actors.
6. Do not place Loamkeepers in ordinary streets or non-garden interiors.

## Phase 9: Deterministic Validation

1. Add a Loamkeeper validator covering the PixelLab character ID, animation group IDs, required directions, frame counts, import settings, portrait, skills, Holy counterparts, combatant wiring, and registry entries.
2. Validate eight idle rotations.
3. Validate four six-frame walking arrays.
4. Validate twelve nine-frame custom-animation arrays.
5. Validate exact skill values, both learnable references, the 0.30 drop chance, and the death trait.
6. Rebuild twice and confirm the second run creates no additional assets or reference drift.
7. Run `git diff --check` on production changes and inspect the final diff for unrelated dirty-tree overlap.

## Phase 10: Runtime Verification

1. Compile Unity scripts and run the Loamkeeper validator with zero new errors.
2. Enter Play mode in the approved garden hybrid encounter.
3. Confirm idle and walking direction for north, south, east, and west.
4. Trigger Rusted Snip in all four directions and verify its corresponding nine-frame animation and damage.
5. Trigger Root-Hook in all four directions and verify its corresponding animation.
6. Verify valid one-cell pulls update transform and occupancy.
7. Verify blocked, occupied, edge-of-grid, elevation-invalid, protected, and immovable pulls retain damage without movement or grid corruption.
8. Defeat a Loamkeeper on eligible terrain and verify the four-direction Grave Mulch/death animation.
9. Verify Grave Mulch applies the approved Poison, expires after two rounds, and restores the original tile.
10. End an encounter early and confirm temporary terrain restoration.
11. Force repeated drops so Benidito absorbs Rusted Snip and Root-Hook, equips them, levels them with duplicate orbs, and casts both successfully.
12. Refine both five-orb skills through the Church and confirm Merciful Sever and Shepherd's Crook replace the effective cast while removing insanity.
13. Verify the portrait in the unit information and character-facing UI surfaces that display enemy portraits.
14. Confirm the final Unity console contains no Loamkeeper-related errors or warnings.

## Phase 11: Documentation and Handoff

1. Update `Docs/MASTER_PROJECT_MEMORY.md` with the source ID, production paths, animation groups, skill behavior, encounter placement, and verification results.
2. Record any PixelLab API limitation or import caveat without recording credentials.
3. Preserve unrelated dirty workspace files and stage only the Loamkeeper implementation when a commit is requested.
