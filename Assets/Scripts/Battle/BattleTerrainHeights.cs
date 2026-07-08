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

    [Tooltip("Sub-cell surface grid: (width*res+1)*(height*res+1) samples")]
    public int res = 4;
    public float[] surfY;

    public float HeightAt(Vector2Int cell)
    {
        if (cellY == null || cellY.Length == 0) return 0f;
        int x = Mathf.Clamp(cell.x, 0, width - 1);
        int y = Mathf.Clamp(cell.y, 0, height - 1);
        return cellY[x + y * width];
    }

    // Bilinear surface height at any world XZ inside the playfield —
    // conforming highlights and future VFX sample this, no raycasts.
    public float SurfaceHeight(float wx, float wz)
    {
        if (surfY == null || surfY.Length == 0) return 0f;
        int nx = width * res + 1;
        float gx = Mathf.Clamp(wx, 0f, width) * res;
        float gz = Mathf.Clamp(wz, 0f, height) * res;
        int x0 = Mathf.Min((int)gx, width * res - 1);
        int z0 = Mathf.Min((int)gz, height * res - 1);
        float fx = gx - x0, fz = gz - z0;
        float h00 = surfY[x0 + z0 * nx],       h10 = surfY[x0 + 1 + z0 * nx];
        float h01 = surfY[x0 + (z0 + 1) * nx], h11 = surfY[x0 + 1 + (z0 + 1) * nx];
        return Mathf.Lerp(Mathf.Lerp(h00, h10, fx), Mathf.Lerp(h01, h11, fx), fz);
    }
}
