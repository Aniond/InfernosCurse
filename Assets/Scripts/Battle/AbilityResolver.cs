using System.Collections.Generic;
using UnityEngine;

public static class AbilityResolver
{
    public static void Resolve(
        BattleUnit user,
        SkillDefinition skill,
        Vector2Int targetPos,
        AbsorbedSkillInstance absorbedInstance = null)
    {
        if (user == null || !user.IsAlive || skill == null) return;

        int spCost = absorbedInstance != null
            ? absorbedInstance.GetEffectiveSPCost()
            : skill.spCost;
        if (spCost > 0)
        {
            if (!user.HasSP(spCost))
            {
                Debug.Log($"{user.Data.displayName} doesn't have enough SP for {skill.skillName}.");
                return;
            }
            user.SpendSP(spCost);
        }

        float powerOverride = absorbedInstance != null
            ? absorbedInstance.GetEffectivePower()
            : -1f;
        Vector2Int resolvedCenter = skill.centerOnCaster ? user.gridPosition : targetPos;

        foreach (BattleUnit target in GatherTargets(user, skill, resolvedCenter))
            ApplySkill(user, target, skill, powerOverride, absorbedInstance);
    }

    public static bool TargetsAllies(SkillDefinition skill)
    {
        if (skill == null) return false;
        if (skill.targetSide == SkillTargetSide.Allied) return true;
        if (skill.targetSide == SkillTargetSide.Hostile) return false;
        return skill.isHealing;
    }

    public static bool IsLegalTarget(
        BattleUnit user,
        BattleUnit target,
        SkillDefinition skill)
    {
        if (user == null || target == null || skill == null || !target.IsAlive) return false;
        if (target == user && !skill.allowSelfTarget) return false;
        bool allied = target.IsPlayer == user.IsPlayer;
        return TargetsAllies(skill) ? allied : !allied;
    }

    static List<BattleUnit> GatherTargets(
        BattleUnit user,
        SkillDefinition skill,
        Vector2Int targetPos)
    {
        var result = new List<BattleUnit>();
        BattleGrid grid = BattleManager.Instance?.Grid;
        if (grid == null) return result;

        if (skill.areaOfEffect <= 0)
        {
            BattleUnit target = grid.GetCell(targetPos)?.occupant;
            if (IsLegalTarget(user, target, skill)) result.Add(target);
            return result;
        }

        foreach (GridCell cell in grid.GetAOECells(targetPos, skill.areaOfEffect))
        {
            BattleUnit target = cell.occupant;
            if (IsLegalTarget(user, target, skill)) result.Add(target);
        }
        return result;
    }

    static void ApplySkill(
        BattleUnit user,
        BattleUnit target,
        SkillDefinition skill,
        float powerOverride,
        AbsorbedSkillInstance absorbedInstance)
    {
        if (target == null || !target.IsAlive || skill.skillType != SkillType.Active) return;

        if (skill.isHealing)
        {
            int heal = BattleFormulas.CalcHeal(user, skill, powerOverride);
            target.Heal(heal);
            ApplySpecialEffect(user, target, skill);
            Debug.Log($"{user.Data.displayName} heals {target.Data.displayName} for {heal}.");
            return;
        }

        if (skill.damageType == DamageType.None && !skill.usesEquippedWeapon)
        {
            ApplyTileEffect(user, target);
            ApplySpecialEffect(user, target, skill);
            ApplySkillStatus(user, target, skill, absorbedInstance);
            Debug.Log($"{user.Data.displayName} uses {skill.skillName} on {target.Data.displayName}.");
            return;
        }

        if (!BattleFormulas.RollHit(user, target, skill))
        {
            Debug.Log($"{user.Data.displayName}'s {skill.skillName} missed {target.Data.displayName}!");
            DamageNumberPool.Instance?.ShowMiss(target);
            return;
        }

        int damage = BattleFormulas.CalcDamage(user, target, skill, powerOverride);
        bool critical = BattleFormulas.RollCrit(user);
        if (critical)
        {
            damage = Mathf.RoundToInt(damage * 1.5f);
            Debug.Log("Critical hit!");
        }

        DamageType effectiveType = BattleFormulas.GetEffectiveDamageType(user, skill);
        target.TakeDamage(damage, effectiveType, user, critical);
        Debug.Log($"{user.Data.displayName} uses {skill.skillName} on {target.Data.displayName} for {damage} {effectiveType} damage.");

        if (target.IsAlive)
        {
            ApplyTileEffect(user, target);
            ApplySkillStatus(user, target, skill, absorbedInstance);
            ApplySpecialEffect(user, target, skill);
        }

        if (!target.IsAlive)
        {
            AbsorbedSkillInstance absorbed = user.TryAbsorb(target);
            if (absorbed != null) BattleManager.Instance?.NotifyAbsorb(user, absorbed);
            BattleManager.Instance?.AwardPostKill(user, target);
        }
    }

    static void ApplySkillStatus(
        BattleUnit source,
        BattleUnit target,
        SkillDefinition skill,
        AbsorbedSkillInstance absorbedInstance)
    {
        if (!skill.appliesStatus || target == null || !target.IsAlive) return;
        if (skill.statusChance < 1f && Random.value > skill.statusChance) return;

        float magnitude = skill.statusMagnitude;
        if (skill.statusType == StatusEffectType.Dread)
        {
            int level = absorbedInstance?.level ?? 1;
            magnitude = level >= 5 ? 5f : level >= 3 ? 4f : 3f;
        }

        target.ApplyStatus(new StatusEffect(
            skill.statusType,
            Mathf.Max(1, skill.statusDuration),
            magnitude,
            source));
        Debug.Log($"{target.Data.displayName} is afflicted with {skill.statusType} from {skill.skillName}.");
    }

    static void ApplySpecialEffect(
        BattleUnit source,
        BattleUnit target,
        SkillDefinition skill)
    {
        switch (skill.specialEffect)
        {
            case SkillSpecialEffect.PullTargetTowardCaster:
                if (!ForcedMovementService.TryPullOneCell(
                        BattleManager.Instance?.Grid,
                        target,
                        source.gridPosition,
                        out ForcedMovementFailure pullFailure))
                    Debug.Log($"[ForcedMovement] {skill.skillName} dealt damage but could not pull {target.Data.displayName}: {pullFailure}.");
                break;

            case SkillSpecialEffect.RemoveDread:
                target.RemoveStatus(StatusEffectType.Dread);
                break;

            case SkillSpecialEffect.PullAllyTowardCasterAndProtect:
                ForcedMovementService.TryPullOneCell(
                    BattleManager.Instance?.Grid,
                    target,
                    source.gridPosition,
                    out _);
                target.ApplyStatus(new StatusEffect(StatusEffectType.Protect, 1, 0.25f, source));
                break;
        }
    }

    static void ApplyTileEffect(BattleUnit source, BattleUnit target)
    {
        BattleGrid grid = BattleManager.Instance?.Grid;
        GridCell cell = grid?.GetCell(target.gridPosition);
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
        }
    }
}
