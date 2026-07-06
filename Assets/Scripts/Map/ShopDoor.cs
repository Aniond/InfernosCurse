using UnityEngine;
using UnityEngine.SceneManagement;

// A shop entrance on a street (Street_EW template): the ZoneExit dwell-trigger
// pattern, but tolerant of not-yet-built interiors. With targetScene set it
// travels like a door (Salone↔Signoria pattern); left empty it just logs —
// streets can ship with closed shops and open them later, no re-wiring.
[RequireComponent(typeof(BoxCollider))]
public class ShopDoor : MonoBehaviour
{
    [Header("Shop")]
    [Tooltip("Stable id for this shop (quests/economy hooks later).")]
    public string shopId = "";
    [Tooltip("Shown in logs/UI (e.g. 'Bottega del Fabbro').")]
    public string displayName = "Shop";

    [Header("Destination (empty = closed)")]
    public string targetScene = "";
    public string targetEntryId = "";

    [Header("Trigger (ZoneExit pattern)")]
    public string playerTag = "Player";
    public float dwellTime = 0.15f;
    public float armDelay = 0.5f;

    float _dwell;
    bool _armed;
    bool _fired;

    void Awake() => GetComponent<BoxCollider>().isTrigger = true;

    void OnEnable()
    {
        _fired = false;
        _dwell = 0f;
        _armed = false;
        CancelInvoke(nameof(Arm));
        Invoke(nameof(Arm), armDelay);
    }

    void OnDisable() => CancelInvoke(nameof(Arm));

    void Arm() => _armed = true;

    void OnTriggerStay(Collider other)
    {
        if (_fired || !_armed) return;
        if (!other.CompareTag(playerTag)) return;
        _dwell += Time.deltaTime;
        if (_dwell >= dwellTime) Fire();
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        _dwell = 0f;
        _fired = false;
    }

    void Fire()
    {
        _fired = true;
        if (string.IsNullOrEmpty(targetScene))
        {
            Debug.Log($"[ShopDoor] '{displayName}' ({shopId}) is closed — no targetScene wired yet.");
            return;
        }
        if (!Application.CanStreamedLevelBeLoaded(targetScene))
        {
            Debug.LogError($"[ShopDoor] Scene '{targetScene}' is not in Build Settings.");
            _fired = false;
            return;
        }
        TravelIntent.SetEntry(targetEntryId);
        SceneManager.LoadScene(targetScene);
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        var col = GetComponent<BoxCollider>();
        if (col == null) return;
        bool open = !string.IsNullOrEmpty(targetScene);
        Gizmos.color = open ? new Color(0.3f, 0.9f, 0.4f, 0.3f) : new Color(0.9f, 0.4f, 0.3f, 0.25f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(col.center, col.size);
    }
#endif
}
