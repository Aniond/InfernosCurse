#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using UnityEngine;

// Editor-only runtime walkthrough. It is compiled out of player builds, but it
// drives the same player interaction, encounter, combat, terrain, and victory
// code used by the game.
public sealed class LimboCrierPlayModeProbe : MonoBehaviour
{
    const string AgentId = "limbo_crier_mercato_01";

    IEnumerator Start()
    {
        IEnumerator walkthrough = Walkthrough();
        while (true)
        {
            bool hasNext;
            object current = null;
            try
            {
                hasNext = walkthrough.MoveNext();
                if (hasNext) current = walkthrough.Current;
            }
            catch (Exception exception)
            {
                Debug.LogError("[LimboCrierPlayModeVerifier] FAIL: " + exception.Message + "\n" + exception.StackTrace);
                UnityEditor.EditorApplication.Exit(1);
                yield break;
            }

            if (!hasNext) break;
            yield return current;
        }

        Debug.Log("[LimboCrierPlayModeVerifier] PASS: hidden discovery gate -> player interaction -> 1 Crier + 2 frontliners -> stain apply/refresh/restore -> persistent victory cleanup; spam did not duplicate the encounter.");
        UnityEditor.EditorApplication.Exit(0);
    }

    IEnumerator Walkthrough()
    {
        yield return null;
        yield return null;

        PersistentLimboWorldState.Reset();
        PersistentLimboWorldState.LoadResourceDefinitions();
        PersistentCrierMaterializer.MaterializeCurrentScene();
        yield return null;

        GameObject player = GameObject.FindWithTag("Player");
        Require(player != null, "Player-tagged exploration actor did not load.");
        var interactor = player.GetComponent<PlayerWorldInteractor>();
        Require(interactor != null, "Mercato player has no PlayerWorldInteractor.");

        var actor = FindFirstObjectByType<WorldAgentEncounterActor>();
        Require(actor != null, "Persistent Crier world actor did not materialize.");
        var visual = actor.GetComponentInChildren<LimboCrierWorldVisual>(true);
        Require(visual != null && visual.spriteRenderer != null, "Crier world visual is missing.");
        Require(!actor.CanInteract(player), "Undiscovered Crier accepted interaction.");
        Require(!visual.spriteRenderer.enabled && visual.worldCollider != null && !visual.worldCollider.enabled,
            "Undiscovered Crier was visible or blocking exploration.");

        Require(PersistentLimboWorldState.MarkAgentDiscovered(AgentId, out string discoverError),
            "Could not discover Crier: " + discoverError);
        yield return null;
        Require(visual.spriteRenderer.enabled && visual.worldCollider.enabled,
            "Discovered Crier did not become visible and interactable.");

        var body = player.GetComponent<Rigidbody>();
        Vector3 interactionPosition = actor.transform.position + Vector3.back * 1.25f;
        if (body != null)
        {
            body.position = interactionPosition;
            body.linearVelocity = Vector3.zero;
        }
        else player.transform.position = interactionPosition;
        Physics.SyncTransforms();
        yield return new WaitForFixedUpdate();

        Require(interactor.DebugInteractNearest(), "Player interaction path did not select the discovered Crier.");
        ZoneEncounterTrigger trigger = FindFirstObjectByType<ZoneEncounterTrigger>();
        Require(trigger != null && trigger.BattleRunning, "Crier interaction did not start an in-place battle.");
        BattleManager battle = trigger.ActiveBattleManager;
        Require(battle != null, "BattleManager was not handed back by the zone encounter.");
        Require(battle.Enemies.Count == 3, $"Expected 3 enemies, got {battle.Enemies.Count}.");
        Require(battle.Enemies.Count(unit => unit.Data.displayName == "Limbo Crier") == 1,
            "Battle composition does not contain exactly one Limbo Crier.");
        Require(battle.Enemies.Count(unit => unit.Data.displayName == "Cursebearer") == 2,
            "Battle composition does not contain two Cursebearer frontliners.");

        // Input-spam edge: repeated confrontation attempts while the first
        // handoff is active must not duplicate managers or enemies.
        for (int i = 0; i < 10; i++) actor.Interact(player);
        Require(FindObjectsByType<BattleManager>(FindObjectsSortMode.None).Length == 1,
            "Repeated interaction created multiple BattleManagers.");
        Require(battle.Enemies.Count == 3, "Repeated interaction duplicated the enemy formation.");

        BattleUnit crier = battle.Enemies.First(unit => unit.Data.displayName == "Limbo Crier");
        GridCell originalCell = battle.Grid.GetCell(crier.gridPosition);
        TileType originalType = originalCell.tileType;
        Require(TemporaryTerrainService.TryApplyLimboStain(
                battle.Grid, crier.gridPosition, crier, 3, out TemporaryTerrainFailure terrainFailure),
            "Could not apply live Limbo Stain: " + terrainFailure);
        var presenter = battle.GetComponent<TemporaryTerrainPresenter>();
        Require(presenter != null && presenter.VisualCount == 1,
            "Live Limbo Stain did not create exactly one battlefield visual.");
        Require(TemporaryTerrainService.TryApplyLimboStain(
                battle.Grid, crier.gridPosition, crier, 3, out terrainFailure) && presenter.VisualCount == 1,
            "Refreshing Limbo Stain duplicated its battlefield visual.");
        TemporaryTerrainService.RestoreAll(battle.Grid);
        Require(presenter.VisualCount == 0 && originalCell.tileType == originalType,
            "Limbo Stain did not restore its authored cell and visual.");

        Require(PersistentLimboWorldState.TryGetAgent(AgentId, out var beforeVictory) && !beforeVictory.defeated,
            "Crier was marked defeated before combat victory.");
        foreach (BattleUnit enemy in battle.Enemies.ToArray())
            enemy.TakeDamage(99999, DamageType.Holy, battle.Players.FirstOrDefault());
        yield return null;
        yield return null;

        Require(PersistentLimboWorldState.TryGetAgent(AgentId, out var afterVictory) && afterVictory.defeated &&
                afterVictory.activityState == CrierActivityState.Defeated,
            "Victory did not permanently defeat the persistent Crier record.");
        Require(!trigger.BattleRunning, "Exploration was not restored after victory.");
        Require(FindObjectsByType<WorldAgentEncounterActor>(FindObjectsInactive.Include).Length == 0,
            "Defeated Crier world actor remained materialized.");
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
