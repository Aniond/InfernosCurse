using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.IO;

[Serializable]
public class SaveData
{
    public string slotName;
    public string sceneName;
    public float playerX, playerY, playerZ;
    public float playerFacingX, playerFacingY;
    public string subLocationId;
    [NonSerialized] public bool migratedLegacyInnScenePosition;
    public float timeOfDay;
    public string weatherType;
    public long savedAt; // UTC ticks

    // ── Guild-era additions (2026-07-03). Old saves deserialize these as
    // null/zero and the ApplySave guards skip them.
    public int florins;
    public int calYear;                 // 0 = field absent (old save) → skip
    public int calMonth;                // GameCalendar.Month index
    public int calDayOfMonth;
    public string driftDayKey;          // DailyCurseDrift idempotence across load
    public string lastDistrictNodeId;
    public string[] guildIds;
    public int[] guildReps;
    public string[] curseNodeIds;
    public float[] curseLevels;
    public float[] curseSanctity;

    // v3: independent per-location Circle influence. The three arrays are a
    // JsonUtility-compatible table and must always have identical lengths.
    public string[] influenceLocationIds;
    public int[] influenceCircleIds;
    public float[] influenceValues;

    // v4: ordinary slots point at, but do not own, the separately stored
    // Campaign Chronicle. The full canonical event ledger remains in the slot.
    public string campaignId;
    public long chronicleSequence;
    public WorldEventRecord[] worldEvents;

    // v5: unloaded world simulation and monotonic exploration discovery.
    public string worldSimulationDayKey;
    public PersistentWorldAgentRecord[] worldAgents;
    public NpcMemoryRecord[] npcMemory;
    public ExplorationDiscoveryRecord[] discoveries;

    // v6: influence arrays contain mutable owner territories only. Florence
    // sites resolve to firenze and aggregate regions are never serialized.
    public string circleSimulationDayKey;
    public CircleWarningRecord[] circleWarnings;
    public SiteOutcomeRecord[] siteOutcomes;

    // v8: player-owned Gugol Mappe knowledge. This stores authored or observed
    // last-known streets, never live NPC simulation positions.
    public GugolNpcMapKnowledgeRecord[] npcMapKnowledge;

    // ── v2 (2026-07-05): the combat-progression layer. Old saves have
    // saveVersion 0 and null arrays — all guarded. Parallel arrays indexed by
    // roster order (PartyRoster order is fixed). Nested data (unlocked skills,
    // absorbed skills) is pipe-joined per member because JsonUtility can't do
    // jagged arrays.
    public int saveVersion;             // 0 = pre-versioned save
    public string[] partyNames;
    public int[] partyHP;
    public int[] partySP;
    public string[] partyJobIds;        // "" = no active job
    public int[] partyJobLevels;
    public int[] partyJobXP;
    public int[] partyJobAP;
    public string[] partyUnlockedSkills;  // per member: "SkillA|SkillB|..."
    public string[] partyAbsorbed;        // per member: "Skill;dupCount;refined01|..."
}

public static class SaveSystem
{
    public const int SLOT_COUNT = 3;
    public const int CURRENT_VERSION = 8;

    // Mid-battle saves capture a scene with none of its encounter state —
    // loading one lands in the arena with no battle and no exit. Forbidden.
    static readonly string[] NoSaveScenes = { "Battle", "BattleArena" };

    static string SlotPath(int slot) =>
        Path.Combine(Application.persistentDataPath, $"save_slot_{slot}.json");

    public static bool CanSaveHere()
    {
        string scene = SceneManager.GetActiveScene().name;
        foreach (var s in NoSaveScenes) if (scene == s) return false;
        return true;
    }

    public static void Save(int slot)
    {
        if (slot < 1 || slot > SLOT_COUNT)
        {
            Debug.LogError($"[SaveSystem] Invalid slot {slot} (valid: 1..{SLOT_COUNT}).");
            return;
        }
        if (!CanSaveHere())
        {
            Debug.LogWarning("[SaveSystem] Saving is not available mid-battle.");
            return;
        }

        if (!CampaignChronicle.TryGetCurrentDocument(out var chronicle, out string chronicleError))
        {
            Debug.LogError("[SaveSystem] Save blocked: " + chronicleError);
            return;
        }
        if (!WorldEventLedger.TryReconcile(
                chronicle, 0, out long incorporatedSequence, out string reconciliationError))
        {
            Debug.LogError("[SaveSystem] Save blocked because permanent history could not be reconciled: " + reconciliationError);
            return;
        }

        var data = new SaveData();
        data.saveVersion = CURRENT_VERSION;
        data.slotName    = $"Slot {slot}";
        data.sceneName   = SceneManager.GetActiveScene().name;
        data.subLocationId = SeamlessInteriorRegistry.ActiveSubLocationId;
        data.savedAt     = DateTime.UtcNow.Ticks;
        data.campaignId = chronicle.campaignId;
        data.chronicleSequence = incorporatedSequence;
        data.worldEvents = WorldEventLedger.Export();
        data.worldAgents = PersistentLimboWorldState.ExportAgents();
        data.npcMemory = PersistentLimboWorldState.ExportNpcs();
        data.discoveries = ExplorationDiscoveryLedger.Export();
        data.circleWarnings = CircleWarningLedger.Export();
        data.siteOutcomes = SiteOutcomeState.Export();
        data.npcMapKnowledge = GugolNpcMapKnowledgeLedger.Export();
        var circleSimulation = UnityEngine.Object.FindAnyObjectByType<CircleWorldSimulationDirector>();
        if (circleSimulation != null) data.circleSimulationDayKey = circleSimulation.AppliedDayKey;

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            data.playerX = player.transform.position.x;
            data.playerY = player.transform.position.y;
            data.playerZ = player.transform.position.z;
            var controller = player.GetComponent<PlayerController>();
            if (controller != null)
            {
                data.playerFacingX = controller.Facing.x;
                data.playerFacingY = controller.Facing.y;
            }
        }

        // Persist real time-of-day (GameClock falls back to noon with no backend).
        data.timeOfDay = GameClock.Hour;

        data.weatherType = FlorenceWeather.CurrentProfileName;

        // Guild-era state (all null-guarded — systems may be absent in tests).
        data.florins = FlorinWallet.Balance;
        data.lastDistrictNodeId = DistrictTracker.CurrentNodeId;
        var cal = GameCalendar.Instance;
        if (cal != null)
        {
            data.calYear = cal.Year;
            data.calMonth = (int)cal.CurrentMonth;
            data.calDayOfMonth = cal.DayOfMonth;
        }
        if (GuildSystem.Instance != null)
            GuildSystem.Instance.ExportTo(out data.guildIds, out data.guildReps);
        if (HubMap.Instance != null)
        {
            // v6 writes mutable owner ledgers only. Legacy Limbo values remain
            // read-compatible for one version but are no longer emitted.
            HubMap.Instance.ExportSanctityStates(out data.curseNodeIds, out data.curseSanctity);
            data.curseLevels = Array.Empty<float>();
            HubMap.Instance.ExportInfluenceStates(
                out data.influenceLocationIds,
                out data.influenceCircleIds,
                out data.influenceValues);
        }

        ExportParty(data);

        // Atomic-ish write: never leave a half-written slot behind a crash.
        string path = SlotPath(slot);
        string tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonUtility.ToJson(data, true));
        if (File.Exists(path)) File.Replace(tmp, path, null);
        else File.Move(tmp, path);
        Debug.Log($"[SaveSystem] Saved slot {slot} to {path}");
    }

    public static SaveData Load(int slot)
    {
        string path = SlotPath(slot);
        if (!File.Exists(path)) return null;
        try
        {
            var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
            if (!TryValidateCampaignEnvelope(data, out string error))
            {
                Debug.LogError($"[SaveSystem] Slot {slot} cannot be loaded: {error}");
                return null;
            }
            MigrateLegacyLocation(data);
            return data;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveSystem] Slot {slot} is unreadable ({e.GetType().Name}: {e.Message}).");
            return null;
        }
    }

    // ── Load flow ──────────────────────────────────────────────────────────────

    // Loading must restore the SAVED scene first — applying positions and
    // per-scene state into whatever scene happens to be open teleports the
    // player into the wrong geometry. GameSystems persists across the load,
    // so world state is applied after the scene is ready.
    static SaveData _pendingApply;

    public static void LoadAndApply(SaveData data)
    {
        if (data == null) return;
        MigrateLegacyLocation(data);
        if (!TryValidateCampaignEnvelope(data, out string validationError))
        {
            Debug.LogError("[SaveSystem] Load blocked before scene change: " + validationError);
            return;
        }

        string active = SceneManager.GetActiveScene().name;
        if (!string.IsNullOrEmpty(data.sceneName) && data.sceneName != active)
        {
            if (!Application.CanStreamedLevelBeLoaded(data.sceneName))
            {
                Debug.LogError($"[SaveSystem] Saved scene '{data.sceneName}' is not loadable — applying in place.");
                ApplySave(data);
                return;
            }
            _pendingApply = data;
            SceneManager.sceneLoaded -= OnSceneLoadedApply;
            SceneManager.sceneLoaded += OnSceneLoadedApply;
            SceneManager.LoadScene(data.sceneName);
            return;
        }

        ApplySave(data);
    }

    static void OnSceneLoadedApply(Scene s, LoadSceneMode m)
    {
        SceneManager.sceneLoaded -= OnSceneLoadedApply;
        var data = _pendingApply;
        _pendingApply = null;
        if (data != null) ApplySave(data);
    }

    public static void ApplySave(SaveData data)
    {
        if (data == null)
        {
            Debug.LogError("[SaveSystem] Load blocked before state apply: save data is empty.");
            return;
        }
        if (!TryValidateCampaignEnvelope(data, out string validationError))
        {
            Debug.LogError("[SaveSystem] Load blocked before state apply: " + validationError);
            return;
        }

        if (string.IsNullOrEmpty(data.campaignId))
        {
            if (!CampaignChronicle.TryAdoptLegacySave(out data.campaignId, out string adoptionError))
            {
                Debug.LogError("[SaveSystem] Legacy save could not be assigned a safe campaign: " + adoptionError);
                return;
            }
            data.chronicleSequence = 0;
        }
        else if (!CampaignChronicle.TryActivate(data.campaignId, data.chronicleSequence, out string activationError))
        {
            Debug.LogError("[SaveSystem] Campaign Chronicle could not be activated: " + activationError);
            return;
        }

        if (!WorldEventLedger.Import(data.worldEvents, out string ledgerError))
        {
            Debug.LogError("[SaveSystem] World event ledger is invalid: " + ledgerError);
            return;
        }
        if (!CircleNarrativeState.TryImport(
                data.circleWarnings, data.siteOutcomes, out string circleNarrativeError))
        {
            Debug.LogError("[SaveSystem] Circle warning or site-outcome state is invalid: " + circleNarrativeError);
            return;
        }
        if (!PersistentLimboWorldState.Import(data.worldAgents, data.npcMemory, out string worldStateError))
        {
            Debug.LogError("[SaveSystem] Persistent Limbo world state is invalid: " + worldStateError);
            return;
        }
        if (!ExplorationDiscoveryLedger.Import(data.discoveries, out string discoveryError))
        {
            Debug.LogError("[SaveSystem] Exploration discovery state is invalid: " + discoveryError);
            return;
        }
        if (!GugolNpcMapKnowledgeLedger.Import(data.npcMapKnowledge, out string mapKnowledgeError))
        {
            Debug.LogError("[SaveSystem] Gugol NPC map knowledge is invalid: " + mapKnowledgeError);
            return;
        }

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            var pos = new Vector3(data.playerX, data.playerY, data.playerZ);
            bool interiorRestored = SeamlessInteriorRegistry.TryRestore(
                data.subLocationId, player.transform, out Vector3 recoveryPosition, out string interiorError);
            if (!interiorRestored)
            {
                pos = recoveryPosition;
                Debug.LogWarning($"[SaveSystem] Interior restore recovered outside: {interiorError}.");
            }
            else if (data.migratedLegacyInnScenePosition &&
                     SeamlessInteriorRegistry.TryGet(data.subLocationId, out SeamlessInteriorModule migratedModule))
            {
                // The standalone inn was authored with its module at the scene
                // origin. Preserve the old room position by treating the saved
                // coordinates as module-local when moving it into Mercato.
                pos = migratedModule.transform.TransformPoint(pos);
            }
            // Move via Rigidbody when present so physics interpolation doesn't
            // smear the teleport across a frame.
            var rb = player.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.position = pos;
                rb.linearVelocity = Vector3.zero;
            }
            player.transform.position = pos;

            if (data.saveVersion >= 7)
            {
                var facing = new Vector2(data.playerFacingX, data.playerFacingY);
                if (facing.sqrMagnitude > 0.01f)
                    player.GetComponent<PlayerController>()?.SetFacing(facing);
            }
        }

        // Restore date BEFORE hour: FlorenceWeather re-rolls off the date, and
        // the drift key must match the restored day.
        var cal = GameCalendar.Instance;
        if (cal != null && data.calYear > 0)
            cal.SetDate(data.calYear, (GameCalendar.Month)data.calMonth, data.calDayOfMonth);

        GameClock.SetHour(data.timeOfDay);
        // A backwards hour jump > 6h would otherwise read as a midnight wrap
        // and fire a spurious AdvanceDay on the next frame.
        if (cal != null) cal.ResyncClock();

        FlorinWallet.SetBalance(data.florins);
        if (!string.IsNullOrEmpty(data.lastDistrictNodeId))
            DistrictTracker.CurrentNodeId = data.lastDistrictNodeId;

        if (GuildSystem.Instance != null)
            GuildSystem.Instance.ImportFrom(data.guildIds, data.guildReps);
        if (HubMap.Instance != null)
        {
            if (!TryImportCircleState(data, HubMap.Instance, out string circleError))
            {
                Debug.LogError("[SaveSystem] Circle territory import failed: " + circleError);
                return;
            }
            HubMap.Instance.ImportSanctityStates(data.curseNodeIds, data.curseSanctity);
        }

        var circleSimulation = UnityEngine.Object.FindAnyObjectByType<CircleWorldSimulationDirector>();
        if (circleSimulation != null)
        {
            string simulationKey = !string.IsNullOrEmpty(data.circleSimulationDayKey)
                ? data.circleSimulationDayKey
                : !string.IsNullOrEmpty(data.worldSimulationDayKey)
                    ? data.worldSimulationDayKey
                    : data.driftDayKey;
            if (!string.IsNullOrEmpty(simulationKey)) circleSimulation.RestoreDayKey(simulationKey);
        }

        if (!string.IsNullOrEmpty(data.weatherType) && FlorenceWeather.Instance != null)
            FlorenceWeather.Instance.Apply(data.weatherType);

        ImportParty(data);

        if (!CampaignChronicle.TryGetCurrentDocument(out var chronicle, out string chronicleError))
        {
            Debug.LogError("[SaveSystem] Permanent-history reconciliation failed: " + chronicleError);
            return;
        }
        if (!WorldEventLedger.TryReconcile(
                chronicle, data.chronicleSequence, out long incorporatedSequence, out string reconcileError))
        {
            Debug.LogError("[SaveSystem] Permanent-history reconciliation failed: " + reconcileError);
            return;
        }
        data.chronicleSequence = incorporatedSequence;
        data.worldEvents = WorldEventLedger.Export();
        data.circleWarnings = CircleWarningLedger.Export();
        data.siteOutcomes = SiteOutcomeState.Export();
    }

    internal static bool MigrateLegacyLocation(SaveData data)
    {
        if (data == null || data.saveVersion >= 7) return false;
        if (!string.Equals(data.sceneName, "FlorentineInnFloor1", StringComparison.Ordinal)) return false;

        data.sceneName = "MercatoVecchio";
        data.subLocationId = "albergo_fiorentino_floor1";
        data.migratedLegacyInnScenePosition = true;
        Debug.Log("[SaveSystem] Migrated legacy Florentine Inn save into MercatoVecchio/albergo_fiorentino_floor1.");
        return true;
    }

    /// <summary>
    /// New Game entry point for the future title flow. Slot files are left
    /// untouched; only an explicit New Game creates an independent Chronicle.
    /// </summary>
    public static bool StartNewCampaign()
    {
        if (CampaignChronicle.StartNewCampaign(out string error))
        {
            PersistentLimboWorldState.Reset();
            ExplorationDiscoveryLedger.Reset();
            CircleWarningLedger.Reset();
            SiteOutcomeState.Reset();
            GugolNpcMapKnowledgeLedger.Reset();
            var director = UnityEngine.Object.FindAnyObjectByType<CircleWorldSimulationDirector>();
            if (director != null) director.RestoreDayKey(string.Empty);
            if (!FlorenceOpeningBaseline.Apply(out string baselineError))
                Debug.LogWarning("[SaveSystem] New campaign created, but Florence baseline is pending: " + baselineError);
            return true;
        }
        Debug.LogError("[SaveSystem] New campaign could not be created: " + error);
        return false;
    }

    static bool TryValidateCampaignEnvelope(SaveData data, out string error)
    {
        error = null;
        if (data == null)
        {
            error = "save data is empty.";
            return false;
        }
        if (data.chronicleSequence < 0)
        {
            error = "Chronicle sequence is negative.";
            return false;
        }
        if (string.IsNullOrEmpty(data.campaignId) && data.chronicleSequence != 0)
        {
            error = "Save has a Chronicle sequence but no campaign ID.";
            return false;
        }
        if (!WorldEventLedger.TryValidateRecords(data.worldEvents, out error)) return false;
        if (!CircleWarningLedger.TryValidateRecords(data.circleWarnings, out error)) return false;
        if (!SiteOutcomeState.TryValidateRecords(data.siteOutcomes, out error)) return false;
        if (!PersistentLimboWorldState.TryValidate(data.worldAgents, data.npcMemory, out error)) return false;
        if (!ExplorationDiscoveryLedger.TryValidateRecords(data.discoveries, out error)) return false;
        if (!GugolNpcMapKnowledgeLedger.TryValidateRecords(data.npcMapKnowledge, out error)) return false;

        bool anyCircleArray = data.influenceLocationIds != null ||
                              data.influenceCircleIds != null ||
                              data.influenceValues != null;
        if (anyCircleArray && !CircleTerritoryMigration.TryValidateInput(
                data.influenceLocationIds,
                data.influenceCircleIds,
                data.influenceValues,
                out error))
            return false;

        if (data.worldEvents != null)
        {
            foreach (var record in data.worldEvents)
            {
                if (record == null || !record.campaignPermanent) continue;
                if (string.IsNullOrEmpty(data.campaignId) ||
                    !string.Equals(record.campaignId, data.campaignId, StringComparison.Ordinal) ||
                    record.chronicleSequence > data.chronicleSequence)
                {
                    error = $"Permanent event '{record.eventInstanceId}' falls outside this save's Chronicle envelope.";
                    return false;
                }
            }
        }

        if (!string.IsNullOrEmpty(data.campaignId))
        {
            if (!CampaignChronicle.TryReadReference(
                    data.campaignId, data.chronicleSequence, out var chronicle, out error))
                return false;
            if (!WorldEventLedger.TryValidateChronicleCoverage(
                    data.worldEvents, chronicle, data.chronicleSequence, out error))
                return false;
        }
        return true;
    }

    static bool TryImportCircleState(SaveData data, HubMap hub, out string error)
    {
        error = null;
        bool hasCircleTable = data.influenceLocationIds != null &&
                              data.influenceCircleIds != null &&
                              data.influenceValues != null;

        string[] locationIds;
        int[] circleIds;
        float[] values;

        if (hasCircleTable && data.saveVersion >= 6)
        {
            if (!CircleTerritoryMigration.TryValidateInput(
                    data.influenceLocationIds,
                    data.influenceCircleIds,
                    data.influenceValues,
                    out error))
                return false;

            for (int i = 0; i < data.influenceLocationIds.Length; i++)
            {
                string locationId = data.influenceLocationIds[i];
                string ownerId = hub.ResolveInfluenceTerritoryId(locationId);
                if (!string.Equals(locationId, ownerId, StringComparison.Ordinal))
                {
                    error = $"v6 entry '{locationId}' is not a mutable Circle territory owner.";
                    return false;
                }
            }
            locationIds = data.influenceLocationIds;
            circleIds = data.influenceCircleIds;
            values = data.influenceValues;
        }
        else
        {
            string[] legacyLocations;
            int[] legacyCircles;
            float[] legacyValues;

            if (hasCircleTable)
            {
                legacyLocations = data.influenceLocationIds;
                legacyCircles = data.influenceCircleIds;
                legacyValues = data.influenceValues;
            }
            else if (data.curseNodeIds != null && data.curseLevels != null &&
                     data.curseNodeIds.Length == data.curseLevels.Length)
            {
                legacyLocations = data.curseNodeIds;
                legacyValues = data.curseLevels;
                legacyCircles = new int[legacyLocations.Length];
                for (int i = 0; i < legacyCircles.Length; i++)
                    legacyCircles[i] = (int)CircleId.Limbo;
            }
            else if (data.curseNodeIds == null && data.curseLevels == null)
            {
                return true; // Pre-Circle save: retain the authored New Game baseline.
            }
            else
            {
                error = "Legacy Limbo arrays are malformed.";
                return false;
            }

            if (!CircleTerritoryMigration.TryMigrateToOwners(
                    hub,
                    legacyLocations,
                    legacyCircles,
                    legacyValues,
                    out locationIds,
                    out circleIds,
                    out values,
                    out error))
                return false;
        }

        return hub.ImportInfluenceStates(locationIds, circleIds, values);
    }

    // ── Party progression (v2) ─────────────────────────────────────────────────

    static void ExportParty(SaveData data)
    {
        var party = RestSystem.PartyMembers;
        int n = party.Count;
        if (n == 0) return;   // roster never initialized this session — skip

        data.partyNames = new string[n];
        data.partyHP = new int[n];
        data.partySP = new int[n];
        data.partyJobIds = new string[n];
        data.partyJobLevels = new int[n];
        data.partyJobXP = new int[n];
        data.partyJobAP = new int[n];
        data.partyUnlockedSkills = new string[n];
        data.partyAbsorbed = new string[n];

        for (int i = 0; i < n; i++)
        {
            var m = party[i];
            data.partyNames[i] = m.displayName;
            data.partyHP[i] = m.currentHP;
            data.partySP[i] = m.currentSP;

            var job = m.activeJob;
            bool hasJob = job != null && job.job != null;
            data.partyJobIds[i] = hasJob ? job.job.name : "";
            data.partyJobLevels[i] = hasJob ? job.jobLevel : 0;
            data.partyJobXP[i] = hasJob ? job.currentXP : 0;
            data.partyJobAP[i] = hasJob ? job.currentAP : 0;

            string unlocked = "";
            if (hasJob)
                foreach (var learned in job.learnedSkills)
                    if (learned.unlocked && learned.skill != null)
                        unlocked += (unlocked.Length > 0 ? "|" : "") + learned.skill.name;
            data.partyUnlockedSkills[i] = unlocked;

            string absorbed = "";
            foreach (var a in m.absorbedSkills)
                if (a != null && a.definition != null)
                    absorbed += (absorbed.Length > 0 ? "|" : "") +
                                $"{a.definition.name};{a.duplicateCount};{(a.isRefined ? 1 : 0)}";
            data.partyAbsorbed[i] = absorbed;
        }
    }

    static void ImportParty(SaveData data)
    {
        if (data.partyNames == null || data.partyNames.Length == 0) return;   // pre-v2 save

        PartyRoster.EnsureInitialized();
        var party = RestSystem.PartyMembers;
        var registry = AssetRegistry.Instance;

        int n = Mathf.Min(party.Count, data.partyNames.Length);
        for (int i = 0; i < n; i++)
        {
            var m = party[i];
            if (m.displayName != data.partyNames[i])
                Debug.LogWarning($"[SaveSystem] Roster slot {i}: '{m.displayName}' vs saved '{data.partyNames[i]}' — applying by index.");

            // Job first (level affects max HP), then unlocked flags, then HP/SP.
            string jobId = data.partyJobIds != null && i < data.partyJobIds.Length ? data.partyJobIds[i] : "";
            if (!string.IsNullOrEmpty(jobId))
            {
                if (m.activeJob == null || m.activeJob.job == null || m.activeJob.job.name != jobId)
                {
                    var jobDef = registry != null ? registry.FindJob(jobId) : null;
                    if (jobDef != null) m.EquipJob(jobDef);
                }
                var job = m.activeJob;
                if (job != null && job.job != null && job.job.name == jobId)
                {
                    job.jobLevel = data.partyJobLevels[i];
                    job.currentXP = data.partyJobXP[i];
                    job.currentAP = data.partyJobAP[i];

                    string unlockedList = data.partyUnlockedSkills != null && i < data.partyUnlockedSkills.Length
                        ? data.partyUnlockedSkills[i] : "";
                    var unlockedSet = new System.Collections.Generic.HashSet<string>(
                        unlockedList.Split('|', StringSplitOptions.RemoveEmptyEntries));
                    foreach (var learned in job.learnedSkills)
                        if (learned.skill != null)
                            learned.unlocked = unlockedSet.Contains(learned.skill.name);
                }
            }

            // Absorbed skills (Benidito's Benidito-role progression).
            string absorbedList = data.partyAbsorbed != null && i < data.partyAbsorbed.Length
                ? data.partyAbsorbed[i] : "";
            if (absorbedList != null)
            {
                m.absorbedSkills.Clear();
                foreach (var entry in absorbedList.Split('|', StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = entry.Split(';');
                    if (parts.Length < 3) continue;
                    var def = registry != null ? registry.FindSkill(parts[0]) : null;
                    if (def == null) continue;
                    int dup = Mathf.Max(1, int.TryParse(parts[1], out var d) ? d : 1);
                    // AddDuplicate refreshes the level from the count.
                    var inst = new AbsorbedSkillInstance
                    {
                        definition = def,
                        duplicateCount = dup - 1,
                        isRefined = parts[2] == "1",
                    };
                    inst.AddDuplicate();
                    m.absorbedSkills.Add(inst);
                }
            }

            // HP/SP last, clamped to the (possibly job-boosted) maxima.
            var stats = m.GetTotalStats();
            m.currentHP = Mathf.Clamp(data.partyHP[i], 0, stats.hpMax);
            m.currentSP = Mathf.Clamp(data.partySP[i], 0, stats.spMax);
        }
        Debug.Log($"[SaveSystem] Party progression restored for {n} member(s).");
    }

    public static bool SlotExists(int slot) => File.Exists(SlotPath(slot));

    public static string SlotLabel(int slot)
    {
        var data = Load(slot);
        if (data == null)
            return SlotExists(slot) ? $"Slot {slot}  —  Corrupt" : $"Slot {slot}  —  Empty";
        var dt = new DateTime(data.savedAt, DateTimeKind.Utc).ToLocalTime();
        return $"Slot {slot}  —  {data.sceneName}  |  {dt:MMM d  h:mm tt}";
    }
}
