using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Produces the reusable thirteenth-century prop language used by the Florentine inn.
/// The prefabs deliberately use ordinary Unity meshes and URP materials so the
/// production scene has no authoring-tool runtime dependency.
/// </summary>
public static class FlorentineInnPropKitBuilder
{
    public const string Root = "Assets/Environment/FlorentineInnFloor1/PropKit";
    public const string PrefabDir = Root + "/Prefabs";
    public const string MaterialDir = Root + "/Materials";
    public const string ModelDir = Root + "/Models";
    public const string FountainModelPath = ModelDir + "/FlorentineInnOctagonalLionFountain.glb";

    static Material _chestnut;
    static Material _darkWood;
    static Material _iron;
    static Material _brass;
    static Material _terracotta;
    static Material _ceramic;
    static Material _linen;
    static Material _parchment;
    static Material _wax;
    static Material _flame;
    static Material _leather;
    static Material _green;
    static Material _red;
    static Material _stone;
    static Material _waterFx;

    public static readonly string[] RequiredPrefabs =
    {
        "ReceptionCounter", "DiningTable", "TeaTable", "PeriodChair", "LobbyBench",
        "Bookcase", "Sideboard", "KitchenIsland", "WorkTable", "PantryShelves",
        "Hearth", "StorageChest", "Barrel", "CrateStack", "SalonRug", "PottedPlant",
        "KeyCubbies", "Candle", "LedgerQuill", "PlaceSetting", "Pitcher", "BreadBoard",
        "Cookware", "PantryGoods", "Luggage", "DocumentBundle", "CourtyardBench",
        "Bucket", "CourtyardFountain"
    };

    [MenuItem("InfernosCurse/Florentine Inn/2. Rebuild Prop Kit")]
    public static void RebuildPropKit()
    {
        EnsureFolders();
        EnsureMaterials();
        foreach (string name in RequiredPrefabs)
        {
            string path = PrefabPath(name);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
                AssetDatabase.DeleteAsset(path);
        }
        BuildMissingPrefabs();
        AssetDatabase.SaveAssets();
        Debug.Log($"[FlorentineInnPropKit] Rebuilt {RequiredPrefabs.Length} reusable prefabs.");
    }

    public static void EnsurePropKit()
    {
        EnsureFolders();
        EnsureMaterials();
        BuildMissingPrefabs();
    }

    public static string PrefabPath(string name) => $"{PrefabDir}/{name}.prefab";

    static void BuildMissingPrefabs()
    {
        Create("ReceptionCounter", BuildReceptionCounter);
        Create("DiningTable", root => BuildTable(root, 1.8f, 1.2f, 0.78f));
        Create("TeaTable", root => BuildTable(root, 1.4f, 1.1f, 0.58f));
        Create("PeriodChair", BuildChair);
        Create("LobbyBench", BuildBench);
        Create("Bookcase", BuildBookcase);
        Create("Sideboard", BuildSideboard);
        Create("KitchenIsland", root => BuildTable(root, 3.6f, 1.5f, 0.88f));
        Create("WorkTable", root => BuildTable(root, 2.2f, 1.1f, 0.82f));
        Create("PantryShelves", BuildPantryShelves);
        Create("Hearth", BuildHearth);
        Create("StorageChest", BuildChest);
        Create("Barrel", BuildBarrel);
        Create("CrateStack", BuildCrateStack);
        Create("SalonRug", BuildRug);
        Create("PottedPlant", BuildPlant);
        Create("KeyCubbies", BuildKeyCubbies);
        Create("Candle", BuildCandle);
        Create("LedgerQuill", BuildLedgerQuill);
        Create("PlaceSetting", BuildPlaceSetting);
        Create("Pitcher", BuildPitcher);
        Create("BreadBoard", BuildBreadBoard);
        Create("Cookware", BuildCookware);
        Create("PantryGoods", BuildPantryGoods);
        Create("Luggage", BuildLuggage);
        Create("DocumentBundle", BuildDocumentBundle);
        Create("CourtyardBench", BuildCourtyardBench);
        Create("Bucket", BuildBucket);
        Create("CourtyardFountain", BuildFountain);
    }

    static void Create(string name, Action<Transform> build)
    {
        string path = PrefabPath(name);
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

        var root = new GameObject(name);
        build(root.transform);
        PrefabUtility.SaveAsPrefabAsset(root, path);
        UnityEngine.Object.DestroyImmediate(root);
    }

    static void BuildReceptionCounter(Transform root)
    {
        Cube(root, "MainCabinet", new Vector3(0f, 0.52f, 0f), new Vector3(5.8f, 1.04f, 0.76f), _darkWood);
        Cube(root, "WorkTop", new Vector3(0f, 1.08f, 0f), new Vector3(6f, 0.12f, 0.94f), _chestnut);
        Cube(root, "WestReturn", new Vector3(-2.55f, 0.52f, 0.64f), new Vector3(0.7f, 1.04f, 1.55f), _darkWood);
        Cube(root, "EastReturn", new Vector3(2.55f, 0.52f, 0.64f), new Vector3(0.7f, 1.04f, 1.55f), _darkWood);
        for (int i = -2; i <= 2; i++)
            Cube(root, $"FrontPanel_{i + 3}", new Vector3(i * 1.05f, 0.55f, -0.405f), new Vector3(0.84f, 0.7f, 0.05f), _chestnut, false);
        Cube(root, "FootRail", new Vector3(0f, 0.18f, -0.58f), new Vector3(5.2f, 0.08f, 0.08f), _brass, false);
    }

    static void BuildTable(Transform root, float width, float depth, float height)
    {
        Cube(root, "Top", new Vector3(0f, height, 0f), new Vector3(width, 0.16f, depth), _chestnut);
        foreach (float x in new[] { -width * 0.38f, width * 0.38f })
            foreach (float z in new[] { -depth * 0.34f, depth * 0.34f })
                Cube(root, "Leg", new Vector3(x, height * 0.5f, z), new Vector3(0.13f, height, 0.13f), _darkWood);
        Cube(root, "ApronFront", new Vector3(0f, height - 0.15f, -depth * 0.43f), new Vector3(width * 0.86f, 0.22f, 0.08f), _darkWood, false);
        Cube(root, "ApronBack", new Vector3(0f, height - 0.15f, depth * 0.43f), new Vector3(width * 0.86f, 0.22f, 0.08f), _darkWood, false);
    }

    static void BuildChair(Transform root)
    {
        Cube(root, "Seat", new Vector3(0f, 0.49f, 0f), new Vector3(0.62f, 0.12f, 0.58f), _chestnut);
        foreach (float x in new[] { -0.24f, 0.24f })
            foreach (float z in new[] { -0.21f, 0.21f })
                Cube(root, "Leg", new Vector3(x, 0.24f, z), new Vector3(0.09f, 0.48f, 0.09f), _darkWood);
        Cube(root, "BackPostL", new Vector3(-0.25f, 0.96f, 0.24f), new Vector3(0.09f, 1.02f, 0.09f), _darkWood);
        Cube(root, "BackPostR", new Vector3(0.25f, 0.96f, 0.24f), new Vector3(0.09f, 1.02f, 0.09f), _darkWood);
        Cube(root, "BackRailLow", new Vector3(0f, 0.82f, 0.24f), new Vector3(0.55f, 0.1f, 0.08f), _chestnut, false);
        Cube(root, "BackRailHigh", new Vector3(0f, 1.32f, 0.24f), new Vector3(0.62f, 0.16f, 0.1f), _chestnut, false);
    }

    static void BuildBench(Transform root)
    {
        Cube(root, "Seat", new Vector3(0f, 0.48f, 0f), new Vector3(2.2f, 0.16f, 0.65f), _chestnut);
        Cube(root, "Back", new Vector3(0f, 0.98f, 0.28f), new Vector3(2.2f, 0.72f, 0.12f), _darkWood);
        foreach (float x in new[] { -0.86f, 0.86f })
            Cube(root, "Leg", new Vector3(x, 0.24f, 0f), new Vector3(0.16f, 0.48f, 0.48f), _darkWood);
    }

    static void BuildBookcase(Transform root)
    {
        Cube(root, "Back", new Vector3(0f, 1.15f, 0.16f), new Vector3(1.5f, 2.3f, 0.1f), _darkWood);
        Cube(root, "SideL", new Vector3(-0.72f, 1.15f, 0f), new Vector3(0.12f, 2.3f, 0.42f), _chestnut);
        Cube(root, "SideR", new Vector3(0.72f, 1.15f, 0f), new Vector3(0.12f, 2.3f, 0.42f), _chestnut);
        for (int i = 0; i < 4; i++)
        {
            float y = 0.12f + i * 0.7f;
            Cube(root, $"Shelf_{i}", new Vector3(0f, y, 0f), new Vector3(1.5f, 0.1f, 0.48f), _chestnut);
            if (i < 3)
                for (int b = 0; b < 6; b++)
                    Cube(root, "Book", new Vector3(-0.55f + b * 0.2f, y + 0.25f, -0.1f),
                        new Vector3(0.12f, 0.38f + (b % 2) * 0.08f, 0.22f), b % 3 == 0 ? _red : _leather, false);
        }
    }

    static void BuildSideboard(Transform root)
    {
        Cube(root, "Cabinet", new Vector3(0f, 0.58f, 0f), new Vector3(1.85f, 1.05f, 0.55f), _darkWood);
        Cube(root, "Top", new Vector3(0f, 1.14f, 0f), new Vector3(2f, 0.12f, 0.68f), _chestnut);
        Cube(root, "DoorL", new Vector3(-0.47f, 0.62f, -0.3f), new Vector3(0.82f, 0.76f, 0.05f), _chestnut, false);
        Cube(root, "DoorR", new Vector3(0.47f, 0.62f, -0.3f), new Vector3(0.82f, 0.76f, 0.05f), _chestnut, false);
        Sphere(root, "PullL", new Vector3(-0.12f, 0.62f, -0.36f), Vector3.one * 0.06f, _brass, false);
        Sphere(root, "PullR", new Vector3(0.12f, 0.62f, -0.36f), Vector3.one * 0.06f, _brass, false);
    }

    static void BuildPantryShelves(Transform root)
    {
        Cube(root, "Back", new Vector3(0f, 1.25f, 0.15f), new Vector3(2.5f, 2.5f, 0.1f), _darkWood);
        Cube(root, "SideL", new Vector3(-1.2f, 1.25f, 0f), new Vector3(0.12f, 2.5f, 0.5f), _chestnut);
        Cube(root, "SideR", new Vector3(1.2f, 1.25f, 0f), new Vector3(0.12f, 2.5f, 0.5f), _chestnut);
        for (int i = 0; i < 4; i++)
            Cube(root, $"Shelf_{i}", new Vector3(0f, 0.1f + i * 0.78f, 0f), new Vector3(2.5f, 0.1f, 0.55f), _chestnut);
    }

    static void BuildHearth(Transform root)
    {
        Cube(root, "Back", new Vector3(0f, 1.05f, 0.2f), new Vector3(1.9f, 2.1f, 0.45f), _stone);
        Cube(root, "Opening", new Vector3(0f, 0.72f, -0.26f), new Vector3(1.15f, 1.15f, 0.08f), _iron, false);
        Cube(root, "Lintel", new Vector3(0f, 1.42f, -0.3f), new Vector3(1.5f, 0.22f, 0.25f), _stone);
        Cube(root, "HearthStone", new Vector3(0f, 0.12f, -0.38f), new Vector3(1.7f, 0.24f, 0.85f), _stone);
        for (int i = -1; i <= 1; i++)
            Cylinder(root, "Log", new Vector3(i * 0.28f, 0.33f, -0.48f), new Vector3(0.1f, 0.35f, 0.1f), _darkWood, false, new Vector3(90f, 0f, 0f));
        Sphere(root, "FireGlow", new Vector3(0f, 0.55f, -0.46f), new Vector3(0.65f, 0.72f, 0.22f), _flame, false);
    }

    static void BuildChest(Transform root)
    {
        Cube(root, "Body", new Vector3(0f, 0.38f, 0f), new Vector3(1.5f, 0.76f, 0.75f), _chestnut);
        Cylinder(root, "Lid", new Vector3(0f, 0.78f, 0f), new Vector3(0.75f, 0.38f, 0.38f), _darkWood, true, new Vector3(0f, 0f, 90f));
        Cube(root, "BandL", new Vector3(-0.5f, 0.42f, -0.39f), new Vector3(0.08f, 0.78f, 0.05f), _iron, false);
        Cube(root, "BandR", new Vector3(0.5f, 0.42f, -0.39f), new Vector3(0.08f, 0.78f, 0.05f), _iron, false);
        Cube(root, "Lock", new Vector3(0f, 0.48f, -0.43f), new Vector3(0.22f, 0.28f, 0.08f), _brass, false);
    }

    static void BuildBarrel(Transform root)
    {
        Cylinder(root, "Staves", new Vector3(0f, 0.55f, 0f), new Vector3(0.38f, 0.55f, 0.38f), _chestnut);
        foreach (float y in new[] { 0.16f, 0.55f, 0.94f })
            Cylinder(root, "IronHoop", new Vector3(0f, y, 0f), new Vector3(0.405f, 0.035f, 0.405f), _iron, false);
    }

    static void BuildCrateStack(Transform root)
    {
        Crate(root, new Vector3(-0.38f, 0.35f, 0f), 0f);
        Crate(root, new Vector3(0.4f, 0.3f, 0.1f), 8f);
        Crate(root, new Vector3(0f, 0.92f, 0.02f), -6f);
    }

    static void Crate(Transform root, Vector3 position, float yaw)
    {
        var holder = new GameObject("Crate").transform;
        holder.SetParent(root, false);
        holder.localPosition = position;
        holder.localRotation = Quaternion.Euler(0f, yaw, 0f);
        Cube(holder, "Body", Vector3.zero, new Vector3(0.72f, 0.62f, 0.72f), _chestnut);
        foreach (float y in new[] { -0.24f, 0.24f })
            Cube(holder, "Brace", new Vector3(0f, y, -0.38f), new Vector3(0.72f, 0.08f, 0.06f), _darkWood, false);
    }

    static void BuildRug(Transform root)
    {
        Cube(root, "WovenRug", new Vector3(0f, 0.025f, 0f), new Vector3(4.8f, 0.05f, 3.8f), _red, false);
        Cube(root, "InnerField", new Vector3(0f, 0.055f, 0f), new Vector3(4.15f, 0.025f, 3.15f), _linen, false);
        for (int i = -4; i <= 4; i++)
            Cube(root, "Fringe", new Vector3(i * 0.48f, 0.03f, -2f), new Vector3(0.025f, 0.025f, 0.32f), _linen, false);
    }

    static void BuildPlant(Transform root)
    {
        Cylinder(root, "Pot", new Vector3(0f, 0.3f, 0f), new Vector3(0.32f, 0.3f, 0.32f), _terracotta);
        Cylinder(root, "Stem", new Vector3(0f, 0.82f, 0f), new Vector3(0.035f, 0.4f, 0.035f), _darkWood, false);
        foreach (Vector3 p in new[] { new Vector3(-0.18f, 1.05f, 0f), new Vector3(0.18f, 1.15f, 0.05f), new Vector3(0f, 1.35f, -0.08f) })
            Sphere(root, "Leaves", p, new Vector3(0.48f, 0.36f, 0.42f), _green, false);
    }

    static void BuildKeyCubbies(Transform root)
    {
        Cube(root, "Back", new Vector3(0f, 1.05f, 0.08f), new Vector3(3.8f, 2.1f, 0.12f), _darkWood);
        for (int x = -3; x <= 3; x++)
            Cube(root, "Divider", new Vector3(x * 0.52f, 1.05f, -0.08f), new Vector3(0.06f, 2f, 0.34f), _chestnut, false);
        for (int y = 0; y < 4; y++)
            Cube(root, "Shelf", new Vector3(0f, 0.28f + y * 0.52f, -0.08f), new Vector3(3.75f, 0.06f, 0.34f), _chestnut, false);
        for (int i = 0; i < 9; i++)
        {
            float x = -1.5f + (i % 6) * 0.6f;
            float y = 0.42f + (i / 6) * 0.55f;
            Sphere(root, "KeyRing", new Vector3(x, y, -0.3f), Vector3.one * 0.055f, _brass, false);
            Cube(root, "KeyStem", new Vector3(x, y - 0.09f, -0.3f), new Vector3(0.035f, 0.16f, 0.035f), _brass, false);
        }
    }

    static void BuildCandle(Transform root)
    {
        Cylinder(root, "Wax", new Vector3(0f, 0.14f, 0f), new Vector3(0.065f, 0.14f, 0.065f), _wax, false);
        Cylinder(root, "Holder", new Vector3(0f, 0.025f, 0f), new Vector3(0.14f, 0.025f, 0.14f), _brass, false);
        Sphere(root, "Flame", new Vector3(0f, 0.34f, 0f), new Vector3(0.05f, 0.11f, 0.05f), _flame, false);
    }

    static void BuildLedgerQuill(Transform root)
    {
        Cube(root, "Ledger", new Vector3(0f, 0.035f, 0f), new Vector3(0.5f, 0.07f, 0.36f), _leather, false);
        Cube(root, "Pages", new Vector3(0f, 0.075f, -0.02f), new Vector3(0.44f, 0.035f, 0.3f), _parchment, false);
        Cylinder(root, "Quill", new Vector3(0.28f, 0.11f, 0f), new Vector3(0.018f, 0.32f, 0.018f), _wax, false, new Vector3(0f, 0f, -58f));
    }

    static void BuildPlaceSetting(Transform root)
    {
        Cylinder(root, "Plate", new Vector3(0f, 0.025f, 0f), new Vector3(0.18f, 0.025f, 0.18f), _ceramic, false);
        Cylinder(root, "Cup", new Vector3(0.25f, 0.09f, 0.02f), new Vector3(0.075f, 0.09f, 0.075f), _terracotta, false);
        Cube(root, "Linen", new Vector3(-0.24f, 0.025f, 0f), new Vector3(0.18f, 0.035f, 0.3f), _linen, false);
    }

    static void BuildPitcher(Transform root)
    {
        Cylinder(root, "Body", new Vector3(0f, 0.18f, 0f), new Vector3(0.13f, 0.18f, 0.13f), _terracotta, false);
        Cylinder(root, "Neck", new Vector3(0f, 0.38f, 0f), new Vector3(0.07f, 0.09f, 0.07f), _terracotta, false);
        Cube(root, "Handle", new Vector3(0.15f, 0.25f, 0f), new Vector3(0.04f, 0.24f, 0.04f), _terracotta, false);
    }

    static void BuildBreadBoard(Transform root)
    {
        Cube(root, "Board", new Vector3(0f, 0.025f, 0f), new Vector3(0.55f, 0.05f, 0.32f), _chestnut, false);
        Sphere(root, "Loaf", new Vector3(0f, 0.12f, 0f), new Vector3(0.38f, 0.14f, 0.2f), _parchment, false);
    }

    static void BuildCookware(Transform root)
    {
        Cylinder(root, "Pot", new Vector3(0f, 0.16f, 0f), new Vector3(0.23f, 0.16f, 0.23f), _iron, false);
        Cube(root, "HandleL", new Vector3(-0.3f, 0.22f, 0f), new Vector3(0.18f, 0.05f, 0.06f), _iron, false);
        Cube(root, "HandleR", new Vector3(0.3f, 0.22f, 0f), new Vector3(0.18f, 0.05f, 0.06f), _iron, false);
        Cylinder(root, "Spoon", new Vector3(0.38f, 0.06f, 0.12f), new Vector3(0.018f, 0.25f, 0.018f), _chestnut, false, new Vector3(72f, 0f, 0f));
    }

    static void BuildPantryGoods(Transform root)
    {
        for (int i = 0; i < 3; i++)
        {
            float x = (i - 1) * 0.28f;
            Cylinder(root, "Jar", new Vector3(x, 0.16f, 0f), new Vector3(0.12f, 0.16f + i * 0.025f, 0.12f), i == 1 ? _ceramic : _terracotta, false);
            Cylinder(root, "Lid", new Vector3(x, 0.34f + i * 0.05f, 0f), new Vector3(0.09f, 0.025f, 0.09f), _darkWood, false);
        }
        Sphere(root, "Sack", new Vector3(0.52f, 0.2f, 0f), new Vector3(0.24f, 0.4f, 0.22f), _linen, false);
    }

    static void BuildLuggage(Transform root)
    {
        Cube(root, "Case", new Vector3(0f, 0.28f, 0f), new Vector3(0.78f, 0.55f, 0.42f), _leather);
        Cube(root, "StrapL", new Vector3(-0.22f, 0.28f, -0.23f), new Vector3(0.07f, 0.55f, 0.04f), _brass, false);
        Cube(root, "StrapR", new Vector3(0.22f, 0.28f, -0.23f), new Vector3(0.07f, 0.55f, 0.04f), _brass, false);
        Cube(root, "Handle", new Vector3(0f, 0.64f, 0f), new Vector3(0.28f, 0.07f, 0.07f), _leather, false);
    }

    static void BuildDocumentBundle(Transform root)
    {
        for (int i = 0; i < 4; i++)
            Cube(root, "Parchment", new Vector3((i % 2) * 0.04f, 0.018f + i * 0.018f, (i % 3) * 0.025f), new Vector3(0.42f, 0.018f, 0.3f), _parchment, false);
        Cube(root, "Ribbon", new Vector3(0f, 0.09f, 0f), new Vector3(0.06f, 0.025f, 0.34f), _red, false);
    }

    static void BuildCourtyardBench(Transform root)
    {
        Cube(root, "Seat", new Vector3(0f, 0.48f, 0f), new Vector3(1.8f, 0.16f, 0.55f), _stone);
        Cube(root, "SupportL", new Vector3(-0.65f, 0.24f, 0f), new Vector3(0.22f, 0.48f, 0.48f), _stone);
        Cube(root, "SupportR", new Vector3(0.65f, 0.24f, 0f), new Vector3(0.22f, 0.48f, 0.48f), _stone);
    }

    static void BuildBucket(Transform root)
    {
        Cylinder(root, "WoodenBucket", new Vector3(0f, 0.24f, 0f), new Vector3(0.22f, 0.24f, 0.22f), _chestnut);
        Cylinder(root, "IronBand", new Vector3(0f, 0.12f, 0f), new Vector3(0.235f, 0.025f, 0.235f), _iron, false);
        Cube(root, "Handle", new Vector3(0f, 0.54f, 0f), new Vector3(0.48f, 0.04f, 0.04f), _iron, false);
    }

    static void BuildFountain(Transform root)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(FountainModelPath);
        if (model == null)
            throw new InvalidOperationException($"The authored fountain model is missing at {FountainModelPath}.");

        var structure = (GameObject)PrefabUtility.InstantiatePrefab(model);
        structure.name = "AuthoredFountainStructure";
        structure.transform.SetParent(root, false);
        structure.transform.localPosition = Vector3.zero;
        structure.transform.localRotation = Quaternion.identity;
        structure.transform.localScale = Vector3.one;

        Bounds rawBounds = RendererBounds(structure);
        structure.transform.localScale = new Vector3(
            3.2f / rawBounds.size.x,
            2.1f / rawBounds.size.y,
            3.2f / rawBounds.size.z);

        Bounds scaledBounds = RendererBounds(structure);
        structure.transform.position -= new Vector3(
            scaledBounds.center.x - root.position.x,
            scaledBounds.min.y - root.position.y,
            scaledBounds.center.z - root.position.z);

        var basinCollider = root.gameObject.AddComponent<BoxCollider>();
        basinCollider.center = new Vector3(0f, 0.36f, 0f);
        basinCollider.size = new Vector3(3.02f, 0.72f, 3.02f);

        Cylinder(root, "WaterSurface", new Vector3(0f, 0.51f, 0f), new Vector3(2.7f, 0.035f, 2.7f), _ceramic, false);

        foreach ((string name, Vector3 source, Vector3 impact) in new[]
        {
            ("East", new Vector3(0.39f, 1.55f, 0f), new Vector3(0.92f, 0.54f, 0f)),
            ("West", new Vector3(-0.39f, 1.55f, 0f), new Vector3(-0.92f, 0.54f, 0f)),
            ("North", new Vector3(0f, 1.55f, 0.39f), new Vector3(0f, 0.54f, 0.92f)),
            ("South", new Vector3(0f, 1.55f, -0.39f), new Vector3(0f, 0.54f, -0.92f))
        })
            WaterStream(root, $"WaterStream_{name}", source, impact);

        Cylinder(root, "Ripple_Inner", new Vector3(0f, 0.505f, 0f), new Vector3(1.04f, 0.012f, 1.04f), _waterFx, false);
        Cylinder(root, "Ripple_Outer", new Vector3(0f, 0.507f, 0f), new Vector3(1.84f, 0.009f, 1.84f), _waterFx, false);
        root.gameObject.AddComponent<InnFountainAnimator>();
    }

    static void WaterStream(Transform root, string name, Vector3 source, Vector3 impact)
    {
        Vector3 delta = impact - source;
        var stream = Cylinder(root, name, (source + impact) * 0.5f,
            new Vector3(0.065f, delta.magnitude * 0.5f, 0.065f), _waterFx, false);
        stream.transform.localRotation = Quaternion.FromToRotation(Vector3.up, delta.normalized);
    }

    static Bounds RendererBounds(GameObject target)
    {
        var renderers = target.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            throw new InvalidOperationException($"{target.name} contains no renderers.");

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    static GameObject Cube(Transform parent, string name, Vector3 position, Vector3 scale, Material material, bool collider = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Finish(go, parent, name, position, scale, material, collider);
        return go;
    }

    static GameObject Cylinder(Transform parent, string name, Vector3 position, Vector3 scale, Material material,
        bool collider = true, Vector3 rotation = default)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Finish(go, parent, name, position, scale, material, collider);
        go.transform.localRotation = Quaternion.Euler(rotation);
        return go;
    }

    static GameObject Sphere(Transform parent, string name, Vector3 position, Vector3 scale, Material material, bool collider = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Finish(go, parent, name, position, scale, material, collider);
        return go;
    }

    static void Finish(GameObject go, Transform parent, string name, Vector3 position, Vector3 scale, Material material, bool collider)
    {
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        if (!collider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
    }

    static void EnsureMaterials()
    {
        _chestnut = MaterialAsset("Prop_DarkChestnut", new Color(0.27f, 0.105f, 0.035f), 0.18f);
        _darkWood = MaterialAsset("Prop_AgedDarkWood", new Color(0.12f, 0.038f, 0.015f), 0.14f);
        _iron = MaterialAsset("Prop_HandForgedIron", new Color(0.10f, 0.095f, 0.085f), 0.28f, 0.72f);
        _brass = MaterialAsset("Prop_WornBrass", new Color(0.43f, 0.28f, 0.08f), 0.42f, 0.62f);
        _terracotta = MaterialAsset("Prop_Terracotta", new Color(0.54f, 0.22f, 0.10f), 0.12f);
        _ceramic = MaterialAsset("Prop_CreamCeramic", new Color(0.72f, 0.67f, 0.51f), 0.32f);
        _linen = MaterialAsset("Prop_NaturalLinen", new Color(0.67f, 0.59f, 0.43f), 0.05f);
        _parchment = MaterialAsset("Prop_Parchment", new Color(0.76f, 0.65f, 0.43f), 0.06f);
        _wax = MaterialAsset("Prop_Beeswax", new Color(0.84f, 0.67f, 0.27f), 0.22f);
        _flame = EmissiveMaterial("Prop_WarmFlame", new Color(1f, 0.28f, 0.035f), 2.5f);
        _leather = MaterialAsset("Prop_OxbloodLeather", new Color(0.25f, 0.045f, 0.025f), 0.22f);
        _green = MaterialAsset("Prop_OliveLeaves", new Color(0.12f, 0.28f, 0.08f), 0.08f);
        _red = MaterialAsset("Prop_MadderRedTextile", new Color(0.38f, 0.055f, 0.035f), 0.06f);
        _stone = MaterialAsset("Prop_PietraSerena", new Color(0.35f, 0.36f, 0.34f), 0.16f);
        _waterFx = TransparentMaterialAsset("Prop_FountainWaterFX", new Color(0.42f, 0.72f, 0.86f, 0.58f));
    }

    static Material MaterialAsset(string name, Color color, float smoothness, float metallic = 0f)
    {
        string path = $"{MaterialDir}/{name}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        material.color = color;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
        EditorUtility.SetDirty(material);
        return material;
    }

    static Material EmissiveMaterial(string name, Color color, float intensity)
    {
        var material = MaterialAsset(name, color * 0.3f, 0.2f);
        material.EnableKeyword("_EMISSION");
        if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", color * intensity);
        EditorUtility.SetDirty(material);
        return material;
    }

    static Material TransparentMaterialAsset(string name, Color color)
    {
        string path = $"{MaterialDir}/{name}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Universal Render Pipeline/Unlit");
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        material.color = color;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.renderQueue = 3000;
        EditorUtility.SetDirty(material);
        return material;
    }

    static void EnsureFolders()
    {
        EnsureFolder("Assets/Environment");
        EnsureFolder("Assets/Environment/FlorentineInnFloor1");
        EnsureFolder(Root);
        EnsureFolder(PrefabDir);
        EnsureFolder(MaterialDir);
        EnsureFolder(ModelDir);
        EnsureFolder(Root + "/Meshes");
        EnsureFolder(Root + "/Textures");
        EnsureFolder(Root + "/Audio");
        EnsureFolder(Root + "/VFX");
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        string parent = path.Substring(0, slash);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
    }
}

public static class FlorentineInnPropValidator
{
    const string ScenePath = "Assets/Scenes/FlorentineInnFloor1.unity";

    static readonly string[] RequiredSceneObjects =
    {
        "ReceptionCounter", "ReceptionLedger", "ReceptionKeys", "LobbyBench", "LobbyLuggage",
        "SalonRug", "SalonTeaTable", "SalonBookcase", "DiningSideboard", "KitchenIsland",
        "KitchenHearth", "StaffWorkTable", "PantryShelves_E", "StorageBarrel_05",
        "OfficeDesk", "OfficeBookcase_E", "OfficeChest", "CourtyardFountain", "CourtyardBench_W"
    };

    [MenuItem("InfernosCurse/Florentine Inn/Validate First-Floor Props")]
    public static void Validate()
    {
        var errors = new List<string>();
        foreach (string name in FlorentineInnPropKitBuilder.RequiredPrefabs)
            if (AssetDatabase.LoadAssetAtPath<GameObject>(FlorentineInnPropKitBuilder.PrefabPath(name)) == null)
                errors.Add($"missing prop prefab {name}");

        var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var transforms = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        foreach (string required in RequiredSceneObjects)
            if (!transforms.Any(t => t.name == required)) errors.Add($"scene is missing {required}");

        string[] forbidden = { "Television", "TV", "Electric", "Plastic", "Microwave", "Refrigerator" };
        foreach (Transform t in transforms)
            if (forbidden.Any(word => t.name.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0))
                errors.Add($"anachronistic object name: {t.name}");

        var counter = transforms.FirstOrDefault(t => t.name == "ReceptionCounter");
        if (counter == null || counter.GetComponent<InnCounterInteraction>() == null)
            errors.Add("reception counter interaction is missing");

        var player = transforms.FirstOrDefault(t => t.CompareTag("Player"));
        if (player == null || player.GetComponent<PlayerWorldInteractor>() == null)
            errors.Add("player world interaction is missing");

        ValidateFountain(transforms, errors);
        ValidateLighting(transforms, errors);

        if (!string.IsNullOrEmpty(previous) && previous != ScenePath)
            EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);

        if (errors.Count > 0)
        {
            foreach (string error in errors) Debug.LogError("[FlorentineInnProps] " + error);
            Debug.LogError($"[FlorentineInnProps] Validation failed with {errors.Count} error(s).");
            return;
        }

        Debug.Log($"[FlorentineInnProps] Validation passed: {FlorentineInnPropKitBuilder.RequiredPrefabs.Length} prefabs and {RequiredSceneObjects.Length} production anchors present.");
    }

    static void ValidateFountain(Transform[] transforms, List<string> errors)
    {
        var fountain = transforms.FirstOrDefault(t => t.name == "CourtyardFountain");
        if (fountain == null) return;

        var fountainTransforms = fountain.GetComponentsInChildren<Transform>(true);
        var authored = fountainTransforms.FirstOrDefault(t => t.name == "AuthoredFountainStructure");
        if (authored == null)
        {
            errors.Add("courtyard fountain is missing the authored 3D AI structure");
            return;
        }

        string[] primitiveParts = { "Basin", "Pedestal", "UpperBowl", "Finial" };
        if (fountainTransforms.Any(t => primitiveParts.Contains(t.name)))
            errors.Add("courtyard fountain restored a retired primitive structure part");

        var renderers = authored.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            errors.Add("authored fountain structure contains no renderers");
        }
        else
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            float footprint = Mathf.Max(bounds.size.x, bounds.size.z);
            if (footprint < 3f || footprint > 3.35f)
                errors.Add($"authored fountain footprint is {footprint:0.00}m, expected about 3.2m");
            if (bounds.size.y < 2f || bounds.size.y > 2.2f)
                errors.Add($"authored fountain height is {bounds.size.y:0.00}m, expected 2.0-2.2m");
        }

        long triangles = authored.GetComponentsInChildren<MeshFilter>(true)
            .Where(filter => filter.sharedMesh != null)
            .Sum(filter => Enumerable.Range(0, filter.sharedMesh.subMeshCount)
                .Sum(subMesh => (long)filter.sharedMesh.GetIndexCount(subMesh) / 3L));
        if (triangles <= 0 || triangles > 120000)
            errors.Add($"authored fountain triangle count is {triangles:N0}, expected 1-120,000");

        int streams = fountainTransforms.Count(t => t.name.StartsWith("WaterStream_"));
        int ripples = fountainTransforms.Count(t => t.name.StartsWith("Ripple_"));
        if (streams != 4) errors.Add($"courtyard fountain has {streams} water streams, expected 4");
        if (ripples != 2) errors.Add($"courtyard fountain has {ripples} ripple layers, expected 2");
        if (fountain.GetComponent<InnFountainAnimator>() == null)
            errors.Add("courtyard fountain animation component is missing");
        if (fountain.GetComponent<BoxCollider>() == null)
            errors.Add("courtyard fountain simple basin collider is missing");
    }

    static void ValidateLighting(Transform[] transforms, List<string> errors)
    {
        var lightingRoot = transforms.FirstOrDefault(t => t.name == "[Lighting]");
        if (lightingRoot == null)
        {
            errors.Add("inn lighting root is missing");
            return;
        }

        var guard = lightingRoot.GetComponent<IndoorWeatherLightingGuard>();
        if (guard == null)
            errors.Add("inn lighting is missing its COZY ambient guard");

        var environment = lightingRoot.GetComponent<WorldWindowEnvironment>();
        if (environment == null)
        {
            errors.Add("inn lighting is missing its window environment adapter");
            return;
        }

        if (environment.LocalLights.Length != 5)
            errors.Add($"inn has {environment.LocalLights.Length} driven room lights, expected 5");

        foreach (var driven in environment.LocalLights)
        {
            if (driven.light == null)
            {
                errors.Add("inn has a missing driven room light reference");
                continue;
            }

            if (driven.light.shadows == LightShadows.None)
                errors.Add($"room light '{driven.light.name}' ignores wall shadows");
            if (driven.light.range > 5f)
                errors.Add($"room light '{driven.light.name}' range {driven.light.range:0.00}m exceeds its room boundary");
            if (driven.nightIntensity > 3.5f)
                errors.Add($"room light '{driven.light.name}' night intensity {driven.nightIntensity:0.00} is too high for the inn");
        }

        if (RenderSettings.ambientMode != UnityEngine.Rendering.AmbientMode.Flat)
            errors.Add("inn ambient lighting is not isolated from global COZY sky ambient");
        if (RenderSettings.ambientLight.maxColorComponent > 0.22f)
            errors.Add($"inn ambient color is too bright ({RenderSettings.ambientLight.maxColorComponent:0.00})");
        if (RenderSettings.reflectionIntensity > 0.5f)
            errors.Add($"inn reflection intensity is too bright ({RenderSettings.reflectionIntensity:0.00})");
    }
}
