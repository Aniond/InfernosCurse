using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using TMPro;

// Rest menu, two modes on one self-built overlay (FastTravelMenu's structure):
//   • Camp — hotkey R anywhere while exploring: sleep rough until morning,
//     partial recovery, the night takes its toll on the district.
//   • Inn — opened by a GuildInteractionZone: full night's rest for florins;
//     Albergatori standing softens the toll, and their own houses can cleanse.
// All feedback is WORDS — the curse is never a number.
public class RestMenuUI : MonoBehaviour
{
    [Header("Open / Close")]
    public Key hotkey = Key.R;
    public KeyCode hotkeyLegacy = KeyCode.R;

    [Header("Behaviour")]
    public bool pauseWhileOpen = true;
    public string[] blockedScenes = { "WorldMap", "MainMenu", "Battle", "BattleArena" };

    // Inn context (set by OpenInn)
    string _innName;
    int _innPrice;
    bool _innIsGuild;

    Canvas _canvas;
    GameObject _root;
    Transform _listParent;
    TMP_Text _title;
    bool _open;
    bool _innMode;
    float _savedTimeScale = 1f;

    static RestMenuUI _instance;
    public static RestMenuUI Instance
    {
        get => _instance != null ? _instance : (_instance = FindAnyObjectByType<RestMenuUI>());
        private set => _instance = value;
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        BuildUI();
        Close();
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Update()
    {
        bool pressed =
            (Keyboard.current != null && hotkey != Key.None && Keyboard.current[hotkey].wasPressedThisFrame)
            || Input.GetKeyDown(hotkeyLegacy);
        if (pressed && !_open) OpenCamp();

        if (_open)
        {
            bool cancel = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                       || Input.GetKeyDown(KeyCode.Escape);
            if (cancel) Close();
        }
    }

    // ── Entry points ─────────────────────────────────────────────────────────

    public void OpenCamp()
    {
        if (IsBlockedScene()) return;
        _innMode = false;
        Rebuild();
        Show();
    }

    /// <summary>Opened by an inn GuildInteractionZone.</summary>
    public void OpenInn(string innName, int price, bool isGuildInn)
    {
        if (IsBlockedScene()) return;
        _innMode = true;
        _innName = innName;
        _innPrice = price;
        _innIsGuild = isGuildInn;
        Rebuild();
        Show();
    }

    void Show()
    {
        _root.SetActive(true);
        _open = true;
        if (pauseWhileOpen)
        {
            _savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }
    }

    public void Close()
    {
        if (_root != null) _root.SetActive(false);
        if (_open && pauseWhileOpen) Time.timeScale = _savedTimeScale;
        _open = false;
    }

    // ── Content ──────────────────────────────────────────────────────────────

    void Rebuild()
    {
        for (int i = _listParent.childCount - 1; i >= 0; i--)
            if (_listParent.GetChild(i).gameObject != _title.gameObject)
                Destroy(_listParent.GetChild(i).gameObject);

        string district = DistrictTracker.CurrentNodeId;
        var guilds = GuildSystem.Instance;

        if (_innMode)
        {
            _title.text = _innName;
            int shownPrice = Mathf.RoundToInt(_innPrice * (guilds != null ? guilds.GetInnPriceMultiplier() : 1f));

            AddHeader(InnFlavor(guilds));
            AddRow($"Rest until morning — {shownPrice} fiorini", () =>
            {
                if (RestSystem.RestAtInn(district, _innPrice, _innIsGuild)) Close();
                else Rebuild(); // couldn't pay — flavor updates
            });
            if (FlorinWallet.Balance < shownPrice)
                AddHeader("Your purse is too light for a bed tonight.");
        }
        else
        {
            _title.text = "Make Camp";
            AddHeader("Sleep rough until morning. The night takes its toll.");
            AddRow("Camp here", () => { RestSystem.Camp(district); Close(); });
        }

        AddSpacer();
        AddRow("Leave", Close);
    }

    string InnFlavor(GuildSystem guilds)
    {
        if (!_innIsGuild || guilds == null) return "A bed, a candle, a bolted door.";
        int rank = guilds.GetRank("albergatori");
        switch (rank)
        {
            case 0: return "The innkeeper eyes you like any other stranger.";
            case 1: return "A nod of recognition — the Albergatori remember a friend.";
            case 2: return "The good room, no questions asked.";
            case 3: return "Wine on the house; the guild looks after its own.";
            default: return "The innkeeper knows your name; the guild's wards hold.";
        }
    }

    bool IsBlockedScene()
    {
        string scene = SceneManager.GetActiveScene().name;
        foreach (var s in blockedScenes)
            if (scene == s) return true;
        return false;
    }

    // ── Auto-built UI (FastTravelMenu pattern) ───────────────────────────────

    void BuildUI()
    {
        var canvasGO = new GameObject("RestMenuCanvas");
        canvasGO.transform.SetParent(transform, false);
        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 500;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGO.AddComponent<GraphicRaycaster>();

        _root = new GameObject("Root", typeof(RectTransform), typeof(Image));
        _root.transform.SetParent(canvasGO.transform, false);
        var rootRt = (RectTransform)_root.transform;
        rootRt.anchorMin = Vector2.zero; rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero; rootRt.offsetMax = Vector2.zero;
        _root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        panel.transform.SetParent(_root.transform, false);
        var pRt = (RectTransform)panel.transform;
        pRt.anchorMin = new Vector2(0.5f, 0.5f);
        pRt.anchorMax = new Vector2(0.5f, 0.5f);
        pRt.pivot = new Vector2(0.5f, 0.5f);
        pRt.sizeDelta = new Vector2(560f, 0f);
        panel.GetComponent<Image>().color = new Color(0.08f, 0.06f, 0.10f, 0.94f);

        var vlg = panel.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(24, 24, 24, 24);
        vlg.spacing = 6;
        vlg.childControlWidth = true;
        vlg.childForceExpandWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandHeight = false;
        panel.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _title = MakeText("Rest", 40, FontStyles.Bold, new Color(0.96f, 0.9f, 0.72f));
        _title.transform.SetParent(panel.transform, false);

        _listParent = panel.transform;
    }

    void AddHeader(string text)
    {
        var t = MakeText(text, 22, FontStyles.Italic, new Color(0.75f, 0.7f, 0.8f));
        t.margin = new Vector4(0, 12, 0, 2);
        t.textWrappingMode = TextWrappingModes.Normal;
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
        colors.pressedColor = new Color(0.45f, 0.36f, 0.20f, 1f);
        colors.fadeDuration = 0.05f;
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
