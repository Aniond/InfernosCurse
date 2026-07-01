using UnityEngine;
using System.Collections.Generic;

public static class AbilityResolver
{
    // Main entry point — resolves a skill use from user onto a target position
    public static void Resolve(BattleUnit user, SkillDefinition skill, Vector2Int targetPos)
    {
        if (skill == null) return;

        // Spend SP
        if (skill.spCost > 0)
        {
            if (!user.HasSP(skill.spCost))
            {
                Debug.Log($"{user.Data.displayName} doesn't have enough SP for {skill.skillName}.");
                return;
            }
            user.SpendSP(skill.spCost);
        }

        // Gather targets
        var targets = GatherTargets(user, skill, targetPos);

        foreach (var target in targets)
            ApplySkill(user, target, skill);
    }

    static List<BattleUnit> GatherTargets(BattleUnit user, SkillDefinition skill, Vector2Int targetPos)
    {
        var result = new List<BattleUnit>();
        var grid   = BattleManager.Instance?.Grid;
        if (grid == null) return result;

        if (skill.areaOfEffect <= 0)
        {
            // Single target
            var cell = grid.GetCell(targetPos);
            if (cell?.occupant != null) result.Add(cell.occupant);
        }
        else
        {
            // AOE
            var cells = grid.GetAOECells(targetPos, skill.areaOfEffect);
            foreach (var cell in cells)
                if (cell.occupant != null && cell.occupant.IsAlive)
                    result.Add(cell.occupant);
        }

        return result;
    }

    static void ApplySkill(BattleUnit user, BattleUnit target, SkillDefinition skill)
    {
        if (!target.IsAlive) return;

        switch (skill.skillType)
        {
            case SkillType.Active:
                ApplyActive(user, target, skill);
                break;

            case SkillType.Passive:
            case SkillType.Reaction:
            case SkillType.Movement:
                // Handled by passive system, not direct resolution
                break;
        }
    }

    static void ApplyActive(BattleUnit user, BattleUnit target, SkillDefinition skill)
    {
        // Healing skills
        if (skill.isHealing)
        {
            int heal = BattleFormulas.CalcHeal(user, skill);
            target.Heal(heal);
            Debug.Log($"{user.Data.displayName} heals {target.Data.displayName} for {heal}.");
            return;
        }

        // Non-damaging utility / buff (damageType None but not flagged healing).
        // Tile/status effects still apply; no damage roll.
        if (skill.damageType == DamageType.None)
        {
            ApplyTileEffect(user, target);
            ApplySkillStatus(user, target, skill);
            Debug.Log($"{user.Data.displayName} uses {skill.skillName} on {target.Data.displayName}.");
            return;
        }

        // Hit check
        if (!BattleFormulas.RollHit(user, target, skill))
        {
            Debug.Log($"{user.Data.displayName}'s {skill.skillName} missed {target.Data.displayName}!");
            DamageNumberPool.Instance?.ShowMiss(target);
            return;
        }

        // Damage
        int dmg  = BattleFormulas.CalcDamage(user, target, skill);
        bool crit = BattleFormulas.RollCrit(user);
        if (crit)
        {
            dmg = Mathf.RoundToInt(dmg * 1.5f);
            Debug.Log($"Critical hit!");
        }

        target.TakeDamage(dmg, skill.damageType, user, crit);
        Debug.Log($"{user.Data.displayName} uses {skill.skillName} on {target.Data.displayName} for {dmg} {skill.damageType} damage.");

        // Status effect from tile
        ApplyTileEffect(user, target);

        // Status effect carried by the skill itself (e.g. ergot poison, oven burn)
        ApplySkillStatus(user, target, skill);

        // Post-kill absorb (Dante only)
        if (!target.IsAlive)
        {
            var absorbed = user.TryAbsorb(target);
            if (absorbed != null)
                BattleManager.Instance?.NotifyAbsorb(user, absorbed);

            // Award AP/XP to all party members
            BattleManager.Instance?.AwardPostKill(user, target);
        }
    }

    // Applies a status carried by the skill definition itself, independent of the
    // target's tile. Rolls statusChance; call only after a hit has landed.
    static void ApplySkillStatus(BattleUnit source, BattleUnit target, SkillDefinition skill)
    {
        if (skill == null || !skill.appliesStatus || target == null || !target.IsAlive) return;
        if (skill.statusChance < 1f && Random.value > skill.statusChance) return;

        target.ApplyStatus(new StatusEffect(
            skill.statusType, skill.statusDuration, skill.statusMagnitude, source));
        Debug.Log($"{target.Data.displayName} is afflicted with {skill.statusType} " +
                  $"from {skill.skillName}.");
    }

    static void ApplyTileEffect(BattleUnit source, BattleUnit target)
    {
        var grid = BattleManager.Instance?.Grid;
        if (grid == null) return;
        var cell = grid.GetCell(target.gridPosition);
        if (cell == null) return;

        switch (cell.tileType)
        {
            case TileType.Fire:
                target.ApplyStatus(new StatusEffect(StatusEffectType.Burn, 3, 0.05f, source));
                break;
            case TileType.Poison:
                target.ApplyStatus(new StatusEffect(StatusEffectType.Poison, 3, 0.04f, source));
                break;
            case TileType.Ice:
                target.ApplyStatus(new StatusEffect(StatusEffectType.Frozen, 2, 0.1f, source));
                break;
            case TileType.Holy:
                // Holy tiles heal friendlies, burn undead — handled by damage type check above
                break;
        }
    }
}
