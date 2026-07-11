using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using UnityEngine;

[Serializable]
public sealed class CampaignChronicleEntry
{
    public string campaignId;
    public long sequence;
    public string eventInstanceId;
    public string eventTypeId;
    public string choiceId;
    public string gameDateKey;
    public string locationId;
    public string[] npcIds = Array.Empty<string>();
    public string[] worldAgentIds = Array.Empty<string>();
    public WorldConsequenceEffect[] consequences = Array.Empty<WorldConsequenceEffect>();
    public string factualOutcome;
    public string[] semanticTags = Array.Empty<string>();
    public string previousHash;
    public string currentHash;
}

[Serializable]
public sealed class CampaignChronicleDocument
{
    public const int CurrentFormatVersion = 1;

    public int formatVersion = CurrentFormatVersion;
    public string campaignId;
    public long createdAtUtcTicks;
    public long lastSequence;
    public CampaignChronicleEntry[] entries = Array.Empty<CampaignChronicleEntry>();
}

[Serializable]
sealed class ActiveCampaignPointer
{
    public int formatVersion = 1;
    public string campaignId;
}

/// <summary>
/// Durable, testable store for the campaign-permanent append-only Chronicle.
/// The primary and rolling backup are both validated before they are trusted.
/// </summary>
public sealed class CampaignChronicleStore
{
    const string ChronicleExtension = ".chronicle.json";
    const string BackupExtension = ".chronicle.bak";
    const string ActivePointerName = "active_campaign.json";
    const string ActivePointerBackupName = "active_campaign.bak";

    public string RootPath { get; }
    public bool HasActivePointerFiles =>
        File.Exists(Path.Combine(RootPath, ActivePointerName)) ||
        File.Exists(Path.Combine(RootPath, ActivePointerBackupName));

    public CampaignChronicleStore(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
            throw new ArgumentException("Chronicle root is required.", nameof(rootPath));
        RootPath = Path.GetFullPath(rootPath);
    }

    public string GetPrimaryPath(string campaignId) =>
        Path.Combine(RootPath, ValidateCampaignIdOrThrow(campaignId) + ChronicleExtension);

    public string GetBackupPath(string campaignId) =>
        Path.Combine(RootPath, ValidateCampaignIdOrThrow(campaignId) + BackupExtension);

    public bool TryCreateCampaign(out CampaignChronicleDocument document, out string error)
    {
        document = new CampaignChronicleDocument
        {
            campaignId = Guid.NewGuid().ToString("N"),
            createdAtUtcTicks = DateTime.UtcNow.Ticks,
            lastSequence = 0,
            entries = Array.Empty<CampaignChronicleEntry>(),
        };

        if (!TryWriteDocument(document, out error)) return false;
        if (!TrySetActiveCampaign(document.campaignId, out error)) return false;
        return true;
    }

    public bool TryLoad(
        string campaignId,
        long minimumSequence,
        out CampaignChronicleDocument document,
        out bool restoredBackup,
        out string error)
    {
        document = null;
        restoredBackup = false;
        error = null;
        if (!TryValidateCampaignId(campaignId, out error)) return false;
        if (minimumSequence < 0)
        {
            error = "Chronicle sequence cannot be negative.";
            return false;
        }

        string primaryPath = GetPrimaryPath(campaignId);
        if (TryReadDocument(primaryPath, campaignId, minimumSequence, out document, out string primaryError))
            return true;

        string backupPath = GetBackupPath(campaignId);
        if (!TryReadDocument(backupPath, campaignId, minimumSequence, out document, out string backupError))
        {
            error = $"Campaign Chronicle '{campaignId}' is unavailable. Primary: {primaryError} Backup: {backupError}";
            document = null;
            return false;
        }

        if (!TryRestorePrimary(document, out string restoreError))
        {
            error = $"The Chronicle backup is valid but the primary could not be restored: {restoreError}";
            document = null;
            return false;
        }

        restoredBackup = true;
        return true;
    }

    public bool TryAppend(
        string campaignId,
        CampaignChronicleEntry draft,
        out CampaignChronicleDocument updatedDocument,
        out CampaignChronicleEntry committedEntry,
        out string error)
    {
        updatedDocument = null;
        committedEntry = null;
        error = null;
        if (draft == null)
        {
            error = "Chronicle entry is required.";
            return false;
        }
        if (!TryLoad(campaignId, 0, out var document, out _, out error)) return false;

        foreach (var existing in document.entries)
        {
            if (!string.Equals(existing.eventInstanceId, draft.eventInstanceId, StringComparison.Ordinal))
                continue;
            if (!string.Equals(existing.eventTypeId, draft.eventTypeId, StringComparison.Ordinal) ||
                !string.Equals(existing.choiceId, draft.choiceId, StringComparison.Ordinal))
            {
                error = $"Permanent event '{draft.eventInstanceId}' was already committed with a different outcome.";
                return false;
            }

            committedEntry = CloneEntry(existing);
            updatedDocument = CloneDocument(document);
            // A retry after an interrupted backup mirror repairs the mirror
            // before the established choice is reported as committed.
            return TryMirrorBackup(document, out error);
        }

        var entry = CloneEntry(draft);
        entry.campaignId = campaignId;
        entry.sequence = document.lastSequence + 1;
        entry.previousHash = document.entries.Length == 0
            ? string.Empty
            : document.entries[document.entries.Length - 1].currentHash;
        entry.currentHash = ComputeEntryHash(entry);

        var entries = new CampaignChronicleEntry[document.entries.Length + 1];
        Array.Copy(document.entries, entries, document.entries.Length);
        entries[entries.Length - 1] = entry;
        document.entries = entries;
        document.lastSequence = entry.sequence;

        if (!TryWriteDocument(document, out error)) return false;
        updatedDocument = CloneDocument(document);
        committedEntry = CloneEntry(entry);
        return true;
    }

    public bool TrySetActiveCampaign(string campaignId, out string error)
    {
        error = null;
        if (!TryValidateCampaignId(campaignId, out error)) return false;
        Directory.CreateDirectory(RootPath);
        var pointer = new ActiveCampaignPointer { campaignId = campaignId };
        string json = JsonUtility.ToJson(pointer, true);
        string primary = Path.Combine(RootPath, ActivePointerName);
        string backup = Path.Combine(RootPath, ActivePointerBackupName);
        if (!TryWriteAndReplace(json, primary, ValidatePointerFile, out error)) return false;
        if (!TryWriteAndReplace(json, backup, ValidatePointerFile, out error)) return false;
        return true;
    }

    public bool TryGetActiveCampaign(out string campaignId, out string error)
    {
        campaignId = null;
        error = null;
        string primary = Path.Combine(RootPath, ActivePointerName);
        if (TryReadPointer(primary, out campaignId, out _)) return true;

        string backup = Path.Combine(RootPath, ActivePointerBackupName);
        if (!TryReadPointer(backup, out campaignId, out string backupError))
        {
            error = "No valid active campaign pointer exists. " + backupError;
            campaignId = null;
            return false;
        }

        var pointer = new ActiveCampaignPointer { campaignId = campaignId };
        if (!TryWriteAndReplace(JsonUtility.ToJson(pointer, true), primary, ValidatePointerFile, out error))
        {
            campaignId = null;
            return false;
        }
        return true;
    }

    public static bool ValidateDocument(
        CampaignChronicleDocument document,
        string expectedCampaignId,
        long minimumSequence,
        out string error)
    {
        error = null;
        if (document == null)
        {
            error = "Document is null.";
            return false;
        }
        if (document.formatVersion != CampaignChronicleDocument.CurrentFormatVersion)
        {
            error = $"Unsupported Chronicle format {document.formatVersion}.";
            return false;
        }
        if (!TryValidateCampaignId(document.campaignId, out error)) return false;
        if (!string.Equals(document.campaignId, expectedCampaignId, StringComparison.Ordinal))
        {
            error = "Campaign ID does not match the requested Chronicle.";
            return false;
        }
        if (document.createdAtUtcTicks <= 0)
        {
            error = "Chronicle creation time is invalid.";
            return false;
        }

        document.entries ??= Array.Empty<CampaignChronicleEntry>();
        if (document.lastSequence != document.entries.Length || document.lastSequence < minimumSequence)
        {
            error = $"Chronicle sequence {document.lastSequence} is inconsistent or older than required {minimumSequence}.";
            return false;
        }

        string previousHash = string.Empty;
        var eventIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < document.entries.Length; i++)
        {
            var entry = document.entries[i];
            if (entry == null)
            {
                error = $"Chronicle entry {i + 1} is null.";
                return false;
            }
            if (entry.sequence != i + 1 || !string.Equals(entry.campaignId, document.campaignId, StringComparison.Ordinal))
            {
                error = $"Chronicle entry {i + 1} has a sequence or campaign mismatch.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(entry.eventInstanceId) ||
                string.IsNullOrWhiteSpace(entry.eventTypeId) ||
                string.IsNullOrWhiteSpace(entry.choiceId) ||
                string.IsNullOrWhiteSpace(entry.gameDateKey) ||
                string.IsNullOrWhiteSpace(entry.locationId))
            {
                error = $"Chronicle entry {entry.sequence} is missing required authored identity.";
                return false;
            }
            if (!eventIds.Add(entry.eventInstanceId))
            {
                error = $"Chronicle event '{entry.eventInstanceId}' appears more than once.";
                return false;
            }
            if (!string.Equals(entry.previousHash ?? string.Empty, previousHash, StringComparison.Ordinal))
            {
                error = $"Chronicle entry {entry.sequence} breaks the previous-hash chain.";
                return false;
            }

            entry.npcIds ??= Array.Empty<string>();
            entry.worldAgentIds ??= Array.Empty<string>();
            entry.consequences ??= Array.Empty<WorldConsequenceEffect>();
            entry.semanticTags ??= Array.Empty<string>();
            foreach (var effect in entry.consequences)
            {
                if (effect == null || string.IsNullOrWhiteSpace(effect.effectId) ||
                    float.IsNaN(effect.numericValue) || float.IsInfinity(effect.numericValue))
                {
                    error = $"Chronicle entry {entry.sequence} contains an invalid consequence payload.";
                    return false;
                }
            }

            string expectedHash = ComputeEntryHash(entry);
            if (!string.Equals(entry.currentHash, expectedHash, StringComparison.Ordinal))
            {
                error = $"Chronicle entry {entry.sequence} failed hash validation.";
                return false;
            }
            previousHash = entry.currentHash;
        }
        return true;
    }

    public static string ComputeEntryHash(CampaignChronicleEntry entry)
    {
        if (entry == null) return string.Empty;
        var builder = new StringBuilder(1024);
        AppendField(builder, "campaignId", entry.campaignId);
        AppendField(builder, "sequence", entry.sequence.ToString(CultureInfo.InvariantCulture));
        AppendField(builder, "eventInstanceId", entry.eventInstanceId);
        AppendField(builder, "eventTypeId", entry.eventTypeId);
        AppendField(builder, "choiceId", entry.choiceId);
        AppendField(builder, "gameDateKey", entry.gameDateKey);
        AppendField(builder, "locationId", entry.locationId);
        AppendArray(builder, "npcIds", entry.npcIds);
        AppendArray(builder, "worldAgentIds", entry.worldAgentIds);

        var effects = entry.consequences ?? Array.Empty<WorldConsequenceEffect>();
        AppendField(builder, "consequenceCount", effects.Length.ToString(CultureInfo.InvariantCulture));
        for (int i = 0; i < effects.Length; i++)
        {
            var effect = effects[i] ?? new WorldConsequenceEffect();
            string prefix = "effect" + i.ToString(CultureInfo.InvariantCulture) + ".";
            AppendField(builder, prefix + "effectId", effect.effectId);
            AppendField(builder, prefix + "targetId", effect.targetId);
            AppendField(builder, prefix + "secondaryId", effect.secondaryId);
            AppendField(builder, prefix + "intValue", effect.intValue.ToString(CultureInfo.InvariantCulture));
            AppendField(builder, prefix + "numericValue", effect.numericValue.ToString("R", CultureInfo.InvariantCulture));
            AppendField(builder, prefix + "stringValue", effect.stringValue);
        }

        AppendField(builder, "factualOutcome", entry.factualOutcome);
        AppendArray(builder, "semanticTags", entry.semanticTags);
        AppendField(builder, "previousHash", entry.previousHash);

        using (var sha = SHA256.Create())
        {
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
            var hex = new StringBuilder(bytes.Length * 2);
            foreach (byte value in bytes) hex.Append(value.ToString("x2", CultureInfo.InvariantCulture));
            return hex.ToString();
        }
    }

    static void AppendArray(StringBuilder builder, string name, string[] values)
    {
        values ??= Array.Empty<string>();
        AppendField(builder, name + ".count", values.Length.ToString(CultureInfo.InvariantCulture));
        for (int i = 0; i < values.Length; i++)
            AppendField(builder, name + "." + i.ToString(CultureInfo.InvariantCulture), values[i]);
    }

    static void AppendField(StringBuilder builder, string name, string value)
    {
        value ??= string.Empty;
        builder.Append(name).Append('=').Append(value.Length.ToString(CultureInfo.InvariantCulture))
            .Append(':').Append(value).Append('\n');
    }

    bool TryWriteDocument(CampaignChronicleDocument document, out string error)
    {
        error = null;
        if (!ValidateDocument(document, document?.campaignId, 0, out error)) return false;
        Directory.CreateDirectory(RootPath);
        string json = JsonUtility.ToJson(document, true);
        string primary = GetPrimaryPath(document.campaignId);
        if (!TryWriteAndReplace(json, primary, ValidateChronicleFile, out error)) return false;
        return TryMirrorBackup(document, out error);
    }

    bool TryMirrorBackup(CampaignChronicleDocument document, out string error)
    {
        string json = JsonUtility.ToJson(document, true);
        return TryWriteAndReplace(json, GetBackupPath(document.campaignId), ValidateChronicleFile, out error);
    }

    bool TryRestorePrimary(CampaignChronicleDocument document, out string error)
    {
        string json = JsonUtility.ToJson(document, true);
        return TryWriteAndReplace(json, GetPrimaryPath(document.campaignId), ValidateChronicleFile, out error);
    }

    delegate bool FileValidator(string path, out string error);

    bool TryWriteAndReplace(string contents, string destination, FileValidator validator, out string error)
    {
        error = null;
        string temporary = destination + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            WriteDurable(temporary, contents);
            if (!validator(temporary, out error))
            {
                TryDelete(temporary);
                return false;
            }
            ReplaceFile(temporary, destination);
            return true;
        }
        catch (Exception exception)
        {
            TryDelete(temporary);
            error = $"{exception.GetType().Name}: {exception.Message}";
            return false;
        }
    }

    bool ValidateChronicleFile(string path, out string error)
    {
        try
        {
            var document = JsonUtility.FromJson<CampaignChronicleDocument>(File.ReadAllText(path));
            return ValidateDocument(document, document?.campaignId, 0, out error);
        }
        catch (Exception exception)
        {
            error = $"{exception.GetType().Name}: {exception.Message}";
            return false;
        }
    }

    bool ValidatePointerFile(string path, out string error) => TryReadPointer(path, out _, out error);

    bool TryReadDocument(
        string path,
        string campaignId,
        long minimumSequence,
        out CampaignChronicleDocument document,
        out string error)
    {
        document = null;
        error = null;
        if (!File.Exists(path))
        {
            error = "file is missing.";
            return false;
        }
        try
        {
            document = JsonUtility.FromJson<CampaignChronicleDocument>(File.ReadAllText(path));
            if (!ValidateDocument(document, campaignId, minimumSequence, out error))
            {
                document = null;
                return false;
            }
            return true;
        }
        catch (Exception exception)
        {
            error = $"{exception.GetType().Name}: {exception.Message}";
            document = null;
            return false;
        }
    }

    bool TryReadPointer(string path, out string campaignId, out string error)
    {
        campaignId = null;
        error = null;
        if (!File.Exists(path))
        {
            error = "pointer file is missing.";
            return false;
        }
        try
        {
            var pointer = JsonUtility.FromJson<ActiveCampaignPointer>(File.ReadAllText(path));
            if (pointer == null || pointer.formatVersion != 1 || !TryValidateCampaignId(pointer.campaignId, out error))
                return false;
            campaignId = pointer.campaignId;
            return true;
        }
        catch (Exception exception)
        {
            error = $"{exception.GetType().Name}: {exception.Message}";
            return false;
        }
    }

    static void WriteDurable(string path, string contents)
    {
        using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, true))
        {
            writer.Write(contents);
            writer.Flush();
            stream.Flush(true);
        }
    }

    static void ReplaceFile(string source, string destination)
    {
        // Atomic replace can briefly lose a race to antivirus, cloud sync, or
        // an indexer on Windows. The durable temp file remains valid, so retry
        // the same atomic operation for just under one second before reporting
        // a genuine save failure.
        const int maxAttempts = 7;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                if (!File.Exists(destination))
                    File.Move(source, destination);
                else
                    File.Replace(source, destination, null);
                return;
            }
            catch (PlatformNotSupportedException)
            {
                File.Copy(source, destination, true);
                File.Delete(source);
                return;
            }
            catch (IOException) when (attempt < maxAttempts - 1)
            {
                Thread.Sleep(15 << attempt);
            }
        }
    }

    static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* A stale .tmp is ignored and replaced on the next write. */ }
    }

    static string ValidateCampaignIdOrThrow(string campaignId)
    {
        if (!TryValidateCampaignId(campaignId, out string error)) throw new ArgumentException(error, nameof(campaignId));
        return campaignId;
    }

    static bool TryValidateCampaignId(string campaignId, out string error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(campaignId) || !Guid.TryParseExact(campaignId, "N", out _))
        {
            error = "Campaign ID is missing or malformed.";
            return false;
        }
        return true;
    }

    public static CampaignChronicleDocument CloneDocument(CampaignChronicleDocument source) =>
        source == null ? null : JsonUtility.FromJson<CampaignChronicleDocument>(JsonUtility.ToJson(source));

    public static CampaignChronicleEntry CloneEntry(CampaignChronicleEntry source) =>
        source == null ? null : JsonUtility.FromJson<CampaignChronicleEntry>(JsonUtility.ToJson(source));
}

/// <summary>Runtime facade for the active campaign Chronicle.</summary>
public static class CampaignChronicle
{
    public const string FirstPermanentChoiceNotice = "This choice will be remembered throughout this journey.";
    public const string LaterPermanentChoiceMarker = "Remembered choice";

    static readonly object Gate = new();
    static CampaignChronicleStore _store;
    static CampaignChronicleDocument _current;

    static CampaignChronicleStore Store => _store ??=
        new CampaignChronicleStore(Path.Combine(Application.persistentDataPath, "CampaignChronicles"));

    public static string CurrentCampaignId => _current?.campaignId ?? string.Empty;
    public static long CurrentSequence => _current?.lastSequence ?? 0;
    public static string PermanentChoiceNotice => CurrentSequence == 0
        ? FirstPermanentChoiceNotice
        : LaterPermanentChoiceMarker;

    public static bool TryEnsureActive(out string error)
    {
        lock (Gate)
        {
            error = null;
            if (_current != null) return true;
            bool hasPointerState = Store.HasActivePointerFiles;
            if (Store.TryGetActiveCampaign(out string campaignId, out string pointerError))
            {
                if (!Store.TryLoad(campaignId, 0, out _current, out bool restored, out error))
                    return false;
                if (restored) Debug.LogWarning("[CampaignChronicle] Restored the active Chronicle from its rolling backup.");
                return true;
            }

            if (hasPointerState)
            {
                error = "Active campaign history is unreadable: " + pointerError;
                return false;
            }

            if (!Store.TryCreateCampaign(out _current, out error)) return false;
            Debug.Log($"[CampaignChronicle] Created development campaign {_current.campaignId}.");
            return true;
        }
    }

    public static bool StartNewCampaign(out string error)
    {
        lock (Gate)
        {
            if (!Store.TryCreateCampaign(out _current, out error)) return false;
            WorldEventLedger.Reset();
            Debug.Log($"[CampaignChronicle] New Game created campaign {_current.campaignId}.");
            return true;
        }
    }

    public static bool TryActivate(string campaignId, long minimumSequence, out string error)
    {
        lock (Gate)
        {
            if (!Store.TryLoad(campaignId, minimumSequence, out var document, out bool restored, out error))
                return false;
            if (!Store.TrySetActiveCampaign(campaignId, out error)) return false;
            _current = document;
            if (restored) Debug.LogWarning("[CampaignChronicle] Restored the selected campaign from its rolling backup.");
            return true;
        }
    }

    public static bool TryValidateReference(string campaignId, long minimumSequence, out string error)
    {
        lock (Gate)
        {
            if (string.IsNullOrEmpty(campaignId))
            {
                error = null; // Legacy pre-Chronicle save; adopted on apply.
                return true;
            }
            return Store.TryLoad(campaignId, minimumSequence, out _, out _, out error);
        }
    }

    public static bool TryReadReference(
        string campaignId,
        long minimumSequence,
        out CampaignChronicleDocument document,
        out string error)
    {
        lock (Gate)
            return Store.TryLoad(campaignId, minimumSequence, out document, out _, out error);
    }

    public static bool TryAdoptLegacySave(out string campaignId, out string error)
    {
        lock (Gate)
        {
            if (!TryEnsureActive(out error))
            {
                campaignId = null;
                return false;
            }
            if (_current.lastSequence > 0 && !Store.TryCreateCampaign(out _current, out error))
            {
                campaignId = null;
                return false;
            }
            campaignId = _current.campaignId;
            return true;
        }
    }

    public static bool TryCommit(
        WorldEventRecord record,
        out CampaignChronicleEntry committedEntry,
        out string error)
    {
        lock (Gate)
        {
            committedEntry = null;
            if (record == null || !record.campaignPermanent)
            {
                error = "Only Campaign-Permanent world events belong in the Chronicle.";
                return false;
            }
            if (!TryEnsureActive(out error)) return false;

            var draft = new CampaignChronicleEntry
            {
                eventInstanceId = record.eventInstanceId,
                eventTypeId = record.eventTypeId,
                choiceId = record.choiceId,
                gameDateKey = record.gameDateKey,
                locationId = record.locationId,
                npcIds = CloneStrings(record.npcIds),
                worldAgentIds = CloneStrings(record.worldAgentIds),
                consequences = WorldEventRecord.CloneEffects(record.consequences),
                factualOutcome = record.factualOutcome,
                semanticTags = CloneStrings(record.semanticTags),
            };

            if (!Store.TryAppend(_current.campaignId, draft, out _current, out committedEntry, out error))
                return false;
            return true;
        }
    }

    public static bool TryGetCommittedChoice(string eventInstanceId, out string choiceId)
    {
        lock (Gate)
        {
            choiceId = null;
            if (!TryEnsureActive(out _)) return false;
            foreach (var entry in _current.entries)
            {
                if (!string.Equals(entry.eventInstanceId, eventInstanceId, StringComparison.Ordinal)) continue;
                choiceId = entry.choiceId;
                return true;
            }
            return false;
        }
    }

    public static bool TryGetCurrentDocument(out CampaignChronicleDocument document, out string error)
    {
        lock (Gate)
        {
            document = null;
            if (!TryEnsureActive(out error)) return false;
            document = CampaignChronicleStore.CloneDocument(_current);
            return true;
        }
    }

    public static void ResetSessionCache()
    {
        lock (Gate) _current = null;
    }

    static string[] CloneStrings(string[] values)
    {
        if (values == null || values.Length == 0) return Array.Empty<string>();
        var copy = new string[values.Length];
        Array.Copy(values, copy, values.Length);
        return copy;
    }
}
