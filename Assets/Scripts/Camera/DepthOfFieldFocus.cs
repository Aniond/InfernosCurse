using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Updates the URP Depth of Field focus distance each frame to track
/// the player's world-space Z position. Attach to the Global Volume GameObject.
/// </summary>
public class DepthOfFieldFocus : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The player transform to track focus on")]
    [SerializeField] private Transform player;

    [Header("DOF Settings")]
    [Tooltip("Extra offset applied to the focus distance (tweak per scene)")]
    [SerializeField] private float focusOffset = 0f;

    [Tooltip("How fast focus distance interpolates to the target (lower = smoother)")]
    [SerializeField, Range(1f, 20f)] private float focusSpeed = 8f;

    private Volume _volume;
    private DepthOfField _dof;
    private Transform _cam;

    private void Awake()
    {
        _volume = GetComponent<Volume>();
        if (_volume == null)
        {
            Debug.LogError("DepthOfFieldFocus: No Volume component found on this GameObject.");
            return;
        }
        _volume.profile.TryGet(out _dof);
        CacheCamera();
    }

    void CacheCamera()
    {
        var c = Camera.main;
        _cam = c != null ? c.transform : null;
    }

    private void LateUpdate()
    {
        if (_dof == null || player == null) return;
        if (_cam == null) CacheCamera();

        // Calculate distance from camera to player's Z plane
        float cam = _cam != null ? _cam.position.z : 0f;
        float targetDist = Mathf.Abs(player.position.z - cam) + focusOffset;

        // Smooth interpolation so focus doesn't snap
        float current = _dof.focusDistance.value;
        _dof.focusDistance.Override(Mathf.Lerp(current, targetDist, Time.deltaTime * focusSpeed));
    }
}
