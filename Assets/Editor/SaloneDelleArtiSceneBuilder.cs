using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Generates Assets/Scenes/SaloneDelleArti.unity — the central guildhall of
// Florence (spec: Docs/superpowers/specs/2026-07-05-salone-delle-arti-design.md,
// approved 7/05). First true multi-floor interior: a 36×16 wu hall with eight
// semicircular Arte bays, a north council dais under an apse, twin south spiral
// stair turrets, and a +5 wu gallery ring with a chapel alcove at the north end.
//
// Coordinate contract (both floorplan sheets read ROTATED 180° from print):
// origin = hall center; world X = east-west (interior 16), world Z =
// north-south (interior 36); entrance south (-Z), dais north (+Z); walls
// H10 T0.6 (Duomo values); gallery floor top at +5, balustrade H1.1.
// Sheet px→wu was derived per-axis off the terra sheet's interior rectangle
// (the sheets' aspect is stylized — never assume a fixed px/wu constant).
//
// The "Level2_" name prefix is LOAD-BEARING: LevelVisibilityFader hides all
// renderers under Level2_* roots while the player is on the hall floor
// (player-height dollhouse). Keep all gallery-level content under the
// Level2_Gallery root — including hero props placed at its markers.
//
// Prop placement is marker-driven (Giardino idiom): MARKER_<assetId>@<h>
// empties, consumed by menu item "3. Place Hero Props" once the approved
// workbench batch lands.
public static class SaloneDelleArtiSceneBuilder
{
    const string ScenePath = "Assets/Scenes/SaloneDelleArti.unity";
    const string PlayerSourceScene = "Assets/Scenes/PonteVecchio.unity";
    const string CameraKitPath = "Assets/Prefabs/HD2D_CameraKit.prefab";
    const string GameSystemsPrefab = "Assets/Resources/GameSystems.prefab";
    const string GuildAssetDir = "Assets/Data/Guilds";
    const string SceneName = "SaloneDelleArti";

    // Hall envelope
    const float HalfW = 8f;      // interior x: -8..8
    const float HalfL = 18f;     // interior z: -18..18
    const float WallH = 10f;
    const float WallT = 0.6f;
    const float WallLine = HalfW + WallT / 2f;   // wall centerline |x| = 8.3

    // Gallery level
    const float GalleryY = 5f;        // walking surface (plate tops)
    const float PlateT = 0.3f;
    const float RingW = 2.5f;         // walkway band width
    const float BalusH = 1.1f;
    const float BalusT = 0.15f;

    // Arte bays (4 per side, semicircular alcoves bulging outward)
    static readonly float[] BayZ = { -9.5f, -3.5f, 2.5f, 8.5f };
    const float BayOpening = 4f;
    const float BayRadius = 2f;

    // Spiral stair turrets (south corners, on BOTH sheets = stacking guarantee)
    static readonly Vector2 TurretW = new Vector2(-5.7f, -15.7f);
    static readonly Vector2 TurretE = new Vector2(5.7f, -15.7f);
    // Shell widened 2.025 -> 2.35 (playtest 7/06 #3): with the fat newel the
    // capsule only had a 30cm channel and ground to a halt on wall contact —
    // now the tread band r 0.85..2.175 gives it ~65cm. The turret embeds a few
    // cm into the hall walls at the corner; reads as a fused tower.
    const float ShellR = 2.35f;       // shell segment center radius
    const float ShellT = 0.35f;
    // Newel r 0.6: the r 0.85 "fat newel" wedged the player's box collider at
    // climb radius (playtest 7/06 #6 — OverlapBox fingered Newel_E as the
    // invariant blocker). With 10cm pseudo-ramp risers the fat newel's
    // steep-inner-slope rationale is obsolete: worst reachable slope ≈ 37°.
    const float NewelR = 0.6f;
    const float TreadInnerR = 0.6f;   // newel face
    const float TreadOuterR = 2.175f; // shell inner face
    // 48 thin treads per turn = a pseudo-ramp (riser ~0.10). Discrete stairs
    // (12 → riser 0.41, then 16 → 0.31) kept stalling in playtests 7/06 #3-#5:
    // whenever the capsule brushes the newel, wall contact defeats the step
    // assist — and on a spiral, newel contact is inevitable. 10cm risers climb
    // cleanly even while grinding the newel.
    const int HelixSteps = 48;
    const float HelixTop = 4.97f;     // 3cm below the plates — avoids coplanar z-fight

    // Bay → guild assignment, south→north. Albergatori (minor inn guild) gets
    // the humblest bay nearest the door; Calimala/Cambio flank the dais end.
    static readonly string[] WestBays = { "albergatori", "vaiai_pellicciai", "medici_speziali", "cambio" };
    static readonly string[] EastBays = { "por_santa_maria", "giudici_notai", "lana", "calimala" };

    // ── 1. Build Scene ────────────────────────────────────────────────────────

    [MenuItem("InfernosCurse/Salone delle Arti/1. Build Scene")]
    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SceneManager.SetActiveScene(scene);

        var arch = new GameObject("[Architecture]").transform;
        BuildFloor(arch);
        BuildWalls(arch);
        BuildBays(arch);
        BuildApseAndDais(arch);
        BuildColumns(arch);
        BuildVestibule(arch);

        var turrets = new GameObject("[Turrets]").transform;
        var level2 = new GameObject("Level2_Gallery").transform;   // prefix LOAD-BEARING
        BuildTurret(TurretW, turrets, level2, "W");
        BuildTurret(TurretE, turrets, level2, "E");
        BuildGallery(level2);

        BuildMarkers(level2);
        BuildLighting();
        BuildBoundaries();
        BuildTravel();

        new GameObject("[LevelFader]").AddComponent<LevelVisibilityFader>();

        CopyPlayerFromPonteVecchio(scene);
        PlaceCameraKit();

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuildSettings(ScenePath);
        Debug.Log("[SaloneDelleArtiSceneBuilder] Shell built. Next: '4. Setup Guilds', " +
                  "then the hero-prop batch + '3. Place Hero Props'.");
    }

    // ── Floor ─────────────────────────────────────────────────────────────────

    static void BuildFloor(Transform parent)
    {
        // One slab under hall + bay recesses + north apse (bays reach |x|≈10.6,
        // apse reaches z≈21.7). Top at y=0.
        Box(parent, "Floor_Salone", new Vector3(0f, -0.25f, 1.6f), new Vector3(21.2f, 0.5f, 40.4f), Terracotta);

        // Vestibule strip south of the hall slab (no overlap — coplanar tops z-fight).
        Box(parent, "Floor_Vestibule", new Vector3(0f, -0.25f, -20.7f), new Vector3(6f, 0.5f, 4.2f), Terracotta);
        // Porch apron outside the door — solid ground under the exit trigger.
        Box(parent, "Floor_Apron", new Vector3(0f, -0.25f, -23.4f), new Vector3(8f, 0.5f, 1.2f), new Color(0.50f, 0.45f, 0.40f));
    }

    // ── Walls (poché) ─────────────────────────────────────────────────────────

    static void BuildWalls(Transform parent)
    {
        // South wall — door gap x -2..2 into the vestibule.
        Box(parent, "Wall_S_West", new Vector3(-5.3f, WallH / 2f, -HalfL - WallT / 2f), new Vector3(6.6f, WallH, WallT), Stone);
        Box(parent, "Wall_S_East", new Vector3(5.3f, WallH / 2f, -HalfL - WallT / 2f), new Vector3(6.6f, WallH, WallT), Stone);

        // North wall — apse gap x -3..3 behind the dais.
        Box(parent, "Wall_N_West", new Vector3(-5.8f, WallH / 2f, HalfL + WallT / 2f), new Vector3(5.6f, WallH, WallT), Stone);
        Box(parent, "Wall_N_East", new Vector3(5.8f, WallH / 2f, HalfL + WallT / 2f), new Vector3(5.6f, WallH, WallT), Stone);

        // East/west walls — five solid piers around the four bay openings
        // (openings are BayZ ± 2).
        var spans = new (float c, float len)[]
        {
            (-15.05f, 7.1f), (-6.5f, 2f), (-0.5f, 2f), (5.5f, 2f), (14.55f, 8.1f),
        };
        foreach (var side in new[] { -1f, 1f })
            foreach (var (c, len) in spans)
                Box(parent, $"Wall_{(side < 0 ? "W" : "E")}_{c:0}", new Vector3(side * WallLine, WallH / 2f, c), new Vector3(WallT, WallH, len), Stone);
    }

    // ── Arte bays (semicircular alcove shells bulging outward) ───────────────

    static void BuildBays(Transform parent)
    {
        foreach (var side in new[] { -1f, 1f })
        {
            foreach (float z in BayZ)
            {
                var center = new Vector3(side * WallLine, 0f, z);
                // 6 chord segments over the outward-facing hemisphere.
                for (int k = 0; k < 6; k++)
                {
                    float a = side < 0 ? 105f + 30f * k : -75f + 30f * k;
                    ArcBox(parent, $"Bay_{(side < 0 ? "W" : "E")}{z:0}_{k}", center, a, BayRadius,
                        1.15f, WallH, ShellT, WallH / 2f, Stone);
                }
            }
        }
    }

    // ── North apse + council dais ─────────────────────────────────────────────

    static void BuildApseAndDais(Transform parent)
    {
        var apseC = new Vector3(0f, 0f, HalfL + WallT / 2f);
        for (int k = 0; k < 8; k++)
            ArcBox(parent, $"Apse_{k}", apseC, 11.25f + 22.5f * k, 3f, 1.3f, WallH, ShellT, WallH / 2f, Stone);

        // Apse floor plinth — 2cm lip above the dais top (coplanar avoidance).
        var plinth = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        plinth.name = "Apse_Plinth";
        plinth.transform.SetParent(parent, false);
        plinth.transform.position = new Vector3(0f, 0.41f, HalfL + WallT / 2f);
        plinth.transform.localScale = new Vector3(5.8f, 0.41f, 5.8f);
        Object.DestroyImmediate(plinth.GetComponent<CapsuleCollider>());
        plinth.AddComponent<BoxCollider>();
        Tint(plinth, DarkStone);

        // Dais platform + two broad steps (risers 0.27 ≤ MaxStepUp 0.45).
        Box(parent, "Dais_Platform", new Vector3(0f, 0.4f, 16.5f), new Vector3(10f, 0.8f, 3f), DarkStone);
        Box(parent, "Dais_Step2", new Vector3(0f, 0.267f, 14.7f), new Vector3(10f, 0.533f, 0.6f), DarkStone);
        Box(parent, "Dais_Step1", new Vector3(0f, 0.133f, 14.1f), new Vector3(10f, 0.267f, 0.6f), DarkStone);
    }

    // ── Columns (the terra sheet's cross pattern) ────────────────────────────

    static void BuildColumns(Transform parent)
    {
        var spots = new List<Vector2>();
        foreach (float z in new[] { -6f, -3f, 0f, 3f, 6f }) spots.Add(new Vector2(0f, z));
        foreach (float x in new[] { -6f, -3f, 3f, 6f }) spots.Add(new Vector2(x, 0f));

        foreach (var s in spots)
        {
            var col = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            col.name = $"Column_{s.x:0}_{s.y:0}";
            col.transform.SetParent(parent, false);
            col.transform.position = new Vector3(s.x, WallH / 2f, s.y);
            col.transform.localScale = new Vector3(0.7f, WallH / 2f, 0.7f);
            Tint(col, LightStone);
        }
    }

    // ── South vestibule + porch ───────────────────────────────────────────────

    static void BuildVestibule(Transform parent)
    {
        // Lower annex walls (H6) flanking the entrance corridor.
        Box(parent, "Vestibule_W", new Vector3(-2.3f, 3f, -20.7f), new Vector3(WallT, 6f, 4.6f), Stone);
        Box(parent, "Vestibule_E", new Vector3(2.3f, 3f, -20.7f), new Vector3(WallT, 6f, 4.6f), Stone);
    }

    // ── Spiral stair turrets ──────────────────────────────────────────────────
    //
    // Shell: 12 arc segments × two bands (0..5 lower, 5..10 upper/Level2_),
    // with a 90° door gap facing the hall center — the ground entrance and the
    // gallery exit share the same angular gap. Inside: a newel column and a
    // one-turn spiral of flat steps (risers ≈0.41), topped by three flat
    // landing segments at gallery height spanning the gap, plus a small
    // bridge box out to the gallery plates.

    static void BuildTurret(Vector2 c, Transform lowerParent, Transform level2, string tag)
    {
        float doorAngle = Mathf.Atan2(-c.y, -c.x) * Mathf.Rad2Deg;   // toward hall center
        var center = new Vector3(c.x, 0f, c.y);

        var lower = new GameObject($"Turret_{tag}").transform;
        lower.SetParent(lowerParent, false);
        var upper = new GameObject($"Level2_TurretShell_{tag}").transform;
        upper.SetParent(level2, false);

        for (int k = 0; k < 12; k++)
        {
            float a = 15f + 30f * k;
            if (Mathf.Abs(Mathf.DeltaAngle(a, doorAngle)) < 40f) continue;   // door gap
            ArcBox(lower, $"Shell_{tag}_{k}", center, a, ShellR, 1.3f, GalleryY, ShellT, GalleryY / 2f, Stone);
            ArcBox(upper, $"Level2_Shell_{tag}_{k}", center, a, ShellR, 1.3f, WallH - GalleryY, ShellT,
                GalleryY + (WallH - GalleryY) / 2f, Stone);
        }

        var newel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        newel.name = $"Newel_{tag}";
        newel.transform.SetParent(lower, false);
        newel.transform.position = center + Vector3.up * (WallH / 2f);
        newel.transform.localScale = new Vector3(NewelR * 2f, WallH / 2f, NewelR * 2f);
        Tint(newel, LightStone);

        // Spiral steps: one CCW turn, treads spanning newel face to shell
        // (r 0.85..1.85). t0 sits just inside the gap's CCW edge so tread 1
        // (top 0.41, boardable) is the ONLY tread in the doorway — starting at
        // doorAngle-45 parked treads 2-3 (0.83/1.24) in the gap, an unboardable
        // knee-wall (playtest 7/06 #2). Tread 12 re-crosses the gap at y≈4.97
        // with 4.5 of headroom, meeting the bridge at doorAngle.
        float rise = HelixTop / HelixSteps;
        float stepArc = 360f / HelixSteps;
        float t0 = doorAngle + 25f;
        float treadMid = (TreadInnerR + TreadOuterR) / 2f;
        float treadW = TreadOuterR - TreadInnerR;
        for (int i = 0; i < HelixSteps; i++)
        {
            float top = rise * (i + 1);
            ArcBox(lower, $"Step_{tag}_{i}", center, t0 + stepArc * i, treadMid, 0.3f, 0.15f, treadW, top - 0.075f, DarkStone);
        }
        // Landing: thin flat segments at gallery height, spanning the door gap.
        for (int j = 0; j < 12; j++)
            ArcBox(lower, $"Landing_{tag}_{j}", center, t0 + 360f + stepArc * j, treadMid, 0.3f, 0.25f, treadW, HelixTop - 0.125f, DarkStone);
        // Bridge over the shell line out to the gallery plates (top 4.98 —
        // 1-2cm steps to landing/plates, never coplanar).
        var dir = new Vector3(Mathf.Cos(doorAngle * Mathf.Deg2Rad), 0f, Mathf.Sin(doorAngle * Mathf.Deg2Rad));
        var bridge = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bridge.name = $"Bridge_{tag}";
        bridge.transform.SetParent(lower, false);
        bridge.transform.position = center + dir * 2.3f + Vector3.up * 4.855f;
        bridge.transform.rotation = Quaternion.LookRotation(dir);
        bridge.transform.localScale = new Vector3(2.6f, 0.25f, 1.2f);
        Tint(bridge, DarkStone);
    }

    // ── Gallery ring (+5) — 4 bands + corner/chapel plates, non-overlapping ──

    static void BuildGallery(Transform level2)
    {
        float py = GalleryY - PlateT / 2f;
        // The SOUTH corners are OPEN stair shafts: gallery floor must NOT cover
        // the turret interiors — the tilted billboard box hits the plate
        // underside at y4.7 and the climb can never top out (playtest 7/06 #10,
        // live OverlapBox: Plate_East). Long bands stop just north of the
        // shells; small exit pads catch the bridge outside each door gap.
        Box(level2, "Plate_West", new Vector3(-(HalfW - RingW / 2f), py, 2.45f), new Vector3(RingW, PlateT, 31.1f), Stone);
        Box(level2, "Plate_East", new Vector3(HalfW - RingW / 2f, py, 2.45f), new Vector3(RingW, PlateT, 31.1f), Stone);
        Box(level2, "Plate_South", new Vector3(0f, py, -16.75f), new Vector3(6.2f, PlateT, RingW), Stone);
        Box(level2, "Plate_North", new Vector3(0f, py, 16.75f), new Vector3(11f, PlateT, RingW), Stone);
        Box(level2, "Plate_ExitPad_E", new Vector3(4.75f, py, -12.95f), new Vector3(1.5f, PlateT, 1.1f), Stone);
        Box(level2, "Plate_ExitPad_W", new Vector3(-4.75f, py, -12.95f), new Vector3(1.5f, PlateT, 1.1f), Stone);
        Box(level2, "Plate_Chapel", new Vector3(0f, py, 14.25f), new Vector3(6f, PlateT, RingW), Stone);

        // Balustrade — closed circuit along every void edge (fall prevention:
        // every renderer gets a collider).
        var runs = new (float x1, float z1, float x2, float z2)[]
        {
            (-3.1f, -15.5f, 3.1f, -15.5f), // south band inner (butts the shells)
            ( 5.5f, -12.4f, 5.5f, 15.5f),  // east inner (starts NORTH of the pad junction)
            ( 5.5f, 15.5f,  3f, 15.5f),    // north band inner east
            ( 3f, 15.5f,    3f, 13f),      // chapel east edge
            ( 3f, 13f,     -3f, 13f),      // chapel south edge
            (-3f, 13f,     -3f, 15.5f),    // chapel west edge
            (-3f, 15.5f,   -5.5f, 15.5f),  // north band inner west
            (-5.5f, 15.5f, -5.5f, -12.4f), // west inner (stops NORTH of the pad junction)
            // Exit pads: rail the west/north void sides. The SOUTH edge stays
            // OPEN — that's where the turret bridge lands (a rail there fenced
            // the stairs off the gallery, playtest 7/06 #11).
            ( 4.0f, -13.5f, 4.0f, -12.4f), // pad E west edge
            ( 4.0f, -12.4f, 5.5f, -12.4f), // pad E north edge
            (-4.0f, -13.5f, -4.0f, -12.4f),
            (-4.0f, -12.4f, -5.5f, -12.4f),
        };
        int n = 0;
        foreach (var (x1, z1, x2, z2) in runs)
        {
            var a = new Vector3(x1, 0f, z1);
            var b = new Vector3(x2, 0f, z2);
            var mid = (a + b) / 2f + Vector3.up * (GalleryY + BalusH / 2f);
            var d = (b - a).normalized;
            var bal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bal.name = $"Balustrade_{n++}";
            bal.transform.SetParent(level2, false);
            bal.transform.position = mid;
            bal.transform.rotation = Quaternion.LookRotation(d);
            bal.transform.localScale = new Vector3(BalusT, BalusH, Vector3.Distance(a, b) + BalusT);
            Tint(bal, LightStone);
        }
    }

    // ── Prop markers (consumed by "3. Place Hero Props") ─────────────────────

    static void BuildMarkers(Transform level2)
    {
        var group = new GameObject("[PropMarkers]").transform;

        void M(Transform parent, string assetId, float h, Vector3 pos, float yRot = 0f)
        {
            var m = new GameObject(string.Format(CultureInfo.InvariantCulture, "MARKER_{0}@{1:0.##}", assetId, h));
            m.transform.SetParent(parent, false);
            m.transform.position = pos;
            m.transform.rotation = Quaternion.Euler(0f, yRot, 0f);
        }

        M(group, "dais-council-bench", 1.5f, new Vector3(0f, 0.8f, 16.9f), 180f);
        M(group, "notary-lectern", 1.3f, new Vector3(2.8f, 0f, 13.4f), 210f);
        // Hangs over the open span south of the dais (placement skips grounding).
        M(group, "iron-chandelier", 2.8f, new Vector3(0f, 6.5f, 11f));

        foreach (var side in new[] { -1f, 1f })
        {
            var guilds = side < 0 ? WestBays : EastBays;
            for (int i = 0; i < BayZ.Length; i++)
            {
                float z = BayZ[i];
                float faceIn = side < 0 ? 90f : -90f;
                // Marker ids stay kebab-case (workbench slugs) — guild ids keep
                // their underscores everywhere else.
                M(group, $"guild-banner-{guilds[i].Replace('_', '-')}", 3.2f, new Vector3(side * 7.95f, 0f, z), faceIn);
                M(group, "bay-bench", 0.9f, new Vector3(side * 6.8f, 0f, z - 1.5f), faceIn);
                M(group, "bay-strongbox", 0.7f, new Vector3(side * 7.3f, 0f, z + 1.5f), faceIn + 30f);
            }
        }

        // Chapel altar on the gallery — parent under Level2_ so the placed prop
        // inherits the fader prefix.
        M(level2, "chapel-altar", 1.6f, new Vector3(0f, GalleryY, 16.5f), 180f);
    }

    // ── Lighting: edit-mode sun (CozySceneAdapter kills suns at runtime) +
    //    TorchFlicker spots + LightShaft rakes from high east windows ─────────

    static void BuildLighting()
    {
        var group = new GameObject("[Lighting]").transform;

        var sun = new GameObject("Directional Light (edit-mode)");
        sun.transform.SetParent(group, false);
        var sunLight = sun.AddComponent<Light>();
        sunLight.type = LightType.Directional;
        sunLight.color = new Color(1f, 0.95f, 0.85f);
        sunLight.intensity = 1.1f;
        sunLight.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(55f, -30f, 0f);

        // Wall torches on the piers between bays, dais pair, chapel candle.
        var torchSpots = new List<(Vector3 pos, Vector3 aim)>();
        foreach (var side in new[] { -1f, 1f })
            foreach (float z in new[] { -6.5f, -0.5f, 5.5f })
                torchSpots.Add((new Vector3(side * 7.6f, 5f, z), new Vector3(side * 3f, 0.3f, z)));
        torchSpots.Add((new Vector3(-4f, 6f, 17.3f), new Vector3(-1.5f, 0.8f, 15f)));
        torchSpots.Add((new Vector3(4f, 6f, 17.3f), new Vector3(1.5f, 0.8f, 15f)));
        torchSpots.Add((new Vector3(0f, 6.3f, 16.8f), new Vector3(0f, GalleryY, 15.5f)));

        int n = 0;
        foreach (var (pos, aim) in torchSpots)
        {
            var t = new GameObject($"Torch_{n++}");
            t.transform.SetParent(group, false);
            t.transform.position = pos;
            t.transform.rotation = Quaternion.LookRotation((aim - pos).normalized);
            var l = t.AddComponent<Light>();
            l.type = LightType.Spot;                       // Spot type required for cookies
            l.range = 14f;
            l.spotAngle = 75f;
            l.color = new Color(1f, 0.75f, 0.45f);
            l.shadows = LightShadows.None;
            t.AddComponent<TorchFlicker>();
        }

        // God rays from implied clerestory windows on the east wall.
        var shafts = new GameObject("[LightShafts]");
        shafts.transform.SetParent(group, false);
        var shaft = shafts.AddComponent<LightShaft>();
        var anchors = new List<Transform>();
        foreach (float z in new[] { -6.5f, -0.5f, 5.5f })
        {
            var a = new GameObject($"ShaftAnchor_{z:0}").transform;
            a.SetParent(shafts.transform, false);
            a.position = new Vector3(7.7f, 8.3f, z);
            a.rotation = Quaternion.Euler(0f, 0f, -40f);   // -Y falls down-west into the hall
            // Shaft length = component base length (6) × anchor Y — 1.2 lands
            // the cone just past the floor (7 made 42 wu spears through it).
            a.localScale = new Vector3(1.3f, 1.2f, 1.3f);
            anchors.Add(a);
        }
        var so = new SerializedObject(shaft);
        var prop = so.FindProperty("anchors");
        prop.arraySize = anchors.Count;
        for (int i = 0; i < anchors.Count; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = anchors[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        shaft.enabled = false; shaft.enabled = true;       // rebuild with anchors bound
    }

    // ── Boundaries / travel ───────────────────────────────────────────────────

    static void BuildBoundaries()
    {
        var group = new GameObject("[Boundaries]").transform;
        foreach (var (name, pos, size) in new[]
        {
            ("Bound_S", new Vector3(0f, 7f, -25f), new Vector3(24f, 14f, 1f)),
            ("Bound_N", new Vector3(0f, 7f, 22.5f), new Vector3(24f, 14f, 1f)),
            ("Bound_E", new Vector3(11.5f, 7f, 0f), new Vector3(1f, 14f, 50f)),
            ("Bound_W", new Vector3(-11.5f, 7f, 0f), new Vector3(1f, 14f, 50f)),
        })
        {
            var go = new GameObject(name);
            go.transform.SetParent(group, false);
            go.transform.position = pos;
            go.AddComponent<BoxCollider>().size = size;
        }
    }

    static void BuildTravel()
    {
        new GameObject("[ZoneEntryPlacer]").AddComponent<ZoneEntryPlacer>();

        var entry = new GameObject("ENTRY_salone_door");
        entry.transform.position = new Vector3(0f, 0.05f, -20.4f);
        var ep = entry.AddComponent<ZoneEntryPoint>();
        ep.entryId = "salone_door";
        ep.displayName = "Salone delle Arti";
        ep.faceDirection = new Vector2(0f, 1f);
        ep.fastTravelDestination = true;

        var exit = new GameObject("ExitZone_Porch");
        exit.transform.position = new Vector3(0f, 1.2f, -23.2f);
        exit.AddComponent<BoxCollider>().size = new Vector3(6f, 3f, 1.6f);
        exit.AddComponent<ZoneExit>().mode = ZoneExit.ExitMode.ToWorldMap;
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
            copy.transform.position = new Vector3(0f, source.transform.position.y, -19.5f);
            Debug.Log($"[SaloneDelleArtiSceneBuilder] Player '{copy.name}' copied from PonteVecchio.");
        }
        else Debug.LogError("[SaloneDelleArtiSceneBuilder] No Player-tagged object found in PonteVecchio!");

        EditorSceneManager.CloseScene(pv, true);
    }

    static void PlaceCameraKit()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CameraKitPath);
        if (prefab == null) { Debug.LogError($"[SaloneDelleArtiSceneBuilder] Missing {CameraKitPath}"); return; }
        var kit = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        // Interior corridors → clearance mode (HD2D_Camera.md).
        var zoom = kit.GetComponentInChildren<DynamicZoom>(true);
        if (zoom != null) zoom.useClearanceZoom = true;
    }

    // ── 3. Place Hero Props ───────────────────────────────────────────────────
    //
    // Marker consumption (Giardino idiom, interior variant): floors are flat,
    // so grounding = bounds.min.y -> marker Y (no terrain sampling). Banners
    // are NOT GLBs: each MARKER_guild-banner-* becomes a magenta-keyed quad
    // from the workbench concept image (2D pipeline — no 3D credits). The
    // chandelier hangs: its bounds CENTER goes to the marker, no grounding.

    const string PropsFolder = "Assets/Environment/SaloneDelleArti/Props";
    const string BannerFolder = "Assets/Environment/SaloneDelleArti/Banners";
    const string WorkbenchModels = "Tools/asset-gen/output/models";
    const string WorkbenchImages = "Tools/asset-gen/output/images";

    [MenuItem("InfernosCurse/Salone delle Arti/3. Place Hero Props")]
    public static void PlaceProps()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != SceneName)
        {
            Debug.LogError($"[SaloneDelleArtiSceneBuilder] Open {SceneName}.unity first.");
            return;
        }

        var markers = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None)
            .Where(g => g.name.StartsWith("MARKER_"))
            .ToArray();

        int placed = 0, pending = 0;
        foreach (var marker in markers)
        {
            if (!TryParseMarker(marker.name, out string assetId, out float targetHeight)) continue;

            if (assetId.StartsWith("guild-banner-"))
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
        Debug.Log($"[SaloneDelleArtiSceneBuilder] Props placed {placed}, pending {pending}. " +
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
        pivot.transform.SetParent(ParentFor(marker), false);

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
        bool hanging = assetId == "iron-chandelier";
        if (!hanging) EnsureCollider(mesh);

        pivot.transform.position = marker.transform.position;
        pivot.transform.rotation = marker.transform.rotation;

        if (renderers.Length > 0)
        {
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            var anchor = marker.transform.position;
            var offset = hanging
                ? b.center - anchor                                   // hangs: center on marker
                : new Vector3(b.center.x - anchor.x, b.min.y - (anchor.y + 0.01f), b.center.z - anchor.z);
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
        quad.transform.SetParent(ParentFor(marker), false);
        Object.DestroyImmediate(quad.GetComponent<Collider>());   // wall dressing — never blocks

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        var mat = new Material(shader != null ? shader : Shader.Find("Standard"));
        mat.mainTexture = tex;
        // Alpha-clipped + double-sided so the keyed cloth reads from any angle.
        mat.SetFloat("_AlphaClip", 1f);
        mat.SetFloat("_Cutoff", 0.5f);
        mat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        mat.EnableKeyword("_ALPHATEST_ON");
        quad.GetComponent<Renderer>().sharedMaterial = mat;

        // Square quad = no distortion (concepts are square with keyed margins).
        // Bottom hangs at 0.5 above the marker's floor.
        quad.transform.localScale = new Vector3(height, height, 1f);
        quad.transform.rotation = marker.transform.rotation;
        quad.transform.position = marker.transform.position + new Vector3(0f, 0.5f + height / 2f, 0f);
        return true;
    }

    // Keep gallery props under the Level2_ root (fader prefix is load-bearing).
    static Transform ParentFor(GameObject marker)
    {
        for (var t = marker.transform.parent; t != null; t = t.parent)
            if (t.name.StartsWith("Level2_")) return t;
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
            // Generous key: Gemini's "solid magenta" wobbles a little.
            if (c.r > 160 && c.b > 130 && c.g < 110 && c.r - c.g > 80 && c.b - c.g > 60)
                px[i] = new Color32(0, 0, 0, 0);
        }
        tex.SetPixels32(px);
        var outPng = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);
        return outPng;
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

    // ── 4. Setup Guilds (data + GameSystems wiring + map node) ───────────────

    [MenuItem("InfernosCurse/Salone delle Arti/4. Setup Guilds")]
    public static void SetupGuilds()
    {
        var defs = CreateGuildAssets();

        var root = PrefabUtility.LoadPrefabContents(GameSystemsPrefab);
        try
        {
            var guildSystem = root.GetComponentInChildren<GuildSystem>(true);
            if (guildSystem == null) { Debug.LogError("[SaloneDelleArtiSceneBuilder] No GuildSystem on GameSystems.prefab!"); return; }
            foreach (var def in defs)
                if (!guildSystem.guilds.Any(g => g != null && g.guildId == def.guildId))
                    guildSystem.guilds.Add(def);

            WireSpawnerZones(root);

            var hub = root.GetComponentInChildren<HubMap>(true);
            if (hub != null) WireSaloneNode(hub);
            else Debug.LogError("[SaloneDelleArtiSceneBuilder] No HubMap on GameSystems.prefab!");

            PrefabUtility.SaveAsPrefabAsset(root, GameSystemsPrefab);
            Debug.Log($"[SaloneDelleArtiSceneBuilder] GameSystems wired: {defs.Count} guild defs ensured, " +
                      "Salone zones spawned, salone_arti node registered.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void WireSpawnerZones(GameObject root)
    {
        var spawner = root.GetComponentInChildren<GuildInteractionSpawner>(true);
        if (spawner == null) { Debug.LogError("[SaloneDelleArtiSceneBuilder] No GuildInteractionSpawner on GameSystems.prefab!"); return; }

        // Idempotent rebuild of this scene's entries only. sceneName must
        // EXACTLY equal the scene asset name (the Duomo_DISABLED_ lesson).
        spawner.entries.RemoveAll(e => e.sceneName == SceneName);

        void Zone(GuildInteractionZone.Kind kind, string guildId, string label, Vector3 pos, Vector3 size)
        {
            spawner.entries.Add(new GuildInteractionSpawner.ZoneEntry
            {
                sceneName = SceneName,
                kind = kind,
                guildId = guildId,
                label = label,
                position = pos,
                triggerSize = size,
                innPrice = 0,
                isGuildInn = false,
            });
        }

        foreach (var side in new[] { -1f, 1f })
        {
            var guilds = side < 0 ? WestBays : EastBays;
            for (int i = 0; i < BayZ.Length; i++)
            {
                string id = guilds[i];
                string display = DisplayNameOf(id);
                float z = BayZ[i];
                Zone(GuildInteractionZone.Kind.Donation, id, display,
                    new Vector3(side * 6.8f, 1f, z - 1.6f), new Vector3(1.8f, 2f, 1.6f));
                Zone(GuildInteractionZone.Kind.Join, id, display,
                    new Vector3(side * 6.8f, 1f, z + 1.6f), new Vector3(1.8f, 2f, 1.6f));
            }
        }

        // Shared standings hotspot at the dais steps; Church donation in the
        // gallery chapel (Transmute stays Church-only, in the Duomo).
        Zone(GuildInteractionZone.Kind.Standings, "", "Guild Standings",
            new Vector3(3.2f, 1f, 12.8f), new Vector3(2.2f, 2f, 2f));
        Zone(GuildInteractionZone.Kind.Donation, "church", "Gallery Chapel",
            new Vector3(0f, GalleryY + 1f, 15.8f), new Vector3(3f, 2f, 2.5f));
    }

    static void WireSaloneNode(HubMap hub)
    {
        if (!hub.nodeData.Exists(n => n.id == "salone_arti"))
        {
            hub.nodeData.Add(new HubNodeData
            {
                id = "salone_arti",
                displayName = "Salone delle Arti",
                sceneName = SceneName,
                entryId = "salone_door",
                kind = NodeKind.District,
                mapLevel = MapLevel.City,
                microClimate = MicroClimate.Sheltered,     // interior — Duomo precedent
                mapImagePosition = new Vector2(0.74f, 0.30f),   // beside the rising Palazzo dei Priori
                blurb = "One roof over the seven great Arti — banners, ledgers, and the low " +
                        "murmur of half the city's money changing hands beneath the council dais.",
                population = 0.7f,
                startingCurseLevel = 0.3f,
                startingSanctity = 0.2f,
                neighborIds = new List<string> { "signoria" },
            });
            Debug.Log("[SaloneDelleArtiSceneBuilder] Added node 'salone_arti'.");
        }
    }

    // ── Guild data (7 new Arti — APPEND to albergatori/church, never rename) ──

    static string DisplayNameOf(string id) => id switch
    {
        "albergatori" => "Arte degli Albergatori",
        "calimala" => "Arte di Calimala",
        "lana" => "Arte della Lana",
        "cambio" => "Arte del Cambio",
        "giudici_notai" => "Giudici e Notai",
        "por_santa_maria" => "Por Santa Maria",
        "medici_speziali" => "Medici e Speziali",
        "vaiai_pellicciai" => "Vaiai e Pellicciai",
        _ => id,
    };

    static List<GuildDefinition> CreateGuildAssets()
    {
        var defs = new List<GuildDefinition>();

        GuildPerk P(GuildPerkType type, int rank, float mag, string flavor) =>
            new GuildPerk { type = type, unlockRank = rank, magnitude = mag, flavorText = flavor };

        // Home districts chosen one-guild-per-node (GuildForHomeNode is
        // first-match): mercato=albergatori, duomo=church already taken.
        defs.Add(Guild("Calimala", "calimala", "Arte di Calimala",
            "The refiners of foreign cloth — the oldest and proudest of the Arti. " +
            "Their fulling mills beat wool in the streams below Fiesole, and their " +
            "seal opens markets from London to the Levant.",
            "fiesole", new[] { 0, 60, 180, 420, 850 }, 3,
            new[] { "Straniero", "Apprendista", "Panniere", "Mercante d'Oltremonte", "Console di Calimala" },
            new[]
            {
                P(GuildPerkType.InnPriceMultiplier, 2, 0.85f, "A Calimala seal on the purse — hosts sharpen their prices."),
                P(GuildPerkType.DonationRepBonus, 3, 1.25f, "The Arte remembers a generous hand twice as long."),
                P(GuildPerkType.RestCurseCostMultiplier, 4, 0.8f, "Foreign linens, washed and blessed — sleep sits lighter on you."),
            }));

        defs.Add(Guild("Lana", "lana", "Arte della Lana",
            "The wool guild — a third of Florence spins, weaves, or dyes for them. " +
            "Their workshops crowd the south bank, and their say in the city's " +
            "councils is worth more than most parishes' prayers.",
            "oltrarno", new[] { 0, 50, 150, 350, 700 }, 2,
            new[] { "Straniero", "Battilano", "Lanaiolo", "Maestro dell'Arte", "Console della Lana" },
            new[]
            {
                P(GuildPerkType.RestCurseCostMultiplier, 1, 0.9f, "Wool of the Arte warms without whispering."),
                P(GuildPerkType.InnRestCleansePercent, 3, 0.05f, "Where the Lana lodges, the quarter's stain thins a little."),
                P(GuildPerkType.DonationRepBonus, 4, 1.2f, "The looms speak well of those who keep them fed."),
            }));

        defs.Add(Guild("Cambio", "cambio", "Arte del Cambio",
            "The money changers. Their benches by the Palazzo dei Priori turn " +
            "florins into influence at rates the priests call usury and the " +
            "priors call Tuesday.",
            "signoria", new[] { 0, 60, 180, 420, 850 }, 3,
            new[] { "Straniero", "Contatore", "Cambiatore", "Banchiere", "Console del Cambio" },
            new[]
            {
                P(GuildPerkType.DonationRepBonus, 2, 1.3f, "Bankers grade gratitude by the ledger — and pay it back with interest."),
                P(GuildPerkType.InnPriceMultiplier, 3, 0.8f, "No host quibbles with a man who sets the exchange rate."),
                P(GuildPerkType.RestCurseCostMultiplier, 4, 0.85f, "Debts squared by dusk make for quiet nights."),
            }));

        defs.Add(Guild("GiudiciNotai", "giudici_notai", "Giudici e Notai",
            "Judges and notaries — the guild of the written word. Nothing in " +
            "Florence is sold, wed, or buried without their ink, and they never " +
            "forget whose name is on which page.",
            "santacroce", new[] { 0, 60, 180, 420, 850 }, 2,
            new[] { "Straniero", "Scrivano", "Notaio", "Giudice", "Proconsolo" },
            new[]
            {
                P(GuildPerkType.InnPriceMultiplier, 2, 0.9f, "A notary's companion signs nothing twice — and is not overcharged."),
                P(GuildPerkType.DonationRepBonus, 3, 1.25f, "Gifts entered into the record accrue like compound clauses."),
                P(GuildPerkType.RestCurseCostMultiplier, 4, 0.85f, "Contracts sealed in good faith keep bad dreams at the door."),
            }));

        defs.Add(Guild("PorSantaMaria", "por_santa_maria", "Por Santa Maria",
            "Silk weavers and goldsmiths of the street running down to the old " +
            "bridge — everything that glitters between the Mercato and the Arno " +
            "passes through their hands.",
            "pontevecchio", new[] { 0, 50, 150, 350, 700 }, 2,
            new[] { "Straniero", "Filatore", "Setaiolo", "Maestro Setaiolo", "Console della Porta" },
            new[]
            {
                P(GuildPerkType.DonationRepBonus, 1, 1.15f, "The silk-weavers notice a bright thread in any purse."),
                P(GuildPerkType.InnPriceMultiplier, 3, 0.85f, "Guild silk on your shoulders is worth a discount at any door."),
                P(GuildPerkType.RestCurseCostMultiplier, 4, 0.85f, "Silk-hung chambers — the night slides off like water."),
            }));

        defs.Add(Guild("MediciSpeziali", "medici_speziali", "Medici e Speziali",
            "Physicians, apothecaries, and painters share one guild and one " +
            "truth: everything is a matter of the right mixture. Their spice " +
            "stalls perfume the parish of San Lorenzo.",
            "sanlorenzo", new[] { 0, 50, 150, 350, 700 }, 2,
            new[] { "Straniero", "Garzone di Bottega", "Speziale", "Medico", "Console degli Speziali" },
            new[]
            {
                P(GuildPerkType.RestCurseCostMultiplier, 1, 0.85f, "A spiced draught before bed — the rot forgets your name till morning."),
                P(GuildPerkType.InnRestCleansePercent, 2, 0.04f, "Fumigations of the Arte sweeten the whole house."),
                P(GuildPerkType.RestCurseCostMultiplier, 4, 0.7f, "The Speziali's tinctures make even cursed sleep nearly clean."),
            }));

        defs.Add(Guild("VaiaiPellicciai", "vaiai_pellicciai", "Vaiai e Pellicciai",
            "Furriers of vair and miniver — the softest guild with the hardest " +
            "eyes. Winter is their season, and in Florence lately, something " +
            "like winter is always coming.",
            "novella", new[] { 0, 50, 150, 350, 700 }, 2,
            new[] { "Straniero", "Conciatore", "Pellicciaio", "Maestro Vaiaio", "Console dei Vaiai" },
            new[]
            {
                P(GuildPerkType.RestCurseCostMultiplier, 1, 0.9f, "Vair-lined blankets — winter and worse stay outside."),
                P(GuildPerkType.InnPriceMultiplier, 3, 0.85f, "Furriers' friends are shown to the warm rooms."),
                P(GuildPerkType.DonationRepBonus, 4, 1.2f, "A patron in ermine is a patron remembered."),
            }));

        return defs;
    }

    static GuildDefinition Guild(string fileName, string id, string display, string blurb,
        string homeNode, int[] ladder, int florinsPerRep, string[] ranks, GuildPerk[] perks)
    {
        string path = $"{GuildAssetDir}/Guild_{fileName}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<GuildDefinition>(path);
        if (existing != null) return existing;   // skip-if-exists — never stomp tuning

        var def = ScriptableObject.CreateInstance<GuildDefinition>();
        def.guildId = id;
        def.displayName = display;
        def.blurb = blurb;
        def.homeNodeId = homeNode;
        def.repPerRank = ladder;
        def.rankNames = ranks;
        def.repPerHomeKill = 5;
        def.florinsPerRep = florinsPerRep;
        def.perks = new List<GuildPerk>(perks);
        AssetDatabase.CreateAsset(def, path);
        Debug.Log($"[SaloneDelleArtiSceneBuilder] Created {path}");
        return def;
    }

    // ── 5. Floor/Wall material pass (DuomoTilePass pattern) ──────────────────
    //
    // Workbench 2D tiles (status stays 'generated' — never approved into the
    // 3D batch): terracotta floor world-aligned per piece so the grid flows
    // across slab joints; pietra serena walls binned by integer repeats so the
    // coursing never stretches.

    const string TexDir = "Assets/Environment/SaloneDelleArti/Textures";
    const string MatDir = "Assets/Environment/SaloneDelleArti/Materials";
    const float FloorTileWu = 2.4f;
    const float WallTileWu = 2.5f;

    static readonly string[] FloorPieces = { "Floor_Salone", "Floor_Vestibule", "Floor_Apron" };
    static readonly string[] WallPrefixes = { "Wall_", "Bay_", "Apse_", "Vestibule_", "Shell_", "Level2_Shell" };

    [MenuItem("InfernosCurse/Salone delle Arti/5. Floor Wall Materials")]
    public static void MaterialPass()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != SceneName)
        {
            Debug.LogError($"[SaloneDelleArtiSceneBuilder] Open {SceneName}.unity first.");
            return;
        }

        var texFloor = ImportTile("salone-floor-terracotta");
        var texWall = ImportTile("salone-wall-pietra-serena");
        if (texFloor == null || texWall == null)
        {
            Debug.LogError("[SaloneDelleArtiSceneBuilder] Tile textures missing from workbench output — aborting.");
            return;
        }
        CreateFolders(MatDir);
        var shader = Shader.Find("Universal Render Pipeline/Lit");

        int floors = 0;
        foreach (var name in FloorPieces)
        {
            var go = GameObject.Find(name);
            if (go == null) continue;
            var r = go.GetComponent<Renderer>();
            var b = r.bounds;
            var mat = new Material(shader) { name = $"Gen_Floor_{name}" };
            mat.SetTexture("_BaseMap", texFloor);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0.18f);
            mat.SetTextureScale("_BaseMap", new Vector2(b.size.x / FloorTileWu, b.size.z / FloorTileWu));
            mat.SetTextureOffset("_BaseMap", new Vector2(
                Mathf.Repeat(b.min.x / FloorTileWu, 1f), Mathf.Repeat(b.min.z / FloorTileWu, 1f)));
            AssetDatabase.CreateAsset(mat, $"{MatDir}/Gen_Floor_{name}.mat");
            r.sharedMaterial = mat;
            floors++;
        }

        var wallMats = new Dictionary<(int, int), Material>();
        int walls = 0;
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            var n = r.gameObject.name;
            bool isWall = false;
            foreach (var p in WallPrefixes)
                if (n.StartsWith(p) && n != "Apse_Plinth") { isWall = true; break; }
            if (!isWall) continue;

            var s = r.transform.localScale;
            float len = Mathf.Max(s.x, s.z);
            int repX = Mathf.Max(1, Mathf.RoundToInt(len / WallTileWu));
            int repY = Mathf.Max(1, Mathf.RoundToInt(s.y / WallTileWu));
            if (!wallMats.TryGetValue((repX, repY), out var mat))
            {
                mat = new Material(shader) { name = $"Gen_Wall_{repX}x{repY}" };
                mat.SetTexture("_BaseMap", texWall);
                mat.SetColor("_BaseColor", Color.white);
                mat.SetFloat("_Smoothness", 0.10f);
                mat.SetTextureScale("_BaseMap", new Vector2(repX, repY));
                AssetDatabase.CreateAsset(mat, $"{MatDir}/Gen_Wall_{repX}x{repY}.mat");
                wallMats[(repX, repY)] = mat;
            }
            r.sharedMaterial = mat;
            walls++;
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[SaloneDelleArtiSceneBuilder] Material pass: floors={floors} walls={walls} bins={wallMats.Count}.");
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

    static readonly Color Terracotta = new Color(0.60f, 0.38f, 0.26f);
    static readonly Color Stone = new Color(0.55f, 0.54f, 0.50f);
    static readonly Color LightStone = new Color(0.66f, 0.63f, 0.57f);
    static readonly Color DarkStone = new Color(0.48f, 0.47f, 0.44f);

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

    // A cube segment on a circle: local Z = radial (out from center), local X =
    // tangent. size = (tangential, height, radialThickness).
    static void ArcBox(Transform parent, string name, Vector3 center, float angleDeg, float rCenter,
        float tangential, float height, float radial, float yCenter, Color color)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        var dir = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(center.x, 0f, center.z) + dir * rCenter + Vector3.up * yCenter;
        go.transform.rotation = Quaternion.LookRotation(dir);
        go.transform.localScale = new Vector3(tangential, height, radial);
        Tint(go, color);
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
        Debug.Log($"[SaloneDelleArtiSceneBuilder] Added {scenePath} to Build Settings.");
    }
}
