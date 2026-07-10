using UnityEngine;

public enum HybridZoneSurfaceFamily
{
    Natural,
    Urban,
}

public enum HybridZoneBlendSource
{
    TerrainControl,
    VertexColors,
}

[CreateAssetMenu(menuName = "Inferno's Curse/Environment/Hybrid Zone Terrain Profile",
    fileName = "HybridZoneTerrainProfile")]
public sealed class HybridZoneTerrainProfile : ScriptableObject
{
    public HybridZoneSurfaceFamily surfaceFamily = HybridZoneSurfaceFamily.Natural;

    [Header("Blend source")]
    public HybridZoneBlendSource blendSource = HybridZoneBlendSource.TerrainControl;
    [Range(0.25f, 4f)] public float vertexBlendContrast = 1f;

    [Header("Layer palette")]
    public Color layer0Tint = new(0.78f, 0.86f, 0.68f, 1f);
    public Color layer1Tint = new(0.83f, 0.76f, 0.62f, 1f);
    public Color layer2Tint = new(0.66f, 0.61f, 0.45f, 1f);
    public Color layer3Tint = Color.white;

    [Header("Painterly depth")]
    [Range(0.25f, 4f)] public float blendExponent = 1.15f;
    [Min(0.001f)] public float macroScale = 0.055f;
    [Range(0f, 0.35f)] public float macroStrength = 0.10f;
    public Color exposedTint = new(1.04f, 1.01f, 0.92f, 1f);
    public Color recessTint = new(0.72f, 0.82f, 0.86f, 1f);
    [Range(0f, 0.65f)] public float slopeStrength = 0.24f;
    [Range(0f, 0.35f)] public float elevationTintStrength = 0.08f;
    public Vector2 heightMinMax = new(1f, 2.2f);
    [Range(0f, 1f)] public float ambientBoost = 0.22f;

    [Header("Persistent weather")]
    [Range(0f, 0.65f)] public float wetDarkening = 0.28f;
    [Range(0f, 0.35f)] public float wetHighlight = 0.08f;
    public Color wetTint = new(0.45f, 0.42f, 0.34f, 1f);
    [Tooltip("Per-layer wet response. X/Y/Z/W correspond to terrain layers 0-3.")]
    public Vector4 layerWetResponse = new(0.45f, 1f, 0.8f, 1f);

    public void ApplyTo(Material material)
    {
        if (material == null) return;
        SetColor(material, "_LayerTint0", layer0Tint);
        SetColor(material, "_LayerTint1", layer1Tint);
        SetColor(material, "_LayerTint2", layer2Tint);
        SetColor(material, "_LayerTint3", layer3Tint);
        SetFloat(material, "_UrbanVertexBlend", blendSource == HybridZoneBlendSource.VertexColors ? 1f : 0f);
        SetFloat(material, "_VertexBlendContrast", vertexBlendContrast);
        SetFloat(material, "_BlendExponent", blendExponent);
        SetFloat(material, "_MacroScale", macroScale);
        SetFloat(material, "_MacroStrength", macroStrength);
        SetColor(material, "_ExposedTint", exposedTint);
        SetColor(material, "_RecessTint", recessTint);
        SetFloat(material, "_SlopeStrength", slopeStrength);
        SetFloat(material, "_ElevationTintStrength", elevationTintStrength);
        SetVector(material, "_HeightMinMax", new Vector4(heightMinMax.x, heightMinMax.y, 0f, 0f));
        SetFloat(material, "_AmbientBoost", ambientBoost);
        SetFloat(material, "_WetDarkening", wetDarkening);
        SetFloat(material, "_WetHighlight", wetHighlight);
        SetColor(material, "_WetColor", wetTint);
        SetVector(material, "_LayerWetResponse", layerWetResponse);
    }

    static void SetFloat(Material material, string property, float value)
    {
        if (material.HasProperty(property)) material.SetFloat(property, value);
    }

    static void SetColor(Material material, string property, Color value)
    {
        if (material.HasProperty(property)) material.SetColor(property, value);
    }

    static void SetVector(Material material, string property, Vector4 value)
    {
        if (material.HasProperty(property)) material.SetVector(property, value);
    }
}
