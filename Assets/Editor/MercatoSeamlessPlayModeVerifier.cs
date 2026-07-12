using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class MercatoSeamlessPlayModeVerifier
{
    const string PendingKey = "InfernosCurse.MercatoSeamlessPlayModeVerifier.Pending";

    static MercatoSeamlessPlayModeVerifier()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem("InfernosCurse/Validation/Run Mercato Seamless Play Mode Probe")]
    public static void Run()
    {
        SessionState.SetBool(PendingKey, true);
        EditorSceneManager.OpenScene(MercatoVecchioProductionBuilder.ScenePath, OpenSceneMode.Single);
        EditorApplication.delayCall += () => EditorApplication.isPlaying = true;
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PendingKey, false)) return;
        SessionState.EraseBool(PendingKey);
        new GameObject("MercatoSeamlessPlayModeProbe").AddComponent<MercatoSeamlessPlayModeProbe>();
    }
}
