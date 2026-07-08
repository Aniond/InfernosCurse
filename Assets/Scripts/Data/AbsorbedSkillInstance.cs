using UnityEngine;
using System;

[Serializable]
public class AbsorbedSkillInstance
{
    public SkillDefinition definition;

    [Tooltip("Orbs OWNED for this skill (1 per absorb, duplicates included).")]
    public int duplicateCount = 1;

    [Tooltip("Orbs SLOTTED = current level (David's skill tab: 5 slots per skill; each slotted orb is +1 level of power AND madness; remove orbs to manage insanity).")]
    public int level = 1;

    [Tooltip("True if this skill has been refined into its Holy version at the Church.")]
    public bool isRefined = false;

    // David's management model (7/08): drops BANK orbs; the skill tab slots
    // them. Slotted orbs = level = power = madness, and orbs can be pulled
    // back out ("if you don't want to up the skill, remove an orb"). Until
    // the skill-tab UI ships, new orbs auto-slot so drops still feel alive —
    // the UI will expose SlotOrb/UnslotOrb directly.
    public int MaxLevel => Mathf.Max(1, definition != null ? definition.maxLevel : 5);
    public int OwnedOrbs => duplicateCount;
    public int SlottableMax => Mathf.Min(duplicateCount, MaxLevel);

    public void AddDuplicate(bool autoSlot = true)
    {
        duplicateCount++;
        if (autoSlot) level = SlottableMax;
        else          level = Mathf.Clamp(level, 1, SlottableMax);
    }

    // Slot a banked orb: +1 level (needs an owned orb free and a slot open).
    public bool SlotOrb()
    {
        if (level >= SlottableMax) return false;
        level++;
        return true;
    }

    // Pull an orb back out: -1 level, floor 1 (the skill itself is known —
    // unequip the orb entirely to carry none of its madness).
    public bool UnslotOrb()
    {
        if (level <= 1) return false;
        level--;
        return true;
    }

    public bool CanRefine()
    {
        return !isRefined
            && definition != null
            && definition.refinable
            && definition.holyVersion != null
            && level >= definition.maxLevel;
    }

    // The definition battle code should CAST: the holy counterpart once
    // refined, the corrupted original otherwise. `definition` itself is never
    // swapped — duplicate matching (AddDuplicate) and re-absorption key off
    // the original, so a reference swap would orphan the lineage. Absorbed
    // skills have no battle casting path yet; this is the contract the future
    // loadout/casting code consumes.
    public SkillDefinition EffectiveDefinition =>
        isRefined && definition != null && definition.holyVersion != null
            ? definition.holyVersion
            : definition;

    // David's growth model (7/08), all flat and balance-testable:
    //   damage: basePower + level      ("starts off +1 damage ... to a maximum of +5")
    //   SP:     spCost + (level - 1)   ("skill points increase with each level up")
    //   stat:   bonusStatPerLevel * level while EQUIPPED (applied in GetTotalStats)
    public float GetEffectivePower()
    {
        if (definition == null) return 0f;
        float holyMult = isRefined ? 1.5f : 1f;
        return (definition.basePower + level) * holyMult;
    }

    public int GetEffectiveSPCost()
    {
        if (definition == null) return 0;
        return definition.spCost + (level - 1);
    }

    // Equipped-orb stat bonus at current level (0 if the skill grants none).
    public int GetStatBonus()
    {
        if (definition == null || definition.bonusStat == StatScaling.None) return 0;
        return definition.bonusStatPerLevel * level;
    }

    // The insanity price of wearing this orb — the corruption the Church
    // rite burns away. Refined = purified = free to carry.
    // Scales with LEVEL (David's tuning 7/08): base% x level, so the greed
    // that buys +5 damage/+5 STR buys 5x the madness with it. Bases: common
    // single-target 1, strong single 2, AoE/status 2-3, passives 1, boss 4-5.
    public int GetInsanityCost()
    {
        if (definition == null || isRefined) return 0;
        return definition.insanityCost * level;
    }

    public string DisplayName()
    {
        if (definition == null) return "???";
        string prefix = isRefined ? "Holy " : "";
        string lvlTag = level > 1 ? $" +{level - 1}" : "";
        return $"{prefix}{definition.skillName}{lvlTag}";
    }
}
