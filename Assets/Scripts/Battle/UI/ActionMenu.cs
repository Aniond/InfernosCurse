using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

// FFT-style action menu — appears after a unit finishes moving.
// Shows 4 skill slots + Wait button. Navigable by keyboard or click.
public class ActionMenu : MonoBehaviour
{
    [Header("Panel")]
    public GameObject panel;

    [Header("Skill Buttons (4 slots)")]
    public SkillButton[] skillButtons = new SkillButton[4];

    [Header("Absorbed Skill Buttons (3 slots, Benidito only)")]
    public SkillButton[] absorbedSkillButtons = new SkillButton[3];

    [Header("Wait / Back Buttons")]
    public Button waitButton;
    public Button backButton;

    [Header("Move Button (cloned from Wait at runtime when the prefab lacks one)")]
    public Button moveButton;

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
    private BattleUnit      _activeUnit;
    // Logical slot index spans both button arrays plus Wait:
    // [0..skillButtons.Length)                                   = actives
    // [skillButtons.Length..skillButtons.Length+absorbed.Length) = absorbed
    // last index                                                 = Wait
    private int _selectedIndex = 0;
    // Logical order: actives, absorbed, Move, End Turn.
    int SlotCount => skillButtons.Length + absorbedSkillButtons.Length + 2;
    int MoveIndex => SlotCount - 2;
    int WaitIndex => SlotCount - 1;

    void Start()
    {
        // Menu-first turn flow (David 7/09): the menu needs a Move entry, but
        // the built BattleKit prefab predates it — clone the Wait button so
        // every existing kit gets one without a prefab rebuild.
        if (moveButton == null && waitButton != null)
            moveButton = CloneAsMoveButton(waitButton);

        moveButton?.onClick.AddListener(OnMove);
        waitButton?.onClick.AddListener(OnWait);
        backButton?.onClick.AddListener(OnBack);
        var waitLabel = waitButton != null ? waitButton.GetComponentInChildren<TMP_Text>() : null;
        if (waitLabel != null) waitLabel.text = "End Turn";
        var backLabel = backButton != null ? backButton.GetComponentInChildren<TMP_Text>() : null;
        if (backLabel != null) backLabel.text = "Undo Move";
        panel?.SetActive(false);
        detailPanel?.SetActive(false);

        if (BattleManager.Instance != null)
            BattleManager.Instance.OnStateChanged += OnStateChanged;
    }

    Button CloneAsMoveButton(Button template)
    {
        var go = Instantiate(template.gameObject, template.transform.parent);
        go.name = "MoveBtn";
        var label = go.GetComponentInChildren<TMP_Text>();
        if (label != null) label.text = "Move";

        // Take the template's slot; push Wait/Back down one row and grow the panel.
        var rt  = (RectTransform)go.transform;
        var wrt = (RectTransform)template.transform;
        rt.anchoredPosition = wrt.anchoredPosition;
        float row = rt.sizeDelta.y + 8f;
        wrt.anchoredPosition -= new Vector2(0f, row);
        if (backButton != null)
            ((RectTransform)backButton.transform).anchoredPosition -= new Vector2(0f, row);
        if (panel != null)
        {
            var prt = (RectTransform)panel.transform;
            prt.sizeDelta += new Vector2(0f, row);
        }

        var btn = go.GetComponent<Button>();
        btn.onClick.RemoveAllListeners();
        return btn;
    }

    void OnDestroy()
    {
        if (BattleManager.Instance != null)
            BattleManager.Instance.OnStateChanged -= OnStateChanged;
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

        // Menu-first flow: grey what this turn already spent.
        var bm = BattleManager.Instance;
        bool hasMoved = bm != null && bm.HasMoved;
        bool hasActed = bm != null && bm.HasActed;

        if (moveButton != null)
        {
            moveButton.interactable = !hasMoved;
            var moveLabel = moveButton.GetComponentInChildren<TMP_Text>();
            if (moveLabel != null) moveLabel.color = hasMoved ? new Color(0.6f, 0.6f, 0.6f) : Color.white;
            var moveBg = moveButton.GetComponent<Image>();
            if (moveBg != null) moveBg.color = hasMoved ? SkillButton.DisabledColor : SkillButton.NormalColor;
        }

        if (hasActed)
        {
            foreach (var b in skillButtons)         b?.SetUsable(false);
            foreach (var b in absorbedSkillButtons) b?.SetUsable(false);
        }

        // Land the highlight on something usable: skills normally, Move once
        // acted, End Turn when everything is spent.
        Select(!hasActed ? 0 : (!hasMoved ? MoveIndex : WaitIndex));
    }

    // Maps a logical slot index to its backing button, or null for Wait/OOB.
    SkillButton ButtonAt(int index)
    {
        if (index < 0) return null;
        if (index < skillButtons.Length) return skillButtons[index];
        int absorbedIdx = index - skillButtons.Length;
        if (absorbedIdx < absorbedSkillButtons.Length) return absorbedSkillButtons[absorbedIdx];
        return null; // Wait or out of range
    }

    public void Close()
    {
        panel?.SetActive(false);
        detailPanel?.SetActive(false);
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
            OnBack();
    }

    void Navigate(int dir)
    {
        var bm = BattleManager.Instance;
        int count = SlotCount;
        int next = (_selectedIndex + dir + count) % count;
        // Skip empty skill slots and spent options
        for (int i = 0; i < count; i++)
        {
            int candidate = ((next + i * dir) % count + count) % count;
            if (candidate == WaitIndex) { Select(candidate); return; } // End Turn always valid
            if (candidate == MoveIndex)
            {
                if (bm == null || !bm.HasMoved) { Select(candidate); return; }
                continue;
            }
            if (bm != null && bm.HasActed) continue;
            var btn = ButtonAt(candidate);
            if (btn != null && btn.HasSkill) { Select(candidate); return; }
        }
    }

    void Confirm()
    {
        if (_selectedIndex == WaitIndex) { OnWait(); return; }
        if (_selectedIndex == MoveIndex) { OnMove(); return; }
        ButtonAt(_selectedIndex)?.OnClick();
    }

    // ── Selection ─────────────────────────────────────────────────────────────

    // Kept for SkillButton's non-absorbed OnClick path — identical to Select,
    // named separately so absorbed buttons have their own call site to hook.
    public void SelectAbsorbed(int absorbedSlotIndex) => Select(skillButtons.Length + absorbedSlotIndex);

    public void Select(int index)
    {
        _selectedIndex = index;

        for (int i = 0; i < skillButtons.Length; i++)
            skillButtons[i]?.SetHighlight(i == index);
        for (int i = 0; i < absorbedSkillButtons.Length; i++)
            absorbedSkillButtons[i]?.SetHighlight((skillButtons.Length + i) == index);

        SetWaitHighlight(index == WaitIndex);
        SetMoveHighlight(index == MoveIndex);

        // Update detail panel
        var btn = ButtonAt(index);
        if (btn != null && btn.HasSkill)
            ShowDetail(btn.Skill);
        else
            detailPanel?.SetActive(false);
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

        if (absorbedInstance != null)
            BattleManager.Instance?.PlayerSelectAbsorbedSkill(absorbedInstance);
        else
            BattleManager.Instance?.PlayerSelectSkill(skill);
        Close();
    }

    void OnMove()
    {
        var bm = BattleManager.Instance;
        if (bm == null || bm.HasMoved) return;
        bm.PlayerChooseMove();   // state change closes the menu
    }

    void OnWait()
    {
        BattleManager.Instance?.PlayerWait();
        Close();
    }

    void OnBack()
    {
        // Return to move selection
        BattleManager.Instance?.PlayerUndoMove();
        Close();
    }

    // ── Detail panel ──────────────────────────────────────────────────────────

    void ShowDetail(SkillDefinition skill)
    {
        if (detailPanel == null) return;
        detailPanel.SetActive(true);

        if (detailSkillName)   detailSkillName.text   = skill.skillName;
        if (detailDescription) detailDescription.text = skill.description;
        if (detailPower)       detailPower.text        = $"Power: {skill.basePower}";
        if (detailRange)       detailRange.text        = $"Range: {skill.range}{(skill.areaOfEffect > 0 ? $"  AoE: {skill.areaOfEffect}" : "")}";
        if (detailSPCost)      detailSPCost.text       = $"SP: {skill.spCost}";
        if (detailDamageType)  detailDamageType.text   = skill.damageType.ToString();
        if (detailIcon)        detailIcon.sprite       = skill.icon;
        if (detailIcon)        detailIcon.gameObject.SetActive(skill.icon != null);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void SetWaitHighlight(bool on)
    {
        if (waitButton == null) return;
        var img = waitButton.GetComponent<Image>();
        if (img) img.color = on ? SkillButton.HighlightColor : SkillButton.NormalColor;
    }

    void SetMoveHighlight(bool on)
    {
        if (moveButton == null) return;
        var img = moveButton.GetComponent<Image>();
        if (img == null) return;
        bool spent = !moveButton.interactable;
        img.color = on ? SkillButton.HighlightColor
                       : (spent ? SkillButton.DisabledColor : SkillButton.NormalColor);
    }

    void FlashNoSP(int slotIndex, bool isAbsorbed = false)
    {
        if (isAbsorbed) absorbedSkillButtons[slotIndex]?.FlashInsufficient();
        else            skillButtons[slotIndex]?.FlashInsufficient();
    }
}
