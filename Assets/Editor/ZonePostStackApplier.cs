using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// One-shot polish: the HD-2D zones shipped with NO post stack (the old
// HD2D_PostProcessing.asset was never wired — known decoy). This menu adds a
// shared ZoneVolume (bloom + vignette + gentle grading; no DoF — these are
// walkable scenes, not dioramas) and enables camera post-processing in every
// zone scene. COZY keeps owning light/fog/time. Re-runnable; skips cleanly.
public static class ZonePostStackApplier
{
    static readonly string[] Zones =
    {
        "Assets/Scenes/MercatoVecchio.unity",
        "Assets/Scenes/PonteVecchio.unity",
        "Assets/Scenes/Duomo.unity",
        "Assets/Scenes/GiardinoDelleRose.unity",
        "Assets/Scenes/PiazzaDellaSignoria.unity",
        "Assets/Scenes/SaloneDelleArti.unity",
        "Assets/Scenes/ViaCalimala.unity",
        "Assets/Scenes/Fiesole.unity",
    };

    [MenuItem("InfernosCurse/Polish/Add Zone Post Stack To All Zones")]
    public static void Apply()
    {
        var profile = EnsureProfile();
        int done = 0;
        foreach (var path in Zones)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) { Debug.LogWarning($"[ZonePost] missing {path}"); continue; }
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            // baseline BEFORE edits — zones legitimately contain "(1)" props
            // from manual dressing; the canary detects NEW dupes only.
            int baseDupes = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Count(t => t.name.Contains("(1)"));

            var volGo = GameObject.Find("ZoneVolume") ?? new GameObject("ZoneVolume");
            var vol = volGo.GetComponent<Volume>() ?? volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = profile;

            var cam = Camera.main;
            if (cam != null)
            {
                var data = cam.GetComponent<UniversalAdditionalCameraData>()
                           ?? cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
                data.renderPostProcessing = true;
            }

            // canaries before save
            int es = Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            int dupes = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Count(t => t.name.Contains("(1)"));
            if (es > 1 || dupes > baseDupes)
            {
                Debug.LogError($"[ZonePost] CANARY FAIL in {scene.name} (eventSystems={es} dupes={dupes}) — NOT saved.");
                continue;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            done++;
        }
        Debug.Log($"[ZonePost] Post stack applied to {done}/{Zones.Length} zones.");
    }

    static VolumeProfile EnsureProfile()
    {
        string path = "Assets/Settings/HD2D_ZonePost.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (profile != null) return profile;
        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, path);

        var bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(1.1f);
        bloom.intensity.Override(0.28f);

        var vig = profile.Add<Vignette>(true);
        vig.intensity.Override(0.16f);
        vig.smoothness.Override(0.55f);

        var ca = profile.Add<ColorAdjustments>(true);
        ca.saturation.Override(8f);
        ca.contrast.Override(4f);

        AssetDatabase.SaveAssets();
        return profile;
    }
}
