using System;
using UnityEngine;

[CreateAssetMenu(fileName = "GugolNpc", menuName = "InfernosCurse/Gugol Mappe/NPC")]
public sealed class GugolNpcMapDefinition : ScriptableObject
{
    public string npcId;
    public string displayName;
    public Sprite portrait;

    [Header("Player knowledge")]
    public string discoveryId;
    public DiscoveryStage minimumVisibleStage = DiscoveryStage.Discovered;

    [Header("Authored usual location")]
    public string usualStreetId;
    public string usualVenueId;
    [TextArea] public string usualLocationText;
    public string[] authoredSchedulePhrases = Array.Empty<string>();

    public bool TryValidate(out string error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(npcId) || string.IsNullOrWhiteSpace(displayName))
            return Fail("Map NPC requires stable ID and display name.", out error);
        if (!Enum.IsDefined(typeof(DiscoveryStage), minimumVisibleStage))
            return Fail($"Map NPC '{npcId}' has an unknown discovery stage.", out error);
        if (string.IsNullOrWhiteSpace(usualStreetId) && string.IsNullOrWhiteSpace(usualVenueId) &&
            string.IsNullOrWhiteSpace(usualLocationText))
            return Fail($"Map NPC '{npcId}' requires an authored usual-location fallback.", out error);
        foreach (string phrase in authoredSchedulePhrases ?? Array.Empty<string>())
            if (string.IsNullOrWhiteSpace(phrase))
                return Fail($"Map NPC '{npcId}' contains an empty schedule phrase.", out error);
        return true;
    }

    static bool Fail(string message, out string error)
    {
        error = message;
        return false;
    }
}
