using UnityEngine;

// Line-of-sight fog of war for grid-authored zones (zones=battlemaps pilot):
// the player only sees cells the GRID says they can see — walls, the tree
// border, hedges-as-cover and the terrace lip all block vision via
// BattleGrid.HasLineOfSight (elevation 2+ blocks). Visibility is written to
// BattleTerrainFog's mask; a ground-conforming overlay mesh renders the veil.
// Runs from the player in explore mode; battles can later drive it from
// party units instead.
[RequireComponent(typeof(BattleMapAuthoring), typeof(BattleTerrainFog), typeof(BattleTerrainHeights))]
public class ZoneFogOfWar : MonoBehaviour
{
    [Tooltip("Vision radius in cells (grid = meters).")]
    public float sightRange = 13f;
    [Tooltip("Character eye height in elevation half-units (2 ≈ 1m over own ground). Objects taller than this block sight.")]
    public int eyeHeight = 3;
    [Tooltip("Seconds between visibility recomputes.")]
    public float updateInterval = 0.25f;
    public Material fogMaterial;

    // Vision spells (append-only): Clairvoyance reveals the WHOLE field
    // (no range, no LoS); TrueSight sees THROUGH objects (LoS ignored,
    // range still applies). Cast via CastVision — the skill system applies
    // these as timed statuses (David 7/08: makes the spell really valuable).
    // Shrink: your eyes drop and your horizon shortens — but your PROFILE
    // shrinks too: once enemy vision runs on this system, low cover that
    // wouldn't hide a standing unit hides a shrunken one (tactical stealth).
    public enum VisionSpell { None, Clairvoyance, TrueSight, Enlarge, Shrink }

    VisionSpell _activeSpell = VisionSpell.None;
    float _spellRemaining;

    BattleGrid _grid;
    BattleTerrainFog _fog;
    BattleTerrainHeights _heights;
    Transform _player;
    float _timer;
    Vector2Int _lastCell = new Vector2Int(-999, -999);

    public void CastVision(VisionSpell spell, float durationSeconds)
    {
        _activeSpell = spell;
        _spellRemaining = durationSeconds;
        _lastCell = new Vector2Int(-999, -999);   // force recompute
    }

    [ContextMenu("Test: Clairvoyance 10s")]
    void TestClairvoyance() => CastVision(VisionSpell.Clairvoyance, 10f);
    [ContextMenu("Test: True Sight 10s")]
    void TestTrueSight() => CastVision(VisionSpell.TrueSight, 10f);
    [ContextMenu("Test: Enlarge 10s")]
    void TestEnlarge() => CastVision(VisionSpell.Enlarge, 10f);
    [ContextMenu("Test: Shrink 10s")]
    void TestShrink() => CastVision(VisionSpell.Shrink, 10f);

    void Start()
    {
        // Prefer the scene's real BattleGrid (arena — already stamped by
        // authoring on Awake, includes obstacles); otherwise build a hidden
        // one from our own authoring (explore zones, applyOnAwake=false).
        _grid = FindFirstObjectByType<BattleGrid>();
        if (_grid == null)
        {
            var auth = GetComponent<BattleMapAuthoring>();
            _grid = gameObject.AddComponent<BattleGrid>();
            auth.Apply(_grid);
        }
        _fog = GetComponent<BattleTerrainFog>();
        _heights = GetComponent<BattleTerrainHeights>();
        var playerGo = GameObject.FindWithTag("Player");
        _player = playerGo != null ? playerGo.transform : null;
        BuildOverlay();
        Recompute(force: true);
    }

    void Update()
    {
        if (_activeSpell != VisionSpell.None)
        {
            _spellRemaining -= Time.deltaTime;
            if (_spellRemaining <= 0f)
            {
                _activeSpell = VisionSpell.None;
                _lastCell = new Vector2Int(-999, -999);   // fog rolls back in
            }
        }
        _timer += Time.deltaTime;
        if (_timer < updateInterval) return;
        _timer = 0f;

        // weather shifts change sight range — force the veil to follow
        float wm = WeatherVision.SightMultiplier();
        bool weatherChanged = !Mathf.Approximately(wm, _lastWeatherMult);
        _lastWeatherMult = wm;

        Recompute(force: weatherChanged);
    }

    float _lastWeatherMult = 1f;

    bool[,] _lastVisible;

    // CT sidebar + spotting queries read this (last computed visibility).
    public bool IsCellVisible(Vector2Int cell)
    {
        if (_lastVisible == null) return true;   // fog not computed yet — fail open
        if (cell.x < 0 || cell.x >= _grid.width || cell.y < 0 || cell.y >= _grid.height) return false;
        return _lastVisible[cell.x, cell.y];
    }

    void Recompute(bool force)
    {
        if (_grid == null) return;

        // BATTLE MODE: when player-side BattleUnits exist, vision is the
        // UNION of the party's eyes — each unit sees with its own SHEET
        // stats (CombatantData.sightRange/eyeHeight). Explore mode falls
        // back to the player transform below.
        var units = Object.FindObjectsByType<BattleUnit>(FindObjectsSortMode.None);
        var party = System.Array.FindAll(units, u => u.IsPlayer && u.state != UnitState.Dead);
        if (party.Length > 0) { RecomputeFromParty(party); return; }

        if (_player == null) return;
        var pc = new Vector2Int(Mathf.FloorToInt(_player.position.x),
                                Mathf.FloorToInt(_player.position.z));
        if (!force && pc == _lastCell) return;
        _lastCell = pc;

        // Enlarge: a bigger body means higher eyes and a longer horizon —
        // low cover (hedges, boulders) stops hiding things from you.
        int eye = _activeSpell switch
        {
            VisionSpell.Enlarge => eyeHeight + 2,
            VisionSpell.Shrink => Mathf.Max(1, eyeHeight - 2),
            _ => eyeHeight,
        };
        float range = _activeSpell switch
        {
            VisionSpell.Enlarge => sightRange * 1.35f,
            VisionSpell.Shrink => sightRange * 0.75f,
            _ => sightRange,
        };
        range *= WeatherVision.SightMultiplier();   // COZY fog/storm closes the world in
        float r2 = range * range;
        var visible = new bool[_grid.width, _grid.height];
        for (int z = 0; z < _grid.height; z++)
            for (int x = 0; x < _grid.width; x++)
            {
                var c = new Vector2Int(x, z);
                float dx = x - pc.x, dz = z - pc.y;
                bool inRange = dx * dx + dz * dz <= r2;
                bool vis = _activeSpell switch
                {
                    VisionSpell.Clairvoyance => true,                       // whole field
                    VisionSpell.TrueSight => inRange,                       // through objects
                    _ => inRange && _grid.HasLineOfSight(pc, c, eye),       // honest eyes
                };
                visible[x, z] = vis;
                _fog.SetVisible(x, z, vis);
            }
        _lastVisible = visible;

        // The rule (David 7/08): you can't use the camera to scout what your
        // character couldn't see — NPCs and enemy units in fogged cells hide
        // entirely; step out from cover to reveal them.
        foreach (var unit in Object.FindObjectsByType<BattleUnit>(FindObjectsSortMode.None))
            SetFogHidden(unit.gameObject, visible);
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            if (t.parent == null && t.name.StartsWith("NPC_"))
                SetFogHidden(t.gameObject, visible);
    }

    void RecomputeFromParty(BattleUnit[] party)
    {
        var visible = new bool[_grid.width, _grid.height];
        for (int z = 0; z < _grid.height; z++)
            for (int x = 0; x < _grid.width; x++)
            {
                var c = new Vector2Int(x, z);
                bool vis = _activeSpell == VisionSpell.Clairvoyance;
                if (!vis)
                    foreach (var u in party)
                    {
                        float dx = x - u.gridPosition.x, dz = z - u.gridPosition.y;
                        float r = u.SightRange * WeatherVision.SightMultiplier();
                        if (dx * dx + dz * dz > r * r) continue;
                        if (_activeSpell == VisionSpell.TrueSight ||
                            _grid.HasLineOfSight(u.gridPosition, c, u.EyeHeight)) { vis = true; break; }
                    }
                visible[x, z] = vis;
                _fog.SetVisible(x, z, vis);
            }
        _lastVisible = visible;

        foreach (var unit in Object.FindObjectsByType<BattleUnit>(FindObjectsSortMode.None))
            if (!unit.IsPlayer) SetFogHidden(unit.gameObject, visible);
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            if (t.parent == null && t.name.StartsWith("NPC_"))
                SetFogHidden(t.gameObject, visible);
    }

    void SetFogHidden(GameObject go, bool[,] visible)
    {
        int x = Mathf.FloorToInt(go.transform.position.x);
        int z = Mathf.FloorToInt(go.transform.position.z);
        bool vis = x >= 0 && x < _grid.width && z >= 0 && z < _grid.height && visible[x, z];
        foreach (var r in go.GetComponentsInChildren<Renderer>())
            r.enabled = vis;
    }

    // Ground-conforming veil mesh over the whole grid (2 verts per cell edge).
    void BuildOverlay()
    {
        int W = _heights.width, H = _heights.height, res = Mathf.Max(1, _heights.res);
        int nx = W * res + 1, nz = H * res + 1;
        var verts = new Vector3[nx * nz];
        for (int iz = 0; iz < nz; iz++)
            for (int ix = 0; ix < nx; ix++)
            {
                float wx = ix / (float)res, wz = iz / (float)res;
                verts[ix + iz * nx] = new Vector3(wx, _heights.SurfaceHeight(wx, wz) + 0.15f, wz);
            }
        var tris = new int[(nx - 1) * (nz - 1) * 6];
        int t = 0;
        for (int iz = 0; iz < nz - 1; iz++)
            for (int ix = 0; ix < nx - 1; ix++)
            {
                int a = ix + iz * nx, b = a + 1, c = a + nx, d = c + 1;
                tris[t++] = a; tris[t++] = c; tris[t++] = b;
                tris[t++] = b; tris[t++] = c; tris[t++] = d;
            }
        var mesh = new Mesh { name = "FogVeil", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.RecalculateBounds();

        var go = new GameObject("FogVeil");
        go.transform.SetParent(transform, false);
        go.AddComponent<MeshFilter>().mesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        var mat = fogMaterial != null ? new Material(fogMaterial) : new Material(Shader.Find("InfernosCurse/ZoneFog"));
        mat.SetVector("_FogRect", new Vector4(0f, 0f, W, H));
        mat.SetTexture("_FogMask", _fog.Mask);
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }
}
