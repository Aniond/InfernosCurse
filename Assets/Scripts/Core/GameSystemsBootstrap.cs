using UnityEngine;

// Spawns the GameSystems prefab once at startup and keeps it alive across all scenes.
// Uses [RuntimeInitializeOnLoadMethod] — no scene setup required, ever.
public static class GameSystemsBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        if (GameObject.FindAnyObjectByType<HubMap>() != null) return;

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
