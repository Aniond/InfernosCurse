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
    public JobDefinition beniditoJob;                  // Job_Hero (no skills — absorption is his tree)

    [Header("Portraits (shown in the CT turn-order display)")]
    public Sprite beniditoPortrait;

    [Header("Benidito starter equipment")]
    public EquipmentDefinition beniditoWeapon;     // Item_AshStaff
    public EquipmentDefinition beniditoArmor;      // Item_PenitentRobe
    public EquipmentDefinition beniditoAccessory;  // Item_TarnishedRing

    void Awake() => EnsureInitialized();

    public static void EnsureInitialized()
    {
        if (RestSystem.PartyMembers.Count > 0) return;

        var inst = FindAnyObjectByType<PartyRoster>();
        RestSystem.PartyMembers.Add(MakeBenidito(inst));
        // Party is Benidito ALONE until David adds companions himself —
        // a prior session invented a "Piero" test guard he never asked for
        // (removed 7/08). Recruits come from story/jobs, not from code.
        Debug.Log("[PartyRoster] Roster initialized: Benidito.");
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
            // Hero job: level/stat growth ONLY. It has no skill tree — Ben's
            // skills come from the monsters he kills and absorbs (David 7/08).
            c.EquipJob(inst.beniditoJob);
        }
        else Debug.LogWarning("[PartyRoster] Benidito's job refs not wired — run Battle Integration setup.");

        if (inst != null)
        {
            c.portrait  = inst.beniditoPortrait;
            c.weapon    = inst.beniditoWeapon;
            c.armor     = inst.beniditoArmor;
            c.accessory = inst.beniditoAccessory;
            c.InitRuntime();   // re-init: equipment hp/sp bonuses land in current pools
        }
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
