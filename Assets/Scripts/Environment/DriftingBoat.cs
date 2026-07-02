using UnityEngine;

// Drifts a boat prop slowly downstream with a gentle bob and sway.
// Purely cosmetic set dressing — wraps back to the east end after it
// floats past the west edge of the water plane.
public class DriftingBoat : MonoBehaviour
{
    [Tooltip("World units per second along -X (the Arno flows east to west).")]
    public float driftSpeed = 0.4f;
    public float wrapMinX = -68f;
    public float wrapMaxX = 68f;

    [Tooltip("Vertical bob on the water surface.")]
    public float bobAmplitude = 0.04f;
    public float bobFrequency = 0.4f;
    public float swayDegrees = 1.5f;

    private float _baseY;
    private float _yaw;
    private float _phase;

    void Start()
    {
        _baseY = transform.position.y;
        _yaw = transform.rotation.eulerAngles.y;
        // desync boats sharing the same settings
        _phase = (GetInstanceID() % 628) * 0.01f;
    }

    void Update()
    {
        Vector3 p = transform.position;
        p.x -= driftSpeed * Time.deltaTime;
        if (p.x < wrapMinX) p.x = wrapMaxX;

        float t = Time.time * bobFrequency * 2f * Mathf.PI + _phase;
        p.y = _baseY + Mathf.Sin(t) * bobAmplitude;
        transform.position = p;

        transform.rotation = Quaternion.Euler(
            Mathf.Sin(t * 0.7f) * swayDegrees,
            _yaw,
            Mathf.Cos(t * 0.5f) * swayDegrees);
    }
}
