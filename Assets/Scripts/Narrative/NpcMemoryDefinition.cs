using System;
using UnityEngine;

// APPEND ONLY. Controls which facts are relevant to an NPC without changing
// the authoritative territory state.
public enum NpcCircleRelevanceLayer
{
    DirectRelationship = 0,
    LocalWitness = 1,
    TerritorySymptom = 2,
    DerivedRegionalEvent = 3,
}

[CreateAssetMenu(fileName = "NpcMemory", menuName = "InfernosCurse/Narrative/NPC Memory Definition")]
public sealed class NpcMemoryDefinition : ScriptableObject
{
    public string npcId;
    public string homeDistrictId;
    public string homeTerritoryId;
    public NpcCircleRelevanceLayer relevanceLayer = NpcCircleRelevanceLayer.LocalWitness;
    [Range(0.5f, 1.5f)] public float susceptibility = 1f;
    public string originalScheduleId;
    public string originalRelationshipId;
    public string[] overlappingPreachingSiteIds = Array.Empty<string>();
    [Tooltip("Sites whose permanent outcomes this NPC can directly remember or lose.")]
    public string[] relevantSiteIds = Array.Empty<string>();
    public bool essentialService;
    public bool questCritical;
}
