using UnityEngine;

// ScriptableObject — one asset per named curse (e.g. "Plague of Shadows", "Blight of Dis")
[CreateAssetMenu(menuName = "InfernosCurse/Curse Definition")]
public class CurseDefinition : ScriptableObject
{
    [Header("Identity")]
    public string curseName   = "Unnamed Curse";
    [TextArea] public string description;
    public Sprite icon;

    [Header("Propagation")]
    [Tooltip("Base spread rate per hub-map tick (0-1). Higher = faster city infection.")]
    [Range(0f, 1f)] public float hubSpreadRate   = 0.05f;

    [Tooltip("Base spread rate per battle automata step (0-1).")]
    [Range(0f, 1f)] public float battleSpreadRate = 0.08f;

    [Tooltip("How much sanctity resists spread. 1 = fully blocked at max sanctity.")]
    [Range(0f, 1f)] public float sanctityResistance = 0.7f;

    [Tooltip("Decay per tick when no source is nearby.")]
    [Range(0f, 1f)] public float decayRate = 0.01f;

    [Header("Thresholds")]
    [Tooltip("curseDensity at which this tile turns to a Cursed tile type in battle.")]
    [Range(0f, 1f)] public float tileCorruptThreshold = 0.6f;

    [Tooltip("globalCurseLevel at which this curse triggers a 'surge' — AI buffs, new rituals spawn.")]
    [Range(0f, 1f)] public float surgeThreshold = 0.75f;

    [Header("Battle Effects")]
    [Tooltip("Enemy stat multiplier when standing on a cursed tile.")]
    public float enemyBuffOnCursedTile = 1.15f;
    [Tooltip("Player stat penalty when standing on a cursed tile.")]
    public float playerDebuffOnCursedTile = 0.90f;

    [Header("Daily Drift")]
    // The corruption tide: every in-game day each district grows by base,
    // modified below. Gentle + linear by design — drama comes from thresholds.
    [Tooltip("Curse added to every node per in-game day.")]
    [Range(0f, 0.2f)] public float dailyGrowthBase = 0.02f;
    [Tooltip("Extra daily growth on ritual-site nodes (permanent sources).")]
    [Range(0f, 0.2f)] public float dailyRitualBonus = 0.02f;
    [Tooltip("Daily growth relief on sanctuary nodes.")]
    [Range(0f, 0.2f)] public float dailySanctuaryRelief = 0.015f;
    [Tooltip("Extra daily growth on river nodes when the Arno runs flood-high " +
             "(FlorenceWeather.FloodRiskToday).")]
    [Range(0f, 0.3f)] public float dailyFloodSpike = 0.05f;
}
