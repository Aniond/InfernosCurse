using UnityEngine;
using System.Collections.Generic;

// Visualises curse density on the battle grid using a pool of overlay sprites.
// One CurseOverlay lives in the battle scene. BattleCurseAutomata.OnCellCurseChanged
// drives it — no polling.
public class CurseOverlay : MonoBehaviour
{
    [Header("References")]
    public BattleGrid         grid;
    public BattleCurseAutomata automata;

    [Header("Overlay Sprite")]
    public Sprite overlaySprite;
    public Color  lowCurseColor  = new Color(0.4f, 0.0f, 0.6f, 0.0f);   // fully transparent
    public Color  highCurseColor = new Color(0.4f, 0.0f, 0.6f, 0.65f);  // purple, 65% opacity
    public int    sortingOrder   = 1;

    private Dictionary<Vector2Int, SpriteRenderer> _overlays = new();
    private BattleTerrainCurse _terrainCurse;   // 3D diorama maps: curse lives IN the terrain shader

    void OnEnable()
    {
        if (!GameFeatures.CorruptionEnabled) return;
        if (automata != null)
            automata.OnCellCurseChanged += OnCellChanged;
    }

    void OnDisable()
    {
        if (automata != null)
            automata.OnCellCurseChanged -= OnCellChanged;
    }

    // Call once at battle start to build the pool
    public void Initialise(InfernalWorldState world)
    {
        Clear();
        if (!GameFeatures.CorruptionEnabled) return;
        _terrainCurse = FindFirstObjectByType<BattleTerrainCurse>();
        for (int x = 0; x < grid.width; x++)
        {
            for (int y = 0; y < grid.height; y++)
            {
                var pos = new Vector2Int(x, y);
                float density = world.GetCurseDensity(pos);
                if (_terrainCurse != null) { _terrainCurse.SetDensity(pos, density); continue; }
                CreateOverlay(pos);
                SetOverlayColor(pos, density);
            }
        }
    }

    void OnCellChanged(Vector2Int pos, float density)
    {
        if (!GameFeatures.CorruptionEnabled) return;
        if (_terrainCurse != null) { _terrainCurse.SetDensity(pos, density); return; }
        if (!_overlays.ContainsKey(pos)) CreateOverlay(pos);
        SetOverlayColor(pos, density);
    }

    void CreateOverlay(Vector2Int pos)
    {
        var go = new GameObject($"CurseOverlay_{pos.x}_{pos.y}");
        go.transform.SetParent(transform);

        var cell = grid.GetCell(pos);
        go.transform.position = grid.GridToWorld(pos, cell?.elevation ?? 0) + new Vector3(0f, 0.01f, 0f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite       = overlaySprite;
        sr.color        = lowCurseColor;
        sr.sortingOrder = sortingOrder;

        _overlays[pos] = sr;
    }

    void SetOverlayColor(Vector2Int pos, float density)
    {
        if (!_overlays.TryGetValue(pos, out var sr)) return;
        sr.color = Color.Lerp(lowCurseColor, highCurseColor, density);
    }

    void Clear()
    {
        foreach (var sr in _overlays.Values)
            if (sr != null) Destroy(sr.gameObject);
        _overlays.Clear();
    }
}
