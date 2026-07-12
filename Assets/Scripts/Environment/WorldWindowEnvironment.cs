using System;
using UnityEngine;
using DistantLands.Cozy;

/// <summary>
/// Read-only presentation adapter for building windows and local interior lights.
/// GameClock, FlorenceWeather, and COZY remain the authoritative owners of time,
/// weather, sun, sky, precipitation, and thunder spawning.
/// </summary>
[DisallowMultipleComponent]
public sealed class WorldWindowEnvironment : MonoBehaviour
{
    public enum WindowRole
    {
        InteriorLookingOut,
        ExteriorOccupied
    }

    public enum WeatherKind
    {
        Clear,
        Cloudy,
        Rain,
        Storm,
        Fog,
        Snow
    }

    [Flags]
    public enum WeatherMask
    {
        None = 0,
        Clear = 1 << 0,
        Cloudy = 1 << 1,
        Rain = 1 << 2,
        Storm = 1 << 3,
        Fog = 1 << 4,
        Snow = 1 << 5,
        Wet = Rain | Storm,
        All = Clear | Cloudy | Rain | Storm | Fog | Snow
    }

    [Serializable]
    public struct WindowSurface
    {
        public Renderer renderer;
        public WindowRole role;
        [ColorUsage(false, true)] public Color emissionTint;
        [Min(0f)] public float emissionMultiplier;
        [Tooltip("Additional lightning response. Zero disables lightning on this surface.")]
        [Min(0f)] public float lightningMultiplier;
    }

    [Serializable]
    public struct DrivenLight
    {
        public Light light;
        [Min(0f)] public float daylightIntensity;
        [Min(0f)] public float nightIntensity;
        public Color daylightColor;
        public Color nightColor;
        [Tooltip("Additional intensity supplied by the authoritative COZY thunder light.")]
        [Min(0f)] public float lightningIntensity;
    }

    [Serializable]
    public struct WeatherObject
    {
        public GameObject target;
        public WeatherMask visibleDuring;
    }

    [Header("Building configuration")]
    [SerializeField] private WindowSurface[] windows = Array.Empty<WindowSurface>();
    [SerializeField] private DrivenLight[] localLights = Array.Empty<DrivenLight>();
    [Tooltip("Rain, fog, cloud, or snow objects positioned outside usable windows.")]
    [SerializeField] private WeatherObject[] exteriorWeatherObjects = Array.Empty<WeatherObject>();

    [Header("Time profile")]
    [SerializeField, Range(0f, 24f)] private float dawnStart = 5.5f;
    [SerializeField, Range(0f, 24f)] private float dayStart = 7.5f;
    [SerializeField, Range(0f, 24f)] private float duskStart = 17.5f;
    [SerializeField, Range(0f, 24f)] private float nightStart = 20f;
    [SerializeField, Min(0f)] private float interiorNightGlow = 0.12f;
    [SerializeField, Min(0f)] private float exteriorNightGlow = 1.8f;
    [SerializeField, ColorUsage(false, true)] private Color nightExteriorTint = new Color(0.18f, 0.28f, 0.55f, 1f);
    [SerializeField, ColorUsage(false, true)] private Color dayExteriorTint = new Color(0.78f, 0.90f, 1f, 1f);
    [SerializeField, ColorUsage(false, true)] private Color dawnDuskExteriorTint = new Color(1f, 0.52f, 0.22f, 1f);

    [Header("Weather profile")]
    [SerializeField, Range(0f, 1f)] private float cloudyLight = 0.72f;
    [SerializeField, Range(0f, 1f)] private float rainLight = 0.55f;
    [SerializeField, Range(0f, 1f)] private float stormLight = 0.30f;
    [SerializeField, Range(0f, 1f)] private float fogLight = 0.48f;
    [SerializeField, Range(0f, 1f)] private float snowLight = 0.78f;
    [SerializeField, Min(0.01f)] private float thunderReferenceIntensity = 3f;
    [SerializeField] private bool warnIfWorldServicesMissing = true;

    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");

    static int _sampleFrame = -1;
    static int _thunderChildCount = -1;
    static Light[] _thunderLights = Array.Empty<Light>();
    static float _lightningSample;

    bool _warnedMissingServices;
    WeatherKind _lastWeather = (WeatherKind)(-1);
    MaterialPropertyBlock _propertyBlock;

    public WindowSurface[] Windows
    {
        get => windows;
        set => windows = value ?? Array.Empty<WindowSurface>();
    }

    public DrivenLight[] LocalLights
    {
        get => localLights;
        set => localLights = value ?? Array.Empty<DrivenLight>();
    }

    public WeatherObject[] ExteriorWeatherObjects
    {
        get => exteriorWeatherObjects;
        set => exteriorWeatherObjects = value ?? Array.Empty<WeatherObject>();
    }

    public WeatherKind CurrentWeather => ClassifyWeather(WorldEnvironmentState.CurrentWeather);

    void OnEnable()
    {
        if (_propertyBlock == null) _propertyBlock = new MaterialPropertyBlock();
        _lastWeather = (WeatherKind)(-1);
        ApplyPresentation();
    }

    void LateUpdate() => ApplyPresentation();

    void ApplyPresentation()
    {
        bool hasClock = GameClock.HasClock;
        bool hasWeather = FlorenceWeather.Instance != null || !string.IsNullOrEmpty(FlorenceWeather.CurrentProfileName);
        if (Application.isPlaying && warnIfWorldServicesMissing && !_warnedMissingServices && (!hasClock || !hasWeather))
        {
            _warnedMissingServices = true;
            Debug.LogWarningFormat(this,
                "[WorldWindowEnvironment] '{0}' is using the clear-noon fallback because world services are unavailable (clock={1}, weather={2}).",
                name, hasClock, hasWeather);
        }

        float hour = hasClock ? GameClock.Hour : 12f;
        float daylight = DaylightAt(hour);
        WeatherKind weather = hasWeather ? ClassifyWeather(WorldEnvironmentState.CurrentWeather) : WeatherKind.Clear;
        float weatherLight = WeatherLightMultiplier(weather);
        float lightning = SampleAuthoritativeLightning(thunderReferenceIntensity);

        ApplyWindows(hour, daylight, weatherLight, lightning);
        ApplyLights(daylight, weatherLight, lightning);

        if (weather != _lastWeather)
        {
            _lastWeather = weather;
            ApplyWeatherObjects(weather);
        }
    }

    void ApplyWindows(float hour, float daylight, float weatherLight, float lightning)
    {
        Color timeTint = ExteriorTintAt(hour, daylight);
        for (int i = 0; i < windows.Length; i++)
        {
            WindowSurface surface = windows[i];
            if (surface.renderer == null) continue;

            float timeGlow = surface.role == WindowRole.InteriorLookingOut
                ? Mathf.Lerp(interiorNightGlow, 1f, daylight) * weatherLight
                : Mathf.Lerp(exteriorNightGlow, 0.08f, daylight) * weatherLight;
            float glow = surface.emissionMultiplier * timeGlow + lightning * surface.lightningMultiplier;
            Color roleTint = surface.role == WindowRole.InteriorLookingOut
                ? surface.emissionTint * timeTint
                : surface.emissionTint;
            Color emission = roleTint * Mathf.Max(0f, glow);

            surface.renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(EmissionColorId, emission);
            _propertyBlock.SetColor(BaseColorId, emission * 0.18f);
            _propertyBlock.SetColor(ColorId, emission * 0.18f);
            surface.renderer.SetPropertyBlock(_propertyBlock);
            _propertyBlock.Clear();
        }
    }

    void ApplyLights(float daylight, float weatherLight, float lightning)
    {
        for (int i = 0; i < localLights.Length; i++)
        {
            DrivenLight driven = localLights[i];
            if (driven.light == null) continue;

            float normal = Mathf.Lerp(driven.nightIntensity, driven.daylightIntensity, daylight);
            driven.light.intensity = normal * weatherLight + lightning * driven.lightningIntensity;
            driven.light.color = Color.Lerp(driven.nightColor, driven.daylightColor, daylight);
            driven.light.enabled = driven.light.intensity > 0.001f;
        }
    }

    void ApplyWeatherObjects(WeatherKind weather)
    {
        WeatherMask activeMask = MaskFor(weather);
        for (int i = 0; i < exteriorWeatherObjects.Length; i++)
        {
            WeatherObject weatherObject = exteriorWeatherObjects[i];
            if (weatherObject.target == null) continue;
            bool active = (weatherObject.visibleDuring & activeMask) != 0;
            if (weatherObject.target.activeSelf != active)
                weatherObject.target.SetActive(active);
        }
    }

    float DaylightAt(float hour)
    {
        float dawn = Mathf.InverseLerp(dawnStart, dayStart, hour);
        float dusk = 1f - Mathf.InverseLerp(duskStart, nightStart, hour);
        return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(dawn * dusk));
    }

    Color ExteriorTintAt(float hour, float daylight)
    {
        Color tint = Color.Lerp(nightExteriorTint, dayExteriorTint, daylight);
        float dawnPeak = (dawnStart + dayStart) * 0.5f;
        float duskPeak = (duskStart + nightStart) * 0.5f;
        float dawnWarmth = 1f - Mathf.Clamp01(Mathf.Abs(hour - dawnPeak) / Mathf.Max(0.1f, dayStart - dawnStart));
        float duskWarmth = 1f - Mathf.Clamp01(Mathf.Abs(hour - duskPeak) / Mathf.Max(0.1f, nightStart - duskStart));
        return Color.Lerp(tint, dawnDuskExteriorTint, Mathf.Max(dawnWarmth, duskWarmth));
    }

    float WeatherLightMultiplier(WeatherKind weather)
    {
        switch (weather)
        {
            case WeatherKind.Cloudy: return cloudyLight;
            case WeatherKind.Rain: return rainLight;
            case WeatherKind.Storm: return stormLight;
            case WeatherKind.Fog: return fogLight;
            case WeatherKind.Snow: return snowLight;
            default: return 1f;
        }
    }

    static WeatherKind ClassifyWeather(WorldWeatherState weather)
    {
        switch (weather.kind)
        {
            case WorldWeatherKind.Storm:
            case WorldWeatherKind.Hail: return WeatherKind.Storm;
            case WorldWeatherKind.Snow:
            case WorldWeatherKind.Sleet: return WeatherKind.Snow;
            case WorldWeatherKind.Drizzle:
            case WorldWeatherKind.Rain:
            case WorldWeatherKind.HeavyRain: return WeatherKind.Rain;
            case WorldWeatherKind.Fog: return WeatherKind.Fog;
            case WorldWeatherKind.PartlyCloudy:
            case WorldWeatherKind.Cloudy:
            case WorldWeatherKind.Wind: return WeatherKind.Cloudy;
            default: return WeatherKind.Clear;
        }
    }

    static WeatherMask MaskFor(WeatherKind weather)
    {
        switch (weather)
        {
            case WeatherKind.Cloudy: return WeatherMask.Cloudy;
            case WeatherKind.Rain: return WeatherMask.Rain;
            case WeatherKind.Storm: return WeatherMask.Storm;
            case WeatherKind.Fog: return WeatherMask.Fog;
            case WeatherKind.Snow: return WeatherMask.Snow;
            default: return WeatherMask.Clear;
        }
    }

    static float SampleAuthoritativeLightning(float referenceIntensity)
    {
        if (_sampleFrame == Time.frameCount) return _lightningSample;
        _sampleFrame = Time.frameCount;
        _lightningSample = 0f;

        CozyWeather cozy = CozyWeather.instance;
        Transform thunderRoot = cozy != null ? cozy.thunderFXParent : null;
        if (thunderRoot == null || !thunderRoot.gameObject.activeInHierarchy) return 0f;

        if (_thunderChildCount != thunderRoot.childCount)
        {
            _thunderChildCount = thunderRoot.childCount;
            _thunderLights = thunderRoot.GetComponentsInChildren<Light>(false);
        }

        float peak = 0f;
        for (int i = 0; i < _thunderLights.Length; i++)
        {
            Light light = _thunderLights[i];
            if (light != null && light.isActiveAndEnabled)
                peak = Mathf.Max(peak, light.intensity);
        }

        _lightningSample = Mathf.Clamp01(peak / Mathf.Max(0.01f, referenceIntensity));
        return _lightningSample;
    }
}
