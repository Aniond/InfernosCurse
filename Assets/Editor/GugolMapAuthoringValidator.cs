using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class GugolMapAuthoringValidator
{
    [MenuItem("InfernosCurse/Validation/Validate Gugol Mappe Refined Authoring")]
    public static void ValidateMenu()
    {
        if (!Validate(out string error)) throw new InvalidOperationException(error);
        Debug.Log("[GugolMapValidator] Validation passed: profile, streets, venues, NPC knowledge, hidden-state isolation, and save v8.");
    }

    public static bool Validate(out string error)
    {
        var errors = new List<string>();
        var streets = Resources.LoadAll<GugolStreetDefinition>("GugolMap/Streets");
        var venues = Resources.LoadAll<GugolVenueDefinition>("GugolMap/Venues");
        var npcs = Resources.LoadAll<GugolNpcMapDefinition>("GugolMap/Npcs");
        var profiles = Resources.LoadAll<GugolMapPresentationProfile>("GugolMap/Presentation");
        var expressions = Resources.LoadAll<GugolMapWorldExpressionDefinition>("GugolMap/WorldExpressions");

        if (profiles.Length != 1) errors.Add($"expected one presentation profile, found {profiles.Length}");
        if (streets.Length < 2) errors.Add("Mercato pilot requires at least two street definitions");
        if (venues.Length < 3) errors.Add("Mercato pilot requires at least three venue definitions");
        if (npcs.Length < 5) errors.Add("Mercato pilot requires at least five NPC map definitions");

        ValidateUnique(streets, street => street?.streetId, "street", errors);
        ValidateUnique(venues, venue => venue?.venueId, "venue", errors);
        ValidateUnique(npcs, npc => npc?.npcId, "NPC", errors);
        ValidateUnique(expressions, expression => expression?.expressionId, "expression", errors);

        foreach (var street in streets)
        {
            if (street == null) errors.Add("null street definition");
            else if (!street.TryValidate(out string message)) errors.Add(message);
        }
        foreach (var venue in venues)
        {
            if (venue == null) errors.Add("null venue definition");
            else if (!venue.TryValidate(out string message)) errors.Add(message);
        }
        foreach (var npc in npcs)
        {
            if (npc == null) errors.Add("null NPC definition");
            else if (!npc.TryValidate(out string message)) errors.Add(message);
        }
        foreach (var expression in expressions)
        {
            if (expression == null) errors.Add("null expression definition");
            else if (!expression.TryValidate(out string message)) errors.Add(message);
        }

        var streetIds = new HashSet<string>(streets.Where(value => value != null).Select(value => value.streetId), StringComparer.Ordinal);
        var venueIds = new HashSet<string>(venues.Where(value => value != null).Select(value => value.venueId), StringComparer.Ordinal);
        foreach (var venue in venues)
            if (venue != null && !streetIds.Contains(venue.streetId))
                errors.Add($"venue '{venue.venueId}' references missing street '{venue.streetId}'");
        foreach (var street in streets)
            foreach (string venueId in street?.venueIds ?? Array.Empty<string>())
                if (!venueIds.Contains(venueId)) errors.Add($"street '{street.streetId}' references missing venue '{venueId}'");
        foreach (var npc in npcs)
        {
            if (npc == null) continue;
            if (!string.IsNullOrWhiteSpace(npc.usualStreetId) && !streetIds.Contains(npc.usualStreetId))
                errors.Add($"NPC '{npc.npcId}' references missing street '{npc.usualStreetId}'");
            if (!string.IsNullOrWhiteSpace(npc.usualVenueId) && !venueIds.Contains(npc.usualVenueId))
                errors.Add($"NPC '{npc.npcId}' references missing venue '{npc.usualVenueId}'");
        }

        int saveVersion = (int)typeof(SaveSystem).GetField(nameof(SaveSystem.CURRENT_VERSION)).GetRawConstantValue();
        if (saveVersion != 8) errors.Add($"SaveSystem version is {saveVersion}, expected 8");
        ValidateNpcKnowledge(errors);
        ValidateForbiddenDependencies(errors);

        error = string.Join("\n- ", errors);
        if (errors.Count > 0) error = "Gugol Mappe validation failed:\n- " + error;
        return errors.Count == 0;
    }

    static void ValidateNpcKnowledge(List<string> errors)
    {
        GugolNpcMapKnowledgeLedger.Reset();
        bool recorded = GugolNpcMapKnowledgeLedger.Record(
            "validator_npc", "mercato_vecchio_square", "albergo_fiorentino", "1265:100",
            GugolTimeBand.Morning, GugolNpcKnowledgeSource.DirectSighting, "validator_sighting",
            out bool changed, out string recordError);
        if (!recorded || !changed) errors.Add("NPC knowledge direct sighting failed: " + recordError);
        var exported = GugolNpcMapKnowledgeLedger.Export();
        string validateError = null;
        if (exported.Length != 1 || !GugolNpcMapKnowledgeLedger.TryValidateRecords(exported, out validateError))
            errors.Add("NPC knowledge export validation failed: " + validateError);
        string importError = null;
        GugolNpcMapKnowledgeRecord restored = null;
        bool imported = GugolNpcMapKnowledgeLedger.Import(exported, out importError);
        bool found = imported && GugolNpcMapKnowledgeLedger.TryGet("validator_npc", out restored);
        if (!found || restored.streetId != "mercato_vecchio_square")
            errors.Add("NPC knowledge round trip failed: " + importError);
        if (restored != null && GugolNpcMapKnowledgeLedger.FormatRecency(restored, "1265:100") != "seen this morning")
            errors.Add("NPC knowledge recency formatting failed");
        GugolNpcMapKnowledgeLedger.Reset();
    }

    static void ValidateForbiddenDependencies(List<string> errors)
    {
        string root = Path.Combine(Application.dataPath, "Scripts", "Map");
        string[] forbidden =
        {
            "HubMap.GetInfluence",
            ".circleInfluence",
            "CircleExpressionProfile.Evaluate",
            "PlayerInsanityState",
            "InsanityPresenter",
        };
        foreach (string path in Directory.GetFiles(root, "*.cs", SearchOption.TopDirectoryOnly))
        {
            string text = File.ReadAllText(path);
            foreach (string token in forbidden)
                if (text.Contains(token, StringComparison.Ordinal))
                    errors.Add($"forbidden hidden-state dependency '{token}' in {Path.GetFileName(path)}");
        }
    }

    static void ValidateUnique<T>(T[] values, Func<T, string> id, string label, List<string> errors)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (T value in values ?? Array.Empty<T>())
        {
            string key = id(value);
            if (string.IsNullOrWhiteSpace(key) || !ids.Add(key))
                errors.Add($"{label} ID is empty or duplicated: '{key}'");
        }
    }
}
