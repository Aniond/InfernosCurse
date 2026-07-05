using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Replaces the placeholder primitives in GiardinoDelleRose.unity with the
// generated hero props (fountain basin, bench, pergola, Florist stall) and
// rose variants (4 colors) from Assets/Environment/GiardinoDelleRose/Props/.
// Follows the established GLB placement recipe (Ponte Vecchio / Duomo props):
// scale by RENDERER bounds (never the pivot), re-center by bounds, add a
// BoxCollider encapsulating every renderer.
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

        PlaceFountain();
        PlaceRoseBeds();
        PlaceBenchAndPergola();

        EditorSceneManager.SaveScene(scene);
        Debug.Log("[GiardinoDelleRosePropPlacement] Hero props placed, placeholders removed, scene saved.");
    }

    // ── Fountain ──────────────────────────────────────────────────────────────

    static void PlaceFountain()
    {
        var placeholder = GameObject.Find("FountainBasin_PLACEHOLDER");
        if (placeholder == null) { Debug.LogWarning("[GiardinoDelleRosePropPlacement] FountainBasin_PLACEHOLDER not found — skipped."); return; }

        Vector3 pos = placeholder.transform.position;
        Transform parent = placeholder.transform.parent;

        var basin = InstantiateScaled("garden-fountain-basin", targetHeight: 1.2f);
        if (basin != null)
        {
            basin.transform.SetParent(parent, false);
            PlaceAtGroundPoint(basin, pos);
        }

        Object.DestroyImmediate(placeholder);
    }

    // ── Rose beds ────────────────────────────────────────────────────────────

    static void PlaceRoseBeds()
    {
        // NOTE: FindGameObjectsWithTag("Untagged") always returns empty —
        // Unity treats the built-in "Untagged" tag as "no tag" and excludes it
        // from tag queries. Search by type + name instead.
        var placeholders = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None)
            .Where(g => g.name == "RoseBed_PLACEHOLDER")
            .ToArray();
        if (placeholders.Length == 0) { Debug.LogWarning("[GiardinoDelleRosePropPlacement] No RoseBed_PLACEHOLDER objects found — skipped."); return; }

        string[] roseIds = { "rose-bush-crimson", "rose-bush-ivory", "rose-bush-gold", "rose-climbing-wine" };
        var group = new GameObject("[RoseBeds]");

        int colorIndex = 0;
        foreach (var placeholder in placeholders)
        {
            Vector3 basePos = placeholder.transform.position;

            // Each former bed spot gets a small cluster of 3 roses, mixing
            // colors so no single bed reads as one flat color — "realistic
            // but surreal" per the design brief (multiple colored roses).
            for (int i = 0; i < 3; i++)
            {
                string roseId = roseIds[colorIndex % roseIds.Length];
                colorIndex++;

                var rose = InstantiateScaled(roseId, targetHeight: Random.Range(0.9f, 1.3f));
                if (rose == null) continue;

                rose.transform.SetParent(group.transform, false);
                Vector2 jitter = Random.insideUnitCircle * 1.1f;
                Vector3 spot = basePos + new Vector3(jitter.x, 0f, jitter.y);
                PlaceAtGroundPoint(rose, spot);
                rose.transform.Rotate(Vector3.up, Random.Range(0f, 360f), Space.World);
            }

            Object.DestroyImmediate(placeholder);
        }

        if (group.transform.childCount == 0) Object.DestroyImmediate(group);
    }

    // ── Bench + pergola (new dressing, no prior placeholder) ────────────────

    static void PlaceBenchAndPergola()
    {
        var group = new GameObject("[GardenDressing]");

        // Raw bounds (1.00, 0.71, 0.47) — its longest extent sits on X, not Y,
        // so the mesh needs a +90 Z correction to stand upright before any
        // facing rotation is applied on top.
        var bench = InstantiateScaled("garden-stone-bench", targetHeight: 0.9f, upAxisFix: new Vector3(0f, 0f, 90f));
        if (bench != null)
        {
            var benchPivot = new GameObject("garden-stone-bench_pivot");
            benchPivot.transform.SetParent(group.transform, false);
            bench.transform.SetParent(benchPivot.transform, false);

            // Near the fountain, facing it, on the mid terrace (matches the
            // fountain's Y=6 terrace per GiardinoDelleRoseSceneBuilder.MidY).
            // Facing is applied to the PIVOT, not the mesh, so it never
            // fights the mesh's own upAxisFix correction.
            PlaceAtGroundPoint(benchPivot, new Vector3(-6f, 6f, -4f));
            benchPivot.transform.rotation = Quaternion.LookRotation(new Vector3(6f, 0f, 4f).normalized, Vector3.up);
        }

        var pergola = InstantiateScaled("garden-wooden-pergola", targetHeight: 2.6f);
        if (pergola != null)
        {
            pergola.transform.SetParent(group.transform, false);
            // At the gate entrance (lower terrace, Y=0), player walks under it.
            PlaceAtGroundPoint(pergola, new Vector3(0f, 0f, -22f));
        }

        var stall = InstantiateScaled("florist-market-stall", targetHeight: 2.0f);
        if (stall != null)
        {
            stall.transform.SetParent(group.transform, false);
            // Beside the (currently inactive) Florist NPC on the upper terrace.
            PlaceAtGroundPoint(stall, new Vector3(7f, 12f, 21f));
        }
    }

    // ── Shared placement helpers ──────────────────────────────────────────────

    // Loads the named GLB prefab from PropsFolder, instantiates it, and scales
    // it so its RENDERER bounds (not the raw mesh pivot/scale) match
    // targetHeight in world units. Generated models vary wildly in raw scale,
    // so measuring bounds after instantiation is the only reliable approach.
    //
    // upAxisFix corrects models whose generated up-axis doesn't land on
    // Unity's Y — found empirically: garden-stone-bench's raw bounds measure
    // (1.00, 0.71, 0.47), i.e. its longest/tallest extent sits on X, not Y, so
    // scaling by bounds.y alone shrinks it into a squashed, sideways-reading
    // bench. Applied BEFORE the bounds measurement/scale so "targetHeight"
    // means what it says.
    static GameObject InstantiateScaled(string assetId, float targetHeight, Vector3? upAxisFix = null)
    {
        string path = $"{PropsFolder}/{assetId}.glb";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogWarning($"[GiardinoDelleRosePropPlacement] Missing {path} — skipped.");
            return null;
        }

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.name = assetId;
        if (upAxisFix.HasValue) go.transform.rotation = Quaternion.Euler(upAxisFix.Value);

        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            Debug.LogWarning($"[GiardinoDelleRosePropPlacement] '{assetId}' has no renderers — cannot measure bounds.");
            return go;
        }

        var bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);

        float currentHeight = bounds.size.y;
        float scale = currentHeight > 0.0001f ? targetHeight / currentHeight : 1f;
        // Scale in LOCAL space so the upAxisFix rotation (already applied to
        // transform.rotation) isn't fought by a world-space scale multiply.
        go.transform.localScale = Vector3.one * scale;

        EnsureCollider(go);
        return go;
    }

    // Positions `go` so the BOTTOM of its (rescaled) renderer bounds sits at
    // groundPoint — generated models are rarely pivoted at their base, so a
    // naive transform.position = groundPoint would float or sink the object.
    static void PlaceAtGroundPoint(GameObject go, Vector3 groundPoint)
    {
        go.transform.position = groundPoint;

        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        var bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);

        float bottomOffset = bounds.min.y - go.transform.position.y;
        go.transform.position -= new Vector3(0f, bottomOffset, 0f);
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
