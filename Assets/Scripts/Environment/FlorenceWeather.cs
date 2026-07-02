using System;
using UnityEngine;
using DistantLands.Cozy;
using DistantLands.Cozy.Data;

// Rolls one weather condition per in-game day from real Arno-valley climate
// data (Assets/Data/Weather/FlorenceClimate.json) and applies it through COZY.
// The roll is SEEDED off the calendar date, so a given playthrough day always
// gets the same sky — revisits are reproducible, not re-randomized.
//
// Selection is deterministic/data-driven by design (see the climate JSON's
// header note); narrative flavor on top of it can come later.
public class FlorenceWeather : MonoBehaviour
{
    // Lazy-resolving so a mid-play domain reload (statics wiped, Awake not
    // re-run) can't leave the singleton permanently null.
    static FlorenceWeather _instance;
    public static FlorenceWeather Instance
    {
        get => _instance != null ? _instance : (_instance = FindAnyObjectByType<FlorenceWeather>());
        private set => _instance = value;
    }

    [Tooltip("FlorenceClimate.json — per-day condition probabilities keyed by real month name.")]
    public TextAsset climateJson;

    [Tooltip("Seconds COZY takes to blend into the day's weather.")]
    public float transitionSeconds = 20f;

    /// <summary>COZY profile name currently applied (for save games).</summary>
    public static string CurrentProfileName { get; private set; } = "";

    [Serializable]
    public class MonthRow
    {
        public int month;
        public string name;
        public float[] tempC;
        public float fogProb, rainProb, stormProb, windProb, snowProb, sleetProb, hailProb, clearProb;
        public string note;
    }

    [Serializable]
    class ClimateFile { public MonthRow[] months; }

    MonthRow[] _months;
    string _appliedDayKey = "";

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        EnsureData();
    }

    // Re-parse after mid-play domain reloads too (non-serialized cache dies).
    void EnsureData()
    {
        if (_months != null) return;
        if (climateJson != null)
            _months = JsonUtility.FromJson<ClimateFile>(climateJson.text).months;
        else
            Debug.LogWarning("[FlorenceWeather] No climate JSON assigned.");
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        // Poll the calendar rather than subscribing — both live on GameSystems
        // and this sidesteps init-order between them. One string compare per frame.
        EnsureData();
        var cal = GameCalendar.Instance;
        if (cal == null || _months == null) return;

        string key = cal.Year + ":" + cal.DayOfYear;
        if (key == _appliedDayKey) return;

        var row = RowFor(cal.CurrentMonth);
        if (row == null) return;

        _appliedDayKey = key;
        RollAndApply(row, cal.Year * 1000 + cal.DayOfYear);
    }

    MonthRow RowFor(GameCalendar.Month month)
    {
        // GameCalendar is stile fiorentino — match by English month NAME, never index.
        string english = GameCalendar.EnglishName(month);
        foreach (var m in _months)
            if (string.Equals(m.name, english, StringComparison.OrdinalIgnoreCase))
                return m;
        return null;
    }

    void RollAndApply(MonthRow m, int seed)
    {
        var rng = new System.Random(seed);
        double r = rng.NextDouble();
        string prof;
        if ((r -= m.fogProb) < 0)        prof = "Dense Fog";
        else if ((r -= m.rainProb) < 0)  prof = rng.NextDouble() < 0.3 ? "Heavy Rain" : "Light Rain";
        else if ((r -= m.stormProb) < 0) prof = "Thunder Storm";
        else if ((r -= m.windProb) < 0)  prof = "Mostly Cloudy";     // gusty grey day
        else if ((r -= m.snowProb) < 0)  prof = "Light Snow";
        else if ((r -= m.sleetProb) < 0) prof = "Light Precipitation"; // cold mix
        else if ((r -= m.hailProb) < 0)  prof = "Hail Storm";
        else
        {
            double c = rng.NextDouble();
            prof = c < 0.5 ? "Clear" : (c < 0.8 ? "Mostly Clear" : "Partly Cloudy");
        }
        Apply(prof);
    }

    /// <summary>Apply a COZY weather profile by name (also used by save-game restore).</summary>
    public void Apply(string profileName)
    {
        var cozy = CozyWeather.instance;
        if (cozy == null || cozy.weatherModule == null)
        {
            Debug.LogWarning("[FlorenceWeather] COZY not present — cannot set weather.");
            return;
        }

        var profile = Resources.Load<WeatherProfile>("Profiles/Weather Profiles/" + profileName);
        if (profile == null)
        {
            Debug.LogWarning($"[FlorenceWeather] COZY profile '{profileName}' not found.");
            return;
        }

        cozy.weatherModule.ecosystem.weatherSelectionMode = CozyEcosystem.EcosystemStyle.manual;
        cozy.weatherModule.ecosystem.SetWeather(profile, transitionSeconds);
        CurrentProfileName = profileName;

        // A save-restore counts as today's weather — don't re-roll over it.
        var cal = GameCalendar.Instance;
        if (cal != null) _appliedDayKey = cal.Year + ":" + cal.DayOfYear;

        Debug.Log($"[FlorenceWeather] Weather set: {profileName}");
    }
}
