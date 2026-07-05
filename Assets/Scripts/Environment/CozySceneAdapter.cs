using UnityEngine;
using UnityEngine.SceneManagement;
using DistantLands.Cozy;

// COZY owns the sun at runtime. Scenes keep their hand-authored directional
// lights so they still look right in edit mode; this sweeper disables those
// authored suns on every scene load so COZY's sun is the only directional
// light in play mode. Point/spot lights (torches) are untouched.
//
// It also owns the WEATHER GUARD: the persistent weather sphere would flood
// the ortho battle scenes with sky/fog, so it's deactivated there. A plain
// SetActive(false) freezes COZY time exactly (CozyTimeModule.Update stops;
// GameClock.HasClock stays true because the C# instance survives) and the
// last weather profile stays resident — sky and clock resume unchanged when
// a normal scene loads. No ResyncClock needed: the hour never moves.
public class CozySceneAdapter : MonoBehaviour
{
    [Tooltip("Scenes where the COZY sphere is suspended (2D battle scenes).")]
    public string[] blockedScenes = { "Battle", "BattleArena" };

    GameObject _cozyRoot;   // cached while available — for re-enabling

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnLoaded;
        Apply(SceneManager.GetActiveScene().name);
    }

    void OnDisable() => SceneManager.sceneLoaded -= OnLoaded;

    void OnLoaded(Scene s, LoadSceneMode m) => Apply(s.name);

    void Apply(string sceneName)
    {
        // Cache the sphere while COZY is discoverable (instance goes stale
        // while the object is inactive, so keep our own reference).
        var cozy = CozyWeather.instance;
        if (cozy != null) _cozyRoot = cozy.gameObject;

        bool blocked = false;
        foreach (var b in blockedScenes)
            if (sceneName == b) { blocked = true; break; }

        if (blocked)
        {
            if (_cozyRoot != null && _cozyRoot.activeSelf)
            {
                _cozyRoot.SetActive(false);
                Debug.Log($"[CozySceneAdapter] Weather suspended for '{sceneName}' (time frozen).");
            }
            return;   // no sun sweep — battle scenes light themselves
        }

        if (_cozyRoot != null && !_cozyRoot.activeSelf)
        {
            _cozyRoot.SetActive(true);
            Debug.Log("[CozySceneAdapter] Weather resumed.");
        }
        Sweep();
    }

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
