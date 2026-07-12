using System;
using UnityEngine;

public enum WorldTimePhase
{
    Dawn,
    Day,
    Dusk,
    Night
}

public enum WorldWeatherKind
{
    Clear,
    PartlyCloudy,
    Cloudy,
    Wind,
    Fog,
    Drizzle,
    Rain,
    HeavyRain,
    Storm,
    Hail,
    Sleet,
    Snow
}

[Serializable]
public struct WorldWeatherState
{
    public WorldWeatherKind kind;
    [Range(0f, 1f)] public float precipitation;
    [Range(0f, 1f)] public float cloudCover;
    [Range(0f, 1f)] public float fogDensity;
    [Range(0f, 1f)] public float windStrength;
    public float temperatureC;
    [Range(0.05f, 1f)] public float visibility;
    [Range(0f, 1f)] public float wetnessTarget;
    [Range(0f, 1f)] public float lightningIntensity;
    public bool floodRisk;
    [Range(0f, 1f)] public float transitionProgress;
    public string sourceProfileName;

    public bool IsWet => precipitation > 0.01f ||
                         kind == WorldWeatherKind.Rain ||
                         kind == WorldWeatherKind.HeavyRain ||
                         kind == WorldWeatherKind.Storm ||
                         kind == WorldWeatherKind.Hail ||
                         kind == WorldWeatherKind.Sleet ||
                         kind == WorldWeatherKind.Snow;

    public WorldWeatherState Clamped()
    {
        WorldWeatherState value = this;
        value.precipitation = Mathf.Clamp01(value.precipitation);
        value.cloudCover = Mathf.Clamp01(value.cloudCover);
        value.fogDensity = Mathf.Clamp01(value.fogDensity);
        value.windStrength = Mathf.Clamp01(value.windStrength);
        value.visibility = Mathf.Clamp(value.visibility, 0.05f, 1f);
        value.wetnessTarget = Mathf.Clamp01(value.wetnessTarget);
        value.lightningIntensity = Mathf.Clamp01(value.lightningIntensity);
        value.transitionProgress = Mathf.Clamp01(value.transitionProgress);
        value.sourceProfileName ??= string.Empty;
        return value;
    }

    public static WorldWeatherState Clear(float temperatureC = 18f)
    {
        return new WorldWeatherState
        {
            kind = WorldWeatherKind.Clear,
            cloudCover = 0.08f,
            temperatureC = temperatureC,
            visibility = 1f,
            transitionProgress = 1f,
            sourceProfileName = "Clear"
        };
    }
}

[Serializable]
public struct WorldEnvironmentSnapshot
{
    public string dateKey;
    public int year;
    public int dayOfYear;
    [Range(0f, 24f)] public float hour;
    public WorldTimePhase phase;
    public string districtId;
    public WorldWeatherState weather;
    [Range(0f, 1f)] public float accumulatedWetness;

    public WorldEnvironmentSnapshot Clamped()
    {
        WorldEnvironmentSnapshot value = this;
        value.dateKey ??= string.Empty;
        value.hour = Mathf.Repeat(value.hour, 24f);
        value.districtId ??= string.Empty;
        value.weather = value.weather.Clamped();
        value.accumulatedWetness = Mathf.Clamp01(value.accumulatedWetness);
        return value;
    }
}

public static class WorldEnvironmentState
{
    public static WorldWeatherState CurrentWeather
    {
        get
        {
            WorldEnvironmentDirector director = WorldEnvironmentDirector.Instance;
            if (director != null) return director.Weather;
            return WorldWeatherClassifier.ClassifyOrClear(FlorenceWeather.CurrentProfileName);
        }
    }

    public static WorldTimePhase CurrentPhase
    {
        get
        {
            WorldEnvironmentDirector director = WorldEnvironmentDirector.Instance;
            if (director != null) return director.Snapshot.phase;
            float hour = GameClock.HasClock ? GameClock.Hour : 12f;
            if (hour >= 20f || hour < 5f) return WorldTimePhase.Night;
            if (hour < 7f) return WorldTimePhase.Dawn;
            if (hour < 18f) return WorldTimePhase.Day;
            return WorldTimePhase.Dusk;
        }
    }
}
