using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class WorldEnvironmentPlayModeVerifier
{
    const string PendingKey = "InfernosCurse.WorldEnvironmentPlayMode.Pending";
    const string ScenePath = "Assets/Scenes/MercatoVecchio.unity";

    static WorldEnvironmentPlayModeVerifier()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem("InfernosCurse/Environment/World Environment/5. Run Play Mode Probe")]
    public static void Run()
    {
        SessionState.SetBool(PendingKey, true);
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EditorApplication.delayCall += () => EditorApplication.isPlaying = true;
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PendingKey, false)) return;
        SessionState.EraseBool(PendingKey);
        new GameObject("WorldEnvironmentPlayModeProbe").AddComponent<WorldEnvironmentPlayModeProbe>();
    }
}
