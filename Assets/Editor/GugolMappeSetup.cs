using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.TextCore.LowLevel;

// One-shot, idempotent wiring for the Gugol Mappe world map. Three menu items,
// run in order (BattleArenaBuilder-style deterministic setup — no hand edits
// of GameSystems.prefab):
//   1. Create TMP Fonts     — Cinzel/EB Garamond TTFs → dynamic TMP SDF assets.
//   2. Import Map Art       — approved workbench PNGs → Assets/UI/Map/ with
//                             magenta keyed to transparency + sprite settings.
//   3. Setup GameSystems    — adds/wires GugolMapUI on the prefab, retires the
//                             FastTravelMenu hotkey, fixes the pontevecchio
//                             node, sets microclimates, adds teaser nodes.
public static class GugolMappeSetup
{
    const string PrefabPath   = "Assets/Resources/GameSystems.prefab";
    const string MapArtDir    = "Assets/UI/Map";
    const string FontDir      = "Assets/UI/Fonts";
    const string WorkbenchImg = "Tools/asset-gen/output/images";
    const string LiberationPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    // ── 1. Fonts ───────────────────────────────────────────────────────────────

    [MenuItem("InfernosCurse/Gugol Mappe/1. Create TMP Fonts")]
    public static void CreateTmpFonts()
    {
        CreateTmpFont($"{FontDir}/Cinzel.ttf",     $"{FontDir}/Cinzel SDF.asset");
        CreateTmpFont($"{FontDir}/EBGaramond.ttf", $"{FontDir}/EBGaramond SDF.asset");
        AssetDatabase.SaveAssets();
        Debug.Log("[GugolMappeSetup] TMP fonts ready.");
    }

    static void CreateTmpFont(string ttfPath, string assetPath)
    {
        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath) != null)
        {
            Debug.Log($"[GugolMappeSetup] {assetPath} already exists — skipped.");
            return;
        }
        var font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
        if (font == null) { Debug.LogError($"[GugolMappeSetup] TTF not found at {ttfPath}"); return; }

        // Dynamic atlas: glyphs rasterize on demand, no Font Asset Creator GUI pass.
        var tmp = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
            AtlasPopulationMode.Dynamic, true);
        tmp.name = Path.GetFileNameWithoutExtension(assetPath);

        AssetDatabase.CreateAsset(tmp, assetPath);
        tmp.material.name = tmp.name + " Material";
        tmp.atlasTexture.name = tmp.name + " Atlas";
        AssetDatabase.AddObjectToAsset(tmp.material, tmp);
        AssetDatabase.AddObjectToAsset(tmp.atlasTexture, tmp);

        var liberation = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LiberationPath);
        if (liberation != null)
            tmp.fallbackFontAssetTable = new List<TMP_FontAsset> { liberation };

        EditorUtility.SetDirty(tmp);
        Debug.Log($"[GugolMappeSetup] Created {assetPath}");
    }

    // ── 2. Art import (post-approval) ──────────────────────────────────────────

    // Everything except the map background was generated on solid #FF00FF so it
    // can be keyed to alpha here (Gemini can't output transparency).
    static readonly string[] KeyedAssets =
    {
        "gugol-pin", "gugol-you-are-here", "gugol-walker", "gugol-crest",
        "gugol-card-frame", "gugol-searchbar",
        "gugol-icon-star", "gugol-icon-clock", "gugol-icon-sun", "gugol-icon-cloud",
        "gugol-icon-rain", "gugol-icon-fog", "gugol-icon-wind", "gugol-icon-storm",
        "gugol-icon-snow", "gugol-icon-flood", "gugol-town-pin",
    };

    [MenuItem("InfernosCurse/Gugol Mappe/2. Import Map Art (approved workbench PNGs)")]
    public static void ImportMapArt()
    {
        Directory.CreateDirectory(MapArtDir);
        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        string srcDir = Path.Combine(projectRoot, WorkbenchImg);
        int imported = 0;

        // Map backgrounds (city + region + Italy sheets): straight copy, no keying.
        foreach (var bg in new[] { "gugol-map-background", "gugol-region-map", "gugol-italy-map" })
        {
            string bgSrc = Path.Combine(srcDir, bg + ".png");
            if (File.Exists(bgSrc))
            {
                File.Copy(bgSrc, $"{MapArtDir}/{bg}.png", true);
                imported++;
            }
            else Debug.LogWarning($"[GugolMappeSetup] {bg}.png not found in workbench output.");
        }

        foreach (var name in KeyedAssets)
        {
            string src = Path.Combine(srcDir, name + ".png");
            if (!File.Exists(src)) { Debug.LogWarning($"[GugolMappeSetup] {name}.png not found — skipped."); continue; }
            var keyed = KeyMagenta(File.ReadAllBytes(src));
            File.WriteAllBytes($"{MapArtDir}/{name}.png", keyed);
            imported++;
        }

        AssetDatabase.Refresh();

        foreach (var guid in AssetDatabase.FindAssets("gugol-", new[] { MapArtDir }))
            ConfigureSpriteImporter(AssetDatabase.GUIDToAssetPath(guid));

        AssetDatabase.Refresh();
        Debug.Log($"[GugolMappeSetup] Imported {imported} map art files into {MapArtDir}. " +
                  "Re-run '3. Setup GameSystems' to bind them.");
    }

    static byte[] KeyMagenta(byte[] png)
    {
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(png);
        var px = tex.GetPixels();
        int w = tex.width, h = tex.height;

        // Gemini's "pure magenta" never is — sample the ACTUAL background from
        // the four corners and key against that.
        Color key = (px[0] + px[w - 1] + px[(h - 1) * w] + px[h * w - 1]) / 4f;

        for (int i = 0; i < px.Length; i++)
        {
            var c = px[i];
            float d = Mathf.Sqrt((c.r - key.r) * (c.r - key.r)
                               + (c.g - key.g) * (c.g - key.g)
                               + (c.b - key.b) * (c.b - key.b));
            // Magenta chroma: high for the key, negative for parchment/sepia.
            // Distance alone can't separate warm-pink key from warm-cream art
            // (matching red channels) — that mistake tinted the panels green.
            float m = Mathf.Min(c.r, c.b) - c.g;

            if (d < 0.18f)
            {
                px[i] = Color.clear;
            }
            else if (d < 0.45f && m > 0.03f)
            {
                // Anti-aliased rim: observed = fg*t + key*(1-t). Un-mix the
                // key's contribution so edges don't glow pink.
                float t = Mathf.InverseLerp(0.18f, 0.45f, d);
                c.r = Mathf.Clamp01((c.r - key.r * (1f - t)) / Mathf.Max(t, 0.05f));
                c.g = Mathf.Clamp01((c.g - key.g * (1f - t)) / Mathf.Max(t, 0.05f));
                c.b = Mathf.Clamp01((c.b - key.b * (1f - t)) / Mathf.Max(t, 0.05f));
                c.a = t;
                px[i] = c;
            }
        }
        tex.SetPixels(px);
        tex.Apply();

        // Trim the (now transparent) margins: without this, 9-slice borders and
        // Image rects measure from the original canvas edges and panels render
        // as thin strips while glyphs shrink inside their rects.
        var trimmed = TrimTransparent(tex, 4);
        var result = trimmed.EncodeToPNG();
        Object.DestroyImmediate(tex);
        if (trimmed != tex) Object.DestroyImmediate(trimmed);
        return result;
    }

    static Texture2D TrimTransparent(Texture2D tex, int pad)
    {
        var px = tex.GetPixels();
        int w = tex.width, h = tex.height;
        int minX = w, minY = h, maxX = -1, maxY = -1;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (px[y * w + x].a > 0.02f)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
        if (maxX < 0) return tex;   // fully transparent — leave as-is

        minX = Mathf.Max(0, minX - pad);
        minY = Mathf.Max(0, minY - pad);
        maxX = Mathf.Min(w - 1, maxX + pad);
        maxY = Mathf.Min(h - 1, maxY + pad);
        int tw = maxX - minX + 1, th = maxY - minY + 1;
        if (tw <= 0 || th <= 0 || (tw == w && th == h)) return tex;

        var outTex = new Texture2D(tw, th, TextureFormat.RGBA32, false);
        outTex.SetPixels(tex.GetPixels(minX, minY, tw, th));
        outTex.Apply();
        return outTex;
    }

    static void ConfigureSpriteImporter(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;

        string name = Path.GetFileNameWithoutExtension(path);
        bool isSheet = name == "gugol-map-background" || name == "gugol-region-map" || name == "gugol-italy-map";
        importer.maxTextureSize = isSheet ? 2048
                                : name.StartsWith("gugol-icon") ? 256 : 512;

        // Panels stretch: give them 9-slice borders proportional to their size.
        if (name == "gugol-card-frame" || name == "gugol-searchbar")
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex != null)
            {
                float b = Mathf.Min(tex.width, tex.height) * 0.25f;
                importer.spriteBorder = new Vector4(b, b, b, b);
            }
        }

        importer.SaveAndReimport();
    }

    // ── 3. GameSystems wiring ──────────────────────────────────────────────────

    [MenuItem("InfernosCurse/Gugol Mappe/3. Setup GameSystems")]
    public static void SetupGameSystems()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var map = root.GetComponent<GugolMapUI>();
            if (map == null) map = root.AddComponent<GugolMapUI>();

            // Art — approved imports first, project placeholders until then.
            map.mapBackground   = LoadSprite($"{MapArtDir}/gugol-map-background.png", "Assets/Art/Map/FlorenceCity.jpg");
            map.pinSprite       = LoadSprite($"{MapArtDir}/gugol-pin.png",           "Assets/Art/Map/PinMedallion.png");
            map.youAreHereSprite = LoadSprite($"{MapArtDir}/gugol-you-are-here.png");
            map.walkerSprite    = LoadSprite($"{MapArtDir}/gugol-walker.png");
            map.crestSprite     = LoadSprite($"{MapArtDir}/gugol-crest.png");
            map.cardSprite      = LoadSprite($"{MapArtDir}/gugol-card-frame.png");
            map.searchBarSprite = LoadSprite($"{MapArtDir}/gugol-searchbar.png");
            map.iconStar  = LoadSprite($"{MapArtDir}/gugol-icon-star.png");
            map.iconClock = LoadSprite($"{MapArtDir}/gugol-icon-clock.png");
            map.iconSun   = LoadSprite($"{MapArtDir}/gugol-icon-sun.png");
            map.iconCloud = LoadSprite($"{MapArtDir}/gugol-icon-cloud.png");
            map.iconRain  = LoadSprite($"{MapArtDir}/gugol-icon-rain.png");
            map.iconFog   = LoadSprite($"{MapArtDir}/gugol-icon-fog.png");
            map.iconWind  = LoadSprite($"{MapArtDir}/gugol-icon-wind.png");
            map.iconStorm = LoadSprite($"{MapArtDir}/gugol-icon-storm.png");
            map.iconSnow  = LoadSprite($"{MapArtDir}/gugol-icon-snow.png");
            map.iconFlood = LoadSprite($"{MapArtDir}/gugol-icon-flood.png");

            map.headerFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{FontDir}/Cinzel SDF.asset");
            map.bodyFont   = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{FontDir}/EBGaramond SDF.asset");

            // Region + Italy layer art (parchment-color fallback until approved/imported).
            map.regionBackground = LoadSprite($"{MapArtDir}/gugol-region-map.png");
            map.worldBackground  = LoadSprite($"{MapArtDir}/gugol-italy-map.png");
            map.townPinSprite    = LoadSprite($"{MapArtDir}/gugol-town-pin.png");

            // One travel authority: the Gugol map takes M; the legacy list menu
            // stays on the prefab (pause-menu fallback) but loses its hotkey.
            var legacy = root.GetComponent<FastTravelMenu>();
            if (legacy != null)
            {
                legacy.hotkey = Key.None;
                legacy.hotkeyLegacy = KeyCode.None;
            }

            var hub = root.GetComponent<HubMap>();
            if (hub != null) WireNodes(hub);
            else Debug.LogError("[GugolMappeSetup] No HubMap on GameSystems.prefab!");

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("[GugolMappeSetup] GameSystems.prefab wired: GugolMapUI + node data. " +
                      $"map art bound: bg={(map.mapBackground != null)}, pin={(map.pinSprite != null)}, " +
                      $"fonts={(map.headerFont != null)}/{(map.bodyFont != null)}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void WireNodes(HubMap hub)
    {
        // Data fix: the pontevecchio node predates the PonteVecchio scene and
        // shipped with an empty sceneName — it rendered as unreachable.
        var pv = hub.nodeData.Find(n => n.id == "pontevecchio");
        if (pv != null)
        {
            if (string.IsNullOrEmpty(pv.sceneName)) pv.sceneName = "PonteVecchio";
            if (string.IsNullOrEmpty(pv.entryId))   pv.entryId = "ponte_fountain";   // central spot; ponte_east/west also exist
        }

        SetClimate(hub, "pontevecchio", MicroClimate.Riverside);
        SetClimate(hub, "oltrarno",     MicroClimate.Riverside);
        SetClimate(hub, "mercato",      MicroClimate.OpenPiazza);
        SetClimate(hub, "signoria",     MicroClimate.OpenPiazza);
        SetClimate(hub, "duomo",        MicroClimate.Sheltered);

        // Pin positions tuned against gugol-map-background.png (approved 7/04,
        // 1024², parchment sheet). Normalized, x left→right, y bottom→top.
        SetPos(hub, "duomo",        0.56f, 0.73f);   // the dome itself
        SetPos(hub, "sanlorenzo",   0.44f, 0.79f);   // parish NW of the cathedral
        SetPos(hub, "mercato",      0.42f, 0.62f);   // open market blocks SW of the Duomo
        SetPos(hub, "novella",      0.20f, 0.55f);   // western quarter by the walls
        SetPos(hub, "signoria",     0.82f, 0.24f);   // fortress palace with the tall tower
        SetPos(hub, "santacroce",   0.87f, 0.33f);   // east quarter above Signoria
        SetPos(hub, "pontevecchio", 0.34f, 0.20f);   // the covered bridge
        SetPos(hub, "oltrarno",     0.25f, 0.09f);   // south-bank strip

        AddTeaserNode(hub, new HubNodeData
        {
            id = "santacroce",
            displayName = "Santa Croce",
            mapImagePosition = new Vector2(0.78f, 0.38f),
            blurb = "The Franciscan basilica rises over the dyers' quarter, its unfinished " +
                    "façade watching workshops stain the Arno every shade of penance.",
            population = 0.5f,
            neighborIds = new List<string> { "signoria" },
        });

        AddTeaserNode(hub, new HubNodeData
        {
            id = "sanlorenzo",
            displayName = "San Lorenzo",
            mapImagePosition = new Vector2(0.47f, 0.86f),
            blurb = "The old market parish north of the Duomo — stalls crowd one of the " +
                    "city's oldest churches, and the Medici pews sit conspicuously near the altar.",
            population = 0.65f,
            neighborIds = new List<string> { "duomo", "mercato", "novella" },
        });

        // Hillside district above the Arno — first zone on the Terrain
        // pipeline (Stylized Water 3 / Grass Shader / Vegetation Spawner /
        // Terrain Painter). Gated: quest flag unlocks the pin per
        // docs/superpowers/specs/2026-07-05-giardino-delle-rose-design.md
        // (unlock trigger itself not yet implemented — sceneName landing in
        // Build Settings makes it VISIBLE; a separate quest system will need
        // to gate visibility further once it exists, same as MapRouting's
        // existing IsUnlocked/IsVisible split).
        AddTeaserNode(hub, new HubNodeData
        {
            id = "giardino_rose",
            displayName = "Giardino delle Rose",
            sceneName = "GiardinoDelleRose",
            entryId = "giardino_gate",
            microClimate = MicroClimate.Hilltop,
            mapImagePosition = new Vector2(0.86f, 0.20f),   // hillside above Signoria, overlooking the Arno
            blurb = "Terraced rose beds climb the hillside above the river, tended (they " +
                    "say) by someone who still remembers what the flowers are for.",
            population = 0.15f,
            startingCurseLevel = 0.05f,
            startingSanctity = 0.6f,
            neighborIds = new List<string> { "signoria", "oltrarno" },
        });

        // ── Region layer (FFT overworld) ─────────────────────────────────────
        // City nodes deserialize kind=District (enum default 0) — correct as-is.

        AddTeaserNode(hub, new HubNodeData
        {
            id = "firenze",
            displayName = "Firenze",
            kind = NodeKind.City,
            mapLevel = MapLevel.Region,
            mapImagePosition = new Vector2(0.54f, 0.56f),   // the walled city, gugol-region-map.png
            blurb = "The city itself — wool, florins, factions, and a curse working " +
                    "through its parishes. Zoom in to walk its districts.",
            population = 1.0f,
            neighborIds = new List<string> { "wp_mugnone" },
        });

        AddTeaserNode(hub, new HubNodeData
        {
            id = "wp_mugnone",
            displayName = "Ponte a Mugnone",
            kind = NodeKind.Waypoint,
            mapLevel = MapLevel.Region,
            mapImagePosition = new Vector2(0.58f, 0.635f),  // on the NE road out of the city
            population = 0.05f,
            // sanlorenzo edge = the curse's road out of the city (diffusion bridge).
            neighborIds = new List<string> { "firenze", "fiesole", "sanlorenzo" },
        });

        AddTeaserNode(hub, new HubNodeData
        {
            id = "fiesole",
            displayName = "Fiesole",
            kind = NodeKind.Town,
            mapLevel = MapLevel.Region,
            sceneName = "Fiesole",
            entryId = "fiesole_gate",
            microClimate = MicroClimate.Hilltop,
            mapImagePosition = new Vector2(0.62f, 0.71f),   // the hilltop hamlet NE of the city
            blurb = "The old Etruscan town on its hill, older than Florence and " +
                    "quietly proud of it — cypress lanes, a crumbling amphitheatre, " +
                    "and the whole valley spread below.",
            population = 0.3f,
            neighborIds = new List<string> { "wp_mugnone" },
        });

        // ── Italy level (the Nine Circles canvas — deliberately empty) ──────
        // Geography only on the sheet; future circle-locations (GameBible §Nine
        // Circles, mostly TBD) land here as data: Town nodes with scenes, or
        // City gateways opening their own region sheets, plus generated map
        // vignettes composited at their coords when David picks each location.
        AddTeaserNode(hub, new HubNodeData
        {
            id = "toscana",
            displayName = "Toscana",
            kind = NodeKind.City,
            mapLevel = MapLevel.World,
            mapImagePosition = new Vector2(0.43f, 0.64f),   // the Arno valley, gugol-italy-map.png
            blurb = "Wool towns, hill roads, and the Arno valley — and beneath " +
                    "its quiet days, the first circle of something worse.",
            population = 1.0f,
        });

        // Re-runs on a prefab that predates a field addition must still land
        // the kinds/levels/positions (AddTeaserNode skips existing ids).
        SetKind(hub, "firenze", NodeKind.City);
        SetKind(hub, "wp_mugnone", NodeKind.Waypoint);
        SetKind(hub, "toscana", NodeKind.City);
        SetPos(hub, "firenze",    0.54f, 0.56f);    // tuned to gugol-region-map.png
        SetPos(hub, "wp_mugnone", 0.58f, 0.635f);
        SetPos(hub, "fiesole",    0.62f, 0.71f);
        SetPos(hub, "toscana",    0.43f, 0.64f);    // tuned to gugol-italy-map.png

        // MapLevel migration — EXPLICIT ids only (a blanket kind-based sweep
        // would stomp future world-level Towns back to Region on re-runs).
        foreach (var n in hub.nodeData)
            if (n.kind == NodeKind.District) n.mapLevel = MapLevel.City;
        SetLevel(hub, "firenze",    MapLevel.Region);
        SetLevel(hub, "wp_mugnone", MapLevel.Region);
        SetLevel(hub, "fiesole",    MapLevel.Region);
        SetLevel(hub, "toscana",    MapLevel.World);

        var fs = hub.nodeData.Find(n => n.id == "fiesole");
        if (fs != null)
        {
            fs.kind = NodeKind.Town;
            if (string.IsNullOrEmpty(fs.sceneName)) fs.sceneName = "Fiesole";
            if (string.IsNullOrEmpty(fs.entryId))   fs.entryId = "fiesole_gate";
            fs.microClimate = MicroClimate.Hilltop;
        }

        ConfigureCircleTerritories(hub);
    }

    static void ConfigureCircleTerritories(HubMap hub)
    {
        ConfigureOwner(hub, "firenze", TerritoryKind.City, "toscana", 8f, 0.08f);
        ConfigureOwner(hub, "fiesole", TerritoryKind.Town, "toscana", 1f, 0.05f);

        string[] florenceSites =
        {
            "duomo", "novella", "mercato", "signoria", "pontevecchio", "oltrarno",
            "santacroce", "sanlorenzo", "giardino_rose", "salone_arti", "via_calimala",
        };
        foreach (string siteId in florenceSites)
            ConfigureSite(hub, siteId, "firenze");

        var waypoint = hub.nodeData.Find(node => node.id == "wp_mugnone");
        if (waypoint != null)
        {
            ClearTerritoryContract(waypoint);
            waypoint.nonStateNode = true;
            waypoint.startingInfluences.Clear();
            waypoint.startingCurseLevel = 0f;
        }

        var region = hub.nodeData.Find(node => node.id == "toscana");
        if (region != null)
        {
            ClearTerritoryContract(region);
            region.aggregateOnly = true;
            region.startingInfluences.Clear();
            region.startingCurseLevel = 0f;
        }

        SetRouteStrength(hub, "firenze", "wp_mugnone", 0.75f);
        SetRouteStrength(hub, "fiesole", "wp_mugnone", 0.75f);
    }

    static void ConfigureOwner(
        HubMap hub,
        string id,
        TerritoryKind kind,
        string parentRegionId,
        float regionalWeight,
        float limboBaseline)
    {
        var node = hub.nodeData.Find(candidate => candidate.id == id);
        if (node == null) return;
        ClearTerritoryContract(node);
        node.ownsCircleState = true;
        node.territoryKind = kind;
        node.parentRegionId = parentRegionId;
        node.regionalWeight = regionalWeight;
        node.nativeCircle = CircleId.Limbo;
        node.startingCurseLevel = 0f;
        node.startingInfluences = new List<CircleInfluenceSeed>
        {
            new CircleInfluenceSeed { circle = CircleId.Limbo, value = limboBaseline },
        };
    }

    static void ConfigureSite(HubMap hub, string id, string ownerId)
    {
        var node = hub.nodeData.Find(candidate => candidate.id == id);
        if (node == null) return;
        ClearTerritoryContract(node);
        node.influenceTerritoryId = ownerId;
        node.nativeCircle = CircleId.Limbo;
        node.startingInfluences.Clear();
        node.startingCurseLevel = 0f;
    }

    static void ClearTerritoryContract(HubNodeData node)
    {
        node.influenceTerritoryId = string.Empty;
        node.parentRegionId = string.Empty;
        node.territoryKind = TerritoryKind.None;
        node.regionalWeight = 0f;
        node.ownsCircleState = false;
        node.aggregateOnly = false;
        node.nonStateNode = false;
        node.routeStrengthOverrides ??= new List<CircleRouteStrength>();
    }

    static void SetRouteStrength(HubMap hub, string id, string neighborId, float strength)
    {
        var node = hub.nodeData.Find(candidate => candidate.id == id);
        if (node == null) return;
        node.routeStrengthOverrides ??= new List<CircleRouteStrength>();
        var route = node.routeStrengthOverrides.Find(candidate => candidate.neighborId == neighborId);
        if (route == null)
        {
            route = new CircleRouteStrength { neighborId = neighborId };
            node.routeStrengthOverrides.Add(route);
        }
        route.strength = Mathf.Clamp01(strength);
    }

    static void SetLevel(HubMap hub, string id, MapLevel level)
    {
        var node = hub.nodeData.Find(n => n.id == id);
        if (node != null) node.mapLevel = level;
    }

    static void SetKind(HubMap hub, string id, NodeKind kind)
    {
        var node = hub.nodeData.Find(n => n.id == id);
        if (node != null) node.kind = kind;
    }

    static void SetClimate(HubMap hub, string id, MicroClimate climate)
    {
        var node = hub.nodeData.Find(n => n.id == id);
        if (node != null) node.microClimate = climate;
    }

    static void SetPos(HubMap hub, string id, float x, float y)
    {
        var node = hub.nodeData.Find(n => n.id == id);
        if (node != null) node.mapImagePosition = new Vector2(x, y);
    }

    static void AddTeaserNode(HubMap hub, HubNodeData node)
    {
        if (hub.nodeData.Exists(n => n.id == node.id)) return;   // idempotent
        hub.nodeData.Add(node);
        Debug.Log($"[GugolMappeSetup] Added teaser node '{node.id}'.");
    }

    static Sprite LoadSprite(string path, string fallback = null)
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s == null && fallback != null) s = AssetDatabase.LoadAssetAtPath<Sprite>(fallback);
        return s;
    }
}
