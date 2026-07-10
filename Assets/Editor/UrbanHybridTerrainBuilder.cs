using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class UrbanHybridTerrainBuilder
{
    const string Root = "Assets/Art/Environment/HybridZones";
    const string ProfileRoot = Root + "/Profiles/Urban";
    const string MaterialRoot = Root + "/Materials/Urban";
    const string MeshRoot = Root + "/Meshes/Urban";
    const string ShaderName = "InfernosCurse/HybridZoneTerrain";
    const string StoneTexture = "Assets/Environment/FlorentineInnFloor1/StructuralKit/Textures/Inn_PietraSerena_Albedo.png";
    const string TerracottaTexture = "Assets/Environment/FlorentineInnFloor1/StructuralKit/Textures/Inn_ServiceTerracotta_Albedo.png";
    const string PaverTexture = "Assets/Environment/FlorentineInnFloor1/StructuralKit/Textures/Inn_CourtyardPavers_Albedo.png";

    readonly struct ZoneDefinition
    {
        public readonly string key;
        public readonly Vector2 size;
        public readonly Color baseTint;
        public readonly Color repairTint;
        public readonly Color grimeTint;
        public readonly float repairDensity;
        public readonly float grimeStrength;

        public ZoneDefinition(string key, Vector2 size, Color baseTint, Color repairTint, Color grimeTint,
            float repairDensity, float grimeStrength)
        {
            this.key = key;
            this.size = size;
            this.baseTint = baseTint;
            this.repairTint = repairTint;
            this.grimeTint = grimeTint;
            this.repairDensity = repairDensity;
            this.grimeStrength = grimeStrength;
        }
    }

    static readonly ZoneDefinition[] Zones =
    {
        new("MercatoVecchio", new Vector2(67.5f, 41.25f), new Color(0.79f, 0.72f, 0.62f, 1f), new Color(0.78f, 0.44f, 0.29f, 1f), new Color(0.42f, 0.35f, 0.27f, 1f), 0.95f, 0.90f),
        new("PonteVecchio", new Vector2(58f, 16f), new Color(0.67f, 0.68f, 0.65f, 1f), new Color(0.66f, 0.43f, 0.31f, 1f), new Color(0.34f, 0.37f, 0.35f, 1f), 0.48f, 0.82f),
        new("Duomo", new Vector2(27f, 27f), new Color(0.88f, 0.84f, 0.75f, 1f), new Color(0.70f, 0.52f, 0.39f, 1f), new Color(0.48f, 0.44f, 0.37f, 1f), 0.26f, 0.35f),
        new("PiazzaDellaSignoria", new Vector2(60f, 56f), new Color(0.77f, 0.75f, 0.68f, 1f), new Color(0.72f, 0.45f, 0.32f, 1f), new Color(0.43f, 0.39f, 0.31f, 1f), 0.52f, 0.58f),
        new("ViaCalimala", new Vector2(64f, 22.4f), new Color(0.62f, 0.59f, 0.53f, 1f), new Color(0.69f, 0.40f, 0.28f, 1f), new Color(0.34f, 0.30f, 0.25f, 1f), 0.70f, 0.96f),
    };

    [MenuItem("InfernosCurse/Hybrid Zones/Build Urban Terrain Assets")]
    public static void BuildAssets()
    {
        EnsureFolder(Root);
        EnsureFolder(ProfileRoot);
        EnsureFolder(MaterialRoot);
        EnsureFolder(MeshRoot);

        Shader shader = Shader.Find(ShaderName);
        if (shader == null || !shader.isSupported)
            throw new InvalidOperationException($"{ShaderName} is missing or unsupported.");

        Texture2D stone = RequireTexture(StoneTexture);
        Texture2D terracotta = RequireTexture(TerracottaTexture);
        Texture2D pavers = RequireTexture(PaverTexture);

        foreach (ZoneDefinition zone in Zones)
        {
            HybridZoneTerrainProfile profile = LoadOrCreateProfile(zone);
            Material material = LoadOrCreateMaterial(zone, shader, profile, stone, terracotta, pavers);
            Mesh mesh = LoadOrCreateMesh(zone);
            if (mesh.colors.Length != mesh.vertexCount)
                throw new InvalidOperationException($"{zone.key} mesh did not retain vertex colors.");
            if (material.shader == null || material.shader.name != ShaderName)
                throw new InvalidOperationException($"{zone.key} material did not retain {ShaderName}.");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[UrbanHybridTerrainBuilder] Built {Zones.Length} urban profiles, materials, and baked vertex-color meshes.");
    }

    static HybridZoneTerrainProfile LoadOrCreateProfile(ZoneDefinition zone)
    {
        string path = $"{ProfileRoot}/{zone.key}_UrbanTerrain.asset";
        HybridZoneTerrainProfile profile = AssetDatabase.LoadAssetAtPath<HybridZoneTerrainProfile>(path);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<HybridZoneTerrainProfile>();
            AssetDatabase.CreateAsset(profile, path);
        }

        profile.surfaceFamily = HybridZoneSurfaceFamily.Urban;
        profile.blendSource = HybridZoneBlendSource.VertexColors;
        profile.vertexBlendContrast = 1.25f;
        profile.layer0Tint = zone.baseTint;
        profile.layer1Tint = zone.repairTint;
        profile.layer2Tint = zone.grimeTint;
        profile.layer3Tint = Color.white;
        profile.blendExponent = 1.1f;
        profile.macroScale = 0.06f;
        profile.macroStrength = 0.045f;
        profile.exposedTint = new Color(1.02f, 1f, 0.94f, 1f);
        profile.recessTint = new Color(0.82f, 0.84f, 0.82f, 1f);
        profile.slopeStrength = 0.10f;
        profile.elevationTintStrength = 0.02f;
        profile.heightMinMax = new Vector2(-0.2f, 0.4f);
        profile.ambientBoost = 0.32f;
        profile.wetDarkening = 0.28f;
        profile.wetHighlight = 0.05f;
        profile.wetTint = new Color(0.55f, 0.52f, 0.46f, 1f);
        profile.layerWetResponse = new Vector4(0.42f, 0.72f, 1f, 0f);
        EditorUtility.SetDirty(profile);
        return profile;
    }

    static Material LoadOrCreateMaterial(ZoneDefinition zone, Shader shader, HybridZoneTerrainProfile profile,
        Texture2D stone, Texture2D terracotta, Texture2D pavers)
    {
        string path = $"{MaterialRoot}/{zone.key}_UrbanTerrain.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader) { name = $"{zone.key}_UrbanTerrain" };
            AssetDatabase.CreateAsset(material, path);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
        }

        material.SetTexture("_Splat0", stone);
        material.SetTexture("_Splat1", terracotta);
        material.SetTexture("_Splat2", pavers);
        material.SetVector("_Splat0_ST", new Vector4(0.25f, 0.25f, 0f, 0f));
        material.SetVector("_Splat1_ST", new Vector4(0.25f, 0.25f, 0f, 0f));
        material.SetVector("_Splat2_ST", new Vector4(0.22f, 0.22f, 0f, 0f));
        profile.ApplyTo(material);
        EditorUtility.SetDirty(material);
        return material;
    }

    static Mesh LoadOrCreateMesh(ZoneDefinition zone)
    {
        string path = $"{MeshRoot}/{zone.key}_UrbanGround.asset";
        Mesh generated = BuildMesh(zone);
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(generated, path);
            return generated;
        }

        EditorUtility.CopySerialized(generated, existing);
        UnityEngine.Object.DestroyImmediate(generated);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    static Mesh BuildMesh(ZoneDefinition zone)
    {
        int columns = Mathf.CeilToInt(zone.size.x) + 1;
        int rows = Mathf.CeilToInt(zone.size.y) + 1;
        var mesh = new Mesh { name = $"{zone.key}_UrbanGround" };
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
            float localZ = (z01 - 0.5f) * zone.size.y;
            for (int x = 0; x < columns; x++, vertex++)
            {
                float x01 = x / (float)(columns - 1);
                float localX = (x01 - 0.5f) * zone.size.x;
                vertices[vertex] = new Vector3(localX, 0f, localZ);
                normals[vertex] = Vector3.up;
                tangents[vertex] = new Vector4(1f, 0f, 0f, 1f);
                uvs[vertex] = new Vector2(localX / 4f, localZ / 4f);

                float band = 1f - Mathf.Clamp01(Mathf.Abs(Mathf.Sin((localX * 0.19f) + (localZ * 0.07f))) / 0.23f);
                float repair = Mathf.Clamp01(band * zone.repairDensity * 0.45f);
                float edge = 1f - Mathf.Clamp01(Mathf.Min(zone.size.x * 0.5f - Mathf.Abs(localX), zone.size.y * 0.5f - Mathf.Abs(localZ)) / 4f);
                float noise = Mathf.PerlinNoise(localX * 0.075f + 29f, localZ * 0.075f + 11f);
                float grime = Mathf.Clamp01((edge * 0.72f + Mathf.Max(0f, noise - 0.7f) * 0.5f) * zone.grimeStrength);
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

    static Texture2D RequireTexture(string path)
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null) throw new InvalidOperationException($"Missing urban terrain texture: {path}");
        return texture;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
        string name = System.IO.Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
