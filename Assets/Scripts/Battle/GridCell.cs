using System;
using UnityEngine;

// APPEND ONLY: authored maps serialize these numeric values.
public enum TileType { Normal, Water, Fire, Ice, Poison, Holy, Void, LimboStain, GraveMulch }

[Flags]
public enum GridEdgeBlock
{
    None = 0,
    North = 1 << 0,
    East = 1 << 1,
    South = 1 << 2,
    West = 1 << 3,
}

[System.Serializable]
public class GridCell
{
    public Vector2Int gridPos;
    public int elevation;           // height in half-units (0 = ground, 2 = one story up)
    public bool walkable;
    public TileType tileType;
    public BattleUnit occupant;     // null if empty

    [Header("Tactical Protection")]
    public bool reserved;
    public bool objective;
    public bool protectedTerrain;
    [Min(0)] public int authoredCorruptionStrength;
    public GridEdgeBlock impassableEdges;

    public GridCell(Vector2Int pos, int elev = 0, bool walk = true, TileType type = TileType.Normal)
    {
        gridPos   = pos;
        elevation = elev;
        walkable  = walk;
        tileType  = type;
        occupant  = null;
    }

    public bool IsOccupied => occupant != null;

    public bool BlocksEdge(Vector2Int direction)
    {
        GridEdgeBlock edge = direction switch
        {
            var d when d == Vector2Int.up => GridEdgeBlock.North,
            var d when d == Vector2Int.right => GridEdgeBlock.East,
            var d when d == Vector2Int.down => GridEdgeBlock.South,
            var d when d == Vector2Int.left => GridEdgeBlock.West,
            _ => GridEdgeBlock.None,
        };
        return edge != GridEdgeBlock.None && (impassableEdges & edge) != 0;
    }

    // Height difference a unit must jump to enter this cell from another
    public int JumpCostFrom(GridCell other)
    {
        return Mathf.Max(0, elevation - other.elevation);
    }
}
