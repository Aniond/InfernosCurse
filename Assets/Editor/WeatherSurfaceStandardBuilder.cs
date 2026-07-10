using System;
using System.Collections.Generic;
using System.Linq;
using sc.stylizedgrass.runtime;
using StylizedWater3;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public enum StandardWaterProfile
{
    River,
    Pond,
    Fountain,
    BattleShallow
}

/// <summary>
/// Authoritative editor factory for all project grass/water surfaces. Builders
/// call this instead of assigning vendor demo assets or custom water materials.
/// </summary>
public static class WeatherSurfaceStandardBuilder
{
    public const string Root = "Assets/Art/Environment/WeatherSurfaces";
    public const string GrassFolder = Root + "/Grass";
    public const string WaterFolder = Root + "/Water";
    public const string VfxFolder = Root + "/VFX";
    public const string MeshFolder = GrassFolder + "/Meshes";

    const string VendorGrass = "Packages/xyz.staggart-creations.stylized-grass/Materials/StylizedGrass.mat";
    const string VendorWater = "Assets/Stylized Water 3/Materials/StylizedWater3_Clear.mat";
    const string VendorSplash = "Assets/Stylized Water 3/Materials/Effects/SplashParticle.mat";
    const string GameSystemsPath = "Assets/Resources/GameSystems.prefab";

    [MenuItem("InfernosCurse/Environment/Weather Surfaces/1. Ensure Shared Standard")]
    public static void EnsureSharedStandard()
    {
        EnsureFolders();
        EnsureGrassMaterial(false);
        EnsureGrassMaterial(true);
        foreach (StandardWaterProfile profile in Enum.GetValues(typeof(StandardWaterProfile)))
            EnsureWaterMaterial(profile);
        EnsureRainMaterial();
        EnsureGrassRenderFeature();
        EnsurePersistentController();
        AssetDatabase.SaveAssets();
        Debug.Log("[WeatherSurfaceStandard] Shared materials, persistent controllers, and URP features are ready.");
    }

    [MenuItem("InfernosCurse/Environment/Weather Surfaces/3. Convert Existing Scenes")]
    public static void ConvertExistingScenes()
    {
        EnsureSharedStandard();
        ConvertWaterTilePrefab();

        var setup = EditorSceneManager.GetSceneManagerSetup();
        string[] paths =
        {
            "Assets/Scenes/Fiesole.unity",
            "Assets/Scenes/GiardinoDelleRose.unity",
            "Assets/Scenes/PonteVecchio.unity",
            "Assets/Scenes/MercatoVecchio.unity",
            "Assets/Scenes/FlorentineInnFloor1.unity"
        };

        try
        {
            foreach (string path in paths)
            {
                Scene scene = SceneManager.GetSceneByPath(path);
                bool wasLoaded = scene.IsValid() && scene.isLoaded;
                if (!wasLoaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                ConvertScene(scene);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }

        AssetDatabase.SaveAssets();
        Debug.Log("[WeatherSurfaceStandard] Existing exploration scenes converted.");
    }

    static void ConvertScene(Scene scene)
    {
        switch (scene.name)
        {
            case "Fiesole":
                ConvertFiesole(scene);
                break;
            case "GiardinoDelleRose":
                ConvertGiardino(scene);
                break;
            case "PonteVecchio":
                ConfigureNamedWater(scene, "Arno_Water", StandardWaterProfile.River, WeatherSurfaceExposure.Outdoor);
                ConfigureNamedWater(scene, "Fountain_Water", StandardWaterProfile.Fountain, WeatherSurfaceExposure.Outdoor);
                break;
            case "MercatoVecchio":
                ConfigureNamedWater(scene, "WaterTile", StandardWaterProfile.River, WeatherSurfaceExposure.Outdoor);
                ConvertMercatoFountain(scene);
                break;
            case "FlorentineInnFloor1":
                ConfigureNamedWater(scene, "CourtyardFountain_Water", StandardWaterProfile.Fountain, WeatherSurfaceExposure.Indoor);
                break;
        }
    }

    static void ConvertFiesole(Scene scene)
    {
        var meadow = FindNamed<MeshRenderer>(scene, "Meadow");
        if (meadow == null) return;
        var existing = FindNamed<WeatherSurface>(scene, "StylizedGrass_Meadow");
        if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);

        Bounds bounds = meadow.bounds;
        CreateGrassField(
            "StylizedGrass_Meadow", meadow.transform.parent, bounds,
            (x, z) => bounds.max.y + 0.025f,
            (x, z) => !(Mathf.Abs(x) < 3f && z > -14f && z < 6f) &&
                      !(Mathf.Abs(x) < 7.5f && z > 0f && z < 11f),
            0.75f, 1269, true, "Fiesole_GrassField");
    }

    static void ConvertGiardino(Scene scene)
    {
        var terrain = FindNamed<Terrain>(scene, "Terrain_Giardino");
        if (terrain != null)
        {
            var existing = FindNamed<WeatherSurface>(scene, "StylizedGrass_Garden");
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);
            var data = terrain.terrainData;
            float[,,] alpha = data.GetAlphamaps(0, 0, data.alphamapWidth, data.alphamapHeight);
            int grassLayer = 0;
            for (int i = 0; i < data.terrainLayers.Length; i++)
            {
                var layer = data.terrainLayers[i];
                string n = layer != null ? layer.name.ToLowerInvariant() : string.Empty;
                string t = layer != null && layer.diffuseTexture != null
                    ? layer.diffuseTexture.name.ToLowerInvariant()
                    : string.Empty;
                if (n.Contains("grass") || n.Contains("meadow") || t.Contains("grass") || t.Contains("meadow"))
                {
                    grassLayer = i;
                    break;
                }
            }

            Bounds bounds = new Bounds(new Vector3(16f, 1f, 16f), new Vector3(31f, 0.1f, 31f));
            CreateGrassField(
                "StylizedGrass_Garden", terrain.transform, bounds,
                (x, z) => terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y + 0.025f,
                (x, z) =>
                {
                    float nx = Mathf.InverseLerp(terrain.transform.position.x,
                        terrain.transform.position.x + data.size.x, x);
                    float nz = Mathf.InverseLerp(terrain.transform.position.z,
                        terrain.transform.position.z + data.size.z, z);
                    int ax = Mathf.Clamp(Mathf.RoundToInt(nx * (data.alphamapWidth - 1)), 0, data.alphamapWidth - 1);
                    int az = Mathf.Clamp(Mathf.RoundToInt(nz * (data.alphamapHeight - 1)), 0, data.alphamapHeight - 1);
                    return alpha[az, ax, grassLayer] > 0.55f &&
                           Vector2.Distance(new Vector2(x, z), new Vector2(16f, 16f)) > 3.4f;
                },
                0.70f, 1333, false, "GiardinoWalled_GrassField");
        }

        ConfigureNamedWater(scene, "FountainWater", StandardWaterProfile.Fountain, WeatherSurfaceExposure.Outdoor);
        ConfigureNamedWater(scene, "PondWater", StandardWaterProfile.Pond, WeatherSurfaceExposure.Outdoor);
    }

    static void ConvertMercatoFountain(Scene scene)
    {
        var existing = FindNamed<WeatherSurface>(scene, "FountainSurface");
        if (existing != null)
        {
            ConfigureWater(existing.gameObject, StandardWaterProfile.Fountain, WeatherSurfaceExposure.Outdoor);
            return;
        }
        var fountain = FindNamed<MeshRenderer>(scene, "Fountain");
        if (fountain == null) return;

        Bounds bounds = fountain.bounds;
        var water = CreateWaterDisk(
            "FountainSurface", 1.75f,
            new Vector3(bounds.center.x, bounds.min.y + 0.44f, bounds.center.z),
            StandardWaterProfile.Fountain, WeatherSurfaceExposure.Outdoor);
        water.transform.SetParent(fountain.transform, true);
        ConfigureWater(water, StandardWaterProfile.Fountain, WeatherSurfaceExposure.Outdoor);
    }

    static void ConfigureNamedWater(Scene scene, string name, StandardWaterProfile profile,
        WeatherSurfaceExposure exposure)
    {
        var renderer = FindNamed<MeshRenderer>(scene, name);
        if (renderer != null) ConfigureWater(renderer.gameObject, profile, exposure);
    }

    static T FindNamed<T>(Scene scene, string name) where T : Component
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var component in root.GetComponentsInChildren<T>(true))
                if (component.name == name) return component;
        return null;
    }

    static void ConvertWaterTilePrefab()
    {
        const string path = "Assets/Prefabs/Environment/WaterTile.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        if (root == null) return;
        try
        {
            var renderer = root.GetComponentInChildren<MeshRenderer>(true);
            if (renderer != null)
                ConfigureWater(renderer.gameObject, StandardWaterProfile.River, WeatherSurfaceExposure.Outdoor);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    public static Material EnsureGrassMaterial(bool dry)
    {
        EnsureFolders();
        string path = $"{GrassFolder}/Grass_{(dry ? "Dry" : "Meadow")}.mat";
        var source = AssetDatabase.LoadAssetAtPath<Material>(VendorGrass);
        var material = LoadOrClone(path, source);
        if (material == null) return null;

        material.name = dry ? "Grass_Dry" : "Grass_Meadow";
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", source.GetTexture("_BaseMap"));
        if (material.HasProperty("_BumpMap")) material.SetTexture("_BumpMap", source.GetTexture("_BumpMap"));
        material.EnableKeyword("_NORMALMAP");
        material.DisableKeyword("_SCALEMAP_ON");
        material.DisableKeyword("_SCALEMAP");
        material.DisableKeyword("_FADING");
        SetFloat(material, "_Scalemap", 0f);
        SetFloat(material, "_FadingOn", 0f);
        SetFloat(material, "_ColorMapStrength", 0f);
        SetColor(material, "_BaseColor", dry
            ? new Color(0.54f, 0.49f, 0.25f, 1f)
            : new Color(0.31f, 0.56f, 0.20f, 1f));
        SetFloat(material, "_Smoothness", 0.08f);
        SetFloat(material, "_WindAmbientStrength", dry ? 0.55f : 0.7f);
        SetFloat(material, "_WindGustStrength", 0.8f);
        SetFloat(material, "_WindVertexRand", 0.35f);
        SetFloat(material, "_VertexColorWindChannel", 0f);
        SetFloat(material, "_VertexColorShadingChannel", 0f);
        SetFloat(material, "_VertexColorBendingChannel", 0f);
        EditorUtility.SetDirty(material);
        return material;
    }

    public static Material EnsureWaterMaterial(StandardWaterProfile profile)
    {
        EnsureFolders();
        string path = $"{WaterFolder}/Water_{profile}.mat";
        var source = AssetDatabase.LoadAssetAtPath<Material>(VendorWater);
        var material = LoadOrClone(path, source);
        if (material == null) return null;

        material.name = "Water_" + profile;
        switch (profile)
        {
            case StandardWaterProfile.River:
                SetColor(material, "_BaseColor", new Color(0.08f, 0.28f, 0.33f, 0.78f));
                SetColor(material, "_ShallowColor", new Color(0.18f, 0.46f, 0.48f, 0.66f));
                SetVector(material, "_Direction", new Vector4(-1f, 0.12f, 0f, 0f));
                SetFloat(material, "_Speed", 0.62f);
                SetFloat(material, "_WaveHeight", 0.12f);
                SetFloat(material, "_WaveFrequency", 0.75f);
                break;
            case StandardWaterProfile.Pond:
                SetColor(material, "_BaseColor", new Color(0.09f, 0.31f, 0.27f, 0.72f));
                SetColor(material, "_ShallowColor", new Color(0.24f, 0.55f, 0.42f, 0.62f));
                SetFloat(material, "_Speed", 0.28f);
                SetFloat(material, "_WaveHeight", 0.055f);
                SetFloat(material, "_WaveFrequency", 1.15f);
                break;
            case StandardWaterProfile.Fountain:
                SetColor(material, "_BaseColor", new Color(0.16f, 0.46f, 0.58f, 0.64f));
                SetColor(material, "_ShallowColor", new Color(0.37f, 0.72f, 0.75f, 0.55f));
                SetFloat(material, "_Speed", 0.42f);
                SetFloat(material, "_WaveHeight", 0.025f);
                SetFloat(material, "_WaveFrequency", 1.8f);
                break;
            default:
                SetColor(material, "_BaseColor", new Color(0.10f, 0.32f, 0.39f, 0.76f));
                SetColor(material, "_ShallowColor", new Color(0.25f, 0.59f, 0.64f, 0.62f));
                SetFloat(material, "_Speed", 0.35f);
                SetFloat(material, "_WaveHeight", 0.035f);
                SetFloat(material, "_WaveFrequency", 1.3f);
                break;
        }
        EditorUtility.SetDirty(material);
        return material;
    }

    public static GameObject CreateWaterDisk(string name, float diameter, Vector3 position,
        StandardWaterProfile profile, WeatherSurfaceExposure exposure)
    {
        EnsureSharedAssetsOnly();
        var mesh = WaterMesh.Create(WaterMesh.Shape.Disk, diameter, 0.2f);
        var water = WaterObject.New(EnsureWaterMaterial(profile), mesh);
        water.gameObject.name = name;
        water.transform.position = position;
        ConfigureWater(water.gameObject, profile, exposure);
        return water.gameObject;
    }

    public static WeatherSurface ConfigureWater(GameObject waterObject,
        StandardWaterProfile profile, WeatherSurfaceExposure exposure)
    {
        if (waterObject == null) return null;
        EnsureSharedAssetsOnly();

        var renderer = waterObject.GetComponent<MeshRenderer>();
        var filter = waterObject.GetComponent<MeshFilter>();
        if (renderer == null || filter == null)
        {
            Debug.LogError($"[WeatherSurfaceStandard] '{waterObject.name}' needs MeshFilter and MeshRenderer.", waterObject);
            return null;
        }

        renderer.sharedMaterial = EnsureWaterMaterial(profile);
        renderer.shadowCastingMode = ShadowCastingMode.Off;

        var vendor = waterObject.GetComponent<WaterObject>();
        if (vendor == null) vendor = waterObject.AddComponent<WaterObject>();
        vendor.meshRenderer = renderer;
        vendor.meshFilter = filter;
        vendor.material = renderer.sharedMaterial;

        int waterLayer = LayerMask.NameToLayer("Water");
        if (waterLayer >= 0) waterObject.layer = waterLayer;

        var legacyScroll = waterObject.GetComponent<ScrollingWater>();
        if (legacyScroll != null) UnityEngine.Object.DestroyImmediate(legacyScroll);

        var surface = waterObject.GetComponent<WeatherSurface>();
        if (surface == null) surface = waterObject.AddComponent<WeatherSurface>();
        surface.surfaceKind = ToSurfaceKind(profile);
        surface.exposure = exposure;
        surface.targetRenderer = renderer;
        surface.targetTerrain = null;
        surface.stormEmissionRate = WeatherSurface.DefaultStormRate(surface.surfaceKind);

        if (exposure == WeatherSurfaceExposure.Indoor)
        {
            var old = waterObject.transform.Find("RainImpacts");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            surface.rainEmitter = null;
        }
        else
        {
            surface.rainEmitter = ConfigureRainEmitter(waterObject.transform, renderer.bounds, surface.surfaceKind);
        }

        EditorUtility.SetDirty(waterObject);
        return surface;
    }

    public static GameObject CreateGrassField(string name, Transform parent, Bounds worldBounds,
        Func<float, float, float> sampleHeight, Func<float, float, bool> allowed,
        float clustersPerSquareMeter, int seed, bool dry, string meshAssetName)
    {
        EnsureSharedAssetsOnly();
        var field = new GameObject(name);
        if (parent != null) field.transform.SetParent(parent, false);
        field.transform.position = Vector3.zero;
        field.transform.rotation = Quaternion.identity;
        field.transform.localScale = Vector3.one;

        var mesh = BuildGrassMesh(field.transform, worldBounds, sampleHeight, allowed,
            clustersPerSquareMeter, seed, meshAssetName);
        var filter = field.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        var renderer = field.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = EnsureGrassMaterial(dry);
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = true;

        var surface = field.AddComponent<WeatherSurface>();
        surface.surfaceKind = WeatherSurfaceKind.GrassField;
        surface.exposure = WeatherSurfaceExposure.Outdoor;
        surface.targetRenderer = renderer;
        surface.stormEmissionRate = WeatherSurface.DefaultStormRate(surface.surfaceKind);
        return field;
    }

    static Mesh BuildGrassMesh(Transform field, Bounds bounds,
        Func<float, float, float> sampleHeight, Func<float, float, bool> allowed,
        float density, int seed, string assetName)
    {
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var colors = new List<Color>();
        var triangles = new List<int>();
        var random = new System.Random(seed);

        int desired = Mathf.Clamp(Mathf.RoundToInt(bounds.size.x * bounds.size.z * density), 1, 2400);
        int accepted = 0;
        int attempts = 0;
        while (accepted < desired && attempts < desired * 5)
        {
            attempts++;
            float x = Mathf.Lerp(bounds.min.x, bounds.max.x, (float)random.NextDouble());
            float z = Mathf.Lerp(bounds.min.z, bounds.max.z, (float)random.NextDouble());
            if (allowed != null && !allowed(x, z)) continue;

            float y = sampleHeight != null ? sampleHeight(x, z) : bounds.max.y;
            float height = Mathf.Lerp(0.34f, 0.68f, (float)random.NextDouble());
            float width = Mathf.Lerp(0.16f, 0.30f, (float)random.NextDouble());
            float yaw = (float)random.NextDouble() * 180f;
            Vector3 center = new Vector3(x, y + 0.015f, z);

            AddBlade(field, center, height, width, yaw, vertices, normals, uvs, colors, triangles);
            AddBlade(field, center, height * 0.92f, width * 0.9f, yaw + 60f, vertices, normals, uvs, colors, triangles);
            AddBlade(field, center, height * 1.06f, width * 0.82f, yaw + 120f, vertices, normals, uvs, colors, triangles);
            accepted++;
        }

        var mesh = new Mesh { name = assetName };
        mesh.indexFormat = vertices.Count > 65000
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetColors(colors);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();

        string path = $"{MeshFolder}/{Sanitize(assetName)}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        EditorUtility.CopySerialized(mesh, existing);
        UnityEngine.Object.DestroyImmediate(mesh);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    static void AddBlade(Transform field, Vector3 worldCenter, float height, float width, float yaw,
        List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs,
        List<Color> colors, List<int> triangles)
    {
        Vector3 right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right * (width * 0.5f);
        Vector3 normal = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        Vector3 bottomLeft = field.InverseTransformPoint(worldCenter - right);
        Vector3 bottomRight = field.InverseTransformPoint(worldCenter + right);
        Vector3 shoulderLeft = field.InverseTransformPoint(worldCenter - right * 0.62f + Vector3.up * height * 0.68f);
        Vector3 shoulderRight = field.InverseTransformPoint(worldCenter + right * 0.62f + Vector3.up * height * 0.68f);
        Vector3 tip = field.InverseTransformPoint(worldCenter + Vector3.up * height);
        int start = vertices.Count;
        vertices.Add(bottomLeft); vertices.Add(bottomRight); vertices.Add(shoulderLeft); vertices.Add(shoulderRight); vertices.Add(tip);
        normals.Add(normal); normals.Add(normal); normals.Add(normal); normals.Add(normal); normals.Add(normal);
        uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(1f, 0f));
        uvs.Add(new Vector2(0.18f, 0.68f)); uvs.Add(new Vector2(0.82f, 0.68f)); uvs.Add(new Vector2(0.5f, 1f));
        colors.Add(Color.black); colors.Add(Color.black);
        colors.Add(new Color(0.68f, 0.68f, 0.68f, 0.68f));
        colors.Add(new Color(0.68f, 0.68f, 0.68f, 0.68f));
        colors.Add(Color.white);
        triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1);
        triangles.Add(start + 1); triangles.Add(start + 2); triangles.Add(start + 3);
        triangles.Add(start + 2); triangles.Add(start + 4); triangles.Add(start + 3);
    }

    static ParticleSystem ConfigureRainEmitter(Transform parent, Bounds bounds, WeatherSurfaceKind kind)
    {
        Transform existing = parent.Find("RainImpacts");
        GameObject go = existing != null ? existing.gameObject : new GameObject("RainImpacts");
        go.transform.SetParent(parent, true);
        go.transform.position = new Vector3(bounds.center.x, parent.position.y + 0.025f, bounds.center.z);
        go.transform.rotation = Quaternion.identity;
        Vector3 parentScale = parent.lossyScale;
        go.transform.localScale = new Vector3(
            SafeInverse(parentScale.x), SafeInverse(parentScale.y), SafeInverse(parentScale.z));

        var particles = go.GetComponent<ParticleSystem>();
        if (particles == null) particles = go.AddComponent<ParticleSystem>();

        var main = particles.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = kind == WeatherSurfaceKind.River ? 0.48f : 0.38f;
        main.startSpeed = 0f;
        main.startSize = kind == WeatherSurfaceKind.River
            ? new ParticleSystem.MinMaxCurve(0.22f, 0.58f)
            : new ParticleSystem.MinMaxCurve(0.12f, 0.34f);
        main.maxParticles = 360;

        var emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;

        var shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(Mathf.Max(0.2f, bounds.size.x), 0.01f, Mathf.Max(0.2f, bounds.size.z));

        var animation = particles.textureSheetAnimation;
        animation.enabled = true;
        animation.mode = ParticleSystemAnimationMode.Grid;
        animation.numTilesX = 6;
        animation.numTilesY = 6;
        animation.animation = ParticleSystemAnimationType.WholeSheet;
        animation.frameOverTime = new ParticleSystem.MinMaxCurve(0f, 1f);
        animation.cycleCount = 1;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        renderer.sharedMaterial = EnsureRainMaterial();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return particles;
    }

    static float SafeInverse(float value) => Mathf.Abs(value) > 0.0001f ? 1f / value : 1f;

    static void EnsurePersistentController()
    {
        var root = PrefabUtility.LoadPrefabContents(GameSystemsPath);
        if (root == null) return;
        try
        {
            if (root.GetComponent<WeatherSurfaceController>() == null)
                root.AddComponent<WeatherSurfaceController>();
            if (root.GetComponent<WindController>() == null)
                root.AddComponent<WindController>();
            if (root.GetComponent<WeatherShadingController>() == null)
                root.AddComponent<WeatherShadingController>();
            PrefabUtility.SaveAsPrefabAsset(root, GameSystemsPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void EnsureGrassRenderFeature()
    {
        if (!sc.stylizedgrass.runtime.PipelineUtilities.RenderFeatureAdded<GrassRenderFeature>())
            sc.stylizedgrass.runtime.PipelineUtilities.SetupRenderFeature<GrassRenderFeature>("Stylized Grass");
    }

    static void EnsureSharedAssetsOnly()
    {
        EnsureFolders();
        EnsureGrassMaterial(false);
        EnsureGrassMaterial(true);
        EnsureRainMaterial();
    }

    static Material EnsureRainMaterial()
    {
        string path = $"{VfxFolder}/WaterRainSplash.mat";
        return LoadOrClone(path, AssetDatabase.LoadAssetAtPath<Material>(VendorSplash));
    }

    static Material LoadOrClone(string path, Material source)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        if (source == null)
        {
            Debug.LogError($"[WeatherSurfaceStandard] Missing source material for {path}.");
            return null;
        }
        material = new Material(source);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    static WeatherSurfaceKind ToSurfaceKind(StandardWaterProfile profile)
    {
        switch (profile)
        {
            case StandardWaterProfile.River: return WeatherSurfaceKind.River;
            case StandardWaterProfile.Pond: return WeatherSurfaceKind.Pond;
            case StandardWaterProfile.Fountain: return WeatherSurfaceKind.Fountain;
            default: return WeatherSurfaceKind.BattleWater;
        }
    }

    static void EnsureFolders()
    {
        EnsureFolder("Assets/Art");
        EnsureFolder("Assets/Art/Environment");
        EnsureFolder(Root);
        EnsureFolder(GrassFolder);
        EnsureFolder(WaterFolder);
        EnsureFolder(VfxFolder);
        EnsureFolder(MeshFolder);
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        string parent = path.Substring(0, slash);
        string name = path.Substring(slash + 1);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    static string Sanitize(string name)
    {
        foreach (char invalid in System.IO.Path.GetInvalidFileNameChars())
            name = name.Replace(invalid, '_');
        return name.Replace(' ', '_');
    }

    static void SetColor(Material material, string name, Color value)
    {
        if (material.HasProperty(name)) material.SetColor(name, value);
    }

    static void SetVector(Material material, string name, Vector4 value)
    {
        if (material.HasProperty(name)) material.SetVector(name, value);
    }

    static void SetFloat(Material material, string name, float value)
    {
        if (material.HasProperty(name)) material.SetFloat(name, value);
    }
}

public static class WeatherSurfaceStandardValidator
{
    [MenuItem("InfernosCurse/Environment/Weather Surfaces/2. Validate All Scenes")]
    public static void ValidateAllScenes()
    {
        int findings = 0;
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach (string path in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" })
                         .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p))
            {
                Scene scene = SceneManager.GetSceneByPath(path);
                bool wasLoaded = scene.IsValid() && scene.isLoaded;
                if (!wasLoaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                findings += ValidateScene(scene, path);
                if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }

        if (findings == 0) Debug.Log("[WeatherSurfaceStandard] Validation passed: no unexplained legacy grass or water surfaces.");
        else Debug.LogError($"[WeatherSurfaceStandard] Validation found {findings} surface issue(s).");
    }

    static int ValidateScene(Scene scene, string path)
    {
        int findings = 0;
        bool grassCandidate = false;
        bool grassStandard = false;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var surface in root.GetComponentsInChildren<WeatherSurface>(true))
            {
                if (surface.IsGrass) grassStandard = true;
                if (surface.IsWater && surface.targetRenderer != null)
                {
                    string shader = surface.targetRenderer.sharedMaterial != null &&
                                    surface.targetRenderer.sharedMaterial.shader != null
                        ? surface.targetRenderer.sharedMaterial.shader.name
                        : string.Empty;
                    if (!shader.Contains("Stylized Water 3"))
                    {
                        Debug.LogError($"[WeatherSurfaceStandard] {path}: registered water '{surface.name}' is not Stylized Water 3.", surface);
                        findings++;
                    }
                    if (surface.exposure != WeatherSurfaceExposure.Indoor && surface.rainEmitter == null)
                    {
                        Debug.LogError($"[WeatherSurfaceStandard] {path}: exposed water '{surface.name}' has no rain impacts.", surface);
                        findings++;
                    }
                }
            }

            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                string combined = (renderer.name + " " +
                    (renderer.sharedMaterial != null ? renderer.sharedMaterial.name : string.Empty)).ToLowerInvariant();
                if (combined.Contains("meadow") || combined.Contains("grass")) grassCandidate = true;
                if ((combined.Contains("water") || combined.Contains("river")) &&
                    renderer.GetComponent<WeatherSurface>() == null)
                {
                    Debug.LogError($"[WeatherSurfaceStandard] {path}: legacy/unregistered water renderer '{renderer.name}'.", renderer);
                    findings++;
                }
            }

            foreach (var terrain in root.GetComponentsInChildren<Terrain>(true))
                if (terrain.terrainData != null && terrain.terrainData.terrainLayers.Any(l =>
                        l != null && (l.name.ToLowerInvariant().Contains("grass") ||
                                      (l.diffuseTexture != null && l.diffuseTexture.name.ToLowerInvariant().Contains("grass")))))
                    grassCandidate = true;
        }

        if (grassCandidate && !grassStandard)
        {
            Debug.LogError($"[WeatherSurfaceStandard] {path}: grass-designated ground has no Stylized Grass surface.");
            findings++;
        }
        return findings;
    }
}
