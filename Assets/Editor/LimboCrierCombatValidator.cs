using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class LimboCrierCombatValidator
{
    [MenuItem("InfernosCurse/Validation/Validate Limbo Crier Combat Foundation")]
    public static void Validate()
    {
        var errors = new List<string>();
        ValidateFoundationAssets(errors);

        using (var fixture = new Fixture())
        {
            ValidateStatuses(fixture, errors);
            ValidateTargeting(fixture, errors);
            ValidateForcedMovement(fixture, errors);
            ValidateTemporaryTerrain(fixture, errors);
            ValidateVisualFallback(fixture, errors);
        }

        if (errors.Count > 0) Fail(errors);
        Debug.Log("[LimboCrierCombatValidator] Validation passed: exact data, two-turn statuses, targeting, forced movement, temporary-terrain restore, equipment modifiers, and humanoid fallback.");
    }

    public static void ValidateFoundationAssets()
    {
        var errors = new List<string>();
        ValidateFoundationAssets(errors);
        if (errors.Count > 0) Fail(errors);
        Debug.Log("[LimboCrierCombatValidator] Foundation assets match the approved Crier specification.");
    }

    public static void ValidateProductionVisualAssets()
    {
        var errors = new List<string>();
        ValidateFoundationAssets(errors);
        ValidateProductionVisualAssets(errors);
        if (errors.Count > 0) Fail(errors);
        Debug.Log("[LimboCrierCombatValidator] Production visuals match: 228 character images, portrait, five icons, source IDs, import settings, and Limbo Stain material.");
    }

    static void ValidateFoundationAssets(List<string> errors)
    {
        Expect((int)StatusEffectType.Dread == 12 && (int)StatusEffectType.FalseZeal == 13,
            "Dread/False Zeal were not appended after the legacy status values.", errors);

        SkillDefinition jab = Load<SkillDefinition>(LimboCrierCombatBuilder.JabPath, errors);
        SkillDefinition knell = Load<SkillDefinition>(LimboCrierCombatBuilder.KnellPath, errors);
        SkillDefinition vigilance = Load<SkillDefinition>(LimboCrierCombatBuilder.VigilancePath, errors);
        SkillDefinition benediction = Load<SkillDefinition>(LimboCrierCombatBuilder.BenedictionPath, errors);
        SkillDefinition hook = Load<SkillDefinition>(LimboCrierCombatBuilder.HookPath, errors);
        SkillDefinition rescue = Load<SkillDefinition>(LimboCrierCombatBuilder.RescuePath, errors);
        EquipmentDefinition staff = Load<EquipmentDefinition>(LimboCrierCombatBuilder.StaffPath, errors);
        EquipmentDefinition jack = Load<EquipmentDefinition>(LimboCrierCombatBuilder.JackPath, errors);
        EquipmentDefinition reliquary = Load<EquipmentDefinition>(LimboCrierCombatBuilder.ReliquaryPath, errors);
        HumanoidBattleVisualProfile profile =
            Load<HumanoidBattleVisualProfile>(LimboCrierCombatBuilder.ProfilePath, errors);
        CombatantData crier = Load<CombatantData>(LimboCrierCombatBuilder.CombatantPath, errors);
        if (jab == null || knell == null || vigilance == null || benediction == null ||
            hook == null || rescue == null || staff == null || jack == null ||
            reliquary == null || profile == null || crier == null) return;

        Expect(jab.skillName == "Bell-Hook Jab" && jab.usesEquippedWeapon &&
               jab.basePower == 8 && Near(jab.baseHit, 0.90f) &&
               jab.minRange == 1 && jab.range == 1 && jab.spCost == 0 &&
               !jab.isAbsorbable,
            "Bell-Hook Jab values drifted.", errors);

        Expect(knell.damageType == DamageType.None && knell.centerOnCaster &&
               knell.areaOfEffect == 2 && knell.chargeTicks == 12 && knell.spCost == 9 &&
               knell.appliesStatus && knell.statusType == StatusEffectType.Dread &&
               knell.statusDuration == 2 && Near(knell.statusMagnitude, 3f) &&
               knell.isAbsorbable && knell.maxLevel == 5 &&
               knell.bonusStat == StatScaling.Faith && knell.insanityCost == 2 &&
               knell.holyVersion == vigilance,
            "Knell of Dread values or Holy link drifted.", errors);

        Expect(vigilance.targetSide == SkillTargetSide.Allied && vigilance.centerOnCaster &&
               vigilance.allowSelfTarget && vigilance.statusType == StatusEffectType.Haste &&
               vigilance.statusDuration == 2 && Near(vigilance.statusMagnitude, 0.10f) &&
               vigilance.specialEffect == SkillSpecialEffect.RemoveDread,
            "Bell of Vigilance values drifted.", errors);

        Expect(benediction.targetSide == SkillTargetSide.Allied && !benediction.allowSelfTarget &&
               benediction.minRange == 1 && benediction.range == 3 && benediction.chargeTicks == 8 &&
               benediction.spCost == 8 && benediction.statusType == StatusEffectType.FalseZeal &&
               benediction.statusDuration == 2 && Near(benediction.statusMagnitude, 0.20f) &&
               !benediction.isAbsorbable,
            "Crooked Benediction values drifted.", errors);

        Expect(hook.damageType == DamageType.Physical && hook.basePower == 7 &&
               Near(hook.baseHit, 0.82f) && hook.minRange == 2 && hook.range == 2 &&
               hook.spCost == 5 && hook.specialEffect == SkillSpecialEffect.PullTargetTowardCaster &&
               hook.isAbsorbable && hook.maxLevel == 5 &&
               hook.bonusStat == StatScaling.Perception && hook.insanityCost == 2 &&
               hook.holyVersion == rescue,
            "Pilgrim's Hook values or Holy link drifted.", errors);

        Expect(rescue.targetSide == SkillTargetSide.Allied &&
               rescue.specialEffect == SkillSpecialEffect.PullAllyTowardCasterAndProtect &&
               rescue.minRange == 2 && rescue.range == 2,
            "Pilgrim's Rescue values drifted.", errors);

        Expect(staff.slot == EquipSlot.Weapon && staff.damageType == DamageType.Dark &&
               staff.bonuses.strength == 1 && staff.bonuses.perception == 1,
            "Bell-Hook Staff values drifted.", errors);
        Expect(jack.slot == EquipSlot.Armor && jack.bonuses.constitution == 2 &&
               Modifier(jack, DamageType.Dark, -0.10f),
            "Crier's Jack values drifted.", errors);
        Expect(reliquary.slot == EquipSlot.Accessory && reliquary.bonuses.creativity == 1 &&
               reliquary.bonuses.faith == 1 && Modifier(reliquary, DamageType.Holy, 0.10f),
            "False Reliquary values drifted.", errors);

        CharacterStats totals = crier.GetTotalStats();
        Expect(crier.role == CombatantRole.Enemy &&
               crier.combatAIProfile == CombatAIProfile.LimboCrier && crier.isLimboAligned &&
               crier.baseStats.level == 2 && crier.baseStats.hpMax == 72 && crier.baseStats.spMax == 36 &&
               crier.baseStats.strength == 7 && crier.baseStats.dexterity == 8 &&
               crier.baseStats.constitution == 7 && crier.baseStats.creativity == 11 &&
               crier.baseStats.faith == 10 && crier.baseStats.perception == 10 &&
               crier.baseStats.speed == 8 && crier.intelligence == 5 && Near(crier.sightRange, 12f) &&
               crier.eyeHeight == 2,
            "Limbo Crier base sheet drifted.", errors);
        Expect(totals.strength == 8 && totals.constitution == 9 && totals.creativity == 12 &&
               totals.faith == 11 && totals.perception == 11,
            "Crier equipment did not enter the existing total-stat pipeline exactly once.", errors);
        Expect(crier.equippedSkills?.actives?.Length == 4 &&
               crier.equippedSkills.actives[0] == jab && crier.equippedSkills.actives[1] == knell &&
               crier.equippedSkills.actives[2] == benediction && crier.equippedSkills.actives[3] == hook &&
               crier.learnableSkills?.Length == 2 && crier.learnableSkills[0] == knell &&
               crier.learnableSkills[1] == hook && Near(crier.skillDropChance, 0.25f),
            "Crier loadout/drop rules drifted.", errors);

        Expect(profile.walk.expectedFramesPerDirection == 6 &&
               profile.hurt.expectedFramesPerDirection == 4 &&
               profile.death.expectedFramesPerDirection == 9 && profile.skills?.Length == 4,
            "Humanoid profile expected frame counts drifted.", errors);
        if (profile.skills != null)
            foreach (HumanoidSkillAnimation animation in profile.skills)
                Expect(animation?.skill != null && animation.sequence?.expectedFramesPerDirection == 9,
                    "A Crier action profile is missing its skill or nine-frame contract.", errors);

        SkillDefinition basic = AssetDatabase.LoadAssetAtPath<SkillDefinition>(
            "Assets/Data/Skills/Skill_BasicAttack.asset");
        Expect(basic != null && basic.usesEquippedWeapon,
            "The shared basic Attack is not routed through equipped weapon type/power.", errors);

        SkillDefinition resourceKnell = Resources.Load<SkillDefinition>(
            "Skills/LimboCrier/Skill_KnellOfDread");
        Expect(resourceKnell == knell,
            "Resource-backed skill registry fallback cannot locate Knell of Dread.", errors);
    }

    static void ValidateProductionVisualAssets(List<string> errors)
    {
        ValidatedImportPaths.Clear();
        HumanoidBattleVisualProfile profile = AssetDatabase.LoadAssetAtPath<HumanoidBattleVisualProfile>(
            LimboCrierCombatBuilder.ProfilePath);
        CombatantData crier = AssetDatabase.LoadAssetAtPath<CombatantData>(
            LimboCrierCombatBuilder.CombatantPath);
        if (profile == null || crier == null) return;

        var allCharacterSprites = new HashSet<Sprite>();
        foreach (Sprite sprite in new[]
                 {
                     profile.south, profile.southEast, profile.east, profile.northEast,
                     profile.north, profile.northWest, profile.west, profile.southWest,
                 })
        {
            Expect(sprite != null, "One of eight Crier rotations is missing.", errors);
            if (sprite != null)
            {
                allCharacterSprites.Add(sprite);
                ValidateSpriteImport(sprite, 196, 196, 64f, "Crier rotation", errors, checkEveryCall: false);
            }
        }
        ValidateSequence(profile.walk, 6, "Walking", allCharacterSprites, errors);
        ValidateSequence(profile.hurt, 4, "Hurt", allCharacterSprites, errors);
        ValidateSequence(profile.death, 9, "Death", allCharacterSprites, errors);

        SkillDefinition jab = AssetDatabase.LoadAssetAtPath<SkillDefinition>(LimboCrierCombatBuilder.JabPath);
        SkillDefinition knell = AssetDatabase.LoadAssetAtPath<SkillDefinition>(LimboCrierCombatBuilder.KnellPath);
        SkillDefinition benediction = AssetDatabase.LoadAssetAtPath<SkillDefinition>(LimboCrierCombatBuilder.BenedictionPath);
        SkillDefinition hook = AssetDatabase.LoadAssetAtPath<SkillDefinition>(LimboCrierCombatBuilder.HookPath);
        foreach (SkillDefinition skill in new[] { jab, knell, benediction, hook })
        {
            HumanoidSkillAnimation animation = profile.GetSkillAnimation(skill);
            Expect(animation != null && !string.IsNullOrWhiteSpace(animation.sourceAnimationGroupId) &&
                   !string.IsNullOrWhiteSpace(animation.generationPrompt),
                $"{skill?.skillName ?? "missing skill"} lacks PixelLab action metadata.", errors);
            if (animation != null)
                ValidateSequence(animation.sequence, 9, skill.skillName, allCharacterSprites, errors);
        }

        Expect(allCharacterSprites.Count == 228,
            $"Crier profile references {allCharacterSprites.Count} unique character images; expected 228.", errors);
        Expect(profile.sourceCharacterId == "7a07c91f-65dc-4501-a80f-a154619092bc" &&
               profile.sourceModelId == "pro" &&
               !string.IsNullOrWhiteSpace(profile.walkGroupId) &&
               !string.IsNullOrWhiteSpace(profile.hurtGroupId) &&
               !string.IsNullOrWhiteSpace(profile.deathGroupId),
            "PixelLab character/model/group metadata is incomplete or mismatched.", errors);

        Expect(crier.portrait != null && crier.battleSprite == profile.south &&
               crier.humanoidVisualProfile == profile && Near(crier.battleVisualScale, 1.15f),
            "Crier portrait/battle/profile/scale wiring is incomplete.", errors);
        ValidateSpriteImport(crier.portrait, 128, 128, 100f, "Crier portrait", errors);

        EquipmentDefinition staff = AssetDatabase.LoadAssetAtPath<EquipmentDefinition>(LimboCrierCombatBuilder.StaffPath);
        EquipmentDefinition jack = AssetDatabase.LoadAssetAtPath<EquipmentDefinition>(LimboCrierCombatBuilder.JackPath);
        EquipmentDefinition reliquary = AssetDatabase.LoadAssetAtPath<EquipmentDefinition>(LimboCrierCombatBuilder.ReliquaryPath);
        foreach ((EquipmentDefinition item, string label) in new[]
                 {
                     (staff, "Bell-Hook Staff"), (jack, "Crier's Jack"),
                     (reliquary, "False Reliquary"),
                 })
        {
            Expect(item != null && item.icon != null, label + " icon is missing.", errors);
            if (item?.icon != null) ValidateSpriteImport(item.icon, 32, 32, 100f, label + " icon", errors);
        }

        StatusEffectPresentationCatalog catalog = AssetDatabase.LoadAssetAtPath<StatusEffectPresentationCatalog>(
            LimboCrierVisualImporter.StatusCatalogPath);
        StatusEffectPresentation dread = catalog?.Find(StatusEffectType.Dread);
        StatusEffectPresentation zeal = catalog?.Find(StatusEffectType.FalseZeal);
        Expect(dread?.icon != null && dread.tooltip ==
               "The end feels near. Faith and turn speed are reduced.",
            "Dread presentation/icon/tooltip is incomplete.", errors);
        Expect(zeal?.icon != null && zeal.tooltip ==
               "Limbo lends strength at the cost of spreading its stain.",
            "False Zeal presentation/icon/tooltip is incomplete.", errors);
        if (dread?.icon != null) ValidateSpriteImport(dread.icon, 32, 32, 100f, "Dread icon", errors);
        if (zeal?.icon != null) ValidateSpriteImport(zeal.icon, 32, 32, 100f, "False Zeal icon", errors);

        Sprite stain = AssetDatabase.LoadAssetAtPath<Sprite>(LimboCrierVisualImporter.StainTexturePath);
        Material stainMaterial = AssetDatabase.LoadAssetAtPath<Material>(LimboCrierVisualImporter.StainMaterialPath);
        Expect(stain != null && stainMaterial != null,
            "Limbo Stain sprite or material is missing.", errors);
        if (stain != null) ValidateSpriteImport(stain, 128, 128, 128f, "Limbo Stain", errors);
        if (stainMaterial != null)
        {
            Texture main = stainMaterial.HasProperty("_BaseMap")
                ? stainMaterial.GetTexture("_BaseMap")
                : stainMaterial.mainTexture;
            Expect(main == stain.texture && stainMaterial.renderQueue >= 3000,
                "Limbo Stain material is not transparent or is not using the authored texture.", errors);
        }

        string sourcePath = "Assets/Characters/LimboCrier/Source/pixellab-source.json";
        Expect(System.IO.File.Exists(sourcePath) &&
               System.IO.File.ReadAllText(sourcePath).Contains(profile.sourceCharacterId),
            "Tracked PixelLab source manifest is missing or references another character.", errors);
    }

    static void ValidateSequence(
        HumanoidDirectionalSequence sequence,
        int expected,
        string label,
        HashSet<Sprite> sprites,
        List<string> errors)
    {
        if (sequence == null)
        {
            errors.Add(label + " sequence is missing.");
            return;
        }
        foreach ((Sprite[] frames, string direction) in new[]
                 {
                     (sequence.south, "south"), (sequence.east, "east"),
                     (sequence.north, "north"), (sequence.west, "west"),
                 })
        {
            Expect(frames != null && frames.Length == expected &&
                   Array.TrueForAll(frames, sprite => sprite != null),
                $"{label}/{direction} does not contain {expected} complete frames.", errors);
            if (frames == null) continue;
            foreach (Sprite sprite in frames)
            {
                if (sprite == null) continue;
                sprites.Add(sprite);
                ValidateSpriteImport(sprite, 196, 196, 64f,
                    $"{label}/{direction}", errors, checkEveryCall: false);
            }
        }
    }

    static readonly HashSet<string> ValidatedImportPaths = new(StringComparer.Ordinal);

    static void ValidateSpriteImport(
        Sprite sprite,
        int width,
        int height,
        float pixelsPerUnit,
        string label,
        List<string> errors,
        bool checkEveryCall = true)
    {
        if (sprite == null) return;
        string path = AssetDatabase.GetAssetPath(sprite);
        if (!checkEveryCall && !ValidatedImportPaths.Add(path)) return;
        if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
        {
            errors.Add(label + " has no TextureImporter.");
            return;
        }
        Expect(sprite.texture.width == width && sprite.texture.height == height,
            $"{label} is {sprite.texture.width}x{sprite.texture.height}; expected {width}x{height}.", errors);
        Expect(importer.textureType == TextureImporterType.Sprite &&
               importer.filterMode == FilterMode.Point && !importer.mipmapEnabled &&
               importer.textureCompression == TextureImporterCompression.Uncompressed &&
               Near(importer.spritePixelsPerUnit, pixelsPerUnit),
            label + " import settings are not point/no-mip/uncompressed/approved PPU.", errors);
    }

    static void ValidateStatuses(Fixture fixture, List<string> errors)
    {
        BattleUnit unit = fixture.Unit("Status Target", new Vector2Int(3, 1), true);
        unit.Data.baseStats.faith = 10;
        unit.Data.baseStats.speed = 10;
        unit.ApplyStatus(new StatusEffect(StatusEffectType.Dread, 2, 3f, null));

        unit.StartTurn();
        Expect(unit.GetEffectiveStats().faith == 7, "Dread did not subtract 3 Faith on turn one.", errors);
        Expect(Near(unit.Status.CombinedSpeedMultiplier(unit), 0.85f),
            "Dread did not reduce CT recovery by 15%.", errors);
        unit.EndTurn();
        Expect(unit.Status.Has(StatusEffectType.Dread), "Dread expired after only one affected turn.", errors);

        unit.StartTurn();
        Expect(unit.GetEffectiveStats().faith == 7, "Dread was not active through turn two.", errors);
        unit.EndTurn();
        Expect(!unit.Status.Has(StatusEffectType.Dread), "Dread did not expire after two affected turns.", errors);

        unit.Data.resistsDreadCtPenalty = true;
        unit.ApplyStatus(new StatusEffect(StatusEffectType.Dread, 2, 3f, null));
        Expect(unit.GetEffectiveStats().faith == 7 &&
               Near(unit.Status.CombinedSpeedMultiplier(unit), 1f),
            "Steady unit did not retain Faith loss while resisting Dread CT loss.", errors);
        unit.RemoveStatus(StatusEffectType.Dread);

        unit.ApplyStatus(new StatusEffect(StatusEffectType.FalseZeal, 2, 0.20f, null));
        Expect(Near(unit.Status.CombinedOutgoingDamageMultiplier(), 1.20f) &&
               Near(unit.Status.CombinedDamageReceivedMultiplier(DamageType.Holy), 1.15f) &&
               Near(unit.Status.CombinedDamageReceivedMultiplier(DamageType.Dark), 1f),
            "False Zeal damage modifiers are not +20% outgoing / +15% Holy received.", errors);
        unit.ApplyStatus(new StatusEffect(StatusEffectType.FalseZeal, 1, 0.10f, null));
        Expect(unit.Status.All.Count == 1 && unit.Status.All[0].remainingTurns == 2 &&
               Near(unit.Status.All[0].magnitude, 0.20f),
            "False Zeal reapplication stacked or weakened instead of refreshing.", errors);
        unit.RemoveStatus(StatusEffectType.FalseZeal);

        var armor = ScriptableObject.CreateInstance<EquipmentDefinition>();
        armor.damageReceivedModifiers = new[]
        {
            new DamageReceivedModifier { damageType = DamageType.Dark, percentage = -0.10f },
        };
        var relic = ScriptableObject.CreateInstance<EquipmentDefinition>();
        relic.damageReceivedModifiers = new[]
        {
            new DamageReceivedModifier { damageType = DamageType.Holy, percentage = 0.10f },
        };
        fixture.Track(armor);
        fixture.Track(relic);
        unit.Data.armor = armor;
        unit.Data.accessory = relic;
        Expect(Near(unit.Data.GetDamageReceivedMultiplier(DamageType.Dark), 0.90f) &&
               Near(unit.Data.GetDamageReceivedMultiplier(DamageType.Holy), 1.10f) &&
               Near(unit.Data.GetDamageReceivedMultiplier(DamageType.Fire), 1f),
            "Signed equipment damage-received modifiers did not fold by damage type.", errors);
    }

    static void ValidateTargeting(Fixture fixture, List<string> errors)
    {
        BattleUnit user = fixture.Unit("Targeting User", new Vector2Int(0, 0), false);
        BattleUnit ally = fixture.Unit("Targeting Ally", new Vector2Int(1, 0), false);
        BattleUnit hostile = fixture.Unit("Targeting Hostile", new Vector2Int(2, 0), true);
        SkillDefinition skill = ScriptableObject.CreateInstance<SkillDefinition>();
        fixture.Track(skill);

        skill.targetSide = SkillTargetSide.Allied;
        skill.allowSelfTarget = false;
        Expect(AbilityResolver.IsLegalTarget(user, ally, skill) &&
               !AbilityResolver.IsLegalTarget(user, hostile, skill) &&
               !AbilityResolver.IsLegalTarget(user, user, skill),
            "Explicit allied utility targeting accepted a hostile or self.", errors);
        skill.allowSelfTarget = true;
        Expect(AbilityResolver.IsLegalTarget(user, user, skill),
            "Self-inclusive allied targeting rejected the caster.", errors);
        skill.targetSide = SkillTargetSide.Automatic;
        skill.isHealing = false;
        Expect(AbilityResolver.IsLegalTarget(user, hostile, skill),
            "Legacy automatic offensive targeting no longer selects hostiles.", errors);
    }

    static void ValidateForcedMovement(Fixture fixture, List<string> errors)
    {
        BattleGrid grid = fixture.Grid;
        BattleUnit target = fixture.Unit("Hook Target", new Vector2Int(3, 2), true);
        Expect(ForcedMovementService.TryPullOneCell(
                grid, target, new Vector2Int(1, 2), out ForcedMovementFailure failure) &&
               target.gridPosition == new Vector2Int(2, 2) &&
               grid.GetCell(2, 2).occupant == target && grid.GetCell(3, 2).occupant == null,
            "Valid one-cell pull did not preserve authoritative occupancy: " + failure, errors);

        grid.MoveUnit(target, new Vector2Int(3, 2));
        BattleUnit blocker = fixture.Unit("Pull Blocker", new Vector2Int(2, 2), false);
        Expect(!ForcedMovementService.TryPullOneCell(
                   grid, target, new Vector2Int(1, 2), out failure) &&
               failure == ForcedMovementFailure.Occupied && target.gridPosition == new Vector2Int(3, 2),
            "Occupied pull destination moved/corrupted the target.", errors);
        grid.RemoveUnit(blocker);

        GridCell destination = grid.GetCell(2, 2);
        destination.reserved = true;
        ExpectFailure(grid, target, ForcedMovementFailure.Reserved, errors);
        destination.reserved = false;
        destination.objective = true;
        ExpectFailure(grid, target, ForcedMovementFailure.Objective, errors);
        destination.objective = false;
        destination.walkable = false;
        ExpectFailure(grid, target, ForcedMovementFailure.Unwalkable, errors);
        destination.walkable = true;
        destination.elevation = grid.GetCell(3, 2).elevation + grid.baseJumpHeight + 1;
        ExpectFailure(grid, target, ForcedMovementFailure.Elevation, errors);
        destination.elevation = grid.GetCell(3, 2).elevation;
        grid.GetCell(3, 2).impassableEdges = GridEdgeBlock.West;
        ExpectFailure(grid, target, ForcedMovementFailure.ImpassableEdge, errors);
        grid.GetCell(3, 2).impassableEdges = GridEdgeBlock.None;

        target.Data.immovable = true;
        ExpectFailure(grid, target, ForcedMovementFailure.Immovable, errors);
        target.Data.immovable = false;
        target.Data.protectedFromForcedMovement = true;
        ExpectFailure(grid, target, ForcedMovementFailure.ProtectedActor, errors);
        target.Data.protectedFromForcedMovement = false;
        grid.GetCell(3, 2).objective = true;
        ExpectFailure(grid, target, ForcedMovementFailure.Objective, errors);
        grid.GetCell(3, 2).objective = false;

        grid.MoveUnit(target, new Vector2Int(0, 2));
        Expect(!ForcedMovementService.TryPullOneCell(
                   grid, target, new Vector2Int(-2, 2), out failure) &&
               failure == ForcedMovementFailure.OutOfBounds && target.gridPosition == new Vector2Int(0, 2),
            "Edge-of-grid pull did not fail safely.", errors);
    }

    static void ValidateTemporaryTerrain(Fixture fixture, List<string> errors)
    {
        TemporaryTerrainService.ResetForTests();
        BattleGrid grid = fixture.Grid;
        BattleUnit source = fixture.Unit("Stain Source", new Vector2Int(0, 3), false);
        BattleUnit hostile = fixture.Unit("Stain Hostile", new Vector2Int(2, 3), true);
        GridCell cell = grid.GetCell(2, 3);
        cell.tileType = TileType.Fire;
        hostile.Data.currentHP = 100;
        hostile.Data.baseStats.hpMax = 100;
        hostile.RemoveStatus(StatusEffectType.Dread);

        Expect(TemporaryTerrainService.TryApplyLimboStain(
                   grid, cell.gridPos, source, 2, out TemporaryTerrainFailure failure) &&
               cell.tileType == TileType.LimboStain,
            "Eligible authored terrain rejected Limbo Stain: " + failure, errors);
        Expect(TemporaryTerrainService.TryApplyLimboStain(
                   grid, cell.gridPos, source, 3, out failure) &&
               TemporaryTerrainService.ActiveCount(grid) == 1,
            "Limbo Stain reapplication nested instead of refreshing.", errors);
        Expect(!TemporaryTerrainService.TryApply(
                   grid, cell.gridPos, TemporaryTerrainKind.GraveMulch, source, 1, 1, out failure) &&
               failure == TemporaryTerrainFailure.NestedTemporaryTerrain,
            "A different temporary terrain nested over Limbo Stain.", errors);

        TemporaryTerrainService.ResolveEndTurn(grid, hostile);
        Expect(hostile.Data.currentHP == 96 && hostile.Status.Has(StatusEffectType.Dread) &&
               hostile.Status.All[0].remainingTurns == 1,
            "Limbo Stain did not deal 4% max HP and apply one-turn Dread.", errors);
        TemporaryTerrainService.TickTurn(grid);
        TemporaryTerrainService.TickTurn(grid);
        Expect(cell.tileType == TileType.LimboStain,
            "Refreshed Limbo Stain expired before its deterministic duration.", errors);
        TemporaryTerrainService.TickTurn(grid);
        Expect(cell.tileType == TileType.Fire && TemporaryTerrainService.ActiveCount(grid) == 0,
            "Expired Limbo Stain did not restore the authored Fire tile.", errors);

        BattleUnit ally = fixture.Unit("Stain Ally", new Vector2Int(3, 3), false);
        ally.Data.currentHP = 100;
        ally.Data.baseStats.hpMax = 100;
        Expect(TemporaryTerrainService.TryApplyLimboStain(
            grid, ally.gridPosition, source, 1, out failure), "Could not create ally-immunity stain.", errors);
        TemporaryTerrainService.ResolveEndTurn(grid, ally);
        Expect(ally.Data.currentHP == 100, "Limbo Stain damaged a same-side unit.", errors);
        TemporaryTerrainService.RestoreAll(grid);

        hostile.Data.isLimboAligned = true;
        Expect(TemporaryTerrainService.TryApplyLimboStain(
            grid, hostile.gridPosition, source, 1, out failure), "Could not create alignment-immunity stain.", errors);
        hostile.Data.currentHP = 100;
        TemporaryTerrainService.ResolveEndTurn(grid, hostile);
        Expect(hostile.Data.currentHP == 100, "Limbo Stain damaged a Limbo-aligned hostile.", errors);
        hostile.Data.isLimboAligned = false;
        TemporaryTerrainService.RestoreAll(grid);

        cell.protectedTerrain = true;
        ExpectRejected(grid, cell, source, TemporaryTerrainFailure.ProtectedTerrain, errors);
        cell.protectedTerrain = false;
        cell.objective = true;
        ExpectRejected(grid, cell, source, TemporaryTerrainFailure.Objective, errors);
        cell.objective = false;
        cell.tileType = TileType.Void;
        ExpectRejected(grid, cell, source, TemporaryTerrainFailure.StrongerAuthoredCorruption, errors);
        cell.tileType = TileType.Normal;

        Expect(TemporaryTerrainService.TryApplyLimboStain(
            grid, cell.gridPos, source, 10, out failure), "Could not create restore-all fixture.", errors);
        TemporaryTerrainService.RestoreAll(grid);
        Expect(cell.tileType == TileType.Normal && TemporaryTerrainService.ActiveCount(grid) == 0,
            "Battle-end restoration left temporary terrain active.", errors);
    }

    static void ValidateVisualFallback(Fixture fixture, List<string> errors)
    {
        BattleUnit unit = fixture.Unit("Visual Target", new Vector2Int(5, 0), true);
        Sprite inline = fixture.Sprite(Color.red);
        Sprite profiled = fixture.Sprite(Color.magenta);
        unit.Data.battleIdleSouth = inline;
        unit.Data.battleSprite = inline;
        var profile = ScriptableObject.CreateInstance<HumanoidBattleVisualProfile>();
        fixture.Track(profile);
        profile.south = profiled;
        unit.Data.humanoidVisualProfile = profile;
        Expect(unit.Data.GetBattleIdleSprite(FacingDir.South) == profiled,
            "Humanoid profile did not win over inline idle data.", errors);
        profile.south = null;
        Expect(unit.Data.GetBattleIdleSprite(FacingDir.South) == inline,
            "Missing profile direction did not fall back to inline visuals.", errors);
    }

    static void ExpectFailure(
        BattleGrid grid,
        BattleUnit target,
        ForcedMovementFailure expected,
        List<string> errors)
    {
        bool moved = ForcedMovementService.TryPullOneCell(
            grid, target, new Vector2Int(1, 2), out ForcedMovementFailure actual);
        Expect(!moved && actual == expected && target.gridPosition == new Vector2Int(3, 2),
            $"Forced movement expected {expected}, got {actual} (moved={moved}).", errors);
    }

    static void ExpectRejected(
        BattleGrid grid,
        GridCell cell,
        BattleUnit source,
        TemporaryTerrainFailure expected,
        List<string> errors)
    {
        bool applied = TemporaryTerrainService.TryApplyLimboStain(
            grid, cell.gridPos, source, 1, out TemporaryTerrainFailure actual);
        Expect(!applied && actual == expected,
            $"Temporary terrain expected {expected}, got {actual} (applied={applied}).", errors);
    }

    static bool Modifier(EquipmentDefinition item, DamageType type, float expected)
    {
        if (item?.damageReceivedModifiers == null) return false;
        foreach (DamageReceivedModifier modifier in item.damageReceivedModifiers)
            if (modifier != null && modifier.damageType == type && Near(modifier.percentage, expected))
                return true;
        return false;
    }

    static T Load<T>(string path, List<string> errors) where T : UnityEngine.Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) errors.Add("Missing required asset: " + path);
        return asset;
    }

    static bool Near(float a, float b) => Mathf.Abs(a - b) <= 0.0001f;

    static void Expect(bool condition, string message, List<string> errors)
    {
        if (!condition) errors.Add(message);
    }

    static void Fail(List<string> errors)
    {
        foreach (string error in errors) Debug.LogError("[LimboCrierCombatValidator] " + error);
        throw new InvalidOperationException(
            $"Limbo Crier combat validation failed with {errors.Count} error(s).");
    }

    sealed class Fixture : IDisposable
    {
        readonly GameObject root;
        readonly List<UnityEngine.Object> assets = new();
        readonly List<Texture2D> textures = new();

        public BattleGrid Grid { get; }

        public Fixture()
        {
            TemporaryTerrainService.ResetForTests();
            root = new GameObject("LimboCrierCombatValidatorFixture");
            Grid = root.AddComponent<BattleGrid>();
            Grid.Initialize(6, 4);
            BattleManager manager = root.AddComponent<BattleManager>();
            manager.Grid = Grid;
        }

        public BattleUnit Unit(string name, Vector2Int position, bool isPlayer)
        {
            var data = ScriptableObject.CreateInstance<CombatantData>();
            data.displayName = name;
            data.role = CombatantRole.Benidito;
            data.baseStats = new CharacterStats
            {
                characterName = name,
                characterClass = "Validator",
                level = 1,
                hp = 100,
                hpMax = 100,
                sp = 100,
                spMax = 100,
                strength = 10,
                dexterity = 10,
                constitution = 10,
                creativity = 10,
                faith = 10,
                perception = 10,
                speed = 10,
            };
            assets.Add(data);

            var go = new GameObject(name);
            go.transform.SetParent(root.transform);
            BattleUnit unit = go.AddComponent<BattleUnit>();
            unit.Initialize(data, isPlayer);
            Grid.PlaceUnit(unit, position);
            return unit;
        }

        public Sprite Sprite(Color color)
        {
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            textures.Add(texture);
            Sprite sprite = UnityEngine.Sprite.Create(
                texture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1f);
            assets.Add(sprite);
            return sprite;
        }

        public void Track(UnityEngine.Object asset)
        {
            if (asset != null) assets.Add(asset);
        }

        public void Dispose()
        {
            TemporaryTerrainService.ResetForTests();
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            foreach (UnityEngine.Object asset in assets)
                if (asset != null) UnityEngine.Object.DestroyImmediate(asset);
            foreach (Texture2D texture in textures)
                if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
