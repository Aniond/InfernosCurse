using UnityEngine;
using UnityEngine.UI;
using TMPro;

[System.Serializable]
public class CharacterStats
{
    public string characterName = "Dante";
    public string characterClass = "Warrior";
    public int level = 1;
    public int hp = 100, hpMax = 100;
    public int sp = 60,  spMax = 60;
    public int xp = 0,   xpNext = 100;

    // Core stats (1-20)
    public int strength     = 12;
    public int dexterity    = 10;
    public int constitution = 10;
    public int creativity   = 8;
    public int faith        = 8;
    public int perception   = 10;
    public int speed        = 10;
}

public class CharacterSheet : MonoBehaviour
{
    [Header("Panel Root")]
    public GameObject sheetPanel;

    [Header("Header")]
    public TMP_Text nameLabel;
    public TMP_Text classLabel;
    public TMP_Text levelLabel;
    public TMP_Text hpLabel;
    public TMP_Text mpLabel;
    public TMP_Text xpLabel;

    [Header("Tabs")]
    public Button tabStats;
    public Button tabSkills;
    public Button tabEquipment;
    public GameObject statsContent;
    public GameObject skillsContent;
    public GameObject equipmentContent;

    [Header("Stat Labels (Stats tab)")]
    public TMP_Text strValue;
    public TMP_Text dexValue;
    public TMP_Text conValue;
    public TMP_Text creValue;
    public TMP_Text faithValue;
    public TMP_Text percValue;
    public TMP_Text spdValue;

    [Header("Stat Bars")]
    public Image strBar, dexBar, conBar, creBar, faithBar, percBar, spdBar;

    private CharacterStats _stats;
    private CombatantData  _combatant;   // the real party member, when known (Skills tab source)
    private bool _isOpen;
    private MenuManager _menuManager;

    // ── Skills tab (absorbed skills — Dante/Benidito only) ───────────────────
    private Transform _absorbedListParent;

    static readonly Color TabActive   = new Color(0.35f, 0.22f, 0.10f, 1f);
    static readonly Color TabInactive = new Color(0.15f, 0.10f, 0.05f, 1f);

    // Lazy — MenuManager may not exist in every scene
    MenuManager MenuMgr => _menuManager != null ? _menuManager : (_menuManager = FindAnyObjectByType<MenuManager>());

    void Start()
    {
        // Wire tab buttons
        tabStats?.onClick.AddListener(() => ShowTab(0));
        tabSkills?.onClick.AddListener(() => ShowTab(1));
        tabEquipment?.onClick.AddListener(() => ShowTab(2));

        // Default stats for Dante
        _stats = new CharacterStats();
        sheetPanel?.SetActive(false);
    }

    void Update()
    {
        if (UnityEngine.InputSystem.Keyboard.current != null &&
            UnityEngine.InputSystem.Keyboard.current.tabKey.wasPressedThisFrame)
            Toggle();
    }

    public void Open(CharacterStats stats = null)
    {
        _combatant = null;
        OpenInternal(stats);
    }

    // Open for a specific party member — pulls the real CombatantData off the
    // roster so the Skills tab can show that member's absorbed skills. Stats
    // tab still reads the placeholder CharacterStats (job-driven stat display
    // is separate, still-open work).
    public void Open(int memberIndex)
    {
        var party = RestSystem.PartyMembers;
        _combatant = (memberIndex >= 0 && memberIndex < party.Count) ? party[memberIndex] : null;
        OpenInternal(stats: null);
    }

    void OpenInternal(CharacterStats stats)
    {
        if (stats != null) _stats = stats;
        _isOpen = true;
        if (sheetPanel) sheetPanel.SetActive(true);
        Refresh();
        ShowTab(0);
    }

    public void Close()
    {
        _isOpen = false;
        if (sheetPanel) sheetPanel.SetActive(false);

        // Return to the pause menu if one is present (sheet was opened from it).
        MenuMgr?.BackToPause();
    }

    public void Toggle()
    {
        if (_isOpen) Close();
        else         Open();
    }

    public void Refresh()
    {
        if (_stats == null) return;
        if (nameLabel)  nameLabel.text  = _stats.characterName;
        if (classLabel) classLabel.text = _stats.characterClass;
        if (levelLabel) levelLabel.text = "Level " + _stats.level;
        if (hpLabel)    hpLabel.text    = $"HP  {_stats.hp} / {_stats.hpMax}";
        if (mpLabel)    mpLabel.text    = $"SP  {_stats.sp} / {_stats.spMax}";
        if (xpLabel)    xpLabel.text    = $"XP  {_stats.xp} / {_stats.xpNext}";

        SetStat(strValue,   strBar,   _stats.strength);
        SetStat(dexValue,   dexBar,   _stats.dexterity);
        SetStat(conValue,   conBar,   _stats.constitution);
        SetStat(creValue,   creBar,   _stats.creativity);
        SetStat(faithValue, faithBar, _stats.faith);
        SetStat(percValue,  percBar,  _stats.perception);
        SetStat(spdValue,   spdBar,   _stats.speed);
    }

    void SetStat(TMP_Text label, Image bar, int value)
    {
        if (label) label.text = value.ToString();
        if (bar)   bar.fillAmount = value / 20f;
    }

    void ShowTab(int index)
    {
        if (statsContent)     statsContent.SetActive(index == 0);
        if (skillsContent)    skillsContent.SetActive(index == 1);
        if (equipmentContent) equipmentContent.SetActive(index == 2);

        SetTabColor(tabStats,     index == 0);
        SetTabColor(tabSkills,    index == 1);
        SetTabColor(tabEquipment, index == 2);

        if (index == 1) RebuildAbsorbedSkills();
    }

    void SetTabColor(Button btn, bool active)
    {
        if (btn == null) return;
        btn.GetComponent<Image>().color = active ? TabActive : TabInactive;
    }

    // ── Skills tab — absorbed skills (Dante/Benidito only) ───────────────────
    //
    // Lists every AbsorbedSkillInstance the member holds (name, level, dupes,
    // corrupted/holy) with an Equip button per open absorbed slot. Self-built
    // into skillsContent the same way GuildPanelUI builds its lists, so no
    // manual prefab wiring is required.

    void RebuildAbsorbedSkills()
    {
        if (skillsContent == null) return;

        if (_absorbedListParent == null)
        {
            var go = new GameObject("AbsorbedSkillsList", typeof(RectTransform),
                typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            go.transform.SetParent(skillsContent.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(12f, rt.offsetMin.y);
            rt.offsetMax = new Vector2(-12f, rt.offsetMax.y);

            var vlg = go.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 6;
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandHeight = false;
            go.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _absorbedListParent = go.transform;
        }

        foreach (Transform child in _absorbedListParent)
            Destroy(child.gameObject);

        if (_combatant == null || _combatant.role != CombatantRole.Dante)
        {
            AddSkillsText("Only Dante's absorbed skills can be viewed here.", FontStyles.Italic,
                new Color(0.6f, 0.58f, 0.62f));
            return;
        }

        if (_combatant.absorbedSkills == null || _combatant.absorbedSkills.Count == 0)
        {
            AddSkillsText("No skills absorbed yet — defeat a marked enemy in battle.",
                FontStyles.Italic, new Color(0.6f, 0.58f, 0.62f));
            return;
        }

        var slots = _combatant.equippedSkills.absorbed;
        foreach (var inst in _combatant.absorbedSkills)
        {
            if (inst == null) continue;

            int equippedSlot = System.Array.IndexOf(slots, inst);
            string state = inst.isRefined ? "Holy" : "Corrupted";
            AddSkillsText($"{inst.DisplayName()}  ({state}, x{inst.duplicateCount})",
                FontStyles.Bold, new Color(0.9f, 0.86f, 0.78f));

            if (equippedSlot >= 0)
            {
                AddSkillsRow($"Equipped — Slot {equippedSlot + 1} (unequip)", () =>
                {
                    _combatant.equippedSkills.UnequipAbsorbed(inst);
                    RebuildAbsorbedSkills();
                });
            }
            else
            {
                int freeSlot = System.Array.IndexOf(slots, null);
                if (freeSlot >= 0)
                {
                    AddSkillsRow($"Equip to Slot {freeSlot + 1}", () =>
                    {
                        _combatant.equippedSkills.EquipAbsorbed(inst, freeSlot);
                        RebuildAbsorbedSkills();
                    });
                }
                else
                {
                    AddSkillsText("All absorbed slots full — unequip one first.",
                        FontStyles.Italic, new Color(0.6f, 0.4f, 0.4f));
                }
            }
        }
    }

    void AddSkillsText(string text, FontStyles style, Color color)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(_absorbedListParent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = 22;
        t.fontStyle = style;
        t.color = color;
        t.alignment = TextAlignmentOptions.MidlineLeft;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.raycastTarget = false;
    }

    void AddSkillsRow(string label, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject("Row_" + label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(_absorbedListParent, false);
        go.GetComponent<LayoutElement>().minHeight = 40;

        var img = go.GetComponent<Image>();
        img.color = new Color(0.16f, 0.13f, 0.20f, 0.9f);

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.30f, 0.24f, 0.36f, 1f);
        colors.pressedColor     = new Color(0.45f, 0.36f, 0.20f, 1f);
        colors.fadeDuration = 0.05f;
        btn.colors = colors;
        btn.onClick.AddListener(onClick);

        var textGo = new GameObject("Text", typeof(RectTransform));
        var t = textGo.AddComponent<TextMeshProUGUI>();
        t.text = label;
        t.fontSize = 20;
        t.color = Color.white;
        t.alignment = TextAlignmentOptions.MidlineLeft;
        t.raycastTarget = false;
        t.margin = new Vector4(16, 0, 8, 0);
        var tRt = (RectTransform)t.transform;
        tRt.SetParent(go.transform, false);
        tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one;
        tRt.offsetMin = Vector2.zero; tRt.offsetMax = Vector2.zero;
    }
}
