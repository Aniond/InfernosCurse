using UnityEngine;

// A walk-into service spot (ZoneExit's dwell-trigger pattern): an inn door,
// a guild donation bench, a chapel. Fires once when the player lingers,
// opens the matching UI, and re-arms when they step out.
[RequireComponent(typeof(BoxCollider))]
public class GuildInteractionZone : MonoBehaviour
{
    public enum Kind { Inn, Donation, Transmute, Join }

    [Header("Service")]
    public Kind kind = Kind.Inn;
    [Tooltip("Guild this spot belongs to (donation/transmute; inn cleanse eligibility).")]
    public string guildId = "albergatori";
    [Tooltip("Shown as the panel title (e.g. 'Locanda del Mercato').")]
    public string label = "Inn";
    [Tooltip("Inn only: base price per night in florins.")]
    public int innPrice = 10;
    [Tooltip("Inn only: is this an Albergatori house (rank perks apply here)?")]
    public bool isGuildInn = true;

    [Header("Trigger (ZoneExit pattern)")]
    public string playerTag = "Player";
    public float dwellTime = 0.15f;
    public float armDelay = 0.5f;

    float _dwell;
    bool _armed;
    bool _fired;

    void Awake()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

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
        _fired = false;   // stepped out — service available again
    }

    void Fire()
    {
        _fired = true;
        switch (kind)
        {
            case Kind.Inn:
                RestMenuUI.Instance?.OpenInn(label, innPrice, isGuildInn);
                break;
            case Kind.Donation:
                GuildPanelUI.Instance?.OpenDonation(guildId, label);
                break;
            case Kind.Transmute:
                GuildPanelUI.Instance?.OpenTransmute(label);
                break;
            case Kind.Join:
                GuildPanelUI.Instance?.OpenJoin(guildId, label, transform.position);
                break;
        }
    }
}
