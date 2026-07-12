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
    [Tooltip("Eye height override when no CombatantData sheet is assigned; the sheet's vision block wins otherwise.")]
    public int         eyeHeight = 2;

    // Vision comes from the character SHEET (CombatantData — player, party,
    // enemy and NPC alike); the field above is only a sheetless fallback.
    public int   EyeHeight  => Data != null ? Data.eyeHeight : eyeHeight;
    public float SightRange => Data != null ? Data.sightRange : 13f;
    public Vector2Int  gridPosition;
    // Pre-battle ambusher staged in an explore zone: never rendered and never
    // fog-revealed — being jumped is the first time the player sees it.
    // ZoneEncounterTrigger flags it; ZoneFogOfWar skips it.
    [System.NonSerialized] public bool stagedAmbusher;
    public FacingDir   facing = FacingDir.South;
    public float       ct     = 0f;      // current charge time (0-100)
    public UnitState   state  = UnitState.Idle;
    public bool        IsPlayer;         // true = controlled by player

    // Queued action (charge-time abilities)
    public SkillDefinition       QueuedSkill;
    public BattleUnit            QueuedTarget;
    public Vector2Int            QueuedTargetPos;
    public int                   ChargeTicksRemaining;
    // Set only when QueuedSkill came from an absorbed slot — carries the
    // level/refine-scaled power override into AbilityResolver at resolve time.
    public AbsorbedSkillInstance QueuedAbsorbedInstance;

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

    public CharacterStats GetEffectiveStats()
    {
        CharacterStats stats = Data.GetTotalStats();
        PlayerInsanityModifierSet insanity = PlayerInsanityModifiers.For(Data);
        stats.perception = Mathf.Max(0, stats.perception - insanity.PerceptionPenalty);
        stats.faith = Mathf.Max(
            0,
            stats.faith - Mathf.RoundToInt(Status.CombinedFaithPenalty()) - insanity.FaithPenalty);
        return stats;
    }

    public void TakeDamage(int amount, DamageType type, BattleUnit source, bool isCrit = false)
    {
        if (!IsAlive) return;
        Data.currentHP = Mathf.Max(0, Data.currentHP - amount);

        // Striking from the fog gives the attacker's position away — the fog
        // parts over it briefly (fog-honesty: you saw where the hit came from).
        if (source != null && source != this)
        {
            var fow = UnityEngine.Object.FindFirstObjectByType<ZoneFogOfWar>();
            if (fow != null) fow.RevealCell(source.gridPosition, 5f);
        }

        OnDamaged?.Invoke(amount, type, isCrit);
        if (Data.currentHP <= 0) Die();
        else PlayHurtAnimation();
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
        Status.EndTurn();
        ct = 0f;
        // A unit that just queued a charged action must KEEP its Charging state —
        // stomping it to Idle here silently killed every charged skill (the CT
        // queue only ticks charge on units in Charging state).
        if (state != UnitState.Charging)
            state = UnitState.Idle;
        SetFacingToward(QueuedTargetPos); // face last action target
    }

    // ── Walk animation (visual only — grid data is already authoritative) ─────

    private Coroutine _walkRoutine;
    private Coroutine _skillRoutine;
    private Coroutine _reactionRoutine;
    private SpriteRenderer _spriteRenderer;

    public void AnimateWalk(System.Collections.Generic.List<Vector2Int> path, BattleGrid grid)
    {
        if (_walkRoutine != null) StopCoroutine(_walkRoutine);
        _walkRoutine = StartCoroutine(WalkRoutine(path, grid));
    }

    System.Collections.IEnumerator WalkRoutine(System.Collections.Generic.List<Vector2Int> path, BattleGrid grid)
    {
        const float speed = 6f; // world units per second
        _spriteRenderer ??= GetComponentInChildren<SpriteRenderer>();
        float frameClock = 0f;

        // PlaceUnit already snapped us to the destination — pull back to the
        // path start before the first rendered frame (no yield yet = no flicker).
        var startCell = grid.GetCell(path[0]);
        transform.position = grid.GridToWorld(path[0], startCell?.elevation ?? 0);

        for (int i = 0; i < path.Count; i++)
        {
            var cell = grid.GetCell(path[i]);
            Vector3 target = grid.GridToWorld(path[i], cell?.elevation ?? 0);

            Sprite[] walkFrames = null;
            if (i > 0)
            {
                // Grid occupancy already points at the final destination, so
                // derive visual facing from this path segment, not gridPosition.
                SetFacingFromDelta(path[i] - path[i - 1], applyIdleSprite: false);
                walkFrames = Data?.GetBattleWalkFrames(facing);
                if (_spriteRenderer != null && walkFrames != null && walkFrames.Length > 0)
                    _spriteRenderer.sprite = walkFrames[0];
            }

            while (Vector3.Distance(transform.position, target) > 0.01f)
            {
                if (_spriteRenderer != null && walkFrames != null && walkFrames.Length > 0)
                {
                    frameClock += Time.deltaTime;
                    float fps = Data != null ? Mathf.Max(1f, Data.GetBattleWalkFps()) : 10f;
                    int frame = Mathf.FloorToInt(frameClock * fps) % walkFrames.Length;
                    if (walkFrames[frame] != null) _spriteRenderer.sprite = walkFrames[frame];
                }
                transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
                yield return null;
            }
        }
        ApplyIdleSprite();
        _walkRoutine = null;
    }

    // ── Facing ────────────────────────────────────────────────────────────────

    public void SetFacingToward(Vector2Int target)
    {
        Vector2Int diff = target - gridPosition;
        if (diff == Vector2Int.zero) return;

        SetFacingFromDelta(diff, applyIdleSprite: true);
    }

    void SetFacingFromDelta(Vector2Int diff, bool applyIdleSprite)
    {
        if (Mathf.Abs(diff.x) >= Mathf.Abs(diff.y))
            facing = diff.x > 0 ? FacingDir.East : FacingDir.West;
        else
            facing = diff.y > 0 ? FacingDir.North : FacingDir.South;

        if (applyIdleSprite) ApplyIdleSprite();
    }

    void ApplyIdleSprite()
    {
        _spriteRenderer ??= GetComponentInChildren<SpriteRenderer>();
        var idle = Data?.GetBattleIdleSprite(facing);
        if (_spriteRenderer != null && idle != null) _spriteRenderer.sprite = idle;
    }

    public void PlaySkillAnimation(SkillDefinition skill)
    {
        var frames = Data?.GetBattleSkillFrames(skill, facing);
        if (frames == null || frames.Length == 0) return;

        if (_skillRoutine != null) StopCoroutine(_skillRoutine);
        _skillRoutine = StartCoroutine(SkillAnimationRoutine(skill));
    }

    System.Collections.IEnumerator SkillAnimationRoutine(SkillDefinition skill)
    {
        // AI movement updates the grid immediately while the billboard catches
        // up visually. Finish that walk before this coroutine owns the sprite.
        while (_walkRoutine != null) yield return null;

        _spriteRenderer ??= GetComponentInChildren<SpriteRenderer>();
        var frames = Data?.GetBattleSkillFrames(skill, facing);
        if (_spriteRenderer != null && frames != null && frames.Length > 0)
        {
            float delay = 1f / Mathf.Max(1f, Data.GetBattleSkillFps(skill));
            foreach (var frame in frames)
            {
                if (frame != null) _spriteRenderer.sprite = frame;
                yield return new WaitForSeconds(delay);
            }
        }

        if (IsAlive) ApplyIdleSprite();
        _skillRoutine = null;
    }

    void PlayHurtAnimation()
    {
        var frames = Data?.GetBattleHurtFrames(facing);
        if (frames == null || frames.Length == 0) return;
        if (_reactionRoutine != null) StopCoroutine(_reactionRoutine);
        _reactionRoutine = StartCoroutine(ReactionRoutine(frames, Data.GetBattleHurtFps(), deactivateAfter: false));
    }

    System.Collections.IEnumerator ReactionRoutine(Sprite[] frames, float fps, bool deactivateAfter)
    {
        _spriteRenderer ??= GetComponentInChildren<SpriteRenderer>();
        float delay = 1f / Mathf.Max(1f, fps);
        if (_spriteRenderer != null)
        {
            foreach (var frame in frames)
            {
                if (frame != null) _spriteRenderer.sprite = frame;
                yield return new WaitForSeconds(delay);
            }
        }

        _reactionRoutine = null;
        if (deactivateAfter) gameObject.SetActive(false);
        else if (IsAlive) ApplyIdleSprite();
    }

    // ── Actions ───────────────────────────────────────────────────────────────

    public void QueueAction(SkillDefinition skill, BattleUnit target, Vector2Int targetPos, AbsorbedSkillInstance absorbedInstance = null)
    {
        QueuedSkill             = skill;
        QueuedTarget            = target;
        QueuedTargetPos         = targetPos;
        QueuedAbsorbedInstance  = absorbedInstance;
        ChargeTicksRemaining    = BattleFormulas.ActionChargeTicks(this, skill);

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

    // ── Absorb skill (Benidito only) ─────────────────────────────────────────────

    public AbsorbedSkillInstance TryAbsorb(BattleUnit defeated)
    {
        if (Data.role != CombatantRole.Benidito) return null;
        if (defeated.Data.learnableSkills == null || defeated.Data.learnableSkills.Length == 0)
            return null;

        if (UnityEngine.Random.value > defeated.Data.skillDropChance) return null;

        var skill    = defeated.Data.learnableSkills[UnityEngine.Random.Range(0, defeated.Data.learnableSkills.Length)];
        var instance = Data.Absorb(skill);
        Debug.Log($"[Absorb] Benidito absorbed {instance.DisplayName()}");
        return instance;
    }

    // ── Death ─────────────────────────────────────────────────────────────────

    void Die()
    {
        state = UnitState.Dead;
        ct    = 0f;
        OnDied?.Invoke();
        BattleManager.Instance?.Grid?.RemoveUnit(this);

        if (_walkRoutine != null) { StopCoroutine(_walkRoutine); _walkRoutine = null; }
        if (_skillRoutine != null) { StopCoroutine(_skillRoutine); _skillRoutine = null; }
        if (_reactionRoutine != null) StopCoroutine(_reactionRoutine);

        var deathFrames = Data?.GetBattleDeathFrames(facing);
        if (deathFrames != null && deathFrames.Length > 0)
            _reactionRoutine = StartCoroutine(ReactionRoutine(
                deathFrames, Data.GetBattleDeathFps(), deactivateAfter: true));
        else
            gameObject.SetActive(false);
    }

    // ── Init ──────────────────────────────────────────────────────────────────

    public void Initialize(CombatantData data, bool isPlayer)
    {
        // Clone the asset so runtime HP/SP/state is per-unit, not shared across
        // every BattleUnit that references the same CombatantData asset.
        // Benidito is the exception — his absorbed skills/jobs must persist on the
        // original asset between battles, so he keeps his reference.
        Data     = (data != null && data.role != CombatantRole.Benidito)
                   ? Instantiate(data)
                   : data;
        IsPlayer = isPlayer;
        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        Data.InitRuntime();
        ct    = UnityEngine.Random.Range(0f, 40f); // stagger starting CT
        state = UnitState.Idle;
        Status = new StatusEffectList();
    }
}
