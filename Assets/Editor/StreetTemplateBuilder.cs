using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Street kit (Persona-5 idiom, HD-2D rig) per
// Docs/superpowers/specs/2026-07-06-content-templates.md:
// a straight east-west street prefab — tall shop-houses north (camera-facing
// facades), low botteghe south (camera sees over), ShopDoor entrances both
// sides, skyline caps, entries/exits at the ends. Duplicate-and-dress:
// menu 2 stamps a fresh street scene; swap slot buildings, set door targets.
public static class StreetTemplateBuilder
{
    const string PrefabPath = "Assets/Prefabs/Templates/Street_EW_Template.prefab";
    const string ScenePath = "Assets/Scenes/NewStreet.unity";
    const string PlayerSourceScene = "Assets/Scenes/SaloneDelleArti.unity";
    const string CameraKitPath = "Assets/Prefabs/HD2D_CameraKit.prefab";
    const string SkylineMatPath = "Assets/Environment/PiazzaSignoria/Materials/Gen_Backdrop_Skyline.mat";
    const string FrictionlessPath = "Assets/Environment/PiazzaSignoria/Frictionless.physicMaterial";

    // Street runs along X (-30..30); walkable Z -5..+5; north wall line z=5.6.
    static readonly float[] SlotX = { -24f, -12f, 0f, 12f, 24f };

    static readonly Color Paving = new Color(0.56f, 0.53f, 0.47f);
    static readonly Color Timber = new Color(0.45f, 0.33f, 0.20f);
    static readonly Color Plaster = new Color(0.72f, 0.66f, 0.56f);
    static readonly Color PlasterAlt = new Color(0.66f, 0.58f, 0.48f);

    [MenuItem("InfernosCurse/Templates/1. Build Street Template Prefab")]
    public static void BuildTemplatePrefab()
    {
        var root = new GameObject("Street_EW_Template");
        try
        {
            BuildContents(root.transform);
            ApplyFrictionless(root);
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Templates"))
                AssetDatabase.CreateFolder("Assets/Prefabs", "Templates");
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log($"[StreetTemplateBuilder] Saved {PrefabPath}. " +
                      "Run 'Templates/2. New Street Scene From Template' to use it.");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    static void BuildContents(Transform root)
    {
        // ── Ground ────────────────────────────────────────────────────────────
        var ground = Group(root, "[Ground]");
        Box(ground, "Street_Outskirts", new Vector3(0f, -0.19f, 0f), new Vector3(90f, 0.3f, 60f),
            new Color(0.45f, 0.41f, 0.35f));
        Box(ground, "Street_Paving", new Vector3(0f, -0.15f, 0f), new Vector3(64f, 0.3f, 14f), Paving);

        // ── North row: tall shop-houses in swappable slots ────────────────────
        var north = Group(root, "[NorthRow]");
        for (int i = 0; i < SlotX.Length; i++)
        {
            var slot = new GameObject($"SLOT_N{i + 1}").transform;
            slot.SetParent(north, false);
            slot.position = new Vector3(SlotX[i], 0f, 7.5f);

            bool wide = i % 2 == 1;   // alternate narrow plaster / wide brick fronts
            string path = wide
                ? "Assets/Environment/MarketSquare/Buildings/Apartment1.glb"
                : "Assets/Environment/MarketSquare/Buildings/Apartment_NE.glb";
            float yRot = wide ? 270f : 180f;
            float boundsFrontOffset = wide ? 2.7f : 2.43f;   // measured (Signoria)
            float posZ = 5.0f + boundsFrontOffset;           // bounds front at z=5.0, walls ~5.6

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { Debug.LogError($"[StreetTemplateBuilder] Missing {path}"); continue; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = $"Bldg_N{i + 1}";
            go.transform.SetParent(slot, false);
            go.transform.rotation = Quaternion.Euler(0f, yRot, 0f);
            go.transform.localScale = Vector3.one * 8.5f;
            go.transform.position = new Vector3(SlotX[i], 0f, posZ);
            ReSeat(go, new Vector3(SlotX[i], 0f, posZ));
            EnsureCollider(go);

            MakeDoor(north, $"DOOR_N{i + 1}", new Vector3(SlotX[i], 0f, 4.9f), $"shop_n{i + 1}");
        }

        // ── South row: low botteghe (camera sees over them) ───────────────────
        var south = Group(root, "[SouthRow]");
        for (int i = 0; i < SlotX.Length; i++)
        {
            float x = SlotX[i];
            var shop = Box(south, $"Bottega_S{i + 1}", new Vector3(x, 2.1f, -7.5f),
                new Vector3(9f, 4.2f, 4f), i % 2 == 0 ? Plaster : PlasterAlt);
            Box(south, $"Bottega_S{i + 1}_Awning", new Vector3(x, 3.15f, -5.05f),
                new Vector3(9.4f, 0.14f, 1.5f), Timber);
            // Door recess on the street-facing (north) face — Quad faces -Z, flip 180
            var doorVis = GameObject.CreatePrimitive(PrimitiveType.Quad);
            doorVis.name = $"Bottega_S{i + 1}_Door";
            doorVis.transform.SetParent(south, false);
            Object.DestroyImmediate(doorVis.GetComponent<Collider>());
            doorVis.transform.position = new Vector3(x, 1.3f, -5.48f);
            doorVis.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            doorVis.transform.localScale = new Vector3(1.8f, 2.6f, 1f);
            Tint(doorVis, new Color(0.12f, 0.09f, 0.07f));

            MakeDoor(south, $"DOOR_S{i + 1}", new Vector3(x, 0f, -5.1f), $"shop_s{i + 1}");
        }

        // ── Skyline caps (Signoria edge recipe: opaque unlit, hug the ring) ───
        var backs = Group(root, "[Backdrops]");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(SkylineMatPath);
        if (mat == null) Debug.LogError("[StreetTemplateBuilder] Skyline material missing — run Signoria menu 5 once.");
        foreach (var (name, pos, yRot, w) in new (string, Vector3, float, float)[]
        {
            // Quads FACE THE INTERIOR (material is Cull Back): when the camera's
            // pullback offset crosses the south quad, it sees through the back
            // instead of a screen-filling orange wall.
            ("Backdrop_E", new Vector3(34f, 15f, 0f), 90f, 70f),     // faces west
            ("Backdrop_W", new Vector3(-34f, 15f, 0f), -90f, 70f),   // faces east
            ("Backdrop_N", new Vector3(0f, 15f, 15f), 0f, 90f),      // faces south
            ("Backdrop_S", new Vector3(0f, 15f, -13.5f), 180f, 90f), // faces north
        })
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = name;
            q.transform.SetParent(backs, false);
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.position = pos;
            q.transform.rotation = Quaternion.Euler(0f, yRot, 0f);
            q.transform.localScale = new Vector3(w, 60f, 1f);
            if (mat != null) q.GetComponent<Renderer>().sharedMaterial = mat;
        }

        // ── Travel + boundaries ───────────────────────────────────────────────
        var travel = Group(root, "[Travel]");
        var placer = new GameObject("[ZoneEntryPlacer]");
        placer.transform.SetParent(travel, false);
        placer.AddComponent<ZoneEntryPlacer>();
        MakeEntry(travel, "street_west", "West End", new Vector3(-27f, 0f, 0f), new Vector2(1f, 0f));
        MakeEntry(travel, "street_east", "East End", new Vector3(27f, 0f, 0f), new Vector2(-1f, 0f));
        MakeExit(travel, "ExitZone_West", new Vector3(-29.2f, 1.2f, 0f), new Vector3(1.6f, 2.4f, 9f));
        MakeExit(travel, "ExitZone_East", new Vector3(29.2f, 1.2f, 0f), new Vector3(1.6f, 2.4f, 9f));

        var bounds = Group(root, "[Boundaries]");
        foreach (var (name, pos, size) in new (string, Vector3, Vector3)[]
        {
            ("Bound_N", new Vector3(0f, 4f, 10.5f), new Vector3(70f, 8f, 1f)),
            ("Bound_S", new Vector3(0f, 4f, -10.5f), new Vector3(70f, 8f, 1f)),
            ("Bound_E", new Vector3(31f, 4f, 0f), new Vector3(1f, 8f, 24f)),
            ("Bound_W", new Vector3(-31f, 4f, 0f), new Vector3(1f, 8f, 24f)),
        })
        {
            var go = new GameObject(name);
            go.transform.SetParent(bounds, false);
            go.transform.position = pos;
            go.AddComponent<BoxCollider>().size = size;
        }

        // Street dressing: lamp posts by the doors
        var dressing = Group(root, "[Dressing]");
        var lampPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Environment/PonteVecchio/Props/LightPost.glb");
        if (lampPrefab != null)
            foreach (float x in new[] { -18f, 6f, 18f })
            {
                var lamp = (GameObject)PrefabUtility.InstantiatePrefab(lampPrefab);
                lamp.name = "Prop_LightPost";
                lamp.transform.SetParent(dressing, false);
                var b = BoundsOf(lamp);
                if (b.size.y > 0.0001f) lamp.transform.localScale = Vector3.one * (3f / b.size.y);
                lamp.transform.position = new Vector3(x, 0f, 4.6f);
                ReSeat(lamp, new Vector3(x, 0f, 4.6f));
                EnsureCollider(lamp);
            }
    }

    [MenuItem("InfernosCurse/Templates/2. New Street Scene From Template")]
    public static void NewStreetScene()
    {
        var template = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (template == null)
        {
            Debug.LogError("[StreetTemplateBuilder] Template prefab missing — run menu 1 first.");
            return;
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SceneManager.SetActiveScene(scene);

        PrefabUtility.InstantiatePrefab(template);

        // Edit-mode sun (CozySceneAdapter owns runtime light)
        var sun = new GameObject("Directional Light");
        var light = sun.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.96f, 0.86f);
        light.intensity = 1.2f;
        light.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

        CopyPlayer(scene, new Vector3(-27f, 0f, 0f));

        var kitPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CameraKitPath);
        if (kitPrefab != null)
        {
            var kit = (GameObject)PrefabUtility.InstantiatePrefab(kitPrefab);
            var zoom = kit.GetComponentInChildren<DynamicZoom>(true);
            if (zoom != null)
            {
                // Corridor: clearance mode (the PV recipe)
                zoom.useClearanceZoom = true;
                zoom.closeClearance = 3f;
                zoom.wideClearance = 9f;
                zoom.minArchitectureHeight = 2.5f;
            }
        }
        else Debug.LogError($"[StreetTemplateBuilder] Missing {CameraKitPath}");

        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log($"[StreetTemplateBuilder] {ScenePath} created. Rename it (File > Save As), " +
                  "swap SLOT_N* buildings, set ShopDoor targets, retarget the end exits, " +
                  "then add to Build Settings.");
    }

    // ── Helpers (Signoria patterns) ───────────────────────────────────────────

    static Transform Group(Transform parent, string name)
    {
        var g = new GameObject(name).transform;
        g.SetParent(parent, false);
        return g;
    }

    static void MakeDoor(Transform parent, string name, Vector3 pos, string shopId)
    {
        var d = new GameObject(name);
        d.transform.SetParent(parent, false);
        d.transform.position = pos + Vector3.up * 1.2f;
        var col = d.AddComponent<BoxCollider>();
        col.size = new Vector3(1.6f, 2.4f, 1.6f);
        var door = d.AddComponent<ShopDoor>();
        door.shopId = shopId;
        door.displayName = shopId;
    }

    static void MakeEntry(Transform parent, string id, string label, Vector3 pos, Vector2 face)
    {
        var go = new GameObject("ENTRY_" + id);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var ep = go.AddComponent<ZoneEntryPoint>();
        ep.entryId = id;
        ep.displayName = label;
        ep.faceDirection = face;
        ep.fastTravelDestination = true;
    }

    static void MakeExit(Transform parent, string name, Vector3 pos, Vector3 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.AddComponent<BoxCollider>().size = size;
        go.AddComponent<ZoneExit>().mode = ZoneExit.ExitMode.ToWorldMap;
    }

    static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.localScale = size;
        Tint(go, color);
        return go;
    }

    // Prefab assets can't reference in-memory materials (they go magenta in
    // any instance) — tints become shared material ASSETS keyed by color.
    static void Tint(GameObject go, Color color)
    {
        var r = go.GetComponent<Renderer>();
        if (r == null) return;
        string key = ColorUtility.ToHtmlStringRGB(color);
        string dir = "Assets/Prefabs/Templates/Materials";
        string path = $"{dir}/Street_{key}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Templates"))
                AssetDatabase.CreateFolder("Assets/Prefabs", "Templates");
            if (!AssetDatabase.IsValidFolder(dir))
                AssetDatabase.CreateFolder("Assets/Prefabs/Templates", "Materials");
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            mat = new Material(shader != null ? shader : Shader.Find("Standard")) { color = color };
            AssetDatabase.CreateAsset(mat, path);
        }
        r.sharedMaterial = mat;
    }

    static Bounds BoundsOf(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    static void ReSeat(GameObject go, Vector3 intendedPos)
    {
        var b = BoundsOf(go);
        go.transform.position = new Vector3(intendedPos.x, go.transform.position.y - b.min.y, intendedPos.z);
    }

    static void EnsureCollider(GameObject go)
    {
        if (go.GetComponentInChildren<Collider>() != null) return;
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return;
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        var col = go.AddComponent<BoxCollider>();
        col.center = go.transform.InverseTransformPoint(b.center);
        var s = go.transform.lossyScale;
        col.size = new Vector3(
            b.size.x / Mathf.Max(0.001f, s.x),
            b.size.y / Mathf.Max(0.001f, s.y),
            b.size.z / Mathf.Max(0.001f, s.z));
    }

    static void ApplyFrictionless(GameObject root)
    {
        var mat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(FrictionlessPath);
        if (mat == null) { Debug.LogWarning("[StreetTemplateBuilder] Frictionless material missing."); return; }
        foreach (var col in root.GetComponentsInChildren<Collider>(true))
            if (!col.isTrigger) col.sharedMaterial = mat;
    }

    static void CopyPlayer(Scene target, Vector3 pos)
    {
        var src = EditorSceneManager.OpenScene(PlayerSourceScene, OpenSceneMode.Additive);
        GameObject source = null;
        foreach (var r in src.GetRootGameObjects())
        {
            foreach (var t in r.GetComponentsInChildren<Transform>(true))
                if (t.CompareTag("Player")) { source = t.gameObject; break; }
            if (source != null) break;
        }
        if (source != null)
        {
            var copy = Object.Instantiate(source);
            copy.name = source.name;
            SceneManager.MoveGameObjectToScene(copy, target);
            copy.transform.position = new Vector3(pos.x, source.transform.position.y, pos.z);
        }
        else Debug.LogError("[StreetTemplateBuilder] No Player in source scene!");
        EditorSceneManager.CloseScene(src, true);
    }
}
