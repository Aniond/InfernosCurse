using UnityEngine;

public enum SkillType { Active, Passive, Reaction, Movement }
public enum DamageType { Physical, Fire, Ice, Lightning, Holy, Dark, Poison, None }
public enum StatScaling { Strength, Dexterity, Constitution, Creativity, Faith, Perception, Speed, None }

// Automatic preserves every legacy skill: heals target allies, everything else
// targets hostiles. New utility skills can opt into an explicit side.
public enum SkillTargetSide { Automatic, Hostile, Allied }

// Narrow, append-only hooks for effects that cannot be represented by ordinary
// damage/status fields. This is deliberately not a general scripting language.
public enum SkillSpecialEffect
{
    None,
    PullTargetTowardCaster,
    RemoveDread,
    PullAllyTowardCasterAndProtect,
}

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

    [Header("Targeting")]
    public SkillTargetSide targetSide = SkillTargetSide.Automatic;
    [Tooltip("Centers the area on the caster regardless of the selected cell.")]
    public bool centerOnCaster;
    [Tooltip("Allows the caster to be included when the resolved target side is Allied.")]
    public bool allowSelfTarget;

    [Header("Weapon & Special Effect")]
    [Tooltip("Uses the equipped weapon's damage type and attack-power bonus.")]
    public bool usesEquippedWeapon;
    public SkillSpecialEffect specialEffect = SkillSpecialEffect.None;

    [Header("AP Cost")]
    public int apCost = 100;

    [Header("Scaling")]
    public StatScaling primaryStat = StatScaling.Strength;
    [Range(0f, 5f)] public float scalingMultiplier = 1.0f;
    public int basePower = 10;

    [Header("Base Hit")]
    [Tooltip("Base chance to land (0-1) before Perception/Dexterity/Speed modifiers. Utility skills (damageType None) always hit.")]
    [Range(0.05f, 1f)] public float baseHit = 0.85f;

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
    [Tooltip("Effect magnitude. Usually a 0-1 fraction; Dread stores its flat Faith penalty.")]
    [Min(0f)] public float statusMagnitude = 0.05f;
    [Tooltip("Chance (0-1) the status procs on a successful hit.")]
    [Range(0f, 1f)] public float statusChance = 1f;

    [Header("Absorbable — Benidito Only")]
    public bool isAbsorbable = true;
    [Tooltip("Max times this skill can be leveled via duplicate drops.")]
    public int maxLevel = 5;
    [Tooltip("Can be refined into a Holy version at the Church.")]
    public bool refinable = true;
    public SkillDefinition holyVersion;

    [Header("Absorbed Growth (David's hierarchy: each duplicate drop = +1 level)")]
    [Tooltip("Stat granted while the orb is EQUIPPED, per level — Vine Slash gives Strength (+1/level to +5 at max). None = no stat bonus.")]
    public StatScaling bonusStat = StatScaling.None;
    [Tooltip("Stat points per level. Level x this = the equipped bonus (cap = maxLevel x this).")]
    public int bonusStatPerLevel = 1;

    [Header("Corruption — the insanity price (CoC-style storytelling layer)")]
    [Tooltip("BASE percentage points of insanity per orb LEVEL (total = base x level, 0-100 scale) carried by EQUIPPING this skill unrefined. David's bands: common single-target 1, strong single 2, AoE/status 2-3, passives 1, signature/boss 4-5. Church refinement burns it to zero. Never shown to the player — the world just... changes.")]
    [Range(0, 20)]
    public int insanityCost = 1;
}
