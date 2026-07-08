using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 3D diorama battle map (FFT-remaster look, David's reference 7/07):
// ONE continuous heightfield mesh generated from the SAME BattleMapAuthoring
// data the sprite maps use. Logic layer untouched — mesh is a pure function
// of it. Flat walkable tile crowns, rounded lips on 1-step edges, beveled
// near-vertical cliffs on 2+ drops, a rolling hill skirt past the playfield,
// and a dark soil diorama cut (walls + bottom) like the remaster's base.
// Grid contract: logical cell (x, z) center = world (x + 0.5, h, z + 0.5).
public static class BattleMapMeshBuilder
{
    const string MapsFolder = "Assets/Prefabs/Battle/Maps";

    const int SKIRT = 4;          // hill-falloff cells beyond the playfield
    const int RES = 4;            // mesh quads per logical cell
    const float HSTEP = 0.5f;     // world Y per elevation unit (FFT-chunky)
    const float BASE_Y = -3.2f;   // diorama cut depth (tall dark cake base)
    const float LIP_SOFT = 0.30f; // rounded shoulder on 1-step edges
    const float LIP_CLIFF = 0.10f;// tight bevel on 2+ step cliffs

    [MenuItem("InfernosCurse/Templates/8. Build 3D Plains Diorama (Prototype)")]
    public static void Build()
    {
        var srcPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{MapsFolder}/BattleMap_Plains.prefab");
        if (srcPrefab == null || srcPrefab.GetComponent<BattleMapAuthoring>() == null)
        {
            Debug.LogError("[BattleMapMeshBuilder] BattleMap_Plains.prefab with BattleMapAuthoring not found. Run Templates menu 3 first.");
            return;
        }
        var srcAuth = srcPrefab.GetComponent<BattleMapAuthoring>();
        int W = srcAuth.width, H = srcAuth.height;

        // ── logical + skirt heights (in elevation units) ─────────────────
        int gw = W + 2 * SKIRT, gh = H + 2 * SKIRT;
        float[,] cellH = new float[gw, gh];
        for (int x = -SKIRT; x < W + SKIRT; x++)
            for (int z = -SKIRT; z < H + SKIRT; z++)
            {
                float h;
                bool inField = x >= 0 && x < W && z >= 0 && z < H;
                if (inField)
                {
                    h = srcAuth.ElevationAt(new Vector2Int(x, z));
                    // visual-only micro-relief: the meadow undulates like the
                    // remaster's, far below gameplay-readable height (±0.15 elev)
                    h += (Mathf.PerlinNoise(x * 0.9f + 3.7f, z * 0.9f + 9.2f) - 0.5f) * 0.3f;
                }
                else
                {
                    int dx = Mathf.Max(0, Mathf.Max(-x, x - (W - 1)));
                    int dz = Mathf.Max(0, Mathf.Max(-z, z - (H - 1)));
                    float r = Mathf.Max(dx, dz);
                    h = -(r * r) * 0.55f;   // hill shoulder rolls away
                    h += (Mathf.PerlinNoise(x * 0.33f + 7.31f, z * 0.33f + 2.17f) - 0.5f)
                         * 2.0f * Mathf.Min(r, 3f);   // organic wobble, grows with distance
                    h = Mathf.Max(h, -3f);  // gentle crown roll; the CUT provides the drama
                }
                cellH[x + SKIRT, z + SKIRT] = h;
            }

        float CellAt(int cx, int cz)
        {
            cx = Mathf.Clamp(cx + SKIRT, 0, gw - 1);
            cz = Mathf.Clamp(cz + SKIRT, 0, gh - 1);
            return cellH[cx, cz];
        }

        float LipStep(float f, float lip)
        {
            float t = Mathf.Clamp01((f - (0.5f - lip)) / (2f * lip));
            return t * t * (3f - 2f * t);
        }

        // Continuous height in world XZ (cells are 1x1, centers at int + 0.5).
        float Sample(float gx, float gz)
        {
            float sx = gx - 0.5f, sz = gz - 0.5f;
            int x0 = Mathf.FloorToInt(sx), z0 = Mathf.FloorToInt(sz);
            float fx = sx - x0, fz = sz - z0;
            float h00 = CellAt(x0, z0), h10 = CellAt(x0 + 1, z0);
            float h01 = CellAt(x0, z0 + 1), h11 = CellAt(x0 + 1, z0 + 1);
            float maxD = Mathf.Max(
                Mathf.Max(Mathf.Abs(h10 - h00), Mathf.Abs(h01 - h00)),
                Mathf.Max(Mathf.Abs(h11 - h10), Mathf.Abs(h11 - h01)));
            // Playfield keeps crisp tile crowns; the hill skirt rolls fully
            // smooth (lip 0.5 = plain bilinear, no terracing).
            bool inField = x0 >= 0 && x0 + 1 < W && z0 >= 0 && z0 + 1 < H;
            float lip = !inField ? 0.5f : (maxD >= 2f ? LIP_CLIFF : LIP_SOFT);
            float wx = LipStep(fx, lip), wz = LipStep(fz, lip);
            return Mathf.Lerp(Mathf.Lerp(h00, h10, wx), Mathf.Lerp(h01, h11, wx), wz) * HSTEP;
        }

        Vector3 NormalAt(float gx, float gz)
        {
            const float e = 0.12f;
            float hl = Sample(gx - e, gz), hr = Sample(gx + e, gz);
            float hd = Sample(gx, gz - e), hu = Sample(gx, gz + e);
            return new Vector3(hl - hr, 2f * e, hd - hu).normalized;
        }

        // ── heightfield surface ──────────────────────────────────────────
        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var cols = new List<Color>();
        var tris = new List<int>();

        int nx = gw * RES + 1, nz = gh * RES + 1;
        float x0w = -SKIRT, z0w = -SKIRT;
        for (int iz = 0; iz < nz; iz++)
            for (int ix = 0; ix < nx; ix++)
            {
                float gx = x0w + (float)ix / RES;
                float gz = z0w + (float)iz / RES;
                float y = Sample(gx, gz);
                var n = NormalAt(gx, gz);
                verts.Add(new Vector3(gx, y, gz));
                norms.Add(n);

                // splat weights: steep = rock, moderate = dirt, flat = grass,
                // plus noisy dirt patches on flats (worn ground)
                // Gentle banks stay GRASS (the ref's knolls are green-sided);
                // dirt lives on CREST LINES (convex lips — negative laplacian),
                // like the remaster's worn plateau edges; rock only near cliffs.
                float slope = 1f - n.y;
                const float le = 0.22f;
                float lap = Sample(gx + le, gz) + Sample(gx - le, gz)
                          + Sample(gx, gz + le) + Sample(gx, gz - le) - 4f * Sample(gx, gz);
                float crest = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((-lap - 0.015f) / 0.06f));
                float rockW = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((slope - 0.42f) / 0.20f));
                float dirtW = Mathf.Max(
                    Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((slope - 0.30f) / 0.18f)),
                    crest * 0.85f) * (1f - rockW);
                float patch = Mathf.PerlinNoise(gx * 0.55f + 31.7f, gz * 0.55f + 11.9f);
                dirtW = Mathf.Max(dirtW, Mathf.SmoothStep(0.78f, 0.9f, patch) * 0.3f * (1f - rockW));
                float grassW = Mathf.Max(0f, 1f - rockW - dirtW);
                // banks shade darker than crowns — sells the relief at FFT angles
                float shade = 1f - Mathf.Clamp01(slope * 1.5f) * 0.32f;
                cols.Add(new Color(grassW, dirtW, rockW, shade));
            }
        for (int iz = 0; iz < nz - 1; iz++)
            for (int ix = 0; ix < nx - 1; ix++)
            {
                int a = iz * nx + ix, b = a + 1, c = a + nx, d = c + 1;
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
            }

        // ── diorama cut: perimeter walls + bottom cap, dark soil ─────────
        void Wall(Vector3 top0, Vector3 top1, Vector3 outward)
        {
            int s = verts.Count;
            var b0 = new Vector3(top0.x, BASE_Y, top0.z);
            var b1 = new Vector3(top1.x, BASE_Y, top1.z);
            verts.Add(top0); verts.Add(top1); verts.Add(b0); verts.Add(b1);
            for (int k = 0; k < 4; k++) { norms.Add(outward); cols.Add(new Color(0f, 0.2f, 0.8f, 0.30f)); }
            tris.Add(s); tris.Add(s + 2); tris.Add(s + 1);
            tris.Add(s + 1); tris.Add(s + 2); tris.Add(s + 3);
        }
        float xMin = x0w, xMax = x0w + gw, zMin = z0w, zMax = z0w + gh;
        for (int i = 0; i < gw * RES; i++)
        {
            float a = x0w + (float)i / RES, b = x0w + (float)(i + 1) / RES;
            Wall(new Vector3(b, Sample(b, zMin), zMin), new Vector3(a, Sample(a, zMin), zMin), Vector3.back);
            Wall(new Vector3(a, Sample(a, zMax), zMax), new Vector3(b, Sample(b, zMax), zMax), Vector3.forward);
        }
        for (int i = 0; i < gh * RES; i++)
        {
            float a = z0w + (float)i / RES, b = z0w + (float)(i + 1) / RES;
            Wall(new Vector3(xMin, Sample(xMin, a), a), new Vector3(xMin, Sample(xMin, b), b), Vector3.left);
            Wall(new Vector3(xMax, Sample(xMax, b), b), new Vector3(xMax, Sample(xMax, a), a), Vector3.right);
        }
        {
            int s = verts.Count;
            verts.Add(new Vector3(xMin, BASE_Y, zMin)); verts.Add(new Vector3(xMax, BASE_Y, zMin));
            verts.Add(new Vector3(xMin, BASE_Y, zMax)); verts.Add(new Vector3(xMax, BASE_Y, zMax));
            for (int k = 0; k < 4; k++) { norms.Add(Vector3.down); cols.Add(new Color(0f, 0.3f, 0.7f, 0.3f)); }
            tris.Add(s); tris.Add(s + 1); tris.Add(s + 2);
            tris.Add(s + 1); tris.Add(s + 3); tris.Add(s + 2);
        }

        // ── persist mesh (same GUID across rebuilds) ─────────────────────
        if (!AssetDatabase.IsValidFolder($"{MapsFolder}/Meshes"))
            AssetDatabase.CreateFolder(MapsFolder, "Meshes");
        string meshPath = $"{MapsFolder}/Meshes/PlainsDiorama.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        bool newMesh = mesh == null;
        if (newMesh) mesh = new Mesh();
        mesh.Clear();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.name = "PlainsDiorama";
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetColors(cols);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        if (newMesh) AssetDatabase.CreateAsset(mesh, meshPath);
        else EditorUtility.SetDirty(mesh);

        // ── material ─────────────────────────────────────────────────────
        if (!AssetDatabase.IsValidFolder($"{MapsFolder}/Materials"))
            AssetDatabase.CreateFolder(MapsFolder, "Materials");
        string matPath = $"{MapsFolder}/Materials/BattleTerrainSplat.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        var shader = Shader.Find("InfernosCurse/BattleTerrainSplat");
        if (shader == null) { Debug.LogError("[BattleMapMeshBuilder] BattleTerrainSplat shader missing/failed to compile."); return; }
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, matPath);
        }
        mat.shader = shader;
        mat.SetTexture("_GrassTex", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Environment/GiardinoDelleRose/TerrainLayers/TL_Meadow_Diffuse.png"));
        mat.SetTexture("_DirtTex", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Environment/PiazzaSignoria/Textures/signoria-ground-dirt.png"));
        mat.SetTexture("_RockTex", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Environment/PiazzaSignoria/Textures/signoria-wall-rusticated.png"));
        mat.SetFloat("_Tiling", 0.35f);
        mat.SetColor("_GrassTint", new Color(0.48f, 0.84f, 0.38f));   // pull meadow toward FFT green
        mat.SetColor("_DirtTint", new Color(0.62f, 0.50f, 0.40f));    // earthy brown, not rust
        mat.SetColor("_RockTint", new Color(0.62f, 0.61f, 0.58f));    // neutral stone
        EditorUtility.SetDirty(mat);

        // ── prefab: terrain + copied authoring data ──────────────────────
        var root = new GameObject("BattleMap_Plains3D");
        var auth = root.AddComponent<BattleMapAuthoring>();
        auth.width = srcAuth.width;
        auth.height = srcAuth.height;
        auth.tileWidth = srcAuth.tileWidth;
        auth.tileHeight = srcAuth.tileHeight;
        auth.plateaus = new List<BattleMapAuthoring.Plateau>();
        foreach (var p in srcAuth.plateaus)
            auth.plateaus.Add(new BattleMapAuthoring.Plateau { cells = p.cells, elevation = p.elevation });

        var terrain = new GameObject("Terrain");
        terrain.transform.SetParent(root.transform, false);
        terrain.AddComponent<MeshFilter>().sharedMesh = mesh;
        terrain.AddComponent<MeshRenderer>().sharedMaterial = mat;

        // Baked cell-center heights — this component is also the flag that
        // switches BattleGrid.GridToWorld into 3D-XZ mode for this map.
        var heights = root.AddComponent<BattleTerrainHeights>();
        heights.width = W;
        heights.height = H;
        heights.cellY = new float[W * H];
        for (int x = 0; x < W; x++)
            for (int z = 0; z < H; z++)
                heights.cellY[x + z * W] = Sample(x + 0.5f, z + 0.5f);

        PrefabUtility.SaveAsPrefabAsset(root, $"{MapsFolder}/BattleMap_Plains3D.prefab");
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Debug.Log($"[BattleMapMeshBuilder] BattleMap_Plains3D saved: {verts.Count} verts, {tris.Count / 3} tris. Logic grid untouched ({W}x{H}).");
    }

    // ── Arena wiring: swap BattleArena to the 3D diorama ─────────────────────

    [MenuItem("InfernosCurse/Templates/9. Use 3D Plains In Arena")]
    public static void WireArena3D()
    {
        var mapPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{MapsFolder}/BattleMap_Plains3D.prefab");
        if (mapPrefab == null) { Debug.LogError("[BattleMapMeshBuilder] Run menu 8 first."); return; }

        var unitPrefab = EnsureUnit3DPrefab();
        var tilePrefab = EnsureFlatHighlight("Highlight_Tile", "Highlight_Tile3D");
        var hoverPrefab = EnsureFlatHighlight("Highlight_Hover", "Highlight_Hover3D");
        if (unitPrefab == null || tilePrefab == null) return;

        var scene = EditorSceneManager.OpenScene("Assets/Scenes/BattleArena.unity", OpenSceneMode.Single);

        var oldVis = GameObject.Find("[GridVisuals]");
        if (oldVis != null) Object.DestroyImmediate(oldVis);
        var oldMap = Object.FindFirstObjectByType<BattleMapAuthoring>();
        if (oldMap != null) Object.DestroyImmediate(oldMap.gameObject);

        PrefabUtility.InstantiatePrefab(mapPrefab);

        // Camera: perspective FFT rig (narrow FOV at distance ≈ near-ortho read)
        var cam = Camera.main;
        Vector3 center = new Vector3(7f, 0.5f, 6f);
        cam.orthographic = false;
        cam.fieldOfView = 22f;
        cam.nearClipPlane = 0.5f;
        cam.farClipPlane = 300f;
        cam.transform.position = center + new Vector3(0.55f, 0.6f, -1f).normalized * 24f;
        cam.transform.LookAt(center);

        // Authored sun (edit-mode only — COZY stays blocked in the arena)
        var sun = Object.FindObjectsByType<Light>(FindObjectsSortMode.None)
            .FirstOrDefault(l => l.type == LightType.Directional);
        if (sun == null) sun = new GameObject("Sun_Battle (authored)").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.98f, 0.92f);
        sun.intensity = 1.35f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.75f;
        sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.42f, 0.50f, 0.62f);
        RenderSettings.ambientEquatorColor = new Color(0.34f, 0.35f, 0.33f);
        RenderSettings.ambientGroundColor = new Color(0.16f, 0.15f, 0.12f);

        // Rewire spawner + cursor prefabs
        var bm = Object.FindFirstObjectByType<BattleManager>(FindObjectsInactive.Include);
        if (bm != null)
        {
            var so = new SerializedObject(bm);
            var prop = so.FindProperty("battleUnitPrefab");
            if (prop != null) { prop.objectReferenceValue = unitPrefab; so.ApplyModifiedPropertiesWithoutUndo(); }
        }
        var cursor = Object.FindFirstObjectByType<BattleCursor>(FindObjectsInactive.Include);
        if (cursor != null)
        {
            var so = new SerializedObject(cursor);
            so.FindProperty("moveTilePrefab").objectReferenceValue = tilePrefab;
            so.FindProperty("attackTilePrefab").objectReferenceValue = tilePrefab;
            so.FindProperty("hoverTilePrefab").objectReferenceValue = hoverPrefab != null ? hoverPrefab : tilePrefab;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (cursor.cursorObject != null)
            {
                cursor.cursorObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                var cs = cursor.cursorObject.transform.localScale;
                cursor.cursorObject.transform.localScale = new Vector3(cs.x, cs.x, 1f) * 2f;
                EditorUtility.SetDirty(cursor.cursorObject);
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[BattleMapMeshBuilder] BattleArena now uses the 3D Plains diorama. " +
                  "Menu 7 restores the sprite map.");
    }

    static GameObject EnsureUnit3DPrefab()
    {
        string path = "Assets/Prefabs/Battle/BattleUnit3D.prefab";
        var sprite = AssetDatabase.LoadAllAssetsAtPath("Assets/Characters/Benidito/Sprites/rotations/south.png")
            .OfType<Sprite>().FirstOrDefault();
        var shader = Shader.Find("InfernosCurse/BillboardUnit");
        if (sprite == null || shader == null)
        {
            Debug.LogError("[BattleMapMeshBuilder] Missing Benidito sprite or BillboardUnit shader.");
            return null;
        }
        string matPath = $"{MapsFolder}/Materials/BillboardUnit.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, matPath); }
        mat.shader = shader;
        EditorUtility.SetDirty(mat);

        var root = new GameObject("BattleUnit3D");
        root.AddComponent<BattleUnit>();
        var vis = new GameObject("Visual");
        vis.transform.SetParent(root.transform, false);
        var sr = vis.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sharedMaterial = mat;
        float s = 1.15f / sprite.bounds.size.y;
        vis.transform.localScale = new Vector3(s, s, 1f);
        vis.transform.localPosition = new Vector3(0f, sprite.bounds.extents.y * s + 0.02f, 0f);
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    // Flat ground-quad variant of an iso diamond highlight prefab: original
    // becomes a child laid flat (X+90), squash-corrected, lifted off the
    // surface. Cursor code positions the ROOT and tints via
    // GetComponentInChildren<SpriteRenderer> — both keep working.
    static GameObject EnsureFlatHighlight(string srcName, string dstName)
    {
        var src = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Battle/{srcName}.prefab");
        if (src == null) { Debug.LogError($"[BattleMapMeshBuilder] {srcName}.prefab missing."); return null; }
        var root = new GameObject(dstName);
        var child = Object.Instantiate(src);
        child.name = "Quad";
        child.transform.SetParent(root.transform, false);
        child.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        var srcScale = src.transform.localScale;
        // undo the 2:1 iso squash and grow to the 1-unit 3D cell
        float w = srcScale.x * 2f * 1.02f;
        child.transform.localScale = new Vector3(w, w, 1f);
        child.transform.localPosition = new Vector3(0f, 0.06f, 0f);
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"Assets/Prefabs/Battle/{dstName}.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }
}
