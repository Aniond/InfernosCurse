using UnityEngine;
using UnityEngine.SceneManagement;
using DistantLands.Cozy;

// COZY owns the sun at runtime. Scenes keep their hand-authored directional
// lights so they still look right in edit mode; this sweeper disables those
// authored suns on every scene load so COZY's sun is the only directional
// light in play mode. Point/spot lights (torches) are untouched.
public class CozySceneAdapter : MonoBehaviour
{
    void OnEnable()
    {
        SceneManager.sceneLoaded += OnLoaded;
        Sweep();
    }

    void OnDisable() => SceneManager.sceneLoaded -= OnLoaded;

    void OnLoaded(Scene s, LoadSceneMode m) => Sweep();

    void Sweep()
    {
        var cozy = CozyWeather.instance;
        foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (l.type != LightType.Directional) continue;
            if (cozy != null && (l == cozy.sunLight || l.transform.IsChildOf(cozy.transform.root))) continue;
            l.enabled = false;
        }
    }
}
