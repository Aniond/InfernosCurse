using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ZoneBattleAuthoring : MonoBehaviour
{
    [Header("Authorization")]
    [Tooltip("Safe by default. Ordinary encounters cannot start unless this is explicitly enabled.")]
    public bool combatAllowed;

    [Header("Required hybrid-zone references")]
    public BattleMapAuthoring mapAuthoring;
    public BattleTerrainHeights terrainHeights;
    public ZoneEncounterTrigger encounterTrigger;
    public GameObject battleKitPrefab;

    [Header("Presentation")]
    public Terrain zoneTerrain;
    public HybridZoneTerrainProfile terrainProfile;
    [Min(0f)] public float cameraBoundaryPadding = 2f;

    [Header("Mode ownership")]
    public GameObject[] explorationOnlyRoots = System.Array.Empty<GameObject>();
    public GameObject[] battleOnlyRoots = System.Array.Empty<GameObject>();
    public ZoneExit[] zoneExits = System.Array.Empty<ZoneExit>();

    void Reset() => ResolveLocalReferences();

    void Awake() => ResolveLocalReferences();

    public void ResolveLocalReferences()
    {
        if (mapAuthoring == null) mapAuthoring = GetComponent<BattleMapAuthoring>();
        if (terrainHeights == null) terrainHeights = GetComponent<BattleTerrainHeights>();
        if (encounterTrigger == null) encounterTrigger = GetComponent<ZoneEncounterTrigger>();
        if (battleKitPrefab == null && encounterTrigger != null)
            battleKitPrefab = encounterTrigger.battleKitPrefab;
        if (zoneTerrain == null) zoneTerrain = Object.FindFirstObjectByType<Terrain>();
        if (zoneExits == null || zoneExits.Length == 0)
            zoneExits = Object.FindObjectsByType<ZoneExit>(FindObjectsSortMode.None);
    }

    public bool TryValidate(out string message)
    {
        ResolveLocalReferences();
        var findings = new List<string>();
        if (!combatAllowed) findings.Add("combat is not authorized");
        if (mapAuthoring == null) findings.Add("BattleMapAuthoring is missing");
        if (terrainHeights == null) findings.Add("BattleTerrainHeights is missing");
        if (encounterTrigger == null) findings.Add("ZoneEncounterTrigger is missing");
        if (battleKitPrefab == null) findings.Add("BattleKit prefab is missing");
        if (mapAuthoring != null && (mapAuthoring.width <= 0 || mapAuthoring.height <= 0))
            findings.Add("grid dimensions are invalid");
        if (mapAuthoring != null && (mapAuthoring.partySpawns == null || mapAuthoring.partySpawns.Count == 0))
            findings.Add("party spawn suggestions are missing");
        if (zoneTerrain != null)
        {
            if (terrainProfile == null) findings.Add("hybrid terrain profile is missing");
            var material = zoneTerrain.materialTemplate;
            if (material == null || material.shader == null ||
                material.shader.name != "InfernosCurse/HybridZoneTerrain")
                findings.Add("terrain is not using InfernosCurse/HybridZoneTerrain");
        }

        message = findings.Count == 0 ? string.Empty : string.Join("; ", findings);
        return findings.Count == 0;
    }
}
