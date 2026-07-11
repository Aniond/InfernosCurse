using System;
using System.Collections.Generic;
using UnityEngine;

// APPEND ONLY. Serialized in save slots and Campaign Chronicle effects.
public enum DiscoveryKind
{
    Rumor = 0,
    PointOfInterest = 1,
    Route = 2,
    Location = 3,
}

// Monotonic: a discovery can advance but never return to an earlier stage.
public enum DiscoveryStage
{
    Hidden = 0,
    Rumored = 1,
    Located = 2,
    Discovered = 3,
}

[Serializable]
public sealed class ExplorationDiscoveryRecord
{
    public string discoveryId;
    public DiscoveryKind kind;
    public DiscoveryStage stage;
    public string sourceEventInstanceId;
    public string updatedDateKey;

    public static ExplorationDiscoveryRecord Clone(ExplorationDiscoveryRecord source) =>
        source == null ? null : JsonUtility.FromJson<ExplorationDiscoveryRecord>(JsonUtility.ToJson(source));
}

public static class ExplorationDiscoveryLedger
{
    static readonly Dictionary<string, ExplorationDiscoveryDefinition> Definitions =
        new(StringComparer.Ordinal);
    static readonly Dictionary<string, ExplorationDiscoveryRecord> Records =
        new(StringComparer.Ordinal);
    static bool _resourcesLoaded;

    public static void Reset()
    {
        Records.Clear();
        Definitions.Clear();
        _resourcesLoaded = false;
    }

    public static bool Register(ExplorationDiscoveryDefinition definition, out string error)
    {
        error = null;
        if (definition == null || string.IsNullOrWhiteSpace(definition.discoveryId))
        {
            error = "Discovery definition requires a stable ID.";
            return false;
        }
        if (Definitions.TryGetValue(definition.discoveryId, out var existing) && existing != definition)
        {
            error = $"Discovery ID '{definition.discoveryId}' is registered more than once.";
            return false;
        }
        Definitions[definition.discoveryId] = definition;
        if (!Records.ContainsKey(definition.discoveryId))
        {
            Records[definition.discoveryId] = new ExplorationDiscoveryRecord
            {
                discoveryId = definition.discoveryId,
                kind = definition.kind,
                stage = DiscoveryStage.Hidden,
            };
        }
        return true;
    }

    public static void LoadResourceDefinitions()
    {
        if (_resourcesLoaded) return;
        _resourcesLoaded = true;
        foreach (var definition in Resources.LoadAll<ExplorationDiscoveryDefinition>("Discoveries"))
            if (!Register(definition, out string error)) Debug.LogError("[Discovery] " + error);
    }

    public static DiscoveryStage GetStage(string discoveryId)
    {
        LoadResourceDefinitions();
        return !string.IsNullOrWhiteSpace(discoveryId) && Records.TryGetValue(discoveryId, out var record)
            ? record.stage
            : DiscoveryStage.Hidden;
    }

    public static bool IsVisible(string discoveryId, DiscoveryStage minimumStage)
    {
        if (string.IsNullOrWhiteSpace(discoveryId)) return true;
        return GetStage(discoveryId) >= minimumStage;
    }

    public static bool CanAdvance(string discoveryId, DiscoveryKind kind, DiscoveryStage stage, out string error)
    {
        error = null;
        LoadResourceDefinitions();
        if (string.IsNullOrWhiteSpace(discoveryId) || !Enum.IsDefined(typeof(DiscoveryKind), kind) ||
            !Enum.IsDefined(typeof(DiscoveryStage), stage))
        {
            error = "Discovery advance contains an unknown ID, kind, or stage.";
            return false;
        }
        if (!Records.TryGetValue(discoveryId, out var record))
        {
            error = $"Discovery '{discoveryId}' is not registered.";
            return false;
        }
        if (record.kind != kind)
        {
            error = $"Discovery '{discoveryId}' is {record.kind}, not {kind}.";
            return false;
        }
        return true;
    }

    public static bool Advance(
        string discoveryId,
        DiscoveryKind kind,
        DiscoveryStage stage,
        string sourceEventInstanceId,
        string dateKey,
        out bool changed,
        out string error)
    {
        changed = false;
        if (!CanAdvance(discoveryId, kind, stage, out error)) return false;
        var record = Records[discoveryId];
        if (stage <= record.stage) return true;
        record.stage = stage;
        record.sourceEventInstanceId = sourceEventInstanceId;
        record.updatedDateKey = dateKey;
        changed = true;
        return true;
    }

    public static ExplorationDiscoveryRecord[] Export()
    {
        LoadResourceDefinitions();
        var keys = new List<string>(Records.Keys);
        keys.Sort(StringComparer.Ordinal);
        var result = new ExplorationDiscoveryRecord[keys.Count];
        for (int i = 0; i < keys.Count; i++) result[i] = ExplorationDiscoveryRecord.Clone(Records[keys[i]]);
        return result;
    }

    public static bool Import(ExplorationDiscoveryRecord[] records, out string error)
    {
        if (!TryValidateRecords(records, out error)) return false;
        LoadResourceDefinitions();
        if (records != null)
        {
            foreach (var saved in records)
            {
                if (Records.TryGetValue(saved.discoveryId, out var existing) && existing.kind != saved.kind)
                {
                    error = $"Saved discovery '{saved.discoveryId}' conflicts with its authored kind.";
                    return false;
                }
            }
        }

        Records.Clear();
        foreach (var definition in Definitions.Values)
        {
            Records[definition.discoveryId] = new ExplorationDiscoveryRecord
            {
                discoveryId = definition.discoveryId,
                kind = definition.kind,
                stage = DiscoveryStage.Hidden,
            };
        }
        if (records != null)
            foreach (var saved in records)
                Records[saved.discoveryId] = ExplorationDiscoveryRecord.Clone(saved);
        return true;
    }

    public static bool TryValidateRecords(ExplorationDiscoveryRecord[] records, out string error)
    {
        error = null;
        if (records == null) return true;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < records.Length; i++)
        {
            var record = records[i];
            if (record == null || string.IsNullOrWhiteSpace(record.discoveryId) ||
                !Enum.IsDefined(typeof(DiscoveryKind), record.kind) ||
                !Enum.IsDefined(typeof(DiscoveryStage), record.stage))
            {
                error = $"Discovery record {i} is malformed.";
                return false;
            }
            if (!ids.Add(record.discoveryId))
            {
                error = $"Discovery '{record.discoveryId}' appears more than once.";
                return false;
            }
        }
        return true;
    }
}
