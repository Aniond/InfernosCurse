using System.Text;
using UnityEngine;

// The dated Circle-influence director. A day transition evaluates explicit
// sources (such as active ritual sites) and high-pressure route bleed. A day
// with no source is inert, so free exploration is never its own punishment.
//
// Ticks via GameCalendar.OnDayChanged — which fires once per AdvanceDay — so a
// multi-day jump (road travel, rest chains) charges EVERY day it burns, not
// just the last one. The day-key poll in Update remains as an init-order /
// domain-reload safety net; the key check makes both paths idempotent per day.
// The first key ever seen is recorded WITHOUT ticking (baseline day); the key
// persists through saves so loading never double-ticks.
//
public class DailyCurseDrift : MonoBehaviour
{
    /// <summary>Last day-key applied (persisted by SaveSystem).</summary>
    public string AppliedDayKey { get; private set; } = "";

    GameCalendar _subscribedTo;

    public void RestoreDayKey(string key) => AppliedDayKey = key;

    void OnDisable()
    {
        if (_subscribedTo != null) _subscribedTo.OnDayChanged -= OnDayChanged;
        _subscribedTo = null;
    }

    void OnDayChanged(GameCalendar cal) => Tick(cal);

    void Update()
    {
        var cal = GameCalendar.Instance;
        if (cal == null) return;

        // Lazy subscribe (calendar lives on the same prefab but init order and
        // mid-play domain reloads make Awake-time subscription unreliable).
        if (_subscribedTo != cal)
        {
            if (_subscribedTo != null) _subscribedTo.OnDayChanged -= OnDayChanged;
            cal.OnDayChanged += OnDayChanged;
            _subscribedTo = cal;
        }

        Tick(cal);   // safety net: catches the baseline day + anything missed
    }

    void Tick(GameCalendar cal)
    {
        if (cal != null && !GameFeatures.CorruptionEnabled)
        {
            // Track the current day while parked so re-enabling never applies a
            // backlog of corruption ticks.
            AppliedDayKey = cal.Year + ":" + cal.DayOfYear;
            return;
        }
        var hub = HubMap.Instance;
        if (cal == null || hub == null) return;

        string key = cal.Year + ":" + cal.DayOfYear;
        if (key == AppliedDayKey) return;

        bool firstSight = string.IsNullOrEmpty(AppliedDayKey);
        AppliedDayKey = key;
        if (firstSight) return; // baseline day — record, don't tick

        ApplyDatedSources(hub);
        ApplyCrossLocationBleed(hub);
    }

    void ApplyDatedSources(HubMap hub)
    {
        foreach (var node in hub.AllNodes)
        {
            if (!node.isRitualSite) continue;
            var definition = hub.GetCircleDefinition(node.nativeCircle);
            if (definition == null || definition.dailyRitualBonus <= 0f) continue;
            float block = 1f - node.sanctity * definition.sanctityResistance;
            hub.AddInfluence(node.id, definition.circleId,
                definition.dailyRitualBonus * Mathf.Clamp01(block));
        }
        Debug.Log($"[CircleInfluence] dated sources evaluated — Limbo {hub.GlobalInfluence(CircleId.Limbo):F3}");
    }

    void ApplyCrossLocationBleed(HubMap hub)
    {
        var definitions = new System.Collections.Generic.List<CurseDefinition>();
        if (hub.circleDefinitions != null)
            foreach (var definition in hub.circleDefinitions)
                if (definition != null && !definitions.Contains(definition)) definitions.Add(definition);
        if (hub.activeCurse != null && !definitions.Contains(hub.activeCurse))
            definitions.Add(hub.activeCurse);

        foreach (var definition in definitions)
        {
            var incoming = new System.Collections.Generic.Dictionary<HubNode, float>();
            foreach (var source in hub.AllNodes)
            {
                float sourceValue = source.GetInfluence(definition.circleId);
                if (sourceValue <= definition.bleedThreshold) continue;

                foreach (var target in source.neighbors)
                {
                    float targetValue = target.GetInfluence(definition.circleId);
                    float delta = CalculateBleed(definition, sourceValue, targetValue, target.sanctity, 1f);
                    if (delta <= 0f) continue;
                    incoming[target] = incoming.TryGetValue(target, out float prior) ? prior + delta : delta;
                }
            }

            foreach (var pair in incoming)
                hub.AddInfluence(pair.Key.id, definition.circleId,
                    Mathf.Min(pair.Value, definition.targetDailyBleedCap));
        }
    }

    public static float CalculateBleed(
        CurseDefinition definition,
        float sourceInfluence,
        float targetInfluence,
        float targetSanctity,
        float routeStrength)
    {
        if (definition == null || sourceInfluence <= definition.bleedThreshold) return 0f;
        float pressure = Mathf.InverseLerp(definition.bleedThreshold, 1f, sourceInfluence);
        float gradient = Mathf.Clamp01((sourceInfluence - targetInfluence) / 0.25f);
        float block = 1f - Mathf.Clamp01(targetSanctity) * definition.sanctityResistance;
        return definition.maxDailyBleed * pressure * gradient *
               Mathf.Clamp01(routeStrength) * Mathf.Clamp01(block);
    }

    // ── Out-of-Unity-style economy sim, runnable from the inspector ─────────
    // Right-click the component header → "Simulate 60 Days". Pure math over a
    // CLONE of the node list — game state is untouched. Compares four rest
    // policies to sanity-check the corruption economy.
    [ContextMenu("Simulate 60 Days")]
    void Simulate60Days()
    {
        if (!GameFeatures.CorruptionEnabled)
        {
            Debug.Log("[DriftSim] Corruption is disabled in GameFeatureSettings.");
            return;
        }
        var hub = HubMap.Instance != null ? HubMap.Instance : FindAnyObjectByType<HubMap>();
        var curse = hub != null ? hub.activeCurse : null;
        if (hub == null || curse == null) { Debug.LogWarning("[DriftSim] HubMap/curse missing"); return; }
        hub.EnsureGraphBuilt(); // edit-mode instantiation skips Awake
        if (hub.AllNodes.Count == 0) { Debug.LogWarning("[DriftSim] no nodes"); return; }

        string[] policies = { "never rest", "camp daily", "inn daily r0", "guild inn daily r4" };
        float[] restAdd = { 0f, 0.035f, 0.02f, 0.02f * 0.40f };
        float[] cleansePct = { 0f, 0f, 0f, 0.10f };

        var sb = new StringBuilder("[DriftSim] mercato curse by policy (rest district = mercato)\n");
        sb.Append("day |");
        foreach (var p in policies) sb.Append($" {p,18} |");
        sb.Append(" ponte(src, never) | global(never)\n");

        // clone state per policy
        int n = hub.AllNodes.Count;
        float[][] lv = new float[policies.Length][];
        float[][] sa = new float[policies.Length][];
        for (int p = 0; p < policies.Length; p++)
        {
            lv[p] = new float[n]; sa[p] = new float[n];
            for (int i = 0; i < n; i++) { lv[p][i] = hub.AllNodes[i].curseLevel; sa[p][i] = hub.AllNodes[i].sanctity; }
        }
        int mercato = hub.AllNodes.FindIndex(x => x.id == "mercato");
        int ponte = hub.AllNodes.FindIndex(x => x.id == "pontevecchio");

        for (int day = 1; day <= 60; day++)
        {
            for (int p = 0; p < policies.Length; p++)
            {
                for (int i = 0; i < n; i++)
                {
                    var node = hub.AllNodes[i];
                    float delta = node.isRitualSite && node.nativeCircle == curse.circleId
                        ? curse.dailyRitualBonus
                        : 0f;
                    delta *= 1f - sa[p][i] * curse.sanctityResistance;
                    if (delta > 0f) lv[p][i] = Mathf.Min(1f, lv[p][i] + delta);
                }
                if (mercato >= 0)
                {
                    lv[p][mercato] = Mathf.Min(1f, lv[p][mercato] + restAdd[p]);
                    if (cleansePct[p] > 0f)
                    {
                        float amt = lv[p][mercato] * cleansePct[p];
                        lv[p][mercato] = Mathf.Max(0f, lv[p][mercato] - amt);
                        sa[p][mercato] = Mathf.Min(1f, sa[p][mercato] + amt * 0.3f);
                    }
                }
            }
            if (day % 10 == 0)
            {
                sb.Append($"{day,3} |");
                for (int p = 0; p < policies.Length; p++)
                    sb.Append($" {(mercato >= 0 ? lv[p][mercato] : -1f),18:F3} |");
                float global = 0f; for (int i = 0; i < n; i++) global += lv[0][i];
                sb.Append($" {(ponte >= 0 ? lv[0][ponte] : -1f),17:F3} | {global / n:F3}\n");
            }
        }
        Debug.Log(sb.ToString());
    }
}
