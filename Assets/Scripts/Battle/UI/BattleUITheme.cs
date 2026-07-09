using UnityEngine;
using UnityEngine.UI;
using TMPro;

// The battle UI's period skin — 1299 Florence: dark leather, aged gold,
// parchment text, Cinzel for headers and EBGaramond for body copy.
// Every code-built battle panel pulls from here so the look stays one thing.
public static class BattleUITheme
{
    // ── Palette ───────────────────────────────────────────────────────────────
    public static readonly Color Ink       = new Color(0.07f, 0.055f, 0.04f, 0.95f); // near-black leather
    public static readonly Color Leather   = new Color(0.16f, 0.11f, 0.07f, 0.95f);  // panel body
    public static readonly Color LeatherHi = new Color(0.24f, 0.17f, 0.10f, 0.95f);  // raised button
    public static readonly Color Gold      = new Color(0.85f, 0.68f, 0.25f, 1f);     // frames, highlights
    public static readonly Color GoldDim   = new Color(0.55f, 0.44f, 0.18f, 0.9f);   // quiet keylines
    public static readonly Color Parchment = new Color(0.92f, 0.87f, 0.76f, 1f);     // body text
    public static readonly Color ParchDim  = new Color(0.62f, 0.58f, 0.50f, 1f);     // spent/disabled text
    public static readonly Color Blood     = new Color(0.55f, 0.10f, 0.08f, 0.95f);  // danger

    // HP bar stops
    public static readonly Color HpHigh = new Color(0.42f, 0.62f, 0.28f); // olive green
    public static readonly Color HpMid  = new Color(0.80f, 0.62f, 0.20f); // worn gold
    public static readonly Color HpLow  = new Color(0.70f, 0.16f, 0.10f); // blood

    // ── Fonts (Resources/UIFonts, loaded once) ────────────────────────────────
    static TMP_FontAsset _header, _body;
    public static TMP_FontAsset HeaderFont =>
        _header != null ? _header : (_header = Resources.Load<TMP_FontAsset>("UIFonts/Cinzel SDF"));
    public static TMP_FontAsset BodyFont =>
        _body != null ? _body : (_body = Resources.Load<TMP_FontAsset>("UIFonts/EBGaramond SDF"));

    // ── Builders ──────────────────────────────────────────────────────────────

    // Framed panel: thin gold frame, dark leather face. Content goes on the
    // returned INNER rect so the frame never draws over it.
    public static RectTransform MakePanel(Transform parent, string name,
                                          Vector2 anchor, Vector2 pivot,
                                          Vector2 anchoredPos, Vector2 size)
    {
        var frameGo = new GameObject(name);
        frameGo.transform.SetParent(parent, false);
        var frame = frameGo.AddComponent<Image>();
        frame.color = GoldDim;
        var frt = frame.rectTransform;
        frt.anchorMin = anchor; frt.anchorMax = anchor;
        frt.pivot = pivot;
        frt.anchoredPosition = anchoredPos;
        frt.sizeDelta = size;

        var innerGo = new GameObject("Face");
        innerGo.transform.SetParent(frameGo.transform, false);
        var inner = innerGo.AddComponent<Image>();
        inner.color = Leather;
        var irt = inner.rectTransform;
        irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
        irt.offsetMin = new Vector2(2f, 2f);
        irt.offsetMax = new Vector2(-2f, -2f);

        return irt;
    }

    public static TMP_Text MakeHeader(Transform parent, string name, float fontSize = 22f)
    {
        var t = MakeText(parent, name, fontSize);
        t.font = HeaderFont != null ? HeaderFont : t.font;
        t.color = Gold;
        return t;
    }

    public static TMP_Text MakeBody(Transform parent, string name, float fontSize = 20f)
    {
        var t = MakeText(parent, name, fontSize);
        t.font = BodyFont != null ? BodyFont : t.font;
        t.color = Parchment;
        return t;
    }

    static TMP_Text MakeText(Transform parent, string name, float fontSize)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.fontSize = fontSize;
        t.alignment = TextAlignmentOptions.Center;
        return t;
    }

    // Restyle an existing label to the theme (for prefab-built UI like the
    // action menu, which already owns its TMP components).
    public static void StyleHeader(TMP_Text t, float? size = null)
    {
        if (t == null) return;
        if (HeaderFont != null) t.font = HeaderFont;
        t.color = Gold;
        if (size.HasValue) t.fontSize = size.Value;
    }

    public static void StyleBody(TMP_Text t, float? size = null)
    {
        if (t == null) return;
        if (BodyFont != null) t.font = BodyFont;
        t.color = Parchment;
        if (size.HasValue) t.fontSize = size.Value;
    }
}
