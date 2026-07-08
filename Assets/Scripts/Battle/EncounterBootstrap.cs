using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

// Drives a REAL road encounter in the BattleArena (the test harness stays for
// direct plays of the scene). Consumes the PendingEncounter payload, builds the
// fight from the persistent roster + enemy assets, and owns the exit: victory
// continues the interrupted journey (rewards overlay, time advance, destination
// scene); defeat drags the party back to the origin at a price.
//
// This is the ONLY subscriber of BattleManager.OnVictory/OnDefeat.
public class EncounterBootstrap : MonoBehaviour
{
    [Header("Enemy assets (wired by BattleArenaBuilder)")]
    public CombatantData cursebearer;
    public CombatantData ashWretch;

    [Header("Tuning")]
    [Tooltip("Encounter curse at/above this adds a third enemy.")]
    public float highCurseThreshold = 0.5f;
    [Tooltip("Curse added to the winning waypoint when the party is defeated.")]
    public float defeatCurseAmount = 0.05f;
    [Tooltip("Fraction of florins LOST on defeat.")]
    public float defeatFlorinLoss = 0.5f;
    [Tooltip("Fraction of max HP the party wakes with after defeat.")]
    public float reviveHpFraction = 0.5f;
    [Tooltip("Minimum HP a roster member enters battle with (never block travel).")]
    public int minEntryHP = 1;

    PendingEncounter.Payload _enc;
    BattleManager _bm;
    List<CombatantData> _roster;
    int[] _snapHP, _snapSP, _snapLevel, _snapAP;
    int _florinBaseline;
    bool _outcomeHandled;
    readonly List<string> _absorbLines = new();

    // Consume BEFORE any Start runs: BattleTestStarter.Start checks Pending AND
    // Current — consuming here (Awake) keeps its guard true either way, so a
    // second test battle can never start on top of the encounter.
    void Awake() => _enc = PendingEncounter.Consume();

    void Start()
    {
        if (_enc == null) return;   // direct arena play — test harness handles it

        _bm = BattleManager.Instance;
        if (_bm == null || cursebearer == null || ashWretch == null)
        {
            Debug.LogError("[EncounterBootstrap] Missing BattleManager or enemy assets — encounter aborted.");
            PendingEncounter.Complete();
            return;
        }

        PartyRoster.EnsureInitialized();
        _roster = new List<CombatantData>(RestSystem.PartyMembers);
        Snapshot();

        // Enemy set scales with the road's corruption; composition seeded so
        // the same day+node always produces the same ambush.
        var enemies = new List<CombatantData> { cursebearer, ashWretch };
        if (_enc.encounterCurse >= highCurseThreshold)
            enemies.Add((_enc.seed & 1) == 0 ? cursebearer : ashWretch);

        var playerSpawns = new List<Vector2Int> { new Vector2Int(3, 3), new Vector2Int(4, 2) };
        var enemySpawns  = new List<Vector2Int> { new Vector2Int(10, 8), new Vector2Int(9, 9), new Vector2Int(11, 8) };

        // SpawnUnit clones every non-Benidito combatant — passing the raw assets
        // is safe (they're never mutated at runtime).
        _bm.StartBattle(_roster, enemies, playerSpawns, enemySpawns);

        // BattleUnit.Initialize calls InitRuntime (full HP) — re-apply the
        // persistent wounds so battles carry HP/SP forward.
        for (int i = 0; i < _bm.Players.Count && i < _roster.Count; i++)
        {
            var data = _bm.Players[i].Data;
            var stats = data.GetTotalStats();
            data.currentHP = Mathf.Clamp(_snapHP[i], minEntryHP, stats.hpMax);
            data.currentSP = Mathf.Clamp(_snapSP[i], 0, stats.spMax);
        }

        // Arena readability tint (same palette as the test harness).
        foreach (var u in _bm.Players)
        { var sr = u.GetComponentInChildren<SpriteRenderer>(); if (sr) sr.color = new Color(0.35f, 0.55f, 1f); }
        foreach (var u in _bm.Enemies)
        { var sr = u.GetComponentInChildren<SpriteRenderer>(); if (sr) sr.color = new Color(1f, 0.35f, 0.3f); }

        _florinBaseline = FlorinWallet.Balance;

        _bm.OnVictory -= HandleVictory;       _bm.OnVictory += HandleVictory;
        _bm.OnDefeat  -= HandleDefeat;        _bm.OnDefeat  += HandleDefeat;
        _bm.OnSkillAbsorbed -= HandleAbsorb;  _bm.OnSkillAbsorbed += HandleAbsorb;

        Debug.Log($"[EncounterBootstrap] Ambush at {_enc.encounterNodeId} " +
                  $"(curse {_enc.encounterCurse:0.00}) — {enemies.Count} enemies.");
    }

    void OnDestroy()
    {
        if (_bm == null) return;
        _bm.OnVictory -= HandleVictory;
        _bm.OnDefeat  -= HandleDefeat;
        _bm.OnSkillAbsorbed -= HandleAbsorb;
    }

    void Snapshot()
    {
        int n = _roster.Count;
        _snapHP = new int[n]; _snapSP = new int[n];
        _snapLevel = new int[n]; _snapAP = new int[n];
        for (int i = 0; i < n; i++)
        {
            _snapHP[i] = _roster[i].currentHP;
            _snapSP[i] = _roster[i].currentSP;
            var job = _roster[i].activeJob;
            _snapLevel[i] = job != null ? job.jobLevel  : 0;
            _snapAP[i]    = job != null ? job.currentAP : 0;
        }
    }

    void HandleAbsorb(BattleUnit absorber, AbsorbedSkillInstance skill)
    {
        if (skill != null) _absorbLines.Add($"Absorbed: {skill.DisplayName()}");
    }

    void HandleVictory()
    {
        if (_outcomeHandled) return;
        _outcomeHandled = true;
        StartCoroutine(VictoryFlow());
    }

    void HandleDefeat()
    {
        if (_outcomeHandled) return;
        _outcomeHandled = true;
        StartCoroutine(DefeatFlow());
    }

    // ── Victory ────────────────────────────────────────────────────────────────

    IEnumerator VictoryFlow()
    {
        // Never load a scene from inside the death-callback chain.
        yield return new WaitForSecondsRealtime(1f);

        CopyBack();

        var lines = new List<string>();
        int florins = FlorinWallet.Balance - _florinBaseline;
        if (florins != 0) lines.Add($"Florins: +{florins}");
        for (int i = 0; i < _roster.Count; i++)
        {
            var m = _roster[i];
            var job = m.activeJob;
            if (job != null && job.job != null)
            {
                bool leveled = job.jobLevel > _snapLevel[i];
                lines.Add($"{m.displayName} — {job.job.name} Lv {job.jobLevel}" +
                          $"  ·  +{job.currentAP - _snapAP[i]} AP" +
                          (leveled ? "  —  Level Up!" : ""));
            }
            else lines.Add($"{m.displayName} — fought well");
        }
        lines.AddRange(_absorbLines);

        ShowOverlay("Victory", lines, "Continue", ContinueJourney);
    }

    // Apply the interrupted journey's remainder, then arrive.
    void ContinueJourney()
    {
        var d = _enc.victory;
        var cal = GameCalendar.Instance;
        if (d.days <= 0 || cal == null)
        {
            // Same-day road: forward wrap rolls the day naturally — no ResyncClock.
            if (GameClock.HasClock) GameClock.SetHour(GameClock.Hour + d.hours);
        }
        else
        {
            for (int i = 0; i < d.days; i++) cal.AdvanceDay();
            GameClock.SetHour(RestSystem.MorningHour);
            cal.ResyncClock();   // backwards jump guard
        }

        Debug.Log($"[EncounterBootstrap] Victory — journey continues to {d.sceneName}.");
        DistrictTracker.CurrentNodeId = d.nodeId;
        TravelIntent.SetEntry(d.entryId);
        PendingEncounter.Complete();
        SceneManager.LoadScene(d.sceneName);
    }

    // ── Defeat ─────────────────────────────────────────────────────────────────

    IEnumerator DefeatFlow()
    {
        yield return new WaitForSecondsRealtime(1f);

        CopyBack();   // hard-won XP/AP survive even a loss

        int loss = Mathf.FloorToInt(FlorinWallet.Balance * defeatFlorinLoss);
        var lines = new List<string>
        {
            "The road takes its due.",
            "You wake the next morning, dragged back the way you came.",
            loss > 0 ? $"Florins lost: {loss}" : "Your purse was already empty.",
        };

        ShowOverlay("Dragged Back", lines, "Continue", AcceptDefeat);
    }

    void AcceptDefeat()
    {
        FlorinWallet.SetBalance(FlorinWallet.Balance - Mathf.FloorToInt(FlorinWallet.Balance * defeatFlorinLoss));
        HubMap.Instance?.AddCurse(_enc.encounterNodeId, defeatCurseAmount);

        var cal = GameCalendar.Instance;
        if (cal != null)
        {
            cal.AdvanceDay();
            GameClock.SetHour(RestSystem.MorningHour);
            cal.ResyncClock();
        }

        foreach (var m in _roster)
        {
            var stats = m.GetTotalStats();
            m.currentHP = Mathf.Max(1, Mathf.RoundToInt(stats.hpMax * reviveHpFraction));
            m.currentSP = Mathf.RoundToInt(stats.spMax * reviveHpFraction);
        }

        var d = _enc.defeat;
        Debug.Log($"[EncounterBootstrap] Defeat — dragged back to {d.sceneName}.");
        DistrictTracker.CurrentNodeId = d.nodeId;
        TravelIntent.SetEntry(d.entryId);
        PendingEncounter.Complete();
        SceneManager.LoadScene(d.sceneName);
    }

    // ── Copy-back (the clone trap) ─────────────────────────────────────────────

    // Non-Benidito units fight as CLONES: their wounds and job progression land on
    // throwaway copies. Copy the numbers back onto the roster objects. Benidito
    // (Benidito role) keeps his original reference — skipped by the == check.
    void CopyBack()
    {
        for (int i = 0; i < _bm.Players.Count && i < _roster.Count; i++)
        {
            var clone  = _bm.Players[i].Data;
            var target = _roster[i];
            if (clone == null || clone == target) continue;

            target.currentHP = clone.currentHP;
            target.currentSP = clone.currentSP;

            // The clone's activeJob may be a serializer-inflated husk (job == null).
            var src = clone.activeJob;
            var dst = target.activeJob;
            if (src != null && src.job != null && dst != null && dst.job == src.job)
            {
                dst.jobLevel  = src.jobLevel;
                dst.currentXP = src.currentXP;
                dst.currentAP = src.currentAP;
            }
        }
    }

    // ── Outcome overlay (Pattern-B visual language; arena has an EventSystem) ─

    void ShowOverlay(string title, List<string> lines, string buttonLabel, System.Action onContinue)
    {
        var canvasGo = new GameObject("EncounterOutcomeCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 600;   // above the battle UI
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        var dim = GugolUi.MakeImage(canvasGo.transform, "Dim", null, new Color(0f, 0f, 0f, 0.65f), raycast: true);
        GugolUi.Stretch(dim.rectTransform);

        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image),
            typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        panel.transform.SetParent(canvasGo.transform, false);
        var pRt = (RectTransform)panel.transform;
        pRt.anchorMin = pRt.anchorMax = new Vector2(0.5f, 0.5f);
        pRt.pivot = new Vector2(0.5f, 0.5f);
        pRt.sizeDelta = new Vector2(520f, 0f);
        panel.GetComponent<Image>().color = new Color(0.08f, 0.06f, 0.10f, 0.94f);
        var vlg = panel.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(28, 28, 24, 24);
        vlg.spacing = 10;
        vlg.childControlWidth = true;
        vlg.childForceExpandWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandHeight = false;
        panel.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var titleText = GugolUi.MakeText(panel.transform, title, 40, FontStyles.Bold,
            new Color(0.95f, 0.85f, 0.55f));
        titleText.alignment = TextAlignmentOptions.Center;

        foreach (var line in lines)
        {
            var t = GugolUi.MakeText(panel.transform, line, 24, FontStyles.Normal,
                new Color(0.92f, 0.90f, 0.86f));
            t.alignment = TextAlignmentOptions.Center;
        }

        GugolUi.MakeRow(panel.transform, buttonLabel, null, () =>
        {
            Destroy(canvasGo);
            onContinue?.Invoke();
        }, minHeight: 52f);
    }
}
