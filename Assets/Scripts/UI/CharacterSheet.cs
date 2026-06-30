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
    private bool _isOpen;
    private MenuManager _menuManager;

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
        if (stats != null) _stats = stats;
        _isOpen = true;
        sheetPanel.SetActive(true);
        Refresh();
        ShowTab(0);
    }

    public void Close()
    {
        _isOpen = false;
        sheetPanel.SetActive(false);
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
    }

    void SetTabColor(Button btn, bool active)
    {
        if (btn == null) return;
        btn.GetComponent<Image>().color = active ? TabActive : TabInactive;
    }
}
