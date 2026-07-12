using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FlorenceLimboWorldAuthoringBuilder
{
    const string MercatoScenePath = "Assets/Scenes/MercatoVecchio.unity";
    const string AgentFolder = "Assets/Resources/WorldAgents";
    const string NpcFolder = "Assets/Resources/NpcMemory";
    const string DiscoveryFolder = "Assets/Resources/Discoveries";
    const string LimboProfileFolder = "Assets/Resources/CircleProfiles/Limbo";
    const string AuthoringRootName = "[LimboWorldAuthoring]";

    static readonly string[] PreachingSiteIds =
    {
        "mercato_crier_sermon_fountain",
        "mercato_crier_sermon_west_stalls",
        "mercato_crier_sermon_south_gate",
    };

    [MenuItem("InfernosCurse/Narrative/Rebuild Florence Limbo World Authoring")]
    public static void Rebuild()
    {
        EnsureFolder(AgentFolder);
        EnsureFolder(NpcFolder);
        EnsureFolder(DiscoveryFolder);
        EnsureFolder(LimboProfileFolder);
        BuildAgentDefinition();
        BuildNpcDefinitions();
        BuildDiscoveryDefinitions();
        BuildExpressionProfile();
        BuildMercatoSites();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        FlorenceLimboWorldAuthoringValidator.Validate();
        Debug.Log("[FlorenceLimboWorldAuthoring] Rebuilt 1 Crier, 5 NPC memory definitions, 5 discoveries, 1 Limbo expression profile, and 4 Mercato sites.");
    }

    static void BuildAgentDefinition()
    {
        var definition = GetOrCreate<WorldAgentDefinition>(
            AgentFolder + "/LimboCrier_Mercato_01.asset");
        definition.agentId = "limbo_crier_mercato_01";
        definition.territoryId = "firenze";
        definition.districtId = "mercato";
        definition.startingSiteId = PreachingSiteIds[0];
        definition.availableSiteIds = (string[])PreachingSiteIds.Clone();
        definition.startsDiscovered = false;
        // worldPrefab is intentionally preserved once the later visual-profile
        // checkpoint assigns it, so rebuilding authoring never unwires art.
        EditorUtility.SetDirty(definition);
    }

    static void BuildNpcDefinitions()
    {
        BuildNpc(
            "Npc_Agnolo_Neighbor.asset", "npc_agnolo_neighbor", 1.10f,
            "schedule_agnolo_home_market", "relationship_agnolo_lifelong_neighbor",
            new[] { PreachingSiteIds[0], PreachingSiteIds[2] },
            NpcCircleRelevanceLayer.DirectRelationship, false, false);
        BuildNpc(
            "Npc_Mercato_Baker.asset", "npc_mercato_baker", 1.00f,
            "schedule_mercato_baker", "relationship_mercato_usual_customer",
            new[] { PreachingSiteIds[0], PreachingSiteIds[1] },
            NpcCircleRelevanceLayer.LocalWitness, false, false);
        BuildNpc(
            "Npc_Mercato_RecordClerk.asset", "npc_mercato_record_clerk", 0.85f,
            "schedule_mercato_record_clerk", "relationship_civic_acquaintance",
            new[] { PreachingSiteIds[0] },
            NpcCircleRelevanceLayer.DerivedRegionalEvent, false, true);
        BuildNpc(
            "Npc_Mercato_Innkeeper.asset", "npc_mercato_innkeeper", 0.75f,
            "schedule_mercato_innkeeper", "relationship_inn_regular",
            new[] { PreachingSiteIds[2] },
            NpcCircleRelevanceLayer.TerritorySymptom, true, false);
        BuildNpc(
            "Npc_Mercato_Stallholder.asset", "npc_mercato_stallholder", 1.25f,
            "schedule_mercato_stallholder", "relationship_market_neighbor",
            new[] { PreachingSiteIds[1] },
            NpcCircleRelevanceLayer.LocalWitness, false, false);
    }

    static void BuildNpc(
        string fileName,
        string npcId,
        float susceptibility,
        string scheduleId,
        string relationshipId,
        string[] overlapSites,
        NpcCircleRelevanceLayer relevanceLayer,
        bool essential,
        bool critical)
    {
        var definition = GetOrCreate<NpcMemoryDefinition>(NpcFolder + "/" + fileName);
        definition.npcId = npcId;
        definition.homeDistrictId = "mercato";
        definition.homeTerritoryId = "firenze";
        definition.relevanceLayer = relevanceLayer;
        definition.susceptibility = susceptibility;
        definition.originalScheduleId = scheduleId;
        definition.originalRelationshipId = relationshipId;
        definition.overlappingPreachingSiteIds = overlapSites;
        definition.relevantSiteIds = new[] { "mercato" };
        definition.essentialService = essential;
        definition.questCritical = critical;
        EditorUtility.SetDirty(definition);
    }

    static void BuildExpressionProfile()
    {
        var profile = GetOrCreate<CircleExpressionProfile>(
            LimboProfileFolder + "/LimboExpression.asset");
        profile.circle = CircleId.Limbo;
        profile.propagationTuning = AssetDatabase.LoadAssetAtPath<CurseDefinition>(
            "Assets/Data/Curses/PlagueOfShadows.asset");
        profile.bands = new[]
        {
            new CircleExpressionBand
            {
                beginsAtInfluence = 0.10f,
                symptomTextId = "limbo_symptom_familiarity_slips",
                eventTags = new[] { "misremembered_name", "missed_greeting" },
                npcOverlayVocabulary = new[] { "hesitates", "searches_for_a_name" },
                environmentalPresentationIds = new[] { "limbo_subtle_absence" },
                encounterTags = new[] { "limbo_unease" },
            },
            new CircleExpressionBand
            {
                beginsAtInfluence = 0.35f,
                symptomTextId = "limbo_symptom_names_misplaced",
                eventTags = new[] { "neighbor_unrecognized", "routine_displaced" },
                npcOverlayVocabulary = new[] { "fragmented", "uncertain_familiarity" },
                environmentalPresentationIds = new[] { "limbo_missing_signs" },
                encounterTags = new[] { "limbo_crier_activity" },
            },
            new CircleExpressionBand
            {
                beginsAtInfluence = 0.55f,
                symptomTextId = "limbo_symptom_routines_unmoored",
                eventTags = new[] { "work_abandoned", "route_forgotten" },
                npcOverlayVocabulary = new[] { "dislocated", "speaks_around_absence" },
                environmentalPresentationIds = new[] { "limbo_unattended_places" },
                encounterTags = new[] { "limbo_unmoored_witness" },
            },
            new CircleExpressionBand
            {
                beginsAtInfluence = 0.75f,
                symptomTextId = "limbo_symptom_places_forgotten",
                eventTags = new[] { "place_erased", "collective_memory_gap" },
                npcOverlayVocabulary = new[] { "denies_the_place", "grief_without_a_name" },
                environmentalPresentationIds = new[] { "limbo_erased_landmark" },
                encounterTags = new[] { "limbo_forgotten_ground" },
            },
        };
        EditorUtility.SetDirty(profile);
    }

    static void BuildDiscoveryDefinitions()
    {
        BuildDiscovery(
            "Rumor_LimboBellsMercato.asset", "rumor_limbo_bells_mercato", DiscoveryKind.Rumor,
            "People mention a bell heard where no procession passed.", "mercato");
        BuildDiscovery(
            "Rumor_MissingAgnolo.asset", "rumor_missing_agnolo", DiscoveryKind.Rumor,
            "Someone is searching for a neighbor other citizens cannot name.", "mercato");
        BuildDiscovery(
            "POI_LostAgnolo.asset", "poi_lost_agnolo", DiscoveryKind.PointOfInterest,
            "A search area tied to the missing lifelong neighbor.", "mercato");
        BuildDiscovery(
            "POI_RomanFlorentiaStone.asset", "poi_roman_florentia_stone", DiscoveryKind.PointOfInterest,
            "A reused Roman stone from Florentia lies hidden beneath the medieval market fabric.", "mercato");
        BuildDiscovery(
            "Route_MercatoCrierSermon.asset", "route_mercato_crier_sermon", DiscoveryKind.Route,
            "Corroborated bell reports narrow the search to a route through Mercato Vecchio.", "mercato");
    }

    static void BuildDiscovery(
        string fileName,
        string discoveryId,
        DiscoveryKind kind,
        string hint,
        string locationId)
    {
        var definition = GetOrCreate<ExplorationDiscoveryDefinition>(DiscoveryFolder + "/" + fileName);
        definition.discoveryId = discoveryId;
        definition.kind = kind;
        definition.journalHint = hint;
        definition.locationId = locationId;
        EditorUtility.SetDirty(definition);
    }

    static void BuildMercatoSites()
    {
        foreach (var loadedScene in GetLoadedScenes())
            if (loadedScene.isDirty)
                throw new InvalidOperationException(
                    $"Save or close dirty scene '{loadedScene.name}' before rebuilding Limbo world authoring.");

        string priorScenePath = SceneManager.GetActiveScene().path;
        var scene = EditorSceneManager.OpenScene(MercatoScenePath, OpenSceneMode.Single);
        var existing = FindSceneTransform(scene, AuthoringRootName);
        if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);

        var root = new GameObject(AuthoringRootName);
        SceneManager.MoveGameObjectToScene(root, scene);

        CreateSite(
            root.transform, PreachingSiteIds[0], WorldAgentSiteRole.Preach,
            AnchorPosition(scene, "Anchor_Crier_Fountain", Vector3.zero, new Vector3(3f, 0f, 3f)));
        CreateSite(
            root.transform, PreachingSiteIds[1], WorldAgentSiteRole.Preach,
            AnchorPosition(scene, "Anchor_Crier_WestStalls", Vector3.zero, new Vector3(-19f, 0f, 7f)));
        CreateSite(
            root.transform, PreachingSiteIds[2], WorldAgentSiteRole.Preach,
            AnchorPosition(scene, "Anchor_Crier_SouthGate", Vector3.zero, new Vector3(7f, 0f, -23f)));
        CreateSite(
            root.transform, "mercato_crier_hide_barrel_alley", WorldAgentSiteRole.Hide,
            AnchorPosition(scene, "Anchor_Crier_HideAlley", Vector3.zero, new Vector3(-25f, 0f, 17f)));

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (!string.IsNullOrEmpty(priorScenePath) && priorScenePath != MercatoScenePath)
            EditorSceneManager.OpenScene(priorScenePath, OpenSceneMode.Single);
    }

    static void CreateSite(
        Transform parent,
        string siteId,
        WorldAgentSiteRole role,
        Vector3 position)
    {
        var gameObject = new GameObject("SITE_" + siteId);
        gameObject.transform.SetParent(parent, false);
        gameObject.transform.position = position;
        var site = gameObject.AddComponent<WorldAgentSite>();
        site.siteId = siteId;
        site.districtId = "mercato";
        site.role = role;
    }

    static Vector3 AnchorPosition(Scene scene, string anchorName, Vector3 offset, Vector3 fallback)
    {
        var anchor = FindSceneTransform(scene, anchorName);
        Vector3 position = anchor != null ? anchor.position + offset : fallback;
        position.y = 0.05f;
        return position;
    }

    static Transform FindSceneTransform(Scene scene, string objectName)
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                if (transform.name == objectName) return transform;
        return null;
    }

    static IEnumerable<Scene> GetLoadedScenes()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++) yield return SceneManager.GetSceneAt(i);
    }

    static T GetOrCreate<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        // Replace only this builder's own invalid first-pass output (for
        // example, an asset created before its ScriptableObject had a matching
        // Unity MonoScript file).
        if (AssetDatabase.LoadMainAssetAtPath(path) != null)
            AssetDatabase.DeleteAsset(path);
        else if (File.Exists(path))
        {
            FileUtil.DeleteFileOrDirectory(path);
            FileUtil.DeleteFileOrDirectory(path + ".meta");
            AssetDatabase.Refresh();
        }
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
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

public static class FlorenceLimboWorldAuthoringValidator
{
    [MenuItem("InfernosCurse/Validation/Validate Florence Limbo World Authoring")]
    public static void Validate()
    {
        var errors = new List<string>();
        ValidateAgent(errors);
        ValidateNpcs(errors);
        ValidateDiscoveries(errors);
        ValidateExpressionProfile(errors);
        ValidateMercatoSites(errors);

        if (errors.Count > 0)
        {
            foreach (string error in errors) Debug.LogError("[FlorenceLimboWorldAuthoringValidator] " + error);
            throw new InvalidOperationException($"Florence Limbo authoring validation failed with {errors.Count} error(s). ");
        }
        Debug.Log("[FlorenceLimboWorldAuthoringValidator] Validation passed: city-owned Crier influence, 5 layered NPC memory records, hidden discoveries, Limbo expression profile, and 4 authored Mercato sites.");
    }

    static void ValidateAgent(List<string> errors)
    {
        var definition = AssetDatabase.LoadAssetAtPath<WorldAgentDefinition>(
            "Assets/Resources/WorldAgents/LimboCrier_Mercato_01.asset");
        if (definition == null)
        {
            errors.Add("Mercato Crier definition is missing.");
            return;
        }
        if (definition.agentId != "limbo_crier_mercato_01" || definition.territoryId != "firenze" ||
            definition.districtId != "mercato" ||
            definition.startsDiscovered || definition.availableSiteIds?.Length != 3)
            errors.Add("Mercato Crier definition does not match the approved cautious opening state.");
    }

    static void ValidateNpcs(List<string> errors)
    {
        string[] ids =
        {
            "npc_agnolo_neighbor",
            "npc_mercato_baker",
            "npc_mercato_record_clerk",
            "npc_mercato_innkeeper",
            "npc_mercato_stallholder",
        };
        var found = new HashSet<string>(StringComparer.Ordinal);
        foreach (string guid in AssetDatabase.FindAssets("t:NpcMemoryDefinition", new[] { "Assets/Resources/NpcMemory" }))
        {
            var definition = AssetDatabase.LoadAssetAtPath<NpcMemoryDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (definition == null) continue;
            found.Add(definition.npcId);
            if (definition.homeDistrictId != "mercato" || definition.homeTerritoryId != "firenze" ||
                !Enum.IsDefined(typeof(NpcCircleRelevanceLayer), definition.relevanceLayer) ||
                definition.susceptibility < 0.5f ||
                definition.susceptibility > 1.5f || definition.overlappingPreachingSiteIds == null ||
                definition.overlappingPreachingSiteIds.Length == 0 ||
                definition.relevantSiteIds == null || definition.relevantSiteIds.Length == 0)
                errors.Add($"NPC definition '{definition.npcId}' has invalid district, susceptibility, or overlap authoring.");
        }
        foreach (string id in ids)
            if (!found.Contains(id)) errors.Add($"Required NPC memory definition '{id}' is missing.");

        var innkeeper = FindNpc("npc_mercato_innkeeper");
        var clerk = FindNpc("npc_mercato_record_clerk");
        if (innkeeper == null || !innkeeper.essentialService) errors.Add("Innkeeper essential-service safeguard is missing.");
        if (clerk == null || !clerk.questCritical) errors.Add("Record-clerk quest safeguard is missing.");
    }

    static void ValidateExpressionProfile(List<string> errors)
    {
        var profile = AssetDatabase.LoadAssetAtPath<CircleExpressionProfile>(
            "Assets/Resources/CircleProfiles/Limbo/LimboExpression.asset");
        if (profile == null || profile.circle != CircleId.Limbo || profile.propagationTuning == null ||
            profile.bands == null || profile.bands.Length != 4)
        {
            errors.Add("Limbo expression profile is missing or incomplete.");
            return;
        }
        float previous = -1f;
        foreach (var band in profile.bands)
        {
            if (band == null || band.beginsAtInfluence <= previous ||
                string.IsNullOrWhiteSpace(band.symptomTextId))
                errors.Add("Limbo expression bands require increasing internal thresholds and authored symptom IDs.");
            previous = band?.beginsAtInfluence ?? previous;
        }
    }

    static void ValidateDiscoveries(List<string> errors)
    {
        var found = new Dictionary<string, ExplorationDiscoveryDefinition>(StringComparer.Ordinal);
        foreach (string guid in AssetDatabase.FindAssets(
                     "t:ExplorationDiscoveryDefinition", new[] { "Assets/Resources/Discoveries" }))
        {
            var definition = AssetDatabase.LoadAssetAtPath<ExplorationDiscoveryDefinition>(
                AssetDatabase.GUIDToAssetPath(guid));
            if (definition != null) found[definition.discoveryId] = definition;
        }
        string[] required =
        {
            "rumor_limbo_bells_mercato",
            "rumor_missing_agnolo",
            "poi_lost_agnolo",
            "poi_roman_florentia_stone",
            "route_mercato_crier_sermon",
        };
        foreach (string id in required)
            if (!found.ContainsKey(id)) errors.Add($"Discovery definition '{id}' is missing.");
        if (found.TryGetValue("poi_roman_florentia_stone", out var cultural) &&
            cultural.kind != DiscoveryKind.PointOfInterest)
            errors.Add("The Roman Florentia cultural discovery is not a POI.");
    }

    static void ValidateMercatoSites(List<string> errors)
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/MercatoVecchio.unity");
        try
        {
            var sites = new List<WorldAgentSite>();
            foreach (var root in scene.GetRootGameObjects())
                sites.AddRange(root.GetComponentsInChildren<WorldAgentSite>(true));
            if (sites.Count != 4) errors.Add($"Mercato contains {sites.Count} Crier sites instead of 4.");

            var ids = new HashSet<string>(StringComparer.Ordinal);
            int preach = 0;
            int hide = 0;
            foreach (var site in sites)
            {
                if (!ids.Add(site.siteId)) errors.Add($"Mercato Crier site '{site.siteId}' is duplicated.");
                if (site.districtId != "mercato") errors.Add($"Site '{site.siteId}' has the wrong district.");
                var position = site.transform.position;
                if (float.IsNaN(position.x) || float.IsInfinity(position.x) ||
                    float.IsNaN(position.y) || float.IsInfinity(position.y) ||
                    float.IsNaN(position.z) || float.IsInfinity(position.z))
                    errors.Add($"Site '{site.siteId}' has a non-finite position.");
                if (site.role == WorldAgentSiteRole.Preach) preach++;
                if (site.role == WorldAgentSiteRole.Hide) hide++;
            }
            if (preach != 3 || hide != 1) errors.Add($"Mercato site roles are {preach} preach/{hide} hide.");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    static NpcMemoryDefinition FindNpc(string npcId)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:NpcMemoryDefinition", new[] { "Assets/Resources/NpcMemory" }))
        {
            var definition = AssetDatabase.LoadAssetAtPath<NpcMemoryDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (definition != null && definition.npcId == npcId) return definition;
        }
        return null;
    }
}
