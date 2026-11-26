using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class RobotCameraController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform robot;
    [SerializeField] private AgentBrain agentBrain;
    [SerializeField] private PermissionManager permissionManager;
    [SerializeField] private Camera targetCamera;

    [Header("Automatic Follow")]
    [SerializeField] private float followZoom = 6.5f;
    [SerializeField] private float positionSmoothTime = 0.35f;
    [SerializeField] private float zoomSmoothTime = 0.20f;

    [Header("User Pan")]
    [SerializeField] private float panSpeed = 1f;

    [Header("User Zoom")]
    [SerializeField] private float zoomSpeed = 0.01f;
    [SerializeField] private float minZoom = 3f;
    [SerializeField] private float maxZoom = 14f;

    private Vector3 overviewPosition;
    private float overviewZoom;

    private Vector3 userPanOffset;
    private float userZoomOffset;

    private Vector3 positionVelocity;
    private float zoomVelocity;

    private bool panGestureStartedOnWorld;
    private bool isDragging;
    private bool wasAgentRunning;

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = GetComponent<Camera>();

        if (targetCamera == null)
        {
            Debug.LogError(
                "RobotCameraController: No Camera assigned or found."
            );

            enabled = false;
            return;
        }

        overviewPosition = transform.position;
        overviewZoom = targetCamera.orthographicSize;

        wasAgentRunning =
            agentBrain != null &&
            agentBrain.IsAgentRunning;
    }

    private void Update()
    {
        HandlePlayStateChange();
        HandlePanInput();
        HandleZoomInput();
    }

    private void LateUpdate()
    {
        bool followMode =
            agentBrain != null &&
            agentBrain.IsAgentRunning &&
            permissionManager != null &&
            !permissionManager.HasPendingRequest &&
            robot != null;

        Vector3 basePosition;
        float baseZoom;

        if (permissionManager != null &&
            permissionManager.HasPendingRequest)
        {
            PermissionRegion requestedRegion =
                permissionManager.PendingRegion;

            Collider2D regionCollider =
                requestedRegion.GetComponentInChildren<Collider2D>();

            Vector3 regionCenter =
                regionCollider != null && regionCollider.enabled
                    ? regionCollider.bounds.center
                    : requestedRegion.transform.position;

            basePosition = new Vector3(
                regionCenter.x,
                regionCenter.y,
                overviewPosition.z
            );

            baseZoom = overviewZoom;
        }
        else if (followMode)
        {
            basePosition = new Vector3(
                robot.position.x,
                robot.position.y,
                overviewPosition.z
            );

            baseZoom = followZoom;
        }
        else
        {
            basePosition = overviewPosition;
            baseZoom = overviewZoom;
        }

        Vector3 targetPosition =
            basePosition + userPanOffset;

        targetPosition.z =
            overviewPosition.z;

        float targetZoom =
            Mathf.Clamp(
                baseZoom + userZoomOffset,
                minZoom,
                maxZoom
            );

        if (!isDragging)
        {
            transform.position =
                Vector3.SmoothDamp(
                    transform.position,
                    targetPosition,
                    ref positionVelocity,
                    positionSmoothTime
                );

            targetCamera.orthographicSize =
                Mathf.SmoothDamp(
                    targetCamera.orthographicSize,
                    targetZoom,
                    ref zoomVelocity,
                    zoomSmoothTime
                );
        }
        else
        {
            transform.position = targetPosition;
            targetCamera.orthographicSize = targetZoom;

            positionVelocity = Vector3.zero;
            zoomVelocity = 0f;
        }
    }

    private void HandlePlayStateChange()
    {
        bool isAgentRunning =
            agentBrain != null &&
            agentBrain.IsAgentRunning;

        // Play was just pressed.
        if (isAgentRunning && !wasAgentRunning)
        {
            userPanOffset = Vector3.zero;
            positionVelocity = Vector3.zero;
        }

        // Pause was just pressed (or the agent stopped).
        if (!isAgentRunning && wasAgentRunning)
        {
            userPanOffset = Vector3.zero;
            userZoomOffset = 0f;
            positionVelocity = Vector3.zero;
            zoomVelocity = 0f;
        }

        wasAgentRunning = isAgentRunning;
    }

    private void HandlePanInput()
    {
        if (agentBrain != null && agentBrain.IsAgentRunning)
        {
            panGestureStartedOnWorld = false;
            isDragging = false;
            return;
        }

        if (Mouse.current == null)
            return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            panGestureStartedOnWorld =
                !IsPointerOverUI();

            isDragging =
                panGestureStartedOnWorld;
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            panGestureStartedOnWorld = false;
            isDragging = false;
            return;
        }

        if (!panGestureStartedOnWorld ||
            !Mouse.current.leftButton.isPressed)
        {
            isDragging = false;
            return;
        }

        isDragging = true;

        Vector2 mouseDelta =
            Mouse.current.delta.ReadValue();

        if (mouseDelta.sqrMagnitude <= 0f)
            return;

        float worldUnitsPerPixel =
            (targetCamera.orthographicSize * 2f) /
            Mathf.Max(1, Screen.height);

        Vector2 worldDelta =
            mouseDelta *
            worldUnitsPerPixel *
            panSpeed;

        userPanOffset -= new Vector3(
            worldDelta.x,
            worldDelta.y,
            0f
        );
    }

    private void HandleZoomInput()
    {
        if (Mouse.current == null ||
            IsPointerOverUI())
        {
            return;
        }

        float scroll =
            Mouse.current.scroll.ReadValue().y;

        if (Mathf.Abs(scroll) < 0.01f)
            return;

        float baseZoom =
            GetCurrentBaseZoom();

        float desiredZoom =
            baseZoom +
            userZoomOffset -
            (scroll * zoomSpeed);

        desiredZoom =
            Mathf.Clamp(
                desiredZoom,
                minZoom,
                maxZoom
            );

        userZoomOffset =
            desiredZoom - baseZoom;

        targetCamera.orthographicSize =
            desiredZoom;

        zoomVelocity = 0f;
    }

    private float GetCurrentBaseZoom()
    {
        bool followMode =
            agentBrain != null &&
            agentBrain.IsAgentRunning &&
            permissionManager != null &&
            !permissionManager.HasPendingRequest &&
            robot != null;

        return followMode
            ? followZoom
            : overviewZoom;
    }

    private bool IsPointerOverUI()
    {
        return EventSystem.current != null &&
               EventSystem.current.IsPointerOverGameObject();
    }

    public void ResetPan()
    {
        userPanOffset = Vector3.zero;
    }

    public void ResetView()
    {
        userPanOffset = Vector3.zero;
        userZoomOffset = 0f;
    }
}