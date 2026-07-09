using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Builds the approved Option-B adaptation of Refrences/maps/inn.png.
// Floor 1 is a safe social hub: no battle authoring, no encounter spawns.
public static class FlorentineInnFloor1Builder
{
    const string ScenePath = "Assets/Scenes/FlorentineInnFloor1.unity";
    const string PlayerSourceScene = "Assets/Scenes/PonteVecchio.unity";
    const string CameraKitPath = "Assets/Prefabs/HD2D_CameraKit.prefab";
    const string MaterialDir = "Assets/Environment/FlorentineInnFloor1/Materials";

    const float Half = 11f;
    const float WallH = 3.6f;
    const float WallT = 0.35f;

    static Material _plaster;
    static Material _stone;
    static Material _terracotta;
    static Material _tile;
    static Material _wood;
    static Material _darkWood;
    static Material _courtyard;
    static Material _water;
    static Material _green;
    static Material _npc;
    static Material _serviceNpc;
    static Material _locked;

    [MenuItem("InfernosCurse/Florentine Inn/1. Build Floor 1")]
    public static void Build()
    {
        EnsureMaterials();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SceneManager.SetActiveScene(scene);

        var architecture = new GameObject("[Architecture]").transform;
        BuildFloors(architecture);
        BuildWalls(architecture);
        BuildCourtyardArcade(architecture);
        BuildStairs(architecture);

        var props = new GameObject("[Props]").transform;
        BuildReception(props);
        BuildSalon(props);
        BuildDining(props);
        BuildKitchenAndService(props);
        BuildOffice(props);
        BuildCourtyard(props);

        BuildNpcMarkers();
        BuildInteractions();
        BuildTravel();
        BuildLighting();
        CopyPlayerFromPonteVecchio(scene);
        PlaceCameraKit();

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuildSettings(ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("[FlorentineInnFloor1Builder] Built playable Floor 1 safe hub: architecture, props, NPC markers, rest service, travel, player, camera, and locked upper landing.");
    }

    static void BuildFloors(Transform parent)
    {
        var floors = new GameObject("Floors").transform;
        floors.SetParent(parent, false);

        Box(floors, "Floor_Base", new Vector3(0f, -0.24f, 0f), new Vector3(22.7f, 0.45f, 22.7f), _stone);
        Box(floors, "Floor_StreetApron", new Vector3(0f, -0.18f, -12.25f), new Vector3(6f, 0.35f, 2.2f), _stone);

        Floor(floors, "Floor_Salon", new Vector3(-7.4f, 0f, -7.5f), new Vector2(7.2f, 7f), _tile);
        Floor(floors, "Floor_Reception", new Vector3(0.35f, 0f, -7.5f), new Vector2(8.3f, 7f), _tile);
        Floor(floors, "Floor_Office", new Vector3(7.75f, 0f, -7.5f), new Vector2(6.5f, 7f), _tile);
        Floor(floors, "Floor_StairsWC", new Vector3(-7.4f, 0f, -2f), new Vector2(7.2f, 4f), _tile);
        Floor(floors, "Floor_Dining", new Vector3(-7.4f, 0f, 5.5f), new Vector2(7.2f, 11f), _tile);
        Floor(floors, "Floor_Courtyard", new Vector3(0.35f, 0f, -0.4f), new Vector2(8.3f, 7.2f), _courtyard);
        Floor(floors, "Floor_Kitchen", new Vector3(1f, 0f, 7.1f), new Vector2(9.6f, 7.8f), _terracotta);
        Floor(floors, "Floor_Storage", new Vector3(7.75f, 0f, -1f), new Vector2(6.5f, 6f), _terracotta);
        Floor(floors, "Floor_Staff", new Vector3(8.4f, 0f, 4.5f), new Vector2(5.2f, 5f), _terracotta);
        Floor(floors, "Floor_Pantry", new Vector3(8.4f, 0f, 9f), new Vector2(5.2f, 4f), _terracotta);
    }

    static void BuildWalls(Transform parent)
    {
        var walls = new GameObject("Walls").transform;
        walls.SetParent(parent, false);

        // Exterior shell. South entry remains four metres wide for the camera and player.
        WallZ(walls, "InnWall_S_W", -11.175f, -6.6f, 9.2f);
        WallZ(walls, "InnWall_S_E", -11.175f, 6.6f, 9.2f);
        WallZ(walls, "InnWall_N", 11.175f, 0f, 22.35f);
        WallX(walls, "InnWall_W", -11.175f, 0f, 22.35f);
        WallX(walls, "InnWall_E", 11.175f, 0f, 22.35f);

        // South public rooms to the central band: three generous door openings.
        WallZ(walls, "InnWall_Zm4_A", -4f, -9.5f, 3f);
        WallZ(walls, "InnWall_Zm4_B", -4f, -3.7f, 4.6f);
        WallZ(walls, "InnWall_Zm4_C", -4f, 4.1f, 5.8f);
        WallZ(walls, "InnWall_Zm4_D", -4f, 9.9f, 2.2f);

        // Salon / reception and reception / office partitions.
        WallX(walls, "InnWall_Xm38_S1", -3.8f, -9.2f, 3.6f);
        WallX(walls, "InnWall_Xm38_S2", -3.8f, -4.8f, 1.6f);
        WallX(walls, "InnWall_X45_S1", 4.5f, -9.2f, 3.6f);
        WallX(walls, "InnWall_X45_S2", 4.5f, -4.8f, 1.6f);

        // West stair/dining split with a door near the courtyard.
        WallZ(walls, "InnWall_WestBand_A", 0f, -9.2f, 3.6f);
        WallZ(walls, "InnWall_WestBand_B", 0f, -4.7f, 1.8f);

        // Dining / kitchen partition with a service opening.
        WallX(walls, "InnWall_Xm38_N1", -3.8f, 4.7f, 3f);
        WallX(walls, "InnWall_Xm38_N2", -3.8f, 9.6f, 2.8f);

        // East service spine and its room splits.
        WallX(walls, "InnWall_X58_N1", 5.8f, 4.1f, 1.8f);
        WallX(walls, "InnWall_X58_N2", 5.8f, 8.9f, 4.2f);
        WallZ(walls, "InnWall_Service_Z2_A", 2f, 7f, 2.4f);
        WallZ(walls, "InnWall_Service_Z2_B", 2f, 10.3f, 1.4f);
        WallZ(walls, "InnWall_Service_Z7_A", 7f, 7f, 2.4f);
        WallZ(walls, "InnWall_Service_Z7_B", 7f, 10.3f, 1.4f);

        // WC enclosure under the staircase.
        WallX(walls, "InnWall_WC_E", -8.2f, -2.9f, 2.2f);
        WallZ(walls, "InnWall_WC_N", -1.8f, -9.7f, 2.9f);
    }

    static void BuildCourtyardArcade(Transform parent)
    {
        var arcade = new GameObject("Courtyard_Arcade").transform;
        arcade.SetParent(parent, false);

        foreach (float x in new[] { -3.55f, 0.35f, 4.25f })
        {
            Column(arcade, $"CourtyardColumn_S_{x:0.0}", new Vector3(x, 0f, -3.75f));
            Column(arcade, $"CourtyardColumn_N_{x:0.0}", new Vector3(x, 0f, 2.95f));
        }
        foreach (float z in new[] { -1.2f, 1.25f })
        {
            Column(arcade, $"CourtyardColumn_W_{z:0.0}", new Vector3(-3.55f, 0f, z));
            Column(arcade, $"CourtyardColumn_E_{z:0.0}", new Vector3(4.25f, 0f, z));
        }

        // High lintels read as arches without blocking the player path.
        Box(arcade, "CourtyardLintel_S", new Vector3(0.35f, 3.05f, -3.75f), new Vector3(8.2f, 0.35f, 0.3f), _stone);
        Box(arcade, "CourtyardLintel_N", new Vector3(0.35f, 3.05f, 2.95f), new Vector3(8.2f, 0.35f, 0.3f), _stone);
        Box(arcade, "CourtyardLintel_W", new Vector3(-3.55f, 3.05f, -0.4f), new Vector3(0.3f, 0.35f, 6.4f), _stone);
        Box(arcade, "CourtyardLintel_E", new Vector3(4.25f, 3.05f, -0.4f), new Vector3(0.3f, 0.35f, 6.4f), _stone);
    }

    static void BuildStairs(Transform parent)
    {
        var stairs = new GameObject("Stairs_FutureFloor2").transform;
        stairs.SetParent(parent, false);

        const int count = 12;
        const float rise = 0.25f;
        const float run = 0.30f;
        for (int i = 0; i < count; i++)
        {
            float top = (i + 1) * rise;
            float z = -3.55f + i * run;
            Box(stairs, $"Stair_{i + 1:00}", new Vector3(-6.4f, top / 2f, z),
                new Vector3(2.5f, top, run + 0.03f), _stone);
        }

        Box(stairs, "Floor2_LockedLanding", new Vector3(-6.4f, 2.88f, 0.5f), new Vector3(2.8f, 0.25f, 1.4f), _stone);
        Box(stairs, "Floor2_LockedBarrier", new Vector3(-6.4f, 3.75f, 1.15f), new Vector3(2.8f, 1.6f, 0.25f), _locked);
        Box(stairs, "Floor2_ComingLater_Plaque", new Vector3(-6.4f, 4.05f, 0.98f), new Vector3(1.5f, 0.45f, 0.08f), _darkWood, collider: false);
    }

    static void BuildReception(Transform parent)
    {
        var root = RoomRoot(parent, "Reception");
        var counter = Box(root, "ReceptionCounter_Main", new Vector3(0.2f, 0.55f, -6.1f), new Vector3(5.8f, 1.1f, 0.8f), _darkWood);
        var innInteraction = counter.AddComponent<InnCounterInteraction>();
        innInteraction.innName = "Albergo Fiorentino";
        innInteraction.innPrice = 10;
        innInteraction.isGuildInn = true;
        innInteraction.prompt = "Speak to Innkeeper";
        Box(root, "ReceptionCounter_W", new Vector3(-2.35f, 0.55f, -5.45f), new Vector3(0.7f, 1.1f, 1.4f), _darkWood);
        Box(root, "ReceptionCounter_E", new Vector3(2.75f, 0.55f, -5.45f), new Vector3(0.7f, 1.1f, 1.4f), _darkWood);
        Box(root, "KeyCubbies", new Vector3(0.2f, 1.45f, -4.35f), new Vector3(4.2f, 2.3f, 0.3f), _wood);
        // Keep the central entrance axis clear from spawn to the reception desk.
        Box(root, "LobbyBench", new Vector3(2.45f, 0.35f, -9.15f), new Vector3(2.2f, 0.7f, 0.7f), _wood);
        Plant(root, "ReceptionPlant_W", new Vector3(-3f, 0f, -9.4f));
        Plant(root, "ReceptionPlant_E", new Vector3(3.6f, 0f, -9.4f));
    }

    static void BuildSalon(Transform parent)
    {
        var root = RoomRoot(parent, "Salon");
        Box(root, "SalonRug", new Vector3(-7.4f, 0.08f, -7.4f), new Vector3(4.8f, 0.08f, 3.8f), _locked, collider: false);
        Table(root, "SalonTeaTable", new Vector3(-7.4f, 0f, -7.3f), new Vector2(1.4f, 1.1f));
        Chair(root, "SalonChair_NW", new Vector3(-8.8f, 0f, -6.3f), 135f);
        Chair(root, "SalonChair_NE", new Vector3(-6f, 0f, -6.3f), 225f);
        Chair(root, "SalonChair_S", new Vector3(-7.4f, 0f, -8.7f), 0f);
        Box(root, "SalonBookcase", new Vector3(-10.55f, 1.2f, -7.5f), new Vector3(0.4f, 2.4f, 4f), _darkWood);
        Plant(root, "SalonPlant", new Vector3(-10f, 0f, -10f));
    }

    static void BuildDining(Transform parent)
    {
        var root = RoomRoot(parent, "Dining");
        int n = 0;
        foreach (float z in new[] { 2f, 5.3f, 8.6f })
            foreach (float x in new[] { -9f, -5.8f })
            {
                n++;
                Table(root, $"DiningTable_{n:00}", new Vector3(x, 0f, z), new Vector2(1.8f, 1.2f));
                Chair(root, $"DiningChair_{n:00}_N", new Vector3(x, 0f, z + 1f), 180f);
                Chair(root, $"DiningChair_{n:00}_S", new Vector3(x, 0f, z - 1f), 0f);
            }
        Box(root, "DiningSideboard", new Vector3(-10.5f, 0.65f, 5.4f), new Vector3(0.55f, 1.3f, 4.2f), _darkWood);
    }

    static void BuildKitchenAndService(Transform parent)
    {
        var kitchen = RoomRoot(parent, "Kitchen");
        Box(kitchen, "KitchenCounter_N", new Vector3(1f, 0.55f, 10.3f), new Vector3(8.5f, 1.1f, 0.75f), _stone);
        Box(kitchen, "KitchenCounter_W", new Vector3(-2.9f, 0.55f, 7.3f), new Vector3(0.75f, 1.1f, 4.8f), _stone);
        Table(kitchen, "KitchenIsland", new Vector3(1.2f, 0f, 7.3f), new Vector2(3.6f, 1.5f));
        Box(kitchen, "KitchenHearth", new Vector3(4.7f, 1f, 10.25f), new Vector3(1.8f, 2f, 0.85f), _darkWood);

        var service = RoomRoot(parent, "Service");
        Table(service, "StaffWorkTable", new Vector3(8.3f, 0f, 4.5f), new Vector2(2.2f, 1.1f));
        Box(service, "PantryShelves_E", new Vector3(10.5f, 1.25f, 9f), new Vector3(0.45f, 2.5f, 3.2f), _wood);
        Box(service, "PantryShelves_N", new Vector3(8.3f, 1.25f, 10.55f), new Vector3(3.2f, 2.5f, 0.45f), _wood);
        for (int i = 0; i < 5; i++)
            Barrel(service, $"StorageBarrel_{i + 1:00}", new Vector3(6.7f + (i % 2) * 1.2f, 0f, -2.7f + (i / 2) * 1.15f));
        Box(service, "StorageCrates", new Vector3(10f, 0.55f, -1.2f), new Vector3(1.5f, 1.1f, 2.5f), _wood);
    }

    static void BuildOffice(Transform parent)
    {
        var root = RoomRoot(parent, "Office");
        Table(root, "OfficeDesk", new Vector3(7.5f, 0f, -7.2f), new Vector2(2.7f, 1.3f));
        Chair(root, "OfficeChair", new Vector3(7.5f, 0f, -5.9f), 180f);
        Box(root, "OfficeBookcase_E", new Vector3(10.5f, 1.35f, -7.5f), new Vector3(0.45f, 2.7f, 4.2f), _darkWood);
        Box(root, "OfficeChest", new Vector3(6.6f, 0.45f, -9.9f), new Vector3(1.8f, 0.9f, 0.9f), _wood);
    }

    static void BuildCourtyard(Transform parent)
    {
        var root = RoomRoot(parent, "Courtyard");
        Cylinder(root, "CourtyardFountain_Basin", new Vector3(0.35f, 0.22f, -0.4f), new Vector3(2.4f, 0.22f, 2.4f), _stone);
        Cylinder(root, "CourtyardFountain_Water", new Vector3(0.35f, 0.47f, -0.4f), new Vector3(1.9f, 0.06f, 1.9f), _water, collider: false);
        Cylinder(root, "CourtyardFountain_Pedestal", new Vector3(0.35f, 0.8f, -0.4f), new Vector3(0.45f, 0.8f, 0.45f), _stone);
        foreach (var pos in new[]
        {
            new Vector3(-2.7f, 0f, -2.8f), new Vector3(3.4f, 0f, -2.8f),
            new Vector3(-2.7f, 0f, 2f), new Vector3(3.4f, 0f, 2f),
        }) Plant(root, "CourtyardPlant", pos);
    }

    static void BuildNpcMarkers()
    {
        var root = new GameObject("[NPCs]").transform;
        Npc(root, "NPC_Innkeeper", new Vector3(0.2f, 0f, -5.1f), _npc);
        Npc(root, "NPC_Cook", new Vector3(2.5f, 0f, 8.4f), _serviceNpc);
        Npc(root, "NPC_Server", new Vector3(8.2f, 0f, 4.4f), _serviceNpc);
        Npc(root, "NPC_Guest_Dining", new Vector3(-7.4f, 0f, 4f), _npc);
        Npc(root, "NPC_Guest_Salon", new Vector3(-6f, 0f, -8.5f), _npc);
    }

    static void BuildInteractions()
    {
        var root = new GameObject("[Interactions]").transform;
        var landing = new GameObject("Floor2LockedInteraction");
        landing.transform.SetParent(root, false);
        landing.transform.position = new Vector3(-6.4f, 3.5f, 0.25f);
    }

    static void BuildTravel()
    {
        var root = new GameObject("[Travel]").transform;
        new GameObject("[ZoneEntryPlacer]").AddComponent<ZoneEntryPlacer>().transform.SetParent(root, false);

        var entry = new GameObject("ENTRY_florentine_inn_street");
        entry.transform.SetParent(root, false);
        entry.transform.position = new Vector3(0f, 0.05f, -9.7f);
        var ep = entry.AddComponent<ZoneEntryPoint>();
        ep.entryId = "florentine_inn_street";
        ep.displayName = "Albergo Fiorentino";
        ep.faceDirection = Vector2.up;
        ep.fastTravelDestination = true;

        var exit = new GameObject("ExitZone_Street");
        exit.transform.SetParent(root, false);
        exit.transform.position = new Vector3(0f, 1.2f, -12.45f);
        exit.AddComponent<BoxCollider>().size = new Vector3(5.5f, 3f, 1.4f);
        exit.AddComponent<ZoneExit>().mode = ZoneExit.ExitMode.ToWorldMap;
    }

    static void BuildLighting()
    {
        var root = new GameObject("[Lighting]").transform;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.34f, 0.30f, 0.25f);

        var sun = new GameObject("Courtyard Sun");
        sun.transform.SetParent(root, false);
        sun.transform.rotation = Quaternion.Euler(48f, -28f, 0f);
        var directional = sun.AddComponent<Light>();
        directional.type = LightType.Directional;
        directional.color = new Color(1f, 0.93f, 0.78f);
        directional.intensity = 1.05f;
        directional.shadows = LightShadows.Soft;

        Point(root, "Reception Lamp", new Vector3(0.2f, 2.8f, -7.2f), 5.5f, 7f);
        Point(root, "Salon Lamp", new Vector3(-7.4f, 2.8f, -7.3f), 4.5f, 6f);
        Point(root, "Dining Lamp", new Vector3(-7.4f, 2.8f, 5.3f), 5.5f, 7f);
        Point(root, "Kitchen Lamp", new Vector3(1f, 2.8f, 7.3f), 5f, 6f);
        Point(root, "Office Lamp", new Vector3(7.5f, 2.8f, -7.2f), 4f, 5f);
    }

    static void CopyPlayerFromPonteVecchio(Scene target)
    {
        var sourceScene = EditorSceneManager.OpenScene(PlayerSourceScene, OpenSceneMode.Additive);
        GameObject source = null;
        foreach (var root in sourceScene.GetRootGameObjects())
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
            copy.transform.position = new Vector3(0f, source.transform.position.y, -9.7f);
            if (copy.GetComponent<PlayerWorldInteractor>() == null)
                copy.AddComponent<PlayerWorldInteractor>();
            Debug.Log($"[FlorentineInnFloor1Builder] Player '{copy.name}' copied from PonteVecchio.");
        }
        else Debug.LogError("[FlorentineInnFloor1Builder] No Player-tagged object found in PonteVecchio.");

        EditorSceneManager.CloseScene(sourceScene, true);
    }

    static void PlaceCameraKit()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CameraKitPath);
        if (prefab == null) { Debug.LogError($"[FlorentineInnFloor1Builder] Missing {CameraKitPath}"); return; }

        var kit = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        var zoom = kit.GetComponentInChildren<DynamicZoom>(true);
        if (zoom != null) zoom.useClearanceZoom = true;

        var camera = kit.GetComponentInChildren<Camera>(true);
        if (camera != null)
        {
            var fader = camera.gameObject.GetComponent<CameraOcclusionFader>();
            if (fader == null) fader = camera.gameObject.AddComponent<CameraOcclusionFader>();
            fader.wallPrefixes = new[] { "InnWall_", "CourtyardLintel_" };
            fader.probeRadius = 0.55f;
        }
    }

    static void EnsureMaterials()
    {
        EnsureFolder("Assets/Environment");
        EnsureFolder("Assets/Environment/FlorentineInnFloor1");
        EnsureFolder(MaterialDir);

        _plaster = MaterialAsset("Inn_Plaster", new Color(0.79f, 0.70f, 0.55f), 0.05f);
        _stone = MaterialAsset("Inn_PietraSerena", new Color(0.39f, 0.40f, 0.38f), 0.18f);
        _terracotta = MaterialAsset("Inn_Terracotta", new Color(0.58f, 0.25f, 0.13f), 0.12f);
        _tile = MaterialAsset("Inn_PublicTile", new Color(0.71f, 0.65f, 0.50f), 0.2f);
        _wood = MaterialAsset("Inn_Wood", new Color(0.34f, 0.17f, 0.07f), 0.16f);
        _darkWood = MaterialAsset("Inn_DarkWood", new Color(0.18f, 0.075f, 0.03f), 0.2f);
        _courtyard = MaterialAsset("Inn_CourtyardBrick", new Color(0.48f, 0.24f, 0.16f), 0.1f);
        _water = MaterialAsset("Inn_Water", new Color(0.18f, 0.48f, 0.63f), 0.75f);
        _green = MaterialAsset("Inn_Plants", new Color(0.14f, 0.36f, 0.12f), 0.1f);
        _npc = MaterialAsset("Inn_NPC_Guest", new Color(0.52f, 0.20f, 0.16f), 0.1f);
        _serviceNpc = MaterialAsset("Inn_NPC_Staff", new Color(0.16f, 0.29f, 0.48f), 0.1f);
        _locked = MaterialAsset("Inn_Accent", new Color(0.52f, 0.36f, 0.11f), 0.25f);
    }

    static Material MaterialAsset(string name, Color color, float smoothness)
    {
        string path = $"{MaterialDir}/{name}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        material = new Material(shader) { name = name, color = color };
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    static void Floor(Transform parent, string name, Vector3 center, Vector2 size, Material material) =>
        Box(parent, name, new Vector3(center.x, -0.04f, center.z), new Vector3(size.x, 0.1f, size.y), material);

    static void WallX(Transform parent, string name, float x, float centerZ, float length) =>
        Box(parent, name, new Vector3(x, WallH / 2f, centerZ), new Vector3(WallT, WallH, length), _plaster);

    static void WallZ(Transform parent, string name, float z, float centerX, float length) =>
        Box(parent, name, new Vector3(centerX, WallH / 2f, z), new Vector3(length, WallH, WallT), _plaster);

    static void Column(Transform parent, string name, Vector3 position)
    {
        Cylinder(parent, name, position + Vector3.up * 1.5f, new Vector3(0.55f, 1.5f, 0.55f), _stone);
        Cylinder(parent, name + "_Base", position + Vector3.up * 0.12f, new Vector3(0.8f, 0.12f, 0.8f), _stone);
    }

    static Transform RoomRoot(Transform parent, string name)
    {
        var root = new GameObject(name).transform;
        root.SetParent(parent, false);
        return root;
    }

    static void Table(Transform parent, string name, Vector3 position, Vector2 top)
    {
        Box(parent, name + "_Top", position + Vector3.up * 0.75f, new Vector3(top.x, 0.16f, top.y), _wood);
        foreach (float x in new[] { -top.x * 0.36f, top.x * 0.36f })
            foreach (float z in new[] { -top.y * 0.34f, top.y * 0.34f })
                Box(parent, name + "_Leg", position + new Vector3(x, 0.37f, z), new Vector3(0.12f, 0.74f, 0.12f), _darkWood);
    }

    static void Chair(Transform parent, string name, Vector3 position, float yaw)
    {
        var root = new GameObject(name).transform;
        root.SetParent(parent, false);
        root.position = position;
        root.rotation = Quaternion.Euler(0f, yaw, 0f);
        Box(root, "Seat", new Vector3(0f, 0.48f, 0f), new Vector3(0.65f, 0.14f, 0.65f), _wood);
        Box(root, "Back", new Vector3(0f, 0.95f, 0.28f), new Vector3(0.65f, 0.9f, 0.12f), _wood);
    }

    static void Barrel(Transform parent, string name, Vector3 position) =>
        Cylinder(parent, name, position + Vector3.up * 0.55f, new Vector3(0.7f, 0.55f, 0.7f), _wood);

    static void Plant(Transform parent, string name, Vector3 position)
    {
        Cylinder(parent, name + "_Pot", position + Vector3.up * 0.28f, new Vector3(0.55f, 0.28f, 0.55f), _terracotta);
        Sphere(parent, name + "_Leaves", position + Vector3.up * 0.95f, new Vector3(0.9f, 1.2f, 0.9f), _green, collider: false);
    }

    static void Npc(Transform parent, string name, Vector3 position, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = position + Vector3.up;
        go.transform.localScale = new Vector3(0.55f, 1f, 0.55f);
        go.GetComponent<Renderer>().sharedMaterial = material;
    }

    static void Point(Transform parent, string name, Vector3 position, float intensity, float range)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.72f, 0.42f);
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.None;
    }

    static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size, Material material, bool collider = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = material;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static GameObject Cylinder(Transform parent, string name, Vector3 position, Vector3 scale, Material material, bool collider = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static GameObject Sphere(Transform parent, string name, Vector3 position, Vector3 scale, Material material, bool collider = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        string parent = path.Substring(0, slash);
        string leaf = path.Substring(slash + 1);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }

    static void AddToBuildSettings(string scenePath)
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == scenePath)) return;
        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
