using UnityEngine;
using System.Collections.Generic;

// The overworld graph. Curse diffuses along node edges each real-time tick.
// One instance lives in the hub scene. BattleStarter reads node curseLevel
// to seed the battle grid's initial curse density.
//
// LOAD-BEARING: this component is DISABLED on the GameSystems prefab, on
// purpose. Awake still runs (Instance/graph/API all work), but the real-time
// diffusion Update stays dormant — DailyCurseDrift is the sole hub-curse
// driver so that DAYS, not wall-clock seconds, move the corruption (time is a
// resource the player spends by resting). Re-enabling this component would
// double-drive curse growth.
public class HubMap : MonoBehaviour
{
    // Lazy-resolving like FlorenceWeather/GameCalendar: a mid-play domain
    // reload (script recompile during play) wipes Awake-set statics without
    // re-running Awake, leaving the singleton permanently null otherwise.
    static HubMap _instance;
    public static HubMap Instance
    {
        get => _instance != null ? _instance : (_instance = FindAnyObjectByType<HubMap>());
        private set => _instance = value;
    }

    [Header("Curse")]
    public CurseDefinition activeCurse;

    [Header("Tick Rate")]
    [Tooltip("How many real seconds between hub propagation ticks.")]
    public float tickInterval = 60f;   // 1 real minute = 1 in-game hour

    [Header("Nodes (configure in Inspector or via code)")]
    public List<HubNodeData> nodeData = new();

    // Runtime graph
    private List<HubNode>      _nodes = new();
    private float              _timer = 0f;

    // ── Events ────────────────────────────────────────────────────────────────
    public event System.Action<HubNode> OnNodeChanged;
    // Fires when any node crosses activeCurse.surgeThreshold.
    // TODO(not-yet-wired): intended to trigger enemy buffs / new ritual spawns.
    // No subscriber exists yet — this is by design until the escalation system
    // is built, not a dropped event.
    public event System.Action          OnSurge;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BuildGraph();
    }

    void Update()
    {
        _timer += Time.deltaTime;
        if (_timer >= tickInterval)
        {
            _timer -= tickInterval;
            Tick();
        }
    }

    // ── Graph construction ────────────────────────────────────────────────────

    void BuildGraph()
    {
        _nodes.Clear();
        foreach (var nd in nodeData)
        {
            var node = new HubNode
            {
                id          = nd.id,
                displayName = nd.displayName,
                mapImagePosition = nd.mapImagePosition,
                blurb       = nd.blurb,
                sceneName   = nd.sceneName,
                entryId     = nd.entryId,
                previewImage = nd.previewImage,
                microClimate = nd.microClimate,
                kind        = nd.kind,
                mapLevel    = nd.mapLevel,
                curseLevel  = nd.startingCurseLevel,
                sanctity    = nd.startingSanctity,
                population  = nd.population,
                isRitualSite   = nd.isRitualSite,
                isSanctuarySite = nd.isSanctuarySite,
            };
            _nodes.Add(node);
        }

        // Wire edges by index pairs
        foreach (var nd in nodeData)
        {
            var src = GetNode(nd.id);
            if (src == null) continue;
            foreach (var neighborId in nd.neighborIds)
            {
                var dst = GetNode(neighborId);
                if (dst != null && !src.neighbors.Contains(dst))
                {
                    src.neighbors.Add(dst);
                    dst.neighbors.Add(src);
                }
            }
        }
    }

    // ── Propagation tick ──────────────────────────────────────────────────────

    void Tick()
    {
        if (activeCurse == null) return;

        // Double-buffer: compute new values, then apply
        var newLevels = new Dictionary<HubNode, float>();
        foreach (var node in _nodes)
            newLevels[node] = node.curseLevel;

        foreach (var node in _nodes)
        {
            float incoming = 0f;

            // Diffuse from each neighbor
            foreach (var nb in node.neighbors)
            {
                float diff = nb.curseLevel - node.curseLevel;
                if (diff > 0f)
                    incoming += diff * activeCurse.hubSpreadRate * nb.population;
            }

            // Ritual sites are permanent sources — keep pumping
            if (node.isRitualSite)
                incoming += activeCurse.hubSpreadRate * 0.5f;

            // Sanctity resists incoming spread
            float resistance = node.sanctity * activeCurse.sanctityResistance;
            incoming *= (1f - resistance);

            // Sanctuary sites actively push back
            float outgoing = 0f;
            if (node.isSanctuarySite)
                outgoing = activeCurse.decayRate * 2f;

            // Natural decay
            outgoing += activeCurse.decayRate;

            newLevels[node] = Mathf.Clamp01(newLevels[node] + incoming - outgoing);
        }

        bool surgeTriggered = false;
        float totalCurse = 0f;

        foreach (var node in _nodes)
        {
            float prev = node.curseLevel;
            node.curseLevel = newLevels[node];
            totalCurse += node.curseLevel;

            if (!Mathf.Approximately(prev, node.curseLevel))
                OnNodeChanged?.Invoke(node);

            if (node.curseLevel >= activeCurse.surgeThreshold && prev < activeCurse.surgeThreshold)
                surgeTriggered = true;
        }

        if (surgeTriggered)
            OnSurge?.Invoke();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Build the runtime graph if it hasn't been (edit-mode tools instantiate
    /// the prefab without Awake running — e.g. the drift economy sim).
    /// </summary>
    public void EnsureGraphBuilt()
    {
        if (_nodes.Count == 0 && nodeData.Count > 0) BuildGraph();
    }

    public HubNode GetNode(string id)
    {
        EnsureGraphBuilt();   // _nodes isn't serialized — self-heal after a mid-play domain reload
        foreach (var n in _nodes) if (n.id == id) return n;
        return null;
    }

    public List<HubNode> AllNodes => _nodes;

    // ── Public API built ahead of use ─────────────────────────────────────────
    // NOTE: Cleanse / ActivateRitual / GetBattleSeedCurse have no callers yet —
    // they're the intended interface for the cleric/ritual/battle-seed systems
    // that aren't built. Only GlobalCurseLevel() is currently consumed.

    // Called by clerics / player abilities (not yet wired)
    public void Cleanse(string nodeId, float amount)
    {
        var node = GetNode(nodeId);
        if (node == null) return;
        node.curseLevel = Mathf.Max(0f, node.curseLevel - amount);
        node.sanctity   = Mathf.Min(1f, node.sanctity   + amount * 0.3f);
        OnNodeChanged?.Invoke(node);
    }

    // The corruption side of the ledger: rest costs and the daily drift land
    // here. Mirror of Cleanse so the world-state tells (pin tints, overlays)
    // react through the same OnNodeChanged path.
    public void AddCurse(string nodeId, float amount)
    {
        var node = GetNode(nodeId);
        if (node == null || amount <= 0f) return;
        node.curseLevel = Mathf.Min(1f, node.curseLevel + amount);
        OnNodeChanged?.Invoke(node);
    }

    // ── Save-game round-trip (SaveSystem) ────────────────────────────────────

    public void ExportNodeStates(out string[] ids, out float[] curse, out float[] sanctity)
    {
        ids = new string[_nodes.Count];
        curse = new float[_nodes.Count];
        sanctity = new float[_nodes.Count];
        for (int i = 0; i < _nodes.Count; i++)
        {
            ids[i] = _nodes[i].id;
            curse[i] = _nodes[i].curseLevel;
            sanctity[i] = _nodes[i].sanctity;
        }
    }

    public void ImportNodeStates(string[] ids, float[] curse, float[] sanctity)
    {
        if (ids == null || curse == null || ids.Length != curse.Length) return;
        for (int i = 0; i < ids.Length; i++)
        {
            var node = GetNode(ids[i]);
            if (node == null) continue; // node list changed since the save — skip
            node.curseLevel = Mathf.Clamp01(curse[i]);
            if (sanctity != null && sanctity.Length == ids.Length)
                node.sanctity = Mathf.Clamp01(sanctity[i]);
            OnNodeChanged?.Invoke(node);
        }
    }

    // Called when a ritual is completed
    public void ActivateRitual(string nodeId)
    {
        var node = GetNode(nodeId);
        if (node == null) return;
        node.isRitualSite = true;
        node.curseLevel   = Mathf.Min(1f, node.curseLevel + 0.2f);
        OnNodeChanged?.Invoke(node);
    }

    // The moral ledger (David 7/08): Limbo's corruption grows not just with
    // the daily tide but with what Ben DOESN'T do — each refused plea, each
    // ignored soul nudges every district. The future quest/dialogue layer
    // calls this when the player walks away from someone in need.
    public void NudgeGlobalCurse(float delta, string reason)
    {
        foreach (var n in _nodes)
        {
            if (n.kind == NodeKind.Waypoint) continue;
            n.curseLevel = Mathf.Clamp01(n.curseLevel + delta);
            OnNodeChanged?.Invoke(n);
        }
        Debug.Log($"[Curse] Florence remembers: {reason} (corruption {(delta >= 0 ? "+" : "")}{delta * 100f:0.#}%)");
    }

    // A plea ignored, a district abandoned (David 7/08: "player decides not
    // to help the florist — the Garden is lost, and corruption goes up").
    // The zone's node falls fully corrupted AND all Florence pays the tithe.
    public void LoseZone(string nodeId, string reason)
    {
        var node = GetNode(nodeId);
        if (node != null)
        {
            node.curseLevel = 1f;
            OnNodeChanged?.Invoke(node);
        }
        NudgeGlobalCurse(0.05f, reason);
    }

    // Returns average curse across all nodes (feeds globalCurseLevel).
    // Road waypoints are excluded — near-unpopulated dots on the region map
    // would dilute the city's corruption average.
    public float GlobalCurseLevel()
    {
        float sum = 0f;
        int count = 0;
        foreach (var n in _nodes)
        {
            if (n.kind == NodeKind.Waypoint) continue;
            sum += n.curseLevel;
            count++;
        }
        return count == 0 ? 0f : sum / count;
    }

    // Seed a battle grid's curse density from this node's curseLevel
    public float GetBattleSeedCurse(string nodeId)
    {
        var node = GetNode(nodeId);
        return node?.curseLevel ?? 0f;
    }
}

// ── Inspector data ─────────────────────────────────────────────────────────────

[System.Serializable]
public class HubNodeData
{
    public string      id;
    public string      displayName;
    public Vector2     mapPosition;
    [Tooltip("Normalized 0-1 position on the Florence map image (x=left→right, " +
             "y=bottom→top). Drives where the clickable pin sits on the artwork.")]
    public Vector2     mapImagePosition = new Vector2(0.5f, 0.5f);
    [TextArea(2, 3)]
    public string      blurb;            // shown in the detail panel
    public string      sceneName;        // scene to load on Enter (optional)
    public string      entryId;          // ZoneEntryPoint id to spawn at on arrival
    public Sprite      previewImage;     // location splash for the detail panel
    [Tooltip("Local weather character — drives the per-district weather variant.")]
    public MicroClimate microClimate = MicroClimate.Default;
    [Tooltip("District = city-layer pin; Town/Waypoint/City = region/world layers.")]
    public NodeKind kind = NodeKind.District;
    [Tooltip("Which zoom sheet this node renders on.")]
    public MapLevel mapLevel = MapLevel.City;
    [Range(0f, 1f)] public float startingCurseLevel = 0f;
    [Range(0f, 1f)] public float startingSanctity   = 0f;
    [Range(0f, 1f)] public float population         = 0.5f;
    public bool        isRitualSite    = false;
    public bool        isSanctuarySite = false;
    public List<string> neighborIds   = new();
}
