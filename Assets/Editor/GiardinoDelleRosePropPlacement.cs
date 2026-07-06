using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Marker-driven prop placement for GiardinoDelleRose.unity (layout v3).
// The scene builder drops objects named `MARKER_<assetId>@<targetHeight>`;
// this consumes them:
//   - GLB exists at Props/<assetId>.glb  -> instantiate, scale by renderer
//     bounds to targetHeight, ground-place at the marker, take the marker's
//     Y rotation, add a BoxCollider, destroy the marker.
//   - GLB missing (still generating)     -> leave the marker in place with a
//     visible fallback so the layout stays readable; re-running this menu
//     item after the batch lands swaps fallbacks for the real models.
// Per-asset up-axis fixes live in AxisFix() — generated GLBs sometimes come
// in lying on their side (garden-stone-bench: longest extent on local X).
public static class GiardinoDelleRosePropPlacement
{
    const string PropsFolder = "Assets/Environment/GiardinoDelleRose/Props";

    [MenuItem("InfernosCurse/Giardino delle Rose/3. Place Hero Props")]
    public static void Place()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "GiardinoDelleRose")
        {
            Debug.LogError("[GiardinoDelleRosePropPlacement] Open GiardinoDelleRose.unity first.");
            return;
        }

        var markers = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None)
            .Where(g => g.name.StartsWith("MARKER_"))
            .ToArray();

        int placed = 0, pending = 0;
        foreach (var marker in markers)
        {
            if (!TryParseMarker(marker.name, out string assetId, out float targetHeight))
            {
                Debug.LogWarning($"[GiardinoDelleRosePropPlacement] Unparseable marker '{marker.name}' — skipped.");
                continue;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PropsFolder}/{assetId}.glb");
            if (prefab == null)
            {
                EnsureFallbackVisual(marker, assetId);
                pending++;
                continue;
            }

            PlaceAsset(prefab, assetId, targetHeight, marker);
            Object.DestroyImmediate(marker);
            placed++;
        }

        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[GiardinoDelleRosePropPlacement] Placed {placed}, pending (GLB not generated yet) {pending}. " +
                  (pending > 0 ? "Re-run this menu item once the asset batch finishes." : "All markers resolved."));
    }

    static bool TryParseMarker(string name, out string assetId, out float targetHeight)
    {
        assetId = null; targetHeight = 1f;
        var body = name.Substring("MARKER_".Length);
        int at = body.LastIndexOf('@');
        if (at < 1) return false;
        assetId = body.Substring(0, at);
        return float.TryParse(body.Substring(at + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out targetHeight);
    }

    // Corrective pre-rotation for GLBs whose generated up-axis isn't Unity Y.
    // Found empirically per asset (raw bounds check) — extend as new models land.
    static Vector3? AxisFix(string assetId) => assetId switch
    {
        "garden-stone-bench" => new Vector3(0f, 0f, 90f),
        _ => null,
    };

    static void PlaceAsset(GameObject prefab, string assetId, float targetHeight, GameObject marker)
    {
        // Pivot carries the marker's placement rotation; the mesh child
        // carries only its own axis correction, so the two never fight.
        var pivot = new GameObject(assetId);
        pivot.transform.SetParent(GetGroup().transform, false);

        var mesh = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        mesh.name = assetId + "_mesh";
        var fix = AxisFix(assetId);
        if (fix.HasValue) mesh.transform.rotation = Quaternion.Euler(fix.Value);

        var renderers = mesh.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            float scale = bounds.size.y > 0.0001f ? targetHeight / bounds.size.y : 1f;
            mesh.transform.localScale = Vector3.one * scale;
        }

        mesh.transform.SetParent(pivot.transform, true);
        EnsureCollider(mesh);

        // Rotation FIRST (grounding measures world bounds — rotating after
        // grounding would swing an off-pivot mesh away from where we measured).
        pivot.transform.position = marker.transform.position;
        pivot.transform.rotation = marker.transform.rotation;

        if (renderers.Length > 0)
        {
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            // One combined correction: bottom of bounds to the marker's Y,
            // bounds center over the marker's XZ.
            var offset = new Vector3(
                b.center.x - marker.transform.position.x,
                b.min.y - marker.transform.position.y,
                b.center.z - marker.transform.position.z);
            pivot.transform.position -= offset;
        }
    }

    static void EnsureFallbackVisual(GameObject marker, string assetId)
    {
        // Hedge markers are already visible primitive cubes; empty markers get
        // a pedestal stub so the layout reads in-scene while GLBs generate.
        if (marker.GetComponent<Renderer>() != null) return;
        if (marker.transform.childCount > 0) return;

        var stub = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stub.name = "FALLBACK_" + assetId;
        stub.transform.SetParent(marker.transform, false);
        stub.transform.localScale = new Vector3(0.8f, 0.5f, 0.8f);
        stub.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        var renderer = stub.GetComponent<Renderer>();
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        renderer.sharedMaterial = new Material(shader != null ? shader : Shader.Find("Standard"))
            { color = new Color(0.62f, 0.58f, 0.52f) };
    }

    static GameObject GetGroup()
    {
        var g = GameObject.Find("[PlacedProps]");
        return g != null ? g : new GameObject("[PlacedProps]");
    }

    static void EnsureCollider(GameObject go)
    {
        if (go.GetComponentInChildren<Collider>() != null) return;
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        var bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);

        var col = go.AddComponent<BoxCollider>();
        col.center = go.transform.InverseTransformPoint(bounds.center);
        var scale = go.transform.lossyScale;
        col.size = new Vector3(
            bounds.size.x / Mathf.Max(0.001f, scale.x),
            bounds.size.y / Mathf.Max(0.001f, scale.y),
            bounds.size.z / Mathf.Max(0.001f, scale.z));
    }
}
