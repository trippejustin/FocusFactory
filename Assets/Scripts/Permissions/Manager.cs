using System;
using System.Collections.Generic;
using UnityEngine;

public class PermissionManager : MonoBehaviour
{
    [SerializeField] private RoutePlanner routePlanner;

    public event Action<PermissionRegion> OnPermissionRequested;
    public event Action<PermissionRegion> OnPermissionRejected;
    public event Action<PermissionRegion> OnMoveAvoided;
    public event Action<PermissionRegion> OnRouteBlocked;

    [Header("Boundary Requests")]
    [SerializeField, Min(0f)] private float boundaryClearance = 0.12f;

    private RobotController pendingRobot;
    private PermissionRegion pendingRegion;
    private RobotController activeRobot;
    private Collider2D activeCollider;

    // Approval applies to one entry, not all future visits.
    private readonly HashSet<PermissionRegion> approvedRegions = new();
    private readonly HashSet<PermissionRegion> enteredApprovedRegions = new();

    public bool HasPendingRequest =>
        pendingRobot != null && pendingRegion != null;

    public PermissionRegion PendingRegion =>
        pendingRegion;

    public void ResetRuntimePermissions()
    {
        CancelCurrentMovement();

        if (routePlanner != null)
        {
            routePlanner.ResetRuntimeBlocks();
        }

        Debug.Log(
            "PermissionManager: Runtime permission decisions reset."
        );
    }

    // Backward-compatible overload for existing debug/test scripts.
    public void RequestMove(
        RobotController robot,
        Vector2 destination)
    {
        RequestMove(
            robot,
            destination,
            out _
        );
    }

    public bool RequestMove(
        RobotController robot,
        Vector2 destination,
        out string result)
    {
        if (pendingRobot != null)
        {
            result =
                "A movement request is already pending.";

            Debug.LogWarning(result);
            return false;
        }

        PermissionRegion destinationRegion =
            FindRegionAt(destination);

        // Both configured Avoid regions and previously denied Ask
        // regions count as blocked for this run.
        if (destinationRegion != null &&
            routePlanner.IsRegionBlocked(destinationRegion))
        {
            result =
                $"Destination is inside blocked region " +
                $"'{destinationRegion.RegionName}'.";

            Debug.Log(
                $"BLOCKED: {result}"
            );

            StudyDataLogger.RecordRouteBlocked(destinationRegion.RegionName);

            OnMoveAvoided?.Invoke(
                destinationRegion
            );

            return false;
        }

        if (!routePlanner.TryFindPath(
                robot.transform.position,
                destination,
                out List<Vector2> path))
        {
            result =
                "No legal route to destination.";

            Debug.LogWarning(result);
            return false;
        }

        if (path == null || path.Count == 0)
        {
            result = "No movement path was produced.";
            return false;
        }

        CancelCurrentMovement();
        activeRobot = robot;
        activeCollider = robot.GetComponent<Collider2D>();
        // Only a robot whose center actually starts inside a room may leave
        // without requesting entry. Clearance overlap alone is not approval.
        Physics2D.SyncTransforms();
        foreach (Collider2D trigger in FindObjectsByType<Collider2D>(FindObjectsSortMode.None))
        {
            PermissionRegion region = GetRoomRegion(trigger);
            if (region != null && trigger.enabled &&
                trigger.OverlapPoint(robot.transform.position))
            {
                approvedRegions.Add(region);
                enteredApprovedRegions.Add(region);
            }
        }
        robot.OnMovementCompleted += HandleRouteCompleted;
        robot.FollowPath(path, CheckMovementStep);

        result = "Movement started. Ask requests occur at region boundaries.";
        return true;
    }

    public void ApprovePendingRequest()
    {
        if (!HasPendingRequest)
            return;

        RobotController robot = pendingRobot;
        PermissionRegion region = pendingRegion;

        if (routePlanner.IsRegionBlocked(region))
        {
            RejectPendingRequest();
            return;
        }

        Debug.Log($"APPROVED: Permission granted for {region.RegionName}.");
        StudyDataLogger.RecordAskApproved(region.RegionName);
        StopCurrentRegionBlink();
        ClearPendingRequest();
        approvedRegions.Add(region);
        robot.ResumePath();
    }

    public void RejectPendingRequest()
    {
        if (!HasPendingRequest)
            return;

        PermissionRegion rejectedRegion = pendingRegion;
        Debug.Log($"REJECTED: Permission denied for {rejectedRegion.RegionName}.");
        StudyDataLogger.RecordAskDenied(rejectedRegion.RegionName);

        // Keep the existing run-scoped denial policy. Clear the old path
        // before notifying the agent, so replanning can begin immediately.
        routePlanner.BlockRegionForRun(rejectedRegion);
        CancelCurrentMovement();
        OnPermissionRejected?.Invoke(rejectedRegion);
    }

    public void CancelCurrentMovement()
    {
        StopCurrentRegionBlink();
        ClearPendingRequest();

        if (activeRobot != null)
        {
            activeRobot.OnMovementCompleted -= HandleRouteCompleted;
            activeRobot.Stop();
        }

        activeRobot = null;
        activeCollider = null;
        approvedRegions.Clear();
        enteredApprovedRegions.Clear();
    }

    private void HandleRouteCompleted()
    {
        // AgentBrain may start a new action from the same completion event.
        // Only clean up if this route is still finished.
        if (activeRobot != null && !activeRobot.IsMoving && !HasPendingRequest)
        {
            activeRobot.OnMovementCompleted -= HandleRouteCompleted;
            activeRobot = null;
            activeCollider = null;
            approvedRegions.Clear();
            enteredApprovedRegions.Clear();
        }
    }

    private bool CheckMovementStep(Vector2 current, Vector2 next)
    {
        if (activeRobot == null || HasPendingRequest)
            return false;

        Vector2 delta = next - current;
        if (delta.sqrMagnitude <= 0.00000001f)
            return true;

        Physics2D.SyncTransforms();

        // Sweep the robot's footprint, with clearance, rather than checking
        // only its center. The robot stops outside the room before asking.
        Vector2 centerOffset = Vector2.zero;
        Vector2 size = new Vector2(0.8f, 0.8f);
        if (activeCollider != null)
        {
            Bounds bounds = activeCollider.bounds;
            size = new Vector2(bounds.size.x, bounds.size.y);
            centerOffset = (Vector2)bounds.center - (Vector2)activeRobot.transform.position;
        }

        size += Vector2.one * (2f * Mathf.Max(0f, boundaryClearance));
        Vector2 origin = current + centerOffset;
        HashSet<PermissionRegion> currentRegions = GetRegionsAt(origin, size);

        // Once the robot exits a region, its approval expires.
        enteredApprovedRegions.RemoveWhere(region =>
        {
            if (currentRegions.Contains(region))
                return false;
            approvedRegions.Remove(region);
            return true;
        });

        foreach (PermissionRegion region in approvedRegions)
        {
            if (currentRegions.Contains(region))
                enteredApprovedRegions.Add(region);
        }

        // Include current and next overlaps as well as swept hits. A cast
        // starting inside the clearance margin must not silently grant entry.
        List<PermissionRegion> candidates = new();
        foreach (PermissionRegion region in currentRegions)
            candidates.Add(region);

        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = true;
        List<RaycastHit2D> hits = new();
        Physics2D.BoxCast(origin, size, 0f, delta.normalized, filter, hits, delta.magnitude);
        hits.Sort((a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit2D hit in hits)
        {
            PermissionRegion region = GetRoomRegion(hit.collider);
            if (region != null && !candidates.Contains(region))
                candidates.Add(region);
        }
        foreach (PermissionRegion region in GetRegionsAt(origin + delta, size))
        {
            if (!candidates.Contains(region))
                candidates.Add(region);
        }

        foreach (PermissionRegion region in candidates)
        {
            if (approvedRegions.Contains(region))
                continue;

            if (routePlanner.IsRegionBlocked(region))
            {
                CancelCurrentMovement();
                StudyDataLogger.RecordRouteBlocked(region.RegionName);
                OnMoveAvoided?.Invoke(region);
                OnRouteBlocked?.Invoke(region);
                return false;
            }

            if (region.Permission != PermissionType.Ask || approvedRegions.Contains(region))
                continue;

            pendingRobot = activeRobot;
            pendingRegion = region;
            pendingRobot.PausePath();

            Debug.Log($"ASK: Permission required at the boundary of {region.RegionName}.");
            StudyDataLogger.RecordAskRequested(region.RegionName);
            StartCurrentRegionBlink();
            OnPermissionRequested?.Invoke(region);
            return false;
        }

        return true;
    }

    private void StartCurrentRegionBlink()
    {
        if (pendingRegion == null)
            return;

        RegionOutlineBlinker blinker =
            pendingRegion.GetComponent<
                RegionOutlineBlinker>();

        if (blinker == null)
        {
            blinker =
                pendingRegion.GetComponentInChildren<
                    RegionOutlineBlinker>();
        }

        if (blinker != null)
        {
            blinker.StartBlinking();
        }
    }

    private void StopCurrentRegionBlink()
    {
        if (pendingRegion == null)
            return;

        RegionOutlineBlinker blinker =
            pendingRegion.GetComponent<
                RegionOutlineBlinker>();

        if (blinker == null)
        {
            blinker =
                pendingRegion.GetComponentInChildren<
                    RegionOutlineBlinker>();
        }

        if (blinker != null)
        {
            blinker.StopBlinking();
        }
    }

    private HashSet<PermissionRegion> GetRegionsAt(Vector2 position, Vector2 size)
    {
        HashSet<PermissionRegion> regions = new();
        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = true;
        List<Collider2D> hits = new();
        Physics2D.OverlapBox(position, size, 0f, filter, hits);

        foreach (Collider2D hit in hits)
        {
            PermissionRegion region = GetRoomRegion(hit);
            if (region != null)
                regions.Add(region);
        }

        return regions;
    }

    private PermissionRegion FindRegionAt(
        Vector2 position)
    {
        Physics2D.SyncTransforms();

        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = true;
        List<Collider2D> hits = new();
        Physics2D.OverlapPoint(position, filter, hits);

        foreach (Collider2D hit in hits)
        {
            PermissionRegion region = GetRoomRegion(hit);

            if (region != null)
            {
                return region;
            }
        }

        return null;
    }

    private static PermissionRegion GetRoomRegion(Collider2D collider)
    {
        if (collider == null || !collider.isTrigger || collider.gameObject.name != "RoomTrigger")
            return null;

        PermissionRegion region = collider.GetComponentInParent<PermissionRegion>();
        if (region != null)
            return region;

        // RoomTrigger and PermissionRegion are siblings in the room prefab.
        Transform room = collider.transform.parent;
        if (room == null)
            return null;

        foreach (Transform child in room)
        {
            region = child.GetComponent<PermissionRegion>();
            if (region != null)
                return region;
        }

        return null;
    }

    private void ClearPendingRequest()
    {
        pendingRobot = null;
        pendingRegion = null;

    }
}