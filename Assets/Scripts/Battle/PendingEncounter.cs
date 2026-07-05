using UnityEngine;
using System.Collections.Generic;

// Cross-scene carrier for a road encounter (TravelIntent idiom: static state
// that survives a SceneManager.LoadScene without any prefab). The map sets it
// when a journey is ambushed; EncounterBootstrap in the BattleArena consumes it
// and applies one of the two pre-resolved outcomes when the battle ends.
public static class PendingEncounter
{
    // Where the player ends up. For victory this is the interrupted journey's
    // destination (time advance included); for defeat it's the origin district.
    [System.Serializable]
    public class Destination
    {
        public string sceneName;
        public string entryId;
        public string nodeId;
        public int    days;    // 0 = same-day journey (SetHour path)
        public float  hours;
    }

    public class Payload
    {
        public string encounterNodeId;   // the waypoint that ambushed us
        public float  encounterCurse;    // node.curseLevel snapshot at roll time
        public int    seed;              // DaySeed ^ NodeSalt — enemy-composition determinism
        public Destination victory;
        public Destination defeat;
    }

    public static Payload Pending { get; private set; }
    // Consumed and live in the arena. BattleTestStarter checks this too — the
    // payload is consumed in EncounterBootstrap.Awake, before any Start runs.
    public static Payload Current { get; private set; }

    public static bool IsPending => Pending != null;

    public static void Set(Payload p) => Pending = p;

    public static Payload Consume()
    {
        Current = Pending;
        Pending = null;
        return Current;
    }

    // Outcome fully applied (or bootstrap bailed) — nothing in flight.
    public static void Complete() => Current = null;
}

// Deterministic road-encounter dice: a pure function of (calendar day, node),
// so reloading a save doesn't reroll the same journey. Mirrors the seeding
// idioms FlorenceWeather uses (its helpers are private — replicated here).
public static class EncounterRoll
{
    // "daySeed:nodeId" keys already fought today — set when an encounter FIRES,
    // so the return journey the same day doesn't re-trigger the identical
    // deterministic fight. In-memory only (wiped by domain reload; accepted).
    static readonly HashSet<string> _resolved = new();

    public static int DaySeed()
    {
        var cal = GameCalendar.Instance;
        return cal != null ? cal.Year * 1000 + cal.DayOfYear : 0;
    }

    // FNV-1a — stable across runtimes, unlike string.GetHashCode.
    public static int NodeSalt(string id)
    {
        unchecked
        {
            uint h = 2166136261;
            foreach (char c in id ?? "") { h ^= c; h *= 16777619; }
            return (int)h;
        }
    }

    public static bool ShouldTrigger(HubNode node, float baseChance, float curseScale, float maxChance)
    {
        if (node == null) return false;
        if (_resolved.Contains(DaySeed() + ":" + node.id)) return false;

        float chance = Mathf.Clamp(baseChance + node.curseLevel * curseScale, 0f, maxChance);
        var rng = new System.Random(DaySeed() ^ NodeSalt(node.id));
        return rng.NextDouble() < chance;
    }

    public static void MarkResolved(string nodeId) => _resolved.Add(DaySeed() + ":" + nodeId);
}
