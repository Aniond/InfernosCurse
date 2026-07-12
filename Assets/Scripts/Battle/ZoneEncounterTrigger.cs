using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

// Starts tactical combat inside an explorable zone. The visible environment is
// never replaced: exploration control is suspended, the authored grid is
// stamped into a temporary BattleKit, and the shared camera changes owner.
[DisallowMultipleComponent]
public class ZoneEncounterTrigger : MonoBehaviour
{
    [Tooltip("The BattleKit prefab (InfernosCurse/Zones/1 builds it from BattleArena).")]
    public GameObject battleKitPrefab;
    [Tooltip("Extra ambush radius even without LoS (creature senses you brushing past).")]
    public float proximityTrigger = 2.5f;
    [Tooltip("Seconds the fog parts over each ambusher at battle start. 0 = no reveal.")]
    public float ambushRevealSeconds = 8f;

    ZoneBattleAuthoring _zone;
    BattleMapAuthoring _auth;
    BattleTerrainHeights _heights;
    CinemachineBrain _cineBrain;
    BattleCameraRig _battleRig;
    BattleManager _battleManager;
    GameObject _kitInstance;
    GameObject _explorePlayer;
    GameObject _createdEventSystem;
    Vector3 _explorePlayerPosition;
    Quaternion _explorePlayerRotation;
    bool _playerWasActive;
    bool _brainWasEnabled;
    bool _ownsBattleRig;
    bool[] _exitStates = Array.Empty<bool>();
    bool[] _exploreRootStates = Array.Empty<bool>();
    bool[] _battleRootStates = Array.Empty<bool>();
    List<BattleUnit> _staged = new();
    WorldAgentEncounterActor _activeWorldEncounter;
    bool _restoreWorldEncounterActor;
    bool _explorationCaptured;
    bool _battleRunning;
    float _poll;

    public bool BattleRunning => _battleRunning;
    public BattleManager ActiveBattleManager => _battleManager;

    void Start()
    {
        _zone = GetComponent<ZoneBattleAuthoring>();
        _auth = GetComponent<BattleMapAuthoring>();
        _heights = GetComponent<BattleTerrainHeights>();
        _explorePlayer = GameObject.FindWithTag("Player");
        _staged = UnityEngine.Object.FindObjectsByType<BattleUnit>(FindObjectsSortMode.None)
            .Where(u => !u.IsPlayer).ToList();

        // Staged ambushers are encounter data, not exploration actors. Their
        // first visible frame is the battle reveal.
        foreach (var unit in _staged)
        {
            unit.stagedAmbusher = true;
            foreach (var renderer in unit.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.gameObject != unit.gameObject) renderer.gameObject.SetActive(false);
                else renderer.enabled = false;
            }
        }
    }

    void Update()
    {
        if (_battleRunning || _zone == null || !_zone.combatAllowed || _staged.Count == 0) return;
        _poll += Time.deltaTime;
        if (_poll < 0.3f) return;
        _poll = 0f;

        var player = _explorePlayer;
        if (player == null) return;
        var playerCell = _auth != null
            ? _auth.WorldToCell(player.transform.position)
            : new Vector2Int(Mathf.FloorToInt(player.transform.position.x),
                             Mathf.FloorToInt(player.transform.position.z));

        foreach (var enemy in _staged)
        {
            if (enemy == null) continue;
            float dx = enemy.gridPosition.x - playerCell.x;
            float dz = enemy.gridPosition.y - playerCell.y;
            float distanceSquared = dx * dx + dz * dz;
            float sight = enemy.SightRange * WeatherVision.SightMultiplier();
            if (distanceSquared <= proximityTrigger * proximityTrigger ||
                distanceSquared <= sight * sight)
            {
                StartEncounter(playerCell);
                return;
            }
        }
    }

    void StartEncounter(Vector2Int playerCell)
    {
        if (!ValidateStartup(out string validationError))
        {
            Debug.LogError($"[ZoneEncounter] Cannot start encounter: {validationError}", this);
            return;
        }

        _battleRunning = true;
        try
        {
            _kitInstance = Instantiate(battleKitPrefab);
            _battleManager = _kitInstance.GetComponentInChildren<BattleManager>(true);
            var grid = _kitInstance.GetComponentInChildren<BattleGrid>(true);
            if (_battleManager == null || grid == null)
                throw new InvalidOperationException("BattleKit is missing BattleManager or BattleGrid");

            _auth.Apply(grid);
            PartyRoster.EnsureInitialized();
            var party = new List<CombatantData>(RestSystem.PartyMembers);
            var enemies = _staged.Where(u => u != null).Select(u => u.Data).ToList();
            if (party.Count == 0) throw new InvalidOperationException("party roster is empty");
            if (enemies.Count == 0 || enemies.Any(data => data == null))
                throw new InvalidOperationException("staged enemies have no combatant data");

            var playerSpawns = SpawnsAround(playerCell, party.Count, grid);
            if (playerSpawns.Count < party.Count)
                throw new InvalidOperationException($"only {playerSpawns.Count}/{party.Count} valid party spawn cells near {playerCell}");

            var enemySpawns = _staged.Where(u => u != null).Select(u => u.gridPosition).ToList();
            if (enemySpawns.Any(cell => grid.GetCell(cell) == null || !grid.GetCell(cell).walkable))
                throw new InvalidOperationException("one or more staged enemies occupy invalid or blocked cells");

            EnsureEventSystem();
            CaptureAndSuspendExploration();
            ConfigureBattleCamera(playerCell, enemySpawns[0], grid);

            _battleManager.OnVictory += HandleVictory;
            _battleManager.OnDefeat += HandleDefeat;
            _battleManager.StartBattle(party, enemies, playerSpawns, enemySpawns);

            foreach (var unit in _staged.Where(u => u != null)) Destroy(unit.gameObject);
            _staged.Clear();

            if (EnemyAI.WorldState != null)
                foreach (var unit in UnityEngine.Object.FindObjectsByType<BattleUnit>(FindObjectsSortMode.None))
                    if (unit.IsPlayer) EnemyAI.WorldState.playerBelief.Update(unit);

            var fog = GetComponent<ZoneFogOfWar>();
            if (fog != null && ambushRevealSeconds > 0f)
                foreach (var spawn in enemySpawns)
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dz = -1; dz <= 1; dz++)
                            fog.RevealCell(spawn + new Vector2Int(dx, dz), ambushRevealSeconds);

            Debug.Log("[ZoneEncounter] AMBUSH — battle begins in place.");
        }
        catch (Exception exception)
        {
            Debug.LogError($"[ZoneEncounter] Startup failed; exploration restored. {exception.Message}", this);
            RestoreExploration(clearBattleUnits: true);
        }
    }

    public bool TryStartWorldAgentEncounter(WorldAgentEncounterActor encounter, GameObject interactor)
    {
        if (_battleRunning || encounter == null) return false;
        if (interactor != null) _explorePlayer = interactor;
        if (!ValidateStartup(out string validationError, requireStagedEnemies: false))
        {
            Debug.LogError($"[ZoneEncounter] Cannot start world-agent encounter: {validationError}", this);
            return false;
        }

        var enemies = encounter.BuildEnemyParty();
        if (enemies.Count == 0 || enemies.Any(data => data == null))
        {
            Debug.LogError("[ZoneEncounter] World-agent encounter has an incomplete enemy composition.", encounter);
            return false;
        }

        _battleRunning = true;
        _activeWorldEncounter = encounter;
        _restoreWorldEncounterActor = true;
        try
        {
            _kitInstance = Instantiate(battleKitPrefab);
            _battleManager = _kitInstance.GetComponentInChildren<BattleManager>(true);
            var grid = _kitInstance.GetComponentInChildren<BattleGrid>(true);
            if (_battleManager == null || grid == null)
                throw new InvalidOperationException("BattleKit is missing BattleManager or BattleGrid");

            _auth.Apply(grid);
            PartyRoster.EnsureInitialized();
            var party = new List<CombatantData>(RestSystem.PartyMembers);
            if (party.Count == 0) throw new InvalidOperationException("party roster is empty");

            var player = _explorePlayer != null ? _explorePlayer : GameObject.FindWithTag("Player");
            if (player == null) throw new InvalidOperationException("active Player-tagged exploration actor is missing");
            Vector2Int playerCell = ClampCell(_auth.WorldToCell(player.transform.position), grid);
            var playerSpawns = SpawnsAround(playerCell, party.Count, grid);
            if (playerSpawns.Count < party.Count)
                throw new InvalidOperationException($"only {playerSpawns.Count}/{party.Count} valid party spawn cells near {playerCell}");

            Vector2Int enemyAnchor = ClampCell(_auth.WorldToCell(encounter.transform.position), grid);
            var enemySpawns = BuildWorldEnemyFormation(
                enemyAnchor, playerCell, enemies.Count, grid, playerSpawns);
            if (enemySpawns.Count < enemies.Count)
                throw new InvalidOperationException($"only {enemySpawns.Count}/{enemies.Count} valid enemy spawn cells near {enemyAnchor}");

            EnsureEventSystem();
            CaptureAndSuspendExploration();
            encounter.gameObject.SetActive(false);
            ConfigureBattleCamera(playerCell, enemySpawns[0], grid);

            _battleManager.OnVictory += HandleVictory;
            _battleManager.OnDefeat += HandleDefeat;
            _battleManager.StartBattle(party, enemies, playerSpawns, enemySpawns);

            if (EnemyAI.WorldState != null)
                foreach (var unit in UnityEngine.Object.FindObjectsByType<BattleUnit>(FindObjectsSortMode.None))
                    if (unit.IsPlayer) EnemyAI.WorldState.playerBelief.Update(unit);

            var fog = GetComponent<ZoneFogOfWar>();
            if (fog != null && ambushRevealSeconds > 0f)
                foreach (var spawn in enemySpawns)
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dz = -1; dz <= 1; dz++)
                            fog.RevealCell(spawn + new Vector2Int(dx, dz), ambushRevealSeconds);

            Debug.Log($"[ZoneEncounter] Persistent agent '{encounter.AgentId}' confronted; battle begins in place.");
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[ZoneEncounter] World-agent startup failed; exploration restored. {exception.Message}", this);
            RestoreExploration(clearBattleUnits: true);
            return false;
        }
    }

    bool ValidateStartup(out string message, bool requireStagedEnemies = true)
    {
        _zone ??= GetComponent<ZoneBattleAuthoring>();
        _auth ??= GetComponent<BattleMapAuthoring>();
        _heights ??= GetComponent<BattleTerrainHeights>();
        if (_zone == null) { message = "ZoneBattleAuthoring is missing"; return false; }
        if (!_zone.TryValidate(out message)) return false;
        if (SeamlessInteriorRegistry.ActiveModule != null &&
            SeamlessInteriorRegistry.ActiveModule.ProtectedSocialInterior)
        {
            message = $"player is inside protected interior '{SeamlessInteriorRegistry.ActiveSubLocationId}'";
            return false;
        }
        if (battleKitPrefab == null) battleKitPrefab = _zone.battleKitPrefab;
        if (battleKitPrefab == null) { message = "BattleKit prefab is missing"; return false; }
        if (battleKitPrefab.GetComponentInChildren<BattleManager>(true) == null ||
            battleKitPrefab.GetComponentInChildren<BattleGrid>(true) == null)
        { message = "BattleKit prefab is incomplete"; return false; }
        if (requireStagedEnemies && (_staged.Count == 0 || _staged.All(unit => unit == null)))
        { message = "no staged enemies remain"; return false; }
        var player = _explorePlayer;
        if (player == null) _explorePlayer = player = GameObject.FindWithTag("Player");
        if (player == null) { message = "active Player-tagged exploration actor is missing"; return false; }
        var camera = Camera.main;
        if (camera == null || camera.GetComponent<CinemachineBrain>() == null)
        { message = "Main Camera with CinemachineBrain is required"; return false; }
        message = string.Empty;
        return true;
    }

    void CaptureAndSuspendExploration()
    {
        if (_explorePlayer == null) _explorePlayer = GameObject.FindWithTag("Player");
        _explorePlayerPosition = _explorePlayer.transform.position;
        _explorePlayerRotation = _explorePlayer.transform.rotation;
        _playerWasActive = _explorePlayer.activeSelf;

        _zone.ResolveLocalReferences();
        _exitStates = _zone.zoneExits.Select(exit => exit != null && exit.enabled).ToArray();
        for (int i = 0; i < _zone.zoneExits.Length; i++)
            if (_zone.zoneExits[i] != null) _zone.zoneExits[i].enabled = false;
        foreach (var interior in _zone.protectedInteriors)
            if (interior != null) interior.SetBattleLocked(true);

        _exploreRootStates = _zone.explorationOnlyRoots.Select(root => root != null && root.activeSelf).ToArray();
        foreach (var root in _zone.explorationOnlyRoots)
            if (root != null) root.SetActive(false);

        _battleRootStates = _zone.battleOnlyRoots.Select(root => root != null && root.activeSelf).ToArray();
        foreach (var root in _zone.battleOnlyRoots)
            if (root != null) root.SetActive(true);

        _explorationCaptured = true;
        _explorePlayer.SetActive(false);
    }

    void ConfigureBattleCamera(Vector2Int playerCell, Vector2Int enemyCell, BattleGrid grid)
    {
        var camera = Camera.main;
        _cineBrain = camera.GetComponent<CinemachineBrain>();
        _brainWasEnabled = _cineBrain.enabled;
        _cineBrain.enabled = false;

        _battleRig = camera.GetComponent<BattleCameraRig>();
        _ownsBattleRig = _battleRig == null;
        if (_battleRig == null) _battleRig = camera.gameObject.AddComponent<BattleCameraRig>();

        Vector3 playerWorld = grid.GridToWorld(playerCell);
        Vector3 enemyWorld = grid.GridToWorld(enemyCell);
        _battleRig.pivot = Vector3.Lerp(playerWorld, enemyWorld, 0.5f) + Vector3.up * 0.65f;
        camera.transform.position = _battleRig.pivot + new Vector3(0.55f, 0.6f, -1f).normalized * 15f;
        camera.transform.LookAt(_battleRig.pivot);
    }

    void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null) return;
        _createdEventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    List<Vector2Int> SpawnsAround(Vector2Int center, int count, BattleGrid grid)
    {
        var spawns = new List<Vector2Int>();
        int maxRing = Mathf.Max(grid.width, grid.height);
        for (int ring = 0; ring <= maxRing && spawns.Count < count; ring++)
            for (int dx = -ring; dx <= ring && spawns.Count < count; dx++)
                for (int dz = -ring; dz <= ring && spawns.Count < count; dz++)
                {
                    var cellPosition = center + new Vector2Int(dx, dz);
                    var cell = grid.GetCell(cellPosition);
                    if (cell != null && cell.walkable && !spawns.Contains(cellPosition))
                        spawns.Add(cellPosition);
                }
        return spawns;
    }

    List<Vector2Int> BuildWorldEnemyFormation(
        Vector2Int anchor,
        Vector2Int playerCell,
        int count,
        BattleGrid grid,
        List<Vector2Int> playerSpawns)
    {
        var result = new List<Vector2Int>();
        var excluded = new HashSet<Vector2Int>(playerSpawns);
        Vector2Int towardPlayer = DominantDirection(playerCell - anchor);
        Vector2Int perpendicular = new Vector2Int(-towardPlayer.y, towardPlayer.x);
        var preferred = new[]
        {
            anchor,
            anchor + towardPlayer + perpendicular,
            anchor + towardPlayer - perpendicular,
            anchor + towardPlayer,
            anchor - towardPlayer + perpendicular,
            anchor - towardPlayer - perpendicular,
        };

        foreach (Vector2Int position in preferred)
        {
            if (result.Count >= count) break;
            AddSpawnIfValid(position, grid, excluded, result);
        }

        int maxRing = Mathf.Max(grid.width, grid.height);
        for (int ring = 1; ring <= maxRing && result.Count < count; ring++)
            for (int dx = -ring; dx <= ring && result.Count < count; dx++)
                for (int dz = -ring; dz <= ring && result.Count < count; dz++)
                    AddSpawnIfValid(anchor + new Vector2Int(dx, dz), grid, excluded, result);
        return result;
    }

    static void AddSpawnIfValid(
        Vector2Int position,
        BattleGrid grid,
        HashSet<Vector2Int> excluded,
        List<Vector2Int> result)
    {
        GridCell cell = grid.GetCell(position);
        if (cell == null || !cell.walkable || excluded.Contains(position) || result.Contains(position)) return;
        result.Add(position);
    }

    static Vector2Int DominantDirection(Vector2Int delta)
    {
        if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
            return new Vector2Int(delta.x >= 0 ? 1 : -1, 0);
        return new Vector2Int(0, delta.y >= 0 ? 1 : -1);
    }

    static Vector2Int ClampCell(Vector2Int position, BattleGrid grid) =>
        new Vector2Int(
            Mathf.Clamp(position.x, 0, grid.width - 1),
            Mathf.Clamp(position.y, 0, grid.height - 1));

    void HandleVictory()
    {
        if (_activeWorldEncounter != null)
        {
            _activeWorldEncounter.ResolveVictory();
            _restoreWorldEncounterActor = false;
        }
        RestoreExploration(clearBattleUnits: true);
    }

    void HandleDefeat()
    {
        _activeWorldEncounter?.ResolveDefeat();
        RestoreExploration(clearBattleUnits: true);
    }

    void RestoreExploration(bool clearBattleUnits)
    {
        if (_battleManager != null)
        {
            _battleManager.OnVictory -= HandleVictory;
            _battleManager.OnDefeat -= HandleDefeat;
        }

        if (clearBattleUnits)
            foreach (var unit in UnityEngine.Object.FindObjectsByType<BattleUnit>(FindObjectsSortMode.None))
                Destroy(unit.gameObject);

        if (_kitInstance != null) Destroy(_kitInstance);
        if (_ownsBattleRig && _battleRig != null) Destroy(_battleRig);
        if (_cineBrain != null) _cineBrain.enabled = _brainWasEnabled;
        if (_createdEventSystem != null) Destroy(_createdEventSystem);

        if (_explorationCaptured) RestoreModeObjects();
        if (_explorationCaptured && _explorePlayer != null)
        {
            _explorePlayer.transform.SetPositionAndRotation(_explorePlayerPosition, _explorePlayerRotation);
            _explorePlayer.SetActive(_playerWasActive);
        }
        if (_restoreWorldEncounterActor && _activeWorldEncounter != null)
            _activeWorldEncounter.gameObject.SetActive(true);

        _battleManager = null;
        _kitInstance = null;
        _battleRig = null;
        _cineBrain = null;
        _createdEventSystem = null;
        _activeWorldEncounter = null;
        _restoreWorldEncounterActor = false;
        _explorationCaptured = false;
        _ownsBattleRig = false;
        _battleRunning = false;
        Debug.Log("[ZoneEncounter] Battle over — exploration restored.");
    }

    void RestoreModeObjects()
    {
        if (_zone == null) return;
        for (int i = 0; i < _zone.zoneExits.Length && i < _exitStates.Length; i++)
            if (_zone.zoneExits[i] != null) _zone.zoneExits[i].enabled = _exitStates[i];
        for (int i = 0; i < _zone.explorationOnlyRoots.Length && i < _exploreRootStates.Length; i++)
            if (_zone.explorationOnlyRoots[i] != null) _zone.explorationOnlyRoots[i].SetActive(_exploreRootStates[i]);
        for (int i = 0; i < _zone.battleOnlyRoots.Length && i < _battleRootStates.Length; i++)
            if (_zone.battleOnlyRoots[i] != null) _zone.battleOnlyRoots[i].SetActive(_battleRootStates[i]);
        foreach (var interior in _zone.protectedInteriors)
            if (interior != null) interior.SetBattleLocked(false);
    }

    void OnDestroy()
    {
        if (_battleManager != null)
        {
            _battleManager.OnVictory -= HandleVictory;
            _battleManager.OnDefeat -= HandleDefeat;
        }
    }
}
