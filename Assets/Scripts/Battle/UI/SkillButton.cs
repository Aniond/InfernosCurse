using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

// One skill slot button inside the ActionMenu.
[RequireComponent(typeof(Button))]
public class SkillButton : MonoBehaviour
{
    [Header("Visuals")]
    public TMP_Text  skillNameLabel;
    public TMP_Text  spCostLabel;
    public Image     icon;
    public Image     background;
    public Image     selectionBorder;

    public static readonly Color HighlightColor    = new Color(0.85f, 0.68f, 0.25f, 1.00f); // gold
    public static readonly Color NormalColor       = new Color(0.18f, 0.12f, 0.08f, 0.90f); // dark leather
    public static readonly Color DisabledColor     = new Color(0.30f, 0.25f, 0.22f, 0.70f); // greyed
    public static readonly Color InsufficientColor = new Color(0.80f, 0.15f, 0.10f, 0.90f); // red flash

    public SkillDefinition       Skill            { get; private set; }
    public bool                  HasSkill         => Skill != null;
    // Set only for absorbed-slot buttons — carries level/refine scaling back
    // to ActionMenu.OnSkillChosen so the cast can pass it through to the
    // resolver instead of using the definition's raw basePower.
    public AbsorbedSkillInstance AbsorbedInstance { get; private set; }

    private ActionMenu  _menu;
    private BattleUnit  _unit;
    private int         _slotIndex;
    private Button      _btn;
    private Coroutine   _flashRoutine;

    void Awake()
    {
        _btn = GetComponent<Button>();
        _btn.onClick.AddListener(OnClick);
    }

    // ── Setup ─────────────────────────────────────────────────────────────────

    public void Setup(SkillDefinition skill, int slotIndex, ActionMenu menu, BattleUnit unit)
    {
        Skill            = skill;
        AbsorbedInstance = null;
        _slotIndex       = slotIndex;
        _menu            = menu;
        _unit            = unit;

        if (skill == null)
        {
            SetEmpty();
            return;
        }

        _btn.interactable = true;

        if (skillNameLabel) skillNameLabel.text = skill.skillName;
        if (spCostLabel)    spCostLabel.text    = $"SP {skill.spCost}";
        if (icon)
        {
            icon.sprite = skill.icon;
            icon.gameObject.SetActive(skill.icon != null);
        }

        // Grey out if not enough SP
        bool canAfford = unit.HasSP(skill.spCost);
        SetBackground(canAfford ? NormalColor : DisabledColor);
        if (skillNameLabel) skillNameLabel.color = canAfford ? Color.white : new Color(0.6f, 0.6f, 0.6f);
    }

    // Absorbed-slot variant — label shows DisplayName() ("Holy X +N" / "X +N")
    // so corrupted vs refined state and level are visible at a glance.
    public void SetupAbsorbed(AbsorbedSkillInstance instance, int slotIndex, ActionMenu menu, BattleUnit unit)
    {
        AbsorbedInstance = instance;
        Skill            = instance?.EffectiveDefinition;
        _slotIndex       = slotIndex;
        _menu            = menu;
        _unit            = unit;

        if (Skill == null)
        {
            SetEmpty();
            return;
        }

        _btn.interactable = true;

        if (skillNameLabel) skillNameLabel.text = instance.DisplayName();
        if (spCostLabel)    spCostLabel.text    = $"SP {Skill.spCost}";
        if (icon)
        {
            icon.sprite = Skill.icon;
            icon.gameObject.SetActive(Skill.icon != null);
        }

        bool canAfford = unit.HasSP(Skill.spCost);
        SetBackground(canAfford ? NormalColor : DisabledColor);
        if (skillNameLabel) skillNameLabel.color = canAfford ? Color.white : new Color(0.6f, 0.6f, 0.6f);
    }

    // Grey a real skill without wiping its label — used when the turn's
    // action is already spent (menu-first flow shows the menu again for Move
    // / End Turn after acting).
    public void SetUsable(bool usable)
    {
        if (!HasSkill) return;
        _btn.interactable = usable;
        bool canAfford = usable && _unit != null && _unit.HasSP(Skill.spCost);
        SetBackground(canAfford ? NormalColor : DisabledColor);
        if (skillNameLabel) skillNameLabel.color = canAfford ? Color.white : new Color(0.6f, 0.6f, 0.6f);
    }

    void SetEmpty()
    {
        _btn.interactable = false;
        AbsorbedInstance = null;
        if (skillNameLabel) skillNameLabel.text  = "—";
        if (spCostLabel)    spCostLabel.text     = "";
        if (icon)           icon.gameObject.SetActive(false);
        SetBackground(DisabledColor);
    }

    // ── Click / Hover ─────────────────────────────────────────────────────────

    public void OnClick()
    {
        if (!HasSkill) return;
        if (AbsorbedInstance != null) _menu?.SelectAbsorbed(_slotIndex);
        else                          _menu?.Select(_slotIndex);
        _menu?.OnSkillChosen(Skill, _slotIndex, AbsorbedInstance);
    }

    // Called when mouse enters — mirrors keyboard highlight behavior
    public void OnPointerEnter()
    {
        _menu?.Select(_slotIndex);
    }

    // ── Highlight ─────────────────────────────────────────────────────────────

    public void SetHighlight(bool on)
    {
        if (selectionBorder) selectionBorder.gameObject.SetActive(on);
        SetBackground(on ? HighlightColor : (HasSkill && _unit != null && _unit.HasSP(Skill.spCost) ? NormalColor : DisabledColor));
    }

    // ── Insufficient SP flash ─────────────────────────────────────────────────

    public void FlashInsufficient()
    {
        if (_flashRoutine != null) StopCoroutine(_flashRoutine);
        _flashRoutine = StartCoroutine(FlashRed());
    }

    IEnumerator FlashRed()
    {
        for (int i = 0; i < 3; i++)
        {
            SetBackground(InsufficientColor);
            yield return new WaitForSecondsRealtime(0.1f);
            SetBackground(NormalColor);
            yield return new WaitForSecondsRealtime(0.08f);
        }
        _flashRoutine = null;
    }

    void SetBackground(Color c)
    {
        if (background) background.color = c;
    }
}
