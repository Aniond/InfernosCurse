using UnityEngine;
using DistantLands.Cozy;

// The game's single source of truth for "what hour is it?" (0-24 float).
//
// Every system that cares about time of day (GameCalendar, TorchFlicker,
// LightShaft, SaveSystem) reads THIS, never a clock implementation directly.
// Backed by COZY's time module since the 2026-07-03 weather migration.
public static class GameClock
{
    static CozyWeather Cozy => CozyWeather.instance;

    /// <summary>True when a clock backend exists (some scenes may run without one).</summary>
    public static bool HasClock => Cozy != null && Cozy.timeModule != null;

    /// <summary>Current in-game hour, 0-24. Falls back to noon with no backend.</summary>
    public static float Hour => HasClock ? (float)Cozy.timeModule.currentTime * 24f : 12f;

    /// <summary>Set the in-game hour (save-game restore).</summary>
    public static void SetHour(float hour)
    {
        if (HasClock)
            Cozy.timeModule.currentTime = Mathf.Repeat(hour, 24f) / 24f;
    }

    /// <summary>Backend state dump for debugging clock issues.</summary>
    public static string Describe()
    {
        if (!HasClock) return "no clock backend";
        var t = Cozy.timeModule;
        var p = t.perennialProfile;
        if (p == null) return $"time={(float)t.currentTime:F4} (no perennial profile!)";
        return $"time={(float)t.currentTime:F4} pause={p.pauseTime} speed={p.timeMovementSpeed} " +
               $"modulate={p.modulateTimeSpeed} progressDay={p.progressDay} reset={p.resetTimeOnStart} " +
               $"day={t.currentDay} year={t.currentYear} profile='{p.name}'";
    }
}
