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
                case "Resume":    btn.onClick.AddListener(Resume);       break;
                case "Party":     btn.onClick.AddListener(OpenParty);    break;
                case "Save Game": btn.onClick.AddListener(OpenSave);     break;
                case "Load Game": btn.onClick.AddListener(OpenLoad);     break;
                case "Settings":  btn.onClick.AddListener(OpenSettings); break;
                case "Quit":      btn.onClick.AddListener(QuitGame);     break;
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
            for (int i = 0; i < 3; i++)
            {
                var slot = savePanel.transform.Find("SaveSlot_" + (i + 1));
                if (slot == null) continue;
                saveSlotButtons[i] = slot.GetComponent<Button>();
                saveSlotLabels[i]  = slot.Find("Lbl")?.GetComponent<TMP_Text>();
                int s = i + 1;
                if (saveSlotButtons[i]) { saveSlotButtons[i].onClick.RemoveAllListeners(); saveSlotButtons[i].onClick.AddListener(() => SaveSlot(s)); }
            }

        if (loadPanel != null)
            for (int i = 0; i < 3; i++)
            {
                var slot = loadPanel.transform.Find("LoadSlot_" + (i + 1));
                if (slot == null) continue;
                loadSlotButtons[i] = slot.GetComponent<Button>();
                loadSlotLabels[i]  = slot.Find("Lbl")?.GetComponent<TMP_Text>();
                int s = i + 1;
                if (loadSlotButtons[i]) { loadSlotButtons[i].onClick.RemoveAllListeners(); loadSlotButtons[i].onClick.AddListener(() => LoadSlot(s)); }
            }
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
        pausePanel.SetActive(true);
    }

    public void Resume()
    {
        CloseAll();
        _isPaused = false;
        Time.timeScale = 1f;
    }

    public void OpenSettings()
    {
        pausePanel.SetActive(false);
        settingsPanel.SetActive(true);

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
        pausePanel.SetActive(false);
        RefreshSlotLabels(saveSlotLabels);
        savePanel.SetActive(true);
    }

    public void OpenLoad()
    {
        pausePanel.SetActive(false);
        RefreshSlotLabels(loadSlotLabels);
        // Disable load buttons for empty slots
        for (int i = 0; i < 3; i++)
            if (loadSlotButtons[i] != null)
                loadSlotButtons[i].interactable = SaveSystem.SlotExists(i + 1);
        loadPanel.SetActive(true);
    }

    void RefreshSlotLabels(TMP_Text[] labels)
    {
        for (int i = 0; i < 3; i++)
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

    public void OpenParty()
    {
        pausePanel.SetActive(false);
        partyPanel.SetActive(true);
    }

    public void BackToPause()
    {
        settingsPanel.SetActive(false);
        savePanel.SetActive(false);
        loadPanel.SetActive(false);
        if (partyPanel) partyPanel.SetActive(false);
        pausePanel.SetActive(true);
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
        if (pausePanel)    pausePanel.SetActive(false);
        if (settingsPanel) settingsPanel.SetActive(false);
        if (savePanel)     savePanel.SetActive(false);
        if (loadPanel)     loadPanel.SetActive(false);
        if (partyPanel)    partyPanel.SetActive(false);
    }
}
