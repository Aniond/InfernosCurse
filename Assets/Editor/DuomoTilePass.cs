using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Duomo tile pass: applies the approved marble floor + inlay wall textures to the
// Santa Maria del Fiore blockout (Duomo.unity).
//
// Floors get PER-PIECE materials with world-aligned UVs (scale/offset computed from
// each piece's world AABB) so the pattern flows continuously across the butt-joined
// nave/crossing/tribune floors — no seams at transitions.
// Walls get shared materials binned by repeat count (integer horizontal repeats per
// wall length) so the inlay never stretches, including on the octagon diagonals.
//
// Run in batch:  Unity -batchmode -executeMethod DuomoTilePass.Run -quit
// Or menu: InfernosCurse → Duomo Tile Pass.
public static class DuomoTilePass
{
    const string ScenePath   = "Assets/Scenes/Duomo.unity";
    const string TexFloor    = "Assets/Data/Materials/Duomo/Textures/Duomo_FloorMarble.png";
    const string TexWall     = "Assets/Data/Materials/Duomo/Textures/Duomo_WallInlay.png";
    const string BaseFloor   = "Assets/Data/Materials/Duomo/Duomo_Floor.mat";
    const string BaseWall    = "Assets/Data/Materials/Duomo/Duomo_Wall.mat";
    const string GenDir      = "Assets/Data/Materials/Duomo/Generated";
    const string ShotDir     = "Tools/asset-gen/output/duomo-tiles/screens";

    const float FloorTile = 3.0f;   // world units per floor pattern repeat
    const float WallTile  = 2.75f;  // world units per wall pattern repeat (11-high walls = 4 rows)

    static readonly string[] FloorPieces = {
        "Floor_Octagon", "Floor_Nave",
        "Floor_Throat_N", "Floor_Throat_E", "Floor_Throat_W",
        "Floor_Tribune_N", "Floor_Tribune_E", "Floor_Tribune_W",
    };

    static readonly string[] WallPrefixes = {
        "Oct_", "Nave_Wall_", "Nave_Stub_", "Facade_", "Throat_", "Trib_",
    };

    [MenuItem("InfernosCurse/Duomo Tile Pass")]
    public static void Run()
    {
        var texFloor = Import(TexFloor);
        var texWall  = Import(TexWall);
        var baseFloor = AssetDatabase.LoadAssetAtPath<Material>(BaseFloor);
        var baseWall  = AssetDatabase.LoadAssetAtPath<Material>(BaseWall);
        if (texFloor == null || texWall == null || baseFloor == null || baseWall == null)
        { Debug.LogError("[DuomoTilePass] missing texture or base material — aborting."); return; }

        if (!AssetDatabase.IsValidFolder(GenDir))
            AssetDatabase.CreateFolder(Path.GetDirectoryName(GenDir).Replace('\\', '/'), Path.GetFileName(GenDir));

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // ── floors: world-aligned per-piece materials ─────────────────────────
        int floors = 0;
        foreach (var name in FloorPieces)
        {
            var go = GameObject.Find(name);
            if (go == null) { Debug.LogWarning($"[DuomoTilePass] floor piece not found: {name}"); continue; }
            var r = go.GetComponent<Renderer>();
            var b = r.bounds; // world AABB — floors are axis-aligned cubes
            var mat = new Material(baseFloor) { name = $"Gen_Floor_{name}" };
            mat.SetTexture("_BaseMap", texFloor);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0.30f); // polished marble sheen for the candlelight
            // cube top face: u along +x, v along +z. World-aligned => neighbouring
            // pieces continue the same pattern across their shared edge.
            mat.SetTextureScale("_BaseMap", new Vector2(b.size.x / FloorTile, b.size.z / FloorTile));
            mat.SetTextureOffset("_BaseMap", new Vector2(
                Mathf.Repeat(b.min.x / FloorTile, 1f), Mathf.Repeat(b.min.z / FloorTile, 1f)));
            AssetDatabase.CreateAsset(mat, $"{GenDir}/Gen_Floor_{name}.mat");
            r.sharedMaterial = mat;
            floors++;
        }

        // ── walls: integer repeats binned by (repX, repY) ─────────────────────
        var wallMats = new Dictionary<(int, int), Material>();
        int walls = 0;
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            var n = r.gameObject.name;
            bool isWall = false;
            foreach (var p in WallPrefixes)
                if (n.StartsWith(p) && !n.Contains("Pier")) { isWall = true; break; }
            if (!isWall) continue;

            // Wall cubes carry their length in EITHER x or z depending on build
            // orientation (the other horizontal axis is the 0.6 thickness) — and the
            // big visible faces' U axis always runs along the long dimension.
            var s = r.transform.localScale;
            float len = Mathf.Max(s.x, s.z);
            int repX = Mathf.Max(1, Mathf.RoundToInt(len / WallTile));
            int repY = Mathf.Max(1, Mathf.RoundToInt(s.y / WallTile));
            if (!wallMats.TryGetValue((repX, repY), out var mat))
            {
                mat = new Material(baseWall) { name = $"Gen_Wall_{repX}x{repY}" };
                mat.SetTexture("_BaseMap", texWall);
                mat.SetColor("_BaseColor", Color.white);
                mat.SetFloat("_Smoothness", 0.12f);
                mat.SetTextureScale("_BaseMap", new Vector2(repX, repY));
                AssetDatabase.CreateAsset(mat, $"{GenDir}/Gen_Wall_{repX}x{repY}.mat");
                wallMats[(repX, repY)] = mat;
            }
            r.sharedMaterial = mat;
            walls++;
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[DuomoTilePass] floors={floors} walls={walls} wallMats={wallMats.Count} — scene saved.");

        Screenshots();
    }

    // Verification captures at the exact transitions: nave→crossing, crossing→tribune,
    // octagon diagonals, plus an overhead.
    static void Screenshots()
    {
        Directory.CreateDirectory(ShotDir);
        var shots = new (string name, Vector3 pos, Vector3 look)[]
        {
            ("1_nave_to_crossing",   new Vector3(0, 7, -26f),  new Vector3(0, 0.5f, -6f)),
            ("2_crossing_to_tribN",  new Vector3(0, 7, 1f),    new Vector3(0, 1f, 19f)),
            ("3_octagon_diagonals",  new Vector3(-6, 4, -6f),  new Vector3(8.6f, 4f, 8.6f)),
            ("4_overhead",           new Vector3(0, 55, -8f),  new Vector3(0, 0, -7.9f)),
        };

        var go = new GameObject("[ShotCam]");
        var cam = go.AddComponent<Camera>();
        go.AddComponent<UniversalAdditionalCameraData>();
        cam.fieldOfView = 60f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 400f;

        var rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
        var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        foreach (var (name, pos, look) in shots)
        {
            go.transform.position = pos;
            go.transform.rotation = Quaternion.LookRotation(look - pos, Vector3.up);
            try
            {
                RenderPipeline.SubmitRenderRequest(cam, new UniversalRenderPipeline.SingleCameraRequest { destination = rt });
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[DuomoTilePass] SingleCameraRequest failed ({e.Message}); falling back to Camera.Render");
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = null;
            }
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(ShotDir, name + ".png"), tex.EncodeToPNG());
            Debug.Log($"[DuomoTilePass] shot {name}");
        }
        Object.DestroyImmediate(go);
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
    }

    static Texture2D Import(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        if (AssetImporter.GetAtPath(path) is TextureImporter imp &&
            (imp.wrapMode != TextureWrapMode.Repeat || imp.maxTextureSize < 2048))
        {
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.maxTextureSize = 2048;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}
