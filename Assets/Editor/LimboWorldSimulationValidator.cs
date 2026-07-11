using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class LimboWorldSimulationValidator
{
    [MenuItem("InfernosCurse/Validation/Validate Limbo World Simulation")]
    public static void Validate()
    {
        var errors = new List<string>();
        try
        {
            ValidateInfluence(errors);
            ValidateExposure(errors);
            ValidateRecovery(errors);
            ValidatePresentationOverlays(errors);
            ValidateLoadedUnloadedParity(errors);
            ValidatePersistentStores(errors);
            ValidateDiscovery(errors);
            ValidateRegisteredEffects(errors);
            ValidateOpeningBaseline(errors);
            ValidateSaveShape(errors);
        }
        finally
        {
            PersistentLimboWorldState.Reset();
            ExplorationDiscoveryLedger.Reset();
        }

        if (errors.Count > 0)
        {
            foreach (string error in errors) Debug.LogError("[LimboWorldSimulationValidator] " + error);
            throw new InvalidOperationException($"Limbo world simulation validation failed with {errors.Count} error(s). ");
        }
        Debug.Log("[LimboWorldSimulationValidator] Validation passed: Crier caps/idempotence, Unmooring stages/recovery, loaded-unloaded parity, discovery monotonicity, and v5 save state.");
    }

    static void ValidateInfluence(List<string> errors)
    {
        var agents = new List<PersistentWorldAgentRecord>();
        for (int i = 0; i < 4; i++) agents.Add(Agent($"active_{i}", true, "market_site"));
        var sink = new FakeSink();
        var result = LimboWorldDailySimulation.ProcessDay("1265:90", agents, null, sink);
        ExpectNear(result.totalInfluenceAdded, 0.02f, "four-Crier district cap", errors);
        ExpectNear(sink.Influence("mercato"), 0.02f, "district influence application", errors);
        Expect(result.agentsProcessed == 4, "Not all active agents were processed.", errors);

        LimboWorldDailySimulation.ProcessDay("1265:90", agents, null, sink);
        ExpectNear(sink.Influence("mercato"), 0.02f, "same-day agent idempotence", errors);

        var cautiousSink = new FakeSink();
        LimboWorldDailySimulation.ProcessDay(
            "1265:91", new[] { Agent("cautious", false, "market_site") }, null, cautiousSink);
        ExpectNear(cautiousSink.Influence("mercato"), 0.0025f, "cautious Crier contribution", errors);

        var sanctified = new FakeSink { resistance = 0.7f };
        sanctified.sanctity["mercato"] = 1f;
        LimboWorldDailySimulation.ProcessDay(
            "1265:92", new[] { Agent("sanctified", true, "market_site") }, null, sanctified);
        ExpectNear(sanctified.Influence("mercato"), 0.00225f, "sanctity-reduced Crier contribution", errors);

        var disrupted = Agent("disrupted", true, "market_site");
        disrupted.disruptedDayKey = "1265:93";
        var disruptedSink = new FakeSink();
        LimboWorldDailySimulation.ProcessDay("1265:93", new[] { disrupted }, null, disruptedSink);
        ExpectNear(disruptedSink.Influence("mercato"), 0f, "same-day disruption suppression", errors);

        var defeated = Agent("defeated", true, "market_site");
        defeated.defeated = true;
        defeated.activityState = CrierActivityState.Defeated;
        var defeatedSink = new FakeSink();
        LimboWorldDailySimulation.ProcessDay("1265:94", new[] { defeated }, null, defeatedSink);
        ExpectNear(defeatedSink.Influence("mercato"), 0f, "defeated-agent suppression", errors);
    }

    static void ValidateExposure(List<string> errors)
    {
        var activeNpc = Npc("npc_active", 0f, "market_site");
        LimboWorldDailySimulation.ProcessDay(
            "1265:100", new[] { Agent("active", true, "market_site") }, new[] { activeNpc }, new FakeSink());
        ExpectNear(activeNpc.exposure, 12f, "active Crier exposure", errors);

        var cautiousNpc = Npc("npc_cautious", 0f, "market_site");
        LimboWorldDailySimulation.ProcessDay(
            "1265:101", new[] { Agent("cautious", false, "market_site") }, new[] { cautiousNpc }, new FakeSink());
        ExpectNear(cautiousNpc.exposure, 6f, "cautious half-weight exposure", errors);

        var cappedNpc = Npc("npc_cap", 0f, "market_site");
        cappedNpc.susceptibility = 1.5f;
        LimboWorldDailySimulation.ProcessDay(
            "1265:102",
            new[]
            {
                Agent("a", true, "market_site"),
                Agent("b", true, "market_site"),
                Agent("c", true, "market_site"),
            },
            new[] { cappedNpc }, new FakeSink());
        ExpectNear(cappedNpc.exposure, 24f, "daily exposure cap", errors);

        var sanctifiedNpc = Npc("npc_sanctified", 0f, "market_site");
        var sanctified = new FakeSink { resistance = 0.7f };
        sanctified.sanctity["mercato"] = 1f;
        LimboWorldDailySimulation.ProcessDay(
            "1265:103", new[] { Agent("a", true, "market_site") }, new[] { sanctifiedNpc }, sanctified);
        ExpectNear(sanctifiedNpc.exposure, 3.6f, "sanctity-reduced exposure", errors);

        ExpectStage(24f, NpcMemoryStage.Grounded, false, errors);
        ExpectStage(25f, NpcMemoryStage.Distracted, false, errors);
        ExpectStage(49f, NpcMemoryStage.Distracted, false, errors);
        ExpectStage(50f, NpcMemoryStage.Unmoored, false, errors);
        ExpectStage(79f, NpcMemoryStage.Unmoored, false, errors);
        ExpectStage(80f, NpcMemoryStage.Forgotten, true, errors);

        var critical = Npc("critical", 0f, "market_site");
        critical.questCritical = true;
        PersistentLimboWorldState.SetExposure(critical, 100f);
        ExpectNear(critical.exposure, 79f, "critical NPC exposure cap", errors);
        Expect(critical.stage == NpcMemoryStage.Unmoored && !critical.forgottenPool,
            "Critical NPC entered the Forgotten pool.", errors);
    }

    static void ValidateRecovery(List<string> errors)
    {
        var ordinary = Npc("ordinary", 50f, "market_site");
        var ordinarySink = new FakeSink();
        ordinarySink.influence["mercato"] = 0.49f;
        LimboWorldDailySimulation.ProcessDay("1265:110", null, new[] { ordinary }, ordinarySink);
        ExpectNear(ordinary.exposure, 42f, "ordinary passive recovery", errors);

        var sanctuaryNpc = Npc("sanctuary", 50f, "market_site");
        var sanctuarySink = new FakeSink();
        sanctuarySink.influence["mercato"] = 0.49f;
        sanctuarySink.sanctuary.Add("mercato");
        LimboWorldDailySimulation.ProcessDay("1265:111", null, new[] { sanctuaryNpc }, sanctuarySink);
        ExpectNear(sanctuaryNpc.exposure, 35f, "sanctuary recovery", errors);

        var blocked = Npc("blocked", 50f, "market_site");
        var blockedSink = new FakeSink();
        blockedSink.influence["mercato"] = 0.5f;
        LimboWorldDailySimulation.ProcessDay("1265:112", null, new[] { blocked }, blockedSink);
        ExpectNear(blocked.exposure, 50f, "high-Limbo recovery block", errors);

        var activeDistrictNpc = Npc("active_district", 50f, "other_site");
        var activeDistrictSink = new FakeSink();
        activeDistrictSink.influence["mercato"] = 0.1f;
        LimboWorldDailySimulation.ProcessDay(
            "1265:113", new[] { Agent("active", true, "market_site") },
            new[] { activeDistrictNpc }, activeDistrictSink);
        ExpectNear(activeDistrictNpc.exposure, 50f, "active-district recovery suppression", errors);

        var forgotten = Npc("forgotten", 80f, "market_site");
        LimboWorldDailySimulation.ProcessDay("1265:114", null, new[] { forgotten }, new FakeSink());
        ExpectNear(forgotten.exposure, 80f, "Forgotten offscreen recovery block", errors);

        forgotten.recoveryState = NpcRecoveryState.AwaitingNextDawn;
        forgotten.rescueRequestedDayKey = "1265:115";
        forgotten.lastProcessedDayKey = string.Empty;
        LimboWorldDailySimulation.ProcessDay("1265:115", null, new[] { forgotten }, new FakeSink());
        ExpectNear(forgotten.exposure, 80f, "same-day rescue returned too early", errors);
        LimboWorldDailySimulation.ProcessDay("1265:116", null, new[] { forgotten }, new FakeSink());
        ExpectNear(forgotten.exposure, 70f, "next-dawn rescue exposure", errors);
        Expect(forgotten.stage == NpcMemoryStage.Unmoored && !forgotten.forgottenPool,
            "Rescued NPC did not leave the Forgotten pool.", errors);
    }

    static void ValidateLoadedUnloadedParity(List<string> errors)
    {
        var agentsA = new[] { Agent("parity_crier", false, "market_site") };
        var npcsA = new[] { Npc("parity_npc", 10f, "market_site") };
        var agentsB = new[] { PersistentWorldAgentRecord.Clone(agentsA[0]) };
        var npcsB = new[] { NpcMemoryRecord.Clone(npcsA[0]) };
        var sinkA = new FakeSink();
        var sinkB = new FakeSink();

        LimboWorldDailySimulation.ProcessDay("1265:120", agentsA, npcsA, sinkA);
        LimboWorldDailySimulation.ProcessDay("1265:120", agentsB, npcsB, sinkB);
        ExpectNear(sinkA.Influence("mercato"), sinkB.Influence("mercato"), "loaded/unloaded influence parity", errors);
        ExpectNear(npcsA[0].exposure, npcsB[0].exposure, "loaded/unloaded exposure parity", errors);
    }

    static void ValidatePresentationOverlays(List<string> errors)
    {
        var grounded = NpcUnmooringPresentation.Evaluate(Npc("overlay_grounded", 0f, "site"), "Agnolo", "1265:1");
        Expect(grounded.displayName == "Agnolo" && grounded.dialogueMode == NpcDialogueOverlayMode.Original &&
               grounded.useOriginalSchedule,
            "Grounded presentation did not preserve authored state.", errors);

        var distracted = NpcUnmooringPresentation.Evaluate(Npc("overlay_distracted", 25f, "site"), "Agnolo", "1265:2");
        Expect(distracted.dialogueMode == NpcDialogueOverlayMode.Fragmented &&
               distracted.arrivalDelayHours >= 1 && distracted.arrivalDelayHours <= 2 &&
               distracted.useOriginalSchedule,
            "Distracted presentation did not apply fragmented/late overlay.", errors);

        var essential = Npc("overlay_essential", 50f, "site");
        essential.essentialService = true;
        var unmoored = NpcUnmooringPresentation.Evaluate(essential, "Innkeeper", "1265:3");
        Expect(unmoored.dialogueMode == NpcDialogueOverlayMode.Dislocated &&
               !unmoored.useOriginalSchedule && unmoored.useBackupServiceAccess,
            "Unmoored essential NPC did not retain backup service access.", errors);

        var forgotten = NpcUnmooringPresentation.Evaluate(Npc("overlay_forgotten", 80f, "site"), "Agnolo", "1265:4");
        Expect(forgotten.displayName == "..." && forgotten.dialogueMode == NpcDialogueOverlayMode.Absent &&
               forgotten.inForgottenPool && !forgotten.useOriginalSchedule,
            "Forgotten presentation did not hide identity/schedule non-destructively.", errors);
    }

    static void ValidatePersistentStores(List<string> errors)
    {
        PersistentLimboWorldState.Reset();
        PersistentLimboWorldState.LoadResourceDefinitions();
        var agentDefinition = ScriptableObject.CreateInstance<WorldAgentDefinition>();
        agentDefinition.agentId = "validator_crier";
        agentDefinition.districtId = "mercato";
        agentDefinition.startingSiteId = "site_a";
        agentDefinition.availableSiteIds = new[] { "site_a", "site_b" };
        var npcDefinition = ScriptableObject.CreateInstance<NpcMemoryDefinition>();
        npcDefinition.npcId = "validator_npc";
        npcDefinition.homeDistrictId = "mercato";
        npcDefinition.susceptibility = 1f;
        npcDefinition.overlappingPreachingSiteIds = new[] { "site_a" };

        if (!PersistentLimboWorldState.RegisterAgentDefinition(agentDefinition, out string error))
            errors.Add("Agent definition registration failed: " + error);
        if (!PersistentLimboWorldState.RegisterNpcDefinition(npcDefinition, out error))
            errors.Add("NPC definition registration failed: " + error);

        PersistentLimboWorldState.TryGetNpc("validator_npc", out var npc);
        PersistentLimboWorldState.SetExposure(npc, 60f);
        PersistentLimboWorldState.CleanseDistrict("mercato", 20f, out _);
        ExpectNear(npc.exposure, 40f, "district cleanse amount", errors);
        PersistentLimboWorldState.SetExposure(npc, 85f);
        PersistentLimboWorldState.CleanseDistrict("mercato", 20f, out _);
        ExpectNear(npc.exposure, 85f, "Forgotten cleanse immunity", errors);
        Expect(PersistentLimboWorldState.RescueForgottenNpc("validator_npc", "1265:130", out _),
            "Forgotten rescue could not be scheduled.", errors);

        Expect(PersistentLimboWorldState.MarkAgentDiscovered("validator_crier", out _),
            "Agent discovery failed.", errors);
        PersistentLimboWorldState.TryGetAgent("validator_crier", out var agent);
        string firstSite = agent.currentSiteId;
        Expect(PersistentLimboWorldState.DisruptAgent("validator_crier", "1265:130", out _),
            "Agent disruption failed.", errors);
        Expect(agent.currentSiteId != firstSite && agent.disruptedDayKey == "1265:130",
            "Disruption did not relocate and stamp the agent.", errors);

        var exportedAgents = PersistentLimboWorldState.ExportAgents();
        var exportedNpcs = PersistentLimboWorldState.ExportNpcs();
        Expect(PersistentLimboWorldState.Import(exportedAgents, exportedNpcs, out error),
            "Persistent world-state round-trip failed: " + error, errors);
        PersistentLimboWorldState.TryGetAgent("validator_crier", out agent);
        Expect(agent.discovered && agent.currentSiteId == "site_b", "Agent state did not round-trip.", errors);

        UnityEngine.Object.DestroyImmediate(agentDefinition);
        UnityEngine.Object.DestroyImmediate(npcDefinition);
    }

    static void ValidateDiscovery(List<string> errors)
    {
        ExplorationDiscoveryLedger.Reset();
        ExplorationDiscoveryLedger.LoadResourceDefinitions();
        var definition = ScriptableObject.CreateInstance<ExplorationDiscoveryDefinition>();
        definition.discoveryId = "validator_hidden_poi";
        definition.kind = DiscoveryKind.PointOfInterest;
        if (!ExplorationDiscoveryLedger.Register(definition, out string error))
        {
            errors.Add("Discovery definition registration failed: " + error);
            UnityEngine.Object.DestroyImmediate(definition);
            return;
        }

        Expect(!ExplorationDiscoveryLedger.IsVisible("validator_hidden_poi", DiscoveryStage.Discovered),
            "Hidden POI began visible.", errors);
        ExplorationDiscoveryLedger.Advance(
            "validator_hidden_poi", DiscoveryKind.PointOfInterest, DiscoveryStage.Rumored,
            "event_1", "1265:140", out _, out _);
        ExplorationDiscoveryLedger.Advance(
            "validator_hidden_poi", DiscoveryKind.PointOfInterest, DiscoveryStage.Located,
            "event_2", "1265:141", out _, out _);
        ExplorationDiscoveryLedger.Advance(
            "validator_hidden_poi", DiscoveryKind.PointOfInterest, DiscoveryStage.Rumored,
            "event_old", "1265:142", out bool regressed, out _);
        Expect(!regressed && ExplorationDiscoveryLedger.GetStage("validator_hidden_poi") == DiscoveryStage.Located,
            "Discovery stage regressed.", errors);
        ExplorationDiscoveryLedger.Advance(
            "validator_hidden_poi", DiscoveryKind.PointOfInterest, DiscoveryStage.Discovered,
            "event_3", "1265:143", out _, out _);
        Expect(ExplorationDiscoveryLedger.IsVisible("validator_hidden_poi", DiscoveryStage.Discovered),
            "Discovered POI remained hidden.", errors);

        var exported = ExplorationDiscoveryLedger.Export();
        Expect(ExplorationDiscoveryLedger.Import(exported, out error),
            "Discovery round-trip failed: " + error, errors);
        Expect(ExplorationDiscoveryLedger.GetStage("validator_hidden_poi") == DiscoveryStage.Discovered,
            "Discovery stage did not round-trip.", errors);
        Expect(ExplorationDiscoveryLedger.Import(null, out error),
            "Legacy empty discovery import failed: " + error, errors);
        Expect(ExplorationDiscoveryLedger.GetStage("validator_hidden_poi") == DiscoveryStage.Hidden,
            "Empty older save leaked current discovery state.", errors);
        Expect(!ExplorationDiscoveryLedger.CanAdvance(
                "unregistered_poi", DiscoveryKind.PointOfInterest, DiscoveryStage.Rumored, out _),
            "Unregistered discovery ID was accepted.", errors);

        UnityEngine.Object.DestroyImmediate(definition);
    }

    static void ValidateSaveShape(List<string> errors)
    {
        var save = new SaveData
        {
            saveVersion = SaveSystem.CURRENT_VERSION,
            worldSimulationDayKey = "1265:150",
            worldAgents = PersistentLimboWorldState.ExportAgents(),
            npcMemory = PersistentLimboWorldState.ExportNpcs(),
            discoveries = ExplorationDiscoveryLedger.Export(),
        };
        var restored = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save));
        Expect(restored != null && restored.worldSimulationDayKey == "1265:150" &&
               restored.worldAgents != null && restored.npcMemory != null && restored.discoveries != null,
            "v5 unloaded-world save state did not round-trip.", errors);
        Expect(SaveSystem.CURRENT_VERSION >= 5, "Save version was not advanced for unloaded world state.", errors);
    }

    static void ValidateOpeningBaseline(List<string> errors)
    {
        foreach (var pair in FlorenceOpeningBaseline.LimboByLocation)
        {
            if (pair.Key == "wp_mugnone") continue;
            Expect(pair.Value >= 0.02f && pair.Value <= 0.15f,
                $"Opening baseline '{pair.Key}' is outside the approved low-burn range.", errors);
        }
        ExpectNear(FlorenceOpeningBaseline.LimboByLocation["mercato"], 0.15f,
            "Mercato troubled opening baseline", errors);
        Expect(FlorenceOpeningBaseline.LimboByLocation["duomo"] <= 0.10f,
            "Stable Duomo opening baseline is too high.", errors);
    }

    static void ValidateRegisteredEffects(List<string> errors)
    {
        PersistentLimboWorldState.Reset();
        ExplorationDiscoveryLedger.Reset();
        PersistentLimboWorldState.LoadResourceDefinitions();
        ExplorationDiscoveryLedger.LoadResourceDefinitions();

        var effects = new[]
        {
            new WorldConsequenceEffect
            {
                effectId = WorldEffectIds.NpcExposureDelta,
                targetId = "npc_agnolo_neighbor",
                numericValue = 25f,
            },
            new WorldConsequenceEffect
            {
                effectId = WorldEffectIds.RumorStageUnlock,
                targetId = "rumor_limbo_bells_mercato",
                intValue = (int)DiscoveryStage.Rumored,
                secondaryId = "validator_event",
            },
            new WorldConsequenceEffect
            {
                effectId = WorldEffectIds.WorldAgentState,
                targetId = "limbo_crier_mercato_01",
                stringValue = "discovered",
            },
        };

        if (!UnityWorldConsequenceSink.Instance.TryApplyBatch(effects, out string error))
        {
            errors.Add("Registered world-effect batch failed: " + error);
            return;
        }
        PersistentLimboWorldState.TryGetNpc("npc_agnolo_neighbor", out var npc);
        PersistentLimboWorldState.TryGetAgent("limbo_crier_mercato_01", out var agent);
        ExpectNear(npc.exposure, 25f, "registered NPC exposure effect", errors);
        Expect(npc.stage == NpcMemoryStage.Distracted, "Exposure effect did not update Unmooring stage.", errors);
        Expect(agent.discovered, "Registered world-agent discovery effect failed.", errors);
        Expect(ExplorationDiscoveryLedger.GetStage("rumor_limbo_bells_mercato") == DiscoveryStage.Rumored,
            "Registered rumor unlock effect failed.", errors);

        var cleanse = new[]
        {
            new WorldConsequenceEffect
            {
                effectId = WorldEffectIds.DistrictNpcCleanse,
                targetId = "mercato",
                numericValue = 20f,
            },
        };
        if (!UnityWorldConsequenceSink.Instance.TryApplyBatch(cleanse, out error))
            errors.Add("Registered district cleanse failed: " + error);
        ExpectNear(npc.exposure, 5f, "registered district cleanse effect", errors);

        PersistentLimboWorldState.SetExposure(npc, 85f);
        var rescue = new[]
        {
            new WorldConsequenceEffect
            {
                effectId = WorldEffectIds.NpcRescue,
                targetId = "npc_agnolo_neighbor",
            },
        };
        if (!UnityWorldConsequenceSink.Instance.TryApplyBatch(rescue, out error))
            errors.Add("Registered Forgotten rescue failed: " + error);
        Expect(npc.recoveryState == NpcRecoveryState.AwaitingNextDawn,
            "Forgotten rescue did not schedule next-dawn return.", errors);
    }

    static PersistentWorldAgentRecord Agent(string id, bool discovered, string site) => new()
    {
        agentId = id,
        districtId = "mercato",
        currentSiteId = site,
        availableSiteIds = new[] { site },
        discovered = discovered,
        activityState = CrierActivityState.Preach,
    };

    static NpcMemoryRecord Npc(string id, float exposure, string overlapSite)
    {
        var record = new NpcMemoryRecord
        {
            npcId = id,
            homeDistrictId = "mercato",
            susceptibility = 1f,
            overlappingPreachingSiteIds = new[] { overlapSite },
        };
        PersistentLimboWorldState.SetExposure(record, exposure);
        return record;
    }

    static void ExpectStage(float exposure, NpcMemoryStage stage, bool forgotten, List<string> errors)
    {
        var record = Npc("threshold_" + exposure, exposure, "site");
        Expect(record.stage == stage && record.forgottenPool == forgotten,
            $"Exposure {exposure} produced {record.stage}/{record.forgottenPool}.", errors);
    }

    static void ExpectNear(float actual, float expected, string label, List<string> errors)
    {
        if (Mathf.Abs(actual - expected) > 0.0001f)
            errors.Add($"{label}: expected {expected:F4}, got {actual:F4}.");
    }

    static void Expect(bool condition, string message, List<string> errors)
    {
        if (!condition) errors.Add(message);
    }

    sealed class FakeSink : ILimboWorldSimulationSink
    {
        public readonly Dictionary<string, float> influence = new(StringComparer.Ordinal);
        public readonly Dictionary<string, float> sanctity = new(StringComparer.Ordinal);
        public readonly HashSet<string> sanctuary = new(StringComparer.Ordinal);
        public float resistance = 0.7f;

        public float LimboSanctityResistance => resistance;
        public float GetSanctity(string districtId) => sanctity.TryGetValue(districtId, out float value) ? value : 0f;
        public float GetLimboInfluence(string districtId) => Influence(districtId);
        public bool IsSanctuary(string districtId) => sanctuary.Contains(districtId);
        public void AddLimboInfluence(string districtId, float amount) =>
            influence[districtId] = Influence(districtId) + amount;
        public float Influence(string districtId) =>
            influence.TryGetValue(districtId, out float value) ? value : 0f;
    }
}
