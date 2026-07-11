using System.Collections.Generic;
using UnityEngine;

public enum ForcedMovementFailure
{
    None,
    MissingGrid,
    MissingTarget,
    DeadTarget,
    SourceCoincident,
    InvalidOccupancy,
    OutOfBounds,
    Occupied,
    Reserved,
    Unwalkable,
    Elevation,
    ImpassableEdge,
    Objective,
    ProtectedActor,
    Immovable,
}

public static class ForcedMovementService
{
    public static bool TryPullOneCell(
        BattleGrid grid,
        BattleUnit target,
        Vector2Int sourcePosition,
        out ForcedMovementFailure failure)
    {
        failure = ValidateTarget(grid, target, sourcePosition);
        if (failure != ForcedMovementFailure.None) return false;

        Vector2Int delta = sourcePosition - target.gridPosition;
        var candidates = BuildTowardCandidates(delta);
        ForcedMovementFailure firstFailure = ForcedMovementFailure.OutOfBounds;

        for (int i = 0; i < candidates.Count; i++)
        {
            Vector2Int destination = target.gridPosition + candidates[i];
            ForcedMovementFailure candidateFailure = ValidateStep(grid, target, destination);
            if (candidateFailure != ForcedMovementFailure.None)
            {
                if (i == 0) firstFailure = candidateFailure;
                continue;
            }

            grid.MoveUnit(target, destination);
            target.SetFacingToward(sourcePosition);
            failure = ForcedMovementFailure.None;
            return true;
        }

        failure = firstFailure;
        return false;
    }

    static ForcedMovementFailure ValidateTarget(
        BattleGrid grid,
        BattleUnit target,
        Vector2Int sourcePosition)
    {
        if (grid == null) return ForcedMovementFailure.MissingGrid;
        if (target == null) return ForcedMovementFailure.MissingTarget;
        if (!target.IsAlive) return ForcedMovementFailure.DeadTarget;
        if (target.gridPosition == sourcePosition) return ForcedMovementFailure.SourceCoincident;
        if (target.Data != null && target.Data.protectedFromForcedMovement)
            return ForcedMovementFailure.ProtectedActor;
        if (target.Data != null && target.Data.immovable)
            return ForcedMovementFailure.Immovable;

        GridCell current = grid.GetCell(target.gridPosition);
        if (current == null || current.occupant != target)
            return ForcedMovementFailure.InvalidOccupancy;
        if (current.objective) return ForcedMovementFailure.Objective;
        return ForcedMovementFailure.None;
    }

    static ForcedMovementFailure ValidateStep(
        BattleGrid grid,
        BattleUnit target,
        Vector2Int destination)
    {
        if (!grid.InBounds(destination)) return ForcedMovementFailure.OutOfBounds;

        GridCell from = grid.GetCell(target.gridPosition);
        GridCell to = grid.GetCell(destination);
        if (to == null) return ForcedMovementFailure.OutOfBounds;
        if (to.IsOccupied) return ForcedMovementFailure.Occupied;
        if (to.reserved) return ForcedMovementFailure.Reserved;
        if (!to.walkable) return ForcedMovementFailure.Unwalkable;
        if (to.objective) return ForcedMovementFailure.Objective;

        if (Mathf.Abs(to.elevation - from.elevation) > Mathf.Max(0, grid.baseJumpHeight))
            return ForcedMovementFailure.Elevation;

        Vector2Int direction = destination - target.gridPosition;
        if (from.BlocksEdge(direction) || to.BlocksEdge(-direction))
            return ForcedMovementFailure.ImpassableEdge;

        return ForcedMovementFailure.None;
    }

    static List<Vector2Int> BuildTowardCandidates(Vector2Int delta)
    {
        var candidates = new List<Vector2Int>(2);
        Vector2Int horizontal = delta.x == 0
            ? Vector2Int.zero
            : new Vector2Int(delta.x > 0 ? 1 : -1, 0);
        Vector2Int vertical = delta.y == 0
            ? Vector2Int.zero
            : new Vector2Int(0, delta.y > 0 ? 1 : -1);

        if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
        {
            if (horizontal != Vector2Int.zero) candidates.Add(horizontal);
            if (vertical != Vector2Int.zero) candidates.Add(vertical);
        }
        else
        {
            if (vertical != Vector2Int.zero) candidates.Add(vertical);
            if (horizontal != Vector2Int.zero) candidates.Add(horizontal);
        }
        return candidates;
    }
}
