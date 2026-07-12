using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class GugolMappeRefinedAuthoringBuilder
{
    const string Root = "Assets/Resources/GugolMap";
    const string StreetFolder = Root + "/Streets";
    const string VenueFolder = Root + "/Venues";
    const string NpcFolder = Root + "/Npcs";
    const string PresentationFolder = Root + "/Presentation";
    const string ExpressionFolder = Root + "/WorldExpressions";

    [InitializeOnLoadMethod]
    static void EnsurePilotAuthoringExists()
    {
        EditorApplication.delayCall += () =>
        {
            if (AssetDatabase.LoadAssetAtPath<GugolMapPresentationProfile>(
                    PresentationFolder + "/FlorenceRefined.asset") == null)
                Build();
            else if (!GugolMapAuthoringValidator.Validate(out string error))
                Debug.LogError("[GugolMapValidator] " + error);
            else
                Debug.Log("[GugolMapValidator] Refined authoring validation passed after reload.");
        };
    }

    [MenuItem("InfernosCurse/Gugol Mappe/Build Refined Authoring")]
    public static void Build()
    {
        EnsureFolder(Root);
        EnsureFolder(StreetFolder);
        EnsureFolder(VenueFolder);
        EnsureFolder(NpcFolder);
        EnsureFolder(PresentationFolder);
        EnsureFolder(ExpressionFolder);

        BuildPresentation();
        BuildStreets();
        BuildVenues();
        BuildNpcs();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        if (!GugolMapAuthoringValidator.Validate(out string validationError))
            throw new InvalidOperationException(validationError);
        Debug.Log("[GugolMappeRefined] Built presentation profile, 2 pilot streets, 3 venues, and 5 NPC map definitions.");
    }

    static void BuildPresentation()
    {
        var profile = GetOrCreate<GugolMapPresentationProfile>(PresentationFolder + "/FlorenceRefined.asset");
        profile.cityBackground = LoadSprite("Assets/UI/Map/gugol-map-background.png", "Assets/Art/Map/FlorenceCity.jpg");
        profile.regionBackground = LoadSprite("Assets/UI/Map/gugol-region-map.png");
        profile.worldBackground = LoadSprite("Assets/UI/Map/gugol-italy-map.png");
        profile.fallbackBackground = profile.cityBackground;
        profile.searchBar = LoadSprite("Assets/UI/Map/gugol-searchbar.png");
        profile.contextCard = LoadSprite("Assets/UI/Map/gugol-card-frame.png");
        profile.defaultPin = LoadSprite("Assets/UI/Map/gugol-pin.png", "Assets/Art/Map/PinMedallion.png");
        profile.townPin = LoadSprite("Assets/UI/Map/gugol-town-pin.png");
        profile.playerMarker = LoadSprite("Assets/UI/Map/gugol-you-are-here.png");
        profile.routeWalker = LoadSprite("Assets/UI/Map/gugol-walker.png");
        profile.parchment = new Color(0.89f, 0.83f, 0.70f, 1f);
        profile.ink = new Color(0.24f, 0.17f, 0.09f, 1f);
        profile.mutedInk = new Color(0.45f, 0.38f, 0.28f, 1f);
        profile.activeRoute = new Color(0.20f, 0.43f, 0.72f, 1f);
        profile.waxSeal = new Color(0.55f, 0.16f, 0.12f, 1f);
        profile.garden = new Color(0.34f, 0.43f, 0.22f, 1f);
        profile.river = new Color(0.31f, 0.48f, 0.58f, 1f);
        profile.viewTransitionSeconds = 0.28f;
        profile.routeDrawSeconds = 0.45f;
        profile.selectedLiftPixels = 8f;
        EditorUtility.SetDirty(profile);
    }

    static void BuildStreets()
    {
        ConfigureStreet(
            "MercatoVecchioSquare.asset",
            "mercato_vecchio_square",
            "Mercato Vecchio",
            new[] { "mercato" },
            new[]
            {
                new Vector2(0.32f, 0.66f),
                new Vector2(0.40f, 0.62f),
                new Vector2(0.49f, 0.58f),
            },
            new Rect(0.28f, 0.50f, 0.34f, 0.25f),
            new[] { "albergo_fiorentino", "mercato_public_stalls", "mercato_records" },
            new Vector2(0.31f, 0.63f),
            100);

        ConfigureStreet(
            "ViaCalimala.asset",
            "via_calimala",
            "Via Calimala",
            new[] { "mercato", "via_calimala" },
            new[]
            {
                new Vector2(0.45f, 0.61f),
                new Vector2(0.52f, 0.54f),
                new Vector2(0.58f, 0.45f),
                new Vector2(0.62f, 0.38f),
            },
            new Rect(0.41f, 0.34f, 0.25f, 0.31f),
            Array.Empty<string>(),
            new Vector2(0.45f, 0.61f),
            80);
    }

    static void ConfigureStreet(
        string fileName,
        string id,
        string displayName,
        string[] districts,
        Vector2[] centerline,
        Rect bounds,
        string[] venues,
        Vector2 routeFallback,
        int priority)
    {
        var street = GetOrCreate<GugolStreetDefinition>(StreetFolder + "/" + fileName);
        street.streetId = id;
        street.displayName = displayName;
        street.parentCityId = "firenze";
        street.districtIds = districts;
        street.discoveryId = string.Empty;
        street.minimumVisibleStage = DiscoveryStage.Rumored;
        street.rumorLabel = "A street spoken of in Florence";
        street.cityCenterline = centerline;
        street.cityHitWidth = 0.035f;
        street.labelPriority = priority;
        street.streetViewBounds = bounds;
        street.streetViewBackground = null;
        street.venueIds = venues;
        street.routeFallbackPosition = routeFallback;
        EditorUtility.SetDirty(street);
    }

    static void BuildVenues()
    {
        ConfigureVenue(
            "AlbergoFiorentino.asset",
            "albergo_fiorentino",
            "The Florentine Inn",
            GugolVenueCategory.Inn,
            "mercato_vecchio_square",
            new Vector2(0.36f, 0.63f),
            "MercatoVecchio",
            string.Empty,
            "albergo_fiorentino",
            "albergo_fiorentino_floor1",
            "mercato",
            "Open day and night",
            new[] { "Lodging", "Rest", "Rumors" });

        ConfigureVenue(
            "MercatoPublicStalls.asset",
            "mercato_public_stalls",
            "Mercato Public Stalls",
            GugolVenueCategory.Shop,
            "mercato_vecchio_square",
            new Vector2(0.43f, 0.61f),
            "MercatoVecchio",
            "mercato_south",
            string.Empty,
            string.Empty,
            "mercato",
            "Open during market hours",
            new[] { "Food", "Household goods", "Trade" });

        ConfigureVenue(
            "MercatoRecords.asset",
            "mercato_records",
            "Mercato Records Desk",
            GugolVenueCategory.Service,
            "mercato_vecchio_square",
            new Vector2(0.47f, 0.64f),
            "MercatoVecchio",
            "mercato_signoria",
            string.Empty,
            string.Empty,
            "mercato",
            "Open during civic hours",
            new[] { "Records", "Local information" });
    }

    static void ConfigureVenue(
        string fileName,
        string id,
        string displayName,
        GugolVenueCategory category,
        string streetId,
        Vector2 anchor,
        string sceneName,
        string entryId,
        string buildingId,
        string subLocationId,
        string siteId,
        string hours,
        string[] services)
    {
        var venue = GetOrCreate<GugolVenueDefinition>(VenueFolder + "/" + fileName);
        venue.venueId = id;
        venue.displayName = displayName;
        venue.category = category;
        venue.streetId = streetId;
        venue.discoveryId = string.Empty;
        venue.minimumVisibleStage = DiscoveryStage.Located;
        venue.rumorLabel = string.Empty;
        venue.streetViewAnchor = anchor;
        venue.sceneName = sceneName;
        venue.entryId = entryId;
        venue.buildingId = buildingId;
        venue.subLocationId = subLocationId;
        venue.siteId = siteId;
        venue.openingHoursText = hours;
        venue.services = services;
        EditorUtility.SetDirty(venue);
    }

    static void BuildNpcs()
    {
        ConfigureNpc("MercatoInnkeeper.asset", "npc_mercato_innkeeper", "Innkeeper",
            "mercato_vecchio_square", "albergo_fiorentino", "Usually at the Florentine Inn");
        ConfigureNpc("MercatoBaker.asset", "npc_mercato_baker", "Baker",
            "mercato_vecchio_square", "mercato_public_stalls", "Usually works near the food stalls");
        ConfigureNpc("MercatoStallholder.asset", "npc_mercato_stallholder", "Market Stallholder",
            "mercato_vecchio_square", "mercato_public_stalls", "Usually works among the public stalls");
        ConfigureNpc("MercatoRecordClerk.asset", "npc_mercato_record_clerk", "Record Clerk",
            "mercato_vecchio_square", "mercato_records", "Usually found at the records desk");
        ConfigureNpc("AgnoloNeighbor.asset", "npc_agnolo_neighbor", "Agnolo",
            "mercato_vecchio_square", string.Empty, "Usually seen around Mercato Vecchio");
    }

    static void ConfigureNpc(
        string fileName,
        string id,
        string displayName,
        string streetId,
        string venueId,
        string usualText)
    {
        var npc = GetOrCreate<GugolNpcMapDefinition>(NpcFolder + "/" + fileName);
        npc.npcId = id;
        npc.displayName = displayName;
        npc.discoveryId = string.Empty;
        npc.minimumVisibleStage = DiscoveryStage.Discovered;
        npc.usualStreetId = streetId;
        npc.usualVenueId = venueId;
        npc.usualLocationText = usualText;
        npc.authoredSchedulePhrases = new[] { usualText };
        EditorUtility.SetDirty(npc);
    }

    static T GetOrCreate<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    static Sprite LoadSprite(params string[] paths)
    {
        foreach (string path in paths)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) return sprite;
        }
        return null;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = path.Substring(0, path.LastIndexOf('/'));
        string name = path.Substring(path.LastIndexOf('/') + 1);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
