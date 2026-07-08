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
        string p = FlorenceWeather.CurrentProfileName;
        if (string.IsNullOrEmpty(p)) return 1f;
        p = p.ToLowerInvariant();
        if (p.Contains("fog"))                          return 0.45f;   // world closes in
        if (p.Contains("storm") || p.Contains("thunder")) return 0.6f;
        if (p.Contains("rain") || p.Contains("snow") ||
            p.Contains("sleet") || p.Contains("hail"))  return 0.75f;
        return 1f;
    }
}
