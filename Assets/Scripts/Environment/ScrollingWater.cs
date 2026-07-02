using UnityEngine;

// Scrolls the water texture so a river reads as flowing.
// Used by the Arno on MercatoVecchio's south edge (and any future water plane —
// drop it on the renderer and set flowSpeed).
[RequireComponent(typeof(Renderer))]
public class ScrollingWater : MonoBehaviour
{
    [Tooltip("UV offset per second. X = along the river. The Arno flows " +
             "east→west through Florence, so X is negative by default.")]
    public Vector2 flowSpeed = new Vector2(-0.02f, 0.006f);

    private Material _mat;   // runtime instance — never dirties the shared asset
    private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");

    void Awake()
    {
        _mat = GetComponent<Renderer>().material;
    }

    void Update()
    {
        Vector2 off = _mat.GetTextureOffset(BaseMap);
        off += flowSpeed * Time.deltaTime;
        // wrap so the offset never grows unbounded (float precision on long sessions)
        off.x = Mathf.Repeat(off.x, 1f);
        off.y = Mathf.Repeat(off.y, 1f);
        _mat.SetTextureOffset(BaseMap, off);
    }
}
