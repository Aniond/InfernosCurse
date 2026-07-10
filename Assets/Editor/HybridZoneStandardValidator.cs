using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class HybridZoneStandardValidator
{
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
