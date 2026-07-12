using UnityEngine;

[CreateAssetMenu(fileName = "GameFeatureSettings", menuName = "InfernosCurse/Game Feature Settings")]
public sealed class GameFeatureSettings : ScriptableObject
{
    [Tooltip("Legacy serialized switch retained for save/project migration only. Runtime code uses the independent controls below.")]
    public bool corruptionEnabled;

    [Header("Circle Influence")]
    [Tooltip("Enables hidden territory Circle state, sources, warnings, NPC effects, and world encounters.")]
    public bool circleWorldEnabled = true;
    [Tooltip("Enables Circle-specific terrain and automata inside tactical battles. Tactical fog remains independent.")]
    public bool circleBattlePresentationEnabled;

    [Header("Player Insanity")]
    [Tooltip("Enables Benidito's loadout-derived Insanity calculation and deterministic combat penalties.")]
    public bool playerInsanityEnabled = true;
    [Tooltip("Enables personal audiovisual Insanity presentation. Calculation and penalties remain independent.")]
    public bool insanityPresentationEnabled;
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

    static GameFeatureSettings Settings
    {
        get
        {
            if (!_loaded)
            {
                _settings = Resources.Load<GameFeatureSettings>(ResourcePath);
                _loaded = true;
            }
            return _settings;
        }
    }

    public static bool CircleWorldEnabled => Settings != null && Settings.circleWorldEnabled;
    public static bool CircleBattlePresentationEnabled =>
        Settings != null && Settings.circleBattlePresentationEnabled;
    public static bool PlayerInsanityEnabled => Settings != null && Settings.playerInsanityEnabled;
    public static bool InsanityPresentationEnabled =>
        Settings != null && Settings.insanityPresentationEnabled;

    // Temporary source-compatibility alias while the remaining Circle callers
    // migrate. It no longer reads the legacy serialized master switch.
    public static bool CorruptionEnabled => CircleWorldEnabled;

#if UNITY_EDITOR
    public static void ReloadForEditorTests()
    {
        _settings = null;
        _loaded = false;
    }
#endif
}
