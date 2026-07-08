using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

// Isometric grid cursor — handles input, tile highlighting, and selection.
// Talks directly to BattleManager for player turn flow.
public class BattleCursor : MonoBehaviour
{
    [Header("Cursor Visuals")]
    public GameObject cursorObject;         // the diamond cursor sprite/mesh
    public float      cursorBobSpeed  = 2f;
    public float      cursorBobAmount = 0.06f;

    [Header("Tile Highlight Prefabs")]
    public GameObject moveTilePrefab;       // blue diamond overlay
    public GameObject attackTilePrefab;     // red diamond overlay
    public GameObject hoverTilePrefab;      // white/yellow outline on hovered tile

    [Header("Colors")]
    public Color moveColor   = new Color(0.25f, 0.55f, 1.00f, 0.55f);
    public Color attackColor = new Color(1.00f, 0.25f, 0.25f, 0.55f);
    public Color hoverMove   = new Color(0.55f, 0.80f, 1.00f, 0.85f);
    public Color hoverAttack = new Color(1.00f, 0.55f, 0.30f, 0.85f);
    public Color hoverNeutral= new Color(1.00f, 0.95f, 0.70f, 0.85f);

    [Header("Input repeat")]
    public float moveRepeatDelay    = 0.35f;
    public float moveRepeatInterval = 0.10f;

    // ── Private state ─────────────────────────────────────────────────────────
    private BattleGrid              _grid;
    private Vector2Int              _cursorPos;
    private Vector2Int              _prevCursorPos;

    private HashSet<Vector2Int>     _moveRange   = new();
    private HashSet<Vector2Int>     _attackRange = new();

    private List<GameObject>        _moveHighlights   = new();
    private List<GameObject>        _attackHighlights = new();
    private List<GameObject>        _aoeHighlights    = new();
    private GameObject              _hoverHighlight;

    private float   _bobTimer;
    private float   _repeatTimer;
    private float   _repeatHeldTimer;
    private Vector2 _heldDir;
    private bool    _active;

    public enum CursorMode { Move, Attack, Inspect }
    public CursorMode Mode { get; private set; } = CursorMode.Move;

    // ── Init ──────────────────────────────────────────────────────────────────

    void Awake()
    {
        _hoverHighlight = hoverTilePrefab != null ? Instantiate(hoverTilePrefab) : null;
        if (_hoverHighlight) _hoverHighlight.SetActive(false);
    }

    void Start()
    {
        _grid = BattleManager.Instance?.Grid;

        // Listen to BattleManager state changes
        if (BattleManager.Instance != null)
        {
            BattleManager.Instance.OnStateChanged     += OnBattleStateChanged;
            BattleManager.Instance.OnMoveRangeReady   += ShowMoveRange;
            BattleManager.Instance.OnAttackRangeReady += ShowAttackRange;
        }
    }

    void OnDestroy()
    {
        if (BattleManager.Instance != null)
        {
            BattleManager.Instance.OnStateChanged     -= OnBattleStateChanged;
            BattleManager.Instance.OnMoveRangeReady   -= ShowMoveRange;
            BattleManager.Instance.OnAttackRangeReady -= ShowAttackRange;
        }
    }

    // ── Update ────────────────────────────────────────────────────────────────

    void Update()
    {
        if (!_active) return;

        HandleMovementInput();
        HandleMouseInput();
        HandleConfirmCancel();
        AnimateCursor();
    }

    // ── Mouse: point at a tile to move the cursor, click to confirm ──────────
    // Ray → heightfield intersection via iterative refinement against the
    // baked BattleTerrainHeights surface (no colliders needed). Keyboard
    // still works — mouse and keys drive the same cursor.

    static BattleTerrainHeights _mouseHeights;

    void HandleMouseInput()
    {
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse == null || _grid == null) return;
        var cam = Camera.main;
        if (cam == null) return;

        bool moved   = mouse.delta.ReadValue().sqrMagnitude > 0.01f;
        bool clicked = mouse.leftButton.wasPressedThisFrame;
        if (!moved && !clicked) return;

        // don't fight the UI — clicks on menus/buttons stay theirs
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;

        if (_mouseHeights == null)
            _mouseHeights = FindFirstObjectByType<BattleTerrainHeights>();

        var ray = cam.ScreenPointToRay(mouse.position.ReadValue());
        if (!TryRayToCell(ray, out var cell)) return;
        if (!_grid.InBounds(cell)) return;

        if (cell != _cursorPos)
        {
            _cursorPos = cell;
            UpdateCursorWorldPosition();
            UpdateHoverHighlight();
        }
        if (clicked) OnConfirm();
    }

    bool TryRayToCell(Ray ray, out Vector2Int cell)
    {
        cell = default;
        if (Mathf.Abs(ray.direction.y) < 1e-4f) return false;
        float y = 0.5f;
        Vector3 p = Vector3.zero;
        for (int i = 0; i < 4; i++)
        {
            float t = (y - ray.origin.y) / ray.direction.y;
            if (t < 0f) return false;
            p = ray.origin + ray.direction * t;
            float ny = _mouseHeights != null ? _mouseHeights.SurfaceHeight(p.x, p.z) : 0f;
            if (Mathf.Abs(ny - y) < 0.03f) break;
            y = ny;
        }
        cell = new Vector2Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.z));
        return true;
    }

    // ── Input ─────────────────────────────────────────────────────────────────

    void HandleMovementInput()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        Vector2Int dir = Vector2Int.zero;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed)    dir = Vector2Int.up;
        else if (kb.sKey.isPressed || kb.downArrowKey.isPressed)  dir = Vector2Int.down;
        else if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  dir = Vector2Int.left;
        else if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) dir = Vector2Int.right;

        if (dir == Vector2Int.zero)
        {
            _heldDir       = Vector2.zero;
            _repeatTimer   = 0f;
            _repeatHeldTimer = 0f;
            return;
        }

        bool firstPress = (dir != new Vector2Int((int)_heldDir.x, (int)_heldDir.y));
        _heldDir = dir;

        if (firstPress)
        {
            MoveCursor(dir);
            _repeatTimer    = moveRepeatDelay;
            _repeatHeldTimer = 0f;
        }
        else
        {
            _repeatHeldTimer += Time.deltaTime;
            if (_repeatHeldTimer >= _repeatTimer)
            {
                MoveCursor(dir);
                _repeatTimer    = moveRepeatInterval;
                _repeatHeldTimer = 0f;
            }
        }
    }

    void MoveCursor(Vector2Int dir)
    {
        Vector2Int next = _cursorPos + dir;
        if (!_grid.InBounds(next)) return;

        _prevCursorPos = _cursorPos;
        _cursorPos     = next;
        UpdateCursorWorldPosition();
        UpdateHoverHighlight();
    }

    void HandleConfirmCancel()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.zKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
            OnConfirm();

        if (kb.xKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)
            OnCancel();
    }

    void OnConfirm()
    {
        var bm = BattleManager.Instance;
        if (bm == null) return;

        switch (bm.State)
        {
            case BattleState.PlayerSelectMove:
                if (_moveRange.Contains(_cursorPos))
                    bm.PlayerSelectMoveTarget(_cursorPos);
                else if (_cursorPos == bm.ActiveUnit?.gridPosition)
                    bm.PlayerSkipMove(); // confirm on own tile = skip move
                break;

            case BattleState.PlayerSelectTarget:
                if (_attackRange.Contains(_cursorPos))
                    bm.PlayerSelectActionTarget(_cursorPos);
                break;
        }
    }

    void OnCancel()
    {
        var bm = BattleManager.Instance;
        if (bm == null) return;

        switch (bm.State)
        {
            case BattleState.PlayerSelectTarget:
                // Back out of aiming, reopen the action menu
                bm.PlayerCancelTarget();
                break;

            case BattleState.PlayerSelectMove:
                // Cancel out of move selection = skip the move (opens the action
                // menu pre-act, ends the turn post-act). Never forfeits a turn.
                bm.PlayerSkipMove();
                break;
        }
    }

    // ── Range highlights ──────────────────────────────────────────────────────

    public void ShowMoveRange(List<GridCell> cells)
    {
        ClearHighlights(_moveHighlights);
        _moveRange.Clear();

        foreach (var cell in cells)
        {
            _moveRange.Add(cell.gridPos);
            SpawnHighlight(_moveHighlights, moveTilePrefab, cell, moveColor);
        }

        Mode = CursorMode.Move;
        UpdateHoverHighlight();
    }

    public void ShowAttackRange(List<GridCell> cells)
    {
        ClearHighlights(_attackHighlights);
        ClearHighlights(_aoeHighlights);
        _attackRange.Clear();

        foreach (var cell in cells)
        {
            _attackRange.Add(cell.gridPos);
            SpawnHighlight(_attackHighlights, attackTilePrefab, cell, attackColor);
        }

        Mode = CursorMode.Attack;
        UpdateHoverHighlight();
    }

    void SpawnHighlight(List<GameObject> list, GameObject prefab, GridCell cell, Color color)
    {
        if (prefab == null) return;
        var go  = Instantiate(prefab);
        go.transform.position = _grid.GridToWorld(cell.gridPos, cell.elevation);

        // Tint the sprite renderer if present
        var sr = go.GetComponentInChildren<SpriteRenderer>();
        if (sr) sr.color = color;
        TintDecal(go, color);

        list.Add(go);
    }

    // 3D diorama maps use terrain-conforming mesh highlights; tint their
    // _BaseColor via property block (sprite highlights ignore this path).
    static void TintDecal(GameObject go, Color color)
    {
        var mr = go.GetComponentInChildren<MeshRenderer>();
        if (mr == null || mr.sharedMaterial == null || !mr.sharedMaterial.HasProperty("_BaseColor")) return;
        var mpb = new MaterialPropertyBlock();
        mr.GetPropertyBlock(mpb);
        mpb.SetColor("_BaseColor", color);
        mr.SetPropertyBlock(mpb);
    }

    void ClearHighlights(List<GameObject> list)
    {
        foreach (var go in list) if (go) Destroy(go);
        list.Clear();
    }

    // ── Hover highlight ───────────────────────────────────────────────────────

    void UpdateHoverHighlight()
    {
        UpdateAOEPreview();

        if (_hoverHighlight == null) return;

        var cell = _grid.GetCell(_cursorPos);
        if (cell == null) { _hoverHighlight.SetActive(false); return; }

        _hoverHighlight.SetActive(true);
        _hoverHighlight.transform.position = _grid.GridToWorld(_cursorPos, cell.elevation);

        // Color the hover based on what's under it
        Color hc = _moveRange.Contains(_cursorPos) ? hoverMove
                 : _attackRange.Contains(_cursorPos) ? hoverAttack : hoverNeutral;
        var sr = _hoverHighlight.GetComponentInChildren<SpriteRenderer>();
        if (sr) sr.color = hc;
        TintDecal(_hoverHighlight, hc);
    }

    // ── AOE splash preview ────────────────────────────────────────────────────

    // While aiming an AOE skill, show every cell the splash would cover.
    void UpdateAOEPreview()
    {
        ClearHighlights(_aoeHighlights);

        var bm    = BattleManager.Instance;
        var skill = bm != null ? bm.SelectedSkill : null;
        if (skill == null || skill.areaOfEffect <= 0) return;
        if (!_attackRange.Contains(_cursorPos)) return;   // only preview on valid targets

        foreach (var cell in _grid.GetAOECells(_cursorPos, skill.areaOfEffect))
            SpawnHighlight(_aoeHighlights, attackTilePrefab, cell,
                           new Color(1f, 0.55f, 0.15f, 0.7f)); // splash orange
    }

    // ── Cursor world position ─────────────────────────────────────────────────

    void UpdateCursorWorldPosition()
    {
        if (cursorObject == null || _grid == null) return;
        var cell = _grid.GetCell(_cursorPos);
        int elev = cell?.elevation ?? 0;
        cursorObject.transform.position = _grid.GridToWorld(_cursorPos, elev);
    }

    // ── Cursor bob animation ──────────────────────────────────────────────────

    void AnimateCursor()
    {
        if (cursorObject == null) return;
        _bobTimer += Time.deltaTime * cursorBobSpeed;
        var pos    = cursorObject.transform.position;
        pos.y     += Mathf.Sin(_bobTimer) * cursorBobAmount * Time.deltaTime * 60f;
        cursorObject.transform.position = pos;
    }

    // ── Battle state changes ──────────────────────────────────────────────────

    void OnBattleStateChanged(BattleState state)
    {
        switch (state)
        {
            case BattleState.PlayerSelectMove:
            case BattleState.PlayerSelectTarget:
                Activate();
                // Snap cursor to active unit position
                var active = BattleManager.Instance?.ActiveUnit;
                if (active != null) SnapTo(active.gridPosition);
                break;

            case BattleState.PlayerSelectAction:
                // Action menu takes over — cursor stays visible but input paused
                Deactivate();
                break;

            case BattleState.EnemyTurn:
            case BattleState.ResolvingAction:
            case BattleState.TickCT:
                Deactivate();
                ClearAllHighlights();
                break;

            case BattleState.BattleVictory:
            case BattleState.BattleDefeat:
                Deactivate();
                ClearAllHighlights();
                if (_hoverHighlight) _hoverHighlight.SetActive(false);
                break;
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Activate()
    {
        _active = true;
        if (cursorObject) cursorObject.SetActive(true);
        if (_hoverHighlight) _hoverHighlight.SetActive(true);
        UpdateCursorWorldPosition();
        UpdateHoverHighlight();
    }

    public void Deactivate()
    {
        _active = false;
        if (cursorObject) cursorObject.SetActive(false);
        if (_hoverHighlight) _hoverHighlight.SetActive(false);
    }

    public void SnapTo(Vector2Int pos)
    {
        _cursorPos = pos;
        UpdateCursorWorldPosition();
        UpdateHoverHighlight();
    }

    public Vector2Int Position => _cursorPos;

    void ClearAllHighlights()
    {
        ClearHighlights(_moveHighlights);
        ClearHighlights(_attackHighlights);
        ClearHighlights(_aoeHighlights);
        _moveRange.Clear();
        _attackRange.Clear();
    }
}
