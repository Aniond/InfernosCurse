using UnityEngine;

[CreateAssetMenu(fileName = "GameFeatureSettings", menuName = "InfernosCurse/Game Feature Settings")]
public sealed class GameFeatureSettings : ScriptableObject
{
    [Tooltip("Master switch for the world-corruption economy, presentation, encounter influence, and battle propagation.")]
    public bool corruptionEnabled;
}

/// <summary>
/// Central runtime feature switches. Missing settings fail closed so an absent
/// asset cannot accidentally reactivate a parked system.
/// </summary>
public static class GameFeatures
{
    const string ResourcePath = "GameFeatureSettings";
    static GameFeatureSettings _settings;
    static bool _loaded;

    public static bool CorruptionEnabled
    {
        get
        {
            if (!_loaded)
            {
                _settings = Resources.Load<GameFeatureSettings>(ResourcePath);
                _loaded = true;
            }
            return _settings != null && _settings.corruptionEnabled;
        }
    }

#if UNITY_EDITOR
    public static void ReloadForEditorTests()
    {
        _settings = null;
        _loaded = false;
    }
#endif
}
