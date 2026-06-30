using UnityEngine;

// Spawns the GameSystems prefab once at startup and keeps it alive across all scenes.
// Uses [RuntimeInitializeOnLoadMethod] — no scene setup required, ever.
public static class GameSystemsBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        // Use the dedicated root marker, not any individual system, so this stays
        // correct even if HubMap is ever placed in a scene on its own.
        if (GameObject.FindAnyObjectByType<GameSystemsRoot>() != null) return;

        var prefab = Resources.Load<GameObject>("GameSystems");
        if (prefab == null)
        {
            Debug.LogError("[GameSystemsBootstrap] Resources/GameSystems.prefab not found.");
            return;
        }

        var instance = Object.Instantiate(prefab);
        instance.name = "GameSystems";
        Object.DontDestroyOnLoad(instance);
        Debug.Log("[GameSystemsBootstrap] GameSystems spawned and persistent.");
    }
}
