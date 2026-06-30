using UnityEngine;
using System.Collections.Generic;

// Object pool for DamageNumber instances. Singleton — place once in battle scene.
public class DamageNumberPool : MonoBehaviour
{
    public static DamageNumberPool Instance { get; private set; }

    [Header("Pool")]
    public GameObject damageNumberPrefab;
    public int        initialSize = 16;
    public Transform  poolParent;

    private Queue<DamageNumber> _pool = new();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        for (int i = 0; i < initialSize; i++)
            Return(CreateNew());
    }

    void Start()
    {
        // Wire into BattleManager events
        var bm = BattleManager.Instance;
        if (bm == null) return;

        bm.OnUnitDamaged  += OnUnitDamaged;
        bm.OnUnitHealed   += OnUnitHealed;
        bm.OnSkillAbsorbed += OnSkillAbsorbed;
    }

    void OnDestroy()
    {
        var bm = BattleManager.Instance;
        if (bm == null) return;
        bm.OnUnitDamaged   -= OnUnitDamaged;
        bm.OnUnitHealed    -= OnUnitHealed;
        bm.OnSkillAbsorbed -= OnSkillAbsorbed;
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    void OnUnitDamaged(BattleUnit unit, int amount, DamageType type, bool isCrit)
    {
        Get()?.ShowDamage(amount, type, isCrit, UnitWorldPos(unit));
    }

    void OnUnitHealed(BattleUnit unit, int amount)
    {
        Get()?.ShowHeal(amount, UnitWorldPos(unit));
    }

    void OnSkillAbsorbed(BattleUnit unit, AbsorbedSkillInstance absorbed)
    {
        Get()?.ShowAbsorb(absorbed.DisplayName(), UnitWorldPos(unit));
    }

    // ── Pool API ──────────────────────────────────────────────────────────────

    public DamageNumber Get()
    {
        if (_pool.Count > 0)
        {
            var dn = _pool.Dequeue();
            dn.gameObject.SetActive(false); // Play() will re-activate
            return dn;
        }
        return CreateNew();
    }

    public void Return(DamageNumber dn)
    {
        if (dn == null) return;
        dn.gameObject.SetActive(false);
        dn.transform.SetParent(poolParent != null ? poolParent : transform);
        _pool.Enqueue(dn);
    }

    // ── Miss helper (called by AbilityResolver) ───────────────────────────────

    public void ShowMiss(BattleUnit target)
    {
        Get()?.ShowMiss(UnitWorldPos(target));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    DamageNumber CreateNew()
    {
        if (damageNumberPrefab == null)
        {
            Debug.LogError("[DamageNumberPool] damageNumberPrefab is not assigned.");
            return null;
        }
        var go = Instantiate(damageNumberPrefab, poolParent != null ? poolParent : transform);
        var dn = go.GetComponent<DamageNumber>();
        if (dn == null)
        {
            Debug.LogError("[DamageNumberPool] Prefab is missing a DamageNumber component.");
            Destroy(go);
            return null;
        }
        go.SetActive(false);
        return dn;
    }

    Vector3 UnitWorldPos(BattleUnit unit)
    {
        // World position of the unit's grid cell, offset up slightly
        var grid = BattleManager.Instance?.Grid;
        if (grid == null) return unit.transform.position + Vector3.up;
        var cell = grid.GetCell(unit.gridPosition);
        return grid.GridToWorld(unit.gridPosition, cell?.elevation ?? 0) + Vector3.up * 0.5f;
    }
}
