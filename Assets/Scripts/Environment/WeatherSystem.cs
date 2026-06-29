using UnityEngine;

public enum WeatherType { None, Rain, Snow, Sleet, Hail, Wind }

public class WeatherSystem : MonoBehaviour
{
    [Header("References")]
    public ParticleSystem rainSystem;
    public ParticleSystem snowSystem;
    public ParticleSystem sleetSystem;
    public ParticleSystem hailSystem;
    public ParticleSystem windSystem;   // dust/leaf debris driven by wind
    public ParticleSystem windGusts;    // secondary horizontal streaks

    [Header("State")]
    public WeatherType current = WeatherType.None;
    public float transitionSpeed = 2f;  // seconds to cross-fade

    [Header("Preview Cycle")]
    public bool previewCycle = false;   // enable in Inspector to auto-cycle all weather
    public float previewDuration = 10f; // seconds per weather type
    private float _previewTimer;
    private int _previewIndex;
    private static readonly WeatherType[] _cycleOrder =
        { WeatherType.Rain, WeatherType.Snow, WeatherType.Sleet, WeatherType.Hail, WeatherType.Wind, WeatherType.None };

    [Header("Audio (optional)")]
    public AudioSource weatherAudio;
    public AudioClip rainClip;
    public AudioClip windClip;
    public AudioClip hailClip;

    [Header("Fog override")]
    public bool overrideFog = true;
    // Each weather type adds extra fog density on top of the day/night base
    public float rainFogBoost   = 0.008f;
    public float snowFogBoost   = 0.015f;
    public float sleetFogBoost  = 0.010f;
    public float hailFogBoost   = 0.004f;
    public float windFogBoost   = 0.003f;

    private WeatherType _previous = WeatherType.None;
    private float _blend = 1f;  // 0→1 transition progress
    private DayNightCycle _dnc;

    void Awake()
    {
        _dnc = FindFirstObjectByType<DayNightCycle>();
        SetWeatherImmediate(current);
    }

    void Update()
    {
        if (_blend < 1f)
        {
            _blend = Mathf.MoveTowards(_blend, 1f, Time.deltaTime / transitionSpeed);
            CrossFadeEmission(_blend);
        }

        if (previewCycle && Application.isPlaying)
        {
            _previewTimer -= Time.deltaTime;
            if (_previewTimer <= 0f)
            {
                _previewIndex = (_previewIndex + 1) % _cycleOrder.Length;
                SetWeather(_cycleOrder[_previewIndex]);
                _previewTimer = previewDuration;
            }
        }
    }

    public void SetWeather(WeatherType type)
    {
        if (type == current) return;
        _previous = current;
        current = type;
        _blend = 0f;

        // Start the new system immediately (emission rate will ramp up via blend)
        ActivateSystem(type, true);
        UpdateFog();
        UpdateAudio();
    }

    public void SetWeatherImmediate(WeatherType type)
    {
        current = type;
        _previous = WeatherType.None;
        _blend = 1f;
        foreach (WeatherType w in System.Enum.GetValues(typeof(WeatherType)))
            ActivateSystem(w, w == type);
        UpdateFog();
        UpdateAudio();
    }

    void ActivateSystem(WeatherType type, bool on)
    {
        var ps = GetSystem(type);
        if (ps == null) return;
        if (on) { if (!ps.isPlaying) ps.Play(); }
        else    { ps.Stop(true, ParticleSystemStopBehavior.StopEmitting); }

        // Wind gusts follow wind weather
        if (windGusts != null)
        {
            if (type == WeatherType.Wind && on) { if (!windGusts.isPlaying) windGusts.Play(); }
            else if (type == WeatherType.Wind && !on) windGusts.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    void CrossFadeEmission(float t)
    {
        // Ramp down previous, ramp up current
        SetEmissionMultiplier(GetSystem(_previous), 1f - t);
        SetEmissionMultiplier(GetSystem(current),   t);
        if (windGusts != null && current == WeatherType.Wind)
            SetEmissionMultiplier(windGusts, t);
        if (windGusts != null && _previous == WeatherType.Wind)
            SetEmissionMultiplier(windGusts, 1f - t);
    }

    void SetEmissionMultiplier(ParticleSystem ps, float mult)
    {
        if (ps == null) return;
        var em = ps.emission;
        em.rateOverTimeMultiplier = mult;
    }

    ParticleSystem GetSystem(WeatherType type)
    {
        return type switch
        {
            WeatherType.Rain  => rainSystem,
            WeatherType.Snow  => snowSystem,
            WeatherType.Sleet => sleetSystem,
            WeatherType.Hail  => hailSystem,
            WeatherType.Wind  => windSystem,
            _ => null
        };
    }

    void UpdateFog()
    {
        if (!overrideFog) return;
        float boost = current switch
        {
            WeatherType.Rain  => rainFogBoost,
            WeatherType.Snow  => snowFogBoost,
            WeatherType.Sleet => sleetFogBoost,
            WeatherType.Hail  => hailFogBoost,
            WeatherType.Wind  => windFogBoost,
            _ => 0f
        };
        RenderSettings.fog = current != WeatherType.None || (_dnc != null && _dnc.useFog);
        if (boost > 0f)
            RenderSettings.fogDensity = (_dnc != null ? _dnc.fogDensity.Evaluate(_dnc.timeOfDay / 24f) : 0.005f) + boost;
    }

    void UpdateAudio()
    {
        if (weatherAudio == null) return;
        AudioClip clip = current switch
        {
            WeatherType.Rain  => rainClip,
            WeatherType.Sleet => rainClip,
            WeatherType.Wind  => windClip,
            WeatherType.Hail  => hailClip,
            _ => null
        };
        if (clip != null)
        {
            weatherAudio.clip = clip;
            weatherAudio.loop = true;
            weatherAudio.Play();
        }
        else
        {
            weatherAudio.Stop();
        }
    }
}
