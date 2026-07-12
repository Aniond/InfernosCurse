using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Small shared builders for the code-built Gugol Mappe UI (map overlay +
// directions card). Same conventions as the other self-building menus
// (FastTravelMenu / GuildPanelUI): plain Images, TMP text, no prefabs.
public static class GugolUi
{
    // Google's brand colors, for the "Gugol Maps" wordmark parody.
    public const string WordmarkRichText =
        "<color=#4285F4>G</color><color=#EA4335>u</color><color=#FBBC05>g</color>" +
        "<color=#4285F4>o</color><color=#34A853>l</color> <color=#EFE3C4>Maps</color>";

    static Sprite _circle;

    // Anti-aliased white circle for route dots / the you-are-here dot when no
    // art is assigned yet. Generated once, kept for the app lifetime.
    public static Sprite CircleSprite
    {
        get
        {
            if (_circle != null) return _circle;
            const int S = 32;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            float r = S * 0.5f - 1f, cx = S * 0.5f - 0.5f;
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cx) * (y - cx));
                    float a = Mathf.Clamp01(r - d + 0.5f);   // 1px AA edge
                    px[y * S + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels(px);
            tex.Apply();
            _circle = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f));
            _circle.hideFlags = HideFlags.HideAndDontSave;
            return _circle;
        }
    }

    public static TextMeshProUGUI MakeText(Transform parent, string text, float size,
        FontStyles style, Color color, TMP_FontAsset font = null)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        if (font != null) t.font = font;
        t.alignment = TextAlignmentOptions.MidlineLeft;
        t.raycastTarget = false;
        return t;
    }

    public static Image MakeImage(Transform parent, string name, Sprite sprite, Color color,
        bool raycast = false)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = raycast;
        return img;
    }

    // Full-anchor stretch within the parent rect.
    public static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // A clickable row in the house style (FastTravelMenu.AddRow palette).
    public static Button MakeRow(Transform parent, string label, TMP_FontAsset font,
        UnityEngine.Events.UnityAction onClick, float minHeight = 44f)
    {
        var go = new GameObject("Row_" + label, typeof(RectTransform), typeof(Image),
            typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().minHeight = minHeight;

        var img = go.GetComponent<Image>();
        img.color = new Color(0.16f, 0.13f, 0.20f, 0.9f);

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.30f, 0.24f, 0.36f, 1f);
        colors.pressedColor     = new Color(0.45f, 0.36f, 0.20f, 1f);
        colors.fadeDuration     = 0.05f;
        btn.colors = colors;
        btn.onClick.AddListener(onClick);

        var t = MakeText(go.transform, label, 22, FontStyles.Normal, Color.white, font);
        t.margin = new Vector4(14, 0, 8, 0);
        Stretch((RectTransform)t.transform);
        return btn;
    }

    // Deterministic parody rating from the node id — NEVER derived from the
    // hidden curse value. Stable FNV-1a so it survives domain reloads and
    // differs per runtime's string.GetHashCode.
    public static int StableHash(string s)
    {
        unchecked
        {
            uint h = 2166136261;
            foreach (char c in s) { h ^= c; h *= 16777619; }
            return (int)h;
        }
    }

    public static float Rating(HubNode node)
        => 3.9f + (Mathf.Abs(StableHash(node.id)) % 11) * 0.1f;   // 3.9–4.9

    // Review counts are ordinary deterministic parody data. Hidden Circle state
    // never changes a rating or review count.
    public static int ReviewCount(HubNode node)
        => 100 + Mathf.Abs(StableHash(node.id + "r")) % 900;
}
