using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using sc.terrain.vegetationspawner;

// Generates Assets/Scenes/GiardinoDelleRose.unity — LAYOUT V3 (approved Draft 2
// + David's corrections 7/05): a 1299 hillside garden shaped like the
// Refrences/images/rose1.jpg parchment map. Fan of terraced trails below, a
// formal quadripartite rose garden (hortus conclusus) as the centerpiece on
// the mid level with the gardener's buildings at its east side, and the
// hilltop road/overlook above. Trails are painted DIRECTLY into the splatmap
// via TerrainData.SetAlphamaps — Procedural Terrain Painter is not used
// (its multi-layer blending is broken in this project, see memory notes).
//
// Period rule: 1299. No modern park furniture — cottage/shrine/wellhead/worn
// Roman marble instead. Pergola tunnels with climbing roses over the trail
// approaches (David's ground-photo reference).
//
// Prop placement is marker-driven: this builder drops MARKER_<assetId>@<h>
// empties; menu item "3. Place Hero Props" consumes them (fallback pedestals
// for GLBs still generating).
public static class GiardinoDelleRoseSceneBuilder
{
    const string ScenePath = "Assets/Scenes/GiardinoDelleRose.unity";
    const string PlayerSourceScene = "Assets/Scenes/PonteVecchio.unity";
    const string CameraKitPath = "Assets/Prefabs/HD2D_CameraKit.prefab";
    const string LayerFolder = "Assets/Environment/GiardinoDelleRose/TerrainLayers";

    // Levels (world Y)
    const float LowerY = 1f;   // fan terraces, city gate
    const float MidY = 6f;     // plaza band — the formal garden
    const float UpperY = 11f;  // hilltop road / overlook
    const float TerrainWidth = 80f;   // x: -40..40
    const float TerrainLength = 70f;  // z: -35..35
    const float TerrainHeight = 20f;

    // Formal garden square (Level 1): x -13..13, z -9..13, fountain at (0,2)
    static readonly Vector2 GardenMin = new Vector2(-13f, -9f);
    static readonly Vector2 GardenMax = new Vector2(13f, 13f);
    static readonly Vector2 GardenCenter = new Vector2(0f, 2f);

    // Pond dug into the plaza band at its west edge (rose1.jpg)
    static readonly Vector2 PondCenter = new Vector2(-17f, 4f);
    const float PondRx = 5f, PondRz = 3.5f, PondDepth = 0.9f;

    [MenuItem("InfernosCurse/Giardino delle Rose/1. Build Scene")]
    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SceneManager.SetActiveScene(scene);

        var terrain = BuildTerrain();
        PaintTerrain(terrain);
        SpawnVegetation(terrain);
        BuildHedgeBlockout();
        BuildMarkers();
        BuildPond();
        BuildBoundaries();
        BuildTravelMarkers();
        BuildFloristPlaceholder();
        BuildLighting();
        BuildBackdrop();
        CopyPlayerFromPonteVecchio(scene);
        PlaceCameraKit();

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuildSettings(ScenePath);
        Debug.Log("[GiardinoDelleRoseSceneBuilder] Layout v3 built. Run " +
                  "'InfernosCurse/Giardino delle Rose/3. Place Hero Props' next.");
    }

    // ── Terrain ───────────────────────────────────────────────────────────────

    // Curved band boundaries (world z as a function of world x).
    static float B1(float x) => -10f + 3f * Mathf.Sin((x + 40f) / 80f * Mathf.PI * 1.1f);
    static float B2(float x) => 15f + 2.5f * Mathf.Sin((x + 40f) / 80f * Mathf.PI * 0.9f + 0.6f);

    // Transition half-widths: wide (walkable ramp) inside trail corridors,
    // narrow (steep terrace bank) everywhere else.
    static float RampW1(float x)
    {
        if (x > -10f && x < 6f) return 7f;      // main axis ramp (gate -> garden)
        if (x > -32f && x < -20f) return 6f;    // west switchback (fan loop)
        return 1.4f;
    }
    static float RampW2(float x)
    {
        if (x > -2f && x < 12f) return 6f;      // rear ramp (garden -> road)
        return 1.4f;
    }

    static float HeightAt(float x, float z)
    {
        float h = LowerY;
        h += (MidY - LowerY) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(B1(x) - RampW1(x), B1(x) + RampW1(x), z));
        h += (UpperY - MidY) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(B2(x) - RampW2(x), B2(x) + RampW2(x), z));

        // Pond depression (elliptical, smooth rim)
        float pdx = (x - PondCenter.x) / PondRx;
        float pdz = (z - PondCenter.y) / PondRz;
        float pd = pdx * pdx + pdz * pdz;
        if (pd < 1f) h -= PondDepth * Mathf.SmoothStep(1f, 0f, pd);

        return h;
    }

    static Terrain BuildTerrain()
    {
        var data = new TerrainData
        {
            heightmapResolution = 513,
            size = new Vector3(TerrainWidth, TerrainHeight, TerrainLength),
        };
        // Required for grass/flower detail meshes — read-only properties,
        // must use the method ("Terrain has zero detail resolution" otherwise).
        data.SetDetailResolution(512, 16);

        int res = data.heightmapResolution;
        var heights = new float[res, res];
        for (int iz = 0; iz < res; iz++)
        {
            float wz = Mathf.Lerp(-TerrainLength / 2f, TerrainLength / 2f, iz / (float)(res - 1));
            for (int ix = 0; ix < res; ix++)
            {
                float wx = Mathf.Lerp(-TerrainWidth / 2f, TerrainWidth / 2f, ix / (float)(res - 1));
                heights[iz, ix] = HeightAt(wx, wz) / TerrainHeight;
            }
        }
        data.SetHeights(0, 0, heights);

        var terrainGO = Terrain.CreateTerrainGameObject(data);
        terrainGO.name = "Terrain_GiardinoDelleRose";
        terrainGO.transform.position = new Vector3(-TerrainWidth / 2f, 0f, -TerrainLength / 2f);
        return terrainGO.GetComponent<Terrain>();
    }

    // ── Trails + splatmap (direct SetAlphamaps — no Terrain Painter) ─────────

    struct Trail { public Vector2[] pts; public float width; }

    static readonly List<Trail> Trails = new List<Trail>
    {
        // Main: gate -> winding approach -> garden south entrance -> through
        // the garden -> north entrance -> ramp -> road -> overlook pad.
        new Trail { width = 2.2f, pts = new[] {
            new Vector2(0,-31), new Vector2(0,-26), new Vector2(-1,-21),
            new Vector2(-3,-16), new Vector2(-2,-12), new Vector2(0,-9),
            new Vector2(0,13), new Vector2(2,16), new Vector2(0,20), new Vector2(0,24) } },
        // Garden cross path (east-west through the fountain)
        new Trail { width = 2.5f, pts = new[] { new Vector2(-13,2), new Vector2(13,2) } },
        // West fan loop: branches off the approach, sweeps the lower terraces,
        // switchbacks up, passes the pond, joins the garden's west entrance.
        new Trail { width = 1.8f, pts = new[] {
            new Vector2(-3,-16), new Vector2(-12,-18), new Vector2(-20,-14),
            new Vector2(-26,-8), new Vector2(-20,-2), new Vector2(-14,2), new Vector2(-13,2) } },
        // Marble-figure spur at the fan's west tip
        new Trail { width = 1.5f, pts = new[] { new Vector2(-26,-8), new Vector2(-28,-16) } },
        // Cottage spur off the garden's east entrance
        new Trail { width = 1.8f, pts = new[] { new Vector2(13,2), new Vector2(16,3), new Vector2(19,5) } },
        // Pond spur
        new Trail { width = 1.5f, pts = new[] { new Vector2(-13,2), new Vector2(-15.5f,3.5f) } },
        // The hilltop road (Via) along the top band
        new Trail { width = 4f, pts = new[] { new Vector2(-34,20), new Vector2(34,20) } },
    };

    // Circular stone pads (center, radius)
    static readonly (Vector2 c, float r)[] Pads =
    {
        (new Vector2(0, 2), 5f),      // fountain circle
        (new Vector2(-28, -16), 2.5f),// marble figure
        (new Vector2(18, 11), 2f),    // wellhead
        (new Vector2(0, 24), 3.5f),   // overlook
        (new Vector2(19, 5), 2.5f),   // cottage yard
    };

    static void PaintTerrain(Terrain terrain)
    {
        if (!AssetDatabase.IsValidFolder(LayerFolder)) CreateFolderRecursive(LayerFolder);

        var grassLayer = CreateTerrainLayer("TL_Garden_Grass", new Color(0.42f, 0.55f, 0.30f));
        var pathLayer = CreateTerrainLayer("TL_Garden_Path", new Color(0.66f, 0.60f, 0.50f));
        terrain.terrainData.terrainLayers = new[] { grassLayer, pathLayer };

        var data = terrain.terrainData;
        data.alphamapResolution = 512;
        int res = data.alphamapResolution;
        var alpha = new float[res, res, 2];

        const float edge = 0.7f; // soft trail edge in meters

        for (int iz = 0; iz < res; iz++)
        {
            float wz = Mathf.Lerp(-TerrainLength / 2f, TerrainLength / 2f, iz / (float)(res - 1));
            for (int ix = 0; ix < res; ix++)
            {
                float wx = Mathf.Lerp(-TerrainWidth / 2f, TerrainWidth / 2f, ix / (float)(res - 1));
                var p = new Vector2(wx, wz);

                float d = float.MaxValue;
                float targetW = 0f;
                foreach (var t in Trails)
                {
                    for (int i = 0; i < t.pts.Length - 1; i++)
                    {
                        float sd = DistToSegment(p, t.pts[i], t.pts[i + 1]);
                        if (sd - t.width / 2f < d - targetW / 2f) { d = sd; targetW = t.width; }
                    }
                }
                float pathWeight = 1f - Mathf.Clamp01((d - targetW / 2f) / edge);

                foreach (var (c, r) in Pads)
                {
                    float pdd = Vector2.Distance(p, c);
                    pathWeight = Mathf.Max(pathWeight, 1f - Mathf.Clamp01((pdd - r) / edge));
                }

                alpha[iz, ix, 0] = 1f - pathWeight;
                alpha[iz, ix, 1] = pathWeight;
            }
        }

        data.SetAlphamaps(0, 0, alpha);
        data.SetBaseMapDirty();
        EditorUtility.SetDirty(data);
    }

    static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
        return Vector2.Distance(p, a + ab * t);
    }

    static TerrainLayer CreateTerrainLayer(string name, Color tint)
    {
        string layerPath = $"{LayerFolder}/{name}.terrainlayer";
        var existing = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
        if (existing != null) return existing;

        // Real saved texture asset — Texture2D.whiteTexture renders as the
        // missing-texture checker when referenced from a .terrainlayer asset.
        string texPath = $"{LayerFolder}/{name}_Diffuse.png";
        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        var pixels = new Color[16];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = tint;
        tex.SetPixels(pixels);
        tex.Apply();
        System.IO.File.WriteAllBytes(texPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(texPath);
        var savedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

        var layer = new TerrainLayer { diffuseTexture = savedTex, tileSize = new Vector2(8f, 8f) };
        AssetDatabase.CreateAsset(layer, layerPath);
        EditorUtility.SetDirty(layer);
        return layer;
    }

    static void CreateFolderRecursive(string path)
    {
        var parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    // ── Vegetation ────────────────────────────────────────────────────────────

    static void SpawnVegetation(Terrain terrain)
    {
        var spawner = terrain.gameObject.AddComponent<VegetationSpawner>();
        spawner.terrains = new List<Terrain> { terrain };
        spawner.waterHeight = MidY - PondDepth + 0.4f; // reject details inside the pond bowl

        var cypress = FindPackPrefab("EA03_Nature_Tree", new[] { "Assets/EmaceArt/Slavic World Free/Prefabs" });
        if (cypress != null)
        {
            // Fan terraces — scattered trees lining the lower trails.
            var fanTrees = VegetationSpawner.TreeType.New(cypress);
            fanTrees.probability = 30f;
            fanTrees.slopeRange = new Vector2(0f, 14f);
            fanTrees.heightRange = new Vector2(LowerY - 0.5f, LowerY + 1.5f); // world meters
            fanTrees.distance = 6f;
            fanTrees.collisionCheck = true;
            spawner.treeTypes.Add(fanTrees);

            // Road line — the Via's tree row band.
            var roadTrees = VegetationSpawner.TreeType.New(cypress);
            roadTrees.probability = 45f;
            roadTrees.slopeRange = new Vector2(0f, 10f);
            roadTrees.heightRange = new Vector2(UpperY - 0.5f, UpperY + 2f);
            roadTrees.distance = 5f;
            roadTrees.collisionCheck = true;
            spawner.treeTypes.Add(roadTrees);
        }
        else Debug.LogWarning("[GiardinoDelleRoseSceneBuilder] No tree prefab found — trees skipped.");

        const string grassPatchPath = "Packages/xyz.staggart-creations.stylized-grass/Prefabs/TerrainGrass/GrassPatch_Terrain.prefab";
        var grassPatch = AssetDatabase.LoadAssetAtPath<GameObject>(grassPatchPath);
        if (grassPatch != null)
        {
            spawner.grassPrefabs.Add(new VegetationSpawner.GrassPrefab
            {
                prefab = grassPatch,
                type = VegetationSpawner.GrassType.Mesh,
                probability = 80f,
                slopeRange = new Vector2(0f, 40f),
            });
        }

        // Wildflower underplanting (the crowded trail edges in David's photo
        // reference) — poppies read as the red valerian in that shot.
        const string poppiesPath = "Packages/xyz.staggart-creations.stylized-grass/Prefabs/TerrainFlowers/Flower_Dandelions_Terrain.prefab";
        var poppies = AssetDatabase.LoadAssetAtPath<GameObject>(poppiesPath);
        if (poppies != null)
        {
            spawner.grassPrefabs.Add(new VegetationSpawner.GrassPrefab
            {
                prefab = poppies,
                type = VegetationSpawner.GrassType.Mesh,
                probability = 25f,
                slopeRange = new Vector2(0f, 30f),
            });
        }

        spawner.RefreshTreePrefabs();
        spawner.RefreshGrassPrototypes();
        spawner.Respawn(grass: true, trees: true);
    }

    static GameObject FindPackPrefab(string search, string[] folders)
    {
        var guid = AssetDatabase.FindAssets($"{search} t:prefab", folders).FirstOrDefault();
        return guid != null ? AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)) : null;
    }

    // ── Formal garden hedge blockout ─────────────────────────────────────────
    //
    // Perimeter of clipped-box hedge around the quad garden, 3m entrance gaps
    // at the four cross-path ends. Primitive green boxes now; each also gets a
    // MARKER so "Place Hero Props" swaps in boxwood-hedge-segment GLBs when
    // the generation batch lands (the primitive is destroyed on swap).

    static void BuildHedgeBlockout()
    {
        var group = new GameObject("[Hedges]");
        const float segLen = 2.4f, gap = 3f;

        void Row(Vector2 from, Vector2 to)
        {
            Vector2 dir = (to - from).normalized;
            float len = Vector2.Distance(from, to);
            int count = Mathf.FloorToInt(len / segLen);
            for (int i = 0; i < count; i++)
            {
                Vector2 c2 = from + dir * (segLen * (i + 0.5f));
                var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                seg.name = "MARKER_boxwood-hedge-segment@1.1";
                seg.transform.SetParent(group.transform, false);
                seg.transform.position = new Vector3(c2.x, MidY + 0.55f, c2.y);
                seg.transform.localScale = new Vector3(segLen, 1.1f, 0.7f);
                seg.transform.rotation = Quaternion.LookRotation(new Vector3(-dir.y, 0f, dir.x), Vector3.up);
                Tint(seg, new Color(0.24f, 0.38f, 0.18f));
            }
        }

        float half = gap / 2f;
        // South edge (gap at x=0 for the main entrance)
        Row(new Vector2(GardenMin.x, GardenMin.y), new Vector2(-half, GardenMin.y));
        Row(new Vector2(half, GardenMin.y), new Vector2(GardenMax.x, GardenMin.y));
        // North edge
        Row(new Vector2(GardenMin.x, GardenMax.y), new Vector2(-half, GardenMax.y));
        Row(new Vector2(half, GardenMax.y), new Vector2(GardenMax.x, GardenMax.y));
        // West edge (gap at z=2 for the cross path)
        Row(new Vector2(GardenMin.x, GardenMin.y), new Vector2(GardenMin.x, GardenCenter.y - half));
        Row(new Vector2(GardenMin.x, GardenCenter.y + half), new Vector2(GardenMin.x, GardenMax.y));
        // East edge
        Row(new Vector2(GardenMax.x, GardenMin.y), new Vector2(GardenMax.x, GardenCenter.y - half));
        Row(new Vector2(GardenMax.x, GardenCenter.y + half), new Vector2(GardenMax.x, GardenMax.y));
    }

    // ── Prop markers (consumed by "3. Place Hero Props") ─────────────────────

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

        // Centerpiece
        M("garden-fountain-basin", 1.2f, new Vector3(0f, MidY, 2f));
        M("garden-stone-bench", 0.9f, new Vector3(-5f, MidY, 5.5f), 125f);   // faces fountain
        M("garden-stone-bench", 0.9f, new Vector3(5f, MidY, -1.5f), -55f);   // faces fountain
        M("shrine-tabernacolo", 2.2f, new Vector3(-2.2f, MidY, -11f), 20f);  // south entrance

        // Quadrant rose grids — 4 per quadrant, colors mixed per bed.
        string[] roses = { "rose-bush-crimson", "rose-bush-ivory", "rose-bush-gold", "rose-climbing-wine" };
        var quadCenters = new[] { new Vector2(-6.5f, 7.5f), new Vector2(6.5f, 7.5f), new Vector2(-6.5f, -3.5f), new Vector2(6.5f, -3.5f) };
        int r = 0;
        foreach (var q in quadCenters)
            foreach (var off in new[] { new Vector2(-2f, -1.8f), new Vector2(2f, -1.8f), new Vector2(-2f, 1.8f), new Vector2(2f, 1.8f) })
                M(roses[r++ % roses.Length], Random.Range(0.85f, 1.15f), new Vector3(q.x + off.x, MidY, q.y + off.y), Random.Range(0f, 360f));

        // East buildings (rose1.jpg puts the structures on the plaza's east side)
        M("gardeners-cottage", 3.2f, new Vector3(21f, MidY, 7f), -90f);      // faces west toward garden
        M("florist-market-stall", 2.0f, new Vector3(15.5f, MidY, 4f), -120f);
        M("stone-wellhead", 1.4f, new Vector3(18f, MidY, 11f));

        // Pergola rose-tunnel on the gate approach (David's photo reference) +
        // pair framing the overlook approach. Climbing wine roses at posts.
        M("garden-wooden-pergola", 2.6f, new Vector3(0f, LowerY, -27f));
        M("garden-wooden-pergola", 2.6f, new Vector3(-0.5f, LowerY, -23.5f), 8f);
        M("garden-wooden-pergola", 2.6f, new Vector3(-1.5f, LowerY, -20f), 15f);
        M("rose-climbing-wine", 2.2f, new Vector3(1.4f, LowerY, -27.4f));
        M("rose-climbing-wine", 2.2f, new Vector3(-1.9f, LowerY, -23.1f));
        M("rose-climbing-wine", 2.2f, new Vector3(-3f, LowerY, -20.3f));
        M("garden-wooden-pergola", 2.6f, new Vector3(0f, UpperY, 20.5f));
        M("garden-wooden-pergola", 2.6f, new Vector3(0f, UpperY, 23f));
        M("rose-climbing-wine", 2.2f, new Vector3(1.5f, UpperY, 21.7f));

        // Fan terrace rose clusters along the west loop trail
        var fanClusters = new[] { new Vector3(-10f, LowerY, -21f), new Vector3(-17f, LowerY, -12f), new Vector3(-22f, MidY, -2f) };
        foreach (var c in fanClusters)
            for (int i = 0; i < 3; i++)
            {
                Vector2 j = Random.insideUnitCircle * 1.4f;
                M(roses[r++ % roses.Length], Random.Range(0.85f, 1.25f), c + new Vector3(j.x, 0f, j.y), Random.Range(0f, 360f));
            }

        // West fan tip — the worn Roman marble, half lost in the growth.
        M("ancient-marble-figure", 1.8f, new Vector3(-28f, LowerY, -16f), 140f);

        // Overlook bench, facing south over the city.
        M("garden-stone-bench", 0.9f, new Vector3(3f, UpperY, 24.5f), 180f);
    }

    // ── Pond (Stylized Water 3 in the dug basin) ─────────────────────────────

    static void BuildPond()
    {
        // Sized EXACTLY via the package's mesh factory — the StylizedWater
        // prefabs carry a huge plane mesh (scaling guesses flooded the whole
        // lower fan on the first attempt; the same oversized plane was also
        // the source of the "cyan cutout patches" misdiagnosed as grass).
        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Stylized Water 3/Materials/StylizedWater3_Clear.mat");
        if (mat == null) { Debug.LogWarning("[GiardinoDelleRoseSceneBuilder] Missing StylizedWater3_Clear.mat — pond skipped."); return; }

        var mesh = StylizedWater3.WaterMesh.Create(StylizedWater3.WaterMesh.Shape.Disk, PondRx * 1.9f, 0.5f);
        var wo = StylizedWater3.WaterObject.New(mat, mesh);
        wo.gameObject.name = "PondWater";
        wo.transform.position = new Vector3(PondCenter.x, MidY - PondDepth * 0.45f, PondCenter.y);
        wo.transform.localScale = new Vector3(1f, 1f, PondRz / PondRx); // squash disk into the ellipse basin
    }

    // ── Boundaries / travel / NPC / dressing ─────────────────────────────────

    static void BuildBoundaries()
    {
        var group = new GameObject("[Boundaries]");
        foreach (var (name, pos, size) in new[]
        {
            ("Bound_S", new Vector3(0f, 3f, -TerrainLength / 2f - 1f), new Vector3(TerrainWidth, 6f, 1f)),
            ("Bound_N", new Vector3(0f, UpperY + 4f, TerrainLength / 2f + 1f), new Vector3(TerrainWidth, 8f, 1f)),
            ("Bound_E", new Vector3(TerrainWidth / 2f + 1f, 7f, 0f), new Vector3(1f, 14f, TerrainLength)),
            ("Bound_W", new Vector3(-TerrainWidth / 2f - 1f, 7f, 0f), new Vector3(1f, 14f, TerrainLength)),
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

        MakeEntry("giardino_gate", "Garden Gate", new Vector3(0f, LowerY, -29f), new Vector2(0f, 1f));
        MakeEntry("giardino_fountain", "The Fountain", new Vector3(0f, MidY, -2f), new Vector2(0f, 1f));
        MakeEntry("giardino_overlook", "The Overlook", new Vector3(0f, UpperY, 24f), new Vector2(0f, -1f));

        var exit = new GameObject("ExitZone_Gate");
        exit.transform.position = new Vector3(0f, LowerY + 1f, -33f);
        exit.AddComponent<BoxCollider>().size = new Vector3(9f, 3f, 2.5f);
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

    static void BuildFloristPlaceholder()
    {
        // Lives at the cottage door — inert until the quest flag (see
        // FloristUnlockGate + design spec "Unlock flow").
        var npc = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        npc.name = "NPC_Florist_PLACEHOLDER";
        npc.transform.position = new Vector3(18.5f, MidY + 1f, 5.5f);
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
        // 1299 Florence from the hill: tower-houses and walls, no dome (the
        // cathedral was barely begun). Tinted quad placeholder — real painted
        // backdrop is a future art pass. South side: the camera looks north
        // from the overlook... the overlook faces SOUTH toward the city, so
        // the backdrop sits beyond the SOUTH edge, seen past the gate.
        var backdrop = GameObject.CreatePrimitive(PrimitiveType.Quad);
        backdrop.name = "Backdrop_City_South";
        Object.DestroyImmediate(backdrop.GetComponent<Collider>());
        backdrop.transform.position = new Vector3(0f, 6f, -44f);
        backdrop.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        backdrop.transform.localScale = new Vector3(110f, 22f, 1f);
        Tint(backdrop, new Color(0.72f, 0.68f, 0.60f)); // hazy stone city
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
            copy.transform.position = new Vector3(0f, LowerY + source.transform.position.y, -29f);
            Debug.Log($"[GiardinoDelleRoseSceneBuilder] Player '{copy.name}' copied from PonteVecchio.");
        }
        else Debug.LogError("[GiardinoDelleRoseSceneBuilder] No Player-tagged object found in PonteVecchio!");

        EditorSceneManager.CloseScene(pv, true);
    }

    static void PlaceCameraKit()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CameraKitPath);
        if (prefab == null) { Debug.LogError($"[GiardinoDelleRoseSceneBuilder] Missing {CameraKitPath}"); return; }
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

    static void AddToBuildSettings(string scenePath)
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == scenePath)) return;
        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log($"[GiardinoDelleRoseSceneBuilder] Added {scenePath} to Build Settings.");
    }
}
