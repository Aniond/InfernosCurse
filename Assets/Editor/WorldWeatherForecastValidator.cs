using System;
using UnityEditor;
using UnityEngine;

public static class WorldWeatherForecastValidator
{
    const string ClimatePath = "Assets/Data/Weather/FlorenceClimate.json";

    [Serializable]
    sealed class ClimateShape
    {
        public FlorenceWeather.MonthRow[] months;
        public FlorenceWeather.RainSpell rainSpell;
    }

    [MenuItem("InfernosCurse/Environment/World Environment/3. Validate Forecasts")]
    public static void Validate()
    {
        TextAsset json = AssetDatabase.LoadAssetAtPath<TextAsset>(ClimatePath);
        Require(json != null, $"Missing {ClimatePath}.");
        ClimateShape climate = JsonUtility.FromJson<ClimateShape>(json.text);
        Require(climate?.months?.Length == 12, "Climate must define all twelve months.");

        const int daysPerMonth = 4;
        int wetFronts = 0;
        int floodDays = 0;
        for (int day = 0; day < daysPerMonth * 12; day++)
        {
            WorldDailyForecast first = WorldForecastGenerator.Generate(
                1299, day, daysPerMonth, climate.months, climate.rainSpell, null);
            WorldDailyForecast second = WorldForecastGenerator.Generate(
                1299, day, daysPerMonth, climate.months, climate.rainSpell, null);

            Require(first.fronts.Length >= 2 && first.fronts.Length <= 4,
                $"Day {day} generated {first.fronts.Length} fronts.");
            Require(first.fronts.Length == second.fronts.Length,
                $"Day {day} front count is not deterministic.");
            float expectedStart = 0f;
            for (int i = 0; i < first.fronts.Length; i++)
            {
                WorldWeatherFront a = first.fronts[i];
                WorldWeatherFront b = second.fronts[i];
                Require(Mathf.Abs(a.startHour - expectedStart) < 0.001f,
                    $"Day {day} has a gap before front {i}.");
                Require(a.endHour > a.startHour, $"Day {day} front {i} has no duration.");
                Require(a.profileName == b.profileName && a.condition == b.condition,
                    $"Day {day} front {i} is not deterministic.");
                Require(WorldWeatherClassifier.TryClassify(a.profileName, out _),
                    $"Day {day} uses unknown COZY profile '{a.profileName}'.");
                if (a.weather.IsWet) wetFronts++;
                expectedStart = a.endHour;
            }
            Require(Mathf.Abs(expectedStart - 24f) < 0.001f,
                $"Day {day} does not cover the full day.");
            if (first.floodRisk) floodDays++;
        }

        Require(wetFronts > 0, "Annual forecast generated no wet fronts.");
        Debug.Log($"[WorldWeatherForecast] PASS: 48 deterministic days, 2-4 complete fronts/day, " +
                  $"{wetFronts} wet fronts, {floodDays} flood-risk days.");
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
