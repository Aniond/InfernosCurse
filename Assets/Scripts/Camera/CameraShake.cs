using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Singleton camera shake utility using Cinemachine Impulse.
/// Call CameraShake.Instance.ShakeCamera(intensity, duration) from any script.
/// Attach to the [CameraRig] root GameObject alongside a CinemachineImpulseSource.
/// </summary>
[RequireComponent(typeof(CinemachineImpulseSource))]
public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }

    [Header("Impulse Settings")]
    [Tooltip("Default shake intensity if none is specified")]
    [SerializeField, Range(0f, 5f)] private float defaultIntensity = 1f;

    [Tooltip("Default shake duration if none is specified")]
    [SerializeField, Range(0f, 2f)] private float defaultDuration = 0.2f;

    private CinemachineImpulseSource _impulseSource;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        _impulseSource = GetComponent<CinemachineImpulseSource>();
    }

    /// <summary>Triggers a camera shake with custom intensity and duration.</summary>
    public void ShakeCamera(float intensity, float duration)
    {
        if (_impulseSource == null) return;
        _impulseSource.ImpulseDefinition.ImpulseDuration = duration;
        _impulseSource.GenerateImpulse(intensity);
    }

    /// <summary>Triggers a camera shake using the default values set in the Inspector.</summary>
    public void ShakeCamera() => ShakeCamera(defaultIntensity, defaultDuration);
}
