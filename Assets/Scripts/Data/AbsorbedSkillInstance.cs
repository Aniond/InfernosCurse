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

    // Duplicates needed per level: 1, 3, 6, 10, 15 (triangular)
    static readonly int[] DuplicatesForLevel = { 0, 1, 3, 6, 10, 15 };

    public void AddDuplicate()
    {
        duplicateCount++;
        RefreshLevel();
    }

    void RefreshLevel()
    {
        int maxLvl = definition != null ? definition.maxLevel : 5;
        // Clamp into [1, table max] — a maxLevel of 0 would otherwise lock level at 1 forever.
        maxLvl = Mathf.Clamp(maxLvl, 1, DuplicatesForLevel.Length - 1);
        for (int i = maxLvl; i >= 1; i--)
        {
            if (duplicateCount >= DuplicatesForLevel[i])
            {
                level = i;
                return;
            }
        }
        level = 1;
    }

    public bool CanRefine()
    {
        return !isRefined
            && definition != null
            && definition.refinable
            && definition.holyVersion != null
            && level >= definition.maxLevel;
    }

    // Power at current level: basePower * (1 + 0.2 * (level-1)) * multiplier from holy if refined
    public float GetEffectivePower()
    {
        if (definition == null) return 0f;
        float lvlMult  = 1f + 0.2f * (level - 1);
        float holyMult = isRefined ? 1.5f : 1f;
        return definition.basePower * lvlMult * holyMult;
    }

    public string DisplayName()
    {
        if (definition == null) return "???";
        string prefix = isRefined ? "Holy " : "";
        string lvlTag = level > 1 ? $" +{level - 1}" : "";
        return $"{prefix}{definition.skillName}{lvlTag}";
    }
}
