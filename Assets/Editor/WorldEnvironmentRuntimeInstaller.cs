using UnityEditor;
using UnityEngine;

public static class WorldEnvironmentRuntimeInstaller
{
    const string Folder = "Assets/Resources/Environment";
    const string ProfilePath = Folder + "/WorldEnvironmentProfile.asset";

    [MenuItem("InfernosCurse/Environment/World Environment/4. Install Runtime Profile")]
    public static void Install()
    {
        EnsureFolder("Assets/Resources", "Environment");
        WorldEnvironmentProfile profile =
            AssetDatabase.LoadAssetAtPath<WorldEnvironmentProfile>(ProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<WorldEnvironmentProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
        }

        profile.fullCycleMinutes = 40f;
        profile.nightSpeedMultiplier = 1.75f;
        profile.dawnStartHour = 5f;
        profile.dayStartHour = 7f;
        profile.duskStartHour = 18f;
        profile.nightStartHour = 20f;
        profile.minimumTransitionGameMinutes = 10f;
        profile.maximumTransitionGameMinutes = 30f;
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        if (!profile.TryValidate(out string error))
            throw new System.InvalidOperationException(error);
        Debug.Log($"[WorldEnvironmentInstaller] PASS: installed {ProfilePath}; " +
                  "runtime attaches without modifying GameSystems.prefab.");
    }

    static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
    }
}
