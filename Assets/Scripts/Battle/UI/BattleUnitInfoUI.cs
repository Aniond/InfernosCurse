using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Unit info card (bottom right): portrait, name, and HP as both a number and
// a colored bar, for whichever living unit sits under the battle cursor.
// Self-builds its canvas — BattleManager spawns one per battle, so kits and
// prefabs need no rebuild.
public class BattleUnitInfoUI : MonoBehaviour
{
    BattleCursor  _cursor;
    GameObject    _panel;
    Image         _portrait;
    TMP_Text      _nameLabel;
    TMP_Text      _hpLabel;
    RectTransform _hpFill;
    Image         _hpFillImg;

    static readonly Color HpHigh = new Color(0.35f, 0.75f, 0.30f); // healthy green
    static readonly Color HpMid  = new Color(0.85f, 0.70f, 0.20f); // worn gold
    static readonly Color HpLow  = new Color(0.80f, 0.20f, 0.12f); // danger red

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

        _panel = new GameObject("UnitInfoPanel");
        _panel.transform.SetParent(canvasGo.transform, false);
        var bg = _panel.AddComponent<Image>();
        bg.color = new Color(0.10f, 0.08f, 0.06f, 0.92f);
        var prt = _panel.GetComponent<RectTransform>();
        prt.anchorMin = new Vector2(1f, 0f);
        prt.anchorMax = new Vector2(1f, 0f);
        prt.pivot     = new Vector2(1f, 0f);
        prt.anchoredPosition = new Vector2(-16f, 16f);
        prt.sizeDelta = new Vector2(400f, 120f);

        // gold keyline (matches forecast/action-menu chrome)
        var line = new GameObject("Keyline");
        line.transform.SetParent(_panel.transform, false);
        var li = line.AddComponent<Image>();
        li.color = new Color(0.85f, 0.68f, 0.25f, 0.8f);
        var lrt = li.rectTransform;
        lrt.anchorMin = new Vector2(0f, 1f); lrt.anchorMax = Vector2.one;
        lrt.pivot = new Vector2(0.5f, 1f);
        lrt.anchoredPosition = Vector2.zero;
        lrt.sizeDelta = new Vector2(0f, 2f);

        // portrait (left)
        var pGo = new GameObject("Portrait");
        pGo.transform.SetParent(_panel.transform, false);
        _portrait = pGo.AddComponent<Image>();
        _portrait.preserveAspect = true;
        var prt2 = _portrait.rectTransform;
        prt2.anchorMin = new Vector2(0f, 0.5f);
        prt2.anchorMax = new Vector2(0f, 0.5f);
        prt2.pivot     = new Vector2(0f, 0.5f);
        prt2.anchoredPosition = new Vector2(12f, 0f);
        prt2.sizeDelta = new Vector2(92f, 92f);

        // name (top right of portrait)
        var nGo = new GameObject("Name");
        nGo.transform.SetParent(_panel.transform, false);
        _nameLabel = nGo.AddComponent<TextMeshProUGUI>();
        _nameLabel.fontSize = 24;
        _nameLabel.alignment = TextAlignmentOptions.TopLeft;
        _nameLabel.color = Color.white;
        var nrt = _nameLabel.rectTransform;
        nrt.anchorMin = new Vector2(0f, 1f); nrt.anchorMax = new Vector2(1f, 1f);
        nrt.pivot = new Vector2(0.5f, 1f);
        nrt.anchoredPosition = new Vector2(60f, -12f);
        nrt.sizeDelta = new Vector2(-140f, 30f);

        // HP bar background
        var bGo = new GameObject("HPBarBG");
        bGo.transform.SetParent(_panel.transform, false);
        var bbg = bGo.AddComponent<Image>();
        bbg.color = new Color(0f, 0f, 0f, 0.6f);
        var brt = bbg.rectTransform;
        brt.anchorMin = new Vector2(0f, 0f); brt.anchorMax = new Vector2(1f, 0f);
        brt.pivot = new Vector2(0.5f, 0f);
        brt.anchoredPosition = new Vector2(60f, 40f);
        brt.sizeDelta = new Vector2(-140f, 22f);

        // HP bar fill — width driven by anchorMax.x = hp ratio
        var fGo = new GameObject("HPBarFill");
        fGo.transform.SetParent(bGo.transform, false);
        _hpFillImg = fGo.AddComponent<Image>();
        _hpFillImg.color = HpHigh;
        _hpFill = _hpFillImg.rectTransform;
        _hpFill.anchorMin = Vector2.zero;
        _hpFill.anchorMax = new Vector2(1f, 1f);
        _hpFill.offsetMin = new Vector2(2f, 2f);
        _hpFill.offsetMax = new Vector2(-2f, -2f);

        // HP number (under the bar)
        var hGo = new GameObject("HPText");
        hGo.transform.SetParent(_panel.transform, false);
        _hpLabel = hGo.AddComponent<TextMeshProUGUI>();
        _hpLabel.fontSize = 19;
        _hpLabel.alignment = TextAlignmentOptions.BottomLeft;
        _hpLabel.color = Color.white;
        var hrt = _hpLabel.rectTransform;
        hrt.anchorMin = new Vector2(0f, 0f); hrt.anchorMax = new Vector2(1f, 0f);
        hrt.pivot = new Vector2(0.5f, 0f);
        hrt.anchoredPosition = new Vector2(60f, 12f);
        hrt.sizeDelta = new Vector2(-140f, 24f);

        _panel.SetActive(false);
    }

    void Update()
    {
        var bm = BattleManager.Instance;
        if (bm == null || _cursor == null) { Show(false); return; }

        var unit = bm.Grid?.GetCell(_cursor.Position)?.occupant;
        if (unit == null || !unit.IsAlive || unit.Data == null) { Show(false); return; }

        Show(true);

        var data  = unit.Data;
        int hp    = data.currentHP;
        int hpMax = data.GetTotalStats().hpMax;
        float ratio = hpMax > 0 ? Mathf.Clamp01(hp / (float)hpMax) : 0f;

        _nameLabel.text = data.displayName;
        _hpLabel.text   = $"HP  {hp} / {hpMax}";

        _hpFill.anchorMax = new Vector2(ratio, 1f);
        _hpFillImg.color  = ratio > 0.5f ? HpHigh : (ratio > 0.25f ? HpMid : HpLow);

        var sprite = data.portrait != null ? data.portrait : data.battleSprite;
        _portrait.sprite = sprite;
        _portrait.enabled = sprite != null;
    }

    void Show(bool on)
    {
        if (_panel != null && _panel.activeSelf != on) _panel.SetActive(on);
    }
}
