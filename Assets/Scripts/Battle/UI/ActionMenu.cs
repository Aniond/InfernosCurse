using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
using System.Collections.Generic;

// FFT-style TIERED action menu (David 7/09, modeled on FFT's command list):
//   Tier 1 — commands:  Move / Act / End Turn (+ Undo Move after moving)
//   Tier 2 — Act:       the skill list (actives + absorbed) with Back
// Navigable by keyboard or click. Spent options grey out.
public class ActionMenu : MonoBehaviour
{
    [Header("Panel")]
    public GameObject panel;

    [Header("Skill Buttons (4 slots)")]
    public SkillButton[] skillButtons = new SkillButton[4];

    [Header("Absorbed Skill Buttons (3 slots, Benidito only)")]
    public SkillButton[] absorbedSkillButtons = new SkillButton[3];

    [Header("Wait / Back Buttons")]
    public Button waitButton;   // relabeled "End Turn"
    public Button backButton;   // tier 1: "Undo Move"; tier 2: "Back"

    [Header("Runtime-cloned commands (prefab predates them)")]
    public Button moveButton;
    public Button actButton;

    [Header("Detail Panel")]
    public GameObject     detailPanel;
    public TMP_Text       detailSkillName;
    public TMP_Text       detailDescription;
    public TMP_Text       detailPower;
    public TMP_Text       detailRange;
    public TMP_Text       detailSPCost;
    public TMP_Text       detailDamageType;
    public Image          detailIcon;

    // ── Private ───────────────────────────────────────────────────────────────
    enum Tier { Commands, Skills }
    Tier _tier = Tier.Commands;

    BattleUnit _activeUnit;

    // The currently visible, selectable rows (rebuilt on every tier switch).
    struct Entry
    {
        public Button      button;       // command entries
        public SkillButton skill;        // skill entries
        public System.Action onConfirm;
        public bool        usable;
    }
    readonly List<Entry> _entries = new();
    int _selected;

    const float RowH = 54f, RowTop = -12f, RowW = 216f;

    void Start()
    {
        if (moveButton == null && waitButton != null) moveButton = CloneCommand(waitButton, "MoveBtn", "Move");
        if (actButton  == null && waitButton != null) actButton  = CloneCommand(waitButton, "ActBtn", "Act");

        moveButton?.onClick.AddListener(OnMove);
        actButton?.onClick.AddListener(OnAct);
        waitButton?.onClick.AddListener(OnWait);
        backButton?.onClick.AddListener(OnBack);

        SetLabel(waitButton, "End Turn");

        ApplyTheme();

        panel?.SetActive(false);
        detailPanel?.SetActive(false);

        if (BattleManager.Instance != null)
            BattleManager.Instance.OnStateChanged += OnStateChanged;
    }

    void OnDestroy()
    {
        if (BattleManager.Instance != null)
            BattleManager.Instance.OnStateChanged -= OnStateChanged;
    }

    Button CloneCommand(Button template, string name, string label)
    {
        var go = Instantiate(template.gameObject, template.transform.parent);
        go.name = name;
        var l = go.GetComponentInChildren<TMP_Text>(true);
        if (l != null) l.text = label;
        var btn = go.GetComponent<Button>();
        btn.onClick.RemoveAllListeners();
        return btn;
    }

    static void SetLabel(Button b, string text)
    {
        var l = b != null ? b.GetComponentInChildren<TMP_Text>(true) : null;
        if (l != null) l.text = text;
    }

    void ApplyTheme()
    {
        var panelImg = panel != null ? panel.GetComponent<Image>() : null;
        if (panelImg != null) panelImg.color = BattleUITheme.Ink;

        StyleCommandButton(moveButton);
        StyleCommandButton(actButton);
        StyleCommandButton(waitButton);
        StyleCommandButton(backButton);

        var detailImg = detailPanel != null ? detailPanel.GetComponent<Image>() : null;
        if (detailImg != null) detailImg.color = BattleUITheme.Ink;
        BattleUITheme.StyleHeader(detailSkillName);
        BattleUITheme.StyleBody(detailDescription);
        BattleUITheme.StyleBody(detailPower);
        BattleUITheme.StyleBody(detailRange);
        BattleUITheme.StyleBody(detailSPCost);
        BattleUITheme.StyleBody(detailDamageType);
    }

    static void StyleCommandButton(Button b)
    {
        if (b == null) return;
        var img = b.GetComponent<Image>();
        if (img != null) img.color = SkillButton.NormalColor;
        var label = b.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            BattleUITheme.StyleHeader(label);
            label.color = BattleUITheme.Parchment;
        }
    }

    // ── State-driven open/close ───────────────────────────────────────────────

    void OnStateChanged(BattleState state)
    {
        if (state == BattleState.PlayerSelectAction)
        {
            _activeUnit = BattleManager.Instance?.ActiveUnit;
            Open(_activeUnit);
        }
        else
        {
            Close();
        }
    }

    // ── Open / Close ──────────────────────────────────────────────────────────

    public void Open(BattleUnit unit)
    {
        if (unit == null) return;
        _activeUnit = unit;
        panel?.SetActive(true);

        // (Re)populate skill slots so tier 2 is ready when Act is chosen.
        var slots = unit.Data.equippedSkills.actives;
        for (int i = 0; i < skillButtons.Length; i++)
        {
            if (skillButtons[i] == null) continue;
            var skill = (i < slots.Length) ? slots[i] : null;
            skillButtons[i].Setup(skill, i, this, unit);
        }
        var absorbed = unit.Data.equippedSkills.absorbed;
        for (int i = 0; i < absorbedSkillButtons.Length; i++)
        {
            if (absorbedSkillButtons[i] == null) continue;
            var instance = (i < absorbed.Length) ? absorbed[i] : null;
            absorbedSkillButtons[i].SetupAbsorbed(instance, i, this, unit);
        }

        ShowCommands();
    }

    public void Close()
    {
        panel?.SetActive(false);
        detailPanel?.SetActive(false);
    }

    // ── Tiers ─────────────────────────────────────────────────────────────────

    void ShowCommands()
    {
        _tier = Tier.Commands;
        detailPanel?.SetActive(false);

        foreach (var b in skillButtons)         if (b != null) b.gameObject.SetActive(false);
        foreach (var b in absorbedSkillButtons) if (b != null) b.gameObject.SetActive(false);

        var bm = BattleManager.Instance;
        bool hasMoved = bm != null && bm.HasMoved;
        bool hasActed = bm != null && bm.HasActed;

        _entries.Clear();
        AddCommand(moveButton, "Move", !hasMoved, OnMove);
        AddCommand(actButton, "Act", !hasActed, OnAct);
        if (hasMoved && !hasActed)
            AddCommand(backButton, "Undo Move", true, OnUndoMove);
        else if (backButton != null)
            backButton.gameObject.SetActive(false);
        AddCommand(waitButton, "End Turn", true, OnWait);

        LayoutEntries();
        SelectFirstUsable();
    }

    void ShowSkills()
    {
        _tier = Tier.Skills;

        if (moveButton) moveButton.gameObject.SetActive(false);
        if (actButton)  actButton.gameObject.SetActive(false);

        // Only real skills get a row — empty slots would render as "—" filler
        // (David 7/09 screenshot) and stretch the panel with dead rows.
        // Exhausted options (no SP, or nothing in range to hit) grey out HARD:
        // unclickable, and the keyboard cursor skips them (David 7/09).
        var bm = BattleManager.Instance;
        _entries.Clear();
        foreach (var b in skillButtons)
        {
            if (b == null) continue;
            b.gameObject.SetActive(b.HasSkill);
            if (!b.HasSkill) continue;
            b.SetUsable(bm == null || bm.SkillHasTarget(_activeUnit, b.Skill));
            var captured = b;
            _entries.Add(new Entry { skill = b, usable = b.Usable, onConfirm = () => captured.OnClick() });
        }
        foreach (var b in absorbedSkillButtons)
        {
            if (b == null) continue;
            b.gameObject.SetActive(b.HasSkill);
            if (!b.HasSkill) continue;
            b.SetUsable(bm == null || bm.SkillHasTarget(_activeUnit, b.Skill));
            var captured = b;
            _entries.Add(new Entry { skill = b, usable = b.Usable, onConfirm = () => captured.OnClick() });
        }
        AddCommand(backButton, "Back", true, OnBack);
        // Escape hatch: nothing in range → end the turn without backing out
        // through the command tier first (David 7/09).
        AddCommand(waitButton, "End Turn", true, OnWait);

        LayoutEntries();
        SelectFirstUsable();
    }

    void AddCommand(Button b, string label, bool usable, System.Action onConfirm)
    {
        if (b == null) return;
        b.gameObject.SetActive(true);
        SetLabel(b, label);
        b.interactable = usable;
        var l = b.GetComponentInChildren<TMP_Text>(true);
        if (l != null) l.color = usable ? BattleUITheme.Parchment : BattleUITheme.ParchDim;
        _entries.Add(new Entry { button = b, usable = usable, onConfirm = onConfirm });
    }

    void LayoutEntries()
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            var go = _entries[i].button != null ? _entries[i].button.gameObject
                   : _entries[i].skill  != null ? _entries[i].skill.gameObject : null;
            if (go == null) continue;
            var rt = (RectTransform)go.transform;
            rt.anchoredPosition = new Vector2(0f, RowTop - i * RowH);
        }
        if (panel != null)
        {
            var prt = (RectTransform)panel.transform;
            prt.sizeDelta = new Vector2(240f, -RowTop + _entries.Count * RowH + 12f);
        }
    }

    void SelectFirstUsable()
    {
        _selected = 0;
        for (int i = 0; i < _entries.Count; i++)
            if (_entries[i].usable) { _selected = i; break; }
        RefreshHighlights();
    }

    // ── Keyboard navigation ───────────────────────────────────────────────────

    void Update()
    {
        if (panel == null || !panel.activeSelf) return;

        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame)
            Navigate(-1);
        else if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame)
            Navigate(1);

        if (kb.zKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
            Confirm();

        if (kb.xKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)
        {
            if (_tier == Tier.Skills) OnBack();
        }
    }

    void Navigate(int dir)
    {
        if (_entries.Count == 0) return;
        for (int step = 1; step <= _entries.Count; step++)
        {
            int candidate = ((_selected + dir * step) % _entries.Count + _entries.Count) % _entries.Count;
            if (_entries[candidate].usable) { _selected = candidate; break; }
        }
        RefreshHighlights();
    }

    void Confirm()
    {
        if (_selected < 0 || _selected >= _entries.Count) return;
        var e = _entries[_selected];
        if (e.usable) e.onConfirm?.Invoke();
    }

    void RefreshHighlights()
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            bool on = i == _selected;
            var e = _entries[i];
            if (e.skill != null)
            {
                e.skill.SetHighlight(on);
                if (on && e.skill.HasSkill) ShowDetail(e.skill.Skill);
            }
            else if (e.button != null)
            {
                var img = e.button.GetComponent<Image>();
                if (img != null)
                    img.color = on ? SkillButton.HighlightColor
                              : (e.usable ? SkillButton.NormalColor : SkillButton.DisabledColor);
            }
        }
        if (_tier == Tier.Commands) detailPanel?.SetActive(false);
    }

    // ── Mouse-hover / click plumbing from SkillButton ─────────────────────────

    // SkillButton hover/click calls these with its slot index; map to entries.
    public void Select(int skillSlotIndex) => SelectSkillEntry(skillButtons, skillSlotIndex, 0);
    public void SelectAbsorbed(int absorbedSlotIndex) => SelectSkillEntry(absorbedSkillButtons, absorbedSlotIndex, 0);

    void SelectSkillEntry(SkillButton[] array, int slotIndex, int _)
    {
        if (_tier != Tier.Skills) return;
        if (slotIndex < 0 || slotIndex >= array.Length) return;
        var target = array[slotIndex];
        if (target == null || !target.Usable) return;   // hover can't land on exhausted options
        for (int i = 0; i < _entries.Count; i++)
            if (_entries[i].skill == target) { _selected = i; break; }
        RefreshHighlights();
    }

    // ── Command handlers ──────────────────────────────────────────────────────

    void OnMove()
    {
        var bm = BattleManager.Instance;
        if (bm == null || bm.HasMoved) return;
        bm.PlayerChooseMove();   // state change closes the menu
    }

    void OnAct()
    {
        var bm = BattleManager.Instance;
        if (bm == null || bm.HasActed) return;
        ShowSkills();
    }

    void OnWait()
    {
        BattleManager.Instance?.PlayerWait();
        Close();
    }

    void OnUndoMove()
    {
        BattleManager.Instance?.PlayerUndoMove();
        Close();
    }

    void OnBack()
    {
        if (_tier == Tier.Skills) ShowCommands();
    }

    // Called by SkillButton when the player clicks a skill. absorbedInstance
    // is non-null only for absorbed-slot buttons and is forwarded so the cast
    // uses its level/refine-scaled power instead of the definition's base.
    public void OnSkillChosen(SkillDefinition skill, int slotIndex, AbsorbedSkillInstance absorbedInstance = null)
    {
        if (_activeUnit == null || skill == null) return;

        if (!_activeUnit.HasSP(skill.spCost))
        {
            FlashNoSP(slotIndex, absorbedInstance != null);
            return;
        }

        // Fail check: nothing this skill could hit from here — say so and stay
        // in the menu instead of entering aiming with no legal target.
        var bmCheck = BattleManager.Instance;
        var effective = absorbedInstance != null ? absorbedInstance.EffectiveDefinition : skill;
        if (bmCheck != null && !bmCheck.SkillHasTarget(_activeUnit, effective))
        {
            FlashNoSP(slotIndex, absorbedInstance != null);
            ShowDetail(effective);
            if (detailDescription)
            {
                detailDescription.text  = "No target within range.";
                detailDescription.color = BattleUITheme.Blood;
            }
            return;
        }

        if (absorbedInstance != null)
            BattleManager.Instance?.PlayerSelectAbsorbedSkill(absorbedInstance);
        else
            BattleManager.Instance?.PlayerSelectSkill(skill);
        Close();
    }

    // ── Detail panel ──────────────────────────────────────────────────────────

    void ShowDetail(SkillDefinition skill)
    {
        if (detailPanel == null) return;
        detailPanel.SetActive(true);

        if (detailSkillName)   detailSkillName.text   = skill.skillName;
        if (detailDescription)
        {
            detailDescription.text  = skill.description;
            detailDescription.color = BattleUITheme.Parchment;   // clear any notice tint
        }
        if (detailPower)       detailPower.text        = $"Power: {skill.basePower}";
        if (detailRange)       detailRange.text        = $"Range: {skill.range}{(skill.areaOfEffect > 0 ? $"  AoE: {skill.areaOfEffect}" : "")}";
        if (detailSPCost)      detailSPCost.text       = $"SP: {skill.spCost}";
        if (detailDamageType)  detailDamageType.text   = skill.damageType.ToString();
        if (detailIcon)        detailIcon.sprite       = skill.icon;
        if (detailIcon)        detailIcon.gameObject.SetActive(skill.icon != null);
    }

    void FlashNoSP(int slotIndex, bool isAbsorbed = false)
    {
        if (isAbsorbed) absorbedSkillButtons[slotIndex]?.FlashInsufficient();
        else            skillButtons[slotIndex]?.FlashInsufficient();
    }
}
