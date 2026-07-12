using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using TMPro;

// "Gugol Mappe" — the Google-Maps-parody world map of Florence, 1348.
// Full-screen self-building overlay in the house Pattern-B style
// (FastTravelMenu / GuildPanelUI): lives on the GameSystems prefab, builds its
// Canvas in Awake, pauses the game while open, works in every scene with zero
// wiring. Replaces the WorldMap scene as the travel surface: hotkey M or a
// ZoneExit opens it in place; ESC returns the player exactly where they stood.
//
// Travel: click a pin → dotted route (BFS through HubMap neighbors, like
// street directions) + directions card → "Vai" → the little walker animates
// down the route (unscaled time), the game clock advances by the parody
// walking minutes × multiplier, then the destination scene loads via the
// existing TravelIntent/ZoneEntryPlacer pipeline (ported from WorldMapUI).
public class GugolMapUI : MonoBehaviour
{
    [Header("Open / Close")]
    public Key hotkey = Key.M;
    public KeyCode hotkeyLegacy = KeyCode.M;
    public bool pauseWhileOpen = true;
    public string[] blockedScenes = { "WorldMap", "MainMenu", "Battle", "BattleArena" };

    [Header("Art — assigned by InfernosCurse/Gugol Mappe/Setup GameSystems")]
    public Sprite mapBackground;
    public Sprite pinSprite;
    public Sprite youAreHereSprite;
    public Sprite walkerSprite;
    public Sprite crestSprite;
    public Sprite cardSprite;        // 9-slice parchment panel
    public Sprite searchBarSprite;   // 9-slice parchment lozenge
    public Sprite iconStar, iconClock;
    public Sprite iconSun, iconCloud, iconRain, iconFog, iconWind, iconStorm, iconSnow, iconFlood;
    public Sprite fallbackPreview;
    [Tooltip("Optional project-owned presentation profile. Existing serialized art remains the fallback.")]
    public GugolMapPresentationProfile presentationProfile;

    [Header("Fonts (Cinzel headers / EB Garamond body; TMP default when null)")]
    public TMP_FontAsset headerFont;
    public TMP_FontAsset bodyFont;

    [Header("Travel")]
    [Tooltip("Directions-card walking minutes per normalized map unit.")]
    public float minutesPerMapUnit = 45f;
    [Tooltip("Game-clock hours advanced = walk minutes × this ÷ 60.")]
    public float gameTimeMultiplier = 4f;
    [Tooltip("Seconds the walker takes to animate down the route.")]
    public float walkDuration = 1.5f;
    public string playerTag = "Player";
    [Tooltip("Log normalized map coords on background clicks (pin re-tuning aid).")]
    public bool debugLogClickCoords = false;

    [Header("Region Layer — assigned by Setup GameSystems")]
    public Sprite regionBackground;
    [Tooltip("The Italy sheet — the Nine Circles canvas (parchment fallback until art).")]
    public Sprite worldBackground;
    [Tooltip("Pin art for region towns; falls back to the city pin sprite.")]
    public Sprite townPinSprite;

    [Header("Region Travel")]
    [Tooltip("Road hours per normalized region-map unit (drives both time and fare).")]
    public float regionHoursPerMapUnit = 50f;
    [Tooltip("Cart fare in florins per road hour (min fare 1).")]
    public float florinsPerTravelHour = 1.3f;
    [Tooltip("Journeys up to this many hours arrive same day; longer ones take days and arrive at morning.")]
    public float sameDayTravelThresholdHours = 12f;
    [Tooltip("Road hours that make up one travel day.")]
    public float travelHoursPerDay = 12f;
    [Tooltip("Seconds the walker takes to animate a region route.")]
    public float regionWalkDuration = 2.5f;

    [Header("Road Encounters")]
    [Tooltip("Base ambush chance per waypoint crossed.")]
    public float baseEncounterChance = 0.15f;
    [Tooltip("Added chance per point of the waypoint's curse level.")]
    public float curseChanceScale = 0.5f;
    [Tooltip("Ambush chance ceiling.")]
    public float maxEncounterChance = 0.9f;
    public string battleSceneName = "BattleArena";

    // ── Runtime ────────────────────────────────────────────────────────────────
    Canvas _canvas;
    GameObject _root;
    RectTransform _pinLayer;         // pins + route both position in this rect
    Image _mapImg;
    AspectRatioFitter _fitter;
    GugolRouteRenderer _route;
    GugolDirectionsCard _card;
    TMP_InputField _search;
    TextMeshProUGUI _searchPlaceholder;
    Button _zoomInBtn, _zoomOutBtn;
    Button _cityBtn, _regionBtn, _worldBtn;
    MapLevel _layer = MapLevel.City;
    RectTransform _featureLayer;
    RectTransform _chromeLayer;
    AspectRatioFitter _chromeFitter;
    GugolMapLayerPresenter _layerPresenter;
    GugolMapFeaturePresenter _featurePresenter;
    GugolMapWeatherPresenter _weatherPresenter;
    GugolMapCardHost _contextCard;
    GugolMapKnowledgeSnapshot _knowledge;
    GugolMapSearchIndex _searchIndex;
    readonly GugolMapSelectionState _selectionState = new();

    readonly Dictionary<string, GugolMapPin> _pins = new();
    readonly List<GameObject> _waypointDots = new();
    GugolMapPin _selected;
    HubMap _hub;
    ZoneExit _sourceExit;            // set when a ZoneExit opened us (re-armed on cancel)
    bool _open;
    bool _travelling;
    float _savedTimeScale = 1f;

    static GugolMapUI _instance;
    public static GugolMapUI Instance
    {
        get => _instance != null ? _instance : (_instance = FindAnyObjectByType<GugolMapUI>());
        private set => _instance = value;
    }

    public static bool IsOpen => _instance != null && _instance._open;
    public GugolMapViewKind CurrentView => _selectionState.View;
    public string FocusedStreetId => _selectionState.FocusedStreetId;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        if (presentationProfile == null)
            presentationProfile = Resources.Load<GugolMapPresentationProfile>(
                "GugolMap/Presentation/FlorenceRefined");
        ApplyPresentationProfileFallbacks();
        BuildUI();
        CloseInternal(rearmExit: false);
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void ApplyPresentationProfileFallbacks()
    {
        if (presentationProfile == null) return;
        mapBackground ??= presentationProfile.cityBackground;
        regionBackground ??= presentationProfile.regionBackground;
        worldBackground ??= presentationProfile.worldBackground;
        pinSprite ??= presentationProfile.defaultPin;
        townPinSprite ??= presentationProfile.townPin;
        youAreHereSprite ??= presentationProfile.playerMarker;
        walkerSprite ??= presentationProfile.routeWalker;
        cardSprite ??= presentationProfile.contextCard;
        searchBarSprite ??= presentationProfile.searchBar;
    }

    void Update()
    {
        // While the player is typing in the search bar, letters are letters.
        bool typing = _search != null && _search.isFocused;

        bool pressed = !typing &&
            ((Keyboard.current != null && hotkey != Key.None && Keyboard.current[hotkey].wasPressedThisFrame)
             || Input.GetKeyDown(hotkeyLegacy));
        if (pressed) Toggle();

        if (_open)
        {
            bool cancel = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                       || Input.GetKeyDown(KeyCode.Escape);
            if (cancel)
            {
                if (_travelling)      _route.SkipToEnd();   // fast-forward, never cancel mid-route
                else if (typing)      { /* TMP eats ESC to unfocus — swallow */ }
                else if (_contextCard != null && _contextCard.IsOpen) _contextCard.Hide();
                else if (_card.IsOpen) _card.Hide();
                else if (_selectionState.View == GugolMapViewKind.Street) ExitStreetView();
                else Close();
            }
        }
    }

    // ── Open / close ───────────────────────────────────────────────────────────

    public void Toggle() { if (_open) Close(); else Open(); }

    public bool CanOpenHere()
        => !_open && !_travelling && !IsBlockedScene() && HubMap.Instance != null;

    public bool Open()
    {
        if (_open || _travelling) return false;
        if (IsBlockedScene())
        {
            Debug.Log("[GugolMapUI] The map isn't available in this scene.");
            return false;
        }
        _hub = HubMap.Instance;
        if (_hub == null)
        {
            Debug.LogError("[GugolMapUI] HubMap.Instance is null — cannot open the map.");
            return false;
        }
        _hub.EnsureGraphBuilt();
        RebuildKnowledgeSnapshot();

        EnsureEventSystem();
        // Activate BEFORE building pins: TMP components under an inactive root
        // haven't awakened, and touching e.g. outlineWidth then NREs inside TMP.
        _root.SetActive(true);

        // Open on the sheet the player is actually on: a Florence district
        // shows the city sheet; Fiesole (or any region location) the region one.
        var curNode = _hub.GetNode(DistrictTracker.CurrentNodeId);
        SetLayer(curNode?.mapLevel ?? MapLevel.City);

        _hub.OnNodeChanged -= OnNodeChanged;
        _hub.OnNodeChanged += OnNodeChanged;
        GugolNpcMapKnowledgeLedger.OnChanged -= OnMapKnowledgeChanged;
        GugolNpcMapKnowledgeLedger.OnChanged += OnMapKnowledgeChanged;

        _open = true;
        if (pauseWhileOpen)
        {
            _savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }
        return true;
    }

    // A ZoneExit walked into at a district edge opens the map; if the player
    // cancels instead of traveling, that exit is re-armed on close.
    public bool OpenFromZoneExit(ZoneExit source)
    {
        if (!Open()) return false;
        _sourceExit = source;
        return true;
    }

    public void Close()
    {
        if (_travelling) return;   // ESC during the walk skips instead (Update)
        CloseInternal(rearmExit: true);
    }

    void CloseInternal(bool rearmExit)
    {
        if (_root != null) _root.SetActive(false);
        if (_open && pauseWhileOpen) Time.timeScale = _savedTimeScale;
        _open = false;
        if (_hub != null) _hub.OnNodeChanged -= OnNodeChanged;
        GugolNpcMapKnowledgeLedger.OnChanged -= OnMapKnowledgeChanged;
        _featurePresenter?.Clear();

        if (rearmExit && _sourceExit != null) _sourceExit.RearmAfterMapClosed();
        _sourceExit = null;
    }

    bool IsBlockedScene()
    {
        string scene = SceneManager.GetActiveScene().name;
        foreach (var s in blockedScenes)
            if (scene == s) return true;
        return false;
    }

    // Scenes built by script never got an EventSystem, so overlay buttons
    // silently ignored clicks — the old "donation ui freeze". Copied from
    // GuildPanelUI: any UI we open guarantees one exists.
    static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        if (FindAnyObjectByType<EventSystem>() != null) return;
        new GameObject("[EventSystem]",
            typeof(EventSystem),
            typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
    }

    // ── Layers ─────────────────────────────────────────────────────────────────

    static MapLevel LayerOf(HubNode node) => node.mapLevel;

    Sprite BackgroundFor(MapLevel level)
    {
        if (presentationProfile != null)
        {
            Sprite profileSprite = level switch
            {
                MapLevel.City => presentationProfile.cityBackground,
                MapLevel.Region => presentationProfile.regionBackground,
                _ => presentationProfile.worldBackground,
            };
            if (profileSprite != null) return profileSprite;
        }
        switch (level)
        {
            case MapLevel.City:   return mapBackground;
            case MapLevel.Region: return regionBackground;
            default:              return worldBackground;
        }
    }

    static string SearchHintFor(MapLevel level)
    {
        switch (level)
        {
            case MapLevel.City:   return "Search Florence...";
            case MapLevel.Region: return "Search Tuscany...";
            default:              return "Search Italy...";
        }
    }

    // The Google-zoom parody: one map, a stack of parchment sheets. Swaps the
    // background, rebuilds pins for the level, clears route/card/search state.
    void SetLayer(MapLevel layer)
    {
        if (_travelling) return;
        _layer = layer;
        _selectionState.SetBase(layer);

        var bg = BackgroundFor(layer);
        if (_layerPresenter != null) _layerPresenter.ShowBase(layer, bg, immediate: !_open);
        else
        {
        if (bg != null) { _mapImg.sprite = bg; _mapImg.color = Color.white; }
        else            { _mapImg.sprite = null; _mapImg.color = new Color(0.89f, 0.83f, 0.70f, 1f); }
        _fitter.aspectRatio = bg != null ? bg.rect.width / bg.rect.height : 1f;

        // The fitter must resize the map rect BEFORE pins read _pinLayer.rect
        // — a stale rect scatters every pin. Toggling re-runs UpdateRect now.
        _fitter.enabled = false;
        _fitter.enabled = true;
        Canvas.ForceUpdateCanvases();
        }
        UpdateChromeAspect(bg);

        RebuildPins();
        if (layer == MapLevel.City) _featurePresenter?.ShowCityStreets(_knowledge);
        else _featurePresenter?.Clear();
        _route.Clear();
        _card.Hide();
        _contextCard?.Hide();
        if (_search != null) _search.SetTextWithoutNotify("");
        if (_searchPlaceholder != null) _searchPlaceholder.text = SearchHintFor(layer);
        SetAllDimmed(null);
        RefreshZoomButtons();
    }

    void RefreshZoomButtons()
    {
        if (_zoomInBtn  != null) _zoomInBtn.interactable  = _layer > MapLevel.City  && !_travelling;
        if (_zoomOutBtn != null) _zoomOutBtn.interactable = _layer < MapLevel.World && !_travelling;
        SetScaleButtonState(_cityBtn, _selectionState.View == GugolMapViewKind.City);
        SetScaleButtonState(_regionBtn, _selectionState.View == GugolMapViewKind.Region);
        SetScaleButtonState(_worldBtn, _selectionState.View == GugolMapViewKind.World);
    }

    void SetScaleButtonState(Button button, bool selected)
    {
        if (button == null) return;
        button.interactable = !_travelling;
        if (button.targetGraphic is Image image)
            image.color = selected
                ? new Color(0.55f, 0.16f, 0.12f, 0.98f)
                : new Color(0.94f, 0.90f, 0.80f, 0.97f);
        var label = button.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null) label.color = selected ? Color.white : new Color(0.30f, 0.23f, 0.13f);
    }

    void UpdateChromeAspect(Sprite background)
    {
        if (_chromeFitter == null) return;
        _chromeFitter.aspectRatio = background != null ? background.rect.width / background.rect.height : 1f;
        _chromeFitter.enabled = false;
        _chromeFitter.enabled = true;
    }

    // ── Pins ───────────────────────────────────────────────────────────────────

    void RebuildPins()
    {
        foreach (var p in _pins.Values) if (p != null) Destroy(p.gameObject);
        _pins.Clear();
        foreach (var d in _waypointDots) if (d != null) Destroy(d);
        _waypointDots.Clear();
        _selected = null;
        if (_selectionState.View == GugolMapViewKind.Street) return;

        var weather = FlorenceWeather.Instance;
        var cur = _hub.GetNode(DistrictTracker.CurrentNodeId);

        foreach (var node in _hub.AllNodes)
        {
            if (LayerOf(node) != _layer) continue;

            // Autohide: a POI appears the moment its scene ships in Build
            // Settings (IsVisible generalizes the rule to waypoints/the city pin).
            if (!MapRouting.IsVisible(node)) continue;

            if (node.kind == NodeKind.Waypoint)
            {
                _waypointDots.Add(BuildWaypointDot(node));
                continue;
            }

            var pin = BuildPin(node);
            _pins[node.id] = pin;

            string cond = weather != null ? weather.ConditionForNode(node) : "clear";
            bool flood = FlorenceWeather.FloodRiskToday && node.microClimate == MicroClimate.Riverside;
            pin.SetWeather(GlyphFor(cond), flood);

            bool here;
            switch (_layer)
            {
                case MapLevel.City:
                    here = node.id == DistrictTracker.CurrentNodeId;
                    break;
                case MapLevel.Region:
                    here = node.id == MapRouting.RegionAnchorId(cur);
                    break;
                default:
                    // Everything playable sits in Tuscany today — the gateway
                    // pin carries the dot. Revisit when a second region exists.
                    here = node.kind == NodeKind.City;
                    break;
            }
            pin.SetYouAreHere(here);
        }
    }

    // FFT-style road dot: a future encounter hook, never clickable.
    GameObject BuildWaypointDot(HubNode node)
    {
        var dot = GugolUi.MakeImage(_pinLayer, "Waypoint_" + node.id, GugolUi.CircleSprite,
            new Color(0.35f, 0.28f, 0.18f, 0.9f));
        var rt = dot.rectTransform;
        PositionPin(rt, node.mapImagePosition);
        rt.sizeDelta = new Vector2(14f, 14f);
        return dot.gameObject;
    }

    GugolMapPin BuildPin(HubNode node)
    {
        var go = new GameObject("Pin_" + node.id, typeof(RectTransform));
        go.transform.SetParent(_pinLayer, false);
        PositionPin((RectTransform)go.transform, node.mapImagePosition);

        var pin = go.AddComponent<GugolMapPin>();

        // Gold selection halo behind the pin.
        var halo = GugolUi.MakeImage(go.transform, "Halo", GugolUi.CircleSprite,
            new Color(0.95f, 0.85f, 0.55f, 0.65f));
        halo.rectTransform.anchoredPosition = new Vector2(0f, 22f);
        halo.rectTransform.sizeDelta = new Vector2(58f, 58f);

        // The teardrop itself: tip sits on the location point. Region towns get
        // the blue town seal when it exists; the city pin stays wax red.
        var teardrop = node.kind == NodeKind.Town && townPinSprite != null ? townPinSprite : pinSprite;
        var icon = GugolUi.MakeImage(go.transform, "PinIcon",
            teardrop != null ? teardrop : GugolUi.CircleSprite, Color.white, raycast: true);
        icon.rectTransform.anchoredPosition = new Vector2(0f, 22f);
        icon.rectTransform.sizeDelta = teardrop != null ? new Vector2(42f, 54f) : new Vector2(30f, 30f);
        icon.preserveAspect = true;

        var label = GugolUi.MakeText(go.transform, node.displayName, 19, FontStyles.Bold,
            new Color(0.25f, 0.18f, 0.10f), bodyFont);
        label.alignment = TextAlignmentOptions.Center;
        label.rectTransform.anchoredPosition = new Vector2(0f, -16f);
        label.rectTransform.sizeDelta = new Vector2(190f, 26f);
        // Parchment-readable outline so labels survive any map art behind them.
        label.outlineWidth = 0.26f;
        label.outlineColor = new Color32(245, 238, 220, 255);

        var weatherIcon = GugolUi.MakeImage(go.transform, "Weather", null, Color.white);
        weatherIcon.rectTransform.anchoredPosition = new Vector2(26f, 40f);
        weatherIcon.rectTransform.sizeDelta = new Vector2(22f, 22f);
        weatherIcon.preserveAspect = true;
        weatherIcon.enabled = false;

        var flood = GugolUi.MakeImage(go.transform, "FloodBadge",
            iconFlood != null ? iconFlood : GugolUi.CircleSprite,
            iconFlood != null ? Color.white : new Color(0.85f, 0.30f, 0.15f));
        flood.rectTransform.anchoredPosition = new Vector2(-26f, 40f);
        flood.rectTransform.sizeDelta = new Vector2(20f, 20f);
        flood.preserveAspect = true;
        flood.enabled = false;

        var here = GugolUi.MakeImage(go.transform, "YouAreHere",
            youAreHereSprite != null ? youAreHereSprite : GugolUi.CircleSprite,
            youAreHereSprite != null ? Color.white : new Color(0.26f, 0.52f, 0.96f));
        here.rectTransform.anchoredPosition = Vector2.zero;   // on the exact location point
        here.rectTransform.sizeDelta = new Vector2(22f, 22f);
        here.preserveAspect = true;
        here.enabled = false;

        var tooltip = GugolUi.MakeText(go.transform, "Closed", 17, FontStyles.Italic,
            new Color(0.45f, 0.20f, 0.15f), bodyFont);
        tooltip.alignment = TextAlignmentOptions.Center;
        tooltip.rectTransform.anchoredPosition = new Vector2(0f, 58f);
        tooltip.rectTransform.sizeDelta = new Vector2(120f, 22f);
        tooltip.gameObject.SetActive(false);

        pin.pinIcon = icon;
        pin.label = label;
        pin.selectedOutline = halo;
        pin.weatherIcon = weatherIcon;
        pin.floodBadge = flood;
        pin.youAreHereDot = here;
        pin.lockedTooltip = tooltip;
        // The city pin has no scene of its own but must never render locked —
        // it's the zoom-in gateway, not a destination.
        pin.Bind(node, this, node.kind == NodeKind.City || MapRouting.IsUnlocked(node));
        return pin;
    }

    // Place a pin at a normalized (0-1) position within the map rect.
    // Ported verbatim from WorldMapUI.PositionPin.
    void PositionPin(RectTransform pinRect, Vector2 normalized)
    {
        if (pinRect == null || _pinLayer == null) return;
        var size = _pinLayer.rect.size;
        pinRect.anchorMin = pinRect.anchorMax = new Vector2(0f, 0f);
        pinRect.pivot = new Vector2(0.5f, 0.5f);
        pinRect.anchoredPosition = new Vector2(normalized.x * size.x, normalized.y * size.y);
    }

    Sprite GlyphFor(string condition)
    {
        switch (condition)
        {
            case "fog":   return iconFog;
            case "rain":  return iconRain;
            case "sleet": return iconRain;
            case "storm": return iconStorm;
            case "hail":  return iconStorm;
            case "wind":  return iconWind;
            case "snow":  return iconSnow;
            case "cloud": return iconCloud;
            default:      return iconSun;
        }
    }

    void OnNodeChanged(HubNode node)
    {
        if (node != null && _pins.TryGetValue(node.id, out var pin)) pin.Refresh();
    }

    void RebuildKnowledgeSnapshot()
    {
        _knowledge = GugolMapKnowledgeService.Build(_hub);
        _searchIndex = new GugolMapSearchIndex(_knowledge);
        _weatherPresenter?.Apply(_knowledge);
    }

    void OnMapKnowledgeChanged()
    {
        if (!_open) return;
        RebuildKnowledgeSnapshot();
        if (_selectionState.View == GugolMapViewKind.Street &&
            _knowledge.TryGet(GugolMapFeatureKind.Street, _selectionState.FocusedStreetId, out var street))
            _featurePresenter?.ShowStreet(_knowledge, street.street);
        else if (_layer == MapLevel.City)
            _featurePresenter?.ShowCityStreets(_knowledge);
    }

    public void OnStreetClicked(GugolStreetDefinition street)
    {
        if (_travelling || street == null || _knowledge == null) return;
        if (!_knowledge.TryGet(GugolMapFeatureKind.Street, street.streetId, out var feature) || !feature.IsVisible)
            return;

        _selectionState.EnterStreet(street.streetId);
        _layer = MapLevel.City;
        _card.Hide();
        _contextCard?.Hide();
        _route.Clear();
        RebuildPins();
        _layerPresenter?.ShowStreet(street, immediate: false);
        UpdateChromeAspect(street.streetViewBackground != null
            ? street.streetViewBackground
            : BackgroundFor(MapLevel.City));
        Canvas.ForceUpdateCanvases();
        _featurePresenter?.ShowStreet(_knowledge, street);
        _contextCard?.ShowStreet(feature);
        RefreshZoomButtons();
        if (_searchPlaceholder != null) _searchPlaceholder.text = "Search this street...";
    }

    void ExitStreetView()
    {
        SetLayer(MapLevel.City);
    }

    public void OnMapFeatureClicked(GugolMapFeatureRecord feature)
    {
        if (_travelling || feature == null || !feature.IsVisible) return;
        _selectionState.Select(feature.kind, feature.featureId);
        switch (feature.kind)
        {
            case GugolMapFeatureKind.Street:
                OnStreetClicked(feature.street);
                break;
            case GugolMapFeatureKind.Venue:
                _contextCard?.ShowVenue(feature, () => ShowDirectionsToFeature(feature), null);
                break;
            case GugolMapFeatureKind.Npc:
                _contextCard?.ShowNpc(feature, () => ShowDirectionsToFeature(feature));
                break;
        }
    }

    void ShowDirectionsToFeature(GugolMapFeatureRecord feature)
    {
        if (feature == null || _knowledge == null ||
            !_knowledge.TryGet(GugolMapFeatureKind.Street, feature.streetId, out var streetFeature) ||
            streetFeature.street == null) return;

        Vector2 destination = streetFeature.street.routeFallbackPosition;
        if (!string.IsNullOrWhiteSpace(feature.venueId) &&
            _knowledge.TryGet(GugolMapFeatureKind.Venue, feature.venueId, out var venueFeature) &&
            venueFeature.venue != null)
            destination = venueFeature.venue.streetViewAnchor;
        else if (feature.venue != null)
            destination = feature.venue.streetViewAnchor;

        _route.ShowNormalizedRoute(new List<Vector2>
        {
            streetFeature.street.routeFallbackPosition,
            destination,
        });
    }

    public void OnContextCardHidden()
    {
        _selectionState.ClearSelection();
    }

    // ── Selection / routing ────────────────────────────────────────────────────

    public void OnPinClicked(GugolMapPin pin)
    {
        if (_travelling || pin == null || pin.Node == null) return;

        var node = pin.Node;

        // Road dots are scenery (belt-and-braces — they aren't raycast targets).
        if (node.kind == NodeKind.Waypoint) return;

        // Gateway pins (Firenze on the region sheet, Toscana on the Italy
        // sheet) always zoom one level in — never a travel card.
        if (node.kind == NodeKind.City)
        {
            if (node.mapLevel > MapLevel.City)
                SetLayer(node.mapLevel - 1);
            return;
        }

        string curId = DistrictTracker.CurrentNodeId;
        var cur = _hub.GetNode(curId);

        if (node.id == curId)
        {
            Select(pin);
            _route.Clear();
            _card.ShowCurrent(node, CollectNearbySpots(), JumpToSpot);
            return;
        }

        // Crossing region anchors (leaving/entering the city) = road travel on
        // the region sheet; same anchor = the familiar city walk.
        string curAnchor = MapRouting.RegionAnchorId(cur);
        string dstAnchor = MapRouting.RegionAnchorId(node);
        if (curAnchor != dstAnchor)
        {
            SelectRegionDestination(node, cur, curAnchor, dstAnchor);
            return;
        }

        Select(pin);
        var path = MapRouting.FindPath(_hub, curId, node.id);
        int minutes = MapRouting.WalkMinutes(MapRouting.PathLengthNormalized(path), minutesPerMapUnit);
        _route.ShowRoute(path);

        string fromName = cur?.displayName ?? "Here";
        var weather = FlorenceWeather.Instance;
        var glyph = GlyphFor(weather != null ? weather.ConditionForNode(node) : "clear");

        _card.ShowDestination(node, fromName, minutes, glyph, pin.Unlocked,
            () => StartCoroutine(TravelSequence(node, minutes)));
    }

    void Select(GugolMapPin pin)
    {
        if (_selected != null) _selected.SetSelected(false);
        _selected = pin;
        if (pin != null) pin.SetSelected(true);
    }

    // Region travel preview: always presented on the region sheet, routed
    // between the two anchors (districts collapse into the Florence pin).
    void SelectRegionDestination(HubNode node, HubNode cur, string curAnchor, string dstAnchor)
    {
        // SetLayer destroys pins and hides the card — switch first, then work
        // with re-resolved references only.
        if (_layer != MapLevel.Region) SetLayer(MapLevel.Region);

        _pins.TryGetValue(dstAnchor, out var anchorPin);
        Select(anchorPin);   // inbound (fiesole→district) selects the city pin

        var path = MapRouting.FindPath(_hub, curAnchor, dstAnchor);
        float hours = MapRouting.RegionHours(MapRouting.PathLengthNormalized(path), regionHoursPerMapUnit);
        int fare = MapRouting.RegionFare(hours, florinsPerTravelHour);
        _route.ShowRoute(path);

        string fromName = cur?.displayName ?? "Here";
        var weather = FlorenceWeather.Instance;
        var glyph = GlyphFor(weather != null ? weather.ConditionForNode(node) : "clear");
        bool unlocked = MapRouting.IsUnlocked(node);
        bool affordable = FlorinWallet.Balance >= fare;

        _card.ShowRegionDestination(node, fromName, FormatRegionStats(node, hours, fare),
            glyph, unlocked, affordable,
            () => StartCoroutine(RegionTravelSequence(node, hours, fare, path)));
    }

    string FormatRegionStats(HubNode node, float hours, int fare)
    {
        string time;
        if (hours <= sameDayTravelThresholdHours)
        {
            int h = Mathf.Max(1, Mathf.RoundToInt(hours));
            time = h == 1 ? "≈1 hour by road" : $"≈{h} hours by road";
        }
        else
        {
            int days = Mathf.Max(1, Mathf.RoundToInt(hours / travelHoursPerDay));
            time = days == 1 ? "1 day by road" : $"{days} days by road";
        }
        string cost = fare == 1 ? "1 florin" : $"{fare} florins";
        return $"{time}   ·   {cost}   ·   {GugolUi.Rating(node):0.0} ({GugolUi.ReviewCount(node)})";
    }

    public void OnPinHovered(GugolMapPin pin, bool entered) { /* reserved */ }

    // Card closed (× / ESC / new selection): drop route + selection, unless the
    // card was hidden because travel is starting and the walker needs the route.
    public void OnCardHidden()
    {
        if (_travelling) return;
        _route?.Clear();
        if (_selected != null) { _selected.SetSelected(false); _selected = null; }
    }

    // ── In-zone jumps (ported from FastTravelMenu's "This Area") ───────────────

    List<ZoneEntryPoint> CollectNearbySpots()
    {
        var spots = new List<ZoneEntryPoint>();
        var seenIds = new HashSet<string>();
        foreach (var ep in FindObjectsByType<ZoneEntryPoint>(FindObjectsInactive.Include))
        {
            if (!ep.fastTravelDestination) continue;
            if (!seenIds.Add(ep.entryId)) continue;   // de-dupe cross-scene arrival markers
            spots.Add(ep);
        }
        return spots;
    }

    void JumpToSpot(ZoneEntryPoint entry)
    {
        var p = GameObject.FindGameObjectWithTag(playerTag);
        if (p == null) { Debug.LogWarning("[GugolMapUI] No player to teleport."); return; }

        var pos = entry.transform.position;
        var rb = p.GetComponent<Rigidbody>();
        if (rb != null) { rb.position = pos; rb.linearVelocity = Vector3.zero; }
        p.transform.position = pos;

        var controller = p.GetComponent<PlayerController>();
        if (controller != null && entry.faceDirection.sqrMagnitude > 0.01f)
            controller.SetFacing(entry.faceDirection);

        Debug.Log($"[GugolMapUI] Jumped to spot '{entry.Label}'.");
        Close();
    }

    // ── Travel ─────────────────────────────────────────────────────────────────

    IEnumerator TravelSequence(HubNode node, int minutes)
    {
        if (_travelling || node == null) yield break;
        if (!MapRouting.IsUnlocked(node))
        {
            Debug.LogError($"[GugolMapUI] Scene '{node.sceneName}' is not loadable — cannot travel.");
            yield break;
        }

        _travelling = true;
        RefreshZoomButtons();
        _card.Hide();

        yield return _route.AnimateTravel(walkDuration, null);

        // Walking costs time: parody minutes × multiplier. SetHour wraps past
        // midnight and GameCalendar's wrap detector rolls the day naturally —
        // never call ResyncClock for a forward jump (RestSystem documents why).
        float hours = minutes * gameTimeMultiplier / 60f;
        if (GameClock.HasClock) GameClock.SetHour(GameClock.Hour + hours);

        Debug.Log($"[GugolMapUI] Traveling to {node.displayName} ({node.sceneName}), " +
                  $"entry '{node.entryId}' — {minutes} min walk ≈ {hours:0.0}h of game time.");

        DistrictTracker.CurrentNodeId = node.id;
        TravelIntent.SetEntry(node.entryId);

        _sourceExit = null;                 // travelled — the old exit is moot
        _travelling = false;
        CloseInternal(rearmExit: false);    // hide UI + restore timeScale BEFORE the load
        SceneManager.LoadScene(node.sceneName);
    }

    // Road travel between region anchors: fare up front, walker down the road,
    // hours or days of game time, then the destination scene. Waypoints along
    // the way may spring a curse-scaled ambush that interrupts the journey.
    IEnumerator RegionTravelSequence(HubNode dest, float hours, int fare, List<HubNode> path)
    {
        if (_travelling || dest == null) yield break;
        if (!MapRouting.IsUnlocked(dest))
        {
            Debug.LogError($"[GugolMapUI] Scene '{dest.sceneName}' is not loadable — cannot travel.");
            yield break;
        }

        // Charge BEFORE anything moves: a failed fare means no travel, no time,
        // and the map stays open.
        if (!FlorinWallet.TrySpend(fare, $"fare to {dest.displayName}"))
        {
            _card.ShowFareError("Not enough florins");
            yield break;
        }

        _travelling = true;
        RefreshZoomButtons();
        _card.Hide();

        // Road encounter: first waypoint on the route that triggers wins.
        // Deterministic per (day, node) — reloading doesn't reroll.
        int ambushIndex = -1;
        if (path != null)
            for (int i = 1; i < path.Count; i++)   // skip the origin anchor
                if (path[i] != null && path[i].kind == NodeKind.Waypoint &&
                    EncounterRoll.ShouldTrigger(path[i], baseEncounterChance, curseChanceScale, maxEncounterChance))
                { ambushIndex = i; break; }

        if (ambushIndex >= 0)
        {
            var wp = path[ambushIndex];
            // Capture the origin BEFORE the tracker is pointed at the road —
            // defeat drags the party back here.
            var origin = _hub.GetNode(DistrictTracker.CurrentNodeId);

            float frac = _route.FractionAtVertex(ambushIndex);
            yield return _route.AnimateTravel(regionWalkDuration * frac, null, frac);

            // Same-day recrossings must not re-trigger the identical fight.
            EncounterRoll.MarkResolved(wp.id);

            PendingEncounter.Set(new PendingEncounter.Payload
            {
                encounterNodeId = wp.id,
                encounterCurse  = wp.curseLevel,
                seed            = EncounterRoll.DaySeed() ^ EncounterRoll.NodeSalt(wp.id),
                victory = new PendingEncounter.Destination
                {
                    sceneName = dest.sceneName,
                    entryId   = dest.entryId,
                    nodeId    = dest.id,
                    days      = hours <= sameDayTravelThresholdHours ? 0
                                : Mathf.Max(1, Mathf.RoundToInt(hours / travelHoursPerDay)),
                    hours     = hours,
                },
                defeat = new PendingEncounter.Destination
                {
                    sceneName = origin?.sceneName,
                    entryId   = origin?.entryId,
                    nodeId    = origin?.id,
                },
            });

            // The battle belongs to the road: curse seeding and rep attribution
            // key off the tracker.
            DistrictTracker.CurrentNodeId = wp.id;

            Debug.Log($"[GugolMapUI] Ambushed at {wp.displayName} en route to {dest.displayName}!");
            _sourceExit = null;
            _travelling = false;
            CloseInternal(rearmExit: false);   // restore timeScale BEFORE the load
            SceneManager.LoadScene(battleSceneName);
            // NO time advance here — victory applies the journey's time on
            // arrival; defeat charges its own day.
            yield break;
        }

        yield return _route.AnimateTravel(regionWalkDuration, null);

        // Short hauls arrive the same day: forward SetHour wrap rolls the day
        // naturally — never ResyncClock on a forward jump. Long hauls burn
        // whole days and arrive at morning — the RestSystem pattern exactly
        // (AdvanceDay ×N is additive-safe and fires OnDayChanged per road day,
        // so the daily curse drift charges the journey; ResyncClock guards the
        // backwards SetHour jump).
        var cal = GameCalendar.Instance;
        if (hours <= sameDayTravelThresholdHours || cal == null)
        {
            if (GameClock.HasClock) GameClock.SetHour(GameClock.Hour + hours);
        }
        else
        {
            int days = Mathf.Max(1, Mathf.RoundToInt(hours / travelHoursPerDay));
            for (int i = 0; i < days; i++) cal.AdvanceDay();
            GameClock.SetHour(RestSystem.MorningHour);
            cal.ResyncClock();
        }

        Debug.Log($"[GugolMapUI] Road travel to {dest.displayName} ({dest.sceneName}), " +
                  $"entry '{dest.entryId}' — {hours:0.0}h on the road, {fare} florin fare.");

        DistrictTracker.CurrentNodeId = dest.id;
        TravelIntent.SetEntry(dest.entryId);

        _sourceExit = null;
        _travelling = false;
        CloseInternal(rearmExit: false);
        SceneManager.LoadScene(dest.sceneName);
    }

    // ── Search ─────────────────────────────────────────────────────────────────

    void OnSearchChanged(string query)
    {
        SetAllDimmed(string.IsNullOrWhiteSpace(query) ? null : query.Trim().ToLowerInvariant());
    }

    void OnSearchSubmit(string query)
    {
        query = (query ?? "").Trim().ToLowerInvariant();
        if (query.Length == 0) return;

        var results = _searchIndex?.Query(query, 1);
        if (results != null && results.Count > 0)
        {
            var feature = results[0];
            if (feature.kind == GugolMapFeatureKind.Location && feature.node != null)
            {
                if (_layer != feature.node.mapLevel) SetLayer(feature.node.mapLevel);
                if (_pins.TryGetValue(feature.node.id, out var locationPin)) OnPinClicked(locationPin);
                return;
            }
            if (feature.kind == GugolMapFeatureKind.Street)
            {
                OnStreetClicked(feature.street);
                return;
            }
            if ((feature.kind == GugolMapFeatureKind.Venue || feature.kind == GugolMapFeatureKind.Npc) &&
                _knowledge.TryGet(GugolMapFeatureKind.Street, feature.streetId, out var street))
            {
                OnStreetClicked(street.street);
                OnMapFeatureClicked(feature);
                return;
            }
        }

        foreach (var pin in _pins.Values)
        {
            if (pin.Node.displayName.ToLowerInvariant().Contains(query))
            {
                OnPinClicked(pin);
                return;
            }
        }
    }

    void SetAllDimmed(string queryLower)
    {
        foreach (var pin in _pins.Values)
            pin.SetDimmed(queryLower != null &&
                          !pin.Node.displayName.ToLowerInvariant().Contains(queryLower));
    }

    // ── UI construction ────────────────────────────────────────────────────────

    void BuildUI()
    {
        var canvasGO = new GameObject("GugolMapCanvas");
        canvasGO.transform.SetParent(transform, false);
        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 490;   // below FastTravel/Rest/Guild (500)
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGO.AddComponent<GraphicRaycaster>();

        // Root — parchment-dark backdrop behind the (possibly letterboxed) map.
        _root = new GameObject("Root", typeof(RectTransform), typeof(Image));
        _root.transform.SetParent(canvasGO.transform, false);
        GugolUi.Stretch((RectTransform)_root.transform);
        _root.GetComponent<Image>().color = new Color(0.10f, 0.08f, 0.06f, 1f);

        // Map image fills the screen (Envelope keeps art aspect, crops overflow).
        var mapGo = new GameObject("MapImage", typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
        mapGo.transform.SetParent(_root.transform, false);
        var mapRt = (RectTransform)mapGo.transform;
        mapRt.anchorMin = mapRt.anchorMax = new Vector2(0.5f, 0.5f);
        mapRt.pivot = new Vector2(0.5f, 0.5f);
        _mapImg = mapGo.GetComponent<Image>();
        if (mapBackground != null) { _mapImg.sprite = mapBackground; _mapImg.color = Color.white; }
        else _mapImg.color = new Color(0.89f, 0.83f, 0.70f, 1f);   // bare parchment until art lands
        _mapImg.raycastTarget = true;
        // FitInParent: the whole parchment sheet stays visible (burned edges and
        // all) on the dark desk backdrop, letterboxing instead of cropping.
        _fitter = mapGo.GetComponent<AspectRatioFitter>();
        _fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        _fitter.aspectRatio = mapBackground != null
            ? mapBackground.rect.width / mapBackground.rect.height
            : 16f / 9f;
        mapGo.AddComponent<MapClickCatcher>().owner = this;
        var transition = mapGo.AddComponent<GugolMapTransitionController>();
        _layerPresenter = mapGo.AddComponent<GugolMapLayerPresenter>();
        _layerPresenter.Configure(_mapImg, _fitter, transition, presentationProfile, mapBackground);

        var chromeGo = new GameObject("MapChrome", typeof(RectTransform), typeof(AspectRatioFitter));
        chromeGo.transform.SetParent(_root.transform, false);
        _chromeLayer = (RectTransform)chromeGo.transform;
        _chromeLayer.anchorMin = _chromeLayer.anchorMax = new Vector2(0.5f, 0.5f);
        _chromeLayer.pivot = new Vector2(0.5f, 0.5f);
        _chromeFitter = chromeGo.GetComponent<AspectRatioFitter>();
        _chromeFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        _chromeFitter.aspectRatio = _fitter.aspectRatio;

        var weatherGo = new GameObject("WeatherOverlay", typeof(RectTransform), typeof(Image),
            typeof(GugolMapWeatherPresenter));
        weatherGo.transform.SetParent(mapGo.transform, false);
        GugolUi.Stretch((RectTransform)weatherGo.transform);
        _weatherPresenter = weatherGo.GetComponent<GugolMapWeatherPresenter>();

        // Route dots under the pins, both in the map rect's coordinate space.
        var routeGo = new GameObject("RouteLayer", typeof(RectTransform));
        routeGo.transform.SetParent(mapGo.transform, false);
        GugolUi.Stretch((RectTransform)routeGo.transform);
        _route = routeGo.AddComponent<GugolRouteRenderer>();
        _route.dotSprite = GugolUi.CircleSprite;
        _route.walkerSprite = walkerSprite != null ? walkerSprite : GugolUi.CircleSprite;

        var featureGo = new GameObject("FeatureLayer", typeof(RectTransform));
        featureGo.transform.SetParent(mapGo.transform, false);
        GugolUi.Stretch((RectTransform)featureGo.transform);
        _featureLayer = (RectTransform)featureGo.transform;
        _featurePresenter = featureGo.AddComponent<GugolMapFeaturePresenter>();
        _featurePresenter.Configure(this, _featureLayer, bodyFont,
            presentationProfile != null ? presentationProfile.ink : new Color(0.24f, 0.17f, 0.09f));

        var pinGo = new GameObject("PinLayer", typeof(RectTransform));
        pinGo.transform.SetParent(mapGo.transform, false);
        GugolUi.Stretch((RectTransform)pinGo.transform);
        _pinLayer = (RectTransform)pinGo.transform;
        _route.mapRect = _pinLayer;

        BuildSearchBar();
        BuildScaleControl();

        // Attribution parody, bottom-right.
        var attrib = GugolUi.MakeText(_root.transform,
            "©MCCXCIX Gugol — Map data: Republic of Florence", 14,
            FontStyles.Italic, new Color(0.35f, 0.28f, 0.18f, 0.85f), bodyFont);
        attrib.alignment = TextAlignmentOptions.MidlineRight;
        var aRt = attrib.rectTransform;
        aRt.anchorMin = aRt.anchorMax = new Vector2(1f, 0f);
        aRt.pivot = new Vector2(1f, 0f);
        aRt.anchoredPosition = new Vector2(-16f, 10f);
        aRt.sizeDelta = new Vector2(560f, 22f);

        // Directions card (hidden until a pin is clicked).
        var cardGo = new GameObject("DirectionsCardHost", typeof(RectTransform));
        cardGo.transform.SetParent(_root.transform, false);
        GugolUi.Stretch((RectTransform)cardGo.transform);
        _card = cardGo.AddComponent<GugolDirectionsCard>();
        _card.Init(this, (RectTransform)cardGo.transform, cardSprite,
            headerFont, bodyFont, walkerSprite, iconStar, iconClock, fallbackPreview);

        var contextGo = new GameObject("MapContextCardHost", typeof(RectTransform));
        contextGo.transform.SetParent(_root.transform, false);
        GugolUi.Stretch((RectTransform)contextGo.transform);
        _contextCard = contextGo.AddComponent<GugolMapCardHost>();
        _contextCard.Init(this, (RectTransform)contextGo.transform, cardSprite, headerFont, bodyFont);
    }

    void BuildSearchBar()
    {
        var barGo = new GameObject("SearchBar", typeof(RectTransform), typeof(Image));
        barGo.transform.SetParent(_chromeLayer, false);
        var barRt = (RectTransform)barGo.transform;
        barRt.anchorMin = barRt.anchorMax = new Vector2(0f, 1f);
        barRt.pivot = new Vector2(0f, 1f);
        barRt.anchoredPosition = new Vector2(24f, -24f);
        barRt.sizeDelta = new Vector2(340f, 52f);

        var barImg = barGo.GetComponent<Image>();
        if (searchBarSprite != null)
        {
            barImg.sprite = searchBarSprite;
            barImg.type = Image.Type.Sliced;
            barImg.color = Color.white;
        }
        else barImg.color = new Color(0.96f, 0.93f, 0.85f, 0.98f);

        _search = barGo.AddComponent<TMP_InputField>();
        _search.targetGraphic = barImg;

        var area = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
        area.transform.SetParent(barGo.transform, false);
        var areaRt = (RectTransform)area.transform;
        GugolUi.Stretch(areaRt);
        areaRt.offsetMin = new Vector2(18f, 8f);
        areaRt.offsetMax = new Vector2(-18f, -8f);

        var ink = new Color(0.24f, 0.17f, 0.09f);
        var text = GugolUi.MakeText(area.transform, "", 22, FontStyles.Normal, ink, bodyFont);
        GugolUi.Stretch((RectTransform)text.transform);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;

        var placeholder = GugolUi.MakeText(area.transform, "Search Florence...", 22,
            FontStyles.Italic, new Color(0.45f, 0.38f, 0.28f, 0.75f), bodyFont);
        GugolUi.Stretch((RectTransform)placeholder.transform);
        placeholder.textWrappingMode = TextWrappingModes.NoWrap;
        _searchPlaceholder = placeholder;

        _search.textViewport = areaRt;
        _search.textComponent = text;
        _search.placeholder = placeholder;
        _search.caretColor = ink;
        _search.customCaretColor = true;
        _search.selectionColor = new Color(0.85f, 0.72f, 0.45f, 0.55f);
        _search.onValueChanged.AddListener(OnSearchChanged);
        _search.onSubmit.AddListener(OnSearchSubmit);
    }

    // The Google zoom control, in parchment: "+" over "−", bottom-right.
    void BuildScaleControl()
    {
        var group = new GameObject("ScaleControl", typeof(RectTransform));
        group.transform.SetParent(_chromeLayer, false);
        var rect = (RectTransform)group.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -86f);
        rect.sizeDelta = new Vector2(510f, 52f);

        _cityBtn = MakeScaleButton(group.transform, "City", -170f, () => SetLayer(MapLevel.City));
        _regionBtn = MakeScaleButton(group.transform, "Tuscany", 0f, () => SetLayer(MapLevel.Region));
        _worldBtn = MakeScaleButton(group.transform, "Italy", 170f, () => SetLayer(MapLevel.World));
        RefreshZoomButtons();
    }

    Button MakeScaleButton(Transform parent, string labelText, float x, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject("Scale_" + labelText, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x, 0f);
        rect.sizeDelta = new Vector2(158f, 48f);

        var image = go.GetComponent<Image>();
        image.color = new Color(0.94f, 0.90f, 0.80f, 0.97f);
        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);
        var label = GugolUi.MakeText(go.transform, labelText, 21f, FontStyles.Bold,
            new Color(0.30f, 0.23f, 0.13f), headerFont);
        label.alignment = TextAlignmentOptions.Center;
        GugolUi.Stretch(label.rectTransform);
        return button;
    }

    // Legacy plus/minus builder retained as a compatibility fallback but is no
    // longer created by the approved browsing hierarchy.
    void BuildZoomControl()
    {
        var group = new GameObject("ZoomControl", typeof(RectTransform));
        group.transform.SetParent(_root.transform, false);
        var gRt = (RectTransform)group.transform;
        gRt.anchorMin = gRt.anchorMax = new Vector2(1f, 0f);
        gRt.pivot = new Vector2(1f, 0f);
        gRt.anchoredPosition = new Vector2(-24f, 48f);
        gRt.sizeDelta = new Vector2(54f, 110f);

        _zoomInBtn  = MakeZoomButton(group.transform, "+", new Vector2(0f, 56f), () =>
        {
            if (_layer > MapLevel.City) SetLayer(_layer - 1);
        });
        _zoomOutBtn = MakeZoomButton(group.transform, "−", new Vector2(0f, 0f), () =>
        {
            if (_layer < MapLevel.World) SetLayer(_layer + 1);
        });
        RefreshZoomButtons();
    }

    Button MakeZoomButton(Transform parent, string glyph, Vector2 pos, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject("Zoom" + glyph, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(54f, 54f);

        var img = go.GetComponent<Image>();
        img.color = new Color(0.94f, 0.90f, 0.80f, 0.97f);   // parchment chip

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.99f, 0.96f, 0.88f, 1f);
        colors.pressedColor     = new Color(0.85f, 0.76f, 0.58f, 1f);
        colors.disabledColor    = new Color(0.94f, 0.90f, 0.80f, 0.35f);
        colors.fadeDuration     = 0.05f;
        btn.colors = colors;
        btn.onClick.AddListener(onClick);

        var label = GugolUi.MakeText(go.transform, glyph, 34, FontStyles.Bold,
            new Color(0.30f, 0.23f, 0.13f), headerFont);
        label.alignment = TextAlignmentOptions.Center;
        GugolUi.Stretch((RectTransform)label.transform);
        return btn;
    }

    void BuildWordmark()
    {
        var group = new GameObject("Wordmark", typeof(RectTransform));
        group.transform.SetParent(_root.transform, false);
        var gRt = (RectTransform)group.transform;
        gRt.anchorMin = gRt.anchorMax = new Vector2(1f, 1f);
        gRt.pivot = new Vector2(1f, 1f);
        gRt.anchoredPosition = new Vector2(-28f, -24f);
        gRt.sizeDelta = new Vector2(330f, 56f);

        if (crestSprite != null)
        {
            var crest = GugolUi.MakeImage(group.transform, "Crest", crestSprite, Color.white);
            crest.preserveAspect = true;
            var cRt = crest.rectTransform;
            cRt.anchorMin = cRt.anchorMax = new Vector2(0f, 0.5f);
            cRt.pivot = new Vector2(0f, 0.5f);
            cRt.anchoredPosition = Vector2.zero;
            cRt.sizeDelta = new Vector2(48f, 48f);
        }

        var mark = GugolUi.MakeText(group.transform, GugolUi.WordmarkRichText, 34,
            FontStyles.Bold, Color.white, headerFont);
        mark.alignment = TextAlignmentOptions.MidlineRight;
        mark.richText = true;
        var mRt = mark.rectTransform;
        GugolUi.Stretch(mRt);
        mRt.offsetMin = new Vector2(52f, 0f);
    }

    // Clicks on empty map: close the card (Google behavior) and optionally log
    // normalized coords — the pin re-tuning aid for new background art.
    class MapClickCatcher : MonoBehaviour, IPointerClickHandler
    {
        public GugolMapUI owner;

        public void OnPointerClick(PointerEventData e)
        {
            if (owner == null) return;
            if (owner.debugLogClickCoords &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)transform, e.position, e.pressEventCamera, out var local))
            {
                var rect = ((RectTransform)transform).rect;
                var n = new Vector2((local.x - rect.xMin) / rect.width,
                                    (local.y - rect.yMin) / rect.height);
                Debug.Log($"[GugolMapUI] map click at normalized ({n.x:0.000}, {n.y:0.000})");
            }
            if (owner._card != null && owner._card.IsOpen && !owner._travelling)
                owner._card.Hide();
        }
    }

#if UNITY_EDITOR
    public int ValidationFeatureVisualCount => _featurePresenter?.ActiveVisualCount ?? 0;
    public bool ValidationContextCardOpen => _contextCard != null && _contextCard.IsOpen;
    public bool ValidationHasWordmark => _root != null && _root.transform.Find("Wordmark") != null;

    public bool ValidationShowCurrentLocationCard()
    {
        HubNode node = _hub?.GetNode(DistrictTracker.CurrentNodeId);
        if (node == null || _card == null) return false;
        _card.ShowCurrent(node, CollectNearbySpots(), JumpToSpot);
        return _card.IsOpen;
    }

    public bool ValidationOpenStreet(string streetId)
    {
        if (_knowledge == null || !_knowledge.TryGet(GugolMapFeatureKind.Street, streetId, out var record))
            return false;
        OnStreetClicked(record.street);
        return CurrentView == GugolMapViewKind.Street && FocusedStreetId == streetId;
    }

    public bool ValidationOpenFeature(GugolMapFeatureKind kind, string featureId)
    {
        if (_knowledge == null || !_knowledge.TryGet(kind, featureId, out var record)) return false;
        OnMapFeatureClicked(record);
        return true;
    }

    public GugolMapFeatureRecord ValidationSearchFirst(string query)
    {
        var results = _searchIndex?.Query(query, 1);
        return results != null && results.Count > 0 ? results[0] : null;
    }

    public void ValidationSetLayer(MapLevel layer) => SetLayer(layer);
    public void ValidationExitStreet() => ExitStreetView();
#endif
}
