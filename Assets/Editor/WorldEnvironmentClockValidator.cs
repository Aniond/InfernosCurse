using System;
using UnityEditor;
using UnityEngine;

public static class WorldEnvironmentClockValidator
{
    [MenuItem("InfernosCurse/Environment/World Environment/2. Validate Clock Pacing")]
    public static void Validate()
    {
        var profile = ScriptableObject.CreateInstance<WorldEnvironmentProfile>();
        try
        {
            double seconds = SimulateCycle(profile, 1d / 60d);
            double expected = profile.fullCycleMinutes * 60d;
            if (Math.Abs(seconds - expected) > 0.2d)
                throw new InvalidOperationException(
                    $"Cycle took {seconds:F3}s; expected {expected:F3}s.");

            Require(profile.PhaseAt(5f) == WorldTimePhase.Dawn, "05:00 must be dawn.");
            Require(profile.PhaseAt(7f) == WorldTimePhase.Day, "07:00 must be day.");
            Require(profile.PhaseAt(18f) == WorldTimePhase.Dusk, "18:00 must be dusk.");
            Require(profile.PhaseAt(20f) == WorldTimePhase.Night, "20:00 must be night.");
            Require(profile.SecondsPerGameHour(22f) < profile.SecondsPerGameHour(12f),
                "Night must advance faster than daytime.");

            Debug.Log($"[WorldEnvironmentClock] PASS: cycle={seconds:F2}s " +
                      $"dayHour={profile.SecondsPerGameHour(12f):F2}s " +
                      $"nightHour={profile.SecondsPerGameHour(22f):F2}s.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(profile);
        }
    }

    static double SimulateCycle(WorldEnvironmentProfile profile, double stepSeconds)
    {
        double hour = 0d;
        double elapsed = 0d;
        while (hour < 24d && elapsed < 10000d)
        {
            hour += profile.GameHoursPerRealSecond((float)hour) * stepSeconds;
            elapsed += stepSeconds;
        }
        return elapsed;
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
