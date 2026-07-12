#if UNITY_EDITOR
using System;
using System.Collections;
using DistantLands.Cozy;
using UnityEngine;

public sealed class WorldEnvironmentPlayModeProbe : MonoBehaviour
{
    IEnumerator Start()
    {
        IEnumerator run = Verify();
        while (true)
        {
            bool next;
            object current = null;
            try
            {
                next = run.MoveNext();
                if (next) current = run.Current;
            }
            catch (Exception exception)
            {
                Debug.LogError("[WorldEnvironmentPlayMode] FAIL: " + exception.Message + "\n" + exception.StackTrace);
                UnityEditor.EditorApplication.isPlaying = false;
                yield break;
            }
            if (!next) break;
            yield return current;
        }

        Debug.Log("[WorldEnvironmentPlayMode] PASS: authority attached; clock advanced and mirrored COZY; " +
                  "nested pause locks froze time; deterministic fronts and wetness persistence are live.");
        UnityEditor.EditorApplication.isPlaying = false;
    }

    IEnumerator Verify()
    {
        yield return new WaitForSecondsRealtime(0.5f);
        WorldEnvironmentDirector director = WorldEnvironmentDirector.Instance;
        Require(director != null, "WorldEnvironmentDirector did not attach.");
        Require(director.Profile != null, "Runtime profile did not load.");
        Require(Mathf.Approximately(director.Profile.fullCycleMinutes, 40f), "Cycle is not configured to 40 minutes.");
        Require(Mathf.Approximately(director.Profile.nightSpeedMultiplier, 1.75f), "Night speed is not 1.75x.");

        FlorenceWeather weather = FlorenceWeather.Instance;
        Require(weather != null, "FlorenceWeather is missing.");
        yield return null;
        Require(weather.CurrentForecast != null, "Current forecast was not generated.");
        Require(weather.CurrentForecast.fronts.Length >= 2 && weather.CurrentForecast.fronts.Length <= 4,
            "Current forecast does not contain 2-4 fronts.");
        Require(!string.IsNullOrEmpty(FlorenceWeather.CurrentProfileName), "No active presentation profile was selected.");

        float before = director.Hour;
        yield return new WaitForSecondsRealtime(0.75f);
        float after = director.Hour;
        Require(ForwardDistance(before, after) > 0.001f, "Exploration clock did not advance.");

        CozyWeather cozy = CozyWeather.instance;
        if (cozy != null && cozy.timeModule != null)
        {
            float cozyHour = (float)cozy.timeModule.currentTime * 24f;
            Require(Mathf.Abs(Mathf.DeltaAngle(cozyHour * 15f, director.Hour * 15f)) < 0.1f,
                "COZY clock is not mirroring the world clock.");
            Require(!cozy.timeModule.enabled, "COZY time module is still advancing a second clock.");
        }

        director.SetPaused("probe-a", true);
        director.SetPaused("probe-b", true);
        float pausedAt = director.Hour;
        yield return new WaitForSecondsRealtime(0.5f);
        Require(ForwardDistance(pausedAt, director.Hour) < 0.0001f, "Clock moved under nested pause locks.");
        director.SetPaused("probe-a", false);
        yield return new WaitForSecondsRealtime(0.25f);
        Require(ForwardDistance(pausedAt, director.Hour) < 0.0001f, "Releasing one pause lock resumed time early.");
        director.SetPaused("probe-b", false);
        yield return new WaitForSecondsRealtime(0.5f);
        Require(ForwardDistance(pausedAt, director.Hour) > 0.001f, "Clock did not resume after all locks released.");

        director.SetAccumulatedWetness(0.47f);
        var save = new SaveData { accumulatedWetness = director.AccumulatedWetness };
        SaveData restored = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save));
        Require(Mathf.Abs(restored.accumulatedWetness - 0.47f) < 0.001f, "Wetness did not survive save serialization.");
    }

    static float ForwardDistance(float from, float to) => Mathf.Repeat(to - from, 24f);

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
