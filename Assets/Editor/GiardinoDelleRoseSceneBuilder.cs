using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using sc.terrain.proceduralpainter;
using sc.terrain.vegetationspawner;

// Generates Assets/Scenes/GiardinoDelleRose.unity — the first zone built on the
// new Terrain pipeline (Stylized Water 3, Stylized Grass Shader, Vegetation
// Spawner, Procedural Terrain Painter) rather than the hand-built flat-plane
// approach used for Duomo/Mercato/Ponte Vecchio. Three stepped terraces rising
// from the city-facing gate to the Florist's overlook. Deterministic,
// idempotent-safe (re-running replaces the saved scene file), same builder
// shape as FiesoleSceneBuilder.
//
// Design spec: docs/superpowers/specs/2026-07-05-giardino-delle-rose-design.md
public static class GiardinoDelleRoseSceneBuilder
{
    const string ScenePath = "Assets/Scenes/GiardinoDelleRose.unity";
    const string PlayerSourceScene = "Assets/Scenes/PonteVecchio.unity";
    const string CameraKitPath = "Assets/Prefabs/HD2D_CameraKit.prefab";
    const string LayerFolder = "Assets/Environment/GiardinoDelleRose/TerrainLayers";

    // Terrace heights (world Y). Each terrace is a flat plateau; slopes between
    // them are the only steep ground, which is exactly what height+slope-based
    // Terrain Painter rules key off.
    const float LowerY = 0f;
    const float MidY = 6f;
    const float UpperY = 12f;
    const float TerrainWidth = 60f;
    const float TerrainLength = 60f;
    const float TerrainHeight = 20f; // max heightmap height, must exceed UpperY

    [MenuItem("InfernosCurse/Giardino delle Rose/1. Build Scene")]
    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SceneManager.SetActiveScene(scene);

        var terrain = BuildTerrain();
        PaintTerrainLayers(terrain);
        SpawnVegetation(terrain);
        BuildFountain();
        BuildBoundaries(terrain);
        BuildTravelMarkers();
        BuildFloristPlaceholder();
        BuildLighting();
        CopyPlayerFromPonteVecchio(scene);
        PlaceCameraKit();

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuildSettings(ScenePath);
        Debug.Log("[GiardinoDelleRoseSceneBuilder] Scene built and saved. Run " +
                  "'InfernosCurse/Giardino delle Rose/2. Register Map Node' next to " +
                  "wire the HubNode and make the pin live.");
    }

    // ── Terrain ───────────────────────────────────────────────────────────────

    static Terrain BuildTerrain()
    {
        var data = new TerrainData
        {
            heightmapResolution = 257,
            size = new Vector3(TerrainWidth, TerrainHeight, TerrainLength),
        };
        // Required for VegetationSpawner's grass detail-mesh placement —
        // TerrainData defaults to 0 detail resolution, which silently drops
        // every detail layer ("Terrain has zero detail resolution"). Both
        // resolution properties are read-only; must go through this method.
        data.SetDetailResolution(512, 16);

        // Three flat plateaus connected by ramps, built directly into the
        // heightmap array (resolution 257 -> 257x257 height samples, 0..1
        // normalized against TerrainHeight).
        int res = data.heightmapResolution;
        var heights = new float[res, res];
        float lowerN = LowerY / TerrainHeight;
        float midN = MidY / TerrainHeight;
        float upperN = UpperY / TerrainHeight;

        for (int z = 0; z < res; z++)
        {
            // z runs south (gate, low) -> north (overlook, high).
            float t = z / (float)(res - 1); // 0 at gate, 1 at overlook
            float h;
            if (t < 0.28f) h = lowerN;
            else if (t < 0.38f) h = Mathf.Lerp(lowerN, midN, (t - 0.28f) / 0.10f); // ramp 1
            else if (t < 0.62f) h = midN;
            else if (t < 0.72f) h = Mathf.Lerp(midN, upperN, (t - 0.62f) / 0.10f); // ramp 2
            else h = upperN;

            for (int x = 0; x < res; x++)
                heights[z, x] = h;
        }
        data.SetHeights(0, 0, heights);

        var terrainGO = Terrain.CreateTerrainGameObject(data);
        terrainGO.name = "Terrain_GiardinoDelleRose";
        terrainGO.transform.position = new Vector3(-TerrainWidth / 2f, 0f, -TerrainLength / 2f);

        return terrainGO.GetComponent<Terrain>();
    }

    // ── Terrain texturing ────────────────────────────────────────────────────
    //
    // KNOWN ISSUE (2026-07-05): Procedural Terrain Painter's multi-layer paint
    // does not blend correctly in this project — verified via isolated tests
    // (two layers with mutually-exclusive height/slope rules; whichever layer
    // is added SECOND always wins at full weight everywhere, regardless of its
    // rule or the first layer's rule). Root cause not resolved — plausibly a
    // TerrainPaintUtility.BeginPaintTexture/EndPaintTexture renormalization
    // quirk specific to this package's sequential single-layer painting.
    // David's call: ship single-layer grass now, revisit ramp/path texture
    // variation later (either a Staggart Creations bug report, or hand-rolled
    // TerrainData.SetAlphamaps painting done directly in C#).
    static void PaintTerrainLayers(Terrain terrain)
    {
        if (!AssetDatabase.IsValidFolder(LayerFolder))
            CreateFolderRecursive(LayerFolder);

        var grassLayer = CreateTerrainLayer("TL_Garden_Grass", new Color(0.42f, 0.55f, 0.30f));

        var painter = terrain.gameObject.AddComponent<TerrainPainter>();
        painter.terrains = new[] { terrain };
        painter.splatmapResolution = 512;
        painter.RecalculateBounds();

        painter.CreateSettingsForLayer(grassLayer);
        painter.layerSettings.Last().modifierStack = new System.Collections.Generic.List<Modifier>();

        painter.SetTerrainLayers();
        painter.RepaintAll();
        painter.FinalizeChanges();
        painter.Dispose();
    }

    static TerrainLayer CreateTerrainLayer(string name, Color tint)
    {
        string layerPath = $"{LayerFolder}/{name}.terrainlayer";
        var existing = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
        if (existing != null) return existing;

        // A real saved Texture2D asset — Texture2D.whiteTexture is a shared
        // built-in singleton that does NOT survive being referenced from a
        // serialized .terrainlayer asset correctly (renders as the missing-
        // texture checker at runtime). Placeholder solid-color tile until a
        // proper ground texture is sourced/generated for this zone.
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

        var layer = new TerrainLayer
        {
            diffuseTexture = savedTex,
            tileSize = new Vector2(8f, 8f),
        };
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
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    // ── Vegetation (rose beds + background trees) ─────────────────────────────

    static void SpawnVegetation(Terrain terrain)
    {
        var spawner = terrain.gameObject.AddComponent<VegetationSpawner>();
        spawner.terrains = new System.Collections.Generic.List<Terrain> { terrain };
        spawner.waterHeight = LowerY - 1f; // no water below ground level yet (fountain is a separate object, not terrain-integrated)

        // Background trees on the upper/mid terraces, avoiding steep slopes.
        var cypress = FindPackPrefab("EA03_Nature_Tree", new[] { "Assets/EmaceArt/Slavic World Free/Prefabs" });
        if (cypress != null)
        {
            var tree = VegetationSpawner.TreeType.New(cypress);
            tree.probability = 35f;
            tree.slopeRange = new Vector2(0f, 15f);
            // heightRange compares against WORLD-SPACE height (meters), not
            // normalized 0-1 — VegetationSpawner.Trees.cs checks worldHeight
            // straight from Terrain.SampleHeight, no normalization.
            tree.heightRange = new Vector2(MidY - 1f, UpperY + 2f);
            tree.distance = 5f;
            tree.collisionCheck = true;
            spawner.treeTypes.Add(tree);
        }
        else
        {
            Debug.LogWarning("[GiardinoDelleRoseSceneBuilder] No tree prefab found for background planting — skipped.");
        }

        // Stylized Grass Shader's ready-made terrain-detail prefab — mesh +
        // material already wired for Vegetation Spawner's detail-mesh system.
        const string grassPatchPath = "Packages/xyz.staggart-creations.stylized-grass/Prefabs/TerrainGrass/GrassPatch_Terrain.prefab";
        var grassPatch = AssetDatabase.LoadAssetAtPath<GameObject>(grassPatchPath);
        if (grassPatch != null)
        {
            var grass = new VegetationSpawner.GrassPrefab
            {
                prefab = grassPatch,
                type = VegetationSpawner.GrassType.Mesh,
                probability = 85f,
                slopeRange = new Vector2(0f, 45f),
            };
            spawner.grassPrefabs.Add(grass);
        }
        else
        {
            Debug.LogWarning($"[GiardinoDelleRoseSceneBuilder] Missing {grassPatchPath} — grass detail skipped.");
        }

        spawner.RefreshTreePrefabs();
        spawner.RefreshGrassPrototypes();
        spawner.Respawn(grass: true, trees: true);

        // Rose beds are hand-placed hero elements (not procedural scatter) so
        // they read as deliberate garden beds rather than randomized shrubs —
        // placeholder markers here; swap for real rose-bed props once
        // generated via the 3D AI Studio pipeline (step 7 of the design spec).
        var bedsGroup = new GameObject("[RoseBeds_PLACEHOLDER]");
        var bedSpots = new[]
        {
            new Vector3(-8f, LowerY, -TerrainLength * 0.32f), new Vector3(8f, LowerY, -TerrainLength * 0.32f),
            new Vector3(-10f, MidY, 0f), new Vector3(10f, MidY, 0f),
        };
        foreach (var pos in bedSpots)
        {
            var bed = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bed.name = "RoseBed_PLACEHOLDER";
            bed.transform.SetParent(bedsGroup.transform, false);
            bed.transform.position = pos + Vector3.up * 0.3f;
            bed.transform.localScale = new Vector3(2.2f, 0.6f, 1.8f);
            Tint(bed, new Color(0.62f, 0.14f, 0.16f));
        }
    }

    static GameObject FindPackPrefab(string search, string[] folders)
    {
        var guid = AssetDatabase.FindAssets($"{search} t:prefab", folders).FirstOrDefault();
        return guid != null ? AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)) : null;
    }

    // ── Fountain (Stylized Water 3) ─────────────────────────────────────────────

    static void BuildFountain()
    {
        const string prefabPath = "Assets/Stylized Water 3/Prefabs/StylizedWater_Toon.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[GiardinoDelleRoseSceneBuilder] Missing {prefabPath} — fountain skipped.");
            return;
        }

        var group = new GameObject("[Fountain]");
        group.transform.position = new Vector3(0f, MidY + 0.05f, 0f);

        var basin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        basin.name = "FountainBasin_PLACEHOLDER";
        basin.transform.SetParent(group.transform, false);
        basin.transform.localScale = new Vector3(4f, 0.3f, 4f);
        Tint(basin, new Color(0.7f, 0.68f, 0.62f));
        EnsureCollider(basin);

        var water = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        water.name = "FountainWater";
        water.transform.SetParent(group.transform, false);
        water.transform.localPosition = new Vector3(0f, 0.2f, 0f);
        water.transform.localScale = Vector3.one * 0.35f; // fit within the basin
    }

    // ── Boundaries ───────────────────────────────────────────────────────────

    static void BuildBoundaries(Terrain terrain)
    {
        var group = new GameObject("[Boundaries]");
        // Terrain already carries a TerrainCollider (auto-added by
        // CreateTerrainGameObject) for the walkable surface itself — these
        // walls just close off the east/west/north/south edges so the
        // camera-containment convention (solid colliders) holds on a
        // non-rectangular terraced layout same as Fiesole/Ponte Vecchio.
        foreach (var (name, pos, size) in new[]
        {
            ("Bound_S", new Vector3(0f, 3f, -TerrainLength / 2f - 1f), new Vector3(TerrainWidth, 6f, 1f)),
            ("Bound_N", new Vector3(0f, UpperY + 4f, TerrainLength / 2f + 1f), new Vector3(TerrainWidth, 8f, 1f)),
            ("Bound_E", new Vector3(TerrainWidth / 2f + 1f, 6f, 0f), new Vector3(1f, 12f, TerrainLength)),
            ("Bound_W", new Vector3(-TerrainWidth / 2f - 1f, 6f, 0f), new Vector3(1f, 12f, TerrainLength)),
        })
        {
            var go = new GameObject(name);
            go.transform.SetParent(group.transform, false);
            go.transform.position = pos;
            var col = go.AddComponent<BoxCollider>();
            col.size = size;
        }
    }

    // ── Travel markers ───────────────────────────────────────────────────────

    static void BuildTravelMarkers()
    {
        var placer = new GameObject("[ZoneEntryPlacer]");
        placer.AddComponent<ZoneEntryPlacer>();

        MakeEntry("giardino_gate", "Garden Gate", new Vector3(0f, LowerY, -TerrainLength * 0.42f), new Vector2(0f, 1f));
        MakeEntry("giardino_fountain", "The Fountain", new Vector3(0f, MidY, 0f), new Vector2(0f, 1f));
        MakeEntry("giardino_overlook", "Florist's Overlook", new Vector3(0f, UpperY, TerrainLength * 0.34f), new Vector2(0f, -1f));

        var exit = new GameObject("ExitZone_Gate");
        exit.transform.position = new Vector3(0f, LowerY + 1f, -TerrainLength * 0.46f);
        var exitCol = exit.AddComponent<BoxCollider>();
        exitCol.size = new Vector3(8f, 3f, 2.5f);
        var zx = exit.AddComponent<ZoneExit>();
        zx.mode = ZoneExit.ExitMode.ToWorldMap;
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

    // ── Florist NPC placeholder ──────────────────────────────────────────────

    static void BuildFloristPlaceholder()
    {
        // Present in the scene from the start but inactive until the
        // (not-yet-designed) quest-complete flag is set — see design spec
        // "Unlock flow". FloristUnlockGate is a minimal placeholder MonoBehaviour
        // (Assets/Scripts/Quests/FloristUnlockGate.cs) that just checks a bool
        // flag on Awake and SetActive(false)s itself if not yet met.
        var npc = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        npc.name = "NPC_Florist_PLACEHOLDER";
        npc.transform.position = new Vector3(4f, UpperY, TerrainLength * 0.36f);
        npc.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
        Tint(npc, new Color(0.55f, 0.32f, 0.55f));
        npc.AddComponent<FloristUnlockGate>();
    }

    // ── Lighting / camera / player ───────────────────────────────────────────

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

    static void PlaceCameraKit()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CameraKitPath);
        if (prefab == null) { Debug.LogError($"[GiardinoDelleRoseSceneBuilder] Missing {CameraKitPath}"); return; }
        PrefabUtility.InstantiatePrefab(prefab);
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
            copy.transform.position = new Vector3(0f, LowerY + source.transform.position.y, -TerrainLength * 0.42f + 2f);
            Debug.Log($"[GiardinoDelleRoseSceneBuilder] Player '{copy.name}' copied from PonteVecchio.");
        }
        else Debug.LogError("[GiardinoDelleRoseSceneBuilder] No Player-tagged object found in PonteVecchio!");

        EditorSceneManager.CloseScene(pv, true);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static void EnsureCollider(GameObject go)
    {
        if (go.GetComponentInChildren<Collider>() != null) return;
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        var bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);

        var col = go.AddComponent<BoxCollider>();
        col.center = go.transform.InverseTransformPoint(bounds.center);
        var scale = go.transform.lossyScale;
        col.size = new Vector3(
            bounds.size.x / Mathf.Max(0.001f, scale.x),
            bounds.size.y / Mathf.Max(0.001f, scale.y),
            bounds.size.z / Mathf.Max(0.001f, scale.z));
    }

    static void Tint(GameObject go, Color color)
    {
        var renderer = go.GetComponent<Renderer>();
        if (renderer == null) return;
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        var mat = new Material(shader) { color = color };
        renderer.sharedMaterial = mat;
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
