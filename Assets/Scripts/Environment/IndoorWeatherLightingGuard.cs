using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Keeps a mixed indoor/courtyard scene on a restrained interior ambient base
/// while COZY continues to own time, weather, fog, sky, and the directional sun.
/// Global sky ambient does not understand room walls, so interior scenes must
/// explicitly retain a flat ambient contribution and let shadowed lights provide
/// the spatial illumination.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(10000)]
public sealed class IndoorWeatherLightingGuard : MonoBehaviour
{
    [SerializeField, ColorUsage(false, true)]
    Color ambientColor = new Color(0.20f, 0.18f, 0.15f, 1f);

    [SerializeField, Min(0f)] float ambientIntensity = 1f;
    [SerializeField, Range(0f, 1f)] float reflectionIntensity = 0.45f;

    public Color AmbientColor
    {
        get => ambientColor;
        set => ambientColor = value;
    }

    public float AmbientIntensity
    {
        get => ambientIntensity;
        set => ambientIntensity = Mathf.Max(0f, value);
    }

    public float ReflectionIntensity
    {
        get => reflectionIntensity;
        set => reflectionIntensity = Mathf.Clamp01(value);
    }

    void OnEnable()
    {
        if (Application.isPlaying) ApplyNow();
    }

    void LateUpdate() => ApplyNow();

    public void ApplyNow()
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = ambientColor;
        RenderSettings.ambientIntensity = ambientIntensity;
        RenderSettings.reflectionIntensity = reflectionIntensity;
    }
}
