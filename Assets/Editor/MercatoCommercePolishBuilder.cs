using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class MercatoCommercePolishBuilder
{
    public const string PrefabRoot = "Assets/Environment/MercatoVecchio/ProductionKit/Prefabs";
    const string ModelRoot = "Assets/Environment/MercatoVecchio/ProductionKit/Models/Commerce";
    const string FlorentineStallPath = "Assets/Environment/MarketSquare/Props/Florentine_Stall.glb";
    const string VendorRoot = "Assets/Low-Poly Medieval Market/Prefabs";

    public static readonly string[] ProductionPrefabs =
    {
        "Mercato_Stall_Bakery",
        "Mercato_Stall_Produce",
        "Mercato_Stall_DryGoods",
        "Mercato_Stall_General",
        "Mercato_MerchantHandcart",
    };

    [MenuItem("InfernosCurse/Mercato Vecchio/Rebuild Production Commerce")]
    public static void Build()
    {
        EnsureFolder(PrefabRoot);
        SaveStall("Mercato_Stall_Bakery", ModelRoot + "/mercato-stall-bakery.glb",
            new Vector3(5f, 3.5f, 3.2f), new Vector3(3.45f, 1.6f, 2.35f));
        SaveStall("Mercato_Stall_Produce", ModelRoot + "/mercato-stall-produce.glb",
            new Vector3(4.8f, 3.5f, 3.5f), new Vector3(3.55f, 1.6f, 2.3f));
        SaveStall("Mercato_Stall_DryGoods", ModelRoot + "/mercato-stall-cloth-dry-goods.glb",
            new Vector3(4.4f, 3.5f, 3.3f), new Vector3(3.55f, 1.6f, 2.35f));
        SaveGeneralStall();
        SaveHandcart();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Validate();
        Debug.Log("[MercatoCommercePolishBuilder] Rebuilt four production trade stalls and one merchant handcart from optimized authored sources.");
    }

    static void SaveStall(string name, string modelPath, Vector3 modelScale, Vector3 colliderSize)
    {
        GameObject root = new(name);
        try
        {
            GameObject model = InstantiateSource(modelPath, root.transform, "AuthoredModel");
            model.transform.localScale = modelScale;
            PositionOnGround(model, 0f);
            ConfigureRenderers(root);
            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, colliderSize.y * 0.5f, 0.12f);
            collider.size = colliderSize;
            AddCullGroup(root, 0.025f);
            PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabRoot}/{name}.prefab");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    static void SaveGeneralStall()
    {
        GameObject root = new("Mercato_Stall_General");
        try
        {
            GameObject model = InstantiateSource(FlorentineStallPath, root.transform, "AuthoredModel");
            model.transform.localScale = Vector3.one * 4.75f;
            PositionOnGround(model, 0f);

            Dress(root.transform, VendorRoot + "/basket_01.prefab", "Basket_Left", new Vector3(-1.25f, 0f, -0.9f), 0.55f, -12f);
            Dress(root.transform, VendorRoot + "/bag.prefab", "Sack_Right", new Vector3(1.25f, 0f, -0.75f), 0.65f, 18f);
            Dress(root.transform, VendorRoot + "/wooden_box.prefab", "Crate_Display", new Vector3(0.95f, 0f, 0.3f), 0.5f, -8f);
            Dress(root.transform, VendorRoot + "/loaf_basket.prefab", "Bread_Display", new Vector3(-0.55f, 0f, 0.38f), 0.55f, 7f);

            ConfigureRenderers(root);
            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.8f, -0.1f);
            collider.size = new Vector3(4.2f, 1.6f, 2.45f);
            AddCullGroup(root, 0.025f);
            PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabRoot}/Mercato_Stall_General.prefab");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    static void SaveHandcart()
    {
        GameObject root = new("Mercato_MerchantHandcart");
        try
        {
            GameObject model = InstantiateSource(ModelRoot + "/mercato-handcart.glb", root.transform, "AuthoredModel");
            model.transform.localScale = Vector3.one * 3f;
            PositionOnGround(model, 0f);
            ConfigureRenderers(root);
            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.5f, 0f);
            collider.size = new Vector3(2.85f, 1f, 1.15f);
            AddCullGroup(root, 0.018f);
            PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabRoot}/Mercato_MerchantHandcart.prefab");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    static GameObject InstantiateSource(string path, Transform parent, string name)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (source == null) throw new InvalidOperationException("Required commerce source is missing: " + path);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
        instance.name = name;
        instance.transform.SetParent(parent, false);
        foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
            UnityEngine.Object.DestroyImmediate(collider);
        return instance;
    }

    static void Dress(Transform parent, string path, string name, Vector3 position, float targetHeight, float yaw)
    {
        GameObject prop = InstantiateSource(path, parent, name);
        Bounds bounds = RendererBounds(prop);
        if (bounds.size.y > 0.001f)
            prop.transform.localScale *= targetHeight / bounds.size.y;
        prop.transform.localEulerAngles = new Vector3(0f, yaw, 0f);
        prop.transform.localPosition = position;
        PositionOnGround(prop, 0f);
    }

    static void PositionOnGround(GameObject target, float groundY)
    {
        Bounds bounds = RendererBounds(target);
        target.transform.localPosition += Vector3.up * (groundY - bounds.min.y);
    }

    static Bounds RendererBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) throw new InvalidOperationException(root.name + " has no renderers.");
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    static void ConfigureRenderers(GameObject root)
    {
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            renderer.allowOcclusionWhenDynamic = true;
        }
    }

    static void AddCullGroup(GameObject root, float cullHeight)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        var lod = root.AddComponent<LODGroup>();
        lod.fadeMode = LODFadeMode.CrossFade;
        lod.animateCrossFading = true;
        lod.SetLODs(new[] { new LOD(cullHeight, renderers) });
        lod.RecalculateBounds();
    }

    [MenuItem("InfernosCurse/Validation/Validate Mercato Production Commerce")]
    public static void Validate()
    {
        var errors = new List<string>();
        foreach (string name in ProductionPrefabs)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/{name}.prefab");
            if (prefab == null) { errors.Add(name + " is missing"); continue; }
            if (prefab.GetComponentsInChildren<Renderer>(true).Length == 0) errors.Add(name + " has no renderers");
            if (prefab.GetComponentsInChildren<Collider>(true).Length != 1) errors.Add(name + " must have exactly one gameplay collider");
            if (prefab.GetComponentsInChildren<Transform>(true).Any(t => t.name.StartsWith("Stall_Goods_", StringComparison.Ordinal)))
                errors.Add(name + " contains legacy primitive goods");
            long triangles = prefab.GetComponentsInChildren<MeshFilter>(true)
                .Where(filter => filter.sharedMesh != null)
                .Sum(filter => Enumerable.Range(0, filter.sharedMesh.subMeshCount)
                    .Sum(subMesh => (long)filter.sharedMesh.GetIndexCount(subMesh) / 3L));
            if (triangles > 100000L) errors.Add($"{name} is too dense for repeated placement ({triangles} triangles)");
        }

        foreach (string error in errors) Debug.LogError("[MercatoCommerceValidator] " + error);
        if (errors.Count > 0) throw new InvalidOperationException($"Mercato commerce validation failed with {errors.Count} error(s).");
        Debug.Log("[MercatoCommerceValidator] Validation passed for four production stalls and one handcart; all are optimized, collider-safe, and free of primitive goods.");
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
}
