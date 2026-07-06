using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// FFT battle-map kit per Docs/superpowers/specs/2026-07-06-content-templates.md.
// Builds map PREFABS (tile visuals + BattleMapAuthoring terrain data +
// BattleObstacle props) and swaps the arena's inline checkerboard for the
// Field map. New maps = duplicate a prefab, move/add obstacles, tweak
// plateaus — no code. Keep default spawn columns (x 1-2, 11-12) walkable.
public static class BattleMapTemplateBuilder
{
    const string MapsFolder = "Assets/Prefabs/Battle/Maps";
    const int W = 14, H = 12;

    [MenuItem("InfernosCurse/Templates/3. Build Battle Map Prefabs")]
    public static void BuildMapPrefabs()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Battle"))
            AssetDatabase.CreateFolder("Assets/Prefabs", "Battle");
        if (!AssetDatabase.IsValidFolder(MapsFolder))
            AssetDatabase.CreateFolder("Assets/Prefabs/Battle", "Maps");

        BuildField();
        BuildRuins();
        BuildPlains();
        Debug.Log("[BattleMapTemplateBuilder] Field + Ruins + Plains saved. " +
                  "Wire the arena via Templates menu 4 (Field) or 7 (Plains).");
    }

    // FFT-style OPEN PLAINS (David's reference 7/06): painted diamond grass
    // sprites, scattered boulders (unwalkable, elev+2 = LoS cover), one small
    // high-ground plateau. First map with real art instead of tinted squares.
    static void BuildPlains()
    {
        var grass = ImportSprite("battle-tile-grass");
        var rock = ImportSprite("battle-rock-boulder");
        if (grass == null || rock == null)
        {
            Debug.LogError("[BattleMapTemplateBuilder] Plains sprites missing in Maps/Sprites/.");
            return;
        }

        var root = NewMapRoot("BattleMap_Plains", out var map);
        map.plateaus.Clear();
        map.plateaus.Add(new BattleMapAuthoring.Plateau
        {
            cells = new RectInt(9, 7, 3, 3),   // NE high ground (reference's 'Height 2' knoll)
            elevation = 2,
        });

        var tiles = new GameObject("[MapTiles]").transform;
        tiles.SetParent(root.transform, false);
        for (int x = 0; x < map.width; x++)
            for (int y = 0; y < map.height; y++)
            {
                int elev = map.ElevationAt(new Vector2Int(x, y));
                var t = new GameObject($"tile_{x}_{y}");
                t.transform.SetParent(tiles, false);
                var sr = t.AddComponent<SpriteRenderer>();
                sr.sprite = grass;
                // Deterministic per-cell variety: tint jitter + mirroring
                int seed = x * 7 + y * 13;
                float v = 0.92f + 0.08f * ((seed * 31) % 7) / 6f;
                sr.color = new Color(v, v, v) * (elev > 0 ? 1.06f : 1f);
                sr.flipX = (seed % 3) == 0;
                sr.sortingOrder = -(x + y);   // painter's order: far tiles first
                t.transform.position = map.CellToWorld(new Vector2Int(x, y), elev);
                float scale = 1.0f / Mathf.Max(0.0001f, sr.sprite.bounds.size.x);
                t.transform.localScale = new Vector3(scale, scale, 1f);

                if (elev > 0)
                {
                    // Earth skirt under raised tiles — hides the gap the
                    // elevation offset opens beneath the plateau edge
                    var sq = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                    var s = new GameObject("skirt");
                    s.transform.SetParent(t.transform, false);
                    var ss = s.AddComponent<SpriteRenderer>();
                    ss.sprite = sq;
                    ss.color = new Color(0.30f, 0.22f, 0.14f);
                    ss.sortingOrder = sr.sortingOrder - 1;
                    // child local units = world / parent scale; oversized so
                    // neighboring skirts overlap and no diagonal notch leaks
                    s.transform.localPosition = new Vector3(0f, -0.6f / scale, 0f);
                    s.transform.localScale = new Vector3(0.15f / scale, 0.095f / scale, 1f);
                }
            }

        // Boulders roughly like the reference: cluster NW, spine down the E,
        // a couple mid-field. Spawn columns (x 1-2, 11-12) stay walkable —
        // x11-12 rocks sit on rows the default spawns don't use.
        foreach (var (cx, cy) in new (int, int)[]
        {
            (4, 10), (5, 10), (4, 9),          // NW cluster
            (9, 8), (10, 7),                   // knoll edge
            (6, 4), (8, 5),                    // mid-field cover
            (10, 2), (5, 1),                   // south scatter
        })
        {
            var g = new GameObject($"OBST_Boulder_{cx}_{cy}");
            g.transform.SetParent(root.transform, false);
            var sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = rock;
            sr.flipX = ((cx + cy) % 2) == 0;
            sr.sortingOrder = 8;
            var ob = g.AddComponent<BattleObstacle>();
            ob.cell = new Vector2Int(cx, cy);
            ob.makeUnwalkable = true;
            ob.addedElevation = 2;
            float scale = 0.62f / Mathf.Max(0.0001f, sr.sprite.bounds.size.x);
            g.transform.localScale = new Vector3(scale, scale, 1f);
            var basePos = map.CellToWorld(new Vector2Int(cx, cy), map.ElevationAt(new Vector2Int(cx, cy)));
            g.transform.position = basePos + new Vector3(0f, sr.sprite.bounds.size.y * scale * 0.32f, 0f);
        }

        SavePrefab(root, "BattleMap_Plains");
    }

    static Sprite ImportSprite(string id)
    {
        string path = $"{MapsFolder}/Sprites/{id}.png";
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null) return existing;
        AssetDatabase.ImportAsset(path);
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) return null;
        ti.textureType = TextureImporterType.Sprite;
        // Scripted type change leaves spriteImportMode = None → NO Sprite
        // sub-asset is generated (silent). Must set Single explicitly.
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.alphaIsTransparency = true;
        ti.mipmapEnabled = false;
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    [MenuItem("InfernosCurse/Templates/7. Use Plains Map In Arena")]
    public static void WireArenaPlains() => WireArenaTo("BattleMap_Plains");

    // Flat farmland field with scattered cover — the base for duplication.
    static void BuildField()
    {
        var root = NewMapRoot("BattleMap_Field", out var map);
        map.plateaus.Clear();
        BuildTiles(root.transform, map,
            new Color(0.36f, 0.31f, 0.23f), new Color(0.41f, 0.35f, 0.26f));

        AddObstacle(root.transform, map, "Crate", new Vector2Int(6, 5), new Color(0.55f, 0.40f, 0.22f), 0.05f);
        AddObstacle(root.transform, map, "Crate", new Vector2Int(7, 7), new Color(0.55f, 0.40f, 0.22f), 0.05f);
        AddObstacle(root.transform, map, "Rock", new Vector2Int(5, 8), new Color(0.45f, 0.44f, 0.42f), 0.06f);
        AddObstacle(root.transform, map, "Rock", new Vector2Int(9, 3), new Color(0.45f, 0.44f, 0.42f), 0.06f);
        AddObstacle(root.transform, map, "Stump", new Vector2Int(4, 3), new Color(0.38f, 0.28f, 0.16f), 0.04f);

        SavePrefab(root, "BattleMap_Field");
    }

    // Duplicate-and-dress proof: same bones + a high plateau + pillar cover.
    static void BuildRuins()
    {
        var root = NewMapRoot("BattleMap_Ruins", out var map);
        map.plateaus.Clear();
        map.plateaus.Add(new BattleMapAuthoring.Plateau
        {
            cells = new RectInt(8, 3, 4, 5),   // x 8..11, y 3..7 — one story up
            elevation = 2,
        });
        BuildTiles(root.transform, map,
            new Color(0.33f, 0.31f, 0.29f), new Color(0.38f, 0.36f, 0.33f));

        // Broken colonnade along the plateau's west lip (tall = LoS wall)
        AddObstacle(root.transform, map, "Pillar", new Vector2Int(8, 3), new Color(0.58f, 0.56f, 0.51f), 0.045f, 4, tall: true);
        AddObstacle(root.transform, map, "Pillar", new Vector2Int(8, 5), new Color(0.58f, 0.56f, 0.51f), 0.045f, 4, tall: true);
        AddObstacle(root.transform, map, "Pillar", new Vector2Int(8, 7), new Color(0.58f, 0.56f, 0.51f), 0.045f, 4, tall: true);
        AddObstacle(root.transform, map, "Rubble", new Vector2Int(5, 5), new Color(0.48f, 0.46f, 0.43f), 0.055f);
        AddObstacle(root.transform, map, "Rubble", new Vector2Int(6, 9), new Color(0.48f, 0.46f, 0.43f), 0.055f);
        AddObstacle(root.transform, map, "Rubble", new Vector2Int(10, 9), new Color(0.48f, 0.46f, 0.43f), 0.055f);

        SavePrefab(root, "BattleMap_Ruins");
    }

    [MenuItem("InfernosCurse/Templates/4. Use Field Map In Arena")]
    public static void WireArena() => WireArenaTo("BattleMap_Field");

    static void WireArenaTo(string mapName)
    {
        var mapPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{MapsFolder}/{mapName}.prefab");
        if (mapPrefab == null) { Debug.LogError("[BattleMapTemplateBuilder] Run menu 3 first."); return; }

        var scene = EditorSceneManager.OpenScene("Assets/Scenes/BattleArena.unity", OpenSceneMode.Single);
        var old = GameObject.Find("[GridVisuals]");
        if (old != null) Object.DestroyImmediate(old);
        var existing = Object.FindFirstObjectByType<BattleMapAuthoring>();
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        PrefabUtility.InstantiatePrefab(mapPrefab);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[BattleMapTemplateBuilder] BattleArena now uses {mapName}. " +
                  "Swap the instance for any Maps/ prefab to change terrain.");
    }

    // ── Shared construction ───────────────────────────────────────────────────

    static GameObject NewMapRoot(string name, out BattleMapAuthoring map)
    {
        var root = new GameObject(name);
        map = root.AddComponent<BattleMapAuthoring>();
        map.width = W;
        map.height = H;
        return root;
    }

    static void BuildTiles(Transform root, BattleMapAuthoring map, Color a, Color b)
    {
        var square = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        var tiles = new GameObject("[MapTiles]").transform;
        tiles.SetParent(root, false);
        for (int x = 0; x < map.width; x++)
            for (int y = 0; y < map.height; y++)
            {
                int elev = map.ElevationAt(new Vector2Int(x, y));
                var t = new GameObject($"tile_{x}_{y}");
                t.transform.SetParent(tiles, false);
                var sr = t.AddComponent<SpriteRenderer>();
                sr.sprite = square;
                var baseCol = ((x + y) % 2 == 0) ? a : b;
                // Higher ground reads lighter (FFT tier tinting)
                sr.color = baseCol + new Color(0.05f, 0.05f, 0.045f, 0f) * elev;
                sr.sortingOrder = 0;
                t.transform.position = map.CellToWorld(new Vector2Int(x, y), elev);
                t.transform.localScale = new Vector3(0.095f, 0.048f, 1f);

                if (elev > 0)
                {
                    // Dark skirt below raised tiles so plateaus read as mass
                    var s = new GameObject("skirt");
                    s.transform.SetParent(t.transform, false);
                    var ss = s.AddComponent<SpriteRenderer>();
                    ss.sprite = square;
                    ss.color = baseCol * 0.45f;
                    ss.sortingOrder = -1;
                    s.transform.localPosition = new Vector3(0f, -0.55f, 0f);
                    s.transform.localScale = new Vector3(1f, 1.2f, 1f);
                }
            }
    }

    static void AddObstacle(Transform root, BattleMapAuthoring map, string name, Vector2Int cell,
        Color color, float scale, int addedElevation = 2, bool tall = false)
    {
        var square = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        var g = new GameObject($"OBST_{name}_{cell.x}_{cell.y}");
        g.transform.SetParent(root, false);
        var sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = square;
        sr.color = color;
        sr.sortingOrder = 8;   // over tiles/highlights, under units (10)
        g.transform.localScale = tall ? new Vector3(scale * 0.7f, scale * 2.6f, 1f)
                                      : new Vector3(scale, scale * 0.9f, 1f);
        var ob = g.AddComponent<BattleObstacle>();
        ob.cell = cell;
        ob.makeUnwalkable = true;
        ob.addedElevation = addedElevation;
        // Seat the sprite so its base sits on the cell's iso point
        var basePos = map.CellToWorld(cell, map.ElevationAt(cell));
        g.transform.position = basePos + new Vector3(0f, sr.bounds.extents.y * 0.6f, 0f);
    }

    static void SavePrefab(GameObject root, string name)
    {
        PrefabUtility.SaveAsPrefabAsset(root, $"{MapsFolder}/{name}.prefab");
        Object.DestroyImmediate(root);
    }
}
