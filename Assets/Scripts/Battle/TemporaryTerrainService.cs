using System;
using System.Collections.Generic;
using UnityEngine;

public enum TemporaryTerrainKind { LimboStain, GraveMulch }

public enum TemporaryTerrainFailure
{
    None,
    MissingGrid,
    OutOfBounds,
    Unwalkable,
    Objective,
    ProtectedTerrain,
    StrongerAuthoredCorruption,
    NestedTemporaryTerrain,
}

public static class TemporaryTerrainService
{
    public static event Action<BattleGrid, Vector2Int, TemporaryTerrainKind> TerrainApplied;
    public static event Action<BattleGrid, Vector2Int, TemporaryTerrainKind> TerrainRestored;

    sealed class Entry
    {
        public TemporaryTerrainKind kind;
        public int remainingTurns;
        public bool sourceIsPlayer;
        public BattleUnit source;
        public TileType originalTileType;
        public bool originalWalkable;
        public int originalElevation;
        public bool originalObjective;
        public bool originalProtectedTerrain;
        public int originalCorruptionStrength;
        public GridEdgeBlock originalEdges;
    }

    static readonly Dictionary<BattleGrid, Dictionary<Vector2Int, Entry>> Active = new();

    public static bool TryApplyLimboStain(
        BattleGrid grid,
        Vector2Int position,
        BattleUnit source,
        int durationTurns,
        out TemporaryTerrainFailure failure) =>
        TryApply(grid, position, TemporaryTerrainKind.LimboStain, source,
            Mathf.Max(1, durationTurns), strength: 1, out failure);

    public static bool TryApply(
        BattleGrid grid,
        Vector2Int position,
        TemporaryTerrainKind kind,
        BattleUnit source,
        int durationTurns,
        int strength,
        out TemporaryTerrainFailure failure)
    {
        failure = TemporaryTerrainFailure.None;
        if (grid == null)
        {
            failure = TemporaryTerrainFailure.MissingGrid;
            return false;
        }
        GridCell cell = grid.GetCell(position);
        if (cell == null)
        {
            failure = TemporaryTerrainFailure.OutOfBounds;
            return false;
        }

        var entries = GetEntries(grid, create: true);
        if (entries.TryGetValue(position, out Entry existing))
        {
            if (existing.kind != kind)
            {
                failure = TemporaryTerrainFailure.NestedTemporaryTerrain;
                return false;
            }
            existing.remainingTurns = Mathf.Max(existing.remainingTurns, Mathf.Max(1, durationTurns));
            TerrainApplied?.Invoke(grid, position, existing.kind);
            return true;
        }

        if (!cell.walkable)
        {
            failure = TemporaryTerrainFailure.Unwalkable;
            return false;
        }
        if (cell.objective)
        {
            failure = TemporaryTerrainFailure.Objective;
            return false;
        }
        if (cell.protectedTerrain)
        {
            failure = TemporaryTerrainFailure.ProtectedTerrain;
            return false;
        }
        if (cell.tileType == TileType.LimboStain || cell.tileType == TileType.GraveMulch)
        {
            failure = TemporaryTerrainFailure.NestedTemporaryTerrain;
            return false;
        }
        int authoredStrength = Mathf.Max(cell.authoredCorruptionStrength,
            cell.tileType == TileType.Void ? 100 : 0);
        if (authoredStrength > strength)
        {
            failure = TemporaryTerrainFailure.StrongerAuthoredCorruption;
            return false;
        }

        entries[position] = new Entry
        {
            kind = kind,
            remainingTurns = Mathf.Max(1, durationTurns),
            sourceIsPlayer = source != null && source.IsPlayer,
            source = source,
            originalTileType = cell.tileType,
            originalWalkable = cell.walkable,
            originalElevation = cell.elevation,
            originalObjective = cell.objective,
            originalProtectedTerrain = cell.protectedTerrain,
            originalCorruptionStrength = cell.authoredCorruptionStrength,
            originalEdges = cell.impassableEdges,
        };
        cell.tileType = kind == TemporaryTerrainKind.LimboStain
            ? TileType.LimboStain
            : TileType.GraveMulch;
        TerrainApplied?.Invoke(grid, position, kind);
        return true;
    }

    public static void ResolveEndTurn(BattleGrid grid, BattleUnit unit)
    {
        if (grid == null || unit == null || !unit.IsAlive) return;
        var entries = GetEntries(grid, create: false);
        if (entries == null || !entries.TryGetValue(unit.gridPosition, out Entry entry)) return;
        if (entry.kind != TemporaryTerrainKind.LimboStain) return;
        if (unit.Data != null && unit.Data.isLimboAligned) return;
        if (unit.IsPlayer == entry.sourceIsPlayer) return;

        int maxHp = unit.Data?.GetTotalStats().hpMax ?? 1;
        int damage = Mathf.Max(1, Mathf.RoundToInt(maxHp * 0.04f));
        unit.TakeDamage(damage, DamageType.Dark, entry.source);
        if (unit.IsAlive)
            unit.ApplyStatus(new StatusEffect(StatusEffectType.Dread, 1, 3f, entry.source));
    }

    public static void TickTurn(BattleGrid grid)
    {
        var entries = GetEntries(grid, create: false);
        if (entries == null || entries.Count == 0) return;

        var expired = new List<Vector2Int>();
        foreach (var pair in entries)
        {
            pair.Value.remainingTurns--;
            if (pair.Value.remainingTurns <= 0) expired.Add(pair.Key);
        }
        foreach (Vector2Int position in expired) Restore(grid, position, entries);
        if (entries.Count == 0) Active.Remove(grid);
    }

    public static void RestoreAll(BattleGrid grid)
    {
        var entries = GetEntries(grid, create: false);
        if (entries == null) return;
        foreach (Vector2Int position in new List<Vector2Int>(entries.Keys))
            Restore(grid, position, entries);
        Active.Remove(grid);
    }

    public static int ActiveCount(BattleGrid grid)
    {
        var entries = GetEntries(grid, create: false);
        return entries?.Count ?? 0;
    }

    public static bool TryGetKind(
        BattleGrid grid,
        Vector2Int position,
        out TemporaryTerrainKind kind)
    {
        var entries = GetEntries(grid, create: false);
        if (entries != null && entries.TryGetValue(position, out Entry entry))
        {
            kind = entry.kind;
            return true;
        }
        kind = default;
        return false;
    }

    public static void ResetForTests()
    {
        foreach (BattleGrid grid in new List<BattleGrid>(Active.Keys))
            if (grid != null) RestoreAll(grid);
        Active.Clear();
    }

    static Dictionary<Vector2Int, Entry> GetEntries(BattleGrid grid, bool create)
    {
        if (grid == null) return null;
        if (Active.TryGetValue(grid, out var entries)) return entries;
        if (!create) return null;
        entries = new Dictionary<Vector2Int, Entry>();
        Active[grid] = entries;
        return entries;
    }

    static void Restore(
        BattleGrid grid,
        Vector2Int position,
        Dictionary<Vector2Int, Entry> entries)
    {
        if (!entries.TryGetValue(position, out Entry entry)) return;
        GridCell cell = grid != null ? grid.GetCell(position) : null;
        if (cell != null)
        {
            cell.tileType = entry.originalTileType;
            cell.walkable = entry.originalWalkable;
            cell.elevation = entry.originalElevation;
            cell.objective = entry.originalObjective;
            cell.protectedTerrain = entry.originalProtectedTerrain;
            cell.authoredCorruptionStrength = entry.originalCorruptionStrength;
            cell.impassableEdges = entry.originalEdges;
        }
        entries.Remove(position);
        TerrainRestored?.Invoke(grid, position, entry.kind);
    }
}
