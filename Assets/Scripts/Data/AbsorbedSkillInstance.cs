using UnityEngine;
using System;

[Serializable]
public class AbsorbedSkillInstance
{
    public SkillDefinition definition;

    [Tooltip("Number of duplicate drops received. Drives level.")]
    public int duplicateCount = 1;

    [Tooltip("Current level, capped at definition.maxLevel.")]
    public int level = 1;

    [Tooltip("True if this skill has been refined into its Holy version at the Church.")]
    public bool isRefined = false;

    // David's hierarchy (7/08): every duplicate drop levels the skill by 1,
    // capped at definition.maxLevel. (The old triangular curve — 15 dupes to
    // max — is parked until the balance pass.)
    public void AddDuplicate()
    {
        duplicateCount++;
        RefreshLevel();
    }

    void RefreshLevel()
    {
        int maxLvl = Mathf.Max(1, definition != null ? definition.maxLevel : 5);
        level = Mathf.Clamp(duplicateCount, 1, maxLvl);
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

    public string DisplayName()
    {
        if (definition == null) return "???";
        string prefix = isRefined ? "Holy " : "";
        string lvlTag = level > 1 ? $" +{level - 1}" : "";
        return $"{prefix}{definition.skillName}{lvlTag}";
    }
}
