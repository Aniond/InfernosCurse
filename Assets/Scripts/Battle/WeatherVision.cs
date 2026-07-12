using UnityEngine;

// COZY weather -> vision: fog, storm and rain shrink sight range for
// EVERYONE symmetrically (player fog-of-war AND enemy perception read this),
// keyed off the active FlorenceWeather profile name. The Gemini weather
// director can therefore CREATE ambush conditions mid-battle — and a
// strategist-tier monster can exploit them, while a beast never notices.
public static class WeatherVision
{
    public static float SightMultiplier()
    {
        WorldWeatherState weather = BattleWeatherDirector.HasLocalWeather
            ? BattleWeatherDirector.LocalWeather
            : WorldEnvironmentState.CurrentWeather;
        switch (weather.kind)
        {
            case WorldWeatherKind.Fog: return 0.45f;
            case WorldWeatherKind.Storm: return 0.6f;
            case WorldWeatherKind.Drizzle:
            case WorldWeatherKind.Rain:
            case WorldWeatherKind.HeavyRain:
            case WorldWeatherKind.Snow:
            case WorldWeatherKind.Sleet:
            case WorldWeatherKind.Hail: return 0.75f;
            default: return 1f;
        }
    }
}
