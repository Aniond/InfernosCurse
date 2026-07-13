# Albergo Fiorentino Medallion Sign Design

## Status

Approved for implementation on 2026-07-13. The user selected the medallion direction because its silhouette is easier to read and explicitly authorized implementation without further approval gates.

## Goal

Replace the Mercato inn façade's dark rectangular placeholder with a period-inspired hanging medallion that clearly identifies the Albergo Fiorentino from either north/south street approach.

## Visual design

- A circular, gently beveled, double-sided sign approximately 1.35 metres in diameter and 0.14 metres thick.
- Dark chestnut wood faces with a worn-brass outer rim.
- A simplified gold Florentine lily above the two-line name `ALBERGO FIORENTINO`.
- Warm ivory or muted-gold lettering using the project's Cinzel display font.
- A forged-black-iron wall bracket with two visible hanging rings.
- Restrained material wear. The sign should look established and handcrafted, not luminous or newly manufactured.

## Placement and readability

- Replace `InnFacade_Sign` while preserving the existing façade and general mounting location.
- Keep the sign perpendicular to the façade and provide the emblem and name on both faces.
- Size and orient the text for the project's elevated HD-2D exploration camera, prioritizing a strong silhouette and readable two-line name.
- The sign remains static; no swinging physics or runtime animation is required.

## Implementation boundaries

- Author the complete static prop in Blender and keep the reproducible Blender Python source under `Tools/Blender/`.
- Export the finished prop as a Unity-ready GLB under the Mercato production kit. The model includes the medallion, brass rim, raised lily, double-sided raised lettering, bracket, and hangers.
- Use the project's Cinzel TTF as the Blender lettering source, converting the final letters to mesh before export so readability does not depend on runtime font setup.
- Instantiate the model through `MercatoVecchioProductionKitBuilder` so rebuilding the production kit and scene remains deterministic.
- Preserve the façade renderer naming convention so the existing seamless-inn camera cutaway continues to include the sign.
- Do not alter inn gameplay, interaction, camera profiles, or the standalone inn interior.

## Validation

- Production-kit validation must confirm the medallion, rim, bracket, two hangers, lily ornament, and both named lettering meshes exist.
- Rebuild the Mercato production kit and scene, then run the production validator.
- Inspect the sign live from both north and south approaches in Game view.
- Run the existing Mercato seamless play-mode verifier to ensure the façade still cuts away and restores correctly.

## Completion record

After live verification passes, add the sign as a new verified-current entry in `Docs/MASTER_COMPLETION_REGISTER.md`.
