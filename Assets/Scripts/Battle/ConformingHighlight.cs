using UnityEngine;

// Grid highlight that drapes over the 3D terrain: rebuilds a small
// subdivided quad whose vertices sample BattleTerrainHeights.SurfaceHeight,
// so move/attack/hover markers hug slopes and lips instead of clipping.
// BattleCursor positions the root at the cell center (GridToWorld) and
// tints via MaterialPropertyBlock (_BaseColor).
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ConformingHighlight : MonoBehaviour
{
    const int N = 6;            // quads per side
    const float SIZE = 0.96f;   // cell coverage
    const float LIFT = 0.035f;  // hover above the surface

    static BattleTerrainHeights _heights;
    Mesh _mesh;
    Vector3 _builtAt = new Vector3(float.NaN, 0, 0);

    void Awake()
    {
        _mesh = new Mesh { name = "ConformingHighlight" };
        GetComponent<MeshFilter>().mesh = _mesh;
    }

    void LateUpdate()
    {
        if (transform.position != _builtAt) Rebuild();
    }

    void Rebuild()
    {
        _builtAt = transform.position;
        if (_heights == null) _heights = FindFirstObjectByType<BattleTerrainHeights>();

        var verts = new Vector3[(N + 1) * (N + 1)];
        var uvs = new Vector2[verts.Length];
        for (int iz = 0; iz <= N; iz++)
            for (int ix = 0; ix <= N; ix++)
            {
                float lx = (ix / (float)N - 0.5f) * SIZE;
                float lz = (iz / (float)N - 0.5f) * SIZE;
                float wy = _heights != null
                    ? _heights.SurfaceHeight(_builtAt.x + lx, _builtAt.z + lz) + LIFT
                    : _builtAt.y + LIFT;
                verts[ix + iz * (N + 1)] = new Vector3(lx, wy - _builtAt.y, lz);
                uvs[ix + iz * (N + 1)] = new Vector2(ix / (float)N, iz / (float)N);
            }
        var tris = new int[N * N * 6];
        int t = 0;
        for (int iz = 0; iz < N; iz++)
            for (int ix = 0; ix < N; ix++)
            {
                int a = ix + iz * (N + 1), b = a + 1, c = a + N + 1, d = c + 1;
                tris[t++] = a; tris[t++] = c; tris[t++] = b;
                tris[t++] = b; tris[t++] = c; tris[t++] = d;
            }
        _mesh.Clear();
        _mesh.vertices = verts;
        _mesh.uv = uvs;
        _mesh.triangles = tris;
        _mesh.RecalculateBounds();
    }
}
