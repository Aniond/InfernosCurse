using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Adds ExteriorWindowFacade only to building renderers that expose usable
/// facade bounds. Imported models and their shared materials are never edited.
/// </summary>
public static class BuildingWindowEnvironmentInstaller
{
    const string MenuPath = "InfernosCurse/Environment/Apply World Windows To Existing Buildings";

    [MenuItem(MenuPath)]
    public static void ApplyToExistingBuildingScenes()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        string activePath = SceneManager.GetActiveScene().path;
        int sceneCount = 0;
        int facadeCount = 0;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (EditorBuildSettingsScene entry in EditorBuildSettings.scenes)
        {
            if (!entry.enabled || string.IsNullOrEmpty(entry.path) || !visited.Add(entry.path)) continue;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(entry.path) == null) continue;

            Scene scene = EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Single);
            int added = ApplyToScene(scene);
            if (added <= 0) continue;
            EditorSceneManager.SaveScene(scene);
            sceneCount++;
            facadeCount += added;
        }

        if (!string.IsNullOrEmpty(activePath) && AssetDatabase.LoadAssetAtPath<SceneAsset>(activePath) != null)
            EditorSceneManager.OpenScene(activePath, OpenSceneMode.Single);

        Debug.Log($"[BuildingWindowEnvironmentInstaller] Updated {facadeCount} building facades across {sceneCount} scenes.");
    }

    public static int ApplyToScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return 0;
        int changed = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (ExteriorWindowFacade existing in root.GetComponentsInChildren<ExteriorWindowFacade>(true))
            {
                Renderer renderer = existing.TargetRenderer;
                if (renderer != null && IsUsableBuildingRenderer(scene.name, renderer)) continue;
                WorldWindowEnvironment environment = existing.GetComponent<WorldWindowEnvironment>();
                UnityEngine.Object.DestroyImmediate(existing);
                if (environment != null) UnityEngine.Object.DestroyImmediate(environment);
                changed++;
            }
        }

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!IsUsableBuildingRenderer(scene.name, renderer)) continue;
                var facade = renderer.GetComponent<ExteriorWindowFacade>();
                if (facade == null)
                {
                    facade = renderer.gameObject.AddComponent<ExteriorWindowFacade>();
                }
                facade.TargetRenderer = renderer;
                ConfigureDensity(facade, renderer.bounds, scene.name, renderer.name);
                EditorUtility.SetDirty(facade);
                changed++;
            }
        }
        if (scene.name == "PonteVecchio") changed += ApplyPonteWindowGaps(scene);
        if (changed > 0) EditorSceneManager.MarkSceneDirty(scene);
        return changed;
    }

    static bool IsUsableBuildingRenderer(string sceneName, Renderer renderer)
    {
        if (renderer == null || renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer)
            return false;
        string name = renderer.name;
        if (name.IndexOf("LOD1", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("LOD2", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("LOD3", StringComparison.OrdinalIgnoreCase) >= 0)
            return false;

        Bounds bounds = renderer.bounds;
        if (bounds.size.y < 2.2f || Mathf.Max(bounds.size.x, bounds.size.z) < 2.2f) return false;

        if (sceneName == "PonteVecchio" && name.IndexOf("windowed", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        if (name.StartsWith("Building_", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Townhouse", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Church_N_", StringComparison.OrdinalIgnoreCase))
            return true;

        UnityEngine.Object source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(renderer.gameObject);
        string sourcePath = source != null ? AssetDatabase.GetAssetPath(source) : string.Empty;
        if (sourcePath.IndexOf("/MarketSquare/Buildings/", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        if (sourcePath.EndsWith("gardeners-cottage.glb", StringComparison.OrdinalIgnoreCase)) return true;
        if (sourcePath.IndexOf("/Slavic World Free/Prefabs/", StringComparison.OrdinalIgnoreCase) >= 0 &&
            name.IndexOf("Wall_Front", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }

    static int ApplyPonteWindowGaps(Scene scene)
    {
        int changed = 0;
        Material material = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Environment/FlorentineInnFloor1/Materials/Inn_WindowGlow.mat");
        if (material == null) return 0;

        foreach (GameObject sceneRoot in scene.GetRootGameObjects())
        {
            foreach (Transform wall in sceneRoot.GetComponentsInChildren<Transform>(true))
            {
                if (wall.name.IndexOf("windowed", StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (wall.Find("[World Window Gaps]") != null) continue;

                Renderer lower = null;
                Renderer upper = null;
                var piers = new List<Renderer>();
                foreach (Renderer renderer in wall.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer.name.IndexOf("Band_Lower", StringComparison.OrdinalIgnoreCase) >= 0) lower = renderer;
                    else if (renderer.name.IndexOf("Band_Upper", StringComparison.OrdinalIgnoreCase) >= 0) upper = renderer;
                    else if (renderer.name.StartsWith("Pier_", StringComparison.OrdinalIgnoreCase)) piers.Add(renderer);
                }
                if (lower == null || upper == null || piers.Count == 0) continue;

                var root = new GameObject("[World Window Gaps]");
                root.transform.SetParent(wall, true);
                var surfaces = new List<WorldWindowEnvironment.WindowSurface>();
                piers.Sort((a, b) => a.bounds.min.x.CompareTo(b.bounds.min.x));

                float cursor = lower.bounds.min.x;
                float bottom = lower.bounds.max.y;
                float top = upper.bounds.min.y;
                float height = Mathf.Max(0.65f, top - bottom);
                for (int i = 0; i <= piers.Count; i++)
                {
                    float gapEnd = i < piers.Count ? piers[i].bounds.min.x : lower.bounds.max.x;
                    float width = gapEnd - cursor;
                    if (width > 0.75f)
                    {
                        var pane = GameObject.CreatePrimitive(PrimitiveType.Quad);
                        pane.name = $"PonteWindow_{surfaces.Count + 1:00}";
                        pane.transform.SetParent(root.transform, true);
                        pane.transform.position = new Vector3((cursor + gapEnd) * 0.5f, bottom + height * 0.5f,
                            lower.bounds.min.z - 0.025f);
                        pane.transform.rotation = Quaternion.identity;
                        pane.transform.localScale = new Vector3(width * 0.82f, height * 0.86f, 1f);
                        UnityEngine.Object.DestroyImmediate(pane.GetComponent<Collider>());
                        Renderer paneRenderer = pane.GetComponent<Renderer>();
                        paneRenderer.sharedMaterial = material;
                        paneRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        paneRenderer.receiveShadows = false;
                        surfaces.Add(new WorldWindowEnvironment.WindowSurface
                        {
                            renderer = paneRenderer,
                            role = WorldWindowEnvironment.WindowRole.ExteriorOccupied,
                            emissionTint = new Color(1f, 0.64f, 0.30f, 1f),
                            emissionMultiplier = 1.5f,
                            lightningMultiplier = 2.5f
                        });
                    }
                    if (i < piers.Count) cursor = Mathf.Max(cursor, piers[i].bounds.max.x);
                }

                if (surfaces.Count == 0)
                {
                    UnityEngine.Object.DestroyImmediate(root);
                    continue;
                }
                var environment = root.AddComponent<WorldWindowEnvironment>();
                environment.Windows = surfaces.ToArray();
                changed++;
            }
        }
        return changed;
    }

    static void ConfigureDensity(ExteriorWindowFacade facade, Bounds bounds, string sceneName, string rendererName)
    {
        if (sceneName == "PonteVecchio" && rendererName.IndexOf("windowed", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            facade.Columns = 6;
            facade.Rows = 1;
            return;
        }

        float width = Mathf.Max(bounds.size.x, bounds.size.z);
        facade.Columns = Mathf.Clamp(Mathf.RoundToInt(width / 3.5f), 2, 5);
        facade.Rows = bounds.size.y > 7.5f ? 2 : 1;
        facade.EmissionMultiplier = 0.25f;
    }
}
