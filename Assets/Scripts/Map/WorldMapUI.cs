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
        _hub = HubMap.Instance;
        if (_hub == null)
        {
            Debug.LogError("[WorldMapUI] No HubMap instance — map cannot build.");
            return;
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
        Debug.Log($"[WorldMapUI] Traveling to {node.displayName} ({node.sceneName}).");
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
