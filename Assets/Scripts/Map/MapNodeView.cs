using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

// One clickable pin on the Florence world map. Spawned by WorldMapUI from a
// HubNode. Hidden Circle state never changes its presentation.
[RequireComponent(typeof(RectTransform))]
public class MapNodeView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Pin Visuals")]
    public Image      pinIcon;
    public TMP_Text   label;
    public GameObject selectedOutline;

    [Header("Authored Site Tint")]
    public Color sanctuaryColor = new Color(0.95f, 0.90f, 0.55f);

    public HubNode Node { get; private set; }

    private WorldMapUI _map;

    public void Bind(HubNode node, WorldMapUI map)
    {
        Node = node;
        _map = map;

        if (label) label.text = node.displayName;
        if (selectedOutline) selectedOutline.SetActive(false);

        Refresh();
    }

    // Re-read ordinary authored state. Hidden Circle values never alter a map
    // pin's color, label, or iconography.
    public void Refresh()
    {
        if (Node == null) return;

        // The medallion art stays gold. Sanctuary is explicit site authoring,
        // not a reading of the hidden Circle ledger.
        Color tint;
        if (Node.isSanctuarySite)
            tint = Color.Lerp(Color.white, sanctuaryColor, 0.30f);
        else
            tint = Color.white;

        if (pinIcon) pinIcon.color = tint;
    }

    public void SetSelected(bool selected)
    {
        if (selectedOutline) selectedOutline.SetActive(selected);
    }

    // ── Pointer events ─────────────────────────────────────────────────────────

    public void OnPointerClick(PointerEventData e) => _map?.OnNodeClicked(this);
    public void OnPointerEnter(PointerEventData e) => _map?.OnNodeHovered(this, true);
    public void OnPointerExit(PointerEventData e)  => _map?.OnNodeHovered(this, false);
}
