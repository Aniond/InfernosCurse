using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Reusable player-side world interaction. E/gamepad south activates the
/// nearest eligible target; a mouse click activates the first visible target
/// hit by the camera ray. Every input path enforces the same proximity limit.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public sealed class PlayerWorldInteractor : MonoBehaviour
{
    [SerializeField, Min(0.25f)] float interactionRange = 2.25f;
    [SerializeField] Camera interactionCamera;

    WorldInteractable _nearest;
    GUIStyle _promptStyle;

    public float InteractionRange => interactionRange;
    public WorldInteractable CurrentTarget => _nearest;

    void Update()
    {
        RefreshNearest();

        if (IsInteractionBlocked()) return;
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        TryInteractWithMouse(Mouse.current.position.ReadValue());
    }

    public void OnInteract(InputValue value)
    {
        if (!value.isPressed || IsInteractionBlocked()) return;
        RefreshNearest();
        _nearest?.Interact(gameObject);
    }

#if UNITY_EDITOR
    public bool DebugInteractNearest()
    {
        RefreshNearest();
        if (_nearest == null) return false;
        _nearest.Interact(gameObject);
        return true;
    }

    public bool DebugInteractAtScreenPoint(Vector2 screenPosition) =>
        TryInteractWithMouse(screenPosition);
#endif

    void RefreshNearest()
    {
        _nearest = null;
        float bestDistanceSq = interactionRange * interactionRange;

        foreach (var candidate in WorldInteractable.Active)
        {
            if (candidate == null || !candidate.CanInteract(gameObject)) continue;

            float distanceSq = (candidate.InteractionPosition - transform.position).sqrMagnitude;
            if (distanceSq > bestDistanceSq) continue;

            bestDistanceSq = distanceSq;
            _nearest = candidate;
        }
    }

    bool TryInteractWithMouse(Vector2 screenPosition)
    {
        var cam = interactionCamera != null ? interactionCamera : Camera.main;
        if (cam == null) return false;

        Ray ray = cam.ScreenPointToRay(screenPosition);
        var hits = Physics.RaycastAll(ray, cam.farClipPlane, ~0, QueryTriggerInteraction.Ignore);
        bool found = false;
        RaycastHit hit = default;
        float nearestDistance = float.PositiveInfinity;

        foreach (var candidate in hits)
        {
            if (candidate.collider.transform.root == transform.root) continue;
            if (candidate.distance >= nearestDistance) continue;
            hit = candidate;
            nearestDistance = candidate.distance;
            found = true;
        }

        if (!found) return false;

        var target = hit.collider.GetComponentInParent<WorldInteractable>();
        if (target == null || !target.CanInteract(gameObject) || !IsInRange(target)) return false;

        target.Interact(gameObject);
        return true;
    }

    bool IsInRange(WorldInteractable target) =>
        (target.InteractionPosition - transform.position).sqrMagnitude <= interactionRange * interactionRange;

    static bool IsInteractionBlocked() =>
        Time.timeScale <= 0f || RestMenuUI.IsOpen || GugolMapUI.IsOpen;

    void OnGUI()
    {
        if (_nearest == null || IsInteractionBlocked()) return;

        _promptStyle ??= new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            padding = new RectOffset(18, 18, 10, 10)
        };

        string text = $"E / A / Click — {_nearest.Prompt}";
        var size = _promptStyle.CalcSize(new GUIContent(text));
        var rect = new Rect((Screen.width - size.x) * 0.5f, Screen.height - size.y - 36f, size.x, size.y);
        GUI.Box(rect, text, _promptStyle);
    }
}
