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

    [Header("AP Cost")]
    public int apCost = 100;

    [Header("Scaling")]
    public StatScaling primaryStat = StatScaling.Strength;
    [Range(0f, 5f)] public float scalingMultiplier = 1.0f;
    public int basePower = 10;

    [Header("Range & Area")]
    public int range = 1;
    public int areaOfEffect = 0;
    public bool requiresLineOfSight = true;

    [Header("CT (Charge Time)")]
    [Tooltip("Ticks added to CT bar before this action fires. 0 = instant.")]
    public int chargeTicks = 0;

    [Header("SP Cost")]
    public int spCost = 0;

    [Header("Absorbable — Dante Only")]
    public bool isAbsorbable = true;
    [Tooltip("Max times this skill can be leveled via duplicate drops.")]
    public int maxLevel = 5;
    [Tooltip("Can be refined into a Holy version at the Church.")]
    public bool refinable = true;
    public SkillDefinition holyVersion;
}
