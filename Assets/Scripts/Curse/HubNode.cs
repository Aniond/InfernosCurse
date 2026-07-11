using UnityEngine;
using System.Collections.Generic;

// Local weather character of a district. FlorenceWeather derives a per-district
// variant of the city's daily roll from this: the river bank catches fog and
// heavy rain, open squares turn gusty, sheltered interiors soften storms,
// hilltops sit above the valley fog but catch the wind.
// APPEND ONLY — serialized as ints on GameSystems.prefab node data.
public enum MicroClimate { Default, Riverside, OpenPiazza, Sheltered, Hilltop }

// What a node IS on the Gugol map: Town = travelable settlement, Waypoint =
// road dot between towns (future encounter hook, never a travel target),
// City = a zoom-in gateway pin standing for everything one level below it
// (firenze on the region sheet, toscana on the Italy sheet).
public enum NodeKind { District, Town, Waypoint, City }

// Which zoom level a node renders on. The Gugol map is a stack of parchment
// sheets: Florence districts → Tuscany region → the Italian peninsula (the
// Nine Circles canvas — future circle-locations land there as data).
// APPEND ONLY — serialized as ints on GameSystems.prefab node data.
public enum MapLevel { City, Region, World }

// Represents one location on the hub map (district, building, landmark).
// Nodes are connected by edges — curse diffuses along those edges each hub tick.
[System.Serializable]
public class HubNode
{
    public string   id;
    public string   displayName;
    public Vector2  mapImagePosition;   // normalized 0-1 pin position on the map art
    public string   blurb;              // detail-panel description
    public string   sceneName;          // scene to load on Enter
    public string   entryId;            // ZoneEntryPoint id to spawn at on arrival
    public Sprite   previewImage;       // location splash shown in the detail panel
    public MicroClimate microClimate = MicroClimate.Default;
    public NodeKind kind = NodeKind.District;
    public MapLevel mapLevel = MapLevel.City;
    public string discoveryId;
    public DiscoveryStage minimumDiscoveryStage = DiscoveryStage.Discovered;

    public CircleId nativeCircle = CircleId.Limbo;
    public List<CircleInfluenceState> circleInfluence = new();
    [Range(0f, 1f)] public float sanctity     = 0f;   // holy resistance to spread
    [Range(0f, 1f)] public float population   = 1f;   // scales how fast curse spreads (dense = faster)

    public bool isRitualSite   = false;   // Anchor AI will defend these
    public bool isSanctuarySite = false;  // Clerics consecrate these — spread firewall

    // Runtime — set by HubMap
    [System.NonSerialized] public List<HubNode> neighbors = new();

    // Compatibility bridge while existing Florence callers move from the old
    // single curse value to explicit Circle influence. In Florence the legacy
    // value means Limbo, never whichever Circle happens to be dominant.
    public float curseLevel
    {
        get => GetInfluence(CircleId.Limbo);
        set => SetInfluence(CircleId.Limbo, value);
    }

    public float GetInfluence(CircleId circle) =>
        CircleInfluenceLedger.Get(circleInfluence, circle);

    public bool SetInfluence(CircleId circle, float value) =>
        CircleInfluenceLedger.Set(circleInfluence, circle, value);

    public bool AddInfluence(CircleId circle, float delta) =>
        CircleInfluenceLedger.Add(circleInfluence, circle, delta);

    public CircleId DominantCircle =>
        CircleInfluenceLedger.Dominant(circleInfluence, nativeCircle);

    public float DominantInfluence => GetInfluence(DominantCircle);

    public bool IsCorrupted  => DominantInfluence >= 0.8f;
    public bool IsCleansed   => DominantInfluence <= 0.05f && sanctity >= 0.5f;
    public bool IsDiscoveryVisible =>
        ExplorationDiscoveryLedger.IsVisible(discoveryId, minimumDiscoveryStage);
}
