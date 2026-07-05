using UnityEngine;
using TMPro;

// FFT-style forecast: while aiming a skill, shows expected damage/heal and hit%
// for the unit under the cursor. Self-builds its canvas text so battle scenes
// need only the bare component.
public class BattleForecastUI : MonoBehaviour
{
    [Tooltip("Optional — assigned automatically if left empty (self-build).")]
    public TMP_Text label;

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

        var textGo = new GameObject("ForecastLabel");
        textGo.transform.SetParent(canvasGo.transform, false);
        label = textGo.AddComponent<TextMeshProUGUI>();
        label.fontSize  = 26;
        label.alignment = TextAlignmentOptions.Top;
        label.color     = Color.white;
        label.outlineWidth = 0.2f;

        var rt = label.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot     = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -24f);
        rt.sizeDelta = new Vector2(640f, 90f);
    }

    void Update()
    {
        var bm = BattleManager.Instance;
        if (bm == null || label == null) return;

        var skill = bm.SelectedSkill;   // non-null only during PlayerSelectTarget
        if (skill == null || _cursor == null)
        {
            if (label.enabled) label.enabled = false;
            return;
        }

        var target = bm.Grid?.GetCell(_cursor.Position)?.occupant;
        if (target == null || !target.IsAlive)
        {
            if (label.enabled) label.enabled = false;
            return;
        }

        label.enabled = true;
        var user = bm.ActiveUnit;
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
}
