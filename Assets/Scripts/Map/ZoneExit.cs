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

    [Tooltip("Scene to load when mode is ToScene.")]
    public string targetScene = "WorldMap";

    [Tooltip("Tag the player root carries. Default 'Player'.")]
    public string playerTag = "Player";

    [Header("Entry (where the player spawns when arriving here)")]
    [Tooltip("If another zone routes the player to THIS exit, they spawn here.")]
    public Transform entryPoint;

    [Tooltip("Unique id so other zones can target this entry point.")]
    public string entryId = "";

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
        Invoke(nameof(Arm), armDelay);
    }

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
                Debug.Log($"[ZoneExit] Traveling to {targetScene}.");
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

        if (entryPoint != null)
        {
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(entryPoint.position, 0.3f);
        }
    }
#endif
}
