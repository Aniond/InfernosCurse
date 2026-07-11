using UnityEngine;

[CreateAssetMenu(fileName = "Discovery", menuName = "InfernosCurse/Narrative/Discovery Definition")]
public sealed class ExplorationDiscoveryDefinition : ScriptableObject
{
    public string discoveryId;
    public DiscoveryKind kind;
    [TextArea] public string journalHint;
    public string locationId;
}
