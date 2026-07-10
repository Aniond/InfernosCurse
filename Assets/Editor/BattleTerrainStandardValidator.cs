using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class BattleTerrainStandardValidator
{
    const string MapsFolder = "Assets/Prefabs/Battle/Maps";
    const string ShaderName = "InfernosCurse/BattleTerrainSplat";

    static readonly string[] RequiredProperties =
    {
        "_GrassTex", "_DirtTex", "_RockTex", "_PathTex",
        "_BlendSoftness", "_MacroScale", "_MacroStrength",
        "_ExposedTint", "_RecessTint", "_FoldStrength",
        "_ElevationTintStrength", "_AmbientBoost", "_HeightMinMax",
        "_WetDarkening", "_WetHighlight"
    };

    [MenuItem("InfernosCurse/Battle Terrain/Validate Shared Standard")]
    public static void ValidateAll()
    {
        var errors = new List<string>();
        var outputs = new HashSet<string>();

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { MapsFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null || !prefab.name.EndsWith("3D")) continue;
            if (prefab.GetComponent<BattleTerrainHeights>() == null) continue;

            string baseName = prefab.name.Substring(0, prefab.name.Length - 2);
            outputs.Add(baseName);
            string stylePath = $"{MapsFolder}/Styles/{baseName}_Style3D.asset";
            var style = AssetDatabase.LoadAssetAtPath<BattleMapStyle3D>(stylePath);
            if (style == null)
                errors.Add($"{prefab.name}: missing style profile {stylePath}");
            else if (style.grassTex == null || style.dirtTex == null || style.rockTex == null)
                errors.Add($"{prefab.name}: style is missing a required terrain texture");

            var renderer = prefab.GetComponentInChildren<MeshRenderer>(true);
            var material = renderer != null ? renderer.sharedMaterial : null;
            if (material == null || material.shader == null || material.shader.name != ShaderName)
            {
                errors.Add($"{prefab.name}: terrain does not use {ShaderName}");
                continue;
            }

            foreach (string property in RequiredProperties)
                if (!material.HasProperty(property))
                    errors.Add($"{prefab.name}: terrain material is missing {property}");

            if (prefab.GetComponent<BattleTerrainFog>() == null)
                errors.Add($"{prefab.name}: missing BattleTerrainFog");
            if (prefab.GetComponent<ZoneFogOfWar>() == null)
                errors.Add($"{prefab.name}: missing ZoneFogOfWar");
        }

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { MapsFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null || prefab.name.EndsWith("3D") ||
                prefab.GetComponent<BattleMapAuthoring>() == null) continue;
            if (!outputs.Contains(prefab.name))
                Debug.Log($"[BattleTerrainStandard] Migration candidate: {prefab.name} has no approved 3D output.");
        }

        var features = AssetDatabase.LoadAssetAtPath<GameFeatureSettings>("Assets/Resources/GameFeatureSettings.asset");
        if (features == null) errors.Add("Missing Assets/Resources/GameFeatureSettings.asset");
        else if (features.corruptionEnabled) errors.Add("Corruption must remain disabled during this rollout");

        if (errors.Count > 0)
        {
            foreach (string error in errors) Debug.LogError("[BattleTerrainStandard] " + error);
            Debug.LogError($"[BattleTerrainStandard] Validation failed with {errors.Count} error(s).");
            return;
        }

        Debug.Log($"[BattleTerrainStandard] Validation passed for {outputs.Count} approved 3D map(s); corruption disabled, tactical fog retained.");
    }
}
