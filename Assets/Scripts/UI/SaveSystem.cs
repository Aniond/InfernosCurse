using UnityEngine;
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
    // null/zero and the ApplySave guards skip them. Job progress and absorbed
    // skills remain unpersisted — pre-existing debt, tracked separately.
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
}

public static class SaveSystem
{
    public const int SLOT_COUNT = 3;

    static string SlotPath(int slot) =>
        Path.Combine(Application.persistentDataPath, $"save_slot_{slot}.json");

    public static void Save(int slot)
    {
        var data = new SaveData();
        data.slotName    = $"Slot {slot}";
        data.sceneName   = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
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

        File.WriteAllText(SlotPath(slot), JsonUtility.ToJson(data, true));
        Debug.Log($"[SaveSystem] Saved slot {slot} to {SlotPath(slot)}");
    }

    public static SaveData Load(int slot)
    {
        string path = SlotPath(slot);
        if (!File.Exists(path)) return null;
        return JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
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
    }

    public static bool SlotExists(int slot) => File.Exists(SlotPath(slot));

    public static string SlotLabel(int slot)
    {
        var data = Load(slot);
        if (data == null) return $"Slot {slot}  —  Empty";
        var dt = new DateTime(data.savedAt, DateTimeKind.Utc).ToLocalTime();
        return $"Slot {slot}  —  {data.sceneName}  |  {dt:MMM d  h:mm tt}";
    }
}
