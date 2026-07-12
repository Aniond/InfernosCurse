using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// The Gugol Mappe directions card — the parody of Google's place panel.
// Shows "{from} → {to}", walking minutes, a star rating, the district's
// weather, the watercolor preview and blurb, and the gold "Vai" button.
// For the district you're already in it flips to "Sei già qui" plus a
// "Nelle vicinanze" list of in-zone jump spots (ported from FastTravelMenu).
// Built entirely in code by GugolMapUI; logic ported from MapNodeDetailPanel.
public class GugolDirectionsCard : MonoBehaviour
{
    // Assigned by Init()
    GugolMapUI _map;
    TMP_FontAsset _headerFont, _bodyFont;
    Sprite _walkerIcon, _starIcon, _clockIcon;
    Sprite _fallbackPreview;
    bool _parchment;               // real card art assigned → dark ink text

    GameObject _rootPanel;
    RectTransform _parentRect;
    RectTransform _content;
    ScrollRect _scrollRect;
    GameObject _statsRow;
    LayoutElement _previewLayout;
    Vector2 _lastParentSize;
    TMP_Text _routeLabel;
    TMP_Text _statsLabel;
    Image    _statsWalker, _statsStar, _statsWeather;
    Image    _preview;
    TMP_Text _blurb;
    TMP_Text _badge;
    Button   _vaiButton;
    TMP_Text _vaiLabel;
    Transform _nearbyParent;
    readonly List<GameObject> _nearbyRows = new();

    Color Ink       => _parchment ? new Color(0.24f, 0.17f, 0.09f) : new Color(0.92f, 0.90f, 0.86f);
    Color InkSoft   => _parchment ? new Color(0.38f, 0.30f, 0.20f) : new Color(0.75f, 0.72f, 0.68f);
    Color Gold      => new Color(0.95f, 0.85f, 0.55f);

    public bool IsOpen => _rootPanel != null && _rootPanel.activeSelf;

    // ── Construction ───────────────────────────────────────────────────────────

    public void Init(GugolMapUI map, RectTransform parent, Sprite cardSprite,
        TMP_FontAsset headerFont, TMP_FontAsset bodyFont,
        Sprite walkerIcon, Sprite starIcon, Sprite clockIcon, Sprite fallbackPreview)
    {
        _map = map;
        _headerFont = headerFont;
        _bodyFont = bodyFont;
        _walkerIcon = walkerIcon;
        _starIcon = starIcon;
        _clockIcon = clockIcon;
        _fallbackPreview = fallbackPreview;
        _parchment = cardSprite != null;

        // Panel: left column under the search bar, Google-style.
        _parentRect = parent;
        _rootPanel = new GameObject("DirectionsCard", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        _rootPanel.transform.SetParent(parent, false);
        var rt = (RectTransform)_rootPanel.transform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(28f, -120f);
        rt.sizeDelta = new Vector2(400f, 600f);

        var bg = _rootPanel.GetComponent<Image>();
        if (cardSprite != null)
        {
            bg.sprite = cardSprite;
            bg.type = Image.Type.Sliced;
            bg.color = Color.white;
        }
        else bg.color = new Color(0.08f, 0.06f, 0.10f, 0.94f);

        var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewportGo.transform.SetParent(_rootPanel.transform, false);
        var viewport = (RectTransform)viewportGo.transform;
        GugolUi.Stretch(viewport);

        var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentGo.transform.SetParent(viewport, false);
        _content = (RectTransform)contentGo.transform;
        _content.anchorMin = new Vector2(0f, 1f);
        _content.anchorMax = new Vector2(1f, 1f);
        _content.pivot = new Vector2(0.5f, 1f);
        _content.anchoredPosition = Vector2.zero;
        _content.sizeDelta = Vector2.zero;

        var vlg = contentGo.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(20, 20, 18, 18);
        vlg.spacing = 8;
        vlg.childControlWidth = true;
        vlg.childForceExpandWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandHeight = false;

        contentGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        _scrollRect = _rootPanel.GetComponent<ScrollRect>();
        _scrollRect.viewport = viewport;
        _scrollRect.content = _content;
        _scrollRect.horizontal = false;
        _scrollRect.vertical = true;
        _scrollRect.movementType = ScrollRect.MovementType.Clamped;
        _scrollRect.scrollSensitivity = 24f;

        // Close "×" floats over the top-right corner.
        var close = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        close.transform.SetParent(_rootPanel.transform, false);
        var closeLe = close.GetComponent<LayoutElement>();
        closeLe.ignoreLayout = true;
        var closeRt = (RectTransform)close.transform;
        closeRt.anchorMin = closeRt.anchorMax = new Vector2(1f, 1f);
        closeRt.pivot = new Vector2(1f, 1f);
        closeRt.anchoredPosition = new Vector2(-5f, -5f);
        closeRt.sizeDelta = new Vector2(44f, 44f);
        var closeImg = close.GetComponent<Image>();
        closeImg.color = new Color(0f, 0f, 0f, 0.001f);   // invisible hit area
        close.GetComponent<Button>().onClick.AddListener(Hide);
        var closeX = GugolUi.MakeText(close.transform, "×", 26, FontStyles.Bold, InkSoft, _bodyFont);
        closeX.alignment = TextAlignmentOptions.Center;
        GugolUi.Stretch((RectTransform)closeX.transform);

        // Route header
        _routeLabel = GugolUi.MakeText(_content, "", 27, FontStyles.Bold, Ink, _headerFont);
        _routeLabel.margin = new Vector4(0f, 0f, 36f, 0f);

        // Stats row: [walker] 14 min a piedi   [star] 4.8 (666)   [weather glyph]
        var stats = new GameObject("Stats", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        stats.transform.SetParent(_content, false);
        _statsRow = stats;
        stats.GetComponent<LayoutElement>().minHeight = 26;
        var hlg = stats.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 6;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;

        _statsWalker = MakeStatIcon(stats.transform, _walkerIcon);
        _statsStar   = MakeStatIcon(stats.transform, _starIcon);      // re-anchored by text below
        _statsLabel  = GugolUi.MakeText(stats.transform, "", 20, FontStyles.Normal, InkSoft, _bodyFont);
        var statsTextLayout = _statsLabel.gameObject.AddComponent<LayoutElement>();
        statsTextLayout.flexibleWidth = 1f;
        statsTextLayout.minWidth = 120f;
        _statsWeather = MakeStatIcon(stats.transform, null);

        // Preview splash
        var prevGo = new GameObject("Preview", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        prevGo.transform.SetParent(_content, false);
        _previewLayout = prevGo.GetComponent<LayoutElement>();
        _previewLayout.preferredHeight = 150;
        _preview = prevGo.GetComponent<Image>();
        _preview.preserveAspect = true;
        _preview.raycastTarget = false;

        // Blurb
        _blurb = GugolUi.MakeText(_content, "", 19, FontStyles.Normal, Ink, _bodyFont);
        _blurb.alignment = TextAlignmentOptions.TopLeft;

        // Status badge ("Sei già qui" / "Chiuso — apre presto")
        _badge = GugolUi.MakeText(_content, "", 21, FontStyles.Bold | FontStyles.Italic, Gold, _bodyFont);

        // Vai (gold travel button)
        var vai = new GameObject("Vai", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        vai.transform.SetParent(_content, false);
        vai.GetComponent<LayoutElement>().minHeight = 46;
        var vaiImg = vai.GetComponent<Image>();
        vaiImg.color = new Color(0.45f, 0.36f, 0.20f, 1f);
        _vaiButton = vai.GetComponent<Button>();
        _vaiButton.targetGraphic = vaiImg;
        var vc = _vaiButton.colors;
        vc.highlightedColor = new Color(0.58f, 0.47f, 0.26f, 1f);
        vc.pressedColor     = new Color(0.70f, 0.58f, 0.32f, 1f);
        vc.fadeDuration     = 0.05f;
        _vaiButton.colors = vc;
        _vaiLabel = GugolUi.MakeText(vai.transform, "Go", 24, FontStyles.Bold, new Color(0.98f, 0.95f, 0.85f), _headerFont);
        _vaiLabel.alignment = TextAlignmentOptions.Center;
        GugolUi.Stretch((RectTransform)_vaiLabel.transform);

        // Nelle vicinanze (in-zone jumps) — rows appended under everything.
        _nearbyParent = _content;

        RefreshResponsiveLayout(true);
        Hide();
    }

    void LateUpdate()
    {
        if (IsOpen) RefreshResponsiveLayout(false);
    }

    void RefreshResponsiveLayout(bool force)
    {
        if (_rootPanel == null || _parentRect == null) return;
        Vector2 parentSize = _parentRect.rect.size;
        if (!force && (parentSize - _lastParentSize).sqrMagnitude < 1f) return;
        _lastParentSize = parentSize;

        float horizontalMargin = parentSize.x < 600f ? 14f : 28f;
        float availableWidth = Mathf.Max(220f, parentSize.x - horizontalMargin * 2f);
        float width = Mathf.Min(400f, availableWidth);
        float availableHeight = Mathf.Max(260f, parentSize.y - 138f);
        float height = Mathf.Min(620f, availableHeight);
        var rt = (RectTransform)_rootPanel.transform;
        rt.anchoredPosition = new Vector2(horizontalMargin, -120f);
        rt.sizeDelta = new Vector2(width, height);

        bool narrow = width < 350f;
        _routeLabel.fontSize = narrow ? 24f : 27f;
        _blurb.fontSize = narrow ? 17f : 19f;
        _previewLayout.preferredHeight = narrow ? 126f : 150f;
        LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
    }

    Image MakeStatIcon(Transform parent, Sprite sprite)
    {
        var img = GugolUi.MakeImage(parent, "StatIcon", sprite, Color.white);
        img.rectTransform.sizeDelta = new Vector2(22f, 22f);
        img.preserveAspect = true;
        if (sprite == null) img.enabled = false;
        return img;
    }

    // ── Content modes ──────────────────────────────────────────────────────────

    // A destination district (unlocked or teaser).
    public void ShowDestination(HubNode node, string fromName, int minutes,
        Sprite weatherGlyph, bool unlocked, Action onVai)
    {
        ClearNearby();
        _statsRow.SetActive(true);

        _routeLabel.text = unlocked
            ? $"{fromName}  →  {node.displayName}"
            : node.displayName;

        _statsLabel.text = $"{minutes} min walk   ·   {GugolUi.Rating(node):0.0} ({GugolUi.ReviewCount(node)})";
        _statsWalker.enabled = _walkerIcon != null;
        _statsStar.enabled = false;   // star folded into the text until icon art lands
        _statsWeather.sprite = weatherGlyph;
        _statsWeather.enabled = weatherGlyph != null;

        SetPreview(node);
        _blurb.text = node.blurb ?? "";

        _badge.gameObject.SetActive(!unlocked);
        _badge.text = "Closed — coming soon";

        _vaiButton.gameObject.SetActive(unlocked);
        _vaiButton.interactable = true;   // a greyed region card must not leak here
        _vaiButton.onClick.RemoveAllListeners();
        if (unlocked) _vaiButton.onClick.AddListener(() => onVai?.Invoke());

        _rootPanel.SetActive(true);
        RefreshResponsiveLayout(true);
        _scrollRect.verticalNormalizedPosition = 1f;
    }

    // A region-layer destination (road travel): stats are preformatted by
    // GugolMapUI ("≈3 hours by road · 4 florins · 4.6 (312)"); an unaffordable
    // fare greys Go and shows the badge up front.
    public void ShowRegionDestination(HubNode node, string fromName, string statsText,
        Sprite weatherGlyph, bool unlocked, bool affordable, Action onVai)
    {
        ClearNearby();
        _statsRow.SetActive(true);

        _routeLabel.text = unlocked ? $"{fromName}  →  {node.displayName}" : node.displayName;

        _statsLabel.text = statsText;
        _statsWalker.enabled = _walkerIcon != null;
        _statsStar.enabled = false;
        _statsWeather.sprite = weatherGlyph;
        _statsWeather.enabled = weatherGlyph != null;

        SetPreview(node);
        _blurb.text = node.blurb ?? "";

        _badge.gameObject.SetActive(!unlocked || !affordable);
        _badge.text = !unlocked ? "Closed — coming soon" : "Not enough florins";

        _vaiButton.gameObject.SetActive(unlocked);
        _vaiButton.interactable = affordable;
        _vaiButton.onClick.RemoveAllListeners();
        if (unlocked && affordable) _vaiButton.onClick.AddListener(() => onVai?.Invoke());

        _rootPanel.SetActive(true);
        RefreshResponsiveLayout(true);
        _scrollRect.verticalNormalizedPosition = 1f;
    }

    // The TrySpend-at-Go race: balance changed between showing the card and
    // pressing Go — surface it without tearing the card down.
    public void ShowFareError(string message)
    {
        if (_rootPanel == null || !_rootPanel.activeSelf) return;
        _badge.gameObject.SetActive(true);
        _badge.text = message;
        _vaiButton.interactable = false;
    }

    // The district the player is standing in: no travel, but quick jumps to
    // this scene's ZoneEntryPoints (ported from FastTravelMenu's "This Area").
    public void ShowCurrent(HubNode node, List<ZoneEntryPoint> spots, Action<ZoneEntryPoint> onJump)
    {
        ClearNearby();
        _statsRow.SetActive(false);

        _routeLabel.text = node.displayName;
        _statsLabel.text = string.Empty;
        _statsWalker.enabled = false;
        _statsStar.enabled = false;
        _statsWeather.enabled = false;

        SetPreview(node);
        _blurb.text = node.blurb ?? "";

        _badge.gameObject.SetActive(true);
        _badge.text = "You are here";

        _vaiButton.gameObject.SetActive(false);

        if (spots != null && spots.Count > 0)
        {
            var header = GugolUi.MakeText(_nearbyParent, "Nearby", 20, FontStyles.Bold, InkSoft, _headerFont);
            header.margin = new Vector4(0, 8, 0, 2);
            _nearbyRows.Add(header.gameObject);

            foreach (var spot in spots)
            {
                var captured = spot;
                var row = GugolUi.MakeRow(_nearbyParent, captured.Label, _bodyFont, () => onJump?.Invoke(captured));
                _nearbyRows.Add(row.gameObject);
            }
        }

        _rootPanel.SetActive(true);
        RefreshResponsiveLayout(true);
        _scrollRect.verticalNormalizedPosition = 1f;
    }

    void SetPreview(HubNode node)
    {
        var sprite = node.previewImage != null ? node.previewImage : _fallbackPreview;
        _preview.sprite = sprite;
        _preview.gameObject.SetActive(sprite != null);
    }

    void ClearNearby()
    {
        foreach (var go in _nearbyRows) if (go != null) Destroy(go);
        _nearbyRows.Clear();
    }

    public void Hide()
    {
        ClearNearby();
        if (_rootPanel != null) _rootPanel.SetActive(false);
        _map?.OnCardHidden();
    }

#if UNITY_EDITOR
    public RectTransform ValidationPanelRect => _rootPanel != null ? (RectTransform)_rootPanel.transform : null;
    public bool ValidationStatsVisible => _statsRow != null && _statsRow.activeSelf;
    public bool ValidationHasScrollViewport => _scrollRect != null && _scrollRect.viewport != null && _scrollRect.content != null;
#endif
}
