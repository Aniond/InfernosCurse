using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

// Persona-5-style preview shown when a map node is selected. Displays the
// location's name, blurb, curse status, and an Enter button.
public class MapNodeDetailPanel : MonoBehaviour
{
    [Header("Root")]
    public GameObject panelRoot;

    [Header("Header")]
    public TMP_Text nameLabel;
    public TMP_Text blurbLabel;

    [Header("Location Preview")]
    public Image    previewImage;    // location splash (transparent watercolor)
    public Sprite   fallbackPreview; // shown when a node has no preview yet

    [Header("Status")]
    public TMP_Text statusTag;       // flavor only — never a measurable number

    [Header("Status Colors")]
    public Color cleanColor    = new Color(0.45f, 0.80f, 0.45f);
    public Color cursedColor   = new Color(0.70f, 0.20f, 0.80f);
    public Color sanctuaryColor = new Color(0.95f, 0.90f, 0.55f);

    [Header("Buttons")]
    public Button enterButton;
    public Button closeButton;
    public TMP_Text enterButtonLabel;

    private HubNode _node;
    private Action<HubNode> _onEnter;

    void Awake()
    {
        if (closeButton) closeButton.onClick.AddListener(Hide);
        if (enterButton) enterButton.onClick.AddListener(() =>
        {
            if (_node != null) _onEnter?.Invoke(_node);
        });
        Hide();
    }

    public void Show(HubNode node, Action<HubNode> onEnter)
    {
        _node    = node;
        _onEnter = onEnter;

        if (nameLabel)  nameLabel.text  = node.displayName;
        if (blurbLabel) blurbLabel.text = string.IsNullOrEmpty(node.blurb)
            ? "" : node.blurb;

        // Location preview splash
        if (previewImage)
        {
            var sprite = node.previewImage != null ? node.previewImage : fallbackPreview;
            previewImage.sprite  = sprite;
            previewImage.enabled = sprite != null;
            previewImage.preserveAspect = true;
        }

        // Flavor-only status tag — describes the place, never the hidden curse
        // number. The player infers the curse from atmosphere, not a readout.
        string tag; Color col;
        if (node.isSanctuarySite)   { tag = "Hallowed Ground"; col = sanctuaryColor; }
        else if (node.isRitualSite) { tag = "Unquiet";         col = cursedColor; }
        else                        { tag = "";                col = Color.white; }

        if (statusTag)
        {
            statusTag.text = tag;
            statusTag.color = col;
            statusTag.gameObject.SetActive(!string.IsNullOrEmpty(tag));
        }

        // A node only appears on the map if it's accessible, so the Travel
        // button is always available here. Hide it only if no scene is wired yet.
        bool canEnter = !string.IsNullOrEmpty(node.sceneName);
        if (enterButton) enterButton.gameObject.SetActive(canEnter);
        if (enterButtonLabel) enterButtonLabel.text = "Travel";

        if (panelRoot) panelRoot.SetActive(true);
    }

    public void Hide()
    {
        if (panelRoot) panelRoot.SetActive(false);
        _node = null;
    }

    public bool IsOpen => panelRoot != null && panelRoot.activeSelf;
}
