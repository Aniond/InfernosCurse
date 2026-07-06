# InfernosCurse — AI Context & Developer Handover Blueprint

**Generated:** 2026-07-05 · **Method:** 8-agent parallel full-codebase sweep (83 first-party C# files, all root docs, all shader assets, pipeline/settings assets) + verified against live editor state.
**Audience:** LLM agents (Claude, Gemini, GPT) onboarding onto this repository. Everything below is *verified against files on disk at generation time* — repo-relative paths are cited so you can re-verify before acting. When this document and the code disagree, **the code wins**; update this document.

**TOC:** [§1 Ground Truth](#1-ground-truth--anti-hallucination-facts) · [§2 Architectural Map](#2-architectural-map) · [§3 Prompt Blocks](#3-ai-optimized-prompt-blocks) · [§4 Behavioral Restrictions](#4-behavioral-restrictions--what-not-to-do) · [§5 Bug-Squashing Workflow](#5-standardized-bug-squashing-workflow) · [§6 Editor Tooling & Asset Pipeline](#6-editor-tooling--asset-pipeline) · [§7 Document Registry](#7-document-registry--staleness-map)

---

## 1. GROUND TRUTH — Anti-Hallucination Facts

Facts LLMs most commonly get wrong about this repo. Internalize before generating any code.

| # | Fact | Verification |
|---|------|--------------|
| 1 | **There are ZERO first-party shader files.** All 20 `.shader` + all `.shadergraph` files belong to third-party packages (TextMesh Pro, Stylized Water 3, Procedural Terrain Painter, EmaceArt). First-party rendering goes through built-in URP shaders via `Shader.Find`, `MaterialPropertyBlock`, `TerrainData`, and package C# APIs. **Never author, edit, or "fix" a ShaderLab file.** | `Glob **/*.shader` |
| 2 | **No C# namespaces.** All 83 first-party scripts live in the global namespace. File = class. Do not add `namespace InfernosCurse { }` wrappers. | any file in `Assets/Scripts` |
| 3 | **Public inspector fields, not `[SerializeField] private`.** House style is `public float x;` under `[Header]`/`[Tooltip]`. `[SerializeField]` appears ~22× project-wide, only for runtime-inspectable private state. | `Assets/Scripts/Battle/*` (zero occurrences) |
| 4 | **The pipeline asset binds through QualitySettings, not GraphicsSettings.** `ProjectSettings/GraphicsSettings.asset` `m_CustomRenderPipeline` is `{fileID: 0}`. The single quality level "PC" → `Assets/Settings/PC_RPAsset.asset` (Forward+, Depth+Opaque ON, SSAO + StylizedWater render features on `PC_Renderer.asset`). `Mobile_RPAsset`/`Mobile_Renderer` exist but are **unreferenced and featureless** — switching to them silently kills water rendering and SSAO. | `ProjectSettings/QualitySettings.asset` |
| 5 | **`HD2D_PostProcessing.asset` is an empty trap** (six `{fileID: 0}` components, referenced by nothing). The real post stack is `Assets/Settings/SampleSceneProfile.asset` — used **both** as the pipeline default volume profile **and** as the HD2D_CameraKit Global Volume profile. Editing it changes every scene at once. | `Assets/Settings/` |
| 6 | **There is no NPC dialogue system.** All "interaction" is walk-into trigger zones (`GuildInteractionZone`) opening self-built panels. Do not invent a dialogue manager. | grep `dialog\|dialogue` → 0 hits |
| 7 | **`WorldMapUI` / `MapNodeView` / `MapNodeDetailPanel` / WorldMap.unity are LEGACY**, superseded by `GugolMapUI` as the travel surface. Don't extend them. | `Assets/Scripts/Map/WorldMapUI.cs:17` |
| 8 | **Skills/jobs do NOT load from `Resources/`.** Save-game restore goes through `AssetRegistry` (name→asset arrays on GameSystems prefab, populated by editor menu). | `Assets/Scripts/Data/AssetRegistry.cs` |
| 9 | **`BUG_AUDIT.md` is historical/closed** (all items `[x]`, references deleted files). **`NIGHT_AUDIT_2026-07-04.md` is the live bug menu** (P0s fixed 7/05 commit `2efaa40`; P1–P3 open). See [§7](#7-document-registry--staleness-map). | root `*.md` |
| 10 | **Game code asks time only via `GameClock`** (static facade over COZY). Never call `CozyWeather.instance.timeModule` from gameplay code. | `WEATHER_SYSTEM.md` |

### 1.1 Machine-ingestable project context

```json
{
  "project": "InfernosCurse",
  "elevator": "HD-2D (Octopath-style) RPG set in 1299 Florence; curse/corruption economy over a district hub graph; FFT-style tactical battles; Google-Maps-parody travel UI",
  "engine": "Unity 6000.4.11f1",
  "renderPipeline": { "urp": "17.4.0", "activeAsset": "Assets/Settings/PC_RPAsset.asset", "renderer": "Assets/Settings/PC_Renderer.asset", "boundVia": "QualitySettings level 'PC' (GraphicsSettings custom RP is null)", "features": ["SSAO(intensity .4, radius .3)", "StylizedWater3.StylizedWaterRenderFeature"], "mode": "Forward+", "postProfile": "Assets/Settings/SampleSceneProfile.asset" },
  "input": "com.unity.inputsystem 1.19 (PlayerInput SendMessages on player; UI code polls Keyboard.current WITH legacy Input fallback)",
  "keyPackages": ["cinemachine 3.1.3", "cloud.gltfast 6.10.1 (GLB import)", "com.distantlands.cozy.core (EMBEDDED, never edit)", "xyz.staggart-creations.stylized-grass (embedded)"],
  "firstPartyCode": { "runtime": "Assets/Scripts/{Battle,Battle/AI,Battle/UI,Camera,Core,Curse,Data,Environment,Guilds,Map,Player,Quests,Rest,UI}", "editor": "Assets/Editor (static [MenuItem] tools only, menu root 'InfernosCurse/')", "count": 83 },
  "thirdPartyInAssets": ["VegetationSpawner (sc.terrain.vegetationspawner)", "Stylized Water 3 (StylizedWater3)", "TextMesh Pro", "EmaceArt/Slavic World Free", "Low-Poly Medieval Market", "ProceduralTerrainPainter (INSTALLED BUT BANNED — multi-layer blending broken in this project)", "Distant Lands (COZY content)"],
  "persistentRoot": { "prefab": "Assets/Resources/GameSystems.prefab", "spawnedBy": "GameSystemsBootstrap [RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]", "carries": ["GameCalendar", "HubMap (component DISABLED — load-bearing)", "DailyCurseDrift", "AssetRegistry", "GuildSystem", "PartyRoster", "FlorenceWeather", "CozySceneAdapter", "COZY sphere", "GugolMapUI", "RestMenuUI", "GuildPanelUI", "FastTravelMenu", "GuildInteractionSpawner"] },
  "scenes": { "explorable": ["MercatoVecchio", "PonteVecchio", "Duomo", "Fiesole", "GiardinoDelleRose"], "battle": ["Battle", "BattleArena"], "legacy": ["WorldMap"], "rule": "scene present in Build Settings == its map pin is unlocked (MapRouting.IsUnlocked → Application.CanStreamedLevelBeLoaded)" },
  "nodeGraph": { "authoredOn": "GameSystems.prefab HubNodeData via Assets/Editor/GugolMappeSetup.WireNodes", "ids": "snake_case Italian (mercato, signoria, giardino_rose, wp_mugnone)", "enums": "MicroClimate/NodeKind/MapLevel are APPEND-ONLY (serialized as ints)" },
  "namingConventions": { "privateFields": "_camelCase", "events": "public event System.Action<T> OnXxx, invoked ?.Invoke()", "logs": "Debug.Log(\"[ClassName] ...\")", "hierarchyGroups": "[BracketNames] ([PlacedProps], [Trees], [CameraRig])", "markers": "MARKER_<assetId>@<height>, ENTRY_<id>, FALLBACK_<id>, <assetId>_mesh", "assets": "TL_* terrain layers, Gen_* generated materials, Skill_/Job_/Enemy_* data assets, HD2D_* camera kit", "sceneNames": "PascalCase; node ids snake_case" },
  "singletonTiers": { "houseLazy": "static T _instance; Instance => _instance != null ? _instance : (_instance = FindAnyObjectByType<T>()) — survives mid-play domain reloads (HubMap, GameCalendar, FlorenceWeather, AssetRegistry, GugolMapUI, GuildSystem, GuildPanelUI, RestMenuUI)", "classic": "Instance { get; private set; } + Awake dup-destroy (BattleManager, DamageNumberPool, BattleCurseAutomata, CameraShake)", "staticServices": "GameClock, SaveSystem, RestSystem, FlorinWallet, TravelIntent, DistrictTracker, PendingEncounter, EncounterRoll, MapRouting, BattleFormulas, AbilityResolver" },
  "crossSceneHandoff": "the 'TravelIntent idiom': static payload class with Set/Consume(/Complete) lifecycle (TravelIntent, PendingEncounter)",
  "serialization": { "save": "JsonUtility, 3 slots, persistentDataPath/save_slot_{n}.json, CURRENT_VERSION=2, atomic .tmp+File.Replace", "jaggedData": "pipe/semicolon-joined strings (JsonUtility cannot do jagged arrays)", "migration": "guard-and-skip (0/null = absent field), never transform" },
  "designRules": { "curse": "HIDDEN value — words/vibes only, never numbers/gauges (666 reviews = the one tell)", "uiLanguage": "chrome ENGLISH, place names accurate Italian; wordmark 'Gugol Maps'", "guildPerks": "bend the corruption economy, never zero it (rest multiplier floor 0.25)", "assetSpend": "NO 3D-generation spend without David's explicit image approval (ASSET_PIPELINE.md THE RULE)" }
}
```

---

## 2. ARCHITECTURAL MAP

### 2.1 System topology (strict data flow)

```
[RuntimeInitializeOnLoadMethod BeforeSceneLoad]
GameSystemsBootstrap ──spawns──► Resources/GameSystems.prefab (DontDestroyOnLoad)
                                   │ re-validated on every sceneLoaded
     ┌─────────────────────────────┼──────────────────────────────────────┐
     ▼                             ▼                                      ▼
TIME/WEATHER CHAIN            WORLD STATE                            UI OVERLAYS (Pattern-B)
COZY sphere (3rd party)       HubMap (graph; component DISABLED)     GugolMapUI (sort 490)
  └─ GameClock [static facade]  ├─ HubNode list + neighbor edges     RestMenuUI / GuildPanelUI /
     │  THE ONLY time API       ├─ OnNodeChanged → pin tints           FastTravelMenu (sort 500)
     ▼                          └─ Export/ImportNodeStates (save)      └─ self-built Canvas in Awake,
GameCalendar (midnight wrap   DailyCurseDrift ◄─OnDayChanged─┐            zero scene wiring
  detector: hour drop >6h)      (SOLE hub-curse driver;      │
  ├─ OnDayChanged ───────────────day-key idempotent)─────────┘
  ├─ OnMonth/Season/YearChanged
  ▼
FlorenceWeather (POLLS calendar+district; deterministic per (year,dayOfYear,nodeId))
  └─ CozyEcosystem.SetWeather(profile-by-name, manual mode)
CozySceneAdapter (sceneLoaded): battle scenes → COZY SetActive(false) [time freezes];
  normal scenes → reactivate + DISABLE every non-COZY directional light
```

```
TRAVEL / ENCOUNTER FLOW (one direction, no cycles)
ZoneExit trigger (dwell 0.15s, arm 0.5s, re-arm-on-exit)
  └─► GugolMapUI.Open (pause: timeScale 0; owns ESC while open)
        └─ MapRouting.FindPath (BFS over HubMap neighbors)
        └─ city travel: advance GameClock minutes → DistrictTracker.CurrentNodeId
                        → TravelIntent.SetEntry(entryId) → restore timeScale → LoadScene
        └─ region travel: FlorinWallet.TrySpend(fare) UP FRONT
                        → per-Waypoint EncounterRoll.ShouldTrigger (FNV-1a, pure fn of (day,node))
                        → ambush: capture ORIGIN before repointing tracker,
                          PendingEncounter.Set(victory=destination, defeat=origin),
                          LoadScene(BattleArena), NO time advance
destination scene: ZoneEntryPlacer.Start → TravelIntent.Consume → rb.position teleport + SetFacing
```

```
BATTLE FLOW (BattleArena)
EncounterBootstrap.Awake CONSUMES PendingEncounter (before any Start — keeps
BattleTestStarter's stand-down guard true)
  └─► PartyRoster.EnsureInitialized (fills static RestSystem.PartyMembers)
  └─► BattleManager.StartBattle
        ├─ BattleUnit.Initialize CLONES CombatantData **except Dante role**
        │    (Benidito keeps original ref so absorption/job progression persist)
        ├─ BattleCurseAutomata.SeedFromHub(HubMap.GetBattleSeedCurse(DistrictTracker.CurrentNodeId))
        ├─ EnemyAI.InitSharedState (MANDATORY or every AI passes forever)
        └─ BattleLoop coroutine: CTQueue.TickUntilReady → charge resolution IN THE LOOP
           (never mid-CT-tick) → ProcessUnitTurn → AbilityResolver.Resolve
           → BattleFormulas (shared CalcDamageCore keeps forecast & roll in lockstep)
           → kill: TryAbsorb (Dante only) + AwardPostKill (AP/XP/florins/guild rep)
           → automata.Step between turns
OnVictory/OnDefeat → EncounterBootstrap (THE ONLY SUBSCRIBER) → wait 1s realtime
  → CopyBack clone numbers onto roster (Benidito skipped by ref-equality)
  → victory: apply journey time (forward SetHour wrap OR AdvanceDay×N+SetHour+ResyncClock)
  → defeat: −50% florins, +0.05 curse on waypoint, +1 day, revive 50%, return to ORIGIN
```

```
SAVE/LOAD (SaveSystem, static, v2)
Save: REFUSED in Battle/BattleArena (scene-NAME guard, double-guarded in MenuManager)
Load: LoadAndApply → load saved scene first (one-shot sceneLoaded) → ApplySave STRICT ORDER:
  player pos (rb-aware) → GameCalendar.SetDate (NO events) → GameClock.SetHour → ResyncClock
  → wallet/district → GuildSystem.ImportFrom → HubMap.ImportNodeStates
  → DailyCurseDrift.RestoreDayKey → FlorenceWeather.Apply → party (job BEFORE HP/SP, clamped last)
Why the order is load-bearing: weather is a pure function of the date; drift key must match
restored day; job level changes HP maxima.
```

### 2.2 C# ↔ rendering interaction contract

There is no first-party ShaderLab. The **entire** C#→GPU surface is:

| Writer (first-party) | Mechanism | Shader/property contract |
|---|---|---|
| `LightShaft.cs` (Environment) | procedural cone mesh + `Shader.Find("Universal Render Pipeline/Particles/Unlit")` (fallback `URP/Unlit`), per-frame **MaterialPropertyBlock** | `_BaseColor`/`_Color`, keyword `_ALPHAPREMULTIPLY_ON`, hand-set `renderQueue = 3100` |
| `TorchFlicker.cs` | `Light.intensity` + baked 64px R8 cookie (throttled 0.1 s) + `.material` instance | cookie requires **Spot** light (URP point lights reject 2D cookies) + `m_SupportsLightCookies` on PC_RPAsset; writes `_EmissionColor` |
| `ScrollingWater.cs` | `.material` runtime instance (comment: "never dirties the shared asset") | `_BaseMap` texture offset (Arno plane — plain URP Lit, **not** Stylized Water) |
| `OcclusionFade.cs` (Camera) | clones `Building`-tag materials at Start, force-converts to URP transparent | `_Surface=1`, keyword `_SURFACE_TYPE_TRANSPARENT`, `_SrcBlend/_DstBlend/_ZWrite`, alpha via `_BaseColor` — **non-URP-Lit shader on a Building breaks the fade** |
| `CameraOcclusionFader.cs` | toggles `Renderer.enabled` (dollhouse), `SphereCastNonAlloc` + static buffer | name-prefix coupled: `Oct_`, `Nave_Wall_`, `Facade_`, `Throat_`, `Trib_`; piers via `PierModel_<name>` sibling map |
| `DepthOfFieldFocus.cs` | URP Volume override `DepthOfField.focusDistance` per LateUpdate | sits on the Global Volume that shares `SampleSceneProfile.asset` with the pipeline default |
| `DuomoTileShots.cs` (**editor, no-save**) | the ONLY `RenderSettings` writer in the repo | runtime fog/sky belongs to COZY — never add RenderSettings writers |
| Editor scene builders | `TerrainData.SetHeights/SetAlphamaps`, `TerrainLayer` assets, `StylizedWater3.WaterMesh.Create(Disk)` + `WaterObject.New`, `new Material(Shader.Find("Universal Render Pipeline/Lit"))` | see [§6](#6-editor-tooling--asset-pipeline) |

**Data direction is strictly C# → render state.** Nothing reads back from the GPU. COZY owns sun/sky/fog at runtime; `CozySceneAdapter` disables any directional light you author (edit-mode lighting only).

### 2.3 Known per-frame allocation debt (verified, current)

These exist today; don't add more (see [§4](#4-behavioral-restrictions--what-not-to-do)). Fix opportunistically if touching the file:

```json
[
  {"file": "Assets/Scripts/Environment/LightShaft.cs", "line": 122, "issue": "new MaterialPropertyBlock() per shaft per LateUpdate ([ExecuteAlways] — churns in edit mode too)"},
  {"file": "Assets/Scripts/Camera/OcclusionFade.cs", "line": "59-65", "issue": "Physics.RaycastAll + new HashSet<Renderer>() + GetComponentsInChildren per hit, every Update"},
  {"file": "Assets/Scripts/Camera/DynamicZoom.cs", "line": 144, "issue": "Physics.RaycastAll ×8 directions per LateUpdate when useClearanceZoom=true"},
  {"file": "Assets/Scripts/Player/PlayerController.cs", "line": 130, "issue": "Physics.RaycastAll every FixedUpdate (SnapToGround)"},
  {"file": "Assets/Scripts/Curse/DailyCurseDrift.cs", "line": 61, "issue": "day-key string concat every frame (acknowledged safety-net poll)"},
  {"file": "Assets/Scripts/Battle/UI/BattleForecastUI.cs", "line": "48-91", "issue": "interpolated label strings every frame while aiming"},
  {"file": "Assets/Scripts/UI/CalendarDisplayUI.cs", "line": 55, "issue": "FindAnyObjectByType per frame UNTIL calendar binds (permanent if none in scene)"}
]
```

The allocation-free counterexample to copy: `CameraOcclusionFader.cs` (static `RaycastHit[32]` + `SphereCastNonAlloc`, reused collections, cached name→object map).

---

## 3. AI-OPTIMIZED PROMPT BLOCKS

Copy-paste these verbatim into future agent sessions. Compose: SYSTEM CORE + one TASK block.

### 3.1 SYSTEM CORE (always include)

```text
You are working on InfernosCurse: a Unity 6000.4 / URP 17.4 HD-2D RPG set in 1299
Florence. Repo root contains Assets/, Packages/, Tools/asset-gen/, docs/, and root-level
audit docs (BUG_AUDIT.md, NIGHT_AUDIT_2026-07-04.md, WEATHER_SYSTEM.md, ASSET_PIPELINE.md).
Read docs/AI_HANDOVER_BLUEPRINT.md before your first edit.

HARD FACTS (do not contradict):
- Zero first-party shaders. Rendering = built-in URP shaders via Shader.Find +
  MaterialPropertyBlock + third-party packages (Stylized Water 3, COZY weather).
  Never create or edit .shader/.shadergraph files.
- All first-party C# is in the GLOBAL namespace. File = class. No namespaces.
- Inspector data = public fields + [Header]/[Tooltip]. Private fields = _camelCase.
- Events: public event System.Action<T> OnXxx; invoke with ?.Invoke(); UI subscribes
  in Start/OnEnable and unsubscribes in OnDestroy/OnDisable — always paired.
- Singletons: lazy pattern `_instance != null ? _instance : (_instance =
  FindAnyObjectByType<T>())` (survives mid-play domain reloads). Static classes for
  stateless services. Cross-scene payloads use static Set/Consume classes
  (TravelIntent idiom).
- Time is read ONLY via GameClock (static facade over COZY). GameCalendar detects
  midnight by wrap; any BACKWARD GameClock.SetHour must be followed by
  GameCalendar.ResyncClock(); NEVER ResyncClock after a forward jump.
- Persistent systems live on Assets/Resources/GameSystems.prefab (spawned by
  GameSystemsBootstrap). HubMap's component is DISABLED there ON PURPOSE
  (DailyCurseDrift is the sole hub-curse driver). Never enable it.
- Saves: JsonUtility, parallel arrays, pipe-joined nested data, versioned
  (CURRENT_VERSION), guard-and-skip migration. Save order is load-bearing
  (date→hour→ResyncClock; job→HP/SP). No saving in Battle/BattleArena.
- Curse values are HIDDEN from the player: words and vibes, never numbers or gauges.
- UI chrome is ENGLISH; place names are accurate Italian.
- Comments in this codebase encode constraints ("LOAD-BEARING", "BUG FIX:", "David
  7/05"). Read surrounding comments before changing anything; preserve their intent.

VERIFICATION BAR: code must compile AND the affected flow must be exercised in play
mode before claiming done. Static checks are not completion. Automated playtests:
inject a driver MonoBehaviour that calls PlayerController.DebugSetMove(Vector2)
(editor-only hook). Programmatically-started play sessions enter PAUSED at frame 0 —
unpause (EditorApplication.isPaused = false) after any scripted play entry.
```

### 3.2 TASK BLOCK — gameplay/runtime code change

```text
TASK RULES (runtime C#):
- Match the subsystem's existing patterns before inventing one. Search first:
  the codebase already has an idiom for cross-scene state, self-built UI, deterministic
  RNG (FNV-1a StableHash — string.GetHashCode is NOT stable), trigger zones
  (dwell+armDelay+re-arm), and forecast/roll lockstep math (BattleFormulas.CalcDamageCore).
- No per-frame allocations in Update/FixedUpdate/LateUpdate: no LINQ, no new
  collections, no RaycastAll where a NonAlloc + static buffer works, no string
  building, no Camera.main/Find* except behind a null-cache self-heal.
- ScriptableObjects carry data ([CreateAssetMenu("InfernosCurse/...")]); runtime
  mutable state does NOT live on shared SO assets (clone per unit — see
  BattleUnit.Initialize; Dante-role exception is deliberate).
- APPEND-ONLY enums: MicroClimate, NodeKind, MapLevel (serialized as ints on
  GameSystems.prefab). Never reorder or remove members.
- New zone registration = HubNodeData via GugolMappeSetup.WireNodes/AddTeaserNode
  (editor menu), NOT a new ScriptableObject type. Pin visibility = scene present in
  Build Settings; there is no separate unlock flag system yet.
- Adding a saved field: bump nothing unless shape changes; default value must mean
  "absent in old save"; extend SaveData with parallel arrays; restore in ApplySave
  respecting the documented order.
- Never load a scene from inside a death/event callback chain (wait ≥1s realtime —
  see EncounterBootstrap.VictoryFlow).
- Menus/overlays: Resume()/restore timeScale BEFORE handing off or loading scenes;
  use unscaled time inside paused overlays; call EnsureEventSystem() in any
  self-building panel.
```

### 3.3 TASK BLOCK — editor tooling / scene building

```text
TASK RULES (Assets/Editor):
- Tools are static classes with [MenuItem("InfernosCurse/<Zone>/<N>. <Step>")] —
  numbers encode execution order. LINQ/Find allowed here (one-shot commands).
- Scene builders are DESTRUCTIVE full rebuilds (NewScene → SaveScene → Build
  Settings); prop placement consumes MARKER_<assetId>@<height> objects and is
  re-runnable (missing GLB → FALLBACK_ stub survives until the next run).
- Terrain: paint splats directly via TerrainData.SetAlphamaps (Procedural Terrain
  Painter is BANNED — its multi-layer blending is broken in this project). Call
  data.SetDetailResolution(512,16) — the property is read-only. Never assign
  Texture2D.whiteTexture to a TerrainLayer (renders as missing-texture checker).
- Grounding: sample REAL terrain (Terrain.SampleHeight), never the analytic height
  function (~15cm error on slopes); ground to the MAX height over the prop's
  FOOTPRINT (9-point sample), sink bushy props ~0.12m, hard-based ~0.03m; rim-
  straddling objects need a raise-only heightmap berm (RaiseShelf pattern).
- Generated GLBs (Prism 3.1): unit-ish scale, sometimes wrong up-axis (per-asset
  AxisFix table), center pivots — NEVER use as Unity terrain-tree prototypes
  (rejected: "no valid mesh renderer" → invisible trees); wrap in a base-pivot
  prefab and place as GameObjects. Rotate pivots BEFORE bounds-based grounding.
- Water: size exactly via StylizedWater3.WaterMesh.Create(Shape.Disk, size, vtxDist)
  + WaterObject.New. NEVER scale the shipped water prefab plane (it is enormous;
  caused the 'cyan cutout grass' misdiagnosis).
- Prefab mutation: PrefabUtility.LoadPrefabContents + SaveAsPrefabAsset +
  UnloadPrefabContents in try/finally. Serialized prefab data predates code
  defaults — changing a field default does NOT reach existing prefabs; patch explicitly.
- Scene saving from automation: EditorSceneManager.SaveScene can be blocked in
  MCP/RunCommand contexts; EditorApplication.ExecuteMenuItem("File/Save") works.
  After ANY scripted scene save run the duplication canaries: exactly 1 Terrain,
  ≤1 EventSystem/AudioListener, expected [group] counts (MercatoVecchio was once
  duplicated 6× by a full-scene save).
- A scene's Build Settings entry is LOAD-BEARING (controls map-pin unlock).
```

### 3.4 TASK BLOCK — bug fixing (pair with §5 protocol)

```text
TASK RULES (bug fix):
- Follow docs/AI_HANDOVER_BLUEPRINT.md §5 exactly: parse → verify-current → reproduce
  → fix → verify → log back.
- Audit entries cite file:line from their generation date — treat line numbers as
  hints, re-locate by symbol. BUG_AUDIT.md references files deleted in the COZY
  migration (DayNightCycle.cs, WeatherSystem.cs) — such entries are historical.
- Defensive-fix house patterns: null-guard with `?.` consistently (MenuManager
  precedent); unsubscribe in OnDestroy; re-check IsAlive after any call that can
  kill (DoT); guard singleton Instance during teardown; lazy-init anything a
  domain reload can wipe; clamp/validate SO data at use site with a loud
  Debug.LogWarning("[Class] ...").
- Every fix gets a reproduction note and a verification note (played, not just
  compiled) in the audit-log entry.
```

---

## 4. BEHAVIORAL RESTRICTIONS — What NOT To Do

Ordered by blast radius. Each rule was learned the hard way; violations have a documented incident.

### 4.1 Memory / hot loops
1. **No allocations in `Update`/`FixedUpdate`/`LateUpdate`**: no LINQ (zero `using System.Linq` in `Assets/Scripts` — keep it that way), no `new` collections, no string interpolation/concat, no `GetComponentsInChildren`, no boxing. Existing debt is catalogued in [§2.3](#23-known-per-frame-allocation-debt-verified-current) — reduce it, never grow it.
2. **No `Physics.RaycastAll` in per-frame code** — use `RaycastNonAlloc`/`SphereCastNonAlloc` with a `static` buffer (`CameraOcclusionFader` is the model).
3. **No `Camera.main`, `GameObject.Find*`, `FindAnyObjectByType` per frame** — only behind a null-cache self-heal (`if (_cam == null) _cam = Camera.main;`).
4. Hoist shader property IDs (`Shader.PropertyToID`) and animator hashes (`Animator.StringToHash`) to `static readonly int`.

### 4.2 Render pipeline / shaders
5. **Never create/edit `.shader` or `.shadergraph` files** — none are first-party; edits would fork third-party packages.
6. **Never switch the active RP asset to `Mobile_RPAsset`** — its renderer has zero features (kills water + SSAO silently). PC_RPAsset requires Depth+Opaque textures ON (water depends on them).
7. **Never edit `SampleSceneProfile.asset` casually** — it is simultaneously the pipeline-default volume profile and every scene's Global Volume profile. `HD2D_PostProcessing.asset` is an empty decoy; don't wire it up.
8. **Never write `RenderSettings` at runtime** (fog/sky/ambient belong to COZY). The only writer is an editor screenshot tool that deliberately never saves.
9. **Never author a directional light expecting it at runtime** — `CozySceneAdapter` disables all non-COZY suns on scene load. Scene suns are edit-mode-only. Don't fight the adapter.
10. **Don't put non-URP-Lit materials on `Building`-tagged objects** (breaks `OcclusionFade`'s transparent conversion), and don't rename Duomo geometry prefixes (`Oct_`, `Nave_Wall_`, `PierModel_<name>` …) — occlusion is name-coupled.
11. TorchFlicker cookies: Spot lights only (URP point lights reject 2D cookies).
42. **Never save in-memory materials into a PREFAB asset** — `new Material(...)` on a renderer serializes fine into a `.unity` scene but goes MAGENTA in any prefab instance (Street template incident 7/06). Prefab tints must be material ASSETS (`Assets/Prefabs/Templates/Materials/Street_<RRGGBB>.mat` pattern).
43. **Skyline backdrop quads: single-sided (Cull Back) + faces pointing INWARD** (N-quad yRot 0, S 180, E +90, W −90). DynamicZoom's pullback (offsets reach −20 behind the player) can put the camera BEHIND the south quad — double-sided art fills the whole screen (street/Signoria incident 7/06). Also: opaque URP-Unlit only — transparent mats write no depth and COZY's fog erases them in play mode.

### 4.3 Time / calendar / weather
12. **Backward `GameClock.SetHour` ⇒ must call `GameCalendar.ResyncClock()`** (else spurious `AdvanceDay` from the >6 h wrap detector). **Forward jump ⇒ never ResyncClock** (the wrap must roll the day). Multi-day = `AdvanceDay()×N` + `SetHour(morning)` + `ResyncClock()`.
13. **Never bypass `GameClock`** to touch `CozyWeather.instance` from gameplay code; never subscribe gameplay to COZY events — poll `GameClock`/`GameCalendar` (init-order-proof house pattern).
14. **Keep weather deterministic**: it is a pure function of `(year, dayOfYear, nodeId)`. No extra RNG in `FlorenceWeather`. Month lookups by English *name*, never index (stile fiorentino calendar starts 25 March).
15. COZY profiles resolve **by name via cached recursive `Resources.LoadAll`** — exact-path `Resources.Load` misses nested profiles.

### 4.4 State / persistence
16. **Never enable the `HubMap` component on GameSystems.prefab** (double-drives curse growth — `DailyCurseDrift` is the sole driver). Its `OnSurge`/`Cleanse`/`ActivateRitual` have no callers *by design* — pre-built interface, not dead code to delete.
17. **APPEND-ONLY enums**: `MicroClimate`, `NodeKind`, `MapLevel` (serialized as ints on the prefab).
18. **Save-order invariants** (ApplySave): date before hour before ResyncClock; job before HP/SP (clamped last). `SetDate` fires no events — a restore is not a day passing.
19. **No mid-battle saves** — the guard is scene-NAME based (`Battle`, `BattleArena`): renaming those scenes silently disables it.
20. **Runtime state never lives on shared ScriptableObjects** — clone per unit (the shared-HP CRITICAL from BUG_AUDIT). `new CharacterStats()` is NOT zero (field initializers are gameplay defaults); `job == null` is the NORMAL enemy case.
21. **Party save/restore is by roster index** — `PartyRoster` order is a contract. `AbsorbedSkillInstance.definition` is never swapped on refine (cast via `EffectiveDefinition`).
22. `FlorinWallet`/`EncounterRoll._resolved` are in-memory statics — wiped by domain reload, restored by SaveData; accepted, don't "fix".

### 4.5 Battle
23. **Never resolve charge actions mid-CT-tick** — only in the `BattleLoop` coroutine. `EndTurn` must not reset the `Charging` state ("silently killed every charged skill").
24. **Stop/Frozen must keep gaining CT** (durations tick at turn start; zero CT = permanent deadlock).
25. Re-check `IsAlive` after any status tick / `StartTurn` (DoT kills mid-tick). Iterate status lists on a copy.
26. **`EnemyAI.InitSharedState` is mandatory at battle start** or all AIs no-op forever.
27. Kills via direct `TakeDamage` (DoT) bypass `AwardPostKill` — known gotcha; if fixing rewards, fix there, not by double-awarding in resolvers.
28. Never `LoadScene` inside the death-callback chain (wait ~1 s realtime first).

### 4.6 UI
29. **`MenuManager` wires buttons by exact English label text** (`Btn_*`/`Lbl` hierarchy). Renaming a label silently unwires the button — labels are IDs.
30. **timeScale contract**: overlays save/restore their own timeScale; hand-offs `Resume()` first; restore timeScale BEFORE `LoadScene`; anything animating while paused uses unscaled time.
31. **Every self-building panel must `EnsureEventSystem()`** (script-built scenes ship without one — the "donation UI freeze"). Known holes: `FastTravelMenu`, `RestMenuUI` lack the guard.
32. Build pins/TMP only under an ACTIVE root (TMP NREs under inactive roots); force canvas layout (`Canvas.ForceUpdateCanvases`) before reading rects.
33. **Curse is hidden**: no numbers, gauges, or curse-derived ratings in UI. UI chrome English, names Italian.
34. `ESC` ownership: map owns ESC while open; `M` is shared — every hotkey handler early-returns while `GugolMapUI.IsOpen`.

### 4.7 Player / movement
35. **PlayerController Rigidbody contract**: `useGravity=false`, `FreezeRotation` only — **Y constraint must stay unfrozen** (terrain ramps). Height is position-controlled by `SnapToGround`; teleports must set `rb.position` (not just transform).
36. **Keep `MaxStepUp` (0.45 m) in `SnapToGround`** — without it any prop collider under the player becomes "floor" and hoists them (the fan-bed stuck bug).
37. `SpriteBillboard` tilts on X only (preserving Y can flip the sprite's back toward camera — documented BUG FIX).

### 4.8 Process / meta
38. **No 3D-generation spend without David's explicit image approval** (`ASSET_PIPELINE.md` THE RULE; concepts→approval→Prism 3.1 at 60cr — Tripo P1 at 100cr is banned).
39. **Never edit `Packages/com.distantlands.cozy.core`** or "clean up" the `Refrences/` typo folder (coupled to .gitignore) or blind-delete `FastTravelMenu` (MenuManager fallback).
40. Dynamic/RunCommand editor scripts: `System.Reflection` imports are blocked; `DistantLands.Cozy` asmdef is not visible — probe via project types (`GameClock.Describe()`).
41. After scripted scene saves, run duplication canaries (§3.3). Never do full-scene MCP saves on large scenes.

---

## 5. STANDARDIZED BUG-SQUASHING WORKFLOW

Protocol for an AI agent working the audit backlog. The **live** menu is `NIGHT_AUDIT_2026-07-04.md` (P1–P3 open); `BUG_AUDIT.md`/`NIGHT_AUDIT.md` are closed historical checklists. New audits append to the current file or create `NIGHT_AUDIT_<date>.md`.

### 5.1 Entry grammars (parse these exactly)

**Checklist format** (`BUG_AUDIT.md`, `NIGHT_AUDIT.md`):
```
- [x] **<File.cs:line–range>** — <symptom, code-quoted>. **Fix:** <prescription>. *(<optional date/deferral note>)*
- [~] ... (partial/deferred)      severity = the ## section (CRITICAL/HIGH/MEDIUM/LOW)
- [ ] ... (open)                  identity = file:line anchor (no numeric IDs)
```

**Tier format** (`NIGHT_AUDIT_2026-07-04.md`): `## P0 — Fix first` (numbered, `**bold summary.**` paragraphs) then `## P1/P2/P3` (bullets: `**bold lead** — detail`), plus non-bug sections (`Design questions`, `Positive findings`, `Suggested attack order`). No checkboxes — status is recorded by appending an italic trailer to the item and/or a dated note under the heading.

Parsed-entry schema:
```json
{
  "sourceDoc": "NIGHT_AUDIT_2026-07-04.md",
  "severity": "P0|P1|P2|P3|CRITICAL|HIGH|MEDIUM|LOW",
  "status": "open|partial|fixed",
  "anchorFile": "Assets/Scripts/Battle/BattleManager.cs",
  "anchorLine": "153 (HINT ONLY — re-locate by symbol)",
  "symptom": "...", "prescribedFix": "... (advisory, re-derive)",
  "trailerNotes": "italic *(...)* content if present"
}
```

### 5.2 The protocol (state machine)

```
PARSE → TRIAGE → VERIFY-CURRENT → REPRODUCE → FIX → VERIFY → LOG → NEXT
```

**1. PARSE** the target doc with the grammars above. Work top-down by severity; within a tier, follow any "Suggested attack order" section.

**2. TRIAGE** — skip and annotate (don't delete) entries that are: already `[x]`/trailer-marked fixed; anchored to deleted files (`git log --diff-filter=D -- <file>` to confirm); or explicitly deferred to a named future task.

**3. VERIFY-CURRENT** — audits are point-in-time. Re-locate the code by **symbol** (`Grep` the method/class), not line number. Confirm the defect still exists by reading the current implementation. If already fixed by a later commit, mark the entry with an italic trailer `*(already fixed by <commit>; verified <date>)*` and move on.

**4. REPRODUCE** — evidence before fixes:
   - *Pure logic* (formulas, queues, parsers): write a throwaway harness — an editor `RunCommand`-style script or a temporary `[MenuItem]` that drives the class directly and logs the wrong output.
   - *Gameplay*: enter play mode, drive the flow. Movement bugs: inject a driver MonoBehaviour calling `PlayerController.DebugSetMove` (editor-only hook; waypoint-following pattern exists in project history). **Programmatic play entry starts PAUSED at frame 0 — unpause first.** Capture telemetry into `EditorPrefs` (survives play-exit) rather than relying on console readers.
   - *Battle*: `BattleArena` direct play uses `BattleTestStarter`'s code-authored parties; road-encounter path needs `PendingEncounter` set (or travel through a waypoint). Remember: direct arena play has NO victory/defeat handler (EncounterBootstrap is the only subscriber).
   - Record the repro: input → observed → expected.

**5. FIX** — defensively, in-house style:
   - Smallest change that removes the failure mode; match the file's existing idiom (§3.1 conventions).
   - Standard defensive moves (precedents in repo): `?.` null-guards (MenuManager), unsubscribe in `OnDestroy` (TurnOrderSidebar fix), lazy re-resolution after domain reload (house singleton), `IsAlive` re-checks after tick chains (BattleManager), clamp-and-warn on bad SO data (`maxLevel < 1` precedent), guard-and-skip save migration.
   - If the prescribed **Fix:** clause in the audit is stale/wrong, do the right fix and say so in the log trailer.
   - No drive-by refactors: one entry = one concern.

**6. VERIFY** — compile clean (`AssetDatabase.Refresh` + type-load probe), then **re-run the reproduction from step 4 and observe the corrected behavior in play mode**. For scene-touching fixes, run the duplication canaries after saving. A fix that was only compiled is not done.

**7. LOG back to the markdown** — mutate the source document minimally:
   - Checklist docs: flip `- [ ]` → `- [x]` (or `- [~]` if partial) and append an italic trailer: `*(Fixed <YYYY-MM-DD> — <one-line what/why>; repro'd via <method>; playtested.)*`
   - Tier docs: append the same italic trailer to the item's line/paragraph. If the doc has a stale preamble ("Nothing has been fixed yet"), update it with a dated note.
   - NEW bugs discovered while working: append to the correct severity section of the **live** audit doc in that doc's grammar, status `- [ ]` / untrailed.
   - Commit message convention: `Fix <anchorFile> <short symptom> (audit <doc> <severity>)`.

Resolution-log schema (what the trailer must encode):
```json
{"fixedDate": "YYYY-MM-DD", "commit": "<sha if committed>", "whatChanged": "1 line",
 "reproMethod": "harness|playtest-driver|manual", "verified": "played the flow, observed corrected behavior"}
```

### 5.3 Per-severity playbooks

| Tier | Bar for shipping |
|---|---|
| P0 / CRITICAL | Repro mandatory. Fix + full playtest of the surrounding flow + check the same pattern elsewhere (`Grep` for sibling call-sites) — these are crash/corruption class. |
| P1 / HIGH | Repro mandatory; playtest the specific behavior. |
| P2 / MEDIUM | Repro when feasible; otherwise assert the guard with a harness + targeted play check. |
| P3 / LOW | Code-read + compile + spot-check is acceptable; note if not playtested. |

---

## 6. EDITOR TOOLING & ASSET PIPELINE

### 6.1 Menu-driven zone workflows (numbers = execution order)

| Zone | Steps |
|---|---|
| Giardino delle Rose | `1. Build Scene` (destructive full rebuild: terrain SetHeights/SetAlphamaps → vegetation → trees/flora (seed 1299) → hedge blockout+berms → MARKER_ drops → water → boundaries → player-copy → camera kit → save+BuildSettings) → *(step 2 = external asset-gen batch)* → `3. Place Hero Props` (consume markers) |
| Gugol Mappe | `1. Create TMP Fonts` → `2. Import Map Art` (magenta-key: corner-sampled key, chroma test `m=min(r,b)-g`) → `3. Setup GameSystems` (WireNodes = THE zone-registration idiom) → `4. Build Fiesole Scene` |
| Battle | Integration `1. Create Enemy Assets` → `2. Setup GameSystems` → `3. Add BattleArena To Build Settings` → `4. Populate Asset Registry` (re-run after adding Skill/Job assets!) → `Build Battle Arena` |
| Duomo | `Duomo Tile Pass` (world-aligned per-piece floor mats, binned wall mats, saves) · `Duomo Tile Shots (no save)` (inspection lighting, never saves) |
| **Content templates** | `Templates/1. Build Street Template Prefab` (destructive prefab rebuild) → `2. New Street Scene From Template` (fresh scene + player + clearance-zoom kit → `Assets/Scenes/NewStreet.unity`, rename after) · `3. Build Battle Map Prefabs` (Field + Ruins) → `4. Use Field Map In Arena` (swaps arena terrain). See §6.3. |

Marker contract: `MARKER_<assetId>@<height>` — assetId is the kebab-case Tools/asset-gen id (keep `@`-free), height `{h:0.##}` InvariantCulture. Placement: pivot under `[PlacedProps]`, GLB child `<assetId>_mesh`, AxisFix pre-rotation, bounds-scaled to height, footprint-max grounding − sink, BoxCollider unless in the `NoCollider` walk-through table (pergolas).

### 6.3 Content template kits (duplicate-and-dress — spec: `Docs/superpowers/specs/2026-07-06-content-templates.md`, built 7/06 `1b47350`)

**Street kit** (`Assets/Prefabs/Templates/Street_EW_Template.prefab`): Persona-5
E-W corridor — street along X −30..30, walkable Z ±5; tall shop-houses north in
swappable `SLOT_N1..5` anchors (replace the child GLB to re-dress), LOW botteghe
south (≤4.2 wu so the pitch-40 camera sees over — the P5 near-side treatment);
10 `ShopDoor` triggers (`Assets/Scripts/Map/ShopDoor.cs`, ZoneExit dwell
pattern: `targetScene` set = door travel, empty = logs "closed"); skyline caps;
end exits default ToWorldMap — retarget to door-link zones (Salone↔Signoria
pattern). New street = menu 2 → rename scene → swap slots → set door targets →
wire exits → Build Settings.

**Battle map kit**: the battle system is ALREADY FFT-isometric (BattleGrid
diamond projection, `GridCell.elevation` half-units + `walkable`). Map prefabs
live in `Assets/Prefabs/Battle/Maps/`; root = `BattleMapAuthoring` (size,
plateau rects+elevations, suggested spawns; stamps the scene BattleGrid on
Awake), props = `BattleObstacle` (cell, makeUnwalkable, addedElevation — 2+
blocks LoS via the existing Bresenham rule; ContextMenu → Snap To Cell). New
map = duplicate a Maps/ prefab, move/add obstacle sprites, snap, adjust
plateaus. **RULE: spawn columns (x 1-2 and 11-12) stay walkable** — callers
pass spawns to `StartBattle`; authored spawns await encounter tables.

### 6.2 Asset generation (Tools/asset-gen, port 3311)

`node server.mjs` → Gemini concept (`POST /api/assets`, `/api/assets/<id>/generate`) → **show David a ≤600px JPEG and STOP for approval** → `/approve` + `POST /api/generate-models` (check no stray approved assets ride along; 3 submits/min gate) → GLB lands in `output/models/<id>.glb` → copy to `Assets/Environment/<Zone>/Props/` → import via glTFast → wrapper/placement per §3.3. Engine is **Prism 3.1** (60 cr standard textured; API path says `tripo/.../3.1/` — legacy module name, don't be confused). Every response journaled in `output/_tripo/` (credits recoverable by task id).

---

## 7. DOCUMENT REGISTRY & Staleness Map

| Doc | Status | Notes |
|---|---|---|
| `NIGHT_AUDIT_2026-07-04.md` | **LIVE bug menu** | P0s fixed+committed 7/05 (`2efaa40`) — preamble stale; P1–P3 open. Tier grammar. |
| `BUG_AUDIT.md` | Closed (2026-06-29→30) | All `[x]`. References deleted `DayNightCycle.cs`/`WeatherSystem.cs`. Checklist grammar reference. |
| `NIGHT_AUDIT.md` | Closed (2026-06-30) | All `[x]`; some subjects later superseded (WorldMapUI legacy). |
| `WEATHER_SYSTEM.md` | **Current** | COZY architecture + gotchas. Read before touching time/weather. |
| `ASSET_PIPELINE.md` | **Current** | THE RULE (approval gate), costs, placement recipes. |
| `docs/HD2D_Camera.md` | **Current** | Camera rig is FOV 36 / pitch 40° / yaw 0° perspective — "do not revisit without reading this doc". New-zone checklist. |
| `docs/DuomoScene.md` | Current (scene reference) | Coordinate contract, name-prefix occlusion coupling, MCP working notes. |
| `docs/superpowers/specs/2026-07-05-giardino-delle-rose-design.md` | Design of record | "Nothing built yet" status line stale (v3 committed `6f67425` + polish pass). |
| `docs/superpowers/specs/2026-07-06-content-templates.md` | **Current** | Street kit + FFT battle-map kit: duplicate-and-dress workflows, coordinate contracts, spawn-column rule. Read before making streets or battle maps. |
| `docs/AI_HANDOVER_BLUEPRINT.md` | This file | Regenerate section facts when subsystems change; code wins over doc. |

**Update contract for agents:** when you fix a bug → log per §5.2-7. When you add a zone/system → extend §1.1 JSON + §2 map + relevant doc. When you learn a new constraint the hard way → add it to §4 with the incident, and to the owning system doc.
