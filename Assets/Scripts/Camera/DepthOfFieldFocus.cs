using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Updates the URP Depth of Field focus distance each frame so the player is
/// always the sharp plane of the HD-2D tilt-shift look. Attach to the Global
/// Volume GameObject. Self-heals its player reference (finds by tag) so the
/// same component works after scene loads/clones without rewiring.
/// </summary>
public class DepthOfFieldFocus : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The player transform to track focus on. Auto-finds by tag if empty.")]
    [SerializeField] private Transform player;
    [SerializeField] private string playerTag = "Player";

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
        if (_dof == null)
            Debug.LogWarning("DepthOfFieldFocus: profile has no DepthOfField override — focus tracking inactive.");
        CacheCamera();
    }

    void CacheCamera()
    {
        var c = Camera.main;
        _cam = c != null ? c.transform : null;
    }

    void CachePlayer()
    {
        var p = GameObject.FindGameObjectWithTag(playerTag);
        if (p != null) player = p.transform;
    }

    private void LateUpdate()
    {
        if (_dof == null) return;
        if (player == null) { CachePlayer(); if (player == null) return; }
        if (_cam == null) { CacheCamera(); if (_cam == null) return; }

        // True camera→player distance (not just the Z gap — with a 40° pitch the
        // camera sits 11-15 units up, so Z-only underestimates by ~30% and threw
        // the player OUT of the sharp plane).
        float targetDist = Vector3.Distance(_cam.position, player.position) + focusOffset;

        // Smooth interpolation so focus doesn't snap
        float current = _dof.focusDistance.value;
        _dof.focusDistance.Override(Mathf.Lerp(current, targetDist, Time.deltaTime * focusSpeed));
    }
}
