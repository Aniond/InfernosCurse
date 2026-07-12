using System;
using System.Collections.Generic;

// APPEND ONLY. Presentation state, not hidden world simulation state.
public enum GugolMapKnowledgeState
{
    Hidden = 0,
    Rumored = 1,
    Known = 2,
    LastKnown = 3,
    Lost = 4,
    Forgotten = 5,
    RememberedLoss = 6,
}

public enum GugolMapFeatureKind
{
    Location = 0,
    Street = 1,
    Venue = 2,
    Npc = 3,
}

public sealed class GugolMapFeatureRecord
{
    public string featureId;
    public GugolMapFeatureKind kind;
    public GugolMapKnowledgeState knowledgeState;
    public string displayName;
    public string subtitle;
    public string searchText;
    public string nodeId;
    public string streetId;
    public string venueId;
    public string siteId;
    public string expressionId;
    public GugolMapLabelTreatment labelTreatment;
    public HubNode node;
    public GugolStreetDefinition street;
    public GugolVenueDefinition venue;
    public GugolNpcMapDefinition npc;

    public bool IsVisible => knowledgeState != GugolMapKnowledgeState.Hidden;
    public bool IsSearchable => IsVisible && knowledgeState != GugolMapKnowledgeState.Forgotten;
}

public sealed class GugolMapKnowledgeSnapshot
{
    readonly Dictionary<string, GugolMapFeatureRecord> _byKey;

    public GugolMapFeatureRecord[] Features { get; }
    public string WeatherCondition { get; }
    public bool FloodRisk { get; }
    public string DayKey { get; }

    public GugolMapKnowledgeSnapshot(
        IEnumerable<GugolMapFeatureRecord> features,
        string weatherCondition,
        bool floodRisk,
        string dayKey)
    {
        var list = new List<GugolMapFeatureRecord>(features ?? Array.Empty<GugolMapFeatureRecord>());
        Features = list.ToArray();
        WeatherCondition = weatherCondition ?? "clear";
        FloodRisk = floodRisk;
        DayKey = dayKey ?? "undated";
        _byKey = new Dictionary<string, GugolMapFeatureRecord>(StringComparer.Ordinal);
        foreach (var feature in Features)
            if (feature != null && !_byKey.ContainsKey(Key(feature.kind, feature.featureId)))
                _byKey.Add(Key(feature.kind, feature.featureId), feature);
    }

    public bool TryGet(GugolMapFeatureKind kind, string featureId, out GugolMapFeatureRecord record) =>
        _byKey.TryGetValue(Key(kind, featureId), out record);

    public List<GugolMapFeatureRecord> Visible(GugolMapFeatureKind kind)
    {
        var result = new List<GugolMapFeatureRecord>();
        foreach (var feature in Features)
            if (feature != null && feature.kind == kind && feature.IsVisible) result.Add(feature);
        return result;
    }

    static string Key(GugolMapFeatureKind kind, string id) => $"{(int)kind}:{id ?? string.Empty}";
}
