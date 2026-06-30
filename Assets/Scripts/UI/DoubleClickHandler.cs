using UnityEngine;
using UnityEngine.EventSystems;
using System;

public class DoubleClickHandler : MonoBehaviour, IPointerClickHandler
{
    public Action onDoubleClick;
    private float _lastClick = -1f;
    private const float Threshold = 0.35f;

    public void OnPointerClick(PointerEventData e)
    {
        if (Time.unscaledTime - _lastClick < Threshold)
        {
            onDoubleClick?.Invoke();
            _lastClick = -1f;
        }
        else
        {
            _lastClick = Time.unscaledTime;
        }
    }
}
