using UnityEngine;
using System.Collections.Generic;

public enum StatusEffectType
{
    Poison,     // lose HP each CT tick
    Blind,      // hit chance reduced
    Slow,       // Speed halved
    Haste,      // Speed doubled
    Stop,       // CT frozen
    Regen,      // gain HP each CT tick
    Protect,    // physical damage reduced
    Shell,      // magic damage reduced
    Burn,       // fire damage over time
    Frozen,     // can't act, vulnerable to shatter
    Cursed,     // Faith reduced, dark damage amplified
    Inspired,   // Creativity bonus, AP gain increased
}

[System.Serializable]
public class StatusEffect
{
    public StatusEffectType type;
    public int              remainingTurns;
    public float            magnitude;      // 0-1 for reductions, flat for DoT
    public BattleUnit       source;         // who applied this

    public StatusEffect(StatusEffectType t, int turns, float mag, BattleUnit src)
    {
        type           = t;
        remainingTurns = turns;
        magnitude      = mag;
        source         = src;
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

    // Stat modifiers applied when computing effective stats
    public float GetSpeedMultiplier()
    {
        if (type == StatusEffectType.Haste) return 2f;
        if (type == StatusEffectType.Slow)  return 0.5f;
        if (type == StatusEffectType.Stop)  return 0f;
        return 1f;
    }

    public float GetPhysicalDamageReduction()
    {
        if (type == StatusEffectType.Protect) return magnitude;
        return 0f;
    }

    public float GetMagicDamageReduction()
    {
        if (type == StatusEffectType.Shell) return magnitude;
        return 0f;
    }

    public float GetHitChanceMultiplier()
    {
        if (type == StatusEffectType.Blind) return 1f - magnitude;
        return 1f;
    }
}

public class StatusEffectList
{
    private List<StatusEffect> _effects = new List<StatusEffect>();

    public IReadOnlyList<StatusEffect> All => _effects;

    public void Apply(StatusEffect effect)
    {
        // Refresh duration if same type already active
        foreach (var e in _effects)
        {
            if (e.type == effect.type)
            {
                e.remainingTurns = Mathf.Max(e.remainingTurns, effect.remainingTurns);
                e.magnitude      = Mathf.Max(e.magnitude, effect.magnitude);
                return;
            }
        }
        _effects.Add(effect);
    }

    public void Remove(StatusEffectType type)
    {
        _effects.RemoveAll(e => e.type == type);
    }

    public bool Has(StatusEffectType type)
    {
        return _effects.Exists(e => e.type == type);
    }

    public void TickAll(BattleUnit owner)
    {
        foreach (var e in _effects) { e.OnTurnStart(owner); e.Tick(); }
        _effects.RemoveAll(e => e.IsExpired);
    }

    public float CombinedSpeedMultiplier()
    {
        float m = 1f;
        foreach (var e in _effects) m *= e.GetSpeedMultiplier();
        return m;
    }

    public float CombinedPhysicalReduction()
    {
        float r = 0f;
        foreach (var e in _effects) r = 1f - (1f - r) * (1f - e.GetPhysicalDamageReduction());
        return r;
    }

    public float CombinedMagicReduction()
    {
        float r = 0f;
        foreach (var e in _effects) r = 1f - (1f - r) * (1f - e.GetMagicDamageReduction());
        return r;
    }

    public float CombinedHitMultiplier()
    {
        float m = 1f;
        foreach (var e in _effects) m *= e.GetHitChanceMultiplier();
        return m;
    }
}
