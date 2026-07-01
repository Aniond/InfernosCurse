using UnityEngine;
using System.Collections.Generic;

// Cellular automata — runs between turns in battle.
// Reads/writes InfernalWorldState.curseDensity and updates BattleGrid tile types.
public class BattleCurseAutomata : MonoBehaviour
{
    public static BattleCurseAutomata Instance { get; private set; }

    [Header("References")]
    public BattleGrid grid;
    [Tooltip("Optional — if set, its overlay pool is built with the seeded curse " +
             "so the initial density is visible before any cell changes.")]
    public CurseOverlay overlay;

    [Header("Curse")]
    public CurseDefinition activeCurse;

    [Tooltip("How many CA steps to run each time Step() is called.")]
    public int stepsPerTurn = 1;

    private InfernalWorldState _world;

    // ── Events ────────────────────────────────────────────────────────────────
    public event System.Action<Vector2Int, float> OnCellCurseChanged;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // Called by BattleManager after each unit turn resolves
    public void Step(InfernalWorldState world)
    {
        _world = world;
        if (activeCurse == null || grid == null) return;

        for (int i = 0; i < stepsPerTurn; i++)
            RunStep();

        ApplyTileTypes();
        world.globalCurseLevel = ComputeGlobalCurse();
    }

    // ── Seed from hub map ─────────────────────────────────────────────────────

    // Call this at battle start to initialise curse density from the hub node
    public void SeedFromHub(InfernalWorldState world, float hubCurseLevel, List<RitualNode> ritualNodes)
    {
        _world = world;
        world.curseDensity.Clear();

        // Base noise seeded by hub level
        for (int x = 0; x < grid.width; x++)
        {
            for (int y = 0; y < grid.height; y++)
            {
                var pos = new Vector2Int(x, y);
                // Perlin-based variation so curse isn't perfectly uniform. The
                // 0.5–1.2 range can push above 1.0 at high hub levels; that's
                // intentional — SetCurseDensity clamps it to a saturated tile.
                float noise = Mathf.PerlinNoise(x * 0.3f, y * 0.3f);
                float seed  = hubCurseLevel * Mathf.Lerp(0.5f, 1.2f, noise);
                world.SetCurseDensity(pos, seed);
            }
        }

        // Ritual nodes are hot spots — max curse at their location, radiating outward
        foreach (var rn in ritualNodes)
        {
            if (!rn.active) continue;
            for (int x = 0; x < grid.width; x++)
            {
                for (int y = 0; y < grid.height; y++)
                {
                    var pos  = new Vector2Int(x, y);
                    int dist = BattleGrid.ManhattanDistance(rn.position, pos);
                    float boost = Mathf.Max(0f, 1f - dist * 0.15f) * rn.reservoirPower;
                    world.SetCurseDensity(pos, world.GetCurseDensity(pos) + boost);
                }
            }
        }

        ApplyTileTypes();

        // Build the overlay pool now so the seeded density is visible immediately
        // (otherwise cells stay invisible until they first change via OnCellCurseChanged).
        if (overlay != null) overlay.Initialise(world);
    }

    // ── Core CA step ──────────────────────────────────────────────────────────

    void RunStep()
    {
        var next = new Dictionary<Vector2Int, float>();

        for (int x = 0; x < grid.width; x++)
        {
            for (int y = 0; y < grid.height; y++)
            {
                var   pos     = new Vector2Int(x, y);
                var   cell    = grid.GetCell(pos);
                float current = _world.GetCurseDensity(pos);

                // Unwalkable cells still propagate but don't accumulate
                float spread = ComputeSpread(pos, current);
                float decay  = ComputeDecay(pos, current);

                float newVal = Mathf.Clamp01(current + spread - decay);
                next[pos] = newVal;
            }
        }

        // Apply and fire events for changed cells
        foreach (var kvp in next)
        {
            float old = _world.GetCurseDensity(kvp.Key);
            _world.SetCurseDensity(kvp.Key, kvp.Value);
            if (!Mathf.Approximately(old, kvp.Value))
                OnCellCurseChanged?.Invoke(kvp.Key, kvp.Value);
        }
    }

    float ComputeSpread(Vector2Int pos, float current)
    {
        if (activeCurse == null) return 0f;

        float incoming = 0f;
        var dirs = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        foreach (var dir in dirs)
        {
            var nb = pos + dir;
            if (!grid.InBounds(nb)) continue;
            float nbCurse = _world.GetCurseDensity(nb);
            float diff    = nbCurse - current;
            if (diff > 0f) incoming += diff * activeCurse.battleSpreadRate;
        }

        // Ritual nodes are local sources
        if (IsOnRitualNode(pos))
            incoming += activeCurse.battleSpreadRate * 0.5f;

        // Sanctity resists spread
        float resistance = _world.GetSanctity(pos) * activeCurse.sanctityResistance;
        return incoming * (1f - resistance);
    }

    float ComputeDecay(Vector2Int pos, float current)
    {
        if (activeCurse == null) return 0f;

        float decay = activeCurse.decayRate;

        // Holy tiles cleanse faster
        var cell = grid.GetCell(pos);
        if (cell != null && cell.tileType == TileType.Holy)
            decay *= 3f;

        // Sanctity accelerates decay
        decay += _world.GetSanctity(pos) * activeCurse.decayRate;

        // Don't decay below zero, don't decay ritual hotspots entirely
        if (IsOnRitualNode(pos)) decay *= 0.1f;

        return decay;
    }

    bool IsOnRitualNode(Vector2Int pos)
    {
        if (_world?.ritualNodes == null) return false;
        foreach (var rn in _world.ritualNodes)
            if (rn.active && rn.position == pos) return true;
        return false;
    }

    // ── Tile type sync ────────────────────────────────────────────────────────

    void ApplyTileTypes()
    {
        if (activeCurse == null) return;

        for (int x = 0; x < grid.width; x++)
        {
            for (int y = 0; y < grid.height; y++)
            {
                var   pos  = new Vector2Int(x, y);
                var   cell = grid.GetCell(pos);
                if (cell == null) continue;

                float cd = _world.GetCurseDensity(pos);

                if (cd >= activeCurse.tileCorruptThreshold)
                {
                    // Override tile to Void (cursed ground)
                    if (cell.tileType != TileType.Void)
                        cell.tileType = TileType.Void;
                }
                else if (cell.tileType == TileType.Void)
                {
                    // Curse receded — restore to Normal
                    cell.tileType = TileType.Normal;
                }
            }
        }
    }

    float ComputeGlobalCurse()
    {
        float sum = 0f;
        int   count = grid.width * grid.height;
        for (int x = 0; x < grid.width; x++)
            for (int y = 0; y < grid.height; y++)
                sum += _world.GetCurseDensity(new Vector2Int(x, y));
        return count > 0 ? sum / count : 0f;
    }

    // ── Player cleanse API ────────────────────────────────────────────────────

    // Pulls the world state from BattleManager if this automata hasn't been
    // seeded/stepped yet — guards against curse API calls before the first step.
    bool EnsureWorld()
    {
        if (_world != null) return true;
        _world = BattleManager.Instance?.WorldState;
        return _world != null;
    }

    // Called by AbilityResolver for Holy skills targeting a tile
    public void CleanseTile(Vector2Int pos, float amount)
    {
        if (!EnsureWorld()) return;
        float current = _world.GetCurseDensity(pos);
        _world.SetCurseDensity(pos, Mathf.Max(0f, current - amount));
        _world.SetSanctity(pos, Mathf.Min(1f, _world.GetSanctity(pos) + amount * 0.4f));
        ApplyTileTypes();
        OnCellCurseChanged?.Invoke(pos, _world.GetCurseDensity(pos));
    }

    // Spread curse from a Carrier unit's position (called by CarrierAI on move)
    public void SpreadFromUnit(Vector2Int pos, float amount)
    {
        if (!EnsureWorld()) return;
        _world.SetCurseDensity(pos, Mathf.Min(1f, _world.GetCurseDensity(pos) + amount));
        var dirs = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        foreach (var dir in dirs)
        {
            var nb = pos + dir;
            if (grid.InBounds(nb))
                _world.SetCurseDensity(nb, Mathf.Min(1f, _world.GetCurseDensity(nb) + amount * 0.4f));
        }
        ApplyTileTypes();
    }
}
