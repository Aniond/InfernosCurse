# Mercato Vecchio Production Rebuild and Seamless Interior Standard

## Summary

`MercatoVecchio` will be rebuilt from its current test-zone form as a faithful, production-quality 3D translation of `Refrences/maps/market square.png`. The square will retain the reference map's recognizable structure: the Loggia to the north, a central fountain plaza, dense market rows, merchant buildings around the perimeter, a walkable Arno edge to the south, the Ponte route to the east, and the Signoria route to the northwest.

The rebuild also establishes a project-wide accessible-building standard. Small and medium interiors such as inns, shops, homes, workshops, and guild rooms will normally be experienced seamlessly within their surrounding zone. Major landmarks whose scale, presentation, or gameplay requires independent authoring, such as the Duomo, will remain dedicated zones.

The Albergo Fiorentino is the pilot for this standard. Its completed first floor will be preserved as a separately authorable interior module, positioned inside the west merchant edge of Mercato, and entered without a scene load.

## Goals

- Replace the current Mercato test layout with a faithful production environment.
- Make the Loggia, fountain, market rows, riverfront, and directional routes immediately recognizable from the supplied reference.
- Preserve the shared locked HD-2D exploration camera and in-place tactical battle architecture.
- Turn the environment's visible architecture and props into authoritative tactical terrain.
- Link the existing inn to Mercato as a seamless, physically present interior.
- Establish a reusable workflow for future shops, inns, homes, and comparable buildings.
- Support time, weather, quests, crowds, and Limbo expression without rebuilding the base scene.
- Create reusable Florentine market modules rather than one-off test geometry.

## Non-Goals

- Do not reproduce the reference map at a literal one-pixel-to-one-metre scale.
- Do not preserve the current test-zone layout where it conflicts with the approved reference.
- Do not replace the completed Florentine Inn first-floor contents.
- Do not make landmark-scale interiors such as the Duomo seamless by default.
- Do not permit outdoor market encounters to spill into the protected inn interior during this pass.
- Do not redesign the Circle Influence, player Insanity, NPC memory, or Crier systems.
- Do not expose hidden world-state values numerically to the player.

## Approved Direction

### Modular faithful rebuild

Mercato will use a deterministic composition of reusable production modules rather than a single brittle scene sculpture. The kit will include:

- Loggia architecture.
- Florentine perimeter townhouses and merchant facades.
- Albergo Fiorentino exterior shell and entrance.
- Shopfront and sign families.
- Market stalls, awnings, carts, crates, barrels, and merchandise families.
- Central fountain and plaza treatment.
- River wall, overlook, and eastern bridge-approach elements.
- Street surfaces, drains, curbs, steps, and boundary dressing.

Modules may use generated assets, approved imported tools, project structural kits, and authored Unity geometry where appropriate. Shared materials, instancing, sensible mesh combination, and deterministic placement will keep the result maintainable and performant.

## Spatial Design

### North: Loggia anchor

The Loggia del Mercato is the dominant northern landmark. It must remain visible from the principal arrival routes and establish the square's silhouette. Its columns and edges remain real collision and become tactical cover during battle.

### Center: fountain plaza

The enlarged fountain and statue composition anchors the central plaza. A clear circulation ring surrounds it, and the main market aisles converge nearby without turning the plaza into an empty arena. Fountain edges provide cover while animated water remains active during exploration and battle.

### West: merchants and inn

The west perimeter contains merchant buildings and the Albergo Fiorentino. The inn entrance faces the fountain and primary aisle, giving the player an immediate landmark when leaving the building. Its exterior sign, doorway, materials, and architectural language must visibly match the completed interior identity.

### East: shop row and Ponte route

The east perimeter includes the weaponsmith and related merchant frontage. A fully walkable approach leads toward the Ponte transition without disguising the route as decorative background.

### South: Arno edge

The southern edge is fully walkable. It includes the river wall, overlook space, environmental dressing, and future transition support for Arno-related destinations. The boundary must read as a real river edge rather than the end of a test board.

### Northwest: Signoria route

The northwest route leads toward Signoria and uses a stable arrival contract for future direct linkage.

### Market interior

Stalls form dense but organized rows with readable primary and secondary aisles. Variation comes from awnings, merchandise, signs, carts, crates, produce, wear, and vendor identity rather than random obstruction. The player must always be able to read the principal routes under the locked HD-2D camera.

## Art and Surface Direction

- Warm terracotta roofs, pale aged stone, lime plaster, dark timber, and restrained cloth colors define the square.
- Building silhouettes and rooflines vary while sharing a coherent Florentine material family.
- Vertex-painted dirt, dampness, moss, edge wear, and foot-traffic patterns are used where they improve form and history.
- Surface technology supports painterly macro variation and weather response without flattening tactical readability.
- Props remain period-appropriate and avoid the generic distribution of a test or stock-prompt environment.
- Detail density is controlled so stalls and facades remain readable at gameplay-camera distance.
- Exterior and interior lighting are separately controlled to prevent daylight or weather effects from ignoring walls.

## Seamless Accessible Building Standard

### Default scope

The following building types should normally use seamless interiors when their size and gameplay permit:

- Inns and taverns.
- Shops and workshops.
- Homes and apartments.
- Small guild rooms and offices.
- Comparable street-accessible social interiors.

These interiors remain independently authorable as prefabs or streamed/additive modules, but they occupy a real footprint within the surrounding zone and do not require a visible loading transition when entered.

### Landmark exception

Major landmarks remain dedicated zones when they require substantially different scale, camera framing, lighting, population, encounter rules, streaming budgets, or narrative staging. The Duomo is the explicit first example. A dedicated zone is a design choice based on experience and scope, not merely the presence of an interior.

### Entry flow

1. The player approaches a physically present exterior doorway.
2. The interior module is already available or finishes loading before the threshold is crossed.
3. Crossing the threshold changes the active sub-location without changing the main zone.
4. Exterior roof or facade occluders fade or cut away according to camera rules.
5. Camera framing blends to the interior profile while retaining the shared camera rig.
6. Interior lighting, audio, interactions, and full NPC behavior become active.
7. Exterior weather and market audio are appropriately occluded or muffled.
8. Walking back through the doorway reverses the presentation without loading another scene.

### Authoring contract

Each seamless building provides:

- A stable building and sub-location ID.
- Exterior shell and interior module anchors in a shared coordinate contract.
- A threshold volume with inside and outside states.
- Camera framing and occlusion profiles.
- Interior lighting and post-processing boundaries.
- Audio portal or blend behavior.
- Activation groups for interior NPCs, props, VFX, and simulation.
- Save/load restoration for the player's active sub-location and position.
- Weather exclusion and light-occlusion validation.
- Explicit battle participation rules.

### Florentine Inn pilot

`FlorentineInnFloor1` will be converted from a standalone destination into a reusable interior module without discarding its approved plan, props, interactions, fountain, lighting work, or locked upper-floor route. It will be positioned behind the Albergo Fiorentino exterior on Mercato's west edge.

The reception counter remains the inn-service interaction. The first floor remains a safe social interior. The outdoor market battle boundary does not include the inn: when a Mercato encounter begins, the entrance is temporarily unavailable and no combatant may route through the threshold. Story-authored interior combat would require a separate explicit authorization in a future design.

## Exploration and Camera Behavior

- Mercato uses the shared uniform locked HD-2D exploration camera.
- Primary routes remain legible without player-controlled orbit or tilt.
- Entering the inn uses a smooth camera-profile blend, not a new camera philosophy.
- Roofs and near walls fade only when necessary to reveal the player and navigable interior.
- Exterior-to-interior transitions must not expose unloaded space, map edges, or hidden facade backs.
- Weather, lighting, and audio changes blend at the threshold rather than popping.

## Hybrid Battle Behavior

Mercato remains one of the project's shared exploration-and-battle zones.

- A battle begins on the same geometry used during exploration.
- The tactical camera replaces the exploration presentation without loading a replacement arena.
- Stalls, carts, crates, columns, fountain edges, walls, steps, and elevation remain in place.
- Visible obstacles must agree with walkability, cover, concealment, height, and line-of-sight data.
- Civilians evacuate, hide, or despawn through authored safe behavior before combat control begins.
- Required enemies, world agents, and scripted NPCs remain according to encounter rules.
- The inn and other protected interiors are excluded from the outdoor combat footprint.
- Victory restores the same market state, exploration camera, civilians, interactions, and building access.

## World-State Expression

The base layout remains stable while presentation layers express time, weather, quests, commerce, and Limbo.

Early Limbo expression should be easy to mistake for ordinary unease:

- A familiar sign is missing or incorrect.
- Goods are misplaced and vendors repeat themselves.
- A stall stands unattended.
- Neighbors hesitate over familiar names.
- Crowd routes feel subtly incomplete.

As Limbo strengthens, expression can escalate through closed shops, absent vendors, forgotten landmarks, Crier activity, broken routines, and NPC memory overlays. Existing Crier encounter and corruption hooks will be relocated into the production layout rather than discarded.

Permanent site outcomes remain permanent. Reducing Limbo may allow NPCs to remember a lost place or person, but it does not silently restore destroyed locations or reverse authored grief.

## Scene and Data Architecture

- `MercatoVecchio` remains the owning exterior zone and world-state context.
- The market composition is generated or rebuilt deterministically from reusable modules.
- The Florentine Inn interior remains separately authorable but is embedded or additively streamed into Mercato's coordinate space.
- Stable sub-location IDs replace cross-scene arrival IDs for seamless interiors.
- Signoria, Ponte, and future Arno routes retain stable cross-zone arrival IDs.
- Save data records the owning zone, active sub-location, player transform, and necessary interior activation state.
- Runtime activation reduces off-camera interior cost without deleting persistent NPC or quest state.

## Failure Handling

- If an interior module is unavailable, keep the doorway blocked and report an actionable error; never allow the player into empty space.
- If camera or occlusion profiles are missing, retain the last safe exterior presentation and reject the transition.
- If an encounter begins near a protected threshold, resolve or cancel building entry before exploration control is suspended.
- If tactical authoring disagrees with visible geometry, validation fails rather than silently accepting incorrect cover.
- If a world-state overlay references a missing anchor, omit that overlay, preserve the base scene, and report the missing ID.
- No transition failure may leave the player hidden, frozen, outside valid collision, or without camera control.

## Validation

### Layout and presentation

- Loggia, fountain, inn, stall rows, riverfront, Ponte route, and Signoria route are present and recognizable.
- Main and secondary aisles remain navigable and readable under the exploration camera.
- No production-facing area exposes test geometry, unloaded space, or unintended map boundaries.
- Materials, scale, and prop density remain coherent at gameplay distance.

### Seamless inn

- The player walks from Mercato into the inn and back without a scene load or visible loading screen.
- The approved first-floor layout, services, props, fountain, and locked upper route remain intact.
- Roof and wall occlusion reveal the player without excessive transparency.
- Interior light remains contained; exterior weather does not illuminate through walls.
- Audio and weather presentation blend correctly at the threshold.
- Save/load restores the player safely inside or outside the inn.
- The inn remains inaccessible to ordinary outdoor combatants.

### Hybrid battle

- Combat begins and ends without replacing the environment.
- Civilians clear safely before tactical control begins.
- Cover, collision, elevation, walkability, and line of sight match visible market geometry.
- The tactical camera does not expose invalid scene edges or protected interior space.
- Victory restores exploration, camera control, interactions, exits, and building access.

### World state and routes

- Base, weather, time, quest, and Limbo presentation layers can be activated independently.
- Existing Crier hooks remain functional in their new anchors.
- Signoria, Ponte, and Arno route contracts use stable IDs and fail safely when destinations are unavailable.

## Acceptance Criteria

The design is implemented when Mercato is a faithful production translation of the approved reference; its modular Florentine kit can be rebuilt deterministically; exploration and battle share the same environment; the Loggia, fountain, market aisles, riverfront, and routes are readable and functional; the completed Florentine Inn can be entered and exited seamlessly as a protected interior; lighting, weather, audio, camera, occlusion, save/load, NPC evacuation, tactical terrain, and world-state layers pass validation; and Unity reports no new attributable runtime errors or warnings.
