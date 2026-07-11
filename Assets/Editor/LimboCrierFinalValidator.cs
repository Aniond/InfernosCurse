using UnityEditor;
using UnityEngine;

public static class LimboCrierFinalValidator
{
    [MenuItem("InfernosCurse/Validation/Validate Complete Limbo Crier Stack")]
    public static void Validate()
    {
        CampaignChronicleValidator.Validate();
        LimboWorldSimulationValidator.Validate();
        GeminiNarrativeValidator.Validate();
        FlorenceLimboWorldAuthoringValidator.Validate();
        LimboCrierCombatValidator.Validate();
        LimboCrierCombatValidator.ValidateProductionVisualAssets();
        LimboCrierEncounterValidator.Validate();
        Debug.Log("[LimboCrierFinalValidator] Complete Chronicle, world simulation, Gemini boundary, authoring, combat, visuals, encounter, and terrain stack passed.");
    }
}
