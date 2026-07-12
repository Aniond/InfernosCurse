using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class GugolMapCardHost : MonoBehaviour
{
    GugolMapUI _owner;
    GameObject _panel;
    TextMeshProUGUI _title;
    TextMeshProUGUI _subtitle;
    TextMeshProUGUI _status;
    Button _primary;
    Button _secondary;
    TextMeshProUGUI _primaryLabel;
    TextMeshProUGUI _secondaryLabel;

    public bool IsOpen => _panel != null && _panel.activeSelf;

    public void Init(
        GugolMapUI owner,
        RectTransform parent,
        Sprite cardSprite,
        TMP_FontAsset headerFont,
        TMP_FontAsset bodyFont)
    {
        _owner = owner;
        _panel = new GameObject("MapContextCard", typeof(RectTransform), typeof(Image));
        _panel.transform.SetParent(parent, false);
        var rect = (RectTransform)_panel.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 24f);
        rect.sizeDelta = new Vector2(680f, 220f);
        var image = _panel.GetComponent<Image>();
        if (cardSprite != null)
        {
            image.sprite = cardSprite;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
        }
        else image.color = new Color(0.94f, 0.90f, 0.80f, 0.98f);

        _title = GugolUi.MakeText(_panel.transform, string.Empty, 31f, FontStyles.Bold,
            new Color(0.22f, 0.15f, 0.08f), headerFont);
        var titleRect = _title.rectTransform;
        titleRect.anchorMin = titleRect.anchorMax = new Vector2(0f, 1f);
        titleRect.pivot = new Vector2(0f, 1f);
        titleRect.anchoredPosition = new Vector2(28f, -20f);
        titleRect.sizeDelta = new Vector2(500f, 44f);

        _status = GugolUi.MakeText(_panel.transform, string.Empty, 17f, FontStyles.Italic,
            new Color(0.47f, 0.22f, 0.15f), bodyFont);
        _status.alignment = TextAlignmentOptions.MidlineRight;
        var statusRect = _status.rectTransform;
        statusRect.anchorMin = statusRect.anchorMax = new Vector2(1f, 1f);
        statusRect.pivot = new Vector2(1f, 1f);
        statusRect.anchoredPosition = new Vector2(-56f, -26f);
        statusRect.sizeDelta = new Vector2(220f, 30f);

        var close = MakeButton(_panel.transform, "×", headerFont, out _, new Vector2(1f, 1f),
            new Vector2(-18f, -16f), new Vector2(34f, 34f));
        close.onClick.AddListener(Hide);

        _subtitle = GugolUi.MakeText(_panel.transform, string.Empty, 21f, FontStyles.Normal,
            new Color(0.32f, 0.24f, 0.14f), bodyFont);
        _subtitle.textWrappingMode = TextWrappingModes.Normal;
        var subtitleRect = _subtitle.rectTransform;
        subtitleRect.anchorMin = new Vector2(0f, 0f);
        subtitleRect.anchorMax = new Vector2(1f, 1f);
        subtitleRect.offsetMin = new Vector2(28f, 76f);
        subtitleRect.offsetMax = new Vector2(-28f, -68f);

        _primary = MakeButton(_panel.transform, "Directions", bodyFont, out _primaryLabel,
            new Vector2(0f, 0f), new Vector2(28f, 20f), new Vector2(210f, 48f));
        _secondary = MakeButton(_panel.transform, "Remember", bodyFont, out _secondaryLabel,
            new Vector2(0f, 0f), new Vector2(250f, 20f), new Vector2(190f, 48f));
        _panel.SetActive(false);
    }

    public void ShowStreet(GugolMapFeatureRecord feature)
    {
        Show(feature, "Street", null, null, null, null);
    }

    public void ShowVenue(GugolMapFeatureRecord feature, Action directions, Action remember)
    {
        Show(feature, "Venue", directions, "Directions", remember, "Remember");
    }

    public void ShowNpc(GugolMapFeatureRecord feature, Action directions)
    {
        Show(feature, "Last known", directions, "Directions", null, null);
    }

    public void Hide()
    {
        if (_panel == null || !_panel.activeSelf) return;
        _panel.SetActive(false);
        _primary.onClick.RemoveAllListeners();
        _secondary.onClick.RemoveAllListeners();
        _owner?.OnContextCardHidden();
    }

    void Show(
        GugolMapFeatureRecord feature,
        string status,
        Action primary,
        string primaryText,
        Action secondary,
        string secondaryText)
    {
        if (_panel == null || feature == null) return;
        _title.text = feature.displayName ?? string.Empty;
        _subtitle.text = feature.subtitle ?? string.Empty;
        _status.text = StatusFor(feature, status);
        ConfigureAction(_primary, _primaryLabel, primary, primaryText);
        ConfigureAction(_secondary, _secondaryLabel, secondary, secondaryText);
        _panel.SetActive(true);
        _panel.transform.SetAsLastSibling();
    }

    static string StatusFor(GugolMapFeatureRecord feature, string fallback) => feature.knowledgeState switch
    {
        GugolMapKnowledgeState.Rumored => "Rumored",
        GugolMapKnowledgeState.LastKnown => "Last known",
        GugolMapKnowledgeState.Lost => "Lost",
        GugolMapKnowledgeState.Forgotten => "Not remembered",
        GugolMapKnowledgeState.RememberedLoss => "Remembered",
        _ => fallback,
    };

    static void ConfigureAction(Button button, TextMeshProUGUI label, Action action, string text)
    {
        button.onClick.RemoveAllListeners();
        bool active = action != null && !string.IsNullOrWhiteSpace(text);
        button.gameObject.SetActive(active);
        if (!active) return;
        label.text = text;
        button.onClick.AddListener(() => action());
    }

    static Button MakeButton(
        Transform parent,
        string label,
        TMP_FontAsset font,
        out TextMeshProUGUI text,
        Vector2 anchor,
        Vector2 position,
        Vector2 size)
    {
        var go = new GameObject("Button_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var image = go.GetComponent<Image>();
        image.color = new Color(0.22f, 0.31f, 0.40f, 0.96f);
        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        text = GugolUi.MakeText(go.transform, label, 20f, FontStyles.Bold, Color.white, font);
        text.alignment = TextAlignmentOptions.Center;
        GugolUi.Stretch(text.rectTransform);
        return button;
    }
}
