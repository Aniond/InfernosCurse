# 3D AI Octagonal Inn Fountain Implementation Plan

Date: 2026-07-10

Design: `Docs/superpowers/specs/2026-07-10-3d-ai-octagonal-inn-fountain-design.md`

## Phase 1: Generate and Approve the Source Concept

1. Add a single isolated fountain entry to the existing `Tools/asset-gen` workbench.
2. Generate a clean three-quarter source image through the configured Gemini image endpoint.
3. Verify the image contains one complete fountain, a plain white background, no floor slab, no environment, no people, no text, no baked water, and no detached geometry.
4. Present the concept to David and do not spend Prism credits until he approves the actual source image.

## Phase 2: Generate and Inspect the 3D Model

1. Confirm no unrelated approved workbench entries are eligible for batch submission.
2. Approve only the selected fountain source.
3. Submit it to the 3D AI Studio Prism 3.1 image-to-3D endpoint at the standard textured PBR tier.
4. Preserve the task journal and poll until the GLB is available.
5. Inspect the GLB for silhouette, completeness, topology, orientation, material assignments, unwanted water or plinth geometry, and baked text or watermark placement.
6. Reject and regenerate rather than disguising a fundamentally incorrect model with Unity primitives.

## Phase 3: Import and Build the Production Prefab

1. Copy the accepted GLB and required textures into the Florentine inn prop-kit hierarchy.
2. Allow glTFast to import the source asset and measure renderer bounds.
3. Create or update the deterministic fountain prefab wrapper at the approved 3.2-metre footprint and 2.0-to-2.2-metre height.
4. Recenter the generated bounds and correct its forward/up axes.
5. Assign compatible URP stone and optional bronze materials without modifying unrelated imported assets.
6. Add a simple octagonal or conservative compound collider instead of a generated mesh collider.

## Phase 4: Restore Production Water and Builder Ownership

1. Retain the shared horizontal fountain-water surface and profile.
2. Reposition four animated streams to the lion-mask mouths and keep two ripple layers inside the basin.
3. Retain `InnFountainAnimator` and its existing runtime contract.
4. Update `FlorentineInnPropKitBuilder` to instantiate the authored model rather than primitive stone geometry.
5. Rebuild the prop kit and `FlorentineInnFloor1` through their authoritative builders.
6. Extend validation so a primitive-only fountain cannot silently return.

## Phase 5: Verify in Unity

1. Refresh/import assets and request a clean script compilation.
2. Run the directional-idle, prop-kit, and inn production validators.
3. Confirm external C# compilation reports zero errors.
4. Enter Play mode and verify the four streams originate at the masks, the basin surface and ripples animate cleanly, and there are no shader artifacts.
5. Probe courtyard navigation and collision around all sides of the 3.2-metre footprint.
6. Capture the fountain from the locked exploration camera and at least three inspection angles.
7. Compare the final capture against the original primitive-fountain screenshot.

## Phase 6: Document the Result

1. Record the source asset, generation task, production prefab, builder ownership, and verification results in `Docs/MASTER_PROJECT_MEMORY.md`.
2. Leave all changes uncommitted until David explicitly requests a commit.
