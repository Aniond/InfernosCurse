using UnityEngine;
using System.Collections.Generic;

// Shared world model — one instance per battle, read by all AI agents.
// Matches the state representation in the Sun Tzu design document.
public class InfernalWorldState
{
    // ── Global ────────────────────────────────────────────────────────────────
    public float    globalCurseLevel;       // 0-1, rises as anchors survive
    public int      currentCircle = 1;      // 1-9, drives dominant weather type
    public float    battleElapsedTicks;

    // ── Per-cell maps (indexed by Vector2Int grid position) ───────────────────
    public Dictionary<Vector2Int, float> curseDensity   = new();  // 0-1
    public Dictionary<Vector2Int, float> sanctityLevel  = new();  // 0-1
    public Dictionary<Vector2Int, float> threatLevel    = new();  // 0-1
    public Dictionary<Vector2Int, float> visibilityMap  = new();  // 0-1
    public Dictionary<Vector2Int, float> retreatSafety  = new();  // 0-1 (for enemies)

    // ── Ritual / anchor nodes ─────────────────────────────────────────────────
    public List<RitualNode> ritualNodes = new();

    // ── Belief state about the player ─────────────────────────────────────────
    public BeliefState playerBelief = new();

    // ── Sound / event splats ──────────────────────────────────────────────────
    public List<SoundEvent> recentSoundEvents = new();

    // ── Accessors ─────────────────────────────────────────────────────────────

    public float GetCurseDensity(Vector2Int pos)  => curseDensity.TryGetValue(pos,  out var v) ? v : 0f;
    public float GetSanctity(Vector2Int pos)      => sanctityLevel.TryGetValue(pos, out var v) ? v : 0f;
    public float GetThreat(Vector2Int pos)        => threatLevel.TryGetValue(pos,   out var v) ? v : 0f;
    public float GetVisibility(Vector2Int pos)    => visibilityMap.TryGetValue(pos, out var v) ? v : 1f;
    public float GetRetreatSafety(Vector2Int pos) => retreatSafety.TryGetValue(pos, out var v) ? v : 0.5f;

    public void SetCurseDensity(Vector2Int pos, float val)  => curseDensity[pos]  = Mathf.Clamp01(val);
    public void SetSanctity(Vector2Int pos, float val)      => sanctityLevel[pos] = Mathf.Clamp01(val);
    public void SetThreat(Vector2Int pos, float val)        => threatLevel[pos]   = Mathf.Clamp01(val);

    // ── Update threat map from all player units ───────────────────────────────

    public void RefreshThreatMap(List<BattleUnit> playerUnits, BattleGrid grid)
    {
        threatLevel.Clear();
        foreach (var u in playerUnits)
        {
            if (!u.IsAlive) continue;
            var stats = u.Data.GetTotalStats();
            int range = Mathf.Max(2, stats.perception / 3);
            var cells = grid.GetAttackRange(u.gridPosition, 0, range);
            foreach (var cell in cells)
            {
                float dist   = BattleGrid.ManhattanDistance(u.gridPosition, cell.gridPos);
                float threat = 1f - (dist / (range + 1f));
                float cur    = GetThreat(cell.gridPos);
                SetThreat(cell.gridPos, Mathf.Max(cur, threat));
            }
        }
    }

    // ── Update visibility (fog of war / weather visibility) ───────────────────

    public void RefreshVisibility(BattleGrid grid, float weatherVisibilityMod = 1f)
    {
        visibilityMap.Clear();
        for (int x = 0; x < grid.width; x++)
            for (int y = 0; y < grid.height; y++)
            {
                var pos = new Vector2Int(x, y);
                visibilityMap[pos] = weatherVisibilityMod;
            }
    }

    // ── Update retreat safety (low threat + low curse = safe for enemies) ─────

    public void RefreshRetreatSafety(BattleGrid grid)
    {
        retreatSafety.Clear();
        for (int x = 0; x < grid.width; x++)
            for (int y = 0; y < grid.height; y++)
            {
                var pos    = new Vector2Int(x, y);
                float safe = (1f - GetThreat(pos)) * (0.5f + GetCurseDensity(pos) * 0.5f);
                retreatSafety[pos] = safe;
            }
    }

    // ── Add sound event (hearing sensor) ─────────────────────────────────────

    public void AddSoundEvent(Vector2Int origin, float radius, string tag)
    {
        recentSoundEvents.Add(new SoundEvent { origin = origin, radius = radius, tag = tag });
        if (recentSoundEvents.Count > 32) recentSoundEvents.RemoveAt(0);
    }

    public void TickBelief(BattleUnit observedPlayer)
    {
        playerBelief.Update(observedPlayer);
    }
}

// ── Supporting types ──────────────────────────────────────────────────────────

[System.Serializable]
public class RitualNode
{
    public Vector2Int position;
    public float      curseLevel;       // 0-1
    public float      anchorPower;      // boosts nearby enemy stats
    public float      reservoirPower;   // fuels curse spread
    public bool       active;
    public BattleUnit guardian;         // null if unguarded
}

[System.Serializable]
public class SoundEvent
{
    public Vector2Int origin;
    public float      radius;
    public string     tag;
}

public class BeliefState
{
    // Where we think the player is (confidence per cell)
    public Dictionary<Vector2Int, float> positionConfidence = new();

    // Player behavioral profile — updated from observations
    public float aggression      = 0.5f;  // 0=passive, 1=rush-down
    public float stealthPreference = 0f;
    public float greedForLoot    = 0.5f;
    public float panicHealThreshold = 0.3f; // HP fraction where player tends to heal
    public float cleanseUsageRate = 0.5f;

    // Last confirmed player position and time
    public Vector2Int lastKnownPosition;
    public float      lastSeenTick = -999f;
    public float      confidence   = 0f;   // 0-1, decays over time

    public bool IsConfident => confidence > 0.6f;

    public void Update(BattleUnit player)
    {
        if (player == null || !player.IsAlive) return;

        lastKnownPosition = player.gridPosition;
        lastSeenTick      = Time.time;
        confidence        = 1f;

        positionConfidence.Clear();
        positionConfidence[player.gridPosition] = 1f;

        // Update aggression estimate — if player is low HP but still advancing, high aggression
        float hpRatio = player.Data.currentHP / (float)player.Data.GetTotalStats().hpMax;
        if (hpRatio < panicHealThreshold)
            aggression = Mathf.Lerp(aggression, 0.2f, 0.3f); // likely to retreat/heal
        else
            aggression = Mathf.Lerp(aggression, 0.7f, 0.1f);
    }

    public void DecayConfidence(float deltaTime)
    {
        confidence = Mathf.Max(0f, confidence - deltaTime * 0.15f);
    }
}
