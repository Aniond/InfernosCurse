using UnityEngine;

// Baked cell-center surface heights for 3D diorama battle maps.
// Presence of this component on the map prefab is what flips BattleGrid
// into 3D-XZ mode (absent = legacy 2D iso sprite map). Heights are baked
// by BattleMapMeshBuilder from the SAME height function that generated the
// terrain mesh, so units stand exactly on the visual surface — micro-relief
// included — with no runtime raycasts.
public class BattleTerrainHeights : MonoBehaviour
{
    public int width;
    public int height;
    [Tooltip("Surface Y at each cell center, index = x + y * width")]
    public float[] cellY;

    public float HeightAt(Vector2Int cell)
    {
        if (cellY == null || cellY.Length == 0) return 0f;
        int x = Mathf.Clamp(cell.x, 0, width - 1);
        int y = Mathf.Clamp(cell.y, 0, height - 1);
        return cellY[x + y * width];
    }
}
