using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections.Generic;

// Where Benidito's insanity comes from (equipped unrefined orbs) — one place
// both the storytelling layer and the encounter hooks read.
public static class InsanityState
{
    // Set >= 0 to preview manifestation tiers without farming orbs
    // (InsanityPresenter exposes it in the inspector). -1 = live value.
    public static int DebugOverride = -1;

    public static int Current()
    {
        if (DebugOverride >= 0) return DebugOverride;
        foreach (var m in RestSystem.PartyMembers)
            if (m != null && m.role == CombatantRole.Benidito)
                return m.CurrentInsanity();
        return 0;
    }
}

// The Call-of-Cthulhu layer (David 7/08): insanity is a STORYTELLING system,
// never a number on screen. Carrying unrefined monster orbs corrupts Ben's
// senses by degree — at low levels nothing is noticeable; as the price grows
// the screen darkens, water runs red, and (once an audio clip is wired) he
// starts hearing whispers. Church refinement burns the corruption off an orb
// and the world quietly comes back.
//
// Lives on GameSystems (persists across zones). Uses a screen-space overlay
// vignette (no post-processing, no fight with COZY) and tints scene water via
// MaterialPropertyBlock (never touches shared material assets).
public class InsanityPresenter : MonoBehaviour
{
    [Header("Tier thresholds (balance knobs)")]
    [Tooltip("Insanity at which the vignette begins to creep in.")]
    public int darkenAt = 3;
    [Tooltip("Insanity at which water begins turning red.")]
    public int redWaterAt = 6;
    [Tooltip("Insanity at which the whispers start (needs whisperLoop assigned).")]
    public int whispersAt = 9;
    [Tooltip("Insanity at which each effect reaches full strength.")]
    public int fullEffectAt = 15;

    [Header("Feel")]
    [Range(0f, 1f)] public float maxVignette = 0.55f;
    public Color bloodShallow = new Color(0.45f, 0.02f, 0.02f);
    public Color bloodDeep    = new Color(0.25f, 0.0f, 0.0f);
    [Tooltip("Whisper loop clip — none exists yet; assign when David sources one.")]
    public AudioClip whisperLoop;
    [Range(0f, 1f)] public float maxWhisperVolume = 0.35f;

    [Header("Debug")]
    [Tooltip("-1 = live insanity from equipped orbs. Set 0-15 to preview tiers.")]
    public int debugInsanityOverride = -1;

    Image _vignette;
    AudioSource _whispers;
    // Runtime material INSTANCES (mr.material) — Stylized Water 3 ignores
    // MaterialPropertyBlock overrides (own CBUFFER), verified 7/08. Instances
    // are runtime-only copies; shared assets are never dirtied.
    readonly List<Material> _water = new();
    readonly List<Color[]> _waterOriginals = new();   // one per WaterProps entry
    // Intersection foam + surface foam carry most of the READ on small basins
    // (the fountain) — tinting only the body colors leaves the water looking
    // white (verified 7/08).
    static readonly string[] WaterProps =
        { "_BaseColor", "_ShallowColor", "_HorizonColor", "_IntersectionColor", "_FoamColor" };
    float _pulse;

    void Start()
    {
        BuildVignette();
        _whispers = gameObject.AddComponent<AudioSource>();
        _whispers.loop = true;
        _whispers.playOnAwake = false;
        _whispers.spatialBlend = 0f;   // inside his head
        SceneManager.sceneLoaded += OnSceneLoaded;
        ScanWater();
    }

    void OnDestroy() => SceneManager.sceneLoaded -= OnSceneLoaded;
    void OnSceneLoaded(Scene s, LoadSceneMode m) => ScanWater();

    void Update()
    {
        InsanityState.DebugOverride = debugInsanityOverride;
        _pulse += Time.deltaTime;
        if (_pulse < 0.5f) return;   // storytelling cadence, not a combat system
        _pulse = 0f;

        int ins = InsanityState.Current();

        // 1. the light thins
        float dark = Ramp(ins, darkenAt, fullEffectAt) * maxVignette;
        if (_vignette != null)
            _vignette.color = new Color(0f, 0f, 0f, dark);

        // 2. the water remembers blood
        float red = Ramp(ins, redWaterAt, fullEffectAt);
        ApplyWaterTint(red);

        // 3. the whispers (hook — silent until a clip is assigned)
        float wh = Ramp(ins, whispersAt, fullEffectAt);
        if (_whispers != null && whisperLoop != null)
        {
            _whispers.clip = _whispers.clip != null ? _whispers.clip : whisperLoop;
            _whispers.volume = wh * maxWhisperVolume;
            if (wh > 0f && !_whispers.isPlaying) _whispers.Play();
            else if (wh <= 0f && _whispers.isPlaying) _whispers.Stop();
        }
    }

    // 0 below start, 1 at full — the slow slide, not a switch.
    static float Ramp(int value, int start, int full) =>
        Mathf.Clamp01((value - start) / Mathf.Max(1f, full - start));

    void ApplyWaterTint(float red)
    {
        // per-prop blood targets: body goes deep, foam goes bright — the foam
        // ring is what the eye actually catches on a fountain
        var targets = new Color[]
        {
            bloodDeep,                              // _BaseColor
            bloodShallow,                           // _ShallowColor
            bloodShallow,                           // _HorizonColor
            new Color(0.75f, 0.10f, 0.10f),         // _IntersectionColor
            new Color(0.85f, 0.25f, 0.22f),         // _FoamColor
        };
        for (int i = 0; i < _water.Count; i++)
        {
            var mat = _water[i];
            if (mat == null) continue;
            var orig = _waterOriginals[i];
            for (int p = 0; p < WaterProps.Length; p++)
                if (mat.HasProperty(WaterProps[p]))
                    mat.SetColor(WaterProps[p], Color.Lerp(orig[p], targets[p], red));
        }
    }

    void ScanWater()
    {
        _water.Clear();
        _waterOriginals.Clear();
        foreach (var mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            var shared = mr.sharedMaterial;
            if (shared == null || shared.shader == null) continue;
            if (!shared.shader.name.ToLowerInvariant().Contains("water")) continue;
            var inst = mr.material;   // runtime instance — safe to recolor
            var originals = new Color[WaterProps.Length];
            for (int p = 0; p < WaterProps.Length; p++)
                originals[p] = inst.HasProperty(WaterProps[p]) ? inst.GetColor(WaterProps[p]) : Color.white;
            _water.Add(inst);
            _waterOriginals.Add(originals);
        }
    }

    // Radial vignette on a persistent overlay canvas — cheap, renderer-agnostic,
    // and COZY never knows it's there.
    void BuildVignette()
    {
        var go = new GameObject("InsanityVignette",
            typeof(Canvas), typeof(CanvasScaler), typeof(Image));
        go.transform.SetParent(transform, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 400;   // above world, below battle menus (500+)
        _vignette = go.GetComponent<Image>();
        _vignette.raycastTarget = false;
        _vignette.sprite = BuildRadialSprite();
        _vignette.color = new Color(0f, 0f, 0f, 0f);
        var rt = _vignette.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static Sprite BuildRadialSprite()
    {
        const int N = 128;
        var tex = new Texture2D(N, N, TextureFormat.Alpha8, false);
        var px = new Color32[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = (x + 0.5f) / N - 0.5f, dy = (y + 0.5f) / N - 0.5f;
                float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;          // 0 center, 1 corner-ish
                float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((r - 0.45f) / 0.75f));
                px[x + y * N] = new Color32(0, 0, 0, (byte)(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply();
        tex.wrapMode = TextureWrapMode.Clamp;
        return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f));
    }
}
