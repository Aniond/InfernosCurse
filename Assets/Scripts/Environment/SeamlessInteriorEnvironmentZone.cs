using System;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Local presentation switch for an embedded interior. It intentionally does
/// not modify RenderSettings: the exterior zone remains the global authority.
/// </summary>
[DisallowMultipleComponent]
public sealed class SeamlessInteriorEnvironmentZone : MonoBehaviour
{
    [SerializeField] Volume interiorVolume;
    [SerializeField] Light[] localLights = Array.Empty<Light>();
    [SerializeField] AudioSource[] interiorAmbience = Array.Empty<AudioSource>();
    [SerializeField] AudioSource[] exteriorAmbience = Array.Empty<AudioSource>();
    [SerializeField, Range(0f, 1f)] float exteriorVolumeWhileInside = 0.18f;

    float[] _localLightIntensities;
    float[] _interiorVolumes;
    float[] _exteriorVolumes;
    bool _captured;

    void Awake() => CaptureDefaults();

    public void SetPlayerInside(bool inside)
    {
        CaptureDefaults();
        if (interiorVolume != null) interiorVolume.weight = inside ? 1f : 0f;

        for (int i = 0; i < localLights.Length; i++)
            if (localLights[i] != null)
                localLights[i].intensity = inside ? _localLightIntensities[i] : 0f;

        for (int i = 0; i < interiorAmbience.Length; i++)
            if (interiorAmbience[i] != null)
                interiorAmbience[i].volume = inside ? _interiorVolumes[i] : 0f;

        for (int i = 0; i < exteriorAmbience.Length; i++)
            if (exteriorAmbience[i] != null)
                exteriorAmbience[i].volume = _exteriorVolumes[i] * (inside ? exteriorVolumeWhileInside : 1f);
    }

    void CaptureDefaults()
    {
        if (_captured) return;
        _localLightIntensities = new float[localLights?.Length ?? 0];
        _interiorVolumes = new float[interiorAmbience?.Length ?? 0];
        _exteriorVolumes = new float[exteriorAmbience?.Length ?? 0];
        for (int i = 0; i < _localLightIntensities.Length; i++)
            if (localLights[i] != null) _localLightIntensities[i] = localLights[i].intensity;
        for (int i = 0; i < _interiorVolumes.Length; i++)
            if (interiorAmbience[i] != null) _interiorVolumes[i] = interiorAmbience[i].volume;
        for (int i = 0; i < _exteriorVolumes.Length; i++)
            if (exteriorAmbience[i] != null) _exteriorVolumes[i] = exteriorAmbience[i].volume;
        _captured = true;
    }

#if UNITY_EDITOR
    public void Configure(Volume volume, Light[] lights, AudioSource[] insideAudio, AudioSource[] outsideAudio)
    {
        interiorVolume = volume;
        localLights = lights ?? Array.Empty<Light>();
        interiorAmbience = insideAudio ?? Array.Empty<AudioSource>();
        exteriorAmbience = outsideAudio ?? Array.Empty<AudioSource>();
        _captured = false;
    }
#endif
}
