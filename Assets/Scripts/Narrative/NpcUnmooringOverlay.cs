using System;
using UnityEngine;

public enum NpcDialogueOverlayMode
{
    Original = 0,
    Fragmented = 1,
    Dislocated = 2,
    Absent = 3,
}

public sealed class NpcUnmooringPresentationState
{
    public string displayName;
    public NpcMemoryStage stage;
    public NpcDialogueOverlayMode dialogueMode;
    public int arrivalDelayHours;
    public bool pauseDuringRoutes;
    public bool missWorkPeriod;
    public bool useOriginalSchedule;
    public bool useBackupServiceAccess;
    public bool inForgottenPool;
}

public static class NpcUnmooringPresentation
{
    public static NpcUnmooringPresentationState Evaluate(
        NpcMemoryRecord record,
        string authoredDisplayName,
        string dayKey)
    {
        if (record == null)
        {
            return new NpcUnmooringPresentationState
            {
                displayName = authoredDisplayName,
                stage = NpcMemoryStage.Grounded,
                dialogueMode = NpcDialogueOverlayMode.Original,
                useOriginalSchedule = true,
            };
        }

        uint seed = StableHash((record.npcId ?? string.Empty) + "|" + (dayKey ?? string.Empty));
        var state = new NpcUnmooringPresentationState
        {
            displayName = authoredDisplayName,
            stage = record.stage,
            useOriginalSchedule = true,
            useBackupServiceAccess = false,
            inForgottenPool = record.forgottenPool,
        };

        switch (record.stage)
        {
            case NpcMemoryStage.Grounded:
                state.dialogueMode = NpcDialogueOverlayMode.Original;
                break;
            case NpcMemoryStage.Distracted:
                state.dialogueMode = NpcDialogueOverlayMode.Fragmented;
                state.arrivalDelayHours = 1 + (int)(seed % 2u);
                state.pauseDuringRoutes = (seed & 1u) == 0u;
                break;
            case NpcMemoryStage.Unmoored:
                state.dialogueMode = NpcDialogueOverlayMode.Dislocated;
                state.pauseDuringRoutes = true;
                state.missWorkPeriod = seed % 3u != 0u;
                state.useOriginalSchedule = false;
                state.useBackupServiceAccess = record.essentialService || record.questCritical;
                break;
            case NpcMemoryStage.Forgotten:
                state.displayName = "...";
                state.dialogueMode = NpcDialogueOverlayMode.Absent;
                state.pauseDuringRoutes = false;
                state.missWorkPeriod = true;
                state.useOriginalSchedule = false;
                state.useBackupServiceAccess = false;
                state.inForgottenPool = true;
                break;
        }
        return state;
    }

    static uint StableHash(string value)
    {
        unchecked
        {
            uint hash = 2166136261u;
            foreach (char character in value)
            {
                hash ^= character;
                hash *= 16777619u;
            }
            return hash;
        }
    }
}

[DisallowMultipleComponent]
public sealed class NpcUnmooringOverlay : MonoBehaviour
{
    public string npcId;
    public string authoredDisplayName;

    public event Action<NpcMemoryStage> OnStageChanged;

    NpcMemoryStage _lastStage = (NpcMemoryStage)(-1);

    public NpcUnmooringPresentationState Current
    {
        get
        {
            PersistentLimboWorldState.TryGetNpc(npcId, out var record);
            var calendar = GameCalendar.Instance;
            string dayKey = calendar != null ? calendar.Year + ":" + calendar.DayOfYear : "undated";
            return NpcUnmooringPresentation.Evaluate(record, authoredDisplayName, dayKey);
        }
    }

    public string OriginalScheduleId =>
        PersistentLimboWorldState.TryGetNpc(npcId, out var record) ? record.originalScheduleId : string.Empty;

    public string OriginalRelationshipId =>
        PersistentLimboWorldState.TryGetNpc(npcId, out var record) ? record.originalRelationshipId : string.Empty;

    void Update()
    {
        var stage = Current.stage;
        if (stage == _lastStage) return;
        _lastStage = stage;
        OnStageChanged?.Invoke(stage);
    }
}
