using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class UrbanHybridTerrainSceneMigrator
{
    const string AssetRoot = "Assets/Art/Environment/HybridZones";
    const string ProductionMeshRoot = AssetRoot + "/Meshes/Urban/Production";

    readonly struct SurfaceTarget
    {
        public readonly string scenePath;
        public readonly string rootName;
        public readonly string objectName;
        public readonly string zoneKey;
        public readonly float repairDensity;
        public readonly float grimeStrength;

        public SurfaceTarget(string scenePath, string rootName, string objectName, string zoneKey,
            float repairDensity, float grimeStrength)
        {
            this.scenePath = scenePath;
            this.rootName = rootName;
            this.objectName = objectName;
            this.zoneKey = zoneKey;
            this.repairDensity = repairDensity;
            this.grimeStrength = grimeStrength;
        }
    }

    static readonly SurfaceTarget Mercato = new(
        "Assets/Scenes/MercatoVecchio.unity", "[MarketSquare]", "Floor_Cobblestone",
        "MercatoVecchio", 0.95f, 0.90f);

    static readonly SurfaceTarget Piazza = new(
        "Assets/Scenes/PiazzaDellaSignoria.unity", "[Ground]", "Floor_Piazza",
        "PiazzaDellaSignoria", 0.52f, 0.58f);

    static readonly SurfaceTarget Ponte = new(
        "Assets/Scenes/PonteVecchio.unity", "[BridgeDeck]", "Deck",
        "PonteVecchio", 0.48f, 0.82f);

    static readonly SurfaceTarget Duomo = new(
        "Assets/Scenes/Duomo.unity", "[Floors]", "Floor_Octagon",
        "Duomo", 0.26f, 0.35f);

    static readonly SurfaceTarget ViaCalimala = new(
        "Assets/Scenes/ViaCalimala.unity", "Street_EW_Template", "Street_Paving",
        "ViaCalimala", 0.70f, 0.96f);

    [MenuItem("InfernosCurse/Hybrid Zones/Migrate All Urban Production Surfaces")]
    public static void MigrateApprovedScenes()
    {
        EnsureFolder(ProductionMeshRoot);
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            ApplyToSceneAsset(Mercato);
            ApplyToSceneAsset(Piazza);
            ApplyToSceneAsset(Ponte);
            ApplyToSceneAsset(Duomo);
            ApplyToSceneAsset(ViaCalimala);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[UrbanTerrainMigrator] Migrated 5 urban production surfaces; colliders, overlays, and hybrid-zone authoring were preserved.");
        }
        finally
        {
            EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
    }

    public static void ApplyPiazzaSurface(Scene scene)
    {
        EnsureFolder(ProductionMeshRoot);
        ApplySurface(scene, Piazza);
        AssetDatabase.SaveAssets();
    }

    public static void ApplyDuomoSurface(Scene scene)
    {
        EnsureFolder(ProductionMeshRoot);
        ApplySurface(scene, Duomo);
        AssetDatabase.SaveAssets();
    }

    static void ApplyToSceneAsset(SurfaceTarget target)
    {
        Scene scene = SceneManager.GetSceneByPath(target.scenePath);
        bool alreadyLoaded = scene.IsValid() && scene.isLoaded;
        if (!alreadyLoaded)
            scene = EditorSceneManager.OpenScene(target.scenePath, OpenSceneMode.Additive);

        ApplySurface(scene, target);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        if (!alreadyLoaded)
            EditorSceneManager.CloseScene(scene, true);
    }

    static void ApplySurface(Scene scene, SurfaceTarget target)
    {
        Transform root = scene.GetRootGameObjects()
            .FirstOrDefault(candidate => candidate.name == target.rootName)?.transform;
        Transform surface = root == null
            ? null
            : root.GetComponentsInChildren<Transform>(true).FirstOrDefault(candidate => candidate.name == target.objectName);
        if (surface == null)
            throw new InvalidOperationException($"{scene.name}: missing {target.rootName}/{target.objectName}.");

        MeshFilter filter = surface.GetComponent<MeshFilter>();
        MeshRenderer renderer = surface.GetComponent<MeshRenderer>();
        Collider collider = surface.GetComponent<Collider>();
        if (filter == null || filter.sharedMesh == null || renderer == null || collider == null)
            throw new InvalidOperationException($"{scene.name}/{target.objectName}: MeshFilter, MeshRenderer, and existing Collider are required.");

        string materialPath = $"{AssetRoot}/Materials/Urban/{target.zoneKey}_UrbanTerrain.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null || material.shader == null || material.shader.name != "InfernosCurse/HybridZoneTerrain")
            throw new InvalidOperationException($"Missing production urban material: {materialPath}");

        string meshPath = $"{ProductionMeshRoot}/{target.zoneKey}_{target.objectName}.asset";
        Mesh generated = BuildTopSurfaceMesh(filter.sharedMesh.bounds, renderer.bounds.size, surface,
            $"{target.zoneKey}_{target.objectName}", target.repairDensity, target.grimeStrength);
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (mesh == null)
        {
            AssetDatabase.CreateAsset(generated, meshPath);
            mesh = generated;
        }
        else
        {
            EditorUtility.CopySerialized(generated, mesh);
            UnityEngine.Object.DestroyImmediate(generated);
            EditorUtility.SetDirty(mesh);
        }

        filter.sharedMesh = mesh;
        renderer.sharedMaterial = material;
        EditorUtility.SetDirty(filter);
        EditorUtility.SetDirty(renderer);
    }

    static Mesh BuildTopSurfaceMesh(Bounds sourceBounds, Vector3 worldSize, Transform surface,
        string meshName, float repairDensity, float grimeStrength)
    {
        int columns = Mathf.Max(2, Mathf.CeilToInt(worldSize.x) + 1);
        int rows = Mathf.Max(2, Mathf.CeilToInt(worldSize.z) + 1);
        var mesh = new Mesh
        {
            name = meshName,
            indexFormat = columns * rows > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16,
        };

        var vertices = new Vector3[columns * rows];
        var normals = new Vector3[vertices.Length];
        var tangents = new Vector4[vertices.Length];
        var uvs = new Vector2[vertices.Length];
        var colors = new Color[vertices.Length];
        var triangles = new int[(columns - 1) * (rows - 1) * 6];

        int vertex = 0;
        for (int z = 0; z < rows; z++)
        {
            float z01 = z / (float)(rows - 1);
            float localZ = Mathf.Lerp(sourceBounds.min.z, sourceBounds.max.z, z01);
            float centeredZ = (z01 - 0.5f) * worldSize.z;
            for (int x = 0; x < columns; x++, vertex++)
            {
                float x01 = x / (float)(columns - 1);
                float localX = Mathf.Lerp(sourceBounds.min.x, sourceBounds.max.x, x01);
                float centeredX = (x01 - 0.5f) * worldSize.x;
                var localPosition = new Vector3(localX, sourceBounds.max.y, localZ);
                Vector3 worldPosition = surface.TransformPoint(localPosition);

                vertices[vertex] = localPosition;
                normals[vertex] = Vector3.up;
                tangents[vertex] = new Vector4(1f, 0f, 0f, 1f);
                uvs[vertex] = new Vector2(worldPosition.x / 4f, worldPosition.z / 4f);

                float band = 1f - Mathf.Clamp01(Mathf.Abs(Mathf.Sin(centeredX * 0.19f + centeredZ * 0.07f)) / 0.23f);
                float repair = Mathf.Clamp01(band * repairDensity * 0.45f);
                float edgeDistance = Mathf.Min(worldSize.x * 0.5f - Mathf.Abs(centeredX), worldSize.z * 0.5f - Mathf.Abs(centeredZ));
                float edge = 1f - Mathf.Clamp01(edgeDistance / 4f);
                float noise = Mathf.PerlinNoise(centeredX * 0.075f + 29f, centeredZ * 0.075f + 11f);
                float grime = Mathf.Clamp01((edge * 0.72f + Mathf.Max(0f, noise - 0.7f) * 0.5f) * grimeStrength);
                repair *= 1f - grime * 0.65f;
                float baseStone = Mathf.Clamp01(1f - Mathf.Max(repair, grime));
                colors[vertex] = new Color(repair, grime, baseStone, grime);
            }
        }

        int triangle = 0;
        for (int z = 0; z < rows - 1; z++)
        for (int x = 0; x < columns - 1; x++)
        {
            int a = z * columns + x;
            int b = a + 1;
            int c = a + columns;
            int d = c + 1;
            triangles[triangle++] = a;
            triangles[triangle++] = c;
            triangles[triangle++] = b;
            triangles[triangle++] = b;
            triangles[triangle++] = c;
            triangles[triangle++] = d;
        }

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.tangents = tangents;
        mesh.uv = uvs;
        mesh.colors = colors;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        return mesh;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string name = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
