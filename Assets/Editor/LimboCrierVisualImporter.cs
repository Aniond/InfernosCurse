using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class LimboCrierVisualImporter
{
    const string CharacterRoot = "Assets/Characters/LimboCrier";
    const string RotationRoot = CharacterRoot + "/Sprites/rotations";
    const string AnimationRoot = CharacterRoot + "/Sprites/animations";
    const string SourceRoot = CharacterRoot + "/Source";
    const string PortraitPath = "Assets/Art/Portraits/portrait-limbo-crier.png";
    const string DreadIconPath = "Assets/UI/StatusIcons/status-dread.png";
    const string FalseZealIconPath = "Assets/UI/StatusIcons/status-false-zeal.png";
    const string StaffIconPath = "Assets/UI/EquipmentIcons/item-bell-hook-staff.png";
    const string JackIconPath = "Assets/UI/EquipmentIcons/item-criers-jack.png";
    const string ReliquaryIconPath = "Assets/UI/EquipmentIcons/item-false-reliquary.png";
    public const string StainTexturePath = "Assets/Resources/VFX/Limbo/limbo-stain.png";
    public const string StainMaterialPath = "Assets/Resources/VFX/Limbo/LimboStain.mat";
    public const string StatusCatalogPath = "Assets/Resources/UI/StatusEffectPresentationCatalog.asset";

    static readonly string[] EightDirections =
    {
        "south", "south-east", "east", "north-east",
        "north", "north-west", "west", "south-west",
    };
    static readonly string[] FourDirections = { "south", "east", "north", "west" };

    static readonly AnimationSpec[] Animations =
    {
        new("walking", "Walking", 6),
        new("bell_hook_jab", "BellHookJab", 9),
        new("knell_of_dread", "KnellOfDread", 9),
        new("crooked_benediction", "CrookedBenediction", 9),
        new("pilgrims_hook", "PilgrimsHook", 9),
        new("hurt", "Hurt", 4),
        new("death", "Death", 9),
    };

    [MenuItem("InfernosCurse/Limbo Crier/Import Approved Visual Package")]
    public static void ImportFromStaging()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string stage = Path.Combine(projectRoot, "GeneratedAssets", "PixelLab", "LimboCrier");
        var errors = ValidateStaging(stage);
        if (errors.Count > 0)
        {
            foreach (string error in errors) Debug.LogError("[LimboCrierVisualImporter] " + error);
            throw new InvalidOperationException(
                $"Limbo Crier staging validation failed with {errors.Count} error(s).");
        }

        LimboCrierCombatBuilder.Build();

        foreach (string direction in EightDirections)
            CopyIfChanged(
                Path.Combine(stage, "rotations", direction + ".png"),
                ProjectAbsolute(projectRoot, $"{RotationRoot}/{direction}.png"));

        foreach (AnimationSpec animation in Animations)
            foreach (string direction in FourDirections)
                for (int frame = 0; frame < animation.frames; frame++)
                    CopyIfChanged(
                        Path.Combine(stage, "animations", animation.sourceKey, direction,
                            $"frame_{frame:D3}.png"),
                        ProjectAbsolute(projectRoot,
                            $"{AnimationRoot}/{animation.productionFolder}/{direction}/frame_{frame:D3}.png"));

        CopyIfChanged(Path.Combine(stage, "support", "portrait-limbo-crier.png"),
            ProjectAbsolute(projectRoot, PortraitPath));
        CopyIfChanged(Path.Combine(stage, "support", "status-dread.png"),
            ProjectAbsolute(projectRoot, DreadIconPath));
        CopyIfChanged(Path.Combine(stage, "support", "status-false-zeal.png"),
            ProjectAbsolute(projectRoot, FalseZealIconPath));
        CopyIfChanged(Path.Combine(stage, "support", "item-bell-hook-staff.png"),
            ProjectAbsolute(projectRoot, StaffIconPath));
        CopyIfChanged(Path.Combine(stage, "support", "item-criers-jack.png"),
            ProjectAbsolute(projectRoot, JackIconPath));
        CopyIfChanged(Path.Combine(stage, "support", "item-false-reliquary.png"),
            ProjectAbsolute(projectRoot, ReliquaryIconPath));
        CopyIfChanged(Path.Combine(stage, "source.json"),
            ProjectAbsolute(projectRoot, SourceRoot + "/pixellab-source.json"));
        WriteProceduralStain(ProjectAbsolute(projectRoot, StainTexturePath));

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        foreach (string path in EnumerateProductionPngPaths())
        {
            float ppu = path.StartsWith(CharacterRoot, StringComparison.Ordinal) ? 64f
                : path == StainTexturePath ? 128f
                : 100f;
            ConfigureTextureImporter(path, ppu);
        }
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        WireProfileAndAssets(stage);
        BuildStainMaterial();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        LimboCrierCombatValidator.ValidateProductionVisualAssets();
        Debug.Log("[LimboCrierVisualImporter] Imported 228 character images, portrait, five icons, and procedural Limbo Stain presentation.");
    }

    static List<string> ValidateStaging(string stage)
    {
        var errors = new List<string>();
        if (!Directory.Exists(stage))
        {
            errors.Add("Missing staging directory: " + stage);
            return errors;
        }

        int canvas = 0;
        foreach (string direction in EightDirections)
            ValidatePng(Path.Combine(stage, "rotations", direction + ".png"),
                expectedWidth: canvas, expectedHeight: canvas, setCanvas: canvas == 0,
                label: "rotation " + direction, errors, ref canvas);

        foreach (AnimationSpec animation in Animations)
            foreach (string direction in FourDirections)
                for (int frame = 0; frame < animation.frames; frame++)
                    ValidatePng(
                        Path.Combine(stage, "animations", animation.sourceKey, direction,
                            $"frame_{frame:D3}.png"),
                        canvas, canvas, false,
                        $"{animation.sourceKey}/{direction}/frame_{frame:D3}", errors, ref canvas);

        int ignored = 0;
        ValidatePng(Path.Combine(stage, "support", "portrait-limbo-crier.png"),
            128, 128, false, "portrait", errors, ref ignored);
        foreach (string icon in new[]
                 {
                     "status-dread.png", "status-false-zeal.png", "item-bell-hook-staff.png",
                     "item-criers-jack.png", "item-false-reliquary.png",
                 })
            ValidatePng(Path.Combine(stage, "support", icon),
                32, 32, false, icon, errors, ref ignored);

        string manifestPath = Path.Combine(stage, "source.json");
        if (!File.Exists(manifestPath)) errors.Add("Missing PixelLab source manifest.");
        else
        {
            SourceManifest manifest = JsonUtility.FromJson<SourceManifest>(File.ReadAllText(manifestPath));
            if (manifest == null || string.IsNullOrWhiteSpace(manifest.character_id))
                errors.Add("Source manifest has no character_id.");
            if (manifest?.animations == null || manifest.animations.Length != Animations.Length)
                errors.Add("Source manifest does not contain seven animation groups.");
            else
                foreach (AnimationSpec expected in Animations)
                {
                    SourceAnimation actual = Array.Find(
                        manifest.animations, candidate => candidate.key == expected.sourceKey);
                    if (actual == null || string.IsNullOrWhiteSpace(actual.animation_group_id) ||
                        actual.expected_frames_per_direction != expected.frames)
                        errors.Add($"Source manifest group '{expected.sourceKey}' is missing or mismatched.");
                }
        }
        return errors;
    }

    static void ValidatePng(
        string path,
        int expectedWidth,
        int expectedHeight,
        bool setCanvas,
        string label,
        List<string> errors,
        ref int canvas)
    {
        if (!File.Exists(path))
        {
            errors.Add("Missing " + label + ": " + path);
            return;
        }
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(path), markNonReadable: false))
            {
                errors.Add("Unreadable PNG for " + label + ".");
                return;
            }
            if (setCanvas)
            {
                canvas = texture.width;
                if (texture.width != texture.height || texture.width < 128 || texture.width > 256)
                    errors.Add($"Character canvas is {texture.width}x{texture.height}; expected one square 128-256px canvas.");
            }
            else if (texture.width != expectedWidth || texture.height != expectedHeight)
                errors.Add($"{label} is {texture.width}x{texture.height}; expected {expectedWidth}x{expectedHeight}.");

            int visible = 0;
            foreach (Color32 pixel in texture.GetPixels32())
                if (pixel.a > 8) visible++;
            if (visible < 8) errors.Add(label + " has no readable nontransparent subject.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    static void WireProfileAndAssets(string stage)
    {
        HumanoidBattleVisualProfile profile = AssetDatabase.LoadAssetAtPath<HumanoidBattleVisualProfile>(
            LimboCrierCombatBuilder.ProfilePath);
        CombatantData crier = AssetDatabase.LoadAssetAtPath<CombatantData>(
            LimboCrierCombatBuilder.CombatantPath);
        SkillDefinition jab = AssetDatabase.LoadAssetAtPath<SkillDefinition>(LimboCrierCombatBuilder.JabPath);
        SkillDefinition knell = AssetDatabase.LoadAssetAtPath<SkillDefinition>(LimboCrierCombatBuilder.KnellPath);
        SkillDefinition benediction = AssetDatabase.LoadAssetAtPath<SkillDefinition>(LimboCrierCombatBuilder.BenedictionPath);
        SkillDefinition hook = AssetDatabase.LoadAssetAtPath<SkillDefinition>(LimboCrierCombatBuilder.HookPath);
        EquipmentDefinition staff = AssetDatabase.LoadAssetAtPath<EquipmentDefinition>(LimboCrierCombatBuilder.StaffPath);
        EquipmentDefinition jack = AssetDatabase.LoadAssetAtPath<EquipmentDefinition>(LimboCrierCombatBuilder.JackPath);
        EquipmentDefinition reliquary = AssetDatabase.LoadAssetAtPath<EquipmentDefinition>(LimboCrierCombatBuilder.ReliquaryPath);

        profile.south = SpriteAt($"{RotationRoot}/south.png");
        profile.southEast = SpriteAt($"{RotationRoot}/south-east.png");
        profile.east = SpriteAt($"{RotationRoot}/east.png");
        profile.northEast = SpriteAt($"{RotationRoot}/north-east.png");
        profile.north = SpriteAt($"{RotationRoot}/north.png");
        profile.northWest = SpriteAt($"{RotationRoot}/north-west.png");
        profile.west = SpriteAt($"{RotationRoot}/west.png");
        profile.southWest = SpriteAt($"{RotationRoot}/south-west.png");

        AssignSequence(profile.walk, "Walking", 6);
        AssignSequence(profile.hurt, "Hurt", 4);
        AssignSequence(profile.death, "Death", 9);
        AssignSkill(profile, jab, "BellHookJab", 9);
        AssignSkill(profile, knell, "KnellOfDread", 9);
        AssignSkill(profile, benediction, "CrookedBenediction", 9);
        AssignSkill(profile, hook, "PilgrimsHook", 9);

        SourceManifest manifest = JsonUtility.FromJson<SourceManifest>(
            File.ReadAllText(Path.Combine(stage, "source.json")));
        profile.sourceCharacterId = manifest.character_id;
        profile.sourceModelId = "pro";
        profile.rotationsGroupId = manifest.character_id;
        profile.walkGroupId = FindSource(manifest, "walking")?.animation_group_id;
        profile.hurtGroupId = FindSource(manifest, "hurt")?.animation_group_id;
        profile.deathGroupId = FindSource(manifest, "death")?.animation_group_id;
        SetSkillMetadata(profile, jab, FindSource(manifest, "bell_hook_jab"));
        SetSkillMetadata(profile, knell, FindSource(manifest, "knell_of_dread"));
        SetSkillMetadata(profile, benediction, FindSource(manifest, "crooked_benediction"));
        SetSkillMetadata(profile, hook, FindSource(manifest, "pilgrims_hook"));

        crier.portrait = SpriteAt(PortraitPath);
        crier.battleSprite = profile.south;
        crier.battleVisualScale = 1.15f;
        crier.battleVisualOffset = new Vector2(0f, 0.72f);
        staff.icon = SpriteAt(StaffIconPath);
        jack.icon = SpriteAt(JackIconPath);
        reliquary.icon = SpriteAt(ReliquaryIconPath);

        StatusEffectPresentationCatalog catalog = LoadOrCreate<StatusEffectPresentationCatalog>(StatusCatalogPath);
        catalog.entries = new[]
        {
            new StatusEffectPresentation
            {
                type = StatusEffectType.Dread,
                icon = SpriteAt(DreadIconPath),
                tooltip = "The end feels near. Faith and turn speed are reduced.",
            },
            new StatusEffectPresentation
            {
                type = StatusEffectType.FalseZeal,
                icon = SpriteAt(FalseZealIconPath),
                tooltip = "Limbo lends strength at the cost of spreading its stain.",
            },
        };

        foreach (UnityEngine.Object asset in new UnityEngine.Object[]
                 { profile, crier, staff, jack, reliquary, catalog })
            EditorUtility.SetDirty(asset);
    }

    static void AssignSequence(HumanoidDirectionalSequence sequence, string folder, int count)
    {
        sequence.south = Frames(folder, "south", count);
        sequence.east = Frames(folder, "east", count);
        sequence.north = Frames(folder, "north", count);
        sequence.west = Frames(folder, "west", count);
        sequence.expectedFramesPerDirection = count;
    }

    static void AssignSkill(
        HumanoidBattleVisualProfile profile,
        SkillDefinition skill,
        string folder,
        int count)
    {
        HumanoidSkillAnimation animation = profile.GetSkillAnimation(skill);
        if (animation == null)
            throw new InvalidOperationException("Profile has no action slot for " + skill.skillName + ".");
        AssignSequence(animation.sequence, folder, count);
    }

    static void SetSkillMetadata(
        HumanoidBattleVisualProfile profile,
        SkillDefinition skill,
        SourceAnimation source)
    {
        HumanoidSkillAnimation animation = profile.GetSkillAnimation(skill);
        if (animation == null || source == null) return;
        animation.sourceAnimationGroupId = source.animation_group_id;
        animation.generationPrompt = source.action_prompt;
    }

    static SourceAnimation FindSource(SourceManifest manifest, string key) =>
        manifest?.animations == null
            ? null
            : Array.Find(manifest.animations, animation => animation.key == key);

    static Sprite[] Frames(string folder, string direction, int count)
    {
        var frames = new Sprite[count];
        for (int i = 0; i < count; i++)
            frames[i] = SpriteAt($"{AnimationRoot}/{folder}/{direction}/frame_{i:D3}.png");
        return frames;
    }

    static Sprite SpriteAt(string path)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) throw new InvalidOperationException("Missing imported Sprite: " + path);
        return sprite;
    }

    static void ConfigureTextureImporter(string path, float pixelsPerUnit)
    {
        if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            throw new InvalidOperationException("No TextureImporter for " + path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 512;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        importer.SetTextureSettings(settings);
        AssetDatabase.WriteImportSettingsIfDirty(path);
    }

    static IEnumerable<string> EnumerateProductionPngPaths()
    {
        foreach (string direction in EightDirections)
            yield return $"{RotationRoot}/{direction}.png";
        foreach (AnimationSpec animation in Animations)
            foreach (string direction in FourDirections)
                for (int frame = 0; frame < animation.frames; frame++)
                    yield return $"{AnimationRoot}/{animation.productionFolder}/{direction}/frame_{frame:D3}.png";
        yield return PortraitPath;
        yield return DreadIconPath;
        yield return FalseZealIconPath;
        yield return StaffIconPath;
        yield return JackIconPath;
        yield return ReliquaryIconPath;
        yield return StainTexturePath;
    }

    static void WriteProceduralStain(string destination)
    {
        const int size = 128;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.SetPixels32(Enumerable.Repeat(new Color32(0, 0, 0, 0), size * size).ToArray());
        var random = new System.Random(12650728);
        var glow = new Color32(94, 50, 99, 54);
        var dark = new Color32(44, 32, 51, 190);
        var violet = new Color32(112, 66, 119, 225);

        foreach ((int start, int end, float radius) in new[]
                 {
                     (12, 76, 38f), (134, 202, 35f), (252, 316, 40f),
                 })
        {
            Vector2Int? previous = null;
            for (int degrees = start; degrees <= end; degrees += 3)
            {
                float radians = degrees * Mathf.Deg2Rad;
                float jitter = (float)(random.NextDouble() * 2.2 - 1.1);
                int x = Mathf.RoundToInt(64f + Mathf.Cos(radians) * (radius + jitter));
                int y = Mathf.RoundToInt(64f + Mathf.Sin(radians) * (radius + jitter));
                var point = new Vector2Int(x, y);
                if (previous.HasValue) DrawCrackLine(texture, previous.Value, point, glow, dark, violet);
                previous = point;
            }
        }

        int[] crackAngles = { 25, 58, 148, 184, 266, 291, 309 };
        foreach (int angle in crackAngles)
        {
            float radians = angle * Mathf.Deg2Rad;
            var point = new Vector2Int(
                Mathf.RoundToInt(64f + Mathf.Cos(radians) * 37f),
                Mathf.RoundToInt(64f + Mathf.Sin(radians) * 37f));
            float heading = radians + (random.Next(0, 2) == 0 ? 0f : Mathf.PI);
            int segments = random.Next(3, 6);
            for (int segment = 0; segment < segments; segment++)
            {
                heading += (float)(random.NextDouble() * 0.75 - 0.375);
                float length = random.Next(5, 11);
                var next = new Vector2Int(
                    Mathf.Clamp(Mathf.RoundToInt(point.x + Mathf.Cos(heading) * length), 4, size - 5),
                    Mathf.Clamp(Mathf.RoundToInt(point.y + Mathf.Sin(heading) * length), 4, size - 5));
                DrawCrackLine(texture, point, next, glow, dark, violet);
                if (segment == 1)
                {
                    float branchHeading = heading + (random.Next(0, 2) == 0 ? 0.85f : -0.85f);
                    var branch = new Vector2Int(
                        Mathf.RoundToInt(next.x + Mathf.Cos(branchHeading) * 6f),
                        Mathf.RoundToInt(next.y + Mathf.Sin(branchHeading) * 6f));
                    DrawCrackLine(texture, next, branch, glow, dark, violet);
                }
                point = next;
            }
        }

        texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
        WriteBytesIfChanged(destination, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
    }

    static void DrawCrackLine(
        Texture2D texture,
        Vector2Int from,
        Vector2Int to,
        Color32 glow,
        Color32 dark,
        Color32 core)
    {
        var points = RasterLine(from, to);
        foreach (Vector2Int point in points)
            for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                    Blend(texture, point.x + x, point.y + y, glow);
        foreach (Vector2Int point in points)
        {
            Blend(texture, point.x + 1, point.y, dark);
            Blend(texture, point.x, point.y + 1, dark);
            Blend(texture, point.x, point.y, core);
        }
    }

    static List<Vector2Int> RasterLine(Vector2Int from, Vector2Int to)
    {
        var result = new List<Vector2Int>();
        int x0 = from.x, y0 = from.y, x1 = to.x, y1 = to.y;
        int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int error = dx + dy;
        while (true)
        {
            result.Add(new Vector2Int(x0, y0));
            if (x0 == x1 && y0 == y1) break;
            int twice = 2 * error;
            if (twice >= dy) { error += dy; x0 += sx; }
            if (twice <= dx) { error += dx; y0 += sy; }
        }
        return result;
    }

    static void Blend(Texture2D texture, int x, int y, Color32 color)
    {
        if (x < 0 || x >= texture.width || y < 0 || y >= texture.height) return;
        Color32 current = texture.GetPixel(x, y);
        if (color.a >= current.a) texture.SetPixel(x, y, color);
    }

    static void BuildStainMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Transparent");
        if (shader == null) throw new InvalidOperationException("No transparent unlit shader is available.");

        Material material = AssetDatabase.LoadAssetAtPath<Material>(StainMaterialPath);
        if (material == null)
        {
            material = new Material(shader) { name = "LimboStain" };
            AssetDatabase.CreateAsset(material, StainMaterialPath);
        }
        else material.shader = shader;

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(StainTexturePath);
        material.SetTexture("_BaseMap", texture);
        material.SetTexture("_MainTex", texture);
        material.SetColor("_BaseColor", Color.white);
        material.SetColor("_Color", Color.white);
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_ZWrite", 0f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(material);
    }

    static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        EnsureAssetFolder(path);
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    static string ProjectAbsolute(string projectRoot, string assetPath) =>
        Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));

    static void CopyIfChanged(string source, string destination)
    {
        byte[] bytes = File.ReadAllBytes(source);
        WriteBytesIfChanged(destination, bytes);
    }

    static void WriteBytesIfChanged(string destination, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination));
        if (File.Exists(destination) && File.ReadAllBytes(destination).SequenceEqual(bytes)) return;
        File.WriteAllBytes(destination, bytes);
    }

    static void EnsureAssetFolder(string assetPath)
    {
        string folder = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
        if (string.IsNullOrEmpty(folder)) return;
        string[] parts = folder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    readonly struct AnimationSpec
    {
        public readonly string sourceKey;
        public readonly string productionFolder;
        public readonly int frames;

        public AnimationSpec(string sourceKey, string productionFolder, int frames)
        {
            this.sourceKey = sourceKey;
            this.productionFolder = productionFolder;
            this.frames = frames;
        }
    }

    [Serializable]
    sealed class SourceManifest
    {
        public string character_id;
        public SourceAnimation[] animations;
    }

    [Serializable]
    sealed class SourceAnimation
    {
        public string key;
        public string animation_group_id;
        public int expected_frames_per_direction;
        public string action_prompt;
    }
}
