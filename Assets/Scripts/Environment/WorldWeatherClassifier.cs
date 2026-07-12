using System;
using System.Collections.Generic;

public static class WorldWeatherClassifier
{
    static readonly Dictionary<string, WorldWeatherState> Profiles =
        new Dictionary<string, WorldWeatherState>(StringComparer.OrdinalIgnoreCase)
        {
            ["Clear"] = State(WorldWeatherKind.Clear, 0.05f, 0f, 0.05f, 0.08f, 1f, 0f),
            ["Mostly Clear"] = State(WorldWeatherKind.Clear, 0.18f, 0f, 0.08f, 0.12f, 0.98f, 0f),
            ["Partly Cloudy"] = State(WorldWeatherKind.PartlyCloudy, 0.42f, 0f, 0.16f, 0.18f, 0.95f, 0f),
            ["Mixed Clouds"] = State(WorldWeatherKind.PartlyCloudy, 0.55f, 0f, 0.2f, 0.22f, 0.92f, 0f),
            ["Mostly Cloudy"] = State(WorldWeatherKind.Cloudy, 0.72f, 0f, 0.28f, 0.30f, 0.88f, 0f),
            ["Dense Clouds"] = State(WorldWeatherKind.Cloudy, 0.90f, 0f, 0.32f, 0.38f, 0.82f, 0f),
            ["Overcast"] = State(WorldWeatherKind.Cloudy, 0.98f, 0f, 0.30f, 0.42f, 0.80f, 0f),
            ["Cirrus Clouds"] = State(WorldWeatherKind.PartlyCloudy, 0.28f, 0f, 0.14f, 0.12f, 0.98f, 0f),
            ["Cirrostratus Clouds"] = State(WorldWeatherKind.PartlyCloudy, 0.38f, 0f, 0.16f, 0.14f, 0.96f, 0f),
            ["High Altitude Clouds"] = State(WorldWeatherKind.PartlyCloudy, 0.32f, 0f, 0.20f, 0.12f, 0.97f, 0f),
            ["Particle Based Clouds"] = State(WorldWeatherKind.PartlyCloudy, 0.50f, 0f, 0.18f, 0.18f, 0.94f, 0f),
            ["Chemtrails"] = State(WorldWeatherKind.PartlyCloudy, 0.24f, 0f, 0.12f, 0.10f, 0.98f, 0f),
            ["Dense Fog"] = State(WorldWeatherKind.Fog, 0.92f, 0f, 0.08f, 0.20f, 0.30f, 0f),
            ["Filtered Fog"] = State(WorldWeatherKind.Fog, 0.62f, 0f, 0.10f, 0.12f, 0.52f, 0f),
            ["Electric Fog"] = State(WorldWeatherKind.Fog, 0.78f, 0f, 0.22f, 0.22f, 0.38f, 0.25f),
            ["Light Rain"] = State(WorldWeatherKind.Rain, 0.78f, 0.35f, 0.30f, 0.55f, 0.78f, 0f),
            ["Heavy Rain"] = State(WorldWeatherKind.HeavyRain, 0.96f, 0.82f, 0.58f, 0.92f, 0.62f, 0.05f),
            ["Light Precipitation"] = State(WorldWeatherKind.Sleet, 0.82f, 0.38f, 0.32f, 0.60f, 0.74f, 0f),
            ["Heavy Precipitation"] = State(WorldWeatherKind.HeavyRain, 0.98f, 0.88f, 0.62f, 0.96f, 0.56f, 0.10f),
            ["Hail Storm"] = State(WorldWeatherKind.Hail, 1f, 0.86f, 0.74f, 0.90f, 0.48f, 0.60f),
            ["Light Snow"] = State(WorldWeatherKind.Snow, 0.82f, 0.30f, 0.18f, 0.42f, 0.82f, 0f, -1f),
            ["Heavy Snow"] = State(WorldWeatherKind.Snow, 0.96f, 0.82f, 0.36f, 0.84f, 0.58f, 0.05f, -4f),
            ["Thunder Snow"] = State(WorldWeatherKind.Snow, 1f, 0.86f, 0.68f, 0.90f, 0.46f, 0.85f, -3f),
            ["Approaching Storm"] = State(WorldWeatherKind.Storm, 0.94f, 0.30f, 0.72f, 0.72f, 0.64f, 0.35f),
            ["Distant Storm"] = State(WorldWeatherKind.Storm, 0.88f, 0.22f, 0.58f, 0.65f, 0.70f, 0.28f),
            ["Imminent Storm"] = State(WorldWeatherKind.Storm, 1f, 0.62f, 0.88f, 0.88f, 0.50f, 0.68f),
            ["Receeding Storm"] = State(WorldWeatherKind.Storm, 0.88f, 0.34f, 0.62f, 0.68f, 0.68f, 0.28f),
            ["Storm Eye"] = State(WorldWeatherKind.Storm, 0.74f, 0.20f, 0.48f, 0.58f, 0.74f, 0.18f),
            ["Thunder Storm"] = State(WorldWeatherKind.Storm, 1f, 0.94f, 0.82f, 1f, 0.42f, 1f),
            ["Dust Storm"] = State(WorldWeatherKind.Storm, 0.90f, 0f, 0.92f, 0.10f, 0.30f, 0.12f)
        };

    static readonly Dictionary<string, string> LegacyAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["clear"] = "Clear",
            ["cloudy"] = "Mostly Cloudy",
            ["wind"] = "Mostly Cloudy",
            ["fog"] = "Dense Fog",
            ["mist"] = "Filtered Fog",
            ["drizzle"] = "Light Rain",
            ["rain"] = "Light Rain",
            ["heavy_rain"] = "Heavy Rain",
            ["storm"] = "Thunder Storm",
            ["hail"] = "Hail Storm",
            ["sleet"] = "Light Precipitation",
            ["snow"] = "Light Snow"
        };

    public static IEnumerable<string> KnownProfileNames => Profiles.Keys;

    public static bool TryClassify(string profileName, out WorldWeatherState state)
    {
        string key = (profileName ?? string.Empty).Trim();
        if (LegacyAliases.TryGetValue(key, out string canonical)) key = canonical;
        if (Profiles.TryGetValue(key, out state))
        {
            state.sourceProfileName = key;
            state.transitionProgress = 1f;
            state = state.Clamped();
            return true;
        }

        state = WorldWeatherState.Clear();
        state.sourceProfileName = profileName ?? string.Empty;
        return false;
    }

    public static WorldWeatherState ClassifyOrClear(string profileName)
    {
        TryClassify(profileName, out WorldWeatherState state);
        return state;
    }

    static WorldWeatherState State(
        WorldWeatherKind kind,
        float clouds,
        float precipitation,
        float wind,
        float wetness,
        float visibility,
        float lightning,
        float temperatureC = 18f)
    {
        return new WorldWeatherState
        {
            kind = kind,
            cloudCover = clouds,
            precipitation = precipitation,
            fogDensity = kind == WorldWeatherKind.Fog ? clouds : 0f,
            windStrength = wind,
            temperatureC = temperatureC,
            visibility = visibility,
            wetnessTarget = wetness,
            lightningIntensity = lightning,
            transitionProgress = 1f
        };
    }
}
