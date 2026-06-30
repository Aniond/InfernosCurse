using UnityEngine;
using TMPro;
using System.Collections;

// Spawned by DamageNumberPool. Floats up, fades out, then returns to pool.
[RequireComponent(typeof(TMP_Text))]
public class DamageNumber : MonoBehaviour
{
    [Header("Motion")]
    public float riseSpeed    = 1.8f;
    public float lifetime     = 0.9f;
    public float spreadX      = 0.35f;  // random horizontal drift
    public float popScale     = 1.4f;   // initial punch scale
    public float normalScale  = 1.0f;

    [Header("Colors by type")]
    public Color colorPhysical  = new Color(1.00f, 0.92f, 0.70f);
    public Color colorFire      = new Color(1.00f, 0.45f, 0.10f);
    public Color colorIce       = new Color(0.60f, 0.85f, 1.00f);
    public Color colorLightning = new Color(0.95f, 0.95f, 0.20f);
    public Color colorHoly      = new Color(1.00f, 1.00f, 0.70f);
    public Color colorDark      = new Color(0.65f, 0.20f, 0.80f);
    public Color colorPoison    = new Color(0.40f, 0.85f, 0.20f);
    public Color colorHeal      = new Color(0.30f, 0.95f, 0.45f);
    public Color colorMiss      = new Color(0.70f, 0.70f, 0.70f);
    public Color colorCrit      = new Color(1.00f, 0.30f, 0.20f);

    private TMP_Text  _text;
    private Coroutine _anim;

    void Awake() => _text = GetComponent<TMP_Text>();

    // ── Public spawn API ──────────────────────────────────────────────────────

    public void ShowDamage(int amount, DamageType type, bool isCrit, Vector3 worldPos)
    {
        string label = isCrit ? $"<b>{amount}!</b>" : amount.ToString();
        Color  color = isCrit ? colorCrit : ColorForType(type);
        Play(label, color, worldPos, isCrit ? popScale * 1.2f : popScale);
    }

    public void ShowHeal(int amount, Vector3 worldPos)
    {
        Play($"+{amount}", colorHeal, worldPos, popScale);
    }

    public void ShowMiss(Vector3 worldPos)
    {
        Play("Miss", colorMiss, worldPos, normalScale);
    }

    public void ShowAbsorb(string skillName, Vector3 worldPos)
    {
        Play($"Absorbed!\n{skillName}", colorHoly, worldPos, popScale);
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    void Play(string label, Color color, Vector3 worldPos, float startScale)
    {
        transform.position = worldPos + Vector3.up * 0.3f;
        _text.text         = label;
        _text.color        = color;
        gameObject.SetActive(true);

        if (_anim != null) StopCoroutine(_anim);
        _anim = StartCoroutine(Animate(startScale));
    }

    IEnumerator Animate(float startScale)
    {
        float  t      = 0f;
        float  driftX = Random.Range(-spreadX, spreadX);
        Color  col    = _text.color;
        Vector3 start = transform.position;

        // Pop in
        transform.localScale = Vector3.one * startScale;

        while (t < lifetime)
        {
            t += Time.deltaTime;
            float progress = t / lifetime;

            // Rise with slight curve
            transform.position = start + new Vector3(driftX * progress, riseSpeed * progress, 0f);

            // Scale back to normal then shrink at end
            float scaleT = Mathf.Clamp01(progress * 4f);
            float scale  = Mathf.Lerp(startScale, normalScale, scaleT);
            if (progress > 0.6f) scale = Mathf.Lerp(normalScale, 0f, (progress - 0.6f) / 0.4f);
            transform.localScale = Vector3.one * scale;

            // Fade out in final third
            float alpha = progress > 0.65f ? Mathf.Lerp(1f, 0f, (progress - 0.65f) / 0.35f) : 1f;
            _text.color = new Color(col.r, col.g, col.b, alpha);

            yield return null;
        }

        gameObject.SetActive(false);
        DamageNumberPool.Instance?.Return(this);
        _anim = null;
    }

    Color ColorForType(DamageType type) => type switch
    {
        DamageType.Physical  => colorPhysical,
        DamageType.Fire      => colorFire,
        DamageType.Ice       => colorIce,
        DamageType.Lightning => colorLightning,
        DamageType.Holy      => colorHoly,
        DamageType.Dark      => colorDark,
        DamageType.Poison    => colorPoison,
        _                    => colorPhysical,
    };
}
