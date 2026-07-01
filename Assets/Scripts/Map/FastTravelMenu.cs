using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using TMPro;

// Persona-5 style fast-travel menu. Opens over the explorable scene and lists
// two kinds of destinations:
//   • Districts — other Florence scenes (from HubMap.AllNodes with a sceneName).
//     Selecting one loads that scene at the node's entryId (mirrors WorldMapUI).
//   • This Area — ZoneEntryPoints in the CURRENT scene. Selecting one teleports
//     the player in place so they skip running across the zone.
//
// Open with the M hotkey (any time while exploring) or via MenuManager's Travel
// button. Self-builds its own Canvas/UI like CalendarDisplayUI, so it needs zero
// scene wiring — drop it on the GameSystems prefab and it works everywhere.
public class FastTravelMenu : MonoBehaviour
{
    [Header("Open / Close")]
    [Tooltip("Hotkey that toggles the menu while exploring.")]
    public Key hotkey = Key.M;
    [Tooltip("Also toggle on this legacy KeyCode (old Input Manager).")]
    public KeyCode hotkeyLegacy = KeyCode.M;

    [Header("Behaviour")]
    [Tooltip("Freeze the game (timeScale 0) while the menu is open.")]
    public bool pauseWhileOpen = true;
    [Tooltip("Tag of the player root to teleport for in-zone jumps.")]
    public string playerTag = "Player";
    [Tooltip("Scenes the menu should never appear in (menus, world map, battle).")]
    public string[] blockedScenes = { "WorldMap", "MainMenu", "Battle" };

    // ── Runtime UI (auto-built) ──────────────────────────────────────────────
    private Canvas     _canvas;
    private GameObject _root;          // the dimmed panel container, toggled on/off
    private Transform  _listParent;    // where destination rows are spawned
    private bool _open;
    private float _savedTimeScale = 1f;

    void Awake()
    {
        BuildUI();
        Close();   // start hidden
    }

    void Update()
    {
        bool pressed =
            (Keyboard.current != null && hotkey != Key.None && Keyboard.current[hotkey].wasPressedThisFrame)
            || Input.GetKeyDown(hotkeyLegacy);

        if (pressed) Toggle();

        // ESC / cancel closes if we're the thing that's open.
        if (_open)
        {
            bool cancel = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                       || Input.GetKeyDown(KeyCode.Escape);
            if (cancel) Close();
        }
    }

    // ── Public entry points ──────────────────────────────────────────────────

    public void Toggle() { if (_open) Close(); else Open(); }

    public void Open()
    {
        if (IsBlockedScene())
        {
            Debug.Log("[FastTravelMenu] Fast-travel isn't available in this scene.");
            return;
        }

        Rebuild();
        _root.SetActive(true);
        _open = true;

        if (pauseWhileOpen)
        {
            _savedTimeScale = Time.timeScale;
            Time.timeScale  = 0f;
        }
    }

    public void Close()
    {
        if (_root != null) _root.SetActive(false);
        if (_open && pauseWhileOpen) Time.timeScale = _savedTimeScale;
        _open = false;
    }

    // ── Destination list ─────────────────────────────────────────────────────

    void Rebuild()
    {
        // Clear previous rows.
        for (int i = _listParent.childCount - 1; i >= 0; i--)
            Destroy(_listParent.GetChild(i).gameObject);

        string currentScene = SceneManager.GetActiveScene().name;

        // ── This Area: ZoneEntryPoints in the current scene ──
        // De-dupe by entryId — scenes can carry several markers with the same id
        // (cross-scene arrival points), and we only want one row each.
        var spots  = new List<ZoneEntryPoint>();
        var seenIds = new HashSet<string>();
        foreach (var ep in FindObjectsByType<ZoneEntryPoint>(FindObjectsInactive.Include))
        {
            if (!ep.fastTravelDestination) continue;
            if (!seenIds.Add(ep.entryId)) continue;   // skip duplicate ids
            spots.Add(ep);
        }

        if (spots.Count > 0)
        {
            AddHeader("This Area");
            foreach (var ep in spots)
            {
                var captured = ep;
                AddRow(ep.Label, () => JumpToSpot(captured));
            }
        }

        // ── Districts: other scenes from the hub map ──
        var hub = HubMap.Instance;
        if (hub != null)
        {
            bool headerAdded = false;
            foreach (var node in hub.AllNodes)
            {
                if (node == null || string.IsNullOrEmpty(node.sceneName)) continue;
                if (node.sceneName == currentScene) continue;          // already here
                if (!Application.CanStreamedLevelBeLoaded(node.sceneName)) continue; // not built yet

                if (!headerAdded) { AddHeader("Districts"); headerAdded = true; }
                var captured = node;
                AddRow(node.displayName, () => TravelToDistrict(captured));
            }
        }

        if (_listParent.childCount == 0)
            AddHeader("No destinations available");

        AddSpacer();
        AddRow("Close", Close);
    }

    // ── Actions ──────────────────────────────────────────────────────────────

    // In-scene teleport — mirrors ZoneEntryPlacer's physics-safe move.
    void JumpToSpot(ZoneEntryPoint entry)
    {
        var p = GameObject.FindGameObjectWithTag(playerTag);
        if (p == null) { Debug.LogWarning("[FastTravelMenu] No player to teleport."); return; }

        var pos = entry.transform.position;
        var rb  = p.GetComponent<Rigidbody>();
        if (rb != null) { rb.position = pos; rb.linearVelocity = Vector3.zero; }
        p.transform.position = pos;

        var controller = p.GetComponent<PlayerController>();
        if (controller != null && entry.faceDirection.sqrMagnitude > 0.01f)
            controller.SetFacing(entry.faceDirection);

        Debug.Log($"[FastTravelMenu] Jumped to spot '{entry.Label}'.");
        Close();
    }

    // Cross-scene travel — mirrors WorldMapUI.TravelTo.
    void TravelToDistrict(HubNode node)
    {
        if (!Application.CanStreamedLevelBeLoaded(node.sceneName))
        {
            Debug.LogError($"[FastTravelMenu] Scene '{node.sceneName}' is not in Build Settings.");
            return;
        }

        TravelIntent.SetEntry(node.entryId);
        Debug.Log($"[FastTravelMenu] Traveling to {node.displayName} ({node.sceneName}).");

        // Restore timescale before the load so the destination scene runs normally.
        if (pauseWhileOpen) Time.timeScale = _savedTimeScale;
        _open = false;
        SceneManager.LoadScene(node.sceneName);
    }

    bool IsBlockedScene()
    {
        string scene = SceneManager.GetActiveScene().name;
        foreach (var s in blockedScenes)
            if (scene == s) return true;
        return false;
    }

    // ── Auto-built UI ────────────────────────────────────────────────────────

    void BuildUI()
    {
        // Canvas
        var canvasGO = new GameObject("FastTravelCanvas");
        canvasGO.transform.SetParent(transform, false);
        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 500;   // above HUD, below hard system dialogs
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGO.AddComponent<GraphicRaycaster>();

        // Full-screen dim + click-catcher
        _root = new GameObject("Root", typeof(RectTransform), typeof(Image));
        _root.transform.SetParent(canvasGO.transform, false);
        var rootRt = (RectTransform)_root.transform;
        rootRt.anchorMin = Vector2.zero; rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero; rootRt.offsetMax = Vector2.zero;
        _root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

        // Left-side panel column
        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        panel.transform.SetParent(_root.transform, false);
        var pRt = (RectTransform)panel.transform;
        pRt.anchorMin = new Vector2(0f, 0.5f);
        pRt.anchorMax = new Vector2(0f, 0.5f);
        pRt.pivot     = new Vector2(0f, 0.5f);
        pRt.anchoredPosition = new Vector2(80f, 0f);
        pRt.sizeDelta = new Vector2(460f, 0f);
        panel.GetComponent<Image>().color = new Color(0.08f, 0.06f, 0.10f, 0.94f);

        var vlg = panel.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(24, 24, 24, 24);
        vlg.spacing = 6;
        vlg.childControlWidth = true;
        vlg.childForceExpandWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandHeight = false;

        var fitter = panel.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Title
        var title = MakeText("Travel", 40, FontStyles.Bold, new Color(0.95f, 0.85f, 0.55f));
        title.transform.SetParent(panel.transform, false);

        _listParent = panel.transform;   // rows spawn into the same vertical layout
    }

    void AddHeader(string text)
    {
        var t = MakeText(text, 22, FontStyles.Bold, new Color(0.75f, 0.7f, 0.8f));
        t.margin = new Vector4(0, 12, 0, 2);
        t.transform.SetParent(_listParent, false);
    }

    void AddSpacer()
    {
        var go = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
        go.GetComponent<LayoutElement>().minHeight = 10;
        go.transform.SetParent(_listParent, false);
    }

    void AddRow(string label, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject("Row_" + label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(_listParent, false);
        go.GetComponent<LayoutElement>().minHeight = 48;

        var img = go.GetComponent<Image>();
        img.color = new Color(0.16f, 0.13f, 0.20f, 0.9f);

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.30f, 0.24f, 0.36f, 1f);
        colors.pressedColor     = new Color(0.45f, 0.36f, 0.20f, 1f);
        colors.fadeDuration     = 0.05f;
        btn.colors = colors;
        btn.onClick.AddListener(onClick);

        var t = MakeText(label, 26, FontStyles.Normal, Color.white);
        t.alignment = TextAlignmentOptions.MidlineLeft;
        t.margin = new Vector4(16, 0, 8, 0);
        var tRt = (RectTransform)t.transform;
        tRt.SetParent(go.transform, false);
        tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one;
        tRt.offsetMin = Vector2.zero; tRt.offsetMax = Vector2.zero;
    }

    TMP_Text MakeText(string text, float size, FontStyles style, Color color)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.alignment = TextAlignmentOptions.MidlineLeft;
        t.raycastTarget = false;
        return t;
    }
}
