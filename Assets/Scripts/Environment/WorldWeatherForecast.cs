using System;
using UnityEngine;

[Serializable]
public struct WorldWeatherFront
{
    [Range(0f, 24f)] public float startHour;
    [Range(0f, 24f)] public float endHour;
    public string condition;
    public string profileName;
    public WorldWeatherState weather;

    public bool Contains(float hour)
    {
        hour = Mathf.Repeat(hour, 24f);
        return hour >= startHour && (endHour >= 24f || hour < endHour);
    }
}

[Serializable]
public sealed class WorldDailyForecast
{
    public int year;
    public int dayOfYear;
    public string districtId;
    public int rainSpellDay;
    public bool floodRisk;
    public WorldWeatherFront[] fronts = Array.Empty<WorldWeatherFront>();

    public int FrontIndexAt(float hour)
    {
        for (int i = 0; i < fronts.Length; i++)
            if (fronts[i].Contains(hour)) return i;
        return fronts.Length > 0 ? fronts.Length - 1 : -1;
    }

    public WorldWeatherFront FrontAt(float hour)
    {
        int index = FrontIndexAt(hour);
        return index >= 0 ? fronts[index] : default;
    }
}

public static class WorldForecastGenerator
{
    public static WorldDailyForecast Generate(
        int year,
        int dayOfYear,
        int daysPerMonth,
        FlorenceWeather.MonthRow[] months,
        FlorenceWeather.RainSpell spell,
        HubNode district)
    {
        var result = new WorldDailyForecast
        {
            year = year,
            dayOfYear = dayOfYear,
            districtId = district != null ? district.id : string.Empty
        };
        FlorenceWeather.MonthRow row = RowForDay(months, daysPerMonth, dayOfYear);
        if (row == null)
        {
            result.fronts = new[] { BuildFront(0f, 24f, "clear", "Clear", 18f, false) };
            return result;
        }

        int seed = Seed(year, dayOfYear);
        int spellDay = SpellDay(year, dayOfYear, daysPerMonth, months, spell);
        result.rainSpellDay = spellDay;
        result.floodRisk = spell != null && spellDay >= spell.floodRiskFromDay;

        var countRng = new System.Random(seed ^ 0x1F3D5B79);
        int frontCount = 2 + countRng.Next(3);
        result.fronts = new WorldWeatherFront[frontCount];
        float span = 24f / frontCount;

        for (int i = 0; i < frontCount; i++)
        {
            float start = i * span;
            float end = i == frontCount - 1 ? 24f : (i + 1) * span;
            string condition = BaseCondition(row, seed ^ (i * 0x45D9F3B));
            if (spellDay > 0 && i < Mathf.Max(1, frontCount - 1)) condition = "rain";
            if (condition == "fog" && start >= 10f) condition = "clear";
            condition = DeriveDistrictCondition(condition, district, seed ^ i);
            string profile = ProfileFor(condition, seed ^ (i * 0x9E37), spellDay, district);
            float temperature = TemperatureAt(row, (start + end) * 0.5f);
            result.fronts[i] = BuildFront(start, end, condition, profile, temperature, result.floodRisk);
        }
        return result;
    }

    public static bool ComputeFloodRisk(
        int year,
        int dayOfYear,
        int daysPerMonth,
        FlorenceWeather.MonthRow[] months,
        FlorenceWeather.RainSpell spell)
    {
        if (spell == null) return false;
        return SpellDay(year, dayOfYear, daysPerMonth, months, spell) >= spell.floodRiskFromDay;
    }

    static WorldWeatherFront BuildFront(
        float start,
        float end,
        string condition,
        string profile,
        float temperature,
        bool floodRisk)
    {
        WorldWeatherState state = WorldWeatherClassifier.ClassifyOrClear(profile);
        state.temperatureC = temperature;
        state.floodRisk = floodRisk && state.IsWet;
        return new WorldWeatherFront
        {
            startHour = start,
            endHour = end,
            condition = condition,
            profileName = profile,
            weather = state.Clamped()
        };
    }

    static int Seed(int year, int dayOfYear) => year * 1000 + dayOfYear;

    static FlorenceWeather.MonthRow RowForDay(
        FlorenceWeather.MonthRow[] months,
        int daysPerMonth,
        int dayOfYear)
    {
        if (months == null || months.Length == 0) return null;
        int monthIndex = Mathf.Clamp(dayOfYear / Mathf.Max(1, daysPerMonth), 0, 11);
        string english = GameCalendar.EnglishName((GameCalendar.Month)monthIndex);
        foreach (FlorenceWeather.MonthRow row in months)
            if (string.Equals(row.name, english, StringComparison.OrdinalIgnoreCase)) return row;
        return null;
    }

    static string BaseCondition(FlorenceWeather.MonthRow row, int seed)
    {
        var rng = new System.Random(seed);
        double roll = rng.NextDouble();
        if ((roll -= row.fogProb) < 0d) return "fog";
        if ((roll -= row.rainProb) < 0d) return "rain";
        if ((roll -= row.stormProb) < 0d) return "storm";
        if ((roll -= row.windProb) < 0d) return "wind";
        if ((roll -= row.snowProb) < 0d) return "snow";
        if ((roll -= row.sleetProb) < 0d) return "sleet";
        if ((roll -= row.hailProb) < 0d) return "hail";
        return "clear";
    }

    static bool IsWet(string condition) => condition == "rain" || condition == "storm";

    static bool InSpellSeason(FlorenceWeather.MonthRow row, FlorenceWeather.RainSpell spell)
    {
        if (row == null || spell?.months == null) return false;
        foreach (string month in spell.months)
            if (string.Equals(month, row.name, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    static int SpellDay(
        int year,
        int dayOfYear,
        int daysPerMonth,
        FlorenceWeather.MonthRow[] months,
        FlorenceWeather.RainSpell spell)
    {
        if (spell == null) return 0;
        int daysPerYear = Mathf.Max(1, daysPerMonth) * 12;
        int start = 1;
        for (; start <= 12; start++)
        {
            NormalizePrevious(year, dayOfYear - start, daysPerYear, out int y, out int d);
            FlorenceWeather.MonthRow row = RowForDay(months, daysPerMonth, d);
            if (row == null) break;
            bool possiblyWet = IsWet(BaseCondition(row, Seed(y, d))) ||
                               (InSpellSeason(row, spell) && ChainRoll(y, d, spell));
            if (!possiblyWet) break;
        }

        int spellDay = 0;
        for (int back = start - 1; back >= 0; back--)
        {
            NormalizePrevious(year, dayOfYear - back, daysPerYear, out int y, out int d);
            FlorenceWeather.MonthRow row = RowForDay(months, daysPerMonth, d);
            if (row == null) { spellDay = 0; continue; }
            bool wet = IsWet(BaseCondition(row, Seed(y, d)));
            if (!wet && spellDay >= 1 && spellDay < spell.maxDays && InSpellSeason(row, spell))
                wet = ChainRoll(y, d, spell);
            spellDay = wet ? Mathf.Min(spellDay + 1, spell.maxDays) : 0;
        }
        return spellDay;
    }

    static void NormalizePrevious(int year, int day, int daysPerYear, out int resultYear, out int resultDay)
    {
        resultYear = year;
        resultDay = day;
        while (resultDay < 0) { resultDay += daysPerYear; resultYear--; }
    }

    static bool ChainRoll(int year, int dayOfYear, FlorenceWeather.RainSpell spell)
    {
        var rng = new System.Random(Seed(year, dayOfYear) ^ 0x05F00D);
        return rng.NextDouble() < spell.chainProb;
    }

    static int StableSalt(string value)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (char c in value ?? string.Empty) { hash ^= c; hash *= 16777619; }
            return (int)hash;
        }
    }

    static string DeriveDistrictCondition(string city, HubNode node, int seed)
    {
        if (node == null || node.microClimate == MicroClimate.Default) return city;
        var rng = new System.Random(seed ^ StableSalt(node.id));
        switch (node.microClimate)
        {
            case MicroClimate.Riverside:
                if (city == "clear" && rng.NextDouble() < 0.35d) return "fog";
                return city;
            case MicroClimate.OpenPiazza:
                if (city == "clear" && rng.NextDouble() < 0.25d) return "wind";
                return city;
            case MicroClimate.Sheltered:
                if (city == "storm" || city == "hail") return "rain";
                if (city == "wind") return "clear";
                return city;
            case MicroClimate.Hilltop:
                if (city == "fog" && rng.NextDouble() < 0.65d) return "clear";
                if (city == "clear" && rng.NextDouble() < 0.30d) return "wind";
                return city;
            default:
                return city;
        }
    }

    static string ProfileFor(string condition, int seed, int spellDay, HubNode node)
    {
        var rng = new System.Random(seed);
        string profile;
        switch (condition)
        {
            case "fog": profile = "Dense Fog"; break;
            case "rain": profile = spellDay >= 2 || rng.NextDouble() < 0.3d ? "Heavy Rain" : "Light Rain"; break;
            case "storm": profile = "Thunder Storm"; break;
            case "wind": profile = "Mostly Cloudy"; break;
            case "snow": profile = "Light Snow"; break;
            case "sleet": profile = "Light Precipitation"; break;
            case "hail": profile = "Hail Storm"; break;
            default:
                double clearRoll = rng.NextDouble();
                profile = clearRoll < 0.5d ? "Clear" : clearRoll < 0.8d ? "Mostly Clear" : "Partly Cloudy";
                break;
        }
        if (node != null && node.microClimate == MicroClimate.Riverside && condition == "rain" && spellDay > 0)
            profile = "Heavy Rain";
        return profile;
    }

    static float TemperatureAt(FlorenceWeather.MonthRow row, float hour)
    {
        if (row.tempC == null || row.tempC.Length < 2) return 18f;
        float high = row.tempC[0];
        float low = row.tempC[1];
        float warmth = Mathf.InverseLerp(-1f, 1f, Mathf.Sin((hour - 8f) / 24f * Mathf.PI * 2f));
        return Mathf.Lerp(low, high, warmth);
    }
}
