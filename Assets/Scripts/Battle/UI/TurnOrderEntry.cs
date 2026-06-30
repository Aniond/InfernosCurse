using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Single row in the turn order sidebar.
public class TurnOrderEntry : MonoBehaviour
{
    public Image    portrait;
    public Image    background;
    public TMP_Text nameLabel;
    public Image    activePip;  // small indicator on the active unit's row

    public void Set(string unitName, Sprite icon, Color tint, bool isActive, bool showLabel)
    {
        if (background) background.color = tint;
        if (portrait)
        {
            portrait.sprite = icon;
            portrait.gameObject.SetActive(icon != null);
        }
        if (nameLabel)
        {
            nameLabel.text = unitName;
            nameLabel.gameObject.SetActive(showLabel);
        }
        if (activePip) activePip.gameObject.SetActive(isActive);
    }
}
