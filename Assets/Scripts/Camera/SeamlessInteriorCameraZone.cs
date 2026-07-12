using System;
using UnityEngine;

/// <summary>
/// Lightweight bridge between an interior module and the shared exploration
/// camera. The first implementation owns explicit occluder groups; camera
/// profile blending is added without introducing a second Main Camera.
/// </summary>
[DisallowMultipleComponent]
public sealed class SeamlessInteriorCameraZone : MonoBehaviour
{
    [SerializeField] CameraOcclusionFader occlusionFader;
    [SerializeField] Renderer[] interiorOccluders = Array.Empty<Renderer>();
    [SerializeField] string[] interiorWallPrefixes = Array.Empty<string>();

    Renderer[] _defaultOccluders;
    string[] _defaultPrefixes;
    bool _captured;

    public void SetPlayerInside(bool inside)
    {
        ResolveCamera();
        if (occlusionFader == null) return;
        CaptureDefaults();
        occlusionFader.explicitOccluders = inside ? interiorOccluders : _defaultOccluders;
        occlusionFader.wallPrefixes = inside ? interiorWallPrefixes : _defaultPrefixes;
    }

    void ResolveCamera()
    {
        if (occlusionFader != null) return;
        Camera main = Camera.main;
        if (main != null) occlusionFader = main.GetComponent<CameraOcclusionFader>();
    }

    void CaptureDefaults()
    {
        if (_captured || occlusionFader == null) return;
        _defaultOccluders = occlusionFader.explicitOccluders ?? Array.Empty<Renderer>();
        _defaultPrefixes = occlusionFader.wallPrefixes ?? Array.Empty<string>();
        _captured = true;
    }

#if UNITY_EDITOR
    public void Configure(Renderer[] occluders, string[] prefixes)
    {
        interiorOccluders = occluders ?? Array.Empty<Renderer>();
        interiorWallPrefixes = prefixes ?? Array.Empty<string>();
    }
#endif
}
