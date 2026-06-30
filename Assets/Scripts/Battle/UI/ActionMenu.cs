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
    private int             _selectedIndex = 0;   // 0-3 = skills, 4 = wait
    private const int       SLOT_COUNT     = 5;   // 4 skills + wait

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

        Select(0);
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
        int next = (_selectedIndex + dir + SLOT_COUNT) % SLOT_COUNT;
        // Skip empty skill slots
        for (int i = 0; i < SLOT_COUNT; i++)
        {
            int candidate = (next + i) % SLOT_COUNT;
            if (candidate == SLOT_COUNT - 1) { Select(candidate); return; } // wait is always valid
            if (skillButtons[candidate] != null && skillButtons[candidate].HasSkill) { Select(candidate); return; }
        }
    }

    void Confirm()
    {
        if (_selectedIndex == SLOT_COUNT - 1) { OnWait(); return; }
        skillButtons[_selectedIndex]?.OnClick();
    }

    // ── Selection ─────────────────────────────────────────────────────────────

    public void Select(int index)
    {
        _selectedIndex = index;

        for (int i = 0; i < skillButtons.Length; i++)
            skillButtons[i]?.SetHighlight(i == index);

        SetWaitHighlight(index == SLOT_COUNT - 1);

        // Update detail panel
        if (index < skillButtons.Length && skillButtons[index] != null && skillButtons[index].HasSkill)
            ShowDetail(skillButtons[index].Skill);
        else
            detailPanel?.SetActive(false);
    }

    // Called by SkillButton when the player clicks a skill
    public void OnSkillChosen(SkillDefinition skill, int slotIndex)
    {
        if (_activeUnit == null || skill == null) return;

        if (!_activeUnit.HasSP(skill.spCost))
        {
            FlashNoSP(slotIndex);
            return;
        }

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

    void FlashNoSP(int slotIndex)
    {
        skillButtons[slotIndex]?.FlashInsufficient();
    }
}
