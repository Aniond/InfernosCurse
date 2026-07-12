using UnityEngine;

public static class CirclePropagationMath
{
    public static float CalculateBleed(
        CurseDefinition definition,
        float sourceInfluence,
        float targetInfluence,
        float targetSanctity,
        float routeStrength)
    {
        if (definition == null) return 0f;
        return CalculateBleed(
            definition.bleedThreshold,
            definition.maxDailyBleed,
            definition.sanctityResistance,
            sourceInfluence,
            targetInfluence,
            targetSanctity,
            routeStrength);
    }

    public static float CalculateBleed(
        float bleedThreshold,
        float maxDailyBleed,
        float sanctityResistance,
        float sourceInfluence,
        float targetInfluence,
        float targetSanctity,
        float routeStrength)
    {
        float threshold = Mathf.Clamp01(bleedThreshold);
        float source = Mathf.Clamp01(sourceInfluence);
        if (source <= threshold || maxDailyBleed <= 0f) return 0f;

        float pressure = Mathf.InverseLerp(threshold, 1f, source);
        float gradient = Mathf.Clamp01((source - Mathf.Clamp01(targetInfluence)) / 0.25f);
        float block = 1f - Mathf.Clamp01(targetSanctity) * Mathf.Clamp01(sanctityResistance);
        return Mathf.Max(0f, maxDailyBleed) * pressure * gradient *
               Mathf.Clamp01(routeStrength) * Mathf.Clamp01(block);
    }

    public static float AddWithDailyCap(float existingIncoming, float delta, float dailyCap)
    {
        return Mathf.Min(Mathf.Max(0f, dailyCap),
            Mathf.Max(0f, existingIncoming) + Mathf.Max(0f, delta));
    }
}
