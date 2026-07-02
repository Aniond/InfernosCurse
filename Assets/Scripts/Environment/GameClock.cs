using UnityEngine;

// The game's single source of truth for "what hour is it?" (0-24 float).
//
// Every system that cares about time of day (GameCalendar, TorchFlicker,
// LightShaft, SaveSystem) reads THIS, never a clock implementation directly.
// COZY migration day: reimplement Hour / SetHour / HasClock against COZY's
// time API and delete DayNightCycle — no consumer changes needed.
public static class GameClock
{
    private static DayNightCycle _cycle;

    private static DayNightCycle Cycle =>
        _cycle != null ? _cycle : (_cycle = Object.FindAnyObjectByType<DayNightCycle>());

    /// <summary>True when a clock backend exists (some scenes may run without one).</summary>
    public static bool HasClock => Cycle != null;

    /// <summary>Current in-game hour, 0-24. Falls back to noon with no backend.</summary>
    public static float Hour => Cycle != null ? Cycle.timeOfDay : 12f;

    /// <summary>Set the in-game hour (save-game restore).</summary>
    public static void SetHour(float hour)
    {
        if (Cycle != null) Cycle.timeOfDay = hour;
    }
}
