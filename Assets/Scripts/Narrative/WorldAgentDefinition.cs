using System;
using UnityEngine;

[CreateAssetMenu(fileName = "WorldAgent", menuName = "InfernosCurse/Narrative/World Agent Definition")]
public sealed class WorldAgentDefinition : ScriptableObject
{
    public string agentId;
    [Tooltip("Influence owner affected by this agent. Local district/site fields remain presentation authoring.")]
    public string territoryId;
    public string districtId;
    public string startingSiteId;
    public string[] availableSiteIds = Array.Empty<string>();
    public bool startsDiscovered;
    public GameObject worldPrefab;
}
