using System.Collections.Generic;
using UnityEngine;

public class RoutePlanner : MonoBehaviour
{
    [Header("Navigation Areas")]
    [SerializeField] private SpriteRenderer[] navigationAreas;

    [Header("Fallback Bounds")]
    [SerializeField] private Vector2 fallbackWorldMin =
        new Vector2(-20f, -20f);

    [SerializeField] private Vector2 fallbackWorldMax =
        new Vector2(20f, 20f);

    [Header("Pathfinding")]
    [SerializeField] private float cellSize = 0.25f;
    [SerializeField] private float turnPenalty = 0.35f;

    [Header("Robot Footprint")]
    [SerializeField] private Collider2D agentCollider;
    [SerializeField] private float edgePadding = 0.08f;

    private readonly HashSet<PermissionRegion> runtimeBlockedRegions =
        new HashSet<PermissionRegion>();

    private static readonly Vector2Int[] Directions =
    {
        new Vector2Int(1, 0),
        new Vector2Int(-1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(0, -1)
    };

    private void Awake()
    {
        if (agentCollider == null)
        {
            RobotController robot =
                FindFirstObjectByType<RobotController>();

            if (robot != null)
            {
                agentCollider =
                    robot.GetComponent<Collider2D>();
            }
        }

        if (agentCollider == null)
        {
            Debug.LogWarning(
                "RoutePlanner: No robot Collider2D assigned."
            );
        }
    }

    public bool TryFindPath(
        Vector2 start,
        Vector2 destination,
        out List<Vector2> path)
    {
        path = new List<Vector2>();

        if (!IsFootprintInsideNavigationArea(start))
        {
            Debug.LogWarning(
                $"RoutePlanner: Start position {start} is outside navigation areas."
            );

            return false;
        }

        if (!IsFootprintInsideNavigationArea(destination))
        {
            Debug.LogWarning(
                $"RoutePlanner: Destination {destination} is outside navigation areas."
            );

            return false;
        }

        if (IsNavigationBlockedAt(destination))
        {
            Debug.LogWarning(
                $"RoutePlanner: Destination {destination} is blocked."
            );

            return false;
        }

        Vector2 worldMin = GetWorldMin();

        Vector2Int startCell =
            WorldToGrid(start, worldMin);

        Vector2Int destinationCell =
            WorldToGrid(destination, worldMin);

        List<Vector2Int> openSet =
            new List<Vector2Int>();

        HashSet<Vector2Int> closedSet =
            new HashSet<Vector2Int>();

        Dictionary<Vector2Int, Vector2Int> cameFrom =
            new Dictionary<Vector2Int, Vector2Int>();

        Dictionary<Vector2Int, float> gScore =
            new Dictionary<Vector2Int, float>();

        openSet.Add(startCell);
        gScore[startCell] = 0f;

        while (openSet.Count > 0)
        {
            Vector2Int current =
                GetLowestScoreCell(
                    openSet,
                    gScore,
                    destinationCell
                );

            if (current == destinationCell)
            {
                path =
                    ReconstructPath(
                        cameFrom,
                        current,
                        worldMin,
                        destination
                    );

                path =
                    BuildOrthogonalPath(
                        start,
                        path,
                        destination
                    );

                return true;
            }

            openSet.Remove(current);
            closedSet.Add(current);

            foreach (Vector2Int direction in Directions)
            {
                Vector2Int neighbor =
                    current + direction;

                if (closedSet.Contains(neighbor))
                    continue;

                Vector2 neighborWorld =
                    GridToWorld(
                        neighbor,
                        worldMin
                    );

                if (!IsFootprintInsideNavigationArea(
                        neighborWorld))
                {
                    continue;
                }

                if (IsNavigationBlockedAt(
                        neighborWorld))
                {
                    continue;
                }

                float movementCost = 1f;

                if (cameFrom.TryGetValue(
                        current,
                        out Vector2Int parent))
                {
                    Vector2Int incomingDirection =
                        current - parent;

                    if (incomingDirection != direction)
                    {
                        movementCost +=
                            turnPenalty;
                    }
                }

                float tentativeScore =
                    gScore[current] +
                    movementCost;

                if (!gScore.TryGetValue(
                        neighbor,
                        out float existingScore) ||
                    tentativeScore < existingScore)
                {
                    cameFrom[neighbor] =
                        current;

                    gScore[neighbor] =
                        tentativeScore;

                    if (!openSet.Contains(neighbor))
                    {
                        openSet.Add(neighbor);
                    }
                }
            }
        }

        Debug.LogWarning(
            "RoutePlanner: No legal route to destination."
        );

        return false;
    }

    public void BlockRegionForRun(
        PermissionRegion region)
    {
        if (region == null)
            return;

        runtimeBlockedRegions.Add(region);

        Debug.Log(
            $"RoutePlanner: {region.RegionName} is blocked for this run."
        );
    }

    public void ResetRuntimeBlocks()
    {
        runtimeBlockedRegions.Clear();
    }

    public bool IsRegionBlocked(
        PermissionRegion region)
    {
        if (region == null)
            return false;

        return
            region.Permission ==
                PermissionType.Avoid ||
            runtimeBlockedRegions.Contains(region);
    }

    private bool IsNavigationBlockedAt(
        Vector2 position)
    {
        Vector2 footprintSize =
            GetAgentFootprintSize();

        Collider2D[] hits =
            Physics2D.OverlapBoxAll(
                position,
                footprintSize,
                0f
            );

        foreach (Collider2D hit in hits)
        {
            PermissionRegion region =
                hit.GetComponentInParent<
                    PermissionRegion>();

            if (IsRegionBlocked(region))
                return true;

            WorldObject worldObject =
                hit.GetComponentInParent<
                    WorldObject>();

            if (worldObject != null)
            {
                RobotInteraction carrier =
                    worldObject.GetComponentInParent<
                        RobotInteraction>();

                if (carrier == null)
                    return true;
            }

            NavigationObstacle obstacle =
                hit.GetComponentInParent<
                    NavigationObstacle>();

            if (obstacle != null)
                return true;
        }

        return false;
    }

    private List<Vector2> ReconstructPath(
        Dictionary<Vector2Int, Vector2Int> cameFrom,
        Vector2Int current,
        Vector2 worldMin,
        Vector2 exactDestination)
    {
        List<Vector2> reversed =
            new List<Vector2>();

        reversed.Add(exactDestination);

        while (cameFrom.ContainsKey(current))
        {
            current =
                cameFrom[current];

            reversed.Add(
                GridToWorld(
                    current,
                    worldMin
                )
            );
        }

        reversed.Reverse();

        return reversed;
    }

    private List<Vector2> BuildOrthogonalPath(
        Vector2 start,
        List<Vector2> rawPath,
        Vector2 destination)
    {
        List<Vector2> result =
            new List<Vector2>();

        if (rawPath == null ||
            rawPath.Count == 0)
        {
            return result;
        }

        Vector2 anchor = start;
        int nextRawIndex = 0;

        while (nextRawIndex < rawPath.Count)
        {
            bool foundConnection = false;

            // Work backward from the destination and find the farthest
            // raw-path point that can be reached with at most one
            // right-angle turn. This collapses stair-step A* paths into
            // long straight runs.
            for (int i = rawPath.Count - 1;
                 i >= nextRawIndex;
                 i--)
            {
                Vector2 candidate =
                    rawPath[i];

                if (!TryGetOrthogonalConnection(
                        anchor,
                        candidate,
                        out Vector2? elbow))
                {
                    continue;
                }

                if (elbow.HasValue)
                {
                    AddWaypointIfDifferent(
                        result,
                        elbow.Value
                    );
                }

                AddWaypointIfDifferent(
                    result,
                    candidate
                );

                anchor = candidate;
                nextRawIndex = i + 1;
                foundConnection = true;
                break;
            }

            if (foundConnection)
                continue;

            // Safe fallback: advance by one raw A* point.
            // Because the A* search itself is 4-directional, this keeps
            // the route orthogonal instead of introducing a diagonal.
            Vector2 fallback =
                rawPath[nextRawIndex];

            if (TryGetOrthogonalConnection(
                    anchor,
                    fallback,
                    out Vector2? fallbackElbow))
            {
                if (fallbackElbow.HasValue)
                {
                    AddWaypointIfDifferent(
                        result,
                        fallbackElbow.Value
                    );
                }

                AddWaypointIfDifferent(
                    result,
                    fallback
                );

                anchor = fallback;
                nextRawIndex++;
                continue;
            }

            Debug.LogWarning(
                "RoutePlanner: Could not simplify an orthogonal path segment."
            );

            return rawPath;
        }

        // ReconstructPath already ends at the exact destination,
        // but keep this guard in case that behavior changes later.
        if (result.Count == 0 ||
            Vector2.Distance(
                result[result.Count - 1],
                destination
            ) > 0.001f)
        {
            if (TryGetOrthogonalConnection(
                    anchor,
                    destination,
                    out Vector2? finalElbow))
            {
                if (finalElbow.HasValue)
                {
                    AddWaypointIfDifferent(
                        result,
                        finalElbow.Value
                    );
                }

                AddWaypointIfDifferent(
                    result,
                    destination
                );
            }
        }

        RemoveRedundantCollinearWaypoints(
            result
        );

        return result;
    }

    private bool TryGetOrthogonalConnection(
        Vector2 from,
        Vector2 to,
        out Vector2? elbow)
    {
        elbow = null;

        if (Vector2.Distance(
                from,
                to) < 0.001f)
        {
            return true;
        }

        bool sameX =
            Mathf.Abs(
                from.x - to.x) < 0.001f;

        bool sameY =
            Mathf.Abs(
                from.y - to.y) < 0.001f;

        if (sameX || sameY)
        {
            return HasClearOrthogonalSegment(
                from,
                to
            );
        }

        Vector2 horizontalFirstElbow =
            new Vector2(
                to.x,
                from.y
            );

        Vector2 verticalFirstElbow =
            new Vector2(
                from.x,
                to.y
            );

        bool horizontalFirstClear =
            HasClearOrthogonalSegment(
                from,
                horizontalFirstElbow
            ) &&
            HasClearOrthogonalSegment(
                horizontalFirstElbow,
                to
            );

        bool verticalFirstClear =
            HasClearOrthogonalSegment(
                from,
                verticalFirstElbow
            ) &&
            HasClearOrthogonalSegment(
                verticalFirstElbow,
                to
            );

        if (!horizontalFirstClear &&
            !verticalFirstClear)
        {
            return false;
        }

        if (horizontalFirstClear &&
            !verticalFirstClear)
        {
            elbow =
                horizontalFirstElbow;

            return true;
        }

        if (verticalFirstClear &&
            !horizontalFirstClear)
        {
            elbow =
                verticalFirstElbow;

            return true;
        }

        // Both L-shaped options are legal. Prefer the elbow that
        // leaves more room around the robot.
        float horizontalClearance =
            GetLocalClearanceScore(
                horizontalFirstElbow
            );

        float verticalClearance =
            GetLocalClearanceScore(
                verticalFirstElbow
            );

        elbow =
            horizontalClearance >=
            verticalClearance
                ? horizontalFirstElbow
                : verticalFirstElbow;

        return true;
    }

    private bool HasClearOrthogonalSegment(
        Vector2 start,
        Vector2 end)
    {
        bool horizontal =
            Mathf.Abs(
                start.y - end.y) < 0.001f;

        bool vertical =
            Mathf.Abs(
                start.x - end.x) < 0.001f;

        if (!horizontal && !vertical)
            return false;

        float distance =
            Vector2.Distance(
                start,
                end
            );

        if (distance <= 0f)
            return true;

        int steps =
            Mathf.Max(
                1,
                Mathf.CeilToInt(
                    distance /
                    (cellSize * 0.5f)
                )
            );

        for (int i = 1;
             i <= steps;
             i++)
        {
            float t =
                i / (float)steps;

            Vector2 point =
                Vector2.Lerp(
                    start,
                    end,
                    t
                );

            if (!IsFootprintInsideNavigationArea(
                    point))
            {
                return false;
            }

            if (IsNavigationBlockedAt(
                    point))
            {
                return false;
            }
        }

        return true;
    }

    private float GetLocalClearanceScore(
        Vector2 point)
    {
        Vector2 footprint =
            GetAgentFootprintSize();

        float probeDistance =
            Mathf.Max(
                footprint.x,
                footprint.y
            ) * 0.35f;

        Vector2[] probes =
        {
            point + Vector2.right * probeDistance,
            point + Vector2.left * probeDistance,
            point + Vector2.up * probeDistance,
            point + Vector2.down * probeDistance
        };

        float score = 0f;

        foreach (Vector2 probe in probes)
        {
            if (IsFootprintInsideNavigationArea(
                    probe) &&
                !IsNavigationBlockedAt(
                    probe))
            {
                score += 1f;
            }
        }

        return score;
    }

    private void RemoveRedundantCollinearWaypoints(
        List<Vector2> path)
    {
        if (path.Count < 3)
            return;

        for (int i = path.Count - 2;
             i > 0;
             i--)
        {
            Vector2 previous =
                path[i - 1];

            Vector2 current =
                path[i];

            Vector2 next =
                path[i + 1];

            bool sameX =
                Mathf.Abs(
                    previous.x - current.x) < 0.001f &&
                Mathf.Abs(
                    current.x - next.x) < 0.001f;

            bool sameY =
                Mathf.Abs(
                    previous.y - current.y) < 0.001f &&
                Mathf.Abs(
                    current.y - next.y) < 0.001f;

            if (sameX || sameY)
            {
                path.RemoveAt(i);
            }
        }
    }

    private void AddWaypointIfDifferent(
        List<Vector2> path,
        Vector2 point)
    {
        if (path.Count == 0 ||
            Vector2.Distance(
                path[path.Count - 1],
                point
            ) > 0.001f)
        {
            path.Add(point);
        }
    }

    private Vector2Int GetLowestScoreCell(
        List<Vector2Int> openSet,
        Dictionary<Vector2Int, float> gScore,
        Vector2Int goal)
    {
        Vector2Int best =
            openSet[0];

        float bestScore =
            GetFScore(
                best,
                gScore,
                goal
            );

        for (int i = 1;
             i < openSet.Count;
             i++)
        {
            Vector2Int candidate =
                openSet[i];

            float candidateScore =
                GetFScore(
                    candidate,
                    gScore,
                    goal
                );

            if (candidateScore < bestScore)
            {
                best =
                    candidate;

                bestScore =
                    candidateScore;
            }
        }

        return best;
    }

    private float GetFScore(
        Vector2Int cell,
        Dictionary<Vector2Int, float> gScore,
        Vector2Int goal)
    {
        float g =
            gScore.TryGetValue(
                cell,
                out float score)
                ? score
                : float.MaxValue;

        float h =
            Mathf.Abs(cell.x - goal.x) +
            Mathf.Abs(cell.y - goal.y);

        return g + h;
    }

    private Vector2Int WorldToGrid(
        Vector2 world,
        Vector2 worldMin)
    {
        return new Vector2Int(
            Mathf.RoundToInt(
                (world.x - worldMin.x) /
                cellSize
            ),

            Mathf.RoundToInt(
                (world.y - worldMin.y) /
                cellSize
            )
        );
    }

    private Vector2 GridToWorld(
        Vector2Int grid,
        Vector2 worldMin)
    {
        return new Vector2(
            worldMin.x +
            grid.x * cellSize,

            worldMin.y +
            grid.y * cellSize
        );
    }

    private Vector2 GetWorldMin()
    {
        if (TryGetCombinedNavigationBounds(
                out Bounds bounds))
        {
            Vector2 halfSize =
                GetAgentFootprintSize() *
                0.5f;

            return new Vector2(
                bounds.min.x +
                halfSize.x,

                bounds.min.y +
                halfSize.y
            );
        }

        return fallbackWorldMin;
    }

    private bool TryGetCombinedNavigationBounds(
        out Bounds combinedBounds)
    {
        combinedBounds =
            default;

        if (navigationAreas == null ||
            navigationAreas.Length == 0)
        {
            return false;
        }

        bool foundArea =
            false;

        foreach (SpriteRenderer area
                 in navigationAreas)
        {
            if (area == null)
                continue;

            if (!foundArea)
            {
                combinedBounds =
                    area.bounds;

                foundArea =
                    true;
            }
            else
            {
                combinedBounds.Encapsulate(
                    area.bounds
                );
            }
        }

        return foundArea;
    }

    private bool IsFootprintInsideNavigationArea(
        Vector2 position)
    {
        Vector2 halfSize =
            GetAgentFootprintSize() *
            0.5f;

        Vector2[] corners =
        {
            position +
            new Vector2(
                -halfSize.x,
                -halfSize.y
            ),

            position +
            new Vector2(
                -halfSize.x,
                halfSize.y
            ),

            position +
            new Vector2(
                halfSize.x,
                -halfSize.y
            ),

            position +
            new Vector2(
                halfSize.x,
                halfSize.y
            )
        };

        foreach (Vector2 corner in corners)
        {
            if (!IsPointInsideNavigationArea(
                    corner))
            {
                return false;
            }
        }

        return true;
    }

    private bool IsPointInsideNavigationArea(
        Vector2 point)
    {
        if (navigationAreas == null ||
            navigationAreas.Length == 0)
        {
            return IsInsideBounds(
                point,
                fallbackWorldMin,
                fallbackWorldMax
            );
        }

        bool foundArea =
            false;

        foreach (SpriteRenderer area
                 in navigationAreas)
        {
            if (area == null)
                continue;

            foundArea =
                true;

            Bounds bounds =
                area.bounds;

            if (point.x >= bounds.min.x &&
                point.x <= bounds.max.x &&
                point.y >= bounds.min.y &&
                point.y <= bounds.max.y)
            {
                return true;
            }
        }

        if (!foundArea)
        {
            return IsInsideBounds(
                point,
                fallbackWorldMin,
                fallbackWorldMax
            );
        }

        return false;
    }

    private Vector2 GetAgentFootprintSize()
    {
        Vector2 baseSize =
            new Vector2(
                0.8f,
                0.8f
            );

        if (agentCollider != null)
        {
            Bounds bounds =
                agentCollider.bounds;

            baseSize =
                new Vector2(
                    bounds.size.x,
                    bounds.size.y
                );
        }

        float padding =
            Mathf.Max(
                0f,
                edgePadding
            ) * 2f;

        return baseSize +
               Vector2.one * padding;
    }

    private bool IsInsideBounds(
        Vector2 point,
        Vector2 min,
        Vector2 max)
    {
        return
            point.x >= min.x &&
            point.x <= max.x &&
            point.y >= min.y &&
            point.y <= max.y;
    }
}