using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

// One clickable pin on the Gugol Mappe overlay. Built entirely in code by
// GugolMapUI (no prefab). Ported from MapNodeView (which still serves the
// legacy WorldMap scene): pointer handlers and the subtle curse tint are
// carried over verbatim; adds locked/teaser state, hover scale, a pulsing
// "you are here" dot, a per-district weather glyph, and search dimming.
[RequireComponent(typeof(RectTransform))]
public class GugolMapPin : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Wired by GugolMapUI at build time")]
    public Image      pinIcon;
    public TMP_Text   label;
    public Image      selectedOutline;
    public Image      weatherIcon;
    public Image      floodBadge;
    public Image      youAreHereDot;
    public TMP_Text   lockedTooltip;

    [Header("Curse Tint (subtle — curse is a hidden value)")]
    public Color cursedColor    = new Color(0.55f, 0.10f, 0.65f);
    public Color sanctuaryColor = new Color(0.95f, 0.90f, 0.55f);

    public Color lockedTint = new Color(0.62f, 0.60f, 0.58f, 0.85f);

    public HubNode Node     { get; private set; }
    public bool    Unlocked { get; private set; }

    private GugolMapUI _map;
    private bool  _selected;
    private bool  _dimmed;
    private float _hoverScale = 1f;
    private Coroutine _pulse;

    public void Bind(HubNode node, GugolMapUI map, bool unlocked)
    {
        Node     = node;
        _map     = map;
        Unlocked = unlocked;

        if (label) label.text = node.displayName;
        if (selectedOutline) selectedOutline.enabled = false;
        if (lockedTooltip)   lockedTooltip.gameObject.SetActive(false);
        if (floodBadge)      floodBadge.enabled = false;
        SetYouAreHere(false);
        Refresh();
    }

    // Re-read state and update visuals. Called on HubMap.OnNodeChanged.
    // The curse level is a HIDDEN value — pins never display it as a measurable
    // gauge. The icon tint shifts only coarsely so the player can sense unease
    // without reading a number. (Ported verbatim from MapNodeView.Refresh.)
    public void Refresh()
    {
        if (Node == null) return;

        Color tint;
        if (!Unlocked)
            tint = lockedTint;
        else if (Node.isSanctuarySite)
            tint = Color.Lerp(Color.white, sanctuaryColor, 0.30f);
        else if (Node.curseLevel >= 0.5f)
            tint = Color.Lerp(Color.white, cursedColor, 0.22f);   // subtle, fixed
        else
            tint = Color.white;

        if (_dimmed) tint.a *= 0.25f;
        if (pinIcon) pinIcon.color = tint;

        if (label)
        {
            var lc = Unlocked ? new Color(0.25f, 0.18f, 0.10f) : new Color(0.45f, 0.42f, 0.40f);
            lc.a = _dimmed ? 0.25f : 1f;
            label.color = lc;
        }
    }

    public void SetSelected(bool selected)
    {
        _selected = selected;
        if (selectedOutline) selectedOutline.enabled = selected;
    }

    // Search filter: non-matching pins fade back instead of vanishing, so the
    // map keeps its shape while the matches pop.
    public void SetDimmed(bool dimmed)
    {
        _dimmed = dimmed;
        Refresh();
        if (weatherIcon) SetIconAlpha(weatherIcon, dimmed ? 0.25f : 1f);
        if (floodBadge)  SetIconAlpha(floodBadge,  dimmed ? 0.25f : 1f);
    }

    static void SetIconAlpha(Image img, float a)
    {
        var c = img.color; c.a = a; img.color = c;
    }

    public void SetWeather(Sprite glyph, bool floodRisk)
    {
        if (weatherIcon)
        {
            weatherIcon.sprite  = glyph;
            weatherIcon.enabled = glyph != null && Unlocked;
        }
        if (floodBadge) floodBadge.enabled = floodRisk && Unlocked;
    }

    // The Gugol blue-dot parody: pulses on the district the player occupies.
    public void SetYouAreHere(bool here)
    {
        if (youAreHereDot == null) return;
        youAreHereDot.enabled = here;
        if (_pulse != null) { StopCoroutine(_pulse); _pulse = null; }
        if (here && isActiveAndEnabled) _pulse = StartCoroutine(Pulse());
    }

    // Unscaled time — the map runs at timeScale 0.
    System.Collections.IEnumerator Pulse()
    {
        var rt = youAreHereDot.rectTransform;
        float t = 0f;
        while (true)
        {
            t += Time.unscaledDeltaTime;
            float s = 1f + 0.18f * Mathf.Sin(t * 3.5f);
            rt.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
    }

    void OnDisable()
    {
        if (_pulse != null) { StopCoroutine(_pulse); _pulse = null; }
    }

    void ApplyScale()
    {
        transform.localScale = Vector3.one * _hoverScale;
    }

    // ── Pointer events ─────────────────────────────────────────────────────────

    public void OnPointerClick(PointerEventData e) => _map?.OnPinClicked(this);

    public void OnPointerEnter(PointerEventData e)
    {
        _hoverScale = 1.15f;
        ApplyScale();
        if (!Unlocked && lockedTooltip) lockedTooltip.gameObject.SetActive(true);
        _map?.OnPinHovered(this, true);
    }

    public void OnPointerExit(PointerEventData e)
    {
        _hoverScale = 1f;
        ApplyScale();
        if (lockedTooltip) lockedTooltip.gameObject.SetActive(false);
        _map?.OnPinHovered(this, false);
    }
}
