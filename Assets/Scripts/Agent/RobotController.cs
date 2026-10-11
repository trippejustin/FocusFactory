using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class RobotController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float arrivalDistance = 0.05f;

    private Rigidbody2D rb;
    private readonly Queue<Vector2> waypoints = new();

    private Vector2 destination;
    private bool isMoving;

    public bool IsMoving => isMoving;
    public Vector2 Destination => destination;

    public event Action OnMovementStarted;
    public event Action OnMovementCompleted;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void FixedUpdate()
    {
        if (!isMoving)
            return;

        Vector2 currentPosition = rb.position;

        if (Vector2.Distance(currentPosition, destination) <= arrivalDistance)
        {
            rb.MovePosition(destination);

            if (waypoints.Count > 0)
            {
                destination = waypoints.Dequeue();
            }
            else
            {
                isMoving = false;
                OnMovementCompleted?.Invoke();
            }

            return;
        }

        Vector2 nextPosition = Vector2.MoveTowards(
            currentPosition,
            destination,
            moveSpeed * Time.fixedDeltaTime
        );

        rb.MovePosition(nextPosition);
    }

    public void MoveTo(Vector2 newDestination)
    {
        waypoints.Clear();
        destination = newDestination;
        isMoving = true;

        OnMovementStarted?.Invoke();
    }

    public void FollowPath(List<Vector2> path)
    {
        if (path == null || path.Count == 0)
            return;

        waypoints.Clear();

        destination = path[0];

        for (int i = 1; i < path.Count; i++)
        {
            waypoints.Enqueue(path[i]);
        }

        isMoving = true;
        OnMovementStarted?.Invoke();
    }

    public void Stop()
    {
        waypoints.Clear();
        isMoving = false;
    }
}