using UnityEngine;

// Keeps the sprite facing the camera so it reads as upright regardless of camera tilt.
// Matches the Octopath Traveler HD-2D look: rotate on X only so the sprite stays vertical.
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteBillboard : MonoBehaviour
{
    [Tooltip("Fraction of the camera's X pitch the sprite tilts to. 1 = full " +
             "match (bottom edge leans hard toward camera and can clip at the " +
             "screen edge); 0.75 keeps the facing while reducing that clip.")]
    [Range(0f, 1f)] public float tiltFactor = 0.75f;

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

        // Match a fraction of the camera's X pitch — sprite stays upright and
        // tilts toward the camera like Octopath sprites, but the reduced tilt
        // stops the bottom edge from clipping when the player is low in frame.
        float camX = _cam.eulerAngles.x * tiltFactor;
        // BUG FIX: force Y and Z to 0. Preserving transform.rotation.eulerAngles.y
        // could carry a Y-flip that spins the sprite to show its back when the
        // rigidbody or physics nudges rotation. Billboard must ONLY tilt on X.
        transform.rotation = Quaternion.Euler(camX, 0f, 0f);
    }
}
