using UnityEngine;

/// <summary>
/// Explicit authoring hook for a direct sighting or completed interaction.
/// It never derives street identity from the NPC's world transform.
/// </summary>
public sealed class GugolNpcSightingReporter : MonoBehaviour
{
    public string npcId;
    public string streetId;
    public string venueId;
    [Tooltip("Stable authoring source, such as npc interaction or public sighting anchor.")]
    public string sourceId;

    public bool ReportNow()
    {
        var calendar = GameCalendar.Instance;
        string dayKey = calendar != null ? $"{calendar.Year}:{calendar.DayOfYear}" : "undated";
        var timeBand = TimeBandFor(GameClock.HasClock ? GameClock.Hour : 12f);
        bool ok = GugolNpcMapKnowledgeLedger.Record(
            npcId,
            streetId,
            venueId,
            dayKey,
            timeBand,
            GugolNpcKnowledgeSource.DirectSighting,
            sourceId,
            out bool changed,
            out string error);
        if (!ok) Debug.LogError("[GugolMapKnowledge] " + error, this);
        else if (changed) Debug.Log($"[GugolMapKnowledge] Updated last-known street for '{npcId}'.", this);
        return ok;
    }

    internal static GugolTimeBand TimeBandFor(float hour)
    {
        hour = Mathf.Repeat(hour, 24f);
        if (hour >= 5f && hour < 12f) return GugolTimeBand.Morning;
        if (hour >= 12f && hour < 17f) return GugolTimeBand.Afternoon;
        if (hour >= 17f && hour < 21f) return GugolTimeBand.Evening;
        return GugolTimeBand.Night;
    }
}
