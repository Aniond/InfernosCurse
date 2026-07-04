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

    // ── Runtime ────────────────────────────────────────────────────────────────
    enum MapLayer { City, Region }

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
    MapLayer _layer = MapLayer.City;

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

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        BuildUI();
        CloseInternal(rearmExit: false);
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

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
                else if (_card.IsOpen) _card.Hide();
                else                   Close();
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

        EnsureEventSystem();
        // Activate BEFORE building pins: TMP components under an inactive root
        // haven't awakened, and touching e.g. outlineWidth then NREs inside TMP.
        _root.SetActive(true);

        // Open on the layer the player is actually on: a Florence district
        // shows the city sheet; Fiesole (or any region location) the region one.
        var curNode = _hub.GetNode(DistrictTracker.CurrentNodeId);
        SetLayer(curNode == null || curNode.kind == NodeKind.District ? MapLayer.City : MapLayer.Region);

        _hub.OnNodeChanged -= OnNodeChanged;
        _hub.OnNodeChanged += OnNodeChanged;

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

    static MapLayer LayerOf(HubNode node)
        => node.kind == NodeKind.District ? MapLayer.City : MapLayer.Region;

    // The Google-zoom parody: one map, two sheets. Swaps the background,
    // rebuilds pins for the layer, and clears route/card/search state.
    void SetLayer(MapLayer layer)
    {
        if (_travelling) return;
        _layer = layer;

        var bg = layer == MapLayer.City ? mapBackground : regionBackground;
        if (bg != null) { _mapImg.sprite = bg; _mapImg.color = Color.white; }
        else            { _mapImg.sprite = null; _mapImg.color = new Color(0.89f, 0.83f, 0.70f, 1f); }
        _fitter.aspectRatio = bg != null ? bg.rect.width / bg.rect.height : 1f;

        // The fitter must resize the map rect BEFORE pins read _pinLayer.rect
        // — a stale rect scatters every pin. Toggling re-runs UpdateRect now.
        _fitter.enabled = false;
        _fitter.enabled = true;
        Canvas.ForceUpdateCanvases();

        RebuildPins();
        _route.Clear();
        _card.Hide();
        if (_search != null) _search.SetTextWithoutNotify("");
        if (_searchPlaceholder != null)
            _searchPlaceholder.text = layer == MapLayer.City ? "Search Florence..." : "Search Tuscany...";
        SetAllDimmed(null);
        RefreshZoomButtons();
    }

    void RefreshZoomButtons()
    {
        if (_zoomInBtn  != null) _zoomInBtn.interactable  = _layer == MapLayer.Region && !_travelling;
        if (_zoomOutBtn != null) _zoomOutBtn.interactable = _layer == MapLayer.City   && !_travelling;
    }

    // ── Pins ───────────────────────────────────────────────────────────────────

    void RebuildPins()
    {
        foreach (var p in _pins.Values) if (p != null) Destroy(p.gameObject);
        _pins.Clear();
        foreach (var d in _waypointDots) if (d != null) Destroy(d);
        _waypointDots.Clear();
        _selected = null;

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

            bool here = _layer == MapLayer.City
                ? node.id == DistrictTracker.CurrentNodeId
                : node.id == MapRouting.RegionAnchorId(cur);
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

    // ── Selection / routing ────────────────────────────────────────────────────

    public void OnPinClicked(GugolMapPin pin)
    {
        if (_travelling || pin == null || pin.Node == null) return;

        var node = pin.Node;

        // Road dots are scenery (belt-and-braces — they aren't raycast targets).
        if (node.kind == NodeKind.Waypoint) return;

        // The Florence pin on the region layer is the zoom-in gateway — always
        // zooms, never opens a travel card.
        if (node.kind == NodeKind.City)
        {
            SetLayer(MapLayer.City);
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
        if (_layer != MapLayer.Region) SetLayer(MapLayer.Region);

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
            () => StartCoroutine(RegionTravelSequence(node, hours, fare)));
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
        foreach (var ep in FindObjectsByType<ZoneEntryPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None))
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
    // hours or days of game time, then the destination scene.
    IEnumerator RegionTravelSequence(HubNode dest, float hours, int fare)
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

        // Route dots under the pins, both in the map rect's coordinate space.
        var routeGo = new GameObject("RouteLayer", typeof(RectTransform));
        routeGo.transform.SetParent(mapGo.transform, false);
        GugolUi.Stretch((RectTransform)routeGo.transform);
        _route = routeGo.AddComponent<GugolRouteRenderer>();
        _route.dotSprite = GugolUi.CircleSprite;
        _route.walkerSprite = walkerSprite != null ? walkerSprite : GugolUi.CircleSprite;

        var pinGo = new GameObject("PinLayer", typeof(RectTransform));
        pinGo.transform.SetParent(mapGo.transform, false);
        GugolUi.Stretch((RectTransform)pinGo.transform);
        _pinLayer = (RectTransform)pinGo.transform;
        _route.mapRect = _pinLayer;

        BuildSearchBar();
        BuildWordmark();
        BuildZoomControl();

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
    }

    void BuildSearchBar()
    {
        var barGo = new GameObject("SearchBar", typeof(RectTransform), typeof(Image));
        barGo.transform.SetParent(_root.transform, false);
        var barRt = (RectTransform)barGo.transform;
        barRt.anchorMin = barRt.anchorMax = new Vector2(0f, 1f);
        barRt.pivot = new Vector2(0f, 1f);
        barRt.anchoredPosition = new Vector2(28f, -28f);
        barRt.sizeDelta = new Vector2(400f, 56f);

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
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;

        var placeholder = GugolUi.MakeText(area.transform, "Search Florence...", 22,
            FontStyles.Italic, new Color(0.45f, 0.38f, 0.28f, 0.75f), bodyFont);
        GugolUi.Stretch((RectTransform)placeholder.transform);
        placeholder.enableWordWrapping = false;
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
            if (_layer == MapLayer.Region) SetLayer(MapLayer.City);
        });
        _zoomOutBtn = MakeZoomButton(group.transform, "−", new Vector2(0f, 0f), () =>
        {
            if (_layer == MapLayer.City) SetLayer(MapLayer.Region);
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
}
