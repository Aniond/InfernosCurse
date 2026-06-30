using UnityEngine;

public enum TileType { Normal, Water, Fire, Ice, Poison, Holy, Void }

[System.Serializable]
public class GridCell
{
    public Vector2Int gridPos;
    public int elevation;           // height in half-units (0 = ground, 2 = one story up)
    public bool walkable;
    public TileType tileType;
    public BattleUnit occupant;     // null if empty

    public GridCell(Vector2Int pos, int elev = 0, bool walk = true, TileType type = TileType.Normal)
    {
        gridPos   = pos;
        elevation = elev;
        walkable  = walk;
        tileType  = type;
        occupant  = null;
    }

    public bool IsOccupied => occupant != null;

    // Height difference a unit must jump to enter this cell from another
    public int JumpCostFrom(GridCell other)
    {
        return Mathf.Max(0, elevation - other.elevation);
    }
}
