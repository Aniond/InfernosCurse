using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using TMPro;

public class MenuManager : MonoBehaviour
{
    [Header("Panels")]
    public GameObject pausePanel;
    public GameObject settingsPanel;
    public GameObject savePanel;
    public GameObject loadPanel;
    public GameObject partyPanel;
    public GameObject characterSheetPanel;

    [Header("Settings Controls")]
    public Slider masterVolumeSlider;
    public Slider musicVolumeSlider;
    public Slider sfxVolumeSlider;
    public Toggle fullscreenToggle;

    [Header("Gemini API")]
    public TMP_InputField geminiKeyField;

    [Header("Save Slots")]
    public Button[] saveSlotButtons = new Button[3];
    public TMP_Text[] saveSlotLabels = new TMP_Text[3];

    [Header("Load Slots")]
    public Button[] loadSlotButtons = new Button[3];
    public TMP_Text[] loadSlotLabels = new TMP_Text[3];

    private bool _isPaused;

    void Start()
    {
        WireButtons();
        CloseAll();
    }

    void WireButtons()
    {
        WirePausePanel();
        WireBackButton(settingsPanel);
        WireBackButton(savePanel);
        WireBackButton(loadPanel);
        WireBackButton(partyPanel);
        WireSettingsSaveButton();
        WireSaveLoadSlots();
        WirePartyMemberCards();
    }

    void WirePausePanel()
    {
        if (pausePanel == null) return;
        var pp = pausePanel.transform.Find("PausePanel") ?? pausePanel.transform;
        foreach (Transform ch in pp)
        {
            if (!ch.name.StartsWith("Btn_")) continue;
            var btn = ch.GetComponent<Button>();
            if (btn == null) continue;
            var lbl = ch.Find("Lbl")?.GetComponent<TMP_Text>()?.text ?? "";
            btn.onClick.RemoveAllListeners();
            switch (lbl)
            {
                case "Resume":    btn.onClick.AddListener(Resume);           break;
                case "Party":     btn.onClick.AddListener(OpenParty);        break;
                case "Character": btn.onClick.AddListener(() => OpenCharacterSheet(0)); break;
                case "Travel":    btn.onClick.AddListener(OpenTravel);       break;
                case "Save Game": btn.onClick.AddListener(OpenSave);         break;
                case "Load Game": btn.onClick.AddListener(OpenLoad);         break;
                case "Settings":  btn.onClick.AddListener(OpenSettings);     break;
                case "Guilds":    btn.onClick.AddListener(OpenGuilds);       break;
                case "Rest":      btn.onClick.AddListener(OpenRest);         break;
                case "Quit":      btn.onClick.AddListener(QuitGame);         break;
            }
        }
    }

    void WireBackButton(GameObject panel)
    {
        if (panel == null) return;
        foreach (Transform ch in panel.transform)
        {
            if (!ch.name.StartsWith("Btn_")) continue;
            var btn = ch.GetComponent<Button>();
            if (btn == null) continue;
            var lbl = ch.Find("Lbl")?.GetComponent<TMP_Text>()?.text ?? "";
            if (lbl == "Back") { btn.onClick.RemoveAllListeners(); btn.onClick.AddListener(BackToPause); }
        }
    }

    void WireSettingsSaveButton()
    {
        if (settingsPanel == null) return;
        foreach (Transform ch in settingsPanel.transform)
        {
            if (!ch.name.StartsWith("Btn_")) continue;
            var btn = ch.GetComponent<Button>();
            if (btn == null) continue;
            var lbl = ch.Find("Lbl")?.GetComponent<TMP_Text>()?.text ?? "";
            if (lbl == "Save") { btn.onClick.RemoveAllListeners(); btn.onClick.AddListener(SaveSettings); }
        }
    }

    void WireSaveLoadSlots()
    {
        if (savePanel != null)
            for (int i = 0; i < SaveSystem.SLOT_COUNT; i++)
            {
                var slot = savePanel.transform.Find("SaveSlot_" + (i + 1));
                if (slot == null) continue;
                saveSlotButtons[i] = slot.GetComponent<Button>();
                saveSlotLabels[i]  = slot.Find("Lbl")?.GetComponent<TMP_Text>();
                int s = i + 1;
                if (saveSlotButtons[i]) { saveSlotButtons[i].onClick.RemoveAllListeners(); saveSlotButtons[i].onClick.AddListener(() => SaveSlot(s)); }
            }

        if (loadPanel != null)
            for (int i = 0; i < SaveSystem.SLOT_COUNT; i++)
            {
                var slot = loadPanel.transform.Find("LoadSlot_" + (i + 1));
                if (slot == null) continue;
                loadSlotButtons[i] = slot.GetComponent<Button>();
                loadSlotLabels[i]  = slot.Find("Lbl")?.GetComponent<TMP_Text>();
                int s = i + 1;
                if (loadSlotButtons[i]) { loadSlotButtons[i].onClick.RemoveAllListeners(); loadSlotButtons[i].onClick.AddListener(() => LoadSlot(s)); }
            }
    }

    void WirePartyMemberCards()
    {
        if (partyPanel == null) return;
        var grid = partyPanel.transform.Find("CardGrid") ?? partyPanel.transform;
        int memberIndex = 0;
        foreach (Transform ch in grid)
        {
            if (!ch.name.StartsWith("MemberCard")) continue;
            var dc = ch.GetComponent<DoubleClickHandler>() ?? ch.gameObject.AddComponent<DoubleClickHandler>();
            int idx = memberIndex++;
            dc.onDoubleClick = () => OpenCharacterSheet(idx);
        }
    }

    public void OpenCharacterSheet(int memberIndex = 0)
    {
        var cs = FindAnyObjectByType<CharacterSheet>();
        if (cs == null) return;
        if (pausePanel) pausePanel.SetActive(false);
        if (partyPanel) partyPanel.SetActive(false);
        cs.Open(memberIndex);
    }

    void Update()
    {
        // Support both input systems
        bool escPressed = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                       || Input.GetKeyDown(KeyCode.Escape);
        if (escPressed) TogglePause();
    }

    public void TogglePause()
    {
        if (_isPaused) Resume();
        else           Pause();
    }

    public void Pause()
    {
        _isPaused = true;
        Time.timeScale = 0f;
        if (pausePanel) pausePanel.SetActive(true);
    }

    public void Resume()
    {
        CloseAll();
        _isPaused = false;
        Time.timeScale = 1f;
    }

    public void OpenSettings()
    {
        if (pausePanel)    pausePanel.SetActive(false);
        if (settingsPanel) settingsPanel.SetActive(true);

        if (masterVolumeSlider) masterVolumeSlider.value = PlayerPrefs.GetFloat("MasterVolume", 1f);
        if (musicVolumeSlider)  musicVolumeSlider.value  = PlayerPrefs.GetFloat("MusicVolume",  1f);
        if (sfxVolumeSlider)    sfxVolumeSlider.value    = PlayerPrefs.GetFloat("SFXVolume",    1f);
        if (fullscreenToggle)   fullscreenToggle.isOn    = Screen.fullScreen;
        if (geminiKeyField)     geminiKeyField.text      = PlayerPrefs.GetString("GeminiKey", "");
    }

    public void SaveSettings()
    {
        if (masterVolumeSlider) PlayerPrefs.SetFloat("MasterVolume", masterVolumeSlider.value);
        if (musicVolumeSlider)  PlayerPrefs.SetFloat("MusicVolume",  musicVolumeSlider.value);
        if (sfxVolumeSlider)    PlayerPrefs.SetFloat("SFXVolume",    sfxVolumeSlider.value);
        if (fullscreenToggle)   Screen.fullScreen = fullscreenToggle.isOn;
        if (geminiKeyField)     PlayerPrefs.SetString("GeminiKey", geminiKeyField.text);
        PlayerPrefs.Save();
        BackToPause();
    }

    public void OpenSave()
    {
        if (pausePanel) pausePanel.SetActive(false);
        RefreshSlotLabels(saveSlotLabels);
        if (savePanel) savePanel.SetActive(true);
    }

    public void OpenLoad()
    {
        if (pausePanel) pausePanel.SetActive(false);
        RefreshSlotLabels(loadSlotLabels);
        // Disable load buttons for empty slots
        for (int i = 0; i < loadSlotButtons.Length; i++)
            if (loadSlotButtons[i] != null)
                loadSlotButtons[i].interactable = SaveSystem.SlotExists(i + 1);
        if (loadPanel) loadPanel.SetActive(true);
    }

    void RefreshSlotLabels(TMP_Text[] labels)
    {
        for (int i = 0; i < labels.Length; i++)
            if (labels[i] != null)
                labels[i].text = SaveSystem.SlotLabel(i + 1);
    }

    public void SaveSlot(int slot)
    {
        SaveSystem.Save(slot);
        RefreshSlotLabels(saveSlotLabels);
    }

    public void LoadSlot(int slot)
    {
        var data = SaveSystem.Load(slot);
        if (data == null) return;
        SaveSystem.ApplySave(data);
        Resume();
    }

    // Hand off to the fast-travel menu. It manages its own pause/timescale, so we
    // fully close the pause menu first (Resume) to avoid two systems fighting over
    // Time.timeScale — the travel menu will re-pause itself.
    public void OpenTravel()
    {
        var travel = FindAnyObjectByType<FastTravelMenu>();
        if (travel == null)
        {
            Debug.LogWarning("[MenuManager] No FastTravelMenu in the scene.");
            return;
        }
        Resume();          // closes panels + restores timeScale to 1
        travel.Open();     // re-pauses via its own logic
    }

    public void OpenGuilds()
    {
        var panel = FindAnyObjectByType<GuildPanelUI>();
        if (panel == null)
        {
            Debug.LogWarning("[MenuManager] No GuildPanelUI in the scene.");
            return;
        }
        Resume();
        panel.OpenStandings();   // re-pauses via its own logic
    }

    public void OpenRest()
    {
        var rest = FindAnyObjectByType<RestMenuUI>();
        if (rest == null)
        {
            Debug.LogWarning("[MenuManager] No RestMenuUI in the scene.");
            return;
        }
        Resume();
        rest.OpenCamp();         // re-pauses via its own logic
    }

    public void OpenParty()
    {
        if (pausePanel) pausePanel.SetActive(false);
        if (partyPanel) partyPanel.SetActive(true);
    }

    public void BackToPause()
    {
        if (settingsPanel) settingsPanel.SetActive(false);
        if (savePanel)     savePanel.SetActive(false);
        if (loadPanel)     loadPanel.SetActive(false);
        if (partyPanel)    partyPanel.SetActive(false);
        if (pausePanel)    pausePanel.SetActive(true);
    }

    public void QuitGame()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void CloseAll()
    {
        if (pausePanel)        pausePanel.SetActive(false);
        if (settingsPanel)     settingsPanel.SetActive(false);
        if (savePanel)         savePanel.SetActive(false);
        if (loadPanel)         loadPanel.SetActive(false);
        if (partyPanel)        partyPanel.SetActive(false);
        if (characterSheetPanel) characterSheetPanel.SetActive(false);
    }
}
