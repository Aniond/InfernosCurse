using System;
using UnityEngine;

/// <summary>
/// Authoritative runtime contract for a small or medium interior embedded in
/// an exterior zone. The module owns presentation and activation state; it
/// does not own a Player, Main Camera, weather authority, or scene travel.
/// </summary>
[DisallowMultipleComponent]
public sealed class SeamlessInteriorModule : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] string buildingId = string.Empty;
    [SerializeField] string subLocationId = string.Empty;
    [SerializeField] bool protectedSocialInterior = true;

    [Header("Threshold and recovery")]
    [SerializeField] Transform exteriorThreshold;
    [SerializeField] Transform interiorThreshold;
    [SerializeField] Transform exteriorFallback;
    [SerializeField] Transform interiorFallback;
    [SerializeField] SeamlessInteriorPortal portal;

    [Header("Presentation")]
    [SerializeField] SeamlessInteriorEnvironmentZone environmentZone;
    [SerializeField] SeamlessInteriorCameraZone cameraZone;
    [SerializeField] GameObject[] enableWhileInside = Array.Empty<GameObject>();
    [SerializeField] GameObject[] enableWhileOutside = Array.Empty<GameObject>();

    [Header("Battle protection")]
    [SerializeField] Collider battleBlocker;

    bool _playerInside;
    bool _battleLocked;

    public string BuildingId => buildingId;
    public string SubLocationId => subLocationId;
    public bool ProtectedSocialInterior => protectedSocialInterior;
    public bool PlayerInside => _playerInside;
    public bool BattleLocked => _battleLocked;
    public SeamlessInteriorPortal Portal => portal;
    public Vector3 ExteriorFallbackPosition =>
        exteriorFallback != null ? exteriorFallback.position :
        exteriorThreshold != null ? exteriorThreshold.position : transform.position;
    public Vector3 InteriorFallbackPosition =>
        interiorFallback != null ? interiorFallback.position :
        interiorThreshold != null ? interiorThreshold.position : transform.position;

    void Reset() => ResolveLocalReferences();

    void Awake() => ResolveLocalReferences();

    void OnEnable()
    {
        if (!SeamlessInteriorRegistry.Register(this, out string error))
            Debug.LogError($"[SeamlessInterior] Registration failed: {error}.", this);
        ApplyState();
    }

    void OnDisable() => SeamlessInteriorRegistry.Unregister(this);

    public void ResolveLocalReferences()
    {
        if (portal == null) portal = GetComponentInChildren<SeamlessInteriorPortal>(true);
        if (environmentZone == null) environmentZone = GetComponentInChildren<SeamlessInteriorEnvironmentZone>(true);
        if (cameraZone == null) cameraZone = GetComponentInChildren<SeamlessInteriorCameraZone>(true);
        if (portal != null) portal.Bind(this);
    }

    public bool TryValidateRuntime(out string error)
    {
        ResolveLocalReferences();
        if (string.IsNullOrWhiteSpace(buildingId))
        {
            error = "building ID is empty";
            return false;
        }
        if (string.IsNullOrWhiteSpace(subLocationId))
        {
            error = "sub-location ID is empty";
            return false;
        }
        if (portal == null)
        {
            error = "portal is missing";
            return false;
        }
        if (exteriorThreshold == null || interiorThreshold == null)
        {
            error = "threshold anchors are incomplete";
            return false;
        }
        if (exteriorFallback == null || interiorFallback == null)
        {
            error = "safe recovery anchors are incomplete";
            return false;
        }
        if (protectedSocialInterior && battleBlocker == null)
        {
            error = "protected interior has no battle blocker";
            return false;
        }
        error = string.Empty;
        return true;
    }

    public void SetPlayerInside(bool inside)
    {
        if (_playerInside == inside)
        {
            if (inside) SeamlessInteriorRegistry.SetActive(this);
            return;
        }

        _playerInside = inside;
        if (inside) SeamlessInteriorRegistry.SetActive(this);
        else if (SeamlessInteriorRegistry.ActiveModule == this) SeamlessInteriorRegistry.SetActive(null);
        ApplyState();
    }

    public void SetBattleLocked(bool locked)
    {
        _battleLocked = locked;
        if (portal != null) portal.SetBattleLocked(locked);
        ApplyBattleBlocker();
    }

    void ApplyState()
    {
        SetGroup(enableWhileInside, _playerInside);
        SetGroup(enableWhileOutside, !_playerInside);
        environmentZone?.SetPlayerInside(_playerInside);
        cameraZone?.SetPlayerInside(_playerInside);
        ApplyBattleBlocker();
    }

    void ApplyBattleBlocker()
    {
        if (battleBlocker != null)
            battleBlocker.enabled = protectedSocialInterior && _battleLocked;
    }

    static void SetGroup(GameObject[] group, bool active)
    {
        if (group == null) return;
        foreach (GameObject target in group)
            if (target != null && target.activeSelf != active)
                target.SetActive(active);
    }

#if UNITY_EDITOR
    public void Configure(
        string newBuildingId,
        string newSubLocationId,
        Transform newExteriorThreshold,
        Transform newInteriorThreshold,
        Transform newExteriorFallback,
        Transform newInteriorFallback,
        SeamlessInteriorPortal newPortal,
        Collider newBattleBlocker,
        SeamlessInteriorEnvironmentZone newEnvironmentZone = null,
        SeamlessInteriorCameraZone newCameraZone = null)
    {
        buildingId = newBuildingId ?? string.Empty;
        subLocationId = newSubLocationId ?? string.Empty;
        exteriorThreshold = newExteriorThreshold;
        interiorThreshold = newInteriorThreshold;
        exteriorFallback = newExteriorFallback;
        interiorFallback = newInteriorFallback;
        portal = newPortal;
        battleBlocker = newBattleBlocker;
        environmentZone = newEnvironmentZone;
        cameraZone = newCameraZone;
        ResolveLocalReferences();
    }
#endif
}
