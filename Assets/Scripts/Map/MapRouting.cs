using System.Collections.Generic;
using UnityEngine;

// Pathing helpers for the Gugol Mappe world map. Routes run through the
// HubMap neighbor graph (BFS, fewest hops) so the dotted line reads like
// street directions rather than a bird's flight.
public static class MapRouting
{
    // Shortest hop path from one node to another, both endpoints included.
    // Falls back to a straight [from, to] segment when the graph doesn't
    // connect them (teaser nodes, missing edges).
    public static List<HubNode> FindPath(HubMap hub, string fromId, string toId)
    {
        var from = hub != null ? hub.GetNode(fromId) : null;
        var to   = hub != null ? hub.GetNode(toId)   : null;
        if (from == null || to == null)
        {
            var direct = new List<HubNode>();
            if (from != null) direct.Add(from);
            if (to   != null) direct.Add(to);
            return direct;
        }
        if (from == to) return new List<HubNode> { from };

        hub.EnsureGraphBuilt();

        var cameFrom = new Dictionary<HubNode, HubNode> { [from] = from };
        var queue = new Queue<HubNode>();
        queue.Enqueue(from);

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            if (cur == to) break;
            foreach (var nb in cur.neighbors)
            {
                if (nb == null || cameFrom.ContainsKey(nb)) continue;
                cameFrom[nb] = cur;
                queue.Enqueue(nb);
            }
        }

        if (!cameFrom.ContainsKey(to))
            return new List<HubNode> { from, to };   // disconnected — straight line

        var path = new List<HubNode>();
        for (var n = to; n != from; n = cameFrom[n]) path.Add(n);
        path.Add(from);
        path.Reverse();
        return path;
    }

    // Total route length in normalized map units (mapImagePosition space).
    public static float PathLengthNormalized(List<HubNode> path)
    {
        if (path == null || path.Count < 2) return 0f;
        float len = 0f;
        for (int i = 1; i < path.Count; i++)
            len += Vector2.Distance(path[i - 1].mapImagePosition, path[i].mapImagePosition);
        return len;
    }

    // Parody-card walking time. Floor of 3 min so adjacent pins never read "0 min".
    public static int WalkMinutes(float lengthNormalized, float minutesPerMapUnit)
        => Mathf.Max(3, Mathf.RoundToInt(lengthNormalized * minutesPerMapUnit));

    // A node is travellable when its scene exists in Build Settings. Teaser
    // districts (empty sceneName) and not-yet-built scenes render as locked.
    // Same availability rule FastTravelMenu applies to its district list.
    public static bool IsUnlocked(HubNode node)
        => node != null
        && !string.IsNullOrEmpty(node.sceneName)
        && Application.CanStreamedLevelBeLoaded(node.sceneName);

    // ── Region layer (FFT overworld) ───────────────────────────────────────────

    // The one City node standing for all of Florence on the region layer.
    public const string CityAnchorId = "firenze";

    // Where a node "lives" at region scale: districts collapse into the city
    // pin; towns/waypoints are their own anchor. Travel is region travel
    // whenever the two anchors differ.
    public static string RegionAnchorId(HubNode node)
        => node != null && node.kind == NodeKind.District ? CityAnchorId : node?.id;

    // Autohide, generalized: a POI appears the moment its scene ships in Build
    // Settings; the City pin always shows; a road waypoint shows once any real
    // place it connects to is visible.
    public static bool IsVisible(HubNode node)
    {
        if (node == null) return false;
        switch (node.kind)
        {
            case NodeKind.City:
                return true;
            case NodeKind.Waypoint:
                foreach (var nb in node.neighbors)
                    if (nb != null && nb.kind != NodeKind.Waypoint && IsVisible(nb))
                        return true;
                return false;
            default:
                return IsUnlocked(node);
        }
    }

    // Road time between region anchors. Near towns cost hours, far ones days.
    public static float RegionHours(float lengthNormalized, float hoursPerMapUnit)
        => Mathf.Max(0.5f, lengthNormalized * hoursPerMapUnit);

    // Cart fare for the journey — charged up front, min 1 florin.
    public static int RegionFare(float hours, float florinsPerHour)
        => Mathf.Max(1, Mathf.CeilToInt(hours * florinsPerHour));
}
