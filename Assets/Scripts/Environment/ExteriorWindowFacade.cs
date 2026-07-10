using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Adds lightweight, non-colliding occupied-window cards to a monolithic
/// building renderer. Imported GLB materials remain untouched.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class ExteriorWindowFacade : MonoBehaviour
{
    public enum FacadeDirection
    {
        AutoTowardSceneOrigin,
        PositiveX,
        NegativeX,
        PositiveZ,
        NegativeZ
    }

    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private FacadeDirection direction = FacadeDirection.AutoTowardSceneOrigin;
    [SerializeField, Range(1, 8)] private int columns = 3;
    [SerializeField, Range(1, 4)] private int rows = 2;
    [SerializeField, Range(0.1f, 0.8f)] private float windowWidthFraction = 0.42f;
    [SerializeField, Range(0.1f, 0.8f)] private float windowHeightFraction = 0.38f;
    [SerializeField, Min(0f)] private float surfaceOffset = 0.035f;
    [SerializeField, ColorUsage(false, true)] private Color windowColor = new Color(1f, 0.64f, 0.30f, 1f);
    [SerializeField, Min(0f)] private float emissionMultiplier = 0.25f;
    [SerializeField, Min(0f)] private float lightningMultiplier = 2.5f;

    const string GeneratedRootName = "[Generated World Windows]";
    Transform _generatedRoot;
    Material _runtimeMaterial;
    Texture2D _windowPattern;

    public Renderer TargetRenderer
    {
        get => targetRenderer;
        set => targetRenderer = value;
    }

    public FacadeDirection Direction
    {
        get => direction;
        set => direction = value;
    }

    public int Columns
    {
        get => columns;
        set => columns = Mathf.Clamp(value, 1, 8);
    }

    public int Rows
    {
        get => rows;
        set => rows = Mathf.Clamp(value, 1, 4);
    }

    public float EmissionMultiplier
    {
        get => emissionMultiplier;
        set => emissionMultiplier = Mathf.Max(0f, value);
    }

    void OnEnable() => Rebuild();

    void OnDisable() => ClearGenerated();

    [ContextMenu("Rebuild Window Facade")]
    public void Rebuild()
    {
        ClearGenerated();
        if (targetRenderer == null) targetRenderer = GetComponentInChildren<Renderer>();
        if (targetRenderer == null) return;

        _runtimeMaterial = CreateWindowMaterial();
        var root = new GameObject(GeneratedRootName);
        root.hideFlags = HideFlags.DontSave;
        root.transform.SetParent(transform, true);
        _generatedRoot = root.transform;

        Bounds bounds = targetRenderer.bounds;
        FacadeDirection resolved = ResolveDirection(bounds.center);
        bool xFace = resolved == FacadeDirection.PositiveX || resolved == FacadeDirection.NegativeX;
        float facadeWidth = xFace ? bounds.size.z : bounds.size.x;
        float facadeHeight = bounds.size.y;
        int safeColumns = Mathf.Clamp(columns, 1, Mathf.Max(1, Mathf.FloorToInt(facadeWidth / 1.25f)));
        int safeRows = Mathf.Clamp(rows, 1, Mathf.Max(1, Mathf.FloorToInt(facadeHeight / 1.4f)));
        float cellWidth = facadeWidth / safeColumns;
        float cellHeight = facadeHeight * 0.70f / safeRows;
        float width = Mathf.Clamp(cellWidth * windowWidthFraction, 0.24f, 0.72f);
        float height = Mathf.Clamp(cellHeight * windowHeightFraction, 0.38f, 0.82f);
        float yMin = bounds.min.y + facadeHeight * 0.08f;
        float ySpan = facadeHeight * 0.30f;

        var surfaces = new List<WorldWindowEnvironment.WindowSurface>(safeColumns * safeRows);
        for (int row = 0; row < safeRows; row++)
        {
            float y = yMin + ySpan * ((row + 0.5f) / safeRows);
            for (int column = 0; column < safeColumns; column++)
            {
                float across = Mathf.Lerp(-facadeWidth * 0.38f, facadeWidth * 0.38f,
                    safeColumns == 1 ? 0.5f : column / (float)(safeColumns - 1));
                Vector3 position = WindowPosition(bounds, resolved, across, y);
                Quaternion rotation = WindowRotation(resolved);
                Renderer pane = CreatePane($"Window_{row + 1}_{column + 1}", position, rotation, width, height);
                surfaces.Add(new WorldWindowEnvironment.WindowSurface
                {
                    renderer = pane,
                    role = WorldWindowEnvironment.WindowRole.ExteriorOccupied,
                    emissionTint = windowColor,
                    emissionMultiplier = emissionMultiplier,
                    lightningMultiplier = lightningMultiplier
                });
            }
        }

        var environment = GetComponent<WorldWindowEnvironment>();
        if (environment == null) environment = gameObject.AddComponent<WorldWindowEnvironment>();
        environment.Windows = surfaces.ToArray();
    }

    Renderer CreatePane(string paneName, Vector3 position, Quaternion rotation, float width, float height)
    {
        var pane = GameObject.CreatePrimitive(PrimitiveType.Quad);
        pane.name = paneName;
        pane.hideFlags = HideFlags.DontSave;
        pane.transform.SetParent(_generatedRoot, true);
        pane.transform.SetPositionAndRotation(position, rotation);
        pane.transform.localScale = new Vector3(width, height, 1f);
        DestroyObject(pane.GetComponent<Collider>());
        var renderer = pane.GetComponent<Renderer>();
        renderer.sharedMaterial = _runtimeMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    Material CreateWindowMaterial()
    {
        _windowPattern = CreateWindowPattern();
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        var material = new Material(shader)
        {
            name = "Runtime Exterior Window",
            hideFlags = HideFlags.DontSave
        };
        material.color = windowColor * 0.08f;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", windowColor * 0.08f);
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", _windowPattern);
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", _windowPattern);
        if (material.HasProperty("_EmissionMap")) material.SetTexture("_EmissionMap", _windowPattern);
        material.EnableKeyword("_EMISSION");
        if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", windowColor * emissionMultiplier);
        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        return material;
    }

    static Texture2D CreateWindowPattern()
    {
        const int size = 16;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Runtime Four-Pane Window",
            hideFlags = HideFlags.DontSave,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        var pixels = new Color32[size * size];
        var pane = new Color32(255, 255, 255, 255);
        var timber = new Color32(10, 4, 1, 255);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                bool frame = x <= 1 || x >= size - 2 || y <= 1 || y >= size - 2 ||
                             x == 7 || x == 8 || y == 7 || y == 8;
                pixels[y * size + x] = frame ? timber : pane;
            }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

    FacadeDirection ResolveDirection(Vector3 center)
    {
        if (direction != FacadeDirection.AutoTowardSceneOrigin) return direction;
        Vector3 towardOrigin = -center;
        if (Mathf.Abs(towardOrigin.x) > Mathf.Abs(towardOrigin.z))
            return towardOrigin.x >= 0f ? FacadeDirection.PositiveX : FacadeDirection.NegativeX;
        return towardOrigin.z >= 0f ? FacadeDirection.PositiveZ : FacadeDirection.NegativeZ;
    }

    Vector3 WindowPosition(Bounds bounds, FacadeDirection resolved, float across, float y)
    {
        switch (resolved)
        {
            case FacadeDirection.PositiveX:
                return new Vector3(bounds.max.x + surfaceOffset, y, bounds.center.z + across);
            case FacadeDirection.NegativeX:
                return new Vector3(bounds.min.x - surfaceOffset, y, bounds.center.z + across);
            case FacadeDirection.PositiveZ:
                return new Vector3(bounds.center.x + across, y, bounds.max.z + surfaceOffset);
            default:
                return new Vector3(bounds.center.x + across, y, bounds.min.z - surfaceOffset);
        }
    }

    static Quaternion WindowRotation(FacadeDirection resolved)
    {
        switch (resolved)
        {
            case FacadeDirection.PositiveX: return Quaternion.Euler(0f, -90f, 0f);
            case FacadeDirection.NegativeX: return Quaternion.Euler(0f, 90f, 0f);
            case FacadeDirection.PositiveZ: return Quaternion.Euler(0f, 180f, 0f);
            default: return Quaternion.identity;
        }
    }

    void ClearGenerated()
    {
        if (_generatedRoot == null)
        {
            Transform existing = transform.Find(GeneratedRootName);
            if (existing != null) _generatedRoot = existing;
        }
        if (_generatedRoot != null) DestroyObject(_generatedRoot.gameObject);
        _generatedRoot = null;
        if (_runtimeMaterial != null) DestroyObject(_runtimeMaterial);
        _runtimeMaterial = null;
        if (_windowPattern != null) DestroyObject(_windowPattern);
        _windowPattern = null;
    }

    static void DestroyObject(Object target)
    {
        if (target == null) return;
        if (Application.isPlaying) Object.Destroy(target);
        else Object.DestroyImmediate(target);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!isActiveAndEnabled) return;
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null && isActiveAndEnabled) Rebuild();
        };
    }
#endif
}
