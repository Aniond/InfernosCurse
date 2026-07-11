using System.Collections.Generic;
using UnityEngine;

// APPEND ONLY: status values are serialized in skill assets and saves.
public enum StatusEffectType
{
    Poison,
    Blind,
    Slow,
    Haste,
    Stop,
    Regen,
    Protect,
    Shell,
    Burn,
    Frozen,
    Cursed,
    Inspired,
    Dread,
    FalseZeal,
}

[System.Serializable]
public class StatusEffect
{
    public StatusEffectType type;
    public int remainingTurns;
    public float magnitude;
    public BattleUnit source;

    public StatusEffect(StatusEffectType t, int turns, float mag, BattleUnit src)
    {
        type = t;
        remainingTurns = turns;
        magnitude = mag;
        source = src;
    }

    public void OnTurnStart(BattleUnit owner)
    {
        switch (type)
        {
            case StatusEffectType.Poison:
                int poisonDmg = Mathf.Max(1, Mathf.RoundToInt(owner.Data.currentHP * magnitude));
                owner.TakeDamage(poisonDmg, DamageType.Poison, null);
                break;
            case StatusEffectType.Regen:
                int healAmt = Mathf.Max(1, Mathf.RoundToInt(owner.Data.GetTotalStats().hpMax * magnitude));
                owner.Heal(healAmt);
                break;
            case StatusEffectType.Burn:
                int burnDmg = Mathf.Max(1, Mathf.RoundToInt(owner.Data.currentHP * magnitude));
                owner.TakeDamage(burnDmg, DamageType.Fire, null);
                break;
        }
    }

    public void Tick() => remainingTurns--;
    public bool IsExpired => remainingTurns <= 0;

    // Stop/Frozen deliberately do not zero CT gain: they consume turns through
    // StatusEffectList.PreventsAction, allowing their durations to expire.
    public float GetSpeedMultiplier(BattleUnit owner)
    {
        if (type == StatusEffectType.Haste) return magnitude > 0f ? 1f + magnitude : 2f;
        if (type == StatusEffectType.Slow) return 0.5f;
        if (type == StatusEffectType.Dread && !(owner?.Data?.resistsDreadCtPenalty ?? false))
            return 0.85f;
        return 1f;
    }

    public float GetPhysicalDamageReduction() =>
        type == StatusEffectType.Protect ? magnitude : 0f;

    public float GetMagicDamageReduction() =>
        type == StatusEffectType.Shell ? magnitude : 0f;

    public float GetHitChanceMultiplier() =>
        type == StatusEffectType.Blind ? 1f - magnitude : 1f;

    public float GetFaithPenalty() =>
        type == StatusEffectType.Dread ? magnitude : 0f;

    public float GetOutgoingDamageMultiplier() =>
        type == StatusEffectType.FalseZeal ? 1f + Mathf.Max(0f, magnitude) : 1f;

    public float GetDamageReceivedMultiplier(DamageType damageType) =>
        type == StatusEffectType.FalseZeal && damageType == DamageType.Holy ? 1.15f : 1f;
}

public class StatusEffectList
{
    readonly List<StatusEffect> _effects = new List<StatusEffect>();
    readonly List<StatusEffect> _turnStartEffects = new List<StatusEffect>();

    public IReadOnlyList<StatusEffect> All => _effects;

    public void Apply(StatusEffect effect)
    {
        if (effect == null) return;
        foreach (var active in _effects)
        {
            if (active.type != effect.type) continue;
            active.remainingTurns = Mathf.Max(active.remainingTurns, effect.remainingTurns);
            active.magnitude = Mathf.Max(active.magnitude, effect.magnitude);
            if (effect.source != null) active.source = effect.source;
            return;
        }
        _effects.Add(effect);
    }

    public void Remove(StatusEffectType type)
    {
        _effects.RemoveAll(effect => effect.type == type);
        _turnStartEffects.RemoveAll(effect => effect.type == type);
    }

    public bool Has(StatusEffectType type) =>
        _effects.Exists(effect => effect.type == type);

    public BattleUnit SourceOf(StatusEffectType type) =>
        _effects.Find(effect => effect.type == type)?.source;

    public bool PreventsAction =>
        Has(StatusEffectType.Stop) || Has(StatusEffectType.Frozen);

    // Kept under its existing API name for callers. Effects are captured and
    // fired at start, then remain active through the entire affected turn.
    public void TickAll(BattleUnit owner)
    {
        _turnStartEffects.Clear();
        _turnStartEffects.AddRange(_effects.ToArray());
        foreach (var effect in _turnStartEffects)
        {
            if (!owner.IsAlive) break;
            effect.OnTurnStart(owner);
        }
    }

    // Only effects present at turn start lose duration. A self-buff applied
    // during the action therefore does not immediately spend one of its turns.
    public void EndTurn()
    {
        foreach (var effect in _turnStartEffects)
            if (_effects.Contains(effect)) effect.Tick();
        _turnStartEffects.Clear();
        _effects.RemoveAll(effect => effect.IsExpired);
    }

    public float CombinedSpeedMultiplier(BattleUnit owner = null)
    {
        float multiplier = 1f;
        foreach (var effect in _effects) multiplier *= effect.GetSpeedMultiplier(owner);
        return multiplier;
    }

    public float CombinedPhysicalReduction()
    {
        float reduction = 0f;
        foreach (var effect in _effects)
            reduction = 1f - (1f - reduction) * (1f - effect.GetPhysicalDamageReduction());
        return reduction;
    }

    public float CombinedMagicReduction()
    {
        float reduction = 0f;
        foreach (var effect in _effects)
            reduction = 1f - (1f - reduction) * (1f - effect.GetMagicDamageReduction());
        return reduction;
    }

    public float CombinedHitMultiplier()
    {
        float multiplier = 1f;
        foreach (var effect in _effects) multiplier *= effect.GetHitChanceMultiplier();
        return multiplier;
    }

    public float CombinedFaithPenalty()
    {
        float penalty = 0f;
        foreach (var effect in _effects) penalty += effect.GetFaithPenalty();
        return penalty;
    }

    public float CombinedOutgoingDamageMultiplier()
    {
        float multiplier = 1f;
        foreach (var effect in _effects) multiplier *= effect.GetOutgoingDamageMultiplier();
        return multiplier;
    }

    public float CombinedDamageReceivedMultiplier(DamageType damageType)
    {
        float multiplier = 1f;
        foreach (var effect in _effects)
            multiplier *= effect.GetDamageReceivedMultiplier(damageType);
        return multiplier;
    }
}
