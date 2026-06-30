using UnityEngine;
using UnityEngine.SceneManagement;

// Spawns the GameSystems prefab and keeps it alive across all scenes.
// Re-validates on every scene load so the persistent systems are guaranteed to
// exist no matter how a scene was entered (direct play, travel, reload).
public static class GameSystemsBootstrap
{
    static bool _hooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        EnsureSpawned();

        // Hook once so we re-check after every scene load.
        if (!_hooked)
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            _hooked = true;
        }
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => EnsureSpawned();

    static void EnsureSpawned()
    {
        // Already present (persistent instance survived) — nothing to do.
        if (Object.FindAnyObjectByType<GameSystemsRoot>() != null) return;

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
