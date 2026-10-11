using System.Collections.Generic;
using UnityEngine;

public class RoutePlanner : MonoBehaviour
{
    [Header("Room Bounds")]
    [SerializeField] private Vector2 worldMin = new Vector2(-5.8f, -3.8f);
    [SerializeField] private Vector2 worldMax = new Vector2(5.8f, 3.8f);

    [Header("Pathfinding")]
    [SerializeField] private float cellSize = 0.25f;
    [SerializeField] private float agentClearance = 0.2f;

    private static readonly Vector2Int[] Directions =
    {
        new Vector2Int(1, 0),
        new Vector2Int(-1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(0, -1),
        new Vector2Int(1, 1),
        new Vector2Int(1, -1),
        new Vector2Int(-1, 1),
        new Vector2Int(-1, -1)
    };

    public bool TryFindPath(Vector2 start, Vector2 destination, out List<Vector2> path)
    {
        path = new List<Vector2>();

        Vector2Int startCell = WorldToCell(start);
        Vector2Int goalCell = WorldToCell(destination);

        List<Vector2Int> open = new List<Vector2Int> { startCell };
        HashSet<Vector2Int> closed = new HashSet<Vector2Int>();

        Dictionary<Vector2Int, Vector2Int> cameFrom =
            new Dictionary<Vector2Int, Vector2Int>();

        Dictionary<Vector2Int, float> gScore =
            new Dictionary<Vector2Int, float>
            {
                [startCell] = 0f
            };

        while (open.Count > 0)
        {
            Vector2Int current = GetLowestScoreCell(open, gScore, goalCell);

            if (current == goalCell)
            {
                path = BuildPath(cameFrom, current);

                if (path.Count > 0)
                    path[path.Count - 1] = destination;

                return true;
            }

            open.Remove(current);
            closed.Add(current);

            foreach (Vector2Int direction in Directions)
            {
                Vector2Int neighbor = current + direction;

                if (closed.Contains(neighbor))
                    continue;

                Vector2 neighborWorld = CellToWorld(neighbor);

                if (!IsInsideBounds(neighborWorld))
                    continue;

                if (neighbor != goalCell && IsBlockedByAvoidRegion(neighborWorld))
                    continue;

                float moveCost = direction.x != 0 && direction.y != 0
                    ? 1.4142f
                    : 1f;

                float tentativeScore = gScore[current] + moveCost;

                if (!gScore.TryGetValue(neighbor, out float existingScore) ||
                    tentativeScore < existingScore)
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeScore;

                    if (!open.Contains(neighbor))
                        open.Add(neighbor);
                }
            }
        }

        Debug.LogWarning("RoutePlanner: No legal route found.");
        return false;
    }

    private Vector2Int GetLowestScoreCell(
        List<Vector2Int> open,
        Dictionary<Vector2Int, float> gScore,
        Vector2Int goal)
    {
        Vector2Int best = open[0];
        float bestScore = GetScore(best, gScore, goal);

        for (int i = 1; i < open.Count; i++)
        {
            float score = GetScore(open[i], gScore, goal);

            if (score < bestScore)
            {
                best = open[i];
                bestScore = score;
            }
        }

        return best;
    }

    private float GetScore(
        Vector2Int cell,
        Dictionary<Vector2Int, float> gScore,
        Vector2Int goal)
    {
        float g = gScore.TryGetValue(cell, out float value)
            ? value
            : float.PositiveInfinity;

        float h = Vector2Int.Distance(cell, goal);
        return g + h;
    }

    private List<Vector2> BuildPath(
        Dictionary<Vector2Int, Vector2Int> cameFrom,
        Vector2Int current)
    {
        List<Vector2Int> cells = new List<Vector2Int> { current };

        while (cameFrom.TryGetValue(current, out Vector2Int previous))
        {
            current = previous;
            cells.Add(current);
        }

        cells.Reverse();

        List<Vector2> worldPath = new List<Vector2>();

        for (int i = 1; i < cells.Count; i++)
        {
            worldPath.Add(CellToWorld(cells[i]));
        }

        return SimplifyPath(worldPath);
    }

    private List<Vector2> SimplifyPath(List<Vector2> original)
    {
        if (original.Count <= 2)
            return original;

        List<Vector2> simplified = new List<Vector2>
        {
            original[0]
        };

        Vector2 previousDirection =
            (original[1] - original[0]).normalized;

        for (int i = 1; i < original.Count - 1; i++)
        {
            Vector2 nextDirection =
                (original[i + 1] - original[i]).normalized;

            if (Vector2.Dot(previousDirection, nextDirection) < 0.999f)
            {
                simplified.Add(original[i]);
            }

            previousDirection = nextDirection;
        }

        simplified.Add(original[original.Count - 1]);
        return simplified;
    }

    private bool IsBlockedByAvoidRegion(Vector2 position)
    {
        Physics2D.SyncTransforms();

        Collider2D[] hits =
            Physics2D.OverlapCircleAll(position, agentClearance);

        foreach (Collider2D hit in hits)
        {
            PermissionRegion region =
                hit.GetComponentInParent<PermissionRegion>();

            if (region != null &&
                region.Permission == PermissionType.Avoid)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsInsideBounds(Vector2 point)
    {
        return point.x >= worldMin.x &&
               point.x <= worldMax.x &&
               point.y >= worldMin.y &&
               point.y <= worldMax.y;
    }

    private Vector2Int WorldToCell(Vector2 world)
    {
        int x = Mathf.RoundToInt((world.x - worldMin.x) / cellSize);
        int y = Mathf.RoundToInt((world.y - worldMin.y) / cellSize);

        return new Vector2Int(x, y);
    }

    private Vector2 CellToWorld(Vector2Int cell)
    {
        return new Vector2(
            worldMin.x + cell.x * cellSize,
            worldMin.y + cell.y * cellSize
        );
    }
}
