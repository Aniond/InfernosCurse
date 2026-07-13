using System;
using UnityEngine;

/// <summary>
/// Lightweight bridge between an interior module and the shared exploration
/// camera. Owns both explicit occluder groups and a temporary camera profile
/// without introducing a second Main Camera.
/// </summary>
[DisallowMultipleComponent]
public sealed class SeamlessInteriorCameraZone : MonoBehaviour
{
    [SerializeField] CameraOcclusionFader occlusionFader;
    [SerializeField] Renderer[] interiorOccluders = Array.Empty<Renderer>();
    [SerializeField] Renderer[] interiorForcedOccluders = Array.Empty<Renderer>();
    [SerializeField] string[] interiorWallPrefixes = Array.Empty<string>();
    [SerializeField] DynamicZoom.CameraOverrideProfile interiorProfile = new();

    Renderer[] _defaultOccluders;
    Renderer[] _defaultForcedOccluders;
    string[] _defaultPrefixes;
    DynamicZoom _dynamicZoom;
    bool _captured;
    bool _warnedMissingZoom;

    public void SetPlayerInside(bool inside)
    {
        ResolveCamera();
        if (occlusionFader != null)
        {
            CaptureDefaults();
            occlusionFader.explicitOccluders = inside ? interiorOccluders : _defaultOccluders;
            occlusionFader.forcedOccluders = inside ? interiorForcedOccluders : _defaultForcedOccluders;
            occlusionFader.wallPrefixes = inside ? interiorWallPrefixes : _defaultPrefixes;
        }
        if (_dynamicZoom != null)
        {
            if (inside) _dynamicZoom.PushOverride(this, interiorProfile);
            else _dynamicZoom.RemoveOverride(this);
        }
    }

    void ResolveCamera()
    {
        Camera main = Camera.main;
        if (main != null)
        {
            if (occlusionFader == null) occlusionFader = main.GetComponent<CameraOcclusionFader>();
            if (_dynamicZoom == null) _dynamicZoom = main.GetComponentInChildren<DynamicZoom>(true);
            if (_dynamicZoom == null) _dynamicZoom = FindFirstObjectByType<DynamicZoom>();
        }
        if (_dynamicZoom == null && !_warnedMissingZoom)
        {
            _warnedMissingZoom = true;
            Debug.LogWarning("[SeamlessInteriorCameraZone] Shared DynamicZoom was not found; occlusion will switch but the camera profile will remain unchanged.", this);
        }
    }

    void CaptureDefaults()
    {
        if (_captured || occlusionFader == null) return;
        _defaultOccluders = occlusionFader.explicitOccluders ?? Array.Empty<Renderer>();
        _defaultForcedOccluders = occlusionFader.forcedOccluders ?? Array.Empty<Renderer>();
        _defaultPrefixes = occlusionFader.wallPrefixes ?? Array.Empty<string>();
        _captured = true;
    }

#if UNITY_EDITOR
    public void Configure(Renderer[] occluders, string[] prefixes,
        DynamicZoom.CameraOverrideProfile profile = null, Renderer[] forcedOccluders = null)
    {
        interiorOccluders = occluders ?? Array.Empty<Renderer>();
        interiorForcedOccluders = forcedOccluders ?? Array.Empty<Renderer>();
        interiorWallPrefixes = prefixes ?? Array.Empty<string>();
        if (profile != null) interiorProfile = profile.Copy();
    }
#endif
}
