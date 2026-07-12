using System.Collections;
using System.Linq;
using UnityEngine;

// Mid-combat weather drama (David 7/08): every N enemy turns the weather can
// SHIFT. Classic mode (no key): weighted random from the real COZY profile
// list. AI mode: Gemini picks a profile + a one-line omen from battle
// context — but only from the ALLOWED profile names (the enemy may lie, the
// game may not; the LLM chooses among authored options, never invents).
// Applies through FlorenceWeather.Apply, so COZY owns the actual transition.
public class BattleWeatherDirector : MonoBehaviour
{
    public static bool HasLocalWeather { get; private set; }
    public static WorldWeatherState LocalWeather { get; private set; }

    [Tooltip("Enemy turns between possible weather shifts.")]
    public int shiftEveryNTurns = 4;
    [Tooltip("Chance a due shift actually happens (classic mode).")]
    [Range(0f, 1f)] public float shiftChance = 0.6f;

    string[] _profiles;
    int _turnCounter;
    bool _busy;

    void Start()
    {
        // profile names straight from the loaded COZY set — always valid
        _profiles = Resources.LoadAll<ScriptableObject>("Profiles/Weather Profiles")
            .Select(p => p.name).ToArray();

        SetLocalWeather(WorldEnvironmentState.CurrentWeather);

        if (BattleManager.Instance != null)
            BattleManager.Instance.OnStateChanged += OnBattleState;
    }

    void OnDestroy()
    {
        if (BattleManager.Instance != null)
            BattleManager.Instance.OnStateChanged -= OnBattleState;
        ClearLocalWeather();
    }

    void OnBattleState(BattleState s)
    {
        if (s != BattleState.EnemyTurn) return;
        _turnCounter++;
        if (_turnCounter < shiftEveryNTurns) return;
        _turnCounter = 0;
        if (Random.value <= shiftChance) RequestShift("mid-battle");
    }

    [ContextMenu("Test: Weather shift now")]
    public void TestShiftNow() => RequestShift("test");

    void RequestShift(string cause)
    {
        if (_busy || _profiles == null || _profiles.Length == 0) return;
        if (!GeminiClient.Available) { ApplyClassic(); return; }
        _busy = true;
        StartCoroutine(AskGemini(cause));
    }

    void ApplyClassic()
    {
        string pick = _profiles[Random.Range(0, _profiles.Length)];
        SetLocalWeather(pick);
        Debug.Log($"[WeatherDirector] (classic) weather shifts: {pick}");
    }

    IEnumerator AskGemini(string cause)
    {
        string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        string current = LocalWeather.sourceProfileName;
        string prompt =
            "You are the weather director of a dark tactical RPG set in 1299 Florence, " +
            "where an infernal curse creeps through the land. A battle is underway in '" + scene + "'. " +
            "Current weather: '" + current + "'. Trigger: " + cause + ". " +
            "Choose the next weather to heighten drama. You MUST pick exactly one profile name from this list: [" +
            string.Join(", ", _profiles) + "]. " +
            "Reply with ONLY two lines:\nPROFILE: <exact name from the list>\nOMEN: <one short in-world sentence a soldier might mutter>";

        yield return GeminiClient.Generate(prompt,
            reply =>
            {
                string profile = null, omen = null;
                foreach (var line in reply.Split('\n'))
                {
                    if (line.StartsWith("PROFILE:")) profile = line.Substring(8).Trim();
                    if (line.StartsWith("OMEN:")) omen = line.Substring(5).Trim();
                }
                // validate against the authored list — reject hallucinations
                string valid = _profiles.FirstOrDefault(p =>
                    string.Equals(p, profile, System.StringComparison.OrdinalIgnoreCase));
                if (valid != null)
                {
                    SetLocalWeather(valid);
                    Debug.Log($"[WeatherDirector] (Gemini) weather shifts: {valid} — \"{omen}\"");
                }
                else
                {
                    Debug.LogWarning($"[WeatherDirector] Gemini picked unknown profile '{profile}' — classic fallback.");
                    ApplyClassic();
                }
                _busy = false;
            },
            err =>
            {
                Debug.LogWarning($"[WeatherDirector] Gemini failed ({err}) — classic fallback.");
                ApplyClassic();
                _busy = false;
            });
    }

    public static void SetLocalWeather(string profileName)
    {
        LocalWeather = WorldWeatherClassifier.ClassifyOrClear(profileName);
        HasLocalWeather = true;
    }

    public static void SetLocalWeather(WorldWeatherState weather)
    {
        LocalWeather = weather.Clamped();
        HasLocalWeather = true;
    }

    public static void ClearLocalWeather() => HasLocalWeather = false;
}
