using UnityEngine;
using System.Collections.Generic;

// The overworld graph and per-location Circle influence ledger. One instance
// lives in the hub scene. Influence changes only through explicit world
// sources/events and the idempotent daily director; wall-clock time is inert.
// BattleStarter's legacy curseLevel reads remain an explicit Limbo bridge.
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

    [Header("Circle Influence")]
    [Tooltip("Legacy Florence definition. Retained while callers migrate to circleDefinitions.")]
    public CurseDefinition activeCurse;
    [Tooltip("Registered Circle definitions. Empty lists fall back to activeCurse.")]
    public List<CurseDefinition> circleDefinitions = new();

    [Header("Nodes (configure in Inspector or via code)")]
    public List<HubNodeData> nodeData = new();

    // Runtime graph
    private List<HubNode>      _nodes = new();

    // ── Events ────────────────────────────────────────────────────────────────
    public event System.Action<HubNode> OnNodeChanged;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BuildGraph();
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
                discoveryId = nd.discoveryId,
                minimumDiscoveryStage = nd.minimumDiscoveryStage,
                nativeCircle = nd.nativeCircle,
                sanctity    = nd.startingSanctity,
                population  = nd.population,
                isRitualSite   = nd.isRitualSite,
                isSanctuarySite = nd.isSanctuarySite,
            };
            if (nd.startingInfluences != null && nd.startingInfluences.Count > 0)
            {
                foreach (var seed in nd.startingInfluences)
                    if (seed != null) node.SetInfluence(seed.circle, seed.value);
            }
            else
            {
                // Legacy authored Florence data becomes native-Circle influence.
                node.SetInfluence(nd.nativeCircle, nd.startingCurseLevel);
            }
            CircleInfluenceLedger.Normalize(node.circleInfluence);
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

    public CurseDefinition GetCircleDefinition(CircleId circle)
    {
        if (circleDefinitions != null)
            foreach (var definition in circleDefinitions)
                if (definition != null && definition.circleId == circle)
                    return definition;
        return activeCurse != null && activeCurse.circleId == circle ? activeCurse : null;
    }

    public float GetInfluence(string nodeId, CircleId circle)
    {
        if (!GameFeatures.CorruptionEnabled) return 0f;
        return GetNode(nodeId)?.GetInfluence(circle) ?? 0f;
    }

    public bool SetInfluence(string nodeId, CircleId circle, float value)
    {
        if (!GameFeatures.CorruptionEnabled) return false;
        var node = GetNode(nodeId);
        if (node == null || !node.SetInfluence(circle, value)) return false;
        OnNodeChanged?.Invoke(node);
        return true;
    }

    public bool AddInfluence(string nodeId, CircleId circle, float amount)
    {
        if (!GameFeatures.CorruptionEnabled || Mathf.Approximately(amount, 0f)) return false;
        var node = GetNode(nodeId);
        if (node == null || !node.AddInfluence(circle, amount)) return false;
        OnNodeChanged?.Invoke(node);
        return true;
    }

    // Canonical event reconciliation must restore stored state even while the
    // temporary corruption presentation feature gate is parked.
    public bool ApplyLedgerInfluenceDelta(string nodeId, CircleId circle, float amount)
    {
        var node = GetNode(nodeId);
        if (node == null) return false;
        if (node.AddInfluence(circle, amount)) OnNodeChanged?.Invoke(node);
        return true; // A clamped no-op is still an idempotently applied effect.
    }

    public bool ApplyLedgerBaseline(string nodeId, CircleId circle, float value, bool clearOtherCircles)
    {
        var node = GetNode(nodeId);
        if (node == null) return false;
        if (clearOtherCircles) node.circleInfluence.Clear();
        node.SetInfluence(circle, value);
        OnNodeChanged?.Invoke(node);
        return true;
    }

    public bool ApplyLedgerSanctityDelta(string nodeId, float amount)
    {
        var node = GetNode(nodeId);
        if (node == null) return false;
        float next = Mathf.Clamp01(node.sanctity + amount);
        if (!Mathf.Approximately(next, node.sanctity))
        {
            node.sanctity = next;
            OnNodeChanged?.Invoke(node);
        }
        return true;
    }

    public bool CleanseInfluence(string nodeId, CircleId circle, float amount, bool addSanctity = true)
    {
        if (!GameFeatures.CorruptionEnabled || amount <= 0f) return false;
        var node = GetNode(nodeId);
        if (node == null) return false;
        bool changed = node.AddInfluence(circle, -amount);
        if (addSanctity)
        {
            float next = Mathf.Clamp01(node.sanctity + amount * 0.3f);
            changed |= !Mathf.Approximately(next, node.sanctity);
            node.sanctity = next;
        }
        if (changed) OnNodeChanged?.Invoke(node);
        return changed;
    }

    // ── Public API built ahead of use ─────────────────────────────────────────
    // NOTE: Cleanse / ActivateRitual / GetBattleSeedCurse have no callers yet —
    // they're the intended interface for the cleric/ritual/battle-seed systems
    // that aren't built. Only GlobalCurseLevel() is currently consumed.

    // Called by clerics / player abilities (not yet wired)
    public void Cleanse(string nodeId, float amount)
    {
        CleanseInfluence(nodeId, CircleId.Limbo, amount);
    }

    // The corruption side of the ledger: rest costs and the daily drift land
    // here. Mirror of Cleanse so the world-state tells (pin tints, overlays)
    // react through the same OnNodeChanged path.
    public void AddCurse(string nodeId, float amount)
    {
        if (amount > 0f) AddInfluence(nodeId, CircleId.Limbo, amount);
    }

    // ── Save-game round-trip (SaveSystem) ────────────────────────────────────

    public void ExportNodeStates(out string[] ids, out float[] curse, out float[] sanctity)
    {
        EnsureGraphBuilt();
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

    public void ExportInfluenceStates(out string[] locationIds, out int[] circleIds, out float[] values)
    {
        EnsureGraphBuilt();
        int count = 0;
        foreach (var node in _nodes)
        {
            CircleInfluenceLedger.Normalize(node.circleInfluence);
            if (node.circleInfluence != null)
                foreach (var state in node.circleInfluence)
                    if (state != null && state.value > 0f) count++;
        }

        locationIds = new string[count];
        circleIds = new int[count];
        values = new float[count];
        int index = 0;
        foreach (var node in _nodes)
        {
            if (node.circleInfluence == null) continue;
            CircleInfluenceLedger.Normalize(node.circleInfluence);
            foreach (var state in node.circleInfluence)
            {
                if (state == null || state.value <= 0f) continue;
                locationIds[index] = node.id;
                circleIds[index] = (int)state.circle;
                values[index] = Mathf.Clamp01(state.value);
                index++;
            }
        }
    }

    public bool ImportInfluenceStates(string[] locationIds, int[] circleIds, float[] values)
    {
        if (locationIds == null || circleIds == null || values == null ||
            locationIds.Length != circleIds.Length || locationIds.Length != values.Length)
        {
            Debug.LogWarning("[CircleInfluence] Rejected malformed save arrays.");
            return false;
        }

        var changed = new HashSet<HubNode>();
        for (int i = 0; i < locationIds.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(locationIds[i]) ||
                !System.Enum.IsDefined(typeof(CircleId), circleIds[i]) ||
                float.IsNaN(values[i]) || float.IsInfinity(values[i]))
            {
                Debug.LogWarning($"[CircleInfluence] Rejected invalid entry at index {i}.");
                return false;
            }
        }

        EnsureGraphBuilt();
        foreach (var node in _nodes)
        {
            if (node.circleInfluence.Count > 0) changed.Add(node);
            node.circleInfluence.Clear();
        }

        for (int i = 0; i < locationIds.Length; i++)
        {
            var node = GetNode(locationIds[i]);
            if (node == null) continue; // Authored graph may have changed since the save.
            if (node.SetInfluence((CircleId)circleIds[i], values[i])) changed.Add(node);
        }

        foreach (var node in _nodes)
        {
            CircleInfluenceLedger.Normalize(node.circleInfluence);
            if (changed.Contains(node)) OnNodeChanged?.Invoke(node);
        }
        return true;
    }

    public void ExportSanctityStates(out string[] ids, out float[] sanctity)
    {
        EnsureGraphBuilt();
        ids = new string[_nodes.Count];
        sanctity = new float[_nodes.Count];
        for (int i = 0; i < _nodes.Count; i++)
        {
            ids[i] = _nodes[i].id;
            sanctity[i] = Mathf.Clamp01(_nodes[i].sanctity);
        }
    }

    public void ImportSanctityStates(string[] ids, float[] sanctity)
    {
        if (ids == null || sanctity == null || ids.Length != sanctity.Length) return;
        for (int i = 0; i < ids.Length; i++)
        {
            var node = GetNode(ids[i]);
            if (node == null || float.IsNaN(sanctity[i]) || float.IsInfinity(sanctity[i])) continue;
            float next = Mathf.Clamp01(sanctity[i]);
            if (Mathf.Approximately(next, node.sanctity)) continue;
            node.sanctity = next;
            OnNodeChanged?.Invoke(node);
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
        if (!GameFeatures.CorruptionEnabled) return;
        var node = GetNode(nodeId);
        if (node == null) return;
        bool newlyActivated = !node.isRitualSite;
        node.isRitualSite = true;
        bool influenceChanged = AddInfluence(nodeId, CircleId.Limbo, 0.2f);
        if (newlyActivated && !influenceChanged) OnNodeChanged?.Invoke(node);
    }

    // The moral ledger (David 7/08): Limbo's corruption grows not just with
    // the daily tide but with what Ben DOESN'T do — each refused plea, each
    // ignored soul nudges every district. The future quest/dialogue layer
    // calls this when the player walks away from someone in need.
    public void NudgeGlobalCurse(float delta, string reason)
        => NudgeGlobalInfluence(CircleId.Limbo, delta, reason);

    public void NudgeGlobalInfluence(CircleId circle, float delta, string reason)
    {
        if (!GameFeatures.CorruptionEnabled || Mathf.Approximately(delta, 0f)) return;
        EnsureGraphBuilt();
        foreach (var node in _nodes)
        {
            if (node.kind == NodeKind.Waypoint) continue;
            if (node.AddInfluence(circle, delta)) OnNodeChanged?.Invoke(node);
        }
        Debug.Log($"[CircleInfluence] {circle}: {reason} ({(delta >= 0 ? "+" : "")}{delta * 100f:0.#}%)");
    }

    // A plea ignored, a district abandoned (David 7/08: "player decides not
    // to help the florist — the Garden is lost, and corruption goes up").
    // The zone's node falls fully corrupted AND all Florence pays the tithe.
    public void LoseZone(string nodeId, string reason)
    {
        if (!GameFeatures.CorruptionEnabled) return;
        var node = GetNode(nodeId);
        if (node != null)
            SetInfluence(nodeId, CircleId.Limbo, 1f);
        NudgeGlobalCurse(0.05f, reason);
    }

    // Returns average curse across all nodes (feeds globalCurseLevel).
    // Road waypoints are excluded — near-unpopulated dots on the region map
    // would dilute the city's corruption average.
    public float GlobalCurseLevel()
        => GlobalInfluence(CircleId.Limbo);

    public float GlobalInfluence(CircleId circle)
    {
        if (!GameFeatures.CorruptionEnabled) return 0f;
        EnsureGraphBuilt();
        float sum = 0f;
        int count = 0;
        foreach (var n in _nodes)
        {
            if (n.kind == NodeKind.Waypoint) continue;
            sum += n.GetInfluence(circle);
            count++;
        }
        return count == 0 ? 0f : sum / count;
    }

    // Seed a battle grid's curse density from this node's curseLevel
    public float GetBattleSeedCurse(string nodeId)
        => GetBattleSeedInfluence(nodeId, CircleId.Limbo);

    public float GetBattleSeedInfluence(string nodeId, CircleId circle)
    {
        if (!GameFeatures.CorruptionEnabled) return 0f;
        var node = GetNode(nodeId);
        return node?.GetInfluence(circle) ?? 0f;
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
    [Tooltip("Optional discovery record. Empty keeps existing authored nodes visible.")]
    public string discoveryId;
    [Tooltip("Exact pin/travel access appears only at this monotonic discovery stage.")]
    public DiscoveryStage minimumDiscoveryStage = DiscoveryStage.Discovered;
    [Tooltip("The Circle native to this location. Florence nodes use Limbo.")]
    public CircleId nativeCircle = CircleId.Limbo;
    [Tooltip("Explicit multi-Circle starting values. Empty uses startingCurseLevel as native-Circle legacy data.")]
    public List<CircleInfluenceSeed> startingInfluences = new();
    [Range(0f, 1f)] public float startingCurseLevel = 0f;
    [Range(0f, 1f)] public float startingSanctity   = 0f;
    [Range(0f, 1f)] public float population         = 0.5f;
    public bool        isRitualSite    = false;
    public bool        isSanctuarySite = false;
    public List<string> neighborIds   = new();
}
