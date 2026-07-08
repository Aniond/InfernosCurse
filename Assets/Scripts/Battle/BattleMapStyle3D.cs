using UnityEngine;

// Per-map look for 3D diorama battle maps — pure data, so new maps are
// plug-and-play: duplicate a BattleMap_* prefab, tweak plateaus/obstacles,
// run "Build 3D Diorama From Selected"; a default style asset is created
// next to the map and every knob lives in the inspector, no shader or
// builder code involved. Read by BattleMapMeshBuilder (terrain + material)
// and the arena wiring menu (lighting).
[CreateAssetMenu(menuName = "InfernosCurse/Battle Map 3D Style")]
public class BattleMapStyle3D : ScriptableObject
{
    [Header("Terrain layers (world-projected)")]
    public Texture2D grassTex;
    public Texture2D dirtTex;
    public Texture2D rockTex;
    public Color grassTint = new Color(0.48f, 0.84f, 0.38f);
    public Color dirtTint = new Color(0.62f, 0.50f, 0.40f);
    public Color rockTint = new Color(0.62f, 0.61f, 0.58f);
    [Tooltip("Texture tiles per world meter")]
    public float tiling = 0.35f;
    [Header("Path layer (painted via BattleMapAuthoring.pathCells)")]
    public Texture2D pathTex;
    public Color pathTint = new Color(0.82f, 0.76f, 0.66f);

    [Header("Water (optional — Arno/river maps)")]
    [Tooltip("World Y of the water plane; -100 = no water")]
    public float waterLevel = -100f;
    public Material waterMaterial;

    [Header("Terrain shape")]
    [Tooltip("World Y per elevation unit (FFT-chunky = 0.5)")]
    public float heightStep = 0.5f;
    [Tooltip("Cells of hill falloff past the playfield")]
    public int skirtCells = 4;
    [Tooltip("Diorama cut depth (dark base)")]
    public float baseY = -3.2f;

    [Header("Arena lighting")]
    public Color sunColor = new Color(1f, 0.98f, 0.92f);
    public float sunIntensity = 1.35f;
    public Vector3 sunEuler = new Vector3(48f, -35f, 0f);
    public Color ambientSky = new Color(0.42f, 0.50f, 0.62f);
    public Color ambientEquator = new Color(0.34f, 0.35f, 0.33f);
    public Color ambientGround = new Color(0.16f, 0.15f, 0.12f);
}
