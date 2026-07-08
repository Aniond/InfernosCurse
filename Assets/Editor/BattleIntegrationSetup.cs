using System.Linq;
using UnityEditor;
using UnityEngine;

// One-shot, idempotent wiring for the battle integration phase (GugolMappeSetup
// precedent). Run in order, then rebuild the arena:
//   1. Create Enemy Assets   — data-driven road enemies (CombatantData .assets).
//   2. Setup GameSystems     — PartyRoster on the prefab + FastTravelMenu's
//                              serialized blockedScenes gains "BattleArena".
//   3. Add BattleArena To Build Settings.
public static class BattleIntegrationSetup
{
    const string PrefabPath   = "Assets/Resources/GameSystems.prefab";
    const string CombatantDir = "Assets/Data/Combatants";
    const string ArenaScene   = "Assets/Scenes/BattleArena.unity";

    // ── 1. Enemy assets ────────────────────────────────────────────────────────

    [MenuItem("InfernosCurse/Battle Integration/1. Create Enemy Assets")]
    public static void CreateEnemyAssets()
    {
        if (!AssetDatabase.IsValidFolder(CombatantDir))
            AssetDatabase.CreateFolder("Assets/Data", "Combatants");

        var curseClaw = AssetDatabase.LoadAssetAtPath<SkillDefinition>("Assets/Data/Skills/Skill_CurseClaw.asset");
        if (curseClaw == null)
        {
            Debug.LogError("[BattleIntegration] Skill_CurseClaw.asset not found — aborting.");
            return;
        }

        // Stats verbatim from the verified test harness. learnableSkills arms
        // Benidito's Dante-role absorb (the harness never exercised drops).
        CreateEnemy("Enemy_Cursebearer", "Cursebearer", curseClaw,
            str: 12, dex: 9, con: 9, cre: 6, faith: 6, per: 9, spd: 9, hp: 90, sp: 30);
        CreateEnemy("Enemy_AshWretch", "Ash Wretch", curseClaw,
            str: 10, dex: 11, con: 8, cre: 6, faith: 5, per: 10, spd: 12, hp: 70, sp: 30);

        AssetDatabase.SaveAssets();
        Debug.Log("[BattleIntegration] Enemy assets ready in " + CombatantDir);
    }

    static void CreateEnemy(string fileName, string displayName, SkillDefinition claw,
        int str, int dex, int con, int cre, int faith, int per, int spd, int hp, int sp)
    {
        string path = $"{CombatantDir}/{fileName}.asset";
        if (AssetDatabase.LoadAssetAtPath<CombatantData>(path) != null)
        {
            Debug.Log($"[BattleIntegration] {path} already exists — skipped.");
            return;
        }

        var c = ScriptableObject.CreateInstance<CombatantData>();
        c.displayName = displayName;
        c.role        = CombatantRole.Enemy;
        c.baseStats.strength     = str;
        c.baseStats.dexterity    = dex;
        c.baseStats.constitution = con;
        c.baseStats.creativity   = cre;
        c.baseStats.faith        = faith;
        c.baseStats.perception   = per;
        c.baseStats.speed        = spd;
        c.baseStats.hpMax        = hp;
        c.baseStats.spMax        = sp;
        // Hand-authored loadout: EnsureBattleSkills fills only null slots, so
        // Curse Claw stays in slot 0 and Basic Attack lands beside it.
        c.equippedSkills.actives[0] = claw;
        c.learnableSkills = new[] { claw };
        c.skillDropChance = 0.3f;

        AssetDatabase.CreateAsset(c, path);
        Debug.Log($"[BattleIntegration] Created {path}");
    }

    // ── 2. GameSystems wiring ──────────────────────────────────────────────────

    [MenuItem("InfernosCurse/Battle Integration/2. Setup GameSystems")]
    public static void SetupGameSystems()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var roster = root.GetComponent<PartyRoster>();
            if (roster == null) roster = root.AddComponent<PartyRoster>();
            roster.beniditoJob = AssetDatabase.LoadAssetAtPath<JobDefinition>("Assets/Data/Jobs/Baker/Job_Baker.asset");
            // No pre-unlocked skills: Ben starts with basic Attack only and
            // EARNS Baker skills through job AP (David 7/08).

            // The prefab's serialized blockedScenes predates BattleArena — a
            // code-default change never reaches existing serialized data.
            var travel = root.GetComponent<FastTravelMenu>();
            if (travel != null && !travel.blockedScenes.Contains("BattleArena"))
                travel.blockedScenes = travel.blockedScenes.Append("BattleArena").ToArray();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log($"[BattleIntegration] GameSystems wired: PartyRoster " +
                      $"(job={(roster.beniditoJob != null)}), FastTravelMenu blockedScenes patched.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ── 4. Asset registry (save-game name→asset restore) ──────────────────────

    [MenuItem("InfernosCurse/Battle Integration/4. Populate Asset Registry")]
    public static void PopulateAssetRegistry()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var registry = root.GetComponent<AssetRegistry>();
            if (registry == null) registry = root.AddComponent<AssetRegistry>();

            registry.allSkills = AssetDatabase.FindAssets("t:SkillDefinition", new[] { "Assets/Data" })
                .Select(g => AssetDatabase.LoadAssetAtPath<SkillDefinition>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(s => s != null)
                .ToArray();
            registry.allJobs = AssetDatabase.FindAssets("t:JobDefinition", new[] { "Assets/Data" })
                .Select(g => AssetDatabase.LoadAssetAtPath<JobDefinition>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(j => j != null)
                .ToArray();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log($"[BattleIntegration] AssetRegistry populated: {registry.allSkills.Length} skills, " +
                      $"{registry.allJobs.Length} jobs. Re-run after adding new skill/job assets.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ── 3. Build settings ──────────────────────────────────────────────────────

    [MenuItem("InfernosCurse/Battle Integration/3. Add BattleArena To Build Settings")]
    public static void AddArenaToBuildSettings()
    {
        EnsureArenaInBuildSettings();
        Debug.Log("[BattleIntegration] Build scenes: " +
                  string.Join(", ", EditorBuildSettings.scenes.Select(s => s.path)));
    }

    public static void EnsureArenaInBuildSettings()
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == ArenaScene)) return;
        scenes.Add(new EditorBuildSettingsScene(ArenaScene, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log("[BattleIntegration] BattleArena added to Build Settings.");
    }
}
