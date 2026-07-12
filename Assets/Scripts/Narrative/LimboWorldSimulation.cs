using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// APPEND ONLY. Serialized in saves and Chronicle consequence values.
public enum CrierActivityState
{
    Travel = 0,
    Preach = 1,
    RelocateEvade = 2,
    Hide = 3,
    Defeated = 4,
}

public enum NpcMemoryStage
{
    Grounded = 0,
    Distracted = 1,
    Unmoored = 2,
    Forgotten = 3,
}

public enum NpcRecoveryState
{
    None = 0,
    AwaitingNextDawn = 1,
    Returned = 2,
}

[Serializable]
public sealed class PersistentWorldAgentRecord
{
    public string agentId;
    public string territoryId;
    public string districtId;
    public string currentSiteId;
    public string[] availableSiteIds = Array.Empty<string>();
    public CrierActivityState activityState = CrierActivityState.Travel;
    public bool discovered;
    public bool defeated;
    public string disruptedDayKey;
    public string lastProcessedDayKey;

    public static PersistentWorldAgentRecord Clone(PersistentWorldAgentRecord source) =>
        source == null ? null : JsonUtility.FromJson<PersistentWorldAgentRecord>(JsonUtility.ToJson(source));
}

[Serializable]
public sealed class NpcMemoryRecord
{
    public string npcId;
    public string homeDistrictId;
    public string homeTerritoryId;
    public NpcCircleRelevanceLayer relevanceLayer = NpcCircleRelevanceLayer.LocalWitness;
    [Range(0.5f, 1.5f)] public float susceptibility = 1f;
    [Range(0f, 100f)] public float exposure;
    public NpcMemoryStage stage;
    public string lastProcessedDayKey;
    public string originalScheduleId;
    public string originalRelationshipId;
    public string[] overlappingPreachingSiteIds = Array.Empty<string>();
    public string[] relevantSiteIds = Array.Empty<string>();
    public string[] erasedMemoryIds = Array.Empty<string>();
    public string[] rememberedMemoryIds = Array.Empty<string>();
    public bool essentialService;
    public bool questCritical;
    public bool forgottenPool;
    public NpcRecoveryState recoveryState;
    public string rescueRequestedDayKey;

    public static NpcMemoryRecord Clone(NpcMemoryRecord source) =>
        source == null ? null : JsonUtility.FromJson<NpcMemoryRecord>(JsonUtility.ToJson(source));
}

public static class PersistentLimboWorldState
{
    public const float PermanentMemoryErosionInfluence = 0.75f;
    static readonly Dictionary<string, WorldAgentDefinition> AgentDefinitions = new(StringComparer.Ordinal);
    static readonly Dictionary<string, NpcMemoryDefinition> NpcDefinitions = new(StringComparer.Ordinal);
    static readonly Dictionary<string, PersistentWorldAgentRecord> Agents = new(StringComparer.Ordinal);
    static readonly Dictionary<string, NpcMemoryRecord> Npcs = new(StringComparer.Ordinal);
    static bool _resourcesLoaded;

    public static void Reset()
    {
        AgentDefinitions.Clear();
        NpcDefinitions.Clear();
        Agents.Clear();
        Npcs.Clear();
        _resourcesLoaded = false;
    }

    public static void LoadResourceDefinitions()
    {
        if (_resourcesLoaded) return;
        _resourcesLoaded = true;
        foreach (var definition in Resources.LoadAll<WorldAgentDefinition>("WorldAgents"))
            if (!RegisterAgentDefinition(definition, out string error)) Debug.LogError("[LimboWorld] " + error);
        foreach (var definition in Resources.LoadAll<NpcMemoryDefinition>("NpcMemory"))
            if (!RegisterNpcDefinition(definition, out string error)) Debug.LogError("[LimboWorld] " + error);
    }

    public static bool RegisterAgentDefinition(WorldAgentDefinition definition, out string error)
    {
        error = null;
        if (definition == null || string.IsNullOrWhiteSpace(definition.agentId) ||
            string.IsNullOrWhiteSpace(definition.territoryId) ||
            string.IsNullOrWhiteSpace(definition.districtId) || string.IsNullOrWhiteSpace(definition.startingSiteId))
        {
            error = "World-agent definition requires stable agent, territory, district, and starting-site IDs.";
            return false;
        }
        if (AgentDefinitions.TryGetValue(definition.agentId, out var existing) && existing != definition)
        {
            error = $"World-agent ID '{definition.agentId}' is registered more than once.";
            return false;
        }
        AgentDefinitions[definition.agentId] = definition;
        if (!Agents.ContainsKey(definition.agentId)) Agents[definition.agentId] = CreateAgentRecord(definition);
        return true;
    }

    public static bool RegisterNpcDefinition(NpcMemoryDefinition definition, out string error)
    {
        error = null;
        if (definition == null || string.IsNullOrWhiteSpace(definition.npcId) ||
            string.IsNullOrWhiteSpace(definition.homeDistrictId) ||
            definition.susceptibility < 0.5f || definition.susceptibility > 1.5f ||
            !Enum.IsDefined(typeof(NpcCircleRelevanceLayer), definition.relevanceLayer))
        {
            error = "NPC memory definition requires stable IDs, a relevance layer, and susceptibility within 0.5..1.5.";
            return false;
        }
        if (NpcDefinitions.TryGetValue(definition.npcId, out var existing) && existing != definition)
        {
            error = $"NPC memory ID '{definition.npcId}' is registered more than once.";
            return false;
        }
        NpcDefinitions[definition.npcId] = definition;
        if (!Npcs.ContainsKey(definition.npcId)) Npcs[definition.npcId] = CreateNpcRecord(definition);
        return true;
    }

    public static bool TryGetAgent(string agentId, out PersistentWorldAgentRecord record)
    {
        LoadResourceDefinitions();
        return Agents.TryGetValue(agentId ?? string.Empty, out record);
    }

    public static bool TryGetNpc(string npcId, out NpcMemoryRecord record)
    {
        LoadResourceDefinitions();
        return Npcs.TryGetValue(npcId ?? string.Empty, out record);
    }

    public static bool TryGetAgentDefinition(string agentId, out WorldAgentDefinition definition)
    {
        LoadResourceDefinitions();
        return AgentDefinitions.TryGetValue(agentId ?? string.Empty, out definition);
    }

    public static List<PersistentWorldAgentRecord> MutableAgents()
    {
        LoadResourceDefinitions();
        return new List<PersistentWorldAgentRecord>(Agents.Values);
    }

    public static List<NpcMemoryRecord> MutableNpcs()
    {
        LoadResourceDefinitions();
        return new List<NpcMemoryRecord>(Npcs.Values);
    }

    public static PersistentWorldAgentRecord[] ExportAgents()
    {
        LoadResourceDefinitions();
        var keys = new List<string>(Agents.Keys);
        keys.Sort(StringComparer.Ordinal);
        var result = new PersistentWorldAgentRecord[keys.Count];
        for (int i = 0; i < keys.Count; i++) result[i] = PersistentWorldAgentRecord.Clone(Agents[keys[i]]);
        return result;
    }

    public static NpcMemoryRecord[] ExportNpcs()
    {
        LoadResourceDefinitions();
        var keys = new List<string>(Npcs.Keys);
        keys.Sort(StringComparer.Ordinal);
        var result = new NpcMemoryRecord[keys.Count];
        for (int i = 0; i < keys.Count; i++) result[i] = NpcMemoryRecord.Clone(Npcs[keys[i]]);
        return result;
    }

    public static bool Import(
        PersistentWorldAgentRecord[] agents,
        NpcMemoryRecord[] npcs,
        out string error)
    {
        if (!TryValidate(agents, npcs, out error)) return false;
        LoadResourceDefinitions();
        ResetRuntimeRecordsToDefinitions();
        if (agents != null)
            foreach (var record in agents) Agents[record.agentId] = PersistentWorldAgentRecord.Clone(record);
        if (npcs != null)
            foreach (var record in npcs) Npcs[record.npcId] = NpcMemoryRecord.Clone(record);
        MergeCurrentAuthoring();
        return true;
    }

    public static bool TryValidate(
        PersistentWorldAgentRecord[] agents,
        NpcMemoryRecord[] npcs,
        out string error)
    {
        error = null;
        var agentIds = new HashSet<string>(StringComparer.Ordinal);
        if (agents != null)
        {
            for (int i = 0; i < agents.Length; i++)
            {
                var record = agents[i];
                if (record == null || string.IsNullOrWhiteSpace(record.agentId) ||
                    string.IsNullOrWhiteSpace(record.districtId) || string.IsNullOrWhiteSpace(record.currentSiteId) ||
                    !Enum.IsDefined(typeof(CrierActivityState), record.activityState) ||
                    (record.defeated && record.activityState != CrierActivityState.Defeated))
                {
                    error = $"World-agent record {i} is malformed.";
                    return false;
                }
                if (!agentIds.Add(record.agentId))
                {
                    error = $"World-agent '{record.agentId}' appears more than once.";
                    return false;
                }
            }
        }

        var npcIds = new HashSet<string>(StringComparer.Ordinal);
        if (npcs != null)
        {
            for (int i = 0; i < npcs.Length; i++)
            {
                var record = npcs[i];
                if (record == null || string.IsNullOrWhiteSpace(record.npcId) ||
                    string.IsNullOrWhiteSpace(record.homeDistrictId) ||
                    float.IsNaN(record.exposure) || float.IsInfinity(record.exposure) ||
                    record.exposure < 0f || record.exposure > 100f ||
                    record.susceptibility < 0.5f || record.susceptibility > 1.5f ||
                    !Enum.IsDefined(typeof(NpcMemoryStage), record.stage) ||
                    !Enum.IsDefined(typeof(NpcRecoveryState), record.recoveryState) ||
                    !Enum.IsDefined(typeof(NpcCircleRelevanceLayer), record.relevanceLayer))
                {
                    error = $"NPC memory record {i} is malformed.";
                    return false;
                }
                if (!npcIds.Add(record.npcId))
                {
                    error = $"NPC memory record '{record.npcId}' appears more than once.";
                    return false;
                }
                var expectedStage = StageForExposure(record.exposure);
                if ((record.essentialService || record.questCritical) && expectedStage == NpcMemoryStage.Forgotten)
                    expectedStage = NpcMemoryStage.Unmoored;
                if (record.stage != expectedStage || (record.stage == NpcMemoryStage.Forgotten) != record.forgottenPool)
                {
                    error = $"NPC '{record.npcId}' has inconsistent exposure, stage, or Forgotten-pool state.";
                    return false;
                }
            }
        }
        return true;
    }

    public static bool MarkAgentDiscovered(string agentId, out string error)
    {
        if (!TryGetAgent(agentId, out var record))
        {
            error = $"World agent '{agentId}' is not registered.";
            return false;
        }
        record.discovered = true;
        error = null;
        return true;
    }

    public static bool DefeatAgent(string agentId, out string error)
    {
        if (!TryGetAgent(agentId, out var record))
        {
            error = $"World agent '{agentId}' is not registered.";
            return false;
        }
        record.defeated = true;
        record.activityState = CrierActivityState.Defeated;
        error = null;
        return true;
    }

    public static bool DisruptAgent(string agentId, string dayKey, out string error)
    {
        if (!TryGetAgent(agentId, out var record) || record.defeated)
        {
            error = $"World agent '{agentId}' is unavailable for disruption.";
            return false;
        }
        record.disruptedDayKey = dayKey;
        record.activityState = CrierActivityState.RelocateEvade;
        SelectNextSite(record);
        error = null;
        return true;
    }

    public static bool SetAgentState(string agentId, string state, string dayKey, out string error)
    {
        switch ((state ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "discovered": return MarkAgentDiscovered(agentId, out error);
            case "defeated": return DefeatAgent(agentId, out error);
            case "disrupted": return DisruptAgent(agentId, dayKey, out error);
            case "escaped":
            case "relocate":
                if (!TryGetAgent(agentId, out var record) || record.defeated)
                {
                    error = $"World agent '{agentId}' cannot relocate.";
                    return false;
                }
                record.activityState = CrierActivityState.RelocateEvade;
                SelectNextSite(record);
                error = null;
                return true;
            default:
                error = $"World-agent state '{state}' is not registered.";
                return false;
        }
    }

    public static bool CanSetAgentState(string agentId, string state, out string error)
    {
        if (!TryGetAgent(agentId, out var record))
        {
            error = $"World agent '{agentId}' is not registered.";
            return false;
        }
        string normalized = (state ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized != "discovered" && normalized != "defeated" && normalized != "disrupted" &&
            normalized != "escaped" && normalized != "relocate")
        {
            error = $"World-agent state '{state}' is not registered.";
            return false;
        }
        if (record.defeated && normalized != "defeated")
        {
            error = $"World agent '{agentId}' is permanently defeated.";
            return false;
        }
        error = null;
        return true;
    }

    public static bool ApplyNpcExposureDelta(string npcId, float delta, out string error)
    {
        if (!TryGetNpc(npcId, out var record))
        {
            error = $"NPC memory target '{npcId}' is not registered.";
            return false;
        }
        if (record.forgottenPool && delta < 0f)
        {
            error = $"Forgotten NPC '{npcId}' requires rescue rather than offscreen recovery.";
            return false;
        }
        SetExposure(record, record.exposure + delta);
        error = null;
        return true;
    }

    public static bool CanApplyNpcExposureDelta(string npcId, float delta, out string error)
    {
        if (!TryGetNpc(npcId, out var record))
        {
            error = $"NPC memory target '{npcId}' is not registered.";
            return false;
        }
        if (record.forgottenPool && delta < 0f)
        {
            error = $"Forgotten NPC '{npcId}' requires rescue.";
            return false;
        }
        error = null;
        return true;
    }

    public static bool RescueForgottenNpc(string npcId, string dayKey, out string error)
    {
        if (!TryGetNpc(npcId, out var record) || !record.forgottenPool)
        {
            error = $"NPC '{npcId}' is not in the Forgotten pool.";
            return false;
        }
        record.recoveryState = NpcRecoveryState.AwaitingNextDawn;
        record.rescueRequestedDayKey = dayKey;
        error = null;
        return true;
    }

    public static bool CleanseDistrict(string districtId, float amount, out string error)
    {
        if (string.IsNullOrWhiteSpace(districtId) || amount < 0f || amount > 100f)
        {
            error = "District cleanse requires a district and exposure amount within 0..100.";
            return false;
        }
        LoadResourceDefinitions();
        foreach (var record in Npcs.Values)
            if (record.homeDistrictId == districtId && !record.forgottenPool)
                SetExposure(record, record.exposure - amount);
        error = null;
        return true;
    }

    public static bool CanCleanseDistrict(string districtId, float amount, out string error)
    {
        if (string.IsNullOrWhiteSpace(districtId) || amount < 0f || amount > 100f)
        {
            error = "District cleanse requires a district and exposure amount within 0..100.";
            return false;
        }
        error = null;
        return true;
    }

    public static NpcMemoryStage StageForExposure(float exposure)
    {
        if (exposure >= 80f) return NpcMemoryStage.Forgotten;
        if (exposure >= 50f) return NpcMemoryStage.Unmoored;
        if (exposure >= 25f) return NpcMemoryStage.Distracted;
        return NpcMemoryStage.Grounded;
    }

    public static void SetExposure(NpcMemoryRecord record, float value)
    {
        if (record == null) return;
        float cap = record.essentialService || record.questCritical ? 79f : 100f;
        record.exposure = Mathf.Clamp(value, 0f, cap);
        record.stage = StageForExposure(record.exposure);
        record.forgottenPool = record.stage == NpcMemoryStage.Forgotten;
        if (!record.forgottenPool && record.recoveryState == NpcRecoveryState.AwaitingNextDawn)
            record.recoveryState = NpcRecoveryState.Returned;
    }

    public static void UpdatePermanentSiteMemories(NpcMemoryRecord record, ILimboWorldSimulationSink sink)
    {
        if (record == null || sink == null) return;
        record.erasedMemoryIds ??= Array.Empty<string>();
        record.rememberedMemoryIds ??= Array.Empty<string>();
        foreach (string siteId in record.relevantSiteIds ?? Array.Empty<string>())
        {
            foreach (var outcome in SiteOutcomeState.QuerySite(siteId))
            {
                if (outcome == null || outcome.recovered ||
                    outcome.permanence != NarrativePermanence.CampaignPermanent)
                    continue;
                string memoryId = SiteMemoryId(outcome.siteId, outcome.outcomeId);
                if (outcome.remembered)
                {
                    record.erasedMemoryIds = Remove(record.erasedMemoryIds, memoryId);
                    record.rememberedMemoryIds = AddUnique(record.rememberedMemoryIds, memoryId);
                }
                else if (record.stage >= NpcMemoryStage.Unmoored &&
                         sink.GetLimboInfluence(siteId) >= PermanentMemoryErosionInfluence &&
                         !Contains(record.rememberedMemoryIds, memoryId))
                {
                    record.erasedMemoryIds = AddUnique(record.erasedMemoryIds, memoryId);
                }
            }
        }
    }

    public static void ApplyAuthoredRemembrance(string siteId, string outcomeId)
    {
        LoadResourceDefinitions();
        string memoryId = SiteMemoryId(siteId, outcomeId);
        foreach (var record in Npcs.Values)
        {
            if (!Contains(record.relevantSiteIds, siteId)) continue;
            record.erasedMemoryIds = Remove(record.erasedMemoryIds, memoryId);
            record.rememberedMemoryIds = AddUnique(record.rememberedMemoryIds, memoryId);
        }
    }

    public static string SiteMemoryId(string siteId, string outcomeId) =>
        (siteId ?? string.Empty) + "/" + (outcomeId ?? string.Empty);

    public static void SelectNextSite(PersistentWorldAgentRecord record)
    {
        if (record?.availableSiteIds == null || record.availableSiteIds.Length == 0) return;
        int current = Array.IndexOf(record.availableSiteIds, record.currentSiteId);
        int next = current < 0 ? 0 : (current + 1) % record.availableSiteIds.Length;
        record.currentSiteId = record.availableSiteIds[next];
    }

    static PersistentWorldAgentRecord CreateAgentRecord(WorldAgentDefinition definition)
    {
        string[] sites = CloneStrings(definition.availableSiteIds);
        if (sites.Length == 0) sites = new[] { definition.startingSiteId };
        return new PersistentWorldAgentRecord
        {
            agentId = definition.agentId,
            territoryId = definition.territoryId,
            districtId = definition.districtId,
            currentSiteId = definition.startingSiteId,
            availableSiteIds = sites,
            activityState = CrierActivityState.Travel,
            discovered = definition.startsDiscovered,
        };
    }

    static NpcMemoryRecord CreateNpcRecord(NpcMemoryDefinition definition) => new()
    {
        npcId = definition.npcId,
        homeDistrictId = definition.homeDistrictId,
        homeTerritoryId = definition.homeTerritoryId,
        relevanceLayer = definition.relevanceLayer,
        susceptibility = definition.susceptibility,
        originalScheduleId = definition.originalScheduleId,
        originalRelationshipId = definition.originalRelationshipId,
        overlappingPreachingSiteIds = CloneStrings(definition.overlappingPreachingSiteIds),
        relevantSiteIds = CloneStrings(definition.relevantSiteIds),
        erasedMemoryIds = Array.Empty<string>(),
        rememberedMemoryIds = Array.Empty<string>(),
        essentialService = definition.essentialService,
        questCritical = definition.questCritical,
        stage = NpcMemoryStage.Grounded,
    };

    static void ResetRuntimeRecordsToDefinitions()
    {
        Agents.Clear();
        foreach (var definition in AgentDefinitions.Values) Agents[definition.agentId] = CreateAgentRecord(definition);
        Npcs.Clear();
        foreach (var definition in NpcDefinitions.Values) Npcs[definition.npcId] = CreateNpcRecord(definition);
    }

    static void MergeCurrentAuthoring()
    {
        foreach (var pair in Agents)
            if (AgentDefinitions.TryGetValue(pair.Key, out var definition))
            {
                pair.Value.territoryId = definition.territoryId;
                pair.Value.districtId = definition.districtId;
            }
        foreach (var pair in Npcs)
            if (NpcDefinitions.TryGetValue(pair.Key, out var definition))
            {
                pair.Value.homeTerritoryId = definition.homeTerritoryId;
                pair.Value.relevanceLayer = definition.relevanceLayer;
                pair.Value.relevantSiteIds = CloneStrings(definition.relevantSiteIds);
                pair.Value.erasedMemoryIds ??= Array.Empty<string>();
                pair.Value.rememberedMemoryIds ??= Array.Empty<string>();
            }
    }

    static string[] CloneStrings(string[] values)
    {
        if (values == null || values.Length == 0) return Array.Empty<string>();
        var copy = new string[values.Length];
        Array.Copy(values, copy, values.Length);
        return copy;
    }

    static bool Contains(string[] values, string target)
    {
        foreach (string value in values ?? Array.Empty<string>())
            if (string.Equals(value, target, StringComparison.Ordinal)) return true;
        return false;
    }

    static string[] AddUnique(string[] values, string value)
    {
        values ??= Array.Empty<string>();
        if (Contains(values, value)) return values;
        var result = new string[values.Length + 1];
        Array.Copy(values, result, values.Length);
        result[^1] = value;
        return result;
    }

    static string[] Remove(string[] values, string value)
    {
        values ??= Array.Empty<string>();
        int index = Array.FindIndex(values, candidate => string.Equals(candidate, value, StringComparison.Ordinal));
        if (index < 0) return values;
        var result = new string[values.Length - 1];
        if (index > 0) Array.Copy(values, 0, result, 0, index);
        if (index + 1 < values.Length) Array.Copy(values, index + 1, result, index, values.Length - index - 1);
        return result;
    }
}

public interface ILimboWorldSimulationSink
{
    string ResolveTerritoryId(string locationId);
    float GetSanctity(string districtId);
    float GetLimboInfluence(string districtId);
    float LimboSanctityResistance { get; }
    bool IsSanctuary(string districtId);
    void AddLimboInfluence(string districtId, float amount);
}

public sealed class UnityLimboWorldSimulationSink : ILimboWorldSimulationSink
{
    public static readonly UnityLimboWorldSimulationSink Instance = new();

    public float LimboSanctityResistance =>
        HubMap.Instance?.GetCircleDefinition(CircleId.Limbo)?.sanctityResistance ?? 0.7f;

    public string ResolveTerritoryId(string locationId) =>
        HubMap.Instance?.ResolveInfluenceTerritoryId(locationId) ?? string.Empty;
    public float GetSanctity(string districtId) =>
        HubMap.Instance?.ResolveInfluenceTerritory(districtId)?.sanctity ?? 0f;
    public float GetLimboInfluence(string districtId) =>
        HubMap.Instance?.GetInfluence(districtId, CircleId.Limbo) ?? 0f;
    public bool IsSanctuary(string districtId) => HubMap.Instance?.GetNode(districtId)?.isSanctuarySite ?? false;
    public void AddLimboInfluence(string districtId, float amount) =>
        HubMap.Instance?.ApplyLedgerInfluenceDelta(districtId, CircleId.Limbo, amount);
}

public sealed class LimboWorldDayResult
{
    public int agentsProcessed;
    public int npcsProcessed;
    public float totalInfluenceAdded;
    public readonly Dictionary<string, float> territoryInfluence = new(StringComparer.Ordinal);
    public Dictionary<string, float> districtInfluence => territoryInfluence;
}

public static class LimboWorldDailySimulation
{
    public const float CautiousDailyInfluence = 0.0025f;
    public const float ActiveDailyInfluence = 0.0075f;
    public const float TerritoryDailyCap = 0.02f;
    public const float DistrictDailyCap = TerritoryDailyCap;
    public const float ExposurePerOverlap = 12f;
    public const float DailyExposureCap = 24f;
    public const float OrdinaryRecovery = 8f;
    public const float SanctuaryRecovery = 15f;

    public static LimboWorldDayResult ProcessDay(
        string dayKey,
        IList<PersistentWorldAgentRecord> agents,
        IList<NpcMemoryRecord> npcs,
        ILimboWorldSimulationSink sink)
    {
        if (string.IsNullOrWhiteSpace(dayKey)) throw new ArgumentException("Day key is required.", nameof(dayKey));
        if (sink == null) throw new ArgumentNullException(nameof(sink));
        agents ??= Array.Empty<PersistentWorldAgentRecord>();
        npcs ??= Array.Empty<NpcMemoryRecord>();

        var result = new LimboWorldDayResult();
        var rawByTerritory = new Dictionary<string, float>(StringComparer.Ordinal);
        var eligibleAgents = new List<PersistentWorldAgentRecord>();
        var activeDistricts = new HashSet<string>(StringComparer.Ordinal);

        foreach (var agent in agents)
        {
            if (agent == null || agent.defeated || agent.activityState == CrierActivityState.Defeated) continue;
            bool disruptedToday = string.Equals(agent.disruptedDayKey, dayKey, StringComparison.Ordinal);
            if (!disruptedToday)
            {
                eligibleAgents.Add(agent);
                activeDistricts.Add(agent.districtId);
            }

            if (string.Equals(agent.lastProcessedDayKey, dayKey, StringComparison.Ordinal)) continue;
            agent.lastProcessedDayKey = dayKey;
            result.agentsProcessed++;
            if (disruptedToday) continue;
            if (agent.activityState == CrierActivityState.RelocateEvade)
                agent.activityState = CrierActivityState.Travel;

            float contribution = agent.discovered ? ActiveDailyInfluence : CautiousDailyInfluence;
            string territoryId = sink.ResolveTerritoryId(
                string.IsNullOrWhiteSpace(agent.territoryId) ? agent.districtId : agent.territoryId);
            if (string.IsNullOrEmpty(territoryId)) continue;
            rawByTerritory[territoryId] = rawByTerritory.TryGetValue(territoryId, out float current)
                ? current + contribution
                : contribution;
        }

        foreach (var pair in rawByTerritory)
        {
            float raw = Mathf.Min(TerritoryDailyCap, pair.Value);
            float block = 1f - Mathf.Clamp01(sink.GetSanctity(pair.Key)) *
                Mathf.Clamp01(sink.LimboSanctityResistance);
            float applied = raw * Mathf.Clamp01(block);
            if (applied <= 0f) continue;
            sink.AddLimboInfluence(pair.Key, applied);
            result.territoryInfluence[pair.Key] = applied;
            result.totalInfluenceAdded += applied;
        }

        foreach (var npc in npcs)
        {
            if (npc == null || string.Equals(npc.lastProcessedDayKey, dayKey, StringComparison.Ordinal)) continue;
            npc.lastProcessedDayKey = dayKey;
            result.npcsProcessed++;

            if (npc.recoveryState == NpcRecoveryState.AwaitingNextDawn)
            {
                if (!string.Equals(npc.rescueRequestedDayKey, dayKey, StringComparison.Ordinal))
                {
                    npc.exposure = 70f;
                    npc.stage = NpcMemoryStage.Unmoored;
                    npc.forgottenPool = false;
                    npc.recoveryState = NpcRecoveryState.Returned;
                    npc.rescueRequestedDayKey = string.Empty;
                }
                PersistentLimboWorldState.UpdatePermanentSiteMemories(npc, sink);
                continue;
            }
            if (npc.forgottenPool)
            {
                PersistentLimboWorldState.UpdatePermanentSiteMemories(npc, sink);
                continue;
            }

            float overlapWeight = 0f;
            foreach (var agent in eligibleAgents)
            {
                if (!string.Equals(agent.districtId, npc.homeDistrictId, StringComparison.Ordinal) ||
                    !Contains(npc.overlappingPreachingSiteIds, agent.currentSiteId))
                    continue;
                overlapWeight += agent.discovered ? 1f : 0.5f;
            }

            if (overlapWeight > 0f)
            {
                float block = 1f - Mathf.Clamp01(sink.GetSanctity(npc.homeDistrictId)) *
                    Mathf.Clamp01(sink.LimboSanctityResistance);
                float exposure = Mathf.Min(DailyExposureCap,
                    ExposurePerOverlap * overlapWeight * Mathf.Clamp(npc.susceptibility, 0.5f, 1.5f) *
                    Mathf.Clamp01(block));
                PersistentLimboWorldState.SetExposure(npc, npc.exposure + exposure);
            }
            else if (!activeDistricts.Contains(npc.homeDistrictId) &&
                     sink.GetLimboInfluence(npc.homeDistrictId) < 0.5f)
            {
                float recovery = sink.IsSanctuary(npc.homeDistrictId)
                    ? SanctuaryRecovery
                    : OrdinaryRecovery;
                PersistentLimboWorldState.SetExposure(npc, npc.exposure - recovery);
            }
            PersistentLimboWorldState.UpdatePermanentSiteMemories(npc, sink);
        }

        return result;
    }

    static bool Contains(string[] values, string target)
    {
        if (values == null || string.IsNullOrEmpty(target)) return false;
        foreach (string value in values)
            if (string.Equals(value, target, StringComparison.Ordinal)) return true;
        return false;
    }
}

[DefaultExecutionOrder(-80)]
public sealed class LimboWorldSimulationDirector : MonoBehaviour
{
    public static LimboWorldSimulationDirector Instance { get; private set; }
    // Retained only to migrate v5 day keys into CircleWorldSimulationDirector.
    public string AppliedDayKey { get; private set; } = string.Empty;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureRuntimeDirector()
    {
        if (FindAnyObjectByType<LimboWorldSimulationDirector>() != null) return;
        var gameObject = new GameObject("[Limbo World Simulation]");
        DontDestroyOnLoad(gameObject);
        gameObject.AddComponent<LimboWorldSimulationDirector>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        PersistentLimboWorldState.LoadResourceDefinitions();
        ExplorationDiscoveryLedger.LoadResourceDefinitions();
    }

    void Start() => PersistentCrierMaterializer.MaterializeCurrentScene();

    void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    public void RestoreDayKey(string dayKey) => AppliedDayKey = dayKey ?? string.Empty;

    public LimboWorldDayResult ProcessClosedDay(string closedDayKey)
    {
        var result = LimboWorldDailySimulation.ProcessDay(
            closedDayKey,
            PersistentLimboWorldState.MutableAgents(),
            PersistentLimboWorldState.MutableNpcs(),
            UnityLimboWorldSimulationSink.Instance);
        Debug.Log($"[LimboWorld] {closedDayKey}: {result.agentsProcessed} agent(s), " +
                  $"{result.npcsProcessed} NPC(s), +{result.totalInfluenceAdded * 100f:0.##} Limbo points.");
        return result;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode) => PersistentCrierMaterializer.MaterializeCurrentScene();
}

public enum WorldAgentSiteRole
{
    Preach = 0,
    Hide = 1,
    Transit = 2,
}

public static class PersistentCrierMaterializer
{
    public static void MaterializeCurrentScene()
    {
        PersistentLimboWorldState.LoadResourceDefinitions();
        var sites = UnityEngine.Object.FindObjectsByType<WorldAgentSite>(FindObjectsInactive.Exclude);
        if (sites.Length == 0) return;
        var actors = UnityEngine.Object.FindObjectsByType<PersistentCrierActor>(FindObjectsInactive.Include);

        foreach (var record in PersistentLimboWorldState.MutableAgents())
        {
            if (record.defeated || HasActor(actors, record.agentId)) continue;
            WorldAgentSite site = null;
            foreach (var candidate in sites)
                if (candidate.siteId == record.currentSiteId) { site = candidate; break; }
            if (site == null) continue;
            if (!PersistentLimboWorldState.TryGetAgentDefinition(record.agentId, out var definition) ||
                definition.worldPrefab == null)
                continue;

            var instance = UnityEngine.Object.Instantiate(
                definition.worldPrefab, site.transform.position, site.transform.rotation);
            var actor = instance.GetComponent<PersistentCrierActor>() ?? instance.AddComponent<PersistentCrierActor>();
            actor.agentId = record.agentId;
        }
    }

    static bool HasActor(PersistentCrierActor[] actors, string agentId)
    {
        foreach (var actor in actors)
            if (actor != null && actor.agentId == agentId) return true;
        return false;
    }
}
