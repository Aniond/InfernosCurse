using UnityEngine;
using System.Collections.Generic;

// Local weather character of a district. FlorenceWeather derives a per-district
// variant of the city's daily roll from this: the river bank catches fog and
// heavy rain, open squares turn gusty, sheltered interiors soften storms,
// hilltops sit above the valley fog but catch the wind.
// APPEND ONLY — serialized as ints on GameSystems.prefab node data.
public enum MicroClimate { Default, Riverside, OpenPiazza, Sheltered, Hilltop }

// What a node IS on the Gugol map. Determines the map layer it renders on
// (District → city zoom, everything else → region zoom) and its behavior:
// Town = travelable settlement, Waypoint = road dot between towns (future
// encounter hook, never a travel target), City = the zoom-in gateway pin
// standing for all of Florence on the region layer.
public enum NodeKind { District, Town, Waypoint, City }

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

    [Range(0f, 1f)] public float curseLevel   = 0f;   // 0 = clean, 1 = fully corrupted
    [Range(0f, 1f)] public float sanctity     = 0f;   // holy resistance to spread
    [Range(0f, 1f)] public float population   = 1f;   // scales how fast curse spreads (dense = faster)

    public bool isRitualSite   = false;   // Anchor AI will defend these
    public bool isSanctuarySite = false;  // Clerics consecrate these — spread firewall

    // Runtime — set by HubMap
    [System.NonSerialized] public List<HubNode> neighbors = new();

    public bool IsCorrupted  => curseLevel >= 0.8f;
    public bool IsCleansed   => curseLevel <= 0.05f && sanctity >= 0.5f;
}
