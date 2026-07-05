using UnityEngine;

// Placeholder gate for the Florist NPC in Giardino delle Rose. The real quest
// (quest-giver identity, objective, completion trigger) is deliberately
// undesigned — see docs/superpowers/specs/2026-07-05-giardino-delle-rose-design.md
// "Deferred". This just keeps the NPC inert until a flag is set, so the zone
// is playable/testable now without the quest existing yet.
//
// PlayerPrefs is a stand-in persistence layer for this placeholder only — once
// the real quest system exists, replace with its flag storage (do not extend
// this pattern to other quests).
public class FloristUnlockGate : MonoBehaviour
{
    public const string UnlockFlagKey = "Quest_GiardinoDelleRose_Complete";

    void Awake()
    {
        bool unlocked = PlayerPrefs.GetInt(UnlockFlagKey, 0) == 1;
        gameObject.SetActive(unlocked);
    }

    // Editor/test hook — call to simulate quest completion without a real quest system.
    public static void DebugUnlock()
    {
        PlayerPrefs.SetInt(UnlockFlagKey, 1);
        PlayerPrefs.Save();
    }
}
