using System;
using UnityEngine;

[CreateAssetMenu(fileName = "NpcMemory", menuName = "InfernosCurse/Narrative/NPC Memory Definition")]
public sealed class NpcMemoryDefinition : ScriptableObject
{
    public string npcId;
    public string homeDistrictId;
    [Range(0.5f, 1.5f)] public float susceptibility = 1f;
    public string originalScheduleId;
    public string originalRelationshipId;
    public string[] overlappingPreachingSiteIds = Array.Empty<string>();
    public bool essentialService;
    public bool questCritical;
}
