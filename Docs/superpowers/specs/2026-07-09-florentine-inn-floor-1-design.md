# Florentine Inn — Floor 1 Design

**Status:** Approved layout direction (Option B) with approved counter-interaction amendment
**Reference:** `Refrences/maps/inn.png`
**Level type:** Safe social hub
**Engine:** Unity 6, existing HD-2D camera/player stack

## Goal

Build a playable first floor for the Florentine inn that preserves the reference plan's room relationships while adapting dimensions and openings for the game's isometric camera and movement controller. The inn is a safe zone: no battle grid, enemy spawns, or encounters.

The first visit should establish the inn as a place to check in, rest, speak with residents and staff, and orient toward the future second floor.

## Layout

The playable footprint is approximately 22m × 22m, with south facing the street entrance.

| Space | Approximate size | Function |
|---|---:|---|
| Reception / Lobby | 8.0 × 6.5m | Main entrance, innkeeper, check-in/rest interaction |
| Sitting Room / Salon | 7.0 × 6.5m | Social NPCs and quiet seating |
| Office | 4.5 × 6.5m | Inn administration and private dialogue point |
| Courtyard | 8.0 × 7.0m | Central visual landmark and circulation hub |
| Dining Room | 7.0 × 10.0m | Guest tables and social NPCs |
| Kitchen | 8.5 × 6.5m | Staff activity and environmental storytelling |
| Pantry | 4.5 × 3.0m | Kitchen support storage |
| Staff Service | 4.5 × 4.5m | Staff work area |
| Storage | 4.5 × 4.5m | Barrels, crates, shelves |
| Stairs + WC | 7.0 × 3.0m | Built stair flight and ground-floor WC |

Doorways are 1.6–2.0m wide and main circulation is at least 2.0m wide. Room adjacency follows the reference even where proportions are widened.

## Player Flow

1. Enter from Via del Porcellana into reception.
2. Meet the innkeeper and access the existing inn-rest service.
3. See through reception into the courtyard, which acts as the orientation landmark.
4. Explore the west loop: salon, stairs/WC, dining room.
5. Explore the east loop: office, storage, staff service, kitchen, pantry.
6. Reach the stair landing, which is visibly complete but blocked until Floor 2 is built.

## Pacing

This is a low-intensity hub, not an encounter level.

`Entry 1 → Reception 2 → Courtyard 2 → Social rooms 3 → Rest 1 → Locked stair hook 2`

- **Entry:** immediate orientation and service visibility.
- **Build:** optional conversations and environmental details around the two loops.
- **Social peak:** dining room and courtyard contain the highest NPC density.
- **Release:** resting/check-in resolves the visit's practical purpose.
- **Hook:** the blocked stair establishes the later upper floor.

## Scene Architecture

Create `Assets/Scenes/FlorentineInnFloor1.unity` through an editor builder matching the project's established scene-generation pattern.

```text
FlorentineInnFloor1
├── [Architecture]
│   ├── Floors
│   ├── ExteriorWalls
│   ├── InteriorWalls
│   ├── DoorOpenings
│   ├── Courtyard
│   └── Stairs
├── [Props]
│   ├── Reception
│   ├── Salon
│   ├── Dining
│   ├── Kitchen
│   ├── Service
│   ├── Office
│   └── Courtyard
├── [NPCs]
│   ├── Innkeeper
│   ├── StaffMarkers
│   └── GuestMarkers
├── [Interactions]
│   ├── InnCounterInteraction
│   ├── DialogueMarkers
│   └── Floor2LockedLanding
├── [Travel]
│   ├── StreetEntrance
│   └── StreetExit
├── [Lighting]
├── Player
└── HD2D_CameraKit
```

The builder will reuse the existing player copied from `PonteVecchio`, `HD2D_CameraKit.prefab`, `GameSystems.prefab`, `ZoneExit`, and `RestMenuUI` patterns.

## Geometry and Camera Rules

- Author floors, walls, courtyard, stair flight, colliders, and room openings in the first pass.
- Use camera-readable wall heights and the existing interior visibility/cutaway conventions.
- Keep door openings wide enough for the current player collider.
- Avoid a ceiling in the first playable pass so the HD-2D camera can read rooms clearly.
- Build the staircase now, but terminate it at a locked landing with no scene transition.
- Do not add battle authoring components; this is a safe hub.

## Visual Treatment

- Florentine domestic interior, circa the game's historical period.
- Terracotta kitchen/courtyard floors; pale stone or patterned tile in public rooms.
- Plaster walls with dark wood trim and restrained carved-stone accents.
- Reuse existing project props and materials where suitable.
- Blockout props may use primitives or available prefabs, but every room must read clearly by silhouette and furniture arrangement.
- Courtyard receives plants and a small fountain as the central landmark.

## Functional Scope

The first playable pass includes:

- Street entrance and exit.
- Player and HD-2D camera.
- Complete walkable Floor 1 geometry and collision.
- Innkeeper rest/check-in interaction using the existing rest system.
- Placeholder innkeeper, staff, and guest NPC markers.
- Furnished room blockouts sufficient to identify every space.
- Locked Floor 2 stair landing.
- Lighting and post-processing consistent with existing zones.

The first pass excludes:

- Floor 2 rooms or transition.
- Combat and battle-grid support.
- Final bespoke dialogue, quests, or schedules.
- New externally generated hero props.

## Reception Counter Interaction

The inn service is a deliberate interaction with the reception counter, not a walk-into trigger.

- Add a reusable player world-interaction component rather than counter-specific input polling.
- Add an `Interact` player action bound to keyboard **E** and the gamepad south button (**A/Cross**).
- Keyboard and gamepad input activate the nearest eligible world interactable within 2.25m.
- A left mouse click casts from the active camera through the cursor and activates the counter only when the counter is the first interactable hit and the player is within 2.25m.
- The reception counter owns the inn interaction component and collider used by both input paths.
- While the counter is eligible, show `E / Click — Speak to Innkeeper` as the interaction prompt.
- Activating the counter opens the existing `RestMenuUI` for **Albergo Fiorentino**, price 10, with Albergatori guild modifiers enabled.
- Remove the automatic lobby `InnRestZone`; merely entering or crossing the reception area must never open the rest menu.
- The reusable interaction layer may support later shops, doors, and NPCs, but this pass wires only the inn counter.

## Data Flow

- Entering the scene places the player at `StreetEntrance`.
- The player enters counter range, then presses E/gamepad A-Cross or clicks the unobstructed counter to open `RestMenuUI`.
- Resting delegates to `RestSystem.RestAtInn`, preserving wallet, time, curse, and guild modifiers.
- The street exit uses `ZoneExit` and returns through the existing world-map/scene travel flow.
- The stair landing provides visual feedback only and performs no load until Floor 2 is implemented.

## Verification

The Floor 1 pass is testable when all of the following are true:

1. The scene opens with no missing references or console errors.
2. The player spawns at the street entrance and the camera follows correctly.
3. Every named room is reachable without clipping or blocked doorways.
4. The courtyard and both circulation loops are navigable.
5. Walking into reception does not open the inn-rest UI.
6. E and gamepad A/Cross open the inn-rest UI while the player is within 2.25m of the counter.
7. Clicking the unobstructed counter opens the inn-rest UI while in range, but not from outside range or through another collider.
8. Completing a rest uses the existing rest system.
9. The street exit works without trapping or repeatedly retriggering the player.
10. The staircase is climbable to its blocked landing and cannot load a missing Floor 2.
11. Interior walls do not obstruct the intended isometric view.

## Future Floor 2 Contract

Floor 2 will be added later. Its scene or vertical layer must align with the north-west stair footprint established here. No Floor 2 content is part of this build.
