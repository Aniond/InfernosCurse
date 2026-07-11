# 3D AI Octagonal Inn Fountain Design

Date: 2026-07-10

## Goal

Replace the Florentine inn courtyard fountain's primitive stacked-cylinder structure with a bespoke, period-appropriate 3D AI Studio asset that reads clearly from the locked HD-2D exploration camera. Preserve the approved courtyard layout, circulation, water standard, and runtime animation behavior.

## Approved Direction

The replacement is a restrained octagonal lion-spout fountain appropriate to the inn's thirteenth-century Florentine art direction.

- The basin retains a 3.2-metre overall footprint.
- The complete stone structure targets 2.1 metres tall and must remain between 2.0 and 2.2 metres after Unity import so it stays readable without obscuring the courtyard.
- A broad octagonal basin rim, faceted central pedestal, carved capital, and four cardinal lion-mask spouts replace the current primitive silhouette.
- Pietra serena is the dominant material. Lion masks may use aged bronze only if the generated mesh provides a clean, independently assignable material region; otherwise they remain carved stone.
- The design uses restrained hand-built imperfection: slightly uneven carving, softened corners, small chips, mineral staining, and sparse moss in lower seams.
- The design excludes heroic sculpture, Baroque ornament, excessive symmetry, and thin fragile details.

## Asset Generation

Use the existing 3D AI Studio API directly to generate a game-ready static fountain mesh.

The generation request must describe an isolated fountain structure with no environment, people, baked water, particle effects, floor slab, presentation plinth, text, or background geometry. The intended result is a watertight, readable prop with a clean octagonal footprint and strong bevels that catch the project's URP and vertex-shading treatment.

The preferred generated deliverable is GLB or FBX with embedded or accompanying PBR textures. Generated water geometry must be removed or excluded because Unity owns the production water presentation.

## Unity Integration

The generated architectural mesh replaces only the fountain's stone and optional bronze structure.

- Keep the existing horizontal water surface and shared fountain-water profile.
- Retain four directional falling-water streams, two ripple layers, and `InnFountainAnimator`.
- Reposition stream origins to the four lion-mask mouths.
- Use simple authored collision that follows the octagonal basin footprint; do not use a complex generated mesh collider for navigation.
- Preserve the existing courtyard anchor, overall footprint, entrance-to-counter route, courtyard circulation, and room openings.
- Store the generated source and production assets under the existing Florentine inn prop-kit hierarchy.
- Update `FlorentineInnPropKitBuilder` so deterministic regeneration instantiates the new authored asset and cannot restore the primitive fountain.
- Continue configuring the production scene through `FlorentineInnFloor1Builder` and the existing water-surface standard.

## Visual Standard

The finished prop must read as a handcrafted Florentine courtyard centerpiece rather than a stack of Unity primitives.

From the locked exploration camera, the player should be able to distinguish the octagonal rim, faceted pedestal, lion-mask spouts, carved capital, surface wear, and water flow directions. Materials should remain restrained and compatible with the inn's lime plaster, terracotta, dark timber, and pietra-serena palette.

## Failure Handling

Reject a generated candidate if it has any of the following:

- Baked or opaque water geometry.
- A circular or stacked-cylinder silhouette.
- Baroque, modern, fantasy, or monumental sculpture.
- Unusable topology, inverted normals, disconnected floating parts, or unreadable lion masks.
- Excessive texture noise or detail that collapses at the exploration-camera distance.
- A footprint or height that compromises courtyard navigation or sightlines.

If the first generation misses the approved silhouette, revise the generation prompt or use a new candidate rather than repairing a fundamentally incorrect result with additional primitives.

## Verification

Completion requires all of the following:

1. Unity imports the generated mesh and materials without errors.
2. Prop-kit generation and the Florentine inn builder complete successfully.
3. The inn validator confirms the fountain anchor, animation components, four streams, two ripple layers, water surface, and collision.
4. External C# compilation reports zero errors.
5. Play mode confirms animated streams originate from the lion masks and land inside the basin.
6. Collision and route probes confirm the fountain does not block approved courtyard circulation.
7. Four-direction and locked-camera visual inspection confirms the fountain reads as an authored octagonal lion-spout asset with no shader artifacts.
8. A final exploration-camera capture is visually compared with the original primitive fountain screenshot.

## Out of Scope

- Changes to the inn floor plan, arcade, courtyard dimensions, or camera rig.
- Floor-two construction.
- New character, NPC, combat, quest, or interaction behavior.
- Replacement of the shared water or weather systems.
- Redesign of other fountains in the project.
