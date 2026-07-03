using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Verification re-shoot for the Duomo tile pass with neutral-warm studio lighting:
// fog off, flat warm ambient, one warm key light — so the texture palette and the
// floor seams are actually inspectable (edit-mode default render drowns the interior
// in blue skybox ambient + weather fog). Screenshot-only: the scene is NOT saved.
public static class DuomoTileShots
{
    const string ScenePath = "Assets/Scenes/Duomo.unity";
    const string ShotDir   = "Tools/asset-gen/output/duomo-tiles/screens";

    [MenuItem("InfernosCurse/Duomo Tile Shots (no save)")]
    public static void Run()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // studio lighting for inspection only — never saved
        RenderSettings.fog = false;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.42f, 0.38f, 0.33f); // warm neutral
        var keyGo = new GameObject("[ShotKeyLight]");
        var key = keyGo.AddComponent<Light>();
        key.type = LightType.Directional;
        key.color = new Color(1.0f, 0.93f, 0.82f); // warm daylight through windows
        key.intensity = 1.1f;
        keyGo.transform.rotation = Quaternion.Euler(55f, -30f, 0f);

        var shots = new (string name, Vector3 pos, Vector3 look)[]
        {
            // straight down the nave/crossing butt-joint at z=-12.15
            ("5_seam_nave_crossing", new Vector3(-3.5f, 3.2f, -16f), new Vector3(3f, 0f, -9f)),
            // crossing floor -> throat -> north tribune floor transition
            ("6_seam_crossing_tribN", new Vector3(-2.5f, 3.2f, 8f),  new Vector3(2f, 0f, 17f)),
            // player-height look across the octagon at the diagonal walls
            ("7_octagon_walls",      new Vector3(-4f, 2f, -4f),      new Vector3(8.6f, 4f, 8.6f)),
            // down the nave toward the facade (long floor run + side walls)
            ("8_nave_length",        new Vector3(0f, 6f, -13f),      new Vector3(0f, 0.5f, -30f)),
            // overhead plan
            ("9_overhead",           new Vector3(0f, 55f, -8f),      new Vector3(0f, 0f, -7.9f)),
        };

        Directory.CreateDirectory(ShotDir);
        var camGo = new GameObject("[ShotCam]");
        var cam = camGo.AddComponent<Camera>();
        camGo.AddComponent<UniversalAdditionalCameraData>();
        cam.fieldOfView = 60f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 400f;

        var rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
        var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        foreach (var (name, pos, look) in shots)
        {
            camGo.transform.position = pos;
            camGo.transform.rotation = Quaternion.LookRotation(look - pos, Vector3.up);
            try
            {
                RenderPipeline.SubmitRenderRequest(cam, new UniversalRenderPipeline.SingleCameraRequest { destination = rt });
            }
            catch
            {
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = null;
            }
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(ShotDir, name + ".png"), tex.EncodeToPNG());
            Debug.Log($"[DuomoTileShots] shot {name}");
        }
        // no SaveScene — lighting tweaks and temp objects are discarded with the process
    }
}
