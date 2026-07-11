using System.Collections.Generic;
using UnityEngine;

// Deliberate bridge from a persistent exploration agent into an in-place
// tactical encounter. The exploration actor never doubles as a BattleUnit;
// combat receives fresh runtime clones and resolves the persistent record only
// after a real victory.
[DisallowMultipleComponent]
public sealed class WorldAgentEncounterActor : WorldInteractable
{
    public string agentId;
    public CombatantData leaderCombatant;
    public CombatantData[] frontlineCombatants = System.Array.Empty<CombatantData>();
    public bool requiresDiscovery = true;

    ZoneEncounterTrigger _zoneEncounter;

    public string AgentId => agentId;
    public override string Prompt => "Confront the Limbo Crier";

    public override bool CanInteract(GameObject interactor)
    {
        if (!base.CanInteract(interactor)) return false;
        if (!PersistentLimboWorldState.TryGetAgent(agentId, out PersistentWorldAgentRecord record)) return false;
        if (record.defeated || (requiresDiscovery && !record.discovered)) return false;
        return FindEncounter() != null && !FindEncounter().BattleRunning;
    }

    public override void Interact(GameObject interactor)
    {
        ZoneEncounterTrigger encounter = FindEncounter();
        if (encounter == null)
        {
            Debug.LogWarning("[WorldAgentEncounter] This zone has no authorized battle handoff.", this);
            return;
        }
        encounter.TryStartWorldAgentEncounter(this, interactor);
    }

    public List<CombatantData> BuildEnemyParty()
    {
        var party = new List<CombatantData>();
        if (leaderCombatant != null) party.Add(leaderCombatant);
        if (frontlineCombatants != null)
            foreach (CombatantData support in frontlineCombatants)
                if (support != null) party.Add(support);
        return party;
    }

    public void ResolveVictory()
    {
        if (!PersistentLimboWorldState.DefeatAgent(agentId, out string error))
            Debug.LogError("[WorldAgentEncounter] " + error, this);
        else
            Debug.Log($"[WorldAgentEncounter] '{agentId}' permanently defeated and removed from daily Limbo simulation.");
        Destroy(gameObject);
    }

    public void ResolveDefeat()
    {
        Debug.Log($"[WorldAgentEncounter] '{agentId}' remains active after the party's defeat.");
    }

    ZoneEncounterTrigger FindEncounter()
    {
        if (_zoneEncounter == null)
            _zoneEncounter = Object.FindFirstObjectByType<ZoneEncounterTrigger>();
        return _zoneEncounter;
    }
}
