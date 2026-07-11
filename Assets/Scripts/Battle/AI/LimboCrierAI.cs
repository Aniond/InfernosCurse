using System;
using System.Collections.Generic;
using UnityEngine;

// Deterministic support-controller priorities approved for the common Crier.
// The behavior selects through ordinary SkillDefinition contracts, so designers
// can retune data without duplicating ability resolution here.
public class LimboCrierAI : EnemyAI
{
    public override void DecideAction(BattleUnit unit)
    {
        BattleManager battle = BattleManager.Instance;
        BattleGrid grid = battle?.Grid;
        if (unit == null || battle == null || grid == null)
        {
            battle?.EndUnitTurn(unit);
            return;
        }

        SkillDefinition benediction = FindSkill(unit, skill =>
            skill.appliesStatus && skill.statusType == StatusEffectType.FalseZeal &&
            AbilityResolver.TargetsAllies(skill));
        if (TryCrookedBenediction(unit, benediction, battle, grid)) return;

        SkillDefinition knell = FindSkill(unit, skill =>
            skill.appliesStatus && skill.statusType == StatusEffectType.Dread &&
            skill.centerOnCaster);
        if (TryKnell(unit, knell, battle)) return;

        SkillDefinition hook = FindSkill(unit, skill =>
            skill.specialEffect == SkillSpecialEffect.PullTargetTowardCaster);
        if (TryHook(unit, hook, battle, grid)) return;

        SkillDefinition jab = FindSkill(unit, skill =>
            skill.usesEquippedWeapon && skill.range == 1);
        if (TryJab(unit, jab, battle, grid)) return;

        RepositionOrRetreat(unit, battle, grid);
    }

    static bool TryCrookedBenediction(
        BattleUnit unit,
        SkillDefinition skill,
        BattleManager battle,
        BattleGrid grid)
    {
        if (!CanAfford(unit, skill)) return false;
        BattleUnit best = null;
        float bestDamage = float.NegativeInfinity;

        foreach (BattleUnit ally in battle.Enemies)
        {
            if (ally == null || ally == unit || !ally.IsAlive ||
                ally.Status.Has(StatusEffectType.FalseZeal)) continue;
            int distance = BattleGrid.ManhattanDistance(unit.gridPosition, ally.gridPosition);
            if (distance < skill.minRange || distance > skill.range) continue;
            if (skill.requiresLineOfSight &&
                !grid.HasLineOfSight(unit.gridPosition, ally.gridPosition, unit.EyeHeight)) continue;

            float expected = EstimateOutgoingDamage(ally);
            if (expected > bestDamage)
            {
                bestDamage = expected;
                best = ally;
            }
        }

        return best != null && Use(unit, skill, best, best.gridPosition, battle);
    }

    static bool TryKnell(
        BattleUnit unit,
        SkillDefinition skill,
        BattleManager battle)
    {
        if (!CanAfford(unit, skill)) return false;
        int affected = 0;
        foreach (BattleUnit player in battle.Players)
            if (player != null && player.IsAlive &&
                BattleGrid.ManhattanDistance(unit.gridPosition, player.gridPosition) <= skill.areaOfEffect)
                affected++;
        return affected >= 2 && Use(unit, skill, null, unit.gridPosition, battle);
    }

    static bool TryHook(
        BattleUnit unit,
        SkillDefinition skill,
        BattleManager battle,
        BattleGrid grid)
    {
        if (!CanAfford(unit, skill)) return false;
        BattleUnit best = null;
        int bestScore = int.MinValue;

        foreach (BattleUnit player in battle.Players)
        {
            if (player == null || !player.IsAlive) continue;
            int distance = BattleGrid.ManhattanDistance(unit.gridPosition, player.gridPosition);
            if (distance < skill.minRange || distance > skill.range) continue;
            if (skill.requiresLineOfSight &&
                !grid.HasLineOfSight(unit.gridPosition, player.gridPosition, unit.EyeHeight)) continue;

            int score = HookPriority(unit, player, battle);
            if (score <= 0 || score <= bestScore) continue;
            bestScore = score;
            best = player;
        }

        return best != null && Use(unit, skill, best, best.gridPosition, battle);
    }

    static bool TryJab(
        BattleUnit unit,
        SkillDefinition skill,
        BattleManager battle,
        BattleGrid grid)
    {
        if (!CanAfford(unit, skill)) return false;
        foreach (BattleUnit player in battle.Players)
        {
            if (player == null || !player.IsAlive) continue;
            if (BattleGrid.ManhattanDistance(unit.gridPosition, player.gridPosition) != 1) continue;
            if (skill.requiresLineOfSight &&
                !grid.HasLineOfSight(unit.gridPosition, player.gridPosition, unit.EyeHeight)) continue;
            return Use(unit, skill, player, player.gridPosition, battle);
        }
        return false;
    }

    static void RepositionOrRetreat(
        BattleUnit unit,
        BattleManager battle,
        BattleGrid grid)
    {
        CharacterStats stats = unit.GetEffectiveStats();
        int move = Mathf.Max(2, stats.speed / 3);
        int jump = Mathf.Max(1, stats.dexterity / 5);
        List<GridCell> range = grid.GetMoveRange(unit.gridPosition, move, jump, unit);
        if (range.Count == 0)
        {
            battle.EndUnitTurn(unit);
            return;
        }

        bool protectedByAlly = false;
        foreach (BattleUnit ally in battle.Enemies)
            if (ally != null && ally != unit && ally.IsAlive &&
                BattleGrid.ManhattanDistance(unit.gridPosition, ally.gridPosition) <= 4)
            {
                protectedByAlly = true;
                break;
            }

        GridCell best = null;
        float bestScore = float.NegativeInfinity;
        foreach (GridCell cell in range)
        {
            float nearestPlayer = NearestDistance(cell.gridPos, battle.Players);
            int adjacentAllies = CountNearby(cell.gridPos, battle.Enemies, unit, 1);
            int knellTargets = CountNearby(cell.gridPos, battle.Players, null, 2);
            float score = protectedByAlly
                ? adjacentAllies * 6f + knellTargets * 4f - nearestPlayer * 0.25f
                : nearestPlayer * 3f + adjacentAllies * 2f;
            if (score > bestScore)
            {
                bestScore = score;
                best = cell;
            }
        }

        if (best != null) grid.MoveUnitAnimated(unit, best.gridPos);
        battle.EndUnitTurn(unit);
    }

    static SkillDefinition FindSkill(
        BattleUnit unit,
        Func<SkillDefinition, bool> predicate)
    {
        if (unit?.Data?.equippedSkills?.actives == null) return null;
        foreach (SkillDefinition skill in unit.Data.equippedSkills.actives)
            if (skill != null && skill.skillType == SkillType.Active && predicate(skill)) return skill;
        return null;
    }

    static bool CanAfford(BattleUnit unit, SkillDefinition skill) =>
        skill != null && unit.HasSP(skill.spCost);

    static bool Use(
        BattleUnit unit,
        SkillDefinition skill,
        BattleUnit target,
        Vector2Int targetPosition,
        BattleManager battle)
    {
        unit.QueueAction(skill, target, targetPosition);
        if (unit.ChargeTicksRemaining > 0) battle.EndUnitTurn(unit);
        else battle.ResolveQueuedAction(unit);
        return true;
    }

    static float EstimateOutgoingDamage(BattleUnit unit)
    {
        CharacterStats stats = unit.GetEffectiveStats();
        float best = stats.strength;
        if (unit.Data.equippedSkills?.actives == null) return best;
        foreach (SkillDefinition skill in unit.Data.equippedSkills.actives)
        {
            if (skill == null || skill.isHealing || skill.damageType == DamageType.None) continue;
            float scaling = skill.primaryStat switch
            {
                StatScaling.Strength => stats.strength,
                StatScaling.Dexterity => stats.dexterity,
                StatScaling.Constitution => stats.constitution,
                StatScaling.Creativity => stats.creativity,
                StatScaling.Faith => stats.faith,
                StatScaling.Perception => stats.perception,
                StatScaling.Speed => stats.speed,
                _ => 0f,
            };
            best = Mathf.Max(best, skill.basePower + scaling * skill.scalingMultiplier);
        }
        return best;
    }

    static int HookPriority(BattleUnit crier, BattleUnit target, BattleManager battle)
    {
        int score = 0;
        if (target.state == UnitState.Charging) score += 6;
        if (target.Elevation > crier.Elevation) score += 3;
        if (HasRangedSkill(target)) score += 4;
        if (CountNearby(target.gridPosition, battle.Players, target, 1) > 0) score += 2;
        return score;
    }

    static bool HasRangedSkill(BattleUnit unit)
    {
        if (unit?.Data?.equippedSkills?.actives == null) return false;
        foreach (SkillDefinition skill in unit.Data.equippedSkills.actives)
            if (skill != null && skill.range >= 3) return true;
        return false;
    }

    static int CountNearby(
        Vector2Int position,
        List<BattleUnit> units,
        BattleUnit excluded,
        int radius)
    {
        int count = 0;
        foreach (BattleUnit unit in units)
            if (unit != null && unit != excluded && unit.IsAlive &&
                BattleGrid.ManhattanDistance(position, unit.gridPosition) <= radius)
                count++;
        return count;
    }

    static float NearestDistance(Vector2Int position, List<BattleUnit> units)
    {
        float nearest = 999f;
        foreach (BattleUnit unit in units)
            if (unit != null && unit.IsAlive)
                nearest = Mathf.Min(nearest,
                    BattleGrid.ManhattanDistance(position, unit.gridPosition));
        return nearest;
    }
}
