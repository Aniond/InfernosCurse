using UnityEngine;
using Unity.Cinemachine;

// When the player enters this trigger, it raises the assigned Cinemachine
// camera's priority so the Brain blends to it. On exit, it drops back.
// Use for edge framing (e.g. tilt down to reveal a river) without affecting
// the main follow camera elsewhere.
[RequireComponent(typeof(BoxCollider))]
public class CameraZoneTrigger : MonoBehaviour
{
    [Header("Camera")]
    [Tooltip("The Cinemachine camera to activate while the player is in this zone.")]
    public CinemachineCamera zoneCamera;

    [Tooltip("Priority to give the zone camera while active (must beat the main cam).")]
    public int activePriority = 20;
    [Tooltip("Priority when inactive (below the main cam).")]
    public int inactivePriority = 0;

    [Tooltip("Player tag.")]
    public string playerTag = "Player";

    void Awake()
    {
        GetComponent<BoxCollider>().isTrigger = true;
        if (zoneCamera != null) zoneCamera.Priority = inactivePriority;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag) || zoneCamera == null) return;
        zoneCamera.Priority = activePriority;
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag) || zoneCamera == null) return;
        zoneCamera.Priority = inactivePriority;
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        var box = GetComponent<BoxCollider>();
        if (box == null) return;
        Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.2f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(box.center, box.size);
        Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.8f);
        Gizmos.DrawWireCube(box.center, box.size);
    }
#endif
}
