using UnityEngine;

[System.Serializable]
public class JobStatGain
{
    [Tooltip("Stat gains applied each time this job levels up.")]
    public int strength;
    public int dexterity;
    public int constitution;
    public int creativity;
    public int faith;
    public int perception;
    public int speed;
    public int hpMax;
    public int spMax;
}

[System.Serializable]
public class JobSkillEntry
{
    public SkillDefinition skill;
    [Tooltip("AP required to unlock this skill.")]
    public int apCost = 100;
    [Tooltip("Job level required before this skill can be purchased.")]
    public int jobLevelRequired = 1;
    [Tooltip("Tier within the skill tree — 1 = base, 2 = mid, 3 = advanced.")]
    public int tier = 1;
}

[CreateAssetMenu(fileName = "Job_New", menuName = "InfernosCurse/Job Definition")]
public class JobDefinition : ScriptableObject
{
    [Header("Identity")]
    public string jobName;
    [TextArea(2, 4)]
    public string description;
    public Sprite jobIcon;

    [Header("Lineage")]
    [Tooltip("The root profession this job belongs to (e.g. Baker, Butcher). Leave null for base jobs.")]
    public JobDefinition lineageRoot;
    [Tooltip("Jobs this one can advance into when unlock conditions are met.")]
    public JobDefinition[] advancedJobs;
    [Tooltip("Job level required to unlock advanced jobs.")]
    public int advancedJobUnlockLevel = 5;

    [Header("XP Curve")]
    [Tooltip("XP needed to reach each job level. Index 0 = level 1→2, index 1 = level 2→3, etc.")]
    public int[] xpPerLevel = { 100, 200, 350, 550, 800, 1100, 1500, 2000, 2600, 3300 };

    [Header("Stat Gains Per Level")]
    public JobStatGain statGainPerLevel;

    [Header("Base Stats (added when job is equipped)")]
    public JobStatGain baseStatBonus;

    [Header("Skill Tree")]
    public JobSkillEntry[] skills;
}
