using UnityEngine;
using UnityEngine.AI;

public sealed class PersistentCrierActor : MonoBehaviour
{
    public string agentId;
    public float fallbackMoveSpeed = 2f;
    public float preachSecondsBeforeRelocation = 30f;

    NavMeshAgent _navigation;
    WorldAgentSite _targetSite;
    float _stateStarted;

    void Awake() => _navigation = GetComponent<NavMeshAgent>();

    void OnEnable()
    {
        _stateStarted = Time.time;
        BindTarget();
    }

    void Update()
    {
        if (!PersistentLimboWorldState.TryGetAgent(agentId, out var record)) return;
        if (record.defeated)
        {
            gameObject.SetActive(false);
            return;
        }

        bool curfew = GameClock.Hour < 6f || GameClock.Hour >= 22f;
        if (curfew && record.activityState != CrierActivityState.Hide)
        {
            record.activityState = CrierActivityState.Hide;
            BindTarget(WorldAgentSiteRole.Hide);
        }
        else if (!curfew && record.activityState == CrierActivityState.Hide)
        {
            record.activityState = CrierActivityState.Travel;
            BindTarget(WorldAgentSiteRole.Preach);
        }

        switch (record.activityState)
        {
            case CrierActivityState.RelocateEvade:
                record.activityState = CrierActivityState.Travel;
                BindTarget(WorldAgentSiteRole.Preach);
                break;
            case CrierActivityState.Travel:
                if (MoveToTarget())
                {
                    record.activityState = curfew ? CrierActivityState.Hide : CrierActivityState.Preach;
                    _stateStarted = Time.time;
                }
                break;
            case CrierActivityState.Preach:
                if (Time.time - _stateStarted >= preachSecondsBeforeRelocation)
                {
                    PersistentLimboWorldState.SelectNextSite(record);
                    record.activityState = CrierActivityState.RelocateEvade;
                }
                break;
            case CrierActivityState.Hide:
                MoveToTarget();
                break;
        }
    }

    public void DisruptCurrentSermon()
    {
        var calendar = GameCalendar.Instance;
        string key = calendar != null ? calendar.Year + ":" + calendar.DayOfYear : "undated";
        if (!PersistentLimboWorldState.DisruptAgent(agentId, key, out string error))
            Debug.LogWarning("[LimboWorld] " + error);
        else
            BindTarget(WorldAgentSiteRole.Preach);
    }

    void BindTarget(WorldAgentSiteRole? preferredRole = null)
    {
        if (!PersistentLimboWorldState.TryGetAgent(agentId, out var record)) return;
        var sites = FindObjectsByType<WorldAgentSite>(FindObjectsInactive.Exclude);
        _targetSite = null;
        foreach (var site in sites)
            if (site.siteId == record.currentSiteId &&
                (!preferredRole.HasValue || site.role == preferredRole.Value))
            { _targetSite = site; break; }
        if (_targetSite == null)
            foreach (var site in sites)
                if (site.districtId == record.districtId &&
                    (!preferredRole.HasValue || site.role == preferredRole.Value))
                { _targetSite = site; break; }
        if (_targetSite != null)
        {
            record.currentSiteId = _targetSite.siteId;
            if (_navigation != null && _navigation.isOnNavMesh)
                _navigation.SetDestination(_targetSite.transform.position);
        }
    }

    bool MoveToTarget()
    {
        if (_targetSite == null) { BindTarget(); return false; }
        if (_navigation != null && _navigation.isOnNavMesh)
        {
            _navigation.SetDestination(_targetSite.transform.position);
            return !_navigation.pathPending && _navigation.remainingDistance <= _navigation.stoppingDistance + 0.1f;
        }
        transform.position = Vector3.MoveTowards(
            transform.position, _targetSite.transform.position, fallbackMoveSpeed * Time.deltaTime);
        return Vector3.SqrMagnitude(transform.position - _targetSite.transform.position) <= 0.01f;
    }
}
