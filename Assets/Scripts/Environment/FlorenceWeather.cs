using System;
using System.Collections.Generic;
using DistantLands.Cozy;
using DistantLands.Cozy.Data;
using UnityEngine;

/// <summary>
/// Deterministic Florence forecast service. The calendar and district generate
/// two to four fronts per day; COZY only presents the currently active front.
/// </summary>
public sealed class FlorenceWeather : MonoBehaviour
{
    static FlorenceWeather _instance;
    public static FlorenceWeather Instance =>
        _instance != null ? _instance : (_instance = FindAnyObjectByType<FlorenceWeather>());

    public TextAsset climateJson;
    [Min(0f)] public float transitionSeconds = 20f;

    public static string CurrentProfileName { get; private set; } = string.Empty;
    public static bool FloodRiskToday { get; private set; }

    [Serializable]
    public sealed class MonthRow
    {
        public int month;
        public string name;
        public float[] tempC;
        public float fogProb, rainProb, stormProb, windProb, snowProb, sleetProb, hailProb, clearProb;
        public string note;
    }

    [Serializable]
    public sealed class RainSpell
    {
        public string[] months;
        public float chainProb = 0.45f;
        public int maxDays = 4;
        public int heavyFromDay = 2;
        public int floodRiskFromDay = 3;
    }

    [Serializable]
    sealed class ClimateFile
    {
        public MonthRow[] months;
        public RainSpell rainSpell;
    }

    MonthRow[] _months;
    RainSpell _spell;
    WorldDailyForecast _forecast;
    string _forecastKey = string.Empty;
    int _activeFrontIndex = -1;

    static Dictionary<string, WeatherProfile> _profileCache;

    public WorldDailyForecast CurrentForecast => _forecast;
    public WorldWeatherState CurrentWeather =>
        _forecast != null ? _forecast.FrontAt(GameClock.Hour).weather : WorldWeatherState.Clear();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        _instance = this;
        EnsureData();
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    void Update()
    {
        EnsureData();
        GameCalendar calendar = GameCalendar.Instance;
        if (calendar == null || _months == null) return;

        string key = $"{calendar.Year}:{calendar.DayOfYear}:{DistrictTracker.CurrentNodeId}";
        if (!string.Equals(key, _forecastKey, StringComparison.Ordinal))
        {
            _forecastKey = key;
            _forecast = Generate(calendar, ResolveNode(DistrictTracker.CurrentNodeId));
            _activeFrontIndex = -1;
            FloodRiskToday = _forecast.floodRisk;
        }

        int index = _forecast.FrontIndexAt(GameClock.Hour);
        if (index == _activeFrontIndex || index < 0) return;
        _activeFrontIndex = index;
        Present(_forecast.fronts[index], transitionSeconds);
    }

    public WorldDailyForecast ForecastForNode(HubNode node)
    {
        EnsureData();
        GameCalendar calendar = GameCalendar.Instance;
        return calendar != null && _months != null
            ? Generate(calendar, node)
            : new WorldDailyForecast();
    }

    public string ConditionForNode(HubNode node)
    {
        WorldDailyForecast forecast = ForecastForNode(node);
        WorldWeatherFront front = forecast.FrontAt(GameClock.HasClock ? GameClock.Hour : 12f);
        return string.IsNullOrEmpty(front.condition) ? "clear" : front.condition;
    }

    public WorldWeatherState WeatherForNode(HubNode node)
    {
        WorldDailyForecast forecast = ForecastForNode(node);
        WorldWeatherFront front = forecast.FrontAt(GameClock.HasClock ? GameClock.Hour : 12f);
        return string.IsNullOrEmpty(front.profileName) ? WorldWeatherState.Clear() : front.weather;
    }

    public bool ComputeFloodRisk(int year, int dayOfYear)
    {
        EnsureData();
        GameCalendar calendar = GameCalendar.Instance;
        return calendar != null && WorldForecastGenerator.ComputeFloodRisk(
            year, dayOfYear, calendar.daysPerMonth, _months, _spell);
    }

    /// <summary>
    /// Compatibility/manual presentation entry point. The deterministic world
    /// forecast resumes on the next update and remains the source of truth.
    /// </summary>
    public void Apply(string profileName)
    {
        WorldWeatherState weather = WorldWeatherClassifier.ClassifyOrClear(profileName);
        var front = new WorldWeatherFront
        {
            startHour = 0f,
            endHour = 24f,
            condition = ConditionName(weather.kind),
            profileName = profileName,
            weather = weather
        };
        Present(front, transitionSeconds);
        _activeFrontIndex = -1;
    }

    WorldDailyForecast Generate(GameCalendar calendar, HubNode node)
    {
        return WorldForecastGenerator.Generate(
            calendar.Year,
            calendar.DayOfYear,
            calendar.daysPerMonth,
            _months,
            _spell,
            node);
    }

    void EnsureData()
    {
        if (_months != null) return;
        if (climateJson == null)
        {
            Debug.LogWarning("[FlorenceWeather] No climate JSON assigned.");
            return;
        }
        ClimateFile file = JsonUtility.FromJson<ClimateFile>(climateJson.text);
        _months = file?.months;
        _spell = file?.rainSpell;
    }

    static HubNode ResolveNode(string nodeId)
    {
        return HubMap.Instance != null ? HubMap.Instance.GetNode(nodeId) : null;
    }

    void Present(WorldWeatherFront front, float transition)
    {
        CurrentProfileName = front.profileName ?? string.Empty;
        FloodRiskToday = front.weather.floodRisk || (_forecast != null && _forecast.floodRisk);

        WorldEnvironmentDirector director = WorldEnvironmentDirector.Instance;
        if (director != null) director.SetWeather(front.weather);

        CozyWeather cozy = CozyWeather.instance;
        if (cozy == null || cozy.weatherModule == null || !cozy.gameObject.activeInHierarchy) return;
        WeatherProfile profile = FindProfile(front.profileName);
        if (profile == null)
        {
            Debug.LogWarning($"[FlorenceWeather] COZY profile '{front.profileName}' not found.");
            return;
        }
        cozy.weatherModule.ecosystem.weatherSelectionMode = CozyEcosystem.EcosystemStyle.manual;
        cozy.weatherModule.ecosystem.SetWeather(profile, Mathf.Max(0f, transition));
        Debug.Log($"[FlorenceWeather] Front {_activeFrontIndex + 1}/{_forecast?.fronts?.Length ?? 0}: " +
                  $"{front.profileName} ({front.startHour:00.0}-{front.endHour:00.0}).");
    }

    static WeatherProfile FindProfile(string profileName)
    {
        if (_profileCache == null)
        {
            _profileCache = new Dictionary<string, WeatherProfile>(StringComparer.OrdinalIgnoreCase);
            foreach (WeatherProfile profile in Resources.LoadAll<WeatherProfile>("Profiles/Weather Profiles"))
                if (!_profileCache.ContainsKey(profile.name)) _profileCache.Add(profile.name, profile);
        }
        _profileCache.TryGetValue(profileName ?? string.Empty, out WeatherProfile found);
        return found;
    }

    static string ConditionName(WorldWeatherKind kind)
    {
        switch (kind)
        {
            case WorldWeatherKind.Fog: return "fog";
            case WorldWeatherKind.Drizzle:
            case WorldWeatherKind.Rain:
            case WorldWeatherKind.HeavyRain: return "rain";
            case WorldWeatherKind.Storm: return "storm";
            case WorldWeatherKind.Wind: return "wind";
            case WorldWeatherKind.Snow: return "snow";
            case WorldWeatherKind.Sleet: return "sleet";
            case WorldWeatherKind.Hail: return "hail";
            default: return "clear";
        }
    }
}
