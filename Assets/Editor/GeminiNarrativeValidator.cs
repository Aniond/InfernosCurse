using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class GeminiNarrativeValidator
{
    [MenuItem("InfernosCurse/Validation/Validate Gemini Narrative Director")]
    public static void Validate()
    {
        var errors = new List<string>();
        GameObject hubObject = null;
        try
        {
            SeedCanonicalState(out hubObject, errors);
            var request = BuildRequest(circleVocabularyUnlocked: true);
            ValidateContext(request, errors);
            ValidateSchema(request, errors);
            ValidateLockedVocabulary(errors);
            ValidateFallback(request, errors);
        }
        finally
        {
            WorldEventLedger.Reset();
            PersistentLimboWorldState.Reset();
            ExplorationDiscoveryLedger.Reset();
            if (hubObject != null) UnityEngine.Object.DestroyImmediate(hubObject);
        }

        if (errors.Count > 0)
        {
            foreach (string error in errors) Debug.LogError("[GeminiNarrativeValidator] " + error);
            throw new InvalidOperationException($"Gemini narrative validation failed with {errors.Count} error(s). ");
        }
        Debug.Log("[GeminiNarrativeValidator] Validation passed: bounded context, strict schema/IDs, locked vocabulary, deterministic fallback, and state-invariant AI output.");
    }

    static void SeedCanonicalState(out GameObject hubObject, List<string> errors)
    {
        PersistentLimboWorldState.Reset();
        PersistentLimboWorldState.LoadResourceDefinitions();
        if (!PersistentLimboWorldState.TryGetNpc("npc_agnolo_neighbor", out var npc))
            errors.Add("Production neighbor NPC definition was unavailable to narrative context.");
        else
            PersistentLimboWorldState.SetExposure(npc, 25f);

        ExplorationDiscoveryLedger.Reset();
        ExplorationDiscoveryLedger.LoadResourceDefinitions();
        if (!ExplorationDiscoveryLedger.Advance(
                "rumor_limbo_bells_mercato", DiscoveryKind.Rumor, DiscoveryStage.Rumored,
                "opening_bell_rumor", "1265:90", out _, out string discoveryError))
            errors.Add("Production bell rumor could not be seeded: " + discoveryError);

        var records = new List<WorldEventRecord>();
        for (int i = 0; i < 25; i++)
        {
            records.Add(new WorldEventRecord
            {
                eventInstanceId = "narrative_validator_" + i,
                eventTypeId = "neighbor_memory_incident",
                gameDateKey = "1265:" + (60 + i),
                locationId = "mercato",
                npcIds = new[] { "npc_agnolo_neighbor" },
                worldAgentIds = Array.Empty<string>(),
                choiceId = "listen_carefully",
                consequences = Array.Empty<WorldConsequenceEffect>(),
                factualOutcome = "FACT_" + i.ToString("00") + ": a remembered local detail remained canonical.",
                semanticTags = new[] { "remembered_a_name" },
            });
        }
        records.Add(new WorldEventRecord
        {
            eventInstanceId = "irrelevant_duomo_event",
            eventTypeId = "cathedral_scaffold",
            gameDateKey = "1265:89",
            locationId = "duomo",
            npcIds = new[] { "npc_unrelated" },
            worldAgentIds = Array.Empty<string>(),
            choiceId = "inspect_scaffold",
            consequences = Array.Empty<WorldConsequenceEffect>(),
            factualOutcome = "IRRELEVANT_DUOMO_FACT",
            semanticTags = new[] { "unrelated" },
        });
        if (!WorldEventLedger.Import(records.ToArray(), out string ledgerError))
            errors.Add("Canonical narrative fixture ledger failed import: " + ledgerError);

        hubObject = new GameObject("NarrativeValidatorHub");
        var hub = hubObject.AddComponent<HubMap>();
        hub.nodeData = new List<HubNodeData>
        {
            new HubNodeData
            {
                id = "firenze",
                displayName = "Florence",
                nativeCircle = CircleId.Limbo,
                startingCurseLevel = 0.15f,
                ownsCircleState = true,
                territoryKind = TerritoryKind.City,
                neighborIds = new List<string> { "mercato" },
            },
            new HubNodeData
            {
                id = "mercato",
                displayName = "Mercato Vecchio",
                nativeCircle = CircleId.Limbo,
                influenceTerritoryId = "firenze",
                neighborIds = new List<string> { "firenze" },
            },
        };
        hub.EnsureGraphBuilt();
    }

    static NarrativeRequest BuildRequest(bool circleVocabularyUnlocked) => new()
    {
        requestId = circleVocabularyUnlocked ? "mercato_after_crier" : "neighbor_name_lapse",
        eventTypeId = "neighbor_memory_incident",
        locationId = "mercato",
        npcId = "npc_agnolo_neighbor",
        playerUtterance = "Tell me what you remember.",
        relevantTags = new[] { "remembered_a_name" },
        allowedFollowupIds = new[] { "followup_shared_memory", "followup_ask_about_bells" },
        circleVocabularyUnlocked = circleVocabularyUnlocked,
        fallbackProse = circleVocabularyUnlocked
            ? "Agnolo presses two fingers to his temple. The memory is there, but it will not hold still."
            : "Agnolo knows your face, yet your name slips away each time he reaches for it.",
        fallbackReaction = "confused",
        fallbackFollowupId = "followup_shared_memory",
        forbiddenClaims = new[] { "Agnolo never existed" },
    };

    static void ValidateContext(NarrativeRequest request, List<string> errors)
    {
        if (!NarrativeContextBuilder.TryBuildPrompt(
                request, out string prompt, out string hash, out string error))
        {
            errors.Add("Bounded narrative context failed: " + error);
            return;
        }
        if (!NarrativeContextBuilder.TryBuildPrompt(
                request, out string secondPrompt, out string secondHash, out error))
            errors.Add("Second bounded context build failed: " + error);
        Expect(prompt == secondPrompt && hash == secondHash, "Identical canonical context produced a different hash.", errors);
        Expect(prompt.Length <= GeminiNarrativeSchema.MaxPromptCharacters, "Narrative prompt exceeded its hard bound.", errors);
        Expect(prompt.Contains("FACT_24") && !prompt.Contains("FACT_04"),
            "Recent-event selector did not keep exactly the newest 20 relevant records.", errors);
        Expect(!prompt.Contains("IRRELEVANT_DUOMO_FACT"), "Unrelated event leaked into narrative context.", errors);
        Expect(prompt.Contains("npcMemoryStage=\"Distracted\""), "NPC memory stage was omitted from context.", errors);
        Expect(prompt.Contains("Limbo:limbo_symptom_familiarity_slips"),
            "Unlocked authored Circle symptom was omitted from context.", errors);
        Expect(!prompt.Contains("Limbo:15%"),
            "Hidden numeric Circle influence leaked into narrative context.", errors);
        Expect(prompt.Contains("rumor_limbo_bells_mercato:Rumored"), "Known rumor was omitted from context.", errors);
        Expect(!prompt.Contains("poi_roman_florentia_stone"), "Hidden cultural POI leaked into context.", errors);
        Expect(prompt.IndexOf("apiKey", StringComparison.OrdinalIgnoreCase) < 0 &&
               prompt.IndexOf("Bearer ", StringComparison.OrdinalIgnoreCase) < 0,
            "Credential-shaped data appeared in narrative context.", errors);
    }

    static void ValidateSchema(NarrativeRequest request, List<string> errors)
    {
        const string valid =
            "{\"prose\":\"Agnolo grips the old token and whispers your name.\",\"reaction\":\"confused\",\"selectedFollowupId\":\"followup_shared_memory\",\"derivedSummary\":\"A shared token briefly steadied Agnolo's memory.\"}";
        Expect(GeminiNarrativeSchema.TryParseAndValidate(valid, request, out var response, out _),
            "Valid strict narrative JSON was rejected.", errors);
        Expect(response != null && response.usedGemini && response.selectedFollowupId == "followup_shared_memory",
            "Valid narrative JSON did not preserve its bounded selection.", errors);

        string unknown = valid.Replace("followup_shared_memory", "invented_followup");
        Expect(!GeminiNarrativeSchema.TryParseAndValidate(unknown, request, out _, out string unknownError) &&
               unknownError == "unknown_followup",
            "Unknown follow-up ID was accepted.", errors);

        string extra = valid.TrimEnd('}') + ",\"statePatch\":\"grant_gold\"}";
        Expect(!GeminiNarrativeSchema.TryParseAndValidate(extra, request, out _, out string extraError) &&
               extraError == "schema_keys",
            "Extra state-patch key was accepted.", errors);
        Expect(!GeminiNarrativeSchema.TryParseAndValidate("```json\n" + valid + "\n```", request, out _, out _),
            "Markdown-wrapped response was accepted.", errors);
        Expect(!GeminiNarrativeSchema.TryParseAndValidate(
                valid.Replace("Agnolo grips the old token", "effectId circle_influence_delta"),
                request, out _, out string stateError) && stateError == "unsafe_payload",
            "State/effect payload text was accepted.", errors);
        Expect(!GeminiNarrativeSchema.TryParseAndValidate(
                valid.Replace("A shared token briefly steadied Agnolo's memory.", "Agnolo never existed"),
                request, out _, out string contradiction) && contradiction == "canonical_contradiction",
            "Authored canonical contradiction was accepted.", errors);
        Expect(!GeminiNarrativeSchema.TryParseAndValidate(
                valid.Replace("\"confused\"", "\"omniscient\""), request, out _, out _),
            "Unknown reaction metadata was accepted.", errors);
        Expect(!GeminiNarrativeSchema.TryParseAndValidate("not json", request, out _, out _),
            "Malformed response was accepted.", errors);

        string oversized = valid.Replace(
            "Agnolo grips the old token and whispers your name.", new string('x', GeminiNarrativeSchema.MaxProseCharacters + 1));
        Expect(!GeminiNarrativeSchema.TryParseAndValidate(oversized, request, out _, out _),
            "Oversized prose was accepted.", errors);
    }

    static void ValidateLockedVocabulary(List<string> errors)
    {
        var request = BuildRequest(circleVocabularyUnlocked: false);
        if (!NarrativeContextBuilder.TryBuildPrompt(request, out string prompt, out _, out string error))
        {
            errors.Add("Locked-vocabulary context failed: " + error);
            return;
        }
        Expect(prompt.IndexOf("Limbo", StringComparison.OrdinalIgnoreCase) < 0,
            "Formal Limbo terminology leaked into pre-confrontation context.", errors);
        Expect(prompt.Contains("known_clue_1:Rumored"), "Locked context did not retain a redacted known clue.", errors);

        const string reveal =
            "{\"prose\":\"Limbo is taking your neighbor.\",\"reaction\":\"afraid\",\"selectedFollowupId\":\"followup_shared_memory\",\"derivedSummary\":\"The Circle was named.\"}";
        Expect(!GeminiNarrativeSchema.TryParseAndValidate(reveal, request, out _, out string errorCode) &&
               errorCode == "locked_vocabulary",
            "Pre-confrontation response was allowed to name the formal cause.", errors);
    }

    static void ValidateFallback(NarrativeRequest request, List<string> errors)
    {
        int before = WorldEventLedger.Count;
        var first = DeterministicNarrativeFallback.Build(request, "offline", "context_hash");
        var second = DeterministicNarrativeFallback.Build(request, "offline", "context_hash");
        Expect(first.prose == second.prose && first.reaction == second.reaction &&
               first.selectedFollowupId == second.selectedFollowupId,
            "Offline authored fallback was not deterministic.", errors);
        Expect(first.usedFallback && !first.usedGemini && first.failureCode == "offline",
            "Fallback provenance was not recorded.", errors);
        Expect(WorldEventLedger.Count == before,
            "Narrative fallback mutated the canonical event ledger.", errors);

        var invalid = BuildRequest(true);
        invalid.fallbackFollowupId = "not_allowed";
        Expect(!GeminiNarrativeSchema.TryValidateRequest(invalid, out _),
            "Request with an unregistered authored fallback ID was accepted.", errors);
    }

    static void Expect(bool condition, string message, List<string> errors)
    {
        if (!condition) errors.Add(message);
    }
}
