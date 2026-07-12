using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns and drives volumetric "god ray" light shafts — the signature HD-2D
/// look (Octopath). Drop this on an empty in the scene, assign anchor transforms
/// (one per church window / archway opening), and it instantiates a soft
/// translucent cone at each. Shaft brightness follows <see cref="GameClock"/>:
/// strongest at low-angle dawn/dusk, gone at midday and night. A gentle shimmer
/// keeps them feeling alive with drifting dust.
///
/// No texture/prefab dependency — the cone mesh and a soft additive material are
/// built procedurally at runtime.
/// </summary>
[ExecuteAlways]
public class LightShaft : MonoBehaviour
{
    [Header("Anchors")]
    [Tooltip("One transform per shaft — position at the opening (window/arch), " +
             "with its local -Y (down) pointing the way light should fall. " +
             "The shaft's length/width come from the anchor's local scale (Y = length, X/Z = mouth width).")]
    [SerializeField] private List<Transform> anchors = new();

    [Header("Appearance")]
    [Tooltip("Warm shaft tint. Alpha is the peak opacity at full strength.")]
    [SerializeField] private Color color = new Color(1f, 0.92f, 0.68f, 0.28f);
    [Tooltip("World length of each shaft (scaled further by the anchor's local Y).")]
    [SerializeField] private float length = 6f;
    [Tooltip("Radius of the shaft mouth at the opening.")]
    [SerializeField] private float mouthRadius = 0.6f;
    [Tooltip("Radius where the shaft lands — usually a bit wider than the mouth.")]
    [SerializeField] private float floorRadius = 1.4f;

    [Header("Time-of-day drive")]
    [Tooltip("Hours (0-24) at which shafts peak. Two entries = dawn & dusk rakes.")]
    [SerializeField] private float dawnPeakHour = 7.5f;
    [SerializeField] private float duskPeakHour = 17.5f;
    [Tooltip("Hours on either side of a peak over which the shaft fades to nothing.")]
    [SerializeField] private float peakWidth = 2.5f;

    [Header("Shimmer")]
    [Tooltip("How fast the dust shimmer breathes.")]
    [SerializeField] private float shimmerSpeed = 0.6f;
    [Tooltip("Fraction of opacity the shimmer swings (0 = steady).")]
    [SerializeField, Range(0f, 1f)] private float shimmerAmount = 0.25f;

    private readonly List<Renderer> _shafts = new();
    private readonly List<float> _shimmerSeed = new();
    private Material _sharedMat;
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int Color1 = Shader.PropertyToID("_Color"); // fallback

    void OnEnable()
    {
        Build();
    }

    void OnDisable() => Clear();

    void Build()
    {
        Clear();
        if (anchors == null) return;

        Mesh cone = BuildConeMesh();
        _sharedMat = BuildMaterial();

        for (int i = 0; i < anchors.Count; i++)
        {
            var a = anchors[i];
            if (a == null) continue;

            var go = new GameObject($"Shaft_{i}");
            go.transform.SetParent(a, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.hideFlags = HideFlags.DontSave; // don't serialize runtime-built children

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = cone;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _sharedMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            _shafts.Add(mr);
            // Deterministic per-index seed (no Random — keeps editor preview stable).
            _shimmerSeed.Add(i * 1.37f);
        }
    }

    void Clear()
    {
        foreach (var r in _shafts)
            if (r != null)
            {
                if (Application.isPlaying) Destroy(r.gameObject);
                else DestroyImmediate(r.gameObject);
            }
        _shafts.Clear();
        _shimmerSeed.Clear();
    }

    void LateUpdate()
    {
        float hour = GameClock.Hour;
        float strength = TimeStrength(hour) * WeatherStrength();

        for (int i = 0; i < _shafts.Count; i++)
        {
            var r = _shafts[i];
            if (r == null) continue;

            // Shimmer breathes the opacity a little (perlin so it's smooth).
            float t = Application.isPlaying ? Time.time : 0f;
            float shimmer = 1f - shimmerAmount * 0.5f
                          + Mathf.PerlinNoise(t * shimmerSpeed + _shimmerSeed[i], 0f) * shimmerAmount;

            float a = color.a * strength * shimmer;
            Color c = new Color(color.r, color.g, color.b, a);

            var mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            mpb.SetColor(BaseColor, c);
            mpb.SetColor(Color1, c);
            r.SetPropertyBlock(mpb);

            r.enabled = a > 0.002f; // skip drawing fully-faded shafts
        }
    }

    /// <summary>0 at midday/night, 1 at a dawn or dusk peak.</summary>
    float TimeStrength(float hour)
    {
        float dawn = 1f - Mathf.Clamp01(Mathf.Abs(hour - dawnPeakHour) / peakWidth);
        float dusk = 1f - Mathf.Clamp01(Mathf.Abs(hour - duskPeakHour) / peakWidth);
        // Smooth the linear tent so the fade eases in/out.
        return Mathf.SmoothStep(0f, 1f, Mathf.Max(dawn, dusk));
    }

    // Uses the same persistent profile FlorenceWeather applies to COZY. Indoor
    // shafts therefore dim with cloud, rain, storm, and fog without owning any
    // weather state or running a separate transition.
    static float WeatherStrength()
    {
        WorldWeatherState weather = WorldEnvironmentState.CurrentWeather;
        switch (weather.kind)
        {
            case WorldWeatherKind.Storm:
            case WorldWeatherKind.Hail: return 0.24f;
            case WorldWeatherKind.Drizzle:
            case WorldWeatherKind.Rain:
            case WorldWeatherKind.HeavyRain:
            case WorldWeatherKind.Sleet: return 0.46f;
            case WorldWeatherKind.Fog: return 0.34f;
            case WorldWeatherKind.PartlyCloudy:
            case WorldWeatherKind.Cloudy:
            case WorldWeatherKind.Wind: return 0.68f;
            case WorldWeatherKind.Snow: return 0.74f;
            default: return 1f;
        }
    }

    /// <summary>
    /// A truncated cone (mouth radius at the opening, floor radius at the far end),
    /// double-sided, with vertex alpha fading along its length so the shaft
    /// dissolves toward the floor. Origin at the mouth, extends down local -Y.
    /// </summary>
    Mesh BuildConeMesh()
    {
        const int seg = 12;
        var verts = new List<Vector3>();
        var cols = new List<Color>();
        var tris = new List<int>();

        for (int s = 0; s <= seg; s++)
        {
            float ang = (s / (float)seg) * Mathf.PI * 2f;
            float cx = Mathf.Cos(ang), cz = Mathf.Sin(ang);

            // Mouth ring (full alpha) at y=0
            verts.Add(new Vector3(cx * mouthRadius, 0f, cz * mouthRadius));
            cols.Add(new Color(1, 1, 1, 1f));
            // Floor ring (zero alpha) at y=-length
            verts.Add(new Vector3(cx * floorRadius, -length, cz * floorRadius));
            cols.Add(new Color(1, 1, 1, 0f));
        }

        for (int s = 0; s < seg; s++)
        {
            int m0 = s * 2, f0 = s * 2 + 1, m1 = (s + 1) * 2, f1 = (s + 1) * 2 + 1;
            // Front faces
            tris.Add(m0); tris.Add(f0); tris.Add(m1);
            tris.Add(m1); tris.Add(f0); tris.Add(f1);
            // Back faces (double-sided so it reads from any camera angle)
            tris.Add(m1); tris.Add(f0); tris.Add(m0);
            tris.Add(f1); tris.Add(f0); tris.Add(m1);
        }

        var mesh = new Mesh { name = "LightShaftCone" };
        mesh.SetVertices(verts);
        mesh.SetColors(cols);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// Soft additive, no depth-write, no cull — a URP Unlit particle-style material.
    /// Uses vertex color (for the length fade) times _BaseColor (for the tint/strength).
    /// </summary>
    Material BuildMaterial()
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
        var mat = new Material(sh) { name = "LightShaftMat" };

        // Additive-ish soft blend
        mat.SetFloat("_Surface", 1f);   // Transparent
        mat.SetFloat("_Blend", 1f);     // Additive if supported
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        mat.SetInt("_ZWrite", 0);
        mat.SetFloat("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3100; // after opaque + most transparents
        mat.SetColor(BaseColor, color);
        return mat;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        // Rebuild in-editor so tweaks preview live, but not during play (would thrash).
        if (!Application.isPlaying && isActiveAndEnabled)
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null && isActiveAndEnabled) Build();
            };
        }
    }
#endif
}
