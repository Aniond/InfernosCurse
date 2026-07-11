using System.Collections.Generic;
using UnityEngine;

// Runtime-only presentation for temporary tactical terrain. The service owns
// authoritative cell state; this component mirrors its lifecycle without
// modifying the authored map or leaving scene objects behind after battle.
[DisallowMultipleComponent]
public sealed class TemporaryTerrainPresenter : MonoBehaviour
{
    const string LimboStainMaterialResource = "VFX/Limbo/LimboStain";
    const string LimboStainSpriteResource = "VFX/Limbo/limbo-stain";

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");

    sealed class TerrainVisual
    {
        public GameObject root;
        public Renderer renderer;
        public Vector3 baseScale;
        public float phase;
    }

    readonly Dictionary<Vector2Int, TerrainVisual> _visuals = new();
    MaterialPropertyBlock _properties;
    BattleGrid _grid;
    bool _subscribed;

    public int VisualCount => _visuals.Count;

    void OnEnable()
    {
        _properties ??= new MaterialPropertyBlock();
        Subscribe();
    }

    void Subscribe()
    {
        if (_subscribed) return;
        TemporaryTerrainService.TerrainApplied += OnTerrainApplied;
        TemporaryTerrainService.TerrainRestored += OnTerrainRestored;
        _subscribed = true;
    }

    void OnDisable()
    {
        if (_subscribed)
        {
            TemporaryTerrainService.TerrainApplied -= OnTerrainApplied;
            TemporaryTerrainService.TerrainRestored -= OnTerrainRestored;
            _subscribed = false;
        }
        Clear();
    }

    public void Initialize(BattleGrid grid)
    {
        _properties ??= new MaterialPropertyBlock();
        Subscribe();
        if (_grid == grid) return;
        Clear();
        _grid = grid;
    }

    void Update()
    {
        if (_visuals.Count == 0) return;

        foreach (TerrainVisual visual in _visuals.Values)
        {
            if (visual?.renderer == null) continue;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.15f + visual.phase);
            Color tint = Color.Lerp(
                new Color(0.38f, 0.20f, 0.48f, 0.58f),
                new Color(0.72f, 0.40f, 0.90f, 0.88f),
                pulse);

            visual.renderer.GetPropertyBlock(_properties);
            _properties.SetColor(BaseColorId, tint);
            _properties.SetColor(ColorId, tint);
            visual.renderer.SetPropertyBlock(_properties);
            visual.root.transform.localScale = visual.baseScale * Mathf.Lerp(0.98f, 1.025f, pulse);
        }
    }

    void OnTerrainApplied(BattleGrid grid, Vector2Int position, TemporaryTerrainKind kind)
    {
        if (grid == null || grid != _grid || kind != TemporaryTerrainKind.LimboStain) return;
        if (_visuals.ContainsKey(position)) return;

        TerrainVisual visual = grid.Is3D
            ? CreateThreeDimensionalVisual(grid, position)
            : CreateLegacyIsometricVisual(grid, position);
        if (visual != null) _visuals[position] = visual;
    }

    void OnTerrainRestored(BattleGrid grid, Vector2Int position, TemporaryTerrainKind kind)
    {
        if (grid != _grid || !_visuals.TryGetValue(position, out TerrainVisual visual)) return;
        _visuals.Remove(position);
        DestroyObject(visual.root);
    }

    TerrainVisual CreateThreeDimensionalVisual(BattleGrid grid, Vector2Int position)
    {
        Material material = Resources.Load<Material>(LimboStainMaterialResource);
        if (material == null)
        {
            Debug.LogError("[TemporaryTerrainPresenter] Limbo Stain material is missing from Resources.", this);
            return null;
        }

        GameObject root = GameObject.CreatePrimitive(PrimitiveType.Quad);
        root.name = $"LimboStain_{position.x}_{position.y}";
        root.transform.SetParent(transform, true);
        root.transform.position = grid.GridToWorld(position) + Vector3.up * 0.035f;
        root.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        root.transform.localScale = new Vector3(grid.tileWidth * 0.92f, grid.tileWidth * 0.92f, 1f);

        var collider = root.GetComponent<Collider>();
        if (collider != null) DestroyObject(collider);
        var renderer = root.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        return new TerrainVisual
        {
            root = root,
            renderer = renderer,
            baseScale = root.transform.localScale,
            phase = position.x * 0.71f + position.y * 1.13f,
        };
    }

    TerrainVisual CreateLegacyIsometricVisual(BattleGrid grid, Vector2Int position)
    {
        Sprite sprite = Resources.Load<Sprite>(LimboStainSpriteResource);
        if (sprite == null)
        {
            Debug.LogError("[TemporaryTerrainPresenter] Limbo Stain sprite is missing from Resources.", this);
            return null;
        }

        var root = new GameObject($"LimboStain_{position.x}_{position.y}");
        root.transform.SetParent(transform, true);
        root.transform.position = grid.GridToWorld(position) + Vector3.forward * 0.01f;
        root.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
        float fit = sprite.bounds.size.x > 0.001f ? grid.tileWidth / sprite.bounds.size.x : 1f;
        root.transform.localScale = new Vector3(fit * 0.72f, fit * 0.36f, 1f);

        var renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = -5;

        return new TerrainVisual
        {
            root = root,
            renderer = renderer,
            baseScale = root.transform.localScale,
            phase = position.x * 0.71f + position.y * 1.13f,
        };
    }

    void Clear()
    {
        foreach (TerrainVisual visual in _visuals.Values)
            if (visual != null) DestroyObject(visual.root);
        _visuals.Clear();
    }

    static void DestroyObject(Object target)
    {
        if (target == null) return;
        if (Application.isPlaying) Object.Destroy(target);
        else Object.DestroyImmediate(target);
    }
}
