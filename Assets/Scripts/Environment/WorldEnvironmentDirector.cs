using System;
using System.Collections.Generic;
using DistantLands.Cozy;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Project-owned authority for continuous world time and the typed environment
/// snapshot. COZY is a renderer: this director advances the clock and mirrors
/// the result into COZY without allowing the package to advance a second clock.
/// </summary>
public sealed class WorldEnvironmentDirector : MonoBehaviour
{
    static WorldEnvironmentDirector _instance;
    public static WorldEnvironmentDirector Instance =>
        _instance != null ? _instance : (_instance = FindAnyObjectByType<WorldEnvironmentDirector>());

    [SerializeField] WorldEnvironmentProfile profile;
    [SerializeField, Range(0f, 24f)] float currentHour = 8f;
    [SerializeField, Range(0f, 1f)] float accumulatedWetness;
    [SerializeField] WorldWeatherState currentWeather = default;
    [SerializeField] string[] blockedScenes = { "Battle", "BattleArena" };

    readonly HashSet<string> _pauseReasons = new HashSet<string>(StringComparer.Ordinal);
    CozyTimeModule _cozyTime;
    bool _cozyTimeWasEnabled;
    WorldTimePhase _lastPhase;
    bool _phaseInitialized;

    public event Action<WorldEnvironmentSnapshot> EnvironmentChanged;
    public event Action<WorldTimePhase> PhaseChanged;

    public WorldEnvironmentProfile Profile => profile;
    public float Hour => Mathf.Repeat(currentHour, 24f);
    public bool IsPaused => _pauseReasons.Count > 0 || Time.timeScale <= 0f;
    public IReadOnlyCollection<string> PauseReasons => _pauseReasons;
    public WorldWeatherState Weather => currentWeather.Clamped();
    public float AccumulatedWetness => accumulatedWetness;

    public WorldEnvironmentSnapshot Snapshot
    {
        get
        {
            GameCalendar calendar = GameCalendar.Instance;
            int year = calendar != null ? calendar.Year : 1299;
            int day = calendar != null ? calendar.DayOfYear : 0;
            string date = calendar != null ? calendar.DateString : $"day-{day}";
            return new WorldEnvironmentSnapshot
            {
                dateKey = date,
                year = year,
                dayOfYear = day,
                hour = Hour,
                phase = EffectiveProfile.PhaseAt(Hour),
                districtId = DistrictTracker.CurrentNodeId,
                weather = Weather,
                accumulatedWetness = accumulatedWetness
            }.Clamped();
        }
    }

    WorldEnvironmentProfile EffectiveProfile
    {
        get
        {
            if (profile != null) return profile;
            profile = Resources.Load<WorldEnvironmentProfile>("Environment/WorldEnvironmentProfile");
            if (profile != null) return profile;
            profile = ScriptableObject.CreateInstance<WorldEnvironmentProfile>();
            profile.hideFlags = HideFlags.HideAndDontSave;
            Debug.LogWarning("[WorldEnvironment] Missing Resources/Environment/WorldEnvironmentProfile; using defaults.");
            return profile;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureRuntimeAuthority()
    {
        if (Instance != null) return;
        GameSystemsRoot systems = FindAnyObjectByType<GameSystemsRoot>();
        GameObject host = systems != null ? systems.gameObject : new GameObject("WorldEnvironmentRuntime");
        if (systems == null) DontDestroyOnLoad(host);
        host.AddComponent<WorldEnvironmentDirector>();
        Debug.Log($"[WorldEnvironment] Runtime authority installed on '{host.name}'.");
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        _instance = this;
        if (Application.isPlaying) DontDestroyOnLoad(gameObject);
        if (string.IsNullOrEmpty(currentWeather.sourceProfileName))
            currentWeather = EffectiveProfile.ClearFallback;
        BindCozyClock();
        ApplyScenePause(SceneManager.GetActiveScene().name);
        Publish(true);
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        RestoreCozyClock();
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    void Update()
    {
        BindCozyClock();
        if (!IsPaused)
        {
            currentHour = Mathf.Repeat(
                currentHour + EffectiveProfile.GameHoursPerRealSecond(currentHour) * Time.unscaledDeltaTime,
                24f);
        }
        MirrorToCozy();
        UpdateWetness(Time.unscaledDeltaTime);
        Publish(false);
    }

    public void SetHour(float hour)
    {
        currentHour = Mathf.Repeat(hour, 24f);
        MirrorToCozy();
        Publish(true);
    }

    public void SetWeather(WorldWeatherState weather)
    {
        currentWeather = weather.Clamped();
        Publish(true);
    }

    public void SetAccumulatedWetness(float wetness)
    {
        accumulatedWetness = Mathf.Clamp01(wetness);
        Publish(true);
    }

    public void SetPaused(string reason, bool paused)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Pause reason is required.", nameof(reason));
        if (paused) _pauseReasons.Add(reason);
        else _pauseReasons.Remove(reason);
    }

    public bool HasPauseReason(string reason) =>
        !string.IsNullOrEmpty(reason) && _pauseReasons.Contains(reason);

    public string Describe()
    {
        string reasons = _pauseReasons.Count == 0 ? "none" : string.Join(",", _pauseReasons);
        return $"hour={Hour:F3} phase={EffectiveProfile.PhaseAt(Hour)} paused={IsPaused} " +
               $"reasons=[{reasons}] weather={Weather.kind} wetness={accumulatedWetness:F2}";
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyScenePause(scene.name);
        BindCozyClock();
        MirrorToCozy();
    }

    void ApplyScenePause(string sceneName)
    {
        bool blocked = false;
        foreach (string blockedScene in blockedScenes)
        {
            if (!string.Equals(sceneName, blockedScene, StringComparison.Ordinal)) continue;
            blocked = true;
            break;
        }
        SetPaused("scene", blocked);
    }

    void BindCozyClock()
    {
        CozyWeather cozy = CozyWeather.instance;
        CozyTimeModule time = cozy != null ? cozy.timeModule : null;
        if (time == null || time == _cozyTime) return;
        RestoreCozyClock();
        _cozyTime = time;
        _cozyTimeWasEnabled = time.enabled;
        currentHour = (float)time.currentTime * 24f;
        time.enabled = false;
        MirrorToCozy();
    }

    void RestoreCozyClock()
    {
        if (_cozyTime == null) return;
        _cozyTime.enabled = _cozyTimeWasEnabled;
        _cozyTime = null;
    }

    void MirrorToCozy()
    {
        if (_cozyTime != null)
            _cozyTime.currentTime = Hour / 24f;
    }

    void UpdateWetness(float realDeltaSeconds)
    {
        float target = currentWeather.Clamped().wetnessTarget;
        float rate = target > accumulatedWetness ? 0.0125f : 0.0035f;
        accumulatedWetness = Mathf.MoveTowards(
            accumulatedWetness,
            target,
            Mathf.Max(0f, realDeltaSeconds) * rate);
    }

    void Publish(bool force)
    {
        WorldTimePhase phase = EffectiveProfile.PhaseAt(Hour);
        if (force || !_phaseInitialized || phase != _lastPhase)
        {
            _lastPhase = phase;
            _phaseInitialized = true;
            PhaseChanged?.Invoke(phase);
        }
        EnvironmentChanged?.Invoke(Snapshot);
    }
}
