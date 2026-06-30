using UnityEngine;

// Keeps the sprite facing the camera so it reads as upright regardless of camera tilt.
// Matches the Octopath Traveler HD-2D look: rotate on X only so the sprite stays vertical.
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteBillboard : MonoBehaviour
{
    private Transform _cam;

    private void Awake()  => CacheCamera();
    private void OnEnable() { if (_cam == null) CacheCamera(); }

    void CacheCamera()
    {
        var c = Camera.main;
        _cam = c != null ? c.transform : null;
    }

    private void LateUpdate()
    {
        // Re-resolve if the cached camera was destroyed (scene change).
        if (_cam == null) { CacheCamera(); if (_cam == null) return; }

        // Match camera's X rotation only — sprite stays vertically upright
        // and tilts toward camera like Octopath sprites do
        float camX = _cam.eulerAngles.x;
        transform.rotation = Quaternion.Euler(camX, transform.rotation.eulerAngles.y, 0f);
    }
}
