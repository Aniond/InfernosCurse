using System;
using UnityEngine;

[CreateAssetMenu(fileName = "WorldAgent", menuName = "InfernosCurse/Narrative/World Agent Definition")]
public sealed class WorldAgentDefinition : ScriptableObject
{
    public string agentId;
    public string districtId;
    public string startingSiteId;
    public string[] availableSiteIds = Array.Empty<string>();
    public bool startsDiscovered;
    public GameObject worldPrefab;
}
