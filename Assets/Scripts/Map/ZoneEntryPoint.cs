using UnityEngine;

// Marks a spawn location in an explorable zone. When the player travels INTO
// this zone targeting this entryId, ZoneEntryPlacer drops them here.
public class ZoneEntryPoint : MonoBehaviour
{
    [Tooltip("Unique id within the project. Exits in other zones target this.")]
    public string entryId = "";

    [Tooltip("Facing direction the player should adopt when spawning here.")]
    public Vector2 faceDirection = Vector2.up;  // world XZ facing (default: into the zone)

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
