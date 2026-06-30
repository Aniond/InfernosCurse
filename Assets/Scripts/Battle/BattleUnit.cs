using UnityEngine;
using System.Collections.Generic;
using System;

public enum UnitState { Idle, Moving, Acting, Charging, Dead }
public enum FacingDir { South, East, North, West }

public class BattleUnit : MonoBehaviour
{
    [Header("Data")]
    public CombatantData Data;

    [Header("Runtime State")]
    public Vector2Int  gridPosition;
    public FacingDir   facing = FacingDir.South;
    public float       ct     = 0f;      // current charge time (0-100)
    public UnitState   state  = UnitState.Idle;
    public bool        IsPlayer;         // true = controlled by player

    // Queued action (charge-time abilities)
    public SkillDefinition  QueuedSkill;
    public BattleUnit       QueuedTarget;
    public Vector2Int       QueuedTargetPos;
    public int              ChargeTicksRemaining;

    public StatusEffectList Status { get; private set; } = new StatusEffectList();

    public int Elevation => BattleManager.Instance?.Grid?.GetCell(gridPosition)?.elevation ?? 0;

    public Vector2Int FacingDirection => facing switch
    {
        FacingDir.North => Vector2Int.up,
        FacingDir.South => Vector2Int.down,
        FacingDir.East  => Vector2Int.right,
        FacingDir.West  => Vector2Int.left,
        _               => Vector2Int.down,
    };

    public event Action<int, DamageType, bool> OnDamaged;   // amount, type, isCrit
    public event Action<int>                OnHealed;
    public event Action                     OnDied;
    public event Action<StatusEffectType>   OnStatusApplied;
    public event Action                     OnTurnStarted;

    // ── HP / MP ───────────────────────────────────────────────────────────────

    public bool IsAlive => state != UnitState.Dead;

    public void TakeDamage(int amount, DamageType type, BattleUnit source, bool isCrit = false)
    {
        if (!IsAlive) return;
        Data.currentHP = Mathf.Max(0, Data.currentHP - amount);
        OnDamaged?.Invoke(amount, type, isCrit);
        if (Data.currentHP <= 0) Die();
    }

    public void Heal(int amount)
    {
        if (!IsAlive) return;
        int maxHP      = Data.GetTotalStats().hpMax;
        Data.currentHP = Mathf.Min(maxHP, Data.currentHP + amount);
        OnHealed?.Invoke(amount);
    }

    public void SpendSP(int amount)
    {
        Data.currentSP = Mathf.Max(0, Data.currentSP - amount);
    }

    public bool HasSP(int cost) => Data.currentSP >= cost;

    // ── Turn logic ────────────────────────────────────────────────────────────

    public void TickCT(float amount)
    {
        if (!IsAlive) return;
        ct += amount;
    }

    public bool IsReady => ct >= 100f && IsAlive && state != UnitState.Charging;

    public void StartTurn()
    {
        Status.TickAll(this);
        OnTurnStarted?.Invoke();
    }

    public void EndTurn()
    {
        ct    = 0f;
        state = UnitState.Idle;
        SetFacingToward(QueuedTargetPos); // face last action target
    }

    // ── Facing ────────────────────────────────────────────────────────────────

    public void SetFacingToward(Vector2Int target)
    {
        Vector2Int diff = target - gridPosition;
        if (diff == Vector2Int.zero) return;

        if (Mathf.Abs(diff.x) >= Mathf.Abs(diff.y))
            facing = diff.x > 0 ? FacingDir.East : FacingDir.West;
        else
            facing = diff.y > 0 ? FacingDir.North : FacingDir.South;
    }

    // ── Actions ───────────────────────────────────────────────────────────────

    public void QueueAction(SkillDefinition skill, BattleUnit target, Vector2Int targetPos)
    {
        QueuedSkill          = skill;
        QueuedTarget         = target;
        QueuedTargetPos      = targetPos;
        ChargeTicksRemaining = BattleFormulas.ActionChargeTicks(this, skill);

        if (ChargeTicksRemaining > 0)
        {
            state = UnitState.Charging;
            ct    = 0f;
        }
    }

    // True once a charging unit has finished charging and is waiting for the
    // battle loop to resolve its queued action. Keeps resolution out of the
    // synchronous CT-ticking loop (which would corrupt turn-flow state).
    public bool ChargeComplete { get; private set; }

    public void TickCharge()
    {
        if (state != UnitState.Charging) return;
        ChargeTicksRemaining--;
        if (ChargeTicksRemaining <= 0)
        {
            state          = UnitState.Acting;
            ChargeComplete = true;   // battle loop will pick this up and resolve
        }
    }

    public void ClearChargeComplete() => ChargeComplete = false;

    // ── Status effects ────────────────────────────────────────────────────────

    public void ApplyStatus(StatusEffect effect)
    {
        Status.Apply(effect);
        OnStatusApplied?.Invoke(effect.type);
    }

    public void RemoveStatus(StatusEffectType type) => Status.Remove(type);

    // ── AP / XP (after battle) ────────────────────────────────────────────────

    public void AwardAP(int amount)
    {
        if (Data.activeJob == null) return;
        Data.activeJob.AddAP(amount);
    }

    public void AwardXP(int amount)
    {
        if (Data.activeJob == null) return;
        bool leveled = Data.activeJob.AddXP(amount);
        if (leveled)
            Debug.Log($"{Data.displayName} job leveled up! {Data.activeJob.job.jobName} Lv {Data.activeJob.jobLevel}");
    }

    // ── Absorb skill (Dante only) ─────────────────────────────────────────────

    public AbsorbedSkillInstance TryAbsorb(BattleUnit defeated)
    {
        if (Data.role != CombatantRole.Dante) return null;
        if (defeated.Data.learnableSkills == null || defeated.Data.learnableSkills.Length == 0)
            return null;

        if (UnityEngine.Random.value > defeated.Data.skillDropChance) return null;

        var skill    = defeated.Data.learnableSkills[UnityEngine.Random.Range(0, defeated.Data.learnableSkills.Length)];
        var instance = Data.Absorb(skill);
        Debug.Log($"[Absorb] Dante absorbed {instance.DisplayName()}");
        return instance;
    }

    // ── Death ─────────────────────────────────────────────────────────────────

    void Die()
    {
        state = UnitState.Dead;
        ct    = 0f;
        OnDied?.Invoke();
        BattleManager.Instance?.Grid?.RemoveUnit(this);
        gameObject.SetActive(false);
    }

    // ── Init ──────────────────────────────────────────────────────────────────

    public void Initialize(CombatantData data, bool isPlayer)
    {
        // Clone the asset so runtime HP/SP/state is per-unit, not shared across
        // every BattleUnit that references the same CombatantData asset.
        // Dante is the exception — his absorbed skills/jobs must persist on the
        // original asset between battles, so he keeps his reference.
        Data     = (data != null && data.role != CombatantRole.Dante)
                   ? Instantiate(data)
                   : data;
        IsPlayer = isPlayer;
        Data.InitRuntime();
        ct    = UnityEngine.Random.Range(0f, 40f); // stagger starting CT
        state = UnitState.Idle;
        Status = new StatusEffectList();
    }
}
