using System;
using UnityEngine;
using DistantLands.Cozy;
using DistantLands.Cozy.Data;

// Rolls one weather condition per in-game day from research-backed Arno-valley
// climate data (Assets/Data/Weather/FlorenceClimate.json) and applies it through
// COZY. Fully deterministic: the base roll AND the autumn rain-spell chain are
// pure functions of the calendar date, so a given playthrough day always gets
// the same sky — across revisits and across save/load.
//
// Era layer (chronicle-verified — see the JSON header and WEATHER_SYSTEM.md):
// the circa-1300 Arno was flood-prone through multi-day autumn rain (Villani:
// 1269, 1280s, 1302-03, 1333). In autumn a wet day can chain into the next;
// late spell days render as Heavy Rain and raise FloodRiskToday for future
// gameplay/story hooks.
//
// Florence fog is a radiation/morning phenomenon: fog days start in Dense Fog
// and burn off to haze mid-morning instead of lasting all day.
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

    [Tooltip("Hour (0-24) at which morning fog burns off into haze.")]
    public float fogBurnOffHour = 10.5f;

    /// <summary>COZY profile name currently applied (for save games).</summary>
    public static string CurrentProfileName { get; private set; } = "";

    /// <summary>True on a late day of an autumn rain spell — the 1269/1333-style Arno flood setup.</summary>
    public static bool FloodRiskToday { get; private set; }

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
    public class RainSpell
    {
        public string[] months;
        public float chainProb = 0.45f;
        public int maxDays = 4;
        public int heavyFromDay = 2;
        public int floodRiskFromDay = 3;
    }

    [Serializable]
    class ClimateFile { public MonthRow[] months; public RainSpell rainSpell; }

    MonthRow[] _months;
    RainSpell _spell;
    string _appliedDayKey = "";
    string _appliedDistrictId = "";
    string _todayCondition = "";
    bool _fogBurnedOff;

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
        {
            var file = JsonUtility.FromJson<ClimateFile>(climateJson.text);
            _months = file.months;
            _spell = file.rainSpell;
        }
        else Debug.LogWarning("[FlorenceWeather] No climate JSON assigned.");
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
        if (key != _appliedDayKey)
        {
            _appliedDayKey = key;
            ApplyToday(cal);
        }
        // Same poll-don't-subscribe pattern for district changes: arriving in a
        // new district re-derives its local variant of today's weather.
        else if (DistrictTracker.CurrentNodeId != _appliedDistrictId)
        {
            ApplyToday(cal);
        }

        // Radiation fog burns off mid-morning; the slow transition reads as the
        // valley haze lifting rather than a weather change.
        if (_todayCondition == "fog" && !_fogBurnedOff && GameClock.Hour >= fogBurnOffHour && GameClock.Hour < 20f)
        {
            _fogBurnedOff = true;
            ApplyProfile("Mostly Clear", 60f);
        }
    }

    // ── deterministic condition engine ──────────────────────────────────────

    static int Seed(int year, int dayOfYear) => year * 1000 + dayOfYear;

    MonthRow RowForDay(GameCalendar cal, int dayOfYear)
    {
        int mi = Mathf.Clamp(dayOfYear / Mathf.Max(1, cal.daysPerMonth), 0, 11);
        return RowFor((GameCalendar.Month)mi);
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

    // The raw dice roll for a date, no chaining.
    static string BaseCondition(MonthRow m, int seed)
    {
        var rng = new System.Random(seed);
        double r = rng.NextDouble();
        if ((r -= m.fogProb) < 0) return "fog";
        if ((r -= m.rainProb) < 0) return "rain";
        if ((r -= m.stormProb) < 0) return "storm";
        if ((r -= m.windProb) < 0) return "wind";
        if ((r -= m.snowProb) < 0) return "snow";
        if ((r -= m.sleetProb) < 0) return "sleet";
        if ((r -= m.hailProb) < 0) return "hail";
        return "clear";
    }

    static bool IsWet(string c) => c == "rain" || c == "storm";

    bool InSpellSeason(MonthRow m)
    {
        if (_spell?.months == null || m == null) return false;
        foreach (var n in _spell.months)
            if (string.Equals(n, m.name, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    // 1 = first wet day of a spell, 2 = second consecutive, … 0 = dry today.
    // Pure function of the date, so spells reproduce identically after
    // save/load or a revisit. Two passes: walk back to the nearest day that is
    // CERTAINLY dry (no base wet, no possible chain roll — decidable without
    // history), then replay the spell recurrence forward from that zero.
    int SpellDay(GameCalendar cal, int year, int dayOfYear)
    {
        int maxDays = _spell != null ? _spell.maxDays : 4;
        int daysPerYear = Mathf.Max(1, cal.daysPerMonth) * 12;
        const int LookbackCap = 12; // spells cap at maxDays; this bound is generous

        int start = 1;
        for (; start <= LookbackCap; start++)
        {
            int y = year, d = dayOfYear - start;
            while (d < 0) { d += daysPerYear; y--; }
            var row = RowForDay(cal, d);
            if (row == null) break;
            bool possiblyWet = IsWet(BaseCondition(row, Seed(y, d))) ||
                               (InSpellSeason(row) && ChainRoll(y, d));
            if (!possiblyWet) break; // spell(d) == 0 for certain
        }

        int spell = 0;
        for (int back = start - 1; back >= 0; back--)
        {
            int y = year, d = dayOfYear - back;
            while (d < 0) { d += daysPerYear; y--; }
            var row = RowForDay(cal, d);
            if (row == null) { spell = 0; continue; }

            bool wet = IsWet(BaseCondition(row, Seed(y, d)));
            if (!wet && spell >= 1 && spell < maxDays && InSpellSeason(row))
                wet = ChainRoll(y, d);
            spell = wet ? Mathf.Min(spell + 1, maxDays) : 0;
        }
        return spell;
    }

    bool ChainRoll(int year, int dayOfYear)
    {
        // Separate seed stream so chaining never disturbs the base roll.
        var rng = new System.Random(Seed(year, dayOfYear) ^ 0x05F00D);
        return rng.NextDouble() < (_spell != null ? _spell.chainProb : 0f);
    }

    // ── daily application ────────────────────────────────────────────────────

    void ApplyToday(GameCalendar cal)
    {
        var row = RowForDay(cal, cal.DayOfYear);
        if (row == null) return;

        int seed = Seed(cal.Year, cal.DayOfYear);
        string cond = BaseCondition(row, seed);

        int spellDay = 0;
        if (IsWet(cond) || InSpellSeason(row))
            spellDay = SpellDay(cal, cal.Year, cal.DayOfYear);
        if (spellDay >= 1 && !IsWet(cond))
            cond = "rain"; // chained extension of yesterday's rain

        FloodRiskToday = _spell != null && spellDay >= _spell.floodRiskFromDay;

        // District layer: the city rolls ONE base condition (rain spells and
        // flood logic stay city-wide), then the district the player occupies
        // renders its own local variant of it.
        var node = HubMap.Instance != null ? HubMap.Instance.GetNode(DistrictTracker.CurrentNodeId) : null;
        string localCond = DeriveDistrictCondition(cond, node, seed);

        _todayCondition = localCond;
        _fogBurnedOff = false;
        _appliedDistrictId = DistrictTracker.CurrentNodeId;

        ApplyProfile(ProfileForDistrict(localCond, node, seed, spellDay), transitionSeconds);
        if (FloodRiskToday)
            Debug.Log($"[FlorenceWeather] Rain spell day {spellDay} — FLOOD RISK on the Arno (1269/1333-style).");
    }

    // ── per-district variants ────────────────────────────────────────────────

    // FNV-1a — stable across runtimes/domain reloads, unlike string.GetHashCode.
    static int NodeSalt(string id)
    {
        unchecked
        {
            uint h = 2166136261;
            foreach (char c in id ?? "") { h ^= c; h *= 16777619; }
            return (int)h;
        }
    }

    // Pure function of (city condition, microclimate, date, node id): a given
    // district always shows the same local weather on a given day.
    static string DeriveDistrictCondition(string city, HubNode node, int daySeed)
    {
        if (node == null || node.microClimate == MicroClimate.Default) return city;
        var rng = new System.Random(daySeed ^ NodeSalt(node.id));
        switch (node.microClimate)
        {
            case MicroClimate.Riverside:
                // Radiation fog pools over the Arno on days the city wakes clear.
                if (city == "clear" && rng.NextDouble() < 0.35) return "fog";
                return city;
            case MicroClimate.OpenPiazza:
                // Open squares turn gusty under an otherwise clear sky.
                if (city == "clear" && rng.NextDouble() < 0.25) return "wind";
                return city;
            case MicroClimate.Sheltered:
                // Walls and rooflines blunt the worst of it.
                if (city == "storm") return "rain";
                if (city == "hail")  return "rain";
                if (city == "wind")  return "clear";
                return city;
            case MicroClimate.Hilltop:
                // Hills sit above the valley's radiation fog but catch the wind.
                if (city == "fog"   && rng.NextDouble() < 0.65) return "clear";
                if (city == "clear" && rng.NextDouble() < 0.30) return "wind";
                return city;
            default:
                return city;
        }
    }

    string ProfileForDistrict(string cond, HubNode node, int seed, int spellDay)
    {
        int salt = node != null ? NodeSalt(node.id) : 0;
        string profile = ProfileFor(cond, seed ^ salt, spellDay);
        // The river bank catches a rain spell hardest.
        if (node != null && node.microClimate == MicroClimate.Riverside &&
            cond == "rain" && spellDay >= 1)
            profile = "Heavy Rain";
        return profile;
    }

    /// <summary>
    /// The condition ("clear","fog","rain","storm","wind","snow","sleet","hail")
    /// a district shows today. Pure and side-effect free — the Gugol Mappe pins
    /// call this for their weather glyphs, including while the game is paused.
    /// </summary>
    public string ConditionForNode(HubNode node)
    {
        EnsureData();
        var cal = GameCalendar.Instance;
        if (cal == null || _months == null || node == null) return "clear";
        var row = RowForDay(cal, cal.DayOfYear);
        if (row == null) return "clear";

        int seed = Seed(cal.Year, cal.DayOfYear);
        string cond = BaseCondition(row, seed);
        int spellDay = 0;
        if (IsWet(cond) || InSpellSeason(row))
            spellDay = SpellDay(cal, cal.Year, cal.DayOfYear);
        if (spellDay >= 1 && !IsWet(cond))
            cond = "rain";
        return DeriveDistrictCondition(cond, node, seed);
    }

    string ProfileFor(string cond, int seed, int spellDay)
    {
        var rng = new System.Random(seed ^ 0x9E37);
        switch (cond)
        {
            case "fog": return "Dense Fog";
            case "rain":
                if (_spell != null && spellDay >= _spell.heavyFromDay) return "Heavy Rain";
                return rng.NextDouble() < 0.3 ? "Heavy Rain" : "Light Rain";
            case "storm": return "Thunder Storm";
            case "wind": return "Mostly Cloudy";              // gusty grey day
            case "snow": return "Light Snow";
            case "sleet": return "Light Precipitation";       // cold mix
            case "hail": return "Hail Storm";
            default:
                double c = rng.NextDouble();
                return c < 0.5 ? "Clear" : (c < 0.8 ? "Mostly Clear" : "Partly Cloudy");
        }
    }

    /// <summary>Apply a COZY weather profile by name (also used by save-game restore).</summary>
    public void Apply(string profileName) => ApplyProfile(profileName, transitionSeconds);

    // COZY nests some profiles in subfolders (e.g. Heavy Rain), so an exact-path
    // Resources.Load misses them — resolve by NAME over a recursive LoadAll once.
    static System.Collections.Generic.Dictionary<string, WeatherProfile> _profileCache;

    static WeatherProfile FindProfile(string name)
    {
        if (_profileCache == null)
        {
            _profileCache = new System.Collections.Generic.Dictionary<string, WeatherProfile>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in Resources.LoadAll<WeatherProfile>("Profiles/Weather Profiles"))
                if (!_profileCache.ContainsKey(p.name)) _profileCache[p.name] = p;
        }
        _profileCache.TryGetValue(name, out var found);
        return found;
    }

    void ApplyProfile(string profileName, float transition)
    {
        var cozy = CozyWeather.instance;
        if (cozy == null || cozy.weatherModule == null)
        {
            Debug.LogWarning("[FlorenceWeather] COZY not present — cannot set weather.");
            return;
        }

        var profile = FindProfile(profileName);
        if (profile == null)
        {
            Debug.LogWarning($"[FlorenceWeather] COZY profile '{profileName}' not found.");
            return;
        }

        cozy.weatherModule.ecosystem.weatherSelectionMode = CozyEcosystem.EcosystemStyle.manual;
        cozy.weatherModule.ecosystem.SetWeather(profile, transition);
        CurrentProfileName = profileName;

        // A save-restore counts as today's weather — don't re-roll over it.
        var cal = GameCalendar.Instance;
        if (cal != null) _appliedDayKey = cal.Year + ":" + cal.DayOfYear;
        _appliedDistrictId = DistrictTracker.CurrentNodeId;

        Debug.Log($"[FlorenceWeather] Weather set: {profileName}");
    }
}
