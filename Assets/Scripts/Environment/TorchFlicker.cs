using UnityEngine;

public class TorchFlicker : MonoBehaviour
{
    [Header("Flicker")]
    public float baseIntensity = 1.8f;
    public float flickerSpeed = 8f;
    public float flickerAmount = 0.4f;

    [Header("Night Fade")]
    public DayNightCycle dayNight;
    // Torches fully on from dusk (hour 17) through dawn (hour 7)
    public float duskHour = 17f;
    public float dawnHour = 7f;

    private Light _light;
    private Renderer _flameRenderer;
    private Material _flameMat;
    private float _noise;
    private static readonly Color _emissionOn  = new Color(2.5f, 0.8f, 0.05f);
    private static readonly Color _emissionOff = Color.black;

    void Awake()
    {
        _light = GetComponent<Light>();
        _noise = Random.Range(0f, 100f);
        if (dayNight == null)
            dayNight = FindAnyObjectByType<DayNightCycle>();

        var flameGO = transform.Find("Flame");
        if (flameGO != null)
        {
            _flameRenderer = flameGO.GetComponent<Renderer>();
            if (_flameRenderer != null)
                _flameMat = _flameRenderer.material;
        }
    }

    void Update()
    {
        if (_light == null) return;

        _noise += Time.deltaTime * flickerSpeed;
        float flicker = Mathf.PerlinNoise(_noise, 0f) * flickerAmount;
        float targetIntensity = (baseIntensity - flickerAmount * 0.5f) + flicker;

        float nightBlend = NightBlend();
        _light.intensity = targetIntensity * nightBlend;

        // Fade flame emission with night blend
        if (_flameMat != null)
            _flameMat.SetColor("_EmissionColor", Color.Lerp(_emissionOff, _emissionOn, nightBlend));
    }

    float NightBlend()
    {
        if (dayNight == null) return 1f;
        float h = dayNight.timeOfDay;
        // Night = hours outside [dawnHour, duskHour]
        if (h >= dawnHour && h <= duskHour)
        {
            // Daytime — fade out near dawn and dusk transitions
            float fadeWidth = 1.5f;
            float toDawn = h - dawnHour;
            float toDusk = duskHour - h;
            float fade = Mathf.Min(toDawn, toDusk);
            return 1f - Mathf.Clamp01(fade / fadeWidth);
        }
        return 1f; // fully on at night
    }
}
