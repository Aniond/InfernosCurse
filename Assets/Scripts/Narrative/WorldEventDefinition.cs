using System;
using UnityEngine;

[CreateAssetMenu(fileName = "WorldEvent", menuName = "InfernosCurse/Narrative/World Event Definition")]
public sealed class WorldEventDefinition : ScriptableObject
{
    public string eventTypeId;
    public bool campaignPermanent;
    public WorldChoiceDefinition[] choices = Array.Empty<WorldChoiceDefinition>();

    public bool TryGetChoice(string choiceId, out WorldChoiceDefinition choice)
    {
        if (choices != null)
            foreach (var candidate in choices)
            {
                if (candidate == null || !string.Equals(candidate.choiceId, choiceId, StringComparison.Ordinal))
                    continue;
                choice = candidate;
                return true;
            }
        choice = null;
        return false;
    }
}
