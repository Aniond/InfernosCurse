using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class SiteOutcomeRecord
{
    public string siteId;
    public string outcomeId;
    public string sourceEventInstanceId;
    public NarrativePermanence permanence;
    public string activatedDayKey;
    public bool recovered;
    public bool remembered;
    public string remembranceTextId;
    public string rememberedDayKey;

    public static SiteOutcomeRecord Clone(SiteOutcomeRecord source) =>
        source == null ? null : JsonUtility.FromJson<SiteOutcomeRecord>(JsonUtility.ToJson(source));
}

/// <summary>
/// Persistent authored state attached to a site. Circle Influence remains owned
/// by the site's territory; this store never creates a numeric site ledger.
/// </summary>
public static class SiteOutcomeState
{
    static readonly List<SiteOutcomeRecord> Records = new();
    static readonly Dictionary<string, int> ByKey = new(StringComparer.Ordinal);

    public static int Count => Records.Count;

    public static void Reset()
    {
        Records.Clear();
        ByKey.Clear();
    }

    public static bool TryGet(string siteId, string outcomeId, out SiteOutcomeRecord record)
    {
        if (ByKey.TryGetValue(Key(siteId, outcomeId), out int index))
        {
            record = SiteOutcomeRecord.Clone(Records[index]);
            return true;
        }
        record = null;
        return false;
    }

    public static bool IsActive(string siteId, string outcomeId) =>
        TryGet(siteId, outcomeId, out var record) && !record.recovered;

    public static SiteOutcomeRecord[] QuerySite(string siteId)
    {
        if (string.IsNullOrWhiteSpace(siteId)) return Array.Empty<SiteOutcomeRecord>();
        var result = new List<SiteOutcomeRecord>();
        foreach (var record in Records)
            if (string.Equals(record.siteId, siteId, StringComparison.Ordinal))
                result.Add(SiteOutcomeRecord.Clone(record));
        return result.ToArray();
    }

    public static SiteOutcomeRecord[] Export()
    {
        var result = new SiteOutcomeRecord[Records.Count];
        for (int i = 0; i < Records.Count; i++) result[i] = SiteOutcomeRecord.Clone(Records[i]);
        return result;
    }

    public static bool Import(SiteOutcomeRecord[] records, out string error)
    {
        if (!TryValidateRecords(records, out error)) return false;
        ReplaceValidated(records);
        return true;
    }

    internal static void ReplaceValidated(SiteOutcomeRecord[] records)
    {
        Reset();
        foreach (var record in records ?? Array.Empty<SiteOutcomeRecord>())
            Add(SiteOutcomeRecord.Clone(record));
    }

    public static bool TryValidateRecords(SiteOutcomeRecord[] records, out string error)
    {
        error = null;
        var keys = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < (records?.Length ?? 0); i++)
        {
            var record = records[i];
            if (record == null || string.IsNullOrWhiteSpace(record.siteId) ||
                string.IsNullOrWhiteSpace(record.outcomeId) ||
                string.IsNullOrWhiteSpace(record.sourceEventInstanceId) ||
                string.IsNullOrWhiteSpace(record.activatedDayKey) ||
                !Enum.IsDefined(typeof(NarrativePermanence), record.permanence))
            {
                error = $"Site outcome {i} is missing stable identity, source, date, or permanence.";
                return false;
            }
            if (record.remembered && (string.IsNullOrWhiteSpace(record.remembranceTextId) ||
                                      string.IsNullOrWhiteSpace(record.rememberedDayKey)))
            {
                error = $"Site outcome '{record.siteId}/{record.outcomeId}' has incomplete remembrance state.";
                return false;
            }
            if (record.recovered && record.permanence == NarrativePermanence.CampaignPermanent)
            {
                error = $"Campaign-permanent site outcome '{record.siteId}/{record.outcomeId}' cannot be recovered.";
                return false;
            }
            if (!keys.Add(Key(record.siteId, record.outcomeId)))
            {
                error = $"Site outcome '{record.siteId}/{record.outcomeId}' is duplicated.";
                return false;
            }
        }
        return true;
    }

    public static bool CanApplyEffects(WorldConsequenceEffect[] effects, out string error)
    {
        error = null;
        var simulated = new Dictionary<string, SiteOutcomeRecord>(StringComparer.Ordinal);
        foreach (var record in Records) simulated[Key(record.siteId, record.outcomeId)] = SiteOutcomeRecord.Clone(record);

        foreach (var effect in effects ?? Array.Empty<WorldConsequenceEffect>())
        {
            if (effect == null) continue;
            if (effect.effectId == WorldEffectIds.SiteOutcomeState)
            {
                if (!TryBuildOutcome(effect, CurrentDayKey(), out var incoming, out error)) return false;
                string key = Key(incoming.siteId, incoming.outcomeId);
                if (simulated.TryGetValue(key, out var existing))
                {
                    if (!SameImmutableOutcome(existing, incoming))
                    {
                        error = $"Site outcome '{incoming.siteId}/{incoming.outcomeId}' contradicts its existing source.";
                        return false;
                    }
                }
                else simulated.Add(key, incoming);
            }
            else if (effect.effectId == WorldEffectIds.SiteRemembrance)
            {
                string key = Key(effect.targetId, effect.secondaryId);
                if (!simulated.TryGetValue(key, out var existing))
                {
                    error = $"Cannot remember absent site outcome '{effect.targetId}/{effect.secondaryId}'.";
                    return false;
                }
                if (string.IsNullOrWhiteSpace(effect.stringValue))
                {
                    error = "Site remembrance requires an authored memory text ID.";
                    return false;
                }
                if (existing.remembered &&
                    !string.Equals(existing.remembranceTextId, effect.stringValue, StringComparison.Ordinal))
                {
                    error = $"Site outcome '{effect.targetId}/{effect.secondaryId}' already has a different remembrance.";
                    return false;
                }
                existing.remembered = true;
                existing.remembranceTextId = effect.stringValue;
            }
        }
        return true;
    }

    public static bool TryApplyEffect(WorldConsequenceEffect effect, string dayKey, out string error)
    {
        error = null;
        if (effect == null)
        {
            error = "Site-outcome effect is null.";
            return false;
        }
        if (effect.effectId == WorldEffectIds.SiteOutcomeState)
        {
            if (!TryBuildOutcome(effect, dayKey, out var incoming, out error)) return false;
            string key = Key(incoming.siteId, incoming.outcomeId);
            if (ByKey.TryGetValue(key, out int index))
            {
                if (SameImmutableOutcome(Records[index], incoming)) return true;
                error = $"Site outcome '{incoming.siteId}/{incoming.outcomeId}' contradicts its existing source.";
                return false;
            }
            Add(incoming);
            return true;
        }
        if (effect.effectId == WorldEffectIds.SiteRemembrance)
        {
            string key = Key(effect.targetId, effect.secondaryId);
            if (!ByKey.TryGetValue(key, out int index))
            {
                error = $"Cannot remember absent site outcome '{effect.targetId}/{effect.secondaryId}'.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(effect.stringValue))
            {
                error = "Site remembrance requires an authored memory text ID.";
                return false;
            }
            var record = Records[index];
            if (record.remembered)
            {
                if (string.Equals(record.remembranceTextId, effect.stringValue, StringComparison.Ordinal)) return true;
                error = $"Site outcome '{effect.targetId}/{effect.secondaryId}' already has a different remembrance.";
                return false;
            }
            record.remembered = true;
            record.remembranceTextId = effect.stringValue;
            record.rememberedDayKey = string.IsNullOrWhiteSpace(dayKey) ? "undated" : dayKey;
            return true;
        }
        error = $"Effect '{effect.effectId}' is not owned by SiteOutcomeState.";
        return false;
    }

    public static bool TryRecover(string siteId, string outcomeId, string dayKey, out string error)
    {
        error = null;
        if (!ByKey.TryGetValue(Key(siteId, outcomeId), out int index))
        {
            error = $"Site outcome '{siteId}/{outcomeId}' does not exist.";
            return false;
        }
        var record = Records[index];
        if (record.permanence == NarrativePermanence.CampaignPermanent)
        {
            error = $"Site outcome '{siteId}/{outcomeId}' is campaign-permanent.";
            return false;
        }
        record.recovered = true;
        return true;
    }

    public static string[] BuildCanonicalFacts()
    {
        var facts = new List<string>();
        foreach (var record in Records)
        {
            facts.Add($"site_outcome:{record.siteId}:{record.outcomeId}:{(record.recovered ? "recovered" : "active")}");
            if (record.remembered) facts.Add($"site_remembered:{record.siteId}:{record.remembranceTextId}");
        }
        return facts.ToArray();
    }

    static bool TryBuildOutcome(
        WorldConsequenceEffect effect,
        string dayKey,
        out SiteOutcomeRecord record,
        out string error)
    {
        record = null;
        error = null;
        if (string.IsNullOrWhiteSpace(effect.targetId) ||
            string.IsNullOrWhiteSpace(effect.secondaryId) ||
            string.IsNullOrWhiteSpace(effect.stringValue) ||
            !Enum.IsDefined(typeof(NarrativePermanence), effect.intValue))
        {
            error = "Site outcome requires site, outcome, source-event, and known permanence values.";
            return false;
        }
        record = new SiteOutcomeRecord
        {
            siteId = effect.targetId,
            outcomeId = effect.secondaryId,
            sourceEventInstanceId = effect.stringValue,
            permanence = (NarrativePermanence)effect.intValue,
            activatedDayKey = string.IsNullOrWhiteSpace(dayKey) ? "undated" : dayKey,
        };
        return true;
    }

    static bool SameImmutableOutcome(SiteOutcomeRecord left, SiteOutcomeRecord right) =>
        left != null && right != null &&
        string.Equals(left.siteId, right.siteId, StringComparison.Ordinal) &&
        string.Equals(left.outcomeId, right.outcomeId, StringComparison.Ordinal) &&
        string.Equals(left.sourceEventInstanceId, right.sourceEventInstanceId, StringComparison.Ordinal) &&
        left.permanence == right.permanence;

    static void Add(SiteOutcomeRecord record)
    {
        ByKey.Add(Key(record.siteId, record.outcomeId), Records.Count);
        Records.Add(record);
    }

    static string Key(string siteId, string outcomeId) =>
        (siteId ?? string.Empty) + "\u001f" + (outcomeId ?? string.Empty);

    static string CurrentDayKey()
    {
        var calendar = GameCalendar.Instance;
        return calendar != null ? calendar.Year + ":" + calendar.DayOfYear : "undated";
    }
}
