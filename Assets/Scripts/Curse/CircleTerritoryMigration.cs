using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class CircleTerritoryMigration
{
    public const string FlorenceOwnerId = "firenze";
    public const string TuscanyRegionId = "toscana";

    // Fixed migration weights copied from the final legacy Florence authoring.
    // They do not read mutable runtime population values.
    public static readonly IReadOnlyDictionary<string, float> FlorenceLegacyWeights =
        new Dictionary<string, float>(StringComparer.Ordinal)
        {
            ["duomo"] = 0.60f,
            ["novella"] = 0.50f,
            ["mercato"] = 0.95f,
            ["signoria"] = 0.70f,
            ["pontevecchio"] = 0.60f,
            ["oltrarno"] = 0.50f,
            ["santacroce"] = 0.50f,
            ["sanlorenzo"] = 0.65f,
            ["giardino_rose"] = 0.15f,
            ["salone_arti"] = 0.70f,
            ["via_calimala"] = 0.80f,
        };

    public static bool TryValidateInput(
        string[] locationIds,
        int[] circleIds,
        float[] values,
        out string error)
    {
        error = null;
        if (locationIds == null || circleIds == null || values == null)
        {
            error = "Circle influence arrays must all be present.";
            return false;
        }
        if (locationIds.Length != circleIds.Length || locationIds.Length != values.Length)
        {
            error = "Circle influence arrays have different lengths.";
            return false;
        }
        for (int i = 0; i < locationIds.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(locationIds[i]))
            {
                error = $"Circle influence location {i} is empty.";
                return false;
            }
            if (!Enum.IsDefined(typeof(CircleId), circleIds[i]))
            {
                error = $"Circle influence entry {i} has invalid Circle ID {circleIds[i]}.";
                return false;
            }
            if (float.IsNaN(values[i]) || float.IsInfinity(values[i]))
            {
                error = $"Circle influence entry {i} has a non-finite value.";
                return false;
            }
        }
        return true;
    }

    public static bool TryMigrateToOwners(
        HubMap hub,
        string[] locationIds,
        int[] circleIds,
        float[] values,
        out string[] ownerLocationIds,
        out int[] ownerCircleIds,
        out float[] ownerValues,
        out string error)
    {
        ownerLocationIds = Array.Empty<string>();
        ownerCircleIds = Array.Empty<int>();
        ownerValues = Array.Empty<float>();
        error = null;

        if (hub == null)
        {
            error = "HubMap is unavailable for Circle territory migration.";
            return false;
        }
        if (!TryValidateInput(locationIds, circleIds, values, out error)) return false;

        hub.EnsureGraphBuilt();
        var direct = new Dictionary<(string owner, CircleId circle), float>();
        var florenceWeighted = new Dictionary<CircleId, (float total, float weight)>();
        var florenceFallback = new Dictionary<CircleId, float>();

        for (int i = 0; i < locationIds.Length; i++)
        {
            string locationId = locationIds[i];
            CircleId circle = (CircleId)circleIds[i];
            float value = Mathf.Clamp01(values[i]);

            if (FlorenceLegacyWeights.TryGetValue(locationId, out float weight))
            {
                florenceWeighted.TryGetValue(circle, out var aggregate);
                aggregate.total += value * weight;
                aggregate.weight += weight;
                florenceWeighted[circle] = aggregate;
                continue;
            }

            if (locationId == FlorenceOwnerId)
            {
                florenceFallback[circle] = value;
                continue;
            }

            string ownerId = hub.ResolveInfluenceTerritoryId(locationId);
            if (string.IsNullOrEmpty(ownerId)) continue;
            direct[(ownerId, circle)] = value;
        }

        var florenceCircles = new HashSet<CircleId>(florenceFallback.Keys);
        florenceCircles.UnionWith(florenceWeighted.Keys);
        foreach (CircleId circle in florenceCircles)
        {
            if (florenceWeighted.TryGetValue(circle, out var aggregate) && aggregate.weight > 0f)
                direct[(FlorenceOwnerId, circle)] = Mathf.Clamp01(aggregate.total / aggregate.weight);
            else if (florenceFallback.TryGetValue(circle, out float fallback))
                direct[(FlorenceOwnerId, circle)] = fallback;
        }

        var ordered = direct
            .OrderBy(pair => pair.Key.owner, StringComparer.Ordinal)
            .ThenBy(pair => (int)pair.Key.circle)
            .ToArray();
        ownerLocationIds = new string[ordered.Length];
        ownerCircleIds = new int[ordered.Length];
        ownerValues = new float[ordered.Length];
        for (int i = 0; i < ordered.Length; i++)
        {
            ownerLocationIds[i] = ordered[i].Key.owner;
            ownerCircleIds[i] = (int)ordered[i].Key.circle;
            ownerValues[i] = Mathf.Clamp01(ordered[i].Value);
        }
        return true;
    }
}
