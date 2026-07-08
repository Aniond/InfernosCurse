using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// GIARDINO V4 — the compact WALLED GARDEN from Refrences/maps/"Giardino delle
// Rose.png" (David 7/08: old 80x70 zone was mostly dead space; garden is the
// focal point) AND the pilot for the zones=battlemaps architecture: the zone
// is authored ON the 1m grid with its SW corner at world (0,0,0), so grid
// cell (x,z) center = world (x+0.5, y, z+0.5) — exactly BattleGrid's 3D-XZ
// mapping. The builder emits BattleMapAuthoring + BattleTerrainHeights +
// invisible BattleObstacle cells from the same layout, so combat starts IN
// PLACE with no arena load ("just add a grid and combat is ready to go").
//
// Layout (reference legend): fountain full-cover center; 4 non-walkable rose
// quadrants; 3m cross paths + ring path; wall (X) + tree border (T) with N/S
// gates (G); bush rows (B) + benches (C) south; raised terrace (H) + stairs
// (S) inside the south gate. Props reuse the MARKER_<assetId>@<h> system —
// run "Giardino delle Rose/3. Place Hero Props" after this.
public static class GiardinoWalledGardenBuilder
{
    const string ScenePath = "Assets/Scenes/GiardinoDelleRose.unity";
    const string PlayerSourceScene = "Assets/Scenes/PonteVecchio.unity";
    const string CameraKitPath = "Assets/Prefabs/HD2D_CameraKit.prefab";
    const string LayerFolder = "Assets/Environment/GiardinoDelleRose/TerrainLayers";

    // Grid: 32x32 cells, 1 cell = 1m. World x/z 0..32. Fountain at (16,16).
    const int GRID = 32;
    const float GroundY = 1f;          // single ground level
    const float TerraceY = GroundY + 1.2f;   // south high-ground (H)
    const float TerrainHeight = 8f;

    static Terrain _terrain;

    [MenuItem("InfernosCurse/Giardino delle Rose/5. Rebuild WALLED GARDEN (v4, zones=battlemaps)")]
    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SceneManager.SetActiveScene(scene);

        _terrain = BuildTerrain();
        PaintTerrain(_terrain);
        BuildWallsAndTrees();
        BuildRoseQuads();
        BuildSouthDressing();
        BuildMarkers();
        BuildFountainWater();
        BuildBattleGridData();
        BuildBoundaries();
        BuildTravelMarkers();
        BuildFlorist();
        BuildLighting();
        BuildBackdrop();
        BuildZonePost();
        CopyPlayerFromPonteVecchio(scene);
        PlaceCameraKit();

        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log("[GiardinoWalledGarden] v4 walled garden built (32x32 grid @ origin). " +
                  "Run 'Giardino delle Rose/3. Place Hero Props' next.");
    }

    static float GroundAt(float x, float z)
    {
        // South terrace: x 12..20, z 2..7 raised, ramped stairs band z 7..9.
        bool inTerraceX = x >= 12f && x <= 20f;
        if (inTerraceX && z >= 2f && z <= 7f) return TerraceY;
        if (inTerraceX && z > 7f && z < 9f)
            return Mathf.Lerp(TerraceY, GroundY, (z - 7f) / 2f);
        return GroundY;
    }

    static float SampleY(float x, float z) =>
        _terrain.SampleHeight(new Vector3(x, 0f, z)) + _terrain.transform.position.y;

    static Terrain BuildTerrain()
    {
        var data = new TerrainData
        {
            heightmapResolution = 257,
            size = new Vector3(GRID + 8f, TerrainHeight, GRID + 8f),   // 4m buffer outside walls
        };
        data.SetDetailResolution(256, 16);
        int res = data.heightmapResolution;
        var heights = new float[res, res];
        for (int iz = 0; iz < res; iz++)
        {
            float wz = -4f + (GRID + 8f) * iz / (res - 1);
            for (int ix = 0; ix < res; ix++)
            {
                float wx = -4f + (GRID + 8f) * ix / (res - 1);
                heights[iz, ix] = GroundAt(wx, wz) / TerrainHeight;
            }
        }
        data.SetHeights(0, 0, heights);
        var go = Terrain.CreateTerrainGameObject(data);
        go.name = "Terrain_Giardino";
        go.transform.position = new Vector3(-4f, 0f, -4f);
        return go.GetComponent<Terrain>();
    }

    // Splat: grass base, gravel paths (cross + ring), dry grass under beds.
    static void PaintTerrain(Terrain terrain)
    {
        var grass = AssetDatabase.LoadAssetAtPath<Texture2D>($"{LayerFolder}/TL_Meadow_Diffuse.png");
        var gravel = AssetDatabase.LoadAssetAtPath<Texture2D>($"{LayerFolder}/TL_Gravel_Diffuse.png");
        var dry = AssetDatabase.LoadAssetAtPath<Texture2D>($"{LayerFolder}/TL_DryGrass_Diffuse.png");
        if (grass == null || gravel == null) { Debug.LogWarning("[GiardinoWalledGarden] terrain layers missing"); return; }

        TerrainLayer L(Texture2D t, float tile) => new TerrainLayer { diffuseTexture = t, tileSize = new Vector2(tile, tile) };
        var layers = new List<TerrainLayer> { L(grass, 4f), L(gravel, 3f) };
        if (dry != null) layers.Add(L(dry, 4f));
        for (int i = 0; i < layers.Count; i++)
        {
            AssetDatabase.CreateAsset(layers[i], $"{LayerFolder}/WG_Layer_{i}.terrainlayer");
        }
        terrain.terrainData.terrainLayers = layers.ToArray();

        int ares = terrain.terrainData.alphamapResolution;
        var maps = new float[ares, ares, layers.Count];
        for (int iz = 0; iz < ares; iz++)
            for (int ix = 0; ix < ares; ix++)
            {
                float wx = -4f + (GRID + 8f) * ix / (ares - 1f);
                float wz = -4f + (GRID + 8f) * iz / (ares - 1f);
                float path = PathMask(wx, wz);
                float bed = (layers.Count > 2 && InAnyBed(wx, wz)) ? 0.55f : 0f;
                float g = Mathf.Max(0f, 1f - path - bed);
                maps[iz, ix, 0] = g;
                maps[iz, ix, 1] = path;
                if (layers.Count > 2) maps[iz, ix, 2] = bed;
            }
        terrain.terrainData.SetAlphamaps(0, 0, maps);
    }

    // Paths: N-S at x 15..18 (3m, gate to gate), E-W at z 15..18, ring path
    // 2m wide just inside the tree border (x/z 4..6 and 26..28 bands).
    static float PathMask(float x, float z)
    {
        float m = 0f;
        float Band(float v, float a, float b) =>
            Mathf.Clamp01(Mathf.Min(v - (a - 0.7f), (b + 0.7f) - v) / 0.7f);
        if (x > 13.5f && x < 18.5f) m = Mathf.Max(m, Band(x, 14.2f, 17.8f));
        if (z > 13.5f && z < 18.5f) m = Mathf.Max(m, Band(z, 14.2f, 17.8f));
        // ring
        bool inGarden = x > 3.4f && x < 28.6f && z > 3.4f && z < 28.6f;
        if (inGarden)
        {
            float dEdge = Mathf.Min(Mathf.Min(x - 4f, 28f - x), Mathf.Min(z - 4f, 28f - z));
            if (dEdge > -0.6f && dEdge < 2f) m = Mathf.Max(m, Mathf.Clamp01(1f - Mathf.Abs(dEdge - 0.9f) / 1.2f));
        }
        return m;
    }

    // Rose quadrants (grid): x 7..13 & 19..25, z 19..25 (north pair, flanking
    // the N-S path) and z 7..13 south-west; SE quadrant shrinks for terrace.
    static readonly RectInt[] Beds =
    {
        new RectInt(7, 19, 6, 6),   // NW
        new RectInt(19, 19, 6, 6),  // NE
        new RectInt(7, 7, 6, 6),    // SW
        new RectInt(19, 10, 6, 4),  // SE (above the terrace)
    };
    static bool InAnyBed(float x, float z) =>
        Beds.Any(b => x >= b.xMin && x <= b.xMax && z >= b.yMin && z <= b.yMax);

    static void BuildWallsAndTrees()
    {
        var walls = new GameObject("[Walls]");
        var stone = new Color(0.62f, 0.58f, 0.52f);
        void Wall(Vector3 center, Vector3 size)
        {
            var w = GameObject.CreatePrimitive(PrimitiveType.Cube);
            w.name = "GardenWall";
            w.transform.SetParent(walls.transform, false);
            w.transform.position = center;
            w.transform.localScale = size;
            Tint(w, stone);
        }
        float wy = GroundY + 1.1f;
        // gates: gaps at x 14..18 on N (z=32) and S (z=0) walls
        Wall(new Vector3(7f, wy, 0f), new Vector3(14f, 2.2f, 0.8f));
        Wall(new Vector3(25f, wy, 0f), new Vector3(14f, 2.2f, 0.8f));
        Wall(new Vector3(7f, wy, 32f), new Vector3(14f, 2.2f, 0.8f));
        Wall(new Vector3(25f, wy, 32f), new Vector3(14f, 2.2f, 0.8f));
        Wall(new Vector3(0f, wy, 16f), new Vector3(0.8f, 2.2f, 32.8f));
        Wall(new Vector3(32f, wy, 16f), new Vector3(0.8f, 2.2f, 32.8f));

        // tree border band just inside the wall (cells 1-2), gaps at gates
        var trees = new GameObject("[Trees]");
        var rng = new System.Random(1299);
        var cypress = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/GiardinoDelleRose/Tree_TuscanCypress.prefab");
        var pine = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/GiardinoDelleRose/Tree_StonePine.prefab");
        _treeCells.Clear();
        void Tree(float x, float z, bool big)
        {
            var prefab = big ? pine : cypress;
            if (prefab == null) return;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetParent(trees.transform, false);
            go.transform.position = new Vector3(x, SampleY(x, z), z);
            go.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            _treeCells.Add(new Vector2Int(Mathf.FloorToInt(x), Mathf.FloorToInt(z)));
        }
        for (float t = 2.5f; t < 30f; t += 3f)
        {
            bool nearGateNS = t > 12.5f && t < 19.5f;
            if (!nearGateNS) { Tree(t, 1.8f, ((int)t % 2) == 0); Tree(t, 30.2f, ((int)t % 2) == 1); }
            Tree(1.8f, t, ((int)t % 2) == 0);
            Tree(30.2f, t, ((int)t % 2) == 1);
        }
    }

    static readonly List<Vector2Int> _treeCells = new();

    static void BuildRoseQuads()
    {
        // low bed border (walkability blocker) + rose markers in rows
        var group = new GameObject("[RoseBeds]");
        string[] variety = { "rose-bush-ivory", "rose-bush-crimson", "rose-bush-gold", "rose-climbing-wine" };
        for (int b = 0; b < Beds.Length; b++)
        {
            var bed = Beds[b];
            var border = GameObject.CreatePrimitive(PrimitiveType.Cube);
            border.name = $"BedBorder_{b}";
            border.transform.SetParent(group.transform, false);
            border.transform.position = new Vector3(bed.center.x, GroundY + 0.12f, bed.center.y);
            border.transform.localScale = new Vector3(bed.width, 0.24f, bed.height);
            Tint(border, new Color(0.5f, 0.42f, 0.34f));
        }
    }

    static void BuildSouthDressing()
    {
        // bush rows (concealment) flanking the S path at z 4..6, benches on ring
        var group = new GameObject("[Hedges]");
        _bushCells.Clear();
        void Bush(float x, float z, bool alongZ = false)
        {
            var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            seg.name = "MARKER_boxwood-hedge-segment@1.0";
            seg.transform.SetParent(group.transform, false);
            seg.transform.position = new Vector3(x, SampleY(x, z) + 0.45f, z);
            seg.transform.localScale = new Vector3(1.8f, 0.9f, 0.8f);
            if (alongZ) seg.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            Tint(seg, new Color(0.24f, 0.38f, 0.18f));
            _bushCells.Add(new Vector2Int(Mathf.FloorToInt(x), Mathf.FloorToInt(z)));
        }
        foreach (float x in new[] { 7f, 9f, 11f, 21f, 23f, 25f })
            Bush(x, 5f);

        // Border hedge ring: clipped box hedges along the inside of the tree
        // border (reference's B band), gaps at the gates and cross-path mouths.
        bool GateGap(float v) => v > 13.2f && v < 18.8f;
        for (float t = 4f; t <= 28f; t += 2f)
        {
            if (!GateGap(t)) { Bush(t, 3.2f); Bush(t, 28.8f); }               // S + N rows
            if (!GateGap(t)) { Bush(3.2f, t, true); Bush(28.8f, t, true); }   // W + E rows
        }
    }

    static readonly List<Vector2Int> _bushCells = new();

    static void BuildMarkers()
    {
        var group = new GameObject("[PropMarkers]");
        void M(string assetId, float h, Vector3 pos, float yRot = 0f)
        {
            var m = new GameObject($"MARKER_{assetId}@{h:0.##}");
            m.transform.SetParent(group.transform, false);
            m.transform.position = pos;
            m.transform.rotation = Quaternion.Euler(0f, yRot, 0f);
        }

        M("garden-fountain-basin", 2.2f, new Vector3(16f, GroundY, 16f));
        M("garden-stone-bench", 0.9f, new Vector3(11f, GroundY, 16.8f), 90f);
        M("garden-stone-bench", 0.9f, new Vector3(21f, GroundY, 15.2f), -90f);
        M("shrine-tabernacolo", 2.2f, new Vector3(5f, GroundY, 27f), 135f);

        var rng = new System.Random(1299);
        string[] variety = { "rose-bush-ivory", "rose-bush-crimson", "rose-bush-gold", "rose-climbing-wine" };
        for (int b = 0; b < Beds.Length; b++)
        {
            var bed = Beds[b];
            for (int row = 0; row < bed.height / 2; row++)
                for (int col = 0; col < bed.width / 2; col++)
                {
                    float x = bed.xMin + 1f + col * 2f;
                    float z = bed.yMin + 1f + row * 2f;
                    M(variety[b], 0.82f + (float)rng.NextDouble() * 0.16f,
                      new Vector3(x, GroundY, z), (float)rng.NextDouble() * 28f - 14f);
                }
        }

        // NE corner: gardener's buildings inside the wall (reference rows 19-21)
        M("gardeners-cottage", 3.2f, new Vector3(26.5f, GroundY, 26.5f), -135f);
        M("florist-market-stall", 2.0f, new Vector3(23f, GroundY, 27.5f), 180f);
        M("stone-wellhead", 1.4f, new Vector3(27.5f, GroundY, 22.5f));
        // terrace overlook (H) — worn marble + bench
        M("ancient-marble-figure", 1.8f, new Vector3(18.5f, TerraceY, 3.5f), 20f);
        M("garden-stone-bench", 0.9f, new Vector3(14f, TerraceY, 4f), 0f);
        // pergola over the south gate approach
        M("garden-wooden-pergola", 2.6f, new Vector3(16f, GroundY, 2.2f));
        M("rose-climbing-wine", 2.2f, new Vector3(17.4f, GroundY, 2.4f));
    }

    static void BuildFountainWater()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Stylized Water 3/Materials/StylizedWater3_Clear.mat");
        if (mat == null) return;
        var mesh = StylizedWater3.WaterMesh.Create(StylizedWater3.WaterMesh.Shape.Disk, 2.9f, 0.2f);
        var wo = StylizedWater3.WaterObject.New(mat, mesh);
        wo.gameObject.name = "FountainWater";
        wo.transform.position = new Vector3(16f, SampleY(16f, 16f) + 1.43f, 16f);
    }

    // ── zones=battlemaps: emit the combat grid over the SAME layout ──────────
    static void BuildBattleGridData()
    {
        var root = new GameObject("[BattleGridData]");
        var auth = root.AddComponent<BattleMapAuthoring>();
        auth.width = GRID;
        auth.height = GRID;
        auth.tileWidth = 1f;
        auth.applyOnAwake = false;   // zone explore mode by default; encounter flow applies it
        auth.plateaus = new List<BattleMapAuthoring.Plateau>
        {
            // south terrace = high ground (elevation 2 = one story, ranged bonus)
            new BattleMapAuthoring.Plateau { cells = new RectInt(12, 2, 8, 5), elevation = 2 },
        };

        var heights = root.AddComponent<BattleTerrainHeights>();
        heights.width = GRID;
        heights.height = GRID;
        heights.res = 2;
        heights.cellY = new float[GRID * GRID];
        heights.surfY = new float[(GRID * 2 + 1) * (GRID * 2 + 1)];
        for (int z = 0; z < GRID; z++)
            for (int x = 0; x < GRID; x++)
                heights.cellY[x + z * GRID] = SampleY(x + 0.5f, z + 0.5f);
        for (int iz = 0; iz <= GRID * 2; iz++)
            for (int ix = 0; ix <= GRID * 2; ix++)
                heights.surfY[ix + iz * (GRID * 2 + 1)] = SampleY(ix / 2f, iz / 2f);
        root.AddComponent<BattleTerrainCurse>();

        // obstacle cells from the layout (invisible; visuals are the props)
        void Ob(int x, int z, string tag, bool unwalkable, int elev)
        {
            var g = new GameObject($"CELL_{tag}_{x}_{z}");
            g.transform.SetParent(root.transform, false);
            var o = g.AddComponent<BattleObstacle>();
            o.cell = new Vector2Int(x, z);
            o.makeUnwalkable = unwalkable;
            o.addedElevation = elev;
            g.transform.position = new Vector3(x + 0.5f, SampleY(x + 0.5f, z + 0.5f), z + 0.5f);
        }
        // wall ring
        for (int i = 0; i < GRID; i++)
        {
            bool gate = i >= 14 && i <= 17;
            if (!gate) { Ob(i, 0, "wall", true, 4); Ob(i, GRID - 1, "wall", true, 4); }
            Ob(0, i, "wall", true, 4);
            Ob(GRID - 1, i, "wall", true, 4);
        }
        foreach (var c in _treeCells.Distinct()) Ob(c.x, c.y, "tree", true, 4);
        foreach (var b in Beds)
            for (int z = b.yMin; z < b.yMax; z++)
                for (int x = b.xMin; x < b.xMax; x++)
                    Ob(x, z, "rosebed", true, 0);   // v1: unwalkable; slow-move later
        // fountain full cover 2x2
        Ob(15, 15, "fountain", true, 4); Ob(16, 15, "fountain", true, 4);
        Ob(15, 16, "fountain", true, 4); Ob(16, 16, "fountain", true, 4);
        foreach (var c in _bushCells.Distinct()) Ob(c.x, c.y, "bush", false, 0); // concealment later

        auth.partySpawns = new List<Vector2Int> { new(15, 4), new(16, 4), new(14, 5), new(17, 5) };
        auth.enemySpawns = new List<Vector2Int> { new(15, 27), new(16, 27), new(14, 26), new(17, 26) };
    }

    static void BuildBoundaries()
    {
        var group = new GameObject("[Boundaries]");
        foreach (var (name, pos, size) in new[]
        {
            ("Bound_S", new Vector3(16f, 3f, -3f), new Vector3(44f, 6f, 1f)),
            ("Bound_N", new Vector3(16f, 3f, 35f), new Vector3(44f, 6f, 1f)),
            ("Bound_E", new Vector3(35f, 3f, 16f), new Vector3(1f, 6f, 44f)),
            ("Bound_W", new Vector3(-3f, 3f, 16f), new Vector3(1f, 6f, 44f)),
        })
        {
            var go = new GameObject(name);
            go.transform.SetParent(group.transform, false);
            go.transform.position = pos;
            go.AddComponent<BoxCollider>().size = size;
        }
    }

    static void BuildTravelMarkers()
    {
        var placer = new GameObject("[ZoneEntryPlacer]");
        placer.AddComponent<ZoneEntryPlacer>();
        MakeEntry("giardino_gate", "Garden Gate", new Vector3(16f, GroundY, 1.5f), new Vector2(0f, 1f));
        MakeEntry("giardino_fountain", "The Fountain", new Vector3(16f, GroundY, 13.5f), new Vector2(0f, 1f));

        var exit = new GameObject("ExitZone_Gate");
        exit.transform.position = new Vector3(16f, GroundY + 1f, -1.5f);
        exit.AddComponent<BoxCollider>().size = new Vector3(6f, 3f, 2f);
        exit.AddComponent<ZoneExit>().mode = ZoneExit.ExitMode.ToWorldMap;
    }

    static void MakeEntry(string id, string label, Vector3 pos, Vector2 face)
    {
        var go = new GameObject("ENTRY_" + id);
        go.transform.position = pos;
        var ep = go.AddComponent<ZoneEntryPoint>();
        ep.entryId = id;
        ep.displayName = label;
        ep.faceDirection = face;
        ep.fastTravelDestination = true;
    }

    static void BuildFlorist()
    {
        var npc = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        npc.name = "NPC_Florist_PLACEHOLDER";
        npc.transform.position = new Vector3(23.5f, GroundY + 1f, 26.5f);
        npc.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
        Tint(npc, new Color(0.55f, 0.32f, 0.55f));
        npc.AddComponent<FloristUnlockGate>();
    }

    static void BuildLighting()
    {
        var sun = new GameObject("Directional Light");
        var light = sun.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.96f, 0.86f);
        light.intensity = 1.2f;
        light.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
    }

    static void BuildBackdrop()
    {
        var backdrop = GameObject.CreatePrimitive(PrimitiveType.Quad);
        backdrop.name = "Backdrop_City_South";
        Object.DestroyImmediate(backdrop.GetComponent<Collider>());
        backdrop.transform.position = new Vector3(16f, 5f, -12f);
        backdrop.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        backdrop.transform.localScale = new Vector3(70f, 18f, 1f);
        Tint(backdrop, new Color(0.72f, 0.68f, 0.60f));
    }

    static void BuildZonePost()
    {
        var profile = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>("Assets/Settings/HD2D_ZonePost.asset");
        if (profile == null) return;
        var volGo = new GameObject("ZoneVolume");
        var vol = volGo.AddComponent<UnityEngine.Rendering.Volume>();
        vol.isGlobal = true;
        vol.sharedProfile = profile;
    }

    static void CopyPlayerFromPonteVecchio(Scene target)
    {
        var pv = EditorSceneManager.OpenScene(PlayerSourceScene, OpenSceneMode.Additive);
        GameObject source = null;
        foreach (var root in pv.GetRootGameObjects())
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.CompareTag("Player")) { source = t.gameObject; break; }
            if (source != null) break;
        }
        if (source != null)
        {
            var copy = Object.Instantiate(source);
            copy.name = source.name;
            SceneManager.MoveGameObjectToScene(copy, target);
            copy.transform.position = new Vector3(16f, GroundY + source.transform.position.y, 3f);
            var cam = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)
                .FirstOrDefault(c => c.CompareTag("MainCamera"));
        }
        else Debug.LogError("[GiardinoWalledGarden] No Player-tagged object found in PonteVecchio!");
        EditorSceneManager.CloseScene(pv, true);
    }

    static void PlaceCameraKit()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CameraKitPath);
        if (prefab == null) { Debug.LogError($"[GiardinoWalledGarden] Missing {CameraKitPath}"); return; }
        PrefabUtility.InstantiatePrefab(prefab);
    }

    static void Tint(GameObject go, Color color)
    {
        var renderer = go.GetComponent<Renderer>();
        if (renderer == null) return;
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        renderer.sharedMaterial = new Material(shader) { color = color };
    }
}
