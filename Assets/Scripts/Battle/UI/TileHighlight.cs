using UnityEngine;

// Attached to move/attack/hover tile highlight prefabs.
// Handles fade-in on spawn and optional pulse animation.
[RequireComponent(typeof(SpriteRenderer))]
public class TileHighlight : MonoBehaviour
{
    [Header("Animation")]
    public bool  pulse       = false;
    public float pulseSpeed  = 2.5f;
    public float pulseMin    = 0.45f;
    public float pulseMax    = 0.75f;
    public float fadeInTime  = 0.12f;

    private SpriteRenderer _sr;
    private Color          _baseColor;
    private float          _fadeTimer;
    private bool           _fadingIn = true;

    void Awake()
    {
        _sr        = GetComponent<SpriteRenderer>();
        _baseColor = _sr.color;
        // Start transparent for fade-in
        _sr.color  = new Color(_baseColor.r, _baseColor.g, _baseColor.b, 0f);
    }

    void Update()
    {
        if (_fadingIn)
        {
            _fadeTimer += Time.deltaTime / fadeInTime;
            float a    = Mathf.Clamp01(_fadeTimer);
            _sr.color  = new Color(_baseColor.r, _baseColor.g, _baseColor.b, _baseColor.a * a);
            if (a >= 1f) _fadingIn = false;
            return;
        }

        if (pulse)
        {
            float a   = Mathf.Lerp(pulseMin, pulseMax, (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f);
            _sr.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b, a);
        }
    }

    public void SetColor(Color c)
    {
        _baseColor = c;
        _fadingIn  = true;
        _fadeTimer = 0f;
    }
}
