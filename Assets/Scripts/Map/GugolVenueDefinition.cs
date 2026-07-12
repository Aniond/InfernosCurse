using System;
using System.Collections.Generic;
using UnityEngine;

// APPEND ONLY. Serialized in Gugol venue assets and search snapshots.
public enum GugolVenueCategory
{
    Shop = 0,
    Inn = 1,
    Service = 2,
    Residence = 3,
    Workshop = 4,
    Landmark = 5,
}

[CreateAssetMenu(fileName = "GugolVenue", menuName = "InfernosCurse/Gugol Mappe/Venue")]
public sealed class GugolVenueDefinition : ScriptableObject
{
    [Header("Stable identity")]
    public string venueId;
    public string displayName;
    public GugolVenueCategory category;
    public string streetId;

    [Header("Player knowledge")]
    public string discoveryId;
    public DiscoveryStage minimumVisibleStage = DiscoveryStage.Located;
    public string rumorLabel;

    [Header("Street View")]
    public Vector2 streetViewAnchor = new(0.5f, 0.5f);
    public Sprite frontageSprite;
    public Sprite icon;

    [Header("Gameplay destination")]
    public string sceneName;
    public string entryId;
    public string buildingId;
    public string subLocationId;

    [Header("Public presentation")]
    public string siteId;
    public string openingHoursText;
    public string[] services = Array.Empty<string>();

    public bool TryValidate(out string error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(venueId) || string.IsNullOrWhiteSpace(displayName) ||
            string.IsNullOrWhiteSpace(streetId))
            return Fail("Venue requires stable ID, display name, and street ID.", out error);
        if (!Enum.IsDefined(typeof(GugolVenueCategory), category) ||
            !Enum.IsDefined(typeof(DiscoveryStage), minimumVisibleStage))
            return Fail($"Venue '{venueId}' has an unknown category or discovery stage.", out error);
        if (!Normalized(streetViewAnchor))
            return Fail($"Venue '{venueId}' has a Street View anchor outside 0..1.", out error);
        if (!string.IsNullOrWhiteSpace(discoveryId) && minimumVisibleStage == DiscoveryStage.Rumored &&
            string.IsNullOrWhiteSpace(rumorLabel))
            return Fail($"Rumored venue '{venueId}' requires a non-revealing rumor label.", out error);
        var uniqueServices = new HashSet<string>(StringComparer.Ordinal);
        foreach (string service in services ?? Array.Empty<string>())
            if (string.IsNullOrWhiteSpace(service) || !uniqueServices.Add(service))
                return Fail($"Venue '{venueId}' contains an empty or duplicate service.", out error);
        return true;
    }

    static bool Normalized(Vector2 value) =>
        value.x >= 0f && value.x <= 1f && value.y >= 0f && value.y <= 1f &&
        !float.IsNaN(value.x) && !float.IsNaN(value.y);

    static bool Fail(string message, out string error)
    {
        error = message;
        return false;
    }
}
