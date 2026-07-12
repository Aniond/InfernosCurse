using System;
using System.Collections.Generic;
using DistantLands.Cozy;
using sc.stylizedgrass.runtime;
using UnityEngine;

/// <summary>
/// Project-wide adapter between persistent Florence weather and registered
/// Stylized Grass / Stylized Water surfaces. FlorenceWeather and COZY remain
/// the only weather owners.
/// </summary>
[DisallowMultipleComponent]
public sealed class WeatherSurfaceController : MonoBehaviour
{
    public enum Presentation
    {
        Clear,
        Cloudy,
        Fog,
        LightRain,
        HeavyRain,
        Storm
    }

    const float UpdateInterval = 0.1f;
    const float GlobalStormParticleBudget = 300f;

    static readonly List<WeatherSurface> Surfaces = new List<WeatherSurface>();
    static readonly int GrassWetnessId = Shader.PropertyToID("_GrassWetness");

    public static WeatherSurfaceController Instance { get; private set; }

    [Min(0.01f)] public float wetnessRisePerSecond = 0.35f;
    [Min(0.01f)] public float wetnessDryPerSecond = 0.045f;

    public Presentation CurrentPresentation { get; private set; } = Presentation.Clear;
    public float RainIntensity { get; private set; }
    public float GrassWetness { get; private set; }

    float _nextUpdate;
    WorldWeatherKind _lastWeatherKind = (WorldWeatherKind)(-1);
    float _targetRain;
    float _targetWetness;
    WindZone _lastCozyWindZone;

    public static void Register(WeatherSurface surface)
    {
        if (surface != null && !Surfaces.Contains(surface)) Surfaces.Add(surface);
        if (Instance != null) Instance.ApplySurfaces();
    }

    public static void Unregister(WeatherSurface surface)
    {
        if (surface != null) Surfaces.Remove(surface);
    }

    void OnEnable()
    {
        if (Instance != null && Instance != this)
        {
            enabled = false;
            return;
        }

        Instance = this;
        RefreshWeatherTargets(true);
        ApplyGrassIntegration();
        ApplySurfaces();
    }

    void OnDisable()
    {
        if (Instance != this) return;

        for (int i = Surfaces.Count - 1; i >= 0; i--)
            if (Surfaces[i] != null) Surfaces[i].ApplyRainRate(0f);

        Shader.SetGlobalFloat(GrassWetnessId, 0f);
        Instance = null;
    }

    void Update()
    {
        if (Time.unscaledTime < _nextUpdate) return;
        _nextUpdate = Time.unscaledTime + UpdateInterval;

        RefreshWeatherTargets(false);

        float rise = wetnessRisePerSecond * UpdateInterval;
        float fall = wetnessDryPerSecond * UpdateInterval;
        GrassWetness = Mathf.MoveTowards(GrassWetness, _targetWetness,
            _targetWetness > GrassWetness ? rise : fall);
        RainIntensity = Mathf.MoveTowards(RainIntensity, _targetRain, 1.75f * UpdateInterval);

        ApplyGrassIntegration();
        ApplySurfaces();
    }

    public void RefreshNow()
    {
        RefreshWeatherTargets(true);
        GrassWetness = _targetWetness;
        RainIntensity = _targetRain;
        ApplyGrassIntegration();
        ApplySurfaces();
    }

    void RefreshWeatherTargets(bool force)
    {
        WorldWeatherState weather = WorldEnvironmentState.CurrentWeather;
        if (!force && weather.kind == _lastWeatherKind) return;

        _lastWeatherKind = weather.kind;
        CurrentPresentation = Classify(weather);

        switch (CurrentPresentation)
        {
            case Presentation.LightRain:
                _targetRain = 0.35f;
                _targetWetness = 0.55f;
                break;
            case Presentation.HeavyRain:
                _targetRain = 0.78f;
                _targetWetness = 0.9f;
                break;
            case Presentation.Storm:
                _targetRain = 1f;
                _targetWetness = 1f;
                break;
            case Presentation.Fog:
                _targetRain = 0f;
                _targetWetness = 0.12f;
                break;
            default:
                _targetRain = 0f;
                _targetWetness = 0f;
                break;
        }
        _targetRain = Mathf.Max(_targetRain, weather.precipitation);
        _targetWetness = Mathf.Max(_targetWetness, weather.wetnessTarget);
    }

    void ApplyGrassIntegration()
    {
        Shader.SetGlobalFloat(GrassWetnessId, GrassWetness);

        if (WeatherShadingController.Instance != null)
        {
            WeatherShadingController.Instance.wetness = GrassWetness;
            WeatherShadingController.Instance.snowAmount = 0f;
        }

        var cozy = CozyWeather.instance;
        if (cozy != null && cozy.windModule != null && cozy.windModule.windZone != null)
            _lastCozyWindZone = cozy.windModule.windZone;

        if (WindController.Instance != null && _lastCozyWindZone != null &&
            WindController.Instance.windZone != _lastCozyWindZone)
        {
            WindController.Instance.windZone = _lastCozyWindZone;
        }
    }

    void ApplySurfaces()
    {
        float requested = 0f;
        for (int i = Surfaces.Count - 1; i >= 0; i--)
        {
            WeatherSurface surface = Surfaces[i];
            if (surface == null)
            {
                Surfaces.RemoveAt(i);
                continue;
            }
            if (surface.isActiveAndEnabled) requested += surface.DesiredRainRate(RainIntensity);
        }

        float budgetScale = requested > GlobalStormParticleBudget
            ? GlobalStormParticleBudget / requested
            : 1f;

        for (int i = 0; i < Surfaces.Count; i++)
        {
            WeatherSurface surface = Surfaces[i];
            if (surface != null && surface.isActiveAndEnabled)
                surface.ApplyRainRate(surface.DesiredRainRate(RainIntensity) * budgetScale);
        }
    }

    public static Presentation Classify(WorldWeatherState weather)
    {
        switch (weather.kind)
        {
            case WorldWeatherKind.Storm:
            case WorldWeatherKind.Hail: return Presentation.Storm;
            case WorldWeatherKind.HeavyRain: return Presentation.HeavyRain;
            case WorldWeatherKind.Drizzle:
            case WorldWeatherKind.Rain:
            case WorldWeatherKind.Sleet:
            case WorldWeatherKind.Snow: return Presentation.LightRain;
            case WorldWeatherKind.Fog: return Presentation.Fog;
            case WorldWeatherKind.PartlyCloudy:
            case WorldWeatherKind.Cloudy:
            case WorldWeatherKind.Wind: return Presentation.Cloudy;
            default: return Presentation.Clear;
        }
    }

    public static Presentation Classify(string profileName) =>
        Classify(WorldWeatherClassifier.ClassifyOrClear(profileName));
}
