using UnityEngine;

// ScriptableObject — one asset per named curse (e.g. "Plague of Shadows", "Blight of Dis")
[CreateAssetMenu(menuName = "InfernosCurse/Curse Definition")]
public class CurseDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Circle represented by this definition. APPEND ONLY in CircleId.")]
    public CircleId circleId = CircleId.Limbo;
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

    // Retained so existing ScriptableObjects deserialize without data loss.
    // Universal time growth, passive sanctuary relief, and flood growth are
    // deliberately retired: only concrete sources/events may alter influence.
    [HideInInspector]
    [Range(0f, 0.2f)] public float dailyGrowthBase = 0.02f;

    [Header("Dated Sources")]
    [Tooltip("Daily influence from an active ritual site native to this Circle.")]
    [Range(0f, 0.2f)] public float dailyRitualBonus = 0.02f;
    [HideInInspector]
    [Range(0f, 0.2f)] public float dailySanctuaryRelief = 0.015f;
    [HideInInspector]
    [Range(0f, 0.3f)] public float dailyFloodSpike = 0.05f;

    [Header("Cross-Location Influence")]
    [Range(0f, 1f)] public float bleedThreshold = 0.70f;
    [Range(0f, 0.05f)] public float maxDailyBleed = 0.012f;
    [Range(0f, 0.1f)] public float targetDailyBleedCap = 0.015f;
}
