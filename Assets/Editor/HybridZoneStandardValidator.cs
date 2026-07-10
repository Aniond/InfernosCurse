using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class HybridZoneStandardValidator
{
    const string UrbanAssetRoot = "Assets/Art/Environment/HybridZones";
    const string UrbanShaderName = "InfernosCurse/HybridZoneTerrain";
    static readonly HashSet<string> SafeScenes = new()
    {
        "FlorentineInnFloor1",
        "SaloneDelleArti",
    };

    static readonly HashSet<string> DedicatedScenes = new()
    {
        "Battle",
        "BattleArena",
        "WorldMap",
    };

    static readonly HashSet<string> NaturalOutdoorScenes = new()
    {
        "Fiesole",
        "GiardinoDelleRose",
    };

    public readonly struct Finding
    {
        public readonly string scene;
        public readonly string classification;
        public readonly string details;

        public Finding(string scene, string classification, string details)
        {
            this.scene = scene;
            this.classification = classification;
            this.details = details;
        }

        public override string ToString() => $"{scene}|{classification}|{details}";
    }

    [MenuItem("InfernosCurse/Zones/Validate Hybrid Zone Standard")]
    public static void ValidateAllScenes()
    {
        var findings = AuditBuildScenes();
        int invalid = findings.Count(f => f.classification == "Invalid partial configuration");
        foreach (var finding in findings)
            Debug.Log($"[HybridZoneValidator] {finding}");

        if (invalid == 0)
            Debug.Log($"[HybridZoneValidator] Validation passed: {findings.Count} scene(s) classified, 0 invalid.");
        else
            Debug.LogError($"[HybridZoneValidator] Validation found {invalid} invalid partial configuration(s).");
    }

    [MenuItem("InfernosCurse/Zones/Validate Urban Terrain Assets")]
    public static void ValidateUrbanTerrainAssets()
    {
        string[] profileGuids = AssetDatabase.FindAssets("t:HybridZoneTerrainProfile", new[] { UrbanAssetRoot + "/Profiles/Urban" });
        var errors = new List<string>();
        foreach (string guid in profileGuids)
        {
            string profilePath = AssetDatabase.GUIDToAssetPath(guid);
            var profile = AssetDatabase.LoadAssetAtPath<HybridZoneTerrainProfile>(profilePath);
            string key = Path.GetFileNameWithoutExtension(profilePath).Replace("_UrbanTerrain", string.Empty);
            string materialPath = $"{UrbanAssetRoot}/Materials/Urban/{key}_UrbanTerrain.mat";
            string meshPath = $"{UrbanAssetRoot}/Meshes/Urban/{key}_UrbanGround.asset";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);

            if (profile == null || profile.surfaceFamily != HybridZoneSurfaceFamily.Urban || profile.blendSource != HybridZoneBlendSource.VertexColors)
                errors.Add($"{key}: urban vertex-color profile is missing or invalid");
            if (material == null || material.shader == null || material.shader.name != UrbanShaderName || material.GetFloat("_UrbanVertexBlend") < 0.5f)
                errors.Add($"{key}: urban hybrid material is missing or invalid");
            if (mesh == null || mesh.vertexCount == 0 || mesh.colors.Length != mesh.vertexCount)
                errors.Add($"{key}: baked vertex-color mesh is missing or invalid");
        }

        foreach (string error in errors) Debug.LogError("[UrbanTerrainValidator] " + error);
        if (errors.Count == 0 && profileGuids.Length == 5)
            Debug.Log("[UrbanTerrainValidator] Validation passed for 5 urban profile/material/mesh sets.");
        else if (errors.Count == 0)
            Debug.LogError($"[UrbanTerrainValidator] Expected 5 urban profile sets, found {profileGuids.Length}.");
        else
            Debug.LogError($"[UrbanTerrainValidator] Validation failed with {errors.Count} error(s).");

        ValidateProductionUrbanSurface("Assets/Scenes/MercatoVecchio.unity", "[MarketSquare]", "Floor_Cobblestone", "MercatoVecchio", errors);
        ValidateProductionUrbanSurface("Assets/Scenes/PiazzaDellaSignoria.unity", "[Ground]", "Floor_Piazza", "PiazzaDellaSignoria", errors);
        ValidateProductionUrbanSurface("Assets/Scenes/PonteVecchio.unity", "[BridgeDeck]", "Deck", "PonteVecchio", errors);
        ValidateProductionUrbanSurface("Assets/Scenes/Duomo.unity", "[Floors]", "Floor_Octagon", "Duomo", errors);
        ValidateProductionUrbanSurface("Assets/Scenes/ViaCalimala.unity", "Street_EW_Template", "Street_Paving", "ViaCalimala", errors);
        if (errors.Count == 0)
            Debug.Log("[UrbanTerrainValidator] Production validation passed for 5 urban surfaces; colliders retained.");
    }

    static void ValidateProductionUrbanSurface(string scenePath, string rootName, string objectName, string zoneKey,
        List<string> errors)
    {
        Scene scene = SceneManager.GetSceneByPath(scenePath);
        bool previewOpened = false;
        try
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                scene = EditorSceneManager.OpenPreviewScene(scenePath);
                previewOpened = true;
            }

            Transform root = scene.GetRootGameObjects().FirstOrDefault(candidate => candidate.name == rootName)?.transform;
            Transform surface = root == null
                ? null
                : root.GetComponentsInChildren<Transform>(true).FirstOrDefault(candidate => candidate.name == objectName);
            MeshFilter filter = surface != null ? surface.GetComponent<MeshFilter>() : null;
            MeshRenderer renderer = surface != null ? surface.GetComponent<MeshRenderer>() : null;
            Collider collider = surface != null ? surface.GetComponent<Collider>() : null;
            string meshPath = filter != null && filter.sharedMesh != null
                ? AssetDatabase.GetAssetPath(filter.sharedMesh)
                : string.Empty;
            string expectedMaterial = $"{zoneKey}_UrbanTerrain";

            if (surface == null || filter == null || filter.sharedMesh == null ||
                filter.sharedMesh.colors.Length != filter.sharedMesh.vertexCount ||
                !meshPath.Contains("/Meshes/Urban/Production/") ||
                renderer == null || renderer.sharedMaterial == null || renderer.sharedMaterial.name != expectedMaterial ||
                collider == null)
            {
                string error = $"{zoneKey}/{objectName}: production vertex mesh, urban material, or retained collider is invalid";
                errors.Add(error);
                Debug.LogError("[UrbanTerrainValidator] " + error);
            }

        }
        finally
        {
            if (previewOpened && scene.IsValid())
                EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    public static List<Finding> AuditBuildScenes()
    {
        var findings = new List<Finding>();
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach (var buildScene in EditorBuildSettings.scenes.Where(scene => scene.enabled))
            {
                string path = buildScene.path;
                string name = Path.GetFileNameWithoutExtension(path);
                if (DedicatedScenes.Contains(name))
                {
                    findings.Add(new Finding(name, "Dedicated/non-zone", "explicit arena or world-map exception"));
                    continue;
                }
                if (SafeScenes.Contains(name))
                {
                    findings.Add(new Finding(name, "Safe/noncombat", "ordinary encounters are intentionally disabled"));
                    continue;
                }

                Scene scene = SceneManager.GetSceneByPath(path);
                bool alreadyLoaded = scene.IsValid() && scene.isLoaded;
                if (!alreadyLoaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                findings.Add(Classify(scene, name));
                if (!alreadyLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }
        finally
        {
            EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
        return findings;
    }

    static Finding Classify(Scene scene, string name)
    {
        var roots = scene.GetRootGameObjects();
        var zone = roots.SelectMany(root => root.GetComponentsInChildren<ZoneBattleAuthoring>(true)).FirstOrDefault();
        var trigger = roots.SelectMany(root => root.GetComponentsInChildren<ZoneEncounterTrigger>(true)).FirstOrDefault();
        var auth = roots.SelectMany(root => root.GetComponentsInChildren<BattleMapAuthoring>(true)).FirstOrDefault();
        var player = roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(transform => transform.CompareTag("Player"));
        var camera = roots.SelectMany(root => root.GetComponentsInChildren<Unity.Cinemachine.CinemachineCamera>(true))
            .FirstOrDefault();

        bool partial = zone != null || trigger != null || auth != null;
        if (zone == null)
        {
            string detail = $"surface={SurfaceFamily(roots, name)}; player={(player != null ? "yes" : "no")}; camera={(camera != null ? "yes" : "no")}";
            return new Finding(name, partial ? "Invalid partial configuration" : "Migration candidate", detail);
        }

        if (!zone.combatAllowed)
            return new Finding(name, "Safe/noncombat", "ZoneBattleAuthoring explicitly disables ordinary combat");

        if (!zone.TryValidate(out string message))
            return new Finding(name, "Invalid partial configuration", message);

        if (player == null || camera == null)
            return new Finding(name, "Invalid partial configuration", "hybrid zone requires Player and Cinemachine exploration camera");

        return new Finding(name, "Valid hybrid zone",
            $"surface={SurfaceFamily(roots, name)}; grid={zone.mapAuthoring.width}x{zone.mapAuthoring.height}; terrainShader=" +
            (zone.zoneTerrain != null && zone.zoneTerrain.materialTemplate != null
                ? zone.zoneTerrain.materialTemplate.shader.name
                : "n/a"));
    }

    static string SurfaceFamily(GameObject[] roots, string sceneName)
    {
        if (roots.Any(root => root.GetComponentInChildren<Terrain>(true) != null)) return "natural-terrain";
        if (NaturalOutdoorScenes.Contains(sceneName)) return "natural-outdoor";
        string combined = string.Join(" ", roots.Select(root => root.name)).ToLowerInvariant();
        if (combined.Contains("inn") || combined.Contains("salone")) return "interior";
        return "urban-or-modular";
    }
}
