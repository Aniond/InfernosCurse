using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

// One clickable pin on the Florence world map. Spawned by WorldMapUI from a
// HubNode. Reflects curse state via tint and reports clicks/hovers upward.
[RequireComponent(typeof(RectTransform))]
public class MapNodeView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Pin Visuals")]
    public Image    pinIcon;
    public Image    curseRing;      // colored ring that fills with curse level
    public TMP_Text label;
    public GameObject sanctuaryBadge;
    public GameObject ritualBadge;
    public GameObject selectedOutline;

    [Header("Curse Tint")]
    public Color cleanColor   = new Color(0.85f, 0.85f, 0.80f);
    public Color cursedColor  = new Color(0.55f, 0.10f, 0.65f);
    public Color sanctuaryColor = new Color(0.95f, 0.90f, 0.55f);

    public HubNode Node { get; private set; }

    private WorldMapUI _map;

    public void Bind(HubNode node, WorldMapUI map)
    {
        Node = node;
        _map = map;

        if (label) label.text = node.displayName;
        if (sanctuaryBadge) sanctuaryBadge.SetActive(node.isSanctuarySite);
        if (ritualBadge)    ritualBadge.SetActive(node.isRitualSite);
        if (selectedOutline) selectedOutline.SetActive(false);

        Refresh();
    }

    // Re-read state and update visuals. Called on HubMap.OnNodeChanged.
    // The curse level is a HIDDEN value — pins never display it as a measurable
    // gauge. The icon tint shifts only coarsely so the player can sense unease
    // without reading a number.
    public void Refresh()
    {
        if (Node == null) return;

        // The medallion art stays gold. State only shifts the tint slightly so
        // the player senses unease without reading a number. Near-white keeps the
        // gold pure; a faint hue creeps in for sanctuary / tainted places.
        Color tint;
        if (Node.isSanctuarySite)
            tint = Color.Lerp(Color.white, sanctuaryColor, 0.30f);
        else if (Node.curseLevel >= 0.5f)
            tint = Color.Lerp(Color.white, cursedColor, 0.22f);   // subtle, fixed
        else
            tint = Color.white;

        if (pinIcon) pinIcon.color = tint;
        if (curseRing) curseRing.gameObject.SetActive(false);     // no proportional gauge
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
