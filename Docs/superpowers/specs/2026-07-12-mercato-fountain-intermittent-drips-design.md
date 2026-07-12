# Mercato Fountain Intermittent Drips Design

Date: 2026-07-12
Status: Approved direction, pending written-spec review

## Goal

Make the upper portion of the Mercato Vecchio fountain feel active by adding intermittent water droplets from beneath the upper bowl and subtle matching splashes in the lower basin. The effect must remain readable from the locked HD-2D exploration camera without becoming a continuous modern-looking spray.

## Approved Visual Direction

- Use irregular individual droplets rather than continuous streams.
- Place four restrained drip points around the upper stem, immediately beneath the upper bowl.
- Vary emission timing, droplet size, and initial delay so the four points do not pulse together.
- Use a cool blue-gray translucent water treatment with restrained brightness.
- Add a small splash response at each corresponding landing point in the lower basin.
- Keep splashes low and brief so they read as droplets landing, not rain or jets.

## Architecture

`MercatoVecchioProductionKitBuilder` remains the single deterministic authoring source for the fountain prefab. It will create one `Fountain_UpperDrips` root beneath `Fountain_AuthoredModel` with four paired effects:

1. A downward droplet particle emitter beneath the upper bowl.
2. A small upward/outward splash particle emitter at the matching lower-basin landing point.

The emitters use local simulation space so they remain aligned if the fountain prefab moves. They do not create gameplay colliders, lights, audio sources, or additional weather-surface registrations. The existing `Fountain_WaterSurface` remains the only registered fountain water surface.

## Droplet Behavior

- Emission uses short randomized intervals rather than a continuous rate.
- Each event emits one small stretched droplet with low lateral velocity and gravity-driven descent.
- Droplet lifetime ends at the lower-basin waterline.
- The four emitters use staggered random seeds or start delays to avoid synchronized repetition.
- Particle bounds stay close to the fountain so culling remains predictable.

## Splash Behavior

- Each landing point emits a tiny burst of two to four short-lived particles.
- Splash particles remain within the 3.55 m lower-basin water surface.
- Splash height and radius remain small enough that particles cannot appear on the plaza base.
- The splash timing follows the paired droplet travel duration closely enough to read as one event.

## Materials and Rendering

- Reuse a project-owned translucent water-compatible material when suitable; otherwise create one deterministic fountain-drip material under the Mercato production-kit materials folder.
- Use stretched billboard rendering for falling droplets and compact billboard rendering for splashes.
- Avoid additive bloom-heavy rendering, thick white foam, or opaque blue particles.
- Effects must remain legible under the existing Mercato day/night and weather lighting.

## Validation

The production-kit validator will require:

- The fountain preserves the authored upright model orientation.
- `Fountain_UpperDrips` exists with four drip emitters and four paired splash emitters.
- Drip emitters originate above the lower-basin waterline.
- Splash emitters lie within the lower basin and above the plaza surface.
- No drip or splash object owns a collider, light, audio source, or `WeatherSurface` component.
- The existing 4.90 m fountain model and contained 3.55 m lower-basin water dimensions remain unchanged.

Verification includes rebuilding the production kit, running the production-kit validator, compiling `Assembly-CSharp-Editor.csproj`, and visually checking the effect in Unity Play Mode from the exploration camera.

## Scope Boundaries

This pass does not add continuous streams, fountain audio, interaction, physics collisions, wet decals, new weather behavior, or changes to the plaza base. Those may be considered separately after the intermittent drips are evaluated in motion.
