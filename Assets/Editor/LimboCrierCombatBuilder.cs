using System;
using UnityEditor;
using UnityEngine;

public static class LimboCrierCombatBuilder
{
    public const string SkillRoot = "Assets/Resources/Skills/LimboCrier";
    public const string EquipmentRoot = "Assets/Resources/Equipment/LimboCrier";
    public const string CombatantRoot = "Assets/Resources/Combatants/LimboCrier";

    public const string JabPath = SkillRoot + "/Skill_BellHookJab.asset";
    public const string KnellPath = SkillRoot + "/Skill_KnellOfDread.asset";
    public const string VigilancePath = SkillRoot + "/Skill_BellOfVigilance.asset";
    public const string BenedictionPath = SkillRoot + "/Skill_CrookedBenediction.asset";
    public const string HookPath = SkillRoot + "/Skill_PilgrimsHook.asset";
    public const string RescuePath = SkillRoot + "/Skill_PilgrimsRescue.asset";

    public const string StaffPath = EquipmentRoot + "/Item_BellHookStaff.asset";
    public const string JackPath = EquipmentRoot + "/Item_CriersJack.asset";
    public const string ReliquaryPath = EquipmentRoot + "/Item_FalseReliquary.asset";

    public const string ProfilePath = CombatantRoot + "/HumanoidVisual_LimboCrier.asset";
    public const string CombatantPath = CombatantRoot + "/Enemy_LimboCrier.asset";

    [MenuItem("InfernosCurse/Limbo Crier/Build Combat Foundation")]
    public static void Build()
    {
        EnsureFolder(SkillRoot);
        EnsureFolder(EquipmentRoot);
        EnsureFolder(CombatantRoot);

        SkillDefinition jab = LoadOrCreate<SkillDefinition>(JabPath);
        SkillDefinition knell = LoadOrCreate<SkillDefinition>(KnellPath);
        SkillDefinition vigilance = LoadOrCreate<SkillDefinition>(VigilancePath);
        SkillDefinition benediction = LoadOrCreate<SkillDefinition>(BenedictionPath);
        SkillDefinition hook = LoadOrCreate<SkillDefinition>(HookPath);
        SkillDefinition rescue = LoadOrCreate<SkillDefinition>(RescuePath);

        ConfigureJab(jab);
        ConfigureKnell(knell, vigilance);
        ConfigureVigilance(vigilance);
        ConfigureBenediction(benediction);
        ConfigureHook(hook, rescue);
        ConfigureRescue(rescue);

        EquipmentDefinition staff = LoadOrCreate<EquipmentDefinition>(StaffPath);
        EquipmentDefinition jack = LoadOrCreate<EquipmentDefinition>(JackPath);
        EquipmentDefinition reliquary = LoadOrCreate<EquipmentDefinition>(ReliquaryPath);
        ConfigureStaff(staff);
        ConfigureJack(jack);
        ConfigureReliquary(reliquary);

        HumanoidBattleVisualProfile profile =
            LoadOrCreate<HumanoidBattleVisualProfile>(ProfilePath);
        ConfigureProfile(profile, jab, knell, benediction, hook);

        CombatantData crier = LoadOrCreate<CombatantData>(CombatantPath);
        ConfigureCombatant(crier, profile, staff, jack, reliquary,
            jab, knell, benediction, hook);

        SkillDefinition basicAttack = AssetDatabase.LoadAssetAtPath<SkillDefinition>(
            "Assets/Data/Skills/Skill_BasicAttack.asset");
        if (basicAttack != null)
        {
            basicAttack.usesEquippedWeapon = true;
            EditorUtility.SetDirty(basicAttack);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        LimboCrierCombatValidator.ValidateFoundationAssets();
        Debug.Log("[LimboCrierCombatBuilder] Combat foundation built idempotently.");
    }

    static void ConfigureJab(SkillDefinition skill)
    {
        skill.skillName = "Bell-Hook Jab";
        skill.description = "A short jab with the cracked bell-hook. The equipped weapon determines its element.";
        ConfigureActive(skill, DamageType.Physical, 8, StatScaling.Strength, 1f,
            0.90f, 1, 1, 0, true, 0, 0);
        skill.targetSide = SkillTargetSide.Hostile;
        skill.usesEquippedWeapon = true;
        skill.isAbsorbable = false;
        skill.maxLevel = 1;
        skill.refinable = false;
        skill.holyVersion = null;
        skill.bonusStat = StatScaling.None;
        skill.insanityCost = 0;
        ClearStatus(skill);
        skill.specialEffect = SkillSpecialEffect.None;
        Dirty(skill);
    }

    static void ConfigureKnell(SkillDefinition skill, SkillDefinition holy)
    {
        skill.skillName = "Knell of Dread";
        skill.description = "Ring the cracked bell. Hostiles within two cells feel the end drawing near.";
        ConfigureActive(skill, DamageType.None, 0, StatScaling.Faith, 0f,
            1f, 0, 0, 2, false, 12, 9);
        skill.targetSide = SkillTargetSide.Hostile;
        skill.centerOnCaster = true;
        skill.allowSelfTarget = false;
        skill.appliesStatus = true;
        skill.statusType = StatusEffectType.Dread;
        skill.statusDuration = 2;
        skill.statusMagnitude = 3f;
        skill.statusChance = 1f;
        skill.isAbsorbable = true;
        skill.maxLevel = 5;
        skill.refinable = true;
        skill.holyVersion = holy;
        skill.bonusStat = StatScaling.Faith;
        skill.bonusStatPerLevel = 1;
        skill.insanityCost = 2;
        skill.specialEffect = SkillSpecialEffect.None;
        Dirty(skill);
    }

    static void ConfigureVigilance(SkillDefinition skill)
    {
        skill.skillName = "Bell of Vigilance";
        skill.description = "A clear answering bell removes Dread and quickens allied CT recovery by 10%.";
        ConfigureActive(skill, DamageType.None, 0, StatScaling.Faith, 0f,
            1f, 0, 0, 2, false, 12, 9);
        skill.targetSide = SkillTargetSide.Allied;
        skill.centerOnCaster = true;
        skill.allowSelfTarget = true;
        skill.appliesStatus = true;
        skill.statusType = StatusEffectType.Haste;
        skill.statusDuration = 2;
        skill.statusMagnitude = 0.10f;
        skill.statusChance = 1f;
        skill.isAbsorbable = false;
        skill.maxLevel = 1;
        skill.refinable = false;
        skill.holyVersion = null;
        skill.bonusStat = StatScaling.None;
        skill.insanityCost = 0;
        skill.specialEffect = SkillSpecialEffect.RemoveDread;
        Dirty(skill);
    }

    static void ConfigureBenediction(SkillDefinition skill)
    {
        skill.skillName = "Crooked Benediction";
        skill.description = "Bless an ally with violent certainty. Power rises, but every completed action stains the ground.";
        ConfigureActive(skill, DamageType.None, 0, StatScaling.Creativity, 0f,
            1f, 1, 3, 0, true, 8, 8);
        skill.targetSide = SkillTargetSide.Allied;
        skill.centerOnCaster = false;
        skill.allowSelfTarget = false;
        skill.appliesStatus = true;
        skill.statusType = StatusEffectType.FalseZeal;
        skill.statusDuration = 2;
        skill.statusMagnitude = 0.20f;
        skill.statusChance = 1f;
        skill.isAbsorbable = false;
        skill.maxLevel = 1;
        skill.refinable = false;
        skill.holyVersion = null;
        skill.bonusStat = StatScaling.None;
        skill.insanityCost = 0;
        skill.specialEffect = SkillSpecialEffect.None;
        Dirty(skill);
    }

    static void ConfigureHook(SkillDefinition skill, SkillDefinition holy)
    {
        skill.skillName = "Pilgrim's Hook";
        skill.description = "Catch a hostile exactly two cells away and attempt to drag them one valid cell closer.";
        ConfigureActive(skill, DamageType.Physical, 7, StatScaling.Strength, 1f,
            0.82f, 2, 2, 0, true, 0, 5);
        skill.targetSide = SkillTargetSide.Hostile;
        skill.isAbsorbable = true;
        skill.maxLevel = 5;
        skill.refinable = true;
        skill.holyVersion = holy;
        skill.bonusStat = StatScaling.Perception;
        skill.bonusStatPerLevel = 1;
        skill.insanityCost = 2;
        ClearStatus(skill);
        skill.specialEffect = SkillSpecialEffect.PullTargetTowardCaster;
        Dirty(skill);
    }

    static void ConfigureRescue(SkillDefinition skill)
    {
        skill.skillName = "Pilgrim's Rescue";
        skill.description = "Pull an ally one valid cell toward the caster and grant Protect for one turn.";
        ConfigureActive(skill, DamageType.None, 0, StatScaling.Faith, 0f,
            1f, 2, 2, 0, true, 0, 5);
        skill.targetSide = SkillTargetSide.Allied;
        skill.isAbsorbable = false;
        skill.maxLevel = 1;
        skill.refinable = false;
        skill.holyVersion = null;
        skill.bonusStat = StatScaling.None;
        skill.insanityCost = 0;
        ClearStatus(skill);
        skill.specialEffect = SkillSpecialEffect.PullAllyTowardCasterAndProtect;
        Dirty(skill);
    }

    static void ConfigureStaff(EquipmentDefinition item)
    {
        item.itemName = "Bell-Hook Staff";
        item.description = "An iron-shod preaching staff combining a shepherd's hook, short polearm, and cracked handbell.";
        item.slot = EquipSlot.Weapon;
        item.bonuses = EmptyStats();
        item.bonuses.strength = 1;
        item.bonuses.perception = 1;
        item.damageType = DamageType.Dark;
        item.attackPowerBonus = 0;
        item.damageReceivedModifiers = Array.Empty<DamageReceivedModifier>();
        item.valueFlorins = 42;
        Dirty(item);
    }

    static void ConfigureJack(EquipmentDefinition item)
    {
        item.itemName = "Crier's Jack";
        item.description = "A patched quilted gambeson concealed beneath a penitential robe.";
        item.slot = EquipSlot.Armor;
        item.bonuses = EmptyStats();
        item.bonuses.constitution = 2;
        item.damageType = DamageType.Physical;
        item.attackPowerBonus = 0;
        item.damageReceivedModifiers = new[]
        {
            new DamageReceivedModifier { damageType = DamageType.Dark, percentage = -0.10f },
        };
        item.valueFlorins = 36;
        Dirty(item);
    }

    static void ConfigureReliquary(EquipmentDefinition item)
    {
        item.itemName = "False Reliquary";
        item.description = "A counterfeit holy container holding a sliver of Limbo-corrupted material.";
        item.slot = EquipSlot.Accessory;
        item.bonuses = EmptyStats();
        item.bonuses.creativity = 1;
        item.bonuses.faith = 1;
        item.damageType = DamageType.Physical;
        item.attackPowerBonus = 0;
        item.damageReceivedModifiers = new[]
        {
            new DamageReceivedModifier { damageType = DamageType.Holy, percentage = 0.10f },
        };
        item.valueFlorins = 58;
        Dirty(item);
    }

    static void ConfigureProfile(
        HumanoidBattleVisualProfile profile,
        SkillDefinition jab,
        SkillDefinition knell,
        SkillDefinition benediction,
        SkillDefinition hook)
    {
        profile.provider = "PixelLab";
        profile.identityPrompt = "Common Florentine Limbo doomsayer in a soot-dark penitential robe and patched jack, iron half-mask, cracked bell-hook staff, false reliquary, restrained violet corruption light, hard dark HD-2D pixel outline.";
        profile.walk ??= Sequence(6, 10f);
        profile.walk.expectedFramesPerDirection = 6;
        profile.walk.fps = 10f;
        profile.hurt ??= Sequence(4, 12f);
        profile.hurt.expectedFramesPerDirection = 4;
        profile.hurt.fps = 12f;
        profile.death ??= Sequence(9, 12f);
        profile.death.expectedFramesPerDirection = 9;
        profile.death.fps = 12f;
        profile.skills = new[]
        {
            SkillAnimation(profile.skills, jab, 9),
            SkillAnimation(profile.skills, knell, 9),
            SkillAnimation(profile.skills, benediction, 9),
            SkillAnimation(profile.skills, hook, 9),
        };
        Dirty(profile);
    }

    static void ConfigureCombatant(
        CombatantData crier,
        HumanoidBattleVisualProfile profile,
        EquipmentDefinition staff,
        EquipmentDefinition jack,
        EquipmentDefinition reliquary,
        SkillDefinition jab,
        SkillDefinition knell,
        SkillDefinition benediction,
        SkillDefinition hook)
    {
        crier.displayName = "Limbo Crier";
        crier.backstory = "A common doomsayer of Limbo who turns fear, hunger, and civic grievance into sermons of inevitable ruin.";
        crier.role = CombatantRole.Enemy;
        crier.combatAIProfile = CombatAIProfile.LimboCrier;
        crier.isLimboAligned = true;
        crier.resistsDreadCtPenalty = false;
        crier.immovable = false;
        crier.protectedFromForcedMovement = false;
        crier.humanoidVisualProfile = profile;
        crier.battleVisualScale = 1.15f;
        crier.battleVisualOffset = new Vector2(0f, 0.72f);
        crier.baseStats = new CharacterStats
        {
            characterName = "Limbo Crier",
            characterClass = "Doomsayer",
            level = 2,
            hp = 72,
            hpMax = 72,
            sp = 36,
            spMax = 36,
            xp = 0,
            xpNext = 100,
            strength = 7,
            dexterity = 8,
            constitution = 7,
            creativity = 11,
            faith = 10,
            perception = 10,
            speed = 8,
        };
        crier.intelligence = 5;
        crier.sightRange = 12f;
        crier.eyeHeight = 2;
        crier.weapon = staff;
        crier.armor = jack;
        crier.accessory = reliquary;
        crier.helmet = null;
        crier.gloves = null;
        crier.boots = null;
        crier.equippedSkills ??= new SkillSlots();
        crier.equippedSkills.actives = new[] { jab, knell, benediction, hook };
        crier.equippedSkills.passives = new SkillDefinition[3];
        crier.equippedSkills.absorbed = new AbsorbedSkillInstance[3];
        crier.learnableSkills = new[] { knell, hook };
        crier.skillDropChance = 0.25f;
        Dirty(crier);
    }

    static void ConfigureActive(
        SkillDefinition skill,
        DamageType damageType,
        int power,
        StatScaling scaling,
        float multiplier,
        float hit,
        int minRange,
        int range,
        int area,
        bool lineOfSight,
        int charge,
        int sp)
    {
        skill.skillType = SkillType.Active;
        skill.damageType = damageType;
        skill.isHealing = false;
        skill.apCost = 100;
        skill.primaryStat = scaling;
        skill.scalingMultiplier = multiplier;
        skill.basePower = power;
        skill.baseHit = hit;
        skill.minRange = minRange;
        skill.range = range;
        skill.areaOfEffect = area;
        skill.requiresLineOfSight = lineOfSight;
        skill.chargeTicks = charge;
        skill.spCost = sp;
        skill.centerOnCaster = false;
        skill.allowSelfTarget = false;
        skill.usesEquippedWeapon = false;
        skill.specialEffect = SkillSpecialEffect.None;
    }

    static void ClearStatus(SkillDefinition skill)
    {
        skill.appliesStatus = false;
        skill.statusType = StatusEffectType.Poison;
        skill.statusDuration = 3;
        skill.statusMagnitude = 0.05f;
        skill.statusChance = 1f;
    }

    static CharacterStats EmptyStats() => new CharacterStats
    {
        characterName = string.Empty,
        characterClass = string.Empty,
        level = 0,
        hp = 0,
        hpMax = 0,
        sp = 0,
        spMax = 0,
        xp = 0,
        xpNext = 0,
        strength = 0,
        dexterity = 0,
        constitution = 0,
        creativity = 0,
        faith = 0,
        perception = 0,
        speed = 0,
    };

    static HumanoidDirectionalSequence Sequence(int expected, float fps) => new()
    {
        expectedFramesPerDirection = expected,
        fps = fps,
        south = Array.Empty<Sprite>(),
        east = Array.Empty<Sprite>(),
        north = Array.Empty<Sprite>(),
        west = Array.Empty<Sprite>(),
    };

    static HumanoidSkillAnimation SkillAnimation(
        HumanoidSkillAnimation[] existing,
        SkillDefinition skill,
        int expected)
    {
        if (existing != null)
            foreach (HumanoidSkillAnimation animation in existing)
                if (animation != null && animation.skill == skill)
                {
                    animation.sequence ??= Sequence(expected, 12f);
                    animation.sequence.expectedFramesPerDirection = expected;
                    animation.sequence.fps = 12f;
                    return animation;
                }
        return new HumanoidSkillAnimation
        {
            skill = skill,
            sequence = Sequence(expected, 12f),
        };
    }

    static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    static void Dirty(UnityEngine.Object asset) => EditorUtility.SetDirty(asset);
}
