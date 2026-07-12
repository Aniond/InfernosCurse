# Gugol Mappe Refinement and Street View Design

**Status:** Approved design

**Date:** 2026-07-11

**Scope:** Presentation, navigation, discovery, and world-state expression for the existing Gugol Mappe system

## Goal

Refine the existing Gugol Mappe interface into a visually distinctive navigation tool that combines modern map clarity with illuminated Florentine cartography. The map must remain fast and useful while expressing discovery, weather, irreversible world outcomes, and Limbo's theme of loss and forgetting without exposing hidden Circle values.

The approved visual direction is a hybrid rather than a direct imitation of a modern mapping product: parchment and ink supply the world identity, while search, zoom levels, routes, selection, and compact information cards supply the usability.

## Approved Decisions

- Remove the large `Gugol Mappe` masthead from the browsing screen. The top of the interface begins with search and scale controls so the map receives more screen space.
- Retain City, Tuscany, and Italy as the principal scale levels.
- Add a fourth contextual Street View reached by selecting a named street from the City layer.
- Street View is an illustrated information layer, not a 360-degree panorama or duplicated 3D environment.
- NPC tracking reports the last known street and contextual time, never an omniscient live position.
- Known locations are precise; rumored locations are approximate; hidden locations remain absent until discovered.
- The map expresses authored world consequences visually but never displays numeric corruption or Circle influence.
- Player Sanity remains completely outside the map's world-state presentation.

## Experience Principles

### Map first

The map remains the dominant visual surface. Search and City/Tuscany/Italy controls occupy a compact band at the top. A single contextual card rises from the bottom only after a street, location, shop, or NPC is selected. Persistent sidebars, stacked panels, and decorative title treatments are avoided.

### Modern clarity, medieval soul

Road hierarchy, labels, selected routes, travel time, and location categories must be readable at a glance. The visual language uses aged cream parchment, sepia ink, muted terracotta, olive gardens, a desaturated Arno, restrained blue navigation accents, and wax-seal red for significant markers. Gilding and ornament are accents rather than borders around every control.

### The map remembers what the world remembers

The visible map is part of the fiction. New knowledge adds ink, labels, and landmarks. Limbo can weaken names, remove records, or reduce a known place to an uncertain outline. When Limbo recedes, memory may return, but irreversible physical losses remain visible and grief remains part of the location's presentation.

## Information Hierarchy

### Italy

- Shows major regions and known long-distance destinations.
- Emphasizes broad travel connections and unlocked regional rumors.
- Does not display Circle values, influence meters, or a colored corruption heat map.

### Tuscany

- Shows settlements, regional roads, travel routes, weather, and discovered points of interest.
- Rumored destinations may appear as broad sketched areas rather than exact pins.
- Route selection reports the established time, fare, and encounter information.

### City

- Shows named streets, districts, the Arno, bridges, gardens, plazas, major landmarks, and known destinations.
- Roads use a clear visual hierarchy so named streets can be selected without covering the city in labels.
- Selecting a named street focuses the map and enters Street View.

### Street View

Street View enlarges one named street and separates its useful frontage into selectable buildings, entrances, services, and landmarks. It is not a panoramic camera and does not allow free movement through a duplicate city.

Street View can show:

- Shops, inns, services, residences, workshops, and usable entrances.
- Open or closed state and authored operating hours when available.
- Known quest, rumor, recruitment, and event indicators.
- NPCs last known on that street, with contextual wording such as `seen this morning` or `usually works here`.
- Weather, closure, damage, abandonment, and other authored world consequences.

Selecting a shop or NPC highlights the associated building or last-known frontage and offers directions. Entering the destination remains ordinary gameplay; the map does not simulate the interior.

## Discovery and Knowledge Rules

Map precision reflects player knowledge:

1. **Hidden:** The place or NPC is not shown.
2. **Rumored:** The map shows an approximate area, handwritten clue, or uncertain street association.
3. **Known:** The location receives a precise pin, label, and route target.
4. **Last known:** A mobile NPC receives a street-level record with an in-world time description.
5. **Lost or forgotten:** The presentation fades, loses its name, becomes uncertain, or disappears according to an authored outcome.

Rumors, conversations, direct sightings, quest events, and the permanent event chronicle can advance knowledge. The interface must not silently grant information merely because the underlying simulation knows it.

NPCs are never tracked as live GPS markers. A last-known record remains useful but may become stale. The UI communicates staleness in fiction rather than with a technical timestamp.

## World-State Expression

The map consumes authored presentation state, not raw simulation values.

Examples include:

- A healthy garden uses richer olive ink and a complete landmark illustration.
- A neglected garden loses saturation and gains authored damage marks.
- A permanently lost garden remains physically scarred after Limbo weakens.
- A forgotten garden may lose its label and eventually its map illustration.
- When memory returns, the name and remembered history can return while the destroyed place remains destroyed.
- Weather may add localized rain stippling, drifting mist, cloud shadow, or flood notation without obscuring routes.

Circle world state and player Sanity remain completely isolated. The map cannot read, infer, modify, or display player Sanity. It also cannot display numeric Circle state. Each visible change must come through an approved expression, warning, discovery, site outcome, or weather record.

## Interaction and Motion

- Search filters known places, streets, shops, services, and NPC records without revealing hidden content.
- Mouse selects a visible street or marker directly.
- Keyboard and gamepad move focus among nearby selectable streets, locations, and card actions.
- Zoom transitions redraw or blend between Italy, Tuscany, City, and Street rather than abruptly replacing the entire screen.
- Selected pins lift slightly and receive a restrained wax-seal shadow.
- Routes draw along the road network like fresh ink, with blue reserved for active navigation.
- Weather motion remains localized and slow.
- Irreversible losses receive one deliberate visual reveal; normal browsing avoids continuous dramatic animation.
- Reduced-motion mode removes ornamental motion while preserving state changes and selection feedback.

## Component Boundaries

The existing map implementation is retained, but presentation responsibilities are separated so each layer can be validated independently.

### `GugolMapUI`

Remains the coordinator for open/close state, input mode, search, selected scale, selection, and card visibility. It does not directly author every visual element.

### Map layer presenter

Owns the Italy, Tuscany, City, and Street canvases, their bounds, focus, label density, and transitions. It accepts a selected scale and focus target and exposes selectable map features.

### Street index

Owns stable street definitions and their relationships to city geometry, venues, entrances, landmarks, and NPC knowledge records. Mercato Vecchio is the pilot Street View area because its production zone and embedded Florentine Inn provide a representative street, shop, entrance, NPC, and seamless-interior test case.

### Discovery presenter

Converts discovery and rumor records into hidden, approximate, known, last-known, lost, or forgotten presentation. It never consults simulation-only facts that the player has not learned.

### World-state overlay presenter

Converts approved weather, warning, Circle-expression, and site-outcome records into visual overlays. It does not receive raw numeric Circle values and has no dependency on player Sanity.

### Pin and feature presenter

Pools and updates streets, pins, venue icons, NPC records, the player marker, weather glyphs, and selection treatments. Categories use consistent iconography rather than unique colors alone.

### Route presenter

Retains the current travel and route authority, then renders the chosen path at the active scale. Street View routes terminate at a valid entrance when known and fall back to the street focus point when an entrance is unavailable.

### Context card presenter

Shows one selected entity at a time. Location cards report travel and world information; street cards summarize frontage; venue cards report service and access; NPC cards report last-known knowledge. The card does not duplicate a permanent sidebar.

## Authoring Data

Street View requires stable, authorable records rather than scene-name guesses.

A street definition includes:

- Stable ID and display name.
- Parent city or district.
- Selectable map geometry and Street View framing bounds.
- Ordered frontage or venue references.
- Route entry and fallback focus points.
- Optional presentation-art references.

A venue record includes:

- Stable ID, display name, category, and street ID.
- Building or frontage anchor.
- Known entrance or seamless-interior destination when applicable.
- Service and schedule presentation.
- Discovery requirement and optional site-outcome relationship.

NPC map presence is derived from player knowledge and event records. It includes the NPC's stable ID, last-known street, associated venue when known, in-world recency text, and the knowledge source. Simulation position is not exposed directly.

## Data Flow

1. Existing location, discovery, event, weather, and travel systems update their authoritative state.
2. A read-only map snapshot translates that state into player-known map records.
3. The active map layer requests only the records appropriate to its scale and focus.
4. Discovery and world-state presenters assign the approved visual expression.
5. Selection updates one context card and, when requested, the existing route calculation.
6. The map never writes Circle state, site outcomes, NPC memory, weather, or player Sanity.

## Failure and Fallback Behavior

- Missing high-detail art falls back to the current parchment layer rather than blocking map access.
- A street with invalid or missing selectable geometry remains visible but cannot enter Street View; validation reports the authoring error.
- A broken venue reference is omitted from Street View and reported in validation.
- Missing NPC knowledge never falls back to omniscient simulation data.
- A stale NPC record remains labeled as last known; it is not silently presented as current.
- If a precise entrance is unavailable, directions terminate at the street focus point.
- If an authored world-state overlay is missing, navigation stays functional with the neutral known-state presentation.
- Search produces a clear empty-known-results state and never reveals hidden entries through suggestions.

## Performance Constraints

- Street View uses authored map geometry and illustrations, not live scene cameras, panoramic capture, or a second rendered city.
- Pins, labels, and weather elements are pooled.
- Only the active scale and its transition partner remain fully active during a zoom.
- World-state overlays update when their source record changes, not every frame.
- Label density is capped by scale and priority to prevent overdraw and unreadable streets.

## Validation

### Edit-mode validation

- Every street, venue, entrance, location, and NPC reference uses a unique stable ID.
- Street geometry is valid and lies within its parent city layer.
- Venue frontage belongs to the declared street.
- Route targets and fallback points exist.
- Hidden entries are excluded from search indexes.
- World-state expressions reference approved presentation states rather than raw numeric fields.
- No map component references player Sanity.

### Play-mode validation

- Open, close, search, scale changes, and selection work with mouse, keyboard, and gamepad.
- Italy, Tuscany, City, and Street transitions preserve selection and back-navigation.
- Selecting a Mercato street exposes the Florentine Inn, known services, and representative NPC last-known records.
- Selecting a venue produces a valid route and entrance or the documented fallback.
- Rumored, known, last-known, lost, forgotten, and recovered-memory states render correctly.
- Giardino delle Rose test states demonstrate healthy, lost, forgotten, and remembered-but-still-lost presentation without changing the permanent site outcome.
- Representative rain, mist, flood, and clear weather remain readable beneath routes and labels.
- The interface scales cleanly across the project's supported aspect ratios and safe areas.
- Missing optional art and data invoke the documented fallbacks without breaking navigation.

### Visual review

- The map occupies the majority of the screen.
- No large Gugol Mappe title masthead appears in the browsing view.
- Search, scale controls, street names, selected route, player marker, and one context card remain legible at gameplay resolution.
- Ornament never competes with streets, landmarks, or navigation.
- World-state damage is readable without resembling a numeric corruption heat map.

## Delivery Sequence

1. Separate coordination, layer presentation, selection, and card duties while preserving current behavior.
2. Apply the approved masthead-free visual system to City, Tuscany, and Italy.
3. Author the Mercato Vecchio street index and pilot Street View, including the Florentine Inn and representative NPC records.
4. Connect discovery, last-known NPC knowledge, weather, and authored site-outcome expressions.
5. Add the Giardino delle Rose loss, forgetting, and memory-return presentation tests.
6. Validate all inputs, aspect ratios, fallbacks, and performance before expanding Street View to additional districts.

## Out of Scope

- 360-degree panoramic Street View.
- Free movement through a duplicate 3D Florence from the map.
- Live scene-camera thumbnails for every street.
- Exact real-time NPC tracking.
- Real web-map or Google service integration.
- Numeric Circle or corruption displays.
- Player Sanity display or modification.
- Automatic procedural creation of unapproved shops, NPC knowledge, or world outcomes.
