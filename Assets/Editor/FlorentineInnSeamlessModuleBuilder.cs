using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class FlorentineInnSeamlessModuleBuilder
{
    public const string SourceScenePath = "Assets/Scenes/FlorentineInnFloor1.unity";
    public const string ModuleRoot = "Assets/Environment/FlorentineInnFloor1/Prefabs";
    public const string ModulePrefabPath = ModuleRoot + "/FlorentineInnFloor1_Module.prefab";

    static readonly string[] ContentRoots =
    {
        "[Architecture]",
        "[Props]",
        "[NPCs]",
        "[Interactions]",
        "[Lighting]",
    };

    [MenuItem("InfernosCurse/Florentine Inn/2. Build Seamless Floor 1 Module")]
    public static void Build()
    {
        EnsureFolder(ModuleRoot);
        Scene scene = EditorSceneManager.OpenPreviewScene(SourceScenePath);
        var moduleObject = new GameObject("FlorentineInnFloor1_Module");
        try
        {
            foreach (string rootName in ContentRoots)
            {
                GameObject source = scene.GetRootGameObjects().FirstOrDefault(root => root.name == rootName);
                if (source == null) throw new InvalidOperationException($"Inn source scene is missing {rootName}.");
                GameObject copy = UnityEngine.Object.Instantiate(source, moduleObject.transform);
                copy.name = rootName;
                copy.transform.localPosition = Vector3.zero;
                copy.transform.localRotation = Quaternion.identity;
                copy.transform.localScale = Vector3.one;
            }

            StripStandaloneLighting(moduleObject);
            BuildRuntimeContract(moduleObject);

            PrefabUtility.SaveAsPrefabAsset(moduleObject, ModulePrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(moduleObject);
            EditorSceneManager.ClosePreviewScene(scene);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        FlorentineInnSeamlessModuleValidator.Validate();
        Debug.Log("[FlorentineInnSeamlessModuleBuilder] Rebuilt the protected Floor 1 seamless-interior module without changing the standalone scene.");
    }

    static void StripStandaloneLighting(GameObject moduleObject)
    {
        foreach (IndoorWeatherLightingGuard guard in moduleObject.GetComponentsInChildren<IndoorWeatherLightingGuard>(true))
            UnityEngine.Object.DestroyImmediate(guard);

        foreach (Light light in moduleObject.GetComponentsInChildren<Light>(true))
            if (light.type == LightType.Directional)
                UnityEngine.Object.DestroyImmediate(light.gameObject);
    }

    static void BuildRuntimeContract(GameObject moduleObject)
    {
        var contractRoot = new GameObject("[SeamlessInterior]");
        contractRoot.transform.SetParent(moduleObject.transform, false);

        Transform exteriorThreshold = Anchor(contractRoot.transform, "ExteriorThreshold", new Vector3(0f, 0.05f, -12.05f));
        Transform interiorThreshold = Anchor(contractRoot.transform, "InteriorThreshold", new Vector3(0f, 0.05f, -10.75f));
        Transform exteriorFallback = Anchor(contractRoot.transform, "ExteriorFallback", new Vector3(0f, 0.05f, -13.25f));
        Transform interiorFallback = Anchor(contractRoot.transform, "InteriorFallback", new Vector3(0f, 0.05f, -9.70f));

        var portalObject = new GameObject("Portal_AlbergoFiorentino");
        portalObject.transform.SetParent(contractRoot.transform, false);
        portalObject.transform.localPosition = new Vector3(0f, 1.2f, -11.40f);
        var portalCollider = portalObject.AddComponent<BoxCollider>();
        portalCollider.size = new Vector3(4.8f, 3f, 1.5f);
        portalCollider.isTrigger = true;
        var portal = portalObject.AddComponent<SeamlessInteriorPortal>();

        var blockerObject = new GameObject("BattleBlocker_AlbergoFiorentino");
        blockerObject.transform.SetParent(contractRoot.transform, false);
        blockerObject.transform.localPosition = new Vector3(0f, 1.2f, -11.25f);
        var battleBlocker = blockerObject.AddComponent<BoxCollider>();
        battleBlocker.size = new Vector3(4.5f, 3f, 0.4f);
        battleBlocker.enabled = false;

        var environmentObject = new GameObject("EnvironmentZone_AlbergoFiorentino");
        environmentObject.transform.SetParent(contractRoot.transform, false);
        var environment = environmentObject.AddComponent<SeamlessInteriorEnvironmentZone>();
        Light[] localLights = moduleObject.GetComponentsInChildren<Light>(true)
            .Where(light => light.type != LightType.Directional).ToArray();
        environment.Configure(null, localLights, Array.Empty<AudioSource>(), Array.Empty<AudioSource>());

        var cameraObject = new GameObject("CameraZone_AlbergoFiorentino");
        cameraObject.transform.SetParent(contractRoot.transform, false);
        var cameraZone = cameraObject.AddComponent<SeamlessInteriorCameraZone>();
        Renderer[] occluders = moduleObject.GetComponentsInChildren<Transform>(true)
            .Where(transform => transform.name.StartsWith("InnWall_", StringComparison.Ordinal) ||
                                transform.name.StartsWith("CourtyardLintel_", StringComparison.Ordinal))
            .SelectMany(transform => transform.GetComponentsInChildren<Renderer>(true))
            .Distinct()
            .ToArray();
        cameraZone.Configure(occluders, new[] { "InnWall_", "CourtyardLintel_", "InnRoof_", "InnFacade_" }, InnCameraProfile());

        var module = moduleObject.AddComponent<SeamlessInteriorModule>();
        module.Configure(
            "albergo_fiorentino",
            "albergo_fiorentino_floor1",
            exteriorThreshold,
            interiorThreshold,
            exteriorFallback,
            interiorFallback,
            portal,
            battleBlocker,
            environment,
            cameraZone);
        portal.Configure(module, -1);

        foreach (Renderer renderer in occluders)
            if (renderer.shadowCastingMode == ShadowCastingMode.Off)
                renderer.shadowCastingMode = ShadowCastingMode.On;
    }

    public static DynamicZoom.CameraOverrideProfile InnCameraProfile()
    {
        return new DynamicZoom.CameraOverrideProfile
        {
            followOffset = new Vector3(0f, 8.2f, -9.2f),
            panOffset = Vector3.zero,
            blendInDuration = 0.7f,
            blendOutDuration = 0.8f,
            priority = 20,
            clampToRoomBounds = false,
        };
    }

    static Transform Anchor(Transform parent, string name, Vector3 position)
    {
        var anchor = new GameObject(name).transform;
        anchor.SetParent(parent, false);
        anchor.localPosition = position;
        return anchor;
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

public static class FlorentineInnSeamlessModuleValidator
{
    [MenuItem("InfernosCurse/Validation/Validate Florentine Inn Seamless Module")]
    public static void Validate()
    {
        var errors = new List<string>();
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            FlorentineInnSeamlessModuleBuilder.ModulePrefabPath);
        if (prefab == null)
        {
            errors.Add("module prefab is missing");
        }
        else
        {
            var module = prefab.GetComponent<SeamlessInteriorModule>();
            if (module == null || module.BuildingId != "albergo_fiorentino" ||
                module.SubLocationId != "albergo_fiorentino_floor1")
                errors.Add("stable module identity is missing or incorrect");
            else if (!module.TryValidateRuntime(out string runtimeError))
                errors.Add(runtimeError);

            if (prefab.GetComponentInChildren<PlayerController>(true) != null)
                errors.Add("module contains a PlayerController");
            if (prefab.GetComponentInChildren<Camera>(true) != null)
                errors.Add("module contains a Camera");
            if (prefab.GetComponentInChildren<ZoneExit>(true) != null)
                errors.Add("module contains a scene-loading ZoneExit");
            if (prefab.GetComponentInChildren<IndoorWeatherLightingGuard>(true) != null)
                errors.Add("module contains a global IndoorWeatherLightingGuard");
            if (prefab.GetComponentsInChildren<Light>(true).Any(light => light.type == LightType.Directional))
                errors.Add("module contains a duplicate directional sun");
            if (prefab.GetComponentInChildren<InnCounterInteraction>(true) == null)
                errors.Add("reception counter interaction is missing");
            if (prefab.GetComponentsInChildren<InnFountainAnimator>(true).Length != 1)
                errors.Add("courtyard fountain animation is missing or duplicated");
            if (prefab.GetComponentsInChildren<CameraOcclusionFader>(true).Length != 0)
                errors.Add("module contains its own camera occlusion authority");

            foreach (string rootName in new[] { "[Architecture]", "[Props]", "[NPCs]", "[Interactions]", "[Lighting]", "[SeamlessInterior]" })
                if (!prefab.GetComponentsInChildren<Transform>(true).Any(transform => transform.name == rootName))
                    errors.Add($"required content root {rootName} is missing");
        }

        foreach (string error in errors)
            Debug.LogError("[FlorentineInnSeamlessModuleValidator] " + error);
        if (errors.Count > 0)
            throw new InvalidOperationException($"Florentine Inn seamless module validation failed with {errors.Count} error(s).");

        Debug.Log("[FlorentineInnSeamlessModuleValidator] Validation passed: stable IDs, protected threshold, preserved inn content, local lighting, and no duplicate player/camera/travel authority.");
    }
}
