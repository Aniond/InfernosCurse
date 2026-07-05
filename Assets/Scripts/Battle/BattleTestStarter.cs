using UnityEngine;
using System.Collections.Generic;

// Test-arena bootstrap: builds two small parties in code and starts a battle on
// scene load. This is the end-to-end harness for the FFT battle system — real
// encounters will replace this with an encounter/transition flow later.
public class BattleTestStarter : MonoBehaviour
{
    [Header("Party setup")]
    [Tooltip("Optional job for the second party member (e.g. Job_Baker). Its " +
             "unlocked actives are auto-equipped by BattleManager.")]
    public JobDefinition testJob;
    [Tooltip("Skills to pre-unlock on the test job (simulates AP purchases).")]
    public SkillDefinition[] preUnlockedJobSkills;

    [Header("Enemy skills")]
    public SkillDefinition[] enemySkills;

    void Start()
    {
        // A real road encounter owns this scene load — stand down.
        if (PendingEncounter.IsPending || PendingEncounter.Current != null) return;

        var bm = BattleManager.Instance;
        if (bm == null) { Debug.LogError("[BattleTestStarter] no BattleManager in scene"); return; }

        var players = new List<CombatantData>
        {
            MakeCombatant("Dante", CombatantRole.Dante,
                          str: 14, dex: 12, con: 12, cre: 8, faith: 12, per: 11, spd: 11, hp: 140, sp: 50),
            MakeCombatant("Benidito", CombatantRole.PartyMember,
                          str: 11, dex: 10, con: 10, cre: 14, faith: 9, per: 10, spd: 10, hp: 110, sp: 60),
        };

        // Give the party member the test job with some skills unlocked
        if (testJob != null)
        {
            var pm = players[1];
            pm.EquipJob(testJob);
            if (preUnlockedJobSkills != null && pm.activeJob != null)
                foreach (var learned in pm.activeJob.learnedSkills)
                    if (learned.skill != null && System.Array.IndexOf(preUnlockedJobSkills, learned.skill) >= 0)
                        learned.unlocked = true;
        }

        var enemies = new List<CombatantData>
        {
            MakeCombatant("Cursebearer", CombatantRole.Enemy,
                          str: 12, dex: 9, con: 9, cre: 6, faith: 6, per: 9, spd: 9, hp: 90, sp: 30),
            MakeCombatant("Ash Wretch", CombatantRole.Enemy,
                          str: 10, dex: 11, con: 8, cre: 6, faith: 5, per: 10, spd: 12, hp: 70, sp: 30),
        };

        // Hand-author enemy loadouts (EnsureBattleSkills won't override these)
        foreach (var e in enemies)
            if (enemySkills != null)
                for (int i = 0; i < enemySkills.Length && i < 4; i++)
                    e.equippedSkills.actives[i] = enemySkills[i];

        var playerSpawns = new List<Vector2Int> { new Vector2Int(3, 3), new Vector2Int(4, 2) };
        var enemySpawns  = new List<Vector2Int> { new Vector2Int(10, 8), new Vector2Int(9, 9) };

        bm.StartBattle(players, enemies, playerSpawns, enemySpawns);

        // Arena readability: tint player units blue, enemies red (placeholder art)
        foreach (var u in bm.Players)
        { var sr = u.GetComponentInChildren<SpriteRenderer>(); if (sr) sr.color = new Color(0.35f, 0.55f, 1f); }
        foreach (var u in bm.Enemies)
        { var sr = u.GetComponentInChildren<SpriteRenderer>(); if (sr) sr.color = new Color(1f, 0.35f, 0.3f); }
    }

    CombatantData MakeCombatant(string name, CombatantRole role,
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
        return c;
    }
}
