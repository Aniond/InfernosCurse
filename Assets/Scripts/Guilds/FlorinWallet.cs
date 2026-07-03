using System;
using UnityEngine;

// The party's money — gold florins, Florence's own coin (minted 1252).
// Static like TravelIntent/GameClock: survives scene loads without a prefab;
// a mid-play domain reload wipes it (same accepted behavior as TravelIntent)
// and SaveData restores it on load.
public static class FlorinWallet
{
    public static int Balance { get; private set; }

    /// <summary>Fired with the new balance after any change.</summary>
    public static event Action<int> OnChanged;

    public static void Add(int amount, string reason)
    {
        if (amount <= 0) return;
        Balance += amount;
        Debug.Log($"[Florins] +{amount} ({reason}) → {Balance}");
        OnChanged?.Invoke(Balance);
    }

    /// <summary>Spend if affordable. False (and no change) when short.</summary>
    public static bool TrySpend(int amount, string reason)
    {
        if (amount < 0) return false;
        if (Balance < amount)
        {
            Debug.Log($"[Florins] cannot afford {amount} ({reason}) — have {Balance}");
            return false;
        }
        Balance -= amount;
        Debug.Log($"[Florins] -{amount} ({reason}) → {Balance}");
        OnChanged?.Invoke(Balance);
        return true;
    }

    /// <summary>Save-game restore — sets the balance outright.</summary>
    public static void SetBalance(int amount)
    {
        Balance = Mathf.Max(0, amount);
        OnChanged?.Invoke(Balance);
    }
}
