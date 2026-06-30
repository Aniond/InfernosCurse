using UnityEngine;
using System.Collections.Generic;

// Manages the CT-based turn order.
// Each Tick() advances all units' CT by their Speed.
// When a unit hits 100, it's pulled out as the active unit.
public class CTQueue
{
    private List<BattleUnit> _units = new List<BattleUnit>();

    public void Register(BattleUnit unit)
    {
        if (!_units.Contains(unit)) _units.Add(unit);
    }

    public void Unregister(BattleUnit unit) => _units.Remove(unit);

    // Advance CT until at least one unit is ready. Returns all ready units sorted by CT overage.
    public List<BattleUnit> TickUntilReady()
    {
        int safety = 10000;
        while (safety-- > 0)
        {
            // Tick CT on all alive, non-charging units
            foreach (var u in _units)
            {
                if (!u.IsAlive) continue;
                float gain = BattleFormulas.CTGainPerTick(u);
                u.TickCT(gain);
            }

            // Tick charge on charging units
            foreach (var u in _units)
            {
                if (u.IsAlive && u.state == UnitState.Charging)
                    u.TickCharge();
            }

            var ready = GetReadyUnits();
            if (ready.Count > 0) return ready;
        }

        Debug.LogError("[CTQueue] Infinite loop guard hit — check Speed stats.");
        return new List<BattleUnit>();
    }

    // Returns units that have reached CT >= 100, sorted descending by CT (highest goes first)
    public List<BattleUnit> GetReadyUnits()
    {
        var ready = new List<BattleUnit>();
        foreach (var u in _units)
            if (u.IsReady) ready.Add(u);

        ready.Sort((a, b) => b.ct.CompareTo(a.ct));
        return ready;
    }

    // Preview of upcoming turn order (for UI sidebar) — simulate without mutating state
    public List<BattleUnit> PeekOrder(int lookahead = 8)
    {
        // Snapshot CT values
        var snapshot = new Dictionary<BattleUnit, float>();
        foreach (var u in _units)
            if (u.IsAlive) snapshot[u] = u.ct;

        var order = new List<BattleUnit>();
        int safety = 100000;

        while (order.Count < lookahead && safety-- > 0)
        {
            // Find minimum ticks for any unit to reach 100
            float minTicks = float.MaxValue;
            foreach (var kvp in snapshot)
            {
                float gain = BattleFormulas.CTGainPerTick(kvp.Key);
                if (gain <= 0) continue;
                float needed = (100f - kvp.Value) / gain;
                if (needed < minTicks) minTicks = needed;
            }

            // Advance all by minTicks
            var keys = new List<BattleUnit>(snapshot.Keys);
            foreach (var u in keys)
                snapshot[u] += BattleFormulas.CTGainPerTick(u) * minTicks;

            // Pull ready units
            var readyNow = new List<BattleUnit>();
            foreach (var kvp in snapshot)
                if (kvp.Value >= 100f) readyNow.Add(kvp.Key);

            readyNow.Sort((a, b) => snapshot[b].CompareTo(snapshot[a]));
            foreach (var u in readyNow)
            {
                order.Add(u);
                snapshot[u] -= 100f;
                if (order.Count >= lookahead) break;
            }
        }

        return order;
    }

    public void Clear() => _units.Clear();

    public int Count => _units.Count;
}
