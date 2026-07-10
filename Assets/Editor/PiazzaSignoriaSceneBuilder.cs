using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Generates Assets/Scenes/PiazzaDellaSignoria.unity — the civic heart of 1299
// Florence per Docs/superpowers/specs/2026-07-06-piazza-della-signoria-design.md
// (approved by David 7/06; sheets of record in Refrences/images/
// signoria-piazza-plan.png + signoria-construction-concept.png).
//
// The premise: Arnolfo di Cambio broke ground on the Palazzo dei Priori THIS
// YEAR, so the hero building is a fenced construction site (rusticated walls
// in timber scaffolding, Foraboschi tower stump, treadwheel crane). The SW
// corner is the Uberti waste — razed traitor ground where nothing may be
// built. Walkable space reads as the historic L around the site.
//
// Coordinate contract: paving X -22..22, Z -17..17 (44x34), origin at piazza
// center, y=0 at paving top, entrance from the SOUTH (-Z) like every zone.
// Buildings ring the paving at Mercato scale (8.5), re-seated to y=0, every
// renderer colliding (boundaries are real geometry, not invisible walls).
//
// Prop placement is marker-driven (MARKER_<assetId>@<h>, kebab ids,
// InvariantCulture heights); menu 3 consumes markers as GLBs/banner art land.
public static class PiazzaSignoriaSceneBuilder
{
    const string SceneName = "PiazzaDellaSignoria";
    const string ScenePath = "Assets/Scenes/PiazzaDellaSignoria.unity";
    const string PlayerSourceScene = "Assets/Scenes/SaloneDelleArti.unity";
    const string CameraKitPath = "Assets/Prefabs/HD2D_CameraKit.prefab";
    const string GameSystemsPrefab = "Assets/Resources/GameSystems.prefab";

    // Paving rectangle (y = 0 at the top surface)
    const float HalfW = 22f;   // world X: -22..22
    const float HalfL = 17f;   // world Z: -17..17

    // Construction site (NE quadrant, fenced): yard + palazzo massing
    static readonly Vector2 YardMin = new Vector2(2f, 2.5f);
    static readonly Vector2 YardMax = new Vector2(22f, 17f);

    // Street mouths
    const float SouthMouthMinX = -2f, SouthMouthMaxX = 4f;
    const float NorthMouthMinX = -6f, NorthMouthMaxX = -1f;

    // Palette
    static readonly Color Paving = new Color(0.56f, 0.53f, 0.47f);      // pietra forte flagstones
    static readonly Color Rusticated = new Color(0.62f, 0.50f, 0.34f);  // new palazzo stone
    static readonly Color OldStone = new Color(0.45f, 0.44f, 0.42f);    // Foraboschi stump
    static readonly Color Timber = new Color(0.45f, 0.33f, 0.20f);      // scaffold / fence wood
    static readonly Color Scorched = new Color(0.28f, 0.24f, 0.20f);    // Uberti waste earth
    static readonly Color YardDirt = new Color(0.47f, 0.40f, 0.30f);    // work-site ground
    static readonly Color Rubble = new Color(0.50f, 0.48f, 0.45f);
    static readonly Color Haze = new Color(0.72f, 0.68f, 0.60f);        // backdrop stone city

    // ── 1. Build Scene ────────────────────────────────────────────────────────

    [MenuItem("InfernosCurse/Piazza della Signoria/1. Build Scene")]
    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SceneManager.SetActiveScene(scene);

        BuildPaving();
        BuildUbertiWaste();
        BuildConstructionSite();
        BuildSaloneFacade();
        BuildBuildingRing();
        BuildSouthEdge();
        BuildCivicDressing();
        BuildReuseProps();
        BuildMarkers();
        BuildTravelMarkers();
        BuildBoundaries();
        BuildBackdrop();
        BuildLighting();
        CopyPlayer(scene);
        PlaceCameraKit();

        ApplyFrictionless();

        BuildingWindowEnvironmentInstaller.ApplyToScene(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuildSettings(ScenePath);
        Debug.Log("[PiazzaSignoriaSceneBuilder] Scene built. Next: prop concepts/batch, " +
                  "then '3. Place Hero Props', '4. Wire Node', '5. Materials'.");
    }

    // ── Ground ────────────────────────────────────────────────────────────────

    static void BuildPaving()
    {
        var group = new GameObject("[Ground]").transform;
        // Outskirt apron: ground continues under and far past the building
        // ring, out beyond the backdrops — buildings never hang over void and
        // ground-level sightlines through gaps land on earth, not skybox
        // (David's in-game shot 7/06 #2).
        Box(group, "Floor_Outskirts", new Vector3(0f, -0.19f, 0f),
            new Vector3(100f, 0.3f, 92f), new Color(0.45f, 0.41f, 0.35f));
        // Paving extends UNDER the building ring and through both street
        // mouths (60x56 > the 44x34 piazza): buildings sit ON tiles instead of
        // floating 4cm above the lower apron, and the mouths read as paved
        // streets running on toward the backdrop (David 7/06 #3).
        Box(group, "Floor_Piazza", new Vector3(0f, -0.15f, 0f),
            new Vector3(60f, 0.3f, 56f), Paving);
        // Work-yard dirt (thin overlay, top at +0.015 — never coplanar)
        Box(group, "Floor_Yard", new Vector3((YardMin.x + YardMax.x) / 2f, -0.135f, (YardMin.y + YardMax.y) / 2f),
            new Vector3(YardMax.x - YardMin.x, 0.33f, YardMax.y - YardMin.y), YardDirt);
    }

    static void BuildUbertiWaste()
    {
        // Razed, salt-sown ground of the Uberti — bare earth breaking the paving.
        var group = new GameObject("[UbertiWaste]").transform;
        Box(group, "Floor_Waste", new Vector3(-16.5f, -0.133f, -12f),
            new Vector3(11f, 0.335f, 10f), Scorched);
        // Broken foundation stubs, half sunk
        foreach (var (pos, size, yRot) in new (Vector3, Vector3, float)[]
        {
            (new Vector3(-19.5f, 0.2f, -14.5f), new Vector3(1.9f, 0.42f, 0.6f), 12f),
            (new Vector3(-17.8f, 0.15f, -9.4f),  new Vector3(0.7f, 0.4f, 1.6f), -8f),
            (new Vector3(-13.6f, 0.12f, -13.8f), new Vector3(1.1f, 0.35f, 0.7f), 30f),
            (new Vector3(-15.2f, 0.10f, -11.2f), new Vector3(0.6f, 0.3f, 0.6f), 0f),
            (new Vector3(-20.4f, 0.14f, -11f),   new Vector3(0.8f, 0.4f, 0.8f), 45f),
        })
        {
            var b = Box(group, "Waste_Rubble", pos, size, Rubble);
            b.transform.rotation = Quaternion.Euler(0f, yRot, 0f);
        }
    }

    // ── The rising Palazzo dei Priori ─────────────────────────────────────────

    static void BuildConstructionSite()
    {
        var group = new GameObject("[ConstructionSite]").transform;

        // Palazzo massing: taller south front, lower rear — a building growing.
        // Hero scale: the ring buildings are ~8.3 wu tall, the palazzo must
        // dominate them even half-built (approved concept: risen-in-scaffold).
        Box(group, "Palazzo_Front", new Vector3(14.5f, 5.5f, 9.5f), new Vector3(13f, 11f, 5f), Rusticated);
        Box(group, "Palazzo_Rear", new Vector3(14.5f, 3.5f, 14.4f), new Vector3(13f, 7f, 4.8f), Rusticated);
        // The old Foraboschi tower stump, swallowed by the new work (older stone)
        Box(group, "Palazzo_TowerStump", new Vector3(9.3f, 8f, 8.3f), new Vector3(3f, 16f, 3f), OldStone);

        // Scaffolding on the rising faces (south + west): poles, ledgers, platforms
        var scaffold = new GameObject("Scaffold").transform;
        scaffold.SetParent(group, false);
        for (float x = 8.2f; x <= 21f; x += 1.6f)
            Pole(scaffold, new Vector3(x, 6f, 6.3f), 12f);
        for (float z = 7.2f; z <= 16.4f; z += 1.6f)
            Pole(scaffold, new Vector3(7.6f, 4f, z), 8f);
        foreach (float y in new[] { 3.4f, 6.8f, 10.2f })
        {
            Box(scaffold, "Scaffold_Platform_S", new Vector3(14.6f, y, 6.7f), new Vector3(13.2f, 0.08f, 0.9f), Timber);
            Box(scaffold, "Scaffold_Ledger_S", new Vector3(14.6f, y + 0.5f, 6.25f), new Vector3(13.2f, 0.07f, 0.07f), Timber);
        }
        foreach (float y in new[] { 3.4f, 6.8f })
        {
            Box(scaffold, "Scaffold_Platform_W", new Vector3(7.2f, y, 11.8f), new Vector3(0.9f, 0.08f, 9.4f), Timber);
            Box(scaffold, "Scaffold_Ledger_W", new Vector3(6.75f, y + 0.5f, 11.8f), new Vector3(0.07f, 0.07f, 9.4f), Timber);
        }
        // Ladder up the south face
        var ladder = Box(scaffold, "Scaffold_Ladder", new Vector3(12.2f, 1.4f, 6.1f), new Vector3(0.45f, 3.1f, 0.08f), Timber);
        ladder.transform.rotation = Quaternion.Euler(-12f, 0f, 0f);

        // Mortar troughs in the yard
        Box(group, "Yard_Trough", new Vector3(10.6f, 0.18f, 3.6f), new Vector3(1.2f, 0.36f, 0.7f), new Color(0.55f, 0.52f, 0.46f));
        Box(group, "Yard_Trough", new Vector3(6.1f, 0.18f, 6.9f), new Vector3(1.2f, 0.36f, 0.7f), new Color(0.55f, 0.52f, 0.46f));

        // Rope-and-post fence around the yard, cart gap on the south run.
        // Two rails: 0.85 (visual, at sprite hip height) + 1.5 (the one the
        // player's FLOATING box collider actually hits — box spans ~y1..2, so
        // a single low rail is walk-through and posts just snag; playtest 7/06).
        var fence = new GameObject("SiteFence").transform;
        fence.SetParent(group, false);
        FenceRun(fence, new Vector2(22f, 2.5f), new Vector2(13f, 2.5f));     // south run, east of gap
        FenceRun(fence, new Vector2(9f, 2.5f), new Vector2(6f, 2.5f));       // south run, west of gap
        FenceRun(fence, new Vector2(6f, 2.5f), new Vector2(2f, 6.5f));       // the diagonal (plan sheet)
        FenceRun(fence, new Vector2(2f, 6.5f), new Vector2(2f, 17f));        // west run
    }

    static void Pole(Transform parent, Vector3 center, float height)
    {
        var p = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        p.name = "Scaffold_Pole";
        p.transform.SetParent(parent, false);
        p.transform.position = center;
        p.transform.localScale = new Vector3(0.18f, height / 2f, 0.18f);
        Tint(p, Timber);
    }

    // Posts/rails are PURE DRESSING (no colliders). Each run's collision is one
    // invisible thick ground-up barrier box: the player's box can never get its
    // center over the fence line, so SnapToGround never vetoes it into a wedge
    // (playtest pass 4: center-over-rail = permanent pin at the gap corner).
    static void FenceRun(Transform parent, Vector2 a, Vector2 b)
    {
        var dir = (b - a);
        float len = dir.magnitude;
        dir /= len;
        float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
        int posts = Mathf.Max(2, Mathf.CeilToInt(len / 2.2f) + 1);
        for (int i = 0; i < posts; i++)
        {
            var pt = Vector2.Lerp(a, b, i / (float)(posts - 1));
            var post = Box(parent, "Fence_Post", new Vector3(pt.x, 0.875f, pt.y), new Vector3(0.16f, 1.75f, 0.16f), Timber);
            Object.DestroyImmediate(post.GetComponent<Collider>());
        }
        var mid = (a + b) / 2f;
        foreach (float y in new[] { 0.85f, 1.5f })
        {
            var rail = Box(parent, "Fence_Rail", new Vector3(mid.x, y, mid.y), new Vector3(0.07f, 0.07f, len), Timber);
            rail.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            Object.DestroyImmediate(rail.GetComponent<Collider>());
        }
        var barrier = new GameObject("Fence_Barrier");
        barrier.transform.SetParent(parent, false);
        barrier.transform.position = new Vector3(mid.x, 1.25f, mid.y);
        barrier.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        var col = barrier.AddComponent<BoxCollider>();
        col.size = new Vector3(0.5f, 2.5f, len);
    }

    // ── The Salone delle Arti street facade (west side) ──────────────────────

    static void BuildSaloneFacade()
    {
        var group = new GameObject("[SaloneFacade]").transform;
        // Main mass — face flush at x -21, hall bulk beyond the paving edge
        Box(group, "Wall_SaloneMass", new Vector3(-23.5f, 4.5f, 1f), new Vector3(5f, 9f, 13f), new Color(0.60f, 0.58f, 0.53f));
        // Projecting porch with slab roof (echoes the hall's own south porch)
        Box(group, "Wall_PorchSideN", new Vector3(-20.3f, 2f, 3.1f), new Vector3(1.4f, 4f, 0.6f), new Color(0.60f, 0.58f, 0.53f));
        Box(group, "Wall_PorchSideS", new Vector3(-20.3f, 2f, -1.1f), new Vector3(1.4f, 4f, 0.6f), new Color(0.60f, 0.58f, 0.53f));
        Box(group, "Wall_PorchRoof", new Vector3(-20.3f, 4.25f, 1f), new Vector3(1.7f, 0.5f, 4.8f), new Color(0.52f, 0.38f, 0.30f));
        // Dark door recess (Quad faces -Z; -90 faces east into the piazza)
        var door = GameObject.CreatePrimitive(PrimitiveType.Quad);
        door.name = "Salone_DoorRecess";
        door.transform.SetParent(group, false);
        Object.DestroyImmediate(door.GetComponent<Collider>());
        door.transform.position = new Vector3(-20.92f, 1.5f, 1f);
        door.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
        door.transform.localScale = new Vector3(2.2f, 3f, 1f);
        Tint(door, new Color(0.12f, 0.09f, 0.07f));
    }

    // ── Building ring (Mercato GLB reuse @ 8.5, re-seated, colliding) ────────

    struct BuildingSpec { public string path, name; public Vector3 pos; public float yRot; }

    static void BuildBuildingRing()
    {
        var group = new GameObject("[Buildings]").transform;
        var specs = new List<BuildingSpec>
        {
            // FACING (David 7/06): Apartment1's FRONT (door + balconies) is on
            // its narrow end — door faces world EAST at yRot 180. So: north row
            // uses yRot 270 (door south, narrow tower-house fronts), west edge
            // yRot 180 (door east), east edge yRot 0 (door west). Apartment_NE
            // reads correctly at 180 (plaster facade + windows south).
            // NO TownhouseDouble anywhere: twin towers + sky-bridge leaks sky.
            new BuildingSpec { path = "Assets/Environment/MarketSquare/Buildings/Apartment1.glb",
                name = "Bldg_West_N", pos = new Vector3(-24f, 0f, 12f), yRot = 180f },
            new BuildingSpec { path = "Assets/Environment/MarketSquare/Buildings/Apartment1.glb",
                name = "Bldg_West_S", pos = new Vector3(-24f, 0f, -10.5f), yRot = 180f },
            // North edge — narrow fronts packed edge to edge, faces at z~17,
            // street mouth kept at x -6..-1 (A1@270: 5.4 wide x 8.3 deep;
            // NE@180: 5.3 wide x 4.9 deep)
            // Row aligned to the tile line (David 7/06 #4): all BOUNDS fronts
            // at z=16.2 so wall bases sit on the piazza grid, eaves overhang.
            // Measured at yRot 270 Apartment1 is 8.3 WIDE x 5.4 deep (door
            // south, wide front) — packed by real widths (NE=5.3, A1=8.3).
            new BuildingSpec { path = "Assets/Environment/MarketSquare/Buildings/Apartment_NE.glb",
                name = "Bldg_North_NW", pos = new Vector3(-19.3f, 0f, 18.65f), yRot = 180f },
            new BuildingSpec { path = "Assets/Environment/MarketSquare/Buildings/Apartment1.glb",
                name = "Bldg_North_W", pos = new Vector3(-12.4f, 0f, 18.9f), yRot = 270f },
            new BuildingSpec { path = "Assets/Environment/MarketSquare/Buildings/Apartment1.glb",
                name = "Bldg_North_E", pos = new Vector3(3.15f, 0f, 18.9f), yRot = 270f },
            new BuildingSpec { path = "Assets/Environment/MarketSquare/Buildings/Apartment_NE.glb",
                name = "Bldg_North_E2", pos = new Vector3(9.95f, 0f, 18.65f), yRot = 180f },
            new BuildingSpec { path = "Assets/Environment/MarketSquare/Buildings/Apartment1.glb",
                name = "Bldg_North_E3", pos = new Vector3(16.75f, 0f, 18.9f), yRot = 270f },
            // San Pier Scheraggio, SE edge (faces west into the piazza)
            new BuildingSpec { path = "Assets/Environment/MarketSquare/Buildings/Church1.glb",
                name = "Bldg_SanPierScheraggio", pos = new Vector3(17f, 0f, -11.5f), yRot = -90f },
            // East edge tower house between church and site fence (door west)
            new BuildingSpec { path = "Assets/Environment/MarketSquare/Buildings/Apartment1.glb",
                name = "Bldg_East", pos = new Vector3(24f, 0f, -2f), yRot = 0f },
        };

        foreach (var s in specs)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(s.path);
            if (prefab == null) { Debug.LogError($"[PiazzaSignoriaSceneBuilder] Missing {s.path}"); continue; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = s.name;
            go.transform.SetParent(group, false);
            go.transform.rotation = Quaternion.Euler(0f, s.yRot, 0f);
            go.transform.localScale = Vector3.one * 8.5f;
            go.transform.position = s.pos;
            ReSeat(go, s.pos);
            EnsureCollider(go);
            var b = BoundsOf(go);
            Debug.Log($"[PiazzaSignoriaSceneBuilder] {s.name} bounds {b.size} at {b.center}");
        }
    }

    // ── South edge: low wall + street mouth (camera side stays low) ──────────

    static void BuildSouthEdge()
    {
        var group = new GameObject("[SouthEdge]").transform;
        float wallZ = -17.4f;
        // West segment: paving edge to mouth
        float wLen = SouthMouthMinX - (-HalfW);
        Box(group, "Wall_South_W", new Vector3((-HalfW + SouthMouthMinX) / 2f, 1.1f, wallZ), new Vector3(wLen, 2.2f, 0.8f), Paving * 1.05f);
        // East segment: mouth to paving edge
        float eLen = HalfW - SouthMouthMaxX;
        Box(group, "Wall_South_E", new Vector3((SouthMouthMaxX + HalfW) / 2f, 1.1f, wallZ), new Vector3(eLen, 2.2f, 0.8f), Paving * 1.05f);
        // Gate posts at the mouth
        Box(group, "Wall_GatePost_W", new Vector3(SouthMouthMinX, 1.6f, wallZ), new Vector3(1f, 3.2f, 1f), OldStone);
        Box(group, "Wall_GatePost_E", new Vector3(SouthMouthMaxX, 1.6f, wallZ), new Vector3(1f, 3.2f, 1f), OldStone);
    }

    // ── Civic dressing (flagpoles + banner markers live here) ───────────────

    static void BuildCivicDressing()
    {
        var group = new GameObject("[Civic]").transform;
        // Twin flagpoles NW of the herald platform; banners hang facing SOUTH
        // (marker yRot 180 — PlaceBanner flips 180, so the FRONT faces -Z/camera).
        foreach (var (x, id) in new[] { (-8f, "banner-comune-lily"), (-4f, "banner-popolo-cross") })
        {
            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Flagpole";
            pole.transform.SetParent(group, false);
            pole.transform.position = new Vector3(x, 3.5f, 2.8f);
            pole.transform.localScale = new Vector3(0.16f, 3.5f, 0.16f);
            Tint(pole, Timber);
            var m = new GameObject($"MARKER_{id}@2.2");
            m.transform.SetParent(group, false);
            m.transform.position = new Vector3(x, 3.9f, 2.55f);
            m.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        }
    }

    // ── Reused prop GLBs (bounds-normalized to a target height) ──────────────

    static void BuildReuseProps()
    {
        var group = new GameObject("[ReusedProps]").transform;
        foreach (var (path, name, pos, yRot, h) in new (string, string, Vector3, float, float)[]
        {
            // Wellhead OFF the main south→center desire line — at (2,-4) it wedged
            // the player's floating box for 27s in playtest pass 2 (collider top
            // 1.3 sits inside the box's y1..2 band; face-on contact can't slide).
            ("Assets/Environment/GiardinoDelleRose/Props/stone-wellhead.glb", "Prop_Wellhead", new Vector3(9f, 0f, -9.5f), 0f, 1.3f),
            ("Assets/Environment/MarketSquare/Props/Stone_Bench.glb", "Prop_Bench_Church", new Vector3(11.5f, 0f, -8f), 90f, 0.55f),
            ("Assets/Environment/MarketSquare/Props/Stone_Bench.glb", "Prop_Bench_W", new Vector3(-14f, 0f, 4.5f), -90f, 0.55f),
            ("Assets/Environment/MarketSquare/Props/Barrel.glb", "Prop_Barrel_1", new Vector3(4.2f, 0f, 11.8f), 0f, 0.9f),
            ("Assets/Environment/MarketSquare/Props/Barrel.glb", "Prop_Barrel_2", new Vector3(4.9f, 0f, 12.4f), 40f, 0.9f),
            ("Assets/Environment/MarketSquare/Props/Barrel.glb", "Prop_Barrel_3", new Vector3(6.3f, 0f, 15.6f), 75f, 0.9f),
            ("Assets/Environment/GiardinoDelleRose/Props/tuscan-cypress.glb", "Prop_Cypress_1", new Vector3(21f, 0f, -16f), 0f, 6f),
            ("Assets/Environment/GiardinoDelleRose/Props/tuscan-cypress.glb", "Prop_Cypress_2", new Vector3(12.5f, 0f, -16.3f), 70f, 5.2f),
            ("Assets/Environment/PonteVecchio/Props/LightPost.glb", "Prop_LightPost_SW", new Vector3(-2.8f, 0f, -15.6f), 0f, 3f),
            ("Assets/Environment/PonteVecchio/Props/LightPost.glb", "Prop_LightPost_SE", new Vector3(4.8f, 0f, -15.6f), 0f, 3f),
            ("Assets/Environment/PonteVecchio/Props/LightPost.glb", "Prop_LightPost_N", new Vector3(-9f, 0f, 14.8f), 0f, 3f),
        })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { Debug.LogError($"[PiazzaSignoriaSceneBuilder] Missing {path}"); continue; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = name;
            go.transform.SetParent(group, false);
            go.transform.rotation = Quaternion.Euler(0f, yRot, 0f);
            var b = BoundsOf(go);
            if (b.size.y > 0.0001f) go.transform.localScale = Vector3.one * (h / b.size.y);
            go.transform.position = pos;
            ReSeat(go, pos);
            EnsureCollider(go);
        }
    }

    // ── Hero-prop markers (menu 3 consumes; models come from the workbench) ──

    static void BuildMarkers()
    {
        var group = new GameObject("[Markers]").transform;
        foreach (var (name, pos, yRot) in new (string, Vector3, float)[]
        {
            ("MARKER_treadwheel-crane@5",   new Vector3(14.5f, 11.02f, 9.5f), 0f),  // atop the rising front wall
            ("MARKER_lion-den@2",           new Vector3(8.5f, 0f, 0.8f), 0f),        // against the site fence (Via dei Leoni nod)
            ("MARKER_masons-lodge@3.2",     new Vector3(5.2f, 0f, 13.5f), 90f),      // open side toward the yard
            ("MARKER_stone-block-pile@1.1", new Vector3(11.5f, 0f, 4.4f), 0f),
            ("MARKER_stone-block-pile@1.1", new Vector3(17.5f, 0f, 4.9f), 55f),
            ("MARKER_stone-block-pile@1.1", new Vector3(4.8f, 0f, 8.6f), 20f),
            ("MARKER_herald-platform@1.4",  new Vector3(-6f, 0f, -2f), 0f),
        })
        {
            var m = new GameObject(name);
            m.transform.SetParent(group, false);
            m.transform.position = pos;
            m.transform.rotation = Quaternion.Euler(0f, yRot, 0f);
        }
    }

    // ── Entries / exits ───────────────────────────────────────────────────────

    static void BuildTravelMarkers()
    {
        var placer = new GameObject("[ZoneEntryPlacer]");
        placer.AddComponent<ZoneEntryPlacer>();

        MakeEntry("signoria_south", "South Gate", new Vector3(1f, 0f, -14.5f), new Vector2(0f, 1f));
        MakeEntry("signoria_salone", "Salone delle Arti", new Vector3(-18.6f, 0f, 1f), new Vector2(1f, 0f));
        MakeEntry("signoria_north", "North Street", new Vector3(-4.6f, 0f, 14.5f), new Vector2(0f, -1f));

        MakeExit("ExitZone_South", new Vector3(1f, 1.2f, -16.5f), new Vector3(5.6f, 2.4f, 1.6f),
            ZoneExit.ExitMode.ToWorldMap, null, null);
        // North mouth walks into Via Calimala (wired 7/06; keep in sync with
        // the street's ExitZone_East -> signoria_north)
        MakeExit("ExitZone_North", new Vector3(-4.6f, 1.2f, 16.5f), new Vector3(6.6f, 2.4f, 1.6f),
            ZoneExit.ExitMode.ToScene, "ViaCalimala", "street_east");
        MakeExit("ExitZone_SaloneDoor", new Vector3(-20.1f, 1.2f, 1f), new Vector3(1.5f, 2.4f, 2.6f),
            ZoneExit.ExitMode.ToScene, "SaloneDelleArti", "salone_door");
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

    static void MakeExit(string name, Vector3 pos, Vector3 size, ZoneExit.ExitMode mode, string scene, string entry)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        go.AddComponent<BoxCollider>().size = size;
        var ex = go.AddComponent<ZoneExit>();
        ex.mode = mode;
        if (!string.IsNullOrEmpty(scene)) ex.targetScene = scene;
        if (!string.IsNullOrEmpty(entry)) ex.targetEntryId = entry;
    }

    static void BuildBoundaries()
    {
        var group = new GameObject("[Boundaries]");
        foreach (var (name, pos, size) in new (string, Vector3, Vector3)[]
        {
            ("Bound_S", new Vector3(0f, 4f, -HalfL - 1.2f), new Vector3(HalfW * 2f + 4f, 8f, 1f)),
            ("Bound_N", new Vector3(0f, 4f, HalfL + 1.2f), new Vector3(HalfW * 2f + 4f, 8f, 1f)),
            ("Bound_E", new Vector3(HalfW + 1.2f, 4f, 0f), new Vector3(1f, 8f, HalfL * 2f + 4f)),
            ("Bound_W", new Vector3(-HalfW - 1.2f, 4f, 0f), new Vector3(1f, 8f, HalfL * 2f + 4f)),
        })
        {
            var go = new GameObject(name);
            go.transform.SetParent(group.transform, false);
            go.transform.position = pos;
            go.AddComponent<BoxCollider>().size = size;
        }
    }

    static void BuildBackdrop()
    {
        // Painted Florence-skyline quads (Mercato's [Backdrop_Florence] recipe:
        // BackdropFlorence.mat, transparent painted skyline, double-sided) on
        // all four sides — hides the raw skybox "white space" behind the ring.
        // Mercato reference: 360x60 quad @ z49.5, y8, yRot 180.
        var group = new GameObject("[Backdrop]").transform;
        // OPAQUE UNLIT variant of Mercato's backdrop: the original transparent
        // material writes no depth, so COZY's runtime fog sees skybox behind it
        // and paints straight over it — visible in Scene view (no COZY), gone
        // in play mode (David 7/06). Opaque writes depth → fog hazes it like
        // real distant geometry. Same texture + top-55% crop as Mercato.
        // SKYLINE-ONLY art (David 7/06): Gemini silhouette of 1299 Florence —
        // slender tower-houses/campaniles/walls on the horizon, no dome, no
        // finished palazzo (both were anachronisms in Mercato's city sprawl).
        const string texPath = "Assets/Environment/PiazzaSignoria/Textures/signoria-skyline-backdrop.png";
        const string matPath = "Assets/Environment/PiazzaSignoria/Materials/Gen_Backdrop_Skyline.mat";
        AssetDatabase.ImportAsset(texPath);
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (tex == null) { Debug.LogError("[PiazzaSignoriaSceneBuilder] " + texPath + " missing!"); return; }
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "Gen_Backdrop_Skyline" };
            mat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            CreateFolders("Assets/Environment/PiazzaSignoria/Materials");
            AssetDatabase.CreateAsset(mat, matPath);
        }
        mat.SetTexture("_BaseMap", tex);          // self-heal: always point at the skyline art
        mat.SetTextureScale("_BaseMap", Vector2.one);
        mat.SetTextureOffset("_BaseMap", Vector2.zero);
        // Single-sided: the camera's pullback can cross the S quad (offset up
        // to -20 behind the player) — Cull Back + inward-facing quads means a
        // crossed quad vanishes instead of filling the screen.
        mat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Back);
        EditorUtility.SetDirty(mat);
        foreach (var (name, pos, yRot, w) in new (string, Vector3, float, float)[]
        {
            // Planes HUG the ring (tightest clearance per side — farther out,
            // the elevated pitch-40 camera sees bare apron edge-on through the
            // mouths). y=12 puts THIS art's horizon band (image v0.28..0.45)
            // at world y -1..+9 — dead in the through-the-mouth sightline.
            // All faces point INWARD (mat is Cull Back — see above)
            ("Backdrop_N", new Vector3(0f, 15f, 24f), 0f, 140f),
            ("Backdrop_S", new Vector3(0f, 15f, -24f), 180f, 130f),
            ("Backdrop_E", new Vector3(30f, 15f, 0f), 90f, 120f),
            ("Backdrop_W", new Vector3(-28f, 15f, 0f), -90f, 120f),
        })
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = name;
            q.transform.SetParent(group, false);
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.position = pos;
            q.transform.rotation = Quaternion.Euler(0f, yRot, 0f);
            q.transform.localScale = new Vector3(w, 60f, 1f);   // Mercato height — top clears steep look-up angles
            q.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }

    static void BuildLighting()
    {
        // Edit-mode sun only — CozySceneAdapter owns runtime light (outdoor zone).
        var sun = new GameObject("Directional Light");
        var light = sun.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.96f, 0.86f);
        light.intensity = 1.2f;
        light.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
    }

    static void CopyPlayer(Scene target)
    {
        var src = EditorSceneManager.OpenScene(PlayerSourceScene, OpenSceneMode.Additive);
        GameObject source = null;
        foreach (var root in src.GetRootGameObjects())
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
            copy.transform.position = new Vector3(1f, source.transform.position.y, -14.5f);
            Debug.Log($"[PiazzaSignoriaSceneBuilder] Player '{copy.name}' copied from SaloneDelleArti.");
        }
        else Debug.LogError("[PiazzaSignoriaSceneBuilder] No Player-tagged object in SaloneDelleArti!");
        EditorSceneManager.CloseScene(src, true);
    }

    static void PlaceCameraKit()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CameraKitPath);
        if (prefab == null) { Debug.LogError($"[PiazzaSignoriaSceneBuilder] Missing {CameraKitPath}"); return; }
        PrefabUtility.InstantiatePrefab(prefab);
    }

    // ── 3. Place Hero Props (marker consumption; safe to re-run) ─────────────

    const string PropsFolder = "Assets/Environment/PiazzaSignoria/Props";
    const string BannerFolder = "Assets/Environment/PiazzaSignoria/Banners";
    const string WorkbenchModels = "Tools/asset-gen/output/models";
    const string WorkbenchImages = "Tools/asset-gen/output/images";

    [MenuItem("InfernosCurse/Piazza della Signoria/3. Place Hero Props")]
    public static void PlaceProps()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != SceneName)
        {
            Debug.LogError($"[PiazzaSignoriaSceneBuilder] Open {SceneName}.unity first.");
            return;
        }

        var markers = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None)
            .Where(g => g.name.StartsWith("MARKER_"))
            .ToArray();

        int placed = 0, pending = 0;
        foreach (var marker in markers)
        {
            if (!TryParseMarker(marker.name, out string assetId, out float targetHeight)) continue;

            if (assetId.StartsWith("banner-"))
            {
                if (PlaceBanner(assetId, targetHeight, marker)) { Object.DestroyImmediate(marker); placed++; }
                else pending++;
                continue;
            }

            var prefab = ImportGlb(assetId);
            if (prefab == null) { pending++; continue; }
            PlaceGlb(prefab, assetId, targetHeight, marker);
            Object.DestroyImmediate(marker);
            placed++;
        }

        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[PiazzaSignoriaSceneBuilder] Props placed {placed}, pending {pending}. " +
                  (pending > 0 ? "Re-run after the model batch/banner images land." : "All markers resolved."));
    }

    static bool TryParseMarker(string name, out string assetId, out float targetHeight)
    {
        assetId = null; targetHeight = 1f;
        var body = name.Substring("MARKER_".Length);
        int at = body.LastIndexOf('@');
        if (at < 1) return false;
        assetId = body.Substring(0, at);
        return float.TryParse(body.Substring(at + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out targetHeight);
    }

    // Corrective pre-rotation for GLBs whose generated up-axis isn't Y —
    // fill in per asset as models land (raw bounds check).
    static Vector3? AxisFix(string assetId) => assetId switch
    {
        _ => (Vector3?)null,
    };

    static GameObject ImportGlb(string assetId)
    {
        string projPath = $"{PropsFolder}/{assetId}.glb";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(projPath);
        if (existing != null) return existing;

        string src = $"{WorkbenchModels}/{assetId}.glb";
        if (!System.IO.File.Exists(src)) return null;
        CreateFolders(PropsFolder);
        System.IO.File.Copy(src, projPath, overwrite: true);
        AssetDatabase.ImportAsset(projPath);
        return AssetDatabase.LoadAssetAtPath<GameObject>(projPath);
    }

    static void PlaceGlb(GameObject prefab, string assetId, float targetHeight, GameObject marker)
    {
        var pivot = new GameObject(assetId);
        pivot.transform.SetParent(PlacedRoot(), false);

        var mesh = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        mesh.name = assetId + "_mesh";
        var fix = AxisFix(assetId);
        if (fix.HasValue) mesh.transform.rotation = Quaternion.Euler(fix.Value);

        var renderers = mesh.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            float scale = bounds.size.y > 0.0001f ? targetHeight / bounds.size.y : 1f;
            mesh.transform.localScale = Vector3.one * scale;
        }

        mesh.transform.SetParent(pivot.transform, true);
        EnsureCollider(mesh);

        pivot.transform.position = marker.transform.position;
        pivot.transform.rotation = marker.transform.rotation;

        if (renderers.Length > 0)
        {
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            var anchor = marker.transform.position;
            var offset = new Vector3(b.center.x - anchor.x, b.min.y - (anchor.y + 0.01f), b.center.z - anchor.z);
            pivot.transform.position -= offset;
        }
    }

    static bool PlaceBanner(string assetId, float height, GameObject marker)
    {
        string texPath = $"{BannerFolder}/{assetId}.png";
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (tex == null)
        {
            string src = $"{WorkbenchImages}/{assetId}.png";
            if (!System.IO.File.Exists(src)) return false;
            CreateFolders(BannerFolder);
            System.IO.File.WriteAllBytes(texPath, KeyMagenta(System.IO.File.ReadAllBytes(src)));
            AssetDatabase.ImportAsset(texPath);
            var ti = (TextureImporter)AssetImporter.GetAtPath(texPath);
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = true;
            ti.SaveAndReimport();
            tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (tex == null) return false;
        }

        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = assetId;
        quad.transform.SetParent(PlacedRoot(), false);
        Object.DestroyImmediate(quad.GetComponent<Collider>());

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        var mat = new Material(shader != null ? shader : Shader.Find("Standard"));
        mat.mainTexture = tex;
        mat.SetFloat("_AlphaClip", 1f);
        mat.SetFloat("_Cutoff", 0.5f);
        mat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        mat.EnableKeyword("_ALPHATEST_ON");
        quad.GetComponent<Renderer>().sharedMaterial = mat;

        // Square quad = no distortion. Bottom hangs 0.5 above the marker; the
        // marker faces NORTH (yRot 180) so the flipped quad front faces the camera.
        quad.transform.localScale = new Vector3(height, height, 1f);
        quad.transform.rotation = marker.transform.rotation * Quaternion.Euler(0f, 180f, 0f);
        quad.transform.position = marker.transform.position + new Vector3(0f, 0.5f + height / 2f, 0f);
        return true;
    }

    static Transform PlacedRoot()
    {
        var g = GameObject.Find("[PlacedProps]");
        return (g != null ? g : new GameObject("[PlacedProps]")).transform;
    }

    static byte[] KeyMagenta(byte[] png)
    {
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(png);
        var px = tex.GetPixels32();
        for (int i = 0; i < px.Length; i++)
        {
            var c = px[i];
            // Gemini renders "#FF00FF" as deep pink ~(210, 30, 115) — key on
            // MEASURED values, not the requested color.
            if (c.r > 150 && c.g < 90 && c.b > 80 && c.r - c.g > 100 && c.b - c.g > 55)
                px[i] = new Color32(0, 0, 0, 0);
        }
        tex.SetPixels32(px);
        var outPng = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);
        return outPng;
    }

    // ── 4. Wire Node (GameSystems prefab + Salone door round-trip) ───────────

    [MenuItem("InfernosCurse/Piazza della Signoria/4. Wire Node")]
    public static void WireNode()
    {
        // 1) Fill the existing signoria node's sceneName/entryId (prefab edit).
        var root = PrefabUtility.LoadPrefabContents(GameSystemsPrefab);
        try
        {
            var hub = root.GetComponentInChildren<HubMap>(true);
            var node = hub != null ? hub.nodeData.FirstOrDefault(n => n.id == "signoria") : null;
            if (node == null) { Debug.LogError("[PiazzaSignoriaSceneBuilder] No 'signoria' node on HubMap!"); return; }
            node.sceneName = SceneName;
            node.entryId = "signoria_south";
            PrefabUtility.SaveAsPrefabAsset(root, GameSystemsPrefab);
            Debug.Log("[PiazzaSignoriaSceneBuilder] signoria node wired: " +
                      $"sceneName={SceneName}, entryId=signoria_south.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        // 2) Re-point the Salone's porch exit at the piazza (was: world map).
        //    Stepping out the guildhall door now lands on the piazza; the world
        //    map stays reachable from the piazza mouths + M fast travel.
        var salone = EditorSceneManager.OpenScene("Assets/Scenes/SaloneDelleArti.unity", OpenSceneMode.Single);
        bool changed = false;
        foreach (var exit in Object.FindObjectsByType<ZoneExit>(FindObjectsSortMode.None))
        {
            if (exit.gameObject.name != "ExitZone_Porch") continue;
            exit.mode = ZoneExit.ExitMode.ToScene;
            exit.targetScene = SceneName;
            exit.targetEntryId = "signoria_salone";
            EditorUtility.SetDirty(exit);
            changed = true;
        }
        if (changed)
        {
            EditorSceneManager.SaveScene(salone);
            Debug.Log("[PiazzaSignoriaSceneBuilder] Salone ExitZone_Porch → PiazzaDellaSignoria @ signoria_salone.");
        }
        else Debug.LogWarning("[PiazzaSignoriaSceneBuilder] ExitZone_Porch not found in SaloneDelleArti — door not rewired.");
    }

    // ── 5. Materials (world-aligned tiles — DuomoTilePass pattern) ───────────

    const string TexDir = "Assets/Environment/PiazzaSignoria/Textures";
    const string MatDir = "Assets/Environment/PiazzaSignoria/Materials";
    const float FloorTileWu = 2.6f;
    const float WallTileWu = 2.5f;

    [MenuItem("InfernosCurse/Piazza della Signoria/5. Materials")]
    public static void MaterialPass()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != SceneName)
        {
            Debug.LogError($"[PiazzaSignoriaSceneBuilder] Open {SceneName}.unity first.");
            return;
        }

        var texPaving = ImportTile("signoria-paving-flagstone");
        var texRust = ImportTile("signoria-wall-rusticated");
        var texDirt = ImportTile("signoria-ground-dirt");
        var texAshen = ImportTile("signoria-ground-ashen");
        if (texPaving == null || texRust == null || texDirt == null || texAshen == null)
        {
            Debug.LogError("[PiazzaSignoriaSceneBuilder] Tile textures missing from workbench output — aborting.");
            return;
        }
        CreateFolders(MatDir);
        var shader = Shader.Find("Universal Render Pipeline/Lit");

        int done = 0;
        foreach (var (goName, tex, tile, smooth) in new (string, Texture2D, float, float)[]
        {
            ("Floor_Piazza", texPaving, FloorTileWu, 0.16f),
            ("Floor_Yard", texDirt, 3.4f, 0.08f),
            ("Floor_Waste", texAshen, 2.6f, 0.05f),
        })
        {
            var go = GameObject.Find(goName);
            if (go == null) continue;
            var r = go.GetComponent<Renderer>();
            var b = r.bounds;
            var mat = new Material(shader) { name = $"Gen_Floor_{goName}" };
            mat.SetTexture("_BaseMap", tex);
            // Waste: ashen tile is too bright raw — pull it down to cursed
            // grey-brown earth (salt streaks stay visible, no marble read).
            mat.SetColor("_BaseColor", goName == "Floor_Waste" ? new Color(0.52f, 0.47f, 0.42f) : Color.white);
            mat.SetFloat("_Smoothness", smooth);
            mat.SetTextureScale("_BaseMap", new Vector2(b.size.x / tile, b.size.z / tile));
            mat.SetTextureOffset("_BaseMap", new Vector2(
                Mathf.Repeat(b.min.x / tile, 1f), Mathf.Repeat(b.min.z / tile, 1f)));
            AssetDatabase.CreateAsset(mat, $"{MatDir}/Gen_Floor_{goName}.mat");
            r.sharedMaterial = mat;
            done++;
        }

        // Rusticated stone on the palazzo massing, binned by integer repeats.
        // The Foraboschi tower stump keeps its OLD grey stone tint (it predates
        // the palazzo — the gold rustication is the NEW work only).
        // Salone facade pieces get a grey pietra-serena cast so the guildhall
        // doesn't read as the same golden stone as the rising palazzo.
        var wallMats = new Dictionary<(int, int, bool), Material>();
        int walls = 0;
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            var n = r.gameObject.name;
            if (!n.StartsWith("Palazzo_") && !n.StartsWith("Wall_")) continue;
            if (n == "Wall_PorchRoof" || n == "Palazzo_TowerStump") continue;
            bool grey = n.StartsWith("Wall_Salone") || n.StartsWith("Wall_Porch");

            var s = r.transform.localScale;
            float len = Mathf.Max(s.x, s.z);
            int repX = Mathf.Max(1, Mathf.RoundToInt(len / WallTileWu));
            int repY = Mathf.Max(1, Mathf.RoundToInt(s.y / WallTileWu));
            if (!wallMats.TryGetValue((repX, repY, grey), out var mat))
            {
                string suffix = grey ? "Grey" : "";
                mat = new Material(shader) { name = $"Gen_Wall_{repX}x{repY}{suffix}" };
                mat.SetTexture("_BaseMap", texRust);
                mat.SetColor("_BaseColor", grey ? new Color(0.62f, 0.65f, 0.68f) : Color.white);
                mat.SetFloat("_Smoothness", 0.10f);
                mat.SetTextureScale("_BaseMap", new Vector2(repX, repY));
                AssetDatabase.CreateAsset(mat, $"{MatDir}/Gen_Wall_{repX}x{repY}{suffix}.mat");
                wallMats[(repX, repY, grey)] = mat;
            }
            r.sharedMaterial = mat;
            walls++;
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[PiazzaSignoriaSceneBuilder] Material pass: floors={done} walls={walls} bins={wallMats.Count}.");
    }

    static Texture2D ImportTile(string id)
    {
        string projPath = $"{TexDir}/{id}.png";
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(projPath);
        if (existing != null) return existing;
        string src = $"Tools/asset-gen/output/images/{id}.png";
        if (!System.IO.File.Exists(src)) return null;
        CreateFolders(TexDir);
        System.IO.File.Copy(src, projPath, overwrite: true);
        AssetDatabase.ImportAsset(projPath);
        var ti = (TextureImporter)AssetImporter.GetAtPath(projPath);
        ti.wrapMode = TextureWrapMode.Repeat;
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(projPath);
    }

    // ── Shared helpers ────────────────────────────────────────────────────────

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

    static void Tint(GameObject go, Color color)
    {
        var renderer = go.GetComponent<Renderer>();
        if (renderer == null) return;
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        renderer.sharedMaterial = new Material(shader) { color = color };
    }

    static Bounds BoundsOf(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        var b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        return b;
    }

    // Drop the object so its render-bounds base sits on y=0 (scaling from
    // pivot sinks/floats GLBs — Mercato lesson).
    static void ReSeat(GameObject go, Vector3 intendedPos)
    {
        var b = BoundsOf(go);
        go.transform.position = new Vector3(
            intendedPos.x, go.transform.position.y - b.min.y, intendedPos.z);
    }

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
        col.sharedMaterial = GetFrictionless();
    }

    // Zero-friction material on every scenery collider: grazing contact slides
    // instead of snagging (fence-post snag + wellhead wedge, playtest 7/06).
    const string FrictionlessPath = "Assets/Environment/PiazzaSignoria/Frictionless.physicMaterial";

    static PhysicsMaterial GetFrictionless()
    {
        var mat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(FrictionlessPath);
        if (mat != null) return mat;
        CreateFolders("Assets/Environment/PiazzaSignoria");
        mat = new PhysicsMaterial("Frictionless")
        {
            dynamicFriction = 0f,
            staticFriction = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
            bounceCombine = PhysicsMaterialCombine.Minimum,
        };
        AssetDatabase.CreateAsset(mat, FrictionlessPath);
        return mat;
    }

    static void ApplyFrictionless()
    {
        var mat = GetFrictionless();
        int n = 0;
        foreach (var col in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
        {
            if (col.isTrigger || col.CompareTag("Player")) continue;
            col.sharedMaterial = mat;
            n++;
        }
        Debug.Log($"[PiazzaSignoriaSceneBuilder] Frictionless material on {n} colliders.");
    }

    static void CreateFolders(string path)
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

    static void AddToBuildSettings(string scenePath)
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == scenePath)) return;
        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log($"[PiazzaSignoriaSceneBuilder] Added {scenePath} to Build Settings.");
    }
}
