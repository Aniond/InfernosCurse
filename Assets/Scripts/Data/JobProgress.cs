using UnityEngine;
using System;
using System.Collections.Generic;

[Serializable]
public class LearnedJobSkill
{
    public SkillDefinition skill;
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

    // Returns stat totals contributed by this job at current level.
    // IMPORTANT: contributions are ADDED to base stats, so the "no job" case and
    // all untouched fields must be ZERO — `new CharacterStats()` is NOT zero
    // (its field initializers are gameplay defaults: 100 HP, 12 STR…). Unity's
    // serializer also inflates a null JobProgress into a default instance when a
    // CombatantData is cloned, so job==null is the NORMAL case for enemies.
    public CharacterStats GetJobStatContribution()
    {
        var contribution = ZeroStats();
        if (job == null) return contribution;

        var g  = job.statGainPerLevel;
        var b  = job.baseStatBonus;
        int lv = jobLevel - 1; // gains applied for levels completed

        contribution.strength     = b.strength     + g.strength     * lv;
        contribution.dexterity    = b.dexterity    + g.dexterity    * lv;
        contribution.constitution = b.constitution + g.constitution * lv;
        contribution.creativity   = b.creativity   + g.creativity   * lv;
        contribution.faith        = b.faith        + g.faith        * lv;
        contribution.perception   = b.perception   + g.perception   * lv;
        contribution.speed        = b.speed        + g.speed        * lv;
        contribution.hpMax        = b.hpMax        + g.hpMax        * lv;
        contribution.spMax        = b.spMax        + g.spMax        * lv;
        return contribution;
    }

    // A genuinely zeroed stat block (field initializers make new CharacterStats() non-zero)
    static CharacterStats ZeroStats() => new CharacterStats
    {
        level = 0,
        hp = 0, hpMax = 0, sp = 0, spMax = 0, xp = 0, xpNext = 0,
        strength = 0, dexterity = 0, constitution = 0, creativity = 0,
        faith = 0, perception = 0, speed = 0,
    };

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
                skill    = entry.skill,
                unlocked = false
            });
    }
}
