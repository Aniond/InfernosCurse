using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class CircleInfluenceValidator
{
    [MenuItem("InfernosCurse/Validation/Validate Circle Influence Foundation")]
    public static void Validate()
    {
        var errors = new List<string>();
        ValidateLedger(errors);
        ValidateIndependentCircles(errors);
        ValidateSaveShape(errors);
        ValidateBleedMath(errors);
        ValidateSourceFreeExploration(errors);

        if (errors.Count > 0)
        {
            foreach (string error in errors) Debug.LogError("[CircleInfluenceValidator] " + error);
            throw new InvalidOperationException($"Circle influence validation failed with {errors.Count} error(s).");
        }

        Debug.Log("[CircleInfluenceValidator] Validation passed: independent ledgers, versioned save shape, bounded bleed, and source-free exploration.");
    }

    static void ValidateLedger(List<string> errors)
    {
        var states = new List<CircleInfluenceState>();
        CircleInfluenceLedger.Set(states, CircleId.Limbo, 0.25f);
        CircleInfluenceLedger.Add(states, CircleId.Limbo, 0.10f);
        ExpectNear(CircleInfluenceLedger.Get(states, CircleId.Limbo), 0.35f, "Limbo add", errors);
        CircleInfluenceLedger.Set(states, CircleId.Limbo, 2f);
        ExpectNear(CircleInfluenceLedger.Get(states, CircleId.Limbo), 1f, "upper clamp", errors);
        CircleInfluenceLedger.Set(states, CircleId.Limbo, 0f);
        ExpectNear(CircleInfluenceLedger.Get(states, CircleId.Limbo), 0f, "zero removal", errors);
        if (states.Count != 0) errors.Add("Zero-valued ledger entries were not removed.");
    }

    static void ValidateIndependentCircles(List<string> errors)
    {
        var node = new HubNode { nativeCircle = CircleId.Limbo, ownsCircleState = true };
        node.influenceTerritory = node;
        node.SetInfluence(CircleId.Limbo, 0.34f);
        node.SetInfluence(CircleId.Greed, 0.61f);
        ExpectNear(node.GetInfluence(CircleId.Limbo), 0.34f, "independent Limbo value", errors);
        ExpectNear(node.GetInfluence(CircleId.Greed), 0.61f, "independent Greed value", errors);
        if (node.DominantCircle != CircleId.Greed) errors.Add("Dominant Circle did not select the highest entry.");
        ExpectNear(node.curseLevel, 0.34f, "legacy curseLevel maps to Limbo", errors);
    }

    static void ValidateSaveShape(List<string> errors)
    {
        var data = new SaveData
        {
            saveVersion = SaveSystem.CURRENT_VERSION,
            influenceLocationIds = new[] { "firenze", "firenze" },
            influenceCircleIds = new[] { (int)CircleId.Limbo, (int)CircleId.Greed },
            influenceValues = new[] { 0.34f, 0.07f },
        };
        string json = JsonUtility.ToJson(data);
        var restored = JsonUtility.FromJson<SaveData>(json);
        if (restored == null || restored.influenceLocationIds == null ||
            restored.influenceLocationIds.Length != 2 || restored.influenceCircleIds.Length != 2 ||
            restored.influenceValues.Length != 2)
            errors.Add("v3 Circle save table did not round-trip through JsonUtility.");
        Expect(SaveSystem.CURRENT_VERSION >= 6,
            "SaveSystem version was not advanced for owner-territory Circle influence.", errors);
    }

    static void ValidateBleedMath(List<string> errors)
    {
        var definition = ScriptableObject.CreateInstance<CurseDefinition>();
        definition.bleedThreshold = 0.70f;
        definition.maxDailyBleed = 0.012f;
        definition.sanctityResistance = 0.70f;
        ExpectNear(CirclePropagationMath.CalculateBleed(definition, 0.69f, 0f, 0f, 1f), 0f, "below-threshold bleed", errors);
        ExpectNear(CirclePropagationMath.CalculateBleed(definition, 0.85f, 0f, 0f, 1f), 0.006f, "85 percent bleed", errors);
        ExpectNear(CirclePropagationMath.CalculateBleed(definition, 1f, 0f, 0f, 1f), 0.012f, "100 percent bleed", errors);
        ExpectNear(CirclePropagationMath.CalculateBleed(definition, 1f, 1f, 0f, 1f), 0f, "zero gradient", errors);
        ExpectNear(CirclePropagationMath.CalculateBleed(definition, 1f, 0f, 1f, 1f), 0.0036f, "sanctity resistance", errors);
        ExpectNear(CirclePropagationMath.CalculateBleed(definition, 1f, 0f, 0f, 0.5f), 0.006f, "weak route", errors);
        UnityEngine.Object.DestroyImmediate(definition);
    }

    static void ValidateSourceFreeExploration(List<string> errors)
    {
        const float start = 0.10f;
        var definition = ScriptableObject.CreateInstance<CurseDefinition>();
        float afterThirtyDays = start;
        for (int i = 0; i < 30; i++)
            afterThirtyDays += CirclePropagationMath.CalculateBleed(definition, afterThirtyDays, afterThirtyDays, 0f, 1f);
        ExpectNear(afterThirtyDays, start, "source-free 30-day exploration", errors);
        UnityEngine.Object.DestroyImmediate(definition);
    }

    static void ExpectNear(float actual, float expected, string label, List<string> errors)
    {
        if (Mathf.Abs(actual - expected) > 0.0001f)
            errors.Add($"{label}: expected {expected:F4}, got {actual:F4}.");
    }

    static void Expect(bool condition, string message, List<string> errors)
    {
        if (!condition) errors.Add(message);
    }
}
