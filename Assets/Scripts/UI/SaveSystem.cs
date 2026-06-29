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
    const int SLOT_COUNT = 3;

    static string SlotPath(int slot) =>
        Path.Combine(Application.persistentDataPath, $"save_slot_{slot}.json");

    public static void Save(int slot)
    {
        var data = new SaveData();
        data.slotName    = $"Slot {slot}";
        data.sceneName   = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        data.savedAt     = DateTime.UtcNow.Ticks;
        data.timeOfDay   = 12f;

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            data.playerX = player.transform.position.x;
            data.playerY = player.transform.position.y;
            data.playerZ = player.transform.position.z;
        }

        var dnc = UnityEngine.Object.FindAnyObjectByType<DayNightCycle>();
        if (dnc != null) data.timeOfDay = dnc.timeOfDay;

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
            player.transform.position = new Vector3(data.playerX, data.playerY, data.playerZ);

        var dnc = UnityEngine.Object.FindAnyObjectByType<DayNightCycle>();
        if (dnc != null) dnc.timeOfDay = data.timeOfDay;

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
