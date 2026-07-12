using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
public sealed class GugolStreetFeatureGraphic : MaskableGraphic,
    IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    GugolStreetDefinition _street;
    GugolMapUI _owner;
    float _visualWidth = 3f;
    float _hitWidth = 24f;
    Color _normalColor = new(0.33f, 0.23f, 0.13f, 0.12f);
    Color _hoverColor = new(0.33f, 0.23f, 0.13f, 0.40f);

    public GugolStreetDefinition Street => _street;

    public void Bind(
        GugolStreetDefinition street,
        GugolMapUI owner,
        Color ink,
        float visualWidth = 3f)
    {
        _street = street;
        _owner = owner;
        _visualWidth = Mathf.Max(1f, visualWidth);
        _normalColor = new Color(ink.r, ink.g, ink.b, 0.12f);
        _hoverColor = new Color(ink.r, ink.g, ink.b, 0.42f);
        raycastTarget = true;
        color = _normalColor;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (_street == null || _street.cityCenterline == null || _street.cityCenterline.Length < 2) return;
        Rect rect = rectTransform.rect;
        _hitWidth = Mathf.Max(_visualWidth, _street.cityHitWidth * Mathf.Min(rect.width, rect.height));
        for (int i = 1; i < _street.cityCenterline.Length; i++)
            AddSegment(vh, ToLocal(rect, _street.cityCenterline[i - 1]), ToLocal(rect, _street.cityCenterline[i]), _visualWidth);
    }

    public override bool Raycast(Vector2 screenPoint, Camera eventCamera)
    {
        if (_street == null || _street.cityCenterline == null) return false;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out var local))
            return false;
        Rect rect = rectTransform.rect;
        for (int i = 1; i < _street.cityCenterline.Length; i++)
            if (DistanceToSegment(local, ToLocal(rect, _street.cityCenterline[i - 1]),
                    ToLocal(rect, _street.cityCenterline[i])) <= _hitWidth * 0.5f)
                return true;
        return false;
    }

    public void OnPointerClick(PointerEventData eventData) => _owner?.OnStreetClicked(_street);

    public void OnPointerEnter(PointerEventData eventData) => color = _hoverColor;

    public void OnPointerExit(PointerEventData eventData) => color = _normalColor;

    static Vector2 ToLocal(Rect rect, Vector2 normalized) =>
        new(rect.xMin + normalized.x * rect.width, rect.yMin + normalized.y * rect.height);

    void AddSegment(VertexHelper vh, Vector2 start, Vector2 end, float width)
    {
        Vector2 direction = (end - start).normalized;
        Vector2 normal = new(-direction.y, direction.x);
        Vector2 offset = normal * width * 0.5f;
        int index = vh.currentVertCount;
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;
        vertex.position = start - offset; vh.AddVert(vertex);
        vertex.position = start + offset; vh.AddVert(vertex);
        vertex.position = end + offset; vh.AddVert(vertex);
        vertex.position = end - offset; vh.AddVert(vertex);
        vh.AddTriangle(index, index + 1, index + 2);
        vh.AddTriangle(index, index + 2, index + 3);
    }

    static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        Vector2 segment = end - start;
        float lengthSquared = segment.sqrMagnitude;
        if (lengthSquared <= Mathf.Epsilon) return Vector2.Distance(point, start);
        float t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSquared);
        return Vector2.Distance(point, start + segment * t);
    }
}
