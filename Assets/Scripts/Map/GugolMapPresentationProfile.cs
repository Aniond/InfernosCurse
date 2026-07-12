using UnityEngine;

[CreateAssetMenu(fileName = "GugolMapPresentation", menuName = "InfernosCurse/Gugol Mappe/Presentation Profile")]
public sealed class GugolMapPresentationProfile : ScriptableObject
{
    [Header("Scale backgrounds")]
    public Sprite cityBackground;
    public Sprite regionBackground;
    public Sprite worldBackground;
    public Sprite fallbackBackground;

    [Header("Shared chrome")]
    public Sprite searchBar;
    public Sprite contextCard;
    public Sprite defaultPin;
    public Sprite townPin;
    public Sprite playerMarker;
    public Sprite routeWalker;

    [Header("Approved palette")]
    public Color parchment = new(0.89f, 0.83f, 0.70f, 1f);
    public Color ink = new(0.24f, 0.17f, 0.09f, 1f);
    public Color mutedInk = new(0.45f, 0.38f, 0.28f, 1f);
    public Color activeRoute = new(0.20f, 0.43f, 0.72f, 1f);
    public Color waxSeal = new(0.55f, 0.16f, 0.12f, 1f);
    public Color garden = new(0.34f, 0.43f, 0.22f, 1f);
    public Color river = new(0.31f, 0.48f, 0.58f, 1f);

    [Header("Motion")]
    [Min(0f)] public float viewTransitionSeconds = 0.28f;
    [Min(0f)] public float routeDrawSeconds = 0.45f;
    [Min(0f)] public float selectedLiftPixels = 8f;
    public bool reducedMotionByDefault;
}
