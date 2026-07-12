using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class GugolMapPlayModeVerifier
{
    const string PendingKey = "InfernosCurse.GugolMapPlayModeVerifier.Pending";
    const string ScenePath = "Assets/Scenes/MercatoVecchio.unity";

    static GugolMapPlayModeVerifier()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem("InfernosCurse/Validation/Run Gugol Mappe Play Mode Probe")]
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
        new GameObject("GugolMapPlayModeProbe").AddComponent<GugolMapPlayModeProbe>();
    }
}
