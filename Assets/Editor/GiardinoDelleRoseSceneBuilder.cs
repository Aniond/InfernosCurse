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
        ScatterTrees(terrain);
        ScatterTrailFlora();
        BuildHedgeBlockout();
        BuildMarkers();
        BuildPond();
        BuildFountainWater();
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

        // Real generated seamless tiles (Gemini, Duomo-tile workflow) — the
        // earlier flat solid-color placeholders read as lifeless green soup.
        var meadowLayer = CreateTerrainLayer("TL_Meadow", new Color(0.42f, 0.55f, 0.30f), "ground-grass-meadow", 5f);
        var dryLayer = CreateTerrainLayer("TL_DryGrass", new Color(0.55f, 0.52f, 0.30f), "ground-grass-dry", 5f);
        var pathLayer = CreateTerrainLayer("TL_Gravel", new Color(0.66f, 0.60f, 0.50f), "ground-path-gravel", 3.5f);
        terrain.terrainData.terrainLayers = new[] { meadowLayer, dryLayer, pathLayer };

        var data = terrain.terrainData;
        data.alphamapResolution = 512;
        int res = data.alphamapResolution;
        var alpha = new float[res, res, 3];

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

                // Meadow/dry blend by low-frequency noise — natural tonal
                // patchiness instead of one uniform green.
                float noise = Mathf.PerlinNoise(wx * 0.07f + 13.7f, wz * 0.07f + 7.3f);
                float dryWeight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 0.72f, noise)) * 0.8f;

                float grass = 1f - pathWeight;
                alpha[iz, ix, 0] = grass * (1f - dryWeight);
                alpha[iz, ix, 1] = grass * dryWeight;
                alpha[iz, ix, 2] = pathWeight;
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

    // Builds a TerrainLayer from a generated seamless tile in the asset-gen
    // workbench output (copied into the project), falling back to a flat
    // solid-color texture when the tile hasn't been generated. Never use
    // Texture2D.whiteTexture here — the built-in singleton renders as the
    // missing-texture checker when referenced from a .terrainlayer asset.
    static TerrainLayer CreateTerrainLayer(string name, Color fallbackTint, string generatedTileId, float tileMeters)
    {
        string layerPath = $"{LayerFolder}/{name}.terrainlayer";
        var existing = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
        if (existing != null) return existing;

        string texPath = $"{LayerFolder}/{name}_Diffuse.png";
        string sourceTile = $"Tools/asset-gen/output/images/{generatedTileId}.png";
        if (!string.IsNullOrEmpty(generatedTileId) && System.IO.File.Exists(sourceTile))
        {
            System.IO.File.Copy(sourceTile, texPath, overwrite: true);
        }
        else
        {
            Debug.LogWarning($"[GiardinoDelleRoseSceneBuilder] Tile '{generatedTileId}' not generated — flat color fallback for {name}.");
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = fallbackTint;
            tex.SetPixels(pixels);
            tex.Apply();
            System.IO.File.WriteAllBytes(texPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
        AssetDatabase.ImportAsset(texPath);
        var savedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

        var layer = new TerrainLayer { diffuseTexture = savedTex, tileSize = new Vector2(tileMeters, tileMeters) };
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

        // Trees are NOT terrain trees: the generated Prism GLBs have a
        // center pivot and Unity's tree-prototype path rejected the old pack
        // prefab outright ("no valid mesh renderer" — trees rendered invisible).
        // ScatterTrees places them as plain GameObjects instead, same idiom as
        // the hero props.

        // Detail grass kept SPARSE — the ground texture carries the green now;
        // dense swaying detail over it read as "animated flowing soup" (David).
        const string grassPatchPath = "Packages/xyz.staggart-creations.stylized-grass/Prefabs/TerrainGrass/GrassPatch_Terrain.prefab";
        var grassPatch = AssetDatabase.LoadAssetAtPath<GameObject>(grassPatchPath);
        if (grassPatch != null)
        {
            spawner.grassPrefabs.Add(new VegetationSpawner.GrassPrefab
            {
                prefab = grassPatch,
                type = VegetationSpawner.GrassType.Mesh,
                probability = 35f,
                slopeRange = new Vector2(0f, 40f),
            });
        }

        const string dandelionsPath = "Packages/xyz.staggart-creations.stylized-grass/Prefabs/TerrainFlowers/Flower_Dandelions_Terrain.prefab";
        var dandelions = AssetDatabase.LoadAssetAtPath<GameObject>(dandelionsPath);
        if (dandelions != null)
        {
            spawner.grassPrefabs.Add(new VegetationSpawner.GrassPrefab
            {
                prefab = dandelions,
                type = VegetationSpawner.GrassType.Mesh,
                probability = 12f,
                slopeRange = new Vector2(0f, 30f),
            });
        }

        spawner.RefreshTreePrefabs();
        spawner.RefreshGrassPrototypes();
        spawner.Respawn(grass: true, trees: true);
    }

    // Actual (heightmap-discretized) ground height — use this for grounding
    // objects; HeightAt is the analytic design surface and sits up to ~15cm
    // above the sampled terrain on slopes.
    static float SampleGroundY(float x, float z)
    {
        var t = Terrain.activeTerrain;
        if (t == null) return HeightAt(x, z);
        return t.SampleHeight(new Vector3(x, 0f, z)) + t.transform.position.y;
    }

    // Raise-only heightmap shelf: lifts every cell inside the world-space
    // rect (center, halfExtents) to at least targetY, with a one-cell
    // feathered ring. Never lowers terrain.
    static void RaiseShelf(Terrain terrain, Vector2 center, Vector2 halfExtents, float targetY)
    {
        if (terrain == null) return;
        var data = terrain.terrainData;
        int res = data.heightmapResolution;
        Vector3 tPos = terrain.transform.position;
        float targetNorm = Mathf.Clamp01((targetY - tPos.y) / data.size.y);

        int x0 = Mathf.Clamp(Mathf.FloorToInt((center.x - halfExtents.x - tPos.x) / data.size.x * (res - 1)) - 1, 0, res - 1);
        int x1 = Mathf.Clamp(Mathf.CeilToInt((center.x + halfExtents.x - tPos.x) / data.size.x * (res - 1)) + 1, 0, res - 1);
        int z0 = Mathf.Clamp(Mathf.FloorToInt((center.y - halfExtents.y - tPos.z) / data.size.z * (res - 1)) - 1, 0, res - 1);
        int z1 = Mathf.Clamp(Mathf.CeilToInt((center.y + halfExtents.y - tPos.z) / data.size.z * (res - 1)) + 1, 0, res - 1);
        if (x1 <= x0 || z1 <= z0) return;

        var h = data.GetHeights(x0, z0, x1 - x0 + 1, z1 - z0 + 1);
        for (int iz = 0; iz <= z1 - z0; iz++)
            for (int ix = 0; ix <= x1 - x0; ix++)
            {
                float wx = tPos.x + (x0 + ix) / (float)(res - 1) * data.size.x;
                float wz = tPos.z + (z0 + iz) / (float)(res - 1) * data.size.z;
                bool inside = Mathf.Abs(wx - center.x) <= halfExtents.x && Mathf.Abs(wz - center.y) <= halfExtents.y;
                float goal = inside ? targetNorm : (h[iz, ix] + targetNorm) * 0.5f; // feather ring
                if (goal > h[iz, ix]) h[iz, ix] = goal;
            }
        data.SetHeights(x0, z0, h);
    }

    // ── Trees (generated Prism 3.1 GLBs, placed as GameObjects) ─────────────

    const string PropsFolder = "Assets/Environment/GiardinoDelleRose/Props";

    // Wrapper prefab = base pivot + capsule collider around a unit-height,
    // center-pivot GLB. Rebuilt from the GLB if the prefab asset is missing.
    static GameObject EnsureTreePrefab(string glbId, string prefabName, float height, float colRadius)
    {
        string prefabPath = $"{PropsFolder}/{prefabName}.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (existing != null) return existing;

        var glb = AssetDatabase.LoadAssetAtPath<GameObject>($"{PropsFolder}/{glbId}.glb");
        if (glb == null)
        {
            Debug.LogWarning($"[GiardinoDelleRoseSceneBuilder] {glbId}.glb missing — generate it in the asset workbench first.");
            return null;
        }

        var root = new GameObject(prefabName);
        var mesh = (GameObject)PrefabUtility.InstantiatePrefab(glb);
        mesh.name = glbId + "_mesh";
        mesh.transform.SetParent(root.transform, false);
        mesh.transform.localScale = Vector3.one * height;
        mesh.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);

        var cap = root.AddComponent<CapsuleCollider>();
        cap.center = new Vector3(0f, height * 0.5f, 0f);
        cap.height = height;
        cap.radius = colRadius;

        var saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        return saved;
    }

    static void ScatterTrees(Terrain terrain)
    {
        var cypress = EnsureTreePrefab("tuscan-cypress", "Tree_TuscanCypress", 6f, 0.5f);
        var pine = EnsureTreePrefab("italian-stone-pine", "Tree_StonePine", 7.5f, 0.45f);
        if (cypress == null || pine == null) return;

        var group = new GameObject("[Trees]");
        var rng = new System.Random(1299); // deterministic rebuilds
        int placed = 0;

        void Place(GameObject prefab, float x, float z, float scaleJitter)
        {
            float y = terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetParent(group.transform, false);
            go.transform.position = new Vector3(x, y, z);
            go.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            float s = 1f + ((float)rng.NextDouble() * 2f - 1f) * scaleJitter;
            go.transform.localScale = new Vector3(s, s, s);
            placed++;
        }

        // Via road line — staggered cypress rows flanking the hilltop road
        // (z=20, w=4), clear of the overlook pad (0,24) and the trail gap at x~0.
        foreach (float x in new[] { -30f, -24f, -18f, -12f, -6f, 6f, 12f, 18f, 24f, 30f })
            Place(cypress, x, 17.6f, 0.12f);
        foreach (float x in new[] { -27f, -21f, -15f, -9f, 9f, 15f, 21f, 27f })
            Place(cypress, x, 22.4f, 0.12f);

        // Fan terraces — stone pines scattered on the lower slopes, off-trail.
        var pineSpots = new Vector2[]
        {
            new Vector2(-11f, -25f), new Vector2(-19f, -21f), new Vector2(-27f, -14f),
            new Vector2(-30f, -4f), new Vector2(-22f, 5f),
            new Vector2(9f, -20f), new Vector2(15f, -26f), new Vector2(22f, -16f),
            new Vector2(28f, -7f),
        };
        foreach (var p in pineSpots) Place(pine, p.x, p.y, 0.15f);

        Debug.Log($"[GiardinoDelleRoseSceneBuilder] Trees: {placed} placed.");
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
                // Sample the REAL terrain, not the analytic MidY — the
                // discretized heightmap sits slightly below it and the
                // hedges read as hovering (David, 7/05). Perimeter hedges
                // straddle the terrace rim, so take the HIGHEST ground along
                // the segment, never the bank below it. Sunk 5cm.
                Vector2 a2 = from + dir * (segLen * i);
                Vector2 b2 = from + dir * (segLen * (i + 1));
                float gy = Mathf.Max(SampleGroundY(c2.x, c2.y),
                    Mathf.Max(SampleGroundY(a2.x, a2.y), SampleGroundY(b2.x, b2.y)));
                seg.transform.position = new Vector3(c2.x, gy + 0.55f - 0.05f, c2.y);
                seg.transform.localScale = new Vector3(segLen, 1.1f, 0.7f);
                seg.transform.rotation = Quaternion.LookRotation(new Vector3(-dir.y, 0f, dir.x), Vector3.up);
                Tint(seg, new Color(0.24f, 0.38f, 0.18f));

                // Rim segments overhang the terrace bank (downhill gaps up to
                // 4m at the corners) — raise a retaining berm under the
                // footprint so the hedge never floats. Raise-only.
                RaiseShelf(Terrain.activeTerrain,
                    new Vector2(c2.x, c2.y), Mathf.Abs(dir.x) > 0.5f
                        ? new Vector2(segLen / 2f + 0.25f, 0.35f + 0.25f)
                        : new Vector2(0.35f + 0.25f, segLen / 2f + 0.25f),
                    gy - 0.01f);
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
        // Centerpiece — taller than the player (David 7/05): it should command
        // the garden, not disappear behind the rose rows.
        M("garden-fountain-basin", 2.2f, new Vector3(0f, MidY, 2f));
        M("garden-stone-bench", 0.9f, new Vector3(-5f, MidY, 5.5f), 125f);   // faces fountain
        M("garden-stone-bench", 0.9f, new Vector3(5f, MidY, -1.5f), -55f);   // faces fountain
        M("shrine-tabernacolo", 2.2f, new Vector3(-2.2f, MidY, -11f), 20f);  // south entrance

        // Quadrant rose beds — planted in ROWS FILLING THE BED (David's photo
        // reference: real rose gardens line them up in batches), one variety
        // per quadrant like a classic quadripartite garden. The wine climbers
        // (built-in trellis stakes) read as a pillar-rose bed.
        var quadrants = new (Vector2 center, string rose)[]
        {
            (new Vector2(-6.5f, 7.5f), "rose-bush-ivory"),
            (new Vector2(6.5f, 7.5f), "rose-bush-crimson"),
            (new Vector2(-6.5f, -3.5f), "rose-bush-gold"),
            (new Vector2(6.5f, -3.5f), "rose-climbing-wine"),
        };
        foreach (var (qc, rose) in quadrants)
            for (int row = -1; row <= 1; row++)               // 3 rows
                for (int col = 0; col < 4; col++)             // 4 per row
                {
                    float dx = -3.3f + col * 2.2f;
                    float dz = row * 2.2f;
                    M(rose, Random.Range(0.82f, 0.98f),
                      new Vector3(qc.x + dx, MidY, qc.y + dz), Random.Range(-14f, 14f));
                }

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

        // Fan terrace beds along the west loop trail — row batches beside the
        // path (2 rows of 4, one color per row), same lined-up planting.
        var fanBeds = new (Vector2 origin, Vector2 rowDir, string roseA, string roseB)[]
        {
            (new Vector2(-8f, -20f), new Vector2(-0.97f, -0.22f), "rose-bush-crimson", "rose-bush-ivory"),
            (new Vector2(-19f, -13f), new Vector2(-0.71f, 0.71f), "rose-bush-gold", "rose-bush-crimson"),
            (new Vector2(-24f, -4f), new Vector2(0.71f, 0.71f), "rose-bush-ivory", "rose-bush-gold"),
        };
        foreach (var (origin, rowDir, roseA, roseB) in fanBeds)
        {
            Vector2 normal = new Vector2(-rowDir.y, rowDir.x);
            for (int row = 0; row < 2; row++)
                for (int col = 0; col < 4; col++)
                {
                    Vector2 p = origin + rowDir * (col * 1.9f) + normal * (row * 2.1f);
                    string rose = row == 0 ? roseA : roseB;
                    M(rose, Random.Range(0.82f, 1.05f),
                      new Vector3(p.x, HeightAt(p.x, p.y), p.y), Random.Range(-14f, 14f));
                }
        }

        // West fan tip — the worn Roman marble, half lost in the growth.
        M("ancient-marble-figure", 1.8f, new Vector3(-28f, LowerY, -16f), 140f);

        // Overlook bench, facing south over the city.
        M("garden-stone-bench", 0.9f, new Vector3(3f, UpperY, 24.5f), 180f);
    }

    // ── Trail-edge flowers (David's photo: blooms crowding the path) ────────

    static void ScatterTrailFlora()
    {
        string[] flowerPaths =
        {
            "Packages/xyz.staggart-creations.stylized-grass/Prefabs/Flowers/Flower_Aster.prefab",
            "Packages/xyz.staggart-creations.stylized-grass/Prefabs/Flowers/Flower_Chamomile.prefab",
            "Packages/xyz.staggart-creations.stylized-grass/Prefabs/Flowers/Groundcover_Poppies.prefab",
            "Packages/xyz.staggart-creations.stylized-grass/Prefabs/Flowers/Groundcover_Daisies.prefab",
        };
        var flowers = flowerPaths
            .Select(p => AssetDatabase.LoadAssetAtPath<GameObject>(p))
            .Where(p => p != null)
            .ToArray();
        if (flowers.Length == 0) { Debug.LogWarning("[GiardinoDelleRoseSceneBuilder] No flower prefabs found — trail flora skipped."); return; }

        var group = new GameObject("[TrailFlora]");
        var rng = new System.Random(1299); // deterministic rebuilds
        int count = 0;

        foreach (var t in Trails)
        {
            for (int i = 0; i < t.pts.Length - 1; i++)
            {
                Vector2 a = t.pts[i], b = t.pts[i + 1];
                float segLen = Vector2.Distance(a, b);
                Vector2 dir = (b - a) / Mathf.Max(0.001f, segLen);
                Vector2 normal = new Vector2(-dir.y, dir.x);

                for (float s = 2f; s < segLen; s += 3.5f)
                {
                    // Alternate sides, jittered offset just past the path edge.
                    float side = (count % 2 == 0) ? 1f : -1f;
                    float off = t.width / 2f + 0.9f + (float)rng.NextDouble() * 1.3f;
                    Vector2 p2 = a + dir * s + normal * side * off;

                    // Keep flora out of the formal garden interior (the beds
                    // are the roses' show) and inside the terrain.
                    if (p2.x > GardenMin.x && p2.x < GardenMax.x && p2.y > GardenMin.y && p2.y < GardenMax.y) { count++; continue; }
                    if (Mathf.Abs(p2.x) > TerrainWidth / 2f - 2f || Mathf.Abs(p2.y) > TerrainLength / 2f - 2f) { count++; continue; }

                    var prefab = flowers[rng.Next(flowers.Length)];
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    go.transform.SetParent(group.transform, false);
                    go.transform.position = new Vector3(p2.x, SampleGroundY(p2.x, p2.y), p2.y);
                    go.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                    float sc = 0.8f + (float)rng.NextDouble() * 0.6f;
                    go.transform.localScale = new Vector3(sc, sc, sc);
                    count++;
                }
            }
        }
        Debug.Log($"[GiardinoDelleRoseSceneBuilder] Trail flora: {group.transform.childCount} flower clumps placed.");
    }

    // ── Fountain water (small disk inside the basin) ─────────────────────────

    static void BuildFountainWater()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Stylized Water 3/Materials/StylizedWater3_Clear.mat");
        if (mat == null) return;
        // Sized/heighted for the 2.2m basin (was 1.6f disk at +0.78 for 1.2m).
        var mesh = StylizedWater3.WaterMesh.Create(StylizedWater3.WaterMesh.Shape.Disk, 2.9f, 0.2f);
        var wo = StylizedWater3.WaterObject.New(mat, mesh);
        wo.gameObject.name = "FountainWater";
        wo.transform.position = new Vector3(GardenCenter.x,
            SampleGroundY(GardenCenter.x, GardenCenter.y) + 1.43f, GardenCenter.y);
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
