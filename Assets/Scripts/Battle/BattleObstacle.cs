using UnityEngine;

// An obstacle prop inside a battle-map prefab. BattleMapAuthoring stamps it
// into the grid: unwalkable cover (default), or standable height if you set
// addedElevation and clear makeUnwalkable (a crate you can climb via Jump).
// "Snap To Cell" places the prop's sprite at its cell's iso position.
public class BattleObstacle : MonoBehaviour
{
    public Vector2Int cell;
    [Tooltip("Blocks movement through this cell (cover). Elevation 2+ also blocks LoS.")]
    public bool makeUnwalkable = true;
    [Tooltip("Half-units added to the cell's elevation (2 = one story — blocks LoS).")]
    public int addedElevation = 2;

    [ContextMenu("Snap To Cell")]
    public void SnapToCell()
    {
        var map = GetComponentInParent<BattleMapAuthoring>();
        if (map == null) { Debug.LogWarning("[BattleObstacle] Not under a BattleMapAuthoring."); return; }
        transform.position = map.CellToWorld(cell, map.ElevationAt(cell));
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        var map = GetComponentInParent<BattleMapAuthoring>();
        if (map == null) return;
        Gizmos.color = makeUnwalkable ? new Color(1f, 0.3f, 0.2f, 0.8f) : new Color(0.3f, 0.8f, 1f, 0.8f);
        Gizmos.DrawWireCube(map.CellToWorld(cell, map.ElevationAt(cell)),
            new Vector3(map.tileWidth * 0.5f, map.tileHeight * 0.5f, 0.1f));
    }
#endif
}
