using System;
using System.Collections.Generic;
using UnityEngine;

public static class FlorenceOpeningBaseline
{
    // Explicit New Game seeds mutable owner territories only. Florence sites
    // resolve to the one citywide Firenze ledger; Tuscany is derived read-only.
    public static readonly IReadOnlyDictionary<string, float> LimboByLocation =
        new Dictionary<string, float>(StringComparer.Ordinal)
        {
            ["firenze"] = 0.08f,
            ["fiesole"] = 0.05f,
        };

    public static bool Apply(out string error)
    {
        error = null;
        var hub = HubMap.Instance;
        if (hub == null)
        {
            error = "HubMap is unavailable for the Florence New Game baseline.";
            return false;
        }
        hub.EnsureGraphBuilt();
        foreach (var pair in LimboByLocation)
        {
            var node = hub.GetNode(pair.Key);
            if (node == null || !node.IsInfluenceOwner)
            {
                error = $"Florence baseline owner '{pair.Key}' is missing or does not own Circle state.";
                return false;
            }
        }
        foreach (var pair in LimboByLocation)
            hub.ApplyLedgerBaseline(pair.Key, CircleId.Limbo, pair.Value, clearOtherCircles: true);
        Debug.Log("[FlorenceOpeningBaseline] Applied owner-only Florence and Fiesole Limbo baselines.");
        return true;
    }
}
