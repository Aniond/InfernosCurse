using System.Collections.Generic;
using UnityEngine;

// Resting: the only way to recover outside battle, and the way time is spent.
// Every rest burns EXACTLY one day (the resource) and feeds the district's
// corruption a little — an inn night less so, a rough camp more, and an
// allied Albergatori guild inn can even cleanse. "Players should want a
// reason to go out and not hug the inn."
public static class RestSystem
{
    public const float MorningHour = 7f;

    // Inn nightly curse cost before guild discounts; camp is worse.
    public const float InnRestCurseCost = 0.02f;
    public const float CampRestCurseCost = 0.035f;
    public const float CampHealFraction = 0.6f;

    // The future party roster feeds this (empty today — battles refill HP/SP
    // on spawn, so nothing breaks; the hook exists so rest is roster-ready).
    public static readonly List<CombatantData> PartyMembers = new List<CombatantData>();

    /// <summary>Rest at an inn. False if the price can't be paid.</summary>
    public static bool RestAtInn(string nodeId, int basePrice, bool isGuildInn)
    {
        var guilds = GuildSystem.Instance;
        int price = Mathf.RoundToInt(basePrice * (guilds != null ? guilds.GetInnPriceMultiplier() : 1f));
        if (!FlorinWallet.TrySpend(price, "inn night")) return false;

        Heal(1f);
        AdvanceToMorning();

        float mult = guilds != null ? guilds.GetRestCurseCostMultiplier() : 1f;
        var hub = HubMap.Instance;
        if (hub != null)
        {
            hub.AddCurse(nodeId, InnRestCurseCost * mult);
            float cleanse = isGuildInn && guilds != null ? guilds.GetInnCleansePercent() : 0f;
            if (cleanse > 0f)
            {
                var node = hub.GetNode(nodeId);
                if (node != null && node.curseLevel > 0f)
                    hub.Cleanse(nodeId, node.curseLevel * cleanse);
            }
        }
        Debug.Log($"[Rest] inn night in {nodeId} (paid {price}, cost×{mult:F2}{(isGuildInn ? ", guild inn" : "")})");
        return true;
    }

    /// <summary>Camp rough wherever the player stands. Always available.</summary>
    public static void Camp(string nodeId)
    {
        Heal(CampHealFraction);
        AdvanceToMorning();
        // No guild discount — the Albergatori are the INNKEEPERS' guild; the
        // open night in a cursed city is always full exposure.
        HubMap.Instance?.AddCurse(nodeId, CampRestCurseCost);
        Debug.Log($"[Rest] camped rough in {nodeId} — the night takes its toll");
    }

    static void Heal(float fraction)
    {
        foreach (var member in PartyMembers)
        {
            if (member == null) continue;
            var total = member.GetTotalStats();
            member.currentHP = Mathf.Min(total.hpMax, member.currentHP + Mathf.CeilToInt(total.hpMax * fraction));
            member.currentSP = Mathf.Min(total.spMax, member.currentSP + Mathf.CeilToInt(total.spMax * fraction));
        }
    }

    // One rest = one day, at ANY hour. Explicit AdvanceDay (not SetHour-and-
    // let-wrap: resting after midnight would skip the burn) + ResyncClock so
    // the calendar's wrap detector doesn't read the backwards hour jump as a
    // second day. Weather re-rolls itself — it's a pure function of the date.
    static void AdvanceToMorning()
    {
        var cal = GameCalendar.Instance;
        if (cal != null)
        {
            cal.AdvanceDay();
            GameClock.SetHour(MorningHour);
            cal.ResyncClock();
        }
        else GameClock.SetHour(MorningHour);
    }
}
