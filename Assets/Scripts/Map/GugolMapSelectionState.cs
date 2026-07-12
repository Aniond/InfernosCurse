public enum GugolMapBackResult
{
    None = 0,
    CardClosed = 1,
    StreetClosed = 2,
    MapShouldClose = 3,
}

public sealed class GugolMapSelectionState
{
    public GugolMapViewKind View { get; private set; } = GugolMapViewKind.City;
    public MapLevel BaseLevel { get; private set; } = MapLevel.City;
    public string FocusedStreetId { get; private set; } = string.Empty;
    public GugolMapFeatureKind? SelectedKind { get; private set; }
    public string SelectedFeatureId { get; private set; } = string.Empty;

    public void SetBase(MapLevel level)
    {
        BaseLevel = level;
        View = level.ToGugolView();
        FocusedStreetId = string.Empty;
        ClearSelection();
    }

    public void EnterStreet(string streetId)
    {
        BaseLevel = MapLevel.City;
        View = GugolMapViewKind.Street;
        FocusedStreetId = streetId ?? string.Empty;
        ClearSelection();
    }

    public void Select(GugolMapFeatureKind kind, string featureId)
    {
        SelectedKind = kind;
        SelectedFeatureId = featureId ?? string.Empty;
    }

    public void ClearSelection()
    {
        SelectedKind = null;
        SelectedFeatureId = string.Empty;
    }

    public GugolMapBackResult Back(bool cardOpen)
    {
        if (cardOpen)
        {
            ClearSelection();
            return GugolMapBackResult.CardClosed;
        }
        if (View == GugolMapViewKind.Street)
        {
            SetBase(MapLevel.City);
            return GugolMapBackResult.StreetClosed;
        }
        return GugolMapBackResult.MapShouldClose;
    }
}
