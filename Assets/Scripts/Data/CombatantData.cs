using UnityEngine;
using System;
using System.Collections.Generic;

public enum CombatantRole { Benidito, PartyMember, Enemy, NPC }

[Serializable]
public class SkillSlots
{
    [Tooltip("4 active skill slots.")]
    public SkillDefinition[] actives  = new SkillDefinition[4];
    [Tooltip("3 passive skill slots.")]
    public SkillDefinition[] passives = new SkillDefinition[3];
    [Tooltip("3 absorbed-skill slots (Benidito only). Separate from actives so " +
             "absorption never competes with job-learned skills for a slot.")]
    public AbsorbedSkillInstance[] absorbed = new AbsorbedSkillInstance[3];

    public bool EquipActive(SkillDefinition skill, int slot)
    {
        if (slot < 0 || slot >= actives.Length) return false;
        actives[slot] = skill;
        return true;
    }

    public bool EquipPassive(SkillDefinition skill, int slot)
    {
        if (slot < 0 || slot >= passives.Length) return false;
        passives[slot] = skill;
        return true;
    }

    public bool EquipAbsorbed(AbsorbedSkillInstance instance, int slot)
    {
        if (slot < 0 || slot >= absorbed.Length) return false;
        absorbed[slot] = instance;
        return true;
    }

    public void UnequipAbsorbed(AbsorbedSkillInstance instance)
    {
        for (int i = 0; i < absorbed.Length; i++)
            if (absorbed[i] == instance) absorbed[i] = null;
    }
}

[CreateAssetMenu(fileName = "Combatant_New", menuName = "InfernosCurse/Combatant Data")]
public class CombatantData : ScriptableObject
{
    [Header("Identity")]
    public string displayName;
    [TextArea(2, 3)]
    public string backstory;
    public Sprite portrait;
    [Tooltip("Battlefield billboard sprite — the spawner applies it to the unit's SpriteRenderer (clones inherit it). Empty = prefab default.")]
    public Sprite battleSprite;
    [Tooltip("Battlefield sprite tint — FFT-style palette swap for enemy variants sharing a sprite. White = untinted.")]
    public Color battleTint = Color.white;
    public CombatantRole role;

    [Header("Base Stats (before job bonuses)")]
    public CharacterStats baseStats = new CharacterStats();

    [Header("Combat Intelligence (1-10 — how much strategy this creature CAN use)")]
    [Range(1, 10)]
    [Tooltip("1-2 beast: rushes, no cover. 3-4 cunning: flanks, retreats hurt. 5-6 soldier: cover, picks weak targets. 7-8 tactician: feints, patience, coordinates. 9-10 strategist: full condition-shaping; in AI mode, Gemini drives it. An Interpreter-type unit can lift nearby lower-I allies.")]
    public int intelligence = 3;

    [Header("Vision (all sheets — player, party, enemies, NPCs)")]
    [Tooltip("Sight radius in grid cells (1 cell = 1m). Drives fog of war and spotting.")]
    public float sightRange = 13f;
    [Tooltip("Eye height in elevation half-units — objects rising taller than this above own ground block sight. 2 = human standing; tall units see over low cover. Enlarge/Shrink modify it temporarily.")]
    public int eyeHeight = 2;

    [Header("Current HP / SP (runtime)")]
    [NonSerialized] public int currentHP;
    [NonSerialized] public int currentSP;

    // ── Benidito-only ────────────────────────────────────────────────────────────
    [Header("Absorbed Skills (Benidito only)")]
    public List<AbsorbedSkillInstance> absorbedSkills = new List<AbsorbedSkillInstance>();

    // ── Party members ─────────────────────────────────────────────────────────
    [Header("Job System (Party Members)")]
    public List<JobProgress> jobHistory  = new List<JobProgress>(); // all jobs ever progressed
    public JobProgress       activeJob;                              // currently equipped job
    public JobProgress       secondaryJob;                          // secondary skill source

    [Header("Skill Slots (all combatants)")]
    public SkillSlots equippedSkills = new SkillSlots();

    [Header("Equipment")]
    public EquipmentDefinition weapon;
    public EquipmentDefinition armor;      // body
    public EquipmentDefinition accessory;  // ring / amulet / relic
    public EquipmentDefinition helmet;
    public EquipmentDefinition gloves;
    public EquipmentDefinition boots;

    public System.Collections.Generic.IEnumerable<EquipmentDefinition> AllEquipment()
    {
        if (weapon) yield return weapon;
        if (armor) yield return armor;
        if (accessory) yield return accessory;
        if (helmet) yield return helmet;
        if (gloves) yield return gloves;
        if (boots) yield return boots;
    }

    // ── Learnable skills (enemies / NPCs) ────────────────────────────────────
    [Header("Learnable Skills (Enemies / NPCs)")]
    [Tooltip("Skills this combatant can drop for Benidito to absorb on defeat.")]
    public SkillDefinition[] learnableSkills;
    [Range(0f, 1f)]
    [Tooltip("Base chance per kill that a skill drops.")]
    public float skillDropChance = 0.3f;

    // ── Computed stats ────────────────────────────────────────────────────────

    public CharacterStats GetTotalStats()
    {
        var total = Copy(baseStats);
        if (activeJob != null)
        {
            var jobBonus = activeJob.GetJobStatContribution();
            Add(ref total, jobBonus);
        }
        foreach (var item in AllEquipment())
            Add(ref total, item.bonuses);
        // Passive skill bonuses would be applied here later
        return total;
    }

    public void InitRuntime()
    {
        var stats  = GetTotalStats();
        currentHP  = stats.hpMax;
        currentSP  = stats.spMax;
    }

    // ── Absorb (Benidito) ────────────────────────────────────────────────────────

    public AbsorbedSkillInstance Absorb(SkillDefinition skill)
    {
        foreach (var s in absorbedSkills)
        {
            if (s.definition == skill)
            {
                s.AddDuplicate();
                return s;
            }
        }
        var newSkill = new AbsorbedSkillInstance { definition = skill };
        absorbedSkills.Add(newSkill);
        return newSkill;
    }

    public bool RefineAtChurch(AbsorbedSkillInstance skill)
    {
        if (!skill.CanRefine()) return false;

        // The rite is the Church's to grant: standing gates it, an offering
        // pays for it. Without a GuildSystem (battle-arena tests, tools) the
        // old ungated behavior stands so tests keep working.
        var guilds = GuildSystem.Instance;
        if (guilds != null)
        {
            if (!guilds.CanTransmute(out int offering))
            {
                Debug.Log("[Church] The Church does not yet trust you with such rites.");
                return false;
            }
            if (!FlorinWallet.TrySpend(offering, "transmutation offering"))
                return false;
        }
        else
        {
            Debug.LogWarning("[Church] No GuildSystem present — transmuting ungated (test context).");
        }

        skill.isRefined = true;
        Debug.Log($"[Church] {skill.DisplayName()} — the corruption is burned away.");
        return true;
    }

    // ── Job management ────────────────────────────────────────────────────────

    public JobProgress GetOrAddJob(JobDefinition job)
    {
        foreach (var jp in jobHistory)
            if (jp.job == job) return jp;

        var newJP = new JobProgress { job = job };
        newJP.Initialize();
        jobHistory.Add(newJP);
        return newJP;
    }

    public bool EquipJob(JobDefinition job)
    {
        // Enforce lineage — cannot equip a job outside this combatant's lineage
        if (!IsInLineage(job)) return false;
        activeJob = GetOrAddJob(job);
        return true;
    }

    public bool IsInLineage(JobDefinition job)
    {
        if (jobHistory.Count == 0) return true; // first job, always ok
        var root = GetLineageRoot(job);
        foreach (var jp in jobHistory)
        {
            if (GetLineageRoot(jp.job) == root) return true;
        }
        return false;
    }

    JobDefinition GetLineageRoot(JobDefinition job)
    {
        if (job == null) return null;
        return job.lineageRoot != null ? job.lineageRoot : job;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static CharacterStats Copy(CharacterStats s) => new CharacterStats
    {
        characterName  = s.characterName,
        characterClass = s.characterClass,
        level          = s.level,
        hp             = s.hp,   hpMax = s.hpMax,
        sp             = s.sp,   spMax = s.spMax,
        xp             = s.xp,   xpNext= s.xpNext,
        strength       = s.strength,
        dexterity      = s.dexterity,
        constitution   = s.constitution,
        creativity     = s.creativity,
        faith          = s.faith,
        perception     = s.perception,
        speed          = s.speed,
    };

    static void Add(ref CharacterStats a, CharacterStats b)
    {
        a.strength     += b.strength;
        a.dexterity    += b.dexterity;
        a.constitution += b.constitution;
        a.creativity   += b.creativity;
        a.faith        += b.faith;
        a.perception   += b.perception;
        a.speed        += b.speed;
        a.hpMax        += b.hpMax;
        a.spMax        += b.spMax;
    }
}
