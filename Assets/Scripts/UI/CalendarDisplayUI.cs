using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// On-screen date readout for <see cref="GameCalendar"/>, with an Italian/English
/// language toggle. Event-driven (updates on day change, not every frame).
///
/// Two ways to use it:
///  • Assign <see cref="dateLabel"/> (and optionally a toggle <see cref="languageButton"/>)
///    to your own HUD canvas, OR
///  • Leave them null and enable <see cref="autoBuildUI"/> — it spawns a small
///    corner overlay canvas at runtime (handy as a playtest/debug display).
///
/// Italian: "3 Ottobre 1299 · Autunno".  English: "3 October 1299 · Autumn".
/// </summary>
public class CalendarDisplayUI : MonoBehaviour
{
    public enum Language { Italian, English }

    [Header("Language")]
    [Tooltip("Which language the date shows in. Toggle at runtime with the button or ToggleLanguage().")]
    public Language language = Language.Italian;
    [Tooltip("Append the season name after the date (e.g. '· Autunno').")]
    public bool showSeason = true;

    [Header("UI refs (optional — leave null to auto-build)")]
    [Tooltip("The label that shows the date. If null and autoBuildUI is on, one is created.")]
    public TMP_Text dateLabel;
    [Tooltip("Optional button that flips the language. Its child label is set to the OTHER language's name.")]
    public Button languageButton;

    [Header("Auto-build overlay")]
    [Tooltip("If no dateLabel is assigned, spawn a simple corner canvas at runtime.")]
    public bool autoBuildUI = true;
    [Tooltip("Optional hotkey to toggle language when using the auto-built overlay.")]
    public KeyCode toggleHotkey = KeyCode.L;

    private GameCalendar _cal;
    private TMP_Text _buttonLabel;

    void OnEnable()
    {
        TryBind();
    }

    void OnDisable()
    {
        if (_cal != null) _cal.OnDayChanged -= OnDayChanged;
    }

    void Update()
    {
        // Late-bind: the calendar singleton may spawn a frame after this UI.
        if (_cal == null) { TryBind(); return; }

        if (toggleHotkey != KeyCode.None && Input.GetKeyDown(toggleHotkey))
            ToggleLanguage();
    }

    void TryBind()
    {
        if (_cal == null)
        {
            _cal = GameCalendar.Instance;
            if (_cal == null) return;               // not spawned yet — retry next Update
            _cal.OnDayChanged += OnDayChanged;
        }

        if (dateLabel == null && autoBuildUI)
            BuildOverlay();

        if (languageButton != null)
        {
            languageButton.onClick.RemoveListener(ToggleLanguage);
            languageButton.onClick.AddListener(ToggleLanguage);
            _buttonLabel = languageButton.GetComponentInChildren<TMP_Text>();
        }

        Refresh();
    }

    void OnDayChanged(GameCalendar cal) => Refresh();

    /// <summary>Flip Italian ⇄ English and refresh immediately.</summary>
    public void ToggleLanguage()
    {
        language = language == Language.Italian ? Language.English : Language.Italian;
        Refresh();
    }

    public void SetLanguage(Language lang)
    {
        language = lang;
        Refresh();
    }

    void Refresh()
    {
        if (_cal == null || dateLabel == null) return;

        dateLabel.text = FormatDate(_cal, language, showSeason);

        // The button offers to switch to the *other* language.
        if (_buttonLabel != null)
            _buttonLabel.text = language == Language.Italian ? "English" : "Italiano";
    }

    /// <summary>"3 Ottobre 1299 · Autunno" (IT) or "3 October 1299 · Autumn" (EN).</summary>
    public static string FormatDate(GameCalendar cal, Language lang, bool withSeason)
    {
        string month = lang == Language.Italian
            ? cal.CurrentMonth.ToString()                 // enum names are already Italian
            : GameCalendar.EnglishName(cal.CurrentMonth);

        string s = $"{cal.DayOfMonth} {month} {cal.Year}";
        if (withSeason)
            s += " · " + SeasonName(cal.CurrentSeason, lang);
        return s;
    }

    static string SeasonName(Season season, Language lang)
    {
        if (lang == Language.English) return season.ToString();
        return season switch      // Italian
        {
            Season.Spring => "Primavera",
            Season.Summer => "Estate",
            Season.Autumn => "Autunno",
            _             => "Inverno",
        };
    }

    // ── Runtime overlay (only used when no dateLabel is assigned) ────────────────
    void BuildOverlay()
    {
        var canvasGO = new GameObject("CalendarOverlayCanvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;                     // sit above gameplay UI
        canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGO.AddComponent<GraphicRaycaster>();

        // Date label — top-left corner.
        var labelGO = new GameObject("DateLabel");
        labelGO.transform.SetParent(canvasGO.transform, false);
        var label = labelGO.AddComponent<TextMeshProUGUI>();
        label.fontSize = 26;
        label.color = new Color(0.96f, 0.9f, 0.72f);   // warm parchment gold
        label.alignment = TextAlignmentOptions.TopLeft;
        label.enableWordWrapping = false;
        var lrt = label.rectTransform;
        lrt.anchorMin = lrt.anchorMax = new Vector2(0f, 1f);
        lrt.pivot = new Vector2(0f, 1f);
        lrt.anchoredPosition = new Vector2(18f, -14f);
        lrt.sizeDelta = new Vector2(420f, 40f);
        dateLabel = label;

        // Language toggle button, just under the label.
        var btnGO = new GameObject("LanguageToggle");
        btnGO.transform.SetParent(canvasGO.transform, false);
        var img = btnGO.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.45f);
        var btn = btnGO.AddComponent<Button>();
        var brt = img.rectTransform;
        brt.anchorMin = brt.anchorMax = new Vector2(0f, 1f);
        brt.pivot = new Vector2(0f, 1f);
        brt.anchoredPosition = new Vector2(18f, -52f);
        brt.sizeDelta = new Vector2(110f, 30f);

        var btnTxtGO = new GameObject("Label");
        btnTxtGO.transform.SetParent(btnGO.transform, false);
        var btnTxt = btnTxtGO.AddComponent<TextMeshProUGUI>();
        btnTxt.fontSize = 18;
        btnTxt.alignment = TextAlignmentOptions.Center;
        btnTxt.color = new Color(0.96f, 0.9f, 0.72f);
        var trt = btnTxt.rectTransform;
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;

        languageButton = btn;
    }
}
