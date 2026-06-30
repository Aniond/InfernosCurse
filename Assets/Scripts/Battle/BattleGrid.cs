using UnityEngine;
using System.Collections.Generic;

public class BattleGrid : MonoBehaviour
{
    [Header("Grid Dimensions")]
    public int width  = 14;
    public int height = 12;

    [Header("Tile Size (world units)")]
    public float tileWidth  = 1f;
    public float tileHeight = 0.5f;

    [Header("Jump Tolerance")]
    [Tooltip("Max elevation difference a unit can move across without Jump stat.")]
    public int baseJumpHeight = 2;

    private GridCell[,] _cells;

    // ── Init ──────────────────────────────────────────────────────────────────

    public void Initialize(int w, int h)
    {
        width  = w;
        height = h;
        _cells = new GridCell[w, h];
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                _cells[x, y] = new GridCell(new Vector2Int(x, y));
    }

    void Awake()
    {
        if (_cells == null) Initialize(width, height);
    }

    // ── Cell access ───────────────────────────────────────────────────────────

    public GridCell GetCell(int x, int y)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return null;
        return _cells[x, y];
    }

    public GridCell GetCell(Vector2Int pos) => GetCell(pos.x, pos.y);

    public bool InBounds(Vector2Int pos) =>
        pos.x >= 0 && pos.x < width && pos.y >= 0 && pos.y < height;

    // ── World ↔ Grid conversion ───────────────────────────────────────────────

    public Vector3 GridToWorld(Vector2Int gridPos, int elevation = 0)
    {
        float wx = (gridPos.x - gridPos.y) * tileWidth  * 0.5f;
        float wy = (gridPos.x + gridPos.y) * tileHeight * 0.5f + elevation * tileHeight * 0.5f;
        return new Vector3(wx, wy, 0f);
    }

    public Vector2Int WorldToGrid(Vector3 worldPos)
    {
        float gx = (worldPos.x / (tileWidth * 0.5f) + worldPos.y / (tileHeight * 0.5f)) * 0.5f;
        float gy = (worldPos.y / (tileHeight * 0.5f) - worldPos.x / (tileWidth * 0.5f)) * 0.5f;
        return new Vector2Int(Mathf.RoundToInt(gx), Mathf.RoundToInt(gy));
    }

    // ── Movement range (BFS) ──────────────────────────────────────────────────

    // mover: the unit doing the moving (optional). Used to decide which occupied
    // cells are hostile (block traversal) vs allied (pass through, can't stop).
    public List<GridCell> GetMoveRange(Vector2Int origin, int movePoints, int jumpHeight, BattleUnit mover = null)
    {
        var reachable = new List<GridCell>();
        var visited   = new Dictionary<Vector2Int, int>(); // pos → move points remaining
        var queue     = new Queue<(Vector2Int pos, int remaining)>();

        queue.Enqueue((origin, movePoints));
        visited[origin] = movePoints;

        var directions = new Vector2Int[]
        {
            Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
        };

        while (queue.Count > 0)
        {
            var (pos, remaining) = queue.Dequeue();

            foreach (var dir in directions)
            {
                var next = pos + dir;
                var cell = GetCell(next);
                if (cell == null || !cell.walkable) continue;

                var fromCell = GetCell(pos);
                int jumpCost = cell.JumpCostFrom(fromCell);
                if (jumpCost > jumpHeight) continue;

                // A cell occupied by a HOSTILE unit blocks both landing and
                // traversal — you can't walk through enemies. Allies can be
                // passed through (but not stopped on). When no mover is given,
                // fall back to "enemies block, players pass".
                if (cell.IsOccupied && cell.occupant != null && cell.occupant.IsAlive)
                {
                    bool hostile = mover != null
                        ? cell.occupant.IsPlayer != mover.IsPlayer
                        : cell.occupant.IsPlayer == false;
                    if (hostile) continue;
                }

                // Movement cost: 1 per tile + jump cost
                int cost = 1 + Mathf.Max(0, jumpCost - 1);
                int left = remaining - cost;
                if (left < 0) continue;

                if (visited.TryGetValue(next, out int prev) && prev >= left) continue;
                visited[next] = left;

                // Can pass through allies but can't stop on an occupied tile.
                if (!cell.IsOccupied)
                    reachable.Add(cell);

                queue.Enqueue((next, left));
            }
        }

        return reachable;
    }

    // ── Attack range ──────────────────────────────────────────────────────────

    public List<GridCell> GetAttackRange(Vector2Int origin, int minRange, int maxRange)
    {
        var result = new List<GridCell>();
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                int dist = ManhattanDistance(origin, new Vector2Int(x, y));
                if (dist >= minRange && dist <= maxRange)
                    result.Add(_cells[x, y]);
            }
        return result;
    }

    // ── AOE spread ────────────────────────────────────────────────────────────

    public List<GridCell> GetAOECells(Vector2Int center, int radius)
    {
        var result = new List<GridCell>();
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                if (ManhattanDistance(center, new Vector2Int(x, y)) <= radius)
                    result.Add(_cells[x, y]);
            }
        return result;
    }

    // ── Path finding (A*) ─────────────────────────────────────────────────────

    public List<Vector2Int> FindPath(Vector2Int start, Vector2Int end, int jumpHeight)
    {
        var open   = new List<Vector2Int> { start };
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        var gScore   = new Dictionary<Vector2Int, int> { [start] = 0 };
        var fScore   = new Dictionary<Vector2Int, int> { [start] = ManhattanDistance(start, end) };

        var dirs = new Vector2Int[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        while (open.Count > 0)
        {
            var current = LowestF(open, fScore);
            if (current == end) return ReconstructPath(cameFrom, current);

            open.Remove(current);
            foreach (var dir in dirs)
            {
                var neighbor = current + dir;
                var cell     = GetCell(neighbor);
                if (cell == null || !cell.walkable) continue;

                int jumpCost = cell.JumpCostFrom(GetCell(current));
                if (jumpCost > jumpHeight) continue;

                int tentativeG = gScore[current] + 1 + Mathf.Max(0, jumpCost - 1);
                if (gScore.TryGetValue(neighbor, out int existing) && tentativeG >= existing) continue;

                cameFrom[neighbor] = current;
                gScore[neighbor]   = tentativeG;
                fScore[neighbor]   = tentativeG + ManhattanDistance(neighbor, end);
                if (!open.Contains(neighbor)) open.Add(neighbor);
            }
        }

        return new List<Vector2Int>(); // no path found
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    public static int ManhattanDistance(Vector2Int a, Vector2Int b) =>
        Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

    // O(n) linear scan of the open set. Fine for our small grids (~14x12); swap
    // for a min-heap priority queue if grid sizes grow significantly.
    Vector2Int LowestF(List<Vector2Int> open, Dictionary<Vector2Int, int> fScore)
    {
        Vector2Int best = open[0];
        int bestF = fScore.TryGetValue(best, out int bv) ? bv : int.MaxValue;
        foreach (var p in open)
        {
            int f = fScore.TryGetValue(p, out int v) ? v : int.MaxValue;
            if (f < bestF) { bestF = f; best = p; }
        }
        return best;
    }

    List<Vector2Int> ReconstructPath(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int current)
    {
        var path = new List<Vector2Int> { current };
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Insert(0, current);
        }
        return path;
    }

    // ── Occupancy ─────────────────────────────────────────────────────────────

    public void PlaceUnit(BattleUnit unit, Vector2Int pos)
    {
        var cell = GetCell(pos);
        if (cell == null) return;
        cell.occupant      = unit;
        unit.gridPosition  = pos;
        unit.transform.position = GridToWorld(pos, cell.elevation);
    }

    public void MoveUnit(BattleUnit unit, Vector2Int newPos)
    {
        var oldCell = GetCell(unit.gridPosition);
        if (oldCell != null) oldCell.occupant = null;
        PlaceUnit(unit, newPos);
    }

    public void RemoveUnit(BattleUnit unit)
    {
        var cell = GetCell(unit.gridPosition);
        if (cell != null && cell.occupant == unit) cell.occupant = null;
    }
}
