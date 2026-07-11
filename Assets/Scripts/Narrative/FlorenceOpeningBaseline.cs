using System;
using System.Collections.Generic;
using UnityEngine;

public static class FlorenceOpeningBaseline
{
    // Stable Florence districts begin at 5-10%; Mercato is the earliest
    // troubled district at 15%. This table is applied only by explicit New Game.
    public static readonly IReadOnlyDictionary<string, float> LimboByLocation =
        new Dictionary<string, float>(StringComparer.Ordinal)
        {
            ["duomo"] = 0.05f,
            ["novella"] = 0.05f,
            ["mercato"] = 0.15f,
            ["signoria"] = 0.08f,
            ["pontevecchio"] = 0.10f,
            ["oltrarno"] = 0.10f,
            ["santacroce"] = 0.07f,
            ["sanlorenzo"] = 0.07f,
            ["firenze"] = 0.08f,
            ["wp_mugnone"] = 0f,
            ["fiesole"] = 0.05f,
            ["toscana"] = 0.02f,
            ["giardino_rose"] = 0.06f,
            ["salone_arti"] = 0.08f,
            ["via_calimala"] = 0.10f,
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
            if (hub.GetNode(pair.Key) == null)
            {
                error = $"Florence baseline location '{pair.Key}' is missing from HubMap.";
                return false;
            }
        }
        foreach (var pair in LimboByLocation)
            hub.ApplyLedgerBaseline(pair.Key, CircleId.Limbo, pair.Value, clearOtherCircles: true);
        Debug.Log("[FlorenceOpeningBaseline] Applied low-burn Limbo opening values for a new campaign.");
        return true;
    }
}
