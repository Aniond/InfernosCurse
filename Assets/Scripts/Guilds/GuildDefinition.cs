using System;
using System.Collections.Generic;
using UnityEngine;

// What a guild perk DOES. Adding a new type needs code in GuildSystem's perk
// queries; everything else about a guild (ranks, magnitudes, flavor) is data,
// so the remaining Arti are authorable as pure assets.
public enum GuildPerkType
{
    // magnitude = multiplier on the curse cost of an INN rest (lower is better).
    RestCurseCostMultiplier,
    // magnitude = fraction of the district's curse cleansed by a guild-inn rest.
    InnRestCleansePercent,
    // magnitude = florin offering charged per transmutation.
    UnlockTransmutation,
    // magnitude = multiplier on inn prices.
    InnPriceMultiplier,
    // magnitude = multiplier on reputation gained from donations.
    DonationRepBonus,
}

[Serializable]
public class GuildPerk
{
    public GuildPerkType type;
    [Tooltip("Guild rank (1-based) at which this perk row unlocks.")]
    public int unlockRank = 1;
    [Tooltip("Meaning depends on type — see GuildPerkType comments.")]
    public float magnitude = 1f;
    [Tooltip("The ONLY text the UI ever shows for this perk. Words, never numbers — the curse stays hidden.")]
    [TextArea] public string flavorText;
}

// One Florentine guild (Arte) or parallel faction (the Church). Immutable
// template — per-save standing lives in GuildSystem's GuildMembership.
[CreateAssetMenu(fileName = "Guild_New", menuName = "InfernosCurse/Guild Definition")]
public class GuildDefinition : ScriptableObject
{
    [Header("Identity")]
    public string guildId;            // "albergatori", "church"
    public string displayName;        // "Arte degli Albergatori"
    [TextArea] public string blurb;
    public Sprite crest;
    [Tooltip("The Church is a parallel faction, not an Arte — flavor/UI flag.")]
    public bool isChurch;

    [Header("Territory")]
    [Tooltip("HubMap node id this guild calls home. Battles won there earn reputation.")]
    public string homeNodeId;

    [Header("Reputation track")]
    [Tooltip("Cumulative reputation required to HOLD each rank. Index 0 = rank 0 " +
             "(always 0). Length defines the max rank.")]
    public int[] repPerRank = { 0, 50, 150, 350, 700 };
    [Tooltip("Rank display names, parallel to repPerRank.")]
    public string[] rankNames = { "Straniero", "Amico", "Sostenitore", "Confratello", "Console" };
    [Tooltip("Reputation per battle won in the home district.")]
    public int repPerHomeKill = 5;
    [Tooltip("Florins per 1 reputation when donating.")]
    public int florinsPerRep = 2;

    [Header("Perks")]
    public List<GuildPerk> perks = new List<GuildPerk>();

    public int MaxRank => repPerRank != null ? repPerRank.Length - 1 : 0;

    public string RankName(int rank)
    {
        if (rankNames == null || rankNames.Length == 0) return rank.ToString();
        return rankNames[Mathf.Clamp(rank, 0, rankNames.Length - 1)];
    }

    /// <summary>Highest rank whose cumulative threshold is met by rep.</summary>
    public int RankForRep(int rep)
    {
        int rank = 0;
        if (repPerRank == null) return 0;
        for (int i = 0; i < repPerRank.Length; i++)
            if (rep >= repPerRank[i]) rank = i;
        return rank;
    }
}
