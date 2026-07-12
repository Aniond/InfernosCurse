using UnityEngine;

/// <summary>
/// Benidito-only state derived exclusively from equipped, unrefined absorbed
/// skills. It never reads Circle Influence, locations, NPCs, time, or saves.
/// </summary>
public static class PlayerInsanityState
{
    // Editor/runtime preview hook. Production authoring keeps this at -1.
    public static int DebugOverride { get; set; } = -1;

    /// <summary>Pure loadout calculation. Feature switches do not affect it.</summary>
    public static int Calculate(CombatantData benidito)
    {
        if (benidito == null || benidito.role != CombatantRole.Benidito)
            return 0;

        var equipped = benidito.equippedSkills?.absorbed;
        if (equipped == null) return 0;

        int total = 0;
        foreach (var skill in equipped)
        {
            if (skill == null) continue;
            total = Mathf.Min(100, total + Mathf.Max(0, skill.GetInsanityCost()));
        }
        return Mathf.Clamp(total, 0, 100);
    }

    public static int Current
    {
        get
        {
            if (!GameFeatures.PlayerInsanityEnabled) return 0;
            if (DebugOverride >= 0) return Mathf.Clamp(DebugOverride, 0, 100);

            foreach (var member in RestSystem.PartyMembers)
                if (member != null && member.role == CombatantRole.Benidito)
                    return Calculate(member);
            return 0;
        }
    }

    public static int CurrentFor(CombatantData combatant)
    {
        if (!GameFeatures.PlayerInsanityEnabled ||
            combatant == null || combatant.role != CombatantRole.Benidito)
            return 0;
        if (DebugOverride >= 0) return Mathf.Clamp(DebugOverride, 0, 100);
        return Calculate(combatant);
    }
}
