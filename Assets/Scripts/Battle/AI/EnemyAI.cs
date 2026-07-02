using UnityEngine;
using System.Collections.Generic;

public enum InfernalArchetype { Generic, Carrier, Anchor, Interpreter }

// Intent tags — what the unit is trying to accomplish this turn
public enum AIIntent
{
    Ambush,         // move to flanking position, wait for player to overcommit
    SpreadCurse,    // move to low-sanctity tile, apply curse to ground
    IsolatePlayer,  // cut off player from allies or retreat paths
    RetreatAndLure, // fall back to cursed ground, draw player in
    SupportAnchor,  // move to guard an undefended ritual node
    DirectAttack,   // straightforward attack from advantage
    Wait,           // pass — conditions not favorable
}

public class EnemyAI : MonoBehaviour
{
    [Header("Archetype")]
    public InfernalArchetype archetype = InfernalArchetype.Generic;

    [Header("Utility Weights — tune per archetype")]
    [Range(0f,1f)] public float weightAmbush        = 0.5f;
    [Range(0f,1f)] public float weightSpreadCurse   = 0.3f;
    [Range(0f,1f)] public float weightIsolate       = 0.4f;
    [Range(0f,1f)] public float weightRetreat       = 0.4f;
    [Range(0f,1f)] public float weightSupportAnchor = 0.3f;
    [Range(0f,1f)] public float weightDirectAttack  = 0.5f;

    [Header("Stochasticity — prevents deterministic exploit loops")]
    [Range(0f,1f)] public float styleVariety = 0.2f; // noise added to intent scores

    // Shared world state and influence map — set by BattleManager on battle start
    public static InfernalWorldState WorldState { get; private set; }
    public static InfluenceMap       Influence  { get; private set; }

    public static void InitSharedState(InfernalWorldState world, InfluenceMap map)
    {
        WorldState = world;
        Influence  = map;
    }

    // ── Main entry point called by BattleManager ──────────────────────────────

    public virtual void DecideAction(BattleUnit unit)
    {
        var bm = BattleManager.Instance;
        if (bm == null)
        {
            Debug.LogWarning("[EnemyAI] No BattleManager — aborting AI turn.");
            return;
        }

        if (WorldState == null || Influence == null)
        {
            Debug.LogWarning("[EnemyAI] WorldState not initialized — passing turn.");
            bm.EndUnitTurn(unit);
            return;
        }

        // 1. Perception — update belief state from what this unit can see
        Perceive(unit);

        // 2. Rebuild influence maps for this turn
        var grid    = bm.Grid;
        var players = bm.Players;
        var allies  = bm.Enemies;
        WorldState.RefreshThreatMap(players, grid);
        WorldState.RefreshRetreatSafety(grid);
        Influence.Rebuild(allies, players);

        // 3. Score all intents
        AIIntent chosen = ScoreIntents(unit, allies);

        // 4. Execute chosen intent
        ExecuteIntent(unit, chosen, players, grid);
    }

    // ── Perception ────────────────────────────────────────────────────────────

    void Perceive(BattleUnit unit)
    {
        // Line-of-sight check to each player unit
        var players = BattleManager.Instance?.Players;
        if (players == null) return;

        foreach (var p in players)
        {
            if (!p.IsAlive) continue;
            int dist = BattleGrid.ManhattanDistance(unit.gridPosition, p.gridPosition);
            float visRange = unit.Data.GetTotalStats().perception / 2f;

            if (dist <= visRange)
                WorldState.playerBelief.Update(p);
        }

        WorldState.playerBelief.DecayConfidence(Time.deltaTime);
    }

    // ── Intent scoring (utility system) ───────────────────────────────────────

    AIIntent ScoreIntents(BattleUnit unit, List<BattleUnit> allies)
    {
        var scores = new Dictionary<AIIntent, float>
        {
            [AIIntent.Ambush]        = Influence.ScoreAmbushIntent(unit, WorldState)        * weightAmbush        + Noise(),
            [AIIntent.SpreadCurse]   = Influence.ScoreSpreadCurseIntent(unit, WorldState)   * weightSpreadCurse   + Noise(),
            [AIIntent.IsolatePlayer] = Influence.ScoreIsolatePlayerIntent(unit, WorldState, allies) * weightIsolate + Noise(),
            [AIIntent.RetreatAndLure]= Influence.ScoreRetreatAndLureIntent(unit, WorldState)* weightRetreat       + Noise(),
            [AIIntent.SupportAnchor] = Influence.ScoreSupportAnchorIntent(unit, WorldState) * weightSupportAnchor + Noise(),
            [AIIntent.DirectAttack]  = Influence.ScoreDirectAttackIntent(unit, WorldState)  * weightDirectAttack  + Noise(),
        };

        // Log intent scores for designer explainability
        LogIntentScores(unit, scores);

        return SoftArgmax(scores);
    }

    // Top-k stochastic selection — not always the single best, prevents exploit loops
    AIIntent SoftArgmax(Dictionary<AIIntent, float> scores)
    {
        // Sort descending
        var sorted = new List<KeyValuePair<AIIntent, float>>(scores);
        sorted.Sort((a, b) => b.Value.CompareTo(a.Value));

        // Top 2 candidates — pick between them weighted by score difference
        if (sorted.Count >= 2 && styleVariety > 0f)
        {
            float top  = sorted[0].Value;
            float next = sorted[1].Value;
            float gap  = top - next;
            // If gap is small and variety is high, sometimes pick second-best
            if (gap < styleVariety && Random.value < 0.35f)
                return sorted[1].Key;
        }

        return sorted.Count > 0 ? sorted[0].Key : AIIntent.Wait;
    }

    // ── Intent execution ──────────────────────────────────────────────────────

    void ExecuteIntent(BattleUnit unit, AIIntent intent, List<BattleUnit> players, BattleGrid grid)
    {
        var stats     = unit.Data.GetTotalStats();
        int movePoints = Mathf.Max(2, stats.speed / 3);
        int jumpHeight = Mathf.Max(1, stats.dexterity / 5);
        var moveRange  = grid.GetMoveRange(unit.gridPosition, movePoints, jumpHeight, unit);

        switch (intent)
        {
            case AIIntent.Ambush:
                ExecuteAmbush(unit, moveRange, players, grid);
                break;

            case AIIntent.SpreadCurse:
                ExecuteSpreadCurse(unit, moveRange, grid);
                break;

            case AIIntent.IsolatePlayer:
                ExecuteIsolate(unit, moveRange, players, grid);
                break;

            case AIIntent.RetreatAndLure:
                ExecuteRetreat(unit, moveRange, grid);
                break;

            case AIIntent.SupportAnchor:
                ExecuteSupportAnchor(unit, moveRange, grid);
                break;

            case AIIntent.DirectAttack:
                ExecuteDirectAttack(unit, moveRange, players, grid);
                break;

            case AIIntent.Wait:
            default:
                Debug.Log($"[EnemyAI] {unit.Data.displayName} waits.");
                BattleManager.Instance?.EndUnitTurn(unit);
                break;
        }
    }

    // ── Ambush: move to flanking position, attack if in range ─────────────────

    void ExecuteAmbush(BattleUnit unit, List<GridCell> moveRange, List<BattleUnit> players, BattleGrid grid)
    {
        var target = NearestPlayer(unit, players);
        if (target == null) { FallbackAttack(unit, moveRange, players, grid); return; }

        Vector2Int dest = Influence.BestFlankPosition(moveRange, target);
        grid.MoveUnitAnimated(unit, dest);
        unit.SetFacingToward(target.gridPosition);

        // Attack if now in range of any skill
        TryAttack(unit, players, grid);
    }

    // ── Spread curse: move to optimal spread tile, apply curse ────────────────

    void ExecuteSpreadCurse(BattleUnit unit, List<GridCell> moveRange, BattleGrid grid)
    {
        Vector2Int dest = Influence.BestCurseSpreadPosition(moveRange);
        grid.MoveUnitAnimated(unit, dest);

        // Carriers route their spread through the automata so the overlay updates
        // and tiles corrupt. Other archetypes just seed the world map directly.
        var automata = BattleCurseAutomata.Instance;
        if (archetype == InfernalArchetype.Carrier && automata != null)
        {
            automata.SpreadFromUnit(dest, 0.3f);
        }
        else
        {
            ApplyCurseToTile(dest, 0.3f);
            foreach (var dir in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                ApplyCurseToTile(dest + dir, 0.15f);
        }

        Debug.Log($"[EnemyAI] {unit.Data.displayName} spreads curse at {dest}.");
        BattleManager.Instance?.EndUnitTurn(unit);
    }

    // ── Isolate: move to cut off player's retreat paths ───────────────────────

    void ExecuteIsolate(BattleUnit unit, List<GridCell> moveRange, List<BattleUnit> players, BattleGrid grid)
    {
        var target = NearestPlayer(unit, players);
        if (target == null) { FallbackAttack(unit, moveRange, players, grid); return; }

        // Move to the cell that most reduces target's retreat safety
        Vector2Int dest = BestIsolationCell(moveRange, target, grid);
        grid.MoveUnitAnimated(unit, dest);
        unit.SetFacingToward(target.gridPosition);
        TryAttack(unit, players, grid);
    }

    // ── Retreat and lure: fall back to high-curse safe ground ─────────────────

    void ExecuteRetreat(BattleUnit unit, List<GridCell> moveRange, BattleGrid grid)
    {
        Vector2Int dest = Influence.BestRetreatPosition(moveRange);
        grid.MoveUnitAnimated(unit, dest);
        Debug.Log($"[EnemyAI] {unit.Data.displayName} retreats to {dest}.");
        BattleManager.Instance?.EndUnitTurn(unit);
    }

    // ── Support anchor: move to guard undefended ritual node ──────────────────

    void ExecuteSupportAnchor(BattleUnit unit, List<GridCell> moveRange, BattleGrid grid)
    {
        Vector2Int dest = Influence.BestAnchorDefensePosition(moveRange);
        grid.MoveUnitAnimated(unit, dest);

        // Mark this unit as guardian of nearest node
        foreach (var node in WorldState.ritualNodes)
        {
            if (node.active && node.guardian == null &&
                BattleGrid.ManhattanDistance(dest, node.position) <= 2)
            {
                node.guardian = unit;
                break;
            }
        }

        Debug.Log($"[EnemyAI] {unit.Data.displayName} guards ritual node.");
        BattleManager.Instance?.EndUnitTurn(unit);
    }

    // ── Direct attack: move adjacent and hit ─────────────────────────────────

    void ExecuteDirectAttack(BattleUnit unit, List<GridCell> moveRange, List<BattleUnit> players, BattleGrid grid)
    {
        FallbackAttack(unit, moveRange, players, grid);
    }

    // ── Shared attack logic ───────────────────────────────────────────────────

    void TryAttack(BattleUnit unit, List<BattleUnit> players, BattleGrid grid)
    {
        // Pick best skill from equipped actives
        var skill = PickBestSkill(unit);
        if (skill == null) { BattleManager.Instance?.EndUnitTurn(unit); return; }

        // Find target in range — honor the skill's own min range and LoS
        var attackRange = grid.GetAttackRange(unit.gridPosition, skill.minRange, skill.range,
                                              skill.requiresLineOfSight, unit.Elevation);
        foreach (var cell in attackRange)
        {
            if (cell.occupant == null || cell.occupant.IsPlayer == false) continue;
            if (!cell.occupant.IsAlive) continue;

            unit.QueueAction(skill, cell.occupant, cell.gridPos);

            // Charged skills charge for enemies too — resolve only instant ones.
            if (unit.ChargeTicksRemaining > 0)
                BattleManager.Instance?.EndUnitTurn(unit);
            else
                BattleManager.Instance?.ResolveQueuedAction(unit);
            return;
        }

        BattleManager.Instance?.EndUnitTurn(unit);
    }

    void FallbackAttack(BattleUnit unit, List<GridCell> moveRange, List<BattleUnit> players, BattleGrid grid)
    {
        var target = NearestPlayer(unit, players);
        if (target == null) { BattleManager.Instance?.EndUnitTurn(unit); return; }

        var skill = PickBestSkill(unit);
        if (skill == null) { BattleManager.Instance?.EndUnitTurn(unit); return; }

        // Move as close as possible
        Vector2Int dest = ClosestReachableToTarget(moveRange, target.gridPosition);
        grid.MoveUnitAnimated(unit, dest);
        unit.SetFacingToward(target.gridPosition);

        TryAttack(unit, players, grid);
    }

    SkillDefinition PickBestSkill(BattleUnit unit)
    {
        // Pick highest base power OFFENSIVE skill the unit has SP for.
        // Healing skills are excluded — casting one at a player would heal them.
        SkillDefinition best    = null;
        int             bestPow = -1;
        foreach (var skill in unit.Data.equippedSkills.actives)
        {
            if (skill == null) continue;
            if (skill.skillType != SkillType.Active) continue;
            if (skill.isHealing) continue;
            if (!unit.HasSP(skill.spCost)) continue;
            if (skill.basePower > bestPow) { bestPow = skill.basePower; best = skill; }
        }
        return best;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    BattleUnit NearestPlayer(BattleUnit unit, List<BattleUnit> players)
    {
        BattleUnit nearest = null;
        int        minDist = int.MaxValue;
        foreach (var p in players)
        {
            if (!p.IsAlive) continue;
            int d = BattleGrid.ManhattanDistance(unit.gridPosition, p.gridPosition);
            if (d < minDist) { minDist = d; nearest = p; }
        }
        return nearest;
    }

    Vector2Int ClosestReachableToTarget(List<GridCell> reachable, Vector2Int target)
    {
        Vector2Int best    = reachable.Count > 0 ? reachable[0].gridPos : Vector2Int.zero;
        int        minDist = int.MaxValue;
        foreach (var cell in reachable)
        {
            int d = BattleGrid.ManhattanDistance(cell.gridPos, target);
            if (d < minDist) { minDist = d; best = cell.gridPos; }
        }
        return best;
    }

    Vector2Int BestIsolationCell(List<GridCell> reachable, BattleUnit target, BattleGrid grid)
    {
        // Pick cell that blocks the most retreat options for the target
        Vector2Int best      = reachable.Count > 0 ? reachable[0].gridPos : Vector2Int.zero;
        int        minEscape = int.MaxValue;
        foreach (var cell in reachable)
        {
            // Simulate placing this unit here, count target's remaining escape routes
            int escapes = CountEscapeRoutes(target.gridPosition, cell.gridPos, grid);
            if (escapes < minEscape) { minEscape = escapes; best = cell.gridPos; }
        }
        return best;
    }

    int CountEscapeRoutes(Vector2Int targetPos, Vector2Int blockerPos, BattleGrid grid)
    {
        int count = 0;
        foreach (var dir in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
        {
            var cell = grid.GetCell(targetPos + dir);
            if (cell == null || !cell.walkable) continue;
            if (targetPos + dir == blockerPos) continue;
            if (cell.IsOccupied) continue;
            count++;
        }
        return count;
    }

    void ApplyCurseToTile(Vector2Int pos, float amount)
    {
        float current = WorldState.GetCurseDensity(pos);
        WorldState.SetCurseDensity(pos, Mathf.Min(1f, current + amount));
    }

    float Noise() => Random.Range(-styleVariety * 0.5f, styleVariety * 0.5f);

    void LogIntentScores(BattleUnit unit, Dictionary<AIIntent, float> scores)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append($"[EnemyAI] {unit.Data.displayName} intent scores: ");
        foreach (var kv in scores)
            sb.Append($"{kv.Key}={kv.Value:F2} ");
        Debug.Log(sb.ToString());
    }
}

// ── Archetype subclasses — weight profiles only ───────────────────────────────

public class CarrierAI : EnemyAI
{
    void Awake()
    {
        archetype          = InfernalArchetype.Carrier;
        weightSpreadCurse  = 0.9f;
        weightAmbush       = 0.3f;
        weightIsolate      = 0.4f;
        weightRetreat      = 0.6f;  // carriers preserve themselves
        weightSupportAnchor= 0.2f;
        weightDirectAttack = 0.2f;
        styleVariety       = 0.15f;
    }
}

public class AnchorAI : EnemyAI
{
    void Awake()
    {
        archetype          = InfernalArchetype.Anchor;
        weightSpreadCurse  = 0.4f;
        weightAmbush       = 0.2f;
        weightIsolate      = 0.3f;
        weightRetreat      = 0.2f;  // anchors hold ground
        weightSupportAnchor= 0.95f;
        weightDirectAttack = 0.6f;
        styleVariety       = 0.1f;  // anchors are predictable — they don't move much
    }
}

public class InterpreterAI : EnemyAI
{
    void Awake()
    {
        archetype          = InfernalArchetype.Interpreter;
        weightSpreadCurse  = 0.3f;
        weightAmbush       = 0.7f;
        weightIsolate      = 0.9f;  // interpreters cut off and coordinate
        weightRetreat      = 0.7f;
        weightSupportAnchor= 0.5f;
        weightDirectAttack = 0.3f;
        styleVariety       = 0.25f; // most unpredictable archetype
    }

    // Interpreters get one extra ability: they boost nearby allies' intent scores
    public override void DecideAction(BattleUnit unit)
    {
        CoordinateAllies(unit);
        base.DecideAction(unit);
    }

    void CoordinateAllies(BattleUnit interpreter)
    {
        var allies = BattleManager.Instance?.Enemies;
        if (allies == null) return;

        foreach (var ally in allies)
        {
            if (ally == interpreter || !ally.IsAlive) continue;
            int dist = BattleGrid.ManhattanDistance(interpreter.gridPosition, ally.gridPosition);
            if (dist > 4) continue;

            // Apply Inspired status to nearby allies — boosts Creativity, increases AP gain
            ally.ApplyStatus(new StatusEffect(StatusEffectType.Inspired, 2, 0.2f, interpreter));
        }
    }
}
