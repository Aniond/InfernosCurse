using System;
using System.Collections.Generic;
using UnityEngine;

// APPEND ONLY. Serialized in warning definitions and save records.
public enum CircleWarningSeverity
{
    Minor = 0,
    Major = 1,
    Critical = 2,
}

// APPEND ONLY. CampaignPermanent outcomes must be committed to the Chronicle
// before their world presentation changes.
public enum NarrativePermanence
{
    Recoverable = 0,
    SavePersistent = 1,
    CampaignPermanent = 2,
}

[Serializable]
public sealed class CircleWarningStageDefinition
{
    [Min(1)] public int dayOffset = 1;
    public string symptomTextId;
}

[CreateAssetMenu(
    fileName = "CircleWarning",
    menuName = "InfernosCurse/Narrative/Circle Warning Definition")]
public sealed class CircleWarningDefinition : ScriptableObject
{
    [Header("Stable identity")]
    public string warningId;
    public string territoryId;
    public CircleId circle = CircleId.Limbo;
    public CircleWarningSeverity severity = CircleWarningSeverity.Major;

    [Header("Hidden trigger")]
    [Range(0f, 1f)] public float opensAtInfluence = 0.35f;
    public string[] prerequisiteEventIds = Array.Empty<string>();

    [Header("Authored symptoms")]
    public string openingSymptomTextId;
    public CircleWarningStageDefinition[] stageSchedule = Array.Empty<CircleWarningStageDefinition>();

    [Header("Response window")]
    [Min(1)] public int deadlineDays = 3;
    [Tooltip("A matching world-event instance ID or event-type ID closes this warning.")]
    public string[] responseEventIds = Array.Empty<string>();

    [Header("Expiration")]
    public WorldEventDefinition expirationEvent;
    public string expirationChoiceId;
    public NarrativePermanence permanence = NarrativePermanence.CampaignPermanent;

    public bool IsMajor => severity >= CircleWarningSeverity.Major;

    public bool TryValidate(out string error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(warningId) || string.IsNullOrWhiteSpace(territoryId))
            return Fail("Warning requires stable warning and territory IDs.", out error);
        if (!Enum.IsDefined(typeof(CircleId), circle) ||
            !Enum.IsDefined(typeof(CircleWarningSeverity), severity) ||
            !Enum.IsDefined(typeof(NarrativePermanence), permanence))
            return Fail($"Warning '{warningId}' contains an unknown serialized enum value.", out error);
        if (float.IsNaN(opensAtInfluence) || float.IsInfinity(opensAtInfluence) ||
            opensAtInfluence < 0f || opensAtInfluence > 1f)
            return Fail($"Warning '{warningId}' influence trigger must be within 0..1.", out error);
        if (string.IsNullOrWhiteSpace(openingSymptomTextId))
            return Fail($"Warning '{warningId}' requires an authored opening symptom text ID.", out error);
        if (deadlineDays < 1)
            return Fail($"Warning '{warningId}' deadline must be at least one day.", out error);

        int previousOffset = 0;
        var stageTextIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var stage in stageSchedule ?? Array.Empty<CircleWarningStageDefinition>())
        {
            if (stage == null || stage.dayOffset <= previousOffset ||
                string.IsNullOrWhiteSpace(stage.symptomTextId) ||
                !stageTextIds.Add(stage.symptomTextId))
                return Fail($"Warning '{warningId}' stages require increasing positive offsets and unique text IDs.", out error);
            if (stage.dayOffset >= deadlineDays)
                return Fail($"Warning '{warningId}' stage {stage.dayOffset} must occur before its deadline.", out error);
            previousOffset = stage.dayOffset;
        }

        if (!ValidateIds(prerequisiteEventIds, "prerequisite", out error) ||
            !ValidateIds(responseEventIds, "response", out error))
            return false;

        if (expirationEvent == null || string.IsNullOrWhiteSpace(expirationEvent.eventTypeId) ||
            !expirationEvent.TryGetChoice(expirationChoiceId, out _))
            return Fail($"Warning '{warningId}' requires a registered expiration event and choice.", out error);
        bool shouldBePermanent = permanence == NarrativePermanence.CampaignPermanent;
        if (expirationEvent.campaignPermanent != shouldBePermanent)
            return Fail($"Warning '{warningId}' expiration permanence does not match its world event.", out error);
        foreach (string responseId in responseEventIds ?? Array.Empty<string>())
            if (string.Equals(responseId, expirationEvent.eventTypeId, StringComparison.Ordinal))
                return Fail($"Warning '{warningId}' expiration event cannot also be a success response.", out error);
        return true;
    }

    bool ValidateIds(string[] values, string label, out string error)
    {
        error = null;
        var unique = new HashSet<string>(StringComparer.Ordinal);
        foreach (string value in values ?? Array.Empty<string>())
            if (string.IsNullOrWhiteSpace(value) || !unique.Add(value))
                return Fail($"Warning '{warningId}' contains an empty or duplicate {label} event ID.", out error);
        return true;
    }

    static bool Fail(string message, out string error)
    {
        error = message;
        return false;
    }
}
