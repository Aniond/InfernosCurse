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

    float _targetYaw;
    float _yaw;
    float _yawVel;
    float _pitchDistSet;
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

        if (!Mathf.Approximately(_yaw, _targetYaw))
        {
            _yaw = Mathf.SmoothDampAngle(_yaw, _targetYaw, ref _yawVel, smoothTime);
            if (Mathf.Abs(Mathf.DeltaAngle(_yaw, _targetYaw)) < 0.05f) _yaw = _targetYaw;
            Apply(_yaw);
        }
    }

    public void RotateStep(int dir) => _targetYaw += step * Mathf.Sign(dir);

    void Apply(float yawDeg)
    {
        Quaternion yawRot = Quaternion.Euler(0f, yawDeg, 0f);
        transform.position = pivot + yawRot * new Vector3(0f, _localOffset.y, _localOffset.z);
        transform.LookAt(pivot);
    }
}
