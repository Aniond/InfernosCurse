using System;
using UnityEngine;

// APPEND ONLY. Serialized on Gugol node data.
public enum TerritoryKind
{
    None = 0,
    City = 1,
    Town = 2,
    Village = 3,
    Estate = 4,
    Winery = 5,
    Monastery = 6,
    Landmark = 7,
}

[Serializable]
public sealed class CircleRouteStrength
{
    public string neighborId;
    [Range(0f, 1f)] public float strength = 1f;
}

public readonly struct CircleTerritoryRoute
{
    public readonly HubNode Target;
    public readonly float Strength;

    public CircleTerritoryRoute(HubNode target, float strength)
    {
        Target = target;
        Strength = Mathf.Clamp01(strength);
    }
}
