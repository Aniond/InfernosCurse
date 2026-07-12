using System;
using System.Collections.Generic;
using UnityEngine;

// APPEND ONLY. Serialized in save data.
public enum GugolNpcKnowledgeSource
{
    DirectSighting = 0,
    Conversation = 1,
    Rumor = 2,
    UsualLocation = 3,
}

// APPEND ONLY. Broad in-world time is useful without exposing a technical clock.
public enum GugolTimeBand
{
    Unknown = 0,
    Morning = 1,
    Afternoon = 2,
    Evening = 3,
    Night = 4,
}

[Serializable]
public sealed class GugolNpcMapKnowledgeRecord
{
    public string npcId;
    public string streetId;
    public string venueId;
    public string observedDayKey;
    public GugolTimeBand observedTimeBand;
    public GugolNpcKnowledgeSource source;
    public string sourceId;

    public static GugolNpcMapKnowledgeRecord Clone(GugolNpcMapKnowledgeRecord sourceRecord) =>
        sourceRecord == null
            ? null
            : JsonUtility.FromJson<GugolNpcMapKnowledgeRecord>(JsonUtility.ToJson(sourceRecord));
}

/// <summary>
/// Player-owned map knowledge. It records explicit sightings and authored
/// information; it never queries live NPC transforms or simulation positions.
/// </summary>
public static class GugolNpcMapKnowledgeLedger
{
    static readonly Dictionary<string, GugolNpcMapKnowledgeRecord> Records =
        new(StringComparer.Ordinal);

    public static event Action OnChanged;

    public static int Count => Records.Count;

    public static void Reset()
    {
        if (Records.Count == 0) return;
        Records.Clear();
        OnChanged?.Invoke();
    }

    public static bool TryGet(string npcId, out GugolNpcMapKnowledgeRecord record)
    {
        if (!string.IsNullOrWhiteSpace(npcId) && Records.TryGetValue(npcId, out var found))
        {
            record = GugolNpcMapKnowledgeRecord.Clone(found);
            return true;
        }
        record = null;
        return false;
    }

    public static bool Record(
        string npcId,
        string streetId,
        string venueId,
        string observedDayKey,
        GugolTimeBand observedTimeBand,
        GugolNpcKnowledgeSource source,
        string sourceId,
        out bool changed,
        out string error)
    {
        var candidate = new GugolNpcMapKnowledgeRecord
        {
            npcId = npcId,
            streetId = streetId,
            venueId = venueId,
            observedDayKey = observedDayKey,
            observedTimeBand = observedTimeBand,
            source = source,
            sourceId = sourceId,
        };
        if (!TryValidateRecord(candidate, 0, out error))
        {
            changed = false;
            return false;
        }

        changed = !Records.TryGetValue(npcId, out var current) || !Same(current, candidate);
        if (!changed) return true;
        Records[npcId] = candidate;
        OnChanged?.Invoke();
        return true;
    }

    public static GugolNpcMapKnowledgeRecord[] Export()
    {
        var ids = new List<string>(Records.Keys);
        ids.Sort(StringComparer.Ordinal);
        var result = new GugolNpcMapKnowledgeRecord[ids.Count];
        for (int i = 0; i < ids.Count; i++) result[i] = GugolNpcMapKnowledgeRecord.Clone(Records[ids[i]]);
        return result;
    }

    public static bool Import(GugolNpcMapKnowledgeRecord[] records, out string error)
    {
        if (!TryValidateRecords(records, out error)) return false;
        Records.Clear();
        foreach (var record in records ?? Array.Empty<GugolNpcMapKnowledgeRecord>())
            Records[record.npcId] = GugolNpcMapKnowledgeRecord.Clone(record);
        OnChanged?.Invoke();
        return true;
    }

    public static bool TryValidateRecords(GugolNpcMapKnowledgeRecord[] records, out string error)
    {
        error = null;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < (records?.Length ?? 0); i++)
        {
            var record = records[i];
            if (!TryValidateRecord(record, i, out error)) return false;
            if (!ids.Add(record.npcId))
            {
                error = $"NPC map knowledge '{record.npcId}' appears more than once.";
                return false;
            }
        }
        return true;
    }

    public static string FormatRecency(GugolNpcMapKnowledgeRecord record, string currentDayKey)
    {
        if (record == null || record.source == GugolNpcKnowledgeSource.UsualLocation)
            return "usually found here";
        string band = BandText(record.observedTimeBand);
        if (!TryParseDay(record.observedDayKey, out int observed) ||
            !TryParseDay(currentDayKey, out int current))
            return string.IsNullOrEmpty(band) ? "last known here" : $"seen {band}";

        int delta = current - observed;
        if (delta == 0) return string.IsNullOrEmpty(band) ? "seen today" : $"seen this {band}";
        if (delta == 1) return "seen yesterday";
        if (delta > 1) return $"seen {delta} days ago";
        return "last known here";
    }

    static bool TryValidateRecord(GugolNpcMapKnowledgeRecord record, int index, out string error)
    {
        error = null;
        if (record == null || string.IsNullOrWhiteSpace(record.npcId) ||
            string.IsNullOrWhiteSpace(record.streetId) || string.IsNullOrWhiteSpace(record.observedDayKey) ||
            !Enum.IsDefined(typeof(GugolTimeBand), record.observedTimeBand) ||
            !Enum.IsDefined(typeof(GugolNpcKnowledgeSource), record.source))
        {
            error = $"NPC map knowledge record {index} is missing stable identity, street, date, or enum state.";
            return false;
        }
        if (record.source != GugolNpcKnowledgeSource.UsualLocation && string.IsNullOrWhiteSpace(record.sourceId))
        {
            error = $"NPC map knowledge '{record.npcId}' requires a stable source ID.";
            return false;
        }
        return true;
    }

    static bool Same(GugolNpcMapKnowledgeRecord left, GugolNpcMapKnowledgeRecord right) =>
        string.Equals(left.npcId, right.npcId, StringComparison.Ordinal) &&
        string.Equals(left.streetId, right.streetId, StringComparison.Ordinal) &&
        string.Equals(left.venueId, right.venueId, StringComparison.Ordinal) &&
        string.Equals(left.observedDayKey, right.observedDayKey, StringComparison.Ordinal) &&
        left.observedTimeBand == right.observedTimeBand && left.source == right.source &&
        string.Equals(left.sourceId, right.sourceId, StringComparison.Ordinal);

    static bool TryParseDay(string key, out int absoluteDay)
    {
        absoluteDay = 0;
        if (string.IsNullOrWhiteSpace(key)) return false;
        string[] parts = key.Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[0], out int year) || !int.TryParse(parts[1], out int day))
            return false;
        absoluteDay = checked(year * 400 + day);
        return true;
    }

    static string BandText(GugolTimeBand band) => band switch
    {
        GugolTimeBand.Morning => "morning",
        GugolTimeBand.Afternoon => "afternoon",
        GugolTimeBand.Evening => "evening",
        GugolTimeBand.Night => "night",
        _ => string.Empty,
    };
}
