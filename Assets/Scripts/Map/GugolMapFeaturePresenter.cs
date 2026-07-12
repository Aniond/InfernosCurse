using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class GugolMapFeaturePresenter : MonoBehaviour
{
    GugolMapUI _owner;
    RectTransform _layer;
    TMP_FontAsset _bodyFont;
    Color _ink;
    GugolMapWorldStatePresenter _worldState;
    readonly List<GameObject> _active = new();

    public int ActiveVisualCount => _active.Count;

    public void Configure(GugolMapUI owner, RectTransform layer, TMP_FontAsset bodyFont, Color ink)
    {
        _owner = owner;
        _layer = layer;
        _bodyFont = bodyFont;
        _ink = ink;
        _worldState = new GugolMapWorldStatePresenter();
    }

    public void ShowCityStreets(GugolMapKnowledgeSnapshot snapshot)
    {
        Clear();
        if (_layer == null || snapshot == null) return;
        foreach (var feature in snapshot.Visible(GugolMapFeatureKind.Street))
        {
            if (feature.street == null || feature.knowledgeState == GugolMapKnowledgeState.Forgotten) continue;
            var go = new GameObject("Street_" + feature.featureId, typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(_layer, false);
            GugolUi.Stretch((RectTransform)go.transform);
            var graphic = go.AddComponent<GugolStreetFeatureGraphic>();
            graphic.Bind(feature.street, _owner, _ink);
            _active.Add(go);

            Vector2 center = Average(feature.street.cityCenterline);
            if (!_worldState.ShowLabel(feature)) continue;
            var label = GugolUi.MakeText(_layer, feature.displayName, 16f, FontStyles.Bold,
                _worldState.TintFor(feature, _ink),
                _bodyFont);
            label.alignment = TextAlignmentOptions.Center;
            label.outlineWidth = 0.22f;
            label.outlineColor = new Color32(245, 238, 220, 255);
            Position(label.rectTransform, center, new Vector2(220f, 25f));
            _active.Add(label.gameObject);
        }
    }

    public void ShowStreet(GugolMapKnowledgeSnapshot snapshot, GugolStreetDefinition street)
    {
        Clear();
        if (_layer == null || snapshot == null || street == null) return;
        var venueById = new Dictionary<string, GugolMapFeatureRecord>(StringComparer.Ordinal);
        foreach (var feature in snapshot.Visible(GugolMapFeatureKind.Venue))
        {
            if (feature.venue == null || !string.Equals(feature.streetId, street.streetId, StringComparison.Ordinal) ||
                feature.knowledgeState == GugolMapKnowledgeState.Forgotten) continue;
            venueById[feature.featureId] = feature;
            BuildMarker(feature, feature.venue.streetViewAnchor, false);
        }

        int npcOffset = 0;
        foreach (var feature in snapshot.Visible(GugolMapFeatureKind.Npc))
        {
            if (!string.Equals(feature.streetId, street.streetId, StringComparison.Ordinal) ||
                feature.knowledgeState == GugolMapKnowledgeState.Forgotten) continue;
            Vector2 anchor = street.routeFallbackPosition;
            if (!string.IsNullOrWhiteSpace(feature.venueId) && venueById.TryGetValue(feature.venueId, out var venue))
                anchor = venue.venue.streetViewAnchor;
            float angle = npcOffset++ * 1.7f;
            anchor += new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.035f;
            anchor = new Vector2(Mathf.Clamp01(anchor.x), Mathf.Clamp01(anchor.y));
            BuildMarker(feature, anchor, true);
        }
    }

    public void Clear()
    {
        foreach (var go in _active) if (go != null) Destroy(go);
        _active.Clear();
    }

    void OnDisable() => Clear();

    void BuildMarker(GugolMapFeatureRecord feature, Vector2 anchor, bool npc)
    {
        var go = new GameObject((npc ? "Npc_" : "Venue_") + feature.featureId,
            typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(_layer, false);
        var rect = (RectTransform)go.transform;
        Position(rect, anchor, npc ? new Vector2(42f, 42f) : new Vector2(50f, 50f));
        // The fallback Street View magnifies the city parchment. Keep UI
        // markers at a readable screen size while their map positions zoom.
        var sizeKeeper = go.AddComponent<GugolMapScreenSizeKeeper>();
        sizeKeeper.zoomRoot = _layer.parent;
        var image = go.GetComponent<Image>();
        image.sprite = GugolUi.CircleSprite;
        Color normal = npc ? new Color(0.20f, 0.43f, 0.72f, 0.96f) : new Color(0.55f, 0.16f, 0.12f, 0.96f);
        image.color = _worldState.TintFor(feature, normal);
        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() => _owner?.OnMapFeatureClicked(feature));

        var glyph = GugolUi.MakeText(go.transform, npc ? "N" : VenueGlyph(feature.venue), 19f,
            FontStyles.Bold, Color.white, _bodyFont);
        glyph.alignment = TextAlignmentOptions.Center;
        GugolUi.Stretch(glyph.rectTransform);

        // Venue names are useful browse-at-a-glance labels. NPC names are
        // intentionally revealed by search or click so a busy street remains
        // readable instead of becoming a wall of overlapping text.
        if (!npc)
        {
            var label = GugolUi.MakeText(go.transform, feature.displayName, 13f, FontStyles.Bold, _ink, _bodyFont);
            label.alignment = TextAlignmentOptions.Center;
            label.outlineWidth = 0.22f;
            label.outlineColor = new Color32(245, 238, 220, 255);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            label.rectTransform.pivot = new Vector2(0.5f, 1f);
            label.rectTransform.anchoredPosition = new Vector2(0f, -6f);
            label.rectTransform.sizeDelta = new Vector2(150f, 22f);
        }
        _active.Add(go);
    }

    static string VenueGlyph(GugolVenueDefinition venue) => venue == null ? "•" : venue.category switch
    {
        GugolVenueCategory.Inn => "I",
        GugolVenueCategory.Shop => "S",
        GugolVenueCategory.Service => "+",
        GugolVenueCategory.Workshop => "W",
        GugolVenueCategory.Landmark => "◆",
        _ => "•",
    };

    void Position(RectTransform rect, Vector2 normalized, Vector2 size)
    {
        Vector2 layerSize = _layer.rect.size;
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(normalized.x * layerSize.x, normalized.y * layerSize.y);
        rect.sizeDelta = size;
    }

    static Vector2 Average(Vector2[] points)
    {
        if (points == null || points.Length == 0) return new Vector2(0.5f, 0.5f);
        Vector2 sum = Vector2.zero;
        foreach (Vector2 point in points) sum += point;
        return sum / points.Length;
    }
}

// Street fallback transitions animate the whole parchment after markers are
// built. Counter-scale each marker continuously so its hit target and label
// remain legible instead of swelling with the map crop.
sealed class GugolMapScreenSizeKeeper : MonoBehaviour
{
    public Transform zoomRoot;

    void LateUpdate()
    {
        float zoom = zoomRoot != null ? Mathf.Abs(zoomRoot.localScale.x) : 1f;
        transform.localScale = Vector3.one / Mathf.Max(1f, zoom);
    }
}
