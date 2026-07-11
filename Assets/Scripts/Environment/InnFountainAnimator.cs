using System.Linq;
using UnityEngine;

/// <summary>
/// Adds restrained motion to the Florentine inn fountain's authored water
/// streams and ripple layers. The shared water shader owns surface flow;
/// this component supplies readable vertical fall and basin impact motion.
/// </summary>
public sealed class InnFountainAnimator : MonoBehaviour
{
    [SerializeField, Min(0.1f)] float pulseSpeed = 2.4f;
    [SerializeField, Range(0f, 0.25f)] float streamPulse = 0.08f;
    [SerializeField, Range(0f, 0.5f)] float rippleExpansion = 0.22f;
    [SerializeField, Min(0.1f)] float rippleSpeed = 0.38f;

    Transform[] _streams;
    Transform[] _ripples;
    Vector3[] _streamScales;
    Vector3[] _streamPositions;
    Vector3[] _rippleScales;

    void Awake()
    {
        _streams = GetComponentsInChildren<Transform>(true)
            .Where(t => t != transform && t.name.StartsWith("WaterStream_"))
            .ToArray();
        _ripples = GetComponentsInChildren<Transform>(true)
            .Where(t => t != transform && t.name.StartsWith("Ripple_"))
            .ToArray();
        _streamScales = _streams.Select(t => t.localScale).ToArray();
        _streamPositions = _streams.Select(t => t.localPosition).ToArray();
        _rippleScales = _ripples.Select(t => t.localScale).ToArray();
    }

    void Update()
    {
        float time = Time.time;
        for (int i = 0; i < _streams.Length; i++)
        {
            float wave = Mathf.Sin(time * pulseSpeed + i * 1.37f);
            Vector3 scale = _streamScales[i];
            scale.y *= 1f + wave * streamPulse;
            _streams[i].localScale = scale;
            _streams[i].localPosition = _streamPositions[i] + Vector3.down * (wave * 0.018f);
        }

        for (int i = 0; i < _ripples.Length; i++)
        {
            float phase = Mathf.Repeat(time * rippleSpeed + i * 0.5f, 1f);
            Vector3 scale = _rippleScales[i];
            float expansion = 1f + phase * rippleExpansion;
            scale.x *= expansion;
            scale.z *= expansion;
            _ripples[i].localScale = scale;
            _ripples[i].localPosition = new Vector3(
                _ripples[i].localPosition.x,
                0.505f + Mathf.Sin(phase * Mathf.PI) * 0.008f,
                _ripples[i].localPosition.z);
        }
    }
}
