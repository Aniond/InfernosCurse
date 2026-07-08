using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// The zones=battlemaps encounter trigger: staged enemies (BattleUnits placed
// in the zone, like the Rose Beast in the flowerbed) AMBUSH the player the
// moment they can SEE them — sheet sight range x weather, true LoS at eye
// height. On trigger: spawn the BattleKit prefab, stamp the zone's authored
// grid, freeze the explore player, and StartBattle right where you stand.
// Victory/defeat tears the kit down and hands the garden back.
public class ZoneEncounterTrigger : MonoBehaviour
{
    [Tooltip("The BattleKit prefab (InfernosCurse/Zones/1 builds it from BattleArena).")]
    public GameObject battleKitPrefab;
    [Tooltip("Extra ambush radius even without LoS (creature senses you brushing past).")]
    public float proximityTrigger = 2.5f;

    BattleMapAuthoring _auth;
    BattleTerrainHeights _heights;
    GameObject _kitInstance;
    GameObject _explorePlayer;
    List<BattleUnit> _staged = new();
    bool _battleRunning;
    float _poll;

    void Start()
    {
        _auth = GetComponent<BattleMapAuthoring>();
        _heights = GetComponent<BattleTerrainHeights>();
        _staged = Object.FindObjectsByType<BattleUnit>(FindObjectsSortMode.None)
            .Where(u => !u.IsPlayer).ToList();
    }

    void Update()
    {
        if (_battleRunning || battleKitPrefab == null || _staged.Count == 0) return;
        _poll += Time.deltaTime;
        if (_poll < 0.3f) return;
        _poll = 0f;

        var player = GameObject.FindWithTag("Player");
        if (player == null) return;
        var pc = new Vector2Int(Mathf.FloorToInt(player.transform.position.x),
                                Mathf.FloorToInt(player.transform.position.z));

        foreach (var enemy in _staged)
        {
            if (enemy == null) continue;
            float dx = enemy.gridPosition.x - pc.x, dz = enemy.gridPosition.y - pc.y;
            float d2 = dx * dx + dz * dz;
            float sight = enemy.SightRange * WeatherVision.SightMultiplier();
            bool sees = d2 <= sight * sight;   // LoS refined below with a real grid
            if (d2 <= proximityTrigger * proximityTrigger || sees)
            {
                StartEncounter(pc);
                return;
            }
        }
    }

    void StartEncounter(Vector2Int playerCell)
    {
        _battleRunning = true;
        Debug.Log("[ZoneEncounter] AMBUSH — battle begins in place.");

        // battle UI needs an EventSystem — zones don't always carry one
        if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));

        _kitInstance = Instantiate(battleKitPrefab);
        var bm = _kitInstance.GetComponentInChildren<BattleManager>();
        var grid = _kitInstance.GetComponentInChildren<BattleGrid>();
        _auth.Apply(grid);   // stamp the zone's authored cells into the kit's grid

        // freeze + hide the explore player; battle-Benidito takes over
        _explorePlayer = GameObject.FindWithTag("Player");
        if (_explorePlayer != null) _explorePlayer.SetActive(false);

        // camera: FFT rig orbiting the ambush site (Q/E works in-zone)
        var cam = Camera.main;
        if (cam != null)
        {
            var rig = cam.GetComponent<BattleCameraRig>();
            if (rig == null) rig = cam.gameObject.AddComponent<BattleCameraRig>();
            var mid = Vector2.Lerp(playerCell, _staged[0].gridPosition, 0.5f);
            rig.pivot = new Vector3(mid.x, _heights != null ? _heights.HeightAt(playerCell) : 1f, mid.y);
            cam.transform.position = rig.pivot + new Vector3(0.55f, 0.6f, -1f).normalized * 22f;
            cam.transform.LookAt(rig.pivot);
        }

        // party from the roster; enemies from the staged units' sheets
        PartyRoster.EnsureInitialized();
        var party = new List<CombatantData>(RestSystem.PartyMembers);
        var enemySheets = _staged.Where(u => u != null).Select(u => u.Data).ToList();

        var playerSpawns = SpawnsAround(playerCell, party.Count, grid);
        var enemySpawns = new List<Vector2Int>();
        foreach (var u in _staged.Where(u => u != null))
            enemySpawns.Add(u.gridPosition);
        while (enemySpawns.Count < enemySheets.Count)
            enemySpawns.Add(enemySpawns[0] + Vector2Int.right);

        // staged visuals hand over to freshly spawned battle units
        var stagedSheets = _staged.Where(u => u != null)
            .ToDictionary(u => u.Data, u => u.GetComponentInChildren<SpriteRenderer>()?.sprite);
        foreach (var u in _staged.Where(u => u != null)) Destroy(u.gameObject);
        _staged.Clear();

        bm.OnVictory += EndEncounter;
        bm.OnDefeat += EndEncounter;
        bm.StartBattle(party, enemySheets, playerSpawns, enemySpawns);

        // pilot cosmetics: give spawned enemies their staged sprite (per-unit
        // battle sprites are the queued proper fix)
        foreach (var unit in Object.FindObjectsByType<BattleUnit>(FindObjectsSortMode.None))
            if (!unit.IsPlayer && unit.Data != null &&
                stagedSheets.TryGetValue(unit.Data, out var spr) && spr != null)
            {
                var sr = unit.GetComponentInChildren<SpriteRenderer>();
                if (sr != null) sr.sprite = spr;
            }
    }

    List<Vector2Int> SpawnsAround(Vector2Int center, int count, BattleGrid grid)
    {
        var spawns = new List<Vector2Int>();
        for (int ring = 0; ring <= 3 && spawns.Count < count; ring++)
            for (int dx = -ring; dx <= ring && spawns.Count < count; dx++)
                for (int dz = -ring; dz <= ring && spawns.Count < count; dz++)
                {
                    var c = center + new Vector2Int(dx, dz);
                    var cell = grid.GetCell(c);
                    if (cell != null && cell.walkable && !spawns.Contains(c)) spawns.Add(c);
                }
        return spawns;
    }

    void EndEncounter()
    {
        var bm = _kitInstance != null ? _kitInstance.GetComponentInChildren<BattleManager>() : null;
        if (bm != null) { bm.OnVictory -= EndEncounter; bm.OnDefeat -= EndEncounter; }

        // battle units belong to the kit's battle — clear survivors
        foreach (var u in Object.FindObjectsByType<BattleUnit>(FindObjectsSortMode.None))
            Destroy(u.gameObject);
        if (_kitInstance != null) Destroy(_kitInstance, 0.5f);

        var cam = Camera.main;
        var rig = cam != null ? cam.GetComponent<BattleCameraRig>() : null;
        if (rig != null) Destroy(rig);

        if (_explorePlayer != null) _explorePlayer.SetActive(true);
        _battleRunning = false;
        Debug.Log("[ZoneEncounter] Battle over — the garden is yours again.");
    }
}
