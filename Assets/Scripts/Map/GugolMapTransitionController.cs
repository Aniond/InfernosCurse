using System;
using System.Collections;
using UnityEngine;

public sealed class GugolMapTransitionController : MonoBehaviour
{
    Coroutine _active;

    public bool IsTransitioning => _active != null;

    public void Move(
        RectTransform target,
        Vector3 scale,
        Vector2 anchoredPosition,
        float seconds,
        bool reducedMotion,
        Action completed = null)
    {
        Stop();
        if (target == null)
        {
            completed?.Invoke();
            return;
        }
        if (reducedMotion || seconds <= 0f || !isActiveAndEnabled)
        {
            target.localScale = scale;
            target.anchoredPosition = anchoredPosition;
            completed?.Invoke();
            return;
        }
        _active = StartCoroutine(MoveRoutine(target, scale, anchoredPosition, seconds, completed));
    }

    public void Stop()
    {
        if (_active == null) return;
        StopCoroutine(_active);
        _active = null;
    }

    void OnDisable() => Stop();

    IEnumerator MoveRoutine(
        RectTransform target,
        Vector3 targetScale,
        Vector2 targetPosition,
        float seconds,
        Action completed)
    {
        Vector3 startScale = target.localScale;
        Vector2 startPosition = target.anchoredPosition;
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / seconds));
            target.localScale = Vector3.LerpUnclamped(startScale, targetScale, t);
            target.anchoredPosition = Vector2.LerpUnclamped(startPosition, targetPosition, t);
            yield return null;
        }
        target.localScale = targetScale;
        target.anchoredPosition = targetPosition;
        _active = null;
        completed?.Invoke();
    }
}
