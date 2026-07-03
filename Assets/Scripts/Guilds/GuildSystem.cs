using System;
using System.Collections.Generic;
using UnityEngine;

// Faction standing with Florence's guilds (Arti) and the Church. Lives on the
// GameSystems prefab. Reputation comes from battles won in a guild's home
// district and from florin donations; ranks unlock perks that bend the
// corruption economy — but never zero it (see GetRestCurseCostMultiplier).
public class GuildSystem : MonoBehaviour
{
    // Lazy-resolving so a mid-play domain reload (statics wiped, Awake not
    // re-run) can't leave the singleton permanently null.
    static GuildSystem _instance;
    public static GuildSystem Instance
    {
        get => _instance != null ? _instance : (_instance = FindAnyObjectByType<GuildSystem>());
        private set => _instance = value;
    }

    [Tooltip("All guilds/factions the player can hold standing with.")]
    public List<GuildDefinition> guilds = new List<GuildDefinition>();

    [Serializable]
    public class GuildMembership
    {
        public string guildId;
        public int rep;
    }

    [SerializeField] List<GuildMembership> _memberships = new List<GuildMembership>();

    /// <summary>(guild, newRep, newRank) after any reputation change.</summary>
    public event Action<GuildDefinition, int, int> OnRepChanged;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ── lookups ──────────────────────────────────────────────────────────────

    public GuildDefinition GetGuild(string guildId)
    {
        foreach (var g in guilds)
            if (g != null && g.guildId == guildId) return g;
        return null;
    }

    public GuildDefinition GuildForHomeNode(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId)) return null;
        foreach (var g in guilds)
            if (g != null && g.homeNodeId == nodeId) return g;
        return null;
    }

    GuildMembership GetOrAdd(string guildId)
    {
        foreach (var m in _memberships)
            if (m.guildId == guildId) return m;
        var nm = new GuildMembership { guildId = guildId, rep = 0 };
        _memberships.Add(nm);
        return nm;
    }

    public int GetRep(string guildId) => GetOrAdd(guildId).rep;

    public int GetRank(string guildId)
    {
        var g = GetGuild(guildId);
        return g != null ? g.RankForRep(GetRep(guildId)) : 0;
    }

    // ── reputation ───────────────────────────────────────────────────────────

    public void AwardRep(string guildId, int amount, string reason)
    {
        var g = GetGuild(guildId);
        if (g == null || amount <= 0) return;

        var m = GetOrAdd(guildId);
        int prevRank = g.RankForRep(m.rep);
        m.rep += amount;
        int rank = g.RankForRep(m.rep);

        Debug.Log($"[Guild] {guildId} +{amount} ({reason}) → {m.rep} (rank {rank}: {g.RankName(rank)})");
        if (rank > prevRank)
            Debug.Log($"[Guild] ★ RANK UP — the {g.displayName} now know you as {g.RankName(rank)}");
        OnRepChanged?.Invoke(g, m.rep, rank);
    }

    /// <summary>Donate florins for reputation. False if unaffordable.</summary>
    public bool Donate(string guildId, int florins)
    {
        var g = GetGuild(guildId);
        if (g == null || florins <= 0) return false;
        if (!FlorinWallet.TrySpend(florins, $"donation to {g.displayName}")) return false;

        float bonus = GetPerkMagnitude(GuildPerkType.DonationRepBonus, 1f, guildId);
        int rep = Mathf.Max(1, Mathf.RoundToInt(florins / (float)Mathf.Max(1, g.florinsPerRep) * bonus));
        AwardRep(guildId, rep, "donation");
        return true;
    }

    // ── perk queries ─────────────────────────────────────────────────────────

    // Walk a guild's (or all guilds') unlocked perk rows of one type and fold.
    float GetPerkMagnitude(GuildPerkType type, float fallback, string onlyGuildId = null, bool takeMin = false)
    {
        float best = fallback;
        bool found = false;
        foreach (var g in guilds)
        {
            if (g == null || (onlyGuildId != null && g.guildId != onlyGuildId)) continue;
            int rank = g.RankForRep(GetRep(g.guildId));
            foreach (var p in g.perks)
            {
                if (p.type != type || p.unlockRank > rank) continue;
                best = found ? (takeMin ? Mathf.Min(best, p.magnitude) : Mathf.Max(best, p.magnitude)) : p.magnitude;
                found = true;
            }
        }
        return best;
    }

    /// <summary>
    /// Multiplier on the curse cost of an inn rest. Lowest unlocked row wins;
    /// floor-clamped so guild perks BEND the corruption tide, never zero it.
    /// </summary>
    public float GetRestCurseCostMultiplier() =>
        Mathf.Max(0.25f, GetPerkMagnitude(GuildPerkType.RestCurseCostMultiplier, 1f, null, takeMin: true));

    /// <summary>Fraction of district curse cleansed by a guild-inn rest (0 = perk locked).</summary>
    public float GetInnCleansePercent() =>
        GetPerkMagnitude(GuildPerkType.InnRestCleansePercent, 0f);

    public float GetInnPriceMultiplier() =>
        Mathf.Clamp(GetPerkMagnitude(GuildPerkType.InnPriceMultiplier, 1f, null, takeMin: true), 0.25f, 1f);

    /// <summary>True when the Church trusts the player with transmutation rites.</summary>
    public bool CanTransmute(out int florinCost)
    {
        florinCost = 0;
        foreach (var g in guilds)
        {
            if (g == null) continue;
            int rank = g.RankForRep(GetRep(g.guildId));
            foreach (var p in g.perks)
            {
                if (p.type != GuildPerkType.UnlockTransmutation) continue;
                if (p.unlockRank <= rank)
                {
                    florinCost = Mathf.RoundToInt(p.magnitude);
                    return true;
                }
            }
        }
        return false;
    }

    // ── persistence (SaveSystem calls these; JsonUtility → parallel arrays) ──

    public void ExportTo(out string[] guildIds, out int[] reps)
    {
        guildIds = new string[_memberships.Count];
        reps = new int[_memberships.Count];
        for (int i = 0; i < _memberships.Count; i++)
        {
            guildIds[i] = _memberships[i].guildId;
            reps[i] = _memberships[i].rep;
        }
    }

    public void ImportFrom(string[] guildIds, int[] reps)
    {
        if (guildIds == null || reps == null || guildIds.Length != reps.Length) return;
        _memberships.Clear();
        for (int i = 0; i < guildIds.Length; i++)
            _memberships.Add(new GuildMembership { guildId = guildIds[i], rep = reps[i] });
    }
}
