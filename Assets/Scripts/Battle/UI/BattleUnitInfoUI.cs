using UnityEngine;
using UnityEngine.UI;
using TMPro;

// FFT-style unit status bar (bottom center): portrait, name + job, and
// HP / SP / CT rows — each a colored bar plus the exact numbers.
// Shows the unit under the cursor; falls back to the ACTIVE unit so the bar
// is always talking about someone during a turn. Self-builds its canvas —
// BattleManager spawns one per battle, so kits need no prefab rebuild.
public class BattleUnitInfoUI : MonoBehaviour
{
    BattleCursor  _cursor;
    GameObject    _panel;
    Image         _portrait;
    TMP_Text      _nameLabel;
    TMP_Text      _jobLabel;
    readonly Image[] _statusIcons = new Image[6];

    struct StatRow
    {
        public RectTransform fill;
        public Image         fillImg;
        public TMP_Text      value;
    }
    StatRow _hp, _sp, _ct;

    static readonly Color SpColor = new Color(0.35f, 0.45f, 0.62f); // slate blue
    static readonly Color CtColor = new Color(0.72f, 0.60f, 0.35f); // brass

    void Awake()  => BuildUI();
    void Start()  => _cursor = FindAnyObjectByType<BattleCursor>();

    void BuildUI()
    {
        var canvasGo = new GameObject("UnitInfoCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        // Bottom-center status bar, FFT-style.
        var face = BattleUITheme.MakePanel(canvasGo.transform, "UnitInfoPanel",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 16f), new Vector2(620f, 140f));
        _panel = face.parent.gameObject;

        // Portrait (left) in its own gold frame.
        var pFace = BattleUITheme.MakePanel(face, "PortraitFrame",
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(12f, 0f), new Vector2(108f, 108f));
        var pGo = new GameObject("Portrait");
        pGo.transform.SetParent(pFace, false);
        _portrait = pGo.AddComponent<Image>();
        _portrait.preserveAspect = true;
        var prt = _portrait.rectTransform;
        prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
        prt.offsetMin = new Vector2(3f, 3f);
        prt.offsetMax = new Vector2(-3f, -3f);

        // Stat rows (middle column): HP / SP / CT.
        _hp = MakeStatRow(face, "HP", 0, BattleUITheme.HpHigh);
        _sp = MakeStatRow(face, "SP", 1, SpColor);
        _ct = MakeStatRow(face, "CT", 2, CtColor);

        // Name + job (right column).
        _nameLabel = BattleUITheme.MakeHeader(face, "Name", 24f);
        _nameLabel.alignment = TextAlignmentOptions.TopLeft;
        var nrt = _nameLabel.rectTransform;
        nrt.anchorMin = new Vector2(1f, 1f); nrt.anchorMax = new Vector2(1f, 1f);
        nrt.pivot = new Vector2(1f, 1f);
        nrt.anchoredPosition = new Vector2(-14f, -14f);
        nrt.sizeDelta = new Vector2(170f, 32f);

        _jobLabel = BattleUITheme.MakeBody(face, "Job", 18f);
        _jobLabel.alignment = TextAlignmentOptions.TopLeft;
        _jobLabel.color = BattleUITheme.ParchDim;
        var jrt = _jobLabel.rectTransform;
        jrt.anchorMin = new Vector2(1f, 1f); jrt.anchorMax = new Vector2(1f, 1f);
        jrt.pivot = new Vector2(1f, 1f);
        jrt.anchoredPosition = new Vector2(-14f, -48f);
        jrt.sizeDelta = new Vector2(170f, 26f);

        for (int i = 0; i < _statusIcons.Length; i++)
        {
            var iconGo = new GameObject($"StatusIcon_{i}");
            iconGo.transform.SetParent(face, false);
            var icon = iconGo.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            var rt = icon.rectTransform;
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-14f - i * 27f, -80f);
            rt.sizeDelta = new Vector2(24f, 24f);
            icon.enabled = false;
            _statusIcons[i] = icon;
        }

        _panel.SetActive(false);
    }

    StatRow MakeStatRow(Transform parent, string statName, int rowIndex, Color fillColor)
    {
        float y = -18f - rowIndex * 36f;

        var label = BattleUITheme.MakeHeader(parent, $"{statName}Label", 17f);
        label.text = statName;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        var lrt = label.rectTransform;
        lrt.anchorMin = new Vector2(0f, 1f); lrt.anchorMax = new Vector2(0f, 1f);
        lrt.pivot = new Vector2(0f, 1f);
        lrt.anchoredPosition = new Vector2(134f, y);
        lrt.sizeDelta = new Vector2(36f, 28f);

        var bGo = new GameObject($"{statName}BarBG");
        bGo.transform.SetParent(parent, false);
        var bbg = bGo.AddComponent<Image>();
        bbg.color = BattleUITheme.Ink;
        var brt = bbg.rectTransform;
        brt.anchorMin = new Vector2(0f, 1f); brt.anchorMax = new Vector2(0f, 1f);
        brt.pivot = new Vector2(0f, 1f);
        brt.anchoredPosition = new Vector2(174f, y - 4f);
        brt.sizeDelta = new Vector2(160f, 18f);

        var fGo = new GameObject($"{statName}BarFill");
        fGo.transform.SetParent(bGo.transform, false);
        var fillImg = fGo.AddComponent<Image>();
        fillImg.color = fillColor;
        var fill = fillImg.rectTransform;
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = Vector2.one;
        fill.offsetMin = new Vector2(2f, 2f);
        fill.offsetMax = new Vector2(-2f, -2f);

        var value = BattleUITheme.MakeBody(parent, $"{statName}Value", 17f);
        value.alignment = TextAlignmentOptions.MidlineLeft;
        var vrt = value.rectTransform;
        vrt.anchorMin = new Vector2(0f, 1f); vrt.anchorMax = new Vector2(0f, 1f);
        vrt.pivot = new Vector2(0f, 1f);
        vrt.anchoredPosition = new Vector2(342f, y);
        vrt.sizeDelta = new Vector2(110f, 28f);

        return new StatRow { fill = fill, fillImg = fillImg, value = value };
    }

    void Update()
    {
        var bm = BattleManager.Instance;
        if (bm == null) { Show(false); return; }

        // Hovered unit wins; otherwise talk about whoever's turn it is.
        BattleUnit unit = null;
        if (_cursor != null) unit = bm.Grid?.GetCell(_cursor.Position)?.occupant;
        if (unit == null || !unit.IsAlive) unit = bm.ActiveUnit;
        if (unit == null || !unit.IsAlive || unit.Data == null) { Show(false); return; }

        Show(true);

        var data  = unit.Data;
        var stats = data.GetTotalStats();

        _nameLabel.text = data.displayName;
        var job = data.activeJob != null ? data.activeJob.job : null;
        _jobLabel.text = job != null ? job.jobName : (unit.IsPlayer ? "" : "Infernal");

        SetRow(_hp, data.currentHP, stats.hpMax, true);
        SetRow(_sp, data.currentSP, stats.spMax, false);
        SetRow(_ct, Mathf.Min(100, Mathf.FloorToInt(unit.ct)), 100, false);

        var sprite = data.portrait != null ? data.portrait : data.battleSprite;
        _portrait.sprite = sprite;
        _portrait.enabled = sprite != null;

        var catalog = StatusEffectPresentationCatalog.Instance;
        int statusIndex = 0;
        foreach (var effect in unit.Status.All)
        {
            if (statusIndex >= _statusIcons.Length) break;
            Sprite icon = catalog?.GetIcon(effect.type);
            if (icon == null) continue;
            _statusIcons[statusIndex].sprite = icon;
            _statusIcons[statusIndex].enabled = true;
            statusIndex++;
        }
        for (int i = statusIndex; i < _statusIcons.Length; i++)
        {
            _statusIcons[i].sprite = null;
            _statusIcons[i].enabled = false;
        }
    }

    void SetRow(StatRow row, int cur, int max, bool hpColors)
    {
        float ratio = max > 0 ? Mathf.Clamp01(cur / (float)max) : 0f;
        row.fill.anchorMax = new Vector2(ratio, 1f);
        row.value.text = $"{cur} / {max}";
        if (hpColors)
            row.fillImg.color = ratio > 0.5f ? BattleUITheme.HpHigh
                              : (ratio > 0.25f ? BattleUITheme.HpMid : BattleUITheme.HpLow);
    }

    void Show(bool on)
    {
        if (_panel != null && _panel.activeSelf != on) _panel.SetActive(on);
    }
}
