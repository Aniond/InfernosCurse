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

    [Header("Fallback Attack")]
    [Tooltip("Basic weapon attack granted to any unit with a free active slot — " +
             "guarantees every unit can always act (FFT's Attack command).")]
    public SkillDefinition basicAttackSkill;

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
    private HashSet<Vector2Int> _validMoves   = new HashSet<Vector2Int>();  // authoritative — UI can't teleport units
    private HashSet<Vector2Int> _validTargets = new HashSet<Vector2Int>();
    private SkillDefinition     _selectedSkill;
    private bool                _hasMoved;
    private bool                _hasActed;
    private bool                _turnComplete;   // set when the active player turn should end
    private Vector2Int          _preMovePosition;

    // Events — UI listens to these
    public event Action<BattleUnit>             OnTurnStart;
    public event Action<BattleState>            OnStateChanged;
    public event Action<List<GridCell>>         OnMoveRangeReady;
    public event Action<List<GridCell>>         OnAttackRangeReady;
    public event Action<BattleUnit, int, DamageType, bool> OnUnitDamaged;  // unit, amount, type, isCrit
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
        if (playerParty == null || enemyParty == null || playerSpawns == null || enemySpawns == null)
        {
            Debug.LogError("[BattleManager] StartBattle called with a null list.");
            return;
        }
        if (playerSpawns.Count < playerParty.Count || enemySpawns.Count < enemyParty.Count)
        {
            Debug.LogError($"[BattleManager] Not enough spawn points — players {playerParty.Count}/{playerSpawns.Count}, enemies {enemyParty.Count}/{enemySpawns.Count}.");
            return;
        }

        _allUnits.Clear(); _players.Clear(); _enemies.Clear();
        _ctQueue.Clear();

        Time.timeScale = 1f;

        for (int i = 0; i < playerParty.Count; i++)
            SpawnUnit(playerParty[i], playerSpawns[i], isPlayer: true);

        for (int i = 0; i < enemyParty.Count; i++)
            SpawnUnit(enemyParty[i], enemySpawns[i], isPlayer: false);

        // Seed curse density from the district the battle belongs to (road
        // encounters point the tracker at their waypoint) — the grid's
        // corruption reflects WHERE you fight.
        float hubCurse = HubMap.Instance != null
            ? HubMap.Instance.GetBattleSeedCurse(DistrictTracker.CurrentNodeId)
            : 0f;
        _worldState.ritualNodes.Clear();
        if (CurseAutomata != null)
            CurseAutomata.SeedFromHub(_worldState, hubCurse, _worldState.ritualNodes);

        // Hand the enemy AI its shared belief state + influence map. Without this
        // every EnemyAI hits its null-guard and passes the turn forever.
        EnemyAI.InitSharedState(_worldState, new InfluenceMap(Grid, _worldState));

        SetState(BattleState.BattleStart);
        StartCoroutine(BattleLoop());
    }

    BattleUnit SpawnUnit(CombatantData data, Vector2Int pos, bool isPlayer)
    {
        var go   = Instantiate(battleUnitPrefab);
        var unit = go.GetComponent<BattleUnit>();
        unit.Initialize(data, isPlayer);
        EnsureBattleSkills(unit);

        Grid.PlaceUnit(unit, pos);
        _ctQueue.Register(unit);
        _allUnits.Add(unit);

        unit.OnDamaged += (dmg, type, crit) => OnUnitDamaged?.Invoke(unit, dmg, type, crit);
        unit.OnHealed  += (amt)             => OnUnitHealed?.Invoke(unit, amt);
        unit.OnDied    += ()                => HandleUnitDeath(unit);

        if (isPlayer) _players.Add(unit);
        else          _enemies.Add(unit);

        return unit;
    }

    // Guarantees a unit enters battle with something to do:
    // - empty active slots are auto-filled from the equipped job's UNLOCKED
    //   Active skills (the loadout screen doesn't exist yet — this is the
    //   battle-side bridge for the job → equippedSkills gap);
    // - a basic Attack goes in the first remaining free slot so no unit is
    //   ever reduced to Wait-only. Hand-authored loadouts are never overwritten.
    void EnsureBattleSkills(BattleUnit unit)
    {
        var slots = unit.Data.equippedSkills.actives;

        var job = unit.Data.activeJob;
        if (job != null && job.learnedSkills != null)
        {
            foreach (var learned in job.learnedSkills)
            {
                if (learned == null || !learned.unlocked || learned.skill == null) continue;
                if (learned.skill.skillType != SkillType.Active) continue;
                if (System.Array.IndexOf(slots, learned.skill) >= 0) continue;

                int free = System.Array.IndexOf(slots, null);
                if (free < 0) break;
                slots[free] = learned.skill;
            }
        }

        if (basicAttackSkill != null && System.Array.IndexOf(slots, basicAttackSkill) < 0)
        {
            int free = System.Array.IndexOf(slots, null);
            if (free >= 0) slots[free] = basicAttackSkill;
        }
    }

    // ── Core loop ─────────────────────────────────────────────────────────────

    IEnumerator BattleLoop()
    {
        yield return new WaitForSeconds(0.5f); // brief pause on battle start
        SetState(BattleState.TickCT);

        while (!IsBattleOver())
        {
            // Tick CT until a unit is ready
            var ready = _ctQueue.TickUntilReady();
            if (ready.Count == 0) { yield return null; continue; }  // stall guard

            foreach (var unit in ready)
            {
                if (IsBattleOver()) break;
                if (!unit.IsAlive) continue;

                // A unit that just finished charging resolves its queued action
                // here (in the coroutine, not mid-CT-tick) instead of taking a turn.
                if (unit.ChargeComplete)
                {
                    unit.ClearChargeComplete();
                    ResolveQueuedAction(unit);
                    unit.EndTurn();
                    if (CurseAutomata != null) CurseAutomata.Step(_worldState);
                    continue;
                }

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
        _turnComplete    = false;
        _preMovePosition = unit.gridPosition;
        unit.StartTurn();

        // StartTurn ticks status effects which can kill the unit (DoT).
        // Bail out before showing ranges / running AI on a corpse.
        if (!unit.IsAlive || IsBattleOver()) yield break;

        OnTurnStart?.Invoke(unit);

        // Stop/Frozen: the turn is lost. (Durations already ticked in StartTurn,
        // so the status runs out even while the unit is locked down.)
        if (unit.Status.PreventsAction)
        {
            Debug.Log($"{unit.Data.displayName} can't act (stopped/frozen).");
            yield return new WaitForSeconds(0.35f);
            unit.EndTurn();
            if (CurseAutomata != null) CurseAutomata.Step(_worldState);
            if (!IsBattleOver()) SetState(BattleState.TickCT);
            yield break;
        }

        if (unit.IsPlayer)
        {
            SetState(BattleState.PlayerSelectMove);
            ShowMoveRange(unit);

            // Wait for the player to finish their turn (Wait, or move+act).
            while (!_turnComplete && !IsBattleOver())
                yield return null;
        }
        else
        {
            SetState(BattleState.EnemyTurn);
            yield return new WaitForSeconds(0.4f);

            var ai = unit.GetComponent<EnemyAI>();
            if (ai != null) ai.DecideAction(unit);
            else            EndUnitTurn(unit);

            // Wait for enemy action to resolve (or battle to end).
            while (State == BattleState.EnemyTurn && !IsBattleOver())
                yield return null;
        }

        if (unit.IsAlive) unit.EndTurn();

        // Step curse automata between turns
        if (CurseAutomata != null)
            CurseAutomata.Step(_worldState);

        if (!IsBattleOver())
            SetState(BattleState.TickCT);
    }

    bool IsBattleOver() =>
        State == BattleState.BattleVictory || State == BattleState.BattleDefeat;

    // ── Player input API (called by UI) ───────────────────────────────────────

    public void PlayerSelectMoveTarget(Vector2Int pos)
    {
        if (State != BattleState.PlayerSelectMove) return;
        if (_activeUnit == null) return;
        if (!_validMoves.Contains(pos)) return;   // authoritative range check

        Grid.MoveUnitAnimated(_activeUnit, pos);
        _activeUnit.SetFacingToward(pos);
        _hasMoved = true;
        HideRangeHighlights();

        // FFT order freedom: if the unit already acted, moving finishes the turn.
        if (_hasActed) _turnComplete = true;
        else           SetState(BattleState.PlayerSelectAction);
    }

    public void PlayerSelectSkill(SkillDefinition skill)
    {
        if (State != BattleState.PlayerSelectAction) return;
        if (_hasActed) return;                    // one action per turn
        _selectedSkill = skill;
        SetState(BattleState.PlayerSelectTarget);
        ShowAttackRange(_activeUnit, skill);
    }

    public void PlayerSelectActionTarget(Vector2Int targetPos)
    {
        if (State != BattleState.PlayerSelectTarget) return;
        if (_selectedSkill == null) return;
        if (!_validTargets.Contains(targetPos)) return;   // authoritative range check

        var target = Grid.GetCell(targetPos)?.occupant;
        _activeUnit.QueueAction(_selectedSkill, target, targetPos);
        SetState(BattleState.ResolvingAction);
        HideRangeHighlights();

        if (_activeUnit.ChargeTicksRemaining == 0)
        {
            ResolveQueuedAction(_activeUnit);
        }
        else
        {
            // Charged skill — unit is now Charging and will resolve on a future
            // CT tick via TickCharge. Charging locks the unit: turn ends now.
            _hasActed     = true;
            _turnComplete = true;
        }
    }

    // Cancel out of target selection — back to the action menu.
    public void PlayerCancelTarget()
    {
        if (State != BattleState.PlayerSelectTarget) return;
        _selectedSkill = null;
        HideRangeHighlights();
        SetState(BattleState.PlayerSelectAction);
    }

    public void PlayerWait()
    {
        if (_activeUnit == null) return;
        _hasMoved     = true;
        _hasActed     = true;
        _turnComplete = true;          // coroutine ends the turn and advances CT
        HideRangeHighlights();
    }

    public void PlayerSkipMove()
    {
        if (State != BattleState.PlayerSelectMove) return;
        _hasMoved = true;
        HideRangeHighlights();

        // Skipping the post-act move ends the turn; pre-act it opens the menu.
        if (_hasActed) _turnComplete = true;
        else           SetState(BattleState.PlayerSelectAction);
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
        unit.QueuedSkill  = null;
        unit.QueuedTarget = null;
        _hasActed         = true;

        if (unit.IsPlayer)
        {
            // FFT order freedom: acting first still allows the move afterward.
            // (Only for the live active player turn — a charge-resolution for a
            // player long past their turn just completes.)
            if (unit == _activeUnit && !_turnComplete && !_hasMoved && unit.IsAlive && !IsBattleOver())
            {
                SetState(BattleState.PlayerSelectMove);
                ShowMoveRange(unit);
            }
            else
            {
                _turnComplete = true;
            }
        }
        else
        {
            EndUnitTurn(unit);
        }
    }

    // ── End of turn / death ───────────────────────────────────────────────────

    public void EndUnitTurn(BattleUnit unit)
    {
        // Only advance if we're still in a live turn state. If a kill already
        // flipped us to Victory/Defeat, leave it — the loop will exit.
        if (IsBattleOver()) return;
        if (State == BattleState.EnemyTurn || State == BattleState.ResolvingAction)
            SetState(BattleState.TickCT);
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
        _moveRange = Grid.GetMoveRange(unit.gridPosition, move, jump, unit);

        _validMoves.Clear();
        foreach (var c in _moveRange) _validMoves.Add(c.gridPos);

        OnMoveRangeReady?.Invoke(_moveRange);
    }

    void ShowAttackRange(BattleUnit unit, SkillDefinition skill)
    {
        _attackRange = Grid.GetAttackRange(unit.gridPosition, skill.minRange, skill.range,
                                           skill.requiresLineOfSight, unit.Elevation);

        _validTargets.Clear();
        foreach (var c in _attackRange) _validTargets.Add(c.gridPos);

        OnAttackRangeReady?.Invoke(_attackRange);
    }

    void HideRangeHighlights()
    {
        OnMoveRangeReady?.Invoke(new List<GridCell>());
        OnAttackRangeReady?.Invoke(new List<GridCell>());
    }

    // Skill the player is currently aiming (null outside PlayerSelectTarget).
    // Used by the cursor's AOE preview and the forecast panel.
    public SkillDefinition SelectedSkill => State == BattleState.PlayerSelectTarget ? _selectedSkill : null;

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

        // Loot + guild standing. A battle belongs to the district the party
        // was last in (DistrictTracker); fighting in a guild's home turf earns
        // its gratitude. Null-guarded — test scenes may lack the systems.
        FlorinWallet.Add(BattleFormulas.FlorinsFromKill(killer, defeated), "kill");
        var guilds = GuildSystem.Instance;
        var homeGuild = guilds != null ? guilds.GuildForHomeNode(DistrictTracker.CurrentNodeId) : null;
        if (homeGuild != null)
            guilds.AwardRep(homeGuild.guildId, homeGuild.repPerHomeKill,
                            $"kill in {DistrictTracker.CurrentNodeId}");
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

    // Turn progress — UI uses these to show/hide the move-undo button etc.
    public bool HasMoved => _hasMoved;
    public bool HasActed => _hasActed;

    // Called by AbilityResolver when a Holy skill targets a tile
    public void CleanseTile(Vector2Int pos, float amount)
    {
        _worldState.SetSanctity(pos, Mathf.Min(1f, _worldState.GetSanctity(pos) + amount));
        CurseAutomata?.CleanseTile(pos, amount);
    }
}
