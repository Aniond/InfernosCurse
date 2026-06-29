using UnityEngine;

[ExecuteAlways]
public class DayNightCycle : MonoBehaviour
{
    [Header("Time")]
    [Range(0f, 24f)] public float timeOfDay = 10f;
    public float dayDurationSeconds = 120f;
    public bool running = true;

    [Header("Sun")]
    public Light sun;

    [Header("Color Keyframes (by hour)")]
    public Gradient sunColor;
    public AnimationCurve sunIntensity;
    public Gradient ambientSkyColor;
    public Gradient ambientEquatorColor;
    public Gradient ambientGroundColor;

    [Header("Camera Background")]
    public Camera mainCamera;
    public Gradient cameraBackgroundColor;

    [Header("Fog")]
    public bool useFog = true;
    public Gradient fogColor;
    public AnimationCurve fogDensity;

    [Header("Backdrop")]
    public Renderer backdropRenderer;
    // Backdrop fully visible during [backdropDawnHour, backdropDuskHour], fades out at night
    public float backdropDawnHour = 6.5f;
    public float backdropDuskHour = 18.5f;
    public float backdropFadeWidth = 1.0f;
    private Material _backdropMat;

    void OnEnable()
    {
        if (sun == null)
            sun = FindFirstObjectByType<Light>();
        if (mainCamera == null)
            mainCamera = Camera.main;
        CacheBackdropMat();
    }

    void Update()
    {
        if (running && Application.isPlaying)
            timeOfDay = (timeOfDay + Time.deltaTime * (24f / dayDurationSeconds)) % 24f;

        ApplyLighting(timeOfDay / 24f);
    }

    void ApplyLighting(float t)
    {
        // Rotate sun: rises at east (−90°) sets at west (270°), midnight = underneath
        float sunAngle = (t * 360f) - 90f;
        if (sun != null)
        {
            sun.transform.rotation = Quaternion.Euler(sunAngle, 45f, 0f);
            sun.color = sunColor.Evaluate(t);
            sun.intensity = sunIntensity.Evaluate(t);
        }

        // Ambient trilight
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = ambientSkyColor.Evaluate(t);
        RenderSettings.ambientEquatorColor = ambientEquatorColor.Evaluate(t);
        RenderSettings.ambientGroundColor = ambientGroundColor.Evaluate(t);

        // Camera background
        if (mainCamera != null)
            mainCamera.backgroundColor = cameraBackgroundColor.Evaluate(t);

        // Fog
        RenderSettings.fog = useFog;
        if (useFog)
        {
            RenderSettings.fogColor = fogColor.Evaluate(t);
            RenderSettings.fogDensity = fogDensity.Evaluate(t);
        }

        // Backdrop fade — visible during day, invisible at night
        ApplyBackdropAlpha(timeOfDay);
    }

    void CacheBackdropMat()
    {
        if (backdropRenderer != null && _backdropMat == null)
            _backdropMat = backdropRenderer.material;
    }

    void ApplyBackdropAlpha(float hour)
    {
        CacheBackdropMat();
        if (_backdropMat == null) return;

        float alpha;
        if (hour >= backdropDawnHour && hour <= backdropDuskHour)
        {
            float toDawn = hour - backdropDawnHour;
            float toDusk = backdropDuskHour - hour;
            alpha = Mathf.Clamp01(Mathf.Min(toDawn, toDusk) / backdropFadeWidth);
        }
        else
        {
            alpha = 0f;
        }

        // URP Unlit uses _BaseColor for tint+alpha
        Color c = _backdropMat.GetColor("_BaseColor");
        c.a = alpha;
        _backdropMat.SetColor("_BaseColor", c);

        // Enable alpha blending on the material
        _backdropMat.SetFloat("_Surface", 1f);        // 0=Opaque, 1=Transparent
        _backdropMat.SetFloat("_Blend", 0f);          // Alpha blend
        _backdropMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _backdropMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _backdropMat.SetInt("_ZWrite", alpha < 0.99f ? 0 : 1);
        _backdropMat.renderQueue = alpha < 0.99f ? 3000 : 1500;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        ApplyLighting(timeOfDay / 24f);
    }
#endif
}
