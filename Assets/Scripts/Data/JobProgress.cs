using UnityEngine;
using System;
using System.Collections.Generic;

[Serializable]
public class LearnedJobSkill
{
    public SkillDefinition skill;
    public int apInvested;
    public bool unlocked;
}

[Serializable]
public class JobProgress
{
    public JobDefinition job;
    public int jobLevel   = 1;
    public int currentXP  = 0;
    public int currentAP  = 0;

    public List<LearnedJobSkill> learnedSkills = new List<LearnedJobSkill>();

    // Returns stat totals contributed by this job at current level
    public CharacterStats GetJobStatContribution()
    {
        if (job == null) return new CharacterStats();
        var g  = job.statGainPerLevel;
        var b  = job.baseStatBonus;
        int lv = jobLevel - 1; // gains applied for levels completed
        return new CharacterStats
        {
            strength     = b.strength     + g.strength     * lv,
            dexterity    = b.dexterity    + g.dexterity    * lv,
            constitution = b.constitution + g.constitution * lv,
            creativity   = b.creativity   + g.creativity   * lv,
            faith        = b.faith        + g.faith        * lv,
            perception   = b.perception   + g.perception   * lv,
            speed        = b.speed        + g.speed        * lv,
            hpMax        = b.hpMax        + g.hpMax        * lv,
            spMax        = b.spMax        + g.spMax        * lv,
        };
    }

    // Add XP, level up if threshold met. Returns true if leveled.
    public bool AddXP(int amount)
    {
        if (job == null) return false;
        currentXP += amount;
        bool leveled = false;
        while (jobLevel - 1 < job.xpPerLevel.Length
               && currentXP >= job.xpPerLevel[jobLevel - 1])
        {
            currentXP -= job.xpPerLevel[jobLevel - 1];
            jobLevel++;
            leveled = true;
        }
        return leveled;
    }

    public void AddAP(int amount) => currentAP += amount;

    // Spend AP on a skill. Returns true if successful.
    public bool SpendAP(SkillDefinition skill)
    {
        var entry = GetEntry(skill);
        if (entry == null || entry.unlocked) return false;
        if (currentAP < entry.apInvested) return false; // shouldn't happen
        // Find cost from job definition
        int cost = GetAPCost(skill);
        if (currentAP < cost) return false;
        currentAP     -= cost;
        entry.unlocked = true;
        return true;
    }

    public bool IsUnlocked(SkillDefinition skill)
    {
        var e = GetEntry(skill); return e != null && e.unlocked;
    }

    public int GetAPCost(SkillDefinition skill)
    {
        if (job == null) return 0;
        foreach (var entry in job.skills)
            if (entry.skill == skill) return entry.apCost;
        return 0;
    }

    public bool CanAdvance()
    {
        return job != null
            && job.advancedJobs != null
            && job.advancedJobs.Length > 0
            && jobLevel >= job.advancedJobUnlockLevel;
    }

    LearnedJobSkill GetEntry(SkillDefinition skill)
    {
        foreach (var e in learnedSkills)
            if (e.skill == skill) return e;
        return null;
    }

    // Call once when this job is first added to a combatant
    public void Initialize()
    {
        if (job == null) return;
        learnedSkills.Clear();
        foreach (var entry in job.skills)
            learnedSkills.Add(new LearnedJobSkill
            {
                skill       = entry.skill,
                apInvested  = 0,
                unlocked    = false
            });
    }
}
