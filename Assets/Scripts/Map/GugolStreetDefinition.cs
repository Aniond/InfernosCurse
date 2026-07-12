using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "GugolStreet", menuName = "InfernosCurse/Gugol Mappe/Street")]
public sealed class GugolStreetDefinition : ScriptableObject
{
    [Header("Stable identity")]
    public string streetId;
    public string displayName;
    public string parentCityId = "firenze";
    public string[] districtIds = Array.Empty<string>();

    [Header("Player knowledge")]
    public string discoveryId;
    public DiscoveryStage minimumVisibleStage = DiscoveryStage.Rumored;
    [Tooltip("Used while the street is only rumored. Must not reveal a hidden proper name.")]
    public string rumorLabel;

    [Header("City map geometry (normalized 0..1)")]
    public Vector2[] cityCenterline = Array.Empty<Vector2>();
    [Range(0.005f, 0.15f)] public float cityHitWidth = 0.035f;
    [Range(0, 100)] public int labelPriority = 50;

    [Header("Street View")]
    public Rect streetViewBounds = new(0.25f, 0.25f, 0.5f, 0.5f);
    public Sprite streetViewBackground;
    public string[] venueIds = Array.Empty<string>();
    public Vector2 routeFallbackPosition = new(0.5f, 0.5f);

    public bool TryValidate(out string error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(streetId) || string.IsNullOrWhiteSpace(displayName) ||
            string.IsNullOrWhiteSpace(parentCityId))
            return Fail("Street requires stable ID, display name, and parent city.", out error);
        if (!Enum.IsDefined(typeof(DiscoveryStage), minimumVisibleStage))
            return Fail($"Street '{streetId}' has an unknown discovery stage.", out error);
        if (cityCenterline == null || cityCenterline.Length < 2)
            return Fail($"Street '{streetId}' requires at least two city centerline points.", out error);
        foreach (Vector2 point in cityCenterline)
            if (!Normalized(point)) return Fail($"Street '{streetId}' has a city point outside 0..1.", out error);
        if (cityHitWidth < 0.005f || cityHitWidth > 0.15f)
            return Fail($"Street '{streetId}' hit width is outside the supported range.", out error);
        if (!NormalizedRect(streetViewBounds) || streetViewBounds.width <= 0f || streetViewBounds.height <= 0f)
            return Fail($"Street '{streetId}' has invalid Street View bounds.", out error);
        if (!Normalized(routeFallbackPosition))
            return Fail($"Street '{streetId}' has an invalid route fallback position.", out error);
        if (!UniqueIds(districtIds) || !UniqueIds(venueIds))
            return Fail($"Street '{streetId}' contains an empty or duplicate district/venue ID.", out error);
        if (!string.IsNullOrWhiteSpace(discoveryId) && minimumVisibleStage == DiscoveryStage.Rumored &&
            string.IsNullOrWhiteSpace(rumorLabel))
            return Fail($"Rumored street '{streetId}' requires a non-revealing rumor label.", out error);
        return true;
    }

    static bool Normalized(Vector2 value) =>
        value.x >= 0f && value.x <= 1f && value.y >= 0f && value.y <= 1f &&
        !float.IsNaN(value.x) && !float.IsNaN(value.y);

    static bool NormalizedRect(Rect value) =>
        value.xMin >= 0f && value.yMin >= 0f && value.xMax <= 1f && value.yMax <= 1f &&
        !float.IsNaN(value.x) && !float.IsNaN(value.y) &&
        !float.IsNaN(value.width) && !float.IsNaN(value.height);

    static bool UniqueIds(string[] values)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (string value in values ?? Array.Empty<string>())
            if (string.IsNullOrWhiteSpace(value) || !ids.Add(value)) return false;
        return true;
    }

    static bool Fail(string message, out string error)
    {
        error = message;
        return false;
    }
}
