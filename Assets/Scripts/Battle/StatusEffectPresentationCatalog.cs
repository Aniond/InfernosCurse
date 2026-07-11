using System;
using UnityEngine;

[Serializable]
public class StatusEffectPresentation
{
    public StatusEffectType type;
    public Sprite icon;
    [TextArea(1, 3)] public string tooltip;
}

[CreateAssetMenu(fileName = "StatusEffectPresentationCatalog", menuName = "InfernosCurse/Status Effect Presentation Catalog")]
public class StatusEffectPresentationCatalog : ScriptableObject
{
    public StatusEffectPresentation[] entries = Array.Empty<StatusEffectPresentation>();

    static StatusEffectPresentationCatalog _instance;
    public static StatusEffectPresentationCatalog Instance =>
        _instance != null
            ? _instance
            : (_instance = Resources.Load<StatusEffectPresentationCatalog>(
                "UI/StatusEffectPresentationCatalog"));

    public StatusEffectPresentation Find(StatusEffectType type)
    {
        if (entries == null) return null;
        foreach (StatusEffectPresentation entry in entries)
            if (entry != null && entry.type == type) return entry;
        return null;
    }

    public Sprite GetIcon(StatusEffectType type) => Find(type)?.icon;
    public string GetTooltip(StatusEffectType type) => Find(type)?.tooltip ?? type.ToString();
}
