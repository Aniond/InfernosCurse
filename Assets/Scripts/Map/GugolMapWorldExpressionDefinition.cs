using System;
using UnityEngine;

// APPEND ONLY. Map expressions consume authored public signals, never raw Circle values.
public enum GugolMapExpressionSource
{
    Warning = 0,
    SiteOutcome = 1,
    EnvironmentalPresentation = 2,
    Weather = 3,
}

public enum GugolMapLabelTreatment
{
    Normal = 0,
    Faded = 1,
    Suppressed = 2,
    Remembered = 3,
}

[CreateAssetMenu(fileName = "GugolMapExpression", menuName = "InfernosCurse/Gugol Mappe/World Expression")]
public sealed class GugolMapWorldExpressionDefinition : ScriptableObject
{
    public string expressionId;
    public GugolMapExpressionSource source;
    [Tooltip("Warning ID, site-outcome ID, environmental presentation ID, or weather ID.")]
    public string sourceId;
    public string siteId;
    public string targetStreetId;
    public string targetVenueId;
    public Sprite overlay;
    public Color tint = Color.white;
    public GugolMapLabelTreatment labelTreatment;
    [TextArea] public string rememberedText;

    public bool TryValidate(out string error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(expressionId) || string.IsNullOrWhiteSpace(sourceId))
            return Fail("Map expression requires stable expression and source IDs.", out error);
        if (!Enum.IsDefined(typeof(GugolMapExpressionSource), source) ||
            !Enum.IsDefined(typeof(GugolMapLabelTreatment), labelTreatment))
            return Fail($"Map expression '{expressionId}' has an unknown source or label treatment.", out error);
        if (string.IsNullOrWhiteSpace(siteId) && string.IsNullOrWhiteSpace(targetStreetId) &&
            string.IsNullOrWhiteSpace(targetVenueId))
            return Fail($"Map expression '{expressionId}' requires a site, street, or venue target.", out error);
        if (labelTreatment == GugolMapLabelTreatment.Remembered && string.IsNullOrWhiteSpace(rememberedText))
            return Fail($"Remembered expression '{expressionId}' requires authored remembrance text.", out error);
        return true;
    }

    static bool Fail(string message, out string error)
    {
        error = message;
        return false;
    }
}
