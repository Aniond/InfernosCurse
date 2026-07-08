using UnityEngine;

// The persistent party. Fills RestSystem.PartyMembers — the roster that
// rest-heal, the guild purification panel, and (now) road battles all share.
// Lives on the GameSystems prefab (wired by InfernosCurse/Battle Integration/
// 2. Setup GameSystems) so the job/skill asset references are serialized.
//
// Roster members are ScriptableObject instances created once per session:
// currentHP/SP, job AP/XP, and Benidito's absorbed skills accumulate on them
// across battles and scene loads. (Not yet in SaveData — known limitation.)
public class PartyRoster : MonoBehaviour
{
    [Header("Benidito (protagonist — Benidito role, absorbs skills)")]
    public JobDefinition beniditoJob;                  // Job_Baker
    public SkillDefinition[] beniditoPreUnlocked;      // AshwoodPeel, ErgotBloom

    [Header("Portraits (shown in the CT turn-order display)")]
    public Sprite beniditoPortrait;
    public Sprite pieroPortrait;

    void Awake() => EnsureInitialized();

    public static void EnsureInitialized()
    {
        if (RestSystem.PartyMembers.Count > 0) return;

        var inst = FindAnyObjectByType<PartyRoster>();
        RestSystem.PartyMembers.Add(MakeBenidito(inst));
        RestSystem.PartyMembers.Add(MakePiero(inst));
        Debug.Log("[PartyRoster] Roster initialized: Benidito + Piero.");
    }

    // Benidito IS the game's Benidito-role combatant: BattleUnit keeps the ORIGINAL
    // reference for Benidito-role units, so his absorbed skills, job progression,
    // and wounds land directly on this roster object.
    static CombatantData MakeBenidito(PartyRoster inst)
    {
        var c = MakeCombatant("Benidito", CombatantRole.Benidito,
            str: 14, dex: 12, con: 12, cre: 8, faith: 12, per: 11, spd: 11, hp: 140, sp: 50);

        if (inst != null && inst.beniditoJob != null)
        {
            c.EquipJob(inst.beniditoJob);
            if (inst.beniditoPreUnlocked != null && c.activeJob != null)
                foreach (var learned in c.activeJob.learnedSkills)
                    if (learned.skill != null &&
                        System.Array.IndexOf(inst.beniditoPreUnlocked, learned.skill) >= 0)
                        learned.unlocked = true;
        }
        else Debug.LogWarning("[PartyRoster] Benidito's job refs not wired — run Battle Integration setup.");

        if (inst != null) c.portrait = inst.beniditoPortrait;
        return c;
    }

    // Piero — a hired guard. PartyMember role (cloned in battle; the encounter
    // exit copies his progression back). No job yet: EnsureBattleSkills hands
    // him Basic Attack.
    static CombatantData MakePiero(PartyRoster inst)
    {
        var c = MakeCombatant("Piero", CombatantRole.PartyMember,
            str: 13, dex: 9, con: 13, cre: 6, faith: 8, per: 9, spd: 9, hp: 125, sp: 30);
        if (inst != null) c.portrait = inst.pieroPortrait;
        return c;
    }

    // Same construction shape as BattleTestStarter.MakeCombatant.
    static CombatantData MakeCombatant(string name, CombatantRole role,
        int str, int dex, int con, int cre, int faith, int per, int spd, int hp, int sp)
    {
        var c = ScriptableObject.CreateInstance<CombatantData>();
        c.displayName = name;
        c.role        = role;
        c.baseStats.strength     = str;
        c.baseStats.dexterity    = dex;
        c.baseStats.constitution = con;
        c.baseStats.creativity   = cre;
        c.baseStats.faith        = faith;
        c.baseStats.perception   = per;
        c.baseStats.speed        = spd;
        c.baseStats.hpMax        = hp;
        c.baseStats.spMax        = sp;
        c.InitRuntime();   // ONCE — battles preserve HP via snapshot/re-apply
        return c;
    }
}
