using UnityEngine;

// Fog-of-war mask for grid-authored zones/battle maps: one R8 texel per
// cell (1 = visible, 0 = fogged), bilinear for soft vision edges, fed to
// the terrain splat shader via property block. Twin of BattleTerrainCurse.
[RequireComponent(typeof(BattleTerrainHeights))]
public class BattleTerrainFog : MonoBehaviour
{
    Texture2D _mask;
    MaterialPropertyBlock _mpb;
    Renderer _terrain;
    int _w, _h;
    bool _dirty;
    byte[] _pixels;

    static readonly int FogMaskId = Shader.PropertyToID("_FogMask");
    static readonly int FogRectId = Shader.PropertyToID("_FogRect");

    void Awake()
    {
        var heights = GetComponent<BattleTerrainHeights>();
        _w = heights.width; _h = heights.height;
        _mask = new Texture2D(_w, _h, TextureFormat.R8, false, true)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
        _pixels = new byte[_w * _h];           // start fully fogged
        _mask.SetPixelData(_pixels, 0);
        _mask.Apply(false, false);

        // terrain may be a Unity Terrain (zones) or a MeshRenderer (dioramas)
        _terrain = GetComponentInChildren<MeshRenderer>();
        _mpb = new MaterialPropertyBlock();
        Push();
    }

    public void SetVisible(int x, int z, bool visible)
    {
        if (_pixels == null || x < 0 || x >= _w || z < 0 || z >= _h) return;
        byte v = visible ? (byte)255 : (byte)0;
        int i = x + z * _w;
        if (_pixels[i] == v) return;
        _pixels[i] = v;
        _dirty = true;
    }

    // Unity Terrain can't take a property block per-splat — expose the mask
    // so ZoneFogOfWar can push it to a material or global instead.
    public Texture2D Mask => _mask;

    void LateUpdate()
    {
        if (!_dirty) return;
        _dirty = false;
        _mask.SetPixelData(_pixels, 0);
        _mask.Apply(false, false);
        Shader.SetGlobalTexture(FogMaskId, _mask);
        Shader.SetGlobalVector(FogRectId, new Vector4(0f, 0f, _w, _h));
        Push();
    }

    void Push()
    {
        if (_terrain == null) return;
        _terrain.GetPropertyBlock(_mpb);
        _mpb.SetTexture(FogMaskId, _mask);
        _mpb.SetVector(FogRectId, new Vector4(0f, 0f, _w, _h));
        _terrain.SetPropertyBlock(_mpb);
    }
}
