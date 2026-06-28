using UnityEngine;
using UnityEngine.UI;

// Bottom-screen gradient fade — Octopath Traveler HD-2D style.
// Hides the ground plane edge at the bottom of the camera view.
// Attach to the BottomFade Canvas GameObject.
[RequireComponent(typeof(Canvas))]
public class BottomFade : MonoBehaviour
{
    [Range(0.05f, 0.5f)] public float fadeHeightPercent = 0.20f;

    private void Awake()
    {
        var canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
        }

        var imageGO = new GameObject("BottomGradientImage");
        imageGO.transform.SetParent(transform, false);

        var img = imageGO.AddComponent<Image>();
        img.color = Color.white;
        img.raycastTarget = false;
        img.sprite = CreateGradientSprite();

        // Anchor full width, bottom fadeHeightPercent of screen
        var rt = imageGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, fadeHeightPercent);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static Sprite CreateGradientSprite()
    {
        int height = 64;
        var tex = new Texture2D(1, height, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < height; y++)
        {
            // y=0 = bottom = opaque black, y=top = transparent
            float t = (float)y / (height - 1);
            float alpha = (1f - t) * (1f - t); // quadratic — soft top edge
            tex.SetPixel(0, y, new Color(0f, 0f, 0f, alpha));
        }
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, 1, height), new Vector2(0.5f, 0.5f));
    }
}
