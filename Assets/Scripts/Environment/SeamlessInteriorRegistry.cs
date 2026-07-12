using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime index for accessible interiors that live inside an owning zone.
/// Stable IDs let save/load and world-state systems address an interior without
/// turning it into a separate Unity scene.
/// </summary>
public static class SeamlessInteriorRegistry
{
    static readonly Dictionary<string, SeamlessInteriorModule> Modules =
        new(StringComparer.Ordinal);

    public static string ActiveSubLocationId { get; private set; } = string.Empty;
    public static SeamlessInteriorModule ActiveModule { get; private set; }

    public static event Action<SeamlessInteriorModule> ActiveModuleChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetRuntimeState()
    {
        Modules.Clear();
        ActiveSubLocationId = string.Empty;
        ActiveModule = null;
        ActiveModuleChanged = null;
    }

    public static bool Register(SeamlessInteriorModule module, out string error)
    {
        error = string.Empty;
        if (module == null)
        {
            error = "module is null";
            return false;
        }

        string id = module.SubLocationId;
        if (string.IsNullOrWhiteSpace(id))
        {
            error = $"{module.name} has no sub-location ID";
            return false;
        }

        if (Modules.TryGetValue(id, out SeamlessInteriorModule existing) &&
            existing != null && existing != module)
        {
            error = $"duplicate sub-location ID '{id}' on {existing.name} and {module.name}";
            return false;
        }

        Modules[id] = module;
        return true;
    }

    public static void Unregister(SeamlessInteriorModule module)
    {
        if (module == null) return;
        string id = module.SubLocationId;
        if (!string.IsNullOrEmpty(id) && Modules.TryGetValue(id, out var existing) && existing == module)
            Modules.Remove(id);

        if (ActiveModule == module)
            SetActive(null);
    }

    public static bool TryGet(string subLocationId, out SeamlessInteriorModule module)
    {
        module = null;
        if (string.IsNullOrWhiteSpace(subLocationId)) return false;
        if (!Modules.TryGetValue(subLocationId, out module) || module == null)
        {
            Modules.Remove(subLocationId);
            module = null;
            return false;
        }
        return true;
    }

    public static void SetActive(SeamlessInteriorModule module)
    {
        if (ActiveModule == module) return;
        ActiveModule = module;
        ActiveSubLocationId = module != null ? module.SubLocationId : string.Empty;
        ActiveModuleChanged?.Invoke(module);
    }

    public static bool TryRestore(
        string subLocationId,
        Transform player,
        out Vector3 recoveryPosition,
        out string error)
    {
        recoveryPosition = player != null ? player.position : Vector3.zero;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(subLocationId))
        {
            ActiveModule?.SetPlayerInside(false);
            SetActive(null);
            return true;
        }

        if (!TryGet(subLocationId, out SeamlessInteriorModule module))
        {
            error = $"sub-location '{subLocationId}' is not registered";
            return false;
        }

        if (!module.TryValidateRuntime(out error))
        {
            recoveryPosition = module.ExteriorFallbackPosition;
            return false;
        }

        module.SetPlayerInside(true);
        recoveryPosition = module.InteriorFallbackPosition;
        return true;
    }
}
