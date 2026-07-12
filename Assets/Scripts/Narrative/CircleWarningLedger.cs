using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class CircleWarningRecord
{
    public string eventInstanceId;
    public string definitionId;
    public string territoryId;
    public CircleId circle;
    public string openedDayKey;
    public int openedDayIndex;
    public int currentStage;
    public int deadlineDayIndex;
    public bool resolved;
    public bool expired;
    public string resolutionEventInstanceId;
    public string expirationEventInstanceId;
    public string lastProcessedDayKey;

    public static CircleWarningRecord Clone(CircleWarningRecord source) =>
        source == null ? null : JsonUtility.FromJson<CircleWarningRecord>(JsonUtility.ToJson(source));
}

/// <summary>
/// Hidden, save-backed warning clock. Definitions supply authored symptoms and
/// consequences; records only hold deterministic state and never expose a meter.
/// </summary>
public static class CircleWarningLedger
{
    const string ResourcePath = "CircleWarnings";

    static readonly List<CircleWarningRecord> Records = new();
    static readonly Dictionary<string, int> ByInstance = new(StringComparer.Ordinal);
    static readonly Dictionary<string, CircleWarningDefinition> KnownDefinitions = new(StringComparer.Ordinal);

    public static int Count => Records.Count;

    public static void Reset()
    {
        Records.Clear();
        ByInstance.Clear();
        KnownDefinitions.Clear();
    }

    public static CircleWarningRecord[] Export()
    {
        var result = new CircleWarningRecord[Records.Count];
        for (int i = 0; i < Records.Count; i++) result[i] = CircleWarningRecord.Clone(Records[i]);
        return result;
    }

    public static bool Import(CircleWarningRecord[] records, out string error)
    {
        if (!TryValidateRecords(records, out error)) return false;
        ReplaceValidated(records);
        return true;
    }

    internal static void ReplaceValidated(CircleWarningRecord[] records)
    {
        Reset();
        foreach (var record in records ?? Array.Empty<CircleWarningRecord>()) Add(CircleWarningRecord.Clone(record));
    }

    public static bool TryValidateRecords(CircleWarningRecord[] records, out string error)
    {
        error = null;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < (records?.Length ?? 0); i++)
        {
            var record = records[i];
            if (record == null || string.IsNullOrWhiteSpace(record.eventInstanceId) ||
                string.IsNullOrWhiteSpace(record.definitionId) ||
                string.IsNullOrWhiteSpace(record.territoryId) ||
                string.IsNullOrWhiteSpace(record.openedDayKey) ||
                !Enum.IsDefined(typeof(CircleId), record.circle) ||
                record.openedDayIndex < 0 || record.deadlineDayIndex <= record.openedDayIndex ||
                record.currentStage < 0 || (record.resolved && record.expired))
            {
                error = $"Circle warning {i} has invalid identity, date, stage, Circle, or terminal state.";
                return false;
            }
            if (record.resolved && string.IsNullOrWhiteSpace(record.resolutionEventInstanceId))
            {
                error = $"Resolved warning '{record.eventInstanceId}' has no response event.";
                return false;
            }
            if (record.expired && string.IsNullOrWhiteSpace(record.expirationEventInstanceId))
            {
                error = $"Expired warning '{record.eventInstanceId}' has no expiration event.";
                return false;
            }
            if (!ids.Add(record.eventInstanceId))
            {
                error = $"Circle warning '{record.eventInstanceId}' is duplicated.";
                return false;
            }
        }
        return true;
    }

    public static bool TryGet(string eventInstanceId, out CircleWarningRecord record)
    {
        if (ByInstance.TryGetValue(eventInstanceId ?? string.Empty, out int index))
        {
            record = CircleWarningRecord.Clone(Records[index]);
            return true;
        }
        record = null;
        return false;
    }

    public static bool ProcessDay(
        string dayKey,
        int dayIndex,
        HubMap hub,
        out string error,
        IEnumerable<CircleWarningDefinition> definitions = null)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(dayKey) || dayIndex < 0 || hub == null)
        {
            error = "Circle warning processing requires a stable date and HubMap.";
            return false;
        }

        if (!BuildDefinitionIndex(definitions, out var orderedDefinitions, out error)) return false;
        foreach (var record in Records)
        {
            if (record.resolved || record.expired) continue;
            if (!KnownDefinitions.ContainsKey(record.definitionId))
            {
                error = $"Active warning '{record.eventInstanceId}' is missing definition '{record.definitionId}'.";
                return false;
            }
        }

        foreach (var record in Records)
        {
            if (record.resolved || record.expired ||
                string.Equals(record.lastProcessedDayKey, dayKey, StringComparison.Ordinal))
                continue;

            var definition = KnownDefinitions[record.definitionId];
            if (dayIndex >= record.deadlineDayIndex)
            {
                string expirationInstanceId = record.eventInstanceId + ":expiration";
                var context = new WorldEventContext
                {
                    eventInstanceId = expirationInstanceId,
                    gameDateKey = dayKey,
                    locationId = record.territoryId,
                };
                if (!WorldEventLedger.TryCommitChoice(
                        definition.expirationEvent,
                        definition.expirationChoiceId,
                        context,
                        out _,
                        out error))
                {
                    error = $"Warning '{record.eventInstanceId}' expiration was not committed: {error}";
                    return false;
                }
                record.expired = true;
                record.expirationEventInstanceId = expirationInstanceId;
                record.lastProcessedDayKey = dayKey;
                continue;
            }

            int elapsedDays = Math.Max(0, dayIndex - record.openedDayIndex);
            int desiredStage = 0;
            var schedule = definition.stageSchedule ?? Array.Empty<CircleWarningStageDefinition>();
            for (int i = 0; i < schedule.Length; i++)
                if (elapsedDays >= schedule[i].dayOffset) desiredStage = i + 1;
            record.currentStage = Math.Max(record.currentStage, desiredStage);
            record.lastProcessedDayKey = dayKey;
        }

        var majorOpenedTerritories = new HashSet<string>(StringComparer.Ordinal);
        foreach (var record in Records)
            if (string.Equals(record.openedDayKey, dayKey, StringComparison.Ordinal) &&
                KnownDefinitions.TryGetValue(record.definitionId, out var existingDefinition) &&
                existingDefinition.IsMajor)
                majorOpenedTerritories.Add(record.territoryId);

        foreach (var definition in orderedDefinitions)
        {
            HubNode owner = hub.ResolveInfluenceTerritory(definition.territoryId);
            if (owner == null || !owner.IsInfluenceOwner)
            {
                error = $"Warning '{definition.warningId}' territory '{definition.territoryId}' has no influence owner.";
                return false;
            }
            if (HasDefinitionRecord(definition.warningId, owner.id)) continue;
            if (definition.IsMajor && majorOpenedTerritories.Contains(owner.id)) continue;
            if (!PrerequisitesMet(definition)) continue;
            if (owner.GetInfluence(definition.circle) < definition.opensAtInfluence) continue;

            var record = new CircleWarningRecord
            {
                eventInstanceId = $"warning:{definition.warningId}:{owner.id}:{dayKey}",
                definitionId = definition.warningId,
                territoryId = owner.id,
                circle = definition.circle,
                openedDayKey = dayKey,
                openedDayIndex = dayIndex,
                currentStage = 0,
                deadlineDayIndex = checked(dayIndex + definition.deadlineDays),
                lastProcessedDayKey = dayKey,
            };
            Add(record);
            if (definition.IsMajor) majorOpenedTerritories.Add(owner.id);
        }
        return true;
    }

    public static void NotifyWorldEvent(WorldEventRecord worldEvent)
    {
        if (worldEvent == null || string.IsNullOrWhiteSpace(worldEvent.eventInstanceId)) return;
        if (KnownDefinitions.Count == 0)
            BuildDefinitionIndex(null, out _, out _);

        foreach (var record in Records)
        {
            if (record.resolved || record.expired ||
                !KnownDefinitions.TryGetValue(record.definitionId, out var definition))
                continue;
            foreach (string responseId in definition.responseEventIds ?? Array.Empty<string>())
            {
                if (!string.Equals(responseId, worldEvent.eventInstanceId, StringComparison.Ordinal) &&
                    !string.Equals(responseId, worldEvent.eventTypeId, StringComparison.Ordinal))
                    continue;
                record.resolved = true;
                record.resolutionEventInstanceId = worldEvent.eventInstanceId;
                break;
            }
        }
    }

    public static string[] BuildCanonicalFacts()
    {
        var facts = new List<string>();
        if (KnownDefinitions.Count == 0) BuildDefinitionIndex(null, out _, out _);
        foreach (var record in Records)
        {
            string state = record.resolved ? "resolved" : record.expired ? "expired" : "active";
            facts.Add($"circle_warning:{record.definitionId}:{record.territoryId}:{state}");
            if (!record.resolved && !record.expired &&
                KnownDefinitions.TryGetValue(record.definitionId, out var definition))
            {
                string textId = definition.openingSymptomTextId;
                int stageIndex = record.currentStage - 1;
                if (stageIndex >= 0 && stageIndex < (definition.stageSchedule?.Length ?? 0))
                    textId = definition.stageSchedule[stageIndex].symptomTextId;
                facts.Add($"circle_warning_symptom:{textId}");
            }
        }
        return facts.ToArray();
    }

    static bool BuildDefinitionIndex(
        IEnumerable<CircleWarningDefinition> definitions,
        out List<CircleWarningDefinition> ordered,
        out string error)
    {
        error = null;
        ordered = new List<CircleWarningDefinition>();
        KnownDefinitions.Clear();
        if (definitions == null) definitions = Resources.LoadAll<CircleWarningDefinition>(ResourcePath);
        foreach (var definition in definitions)
        {
            if (definition == null || !definition.TryValidate(out error)) return false;
            if (!KnownDefinitions.TryAdd(definition.warningId, definition))
            {
                error = $"Circle warning definition '{definition.warningId}' is duplicated.";
                return false;
            }
            ordered.Add(definition);
        }
        ordered.Sort((left, right) => string.CompareOrdinal(left.warningId, right.warningId));
        return true;
    }

    static bool HasDefinitionRecord(string definitionId, string territoryId)
    {
        foreach (var record in Records)
            if (string.Equals(record.definitionId, definitionId, StringComparison.Ordinal) &&
                string.Equals(record.territoryId, territoryId, StringComparison.Ordinal))
                return true;
        return false;
    }

    static bool PrerequisitesMet(CircleWarningDefinition definition)
    {
        foreach (string prerequisite in definition.prerequisiteEventIds ?? Array.Empty<string>())
            if (!WorldEventLedger.Contains(prerequisite)) return false;
        return true;
    }

    static void Add(CircleWarningRecord record)
    {
        ByInstance.Add(record.eventInstanceId, Records.Count);
        Records.Add(record);
    }
}

public static class CircleNarrativeState
{
    public static bool TryImport(
        CircleWarningRecord[] warnings,
        SiteOutcomeRecord[] siteOutcomes,
        out string error)
    {
        if (!CircleWarningLedger.TryValidateRecords(warnings, out error) ||
            !SiteOutcomeState.TryValidateRecords(siteOutcomes, out error))
            return false;
        CircleWarningLedger.ReplaceValidated(warnings);
        SiteOutcomeState.ReplaceValidated(siteOutcomes);
        return true;
    }
}
