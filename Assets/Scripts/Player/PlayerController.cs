using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Animator))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 3f;
    [SerializeField] private float runSpeed = 6f;

    private Rigidbody _rb;
    private Animator _anim;
    private Vector2 _moveInput;
    private Vector2 _snapped;
    private bool _isRunning;

    private static readonly int MoveX = Animator.StringToHash("MoveX");
    private static readonly int MoveY = Animator.StringToHash("MoveY");
    private static readonly int Speed  = Animator.StringToHash("Speed");

    private void Awake()
    {
        _rb  = GetComponent<Rigidbody>();
        _anim = GetComponent<Animator>();

        _rb.freezeRotation = true;
        _rb.useGravity = false;
        _rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
    }

    public void OnMove(InputValue value)
    {
        _moveInput = value.Get<Vector2>();
    }

    public void OnRun(InputValue value)
    {
        _isRunning = value.isPressed;
    }

    private void FixedUpdate()
    {
        // Snap raw input to 4 cardinal directions
        _snapped = SnapToCardinal(_moveInput);

        float speed = _snapped.sqrMagnitude > 0.01f
            ? (_isRunning ? runSpeed : walkSpeed)
            : 0f;

        Vector3 move = new Vector3(_snapped.x, 0f, _snapped.y) * speed;
        _rb.linearVelocity = new Vector3(move.x, _rb.linearVelocity.y, move.z);

        // Update animator only when moving so idle holds last facing direction
        if (_snapped.sqrMagnitude > 0.01f)
        {
            _anim.SetFloat(MoveX, _snapped.x);
            _anim.SetFloat(MoveY, _snapped.y);
        }
        _anim.SetFloat(Speed, speed);
    }

    // Set the idle facing without moving — used when spawning at an entry point.
    public void SetFacing(Vector2 dir)
    {
        var snapped = SnapToCardinal(dir);
        if (snapped.sqrMagnitude < 0.01f) return;
        if (_anim == null) _anim = GetComponent<Animator>();
        _anim.SetFloat(MoveX, snapped.x);
        _anim.SetFloat(MoveY, snapped.y);
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
