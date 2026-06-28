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

    // Track current alpha per renderer
    private readonly Dictionary<Renderer, float> _targetAlpha = new();
    private readonly Dictionary<Renderer, Material[]> _materials = new();

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
                _materials[r] = mats;
                _targetAlpha[r] = 1f;
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

        // Find all building renderers hit by the ray
        var hits = Physics.RaycastAll(camPos, dir.normalized, dist, ~0, QueryTriggerInteraction.Ignore);
        var occluding = new HashSet<Renderer>();

        foreach (var hit in hits)
        {
            if (!hit.collider.CompareTag("Building")) continue;
            var renderers = hit.collider.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
                occluding.Add(r);
        }

        // Update target alphas and lerp
        foreach (var kvp in _materials)
        {
            var r = kvp.Key;
            bool isOccluding = occluding.Contains(r);
            float target = isOccluding ? fadedAlpha : 1f;
            float speed = isOccluding ? fadeSpeed : restoreSpeed;

            _targetAlpha[r] = Mathf.Lerp(_targetAlpha[r], target, Time.deltaTime * speed);
            float alpha = _targetAlpha[r];

            foreach (var mat in kvp.Value)
                SetAlpha(mat, alpha);
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
