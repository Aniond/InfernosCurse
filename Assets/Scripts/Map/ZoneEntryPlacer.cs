using UnityEngine;

// Place one per explorable zone scene. On Start it reads the TravelIntent and
// moves the player to the matching ZoneEntryPoint. If there's no intent (e.g.
// the scene was opened directly), the player keeps their authored position.
public class ZoneEntryPlacer : MonoBehaviour
{
    [Tooltip("Player root to reposition. Auto-finds by tag if left empty.")]
    public GameObject player;

    [Tooltip("Tag used to find the player when not assigned.")]
    public string playerTag = "Player";

    void Start()
    {
        if (!TravelIntent.HasIntent) return;

        string targetId = TravelIntent.Consume();

        var entry = FindEntry(targetId);
        if (entry == null)
        {
            Debug.LogWarning($"[ZoneEntryPlacer] No ZoneEntryPoint with id '{targetId}' in this scene.");
            return;
        }

        var p = player != null ? player : GameObject.FindGameObjectWithTag(playerTag);
        if (p == null)
        {
            Debug.LogWarning("[ZoneEntryPlacer] No player found to place.");
            return;
        }

        // Move via Rigidbody when present so physics doesn't fight the teleport.
        var pos = entry.transform.position;
        var rb = p.GetComponent<Rigidbody>();
        if (rb != null) { rb.position = pos; rb.linearVelocity = Vector3.zero; }
        p.transform.position = pos;

        // Apply facing if the player exposes it
        var face = entry.faceDirection;
        var controller = p.GetComponent<PlayerController>();
        if (controller != null && face.sqrMagnitude > 0.01f)
            controller.SetFacing(face);

        Debug.Log($"[ZoneEntryPlacer] Placed player at entry '{targetId}'.");
    }

    ZoneEntryPoint FindEntry(string id)
    {
        foreach (var e in FindObjectsByType<ZoneEntryPoint>(FindObjectsInactive.Include))
            if (e.entryId == id) return e;
        return null;
    }
}
