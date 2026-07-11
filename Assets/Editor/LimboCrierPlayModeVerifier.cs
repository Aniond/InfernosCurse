using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class LimboCrierPlayModeVerifier
{
    const string PendingKey = "InfernosCurse.LimboCrierPlayModeVerifier.Pending";

    static LimboCrierPlayModeVerifier()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    // Batch entry point. Do not combine with -quit: the runtime probe exits the
    // editor after walking the complete exploration -> combat -> victory path.
    public static void RunBatch()
    {
        SessionState.SetBool(PendingKey, true);
        EditorSceneManager.OpenScene(
            LimboCrierEncounterBuilder.MercatoScenePath, OpenSceneMode.Single);
        EditorApplication.delayCall += () => EditorApplication.isPlaying = true;
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PendingKey, false)) return;
        SessionState.EraseBool(PendingKey);
        new GameObject("LimboCrierPlayModeProbe").AddComponent<LimboCrierPlayModeProbe>();
    }
}
