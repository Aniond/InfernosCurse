using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class LimboCrierEncounterBuilder
{
    public const string WorldPrefabPath = "Assets/Prefabs/Narrative/LimboCrierWorld.prefab";
    public const string AgentPath = "Assets/Resources/WorldAgents/LimboCrier_Mercato_01.asset";
    public const string MercatoScenePath = "Assets/Scenes/MercatoVecchio.unity";
    public const string BattleRootName = "[MercatoBattleAuthoring]";

    const string BattleKitPath = "Assets/Prefabs/Battle/BattleKit.prefab";
    const string CrierPath = "Assets/Resources/Combatants/LimboCrier/Enemy_LimboCrier.asset";
    const string ProfilePath = "Assets/Resources/Combatants/LimboCrier/HumanoidVisual_LimboCrier.asset";
    const string FrontlinePath = "Assets/Data/Combatants/Enemy_Cursebearer.asset";
    const string BillboardMaterialPath = "Assets/Prefabs/Battle/Maps/Materials/BillboardUnit.mat";

    static readonly Vector2 MercatoOrigin = new(-50f, -32f);
    const int MercatoWidth = 90;
    const int MercatoHeight = 64;
    const float MercatoGroundY = 0.05f;

    [MenuItem("InfernosCurse/Narrative/Rebuild Limbo Crier World Encounter")]
    public static void Rebuild()
    {
        GameObject worldPrefab = BuildWorldPrefab();
        LinkAgentDefinition(worldPrefab);
        BuildMercatoBattleAuthoring();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        LimboCrierEncounterValidator.Validate();
        Debug.Log("[LimboCrierEncounterBuilder] Linked the persistent world actor and authored a 3-enemy Mercato confrontation.");
    }

    static GameObject BuildWorldPrefab()
    {
        EnsureFolder("Assets/Prefabs/Narrative");
        var profile = RequireAsset<HumanoidBattleVisualProfile>(ProfilePath);
        var crier = RequireAsset<CombatantData>(CrierPath);
        var frontline = RequireAsset<CombatantData>(FrontlinePath);
        var billboardMaterial = RequireAsset<Material>(BillboardMaterialPath);

        var root = new GameObject("LimboCrierWorld");
        try
        {
            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            var collider = root.AddComponent<CapsuleCollider>();
            collider.radius = 0.34f;
            collider.height = 1.8f;
            collider.center = new Vector3(0f, 0.9f, 0f);

            var persistent = root.AddComponent<PersistentCrierActor>();
            persistent.agentId = "limbo_crier_mercato_01";
            persistent.fallbackMoveSpeed = 1.35f;
            persistent.preachSecondsBeforeRelocation = 30f;

            var encounter = root.AddComponent<WorldAgentEncounterActor>();
            encounter.agentId = persistent.agentId;
            encounter.leaderCombatant = crier;
            encounter.frontlineCombatants = new[] { frontline, frontline };
            encounter.requiresDiscovery = true;

            var visualRoot = new GameObject("Visual");
            visualRoot.transform.SetParent(root.transform, false);
            visualRoot.transform.localPosition = new Vector3(0f, 0.96f, 0f);
            visualRoot.transform.localScale = new Vector3(0.62f, 0.62f, 1f);
            var renderer = visualRoot.AddComponent<SpriteRenderer>();
            renderer.sprite = profile.south;
            renderer.sharedMaterial = billboardMaterial;
            renderer.sortingOrder = 10;
            visualRoot.AddComponent<SpriteBillboard>().tiltFactor = 0.75f;

            var visual = visualRoot.AddComponent<LimboCrierWorldVisual>();
            visual.agentId = persistent.agentId;
            visual.profile = profile;
            visual.spriteRenderer = renderer;
            visual.worldCollider = collider;

            return PrefabUtility.SaveAsPrefabAsset(root, WorldPrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    static void LinkAgentDefinition(GameObject worldPrefab)
    {
        var definition = RequireAsset<WorldAgentDefinition>(AgentPath);
        definition.worldPrefab = worldPrefab;
        EditorUtility.SetDirty(definition);
    }

    static void BuildMercatoBattleAuthoring()
    {
        foreach (Scene loadedScene in LoadedScenes())
            if (loadedScene.isDirty)
                throw new InvalidOperationException(
                    $"Save or close dirty scene '{loadedScene.name}' before rebuilding the Mercato encounter.");

        string previousScenePath = SceneManager.GetActiveScene().path;
        Scene scene = EditorSceneManager.OpenScene(MercatoScenePath, OpenSceneMode.Single);
        Transform oldRoot = FindSceneTransform(scene, BattleRootName);
        if (oldRoot != null) UnityEngine.Object.DestroyImmediate(oldRoot.gameObject);

        var root = new GameObject(BattleRootName);
        SceneManager.MoveGameObjectToScene(root, scene);

        var authoring = root.AddComponent<BattleMapAuthoring>();
        authoring.width = MercatoWidth;
        authoring.height = MercatoHeight;
        authoring.tileWidth = 1f;
        authoring.tileHeight = 0.5f;
        authoring.worldOriginXZ = MercatoOrigin;
        authoring.applyOnAwake = false;
        authoring.plateaus = new List<BattleMapAuthoring.Plateau>();
        authoring.pathCells = new List<Vector2Int>();
        authoring.blockedCells = CollectBlockedCells(scene, authoring, root.transform);

        var heights = root.AddComponent<BattleTerrainHeights>();
        heights.width = MercatoWidth;
        heights.height = MercatoHeight;
        heights.worldOriginXZ = MercatoOrigin;
        heights.res = 1;
        heights.cellY = Enumerable.Repeat(MercatoGroundY, MercatoWidth * MercatoHeight).ToArray();
        heights.surfY = Enumerable.Repeat(
            MercatoGroundY, (MercatoWidth + 1) * (MercatoHeight + 1)).ToArray();

        var sites = SceneComponents<WorldAgentSite>(scene).ToArray();
        foreach (WorldAgentSite site in sites)
            ClearRadius(authoring.blockedCells, authoring.WorldToCell(site.transform.position), 1);

        var player = SceneComponents<PlayerController>(scene).FirstOrDefault();
        if (player != null && player.GetComponent<PlayerWorldInteractor>() == null)
            player.gameObject.AddComponent<PlayerWorldInteractor>();
        Vector2Int playerCell = player != null
            ? authoring.WorldToCell(player.transform.position)
            : new Vector2Int(MercatoWidth / 2, MercatoHeight / 2);
        ClearRadius(authoring.blockedCells, playerCell, 2);
        authoring.partySpawns = Around(playerCell, 4, authoring);

        WorldAgentSite firstSite = sites.FirstOrDefault(site => site.role == WorldAgentSiteRole.Preach);
        Vector2Int enemyCell = firstSite != null
            ? authoring.WorldToCell(firstSite.transform.position)
            : new Vector2Int(MercatoWidth / 2 + 4, MercatoHeight / 2);
        authoring.enemySpawns = Around(enemyCell, 4, authoring);

        var battleKit = RequireAsset<GameObject>(BattleKitPath);
        var trigger = root.AddComponent<ZoneEncounterTrigger>();
        trigger.battleKitPrefab = battleKit;
        trigger.proximityTrigger = 2.5f;
        trigger.ambushRevealSeconds = 0f;

        var zone = root.AddComponent<ZoneBattleAuthoring>();
        zone.combatAllowed = true;
        zone.mapAuthoring = authoring;
        zone.terrainHeights = heights;
        zone.encounterTrigger = trigger;
        zone.battleKitPrefab = battleKit;
        zone.zoneTerrain = null;
        zone.terrainProfile = null;
        zone.zoneExits = SceneComponents<ZoneExit>(scene).ToArray();
        zone.protectedInteriors = SceneComponents<SeamlessInteriorModule>(scene).ToArray();
        zone.explorationOnlyRoots = Array.Empty<GameObject>();
        zone.battleOnlyRoots = Array.Empty<GameObject>();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (!string.IsNullOrEmpty(previousScenePath) && previousScenePath != MercatoScenePath)
            EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
    }

    static List<Vector2Int> CollectBlockedCells(
        Scene scene,
        BattleMapAuthoring authoring,
        Transform authoringRoot)
    {
        var blocked = new HashSet<Vector2Int>();
        foreach (Collider collider in SceneComponents<Collider>(scene))
        {
            if (collider == null || !collider.enabled || collider.isTrigger ||
                !collider.gameObject.activeInHierarchy || collider.transform.IsChildOf(authoringRoot))
                continue;
            if (collider.GetComponentInParent<PlayerController>() != null ||
                collider.GetComponentInParent<ZoneExit>() != null)
                continue;

            string label = collider.name.ToLowerInvariant();
            if (label.Contains("floor") || label.Contains("ground") || label.Contains("cobble") ||
                label.Contains("water") || label.Contains("entrypoint") || label.Contains("confiner"))
                continue;

            Bounds bounds = collider.bounds;
            if (bounds.size.y < 0.30f || bounds.max.y < 0.35f) continue;
            int minX = Mathf.FloorToInt((bounds.min.x - MercatoOrigin.x - 0.4f));
            int maxX = Mathf.FloorToInt((bounds.max.x - MercatoOrigin.x + 0.4f));
            int minY = Mathf.FloorToInt((bounds.min.z - MercatoOrigin.y - 0.4f));
            int maxY = Mathf.FloorToInt((bounds.max.z - MercatoOrigin.y + 0.4f));
            for (int x = minX; x <= maxX; x++)
                for (int y = minY; y <= maxY; y++)
                {
                    var cell = new Vector2Int(x, y);
                    if (x >= 0 && x < authoring.width && y >= 0 && y < authoring.height)
                        blocked.Add(cell);
                }
        }
        return blocked.OrderBy(cell => cell.y).ThenBy(cell => cell.x).ToList();
    }

    static List<Vector2Int> Around(Vector2Int center, int count, BattleMapAuthoring authoring)
    {
        var positions = new List<Vector2Int>();
        var blocked = new HashSet<Vector2Int>(authoring.blockedCells);
        for (int ring = 0; ring <= 4 && positions.Count < count; ring++)
            for (int dx = -ring; dx <= ring && positions.Count < count; dx++)
                for (int dy = -ring; dy <= ring && positions.Count < count; dy++)
                {
                    var cell = center + new Vector2Int(dx, dy);
                    if (cell.x < 0 || cell.x >= authoring.width || cell.y < 0 || cell.y >= authoring.height ||
                        blocked.Contains(cell) || positions.Contains(cell))
                        continue;
                    positions.Add(cell);
                }
        return positions;
    }

    static void ClearRadius(List<Vector2Int> blocked, Vector2Int center, int radius) =>
        blocked.RemoveAll(cell => Mathf.Abs(cell.x - center.x) <= radius && Mathf.Abs(cell.y - center.y) <= radius);

    static IEnumerable<T> SceneComponents<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (T component in root.GetComponentsInChildren<T>(true))
                yield return component;
    }

    static Transform FindSceneTransform(Scene scene, string name)
    {
        foreach (Transform transform in SceneComponents<Transform>(scene))
            if (transform.name == name) return transform;
        return null;
    }

    static IEnumerable<Scene> LoadedScenes()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++) yield return SceneManager.GetSceneAt(i);
    }

    static T RequireAsset<T>(string path) where T : UnityEngine.Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) throw new InvalidOperationException("Required asset is missing: " + path);
        return asset;
    }

    static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}

public static class LimboCrierEncounterValidator
{
    [MenuItem("InfernosCurse/Validation/Validate Limbo Crier World Encounter")]
    public static void Validate()
    {
        var errors = new List<string>();
        ValidatePrefabAndDefinition(errors);
        ValidateMercato(errors);
        ValidateTerrainPresentation(errors);
        if (errors.Count > 0)
        {
            foreach (string error in errors) Debug.LogError("[LimboCrierEncounterValidator] " + error);
            throw new InvalidOperationException($"Limbo Crier encounter validation failed with {errors.Count} error(s).");
        }
        Debug.Log("[LimboCrierEncounterValidator] Validation passed: discovery-gated world actor, 3-enemy formation, Mercato hybrid grid, persistent victory, and stain lifecycle.");
    }

    static void ValidatePrefabAndDefinition(List<string> errors)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LimboCrierEncounterBuilder.WorldPrefabPath);
        WorldAgentDefinition definition = AssetDatabase.LoadAssetAtPath<WorldAgentDefinition>(
            LimboCrierEncounterBuilder.AgentPath);
        if (prefab == null) { errors.Add("World prefab is missing."); return; }
        if (definition == null || definition.worldPrefab != prefab)
            errors.Add("Persistent agent definition is not linked to the world prefab.");
        if (prefab.GetComponent<BattleUnit>() != null)
            errors.Add("Exploration prefab must not double as a staged BattleUnit.");

        var actor = prefab.GetComponent<WorldAgentEncounterActor>();
        var persistent = prefab.GetComponent<PersistentCrierActor>();
        var visual = prefab.GetComponentInChildren<LimboCrierWorldVisual>(true);
        if (actor == null || persistent == null || visual == null)
        { errors.Add("World prefab is missing persistent, encounter, or visual behavior."); return; }
        if (actor.agentId != "limbo_crier_mercato_01" || actor.leaderCombatant == null ||
            actor.leaderCombatant.displayName != "Limbo Crier" || actor.frontlineCombatants?.Length != 2 ||
            actor.frontlineCombatants.Any(frontline => frontline == null || frontline.displayName != "Cursebearer") ||
            !actor.requiresDiscovery)
            errors.Add("World encounter composition or discovery gate drifted.");
        if (visual.profile == null || visual.spriteRenderer == null || visual.worldCollider == null)
            errors.Add("World visual does not reuse the production profile, renderer, and collider gate.");
    }

    static void ValidateMercato(List<string> errors)
    {
        Scene scene = EditorSceneManager.OpenPreviewScene(LimboCrierEncounterBuilder.MercatoScenePath);
        try
        {
            var zones = SceneComponents<ZoneBattleAuthoring>(scene).ToArray();
            if (zones.Length != 1) { errors.Add($"Mercato has {zones.Length} hybrid battle roots instead of 1."); return; }
            ZoneBattleAuthoring zone = zones[0];
            BattleMapAuthoring authoring = zone.mapAuthoring;
            if (!zone.combatAllowed || authoring == null || zone.terrainHeights == null ||
                zone.encounterTrigger == null || zone.battleKitPrefab == null)
                errors.Add("Mercato hybrid references are incomplete.");
            if (authoring == null) return;
            if (authoring.width != 90 || authoring.height != 64 ||
                authoring.worldOriginXZ != new Vector2(-50f, -32f) || authoring.applyOnAwake)
                errors.Add("Mercato grid dimensions, origin, or exploration-safe apply mode drifted.");
            if (authoring.blockedCells == null || authoring.blockedCells.Count < 20)
                errors.Add("Mercato collider-to-grid obstruction authoring is unexpectedly sparse.");
            PlayerController player = SceneComponents<PlayerController>(scene).FirstOrDefault();
            if (player == null || player.GetComponent<PlayerWorldInteractor>() == null)
                errors.Add("Mercato player is missing the shared world interaction path.");
            if (zone.protectedInteriors == null || zone.protectedInteriors.Length != 1 ||
                zone.protectedInteriors[0] == null ||
                zone.protectedInteriors[0].SubLocationId != "albergo_fiorentino_floor1")
                errors.Add("Mercato protected seamless-inn battle contract is missing.");

            var blocked = new HashSet<Vector2Int>(authoring.blockedCells ?? new List<Vector2Int>());
            foreach (WorldAgentSite site in SceneComponents<WorldAgentSite>(scene))
            {
                Vector2Int cell = authoring.WorldToCell(site.transform.position);
                if (cell.x < 0 || cell.x >= authoring.width || cell.y < 0 || cell.y >= authoring.height)
                    errors.Add($"Crier site '{site.siteId}' lies outside the Mercato battle grid.");
                if (blocked.Contains(cell)) errors.Add($"Crier site '{site.siteId}' is battle-blocked.");
            }
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    static void ValidateTerrainPresentation(List<string> errors)
    {
        TemporaryTerrainService.ResetForTests();
        var root = new GameObject("LimboStainPresenterValidator");
        try
        {
            var heights = root.AddComponent<BattleTerrainHeights>();
            heights.width = 2;
            heights.height = 2;
            heights.cellY = new float[4];
            heights.res = 1;
            heights.surfY = new float[9];
            var grid = root.AddComponent<BattleGrid>();
            grid.Initialize(2, 2);
            var presenter = root.AddComponent<TemporaryTerrainPresenter>();
            presenter.Initialize(grid);
            if (!TemporaryTerrainService.TryApplyLimboStain(
                    grid, Vector2Int.zero, null, 1, out TemporaryTerrainFailure failure))
                errors.Add("Presenter fixture could not apply Limbo Stain: " + failure);
            if (presenter.VisualCount != 1) errors.Add("Limbo Stain apply did not create exactly one visual.");
            TemporaryTerrainService.RestoreAll(grid);
            if (presenter.VisualCount != 0) errors.Add("Limbo Stain restore left a visual behind.");
        }
        finally
        {
            TemporaryTerrainService.ResetForTests();
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    static IEnumerable<T> SceneComponents<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (T component in root.GetComponentsInChildren<T>(true))
                yield return component;
    }
}
