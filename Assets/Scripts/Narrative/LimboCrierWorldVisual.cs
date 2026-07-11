using UnityEngine;

// Uses the same directional PixelLab package in exploration and battle. The
// actor remains hidden and non-blocking until its event/rumor discovery is
// committed to persistent state.
[DisallowMultipleComponent]
public sealed class LimboCrierWorldVisual : MonoBehaviour
{
    public string agentId;
    public HumanoidBattleVisualProfile profile;
    public SpriteRenderer spriteRenderer;
    public Collider worldCollider;
    [Min(0.0001f)] public float movementThreshold = 0.0025f;

    Vector3 _lastPosition;
    FacingDir _facing = FacingDir.South;
    float _animationTime;

    void OnEnable()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        _lastPosition = transform.position;
        RefreshVisibility();
    }

    void LateUpdate()
    {
        if (!RefreshVisibility())
        {
            _lastPosition = transform.position;
            return;
        }

        Vector3 movement = transform.position - _lastPosition;
        movement.y = 0f;
        bool moving = movement.sqrMagnitude >= movementThreshold * movementThreshold;
        if (moving)
        {
            _facing = FacingFor(movement);
            _animationTime += Time.deltaTime;
            Sprite[] frames = profile != null ? profile.walk.GetFrames(_facing) : null;
            if (frames != null && frames.Length > 0)
            {
                int frame = Mathf.FloorToInt(_animationTime * Mathf.Max(1f, profile.walk.fps)) % frames.Length;
                spriteRenderer.sprite = frames[frame];
            }
        }
        else
        {
            _animationTime = 0f;
            Sprite idle = profile != null ? profile.GetIdleSprite(_facing) : null;
            if (idle != null) spriteRenderer.sprite = idle;
        }
        _lastPosition = transform.position;
    }

    bool RefreshVisibility()
    {
        bool visible = PersistentLimboWorldState.TryGetAgent(agentId, out PersistentWorldAgentRecord record) &&
                       record.discovered && !record.defeated;
        if (spriteRenderer != null) spriteRenderer.enabled = visible;
        if (worldCollider != null) worldCollider.enabled = visible;
        return visible;
    }

    static FacingDir FacingFor(Vector3 movement)
    {
        if (Mathf.Abs(movement.x) >= Mathf.Abs(movement.z))
            return movement.x >= 0f ? FacingDir.East : FacingDir.West;
        return movement.z >= 0f ? FacingDir.North : FacingDir.South;
    }
}
