using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Animator))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 3f;
    [SerializeField] private float runSpeed = 6f;

    [Header("Ground (terrain zones)")]
    [Tooltip("Ground steeper than this doesn't lift the player — terrace banks " +
             "stay walls, ramps stay walkable.")]
    [SerializeField] private float maxWalkableSlope = 40f;
    [Tooltip("How fast the player's height tracks the ground (m/s).")]
    [SerializeField] private float groundSnapSpeed = 8f;

    private Rigidbody _rb;
    private Animator _anim;
    private Transform _cam;
    private Vector2 _moveInput;
    private Vector2 _snapped;
    private bool _isRunning;
    private float _footOffset;

    private static readonly int MoveX = Animator.StringToHash("MoveX");
    private static readonly int MoveY = Animator.StringToHash("MoveY");
    private static readonly int Speed  = Animator.StringToHash("Speed");

    // Kneel ceremony (shrine pledge): movement locks and a directional kneel
    // state plays until the timer runs out, then control returns.
    private float _kneelUntil;
    private int _stateBeforeKneel;

    private void Awake()
    {
        _rb  = GetComponent<Rigidbody>();
        _anim = GetComponent<Animator>();

        _rb.freezeRotation = true;
        _rb.useGravity = false;
        // Y is NOT frozen anymore (it was, when every zone was a flat floor) —
        // terrain zones (Giardino delle Rose) need the player to climb ramps.
        // Height is position-controlled by SnapToGround instead of physics,
        // so flat scenes behave exactly as before.
        _rb.constraints = RigidbodyConstraints.FreezeRotation;

        // Feet-to-pivot distance, so ground snapping keeps the sprite's feet
        // on the ground rather than burying the pivot.
        var col = GetComponent<Collider>();
        _footOffset = col != null ? transform.position.y - col.bounds.min.y : 1.1f;
        if (_footOffset < 0.01f) _footOffset = 1.1f;
    }

    public void OnMove(InputValue value)
    {
        _moveInput = value.Get<Vector2>();
    }

#if UNITY_EDITOR
    // Editor-only hook so automated playtests can drive movement through the
    // full controller path (PlayerInput events can't be synthesized from
    // editor tooling). Not compiled into builds.
    public void DebugSetMove(Vector2 input) => _moveInput = input;
#endif

    public void OnRun(InputValue value)
    {
        _isRunning = value.isPressed;
    }

    private void FixedUpdate()
    {
        if (Time.time < _kneelUntil)
        {
            _rb.linearVelocity = Vector3.zero;
            _anim.SetFloat(Speed, 0f);
            return;
        }
        if (_stateBeforeKneel != 0)
        {
            // kneel just ended — ease back into the locomotion tree
            _anim.CrossFade(_stateBeforeKneel, 0.15f);
            _stateBeforeKneel = 0;
        }

        // Snap raw input to 4 cardinal directions
        _snapped = SnapToCardinal(_moveInput);

        float speed = _snapped.sqrMagnitude > 0.01f
            ? (_isRunning ? runSpeed : walkSpeed)
            : 0f;

        // Camera-relative movement: screen-up walks away from the camera whatever
        // the rig's bearing (PV/Mercato look north so nothing changes there; the
        // Duomo rig looks south and world-fixed axes felt inverted). Animator
        // still gets the raw screen-space input, so sprite facing matches screen.
        if (_cam == null) _cam = Camera.main ? Camera.main.transform : null;
        Vector3 fwd = _cam ? Vector3.ProjectOnPlane(_cam.forward, Vector3.up).normalized : Vector3.forward;
        Vector3 right = _cam ? Vector3.ProjectOnPlane(_cam.right, Vector3.up).normalized : Vector3.right;
        Vector3 move = (right * _snapped.x + fwd * _snapped.y) * speed;
        _rb.linearVelocity = new Vector3(move.x, 0f, move.z); // Y is snap-controlled

        SnapToGround();

        // Update animator only when moving so idle holds last facing direction
        if (_snapped.sqrMagnitude > 0.01f)
        {
            _anim.SetFloat(MoveX, _snapped.x);
            _anim.SetFloat(MoveY, _snapped.y);
        }
        _anim.SetFloat(Speed, speed);
    }

    // Keeps the player's feet on the walkable ground beneath them. Ground
    // steeper than maxWalkableSlope never lifts the player, so terrace banks
    // still act as walls (the collider stops horizontal motion) while ramp
    // corridors (~20-25 degrees) walk normally. On flat floors the snap
    // target equals the current height and this is a no-op.
    //
    // A surface only counts as ground if it sits within maxStepUp of the
    // feet — otherwise any prop collider under the player (bench top,
    // rose-bush box) reads as "floor" and the snap hoists the player onto it.
    private const float MaxStepUp = 0.45f;

    private void SnapToGround()
    {
        Vector3 origin = _rb.position + Vector3.up * 2f;
        var hits = Physics.RaycastAll(origin, Vector3.down, 12f, ~0, QueryTriggerInteraction.Ignore);
        float feetY = _rb.position.y - _footOffset;

        // Highest surface that is still at most a step above the feet.
        bool found = false;
        RaycastHit best = default;
        foreach (var h in hits)
        {
            if (h.collider.transform.root == transform.root) continue;
            if (h.point.y > feetY + MaxStepUp) continue;
            if (!found || h.point.y > best.point.y) { best = h; found = true; }
        }
        if (!found) return;

        float targetY = best.point.y + _footOffset;
        bool tooSteep = Vector3.Angle(best.normal, Vector3.up) > maxWalkableSlope;
        if (targetY > _rb.position.y && tooSteep) return; // never climb walls

        float newY = Mathf.MoveTowards(_rb.position.y, targetY, groundSnapSpeed * Time.fixedDeltaTime);
        _rb.position = new Vector3(_rb.position.x, newY, _rb.position.z);
    }

    // Set the idle facing without moving — used when spawning at an entry point.
    public void SetFacing(Vector2 dir)
    {
        var snapped = SnapToCardinal(dir);
        if (snapped.sqrMagnitude < 0.01f) return;
        // _anim is guaranteed by [RequireComponent(typeof(Animator))].
        _anim.SetFloat(MoveX, snapped.x);
        _anim.SetFloat(MoveY, snapped.y);
    }

    // Kneel facing a world position (the shrine) for a few seconds. The sprite
    // set has north (back to camera) and south (facing camera) kneels; pick by
    // the camera-relative direction toward the target, and hold that facing.
    public void KneelToward(Vector3 worldPos, float seconds = 3f)
    {
        if (_cam == null) _cam = Camera.main ? Camera.main.transform : null;
        Vector3 to = worldPos - transform.position;
        float screenY = _cam ? Vector3.Dot(to, Vector3.ProjectOnPlane(_cam.forward, Vector3.up).normalized) : to.z;
        bool north = screenY >= 0f; // target is up-screen -> kneel showing his back

        SetFacing(new Vector2(0f, north ? 1f : -1f));
        _stateBeforeKneel = _anim.GetCurrentAnimatorStateInfo(0).fullPathHash;
        _kneelUntil = Time.time + seconds;
        _anim.CrossFade(north ? "Kneeling_north" : "Kneeling_south", 0.1f);
    }

    // Returns one of: (1,0) (−1,0) (0,1) (0,−1) or (0,0)
    private static Vector2 SnapToCardinal(Vector2 input)
    {
        if (input.sqrMagnitude < 0.01f) return Vector2.zero;

        if (Mathf.Abs(input.x) >= Mathf.Abs(input.y))
            return new Vector2(Mathf.Sign(input.x), 0f);
        else
            return new Vector2(0f, Mathf.Sign(input.y));
    }
}
