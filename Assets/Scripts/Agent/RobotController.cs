using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class RobotController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float arrivalDistance = 0.05f;

    [Header("Visuals")]
    [SerializeField] private RobotVisualController visualController;

    private Rigidbody2D rb;
    private readonly Queue<Vector2> waypoints = new();

    private Vector2 destination;
    private bool isMoving;
    private Func<Vector2, Vector2, bool> movementGuard;

    public bool IsMoving => isMoving;
    public Vector2 Destination => destination;

    public event Action OnMovementStarted;
    public event Action OnMovementCompleted;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        if (visualController == null)
        {
            visualController =
                GetComponent<RobotVisualController>();
        }
    }

    private void FixedUpdate()
    {
        if (!isMoving)
            return;

        Vector2 currentPosition =
            rb.position;

        // Do not finish a waypoint until physics has actually reached it.
        if (Vector2.Distance(currentPosition, destination) <= 0.001f)
        {
            if (waypoints.Count > 0)
            {
                destination = waypoints.Dequeue();
            }
            else
            {
                isMoving = false;
                movementGuard = null;
                OnMovementCompleted?.Invoke();
            }

            return;
        }

        Vector2 nextPosition = Vector2.Distance(
                currentPosition, destination) <= arrivalDistance
            ? destination
            : Vector2.MoveTowards(
                currentPosition,
                destination,
                moveSpeed * Time.fixedDeltaTime
            );

        // Permission checks happen before every physical movement step.
        if (movementGuard != null &&
            !movementGuard(currentPosition, nextPosition))
        {
            return;
        }

        Vector2 movementDirection =
            nextPosition - currentPosition;

        if (visualController != null)
        {
            visualController.SetMovementDirection(
                movementDirection
            );
        }

        rb.MovePosition(nextPosition);
    }

    public void MoveTo(Vector2 newDestination)
    {
        waypoints.Clear();
        movementGuard = null;

        destination =
            newDestination;

        isMoving = true;

        OnMovementStarted?.Invoke();
    }

    public void FollowPath(List<Vector2> path)
    {
        FollowPath(path, null);
    }

    public void FollowPath(
        List<Vector2> path,
        Func<Vector2, Vector2, bool> guard)
    {
        if (path == null ||
            path.Count == 0)
        {
            return;
        }

        waypoints.Clear();
        movementGuard = guard;

        destination =
            path[0];

        for (int i = 1;
             i < path.Count;
             i++)
        {
            waypoints.Enqueue(
                path[i]
            );
        }

        isMoving = true;

        OnMovementStarted?.Invoke();
    }

    public void PausePath()
    {
        isMoving = false;
        rb.linearVelocity = Vector2.zero;
    }

    public void ResumePath()
    {
        if (isMoving)
            return;

        isMoving = true;
        OnMovementStarted?.Invoke();
    }

    public void Stop()
    {
        waypoints.Clear();
        movementGuard = null;
        isMoving = false;
        if (rb != null)
            rb.linearVelocity = Vector2.zero;
    }
}