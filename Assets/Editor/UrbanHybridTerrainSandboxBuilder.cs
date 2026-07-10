using RealBlend;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class UrbanHybridTerrainSandboxBuilder
{
    const string Root = "Assets/ToolingSandbox/UrbanHybridTerrain";
    const string ScenePath = "Assets/ToolingSandbox/UrbanHybridTerrainSandbox.unity";
    const string MeshPath = Root + "/Piazza_VertexProof.asset";
    const string MaterialPath = Root + "/Piazza_RealBlendProof.mat";
    const string ControlMaterialPath = Root + "/Piazza_Control.mat";
    const string RealBlendExampleMaterial = "Assets/RealBlend/Art/Materials/URP/VariationExampleURP.mat";
    const string StoneTexture = "Assets/Environment/FlorentineInnFloor1/StructuralKit/Textures/Inn_PietraSerena_Albedo.png";
    const string TerracottaTexture = "Assets/Environment/FlorentineInnFloor1/StructuralKit/Textures/Inn_ServiceTerracotta_Albedo.png";
    const string PaverTexture = "Assets/Environment/FlorentineInnFloor1/StructuralKit/Textures/Inn_CourtyardPavers_Albedo.png";

    const float Width = 60f;
    const float Length = 56f;
    const int Columns = 61;
    const int Rows = 57;

    [MenuItem("InfernosCurse/Hybrid Zones/Build Urban RealBlend Sandbox")]
    public static void Build()
    {
        EnsureFolder("Assets/ToolingSandbox");
        EnsureFolder(Root);

        Mesh generated = BuildVertexProofMesh();
        Mesh proofMesh = CreateOrUpdateAsset(generated, MeshPath);
        Material proofMaterial = BuildRealBlendMaterial();
        Material controlMaterial = BuildControlMaterial();

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SceneManager.SetActiveScene(scene);

        BuildSurface("RealBlend_VertexPainted", new Vector3(-34f, 0f, 0f), proofMesh, proofMaterial, true);
        BuildSurface("Control_URPLit", new Vector3(34f, 0f, 0f), proofMesh, controlMaterial, false);
        BuildLightingAndCamera();

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[UrbanHybridTerrainSandbox] Built {ScenePath}: " +
                  $"mesh={proofMesh.vertexCount} vertices/{proofMesh.triangles.Length / 3} triangles, " +
                  $"colors={proofMesh.colors.Length}, shader={proofMaterial.shader.name}.");
    }

    static Mesh BuildVertexProofMesh()
    {
        var mesh = new Mesh { name = "Piazza_VertexProof" };
        var vertices = new Vector3[Columns * Rows];
        var normals = new Vector3[vertices.Length];
        var tangents = new Vector4[vertices.Length];
        var uvs = new Vector2[vertices.Length];
        var colors = new Color[vertices.Length];
        var triangles = new int[(Columns - 1) * (Rows - 1) * 6];

        int vertex = 0;
        for (int z = 0; z < Rows; z++)
        {
            float z01 = z / (float)(Rows - 1);
            float localZ = (z01 - 0.5f) * Length;
            for (int x = 0; x < Columns; x++, vertex++)
            {
                float x01 = x / (float)(Columns - 1);
                float localX = (x01 - 0.5f) * Width;
                vertices[vertex] = new Vector3(localX, 0f, localZ);
                normals[vertex] = Vector3.up;
                tangents[vertex] = new Vector4(1f, 0f, 0f, 1f);
                uvs[vertex] = new Vector2(localX / 4f, localZ / 4f);

                float repairA = 1f - Mathf.Clamp01(Mathf.Abs(localZ - (localX * 0.18f + 3f)) / 1.35f);
                float repairB = 1f - Mathf.Clamp01(Mathf.Abs(localX + 14f) / 1.1f);
                float repairNoise = Mathf.PerlinNoise(localX * 0.085f + 19f, localZ * 0.085f + 7f);
                float repair = Mathf.Clamp01(Mathf.Max(repairA * 0.85f, repairB * 0.65f) * Mathf.Lerp(0.65f, 1f, repairNoise));

                float edgeDistance = Mathf.Min(Width * 0.5f - Mathf.Abs(localX), Length * 0.5f - Mathf.Abs(localZ));
                float edgeGrime = 1f - Mathf.Clamp01(edgeDistance / 5f);
                float patchNoise = Mathf.PerlinNoise(localX * 0.06f + 41f, localZ * 0.06f + 13f);
                float grime = Mathf.Clamp01(edgeGrime * 0.72f + Mathf.Max(0f, patchNoise - 0.62f) * 0.6f);

                repair *= 1f - grime * 0.55f;
                float baseStone = Mathf.Clamp01(1f - Mathf.Max(repair, grime));
                colors[vertex] = new Color(repair, grime, baseStone, grime);
            }
        }

        int triangle = 0;
        for (int z = 0; z < Rows - 1; z++)
        {
            for (int x = 0; x < Columns - 1; x++)
            {
                int a = z * Columns + x;
                int b = a + 1;
                int c = a + Columns;
                int d = c + 1;
                triangles[triangle++] = a;
                triangles[triangle++] = c;
                triangles[triangle++] = b;
                triangles[triangle++] = b;
                triangles[triangle++] = c;
                triangles[triangle++] = d;
            }
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

    static Mesh CreateOrUpdateAsset(Mesh generated, string path)
    {
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(generated, path);
            return generated;
        }

        EditorUtility.CopySerialized(generated, existing);
        Object.DestroyImmediate(generated);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    static Material BuildRealBlendMaterial()
    {
        Material example = AssetDatabase.LoadAssetAtPath<Material>(RealBlendExampleMaterial);
        if (example == null || example.shader == null || !example.shader.isSupported)
            throw new System.InvalidOperationException("RealBlend URP example material is missing or unsupported.");

        var generated = new Material(example) { name = "Piazza_RealBlendProof" };
        Texture2D stone = RequireTexture(StoneTexture);
        Texture2D terracotta = RequireTexture(TerracottaTexture);
        Texture2D pavers = RequireTexture(PaverTexture);

        generated.SetTexture("_Base_Albedo", stone);
        generated.SetTexture("_Layer1_Albedo", terracotta);
        generated.SetTexture("_Layer2_Albedo", pavers);
        generated.SetColor("_Base_Tint", new Color(0.84f, 0.80f, 0.72f, 1f));
        generated.SetColor("_Layer1_Tint", new Color(0.78f, 0.46f, 0.31f, 1f));
        generated.SetColor("_Layer2_Tint", new Color(0.37f, 0.33f, 0.27f, 1f));
        generated.SetVector("_Base_Tiling", new Vector4(0.25f, 0.25f, 0f, 0f));
        generated.SetVector("_Layer1_Tiling", new Vector4(0.25f, 0.25f, 0f, 0f));
        generated.SetVector("_Layer2_Tiling", new Vector4(0.22f, 0.22f, 0f, 0f));
        generated.SetFloat("_Layer1_Opacity", 1f);
        generated.SetFloat("_Layer2_Opacity", 0.72f);
        generated.SetFloat("_Layer1_Contrast", 1.6f);
        generated.SetFloat("_Layer2_Contrast", 1.35f);
        return CreateOrUpdateMaterial(generated, MaterialPath);
    }

    static Material BuildControlMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new System.InvalidOperationException("URP Lit shader is missing.");
        var generated = new Material(shader) { name = "Piazza_Control" };
        generated.SetTexture("_BaseMap", RequireTexture(StoneTexture));
        generated.SetColor("_BaseColor", new Color(0.84f, 0.80f, 0.72f, 1f));
        generated.SetFloat("_Smoothness", 0.15f);
        generated.SetFloat("_Cull", 0f);
        generated.doubleSidedGI = true;
        return CreateOrUpdateMaterial(generated, ControlMaterialPath);
    }

    static Material CreateOrUpdateMaterial(Material generated, string path)
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(generated, path);
            return generated;
        }

        EditorUtility.CopySerialized(generated, existing);
        Object.DestroyImmediate(generated);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    static void BuildSurface(string name, Vector3 position, Mesh mesh, Material material, bool proveRealBlendBake)
    {
        var go = new GameObject(name);
        go.transform.position = position;
        var filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        var collider = go.AddComponent<MeshCollider>();
        collider.sharedMesh = mesh;

        if (!proveRealBlendBake) return;

        var storage = go.AddComponent<VertexPaintStorage>();
        storage.SetBaseTopology(mesh);
        storage.paintedColors = mesh.colors;
        storage.ApplyColors();
        Object.DestroyImmediate(storage);
    }

    static void BuildLightingAndCamera()
    {
        var lightGo = new GameObject("Directional Light");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.91f, 0.78f);
        light.intensity = 1.25f;
        light.shadows = LightShadows.Soft;
        lightGo.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

        var cameraGo = new GameObject("Main Camera");
        cameraGo.tag = "MainCamera";
        var camera = cameraGo.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 48f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 300f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.12f, 0.11f, 0.10f);
        cameraGo.transform.position = new Vector3(0f, 72f, -82f);
        cameraGo.transform.rotation = Quaternion.Euler(40f, 0f, 0f);
    }

    static Texture2D RequireTexture(string path)
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null) throw new System.InvalidOperationException($"Missing proof texture: {path}");
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
