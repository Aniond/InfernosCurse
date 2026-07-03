# Asset Generation Pipeline

How 3D props get made for Inferno's Curse: AI concept image → human approval →
image-to-3D → Unity placement. Established 2026-07-03.

## The workbench

`Tools/asset-gen/` — a zero-dependency local web app (Node 20).

```
cd Tools/asset-gen
node server.mjs        # → http://localhost:3311
```

- **Keys** live in `Tools/asset-gen/.env` (gitignored — never commit keys):
  `GEMINI_API_KEY` and `3D_AI_STUDIO_API_KEY`.
- Asset list persists in `assets.json`. Images land in `output/images/<id>.png`,
  models in `output/models/<id>.glb` (both gitignored).
- Every 3D AI Studio response is journaled to `output/_tripo/` (JSONL + per-task
  JSON), so an interrupted generation is recoverable by task id — credits are
  spent at submit time.

## The workflow (THE RULE)

1. **Concept**: generate the image with Gemini (`gemini-2.5-flash-image`,
   "Nano Banana"). Prompt discipline for image-to-3D:
   - *single object*, entire object in frame, three-quarter view
   - *plain white background*, soft even lighting, **no cast shadows**
   - **"No text anywhere in the image"** — Gemini loves adding banners/captions,
     and they get baked into the 3D mesh as floating geometry.
2. **Approval — David only.** The concept is shown for review (chat or the
   workbench UI). Regenerate with the "extra direction" field as many times as
   needed. **No 3D submission happens until David explicitly approves the
   image.** Credits are real money; image quality is his call.
3. **3D generation**: approved images go to 3D AI Studio's **Prism 3.1**
   (API path `/v1/3d-models/tripo/image-to-3d/3.1/` — "Prism" is the Studio UI
   name for this model). Standard textured tier = **60 credits/model**
   (40 base + 20 texture, PBR included). Do **not** use Tripo P1 (100 credits).
   - Rate limit: 3 submissions/minute, enforced by a 21s submission gate in
     `tripo.mjs`. Batches submit spaced but poll concurrently.
   - Generation takes ~1.5–5 minutes per model.
4. **Preview**: `http://localhost:3311/preview/<id>` — Three.js orbit viewer.
5. **Unity placement** (recipes proven on the Ponte Vecchio props):
   - Copy the GLB into `Assets/Environment/<Zone>/Props/` — glTFast (already in
     the project) imports it natively.
   - **Measure renderer bounds, not the pivot** — scale by
     `target_height / bounds.size.y`, then re-center by bounds.
   - These generations tend to have their **front along +X**; rotate to face
     (verify with a camera capture — the back is usually plain).
   - Generator **watermarks are baked onto the base** — sink the model's plinth
     into a stone pedestal to bury them.
   - Add a BoxCollider sized to the bounds; every renderer in a scene gets a
     collider (project census rule).

## Cost cheat-sheet

| Step | Cost |
| --- | --- |
| Gemini concept image | ~free (API pennies) — iterate freely |
| Prism 3.1 model, standard textured | 60 credits |
| Prism 3.1 "detailed" texture | +40 (don't, unless hero asset) |
| Tripo P1 | 100 — not used |
