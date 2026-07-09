using UnityEngine;

/// <summary>Opens the existing inn rest service from a reception counter.</summary>
public sealed class InnCounterInteraction : WorldInteractable
{
    public string innName = "Albergo Fiorentino";
    public int innPrice = 10;
    public bool isGuildInn = true;
    public string prompt = "Speak to Innkeeper";

    public override string Prompt => prompt;

    public override void Interact(GameObject interactor)
    {
        var menu = RestMenuUI.Instance;
        if (menu == null)
        {
            Debug.LogWarning("[InnCounterInteraction] RestMenuUI is unavailable.", this);
            return;
        }

        menu.OpenInn(innName, innPrice, isGuildInn);
    }
}
