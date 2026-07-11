using System;
using System.Collections.Generic;
using UnityEngine;

public static class WorldEffectIds
{
    public const string CircleInfluenceDelta = "circle_influence_delta";
    public const string SanctityDelta = "sanctity_delta";
    public const string NpcExposureDelta = "npc_exposure_delta";
    public const string NpcRelationshipDelta = "npc_relationship_delta";
    public const string NpcRescue = "npc_rescue";
    public const string DistrictNpcCleanse = "district_npc_cleanse";
    public const string RumorStageUnlock = "rumor_stage_unlock";
    public const string PoiStageUnlock = "poi_stage_unlock";
    public const string RouteUnlock = "route_unlock";
    public const string WorldAgentState = "world_agent_state";
    public const string InventoryDelta = "inventory_delta";
    public const string FlorinsDelta = "florins_delta";
}

[Serializable]
public sealed class WorldConsequenceEffect
{
    [Tooltip("Registered WorldEffectIds value.")]
    public string effectId;
    public string targetId;
    public string secondaryId;
    public int intValue;
    public float numericValue;
    public string stringValue;
}

[Serializable]
public sealed class WorldEventRecord
{
    public string eventInstanceId;
    public string eventTypeId;
    public string gameDateKey;
    public string locationId;
    public string[] npcIds = Array.Empty<string>();
    public string[] worldAgentIds = Array.Empty<string>();
    public string choiceId;
    public WorldConsequenceEffect[] consequences = Array.Empty<WorldConsequenceEffect>();
    public string factualOutcome;
    public string[] semanticTags = Array.Empty<string>();
    public bool campaignPermanent;
    public string campaignId;
    public long chronicleSequence;

    public static WorldEventRecord Clone(WorldEventRecord source) =>
        source == null ? null : JsonUtility.FromJson<WorldEventRecord>(JsonUtility.ToJson(source));

    public static WorldConsequenceEffect[] CloneEffects(WorldConsequenceEffect[] source)
    {
        if (source == null || source.Length == 0) return Array.Empty<WorldConsequenceEffect>();
        var copy = new WorldConsequenceEffect[source.Length];
        for (int i = 0; i < source.Length; i++)
        {
            var value = source[i];
            copy[i] = value == null ? null : new WorldConsequenceEffect
            {
                effectId = value.effectId,
                targetId = value.targetId,
                secondaryId = value.secondaryId,
                intValue = value.intValue,
                numericValue = value.numericValue,
                stringValue = value.stringValue,
            };
        }
        return copy;
    }

    public static WorldEventRecord FromChronicle(CampaignChronicleEntry entry)
    {
        if (entry == null) return null;
        return new WorldEventRecord
        {
            eventInstanceId = entry.eventInstanceId,
            eventTypeId = entry.eventTypeId,
            gameDateKey = entry.gameDateKey,
            locationId = entry.locationId,
            npcIds = CloneStrings(entry.npcIds),
            worldAgentIds = CloneStrings(entry.worldAgentIds),
            choiceId = entry.choiceId,
            consequences = CloneEffects(entry.consequences),
            factualOutcome = entry.factualOutcome,
            semanticTags = CloneStrings(entry.semanticTags),
            campaignPermanent = true,
            campaignId = entry.campaignId,
            chronicleSequence = entry.sequence,
        };
    }

    static string[] CloneStrings(string[] values)
    {
        if (values == null || values.Length == 0) return Array.Empty<string>();
        var copy = new string[values.Length];
        Array.Copy(values, copy, values.Length);
        return copy;
    }
}

[Serializable]
public sealed class WorldChoiceDefinition
{
    public string choiceId;
    [TextArea] public string playerText;
    [TextArea] public string factualOutcome;
    public string[] semanticTags = Array.Empty<string>();
    public WorldConsequenceEffect[] consequences = Array.Empty<WorldConsequenceEffect>();
}

public sealed class WorldEventContext
{
    public string eventInstanceId;
    public string gameDateKey;
    public string locationId;
    public string[] npcIds = Array.Empty<string>();
    public string[] worldAgentIds = Array.Empty<string>();

    public void FillRuntimeDefaults()
    {
        if (string.IsNullOrWhiteSpace(gameDateKey))
        {
            var calendar = GameCalendar.Instance;
            gameDateKey = calendar != null ? $"{calendar.Year}:{calendar.DayOfYear}" : "undated";
        }
        if (string.IsNullOrWhiteSpace(locationId)) locationId = DistrictTracker.CurrentNodeId;
        npcIds ??= Array.Empty<string>();
        worldAgentIds ??= Array.Empty<string>();
    }
}

public interface IWorldConsequenceSink
{
    bool CanApply(WorldConsequenceEffect effect, out string error);
    bool TryApply(WorldConsequenceEffect effect, out string error);
}

public interface IWorldConsequenceBatchSink : IWorldConsequenceSink
{
    bool CanApplyBatch(WorldConsequenceEffect[] effects, out string error);
    bool TryApplyBatch(WorldConsequenceEffect[] effects, out string error);
}

public sealed class UnityWorldConsequenceSink : IWorldConsequenceBatchSink
{
    public static readonly UnityWorldConsequenceSink Instance = new();

    public bool CanApply(WorldConsequenceEffect effect, out string error)
    {
        error = null;
        if (!WorldConsequenceRegistry.TryValidate(effect, out error)) return false;
        switch (effect.effectId)
        {
            case WorldEffectIds.CircleInfluenceDelta:
            case WorldEffectIds.SanctityDelta:
                var hub = HubMap.Instance;
                if (hub == null)
                {
                    error = "HubMap is unavailable.";
                    return false;
                }
                if (hub.GetNode(effect.targetId) == null)
                {
                    error = $"World location '{effect.targetId}' is not registered.";
                    return false;
                }
                return true;
            case WorldEffectIds.FlorinsDelta:
                if (effect.intValue < 0 && FlorinWallet.Balance < -effect.intValue)
                {
                    error = $"Cannot remove {-effect.intValue} florins from a balance of {FlorinWallet.Balance}.";
                    return false;
                }
                return true;
            case WorldEffectIds.NpcExposureDelta:
                return PersistentLimboWorldState.CanApplyNpcExposureDelta(
                    effect.targetId, effect.numericValue, out error);
            case WorldEffectIds.NpcRescue:
                if (!PersistentLimboWorldState.TryGetNpc(effect.targetId, out var npc) || !npc.forgottenPool)
                {
                    error = $"NPC '{effect.targetId}' is not available for Forgotten rescue.";
                    return false;
                }
                return true;
            case WorldEffectIds.DistrictNpcCleanse:
                return PersistentLimboWorldState.CanCleanseDistrict(
                    effect.targetId, effect.numericValue, out error);
            case WorldEffectIds.WorldAgentState:
                return PersistentLimboWorldState.CanSetAgentState(
                    effect.targetId, effect.stringValue, out error);
            case WorldEffectIds.RumorStageUnlock:
                return ExplorationDiscoveryLedger.CanAdvance(
                    effect.targetId, DiscoveryKind.Rumor, (DiscoveryStage)effect.intValue, out error);
            case WorldEffectIds.PoiStageUnlock:
                return ExplorationDiscoveryLedger.CanAdvance(
                    effect.targetId, DiscoveryKind.PointOfInterest, (DiscoveryStage)effect.intValue, out error);
            case WorldEffectIds.RouteUnlock:
                return ExplorationDiscoveryLedger.CanAdvance(
                    effect.targetId, DiscoveryKind.Route, (DiscoveryStage)effect.intValue, out error);
            default:
                error = $"Effect '{effect.effectId}' is registered but its runtime owner is not installed yet.";
                return false;
        }
    }

    public bool TryApply(WorldConsequenceEffect effect, out string error)
        => TryApplyBatch(new[] { effect }, out error);

    public bool CanApplyBatch(WorldConsequenceEffect[] effects, out string error)
    {
        error = null;
        int florinDelta = 0;
        var npcExposureDeltas = new Dictionary<string, float>(StringComparer.Ordinal);
        foreach (var effect in effects ?? Array.Empty<WorldConsequenceEffect>())
        {
            if (!WorldConsequenceRegistry.TryValidate(effect, out error)) return false;
            if (effect.effectId == WorldEffectIds.FlorinsDelta)
                florinDelta += effect.intValue;
            else if (effect.effectId == WorldEffectIds.NpcExposureDelta)
                npcExposureDeltas[effect.targetId] = npcExposureDeltas.TryGetValue(effect.targetId, out float current)
                    ? current + effect.numericValue
                    : effect.numericValue;
            else if (!CanApply(effect, out error))
                return false;
        }
        if (FlorinWallet.Balance + florinDelta < 0)
        {
            error = "Combined world consequences would make the florin balance negative.";
            return false;
        }
        foreach (var pair in npcExposureDeltas)
            if (!PersistentLimboWorldState.CanApplyNpcExposureDelta(pair.Key, pair.Value, out error))
                return false;
        return true;
    }

    public bool TryApplyBatch(WorldConsequenceEffect[] effects, out string error)
    {
        if (!CanApplyBatch(effects, out error)) return false;
        int florinDelta = 0;
        var npcExposureDeltas = new Dictionary<string, float>(StringComparer.Ordinal);
        foreach (var effect in effects ?? Array.Empty<WorldConsequenceEffect>())
        {
            switch (effect.effectId)
            {
                case WorldEffectIds.CircleInfluenceDelta:
                    HubMap.Instance.ApplyLedgerInfluenceDelta(
                        effect.targetId, (CircleId)effect.intValue, effect.numericValue);
                    break;
                case WorldEffectIds.SanctityDelta:
                    HubMap.Instance.ApplyLedgerSanctityDelta(effect.targetId, effect.numericValue);
                    break;
                case WorldEffectIds.FlorinsDelta:
                    florinDelta += effect.intValue;
                    break;
                case WorldEffectIds.NpcExposureDelta:
                    npcExposureDeltas[effect.targetId] = npcExposureDeltas.TryGetValue(effect.targetId, out float current)
                        ? current + effect.numericValue
                        : effect.numericValue;
                    break;
                case WorldEffectIds.NpcRescue:
                    if (!PersistentLimboWorldState.RescueForgottenNpc(
                            effect.targetId, CurrentDayKey(), out error)) return false;
                    break;
                case WorldEffectIds.DistrictNpcCleanse:
                    if (!PersistentLimboWorldState.CleanseDistrict(
                            effect.targetId, effect.numericValue, out error)) return false;
                    break;
                case WorldEffectIds.WorldAgentState:
                    if (!PersistentLimboWorldState.SetAgentState(
                            effect.targetId, effect.stringValue, CurrentDayKey(), out error)) return false;
                    break;
                case WorldEffectIds.RumorStageUnlock:
                case WorldEffectIds.PoiStageUnlock:
                case WorldEffectIds.RouteUnlock:
                    var kind = effect.effectId == WorldEffectIds.RumorStageUnlock
                        ? DiscoveryKind.Rumor
                        : effect.effectId == WorldEffectIds.PoiStageUnlock
                            ? DiscoveryKind.PointOfInterest
                            : DiscoveryKind.Route;
                    if (!ExplorationDiscoveryLedger.Advance(
                            effect.targetId, kind, (DiscoveryStage)effect.intValue,
                            effect.secondaryId, CurrentDayKey(), out _, out error)) return false;
                    break;
                default:
                    error = $"Effect '{effect.effectId}' has no runtime applier.";
                    return false;
            }
        }
        if (florinDelta > 0)
            FlorinWallet.Add(florinDelta, "world consequences");
        else if (florinDelta < 0)
            FlorinWallet.TrySpend(-florinDelta, "world consequences");
        foreach (var pair in npcExposureDeltas)
            if (!PersistentLimboWorldState.ApplyNpcExposureDelta(pair.Key, pair.Value, out error)) return false;
        return true;
    }

    static string CurrentDayKey()
    {
        var calendar = GameCalendar.Instance;
        return calendar != null ? calendar.Year + ":" + calendar.DayOfYear : "undated";
    }
}

public static class WorldConsequenceRegistry
{
    static readonly HashSet<string> KnownEffectIds = new(StringComparer.Ordinal)
    {
        WorldEffectIds.CircleInfluenceDelta,
        WorldEffectIds.SanctityDelta,
        WorldEffectIds.NpcExposureDelta,
        WorldEffectIds.NpcRelationshipDelta,
        WorldEffectIds.NpcRescue,
        WorldEffectIds.DistrictNpcCleanse,
        WorldEffectIds.RumorStageUnlock,
        WorldEffectIds.PoiStageUnlock,
        WorldEffectIds.RouteUnlock,
        WorldEffectIds.WorldAgentState,
        WorldEffectIds.InventoryDelta,
        WorldEffectIds.FlorinsDelta,
    };

    public static bool IsRegistered(string effectId) =>
        !string.IsNullOrWhiteSpace(effectId) && KnownEffectIds.Contains(effectId);

    public static bool TryValidate(WorldConsequenceEffect effect, out string error)
    {
        error = null;
        if (effect == null)
        {
            error = "Consequence effect is null.";
            return false;
        }
        if (!IsRegistered(effect.effectId))
        {
            error = $"Consequence effect '{effect.effectId}' is not registered.";
            return false;
        }
        if (float.IsNaN(effect.numericValue) || float.IsInfinity(effect.numericValue))
        {
            error = $"Consequence effect '{effect.effectId}' has a non-finite value.";
            return false;
        }

        switch (effect.effectId)
        {
            case WorldEffectIds.CircleInfluenceDelta:
                if (string.IsNullOrWhiteSpace(effect.targetId) ||
                    !Enum.IsDefined(typeof(CircleId), effect.intValue) ||
                    Mathf.Abs(effect.numericValue) > 1f)
                    error = "Circle influence deltas require a location, known Circle ID, and value within -1..1.";
                break;
            case WorldEffectIds.SanctityDelta:
                if (string.IsNullOrWhiteSpace(effect.targetId) || Mathf.Abs(effect.numericValue) > 1f)
                    error = "Sanctity deltas require a location and value within -1..1.";
                break;
            case WorldEffectIds.NpcExposureDelta:
            case WorldEffectIds.NpcRelationshipDelta:
                if (string.IsNullOrWhiteSpace(effect.targetId) || Mathf.Abs(effect.numericValue) > 100f)
                    error = $"{effect.effectId} requires a target and value within -100..100.";
                break;
            case WorldEffectIds.NpcRescue:
                if (string.IsNullOrWhiteSpace(effect.targetId))
                    error = "NPC rescue requires a stable NPC target.";
                break;
            case WorldEffectIds.DistrictNpcCleanse:
                if (string.IsNullOrWhiteSpace(effect.targetId) ||
                    effect.numericValue <= 0f || effect.numericValue > 100f)
                    error = "District NPC cleanse requires a district and amount within 0..100.";
                break;
            case WorldEffectIds.RumorStageUnlock:
            case WorldEffectIds.PoiStageUnlock:
            case WorldEffectIds.RouteUnlock:
                if (string.IsNullOrWhiteSpace(effect.targetId) || effect.intValue < 0 ||
                    effect.intValue > (int)DiscoveryStage.Discovered)
                    error = $"{effect.effectId} requires a registered target and valid discovery stage.";
                break;
            case WorldEffectIds.WorldAgentState:
                if (string.IsNullOrWhiteSpace(effect.targetId) || string.IsNullOrWhiteSpace(effect.stringValue))
                    error = "World-agent state effects require an agent ID and registered state value.";
                break;
            case WorldEffectIds.InventoryDelta:
                if (string.IsNullOrWhiteSpace(effect.targetId) || effect.intValue == 0)
                    error = "Inventory deltas require an item ID and non-zero quantity.";
                break;
            case WorldEffectIds.FlorinsDelta:
                if (effect.intValue == 0)
                    error = "Florin deltas must be non-zero.";
                break;
        }
        return error == null;
    }

    public static bool TryValidateAll(WorldConsequenceEffect[] effects, out string error)
    {
        error = null;
        if (effects == null) return true;
        for (int i = 0; i < effects.Length; i++)
        {
            if (TryValidate(effects[i], out error)) continue;
            error = $"Effect {i}: {error}";
            return false;
        }
        return true;
    }
}

/// <summary>
/// Canonical in-memory ledger saved inside ordinary slots. Permanent records
/// are reconciled forward from CampaignChronicle before play resumes.
/// </summary>
public static class WorldEventLedger
{
    static readonly List<WorldEventRecord> Records = new();
    static readonly Dictionary<string, int> ByEventId = new(StringComparer.Ordinal);
    static readonly Dictionary<string, List<int>> ByNpc = new(StringComparer.Ordinal);
    static readonly Dictionary<string, List<int>> ByAgent = new(StringComparer.Ordinal);
    static readonly Dictionary<string, List<int>> ByLocation = new(StringComparer.Ordinal);
    static readonly Dictionary<string, List<int>> ByEventType = new(StringComparer.Ordinal);
    static readonly Dictionary<string, List<int>> ByTag = new(StringComparer.Ordinal);

    public static int Count => Records.Count;

    public static void Reset()
    {
        Records.Clear();
        ByEventId.Clear();
        ByNpc.Clear();
        ByAgent.Clear();
        ByLocation.Clear();
        ByEventType.Clear();
        ByTag.Clear();
    }

    public static bool Contains(string eventInstanceId) =>
        !string.IsNullOrEmpty(eventInstanceId) && ByEventId.ContainsKey(eventInstanceId);

    public static bool TryGet(string eventInstanceId, out WorldEventRecord record)
    {
        if (ByEventId.TryGetValue(eventInstanceId ?? string.Empty, out int index))
        {
            record = WorldEventRecord.Clone(Records[index]);
            return true;
        }
        record = null;
        return false;
    }

    public static WorldEventRecord[] Export()
    {
        var exported = new WorldEventRecord[Records.Count];
        for (int i = 0; i < Records.Count; i++) exported[i] = WorldEventRecord.Clone(Records[i]);
        return exported;
    }

    public static bool Import(WorldEventRecord[] records, out string error)
    {
        if (!TryValidateRecords(records, out error)) return false;
        Reset();
        if (records == null) return true;
        foreach (var record in records) AddIndexed(WorldEventRecord.Clone(record));
        return true;
    }

    public static bool TryValidateRecords(WorldEventRecord[] records, out string error)
    {
        error = null;
        if (records == null) return true;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < records.Length; i++)
        {
            if (!TryValidateRecord(records[i], out error))
            {
                error = $"World event {i}: {error}";
                return false;
            }
            if (!ids.Add(records[i].eventInstanceId))
            {
                error = $"World event '{records[i].eventInstanceId}' is duplicated.";
                return false;
            }
        }
        return true;
    }

    public static bool TryCommitChoice(
        WorldEventDefinition definition,
        string choiceId,
        WorldEventContext context,
        out WorldEventRecord committedRecord,
        out string error,
        IWorldConsequenceSink sink = null)
    {
        committedRecord = null;
        error = null;
        if (definition == null || string.IsNullOrWhiteSpace(definition.eventTypeId))
        {
            error = "A registered WorldEventDefinition is required.";
            return false;
        }
        if (!definition.TryGetChoice(choiceId, out var choice))
        {
            error = $"Choice '{choiceId}' is not registered for event '{definition.eventTypeId}'.";
            return false;
        }
        context ??= new WorldEventContext();
        context.FillRuntimeDefaults();

        var record = new WorldEventRecord
        {
            eventInstanceId = context.eventInstanceId,
            eventTypeId = definition.eventTypeId,
            gameDateKey = context.gameDateKey,
            locationId = context.locationId,
            npcIds = CloneStrings(context.npcIds),
            worldAgentIds = CloneStrings(context.worldAgentIds),
            choiceId = choice.choiceId,
            consequences = WorldEventRecord.CloneEffects(choice.consequences),
            factualOutcome = choice.factualOutcome,
            semanticTags = CloneStrings(choice.semanticTags),
            campaignPermanent = definition.campaignPermanent,
        };

        if (TryGet(record.eventInstanceId, out var existing))
        {
            if (!string.Equals(existing.choiceId, record.choiceId, StringComparison.Ordinal))
            {
                error = $"Event '{record.eventInstanceId}' was already resolved with choice '{existing.choiceId}'.";
                return false;
            }
            committedRecord = existing;
            return true;
        }

        sink ??= UnityWorldConsequenceSink.Instance;
        if (!TryPreflight(record, sink, out error, allowPermanentDraft: true)) return false;

        if (record.campaignPermanent)
        {
            if (!CampaignChronicle.TryCommit(record, out var chronicleEntry, out error)) return false;
            record.campaignId = chronicleEntry.campaignId;
            record.chronicleSequence = chronicleEntry.sequence;
        }

        if (!TryApplyAndRecord(record, sink, out error)) return false;
        committedRecord = WorldEventRecord.Clone(record);
        return true;
    }

    public static bool TryReconcile(
        CampaignChronicleDocument chronicle,
        long saveSequence,
        out long incorporatedSequence,
        out string error,
        IWorldConsequenceSink sink = null)
    {
        incorporatedSequence = saveSequence;
        error = null;
        if (chronicle == null || !CampaignChronicleStore.ValidateDocument(
                chronicle, chronicle.campaignId, saveSequence, out error))
            return false;
        sink ??= UnityWorldConsequenceSink.Instance;

        var pending = new List<WorldEventRecord>();

        foreach (var entry in chronicle.entries)
        {
            if (TryGet(entry.eventInstanceId, out var existing))
            {
                if (!MatchesChronicle(existing, entry))
                {
                    error = $"Saved world event '{entry.eventInstanceId}' contradicts the Campaign Chronicle.";
                    return false;
                }
                incorporatedSequence = Math.Max(incorporatedSequence, entry.sequence);
                continue;
            }

            if (entry.sequence <= saveSequence)
            {
                error = $"Save claims Chronicle sequence {saveSequence} but is missing permanent event '{entry.eventInstanceId}'.";
                return false;
            }

            var record = WorldEventRecord.FromChronicle(entry);
            if (!TryValidateRecord(record, out error)) return false;
            pending.Add(record);
        }

        if (pending.Count > 0 && sink is IWorldConsequenceBatchSink batchSink)
        {
            var combined = CombineEffects(pending);
            if (!batchSink.CanApplyBatch(combined, out error) || !batchSink.TryApplyBatch(combined, out error))
                return false;
            foreach (var record in pending) AddIndexed(WorldEventRecord.Clone(record));
        }
        else
        {
            foreach (var record in pending)
                if (!TryApplyAndRecord(record, sink, out error)) return false;
        }
        incorporatedSequence = chronicle.lastSequence;
        return true;
    }

    public static bool TryValidateChronicleCoverage(
        WorldEventRecord[] records,
        CampaignChronicleDocument chronicle,
        long incorporatedSequence,
        out string error)
    {
        error = null;
        if (!TryValidateRecords(records, out error)) return false;
        if (chronicle == null || !CampaignChronicleStore.ValidateDocument(
                chronicle, chronicle.campaignId, incorporatedSequence, out error))
            return false;

        var byId = new Dictionary<string, WorldEventRecord>(StringComparer.Ordinal);
        if (records != null)
            foreach (var record in records) byId[record.eventInstanceId] = record;

        foreach (var entry in chronicle.entries)
        {
            if (entry.sequence > incorporatedSequence) break;
            if (!byId.TryGetValue(entry.eventInstanceId, out var record) || !MatchesChronicle(record, entry))
            {
                error = $"Save sequence {incorporatedSequence} is missing or contradicts permanent event '{entry.eventInstanceId}'.";
                return false;
            }
        }

        if (records != null)
        {
            var entriesById = new Dictionary<string, CampaignChronicleEntry>(StringComparer.Ordinal);
            foreach (var entry in chronicle.entries) entriesById[entry.eventInstanceId] = entry;
            foreach (var record in records)
            {
                if (!record.campaignPermanent) continue;
                if (!entriesById.TryGetValue(record.eventInstanceId, out var entry) ||
                    entry.sequence > incorporatedSequence || !MatchesChronicle(record, entry))
                {
                    error = $"Permanent save record '{record.eventInstanceId}' has no matching incorporated Chronicle entry.";
                    return false;
                }
            }
        }
        return true;
    }

    public static WorldEventRecord[] QueryByNpc(string npcId) => QueryIndex(ByNpc, npcId);
    public static WorldEventRecord[] QueryByWorldAgent(string agentId) => QueryIndex(ByAgent, agentId);
    public static WorldEventRecord[] QueryByLocation(string locationId) => QueryIndex(ByLocation, locationId);
    public static WorldEventRecord[] QueryByEventType(string eventTypeId) => QueryIndex(ByEventType, eventTypeId);
    public static WorldEventRecord[] QueryByTag(string tag) => QueryIndex(ByTag, tag);

    public static WorldEventRecord[] QueryByCircle(CircleId circle)
    {
        var matches = new List<WorldEventRecord>();
        foreach (var record in Records)
        {
            foreach (var effect in record.consequences ?? Array.Empty<WorldConsequenceEffect>())
            {
                if (effect != null && effect.effectId == WorldEffectIds.CircleInfluenceDelta &&
                    effect.intValue == (int)circle)
                {
                    matches.Add(WorldEventRecord.Clone(record));
                    break;
                }
            }
        }
        return matches.ToArray();
    }

    public static string[] BuildCanonicalFacts()
    {
        var facts = new List<string>();
        foreach (var record in Records)
            if (!string.IsNullOrWhiteSpace(record.factualOutcome))
                facts.Add(record.factualOutcome);
        return facts.ToArray();
    }

    static bool TryApplyAndRecord(WorldEventRecord record, IWorldConsequenceSink sink, out string error)
    {
        if (TryGet(record.eventInstanceId, out var existing))
        {
            error = null;
            return string.Equals(existing.choiceId, record.choiceId, StringComparison.Ordinal);
        }
        if (!TryPreflight(record, sink, out error)) return false;
        if (sink is IWorldConsequenceBatchSink batchSink)
        {
            if (!batchSink.TryApplyBatch(record.consequences ?? Array.Empty<WorldConsequenceEffect>(), out error))
                return false;
        }
        else
        {
            foreach (var effect in record.consequences ?? Array.Empty<WorldConsequenceEffect>())
                if (!sink.TryApply(effect, out error)) return false;
        }
        AddIndexed(WorldEventRecord.Clone(record));
        return true;
    }

    static bool TryPreflight(
        WorldEventRecord record,
        IWorldConsequenceSink sink,
        out string error,
        bool allowPermanentDraft = false)
    {
        if (!TryValidateRecord(record, out error, allowPermanentDraft)) return false;
        var effects = record.consequences ?? Array.Empty<WorldConsequenceEffect>();
        if (sink is IWorldConsequenceBatchSink batchSink)
            return batchSink.CanApplyBatch(effects, out error);
        foreach (var effect in effects)
            if (!sink.CanApply(effect, out error)) return false;
        return true;
    }

    static bool TryValidateRecord(
        WorldEventRecord record,
        out string error,
        bool allowPermanentDraft = false)
    {
        error = null;
        if (record == null || string.IsNullOrWhiteSpace(record.eventInstanceId) ||
            string.IsNullOrWhiteSpace(record.eventTypeId) || string.IsNullOrWhiteSpace(record.choiceId) ||
            string.IsNullOrWhiteSpace(record.gameDateKey) || string.IsNullOrWhiteSpace(record.locationId) ||
            string.IsNullOrWhiteSpace(record.factualOutcome))
        {
            error = "World event is missing required immutable identity or factual outcome.";
            return false;
        }
        record.npcIds ??= Array.Empty<string>();
        record.worldAgentIds ??= Array.Empty<string>();
        record.consequences ??= Array.Empty<WorldConsequenceEffect>();
        record.semanticTags ??= Array.Empty<string>();
        if (!WorldConsequenceRegistry.TryValidateAll(record.consequences, out error)) return false;
        if (record.campaignPermanent && !allowPermanentDraft &&
            (string.IsNullOrWhiteSpace(record.campaignId) || record.chronicleSequence <= 0))
        {
            // A draft permanent record is allowed only before Chronicle commit.
            // Callers validate drafts through TryCommitChoice, not imports.
            error = "Permanent world event lacks its campaign ID or Chronicle sequence.";
            return false;
        }
        return true;
    }

    static bool MatchesChronicle(WorldEventRecord record, CampaignChronicleEntry entry)
    {
        if (record == null || entry == null || !record.campaignPermanent ||
            !string.Equals(record.campaignId, entry.campaignId, StringComparison.Ordinal) ||
            record.chronicleSequence != entry.sequence)
            return false;

        var candidate = new CampaignChronicleEntry
        {
            campaignId = record.campaignId,
            sequence = record.chronicleSequence,
            eventInstanceId = record.eventInstanceId,
            eventTypeId = record.eventTypeId,
            choiceId = record.choiceId,
            gameDateKey = record.gameDateKey,
            locationId = record.locationId,
            npcIds = CloneStrings(record.npcIds),
            worldAgentIds = CloneStrings(record.worldAgentIds),
            consequences = WorldEventRecord.CloneEffects(record.consequences),
            factualOutcome = record.factualOutcome,
            semanticTags = CloneStrings(record.semanticTags),
            previousHash = entry.previousHash,
        };
        return string.Equals(
            CampaignChronicleStore.ComputeEntryHash(candidate), entry.currentHash, StringComparison.Ordinal);
    }

    static WorldConsequenceEffect[] CombineEffects(List<WorldEventRecord> records)
    {
        int count = 0;
        foreach (var record in records) count += record.consequences?.Length ?? 0;
        var result = new WorldConsequenceEffect[count];
        int index = 0;
        foreach (var record in records)
            foreach (var effect in record.consequences ?? Array.Empty<WorldConsequenceEffect>())
                result[index++] = effect;
        return result;
    }

    static void AddIndexed(WorldEventRecord record)
    {
        int index = Records.Count;
        Records.Add(record);
        ByEventId.Add(record.eventInstanceId, index);
        Index(ByLocation, record.locationId, index);
        Index(ByEventType, record.eventTypeId, index);
        IndexMany(ByNpc, record.npcIds, index);
        IndexMany(ByAgent, record.worldAgentIds, index);
        IndexMany(ByTag, record.semanticTags, index);
    }

    static void Index(Dictionary<string, List<int>> index, string key, int recordIndex)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        if (!index.TryGetValue(key, out var list)) index[key] = list = new List<int>();
        list.Add(recordIndex);
    }

    static void IndexMany(Dictionary<string, List<int>> index, string[] keys, int recordIndex)
    {
        if (keys == null) return;
        var unique = new HashSet<string>(StringComparer.Ordinal);
        foreach (string key in keys)
            if (!string.IsNullOrWhiteSpace(key) && unique.Add(key)) Index(index, key, recordIndex);
    }

    static WorldEventRecord[] QueryIndex(Dictionary<string, List<int>> index, string key)
    {
        if (string.IsNullOrWhiteSpace(key) || !index.TryGetValue(key, out var positions))
            return Array.Empty<WorldEventRecord>();
        var result = new WorldEventRecord[positions.Count];
        for (int i = 0; i < positions.Count; i++) result[i] = WorldEventRecord.Clone(Records[positions[i]]);
        return result;
    }

    static string[] CloneStrings(string[] values)
    {
        if (values == null || values.Length == 0) return Array.Empty<string>();
        var copy = new string[values.Length];
        Array.Copy(values, copy, values.Length);
        return copy;
    }
}
