using System;
using System.Collections.Generic;
using UnityEngine;

// APPEND ONLY. Serialized in saves and authored assets.
public enum CircleId
{
    Limbo = 0,
    Lust = 1,
    Gluttony = 2,
    Greed = 3,
    Anger = 4,
    Heresy = 5,
    Violence = 6,
    Fraud = 7,
    Treachery = 8,
}

[Serializable]
public class CircleInfluenceState
{
    public CircleId circle = CircleId.Limbo;
    [Range(0f, 1f)] public float value;

    public CircleInfluenceState() { }

    public CircleInfluenceState(CircleId circle, float value)
    {
        this.circle = circle;
        this.value = Mathf.Clamp01(value);
    }
}

[Serializable]
public class CircleInfluenceSeed
{
    public CircleId circle = CircleId.Limbo;
    [Range(0f, 1f)] public float value;
}

public static class CircleInfluenceLedger
{
    public static float Get(IList<CircleInfluenceState> states, CircleId circle)
    {
        if (states == null) return 0f;
        for (int i = 0; i < states.Count; i++)
        {
            var state = states[i];
            if (state != null && state.circle == circle)
                return Mathf.Clamp01(state.value);
        }
        return 0f;
    }

    public static bool Set(List<CircleInfluenceState> states, CircleId circle, float value)
    {
        if (states == null) return false;
        float clamped = Mathf.Clamp01(value);
        for (int i = 0; i < states.Count; i++)
        {
            var state = states[i];
            if (state == null || state.circle != circle) continue;
            if (clamped <= 0f)
            {
                states.RemoveAt(i);
                RemoveAll(states, circle);
                return true;
            }
            if (Mathf.Approximately(state.value, clamped)) return false;
            state.value = clamped;
            RemoveDuplicates(states, circle, i);
            return true;
        }

        if (clamped <= 0f) return false;
        states.Add(new CircleInfluenceState(circle, clamped));
        return true;
    }

    public static bool Add(List<CircleInfluenceState> states, CircleId circle, float delta)
    {
        if (Mathf.Approximately(delta, 0f)) return false;
        return Set(states, circle, Get(states, circle) + delta);
    }

    public static CircleId Dominant(IList<CircleInfluenceState> states, CircleId fallback)
    {
        CircleId result = fallback;
        float highest = Get(states, fallback);
        if (states == null) return result;

        for (int i = 0; i < states.Count; i++)
        {
            var state = states[i];
            if (state == null) continue;
            float value = Mathf.Clamp01(state.value);
            if (value > highest)
            {
                highest = value;
                result = state.circle;
            }
        }
        return result;
    }

    public static void Normalize(List<CircleInfluenceState> states)
    {
        if (states == null) return;
        var seen = new HashSet<CircleId>();
        for (int i = states.Count - 1; i >= 0; i--)
        {
            var state = states[i];
            if (state == null || state.value <= 0f || !seen.Add(state.circle))
            {
                states.RemoveAt(i);
                continue;
            }
            state.value = Mathf.Clamp01(state.value);
        }
    }

    static void RemoveDuplicates(List<CircleInfluenceState> states, CircleId circle, int keep)
    {
        for (int i = states.Count - 1; i >= 0; i--)
            if (i != keep && states[i] != null && states[i].circle == circle)
                states.RemoveAt(i);
    }

    static void RemoveAll(List<CircleInfluenceState> states, CircleId circle)
    {
        for (int i = states.Count - 1; i >= 0; i--)
            if (states[i] != null && states[i].circle == circle)
                states.RemoveAt(i);
    }
}
