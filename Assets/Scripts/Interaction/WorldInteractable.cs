using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Base class for world objects that can be deliberately activated by the player.
/// Active instances register themselves so the player interactor can find the
/// nearest eligible target without scanning the whole scene every frame.
/// </summary>
public abstract class WorldInteractable : MonoBehaviour
{
    static readonly List<WorldInteractable> ActiveItems = new();

    public static IReadOnlyList<WorldInteractable> Active => ActiveItems;

    [SerializeField] Transform interactionPoint;

    public Vector3 InteractionPosition => interactionPoint != null
        ? interactionPoint.position
        : transform.position;

    public abstract string Prompt { get; }

    public virtual bool CanInteract(GameObject interactor) =>
        interactor != null && isActiveAndEnabled;

    public abstract void Interact(GameObject interactor);

    protected virtual void OnEnable()
    {
        if (!ActiveItems.Contains(this)) ActiveItems.Add(this);
    }

    protected virtual void OnDisable() => ActiveItems.Remove(this);
}
