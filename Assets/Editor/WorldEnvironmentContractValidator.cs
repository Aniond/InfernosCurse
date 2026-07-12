using System.Collections.Generic;
using System.Linq;
using DistantLands.Cozy.Data;
using UnityEditor;
using UnityEngine;

public static class WorldEnvironmentContractValidator
{
    const string MenuPath = "InfernosCurse/Environment/World Environment/1. Validate Contract";

    [MenuItem(MenuPath)]
    public static void ValidateContract()
    {
        var errors = new List<string>();
        var profile = ScriptableObject.CreateInstance<WorldEnvironmentProfile>();
        try
        {
            if (!profile.TryValidate(out string profileError)) errors.Add(profileError);

            float simulatedSeconds = SimulateCycle(profile);
            float expectedSeconds = profile.fullCycleMinutes * 60f;
            if (Mathf.Abs(simulatedSeconds - expectedSeconds) > 0.1f)
                errors.Add($"Default cycle integrates to {simulatedSeconds:0.00}s, expected {expectedSeconds:0.00}s.");

            string[] legacy =
            {
                "clear", "cloudy", "wind", "fog", "mist", "drizzle", "rain",
                "heavy_rain", "storm", "hail", "sleet", "snow"
            };
            foreach (string alias in legacy)
                if (!WorldWeatherClassifier.TryClassify(alias, out _))
                    errors.Add($"Legacy weather alias '{alias}' is unmapped.");

            WeatherProfile[] installed = Resources.LoadAll<WeatherProfile>("Profiles/Weather Profiles");
            foreach (WeatherProfile weather in installed.OrderBy(item => item.name))
                if (!WorldWeatherClassifier.TryClassify(weather.name, out _))
                    errors.Add($"Installed COZY profile '{weather.name}' is unmapped.");

            if (installed.Length == 0)
                errors.Add("No COZY weather profiles were found under Resources/Profiles/Weather Profiles.");
        }
        finally
        {
            Object.DestroyImmediate(profile);
        }

        if (errors.Count == 0)
        {
            Debug.Log("[WorldEnvironmentContract] PASS: profile, cycle pacing, legacy aliases, and installed COZY profiles are valid.");
            return;
        }

        Debug.LogError($"[WorldEnvironmentContract] FAIL ({errors.Count}):\n- {string.Join("\n- ", errors)}");
    }

    static float SimulateCycle(WorldEnvironmentProfile profile)
    {
        const float StepHours = 0.01f;
        float seconds = 0f;
        for (float hour = 0f; hour < 24f; hour += StepHours)
            seconds += profile.SecondsPerGameHour(hour) * StepHours;
        return seconds;
    }
}
