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
