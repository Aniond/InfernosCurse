using System.Text;
using UnityEngine;

// The corruption tide: once per in-game DAY, every district's curse grows a
// little. This — not HubMap's dormant real-time diffusion — is the hub-curse
// driver, so time itself is the resource the player spends by resting.
//
// Ticks via GameCalendar.OnDayChanged — which fires once per AdvanceDay — so a
// multi-day jump (road travel, rest chains) charges EVERY day it burns, not
// just the last one. The day-key poll in Update remains as an init-order /
// domain-reload safety net; the key check makes both paths idempotent per day.
// The first key ever seen is recorded WITHOUT ticking (baseline day); the key
// persists through saves so loading never double-ticks.
//
// Guild perks deliberately have no hook here: guilds bend REST costs and
// enable cleansing — the tide itself is untouchable (design rule: perks
// reduce/redirect, never zero, the daily increment).
public class DailyCurseDrift : MonoBehaviour
{
    [Tooltip("Nodes that touch the Arno — they take the flood spike on " +
             "flood-risk days.")]
    public string[] riverNodeIds = { "pontevecchio", "oltrarno" };

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
        var hub = HubMap.Instance;
        if (cal == null || hub == null || hub.activeCurse == null) return;

        string key = cal.Year + ":" + cal.DayOfYear;
        if (key == AppliedDayKey) return;

        bool firstSight = string.IsNullOrEmpty(AppliedDayKey);
        AppliedDayKey = key;
        if (firstSight) return; // baseline day — record, don't tick

        ApplyDrift(hub, hub.activeCurse, cal);
    }

    void ApplyDrift(HubMap hub, CurseDefinition curse, GameCalendar cal)
    {
        // Per-day flood check: the cached FloodRiskToday static goes stale
        // across AdvanceDay×N loops (it reflects the last ApplyToday only).
        var fw = FlorenceWeather.Instance;
        bool flood = fw != null
            ? fw.ComputeFloodRisk(cal.Year, cal.DayOfYear)
            : FlorenceWeather.FloodRiskToday;
        foreach (var node in hub.AllNodes)
        {
            float delta = curse.dailyGrowthBase;
            if (node.isRitualSite) delta += curse.dailyRitualBonus;
            if (node.isSanctuarySite) delta -= curse.dailySanctuaryRelief;
            delta *= 1f - node.sanctity * curse.sanctityResistance;
            if (flood && IsRiverNode(node.id)) delta += curse.dailyFloodSpike;

            if (delta > 0f) hub.AddCurse(node.id, delta);
            // negative delta (deep sanctuary) = the site holds the line; it
            // does not passively cleanse — cleansing is earned (guild/ritual).
        }
        Debug.Log($"[DailyCurseDrift] day tick{(flood ? " (ARNO FLOOD-HIGH)" : "")} — global {hub.GlobalCurseLevel():F3}");
    }

    bool IsRiverNode(string id)
    {
        if (riverNodeIds == null) return false;
        foreach (var r in riverNodeIds) if (r == id) return true;
        return false;
    }

    // ── Out-of-Unity-style economy sim, runnable from the inspector ─────────
    // Right-click the component header → "Simulate 60 Days". Pure math over a
    // CLONE of the node list — game state is untouched. Compares four rest
    // policies to sanity-check the corruption economy.
    [ContextMenu("Simulate 60 Days")]
    void Simulate60Days()
    {
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
                    float delta = curse.dailyGrowthBase;
                    if (node.isRitualSite) delta += curse.dailyRitualBonus;
                    if (node.isSanctuarySite) delta -= curse.dailySanctuaryRelief;
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
