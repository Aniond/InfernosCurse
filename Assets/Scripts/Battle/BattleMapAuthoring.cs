using System.Collections.Generic;
using UnityEngine;

// Root of a battle-map prefab (FFT-style duplicate-and-dress workflow):
// duplicate BattleMap_Field, move/add BattleObstacle props, tweak plateaus —
// new map, no code. On Awake it stamps its terrain data (size, plateau
// elevations, obstacle walkability) into the scene's BattleGrid before the
// battle starts. Elevation is in HALF-UNITS like GridCell (2 = one story;
// terrain 2+ above both endpoints blocks line of sight — walls come free).
public class BattleMapAuthoring : MonoBehaviour
{
    [System.Serializable]
    public struct Plateau
    {
        public RectInt cells;      // grid-cell rect (xMin,yMin,w,h)
        public int elevation;      // half-units
    }

    [Header("Grid (must match BattleGrid conventions)")]
    public int width = 14;
    public int height = 12;
    public float tileWidth = 1f;
    public float tileHeight = 0.5f;

    [Header("Terrain")]
    public List<Plateau> plateaus = new();

    // Suggested spawns for future encounter tables — callers still pass
    // their own lists to StartBattle.
    [Header("Suggested spawns")]
    public List<Vector2Int> partySpawns = new() { new(1, 4), new(1, 5), new(2, 4), new(2, 5) };
    public List<Vector2Int> enemySpawns = new() { new(12, 6), new(12, 7), new(11, 6), new(11, 7) };

    [Tooltip("Stamp this map into the scene's BattleGrid on Awake.")]
    public bool applyOnAwake = true;

    void Awake()
    {
        if (!applyOnAwake) return;
        var grid = FindFirstObjectByType<BattleGrid>();
        if (grid == null)
        {
            Debug.LogError("[BattleMapAuthoring] No BattleGrid in scene — map not applied.");
            return;
        }
        Apply(grid);
    }

    public void Apply(BattleGrid grid)
    {
        grid.tileWidth = tileWidth;
        grid.tileHeight = tileHeight;
        grid.Initialize(width, height);

        foreach (var p in plateaus)
            for (int x = p.cells.xMin; x < p.cells.xMax; x++)
                for (int y = p.cells.yMin; y < p.cells.yMax; y++)
                {
                    var c = grid.GetCell(x, y);
                    if (c != null) c.elevation = p.elevation;
                }

        int blocked = 0;
        foreach (var ob in GetComponentsInChildren<BattleObstacle>(true))
        {
            var c = grid.GetCell(ob.cell);
            if (c == null) continue;
            if (ob.addedElevation != 0) c.elevation += ob.addedElevation;
            if (ob.makeUnwalkable) { c.walkable = false; blocked++; }
        }
        Debug.Log($"[BattleMapAuthoring] '{name}' applied: {width}x{height}, " +
                  $"{plateaus.Count} plateaus, {blocked} blocked cells.");
    }

    // Same math as BattleGrid.GridToWorld — usable at edit time (no grid).
    public Vector3 CellToWorld(Vector2Int cell, int elevation = 0)
    {
        float wx = (cell.x - cell.y) * tileWidth * 0.5f;
        float wy = (cell.x + cell.y) * tileHeight * 0.5f + elevation * tileHeight * 0.5f;
        return new Vector3(wx, wy, 0f);
    }

    public int ElevationAt(Vector2Int cell)
    {
        int e = 0;
        foreach (var p in plateaus)
            if (p.cells.Contains(cell)) e = p.elevation;
        return e;
    }
}
