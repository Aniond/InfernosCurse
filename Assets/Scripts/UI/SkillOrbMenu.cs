using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

// The Skill Orb screen (David's 7/08 mockup, applied to our systems):
// central orb + satellite duplicate sockets, rank progression column, rank
// bar, SLOT/REMOVE orb actions, banked count. Pure POWER presentation —
// by HARD LAW nothing here hints at insanity/corruption; the world is the
// only place the price shows. Toggle with K in explore mode.
public class SkillOrbMenu : MonoBehaviour
{
    [Header("Fonts (wired on GameSystems — Cinzel headers, EBGaramond body)")]
    public TMP_FontAsset headerFont;
    public TMP_FontAsset bodyFont;

    static readonly Color PanelBg   = new Color(0.075f, 0.06f, 0.05f, 0.98f);
    static readonly Color PanelEdge = new Color(0.72f, 0.58f, 0.28f, 0.9f);   // old gold
    static readonly Color Gold      = new Color(0.87f, 0.72f, 0.38f);
    static readonly Color Parchment = new Color(0.92f, 0.88f, 0.78f);
    static readonly Color Dim       = new Color(0.62f, 0.58f, 0.50f);
    static readonly Color OrbFire   = new Color(0.85f, 0.32f, 0.10f);
    static readonly Color OrbPoison = new Color(0.45f, 0.75f, 0.20f);
    static readonly Color OrbHoly   = new Color(0.95f, 0.85f, 0.45f);
    static readonly Color SocketEmpty = new Color(0.24f, 0.19f, 0.15f);

    Canvas _canvas;
    GameObject _root;
    int _index;
    CombatantData _ben;

    // rebuilt-on-refresh widgets
    TextMeshProUGUI _title, _rank, _desc, _stats, _banked, _rankRows;
    Image _bigOrb;
    readonly List<Image> _sockets = new();
    Button _slotBtn, _removeBtn, _prevBtn, _nextBtn;
    Transform _socketRing;

    public bool IsOpen => _root != null && _root.activeSelf;

    void Update()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.kKey.wasPressedThisFrame && BattleManager.Instance == null)
            Toggle();
        if (IsOpen && kb != null && kb.escapeKey.wasPressedThisFrame)
            _root.SetActive(false);
    }

    public void Toggle()
    {
        if (_root == null) Build();
        _root.SetActive(!_root.activeSelf);
        if (_root.activeSelf) { ResolveBen(); Refresh(); }
    }

    void ResolveBen()
    {
        PartyRoster.EnsureInitialized();
        _ben = null;
        foreach (var m in RestSystem.PartyMembers)
            if (m != null && m.role == CombatantRole.Benidito) { _ben = m; break; }
    }

    List<AbsorbedSkillInstance> Skills =>
        _ben != null ? _ben.absorbedSkills : new List<AbsorbedSkillInstance>();

    // ── interactions ─────────────────────────────────────────────────────────

    void SlotOrb()
    {
        var s = Current(); if (s == null) return;
        if (s.SlotOrb()) Refresh();
    }

    void RemoveOrb()
    {
        var s = Current(); if (s == null) return;
        if (s.UnslotOrb()) Refresh();
    }

    void Step(int dir)
    {
        int n = Skills.Count; if (n == 0) return;
        _index = (_index + dir + n) % n;
        Refresh();
    }

    AbsorbedSkillInstance Current()
    {
        var list = Skills;
        if (list.Count == 0) return null;
        _index = Mathf.Clamp(_index, 0, list.Count - 1);
        return list[_index];
    }

    // ── refresh ──────────────────────────────────────────────────────────────

    void Refresh()
    {
        var s = Current();
        bool has = s != null && s.definition != null;
        _slotBtn.interactable   = has && s.level < s.SlottableMax;
        _removeBtn.interactable = has && s.level > 1;
        _prevBtn.gameObject.SetActive(Skills.Count > 1);
        _nextBtn.gameObject.SetActive(Skills.Count > 1);

        if (!has)
        {
            _title.text = "NO ORBS YET";
            _rank.text  = "";
            _desc.text  = "Slay a curse-touched creature and absorb what it leaves behind.";
            _stats.text = "";
            _banked.text = "";
            _rankRows.text = "";
            _bigOrb.color = SocketEmpty;
            foreach (var so in _sockets) so.color = SocketEmpty;
            return;
        }

        var def = s.EffectiveDefinition;
        int maxLv = s.MaxLevel;
        _title.text = def.skillName.ToUpperInvariant();
        _rank.text  = $"Rank {Roman(s.level)} / {Roman(maxLv)}";
        _desc.text  = def.description;

        int dmg = def.basePower + s.level;
        _stats.text =
            $"Damage\t<color=#EBD79A>{dmg}</color>  <color=#7DBE6A>(+{s.level})</color>\n" +
            $"Hit\t<color=#EBD79A>{Mathf.RoundToInt(def.baseHit * 100)}%</color>\n" +
            $"Range\t<color=#EBD79A>{def.range}</color>{(def.areaOfEffect > 0 ? $"  ·  AoE {def.areaOfEffect}" : "")}\n" +
            $"SP\t<color=#EBD79A>{s.GetEffectiveSPCost()}</color>" +
            (def.chargeTicks > 0 ? $"\nCharge\t<color=#EBD79A>{def.chargeTicks} CT</color>" : "");

        int banked = s.OwnedOrbs - s.level;
        _banked.text = $"{s.level} / {maxLv}   ·   orbs in reserve: {Mathf.Max(0, banked)}";

        Color orb = s.isRefined ? OrbHoly :
                    def.damageType == DamageType.Poison ? OrbPoison : OrbFire;
        _bigOrb.color = orb;
        for (int i = 0; i < _sockets.Count; i++)
            _sockets[i].color = i < s.level - 1 ? orb : SocketEmpty;

        var rows = new System.Text.StringBuilder();
        for (int lv = 1; lv <= maxLv; lv++)
        {
            bool cur = lv == s.level;
            string open = cur ? "<color=#EBD79A>" : "<color=#9E9482>";
            rows.AppendLine($"{open}{(cur ? "> " : "  ")}RANK {Roman(lv)}   " +
                            $"dmg {def.basePower + lv}  ·  SP {def.spCost + (lv - 1)}</color>");
        }
        _rankRows.text = rows.ToString();
    }

    static string Roman(int n) => n switch
    { 1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V", 6 => "VI", 7 => "VII", _ => n.ToString() };

    // ── construction (code-built, mockup layout) ─────────────────────────────

    void Build()
    {
        var go = new GameObject("SkillOrbMenu_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(transform, false);
        _canvas = go.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 450;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));

        _root = Panel(go.transform, new Vector2(1460, 830), Vector2.zero, PanelBg, PanelEdge);

        Text(_root.transform, "SKILL ORB", headerFont, 44, Gold, new Vector2(0, 368), new Vector2(800, 60));
        Text(_root.transform, "SLAY.  ABSORB.  RANK UP.", bodyFont, 20, Dim, new Vector2(0, 326), new Vector2(800, 30));

        // left column — identity + stats
        var left = Panel(_root.transform, new Vector2(360, 620), new Vector2(-510, -40), new Color(0, 0, 0, 0.35f), PanelEdge * 0.6f);
        _title = Text(left.transform, "", headerFont, 30, new Color(0.95f, 0.45f, 0.15f), new Vector2(0, 262), new Vector2(330, 44));
        _rank  = Text(left.transform, "", bodyFont, 22, Parchment, new Vector2(0, 224), new Vector2(330, 30));
        _desc  = Text(left.transform, "", bodyFont, 19, Parchment, new Vector2(0, 110), new Vector2(316, 170));
        _desc.alignment = TextAlignmentOptions.TopLeft;
        _stats = Text(left.transform, "", bodyFont, 21, Parchment, new Vector2(0, -110), new Vector2(316, 220));
        _stats.alignment = TextAlignmentOptions.TopLeft;

        // center — the orb and its satellites
        _socketRing = new GameObject("OrbRing", typeof(RectTransform)).transform;
        _socketRing.SetParent(_root.transform, false);
        ((RectTransform)_socketRing).anchoredPosition = new Vector2(-60, 20);
        _bigOrb = Circle(_socketRing, 190, Vector2.zero, OrbFire);
        _sockets.Clear();
        // satellites fill clockwise from the top
        Vector2[] at = { new(0, 150), new(150, 0), new(0, -150), new(-150, 0) };
        for (int i = 0; i < at.Length; i++)
            _sockets.Add(Circle(_socketRing, 72, at[i], SocketEmpty));

        _banked = Text(_root.transform, "", bodyFont, 22, Parchment, new Vector2(-60, -215), new Vector2(500, 30));

        // actions
        _slotBtn   = ActionButton(_root.transform, "SLOT ORB", new Vector2(-165, -290), SlotOrb);
        _removeBtn = ActionButton(_root.transform, "REMOVE ORB", new Vector2(45, -290), RemoveOrb);
        _prevBtn   = ActionButton(_root.transform, "<", new Vector2(-330, 20), () => Step(-1), 54);
        _nextBtn   = ActionButton(_root.transform, ">", new Vector2(210, 20), () => Step(1), 54);
        ActionButton(_root.transform, "CLOSE", new Vector2(640, 368), () => _root.SetActive(false), 120);

        // right column — rank progression
        var right = Panel(_root.transform, new Vector2(420, 620), new Vector2(490, -40), new Color(0, 0, 0, 0.35f), PanelEdge * 0.6f);
        Text(right.transform, "RANK PROGRESSION", headerFont, 24, Gold, new Vector2(0, 272), new Vector2(390, 36));
        _rankRows = Text(right.transform, "", bodyFont, 22, Parchment, new Vector2(0, -30), new Vector2(380, 520));
        _rankRows.alignment = TextAlignmentOptions.TopLeft;

        _root.SetActive(false);
    }

    GameObject Panel(Transform parent, Vector2 size, Vector2 pos, Color bg, Color edge)
    {
        var p = new GameObject("Panel", typeof(Image), typeof(Outline));
        p.transform.SetParent(parent, false);
        var img = p.GetComponent<Image>();
        img.color = bg;
        var o = p.GetComponent<Outline>();
        o.effectColor = edge;
        o.effectDistance = new Vector2(2, -2);
        var rt = (RectTransform)p.transform;
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        return p;
    }

    TextMeshProUGUI Text(Transform parent, string txt, TMP_FontAsset font, float size, Color color, Vector2 pos, Vector2 box)
    {
        var t = new GameObject("Text", typeof(TextMeshProUGUI));
        t.transform.SetParent(parent, false);
        var tm = t.GetComponent<TextMeshProUGUI>();
        tm.text = txt;
        if (font != null) tm.font = font;
        tm.fontSize = size;
        tm.color = color;
        tm.alignment = TextAlignmentOptions.Center;
        tm.richText = true;
        var rt = (RectTransform)t.transform;
        rt.sizeDelta = box;
        rt.anchoredPosition = pos;
        return tm;
    }

    Image Circle(Transform parent, float dia, Vector2 pos, Color color)
    {
        var c = new GameObject("Orb", typeof(Image), typeof(Outline));
        c.transform.SetParent(parent, false);
        var img = c.GetComponent<Image>();
        img.sprite = RadialOrbSprite;
        img.color = color;
        var o = c.GetComponent<Outline>();
        o.effectColor = PanelEdge * 0.7f;    // socket rim — empty sockets stay legible
        o.effectDistance = new Vector2(2, -2);
        var rt = (RectTransform)c.transform;
        rt.sizeDelta = new Vector2(dia, dia);
        rt.anchoredPosition = pos;
        return img;
    }

    Button ActionButton(Transform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick, float w = 190)
    {
        var b = new GameObject(label, typeof(Image), typeof(Button), typeof(Outline));
        b.transform.SetParent(parent, false);
        var img = b.GetComponent<Image>();
        img.color = new Color(0.32f, 0.10f, 0.08f, 0.95f);
        b.GetComponent<Outline>().effectColor = PanelEdge;
        var rt = (RectTransform)b.transform;
        rt.sizeDelta = new Vector2(w, 46);
        rt.anchoredPosition = pos;
        var btn = b.GetComponent<Button>();
        btn.onClick.AddListener(onClick);
        var colors = btn.colors;
        colors.disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);
        btn.colors = colors;
        Text(b.transform, label, headerFont, 20, Parchment, Vector2.zero, new Vector2(w - 8, 40));
        return btn;
    }

    // soft round orb sprite, generated once (no art dependency)
    static Sprite _orbSprite;
    static Sprite RadialOrbSprite
    {
        get
        {
            if (_orbSprite != null) return _orbSprite;
            const int N = 96;
            var tex = new Texture2D(N, N, TextureFormat.ARGB32, false);
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N - 0.5f, dy = (y + 0.5f) / N - 0.5f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                    float a = 1f - Mathf.SmoothStep(0.86f, 1f, r);
                    float core = 1f - Mathf.SmoothStep(0f, 0.9f, r) * 0.35f;   // brighter center
                    px[x + y * N] = new Color(core, core, core, a);
                }
            tex.SetPixels32(px);
            tex.Apply();
            _orbSprite = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f));
            return _orbSprite;
        }
    }
}
