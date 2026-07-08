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
    public const int CURRENT_VERSION = 2;

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

        var data = new SaveData();
        data.saveVersion = CURRENT_VERSION;
        data.slotName    = $"Slot {slot}";
        data.sceneName   = SceneManager.GetActiveScene().name;
        data.savedAt     = DateTime.UtcNow.Ticks;

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            data.playerX = player.transform.position.x;
            data.playerY = player.transform.position.y;
            data.playerZ = player.transform.position.z;
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
        var drift = UnityEngine.Object.FindAnyObjectByType<DailyCurseDrift>();
        if (drift != null) data.driftDayKey = drift.AppliedDayKey;
        if (GuildSystem.Instance != null)
            GuildSystem.Instance.ExportTo(out data.guildIds, out data.guildReps);
        if (HubMap.Instance != null)
            HubMap.Instance.ExportNodeStates(out data.curseNodeIds, out data.curseLevels, out data.curseSanctity);

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
            return JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
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
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            var pos = new Vector3(data.playerX, data.playerY, data.playerZ);
            // Move via Rigidbody when present so physics interpolation doesn't
            // smear the teleport across a frame.
            var rb = player.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.position = pos;
                rb.linearVelocity = Vector3.zero;
            }
            player.transform.position = pos;
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
            HubMap.Instance.ImportNodeStates(data.curseNodeIds, data.curseLevels, data.curseSanctity);

        var drift = UnityEngine.Object.FindAnyObjectByType<DailyCurseDrift>();
        if (drift != null && !string.IsNullOrEmpty(data.driftDayKey))
            drift.RestoreDayKey(data.driftDayKey);

        if (!string.IsNullOrEmpty(data.weatherType) && FlorenceWeather.Instance != null)
            FlorenceWeather.Instance.Apply(data.weatherType);

        ImportParty(data);
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
