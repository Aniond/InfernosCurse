using UnityEngine;

// FFT-style battle camera: orbits the map center in discrete steps so the
// player can view the field from any side (Q/E or [ ]). Pitch and distance
// come from the authored camera position; only yaw changes. Unit billboards,
// conforming highlights, the flat cursor and damage numbers are all
// camera-rotation-safe already.
public class BattleCameraRig : MonoBehaviour
{
    [Tooltip("World point the camera orbits (map center; set by arena wiring).")]
    public Vector3 pivot = new Vector3(7f, 0.5f, 6f);
    [Tooltip("Degrees per rotation step (FFT uses 90).")]
    public float step = 90f;
    [Tooltip("Seconds to settle into the new angle.")]
    public float smoothTime = 0.35f;

    public KeyCode rotateLeftKey = KeyCode.Q;
    public KeyCode rotateRightKey = KeyCode.E;
    public KeyCode rotateLeftAlt = KeyCode.LeftBracket;
    public KeyCode rotateRightAlt = KeyCode.RightBracket;

    [Header("Zoom (mouse wheel)")]
    public float zoomMin = 0.45f;
    public float zoomMax = 1.5f;
    public float zoomStep = 0.12f;
    public float zoomSmooth = 9f;

    float _targetYaw;
    float _yaw;
    float _yawVel;
    float _zoom = 1f;
    float _zoomTarget = 1f;
    Vector3 _localOffset;   // camera offset from pivot at yaw 0, in pivot space

    void Start()
    {
        // derive current orbit params from the authored transform
        Vector3 off = transform.position - pivot;
        _yaw = _targetYaw = Mathf.Atan2(off.x, off.z) * Mathf.Rad2Deg;
        float horiz = new Vector2(off.x, off.z).magnitude;
        _localOffset = new Vector3(0f, off.y, horiz);
        Apply(_yaw);
    }

    void Update()
    {
        if (Input.GetKeyDown(rotateLeftKey) || Input.GetKeyDown(rotateLeftAlt)) _targetYaw -= step;
        if (Input.GetKeyDown(rotateRightKey) || Input.GetKeyDown(rotateRightAlt)) _targetYaw += step;

        // mouse-wheel zoom (scales orbit distance, keeps pitch)
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null)
        {
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
                _zoomTarget = Mathf.Clamp(_zoomTarget - Mathf.Sign(scroll) * zoomStep, zoomMin, zoomMax);
        }

        bool zooming = !Mathf.Approximately(_zoom, _zoomTarget);
        bool rotating = !Mathf.Approximately(_yaw, _targetYaw);
        if (rotating)
        {
            _yaw = Mathf.SmoothDampAngle(_yaw, _targetYaw, ref _yawVel, smoothTime);
            if (Mathf.Abs(Mathf.DeltaAngle(_yaw, _targetYaw)) < 0.05f) _yaw = _targetYaw;
        }
        if (zooming)
        {
            _zoom = Mathf.Lerp(_zoom, _zoomTarget, Time.deltaTime * zoomSmooth);
            if (Mathf.Abs(_zoom - _zoomTarget) < 0.005f) _zoom = _zoomTarget;
        }
        if (rotating || zooming) Apply(_yaw);
    }

    public void RotateStep(int dir) => _targetYaw += step * Mathf.Sign(dir);

    void Apply(float yawDeg)
    {
        Quaternion yawRot = Quaternion.Euler(0f, yawDeg, 0f);
        transform.position = pivot + yawRot * (new Vector3(0f, _localOffset.y, _localOffset.z) * _zoom);
        transform.LookAt(pivot);
    }
}
