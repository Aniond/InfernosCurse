using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// HD-2D dollhouse occlusion. The camera spherecasts toward the player and
// hides only approved architecture. Seamless interiors can keep hidden roofs
// and walls casting shadows so exterior sunlight never ignores the building.
public class CameraOcclusionFader : MonoBehaviour
{
    public enum OccluderHideMode
    {
        DisableRenderer,
        ShadowsOnly,
    }

    [Header("Target")]
    [Tooltip("Player to keep visible. Auto-finds by tag if empty.")]
    public Transform target;
    public string playerTag = "Player";
    [Tooltip("Aim point above the player's feet (chest height).")]
    public float targetHeight = 1.2f;

    [Header("What counts as occluding architecture")]
    [Tooltip("Objects whose name starts with one of these may be hidden.")]
    public string[] wallPrefixes = { "Oct_", "Nave_Wall_", "Nave_Stub_", "Facade_", "Throat_", "Trib_" };
    [Tooltip("Explicit renderers that may occlude the player, independent of naming.")]
    public Renderer[] explicitOccluders = System.Array.Empty<Renderer>();
    [Tooltip("Radius of the sightline probe.")]
    public float probeRadius = 0.6f;
    [Tooltip("ShadowsOnly is recommended for seamless interiors.")]
    public OccluderHideMode hideMode = OccluderHideMode.DisableRenderer;

    readonly Dictionary<Renderer, RendererState> hidden = new();
    readonly HashSet<Renderer> stillBlocking = new();
    readonly Dictionary<Collider, Renderer[]> pierMap = new();
    static readonly RaycastHit[] hits = new RaycastHit[32];

    readonly struct RendererState
    {
        public readonly bool enabled;
        public readonly ShadowCastingMode shadowCastingMode;

        public RendererState(Renderer renderer)
        {
            enabled = renderer.enabled;
            shadowCastingMode = renderer.shadowCastingMode;
        }
    }

    void Start()
    {
        if (target == null)
        {
            var go = GameObject.FindGameObjectWithTag(playerTag);
            if (go != null) target = go.transform;
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 from = transform.position;
        Vector3 to = target.position + Vector3.up * targetHeight;
        Vector3 dir = to - from;
        float dist = dir.magnitude;
        if (dist < 0.01f) return;
        dir /= dist;

        stillBlocking.Clear();
        int count = Physics.SphereCastNonAlloc(
            from, probeRadius, dir, hits, dist, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider collider = hits[i].collider;
            if (collider == null || collider.transform == target) continue;
            string colliderName = collider.gameObject.name;

            if (IsWall(colliderName) || IsExplicit(collider.transform))
            {
                Renderer renderer = collider.GetComponent<Renderer>();
                if (renderer == null) renderer = collider.GetComponentInChildren<Renderer>();
                if (renderer == null) continue;
                stillBlocking.Add(renderer);
                Hide(renderer);
            }
            else if (colliderName.StartsWith("Pier_") || colliderName.StartsWith("TribPier_"))
            {
                if (!pierMap.TryGetValue(collider, out Renderer[] pierRenderers))
                {
                    var model = GameObject.Find("PierModel_" + colliderName);
                    pierRenderers = model != null
                        ? model.GetComponentsInChildren<Renderer>()
                        : System.Array.Empty<Renderer>();
                    pierMap[collider] = pierRenderers;
                }
                foreach (Renderer renderer in pierRenderers)
                {
                    if (renderer == null) continue;
                    stillBlocking.Add(renderer);
                    Hide(renderer);
                }
            }
        }

        var restore = new List<Renderer>();
        foreach (var pair in hidden)
        {
            Renderer renderer = pair.Key;
            if (renderer == null || !stillBlocking.Contains(renderer)) restore.Add(renderer);
        }
        foreach (Renderer renderer in restore) Restore(renderer);
    }

    bool IsWall(string objectName)
    {
        if (wallPrefixes == null) return false;
        foreach (string prefix in wallPrefixes)
            if (!string.IsNullOrEmpty(prefix) && objectName.StartsWith(prefix)) return true;
        return false;
    }

    bool IsExplicit(Transform hit)
    {
        if (explicitOccluders == null) return false;
        foreach (Renderer renderer in explicitOccluders)
        {
            if (renderer == null) continue;
            Transform candidate = renderer.transform;
            if (hit == candidate || hit.IsChildOf(candidate) || candidate.IsChildOf(hit)) return true;
        }
        return false;
    }

    void Hide(Renderer renderer)
    {
        if (renderer == null || hidden.ContainsKey(renderer)) return;
        hidden.Add(renderer, new RendererState(renderer));
        if (hideMode == OccluderHideMode.ShadowsOnly && renderer.shadowCastingMode != ShadowCastingMode.Off)
        {
            renderer.enabled = true;
            renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        }
        else
        {
            renderer.enabled = false;
        }
    }

    void Restore(Renderer renderer)
    {
        if (renderer == null)
        {
            hidden.Remove(renderer);
            return;
        }
        if (!hidden.TryGetValue(renderer, out RendererState state)) return;
        renderer.enabled = state.enabled;
        renderer.shadowCastingMode = state.shadowCastingMode;
        hidden.Remove(renderer);
    }

    void OnDisable()
    {
        foreach (var pair in hidden)
        {
            Renderer renderer = pair.Key;
            if (renderer == null) continue;
            renderer.enabled = pair.Value.enabled;
            renderer.shadowCastingMode = pair.Value.shadowCastingMode;
        }
        hidden.Clear();
    }
}
