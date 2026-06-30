using UnityEngine;

// Carries "where should the player spawn" across a scene load. Set it right
// before loading a scene; the destination scene's ZoneEntryPlacer reads and
// clears it. Static so it survives the scene transition without a prefab.
public static class TravelIntent
{
    // The entryId the player should spawn at in the next scene. Empty = use
    // the scene's default spawn (don't reposition).
    public static string TargetEntryId { get; private set; } = "";

    public static void SetEntry(string entryId)
    {
        TargetEntryId = entryId ?? "";
    }

    public static string Consume()
    {
        var id = TargetEntryId;
        TargetEntryId = "";
        return id;
    }

    public static bool HasIntent => !string.IsNullOrEmpty(TargetEntryId);
}
