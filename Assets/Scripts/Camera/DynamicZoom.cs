using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

// HD-2D dynamic zoom (Octopath-style). Zooms the follow camera IN when the
// player is hemmed in by architecture (corridors, building faces) and OUT in
// open space. v2: zoom is driven by measured CLEARANCE — horizontal rays from
// the player to the nearest tall collider — so the same rig works in the open
// plaza (Mercato), narrow corridors (Ponte Vecchio), and future street scenes
// without per-scene center/radius tuning. The legacy center-distance mode is
// kept for scenes that want an authored open-center instead.
[RequireComponent(typeof(CinemachineCamera))]
public class DynamicZoom : MonoBehaviour
{
    [Serializable]
    public sealed class CameraOverrideProfile
    {
        [Tooltip("Camera follow offset while this profile owns the rig.")]
        public Vector3 followOffset = new Vector3(0f, 8.5f, -11.5f);
        [Tooltip("Small world-space composition shift added to the follow offset.")]
        public Vector3 panOffset = Vector3.zero;
        [Min(0f)] public float blendInDuration = 0.65f;
        [Min(0f)] public float blendOutDuration = 0.75f;
        public int priority = 10;
        [Tooltip("Optional world-space bounds used to keep the composed target inside a room.")]
        public bool clampToRoomBounds;
        public Bounds roomBounds = new Bounds(Vector3.zero, new Vector3(10f, 6f, 14f));

        public CameraOverrideProfile Copy()
        {
            return new CameraOverrideProfile
            {
                followOffset = followOffset,
                panOffset = panOffset,
                blendInDuration = blendInDuration,
                blendOutDuration = blendOutDuration,
                priority = priority,
                clampToRoomBounds = clampToRoomBounds,
                roomBounds = roomBounds,
            };
        }
    }

    sealed class OverrideEntry
    {
        public CameraOverrideProfile profile;
        public long order;
    }

    [Header("Target")]
    [Tooltip("Player to track. Auto-finds by tag if empty.")]
    public Transform target;
    public string playerTag = "Player";

    [Header("Clearance zoom (corridors / streets)")]
    [Tooltip("Drive zoom by distance to nearest tall collider around the player — " +
             "right for corridors and street scenes (Ponte Vecchio). OFF (default) = " +
             "legacy zone-center mode, which is the tuned behavior for open plazas " +
             "(Mercato Vecchio). Pick per scene.")]
    public bool useClearanceZoom = false;
    [Tooltip("Clearance at or below this = fully zoomed IN (close).")]
    public float closeClearance = 3f;
    [Tooltip("Clearance at or above this = fully zoomed OUT (wide).")]
    public float wideClearance = 9f;
    [Tooltip("Colliders shorter than this are props, not architecture — ignored. " +
             "Default 6.5 counts only real buildings (market stalls/fountains " +
             "don't pump the zoom). Corridor blockouts whose dividers are low " +
             "(e.g. Ponte Vecchio, 3.0-high dividers) should lower this to ~2.5 " +
             "per scene.")]
    public float minArchitectureHeight = 6.5f;
    [Tooltip("Height above the player's feet the clearance rays are cast at.")]
    public float rayHeight = 1.2f;

    [Header("Legacy zone-center mode (useClearanceZoom = false)")]
    [Tooltip("The open-square center. Far from here = near a building = zoom in.")]
    public Vector2 zoneCenterXZ = Vector2.zero;
    [Tooltip("Distance from center at which the camera is fully zoomed OUT (wide).")]
    public float outerRadius = 4f;
    [Tooltip("Distance from center at which the camera is fully zoomed IN (close).")]
    public float innerRadius = 13f;

    [Header("Camera follow offsets (blended by proximity)")]
    [Tooltip("Offset when in the open — wide, pulled back.")]
    public Vector3 wideOffset  = new Vector3(0f, 14f, -20f);
    [Tooltip("Offset when hemmed in — zoomed in.")]
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
    private Camera _mainCam;      // cached — avoids Camera.main every LateUpdate
    private float _t;             // 0 = wide, 1 = close
    private float _lastClearance; // for gizmos
    private readonly Dictionary<UnityEngine.Object, OverrideEntry> _overrides = new();
    private Vector3 _appliedOffset;
    private bool _offsetInitialized;
    private long _overrideOrder;
    private float _lastBlendOutDuration = 0.75f;

    public int ActiveOverrideCount => _overrides.Count;
    public bool HasActiveOverride => ResolveOverride() != null;
    public Vector3 AppliedOffset => _follow != null ? _follow.FollowOffset : _appliedOffset;

    // 8 compass directions, built once.
    private static readonly Vector3[] RayDirs =
    {
        new Vector3( 1, 0,  0), new Vector3(-1, 0,  0),
        new Vector3( 0, 0,  1), new Vector3( 0, 0, -1),
        new Vector3( 0.7071f, 0,  0.7071f), new Vector3(-0.7071f, 0,  0.7071f),
        new Vector3( 0.7071f, 0, -0.7071f), new Vector3(-0.7071f, 0, -0.7071f),
    };

    void Awake()
    {
        _cam = GetComponent<CinemachineCamera>();
        _follow = GetComponent<CinemachineFollow>();
        if (_follow == null)
        {
            // Nothing authored a Body — add one so the offset can be driven.
            Debug.LogWarning("[DynamicZoom] No CinemachineFollow found — adding one at runtime. " +
                             "Author it in the scene to avoid conflicts with other body components.");
            _follow = gameObject.AddComponent<CinemachineFollow>();
        }
        _mainCam = Camera.main;
        if (_follow != null)
        {
            _appliedOffset = _follow.FollowOffset;
            _offsetInitialized = true;
        }
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

        float targetT = useClearanceZoom ? ClearanceT() : CenterDistanceT();

        // Ease toward it
        _t = Mathf.Lerp(_t, targetT, Time.deltaTime * zoomLerpSpeed);

        // Blended offset
        Vector3 currentOffset = Vector3.Lerp(wideOffset, closeOffset, _t);

        // Screen-position clamp: if the player is drifting too close to the
        // bottom edge (e.g. walking south toward the camera), push the camera
        // back and up so the sprite stays fully visible.
        if (_mainCam == null) _mainCam = Camera.main;   // re-resolve after scene load
        if (_mainCam != null)
        {
            Vector3 vp = _mainCam.WorldToViewportPoint(target.position);
            if (vp.z > 0f && vp.y < bottomSafeMargin)
            {
                float deficit = (bottomSafeMargin - vp.y) / bottomSafeMargin; // 0..1
                currentOffset.z -= edgePushBack * deficit;
                currentOffset.y += edgePushUp  * deficit;
            }
        }

        CameraOverrideProfile activeOverride = ResolveOverride();
        Vector3 desiredOffset = activeOverride != null
            ? ResolveOverrideOffset(activeOverride)
            : currentOffset;
        float blendDuration = activeOverride != null
            ? activeOverride.blendInDuration
            : _lastBlendOutDuration;

        if (!_offsetInitialized)
        {
            _appliedOffset = _follow.FollowOffset;
            _offsetInitialized = true;
        }

        float blend = blendDuration <= 0f
            ? 1f
            : 1f - Mathf.Exp(-4.60517f * Time.deltaTime / blendDuration);
        _appliedOffset = Vector3.Lerp(_appliedOffset, desiredOffset, blend);
        _follow.FollowOffset = _appliedOffset;
    }

    public void PushOverride(UnityEngine.Object owner, CameraOverrideProfile profile)
    {
        if (owner == null || profile == null) return;
        if (_overrides.TryGetValue(owner, out OverrideEntry existing))
        {
            existing.profile = profile.Copy();
            return;
        }

        _overrides.Add(owner, new OverrideEntry
        {
            profile = profile.Copy(),
            order = ++_overrideOrder,
        });
    }

    public void RemoveOverride(UnityEngine.Object owner)
    {
        if (owner == null || !_overrides.TryGetValue(owner, out OverrideEntry removed)) return;
        _lastBlendOutDuration = removed.profile.blendOutDuration;
        _overrides.Remove(owner);
    }

    CameraOverrideProfile ResolveOverride()
    {
        CameraOverrideProfile best = null;
        long bestOrder = long.MinValue;
        foreach (OverrideEntry entry in _overrides.Values)
        {
            if (entry?.profile == null) continue;
            if (best == null || entry.profile.priority > best.priority ||
                (entry.profile.priority == best.priority && entry.order > bestOrder))
            {
                best = entry.profile;
                bestOrder = entry.order;
            }
        }
        return best;
    }

    Vector3 ResolveOverrideOffset(CameraOverrideProfile profile)
    {
        Vector3 pan = profile.panOffset;
        if (profile.clampToRoomBounds && target != null)
        {
            Vector3 composedTarget = target.position + pan;
            Vector3 min = profile.roomBounds.min;
            Vector3 max = profile.roomBounds.max;
            Vector3 clamped = new Vector3(
                Mathf.Clamp(composedTarget.x, min.x, max.x),
                Mathf.Clamp(composedTarget.y, min.y, max.y),
                Mathf.Clamp(composedTarget.z, min.z, max.z));
            pan = clamped - target.position;
        }
        return profile.followOffset + pan;
    }

    // ── Zoom drivers ──────────────────────────────────────────────────────────

    // Nearest tall-collider distance around the player → 0 (open) .. 1 (hemmed in).
    float ClearanceT()
    {
        Vector3 origin = target.position + Vector3.up * rayHeight;
        float clearance = wideClearance;

        for (int i = 0; i < RayDirs.Length; i++)
        {
            var hits = Physics.RaycastAll(origin, RayDirs[i], wideClearance,
                                          Physics.DefaultRaycastLayers,
                                          QueryTriggerInteraction.Ignore);
            for (int h = 0; h < hits.Length; h++)
            {
                var col = hits[h].collider;
                if (col.transform.root == target.root) continue;              // self
                if (col.bounds.size.y < minArchitectureHeight) continue;      // prop
                if (hits[h].distance < clearance) clearance = hits[h].distance;
            }
        }

        _lastClearance = clearance;
        return Mathf.InverseLerp(wideClearance, closeClearance, clearance);
    }

    // Legacy: distance from an authored open-center.
    float CenterDistanceT()
    {
        Vector2 pos = new Vector2(target.position.x, target.position.z);
        float dist = Vector2.Distance(pos, zoneCenterXZ);
        return Mathf.InverseLerp(outerRadius, innerRadius, dist);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (useClearanceZoom)
        {
            if (target == null) return;
            Vector3 origin = target.position + Vector3.up * rayHeight;
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.6f);
            foreach (var d in RayDirs) Gizmos.DrawRay(origin, d * _lastClearance);
            return;
        }
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
