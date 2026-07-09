using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Context-aware combat HUD panel (top center). Self-builds so battle scenes
// need only the bare component.
//   Move mode:   tiles this hop costs + what's left of the move budget.
//   Aim mode:    FFT-style forecast — damage/heal, hit%, target HP.
// Hidden in every other state.
public class BattleForecastUI : MonoBehaviour
{
    [Tooltip("Optional — assigned automatically if left empty (self-build).")]
    public TMP_Text label;

    private GameObject   _panel;
    private BattleCursor _cursor;

    void Awake()
    {
        if (label == null) BuildUI();
    }

    void Start()
    {
        _cursor = FindAnyObjectByType<BattleCursor>();
    }

    void BuildUI()
    {
        var canvasGo = new GameObject("ForecastCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        // Panel block — same dark-leather chrome as the action menu.
        _panel = new GameObject("ForecastPanel");
        _panel.transform.SetParent(canvasGo.transform, false);
        var bg = _panel.AddComponent<Image>();
        bg.color = new Color(0.10f, 0.08f, 0.06f, 0.92f);
        var prt = _panel.GetComponent<RectTransform>();
        prt.anchorMin = new Vector2(0.5f, 1f);
        prt.anchorMax = new Vector2(0.5f, 1f);
        prt.pivot     = new Vector2(0.5f, 1f);
        prt.anchoredPosition = new Vector2(0f, -16f);
        prt.sizeDelta = new Vector2(520f, 84f);

        // Thin gold keyline so the block reads as UI, not floating text.
        var line = new GameObject("Keyline");
        line.transform.SetParent(_panel.transform, false);
        var li = line.AddComponent<Image>();
        li.color = new Color(0.85f, 0.68f, 0.25f, 0.8f);
        var lrt = li.rectTransform;
        lrt.anchorMin = new Vector2(0f, 0f); lrt.anchorMax = new Vector2(1f, 0f);
        lrt.pivot = new Vector2(0.5f, 0f);
        lrt.anchoredPosition = Vector2.zero;
        lrt.sizeDelta = new Vector2(0f, 2f);

        var textGo = new GameObject("ForecastLabel");
        textGo.transform.SetParent(_panel.transform, false);
        label = textGo.AddComponent<TextMeshProUGUI>();
        label.fontSize  = 24;
        label.alignment = TextAlignmentOptions.Center;
        label.color     = Color.white;

        var rt = label.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(14f, 6f);
        rt.offsetMax = new Vector2(-14f, -6f);

        _panel.SetActive(false);
    }

    void Update()
    {
        var bm = BattleManager.Instance;
        if (bm == null || label == null) { Show(false); return; }

        if (bm.State == BattleState.PlayerSelectMove && _cursor != null)
        {
            ShowMoveForecast(bm);
            return;
        }

        var skill = bm.SelectedSkill;   // non-null only during PlayerSelectTarget
        if (skill == null || _cursor == null) { Show(false); return; }

        // Only forecast shots you could actually take: cursor must be on a
        // legal target cell, with a living occupant that isn't yourself
        // (the cursor starts on the active unit, which previewed Ben
        // attacking Ben — David 7/09). Healing may still target self.
        var user = bm.ActiveUnit;
        var target = bm.Grid?.GetCell(_cursor.Position)?.occupant;
        if (target == null || !target.IsAlive ||
            !bm.IsValidActionTarget(_cursor.Position) ||
            (target == user && !skill.isHealing))
        {
            Show(false);
            return;
        }

        Show(true);
        var absorbedInstance = bm.SelectedAbsorbedInstance;
        float powerOverride = absorbedInstance != null ? absorbedInstance.GetEffectivePower() : -1f;

        if (skill.isHealing)
        {
            int heal = BattleFormulas.CalcHeal(user, skill, powerOverride); // small variance — close enough for forecast
            label.text = $"{skill.skillName} → {target.Data.displayName}\n" +
                         $"heals ~{heal}   HP {target.Data.currentHP}/{target.Data.GetTotalStats().hpMax}";
        }
        else if (skill.damageType == DamageType.None)
        {
            string status = skill.appliesStatus ? $"{skill.statusType} {(int)(skill.statusChance * 100)}%" : "utility";
            label.text = $"{skill.skillName} → {target.Data.displayName}\n{status}";
        }
        else
        {
            var (min, max, hit) = BattleFormulas.PreviewAttack(user, target, skill, powerOverride);
            string status = skill.appliesStatus ? $"  +{skill.statusType} {(int)(skill.statusChance * 100)}%" : "";
            label.text = $"{skill.skillName} → {target.Data.displayName}\n" +
                         $"{min}-{max} dmg   {(int)(hit * 100)}% hit{status}   " +
                         $"HP {target.Data.currentHP}/{target.Data.GetTotalStats().hpMax}";
        }
    }

    // "Move  3 / 4 tiles — 1 left" for the hovered tile; budget-only when the
    // cursor sits on the unit or out of range.
    void ShowMoveForecast(BattleManager bm)
    {
        var unit = bm.ActiveUnit;
        if (unit == null) { Show(false); return; }

        Show(true);
        int budget = bm.MovePoints;
        var hovered = _cursor.Position;

        if (hovered == unit.gridPosition)
        {
            label.text = $"Move\n{budget} tiles available";
            return;
        }

        int cost = bm.MoveCostTo(hovered);
        if (cost < 0)
        {
            label.text = $"Move\nout of range   ({budget} tiles available)";
            return;
        }

        int left = budget - cost;
        label.text = $"Move\n{cost} / {budget} tiles — {left} left after";
    }

    void Show(bool on)
    {
        if (_panel != null) { if (_panel.activeSelf != on) _panel.SetActive(on); }
        else if (label != null && label.enabled != on) label.enabled = on;
    }
}
