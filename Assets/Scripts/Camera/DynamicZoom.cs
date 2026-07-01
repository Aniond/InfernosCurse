using UnityEngine;
using Unity.Cinemachine;

// HD-2D dynamic zoom (Octopath-style). Zooms the follow camera IN as the player
// nears the edges/buildings of a zone, and OUT when in the open center.
// Drive it off distance-from-center: center = wide, edges = close.
[RequireComponent(typeof(CinemachineCamera))]
public class DynamicZoom : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Player to track. Auto-finds by tag if empty.")]
    public Transform target;
    public string playerTag = "Player";

    [Header("Zone center (open area) in world space")]
    [Tooltip("The open-square center. Far from here = near a building = zoom in.")]
    public Vector2 zoneCenterXZ = Vector2.zero;
    [Tooltip("Distance from center at which the camera is fully zoomed OUT (wide).")]
    public float outerRadius = 4f;
    [Tooltip("Distance from center at which the camera is fully zoomed IN (close).")]
    public float innerRadius = 13f;

    [Header("Camera follow offsets (blended by proximity)")]
    [Tooltip("Offset when in the open center — wide, pulled back.")]
    public Vector3 wideOffset  = new Vector3(0f, 14f, -20f);
    [Tooltip("Offset when close to a building/edge — zoomed in.")]
    public Vector3 closeOffset = new Vector3(0f, 7f, -10f);

    [Header("Smoothing")]
    [Tooltip("How fast the zoom eases (higher = snappier).")]
    public float zoomLerpSpeed = 3f;

    [Header("Bottom-edge safety clamp")]
    [Tooltip("If the player's viewport Y drops below this, the camera pushes " +
             "back/up so the sprite stays fully visible (prevents south cutoff).")]
    [Range(0f, 0.5f)] public float bottomSafeMargin = 0.25f;
    [Tooltip("How much to pull the camera back (Z) at max deficit.")]
    public float edgePushBack = 1.0f;
    [Tooltip("How much to raise the camera (Y) at max deficit.")]
    public float edgePushUp = 0.5f;

    private CinemachineCamera _cam;
    private CinemachineFollow _follow;
    private float _t;   // 0 = wide, 1 = close

    void Awake()
    {
        _cam = GetComponent<CinemachineCamera>();
        _follow = GetComponent<CinemachineFollow>();
        if (_follow == null) _follow = gameObject.AddComponent<CinemachineFollow>();
    }

    void OnEnable()
    {
        if (target == null)
        {
            var p = GameObject.FindGameObjectWithTag(playerTag);
            if (p != null) target = p.transform;
        }
        if (_cam != null && target != null) _cam.Follow = target;
    }

    void LateUpdate()
    {
        if (target == null || _follow == null) return;

        // How far is the player from the open center?
        Vector2 pos = new Vector2(target.position.x, target.position.z);
        float dist = Vector2.Distance(pos, zoneCenterXZ);

        // Map distance -> zoom amount. Near center (<outerRadius) = wide (0),
        // near edge (>innerRadius) = close (1).
        float targetT = Mathf.InverseLerp(outerRadius, innerRadius, dist);

        // Ease toward it
        _t = Mathf.Lerp(_t, targetT, Time.deltaTime * zoomLerpSpeed);

        // Blended offset
        Vector3 currentOffset = Vector3.Lerp(wideOffset, closeOffset, _t);

        // Screen-position clamp: if the player is drifting too close to the
        // bottom edge (e.g. walking south toward the camera), push the camera
        // back and up so the sprite stays fully visible.
        var cam = Camera.main;
        if (cam != null)
        {
            Vector3 vp = cam.WorldToViewportPoint(target.position);
            if (vp.z > 0f && vp.y < bottomSafeMargin)
            {
                float deficit = (bottomSafeMargin - vp.y) / bottomSafeMargin; // 0..1
                currentOffset.z -= edgePushBack * deficit;
                currentOffset.y += edgePushUp  * deficit;
            }
        }

        _follow.FollowOffset = currentOffset;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Vector3 c = new Vector3(zoneCenterXZ.x, 0.1f, zoneCenterXZ.y);
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.6f);
        DrawCircle(c, outerRadius);
        Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.6f);
        DrawCircle(c, innerRadius);
    }

    static void DrawCircle(Vector3 c, float r)
    {
        int seg = 48;
        Vector3 prev = c + new Vector3(r, 0, 0);
        for (int i = 1; i <= seg; i++)
        {
            float a = i / (float)seg * Mathf.PI * 2f;
            Vector3 next = c + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
#endif
}
