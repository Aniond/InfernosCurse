using System;
using UnityEngine;

/// <summary>
/// Season of the year, derived from the current month. Spring begins in March —
/// matching both the astronomical season and the Florentine civil new year.
/// </summary>
public enum Season { Spring, Summer, Autumn, Winter }

/// <summary>
/// Persistent in-game calendar using the medieval Florentine reckoning
/// (<i>stile fiorentino</i>): the civil year begins on <b>25 March</b>, the Feast
/// of the Annunciation — NOT 1 January. Months carry period Italian names.
///
/// Time is driven off <see cref="DayNightCycle.timeOfDay"/>: each time the day
/// clock wraps past midnight, one calendar day passes. Months are a small fixed
/// length (<see cref="daysPerMonth"/>) so seasons cycle at a playable pace while
/// the month identity + Annunciation new-year stay historically authentic.
///
/// Add this to the GameSystems prefab — it's a DontDestroyOnLoad singleton like
/// HubMap / DayNightCycle. The future weather picker reads <see cref="CurrentMonth"/>
/// / <see cref="CurrentSeason"/> / <see cref="YearProgress"/> to key a real-Tuscan
/// weather-probability table.
/// </summary>
public class GameCalendar : MonoBehaviour
{
    public static GameCalendar Instance { get; private set; }

    /// <summary>
    /// The twelve months in Florentine civil-year order — the year opens with
    /// March (Marzo) because of the 25 March Annunciation new year.
    /// Index 0 = Marzo ... index 11 = Febbraio.
    /// </summary>
    public enum Month
    {
        Marzo, Aprile, Maggio, Giugno, Luglio, Agosto,
        Settembre, Ottobre, Novembre, Dicembre, Gennaio, Febbraio
    }

    [Header("Pace")]
    [Tooltip("In-game days per month. Short + fixed so seasons cycle at a playable " +
             "rate. The month NAMES and Annunciation new-year stay authentic; only " +
             "the length is compressed. Full year = daysPerMonth × 12 in-game days.")]
    [Min(1)] public int daysPerMonth = 4;

    [Header("Start date (Florentine)")]
    [Tooltip("Civil year to start in (e.g. 1299). Increments each 25 March rollover.")]
    public int startYear = 1299;
    [Tooltip("Month to start in (Florentine order: Marzo=year start).")]
    public Month startMonth = Month.Ottobre;
    [Tooltip("Day within the start month (1-based, clamped to daysPerMonth).")]
    [Min(1)] public int startDayOfMonth = 1;

    // ── Live state ──────────────────────────────────────────────────────────────
    // Initialised to valid values at declaration so the date is never in an invalid
    // (e.g. _dayOfMonth == 0 → negative DayOfYear) state before Awake copies the
    // start fields. Awake overwrites these with the configured start date.
    [Header("Current (read-only at runtime)")]
    [SerializeField] private int _year = 1299;
    [SerializeField] private Month _month = Month.Marzo;
    [SerializeField] private int _dayOfMonth = 1;   // 1-based

    public int Year => _year;
    public Month CurrentMonth => _month;
    public int DayOfMonth => _dayOfMonth;
    public Season CurrentSeason => SeasonOf(_month);

    /// <summary>0-based day within the civil year (0 .. daysPerMonth*12 - 1).</summary>
    public int DayOfYear => (int)_month * daysPerMonth + (_dayOfMonth - 1);

    /// <summary>
    /// Fraction through the civil year, 0 at 25 March, →1 approaching next 25 March.
    /// This is the value a seasonal weather curve keys off.
    /// </summary>
    public float YearProgress => DayOfYear / (float)(daysPerMonth * 12);

    /// <summary>Human string like "3 Ottobre 1299".</summary>
    public string DateString => $"{_dayOfMonth} {_month} {_year}";

    // ── Events ──────────────────────────────────────────────────────────────────
    public event Action<GameCalendar> OnDayChanged;
    public event Action<Month> OnMonthChanged;
    public event Action<Season> OnSeasonChanged;
    /// <summary>Fires once when the civil year rolls over (past 25 March).</summary>
    public event Action<int> OnYearChanged;

    private DayNightCycle _dnc;
    private float _lastTimeOfDay;
    private bool _hasPrevTime;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (Application.isPlaying) DontDestroyOnLoad(gameObject);

        _year = startYear;
        _month = startMonth;
        _dayOfMonth = Mathf.Clamp(startDayOfMonth, 1, Mathf.Max(1, daysPerMonth));
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    DayNightCycle DNC => _dnc != null ? _dnc : (_dnc = FindAnyObjectByType<DayNightCycle>());

    void Update()
    {
        if (!Application.isPlaying) return;
        var dnc = DNC;
        if (dnc == null) return;

        float t = dnc.timeOfDay;
        if (!_hasPrevTime) { _lastTimeOfDay = t; _hasPrevTime = true; return; }

        // The day clock counts up and wraps (…23.9 → 0.x). A large drop = midnight
        // crossed = one day elapsed. Guard against tiny jitter with a threshold.
        if (t + 6f < _lastTimeOfDay)
            AdvanceDay();

        _lastTimeOfDay = t;
    }

    /// <summary>Advance the calendar by one day, firing rollover events as needed.</summary>
    public void AdvanceDay()
    {
        Season prevSeason = CurrentSeason;
        Month prevMonth = _month;

        _dayOfMonth++;
        if (_dayOfMonth > daysPerMonth)
        {
            _dayOfMonth = 1;
            RollMonth();
        }

        OnDayChanged?.Invoke(this);

        if (_month != prevMonth)
        {
            OnMonthChanged?.Invoke(_month);
            Season s = CurrentSeason;
            if (s != prevSeason) OnSeasonChanged?.Invoke(s);
        }
    }

    void RollMonth()
    {
        int next = (int)_month + 1;
        if (next > (int)Month.Febbraio)
        {
            // Wrapped past Febbraio back to Marzo — a new civil year begins
            // (the 25 March Annunciation rollover, compressed to the month edge).
            next = (int)Month.Marzo;
            _year++;
            OnYearChanged?.Invoke(_year);
        }
        _month = (Month)next;
    }

    /// <summary>
    /// Season from month. Florentine/astronomical: spring opens in March (the civil
    /// new year), each season spanning three months.
    /// </summary>
    public static Season SeasonOf(Month m)
    {
        switch (m)
        {
            case Month.Marzo:
            case Month.Aprile:
            case Month.Maggio:    return Season.Spring;
            case Month.Giugno:
            case Month.Luglio:
            case Month.Agosto:    return Season.Summer;
            case Month.Settembre:
            case Month.Ottobre:
            case Month.Novembre:  return Season.Autumn;
            default:              return Season.Winter; // Dicembre, Gennaio, Febbraio
        }
    }

    /// <summary>English month name for UI/tooltips (Marzo → "March").</summary>
    public static string EnglishName(Month m)
    {
        switch (m)
        {
            case Month.Marzo:     return "March";
            case Month.Aprile:    return "April";
            case Month.Maggio:    return "May";
            case Month.Giugno:    return "June";
            case Month.Luglio:    return "July";
            case Month.Agosto:    return "August";
            case Month.Settembre: return "September";
            case Month.Ottobre:   return "October";
            case Month.Novembre:  return "November";
            case Month.Dicembre:  return "December";
            case Month.Gennaio:   return "January";
            default:              return "February";
        }
    }
}
