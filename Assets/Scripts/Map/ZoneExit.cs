using UnityEngine;
using UnityEngine.SceneManagement;

// A walk-through exit at the edge of an explorable zone. When the player enters
// the trigger, it travels — either back to the world map or to a linked scene.
// Pair with a ground fade so the player can see where the edge leads.
[RequireComponent(typeof(BoxCollider))]
public class ZoneExit : MonoBehaviour
{
    public enum ExitMode { ToWorldMap, ToScene }

    [Header("Exit")]
    public ExitMode mode = ExitMode.ToWorldMap;

    [Tooltip("Scene to load. For ToWorldMap this defaults to the world map; " +
             "for ToScene set the destination zone scene.")]
    public string targetScene = "WorldMap";

    [Tooltip("ZoneEntryPoint id to spawn at in the destination scene (ToScene). " +
             "Leave empty to use the destination's default spawn.")]
    public string targetEntryId = "";

    [Tooltip("Tag the player root carries. Default 'Player'.")]
    public string playerTag = "Player";

    [Header("Behaviour")]
    [Tooltip("Seconds the player must stay in the zone before it fires (prevents accidental brush).")]
    public float dwellTime = 0.15f;
    [Tooltip("Prevents re-trigger immediately after arriving via this exit.")]
    public float armDelay = 0.5f;

    private float _dwell;
    private bool  _armed;
    private bool  _fired;

    void Awake()
    {
        var col = GetComponent<BoxCollider>();
        col.isTrigger = true;
    }

    void OnEnable()
    {
        _fired = false;
        _dwell = 0f;
        _armed = false;
        CancelInvoke(nameof(Arm));   // avoid stacked Arm invokes on rapid re-enable
        Invoke(nameof(Arm), armDelay);
    }

    void OnDisable() => CancelInvoke(nameof(Arm));

    void Arm() => _armed = true;

    void OnTriggerStay(Collider other)
    {
        if (_fired || !_armed) return;
        if (!other.CompareTag(playerTag)) return;

        _dwell += Time.deltaTime;
        if (_dwell >= dwellTime)
            Fire();
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(playerTag)) _dwell = 0f;
    }

    void Fire()
    {
        _fired = true;
        switch (mode)
        {
            case ExitMode.ToWorldMap:
                Debug.Log("[ZoneExit] Returning to world map.");
                LoadIfPossible("WorldMap");
                break;
            case ExitMode.ToScene:
                // Tell the destination scene where to spawn the player.
                TravelIntent.SetEntry(targetEntryId);
                Debug.Log($"[ZoneExit] Traveling to {targetScene}, entry '{targetEntryId}'.");
                LoadIfPossible(targetScene);
                break;
        }
    }

    void LoadIfPossible(string scene)
    {
        if (string.IsNullOrEmpty(scene) || !Application.CanStreamedLevelBeLoaded(scene))
        {
            Debug.LogError($"[ZoneExit] Scene '{scene}' is not in Build Settings.");
            _fired = false; // allow retry once fixed
            return;
        }
        SceneManager.LoadScene(scene);
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        var col = GetComponent<BoxCollider>();
        if (col == null) return;
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.25f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(col.center, col.size);
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.8f);
        Gizmos.DrawWireCube(col.center, col.size);
    }
#endif
}
