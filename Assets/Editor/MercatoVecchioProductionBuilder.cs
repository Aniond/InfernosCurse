using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public static class MercatoVecchioProductionBuilder
{
    public const string ScenePath = "Assets/Scenes/MercatoVecchio.unity";
    public static readonly Vector2 BattleOrigin = new(-50f, -32f);
    public const int BattleWidth = 90;
    public const int BattleHeight = 64;
    const string ModulePath = "Assets/Environment/FlorentineInnFloor1/Prefabs/FlorentineInnFloor1_Module.prefab";
    const string UrbanMaterialPath = "Assets/Art/Environment/HybridZones/Materials/Urban/MercatoVecchio_UrbanTerrain.mat";
    const string RiverMaterialPath = "Assets/Art/Environment/WeatherSurfaces/Water/Water_River.mat";
    const string ApartmentPath = "Assets/Environment/MarketSquare/Buildings/Apartment1.glb";
    const string ApartmentNePath = "Assets/Environment/MarketSquare/Buildings/Apartment_NE.glb";
    const string TownhousePath = "Assets/Environment/MarketSquare/Buildings/TownhouseDouble.glb";

    static readonly HashSet<string> PreservedRootNames = new(StringComparer.Ordinal)
    {
        "Directional Light", "Global Volume", "Benidito", "CameraManager", "[CameraRig]",
        "[UICanvas]", "EventSystem", "[MenuManager]", "[CharacterSheet]", "ZoneVolume",
        "[MercatoBattleAuthoring]", "[LimboWorldAuthoring]",
    };

    [MenuItem("InfernosCurse/Mercato Vecchio/2. Rebuild Production Scene")]
    public static void Build()
    {
        MercatoVecchioProductionKitBuilder.Build();
        FlorentineInnSeamlessModuleBuilder.Build();

        Scene active = SceneManager.GetActiveScene();
        if (active.path == ScenePath && active.isDirty)
            throw new InvalidOperationException("Save or revert the currently dirty Mercato scene before a deterministic rebuild.");

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        RemoveLegacyEnvironment(scene);

        BuildGroundAndRiver();
        BuildArchitecture();
        BuildCommerce();
        BuildLandmarks();
        BuildInn();
        BuildWorldStateAnchors();
        BuildTravel();
        ConfigurePlayerAndCamera(scene);

        UrbanHybridTerrainSceneMigrator.ApplyMercatoSurface(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        FlorenceLimboWorldAuthoringBuilder.Rebuild();
        LimboCrierEncounterBuilder.Rebuild();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        MercatoVecchioProductionValidator.Validate();
        Debug.Log("[MercatoVecchioProductionBuilder] Rebuilt the reference-faithful production square with a seamless Florentine Inn.");
    }

    static void RemoveLegacyEnvironment(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (ShouldPreserve(root)) continue;
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    static bool ShouldPreserve(GameObject root)
    {
        if (PreservedRootNames.Contains(root.name)) return true;
        if (root.GetComponentInChildren<PlayerController>(true) != null) return true;
        if (root.GetComponentInChildren<Camera>(true) != null) return true;
        if (root.GetComponentInChildren<EventSystem>(true) != null) return true;
        return false;
    }

    static void BuildGroundAndRiver()
    {
        Material urban = Require<Material>(UrbanMaterialPath);
        Material water = Require<Material>(RiverMaterialPath);

        var marketSquare = new GameObject("[MarketSquare]").transform;
        Box(marketSquare, "Floor_Cobblestone", new Vector3(-5f, -0.12f, 0f), new Vector3(90f, 0.24f, 64f), urban);

        var environment = new GameObject("[MercatoEnvironment]").transform;
        GameObject river = Box(environment, "Arno_River", new Vector3(-5f, -0.42f, -37f), new Vector3(94f, 0.18f, 12f), water, false);
        river.AddComponent<ScrollingWater>();

        GameObject riverWall = Require<GameObject>(MercatoVecchioProductionKitBuilder.PrefabRoot + "/Mercato_RiverWall.prefab");
        for (int i = 0; i < 7; i++)
            Instance(riverWall, environment, $"RiverWall_{i + 1:00}", new Vector3(-41f + i * 12f, 0f, -29.2f), 0f);

        Material stone = Require<Material>("Assets/Environment/FlorentineInnFloor1/StructuralKit/Materials/Inn_PietraSerena.mat");
        Box(environment, "Riverfront_Walk", new Vector3(-5f, 0.02f, -26.7f), new Vector3(84f, 0.18f, 4.8f), stone);
        Box(environment, "PonteApproach_East", new Vector3(36.5f, 0.04f, -16f), new Vector3(7f, 0.2f, 20f), stone);
    }

    static void BuildArchitecture()
    {
        var architecture = new GameObject("[MercatoArchitecture]").transform;
        GameObject loggia = Require<GameObject>(MercatoVecchioProductionKitBuilder.PrefabRoot + "/Mercato_Loggia.prefab");
        Instance(loggia, architecture, "Landmark_LoggiaDelMercato", new Vector3(0f, 0f, 20.5f), 0f);

        GameObject apartment = Require<GameObject>(ApartmentPath);
        GameObject apartmentNe = Require<GameObject>(ApartmentNePath);
        GameObject townhouse = Require<GameObject>(TownhousePath);

        var north = new[]
        {
            new Vector3(-25f,0f,28f), new Vector3(-15f,0f,29f), new Vector3(15f,0f,29f), new Vector3(26f,0f,28f),
        };
        for (int i = 0; i < north.Length; i++)
            Instance(i % 2 == 0 ? apartment : apartmentNe, architecture, $"NorthBuilding_{i + 1:00}", north[i], 180f, 1.15f);

        var east = new[]
        {
            new Vector3(34f,0f,20f), new Vector3(35f,0f,10f), new Vector3(35f,0f,0f), new Vector3(35f,0f,-10f),
        };
        for (int i = 0; i < east.Length; i++)
            Instance(i % 2 == 0 ? townhouse : apartmentNe, architecture, $"EastShopBuilding_{i + 1:00}", east[i], -90f, 1.1f);

        Instance(apartment, architecture, "WestMerchant_North", new Vector3(-29f, 0f, 20f), 90f, 1.15f);
        Instance(townhouse, architecture, "WestMerchant_South", new Vector3(-30f, 0f, -20f), 90f, 1.1f);

        Material plaster = Require<Material>("Assets/Environment/FlorentineInnFloor1/StructuralKit/Materials/Inn_LimePlaster.mat");
        Box(architecture, "NorthBoundary_Backdrop", new Vector3(0f, 5f, 33f), new Vector3(76f, 10f, 0.6f), plaster);
        Box(architecture, "EastBoundary_Backdrop", new Vector3(40f, 5f, 7f), new Vector3(0.6f, 10f, 42f), plaster);
    }

    static void BuildCommerce()
    {
        var commerce = new GameObject("[MercatoCommerce]").transform;
        GameObject red = Require<GameObject>(MercatoVecchioProductionKitBuilder.PrefabRoot + "/Mercato_Stall_Red.prefab");
        GameObject ochre = Require<GameObject>(MercatoVecchioProductionKitBuilder.PrefabRoot + "/Mercato_Stall_Ochre.prefab");
        GameObject green = Require<GameObject>(MercatoVecchioProductionKitBuilder.PrefabRoot + "/Mercato_Stall_Green.prefab");
        GameObject[] variants = { red, ochre, green };

        int index = 0;
        foreach (float z in new[] { -12f, -6f, 8f, 14f })
        foreach (float x in new[] { -17f, -10f, 10f, 17f })
        {
            float rotation = z < 0f ? 0f : 180f;
            Instance(variants[index % variants.Length], commerce, $"MarketStall_{index + 1:00}", new Vector3(x, 0f, z), rotation);
            index++;
        }

        GameObject barrel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Environment/MarketSquare/Props/Barrel.glb");
        if (barrel != null)
        {
            for (int i = 0; i < 8; i++)
                Instance(barrel, commerce, $"MarketBarrel_{i + 1:00}", new Vector3(-23f + (i % 2) * 2f, 0f, -17f + (i / 2) * 7f), i * 27f, 0.85f);
        }
    }

    static void BuildLandmarks()
    {
        var landmarks = new GameObject("[MercatoLandmarks]").transform;
        GameObject fountain = Require<GameObject>(MercatoVecchioProductionKitBuilder.PrefabRoot + "/Mercato_FountainPlaza.prefab");
        Instance(fountain, landmarks, "Landmark_Fountain", new Vector3(0f, 0f, 1f), 0f);

        Material timber = Require<Material>("Assets/Environment/FlorentineInnFloor1/StructuralKit/Materials/Inn_StructuralTimber.mat");
        Sign(landmarks, "RouteSign_Signoria", new Vector3(-23f, 0f, 23f), new Vector3(-1f, 0f, 0.2f), timber);
        Sign(landmarks, "RouteSign_Ponte", new Vector3(29f, 0f, -10f), new Vector3(1f, 0f, -0.2f), timber);
        Sign(landmarks, "RouteSign_Arno", new Vector3(-5.5f, 0f, -23.5f), new Vector3(0f, 0f, -1f), timber);
    }

    static void BuildInn()
    {
        var interiors = new GameObject("[MercatoInteriors]").transform;
        GameObject modulePrefab = Require<GameObject>(ModulePath);
        GameObject moduleObject = Instance(modulePrefab, interiors, "AlbergoFiorentino_Interior", new Vector3(-39f, 0f, 0f), -90f);

        Material terracotta = Require<Material>("Assets/Environment/FlorentineInnFloor1/StructuralKit/Materials/Inn_ServiceTerracotta.mat");
        GameObject roofA = Box(moduleObject.transform, "InnRoof_NorthSlope", new Vector3(-5.4f, 4.7f, 0f), new Vector3(12f, 0.45f, 23f), terracotta, false, new Vector3(0f, 0f, -18f));
        GameObject roofB = Box(moduleObject.transform, "InnRoof_SouthSlope", new Vector3(5.4f, 4.7f, 0f), new Vector3(12f, 0.45f, 23f), terracotta, false, new Vector3(0f, 0f, 18f));

        var cameraZone = moduleObject.GetComponentInChildren<SeamlessInteriorCameraZone>(true);
        if (cameraZone != null)
        {
            Renderer[] occluders = moduleObject.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer.name.StartsWith("InnWall_") ||
                                   renderer.name.StartsWith("CourtyardLintel_") ||
                                   renderer.name.StartsWith("InnRoof_"))
                .ToArray();
            cameraZone.Configure(occluders, new[] { "InnWall_", "CourtyardLintel_", "InnRoof_" });
        }

        GameObject facade = Require<GameObject>(MercatoVecchioProductionKitBuilder.PrefabRoot + "/Mercato_InnFacade.prefab");
        Instance(facade, interiors, "AlbergoFiorentino_Facade", new Vector3(-27.65f, 0f, 0f), -90f);
    }

    static void BuildWorldStateAnchors()
    {
        var root = new GameObject("[MercatoWorldState]").transform;
        Anchor(root, "Anchor_Crier_Fountain", new Vector3(3.8f, 0.05f, 3f));
        Anchor(root, "Anchor_Crier_WestStalls", new Vector3(-19f, 0.05f, 7f));
        Anchor(root, "Anchor_Crier_SouthGate", new Vector3(7f, 0.05f, -23f));
        Anchor(root, "Anchor_Crier_HideAlley", new Vector3(-25f, 0.05f, 17f));
        Anchor(root, "Anchor_Npc_Baker", new Vector3(-12f, 0.05f, 14f));
        Anchor(root, "Anchor_Npc_RecordClerk", new Vector3(27f, 0.05f, 16f));
        Anchor(root, "Anchor_Npc_Stallholder", new Vector3(12f, 0.05f, -8f));
        Anchor(root, "Anchor_Npc_Innkeeper", new Vector3(-27f, 0.05f, 0f));
    }

    static void BuildTravel()
    {
        var root = new GameObject("[MercatoTravel]").transform;
        new GameObject("[ZoneEntryPlacer]").AddComponent<ZoneEntryPlacer>().transform.SetParent(root, false);
        Entry(root, "ENTRY_mercato_south", "mercato_south", "Mercato Vecchio", new Vector3(0f, 0.05f, -22f), Vector2.up, true);
        Entry(root, "ENTRY_from_signoria", "mercato_signoria", "Via della Signoria", new Vector3(-21f, 0.05f, 23f), Vector2.right, false);
        Entry(root, "ENTRY_from_ponte", "mercato_ponte", "Ponte Vecchio Approach", new Vector3(29f, 0.05f, -11f), Vector2.left, false);
        Entry(root, "ANCHOR_arno_future", "mercato_arno", "Arno Riverfront", new Vector3(0f, 0.05f, -25f), Vector2.up, false);

        Exit(root, "Exit_Signoria", new Vector3(-24.5f, 1.5f, 26f), new Vector3(7f, 3f, 2f), "PiazzaDellaSignoria", "signoria_south");
        Exit(root, "Exit_Ponte", new Vector3(38f, 1.5f, -14f), new Vector3(2f, 3f, 9f), "PonteVecchio", "ponte_west");
    }

    static void ConfigurePlayerAndCamera(Scene scene)
    {
        PlayerController player = SceneComponents<PlayerController>(scene).FirstOrDefault();
        if (player == null) throw new InvalidOperationException("Mercato has no PlayerController to preserve.");
        player.transform.position = new Vector3(0f, player.transform.position.y, -22f);

        DynamicZoom zoom = SceneComponents<DynamicZoom>(scene).FirstOrDefault();
        if (zoom != null)
        {
            zoom.useClearanceZoom = true;
            zoom.closeClearance = 3f;
            zoom.wideClearance = 12f;
            zoom.minArchitectureHeight = 2.5f;
            zoom.rayHeight = 1.2f;
        }
        CameraOcclusionFader fader = SceneComponents<CameraOcclusionFader>(scene).FirstOrDefault();
        if (fader != null)
        {
            fader.hideMode = CameraOcclusionFader.OccluderHideMode.ShadowsOnly;
            fader.probeRadius = 0.55f;
        }
    }

    static void Entry(Transform parent, string name, string id, string label, Vector3 position, Vector2 facing, bool fastTravel)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        var entry = go.AddComponent<ZoneEntryPoint>();
        entry.entryId = id;
        entry.displayName = label;
        entry.faceDirection = facing;
        entry.fastTravelDestination = fastTravel;
    }

    static void Exit(Transform parent, string name, Vector3 position, Vector3 size, string scene, string entryId)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.AddComponent<BoxCollider>().size = size;
        var exit = go.AddComponent<ZoneExit>();
        exit.mode = ZoneExit.ExitMode.ToScene;
        exit.targetScene = scene;
        exit.targetEntryId = entryId;
    }

    static void Sign(Transform parent, string name, Vector3 position, Vector3 direction, Material material)
    {
        Box(parent, name + "_Post", position + Vector3.up * 1.5f, new Vector3(0.18f, 3f, 0.18f), material);
        GameObject board = Box(parent, name, position + Vector3.up * 2.6f, new Vector3(2.4f, 0.7f, 0.18f), material, false);
        board.transform.forward = direction.normalized;
    }

    static Transform Anchor(Transform parent, string name, Vector3 position)
    {
        var anchor = new GameObject(name).transform;
        anchor.SetParent(parent, false);
        anchor.position = position;
        return anchor;
    }

    static GameObject Instance(GameObject prefab, Transform parent, string name, Vector3 position, float yaw, float scale = 1f)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = name;
        instance.transform.SetParent(parent, false);
        instance.transform.position = position;
        instance.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        instance.transform.localScale *= scale;
        return instance;
    }

    static GameObject Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material,
        bool collider = true, Vector3 rotation = default)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localEulerAngles = rotation;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        if (!collider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static T Require<T>(string path) where T : UnityEngine.Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) throw new InvalidOperationException("Required asset is missing: " + path);
        return asset;
    }

    static IEnumerable<T> SceneComponents<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (T component in root.GetComponentsInChildren<T>(true))
            yield return component;
    }
}

public static class MercatoVecchioProductionValidator
{
    [MenuItem("InfernosCurse/Validation/Validate Mercato Production Scene")]
    public static void Validate()
    {
        Scene scene = SceneManager.GetSceneByPath(MercatoVecchioProductionBuilder.ScenePath);
        bool opened = false;
        if (!scene.IsValid() || !scene.isLoaded)
        {
            scene = EditorSceneManager.OpenPreviewScene(MercatoVecchioProductionBuilder.ScenePath);
            opened = true;
        }
        var errors = new List<string>();
        try
        {
            var transforms = SceneComponents<Transform>(scene).ToArray();
            string[] required =
            {
                "[MarketSquare]", "[MercatoEnvironment]", "[MercatoArchitecture]", "[MercatoCommerce]",
                "[MercatoLandmarks]", "[MercatoInteriors]", "[MercatoWorldState]", "[MercatoTravel]",
                "Landmark_LoggiaDelMercato", "Landmark_Fountain", "AlbergoFiorentino_Interior",
                "Anchor_Crier_Fountain", "Anchor_Crier_WestStalls", "Anchor_Crier_SouthGate", "Anchor_Crier_HideAlley",
            };
            foreach (string name in required)
                if (!transforms.Any(transform => transform.name == name)) errors.Add("missing " + name);

            if (SceneComponents<PlayerController>(scene).Count() != 1) errors.Add("scene must contain exactly one PlayerController");
            if (SceneComponents<Camera>(scene).Count(camera => camera.CompareTag("MainCamera")) != 1) errors.Add("scene must contain exactly one Main Camera");
            if (SceneComponents<EventSystem>(scene).Count() != 1) errors.Add("scene must contain exactly one EventSystem");
            if (SceneComponents<SeamlessInteriorModule>(scene).Count() != 1) errors.Add("scene must contain exactly one seamless interior module");
            if (SceneComponents<ZoneExit>(scene).Count() != 2) errors.Add("scene must contain exactly two active cross-zone exits");
            if (SceneComponents<ZoneEntryPoint>(scene).Count() < 4) errors.Add("scene requires four stable entry/route anchors");
            if (SceneComponents<WorldAgentSite>(scene).Count() != 4) errors.Add("scene requires four Crier world-agent sites");

            SeamlessInteriorModule module = SceneComponents<SeamlessInteriorModule>(scene).FirstOrDefault();
            if (module != null && !module.TryValidateRuntime(out string moduleError)) errors.Add(moduleError);

            Transform floor = transforms.FirstOrDefault(transform => transform.name == "Floor_Cobblestone");
            MeshFilter filter = floor != null ? floor.GetComponent<MeshFilter>() : null;
            if (filter == null || filter.sharedMesh == null || filter.sharedMesh.colors.Length != filter.sharedMesh.vertexCount)
                errors.Add("production urban floor has no vertex-color mesh");

            foreach (string legacy in new[] { "SouthExit", "WestWall", "NorthWall", "[Props_Clutter]", "TopFade" })
                if (transforms.Any(transform => transform.name == legacy)) errors.Add("legacy test-zone object remains: " + legacy);
        }
        finally
        {
            if (opened) EditorSceneManager.ClosePreviewScene(scene);
        }

        foreach (string error in errors) Debug.LogError("[MercatoVecchioProductionValidator] " + error);
        if (errors.Count > 0) throw new InvalidOperationException($"Mercato production validation failed with {errors.Count} error(s).");
        Debug.Log("[MercatoVecchioProductionValidator] Validation passed: production landmarks, seamless inn, travel, world-state anchors, urban surface, and unique runtime authorities.");
    }

    static IEnumerable<T> SceneComponents<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (T component in root.GetComponentsInChildren<T>(true))
            yield return component;
    }
}
