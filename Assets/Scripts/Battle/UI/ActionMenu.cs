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

    [Header("Absorbed Skill Buttons (3 slots, Dante only)")]
    public SkillButton[] absorbedSkillButtons = new SkillButton[3];

    [Header("Wait / Back Buttons")]
    public Button waitButton;
    public Button backButton;

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
    int SlotCount => skillButtons.Length + absorbedSkillButtons.Length + 1;
    int WaitIndex => SlotCount - 1;

    void Start()
    {
        waitButton?.onClick.AddListener(OnWait);
        backButton?.onClick.AddListener(OnBack);
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

        Select(0);
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
        int count = SlotCount;
        int next = (_selectedIndex + dir + count) % count;
        // Skip empty skill slots
        for (int i = 0; i < count; i++)
        {
            int candidate = (next + i) % count;
            if (candidate == WaitIndex) { Select(candidate); return; } // wait is always valid
            var btn = ButtonAt(candidate);
            if (btn != null && btn.HasSkill) { Select(candidate); return; }
        }
    }

    void Confirm()
    {
        if (_selectedIndex == WaitIndex) { OnWait(); return; }
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

    void FlashNoSP(int slotIndex, bool isAbsorbed = false)
    {
        if (isAbsorbed) absorbedSkillButtons[slotIndex]?.FlashInsufficient();
        else            skillButtons[slotIndex]?.FlashInsufficient();
    }
}
