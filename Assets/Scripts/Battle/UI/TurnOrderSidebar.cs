using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

// FFT-style turn order sidebar — shows next N units in CT order.
// Rebuilds every time the BattleManager fires OnTurnStart or state changes to TickCT.
public class TurnOrderSidebar : MonoBehaviour
{
    [Header("References")]
    public Transform        entryContainer; // vertical layout group parent
    public GameObject       entryPrefab;    // TurnOrderEntry prefab

    [Header("Settings")]
    public int  lookahead  = 8;     // how many turns ahead to show
    public bool showLabels = true;

    [Header("Active unit highlight")]
    public Color activeColor  = new Color(0.85f, 0.68f, 0.25f, 1f);
    public Color playerColor  = new Color(0.30f, 0.55f, 0.90f, 1f);
    public Color enemyColor   = new Color(0.80f, 0.25f, 0.20f, 1f);

    private List<TurnOrderEntry> _entries = new();

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        if (BattleManager.Instance != null)
        {
            BattleManager.Instance.OnTurnStart    += _ => Rebuild();
            BattleManager.Instance.OnStateChanged += OnStateChanged;
            BattleManager.Instance.OnUnitDied     += _ => Rebuild();
        }
    }

    void OnStateChanged(BattleState state)
    {
        if (state == BattleState.TickCT || state == BattleState.BattleStart)
            Rebuild();

        bool visible = state != BattleState.Inactive;
        gameObject.SetActive(visible);
    }

    // ── Rebuild ───────────────────────────────────────────────────────────────

    public void Rebuild()
    {
        var bm = BattleManager.Instance;
        if (bm == null) return;

        var order = bm.GetTurnOrder(lookahead);

        // Grow pool if needed
        while (_entries.Count < order.Count)
        {
            var go    = Instantiate(entryPrefab, entryContainer);
            var entry = go.GetComponent<TurnOrderEntry>();
            _entries.Add(entry);
        }

        // Update visible entries
        for (int i = 0; i < _entries.Count; i++)
        {
            if (i < order.Count)
            {
                var unit = order[i];
                bool isActive = unit == bm.ActiveUnit;
                Color tint = isActive ? activeColor : (unit.IsPlayer ? playerColor : enemyColor);
                _entries[i].Set(unit.Data.displayName, unit.Data.portrait, tint, isActive, showLabels);
                _entries[i].gameObject.SetActive(true);
            }
            else
            {
                _entries[i].gameObject.SetActive(false);
            }
        }
    }
}
