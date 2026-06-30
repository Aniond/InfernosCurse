using UnityEngine;
using System.Collections.Generic;

// Layered scalar fields across the battle grid.
// Shared per battle — NOT duplicated per agent.
// Each agent reads from this; only the world state updater writes to it.
public class InfluenceMap
{
    private BattleGrid       _grid;
    private InfernalWorldState _world;

    // Scored positions for common intents — rebuilt each turn
    public Dictionary<Vector2Int, float> ambushScore      = new();
    public Dictionary<Vector2Int, float> curseSpreadScore = new();
    public Dictionary<Vector2Int, float> flankScore       = new();
    public Dictionary<Vector2Int, float> retreatScore     = new();
    public Dictionary<Vector2Int, float> anchorDefScore   = new();

    public InfluenceMap(BattleGrid grid, InfernalWorldState world)
    {
        _grid  = grid;
        _world = world;
    }

    // ── Full rebuild — call once per turn start ────────────────────────────────

    public void Rebuild(List<BattleUnit> enemies, List<BattleUnit> players)
    {
        ambushScore.Clear();
        curseSpreadScore.Clear();
        flankScore.Clear();
        retreatScore.Clear();
        anchorDefScore.Clear();

        for (int x = 0; x < _grid.width; x++)
        {
            for (int y = 0; y < _grid.height; y++)
            {
                var pos  = new Vector2Int(x, y);
                var cell = _grid.GetCell(pos);
                if (cell == null || !cell.walkable) continue;

                float threat   = _world.GetThreat(pos);
                float curse    = _world.GetCurseDensity(pos);
                float sanctity = _world.GetSanctity(pos);
                float elev     = cell.elevation * 0.1f;

                // Ambush: low threat, adjacent to player movement corridor, high curse
                ambushScore[pos]      = (1f - threat) * 0.5f + curse * 0.3f + elev * 0.2f;

                // Curse spread: low sanctity, many empty neighbors for propagation
                int emptyNeighbors    = CountEmptyNeighbors(pos);
                curseSpreadScore[pos] = (1f - sanctity) * 0.6f + (emptyNeighbors / 4f) * 0.4f;

                // Flank: medium threat (near player but not directly targeted), elevation bonus
                flankScore[pos]       = threat * 0.3f + (1f - threat) * 0.4f + elev * 0.3f;

                // Retreat: low threat, high curse (safe for infernal units), high elevation
                retreatScore[pos]     = _world.GetRetreatSafety(pos) * 0.7f + elev * 0.3f;

                // Anchor defense: proximity to ritual nodes
                anchorDefScore[pos]   = ScoreAnchorProximity(pos);
            }
        }
    }

    // ── Best position queries ─────────────────────────────────────────────────

    public Vector2Int BestAmbushPosition(List<GridCell> reachable)
        => BestFrom(reachable, ambushScore);

    public Vector2Int BestFlankPosition(List<GridCell> reachable, BattleUnit target)
    {
        // Prefer cells that are to the side or behind the target
        var scored = new Dictionary<Vector2Int, float>();
        foreach (var cell in reachable)
        {
            float baseScore = flankScore.TryGetValue(cell.gridPos, out float s) ? s : 0f;
            float facing    = FacingBonus(cell.gridPos, target);
            scored[cell.gridPos] = baseScore + facing * 0.4f;
        }
        return BestFrom(reachable, scored);
    }

    public Vector2Int BestRetreatPosition(List<GridCell> reachable)
        => BestFrom(reachable, retreatScore);

    public Vector2Int BestCurseSpreadPosition(List<GridCell> reachable)
        => BestFrom(reachable, curseSpreadScore);

    public Vector2Int BestAnchorDefensePosition(List<GridCell> reachable)
        => BestFrom(reachable, anchorDefScore);

    // ── Utility intent scoring ────────────────────────────────────────────────

    // Score the "ambush now" intent for a specific unit
    public float ScoreAmbushIntent(BattleUnit unit, InfernalWorldState world)
    {
        if (!world.playerBelief.IsConfident) return 0.1f;
        float hpRatio    = unit.Data.currentHP / (float)unit.Data.GetTotalStats().hpMax;
        float localCurse = world.GetCurseDensity(unit.gridPosition);
        float threat     = world.GetThreat(unit.gridPosition);
        // Good ambush conditions: unit healthy, low threat on position, player overextended
        return hpRatio * 0.4f + localCurse * 0.3f + (1f - threat) * 0.3f;
    }

    public float ScoreSpreadCurseIntent(BattleUnit unit, InfernalWorldState world)
    {
        // Carriers score this highest; score rises when global curse is low (more room to spread)
        float globalRoom = 1f - world.globalCurseLevel;
        float localCurse = world.GetCurseDensity(unit.gridPosition);
        return globalRoom * 0.6f + (1f - localCurse) * 0.4f;
    }

    public float ScoreIsolatePlayerIntent(BattleUnit unit, InfernalWorldState world, List<BattleUnit> allies)
    {
        // Strong when multiple allies are alive and player is cornered (low retreat safety around player)
        int aliveAllies = 0;
        foreach (var a in allies) if (a.IsAlive) aliveAllies++;
        float allyStrength = Mathf.Clamp01(aliveAllies / 4f);
        float playerRetreat = world.GetRetreatSafety(world.playerBelief.lastKnownPosition);
        return allyStrength * 0.5f + (1f - playerRetreat) * 0.5f;
    }

    public float ScoreRetreatAndLureIntent(BattleUnit unit, InfernalWorldState world)
    {
        float hpRatio = unit.Data.currentHP / (float)unit.Data.GetTotalStats().hpMax;
        float threat  = world.GetThreat(unit.gridPosition);
        // Retreat when hurt and under threat, to pull player into worse terrain
        return (1f - hpRatio) * 0.5f + threat * 0.5f;
    }

    public float ScoreSupportAnchorIntent(BattleUnit unit, InfernalWorldState world)
    {
        // Anchors always score this high; others score it when a ritual node is threatened
        foreach (var node in world.ritualNodes)
        {
            if (!node.active) continue;
            int dist = BattleGrid.ManhattanDistance(unit.gridPosition, node.position);
            if (dist <= 3 && node.guardian == null)
                return 0.8f; // unguarded nearby ritual node — high priority
        }
        return 0.1f;
    }

    public float ScoreDirectAttackIntent(BattleUnit unit, InfernalWorldState world)
    {
        float hpRatio    = unit.Data.currentHP / (float)unit.Data.GetTotalStats().hpMax;
        float localCurse = world.GetCurseDensity(unit.gridPosition);
        // Direct attack is favorable when healthy and on cursed ground
        return hpRatio * 0.5f + localCurse * 0.3f + (world.playerBelief.IsConfident ? 0.2f : 0f);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    Vector2Int BestFrom(List<GridCell> reachable, Dictionary<Vector2Int, float> scores)
    {
        Vector2Int best      = reachable.Count > 0 ? reachable[0].gridPos : Vector2Int.zero;
        float      bestScore = float.MinValue;
        foreach (var cell in reachable)
        {
            float s = scores.TryGetValue(cell.gridPos, out float v) ? v : 0f;
            if (s > bestScore) { bestScore = s; best = cell.gridPos; }
        }
        return best;
    }

    float FacingBonus(Vector2Int attackerPos, BattleUnit target)
    {
        // Back attack = 1.0, side = 0.5, front = 0.0
        Vector2Int dir     = attackerPos - target.gridPosition;
        Vector2Int facing  = target.FacingDirection;
        int dot            = dir.x * facing.x + dir.y * facing.y;
        if (dot < 0)  return 1.0f;
        if (dot == 0) return 0.5f;
        return 0f;
    }

    int CountEmptyNeighbors(Vector2Int pos)
    {
        int count = 0;
        var dirs  = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        foreach (var d in dirs)
        {
            var cell = _grid.GetCell(pos + d);
            if (cell != null && cell.walkable && !cell.IsOccupied) count++;
        }
        return count;
    }

    float ScoreAnchorProximity(Vector2Int pos)
    {
        float score = 0f;
        foreach (var node in _world.ritualNodes)
        {
            if (!node.active) continue;
            int dist = BattleGrid.ManhattanDistance(pos, node.position);
            score   += Mathf.Max(0f, 1f - dist / 5f);
        }
        return Mathf.Clamp01(score);
    }
}
