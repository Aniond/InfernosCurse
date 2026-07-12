using UnityEngine;

/// <summary>
/// Detects a real threshold crossing. Merely entering the trigger does not
/// change sub-location state; the player must leave on the opposite side.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public sealed class SeamlessInteriorPortal : MonoBehaviour
{
    [SerializeField] SeamlessInteriorModule module;
    [SerializeField] string playerTag = "Player";
    [Tooltip("Local-Z side that represents the exterior: -1 or +1.")]
    [SerializeField] int exteriorSide = -1;

    Transform _trackedPlayer;
    int _entrySide;
    bool _battleLocked;
    BoxCollider _trigger;

    public bool BattleLocked => _battleLocked;

    void Awake()
    {
        _trigger = GetComponent<BoxCollider>();
        _trigger.isTrigger = true;
        exteriorSide = exteriorSide >= 0 ? 1 : -1;
    }

    public void Bind(SeamlessInteriorModule owner) => module = owner;

    public void SetBattleLocked(bool locked)
    {
        _battleLocked = locked;
        if (locked)
        {
            _trackedPlayer = null;
            _entrySide = 0;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (_battleLocked || !other.CompareTag(playerTag)) return;
        if (module == null)
        {
            Debug.LogError("[SeamlessInteriorPortal] No module is bound to the portal.", this);
            return;
        }
        if (!module.TryValidateRuntime(out string error))
        {
            Debug.LogError($"[SeamlessInteriorPortal] Entry blocked: {error}.", module);
            return;
        }

        _trackedPlayer = other.transform.root;
        _entrySide = SideOf(_trackedPlayer.position);
    }

    void OnTriggerExit(Collider other)
    {
        if (_trackedPlayer == null || other.transform.root != _trackedPlayer) return;
        int exitSide = SideOf(_trackedPlayer.position);
        if (!_battleLocked && exitSide != _entrySide)
            module.SetPlayerInside(exitSide != exteriorSide);

        _trackedPlayer = null;
        _entrySide = 0;
    }

    void OnTriggerStay(Collider other)
    {
        if (_battleLocked || _trackedPlayer != null || !other.CompareTag(playerTag)) return;
        _trackedPlayer = other.transform.root;
        _entrySide = SideOf(_trackedPlayer.position);
    }

    int SideOf(Vector3 worldPosition)
    {
        float localZ = transform.InverseTransformPoint(worldPosition).z;
        if (Mathf.Abs(localZ) < 0.001f)
            return exteriorSide;
        return localZ >= 0f ? 1 : -1;
    }

#if UNITY_EDITOR
    public void Configure(SeamlessInteriorModule owner, int newExteriorSide = -1)
    {
        module = owner;
        exteriorSide = newExteriorSide >= 0 ? 1 : -1;
        var box = GetComponent<BoxCollider>();
        box.isTrigger = true;
    }

    void OnDrawGizmos()
    {
        var box = GetComponent<BoxCollider>();
        if (box == null) return;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.22f);
        Gizmos.DrawCube(box.center, box.size);
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
        Gizmos.DrawWireCube(box.center, box.size);
        Vector3 direction = Vector3.forward * (exteriorSide >= 0 ? 1f : -1f);
        Gizmos.DrawLine(Vector3.zero, direction * 1.5f);
    }
#endif
}
