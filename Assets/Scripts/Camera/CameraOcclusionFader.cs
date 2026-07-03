using System.Collections.Generic;
using UnityEngine;

// HD-2D interior occlusion fix (Duomo). The follow camera sits south of the
// player looking north, so at the entrance and at every throat turn a wall can
// sit between the lens and the player. Each frame this spherecasts from the
// camera to the player and hides ONLY architecture segments on that sightline
// (dollhouse-style), restoring them the moment the line clears. Piers are left
// alone on purpose — they're thin and sweeping past them reads naturally.
public class CameraOcclusionFader : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Player to keep visible. Auto-finds by tag if empty.")]
    public Transform target;
    public string playerTag = "Player";
    [Tooltip("Aim point above the player's feet (chest height).")]
    public float targetHeight = 1.2f;

    [Header("What counts as occluding architecture")]
    [Tooltip("Only objects whose name starts with one of these fade out. " +
             "Walls yes, piers no (thin, momentary occlusion reads fine).")]
    public string[] wallPrefixes = { "Oct_", "Nave_Wall_", "Nave_Stub_", "Facade_", "Throat_", "Trib_" };
    [Tooltip("Radius of the sightline probe — catches walls that graze the line.")]
    public float probeRadius = 0.6f;

    readonly HashSet<Renderer> hidden = new HashSet<Renderer>();
    readonly HashSet<Renderer> stillBlocking = new HashSet<Renderer>();
    // pier colliders live on hidden placeholder cylinders; the visible mesh is a
    // sibling named PierModel_<cylinder name>. Cache the mapping per collider.
    readonly Dictionary<Collider, Renderer[]> pierMap = new Dictionary<Collider, Renderer[]>();
    static readonly RaycastHit[] hits = new RaycastHit[32];

    void Start()
    {
        if (target == null)
        {
            var go = GameObject.FindGameObjectWithTag(playerTag);
            if (go != null) target = go.transform;
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 from = transform.position;
        Vector3 to = target.position + Vector3.up * targetHeight;
        Vector3 dir = to - from;
        float dist = dir.magnitude;
        if (dist < 0.01f) return;
        dir /= dist;

        stillBlocking.Clear();
        int n = Physics.SphereCastNonAlloc(from, probeRadius, dir, hits, dist, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var col = hits[i].collider;
            if (col == null || col.transform == target) continue;
            var cn = col.gameObject.name;

            if (IsWall(cn))
            {
                var r = col.GetComponent<Renderer>();
                if (r == null) continue;
                stillBlocking.Add(r);
                if (hidden.Add(r)) r.enabled = false;
            }
            else if (cn.StartsWith("Pier_") || cn.StartsWith("TribPier_"))
            {
                if (!pierMap.TryGetValue(col, out var prs))
                {
                    var model = GameObject.Find("PierModel_" + cn);
                    prs = model != null ? model.GetComponentsInChildren<Renderer>() : new Renderer[0];
                    pierMap[col] = prs;
                }
                foreach (var r in prs)
                {
                    if (r == null) continue;
                    stillBlocking.Add(r);
                    if (hidden.Add(r)) r.enabled = false;
                }
            }
        }

        // restore anything we hid that no longer blocks the sightline
        hidden.RemoveWhere(r =>
        {
            if (r == null) return true;
            if (stillBlocking.Contains(r)) return false;
            r.enabled = true;
            return true;
        });
    }

    bool IsWall(string n)
    {
        foreach (var p in wallPrefixes)
            if (n.StartsWith(p)) return true;
        return false;
    }

    void OnDisable()
    {
        foreach (var r in hidden)
            if (r != null) r.enabled = true;
        hidden.Clear();
    }
}
