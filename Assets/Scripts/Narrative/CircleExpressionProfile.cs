using System;
using UnityEngine;

[Serializable]
public sealed class CircleExpressionBand
{
    [Range(0f, 1f)] public float beginsAtInfluence;
    public string symptomTextId;
    public string[] eventTags = Array.Empty<string>();
    public string[] npcOverlayVocabulary = Array.Empty<string>();
    public string[] environmentalPresentationIds = Array.Empty<string>();
    public string[] encounterTags = Array.Empty<string>();
}

[CreateAssetMenu(
    fileName = "CircleExpressionProfile",
    menuName = "InfernosCurse/Narrative/Circle Expression Profile")]
public sealed class CircleExpressionProfile : ScriptableObject
{
    public CircleId circle = CircleId.Limbo;
    public CurseDefinition propagationTuning;
    public CircleExpressionBand[] bands = Array.Empty<CircleExpressionBand>();

    public CircleExpressionBand Evaluate(float hiddenInfluence)
    {
        CircleExpressionBand selected = null;
        float value = Mathf.Clamp01(hiddenInfluence);
        foreach (var band in bands ?? Array.Empty<CircleExpressionBand>())
        {
            if (band == null || band.beginsAtInfluence > value) continue;
            if (selected == null || band.beginsAtInfluence > selected.beginsAtInfluence)
                selected = band;
        }
        return selected;
    }
}
