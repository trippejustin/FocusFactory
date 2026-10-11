using System;
using System.Collections.Generic;
using UnityEngine;

public class PermissionManager : MonoBehaviour
{
    [SerializeField] private RoutePlanner routePlanner;

    public event Action<PermissionRegion> OnPermissionRequested;
    public event Action<PermissionRegion> OnPermissionRejected;
    public event Action<PermissionRegion> OnMoveAvoided;

    private RobotController pendingRobot;
    private List<Vector2> pendingPath;
    private PermissionRegion pendingRegion;

    private readonly Queue<PermissionRegion> pendingAskRegions = new();

    public bool HasPendingRequest =>
        pendingRobot != null && pendingRegion != null;

    public PermissionRegion PendingRegion => pendingRegion;

    public void RequestMove(RobotController robot, Vector2 destination)
    {
        if (pendingRobot != null)
        {
            Debug.LogWarning("A movement request is already pending.");
            return;
        }

        PermissionRegion destinationRegion = FindRegionAt(destination);

        // The destination itself cannot be inside an Avoid region.
        if (destinationRegion != null &&
            destinationRegion.Permission == PermissionType.Avoid)
        {
            Debug.Log(
                $"AVOID: Destination is inside {destinationRegion.RegionName}."
            );

            OnMoveAvoided?.Invoke(destinationRegion);
            return;
        }

        if (!routePlanner.TryFindPath(
                robot.transform.position,
                destination,
                out List<Vector2> path))
        {
            Debug.LogWarning("No legal route to destination.");
            return;
        }

        List<PermissionRegion> askRegions =
            FindAskRegionsAlongPath(robot.transform.position, path);

        if (askRegions.Count == 0)
        {
            robot.FollowPath(path);
            return;
        }

        pendingRobot = robot;
        pendingPath = path;

        foreach (PermissionRegion region in askRegions)
        {
            pendingAskRegions.Enqueue(region);
        }

        RequestNextPermission();
    }

    public void ApprovePendingRequest()
    {
        if (!HasPendingRequest)
            return;

        Debug.Log(
            $"APPROVED: Permission granted for {pendingRegion.RegionName}."
        );

        pendingRegion = null;
        RequestNextPermission();
    }

    public void RejectPendingRequest()
    {
        if (!HasPendingRequest)
            return;

        Debug.Log(
            $"REJECTED: Permission denied for {pendingRegion.RegionName}."
        );

        OnPermissionRejected?.Invoke(pendingRegion);

        ClearPendingRequest();
    }

    private void RequestNextPermission()
    {
        if (pendingAskRegions.Count > 0)
        {
            pendingRegion = pendingAskRegions.Dequeue();

            Debug.Log(
                $"ASK: Permission required for {pendingRegion.RegionName}."
            );

            OnPermissionRequested?.Invoke(pendingRegion);
            return;
        }

        RobotController robot = pendingRobot;
        List<Vector2> path = pendingPath;

        ClearPendingRequest();

        robot.FollowPath(path);
    }

    private List<PermissionRegion> FindAskRegionsAlongPath(
        Vector2 start,
        List<Vector2> path)
    {
        Physics2D.SyncTransforms();

        List<PermissionRegion> foundRegions = new();
        HashSet<PermissionRegion> alreadyFound = new();

        Vector2 segmentStart = start;

        foreach (Vector2 waypoint in path)
        {
            float distance = Vector2.Distance(segmentStart, waypoint);
            int samples = Mathf.Max(
                2,
                Mathf.CeilToInt(distance / 0.1f)
            );

            for (int i = 1; i <= samples; i++)
            {
                Vector2 point = Vector2.Lerp(
                    segmentStart,
                    waypoint,
                    i / (float)samples
                );

                Collider2D[] hits = Physics2D.OverlapPointAll(point);

                foreach (Collider2D hit in hits)
                {
                    PermissionRegion region =
                        hit.GetComponentInParent<PermissionRegion>();

                    if (region == null)
                        continue;

                    if (region.Permission != PermissionType.Ask)
                        continue;

                    if (alreadyFound.Contains(region))
                        continue;

                    alreadyFound.Add(region);
                    foundRegions.Add(region);
                }
            }

            segmentStart = waypoint;
        }

        return foundRegions;
    }

    private PermissionRegion FindRegionAt(Vector2 position)
    {
        Physics2D.SyncTransforms();

        Collider2D[] hits = Physics2D.OverlapPointAll(position);

        foreach (Collider2D hit in hits)
        {
            PermissionRegion region =
                hit.GetComponentInParent<PermissionRegion>();

            if (region != null)
                return region;
        }

        return null;
    }

    private void ClearPendingRequest()
    {
        pendingRobot = null;
        pendingPath = null;
        pendingRegion = null;

        pendingAskRegions.Clear();
    }
}