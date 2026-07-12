using UnityEngine;

// All numeric combat calculations live here.
// Change numbers in one place, affects the whole game.
public static class BattleFormulas
{
    // ── Damage ────────────────────────────────────────────────────────────────

    public static int CalcDamage(BattleUnit attacker, BattleUnit defender, SkillDefinition skill, float powerOverride = -1f)
    {
        float dmg = CalcDamageCore(attacker, defender, skill, powerOverride);

        // Random variance ±10%
        dmg *= Random.Range(0.9f, 1.1f);

        return Mathf.Max(1, Mathf.RoundToInt(dmg));
    }

    // Deterministic damage before variance/crit — shared by the real roll and
    // the forecast preview so the two can never drift apart. powerOverride lets
    // an absorbed skill substitute its level/refine-scaled power (see
    // AbsorbedSkillInstance.GetEffectivePower) for the definition's basePower.
    static float CalcDamageCore(BattleUnit attacker, BattleUnit defender, SkillDefinition skill, float powerOverride = -1f)
    {
        var atkStats = attacker.GetEffectiveStats();
        var defStats = defender.GetEffectiveStats();
        DamageType damageType = GetEffectiveDamageType(attacker, skill);

        float power       = powerOverride >= 0f ? powerOverride : skill.basePower;
        if (skill.usesEquippedWeapon && attacker.Data.weapon != null)
            power += attacker.Data.weapon.attackPowerBonus;
        float scalingStat = GetScalingStat(atkStats, skill.primaryStat);
        float baseDmg     = power + scalingStat * skill.scalingMultiplier;

        // Defense reduction based on damage type
        float defense = GetDefenseStat(defStats, damageType);

        // Cursed: the target's spiritual guard is broken — magic defense suffers
        if (!IsPhysical(damageType) && defender.Status.Has(StatusEffectType.Cursed))
            defense *= 0.6f;

        float dmg = Mathf.Max(1f, baseDmg - defense * 0.5f);

        // Status effect reductions
        if (IsPhysical(damageType))
            dmg *= 1f - defender.Status.CombinedPhysicalReduction();
        else
            dmg *= 1f - defender.Status.CombinedMagicReduction();

        // Inspired: attacker channels heightened creativity into the strike
        if (attacker.Status.Has(StatusEffectType.Inspired))
            dmg *= 1.15f;

        dmg *= attacker.Status.CombinedOutgoingDamageMultiplier();

        // Facing multiplier (FFT-style)
        dmg *= GetFacingMultiplier(attacker, defender);

        // Elevation bonus — attacker higher than defender
        int elevDiff = attacker.Elevation - defender.Elevation;
        if (elevDiff > 0) dmg *= 1f + elevDiff * 0.1f;

        dmg *= defender.Data.GetDamageReceivedMultiplier(damageType);
        dmg *= defender.Status.CombinedDamageReceivedMultiplier(damageType);

        return dmg;
    }

    // ── Forecast (FFT confirm-panel numbers — no RNG rolled) ──────────────────

    public static (int min, int max, float hitChance) PreviewAttack(
        BattleUnit attacker, BattleUnit defender, SkillDefinition skill, float powerOverride = -1f)
    {
        float core = CalcDamageCore(attacker, defender, skill, powerOverride);
        int min = Mathf.Max(1, Mathf.RoundToInt(core * 0.9f));
        int max = Mathf.Max(1, Mathf.RoundToInt(core * 1.1f));
        return (min, max, PreviewHitChance(attacker, defender, skill));
    }

    public static float PreviewHitChance(BattleUnit attacker, BattleUnit defender, SkillDefinition skill)
    {
        if (skill.damageType == DamageType.None) return 1f;  // utility path skips the roll
        var atkStats = attacker.GetEffectiveStats();
        var defStats = defender.GetEffectiveStats();

        float baseHit   = skill.baseHit;   // per-skill accuracy (David's hierarchy)
        float percBonus = (atkStats.perception - defStats.perception) * 0.02f;
        float dexBonus  = (atkStats.dexterity  - defStats.speed)      * 0.015f;

        float hitChance = Mathf.Clamp(baseHit + percBonus + dexBonus, 0.05f, 0.99f);
        return hitChance * attacker.Status.CombinedHitMultiplier();
    }

    // ── Healing ───────────────────────────────────────────────────────────────

    public static int CalcHeal(BattleUnit healer, SkillDefinition skill, float powerOverride = -1f)
    {
        var stats   = healer.GetEffectiveStats();
        float faith = stats.faith;
        float cre   = stats.creativity;
        float power = powerOverride >= 0f ? powerOverride : skill.basePower;
        float heal  = power + faith * skill.scalingMultiplier + cre * 0.25f;

        // Holy refinement bonus — if Benidito is using a refined skill
        if (skill.damageType == DamageType.Holy) heal *= 1.2f;

        heal *= Random.Range(0.95f, 1.05f);
        return Mathf.Max(1, Mathf.RoundToInt(heal));
    }

    // ── Hit chance ────────────────────────────────────────────────────────────

    // Accuracy: attacker Perception vs defender Perception (tracking/awareness).
    // Evasion: attacker Dexterity vs defender Speed — Speed doubles as the
    // dodge stat, so a faster defender is harder to land hits on.
    public static bool RollHit(BattleUnit attacker, BattleUnit defender, SkillDefinition skill)
    {
        return Random.value <= PreviewHitChance(attacker, defender, skill);
    }

    // ── Crit chance ───────────────────────────────────────────────────────────

    public static bool RollCrit(BattleUnit attacker)
    {
        var stats    = attacker.Data.GetTotalStats();
        float chance = 0.05f + stats.dexterity * 0.005f;
        return Random.value <= Mathf.Clamp(chance, 0f, 0.5f);
    }

    // ── CT system ─────────────────────────────────────────────────────────────

    // CT gain per tick — Speed drives how quickly a unit reaches 100
    public static float CTGainPerTick(BattleUnit unit)
    {
        var stats = unit.GetEffectiveStats();
        float insanityMultiplier = PlayerInsanityModifiers.For(unit.Data).CtGainMultiplier;
        return stats.speed * unit.Status.CombinedSpeedMultiplier(unit) * insanityMultiplier;
    }

    // Charge ticks before a queued action fires
    public static int ActionChargeTicks(BattleUnit unit, SkillDefinition skill)
    {
        if (skill.chargeTicks == 0) return 0;
        var stats  = unit.Data.GetTotalStats();
        float mod  = 1f - (stats.speed - 10) * 0.03f; // faster units charge quicker
        return Mathf.Max(1, Mathf.RoundToInt(skill.chargeTicks * mod));
    }

    // ── Job / AP ──────────────────────────────────────────────────────────────

    // AP earned per kill — scales with enemy level relative to party
    public static int APFromKill(BattleUnit killer, BattleUnit defeated)
    {
        int lvDiff = defeated.Data.baseStats.level - killer.Data.baseStats.level;
        int base_  = 10;
        return Mathf.Max(1, base_ + lvDiff * 2);
    }

    // XP earned per kill
    public static int XPFromKill(BattleUnit killer, BattleUnit defeated)
    {
        int lvDiff = defeated.Data.baseStats.level - killer.Data.baseStats.level;
        int base_  = 30;
        return Mathf.Max(1, base_ + lvDiff * 5);
    }

    // Florins looted per kill (party pot, once per kill — not per survivor)
    public static int FlorinsFromKill(BattleUnit killer, BattleUnit defeated)
    {
        int lvDiff = defeated.Data.baseStats.level - killer.Data.baseStats.level;
        int base_  = 8;
        return Mathf.Max(1, base_ + lvDiff * 3);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static float GetScalingStat(CharacterStats s, StatScaling scaling) => scaling switch
    {
        StatScaling.Strength     => s.strength,
        StatScaling.Dexterity    => s.dexterity,
        StatScaling.Constitution => s.constitution,
        StatScaling.Creativity   => s.creativity,
        StatScaling.Faith        => s.faith,
        StatScaling.Perception   => s.perception,
        StatScaling.Speed        => s.speed,
        _                        => 0f,
    };

    static float GetDefenseStat(CharacterStats s, DamageType dt) =>
        IsPhysical(dt) ? s.constitution : s.faith;

    static bool IsPhysical(DamageType dt) =>
        dt == DamageType.Physical;

    public static DamageType GetEffectiveDamageType(BattleUnit attacker, SkillDefinition skill)
    {
        if (skill == null) return DamageType.None;
        if (skill.usesEquippedWeapon && attacker?.Data?.weapon != null)
            return attacker.Data.weapon.damageType;
        return skill.damageType;
    }

    static float GetFacingMultiplier(BattleUnit attacker, BattleUnit defender)
    {
        // FFT facing: back = 1.25x, side = 1.1x, front = 1.0x
        Vector2Int dir = attacker.gridPosition - defender.gridPosition;

        // Convert attacker direction to defender's facing space
        Vector2Int defFacing = defender.FacingDirection;

        // Dot product: -1 = attacker behind defender (back attack)
        int dot = dir.x * defFacing.x + dir.y * defFacing.y;
        if (dot < 0)  return 1.25f; // back
        if (dot == 0) return 1.10f; // side
        return 1.00f;               // front
    }
}
