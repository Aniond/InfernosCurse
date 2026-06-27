using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Manages the active virtual camera and exposes camera state.
/// Attach to the [CameraRig] root GameObject.
/// </summary>
public class CameraController : MonoBehaviour
{
    [Header("Virtual Cameras")]
    [Tooltip("The main player-follow virtual camera")]
    [SerializeField] private CinemachineCamera playerCam;

    [Header("Follow Target")]
    [Tooltip("The transform Cinemachine will follow and look at")]
    [SerializeField] private Transform followTarget;

    private CinemachineCamera _activeCam;

    private void Start()
    {
        _activeCam = playerCam;
        ApplyTarget(_activeCam);
    }

    /// <summary>Switches to a different virtual camera (room transitions, cutscenes).</summary>
    public void SwitchTo(CinemachineCamera cam)
    {
        if (cam == null) return;
        _activeCam = cam;
        ApplyTarget(_activeCam);
    }

    /// <summary>Returns the currently active virtual camera.</summary>
    public CinemachineCamera ActiveCamera => _activeCam;

    private void ApplyTarget(CinemachineCamera cam)
    {
        if (cam == null || followTarget == null) return;
        cam.Follow = followTarget;
        cam.LookAt = followTarget;
    }
}
