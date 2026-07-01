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

    [Header("Ground Cookie")]
    [Tooltip("Bake and animate a soft noise cookie so the light pool shimmers on " +
             "the ground like real flame. Requires a Spot light (URP point lights " +
             "can't take a 2D cookie).")]
    public bool animatedCookie = true;
    [Tooltip("How fast the cast pattern drifts/swirls.")]
    public float cookieDriftSpeed = 0.4f;
    [Tooltip("Cookie resolution to bake. 64 is plenty for a soft floor pool.")]
    public int cookieResolution = 64;
    [Tooltip("Seconds between cookie re-bakes. The pattern only needs to shift a few " +
             "times a second to read as shimmer — rebaking every frame is wasteful.")]
    public float cookieRebakeInterval = 0.1f;

    private Light _light;
    private Renderer _flameRenderer;
    private Material _flameMat;
    private float _noise;
    private Texture2D _cookieTex;
    private Color32[] _cookiePixels;
    private bool _cookieActive;
    private float _cookieDrift;
    private float _rebakeTimer;
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

        SetupCookie();
    }

    void SetupCookie()
    {
        if (!animatedCookie || _light == null) return;

        if (_light.type != LightType.Spot)
        {
            Debug.Log($"[TorchFlicker] '{name}' is a {_light.type} light — the animated " +
                      "ground cookie needs a Spot light. Flicker still works; switch to " +
                      "Spot (aimed down) to get the shimmering floor pattern.", this);
            return;
        }

        int size = Mathf.Max(16, cookieResolution);
        _cookieTex = new Texture2D(size, size, TextureFormat.R8, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name = "TorchCookie"
        };
        _cookiePixels = new Color32[size * size];
        BakeFlameCookie(0f);          // initial frame
        _light.cookie = _cookieTex;
        _cookieActive = true;
    }

    void OnDestroy()
    {
        if (_cookieTex != null)
        {
            if (Application.isPlaying) Destroy(_cookieTex);
            else DestroyImmediate(_cookieTex);
        }
    }

    // A soft radial falloff broken up with perlin noise so the projected pool has
    // organic bright/dark blotches instead of a clean circle. The noise is sampled
    // at a drifting offset each re-bake, so the blotches crawl and the cast pattern
    // shimmers like firelight. R8 + throttled re-bake keeps it cheap.
    void BakeFlameCookie(float drift)
    {
        int size = _cookieTex.width;
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - half) / half;
                float dy = (y - half) / half;
                float r = Mathf.Sqrt(dx * dx + dy * dy);

                // Soft-edged disc: 1 at center, 0 past the rim.
                float disc = Mathf.Clamp01(1f - Mathf.SmoothStep(0.55f, 1f, r));

                // Blotchy noise, drifting so the pattern crawls over time.
                float n = Mathf.PerlinNoise(x * 0.06f + drift, y * 0.06f + drift * 0.7f);
                float val = disc * Mathf.Lerp(0.65f, 1f, n);

                byte b = (byte)(Mathf.Clamp01(val) * 255f);
                _cookiePixels[y * size + x] = new Color32(b, b, b, b);
            }
        }
        _cookieTex.SetPixels32(_cookiePixels);
        _cookieTex.Apply(false);
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

        AnimateCookie();
    }

    // Drift the cookie's noise so the projected ground pool shimmers. Throttled to
    // cookieRebakeInterval — the pattern only needs to shift a few times a second.
    void AnimateCookie()
    {
        if (!_cookieActive || _light == null) return;

        _cookieDrift += Time.deltaTime * cookieDriftSpeed;
        _rebakeTimer += Time.deltaTime;
        if (_rebakeTimer >= cookieRebakeInterval)
        {
            _rebakeTimer = 0f;
            BakeFlameCookie(_cookieDrift);
        }
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
