using System;
using UnityEditor;
using UnityEngine;

public static class CircleInsanitySetup
{
    public const string SettingsPath = "Assets/Resources/GameFeatureSettings.asset";
    public const string GameSystemsPath = "Assets/Resources/GameSystems.prefab";

    [MenuItem("InfernosCurse/Systems/Apply Circle and Insanity Setup")]
    public static void Apply()
    {
        ApplySettings();
        ApplyPresenterAuthoring();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        GameFeatures.ReloadForEditorTests();
        Debug.Log("[CircleInsanitySetup] Independent feature controls and production Insanity thresholds applied.");
    }

    static void ApplySettings()
    {
        var settings = AssetDatabase.LoadAssetAtPath<GameFeatureSettings>(SettingsPath);
        if (settings == null)
            throw new InvalidOperationException("Missing GameFeatureSettings at " + SettingsPath);

        settings.circleWorldEnabled = true;
        settings.playerInsanityEnabled = true;
        settings.insanityPresentationEnabled = false;
        settings.circleBattlePresentationEnabled = false;
        EditorUtility.SetDirty(settings);
    }

    static void ApplyPresenterAuthoring()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(GameSystemsPath);
        try
        {
            var presenter = root.GetComponentInChildren<InsanityPresenter>(true);
            if (presenter == null)
                throw new InvalidOperationException("GameSystems is missing InsanityPresenter.");

            presenter.darkenAt = 15;
            presenter.redWaterAt = 35;
            presenter.whispersAt = 55;
            presenter.fullEffectAt = 85;
            presenter.debugInsanityOverride = -1;
            EditorUtility.SetDirty(presenter);
            PrefabUtility.SaveAsPrefabAsset(root, GameSystemsPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
