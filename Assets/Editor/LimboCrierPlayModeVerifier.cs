using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class LimboCrierPlayModeVerifier
{
    const string PendingKey = "InfernosCurse.LimboCrierPlayModeVerifier.Pending";
    const string InteractiveKey = "InfernosCurse.LimboCrierPlayModeVerifier.Interactive";

    static LimboCrierPlayModeVerifier()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    // Batch entry point. Do not combine with -quit: the runtime probe exits the
    // editor after walking the complete exploration -> combat -> victory path.
    public static void RunBatch()
    {
        SessionState.SetBool(InteractiveKey, false);
        QueueRun();
    }

    [MenuItem("InfernosCurse/Validation/Run Limbo Crier Interactive Play Mode Probe")]
    public static void RunInteractive()
    {
        SessionState.SetBool(InteractiveKey, true);
        QueueRun();
    }

    static void QueueRun()
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
        bool interactive = SessionState.GetBool(InteractiveKey, false);
        SessionState.EraseBool(InteractiveKey);
        var probe = new GameObject("LimboCrierPlayModeProbe").AddComponent<LimboCrierPlayModeProbe>();
        probe.exitEditorOnComplete = !interactive;
    }
}
