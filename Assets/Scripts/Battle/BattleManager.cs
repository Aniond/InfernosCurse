using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

public enum BattleState
{
    Inactive,
    BattleStart,
    TickCT,
    PlayerSelectMove,
    PlayerSelectAction,
    PlayerSelectTarget,
    ResolvingAction,
    EnemyTurn,
    BattleVictory,
    BattleDefeat,
}

public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance { get; private set; }

    [Header("References")]
    public BattleGrid Grid;
    public BattleCurseAutomata CurseAutomata;

    [Header("Combatant Prefab")]
    public GameObject battleUnitPrefab;

    // ── State ─────────────────────────────────────────────────────────────────
    public BattleState State { get; private set; } = BattleState.Inactive;

    private CTQueue             _ctQueue  = new CTQueue();
    private List<BattleUnit>    _allUnits = new List<BattleUnit>();
    private List<BattleUnit>    _players  = new List<BattleUnit>();
    private List<BattleUnit>    _enemies  = new List<BattleUnit>();
    private BattleUnit          _activeUnit;
    private InfernalWorldState  _worldState = new InfernalWorldState();

    // Player action selection state
    private List<GridCell>      _moveRange;
    private List<GridCell>      _attackRange;
    private SkillDefinition     _selectedSkill;
    private bool                _hasMoved;
    private bool                _hasActed;
    private Vector2Int          _preMovePosition;

    // Events — UI listens to these
    public event Action<BattleUnit>             OnTurnStart;
    public event Action<BattleState>            OnStateChanged;
    public event Action<List<GridCell>>         OnMoveRangeReady;
    public event Action<List<GridCell>>         OnAttackRangeReady;
    public event Action<BattleUnit, int, DamageType> OnUnitDamaged;
    public event Action<BattleUnit, int>        OnUnitHealed;
    public event Action<BattleUnit>             OnUnitDied;
    public event Action<BattleUnit, AbsorbedSkillInstance> OnSkillAbsorbed;
    public event Action                         OnVictory;
    public event Action                         OnDefeat;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ── Battle start ──────────────────────────────────────────────────────────

    public void StartBattle(List<CombatantData> playerParty, List<CombatantData> enemyParty,
                            List<Vector2Int> playerSpawns, List<Vector2Int> enemySpawns)
    {
        _allUnits.Clear(); _players.Clear(); _enemies.Clear();
        _ctQueue.Clear();

        Time.timeScale = 1f;

        for (int i = 0; i < playerParty.Count; i++)
            SpawnUnit(playerParty[i], playerSpawns[i], isPlayer: true);

        for (int i = 0; i < enemyParty.Count; i++)
            SpawnUnit(enemyParty[i], enemySpawns[i], isPlayer: false);

        // Seed curse density from hub map if available
        float hubCurse = HubMap.Instance != null
            ? HubMap.Instance.GlobalCurseLevel()
            : 0f;
        _worldState.ritualNodes.Clear();
        if (CurseAutomata != null)
            CurseAutomata.SeedFromHub(_worldState, hubCurse, _worldState.ritualNodes);

        SetState(BattleState.BattleStart);
        StartCoroutine(BattleLoop());
    }

    BattleUnit SpawnUnit(CombatantData data, Vector2Int pos, bool isPlayer)
    {
        var go   = Instantiate(battleUnitPrefab);
        var unit = go.GetComponent<BattleUnit>();
        unit.Initialize(data, isPlayer);

        Grid.PlaceUnit(unit, pos);
        _ctQueue.Register(unit);
        _allUnits.Add(unit);

        unit.OnDamaged += (dmg, type) => OnUnitDamaged?.Invoke(unit, dmg, type);
        unit.OnHealed  += (amt)       => OnUnitHealed?.Invoke(unit, amt);
        unit.OnDied    += ()          => HandleUnitDeath(unit);

        if (isPlayer) _players.Add(unit);
        else          _enemies.Add(unit);

        return unit;
    }

    // ── Core loop ─────────────────────────────────────────────────────────────

    IEnumerator BattleLoop()
    {
        yield return new WaitForSeconds(0.5f); // brief pause on battle start
        SetState(BattleState.TickCT);

        while (State != BattleState.BattleVictory && State != BattleState.BattleDefeat)
        {
            // Tick CT until a unit is ready
            var ready = _ctQueue.TickUntilReady();
            foreach (var unit in ready)
            {
                if (State == BattleState.BattleVictory || State == BattleState.BattleDefeat) break;
                yield return StartCoroutine(ProcessUnitTurn(unit));
            }
        }
    }

    IEnumerator ProcessUnitTurn(BattleUnit unit)
    {
        if (!unit.IsAlive) yield break;

        _activeUnit      = unit;
        _hasMoved        = false;
        _hasActed        = false;
        _preMovePosition = unit.gridPosition;
        unit.StartTurn();
        OnTurnStart?.Invoke(unit);

        if (unit.IsPlayer)
        {
            SetState(BattleState.PlayerSelectMove);
            ShowMoveRange(unit);

            // Wait for player to complete their turn
            while (!_hasActed || !_hasMoved)
            {
                // Player can end turn early with Wait
                if (State == BattleState.TickCT) break;
                yield return null;
            }
        }
        else
        {
            SetState(BattleState.EnemyTurn);
            yield return new WaitForSeconds(0.4f);

            var ai = unit.GetComponent<EnemyAI>();
            if (ai != null) ai.DecideAction(unit);
            else            EndUnitTurn(unit);

            // Wait for enemy action to resolve
            while (State == BattleState.EnemyTurn) yield return null;
        }

        unit.EndTurn();

        // Step curse automata between turns
        if (CurseAutomata != null)
            CurseAutomata.Step(_worldState);

        SetState(BattleState.TickCT);
    }

    // ── Player input API (called by UI) ───────────────────────────────────────

    public void PlayerSelectMoveTarget(Vector2Int pos)
    {
        if (State != BattleState.PlayerSelectMove) return;
        if (_activeUnit == null) return;

        Grid.MoveUnit(_activeUnit, pos);
        _activeUnit.SetFacingToward(pos);
        _hasMoved = true;
        SetState(BattleState.PlayerSelectAction);
        HideRangeHighlights();
    }

    public void PlayerSelectSkill(SkillDefinition skill)
    {
        if (State != BattleState.PlayerSelectAction) return;
        _selectedSkill = skill;
        SetState(BattleState.PlayerSelectTarget);
        ShowAttackRange(_activeUnit, skill);
    }

    public void PlayerSelectActionTarget(Vector2Int targetPos)
    {
        if (State != BattleState.PlayerSelectTarget) return;
        if (_selectedSkill == null) return;

        var target = Grid.GetCell(targetPos)?.occupant;
        _activeUnit.QueueAction(_selectedSkill, target, targetPos);
        SetState(BattleState.ResolvingAction);
        HideRangeHighlights();

        if (_activeUnit.ChargeTicksRemaining == 0)
            ResolveQueuedAction(_activeUnit);
    }

    public void PlayerWait()
    {
        if (_activeUnit == null) return;
        _hasMoved  = true;
        _hasActed  = true;
        HideRangeHighlights();
        SetState(BattleState.TickCT);
    }

    public void PlayerSkipMove()
    {
        if (State != BattleState.PlayerSelectMove) return;
        _hasMoved = true;
        SetState(BattleState.PlayerSelectAction);
        HideRangeHighlights();
    }

    // Return from action menu to move selection — only valid before acting
    public void PlayerUndoMove()
    {
        if (State != BattleState.PlayerSelectAction) return;
        if (_hasActed) return; // can't undo after acting

        // Snap unit back to turn-start position
        if (_activeUnit != null && _preMovePosition != _activeUnit.gridPosition)
        {
            Grid.MoveUnit(_activeUnit, _preMovePosition);
            _activeUnit.SetFacingToward(_preMovePosition);
        }

        _hasMoved = false;
        SetState(BattleState.PlayerSelectMove);
        ShowMoveRange(_activeUnit);
    }

    // ── Action resolution ─────────────────────────────────────────────────────

    public void ResolveQueuedAction(BattleUnit unit)
    {
        if (unit.QueuedSkill == null) { EndUnitTurn(unit); return; }

        AbilityResolver.Resolve(unit, unit.QueuedSkill, unit.QueuedTargetPos);
        unit.QueuedSkill   = null;
        unit.QueuedTarget  = null;
        _hasActed = true;

        SetState(BattleState.TickCT);
    }

    // ── End of turn / death ───────────────────────────────────────────────────

    public void EndUnitTurn(BattleUnit unit)
    {
        if (State == BattleState.EnemyTurn) SetState(BattleState.TickCT);
    }

    void HandleUnitDeath(BattleUnit unit)
    {
        _ctQueue.Unregister(unit);
        OnUnitDied?.Invoke(unit);

        if (!_enemies.Exists(u => u.IsAlive))
        {
            SetState(BattleState.BattleVictory);
            OnVictory?.Invoke();
            return;
        }

        if (!_players.Exists(u => u.IsAlive))
        {
            SetState(BattleState.BattleDefeat);
            OnDefeat?.Invoke();
        }
    }

    // ── Range display ─────────────────────────────────────────────────────────

    void ShowMoveRange(BattleUnit unit)
    {
        var stats  = unit.Data.GetTotalStats();
        int move   = Mathf.Max(2, stats.speed / 3);
        int jump   = Mathf.Max(1, stats.dexterity / 5);
        _moveRange = Grid.GetMoveRange(unit.gridPosition, move, jump);
        OnMoveRangeReady?.Invoke(_moveRange);
    }

    void ShowAttackRange(BattleUnit unit, SkillDefinition skill)
    {
        _attackRange = Grid.GetAttackRange(unit.gridPosition, 1, skill.range);
        OnAttackRangeReady?.Invoke(_attackRange);
    }

    void HideRangeHighlights()
    {
        OnMoveRangeReady?.Invoke(new List<GridCell>());
        OnAttackRangeReady?.Invoke(new List<GridCell>());
    }

    // ── Post-kill rewards ─────────────────────────────────────────────────────

    public void AwardPostKill(BattleUnit killer, BattleUnit defeated)
    {
        int ap = BattleFormulas.APFromKill(killer, defeated);
        int xp = BattleFormulas.XPFromKill(killer, defeated);

        foreach (var u in _players)
        {
            if (!u.IsAlive) continue;
            u.AwardAP(ap);
            u.AwardXP(xp);
        }
    }

    public void NotifyAbsorb(BattleUnit absorber, AbsorbedSkillInstance skill)
    {
        OnSkillAbsorbed?.Invoke(absorber, skill);
    }

    // ── CT order peek (for UI turn order sidebar) ─────────────────────────────

    public List<BattleUnit> GetTurnOrder(int count = 8) => _ctQueue.PeekOrder(count);

    // ── State machine ─────────────────────────────────────────────────────────

    void SetState(BattleState newState)
    {
        State = newState;
        OnStateChanged?.Invoke(newState);
    }

    public BattleUnit          ActiveUnit  => _activeUnit;
    public List<BattleUnit>    AllUnits    => _allUnits;
    public List<BattleUnit>    Players     => _players;
    public List<BattleUnit>    Enemies     => _enemies;
    public InfernalWorldState  WorldState  => _worldState;

    // Called by AbilityResolver when a Holy skill targets a tile
    public void CleanseTile(Vector2Int pos, float amount)
    {
        _worldState.SetSanctity(pos, Mathf.Min(1f, _worldState.GetSanctity(pos) + amount));
        CurseAutomata?.CleanseTile(pos, amount);
    }
}
