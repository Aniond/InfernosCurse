using UnityEngine;

public enum SkillType { Active, Passive, Reaction, Movement }
public enum DamageType { Physical, Fire, Ice, Lightning, Holy, Dark, Poison, None }
public enum StatScaling { Strength, Dexterity, Constitution, Creativity, Faith, Perception, Speed, None }

[CreateAssetMenu(fileName = "Skill_New", menuName = "InfernosCurse/Skill Definition")]
public class SkillDefinition : ScriptableObject
{
    [Header("Identity")]
    public string skillName;
    [TextArea(2, 4)]
    public string description;
    public Sprite icon;
    public SkillType skillType;
    public DamageType damageType;

    [Tooltip("If true, this skill restores HP to its target(s). Without this, a " +
             "skill with damageType None is treated as a non-damaging utility/buff " +
             "rather than a heal.")]
    public bool isHealing = false;

    [Header("AP Cost")]
    public int apCost = 100;

    [Header("Scaling")]
    public StatScaling primaryStat = StatScaling.Strength;
    [Range(0f, 5f)] public float scalingMultiplier = 1.0f;
    public int basePower = 10;

    [Header("Range & Area")]
    [Tooltip("Minimum target distance. 0 = can target self / own tile.")]
    public int minRange = 1;
    public int range = 1;
    public int areaOfEffect = 0;
    public bool requiresLineOfSight = true;

    [Header("CT (Charge Time)")]
    [Tooltip("Ticks added to CT bar before this action fires. 0 = instant.")]
    public int chargeTicks = 0;

    [Header("SP Cost")]
    public int spCost = 0;

    [Header("Applied Status (optional)")]
    [Tooltip("If true, a successful use also inflicts a StatusEffect on the target " +
             "(in addition to any tile-based status). Lets a skill apply its own DoT / " +
             "debuff — e.g. ergot poison, oven burn — without relying on the target's tile.")]
    public bool appliesStatus = false;
    public StatusEffectType statusType;
    [Tooltip("Duration in target-turns (status ticks on the afflicted unit's turn start).")]
    public int statusDuration = 3;
    [Tooltip("DoT fraction (0-1 of HP per tick) for Poison/Burn/Regen, or reduction " +
             "amount (0-1) for Blind/Protect/Shell.")]
    [Range(0f, 1f)] public float statusMagnitude = 0.05f;
    [Tooltip("Chance (0-1) the status procs on a successful hit.")]
    [Range(0f, 1f)] public float statusChance = 1f;

    [Header("Absorbable — Dante Only")]
    public bool isAbsorbable = true;
    [Tooltip("Max times this skill can be leveled via duplicate drops.")]
    public int maxLevel = 5;
    [Tooltip("Can be refined into a Holy version at the Church.")]
    public bool refinable = true;
    public SkillDefinition holyVersion;
}
