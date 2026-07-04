using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Generates Assets/Scenes/Fiesole.unity — the first outside-Florence zone —
// deterministically from code (BattleArenaBuilder precedent). Small hilltop
// village per the zone anatomy checklist: player copied from PonteVecchio (no
// player prefab exists), HD2D_CameraKit, entry points + placer + gate exit,
// colliders on all structures (camera containment), north backdrop, authored
// sun (COZY overrides at runtime). Adds the scene to Build Settings, which is
// what makes the fiesole pin auto-appear on the region map (autohide rule).
//
// Environment prefabs are resolved by NAME SEARCH inside the art packs with
// primitive fallbacks — the builder never hard-fails on a pack path.
public static class FiesoleSceneBuilder
{
    const string ScenePath = "Assets/Scenes/Fiesole.unity";
    const string PlayerSourceScene = "Assets/Scenes/PonteVecchio.unity";
    const string CameraKitPath = "Assets/Prefabs/HD2D_CameraKit.prefab";
    const string ChurchPath = "Assets/Environment/MarketSquare/Buildings/Church1.glb";

    static readonly string[] SlavicFolders = { "Assets/EmaceArt/Slavic World Free/Prefabs" };
    static readonly string[] MarketFolders = { "Assets/Low-Poly Medieval Market/Prefabs" };

    [MenuItem("InfernosCurse/Gugol Mappe/4. Build Fiesole Scene")]
    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SceneManager.SetActiveScene(scene);

        BuildGround();
        BuildStructures();
        BuildDressing();
        BuildBoundaries();
        BuildTravelMarkers();
        BuildLighting();
        CopyPlayerFromPonteVecchio(scene);
        PlaceCameraKit();

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuildSettings(ScenePath);
        Debug.Log("[FiesoleSceneBuilder] Fiesole built, saved, and added to Build Settings — " +
                  "the region pin is now live (autohide rule).");
    }

    // ── Ground ────────────────────────────────────────────────────────────────

    static void BuildGround()
    {
        var group = new GameObject("[Ground]");

        // Hilltop meadow (players walk on this collider).
        var meadow = GameObject.CreatePrimitive(PrimitiveType.Plane);
        meadow.name = "Meadow";
        meadow.transform.SetParent(group.transform, false);
        meadow.transform.localScale = new Vector3(4.6f, 1f, 3.6f);   // 46×36
        Tint(meadow, new Color(0.45f, 0.52f, 0.28f));                // dry Tuscan grass

        // Cobble road spine: gate (south) → piazza (center-north).
        var road = GameObject.CreatePrimitive(PrimitiveType.Plane);
        road.name = "Road";
        Object.DestroyImmediate(road.GetComponent<Collider>());      // meadow is the walk surface
        road.transform.SetParent(group.transform, false);
        road.transform.position = new Vector3(0f, 0.02f, -4f);
        road.transform.localScale = new Vector3(0.5f, 1f, 1.9f);     // 5×19 strip
        Tint(road, new Color(0.52f, 0.48f, 0.42f));

        // The piazza in front of the church.
        var piazza = GameObject.CreatePrimitive(PrimitiveType.Plane);
        piazza.name = "Piazza";
        Object.DestroyImmediate(piazza.GetComponent<Collider>());
        piazza.transform.SetParent(group.transform, false);
        piazza.transform.position = new Vector3(0f, 0.04f, 5.5f);
        piazza.transform.localScale = new Vector3(1.4f, 1f, 1.0f);   // 14×10
        Tint(piazza, new Color(0.58f, 0.53f, 0.45f));
    }

    // ── Structures ───────────────────────────────────────────────────────────

    static void BuildStructures()
    {
        var group = new GameObject("[Buildings]");

        // Church at the head of the piazza, facing south down the road.
        var church = InstantiateAsset(ChurchPath, "Church");
        if (church != null)
        {
            church.transform.SetParent(group.transform, false);
            EnsureCollider(church);
            church.transform.position = new Vector3(0f, 0f, 13f);
            church.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        }

        // Village houses flanking the road — Slavic pack by name, cube fallback.
        var housePositions = new[]
        {
            (new Vector3(-10f, 0f, -6f),  90f), (new Vector3(-11f, 0f,  1f),  90f),
            (new Vector3(-10f, 0f,  8f),  90f), (new Vector3( 10f, 0f, -6f), -90f),
            (new Vector3( 11f, 0f,  1f), -90f), (new Vector3( 10f, 0f,  8f), -90f),
        };
        string[] houseNames = { "EA03_Town_House_Comp_01", "EA03_Town_House_Comp_02",
                                "EA03_Town_House_Comp_03" };
        for (int i = 0; i < housePositions.Length; i++)
        {
            var (pos, yRot) = housePositions[i];
            var house = PlacePackPrefab(houseNames[i % houseNames.Length], SlavicFolders,
                $"House_{i + 1}", group.transform, fallbackSize: new Vector3(6f, 5f, 6f));
            house.transform.position = pos;
            house.transform.rotation = Quaternion.Euler(0f, yRot, 0f);
        }

        // Low stone wall ring along the south edge, gate opening in the middle.
        foreach (var (x, width) in new[] { (-11.5f, 13f), (11.5f, 13f) })
        {
            var wall = PlacePackPrefab("Wall", SlavicFolders, $"SouthWall_{(x < 0 ? "W" : "E")}",
                group.transform, fallbackSize: new Vector3(width, 1.6f, 0.8f));
            wall.transform.position = new Vector3(x, 0f, -14f);
        }
    }

    // ── Dressing (trees, rocks, lamps) ───────────────────────────────────────

    static void BuildDressing()
    {
        var group = new GameObject("[Dressing]");

        var treeSpots = new[]
        {
            new Vector3(-16f, 0f, -10f), new Vector3(-17f, 0f, 3f), new Vector3(-15f, 0f, 13f),
            new Vector3(16f, 0f, -10f),  new Vector3(17f, 0f, 4f),  new Vector3(15f, 0f, 13f),
            new Vector3(-6f, 0f, 15f),   new Vector3(7f, 0f, 15f),  new Vector3(-14f, 0f, -13f),
            new Vector3(14f, 0f, -13f),
        };
        for (int i = 0; i < treeSpots.Length; i++)
        {
            var tree = PlacePackPrefab($"EA03_Nature_Tree_0{(i % 3) + 1}", SlavicFolders,
                $"Tree_{i + 1}", group.transform, fallbackSize: null);
            if (tree != null) tree.transform.position = treeSpots[i];
        }

        // Cliff-rim boulders on the north/east edges — the hilltop impression.
        var rockSpots = new[]
        {
            new Vector3(-19f, 0f, 8f), new Vector3(-19f, 0f, -2f),
            new Vector3(19f, 0f, 10f), new Vector3(19f, 0f, 0f),
            new Vector3(-10f, 0f, 16f), new Vector3(11f, 0f, 16f),
        };
        for (int i = 0; i < rockSpots.Length; i++)
        {
            var rock = PlacePackPrefab("EA03_Environment_Rock", SlavicFolders,
                $"Rock_{i + 1}", group.transform, fallbackSize: new Vector3(2.5f, 1.8f, 2.2f));
            rock.transform.position = rockSpots[i];
            rock.transform.rotation = Quaternion.Euler(0f, i * 57f, 0f);
        }

        foreach (var (pos, n) in new[] { (new Vector3(-6f, 0f, 2f), 1), (new Vector3(6f, 0f, 2f), 2) })
        {
            var lamp = PlacePackPrefab("lamp", MarketFolders, $"LampPost_{n}", group.transform,
                fallbackSize: null);
            if (lamp != null) lamp.transform.position = pos;
        }

        // North backdrop: the far valley, seen past the church by the
        // down-pitched camera. DoF blurs it into a painted distance.
        var backdrop = GameObject.CreatePrimitive(PrimitiveType.Quad);
        backdrop.name = "Backdrop_North";
        Object.DestroyImmediate(backdrop.GetComponent<Collider>());
        backdrop.transform.position = new Vector3(0f, 6f, 24f);
        backdrop.transform.localScale = new Vector3(70f, 18f, 1f);
        Tint(backdrop, new Color(0.62f, 0.70f, 0.72f));   // hazy valley blue-green
    }

    // ── Invisible boundaries ─────────────────────────────────────────────────

    static void BuildBoundaries()
    {
        var group = new GameObject("[Boundaries]");
        foreach (var (name, pos, size) in new[]
        {
            ("Bound_N", new Vector3(0f, 1.5f, 17f),  new Vector3(46f, 3f, 1f)),
            ("Bound_S", new Vector3(0f, 1.5f, -16.5f), new Vector3(46f, 3f, 1f)),
            ("Bound_E", new Vector3(20f, 1.5f, 0f),  new Vector3(1f, 3f, 36f)),
            ("Bound_W", new Vector3(-20f, 1.5f, 0f), new Vector3(1f, 3f, 36f)),
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

        MakeEntry("fiesole_gate", "Town Gate", new Vector3(0f, 0f, -11f), new Vector2(0f, 1f));
        MakeEntry("fiesole_piazza", "The Piazza", new Vector3(0f, 0f, 4f), new Vector2(0f, 1f));
        MakeEntry("fiesole_overlook", "Valley Overlook", new Vector3(-13f, 0f, 9f), new Vector2(-1f, 0f));

        // Walking out the gate opens the Gugol map on the region layer.
        var exit = new GameObject("ExitZone_Gate");
        exit.transform.position = new Vector3(0f, 1f, -15f);
        var exitCol = exit.AddComponent<BoxCollider>();
        exitCol.size = new Vector3(7f, 3f, 2.5f);
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

    // ── Lighting / camera / player ───────────────────────────────────────────

    static void BuildLighting()
    {
        var sun = new GameObject("Directional Light");
        var light = sun.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.95f, 0.84f);
        light.intensity = 1.15f;
        light.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
    }

    static void PlaceCameraKit()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CameraKitPath);
        if (prefab == null) { Debug.LogError($"[FiesoleSceneBuilder] Missing {CameraKitPath}"); return; }
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
            copy.transform.position = new Vector3(0f, source.transform.position.y, -11f);
            Debug.Log($"[FiesoleSceneBuilder] Player '{copy.name}' copied from PonteVecchio.");
        }
        else Debug.LogError("[FiesoleSceneBuilder] No Player-tagged object found in PonteVecchio!");

        EditorSceneManager.CloseScene(pv, true);
    }

    // ── Asset helpers ────────────────────────────────────────────────────────

    // Find a prefab by name inside pack folders; graceful primitive fallback
    // (or null when fallbackSize is null — decor we can simply skip).
    static GameObject PlacePackPrefab(string search, string[] folders, string name,
        Transform parent, Vector3? fallbackSize)
    {
        var guid = AssetDatabase.FindAssets($"{search} t:prefab", folders).FirstOrDefault();
        if (guid != null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (prefab != null)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                go.name = name;
                go.transform.SetParent(parent, false);
                EnsureCollider(go);
                return go;
            }
        }

        if (fallbackSize == null)
        {
            Debug.LogWarning($"[FiesoleSceneBuilder] '{search}' not found — skipped {name}.");
            return null;
        }

        Debug.LogWarning($"[FiesoleSceneBuilder] '{search}' not found — cube placeholder for {name}.");
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name + "_PLACEHOLDER";
        cube.transform.SetParent(parent, false);
        cube.transform.localScale = fallbackSize.Value;
        cube.transform.position = new Vector3(0f, fallbackSize.Value.y * 0.5f, 0f);
        Tint(cube, new Color(0.72f, 0.64f, 0.52f));
        return cube;
    }

    static GameObject InstantiateAsset(string path, string name)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null)
        {
            Debug.LogWarning($"[FiesoleSceneBuilder] Missing asset {path} — skipped {name}.");
            return null;
        }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        go.name = name;
        return go;
    }

    // Camera containment relies on physical colliders — every structure gets one.
    // Computed at identity rotation, so call before rotating the object.
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
        Debug.Log($"[FiesoleSceneBuilder] Added {scenePath} to Build Settings.");
    }
}
