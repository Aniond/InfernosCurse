# Mercato Vecchio Stall, Seamless Camera, and Map Card Design

Date: 2026-07-12
Status: Approved visual direction; written-spec review pending

## Goal

Bring the visible Mercato Vecchio presentation up to the production standard already established by its character, fountain, terrain, weather, and seamless inn. This pass replaces the blockout market stalls, adds reusable automatic room framing for seamless interiors, and repairs the City map location-card layout at narrow/mobile widths.

The pass must preserve the locked HD-2D exploration camera identity, existing player routes, hybrid-battle navigation, seamless-inn ownership, stable map data, and all unrelated dirty worktree changes.

## Decisions

- Replace both the current primitive stall structures and their cube merchandise.
- Prefer the existing 3D AI Studio `Florentine_Stall.glb` as the principal stall structure when its live scale, materials, and silhouette pass Unity review.
- Populate project-owned stall variants with the imported medieval-market baskets, bread, apples, produce boxes, sacks, and lanterns.
- 3D AI Studio is a primary production source for this project. Use it proactively for high-visibility stalls and supporting props that give each trade and zone a distinct authored identity.
- Keep the camera automatic. Entering the inn blends to authored room framing; exiting restores the standard Mercato follow profile without a snap.
- Build the camera feature as a reusable seamless-interior profile system, but author only the Florentine Inn in this pass.
- Repair the City map location card responsively and remove the unexplained rating/review line from the player's current-location mode.

## Considered Approaches

### A. Curated production kit — selected

Use the 3D AI Studio Florentine stall as the hero structure, then assemble multiple project-owned commerce variants from imported merchandise props. Normalize materials toward Mercato timber, cloth, terracotta, and muted produce colors. This provides the best silhouette and detail while retaining deterministic placement and a coherent palette.

### B. Hybrid retrofit

Keep the current procedural cube frame and replace only the merchandise and awning. This is faster but leaves the obvious blockout silhouette visible, so it does not solve the reported problem.

### C. Generate new stalls

Create additional 3D AI Studio stall meshes. This is approved when a distinct trade role needs a silhouette the existing Florentine stall cannot provide, but it remains less efficient than first proving the strong generated source already in the project.

## Production Stall System

### Asset ownership

Vendor and generated source assets remain unmodified. New project-owned prefabs live under:

`Assets/Environment/MercatoVecchio/ProductionKit/Prefabs/Commerce/`

The production builder references source prefabs/models and constructs normalized wrappers. Rebuilding Mercato therefore remains deterministic and does not depend on hand-edited scene instances.

### 3D AI Studio generation policy

- Credits were purchased specifically for this project and are approved for deliberate environment production batches.
- Existing assets remain useful supporting inventory, but generation is not restricted to last-resort gap filling.
- The first Mercato batch targets at least three high-visibility authored pieces: a bakery-specific stall/display, a produce merchant stall/display, and a dry-goods or cloth-merchant structure. Secondary candidates include a fish/meat table, period handcart, scales, racks, and distinctive street furniture.
- Every generated asset records its task/source ID and prompt in project-owned metadata, uses PBR textures, and is imported through a normalized wrapper prefab.
- Target ordinary market props at mobile-conscious production complexity; reserve higher detail for large hero structures that dominate the locked camera.
- Generated assets require scale, pivot, material, collider, shadow, LOD/culling, and gameplay-camera review before placement.
- Credentials and API keys remain environment-only and never enter Unity assets, scripts, logs committed to Git, or design documentation.

### Variants

Author at least four readable stall roles:

1. Produce: apple boxes, loose fruit, vegetable baskets, and sacks.
2. Bakery: loaf baskets, wooden boxes, cloth, and small storage props.
3. Dry goods: baskets, bags, crates, and a lantern.
4. General merchant: mixed containers and restrained display goods.

The specialized Giardino florist stall is not repeated as a generic market stall. It may be used only if a future authored florist vendor calls for it.

### Placement rules

- Retain the current commerce footprint and walkable aisles unless a source model requires a small, validated offset.
- Preserve stall-facing directions and keep the player-readable counter side toward the aisle.
- Maintain or simplify collision to stable box/compound colliders; merchandise does not receive individual gameplay collision.
- Keep tall geometry below the DynamicZoom architecture-height threshold so stalls cannot pump the exploration zoom.
- Vary goods, rotations, and small scale offsets deterministically. Do not scatter props into navigation lanes or tactical cells.
- Replace the current 16 identical/blockout instances with a controlled repeated set, not 16 unique heavy prefabs.

### Visual treatment

- Use the source stall's authored shape and texture detail where it reads well.
- Normalize overly glossy, saturated, or bright materials with project-owned URP materials or material overrides.
- Keep red, ochre, and green cloth families, but use faded natural dyes rather than flat color slabs.
- Merchandise should create asymmetrical clusters and visible negative space instead of a straight row of identical objects.
- Lighting, shadows, weather, and vertex/terrain systems remain unchanged.

## Seamless Interior Camera Profiles

### Architecture

Extend `SeamlessInteriorCameraZone` from an occlusion-only bridge into a reusable camera-profile owner. It continues to use the one shared Main Camera and existing Cinemachine camera; it does not create a second camera or scene transition.

The shared `DynamicZoom`/camera adapter exposes a temporary profile override containing:

- follow offset;
- optional look/pan offset relative to the player or room anchor;
- blend-in and blend-out duration;
- optional room bounds for clamping the camera target;
- priority for future overlapping room zones.

The camera zone captures the exterior/default profile once, applies the inn profile on entry, and restores the captured profile on exit. Repeated crossings must be idempotent.

### Florentine Inn authoring

- The threshold begins the blend; it must not snap exactly on the trigger frame.
- The lobby/courtyard receives a tighter offset and a modest inward pan so the player and room architecture remain visible together.
- The camera continues to track the player; this is not a fixed security-camera view.
- Room framing must not reveal missing exterior backsides or show roofs/walls that the occlusion system is hiding.
- The exterior Mercato profile is restored smoothly when the player leaves.
- Battle startup inside the protected social interior remains blocked as it is today.

### Failure behavior

If the Main Camera, Cinemachine body, or target cannot be resolved, the camera zone logs one actionable warning and leaves the current camera unchanged. It must never disable player control, lose the follow target, or leave a partial override active.

## Responsive City Map Location Card

### Current problem

The current-location card uses a fixed 400-pixel-wide vertical layout. At the observed phone-sized viewport, the rating/review text, preview, long blurb, current-location badge, Nearby header, and location row exceed the available height and overlap the frame.

### New layout

- Use a bounded responsive width derived from the map canvas, with safe side margins.
- Place title and close button in a dedicated header row.
- Hide rating/review data in `ShowCurrent`; it is not useful when describing the player's present district and currently appears as an unexplained placeholder.
- Give the preview a clamped responsive height and preserve aspect without forcing the entire card taller than the viewport.
- Let the description use its preferred height up to a defined maximum; overflow lives in the card's vertical scroll region.
- Keep `You are here`, `Nearby locations`, and rows in normal layout order below the description.
- Give nearby rows a minimum mobile tap height and prevent their backgrounds from escaping the card padding.
- Destination and region cards retain meaningful travel-time/fare/weather information, but their stats row wraps or reflows instead of using a fixed 300-pixel text width.

### Presentation

Maintain the parchment, Cinzel/EB Garamond typography, dark ink, gold status accent, and existing map-first hierarchy. This is a layout repair, not a visual rebrand.

## Validation

### Stall validation

- No primitive `Stall_Goods_*` renderers remain in production stall variants.
- Every production stall resolves its source structure and required merchandise prefabs.
- Colliders remain stable, non-triggering, and do not block the authored aisle/tactical route set.
- A gameplay-camera screenshot confirms readable silhouettes, non-repetitive goods, restrained materials, and no obvious source-scale mismatch.

### Camera validation

- The seamless Mercato Play Mode walkthrough crosses the inn threshold repeatedly without duplicating cameras or losing the follow target.
- The camera offset changes gradually on entry, stays within the authored interior profile, and restores gradually on exit.
- Occlusion groups still switch correctly, and exterior framing after five repeated crossings matches the captured default profile.
- Player control, interaction, weather, and protected-interior battle locking remain intact.

### Map-card validation

- Test current, district-destination, and region-destination modes at desktop and phone-like portrait widths.
- No text, preview, badge, Nearby header, or row leaves the card bounds.
- Current-location mode contains no rating/review placeholder.
- Nearby rows remain tappable and invoke the same existing callbacks.
- Reopening the map does not create duplicate cards or canvases.

## Scope Boundaries

This pass does not add exterior street camera zones, redesign Mercato's full layout, create new vendors/NPCs, change commerce gameplay, replace the fountain, modify weather authority, or alter hybrid battle rules. New 3D AI Studio props remain limited to the approved stall/commerce presentation gaps identified during live review.
