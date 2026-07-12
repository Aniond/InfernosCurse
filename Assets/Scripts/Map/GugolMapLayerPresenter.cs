using UnityEngine;
using UnityEngine.UI;

public sealed class GugolMapLayerPresenter : MonoBehaviour
{
    Image _image;
    AspectRatioFitter _fitter;
    RectTransform _mapRect;
    GugolMapTransitionController _transition;
    GugolMapPresentationProfile _profile;
    Sprite _cityFallback;

    public GugolMapViewKind View { get; private set; } = GugolMapViewKind.City;
    public string FocusedStreetId { get; private set; } = string.Empty;

    public void Configure(
        Image image,
        AspectRatioFitter fitter,
        GugolMapTransitionController transition,
        GugolMapPresentationProfile profile,
        Sprite cityFallback)
    {
        _image = image;
        _fitter = fitter;
        _mapRect = image != null ? image.rectTransform : null;
        _transition = transition;
        _profile = profile;
        _cityFallback = cityFallback;
    }

    public void ShowBase(MapLevel level, Sprite fallback, bool immediate)
    {
        View = level.ToGugolView();
        FocusedStreetId = string.Empty;
        Sprite background = BackgroundFor(level, fallback);
        ApplyBackground(background);
        Move(Vector3.one, Vector2.zero, immediate);
    }

    public void ShowStreet(GugolStreetDefinition street, bool immediate)
    {
        if (street == null) return;
        View = GugolMapViewKind.Street;
        FocusedStreetId = street.streetId;
        if (street.streetViewBackground != null)
        {
            ApplyBackground(street.streetViewBackground);
            Move(Vector3.one, Vector2.zero, immediate);
            return;
        }

        ApplyBackground(BackgroundFor(MapLevel.City, _cityFallback));
        Rect bounds = street.streetViewBounds;
        float scale = Mathf.Clamp(Mathf.Min(1f / bounds.width, 1f / bounds.height), 1f, 4f);
        Vector2 center = bounds.center;
        Vector2 size = _mapRect != null ? _mapRect.rect.size : Vector2.one;
        Vector2 target = new(
            (0.5f - center.x) * size.x * scale,
            (0.5f - center.y) * size.y * scale);
        Move(Vector3.one * scale, target, immediate);
    }

    Sprite BackgroundFor(MapLevel level, Sprite fallback)
    {
        Sprite profileSprite = _profile == null ? null : level switch
        {
            MapLevel.City => _profile.cityBackground,
            MapLevel.Region => _profile.regionBackground,
            _ => _profile.worldBackground,
        };
        return profileSprite != null ? profileSprite : fallback != null ? fallback : _profile?.fallbackBackground;
    }

    void ApplyBackground(Sprite background)
    {
        if (_image == null || _fitter == null) return;
        _image.sprite = background;
        _image.color = background != null ? Color.white : _profile != null ? _profile.parchment : Color.white;
        if (background != null) _fitter.aspectRatio = background.rect.width / background.rect.height;
        _fitter.enabled = false;
        _fitter.enabled = true;
        Canvas.ForceUpdateCanvases();
    }

    void Move(Vector3 scale, Vector2 position, bool immediate)
    {
        if (_mapRect == null) return;
        float seconds = _profile != null ? _profile.viewTransitionSeconds : 0.25f;
        bool reduced = immediate || (_profile != null && _profile.reducedMotionByDefault);
        if (_transition != null) _transition.Move(_mapRect, scale, position, seconds, reduced);
        else
        {
            _mapRect.localScale = scale;
            _mapRect.anchoredPosition = position;
        }
    }
}
