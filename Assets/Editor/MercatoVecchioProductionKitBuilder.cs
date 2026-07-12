using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class MercatoVecchioProductionKitBuilder
{
    public const string Root = "Assets/Environment/MercatoVecchio/ProductionKit";
    public const string PrefabRoot = Root + "/Prefabs";
    public const string MaterialRoot = Root + "/Materials";

    const string StonePath = "Assets/Environment/FlorentineInnFloor1/StructuralKit/Materials/Inn_PietraSerena.mat";
    const string PlasterPath = "Assets/Environment/FlorentineInnFloor1/StructuralKit/Materials/Inn_LimePlaster.mat";
    const string TimberPath = "Assets/Environment/FlorentineInnFloor1/StructuralKit/Materials/Inn_StructuralTimber.mat";
    const string TerracottaPath = "Assets/Environment/FlorentineInnFloor1/StructuralKit/Materials/Inn_ServiceTerracotta.mat";
    const string FountainModelPath = "Assets/Environment/MarketSquare/Props/Fountain.glb";
    const string WaterPath = "Assets/Art/Environment/WeatherSurfaces/Water/Water_Fountain.mat";
    const float FountainOuterDiameter = 4.9f;
    const float FountainPlazaTop = 0.24f;
    const float FountainWaterDiameter = 3.55f;
    const float FountainWaterSurfaceY = 1.06f;

    [MenuItem("InfernosCurse/Mercato Vecchio/1. Rebuild Production Kit")]
    public static void Build()
    {
        EnsureFolder(PrefabRoot);
        EnsureFolder(MaterialRoot);

        Material stone = Require<Material>(StonePath);
        Material plaster = Require<Material>(PlasterPath);
        Material timber = Require<Material>(TimberPath);
        Material terracotta = Require<Material>(TerracottaPath);
        Material clothRed = MaterialAsset("Mercato_Cloth_Red", new Color(0.48f, 0.12f, 0.09f), 0.52f);
        Material clothOchre = MaterialAsset("Mercato_Cloth_Ochre", new Color(0.68f, 0.43f, 0.13f), 0.58f);
        Material clothGreen = MaterialAsset("Mercato_Cloth_Green", new Color(0.20f, 0.34f, 0.18f), 0.62f);
        Material darkIron = MaterialAsset("Mercato_DarkIron", new Color(0.11f, 0.10f, 0.09f), 0.28f, 0.38f);

        Save("Mercato_Loggia", BuildLoggia(stone, plaster, timber, terracotta));
        Save("Mercato_Stall_Red", BuildStall("Mercato_Stall_Red", timber, clothRed, stone));
        Save("Mercato_Stall_Ochre", BuildStall("Mercato_Stall_Ochre", timber, clothOchre, stone));
        Save("Mercato_Stall_Green", BuildStall("Mercato_Stall_Green", timber, clothGreen, stone));
        Save("Mercato_InnFacade", BuildInnFacade(stone, plaster, timber, terracotta, darkIron));
        Save("Mercato_RiverWall", BuildRiverWall(stone));
        Save("Mercato_FountainPlaza", BuildFountain(stone, darkIron));
        MercatoCommercePolishBuilder.Build();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Validate();
        Debug.Log("[MercatoProductionKit] Rebuilt the structural kit plus five authored commerce prefabs.");
    }

    static GameObject BuildLoggia(Material stone, Material plaster, Material timber, Material terracotta)
    {
        var root = new GameObject("Mercato_Loggia");
        Box(root.transform, "Loggia_Steps", new Vector3(0f, 0.15f, 0f), new Vector3(27f, 0.3f, 9f), stone);
        Box(root.transform, "Loggia_Floor", new Vector3(0f, 0.42f, 0.5f), new Vector3(25.5f, 0.25f, 7.5f), stone);
        Box(root.transform, "Loggia_BackWall", new Vector3(0f, 3.1f, 3.6f), new Vector3(25.5f, 5.6f, 0.45f), plaster);
        for (int i = 0; i < 7; i++)
        {
            float x = -12f + i * 4f;
            Cylinder(root.transform, $"Loggia_Column_{i + 1:00}", new Vector3(x, 2.55f, -3f), 0.46f, 4.7f, stone, 12);
            Box(root.transform, $"Loggia_Capital_{i + 1:00}", new Vector3(x, 4.75f, -3f), new Vector3(1.1f, 0.32f, 1.1f), stone);
        }
        Box(root.transform, "Loggia_FrontLintel", new Vector3(0f, 5.15f, -3f), new Vector3(25.5f, 0.55f, 0.65f), timber);
        Box(root.transform, "Loggia_Roof_North", new Vector3(0f, 6.25f, 1f), new Vector3(27f, 0.5f, 5.6f), terracotta, new Vector3(16f, 0f, 0f));
        Box(root.transform, "Loggia_Roof_South", new Vector3(0f, 6.25f, -1.8f), new Vector3(27f, 0.5f, 5.6f), terracotta, new Vector3(-16f, 0f, 0f));
        return root;
    }

    static GameObject BuildStall(string name, Material timber, Material cloth, Material stone)
    {
        var root = new GameObject(name);
        Box(root.transform, "Stall_Counter", new Vector3(0f, 0.9f, 0f), new Vector3(3.6f, 0.28f, 1.45f), timber);
        Box(root.transform, "Stall_Base", new Vector3(0f, 0.25f, 0.25f), new Vector3(3.5f, 0.5f, 1.25f), timber);
        foreach (float x in new[] { -1.65f, 1.65f })
        foreach (float z in new[] { -0.55f, 0.55f })
            Box(root.transform, $"Stall_Post_{x}_{z}", new Vector3(x, 1.8f, z), new Vector3(0.16f, 3.5f, 0.16f), timber);
        Box(root.transform, "Stall_Awning", new Vector3(0f, 3.25f, 0f), new Vector3(4.25f, 0.16f, 2.65f), cloth, new Vector3(4f, 0f, 0f), false);
        for (int i = 0; i < 5; i++)
            Box(root.transform, $"Stall_Goods_{i + 1:00}", new Vector3(-1.25f + i * 0.62f, 1.12f, 0f), new Vector3(0.45f, 0.3f + (i % 2) * 0.1f, 0.55f), stone, Vector3.zero, false);
        return root;
    }

    static GameObject BuildInnFacade(Material stone, Material plaster, Material timber, Material terracotta, Material iron)
    {
        var root = new GameObject("Mercato_InnFacade");
        Box(root.transform, "InnFacade_Left", new Vector3(-4.7f, 3.4f, 0f), new Vector3(6.6f, 6.8f, 0.55f), plaster);
        Box(root.transform, "InnFacade_Right", new Vector3(4.7f, 3.4f, 0f), new Vector3(6.6f, 6.8f, 0.55f), plaster);
        Box(root.transform, "InnFacade_DoorLintel", new Vector3(0f, 5.6f, 0f), new Vector3(3f, 2.4f, 0.55f), plaster);
        Box(root.transform, "InnFacade_StoneBase", new Vector3(0f, 0.45f, -0.05f), new Vector3(16f, 0.9f, 0.75f), stone);
        Box(root.transform, "InnFacade_TimberBand", new Vector3(0f, 4.3f, -0.36f), new Vector3(16f, 0.25f, 0.22f), timber, Vector3.zero, false);
        for (int i = 0; i < 4; i++)
            Box(root.transform, $"InnFacade_Shutter_{i + 1:00}", new Vector3(-5.8f + i * 3.9f, 4.9f, -0.42f), new Vector3(1.6f, 1.9f, 0.16f), timber, Vector3.zero, false);
        Box(root.transform, "InnFacade_Roof", new Vector3(0f, 7.3f, 0.7f), new Vector3(17f, 0.5f, 5f), terracotta, new Vector3(12f, 0f, 0f));
        Box(root.transform, "InnFacade_SignBracket", new Vector3(2.1f, 5.0f, -0.85f), new Vector3(0.16f, 0.16f, 1.4f), iron, Vector3.zero, false);
        Box(root.transform, "InnFacade_Sign", new Vector3(2.1f, 4.45f, -1.5f), new Vector3(1.8f, 1.2f, 0.16f), timber, Vector3.zero, false);
        return root;
    }

    static GameObject BuildRiverWall(Material stone)
    {
        var root = new GameObject("Mercato_RiverWall");
        Box(root.transform, "RiverWall_Body", new Vector3(0f, 1.05f, 0f), new Vector3(12f, 2.1f, 1.2f), stone);
        Box(root.transform, "RiverWall_Cap", new Vector3(0f, 2.2f, 0f), new Vector3(12.3f, 0.28f, 1.5f), stone);
        for (int i = 0; i < 4; i++)
            Box(root.transform, $"RiverWall_Pier_{i + 1:00}", new Vector3(-4.5f + i * 3f, 2.55f, 0f), new Vector3(0.5f, 0.8f, 1.6f), stone);
        return root;
    }

    static GameObject BuildFountain(Material stone, Material iron)
    {
        var root = new GameObject("Mercato_FountainPlaza");
        Cylinder(root.transform, "Fountain_PlazaBase", new Vector3(0f, 0.12f, 0f), 4.8f, 0.24f, stone, 16);
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(FountainModelPath);
        if (source != null)
        {
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(source);
            model.name = "Fountain_AuthoredModel";
            model.transform.SetParent(root.transform, false);
            NormalizeModelToHorizontalDiameter(model, FountainOuterDiameter, FountainPlazaTop);
        }
        else
        {
            Cylinder(root.transform, "Fountain_BasinFallback", new Vector3(0f, 0.65f, 0f), 2.4f, 0.5f, stone, 16);
            Cylinder(root.transform, "Fountain_ColumnFallback", new Vector3(0f, 1.8f, 0f), 0.45f, 2.8f, iron, 12);
        }

        Material water = AssetDatabase.LoadAssetAtPath<Material>(WaterPath);
        if (water != null)
        {
            GameObject surface = Cylinder(root.transform, "Fountain_WaterSurface", new Vector3(0f, FountainWaterSurfaceY, 0f), FountainWaterDiameter * 0.5f, 0.04f, water, 32, false);
            WeatherSurfaceStandardBuilder.ConfigureWater(surface, StandardWaterProfile.Fountain, WeatherSurfaceExposure.Outdoor);
        }
        return root;
    }

    static void NormalizeModelToHorizontalDiameter(GameObject model, float targetDiameter, float groundY)
    {
        model.transform.localPosition = Vector3.zero;
        model.transform.localScale = Vector3.one;

        Bounds bounds = RendererBounds(model);
        float diameter = Mathf.Max(bounds.size.x, bounds.size.z);
        if (diameter <= 0.001f)
            throw new InvalidOperationException("Authored fountain has no measurable horizontal renderer bounds.");

        model.transform.localScale = Vector3.one * (targetDiameter / diameter);
        bounds = RendererBounds(model);
        model.transform.position += new Vector3(-bounds.center.x, groundY - bounds.min.y, -bounds.center.z);
    }

    static Bounds RendererBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            throw new InvalidOperationException(root.name + " has no renderers.");
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    static void Save(string name, GameObject root)
    {
        try { PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabRoot}/{name}.prefab"); }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    static GameObject Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material,
        Vector3 rotation = default, bool collider = true)
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

    static GameObject Cylinder(Transform parent, string name, Vector3 position, float radius, float height,
        Material material, int sides, bool collider = true)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
        go.GetComponent<Renderer>().sharedMaterial = material;
        if (!collider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static Material MaterialAsset(string name, Color color, float smoothness, float metallic = 0f)
    {
        string path = $"{MaterialRoot}/{name}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP/Lit shader is unavailable.");
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        material.color = color;
        material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_Metallic", metallic);
        EditorUtility.SetDirty(material);
        return material;
    }

    static T Require<T>(string path) where T : UnityEngine.Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) throw new InvalidOperationException("Required asset is missing: " + path);
        return asset;
    }

    static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    [MenuItem("InfernosCurse/Validation/Validate Mercato Production Kit")]
    public static void Validate()
    {
        var errors = new List<string>();
        string[] required =
        {
            "Mercato_Loggia", "Mercato_Stall_Red", "Mercato_Stall_Ochre", "Mercato_Stall_Green",
            "Mercato_InnFacade", "Mercato_RiverWall", "Mercato_FountainPlaza",
        };
        foreach (string name in required)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/{name}.prefab");
            if (prefab == null) { errors.Add(name + " is missing"); continue; }
            if (prefab.GetComponentsInChildren<Renderer>(true).Length == 0) errors.Add(name + " has no renderers");
            if (name != "Mercato_FountainPlaza" && prefab.GetComponentsInChildren<Collider>(true).Length == 0)
                errors.Add(name + " has no colliders");
        }
        ValidateFountain(errors);
        foreach (string error in errors) Debug.LogError("[MercatoProductionKitValidator] " + error);
        if (errors.Count > 0) throw new InvalidOperationException($"Mercato production kit validation failed with {errors.Count} error(s).");
        MercatoCommercePolishBuilder.Validate();
        Debug.Log("[MercatoProductionKitValidator] Validation passed for 7 reusable production prefabs.");
    }

    static void ValidateFountain(List<string> errors)
    {
        string path = $"{PrefabRoot}/Mercato_FountainPlaza.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Transform model = root.transform.Find("Fountain_AuthoredModel");
            Transform water = root.transform.Find("Fountain_WaterSurface");
            if (model == null)
            {
                errors.Add("Mercato fountain is missing its authored model");
                return;
            }
            if (water == null)
            {
                errors.Add("Mercato fountain is missing its water surface");
                return;
            }

            Bounds modelBounds = RendererBounds(model.gameObject);
            Renderer waterRenderer = water.GetComponent<Renderer>();
            if (waterRenderer == null)
            {
                errors.Add("Mercato fountain water surface has no renderer");
                return;
            }
            Bounds waterBounds = waterRenderer.bounds;
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(FountainModelPath);
            if (source == null || Quaternion.Angle(model.localRotation, source.transform.localRotation) > 0.1f)
                errors.Add("Mercato fountain does not preserve the authored model orientation");
            float modelDiameter = Mathf.Max(modelBounds.size.x, modelBounds.size.z);
            float waterDiameter = Mathf.Max(waterBounds.size.x, waterBounds.size.z);
            if (Mathf.Abs(modelDiameter - FountainOuterDiameter) > 0.05f)
                errors.Add($"Mercato fountain authored diameter is {modelDiameter:0.00}m; expected {FountainOuterDiameter:0.00}m");
            if (Mathf.Abs(waterDiameter - FountainWaterDiameter) > 0.05f)
                errors.Add($"Mercato fountain water diameter is {waterDiameter:0.00}m; expected {FountainWaterDiameter:0.00}m");
            if (modelDiameter - waterDiameter < 1.2f)
                errors.Add("Mercato fountain water must remain contained inside the lower stone basin");
            if (waterBounds.min.y < FountainWaterSurfaceY - 0.03f)
                errors.Add($"Mercato fountain water is sitting too low at {waterBounds.min.y:0.00}m");
            if (Mathf.Abs(modelBounds.min.y - FountainPlazaTop) > 0.03f)
                errors.Add($"Mercato fountain is not grounded on the plaza ({modelBounds.min.y:0.00}m)");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
