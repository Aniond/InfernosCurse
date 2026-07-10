# Inn Props, Lighting, and Weather Design

## Objective

Replace the remaining primitive prop placeholders in `FlorentineInnFloor1`, fully dress its rooms, add an animated courtyard fountain, prepare reusable upstairs bed/rest behavior, and make interior windows and lighting reflect persistent world time and weather.

The approved structural layout, doorway widths, collider footprints, counter interaction, and blocked second-floor landing remain unchanged.

## Production Approach

Use a hybrid curated asset pipeline:

1. Search Asset Inventory for period-appropriate reusable props.
2. Reuse assets whose style, scale, topology, and URP materials fit the inn.
3. Create missing signature pieces with 3D AI Studio or UModeler X.
4. Standardize scale, pivots, materials, colliders, naming, and prefab organization.
5. Register finished prefabs in a GSpawn library named `Florentine Inn Prop Kit`.

An asset-library-only pass was rejected because mixed packs may produce inconsistent scale and art direction. A fully custom pass was rejected because it would add cost and production time without improving ordinary background props enough to justify it.

## Room Dressing

### Reception

- Finished reception counter and side sections.
- Key cubbies.
- Ledger, quill, keys, candles, and restrained desk dressing.
- Period bench and luggage.
- Two potted plants.

The entrance-to-counter route and the counter's interaction collider remain clear.

### Salon

- Period rug.
- Low tea table and three chairs.
- A recognizable bookcase rather than a featureless rectangular block.
- Books, candles, cups, and restrained decorative objects.

### Dining Room

- Six tables and twelve chairs at the existing positions.
- Sideboard.
- Plates, cups, pitchers, bread boards, candles, and folded linens.
- Tabletop dressing uses small variation sets so every table is not identical.

### Kitchen and Service Rooms

- Kitchen counters and work island.
- Working hearth presentation.
- Cookware, utensils, sacks, baskets, pantry goods, and ceramic storage.
- Pantry shelves.
- Five barrels and storage crates.
- Staff worktable and related dressing.

### Office

- Desk, chair, bookcase, and chest.
- Books, ledger, writing tools, candle, and document bundles.

### Courtyard

- Upgraded fountain.
- Planters, restrained benches, buckets, and limited garden dressing.
- Props must not obstruct the approved courtyard routes or column spacing.

## Interactivity Boundary

Small dressing props are decorative and static in this pass. Gameplay interaction remains limited to:

- Existing reception-counter inn service.
- Reusable future bed interaction.
- Animated fountain presentation.

The office chest, books, pantry goods, and other dressing do not gain gameplay interactions unless separately approved later.

## Bed and Rest System

Bedrooms remain reserved for the future second floor. This pass creates the reusable assets and behavior without opening or building upstairs rooms.

The bed kit includes a period bed, bedside chest, wash basin, linens, and a wake-point transform. A reusable `InnBedInteraction` component will support the following rules:

- A valid rest grants its gameplay benefits once per eligible rest cycle.
- Reusing the bed after the benefit is consumed may still play the rest presentation but cannot duplicate healing, recovery, or other benefits.
- The inn service can assign a bed wake point.
- Once floor 2 exists, completing a rest positions the player beside the assigned bed.
- Until floor 2 is built, the existing counter rest flow and wake behavior remain unchanged.

The eligibility state must be stored in the same persistent gameplay state used by the rest system rather than only on a scene object, so scene reloads cannot duplicate benefits.

## Animated Fountain

The courtyard fountain uses:

- Animated water-surface UV movement.
- Restrained falling-water meshes or particles.
- Subtle ripple or foam treatment at impact points.
- Looping spatial water audio.

The fountain remains decorative and non-interactable. Effects stop or reduce when the fountain is offscreen when practical, and they must not emit particles inside rooms.

## Persistent Time and Interior Lighting

The inn uses the same persistent world clock as outdoor maps. Entering or leaving the inn never changes the hour.

The presentation includes:

- Visible sunrise and sunset through windows.
- Courtyard sun direction, intensity, shadowing, and color driven by world time.
- Ambient light transitioning through dawn, day, sunset, and night palettes.
- Interior lamps subdued during daylight and strengthened automatically after dusk.
- Exterior light cards, sky treatment, or limited exterior geometry outside windows so they never reveal empty space.

The lighting adapter reads world-time state and applies it to scene lights and window presentation. It does not own or advance the clock.

## Persistent Weather Through Windows

Windows use the same persistent world-weather state as outdoor maps. Indoor weather is visual and audible only; precipitation never appears inside the rooms.

Supported presentation states are:

- Clear: unobstructed exterior and normal time-of-day lighting.
- Cloudy: muted exterior contrast, cooler ambient light, and reduced direct sun.
- Rain: visible rainfall beyond the glass, rain streaks or droplets on glass, darker daylight, and exterior rain audio.
- Storm: heavier rain, darker ambient conditions, and lightning flashes that briefly illuminate windows and interior surfaces.
- Fog: muted exterior views and softened window contrast.
- Snow may be supported only if the global weather system already exposes it; it is not required for this pass.

Window effects react to weather state changes without reloading the scene. Lightning must use the outdoor storm event/state rather than an unrelated indoor random timer, keeping visible flashes and lighting synchronized.

## Builder and Asset Organization

Prop assets live under `Assets/Environment/FlorentineInnFloor1/PropKit` with separate `Meshes`, `Prefabs`, `Materials`, `Textures`, `Audio`, and `VFX` folders.

`FlorentineInnFloor1Builder` remains authoritative. Existing prop coordinates are retained where they preserve the approved floor plan. Finished prop prefabs replace primitive helper calls so rebuilding the scene does not restore placeholders. Small decorative variants may use deterministic selection or explicitly authored placements; scene rebuilds must remain repeatable.

Time and weather components receive references to the persistent world-state services through the project's existing access pattern. Missing services must degrade safely to a documented default daytime/clear-weather presentation and log at most one actionable warning.

## Validation

The pass is accepted when:

- All listed room placeholders are replaced with recognizable period props.
- No television, modern fixture, or other anachronistic object appears.
- Entrance, counter, doorways, dining routes, service routes, and courtyard paths remain clear.
- Counter keyboard, gamepad, and mouse interaction still work.
- Bed benefits cannot be duplicated within one eligibility cycle.
- Future wake-point assignment positions the player beside the assigned bed when enabled.
- Fountain surface, flow, ripple, and audio loop correctly without visible indoor particles.
- Dawn, noon, sunset, and night produce visibly different but readable interior lighting.
- Interior lamp intensity responds correctly to time of day.
- Clear, cloudy, rain, storm, and fog states are visible through windows.
- Storm lightning synchronizes window flashes and temporary interior illumination.
- Entering and leaving the inn preserves time and weather state.
- Rebuilding the scene twice retains the finished props and deterministic dressing.
- Unity reports zero console errors after rebuild and runtime testing.

## Delivery Boundary

This pass does not build the second floor, replace NPC capsules, add loot interactions, create new quests, or change the approved structural layout. Upstairs bedrooms and live bed wake-up placement are activated only during the future second-floor pass.
