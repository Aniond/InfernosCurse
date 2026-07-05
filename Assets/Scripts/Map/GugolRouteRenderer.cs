using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Draws the dotted Gugol Mappe route between districts and animates the tiny
// walker along it when travel is confirmed. Lives on a dedicated RouteLayer
// RectTransform under the map image (below the pins). All positions use the
// same normalized→anchored math as the pins; all animation runs on UNSCALED
// time because the map pauses the game (timeScale 0).
public class GugolRouteRenderer : MonoBehaviour
{
    [Header("Wired by GugolMapUI at build time")]
    public RectTransform mapRect;       // pins/dots live in this rect's space
    public Sprite dotSprite;            // small round dot (runtime-generated fallback)
    public Sprite walkerSprite;         // hooded traveler marker

    [Header("Look")]
    public float dotSpacing   = 22f;    // px between dots at reference resolution
    public float dotSize      = 10f;
    public Color dotDim       = new Color(0.26f, 0.52f, 0.96f, 0.85f);  // Gugol route blue
    public Color dotWalked    = new Color(0.78f, 0.22f, 0.16f, 0.95f);  // wax red behind the walker
    public float walkerSize   = 42f;

    readonly List<Image> _dots = new();
    readonly List<float> _dotDistances = new();   // distance along polyline per dot
    List<Vector2> _points = new();                // anchored positions of route vertices
    float _totalLength;
    Image _walker;
    bool  _skipRequested;

    // ── Route preview ──────────────────────────────────────────────────────────

    // Show the dotted route for a node path (normalized map coords → anchored).
    public void ShowRoute(List<HubNode> path)
    {
        Clear();
        if (path == null || path.Count < 2 || mapRect == null) return;

        _points = new List<Vector2>(path.Count);
        foreach (var node in path)
            _points.Add(NormalizedToAnchored(node.mapImagePosition));

        _totalLength = 0f;
        for (int i = 1; i < _points.Count; i++)
            _totalLength += Vector2.Distance(_points[i - 1], _points[i]);
        if (_totalLength <= 0f) return;

        // Lay dots at even spacing along the whole polyline.
        for (float d = dotSpacing * 0.5f; d < _totalLength; d += dotSpacing)
        {
            var img = MakeDot();
            img.rectTransform.anchoredPosition = PointAt(d);
            _dots.Add(img);
            _dotDistances.Add(d);
        }
    }

    public void Clear()
    {
        foreach (var d in _dots) if (d != null) Destroy(d.gameObject);
        _dots.Clear();
        _dotDistances.Clear();
        _points.Clear();
        _totalLength = 0f;
        if (_walker != null) { Destroy(_walker.gameObject); _walker = null; }
        _skipRequested = false;
    }

    public bool HasRoute => _totalLength > 0f;

    // ── Travel animation ───────────────────────────────────────────────────────

    // ESC during the walk fast-forwards to the walk's endpoint (arrival, or the
    // ambush when a road encounter cut the journey short); travel is never
    // cancelled mid-route (no ambiguous half-travelled state).
    public void SkipToEnd() => _skipRequested = true;

    // stopAtFraction < 1 walks only part of the route (a road encounter):
    // the walker halts there and dots beyond the stop stay route-blue.
    // Callers scale duration by the fraction to keep walker speed constant.
    public IEnumerator AnimateTravel(float duration, Action onComplete, float stopAtFraction = 1f)
    {
        if (!HasRoute) { onComplete?.Invoke(); yield break; }

        float stopDist = _totalLength * Mathf.Clamp01(stopAtFraction);
        _walker = MakeImage("Walker", walkerSprite, new Vector2(walkerSize, walkerSize), Color.white);
        _skipRequested = false;

        float t = 0f;
        while (t < duration && !_skipRequested)
        {
            t += Time.unscaledDeltaTime;
            float dist = Mathf.Clamp01(t / duration) * stopDist;
            _walker.rectTransform.anchoredPosition = PointAt(dist);

            // Dots behind the walker flare wax-red.
            for (int i = 0; i < _dots.Count; i++)
                _dots[i].color = _dotDistances[i] <= dist ? dotWalked : dotDim;

            yield return null;
        }

        if (_walker != null) _walker.rectTransform.anchoredPosition = PointAt(stopDist);
        for (int i = 0; i < _dots.Count; i++)
            _dots[i].color = _dotDistances[i] <= stopDist ? dotWalked : dotDim;
        onComplete?.Invoke();
    }

    // Fraction of the total route length at a path vertex — computed in
    // anchored space (the same space as _totalLength; normalized space has
    // different x/y scale factors and must not be mixed in).
    public float FractionAtVertex(int index)
    {
        if (_points.Count < 2 || _totalLength <= 0f) return 1f;
        index = Mathf.Clamp(index, 0, _points.Count - 1);
        float dist = 0f;
        for (int i = 1; i <= index; i++)
            dist += Vector2.Distance(_points[i - 1], _points[i]);
        return Mathf.Clamp01(dist / _totalLength);
    }

    // ── Geometry ───────────────────────────────────────────────────────────────

    // Same math as pin placement (ported from WorldMapUI.PositionPin): anchored
    // to the map rect's bottom-left, offset by normalized * rect size.
    Vector2 NormalizedToAnchored(Vector2 normalized)
    {
        var size = mapRect.rect.size;
        return new Vector2(normalized.x * size.x, normalized.y * size.y);
    }

    Vector2 PointAt(float distance)
    {
        for (int i = 1; i < _points.Count; i++)
        {
            float seg = Vector2.Distance(_points[i - 1], _points[i]);
            if (distance <= seg || i == _points.Count - 1)
                return Vector2.Lerp(_points[i - 1], _points[i], seg > 0f ? Mathf.Clamp01(distance / seg) : 0f);
            distance -= seg;
        }
        return _points.Count > 0 ? _points[_points.Count - 1] : Vector2.zero;
    }

    Image MakeDot() => MakeImage("RouteDot", dotSprite, new Vector2(dotSize, dotSize), dotDim);

    Image MakeImage(string name, Sprite sprite, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = Vector2.zero;   // bottom-left, like the pins
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }
}
