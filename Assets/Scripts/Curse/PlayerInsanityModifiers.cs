using UnityEngine;

public enum PlayerInsanityTier
{
    Clear,
    Warning,
    Fractured,
    Unmoored,
    Abyssal,
}

public readonly struct PlayerInsanityModifierSet
{
    public readonly PlayerInsanityTier Tier;
    public readonly int PerceptionPenalty;
    public readonly int FaithPenalty;
    public readonly float CtGainMultiplier;

    public PlayerInsanityModifierSet(
        PlayerInsanityTier tier,
        int perceptionPenalty,
        int faithPenalty,
        float ctGainMultiplier)
    {
        Tier = tier;
        PerceptionPenalty = perceptionPenalty;
        FaithPenalty = faithPenalty;
        CtGainMultiplier = ctGainMultiplier;
    }
}

/// <summary>Pure deterministic mapping from hidden Insanity to personal penalties.</summary>
public static class PlayerInsanityModifiers
{
    static readonly PlayerInsanityModifierSet Clear =
        new(PlayerInsanityTier.Clear, 0, 0, 1f);

    public static PlayerInsanityModifierSet Evaluate(int insanity)
    {
        int value = Mathf.Clamp(insanity, 0, 100);
        if (value >= 75)
            return new PlayerInsanityModifierSet(PlayerInsanityTier.Abyssal, 4, 4, 0.90f);
        if (value >= 55)
            return new PlayerInsanityModifierSet(PlayerInsanityTier.Unmoored, 3, 2, 1f);
        if (value >= 35)
            return new PlayerInsanityModifierSet(PlayerInsanityTier.Fractured, 2, 0, 1f);
        if (value >= 15)
            return new PlayerInsanityModifierSet(PlayerInsanityTier.Warning, 0, 0, 1f);
        return Clear;
    }

    public static PlayerInsanityModifierSet For(CombatantData combatant)
    {
        if (!GameFeatures.PlayerInsanityEnabled ||
            combatant == null || combatant.role != CombatantRole.Benidito)
            return Clear;
        return Evaluate(PlayerInsanityState.CurrentFor(combatant));
    }
}
