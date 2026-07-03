using UnityEngine;
using UnityEngine.SceneManagement;

// Tracks which Florence district (HubMap node) the player is in, surviving
// scene loads. Attribution rule: a battle or rest belongs to the district you
// were LAST in — scenes with no node of their own (BattleArena, WorldMap)
// keep the previous value.
public static class DistrictTracker
{
    public static string CurrentNodeId { get; set; } = "mercato";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        var hub = HubMap.Instance;
        if (hub == null) return;

        foreach (var node in hub.AllNodes)
        {
            if (!string.IsNullOrEmpty(node.sceneName) && node.sceneName == scene.name)
            {
                CurrentNodeId = node.id;
                Debug.Log($"[DistrictTracker] {CurrentNodeId}");
                return;
            }
        }
        // No matching node (battle arena, world map, menus): keep last district.
    }
}
