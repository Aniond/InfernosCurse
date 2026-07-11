using UnityEngine;

public sealed class WorldAgentSite : MonoBehaviour
{
    public string siteId;
    public string districtId;
    public WorldAgentSiteRole role;

    void OnDrawGizmos()
    {
        Gizmos.color = role == WorldAgentSiteRole.Hide
            ? new Color(0.25f, 0.2f, 0.45f, 0.9f)
            : new Color(0.75f, 0.55f, 0.15f, 0.9f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.25f, 0.35f);
    }
}
