using UnityEngine;
using System.Collections.Generic;

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
