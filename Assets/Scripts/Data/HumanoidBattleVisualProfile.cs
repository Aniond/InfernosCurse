using System;
using UnityEngine;

[Serializable]
public class HumanoidDirectionalSequence
{
    public Sprite[] south = Array.Empty<Sprite>();
    public Sprite[] east = Array.Empty<Sprite>();
    public Sprite[] north = Array.Empty<Sprite>();
    public Sprite[] west = Array.Empty<Sprite>();
    [Min(1f)] public float fps = 12f;
    [Min(0)] public int expectedFramesPerDirection;

    public bool HasAnyFrames =>
        (south != null && south.Length > 0) ||
        (east != null && east.Length > 0) ||
        (north != null && north.Length > 0) ||
        (west != null && west.Length > 0);

    public Sprite[] GetFrames(FacingDir direction)
    {
        Sprite[] directional = direction switch
        {
            FacingDir.East => east,
            FacingDir.North => north,
            FacingDir.West => west,
            _ => south,
        };
        return directional != null && directional.Length > 0 ? directional : south;
    }
}

[Serializable]
public class HumanoidSkillAnimation
{
    public SkillDefinition skill;
    public HumanoidDirectionalSequence sequence = new HumanoidDirectionalSequence();
    public string sourceAnimationGroupId;
    [TextArea(2, 4)] public string generationPrompt;
}

[CreateAssetMenu(fileName = "HumanoidVisual_New", menuName = "InfernosCurse/Humanoid Battle Visual Profile")]
public class HumanoidBattleVisualProfile : ScriptableObject
{
    [Header("Static Eight-Direction Rotations")]
    public Sprite south;
    public Sprite southEast;
    public Sprite east;
    public Sprite northEast;
    public Sprite north;
    public Sprite northWest;
    public Sprite west;
    public Sprite southWest;

    [Header("Cardinal Sequences")]
    public HumanoidDirectionalSequence walk = new HumanoidDirectionalSequence
    {
        fps = 10f,
        expectedFramesPerDirection = 6,
    };
    public HumanoidDirectionalSequence hurt = new HumanoidDirectionalSequence
    {
        fps = 12f,
        expectedFramesPerDirection = 4,
    };
    public HumanoidDirectionalSequence death = new HumanoidDirectionalSequence
    {
        fps = 12f,
        expectedFramesPerDirection = 9,
    };
    public HumanoidSkillAnimation[] skills = Array.Empty<HumanoidSkillAnimation>();

    [Header("Source Generation Metadata (never credentials)")]
    public string provider;
    public string sourceCharacterId;
    public string sourceModelId;
    [TextArea(2, 6)] public string identityPrompt;
    public string rotationsGroupId;
    public string walkGroupId;
    public string hurtGroupId;
    public string deathGroupId;

    public Sprite GetIdleSprite(FacingDir direction) => direction switch
    {
        FacingDir.East => east,
        FacingDir.North => north,
        FacingDir.West => west,
        _ => south,
    };

    public HumanoidSkillAnimation GetSkillAnimation(SkillDefinition skill)
    {
        if (skill == null || skills == null) return null;
        foreach (var animation in skills)
            if (animation != null && animation.skill == skill) return animation;
        return null;
    }
}
