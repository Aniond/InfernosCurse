using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

// The Florence world map screen. Spawns a MapNodeView pin for every HubMap node,
// positions it on the map artwork by normalized coordinates, and routes
// selection into the detail panel. Travel is gated by node adjacency.
public class WorldMapUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("RectTransform of the map image — pins are positioned within its rect.")]
    public RectTransform   mapImageRect;
    public GameObject      nodePrefab;     // a MapNodeView prefab
    public MapNodeDetailPanel detailPanel;

    [Header("Travel")]
    // Defaults false: early game allows fast-travel to any discovered node. The
    // adjacency-gated path (IsReachable/currentNodeId/neighbors) is implemented
    // but untested in play — flip this on once node discovery drives currentNodeId.
    [Tooltip("If true, you can only open/travel to nodes adjacent to the current one.")]
    public bool restrictToNeighbors = false;
    [Tooltip("Node id the player currently occupies. Empty = anywhere allowed.")]
    public string currentNodeId = "";

    private readonly Dictionary<string, MapNodeView> _pins = new();
    private MapNodeView _selected;
    private HubMap      _hub;

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    void Start()
    {
        StartCoroutine(BuildWhenReady());
    }

    // HubMap is spawned by GameSystemsBootstrap; on a fresh scene load its Awake
    // may run after ours. Wait a few frames for the instance before failing.
    System.Collections.IEnumerator BuildWhenReady()
    {
        int tries = 0;
        while (HubMap.Instance == null && tries < 300)   // ~5s at 60fps
        {
            tries++;
            yield return null;
        }

        _hub = HubMap.Instance;
        if (_hub == null)
        {
            Debug.LogError("[WorldMapUI] HubMap never became available — map cannot build.");
            yield break;
        }

        BuildPins();
        _hub.OnNodeChanged += OnNodeChanged;
    }

    void OnDestroy()
    {
        if (_hub != null) _hub.OnNodeChanged -= OnNodeChanged;
    }

    // ── Pin construction ───────────────────────────────────────────────────────

    void BuildPins()
    {
        if (nodePrefab == null || mapImageRect == null)
        {
            Debug.LogError("[WorldMapUI] nodePrefab or mapImageRect is not assigned.");
            return;
        }

        foreach (var node in _hub.AllNodes)
        {
            if (!MapRouting.IsVisible(node)) continue;
            var go  = Instantiate(nodePrefab, mapImageRect);
            var pin = go.GetComponent<MapNodeView>();
            if (pin == null)
            {
                Debug.LogError("[WorldMapUI] nodePrefab is missing a MapNodeView component.");
                Destroy(go);
                continue;
            }

            PositionPin(pin.GetComponent<RectTransform>(), node.mapImagePosition);
            pin.Bind(node, this);
            _pins[node.id] = pin;
        }
    }

    // Place a pin at a normalized (0-1) position within the map image rect.
    void PositionPin(RectTransform pinRect, Vector2 normalized)
    {
        if (pinRect == null || mapImageRect == null) return;
        var size = mapImageRect.rect.size;
        // Anchor to bottom-left of the map rect, offset by normalized * size.
        // Pivot is forced to center so the pin sits ON the target point regardless
        // of how the prefab's RectTransform pivot was authored.
        pinRect.anchorMin = pinRect.anchorMax = new Vector2(0f, 0f);
        pinRect.pivot = new Vector2(0.5f, 0.5f);
        pinRect.anchoredPosition = new Vector2(normalized.x * size.x, normalized.y * size.y);
    }

    // ── Selection ──────────────────────────────────────────────────────────────

    public void OnNodeClicked(MapNodeView pin)
    {
        if (pin == null || pin.Node == null) return;

        if (restrictToNeighbors && !IsReachable(pin.Node))
        {
            Debug.Log($"[WorldMapUI] {pin.Node.displayName} is not reachable from here.");
            return;
        }

        Select(pin);
    }

    public void OnNodeHovered(MapNodeView pin, bool entered)
    {
        // Reserved for hover tooltips / pin scale-up. No-op for now.
    }

    void Select(MapNodeView pin)
    {
        if (_selected != null) _selected.SetSelected(false);
        _selected = pin;
        pin.SetSelected(true);

        detailPanel?.Show(pin.Node, TravelTo);
    }

    // ── Travel ─────────────────────────────────────────────────────────────────

    void TravelTo(HubNode node)
    {
        if (node == null || string.IsNullOrEmpty(node.sceneName)) return;

        // Guard against a scene that isn't in Build Settings — LoadScene would
        // throw and strand the player on the map otherwise.
        if (!Application.CanStreamedLevelBeLoaded(node.sceneName))
        {
            Debug.LogError($"[WorldMapUI] Scene '{node.sceneName}' is not in Build Settings — cannot travel.");
            return;
        }

        currentNodeId = node.id;
        DistrictTracker.CurrentNodeId = node.id;
        // Tell the destination scene where to spawn the player.
        TravelIntent.SetEntry(node.entryId);
        Debug.Log($"[WorldMapUI] Traveling to {node.displayName} ({node.sceneName}), entry '{node.entryId}'.");
        SceneManager.LoadScene(node.sceneName);
    }

    // A node is reachable if it's the current node or a neighbor of it.
    bool IsReachable(HubNode node)
    {
        if (string.IsNullOrEmpty(currentNodeId)) return true;
        if (node.id == currentNodeId) return true;

        var current = _hub.GetNode(currentNodeId);
        if (current == null) return true;
        return current.neighbors.Exists(n => n.id == node.id);
    }

    // ── Curse updates ──────────────────────────────────────────────────────────

    void OnNodeChanged(HubNode node)
    {
        if (node == null) return;
        if (_pins.TryGetValue(node.id, out var pin))
            pin.Refresh();

        // Keep the open detail panel in sync if it's showing this node.
        if (detailPanel != null && detailPanel.IsOpen && _selected != null && _selected.Node == node)
            detailPanel.Show(node, TravelTo);
    }
}
