using UnityEngine;

[CreateAssetMenu(
    fileName = "WorldEnvironmentProfile",
    menuName = "InfernosCurse/Environment/World Environment Profile")]
public sealed class WorldEnvironmentProfile : ScriptableObject
{
    [Header("Cycle pacing")]
    [Min(1f)] public float fullCycleMinutes = 40f;
    [Min(1f)] public float nightSpeedMultiplier = 1.75f;

    [Header("Time phases")]
    [Range(0f, 24f)] public float dawnStartHour = 5f;
    [Range(0f, 24f)] public float dayStartHour = 7f;
    [Range(0f, 24f)] public float duskStartHour = 18f;
    [Range(0f, 24f)] public float nightStartHour = 20f;

    [Header("Weather transitions")]
    [Min(0.1f)] public float minimumTransitionGameMinutes = 10f;
    [Min(0.1f)] public float maximumTransitionGameMinutes = 30f;

    [Header("Fallback")]
    public float fallbackTemperatureC = 18f;

    public WorldTimePhase PhaseAt(float hour)
    {
        hour = Mathf.Repeat(hour, 24f);
        if (hour >= nightStartHour || hour < dawnStartHour) return WorldTimePhase.Night;
        if (hour < dayStartHour) return WorldTimePhase.Dawn;
        if (hour < duskStartHour) return WorldTimePhase.Day;
        return WorldTimePhase.Dusk;
    }

    public bool IsNight(float hour) => PhaseAt(hour) == WorldTimePhase.Night;

    public float BaseSecondsPerGameHour
    {
        get
        {
            float nightHours = 24f - nightStartHour + dawnStartHour;
            float nonNightHours = 24f - nightHours;
            float weightedHours = nonNightHours + nightHours / Mathf.Max(1f, nightSpeedMultiplier);
            return fullCycleMinutes * 60f / Mathf.Max(0.01f, weightedHours);
        }
    }

    public float SecondsPerGameHour(float hour)
    {
        return BaseSecondsPerGameHour /
               (IsNight(hour) ? Mathf.Max(1f, nightSpeedMultiplier) : 1f);
    }

    public float GameHoursPerRealSecond(float hour)
    {
        return 1f / Mathf.Max(0.001f, SecondsPerGameHour(hour));
    }

    public bool TryValidate(out string error)
    {
        if (fullCycleMinutes < 1f)
        {
            error = "Full cycle must last at least one real minute.";
            return false;
        }
        if (nightSpeedMultiplier < 1f)
        {
            error = "Night speed multiplier cannot be slower than daytime.";
            return false;
        }
        if (!(0f <= dawnStartHour && dawnStartHour < dayStartHour &&
              dayStartHour < duskStartHour && duskStartHour < nightStartHour &&
              nightStartHour < 24f))
        {
            error = "Time phases must be ordered Dawn < Day < Dusk < Night within 0-24.";
            return false;
        }
        if (minimumTransitionGameMinutes <= 0f ||
            maximumTransitionGameMinutes < minimumTransitionGameMinutes)
        {
            error = "Weather transition range is invalid.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public WorldWeatherState ClearFallback => WorldWeatherState.Clear(fallbackTemperatureC);
}
