using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// 3D diorama battle-map TEMPLATE (FFT-remaster look). Plug-and-play:
//   1. duplicate any BattleMap_* prefab (authoring data: plateaus/obstacles)
//   2. select it → "8b. Build 3D Diorama From Selected Map Prefab"
//      (a default BattleMapStyle3D asset appears next to it — every look
//       knob is inspector data: textures, tints, height step, sun)
//   3. "9. Use 3D Map In Arena" with the new *3D prefab selected
// No shader or builder code involved for new maps. Logic grid untouched:
// the mesh is a pure function of BattleMapAuthoring; a baked
// BattleTerrainHeights component flips BattleGrid into 3D-XZ mode, and
// BattleTerrainCurse feeds the curse mask straight into the terrain shader.
public static class BattleMapMeshBuilder
{
    const string MapsFolder = "Assets/Prefabs/Battle/Maps";

    const int RES = 4;            // mesh quads per logical cell
    const float LIP_SOFT = 0.30f; // rounded shoulder on 1-step edges
    const float LIP_CLIFF = 0.10f;// tight bevel on 2+ step cliffs

    [MenuItem("InfernosCurse/Templates/8. Build 3D Plains Diorama")]
    public static void BuildPlains3D()
    {
        var src = AssetDatabase.LoadAssetAtPath<GameObject>($"{MapsFolder}/BattleMap_Plains.prefab");
        if (src == null) { Debug.LogError("[BattleMapMeshBuilder] Run Templates menu 3 first."); return; }
        BuildFor(src);
    }

    [MenuItem("InfernosCurse/Templates/8b. Build 3D Diorama From Selected Map Prefab")]
    public static void BuildSelected3D()
    {
        var src = Selection.activeGameObject;
        if (src == null || src.GetComponent<BattleMapAuthoring>() == null ||
            !AssetDatabase.Contains(src))
        {
            Debug.LogError("[BattleMapMeshBuilder] Select a battle-map PREFAB ASSET (with BattleMapAuthoring) in the Project window.");
            return;
        }
        BuildFor(src);
    }

    static void BuildFor(GameObject srcPrefab)
    {
        WeatherSurfaceStandardBuilder.EnsureSharedStandard();
        var srcAuth = srcPrefab.GetComponent<BattleMapAuthoring>();
        string baseName = srcPrefab.name;                       // e.g. BattleMap_Plains
        string name3D = baseName + "3D";
        var style = LoadOrCreateStyle(baseName);
        int W = srcAuth.width, H = srcAuth.height;
        int SKIRT = Mathf.Max(1, style.skirtCells);
        float HSTEP = style.heightStep;
        float BASE_Y = style.baseY;

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
                    h += (Mathf.PerlinNoise(x * 0.9f + 3.7f, z * 0.9f + 9.2f) - 0.5f) * 0.3f;
                }
                else
                {
                    int dx = Mathf.Max(0, Mathf.Max(-x, x - (W - 1)));
                    int dz = Mathf.Max(0, Mathf.Max(-z, z - (H - 1)));
                    float r = Mathf.Max(dx, dz);
                    h = -(r * r) * 0.55f;
                    h += (Mathf.PerlinNoise(x * 0.33f + 7.31f, z * 0.33f + 2.17f) - 0.5f)
                         * 2.0f * Mathf.Min(r, 3f);
                    h = Mathf.Max(h, -3f);
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
            bool inField = x0 >= 0 && x0 + 1 < W && z0 >= 0 && z0 + 1 < H;
            float lip = !inField ? 0.5f : (maxD >= 2f ? LIP_CLIFF : LIP_SOFT);
            float wx = LipStep(fx, lip), wz = LipStep(fz, lip);
            return Mathf.Lerp(Mathf.Lerp(h00, h10, wx), Mathf.Lerp(h01, h11, wx), wz) * HSTEP;
        }

        Vector3 NormalAt(float gx, float gz)
        {
            const float e = 0.12f;
            return new Vector3(Sample(gx - e, gz) - Sample(gx + e, gz), 2f * e,
                               Sample(gx, gz - e) - Sample(gx, gz + e)).normalized;
        }

        // ── heightfield surface ──────────────────────────────────────────
        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var cols = new List<Color>();
        var uv2s = new List<Vector2>();     // x = slope shade (freed alpha for path)
        var tris = new List<int>();

        float PathWeight(float gx, float gz)
        {
            if (srcAuth.pathCells == null || srcAuth.pathCells.Count == 0) return 0f;
            float best = 0f;
            foreach (var pc in srcAuth.pathCells)
            {
                float dx = gx - (pc.x + 0.5f), dz = gz - (pc.y + 0.5f);
                float d = Mathf.Sqrt(dx * dx + dz * dz);
                best = Mathf.Max(best, Mathf.Clamp01(1f - (d - 0.38f) / 0.35f));
            }
            // ragged organic edge
            best *= 0.75f + 0.25f * Mathf.PerlinNoise(gx * 2.3f + 5.1f, gz * 2.3f + 8.8f);
            return Mathf.Clamp01(best);
        }

        int nx = gw * RES + 1, nz = gh * RES + 1;
        float x0w = -SKIRT, z0w = -SKIRT;
        for (int iz = 0; iz < nz; iz++)
            for (int ix = 0; ix < nx; ix++)
            {
                float gx = x0w + (float)ix / RES;
                float gz = z0w + (float)iz / RES;
                var n = NormalAt(gx, gz);
                verts.Add(new Vector3(gx, Sample(gx, gz), gz));
                norms.Add(n);

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
                float shade = 1f - Mathf.Clamp01(slope * 1.5f) * 0.32f;
                cols.Add(new Color(grassW, dirtW, rockW, PathWeight(gx, gz)));
                uv2s.Add(new Vector2(shade, 0f));
            }
        for (int iz = 0; iz < nz - 1; iz++)
            for (int ix = 0; ix < nx - 1; ix++)
            {
                int a = iz * nx + ix, b = a + 1, c = a + nx, d = c + 1;
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
            }

        // ── diorama cut: perimeter walls + bottom, dark rock ─────────────
        void Wall(Vector3 top0, Vector3 top1, Vector3 outward)
        {
            int s = verts.Count;
            verts.Add(top0); verts.Add(top1);
            verts.Add(new Vector3(top0.x, BASE_Y, top0.z));
            verts.Add(new Vector3(top1.x, BASE_Y, top1.z));
            for (int k = 0; k < 4; k++) { norms.Add(outward); cols.Add(new Color(0f, 0.2f, 0.8f, 0f)); uv2s.Add(new Vector2(0.30f, 0f)); }
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
            for (int k = 0; k < 4; k++) { norms.Add(Vector3.down); cols.Add(new Color(0f, 0.2f, 0.8f, 0f)); uv2s.Add(new Vector2(0.30f, 0f)); }
            tris.Add(s); tris.Add(s + 1); tris.Add(s + 2);
            tris.Add(s + 1); tris.Add(s + 3); tris.Add(s + 2);
        }

        // ── persist mesh + material (stable GUIDs across rebuilds) ───────
        if (!AssetDatabase.IsValidFolder($"{MapsFolder}/Meshes"))
            AssetDatabase.CreateFolder(MapsFolder, "Meshes");
        string meshPath = $"{MapsFolder}/Meshes/{name3D}.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        bool newMesh = mesh == null;
        if (newMesh) mesh = new Mesh();
        mesh.Clear();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.name = name3D;
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetColors(cols);
        mesh.SetUVs(1, uv2s);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        if (newMesh) AssetDatabase.CreateAsset(mesh, meshPath);
        else EditorUtility.SetDirty(mesh);

        if (!AssetDatabase.IsValidFolder($"{MapsFolder}/Materials"))
            AssetDatabase.CreateFolder(MapsFolder, "Materials");
        string matPath = $"{MapsFolder}/Materials/{name3D}_Terrain.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        var shader = Shader.Find("InfernosCurse/BattleTerrainSplat");
        if (shader == null) { Debug.LogError("[BattleMapMeshBuilder] BattleTerrainSplat shader missing."); return; }
        if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, matPath); }
        mat.shader = shader;
        mat.SetTexture("_GrassTex", style.grassTex);
        mat.SetTexture("_DirtTex", style.dirtTex);
        mat.SetTexture("_RockTex", style.rockTex);
        mat.SetColor("_GrassTint", style.grassTint);
        mat.SetColor("_DirtTint", style.dirtTint);
        mat.SetColor("_RockTint", style.rockTint);
        mat.SetFloat("_Tiling", style.tiling);
        if (style.pathTex != null) mat.SetTexture("_PathTex", style.pathTex);
        mat.SetColor("_PathTint", style.pathTint);
        mat.SetFloat("_WaterLevel", style.waterLevel);
        EditorUtility.SetDirty(mat);

        // ── prefab: terrain + copied authoring + baked heights + curse ───
        var root = new GameObject(name3D);
        var auth = root.AddComponent<BattleMapAuthoring>();
        auth.width = srcAuth.width;
        auth.height = srcAuth.height;
        auth.tileWidth = srcAuth.tileWidth;
        auth.tileHeight = srcAuth.tileHeight;
        auth.plateaus = srcAuth.plateaus
            .Select(p => new BattleMapAuthoring.Plateau { cells = p.cells, elevation = p.elevation }).ToList();
        auth.partySpawns = new List<Vector2Int>(srcAuth.partySpawns);
        auth.enemySpawns = new List<Vector2Int>(srcAuth.enemySpawns);

        var terrain = new GameObject("Terrain");
        terrain.transform.SetParent(root.transform, false);
        terrain.AddComponent<MeshFilter>().sharedMesh = mesh;
        terrain.AddComponent<MeshRenderer>().sharedMaterial = mat;

        var grassBlocked = new HashSet<Vector2Int>(srcAuth.pathCells);
        foreach (var cell in srcAuth.partySpawns) grassBlocked.Add(cell);
        foreach (var cell in srcAuth.enemySpawns) grassBlocked.Add(cell);
        foreach (var obstacle in srcPrefab.GetComponentsInChildren<BattleObstacle>(true))
            grassBlocked.Add(obstacle.cell);

        int grassSeed = 1269;
        foreach (char c in baseName) grassSeed = grassSeed * 31 + c;
        WeatherSurfaceStandardBuilder.CreateGrassField(
            "StylizedGrass_Battle", root.transform,
            new Bounds(new Vector3(W * 0.5f, BASE_Y, H * 0.5f), new Vector3(W, 0.1f, H)),
            (x, z) => Sample(x, z) + 0.025f,
            (x, z) =>
            {
                var cell = new Vector2Int(Mathf.FloorToInt(x), Mathf.FloorToInt(z));
                if (grassBlocked.Contains(cell)) return false;
                return style.waterLevel <= -99f || Sample(x, z) > style.waterLevel + 0.08f;
            },
            0.35f, grassSeed, false, name3D + "_GrassField");

        var heights = root.AddComponent<BattleTerrainHeights>();
        heights.width = W;
        heights.height = H;
        heights.cellY = new float[W * H];
        for (int x = 0; x < W; x++)
            for (int z = 0; z < H; z++)
                heights.cellY[x + z * W] = Sample(x + 0.5f, z + 0.5f);
        heights.res = RES;
        heights.surfY = new float[(W * RES + 1) * (H * RES + 1)];
        for (int iz = 0; iz <= H * RES; iz++)
            for (int ix = 0; ix <= W * RES; ix++)
                heights.surfY[ix + iz * (W * RES + 1)] = Sample(ix / (float)RES, iz / (float)RES);
        root.AddComponent<BattleTerrainCurse>();
        root.AddComponent<BattleTerrainFog>();
        root.AddComponent<ZoneFogOfWar>();   // battle fog: party-sheet vision + CT ambush

        // Optional water plane (Arno/river maps): style sets level + material.
        if (style.waterLevel > -99f)
        {
            style.waterMaterial = WeatherSurfaceStandardBuilder.EnsureWaterMaterial(StandardWaterProfile.BattleShallow);
            EditorUtility.SetDirty(style);
            var water = GameObject.CreatePrimitive(PrimitiveType.Quad);
            water.name = "Water";
            Object.DestroyImmediate(water.GetComponent<Collider>());
            water.transform.SetParent(root.transform, false);
            water.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            water.transform.position = new Vector3(W * 0.5f, style.waterLevel, H * 0.5f);
            water.transform.localScale = new Vector3(gw + 6f, gh + 6f, 1f);
            water.GetComponent<MeshRenderer>().sharedMaterial = style.waterMaterial;
            WeatherSurfaceStandardBuilder.ConfigureWater(
                water, StandardWaterProfile.BattleShallow, WeatherSurfaceExposure.Outdoor);
        }

        // Obstacles: copy cells (gameplay); visuals come later with props.
        foreach (var ob in srcPrefab.GetComponentsInChildren<BattleObstacle>(true))
        {
            var g = new GameObject(ob.name);
            g.transform.SetParent(root.transform, false);
            var o = g.AddComponent<BattleObstacle>();
            o.cell = ob.cell;
            o.makeUnwalkable = ob.makeUnwalkable;
            o.addedElevation = ob.addedElevation;
            g.transform.position = auth.CellToWorld(ob.cell);
        }

        PrefabUtility.SaveAsPrefabAsset(root, $"{MapsFolder}/{name3D}.prefab");
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Debug.Log($"[BattleMapMeshBuilder] {name3D} saved ({verts.Count} verts). Style: {AssetDatabase.GetAssetPath(style)}. " +
                  "Select the 3D prefab and run menu 9 to wire the arena.");
    }

    static BattleMapStyle3D LoadOrCreateStyle(string mapBaseName)
    {
        if (!AssetDatabase.IsValidFolder($"{MapsFolder}/Styles"))
            AssetDatabase.CreateFolder(MapsFolder, "Styles");
        string path = $"{MapsFolder}/Styles/{mapBaseName}_Style3D.asset";
        var style = AssetDatabase.LoadAssetAtPath<BattleMapStyle3D>(path);
        if (style != null) return style;
        style = ScriptableObject.CreateInstance<BattleMapStyle3D>();
        style.grassTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Environment/GiardinoDelleRose/TerrainLayers/TL_Meadow_Diffuse.png");
        style.dirtTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Environment/PiazzaSignoria/Textures/signoria-ground-dirt.png");
        style.rockTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Environment/PiazzaSignoria/Textures/signoria-wall-rusticated.png");
        AssetDatabase.CreateAsset(style, path);
        return style;
    }

    // ── Arena wiring ──────────────────────────────────────────────────────

    [MenuItem("InfernosCurse/Templates/9. Use 3D Map In Arena (selected, or Plains3D)")]
    public static void WireArena3D()
    {
        GameObject mapPrefab = null;
        var sel = Selection.activeGameObject;
        if (sel != null && AssetDatabase.Contains(sel) && sel.GetComponent<BattleTerrainHeights>() != null)
            mapPrefab = sel;
        if (mapPrefab == null)
            mapPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{MapsFolder}/BattleMap_Plains3D.prefab");
        if (mapPrefab == null) { Debug.LogError("[BattleMapMeshBuilder] No 3D map — run menu 8 first."); return; }
        var style = LoadOrCreateStyle(mapPrefab.name.EndsWith("3D") ? mapPrefab.name[..^2] : mapPrefab.name);

        EnsureDecalFeature("Assets/Settings/PC_Renderer.asset");   // for future blob shadows/rings
        EnsureDecalFeature("Assets/Settings/Mobile_Renderer.asset");
        var unitPrefab = EnsureUnit3DPrefab();
        var tilePrefab = EnsureConformingHighlight("Highlight_Tile3D");
        var hoverPrefab = EnsureConformingHighlight("Highlight_Hover3D");
        if (unitPrefab == null || tilePrefab == null) return;

        var scene = EditorSceneManager.OpenScene("Assets/Scenes/BattleArena.unity", OpenSceneMode.Single);
        var oldVis = GameObject.Find("[GridVisuals]");
        if (oldVis != null) Object.DestroyImmediate(oldVis);
        var oldMap = Object.FindFirstObjectByType<BattleMapAuthoring>();
        if (oldMap != null) Object.DestroyImmediate(oldMap.gameObject);
        PrefabUtility.InstantiatePrefab(mapPrefab);

        var auth = mapPrefab.GetComponent<BattleMapAuthoring>();
        Vector3 center = new Vector3(auth.width * 0.5f, 0.5f, auth.height * 0.5f);
        var cam = Camera.main;
        cam.orthographic = false;
        cam.fieldOfView = 22f;
        cam.nearClipPlane = 0.5f;
        cam.farClipPlane = 300f;
        cam.transform.position = center + new Vector3(0.55f, 0.6f, -1f).normalized * 22f;
        cam.transform.LookAt(center);
        var camData = cam.GetComponent<UniversalAdditionalCameraData>();
        if (camData == null) camData = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
        camData.renderPostProcessing = true;
        var rig = cam.GetComponent<BattleCameraRig>();
        if (rig == null) rig = cam.gameObject.AddComponent<BattleCameraRig>();
        rig.pivot = center;   // Q/E or [ ] rotate the view in 90° steps

        // Diorama post: tilt-shift DoF focused on the battlefield + gentle
        // bloom/vignette/grading. One shared profile asset, style-agnostic.
        var volGo = GameObject.Find("BattleVolume") ?? new GameObject("BattleVolume");
        var vol = volGo.GetComponent<UnityEngine.Rendering.Volume>() ?? volGo.AddComponent<UnityEngine.Rendering.Volume>();
        vol.isGlobal = true;
        vol.sharedProfile = EnsureBattleVolumeProfile();

        var sun = Object.FindObjectsByType<Light>(FindObjectsSortMode.None)
            .FirstOrDefault(l => l.type == LightType.Directional);
        if (sun == null) sun = new GameObject("Sun_Battle (authored)").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = style.sunColor;
        sun.intensity = style.sunIntensity;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.75f;
        sun.transform.rotation = Quaternion.Euler(style.sunEuler);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = style.ambientSky;
        RenderSettings.ambientEquatorColor = style.ambientEquator;
        RenderSettings.ambientGroundColor = style.ambientGround;

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
            so.FindProperty("hoverTilePrefab").objectReferenceValue = hoverPrefab;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (cursor.cursorObject != null)
            {
                cursor.cursorObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                EditorUtility.SetDirty(cursor.cursorObject);
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[BattleMapMeshBuilder] BattleArena wired to {mapPrefab.name}. Menu 7 restores the sprite map.");
    }

    static UnityEngine.Rendering.VolumeProfile EnsureBattleVolumeProfile()
    {
        string path = $"{MapsFolder}/BattleDiorama_Post.asset";
        var profile = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(path);
        if (profile != null) return profile;
        profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
        AssetDatabase.CreateAsset(profile, path);

        var dof = profile.Add<DepthOfField>(true);
        dof.mode.Override(DepthOfFieldMode.Bokeh);
        dof.focusDistance.Override(22f);
        dof.focalLength.Override(42f);
        dof.aperture.Override(5.6f);

        var bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(1.05f);
        bloom.intensity.Override(0.35f);

        var vig = profile.Add<Vignette>(true);
        vig.intensity.Override(0.22f);
        vig.smoothness.Override(0.5f);

        var ca = profile.Add<ColorAdjustments>(true);
        ca.saturation.Override(10f);
        ca.contrast.Override(6f);

        AssetDatabase.SaveAssets();
        return profile;
    }

    static GameObject EnsureUnit3DPrefab()
    {
        string path = "Assets/Prefabs/Battle/BattleUnit3D.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;
        var sprite = AssetDatabase.LoadAllAssetsAtPath("Assets/Characters/Benidito/Sprites/rotations/south.png")
            .OfType<Sprite>().FirstOrDefault();
        var shader = Shader.Find("InfernosCurse/BillboardUnit");
        if (sprite == null || shader == null) { Debug.LogError("[BattleMapMeshBuilder] Missing sprite or BillboardUnit shader."); return null; }
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

    // Ground highlights as terrain-conforming meshes: ConformingHighlight
    // rebuilds a draped quad from the baked sub-cell heightfield wherever
    // the cursor moves it — slopes and lips wrap correctly, and it needs
    // no renderer features (identical on mobile).
    static GameObject EnsureConformingHighlight(string name)
    {
        var tex = EnsureHighlightTexture();
        var shader = Shader.Find("InfernosCurse/GroundHighlight");
        if (shader == null) { Debug.LogError("[BattleMapMeshBuilder] GroundHighlight shader missing."); return null; }
        string matPath = $"{MapsFolder}/Materials/GroundHighlight.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, matPath); }
        mat.shader = shader;
        mat.SetTexture("_MainTex", tex);
        EditorUtility.SetDirty(mat);

        var root = new GameObject(name);
        root.AddComponent<MeshFilter>();
        root.AddComponent<MeshRenderer>().sharedMaterial = mat;
        root.AddComponent<ConformingHighlight>();
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"Assets/Prefabs/Battle/{name}.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    static Texture2D EnsureHighlightTexture()
    {
        string path = $"{MapsFolder}/Sprites/highlight-square.png";
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null) return existing;
        const int S = 64;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = Mathf.Max(0, Mathf.Abs(x - (S - 1) * 0.5f) - S * 0.30f) / (S * 0.18f);
                float dy = Mathf.Max(0, Mathf.Abs(y - (S - 1) * 0.5f) - S * 0.30f) / (S * 0.18f);
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        ti.textureType = TextureImporterType.Default;
        ti.alphaIsTransparency = true;
        ti.mipmapEnabled = false;
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static void EnsureDecalFeature(string rendererPath)
    {
        var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
        if (data == null) { Debug.LogWarning($"[BattleMapMeshBuilder] {rendererPath} not found — decals skipped there."); return; }
        if (data.rendererFeatures.Any(f => f is DecalRendererFeature)) return;

        var feat = ScriptableObject.CreateInstance<DecalRendererFeature>();
        feat.name = "DecalRendererFeature";
        AssetDatabase.AddObjectToAsset(feat, data);
        AssetDatabase.SaveAssets();

        var so = new SerializedObject(data);
        var list = so.FindProperty("m_RendererFeatures");
        list.arraySize++;
        list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = feat;
        var map = so.FindProperty("m_RendererFeatureMap");
        if (map != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feat, out _, out long localId))
        {
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
        }
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        Debug.Log($"[BattleMapMeshBuilder] Decal renderer feature added to {rendererPath}.");
    }
}
