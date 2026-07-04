using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using TMPro;

// Guild standings and services, self-built like FastTravelMenu.
// Three modes on one overlay:
//   • Standings (hotkey G) — every known guild: rank name, a WORDED progress
//     caption, and unlocked/locked perk flavor. No numbers for anything the
//     curse touches; reputation itself shows as words.
//   • Donation (opened by a guild bench zone) — florin gifts for standing.
//   • Transmute (opened by a chapel zone) — corrupted→holy rites, gated by
//     Church rank + offering.
public class GuildPanelUI : MonoBehaviour
{
    [Header("Open / Close")]
    public Key hotkey = Key.G;
    public KeyCode hotkeyLegacy = KeyCode.G;

    [Header("Behaviour")]
    public bool pauseWhileOpen = true;
    public string[] blockedScenes = { "WorldMap", "MainMenu", "Battle", "BattleArena" };

    enum Mode { Standings, Donation, Transmute, Join }
    Mode _mode;
    string _donationGuildId;
    string _contextLabel;
    Vector3 _shrinePos;

    Canvas _canvas;
    GameObject _root;
    Transform _listParent;
    TMP_Text _title;
    bool _open;
    float _savedTimeScale = 1f;

    static GuildPanelUI _instance;
    public static GuildPanelUI Instance
    {
        get => _instance != null ? _instance : (_instance = FindAnyObjectByType<GuildPanelUI>());
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
        if (GugolMapUI.IsOpen) return;   // typing in the map's search bar must not open menus

        bool pressed =
            (Keyboard.current != null && hotkey != Key.None && Keyboard.current[hotkey].wasPressedThisFrame)
            || Input.GetKeyDown(hotkeyLegacy);
        if (pressed && !_open) OpenStandings();

        if (_open)
        {
            bool cancel = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                       || Input.GetKeyDown(KeyCode.Escape);
            if (cancel) Close();
        }
    }

    // ── Entry points ─────────────────────────────────────────────────────────

    public void OpenStandings()
    {
        if (IsBlockedScene()) return;
        _mode = Mode.Standings;
        Rebuild();
        Show();
    }

    public void OpenDonation(string guildId, string label)
    {
        if (IsBlockedScene()) return;
        _mode = Mode.Donation;
        _donationGuildId = guildId;
        _contextLabel = label;
        Rebuild();
        Show();
    }

    public void OpenTransmute(string label)
    {
        if (IsBlockedScene()) return;
        _mode = Mode.Transmute;
        _contextLabel = label;
        Rebuild();
        Show();
    }

    public void OpenJoin(string guildId, string label, Vector3 shrinePos)
    {
        if (IsBlockedScene()) return;
        _mode = Mode.Join;
        _donationGuildId = guildId; // reuse the context guild slot
        _contextLabel = label;
        _shrinePos = shrinePos;
        Rebuild();
        Show();
    }

    void Show()
    {
        EnsureEventSystem();
        _root.SetActive(true);
        _open = true;
        if (pauseWhileOpen)
        {
            _savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }
    }

    // Scenes built by script (Duomo, Ponte Vecchio) never got an EventSystem, so
    // panel buttons silently ignored clicks — the old "donation ui freeze". Any
    // UI this panel opens now guarantees one exists in the loaded scene.
    static void EnsureEventSystem()
    {
        if (UnityEngine.EventSystems.EventSystem.current != null) return;
        if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;
        var go = new GameObject("[EventSystem]",
            typeof(UnityEngine.EventSystems.EventSystem),
            typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        go.name = "[EventSystem]";
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

        switch (_mode)
        {
            case Mode.Standings: BuildStandings(); break;
            case Mode.Donation:  BuildDonation();  break;
            case Mode.Transmute: BuildTransmute(); break;
            case Mode.Join:      BuildJoin();      break;
        }

        AddSpacer();
        AddRow(_mode == Mode.Standings ? "Close" : "Leave", Close);
    }

    void BuildStandings()
    {
        _title.text = "The Guilds of Florence";
        var guilds = GuildSystem.Instance;
        if (guilds == null || guilds.guilds.Count == 0)
        {
            AddHeader("You know no one of consequence yet.");
            return;
        }

        foreach (var g in guilds.guilds)
        {
            if (g == null) continue;
            int rep = guilds.GetRep(g.guildId);
            int rank = g.RankForRep(rep);

            AddHeader($"{g.displayName} — {g.RankName(rank)}");
            AddCaption(ProgressWords(g, rep, rank));
            foreach (var p in g.perks)
            {
                bool unlocked = p.unlockRank <= rank;
                if (string.IsNullOrEmpty(p.flavorText)) continue;
                AddCaption((unlocked ? "◆ " : "◇ ") + p.flavorText, unlocked
                    ? new Color(0.96f, 0.9f, 0.72f)
                    : new Color(0.45f, 0.42f, 0.5f));
            }
        }
    }

    // Reputation as words — the project shows no meters for the soul of things.
    string ProgressWords(GuildDefinition g, int rep, int rank)
    {
        if (rank >= g.MaxRank) return "They could ask no more of you.";
        int span = g.repPerRank[rank + 1] - g.repPerRank[rank];
        float f = span > 0 ? (rep - g.repPerRank[rank]) / (float)span : 0f;
        if (f < 0.25f) return "Your name is new among them.";
        if (f < 0.5f) return "They speak of you, sometimes kindly.";
        if (f < 0.75f) return "Doors open a little easier now.";
        return "They are close to trusting you with more.";
    }

    // Faction pledge at a shrine (the Duomo's Santuario di San Zenobio). Rep 0 =
    // not yet sworn: offer the pledge. Otherwise show this one guild's standing —
    // rank gates on the other rooms' services read from the same reputation.
    void BuildJoin()
    {
        var guilds = GuildSystem.Instance;
        var g = guilds != null ? guilds.GetGuild(_donationGuildId) : null;
        _title.text = _contextLabel;
        if (g == null) { AddHeader("The shrine is silent."); return; }

        int rep = guilds.GetRep(g.guildId);
        if (rep <= 0)
        {
            AddHeader("The shrine of San Zenobio, first bishop of Florence.");
            AddCaption("Kneel, and pledge yourself to the Church. The city's grace" +
                       " — and its secrets — open only to the faithful.");
            AddRow("Pledge yourself to the Church", () =>
            {
                guilds.AwardRep(g.guildId, 25, "pledge of faith");
                Close(); // ceremony: panel yields to the kneel (timescale resumes)
                FindAnyObjectByType<PlayerController>()?.KneelToward(_shrinePos, 3.2f);
            });
        }
        else
        {
            int rank = g.RankForRep(rep);
            AddHeader($"{g.displayName} — {g.RankName(rank)}");
            AddCaption(ProgressWords(g, rep, rank));
            foreach (var p in g.perks)
            {
                if (string.IsNullOrEmpty(p.flavorText)) continue;
                bool unlocked = p.unlockRank <= rank;
                AddCaption((unlocked ? "◆ " : "◇ ") + p.flavorText, unlocked
                    ? new Color(0.96f, 0.9f, 0.72f)
                    : new Color(0.45f, 0.42f, 0.5f));
            }
        }
    }

    void BuildDonation()
    {
        var guilds = GuildSystem.Instance;
        var g = guilds != null ? guilds.GetGuild(_donationGuildId) : null;
        _title.text = _contextLabel;
        if (g == null) { AddHeader("No one is here to receive gifts."); return; }

        int rank = g.RankForRep(guilds.GetRep(g.guildId));
        AddHeader($"{g.displayName} — {g.RankName(rank)}");
        AddCaption($"Your purse: {FlorinWallet.Balance} fiorini");

        foreach (int amount in new[] { 10, 50, 200 })
        {
            int captured = amount;
            AddRow($"Donate {captured} fiorini", () =>
            {
                guilds.Donate(g.guildId, captured);
                Rebuild(); // refresh rank/purse
            });
        }
    }

    void BuildTransmute()
    {
        _title.text = _contextLabel;
        var guilds = GuildSystem.Instance;

        int cost = 0;
        bool allowed = guilds != null && guilds.CanTransmute(out cost);

        if (!allowed)
        {
            AddHeader("The Church does not yet trust you with such rites.");
            return;
        }

        AddHeader($"The rite of purification asks an offering of {cost} fiorini.");
        AddCaption($"Your purse: {FlorinWallet.Balance} fiorini");

        int candidates = 0;
        foreach (var member in RestSystem.PartyMembers)
        {
            if (member == null || member.absorbedSkills == null) continue;
            foreach (var inst in member.absorbedSkills)
            {
                if (inst == null || !inst.CanRefine()) continue;
                candidates++;
                var capturedMember = member;
                var capturedInst = inst;
                AddRow($"Purify {inst.DisplayName()}", () =>
                {
                    capturedMember.RefineAtChurch(capturedInst);
                    Rebuild();
                });
            }
        }
        if (candidates == 0)
            AddCaption("You carry nothing ripe for purification.");
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
        var canvasGO = new GameObject("GuildPanelCanvas");
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
        pRt.sizeDelta = new Vector2(640f, 0f);
        panel.GetComponent<Image>().color = new Color(0.08f, 0.06f, 0.10f, 0.94f);

        var vlg = panel.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(24, 24, 24, 24);
        vlg.spacing = 6;
        vlg.childControlWidth = true;
        vlg.childForceExpandWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandHeight = false;
        panel.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _title = MakeText("Guilds", 40, FontStyles.Bold, new Color(0.96f, 0.9f, 0.72f));
        _title.transform.SetParent(panel.transform, false);

        _listParent = panel.transform;
    }

    void AddHeader(string text)
    {
        var t = MakeText(text, 24, FontStyles.Bold, new Color(0.85f, 0.8f, 0.88f));
        t.margin = new Vector4(0, 12, 0, 2);
        t.textWrappingMode = TextWrappingModes.Normal;
        t.transform.SetParent(_listParent, false);
    }

    void AddCaption(string text) =>
        AddCaption(text, new Color(0.7f, 0.66f, 0.74f));

    void AddCaption(string text, Color color)
    {
        var t = MakeText(text, 20, FontStyles.Italic, color);
        t.margin = new Vector4(12, 0, 0, 2);
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
