using UnityEngine;

// Curse rendered IN the terrain on 3D diorama maps: owns a small R8 mask
// texture (one texel per grid cell, bilinear = soft creep) that the splat
// shader blends toward the curse palette. CurseOverlay forwards densities
// here instead of spawning sprites when the map is 3D. Curse is never shown
// as a number — this is corruption spreading through the earth.
[RequireComponent(typeof(BattleTerrainHeights))]
public class BattleTerrainCurse : MonoBehaviour
{
    Texture2D _mask;
    MaterialPropertyBlock _mpb;
    Renderer _terrain;
    int _w, _h;
    bool _dirty;

    static readonly int CurseMaskId = Shader.PropertyToID("_CurseMask");
    static readonly int CurseRectId = Shader.PropertyToID("_CurseRect");

    void Awake()
    {
        var heights = GetComponent<BattleTerrainHeights>();
        _w = heights.width; _h = heights.height;
        _mask = new Texture2D(_w, _h, TextureFormat.R8, false, true)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
        var clear = new byte[_w * _h];
        _mask.SetPixelData(clear, 0);
        _mask.Apply(false, false);

        _terrain = GetComponentInChildren<Renderer>();
        _mpb = new MaterialPropertyBlock();
        Push();
    }

    public void SetDensity(Vector2Int cell, float density)
    {
        if (_mask == null) return;
        if (cell.x < 0 || cell.x >= _w || cell.y < 0 || cell.y >= _h) return;
        _mask.SetPixel(cell.x, cell.y, new Color(Mathf.Clamp01(density), 0f, 0f, 1f));
        _dirty = true;
    }

    void LateUpdate()
    {
        if (!_dirty) return;
        _dirty = false;
        _mask.Apply(false, false);
        Push();
    }

    void Push()
    {
        if (_terrain == null) return;
        _terrain.GetPropertyBlock(_mpb);
        _mpb.SetTexture(CurseMaskId, _mask);
        _mpb.SetVector(CurseRectId, new Vector4(0f, 0f, _w, _h));
        _terrain.SetPropertyBlock(_mpb);
    }
}
