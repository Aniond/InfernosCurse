using UnityEngine;

// Keeps the sprite facing the camera so it reads as upright regardless of camera tilt.
// Matches the Octopath Traveler HD-2D look: rotate on X only so the sprite stays vertical.
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteBillboard : MonoBehaviour
{
    private void LateUpdate()
    {
        if (Camera.main == null) return;
        // Match camera's X rotation only — sprite stays vertically upright
        // and tilts toward camera like Octopath sprites do
        float camX = Camera.main.transform.eulerAngles.x;
        transform.rotation = Quaternion.Euler(camX, transform.rotation.eulerAngles.y, 0f);
    }
}
