using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Resolves authored expression IDs into visual treatment. This presenter has
/// no access to the hidden state that caused a warning or outcome.
/// </summary>
public sealed class GugolMapWorldStatePresenter
{
    readonly Dictionary<string, GugolMapWorldExpressionDefinition> _expressions =
        new(StringComparer.Ordinal);

    public GugolMapWorldStatePresenter()
    {
        foreach (var definition in Resources.LoadAll<GugolMapWorldExpressionDefinition>(
                     "GugolMap/WorldExpressions"))
            if (definition != null && !string.IsNullOrWhiteSpace(definition.expressionId))
                _expressions[definition.expressionId] = definition;
    }

    public Color TintFor(GugolMapFeatureRecord feature, Color normal)
    {
        if (feature == null) return normal;
        if (!string.IsNullOrWhiteSpace(feature.expressionId) &&
            _expressions.TryGetValue(feature.expressionId, out var expression))
            return expression.tint;
        return feature.knowledgeState switch
        {
            GugolMapKnowledgeState.Rumored => WithAlpha(normal, 0.68f),
            GugolMapKnowledgeState.Lost => Color.Lerp(normal, new Color(0.33f, 0.28f, 0.23f), 0.55f),
            GugolMapKnowledgeState.Forgotten => WithAlpha(normal, 0.16f),
            GugolMapKnowledgeState.RememberedLoss => Color.Lerp(normal, new Color(0.48f, 0.34f, 0.20f), 0.42f),
            _ => normal,
        };
    }

    public bool ShowLabel(GugolMapFeatureRecord feature) =>
        feature != null && feature.knowledgeState != GugolMapKnowledgeState.Hidden &&
        feature.knowledgeState != GugolMapKnowledgeState.Forgotten &&
        feature.labelTreatment != GugolMapLabelTreatment.Suppressed;

    static Color WithAlpha(Color color, float alpha)
    {
        color.a *= alpha;
        return color;
    }
}
