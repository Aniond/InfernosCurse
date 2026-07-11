using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEditor;
using UnityEngine;

public static class CampaignChronicleValidator
{
    [MenuItem("InfernosCurse/Validation/Validate Campaign Chronicle")]
    public static void Validate()
    {
        var errors = new List<string>();
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string validationBase = Path.GetFullPath(Path.Combine(projectRoot, "Temp", "CampaignChronicleValidator"));
        string root = Path.Combine(validationBase, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            ValidateStore(root, errors);
        }
        finally
        {
            WorldEventLedger.Reset();
            string resolved = Path.GetFullPath(root);
            string requiredPrefix = validationBase.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (resolved.StartsWith(requiredPrefix, StringComparison.OrdinalIgnoreCase) && Directory.Exists(resolved))
                Directory.Delete(resolved, true);
        }

        if (errors.Count > 0)
        {
            foreach (string error in errors) Debug.LogError("[CampaignChronicleValidator] " + error);
            throw new InvalidOperationException($"Campaign Chronicle validation failed with {errors.Count} error(s). ");
        }
        Debug.Log("[CampaignChronicleValidator] Validation passed: hash chain, atomic mirror, backup recovery, save envelope, indexed ledger, and idempotent reconciliation.");
    }

    static void ValidateStore(string root, List<string> errors)
    {
        var store = new CampaignChronicleStore(root);
        if (!store.TryCreateCampaign(out var created, out string error))
        {
            errors.Add("Could not create isolated campaign: " + error);
            return;
        }
        Expect(File.Exists(store.GetPrimaryPath(created.campaignId)), "Primary Chronicle was not created.", errors);
        Expect(File.Exists(store.GetBackupPath(created.campaignId)), "Rolling backup was not created.", errors);
        Expect(created.lastSequence == 0, "New campaign was not empty.", errors);

        var firstDraft = Draft(
            "opening_neighbor_forgot_benidito",
            "florence_neighbor_forgets_name",
            "remember_neighbor",
            "mercato",
            new WorldConsequenceEffect
            {
                effectId = WorldEffectIds.CircleInfluenceDelta,
                targetId = "mercato",
                intValue = (int)CircleId.Limbo,
                numericValue = -0.005f,
            },
            "Benidito reminded his neighbor of a shared memory.",
            "remembered_a_name");

        if (!store.TryAppend(created.campaignId, firstDraft, out var afterFirst, out var first, out error))
        {
            errors.Add("First Chronicle append failed: " + error);
            return;
        }
        Expect(first.sequence == 1 && first.previousHash == string.Empty, "Genesis entry sequence/hash was wrong.", errors);
        Expect(!string.IsNullOrEmpty(first.currentHash), "Genesis entry did not receive a hash.", errors);

        if (!store.TryAppend(created.campaignId, firstDraft, out var duplicateDocument, out var duplicate, out error))
            errors.Add("Idempotent duplicate append failed: " + error);
        else
        {
            Expect(duplicate.sequence == 1, "Duplicate event created a second sequence.", errors);
            Expect(duplicateDocument.lastSequence == 1, "Duplicate event advanced the Chronicle.", errors);
        }

        var secondDraft = Draft(
            "opening_neighbor_walked_home",
            "florence_neighbor_returns_home",
            "escort_neighbor_home",
            "mercato",
            new WorldConsequenceEffect
            {
                effectId = WorldEffectIds.SanctityDelta,
                targetId = "mercato",
                numericValue = 0.01f,
            },
            "Benidito helped his neighbor return home.",
            "helped_someone_return_home");

        if (!store.TryAppend(created.campaignId, secondDraft, out var afterSecond, out var second, out error))
        {
            errors.Add("Second Chronicle append failed: " + error);
            return;
        }
        Expect(second.sequence == 2 && second.previousHash == first.currentHash,
            "Second entry did not extend the hash chain.", errors);
        Expect(CampaignChronicleStore.ValidateDocument(afterSecond, created.campaignId, 2, out _),
            "Valid two-entry Chronicle failed validation.", errors);

        var gap = CampaignChronicleStore.CloneDocument(afterSecond);
        gap.entries[1].sequence = 3;
        Expect(!CampaignChronicleStore.ValidateDocument(gap, created.campaignId, 0, out _),
            "Sequence gap was accepted.", errors);

        var tampered = CampaignChronicleStore.CloneDocument(afterSecond);
        tampered.entries[0].choiceId = "deny_neighbor_existed";
        Expect(!CampaignChronicleStore.ValidateDocument(tampered, created.campaignId, 0, out _),
            "Hash mismatch was accepted.", errors);

        File.WriteAllText(store.GetPrimaryPath(created.campaignId) + ".tmp", "{partial");
        if (!store.TryLoad(created.campaignId, 2, out var afterPartial, out bool partialRecovered, out error))
            errors.Add("Stale partial temp file disturbed the primary: " + error);
        else
        {
            Expect(afterPartial.lastSequence == 2, "Stale temp load returned the wrong sequence.", errors);
            Expect(!partialRecovered, "A stale temp file incorrectly triggered backup recovery.", errors);
        }

        File.WriteAllText(store.GetPrimaryPath(created.campaignId), "{corrupt-primary");
        if (!store.TryLoad(created.campaignId, 2, out var recovered, out bool restoredBackup, out error))
            errors.Add("Valid rolling backup did not recover the primary: " + error);
        else
        {
            Expect(restoredBackup, "Primary corruption did not report backup recovery.", errors);
            Expect(recovered.lastSequence == 2, "Recovered backup lost the latest sequence.", errors);
        }

        ValidateLedger(afterSecond, errors);

        var slot = new SaveData
        {
            saveVersion = SaveSystem.CURRENT_VERSION,
            campaignId = created.campaignId,
            chronicleSequence = 2,
            worldEvents = WorldEventLedger.Export(),
        };
        var restoredSlot = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(slot));
        Expect(restoredSlot != null && restoredSlot.campaignId == created.campaignId &&
               restoredSlot.chronicleSequence == 2 && restoredSlot.worldEvents?.Length == 2,
            "v4 campaign save envelope did not round-trip.", errors);
        Expect(SaveSystem.CURRENT_VERSION >= 4, "SaveSystem version was not advanced for Chronicle data.", errors);

        ValidateTransientReplacementLock(root, errors);

        File.WriteAllText(store.GetPrimaryPath(created.campaignId), "{broken");
        File.WriteAllText(store.GetBackupPath(created.campaignId), "{also-broken");
        Expect(!store.TryLoad(created.campaignId, 0, out _, out _, out _),
            "Store silently accepted two damaged Chronicle copies.", errors);
    }

    static void ValidateLedger(CampaignChronicleDocument chronicle, List<string> errors)
    {
        WorldEventLedger.Reset();
        var sink = new CountingSink();
        if (!WorldEventLedger.TryReconcile(chronicle, 0, out long sequence, out string error, sink))
        {
            errors.Add("Initial Chronicle reconciliation failed: " + error);
            return;
        }
        Expect(sequence == 2 && WorldEventLedger.Count == 2, "Reconciliation did not import both entries.", errors);
        Expect(sink.AppliedCount == 2, "Reconciliation did not apply each consequence once.", errors);

        if (!WorldEventLedger.TryReconcile(chronicle, 0, out sequence, out error, sink))
            errors.Add("Repeat reconciliation failed: " + error);
        Expect(sink.AppliedCount == 2, "Repeat reconciliation duplicated a consequence.", errors);
        Expect(WorldEventLedger.QueryByLocation("mercato").Length == 2, "Location index missed records.", errors);
        Expect(WorldEventLedger.QueryByNpc("npc_neighbor_agnolo").Length == 2, "NPC index missed records.", errors);
        Expect(WorldEventLedger.QueryByTag("remembered_a_name").Length == 1, "Tag index missed a record.", errors);
        Expect(WorldEventLedger.QueryByCircle(CircleId.Limbo).Length == 1, "Circle query missed a record.", errors);
        Expect(WorldEventLedger.BuildCanonicalFacts().Length == 2, "Canonical fact cache omitted outcomes.", errors);
        Expect(WorldEventLedger.TryValidateChronicleCoverage(
                WorldEventLedger.Export(), chronicle, 2, out _),
            "Complete save ledger failed Chronicle coverage.", errors);
        var altered = WorldEventLedger.Export();
        altered[0].factualOutcome = "A contradictory save-local version.";
        Expect(!WorldEventLedger.TryValidateChronicleCoverage(altered, chronicle, 2, out _),
            "Save-local event payload was allowed to contradict the Chronicle hash.", errors);
        Expect(!WorldEventLedger.TryValidateChronicleCoverage(
                Array.Empty<WorldEventRecord>(), chronicle, 2, out _),
            "Save sequence was accepted without its permanent records.", errors);
        Expect(WorldEventLedger.TryValidateChronicleCoverage(
                Array.Empty<WorldEventRecord>(), chronicle, 0, out _),
            "Older pre-choice save was not accepted for forward reconciliation.", errors);
    }

    static void ValidateTransientReplacementLock(string root, List<string> errors)
    {
        string lockedRoot = Path.Combine(root, "transient_replace_lock");
        var store = new CampaignChronicleStore(lockedRoot);
        if (!store.TryCreateCampaign(out var campaign, out string error))
        {
            errors.Add("Transient-lock fixture could not create a campaign: " + error);
            return;
        }

        FileStream heldFile = null;
        Thread releaseThread = null;
        try
        {
            heldFile = new FileStream(
                store.GetPrimaryPath(campaign.campaignId),
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
            FileStream captured = heldFile;
            releaseThread = new Thread(() =>
            {
                Thread.Sleep(125);
                captured.Dispose();
            }) { IsBackground = true };
            releaseThread.Start();

            var draft = Draft(
                "transient_lock_event",
                "validator_transient_lock",
                "continue_after_lock",
                "mercato",
                new WorldConsequenceEffect
                {
                    effectId = WorldEffectIds.CircleInfluenceDelta,
                    targetId = "mercato",
                    intValue = (int)CircleId.Limbo,
                    numericValue = 0.001f,
                },
                "The Chronicle survived a brief external file lock.",
                "transient_lock_recovered");

            bool appended = store.TryAppend(
                campaign.campaignId, draft, out var updated, out var entry, out error);
            if (!appended)
                errors.Add("Atomic Chronicle write did not survive a transient Windows lock: " + error);
            else
                Expect(updated.lastSequence == 1 && entry.sequence == 1,
                    "Transient-lock append returned the wrong sequence.", errors);
        }
        finally
        {
            if (releaseThread != null && releaseThread.IsAlive) releaseThread.Join(2000);
            heldFile?.Dispose();
        }
    }

    static CampaignChronicleEntry Draft(
        string instanceId,
        string eventTypeId,
        string choiceId,
        string locationId,
        WorldConsequenceEffect effect,
        string outcome,
        string tag) => new()
    {
        eventInstanceId = instanceId,
        eventTypeId = eventTypeId,
        choiceId = choiceId,
        gameDateKey = "1265:91",
        locationId = locationId,
        npcIds = new[] { "npc_neighbor_agnolo" },
        worldAgentIds = Array.Empty<string>(),
        consequences = new[] { effect },
        factualOutcome = outcome,
        semanticTags = new[] { tag },
    };

    static void Expect(bool condition, string message, List<string> errors)
    {
        if (!condition) errors.Add(message);
    }

    sealed class CountingSink : IWorldConsequenceSink
    {
        public int AppliedCount { get; private set; }

        public bool CanApply(WorldConsequenceEffect effect, out string error) =>
            WorldConsequenceRegistry.TryValidate(effect, out error);

        public bool TryApply(WorldConsequenceEffect effect, out string error)
        {
            if (!CanApply(effect, out error)) return false;
            AppliedCount++;
            return true;
        }
    }
}
