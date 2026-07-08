using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Packages BattleArena's battle systems into ONE prefab that any grid-
// authored zone can spawn at encounter time (zones=battlemaps: the fight
// happens where you stand, no scene load). Reparents the live arena objects
// under a temp root so every cross-reference (cursor->CursorVisual, canvas
// children, manager events) survives into the prefab, then closes the arena
// WITHOUT saving — the arena scene is untouched.
public static class ZoneBattleKitMaker
{
    const string KitPath = "Assets/Prefabs/Battle/BattleKit.prefab";
    static readonly string[] KitRoots =
        { "BattleGrid", "BattleManager", "CursorVisual", "BattleCursor", "BattleCanvas", "BattleForecast" };

    [MenuItem("InfernosCurse/Zones/1. Make Battle Kit Prefab (from BattleArena)")]
    public static void Make()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/BattleArena.unity", OpenSceneMode.Single);
        var root = new GameObject("BattleKit");
        int found = 0;
        foreach (var name in KitRoots)
        {
            var go = scene.GetRootGameObjects().FirstOrDefault(g => g.name == name);
            if (go == null) { Debug.LogWarning($"[BattleKit] '{name}' not found in arena."); continue; }
            go.transform.SetParent(root.transform, true);
            found++;
        }
        PrefabUtility.SaveAsPrefabAsset(root, KitPath);
        // reopen arena fresh (discard our reparenting) — prefab already saved
        EditorSceneManager.OpenScene("Assets/Scenes/BattleArena.unity", OpenSceneMode.Single);
        Debug.Log($"[BattleKit] Saved {KitPath} with {found}/{KitRoots.Length} systems. " +
                  "Assign it to ZoneEncounterTrigger in grid-authored zones.");
    }
}
