using System.Collections.Generic;
using UnityEngine;

// Fades buildings that occlude the player between camera and player position.
// Attach to a CameraManager GameObject. Assign mainCamera and player in Inspector.
public class OcclusionFade : MonoBehaviour
{
    [Header("References")]
    public Camera mainCamera;
    public Transform player;

    [Header("Fade Settings")]
    [Range(0f, 1f)] public float fadedAlpha = 0.25f;
    public float fadeSpeed = 5f;    // lerp speed for fading out
    public float restoreSpeed = 3f; // lerp speed for restoring

    [Tooltip("Layers the occlusion ray tests against. Set to the building layer(s) " +
             "only — keeps the player and other geometry out of the raycast.")]
    public LayerMask occluderMask = ~0;

    // Per-renderer fade state: its cloned materials + current alpha.
    private class FadeEntry { public Material[] materials; public float alpha = 1f; }
    private readonly Dictionary<Renderer, FadeEntry> _entries = new();

    private void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;

        // Cache all building renderers and swap to transparent-capable material instances
        var buildings = GameObject.FindGameObjectsWithTag("Building");
        foreach (var b in buildings)
        {
            var renderers = b.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                // Clone materials so we don't modify shared assets
                var mats = r.materials;
                for (int i = 0; i < mats.Length; i++)
                {
                    mats[i] = new Material(mats[i]);
                    SetMaterialTransparent(mats[i]);
                }
                r.materials = mats;
                _entries[r] = new FadeEntry { materials = mats, alpha = 1f };
            }
        }
    }

    private void Update()
    {
        if (mainCamera == null || player == null) return;

        Vector3 camPos = mainCamera.transform.position;
        Vector3 playerPos = player.position + Vector3.up * 1f; // aim at player chest height
        Vector3 dir = playerPos - camPos;
        float dist = dir.magnitude;

        // Find all building renderers hit by the ray (filtered to occluder layers)
        var hits = Physics.RaycastAll(camPos, dir.normalized, dist, occluderMask, QueryTriggerInteraction.Ignore);
        var occluding = new HashSet<Renderer>();

        foreach (var hit in hits)
        {
            if (!hit.collider.CompareTag("Building")) continue;
            var renderers = hit.collider.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
                occluding.Add(r);
        }

        // Update target alphas and lerp
        foreach (var kvp in _entries)
        {
            var entry = kvp.Value;
            bool isOccluding = occluding.Contains(kvp.Key);
            float target = isOccluding ? fadedAlpha : 1f;
            float speed  = isOccluding ? fadeSpeed : restoreSpeed;

            entry.alpha = Mathf.Lerp(entry.alpha, target, Time.deltaTime * speed);

            foreach (var mat in entry.materials)
                SetAlpha(mat, entry.alpha);
        }
    }

    private static void SetMaterialTransparent(Material mat)
    {
        // Switch URP Lit material to Transparent surface type
        mat.SetFloat("_Surface", 1f);           // 1 = Transparent
        mat.SetFloat("_Blend", 0f);             // Alpha blend
        mat.SetFloat("_AlphaClip", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        mat.SetInt("_SrcBlend",  (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend",  (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite",    0);
    }

    private static void SetAlpha(Material mat, float alpha)
    {
        Color c = mat.color;
        c.a = alpha;
        mat.color = c;

        // Also drive _BaseColor which URP Lit uses
        if (mat.HasProperty("_BaseColor"))
        {
            Color bc = mat.GetColor("_BaseColor");
            bc.a = alpha;
            mat.SetColor("_BaseColor", bc);
        }
    }
}
