using System;
using System.Collections.Generic;

public sealed class GugolMapSearchIndex
{
    readonly List<Entry> _entries = new();

    sealed class Entry
    {
        public GugolMapFeatureRecord record;
        public string normalized;
    }

    public GugolMapSearchIndex(GugolMapKnowledgeSnapshot snapshot)
    {
        foreach (var feature in snapshot?.Features ?? Array.Empty<GugolMapFeatureRecord>())
        {
            if (feature == null || !feature.IsSearchable || string.IsNullOrWhiteSpace(feature.displayName)) continue;
            string searchable = string.IsNullOrWhiteSpace(feature.searchText)
                ? feature.displayName
                : feature.displayName + " " + feature.searchText;
            _entries.Add(new Entry { record = feature, normalized = searchable.ToLowerInvariant() });
        }
        _entries.Sort((left, right) => string.Compare(
            left.record.displayName, right.record.displayName, StringComparison.Ordinal));
    }

    public List<GugolMapFeatureRecord> Query(string query, int maxResults = 12)
    {
        var result = new List<GugolMapFeatureRecord>();
        string normalized = (query ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Length == 0) return result;
        foreach (var entry in _entries)
        {
            if (!entry.normalized.Contains(normalized)) continue;
            result.Add(entry.record);
        }
        result.Sort((left, right) =>
        {
            bool leftExact = string.Equals(left.displayName, query, StringComparison.OrdinalIgnoreCase);
            bool rightExact = string.Equals(right.displayName, query, StringComparison.OrdinalIgnoreCase);
            if (leftExact != rightExact) return leftExact ? -1 : 1;
            bool leftStarts = left.displayName.StartsWith(query, StringComparison.OrdinalIgnoreCase);
            bool rightStarts = right.displayName.StartsWith(query, StringComparison.OrdinalIgnoreCase);
            if (leftStarts != rightStarts) return leftStarts ? -1 : 1;
            return string.Compare(left.displayName, right.displayName, StringComparison.Ordinal);
        });
        int limit = Math.Max(1, maxResults);
        if (result.Count > limit) result.RemoveRange(limit, result.Count - limit);
        return result;
    }
}
