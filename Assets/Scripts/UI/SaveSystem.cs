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

        var ws = UnityEngine.Object.FindAnyObjectByType<WeatherSystem>();
        if (ws != null) data.weatherType = ws.current.ToString();

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

        GameClock.SetHour(data.timeOfDay);

        if (!string.IsNullOrEmpty(data.weatherType))
        {
            var ws = UnityEngine.Object.FindAnyObjectByType<WeatherSystem>();
            if (ws != null && Enum.TryParse<WeatherType>(data.weatherType, out var wt))
                ws.SetWeatherImmediate(wt);
        }
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
