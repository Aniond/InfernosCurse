using UnityEngine;

public enum WeatherSurfaceKind
{
    GrassField,
    GrassTerrain,
    River,
    Pond,
    Fountain,
    BattleWater
}

public enum WeatherSurfaceExposure
{
    Outdoor,
    Sheltered,
    Indoor
}

/// <summary>
/// Explicit registration for a grass or water surface that follows the
/// persistent Florence weather. It does not own weather state.
/// </summary>
[DisallowMultipleComponent]
public sealed class WeatherSurface : MonoBehaviour
{
    public WeatherSurfaceKind surfaceKind;
    public WeatherSurfaceExposure exposure = WeatherSurfaceExposure.Outdoor;
    public Renderer targetRenderer;
    public Terrain targetTerrain;
    public ParticleSystem rainEmitter;

    [Min(0f)] public float stormEmissionRate = 40f;
    [Range(0f, 2f)] public float weatherResponse = 1f;

    public bool IsGrass => surfaceKind == WeatherSurfaceKind.GrassField ||
                           surfaceKind == WeatherSurfaceKind.GrassTerrain;

    public bool IsWater => !IsGrass;

    public Bounds WorldBounds
    {
        get
        {
            if (targetRenderer != null) return targetRenderer.bounds;
            if (targetTerrain != null && targetTerrain.terrainData != null)
            {
                Vector3 size = targetTerrain.terrainData.size;
                return new Bounds(targetTerrain.transform.position + size * 0.5f, size);
            }
            return new Bounds(transform.position, Vector3.one);
        }
    }

    void Reset()
    {
        targetRenderer = GetComponent<Renderer>();
        targetTerrain = GetComponent<Terrain>();
        rainEmitter = GetComponentInChildren<ParticleSystem>(true);
        stormEmissionRate = DefaultStormRate(surfaceKind);
    }

    void OnEnable() => WeatherSurfaceController.Register(this);

    void OnDisable()
    {
        WeatherSurfaceController.Unregister(this);
        ApplyRainRate(0f);
    }

    public float DesiredRainRate(float rainIntensity)
    {
        if (rainEmitter == null || exposure == WeatherSurfaceExposure.Indoor) return 0f;

        float exposureMultiplier = exposure == WeatherSurfaceExposure.Sheltered ? 0.25f : 1f;
        return stormEmissionRate * weatherResponse * exposureMultiplier *
               Mathf.Clamp01(rainIntensity);
    }

    public void ApplyRainRate(float rate)
    {
        if (rainEmitter == null) return;

        var emission = rainEmitter.emission;
        emission.rateOverTime = Mathf.Max(0f, rate);

        if (rate > 0.01f)
        {
            if (!rainEmitter.isPlaying) rainEmitter.Play(true);
        }
        else if (rainEmitter.isPlaying)
        {
            rainEmitter.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    public static float DefaultStormRate(WeatherSurfaceKind kind)
    {
        switch (kind)
        {
            case WeatherSurfaceKind.River: return 160f;
            case WeatherSurfaceKind.Pond: return 55f;
            case WeatherSurfaceKind.Fountain: return 28f;
            case WeatherSurfaceKind.BattleWater: return 45f;
            case WeatherSurfaceKind.GrassTerrain: return 18f;
            default: return 12f;
        }
    }
}
