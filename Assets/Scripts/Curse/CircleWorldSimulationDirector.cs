using System.Collections.Generic;
using UnityEngine;

// One authoritative daily order: registered sources, simultaneous route bleed,
// then downstream warning evaluation. Time alone never changes Circle state.
[DefaultExecutionOrder(-90)]
public sealed class CircleWorldSimulationDirector : MonoBehaviour
{
    public static CircleWorldSimulationDirector Instance { get; private set; }
    public string AppliedDayKey { get; private set; } = string.Empty;

    GameCalendar _subscribedCalendar;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureRuntimeDirector()
    {
        if (FindAnyObjectByType<CircleWorldSimulationDirector>() != null) return;
        var gameObject = new GameObject("[Circle World Simulation]");
        DontDestroyOnLoad(gameObject);
        gameObject.AddComponent<CircleWorldSimulationDirector>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void RestoreDayKey(string key) => AppliedDayKey = key ?? string.Empty;

    void OnDisable()
    {
        if (_subscribedCalendar != null) _subscribedCalendar.OnDayChanged -= OnDayChanged;
        _subscribedCalendar = null;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void OnDayChanged(GameCalendar calendar) => Tick(calendar);

    void Update()
    {
        var calendar = GameCalendar.Instance;
        if (calendar == null) return;
        if (_subscribedCalendar != calendar)
        {
            if (_subscribedCalendar != null) _subscribedCalendar.OnDayChanged -= OnDayChanged;
            calendar.OnDayChanged += OnDayChanged;
            _subscribedCalendar = calendar;
        }
        Tick(calendar);
    }

    void Tick(GameCalendar calendar)
    {
        if (calendar == null) return;
        string key = calendar.Year + ":" + calendar.DayOfYear;
        if (key == AppliedDayKey) return;

        if (!GameFeatures.CircleWorldEnabled)
        {
            AppliedDayKey = key;
            return;
        }

        var hub = HubMap.Instance;
        if (hub == null) return;
        bool firstSight = string.IsNullOrEmpty(AppliedDayKey);
        string closedDayKey = AppliedDayKey;
        AppliedDayKey = key;
        if (firstSight) return;

        ApplyDatedSources(hub);
        LimboWorldDayResult limboResult =
            LimboWorldSimulationDirector.Instance?.ProcessClosedDay(closedDayKey);
        ApplyCrossTerritoryBleed(hub);
        if (!TryDayIndex(closedDayKey, calendar.daysPerMonth, out int closedDayIndex))
        {
            Debug.LogError($"[CircleWorld] Cannot evaluate warnings for invalid day key '{closedDayKey}'.");
        }
        else if (!CircleWarningLedger.ProcessDay(
                     closedDayKey, closedDayIndex, hub, out string warningError))
        {
            Debug.LogError("[CircleWorld] Warning processing stopped safely: " + warningError);
        }

        float sourceDelta = limboResult != null ? limboResult.totalInfluenceAdded : 0f;
        Debug.Log($"[CircleWorld] {closedDayKey}: sources +{sourceDelta * 100f:0.##}; " +
                  $"Florence Limbo {hub.GetInfluence("firenze", CircleId.Limbo):F3}.");
    }

    static void ApplyDatedSources(HubMap hub)
    {
        var contributions = new Dictionary<(HubNode owner, CircleId circle), float>();
        foreach (var site in hub.AllNodes)
        {
            if (!site.isRitualSite) continue;
            HubNode owner = hub.ResolveInfluenceTerritory(site.id);
            var definition = hub.GetCircleDefinition(site.nativeCircle);
            if (owner == null || definition == null || definition.dailyRitualBonus <= 0f) continue;
            float block = 1f - owner.sanctity * definition.sanctityResistance;
            var key = (owner, definition.circleId);
            float delta = definition.dailyRitualBonus * Mathf.Clamp01(block);
            contributions[key] = contributions.TryGetValue(key, out float prior) ? prior + delta : delta;
        }

        foreach (var pair in contributions)
            hub.AddInfluence(pair.Key.owner.id, pair.Key.circle, pair.Value);
    }

    public static void ApplyCrossTerritoryBleed(HubMap hub)
    {
        if (hub == null) return;
        hub.EnsureGraphBuilt();
        List<HubNode> owners = hub.GetInfluenceTerritories();
        List<CurseDefinition> definitions = CollectDefinitions(hub);
        var snapshot = new Dictionary<(HubNode owner, CircleId circle), float>();
        foreach (var owner in owners)
            foreach (var definition in definitions)
                snapshot[(owner, definition.circleId)] = owner.GetOwnedInfluence(definition.circleId);

        var incoming = new Dictionary<(HubNode owner, CircleId circle), float>();
        foreach (var definition in definitions)
        {
            foreach (var source in owners)
            {
                float sourceValue = snapshot[(source, definition.circleId)];
                if (sourceValue <= definition.bleedThreshold) continue;

                foreach (var route in hub.GetInfluenceRoutes(source))
                {
                    var targetKey = (route.Target, definition.circleId);
                    float targetValue = snapshot.TryGetValue(targetKey, out float stored) ? stored : 0f;
                    float delta = CirclePropagationMath.CalculateBleed(
                        definition,
                        sourceValue,
                        targetValue,
                        route.Target.sanctity,
                        route.Strength);
                    float prior = incoming.TryGetValue(targetKey, out float value) ? value : 0f;
                    incoming[targetKey] = CirclePropagationMath.AddWithDailyCap(
                        prior,
                        delta,
                        definition.targetDailyBleedCap);
                }
            }
        }

        foreach (var pair in incoming)
            hub.AddInfluence(pair.Key.owner.id, pair.Key.circle, pair.Value);
    }

    static List<CurseDefinition> CollectDefinitions(HubMap hub)
    {
        var definitions = new List<CurseDefinition>();
        if (hub.circleDefinitions != null)
            foreach (var definition in hub.circleDefinitions)
                if (definition != null && !definitions.Contains(definition)) definitions.Add(definition);
        if (hub.activeCurse != null && !definitions.Contains(hub.activeCurse))
            definitions.Add(hub.activeCurse);
        return definitions;
    }

    static bool TryDayIndex(string dayKey, int daysPerMonth, out int dayIndex)
    {
        dayIndex = 0;
        string[] parts = (dayKey ?? string.Empty).Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[0], out int year) ||
            !int.TryParse(parts[1], out int dayOfYear) || year < 0 || dayOfYear < 0)
            return false;
        int daysPerYear = Mathf.Max(1, daysPerMonth) * 12;
        if (dayOfYear >= daysPerYear) return false;
        dayIndex = checked(year * daysPerYear + dayOfYear);
        return true;
    }
}
