// APPEND ONLY. This view enum is map-UI state, not the serialized HubMap level.
public enum GugolMapViewKind
{
    City = 0,
    Region = 1,
    World = 2,
    Street = 3,
}

public static class GugolMapViewKindExtensions
{
    public static GugolMapViewKind ToGugolView(this MapLevel level)
    {
        return level switch
        {
            MapLevel.City => GugolMapViewKind.City,
            MapLevel.Region => GugolMapViewKind.Region,
            _ => GugolMapViewKind.World,
        };
    }

    public static bool TryToMapLevel(this GugolMapViewKind view, out MapLevel level)
    {
        switch (view)
        {
            case GugolMapViewKind.City:
                level = MapLevel.City;
                return true;
            case GugolMapViewKind.Region:
                level = MapLevel.Region;
                return true;
            case GugolMapViewKind.World:
                level = MapLevel.World;
                return true;
            default:
                level = MapLevel.City;
                return false;
        }
    }
}
