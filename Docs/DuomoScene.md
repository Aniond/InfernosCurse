# Duomo (Santa Maria del Fiore) — Scene Reference

Interior scene, anachronistically complete for 1299 (deliberate: "too magnificent
to leave out"). Blockout + full art pass completed 2026-07-03. This doc records
the scene's contracts, how its art was produced, and the gotchas that will bite
anyone touching it. Companion docs: `ASSET_PIPELINE.md`, `Docs/HD2D_Camera.md`.

## Coordinate contract
- Design map `Refrences/designmaps/duomodesignmap.png`, **14 px = 1 wu**,
  origin = octagon center, **plan rotated: nave runs SOUTH** (player enters the
  south facade walking north). Walls H=11, T=0.6.
- Octagon: apothem 12.15 (wall centerline), face len ~10.07. Diagonal faces
  solid, cardinal faces open. **Open cardinal faces are WIDER than the 6.6
  throat mouths** → the ~1.4 wu slits flanking each throat are plugged by
  `Oct_Fill_{E,W,N}_{N,S,E,W}` (0.6×11×1.6, under `[Octagon]`).
- Nave interior x ±5.5, z −12.15..−34. Facade z=−34: 4 wall segments with door
  gaps at x `[-4.7..-3.1] [-1.2..1.2] [3.1..4.7]`, lintel solid y4–11.
  `Nave_Stub_E/W` plug the S-face/nave-wall junction.
- Piers: nave rows x ±2.75, z −14.5..−32.5 step 3; tribune chapel piers r=0.45.
- Floors are butt-joined (no coplanar overlap).

## Materials & floors (Assets/Data/Materials/Duomo/)
- Textures generated via **Tools/asset-gen** (Gemini) from David-approved
  concepts; 3DAI Studio's Seamless Texture Generator has **no API** — its use
  is manual in their web UI (we skipped it; straight Gemini albedos in use).
- `Generated/Gen_Floor_<piece>.mat` — **world-aligned UVs on stock URP Lit**:
  scale = worldSize/3.0, offset = worldMin/3.0 (from renderer.bounds). Pattern
  runs continuously across butt-joints → zero seams at transitions.
- `Gen_Floor_Floor_Octagon` — pavement medallion mapped ONCE (wrap=Clamp,
  scale 27/24.3 so the composition fits the visible interior; clamp margin is
  plain marble and reads invisibly through the throat mouths).
- `Generated/Gen_Wall_<repX>x<repY>.mat` — plaster walls, integer repeats,
  WallTile=2.75 (11-high walls = 4 rows). **Wall cube length lives in
  localScale.x OR .z** (the other is 0.6 thickness) — always
  `max(s.x, s.z)`; assuming x stretched every north–south wall 8× once.
- Editor tooling: `Assets/Editor/DuomoTilePass.cs` (menu *InfernosCurse → Duomo
  Tile Pass*, batch-runnable) and `DuomoTileShots.cs` (no-save verification
  captures with studio lighting).

## 3D art (Prism 3.1 via Tools/asset-gen, ~660 credits total)
- **Paintings** (`Assets/Models/Duomo/Paintings/`, under `[Paintings]`):
  Dante Illumining Florence (nave E, z=−22 bay midpoint), Annunciation (nave W —
  generated as a full 3D loggia diorama), Last Judgment gothic altarpiece
  (N tribune), Madonna Enthroned (E tribune), St John Baptist (W tribune).
- **Compound pier** (`duomo-compound-pier.glb`): instanced at all 14 nave piers
  (`[NavePierModels]`, footprint clamped 1.6 wu × 11 high — the ~2.5× vertical
  stretch of the stocky concept reads as elegant Gothic) and all 24 tribune
  piers (`[TribPierModels]`, 0.9 × 9). Placeholder cylinders keep colliders,
  renderers disabled.
- **Statues** (`Assets/Models/Duomo/Statues/`, under `[Statues]`), from the real
  cathedral's program (12 Apostles cycle, Arnolfo facade figures, Donatello
  prophets): Zenobius (N tribune), Madonna of the Glass Eyes (facade pier
  x=2.15), Reparata (E tribune), John Baptist (W tribune), Zuccone (nave W
  aisle z=−25). Penitent Magdalene v1 was REJECTED by Tripo's content policy
  ("clothed in her own hair"); robed v2 concept in the workbench.
- **GLB placement recipe**: instantiate → measure combined renderer bounds →
  uniform-scale to target height → yaw-only rotation → snap bounds to anchor.
  **Front axis is NOT consistent** (this batch: 5× −X, 3× +X). Capture-verify
  every model and flip 180° as needed. Never use
  `Quaternion.FromToRotation(+X, −X)` — it can pick the Z axis and hang things
  upside down; use `SignedAngle`/`AngleAxis` about Y.

## Camera & runtime
- `Assets/Scripts/Camera/CameraOcclusionFader.cs` (on Main Camera): spherecasts
  camera→player each LateUpdate, hides wall segments (and pier models via the
  collider→`PierModel_<name>` map) on the sightline, restores when clear.
  Fixes the hidden-player problem at the entrance and throat turns.
- `PlayerController` moves camera-relative (screen-up = away from camera) —
  required because this rig looks south while PV/Mercato look north.
- COZY: `[FXBlockZones]` — 5 trigger BoxColliders tagged **"FX Block Zone"**
  (nave, octagon+throats, 3 tribunes; y 0..30 columns) suppress leaves/rain
  indoors. That tag was missing project-wide until 7/03 — its absence spams
  `Tag: FX Block Zone is not defined` errors on scene load and can block Play.
- Guild zones: Cappella Maggiore + Cassetta delle Offerte both DISABLED in
  `GameSystems.prefab` (sceneName → `Duomo_DISABLED_*`) until their flows are
  ready.

## Unity-MCP working notes (apply to any scene)
- RunCommand snippets that call `EditorSceneManager.SaveScene` or
  `AssetDatabase.DeleteAsset` fail with "user interactions are not supported".
  **Workaround: `EditorApplication.ExecuteMenuItem("File/Save")` works.**
- Editor throttles rendering when unfocused → capture tools return **stale
  frames** (identical byte sizes = the tell). Fixed via
  `EditorPrefs.SetInt("InteractionMode", 1)` (No Throttling).
- Script edits → domain reload → cached instance IDs go stale ("Failed to
  render scene preview") — re-Find objects for fresh IDs.
- Manual RenderTexture captures are unreliable in the live editor; use the MCP
  camera-capture tool with a temp positioned camera. RT captures DO work in
  `-batchmode` (and check `Get-Process Unity` first — two editors = lock).

## Remaining work
- Props pass: high altar (N tribune dais), baptismal font, choir screen,
  candelabra, door props for the open facade gaps.
- Magdalene v2 statue (concept awaiting approval), nave walkway floor tile
  (circles + porphyry discs, generated, unapproved).
- Candle intensity / DoF tune under the final art; playtest captures.
