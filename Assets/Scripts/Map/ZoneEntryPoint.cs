using UnityEngine;

// Marks a spawn location in an explorable zone. When the player travels INTO
// this zone targeting this entryId, ZoneEntryPlacer drops them here.
public class ZoneEntryPoint : MonoBehaviour
{
    [Tooltip("Unique id within the project. Exits in other zones target this.")]
    public string entryId = "";

    [Tooltip("Human-readable name shown in the fast-travel menu (e.g. 'The Fountain'). " +
             "Falls back to entryId when empty.")]
    public string displayName = "";

    [Tooltip("If false, this entry is a cross-scene arrival point only and won't " +
             "appear as a fast-travel destination within the zone.")]
    public bool fastTravelDestination = true;

    [Tooltip("Facing direction the player should adopt when spawning here.")]
    public Vector2 faceDirection = Vector2.up;  // world XZ facing (default: into the zone)

    // Label for menus: displayName if set, otherwise the raw id.
    public string Label => string.IsNullOrEmpty(displayName) ? entryId : displayName;

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(transform.position, 0.3f);
        Gizmos.DrawLine(transform.position,
            transform.position + new Vector3(faceDirection.x, 0f, faceDirection.y) * 1.5f);
    }
#endif
}
