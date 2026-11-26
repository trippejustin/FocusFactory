using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider2D))]
public class RegionPermissionClick : MonoBehaviour
{
    [SerializeField] private float maxClickMovement = 8f;
    [SerializeField] private Camera mainCamera;

    private PermissionRegion region;
    private Collider2D clickCollider;

    private Vector2 mouseDownPosition;
    private bool validClickStarted;

    private void Awake()
    {
        region =
            GetComponentInParent<PermissionRegion>();

        clickCollider =
            GetComponent<Collider2D>();

        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    private void Update()
    {
        if (Mouse.current == null ||
            mainCamera == null ||
            clickCollider == null ||
            region == null)
        {
            return;
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
            BeginClick();

        if (Mouse.current.leftButton.wasReleasedThisFrame)
            EndClick();
    }

    private void BeginClick()
    {
        validClickStarted = false;

        if (!PermissionRegion.CanEditPermissions || IsPointerOverUI())
            return;

        Vector2 screenPosition =
            Mouse.current.position.ReadValue();

        if (!IsInsideOwnCollider(screenPosition))
            return;

        mouseDownPosition = screenPosition;
        validClickStarted = true;
    }

    private void EndClick()
    {
        if (!validClickStarted)
            return;

        validClickStarted = false;

        if (IsPointerOverUI())
            return;

        Vector2 mouseUpPosition =
            Mouse.current.position.ReadValue();

        float movement =
            Vector2.Distance(
                mouseDownPosition,
                mouseUpPosition
            );

        // Camera drag, not a click.
        if (movement > maxClickMovement)
            return;

        if (!IsInsideOwnCollider(mouseUpPosition))
            return;

        CyclePermission();
    }

    private bool IsInsideOwnCollider(Vector2 screenPosition)
    {
        Vector3 worldPosition =
            mainCamera.ScreenToWorldPoint(
                new Vector3(
                    screenPosition.x,
                    screenPosition.y,
                    -mainCamera.transform.position.z
                )
            );

        return clickCollider.OverlapPoint(worldPosition);
    }

    private void CyclePermission()
    {
        switch (region.Permission)
        {
            case PermissionType.Allow:
                region.SetPermission(PermissionType.Ask);
                break;

            case PermissionType.Ask:
                region.SetPermission(PermissionType.Avoid);
                break;

            case PermissionType.Avoid:
                region.SetPermission(PermissionType.Allow);
                break;
        }
    }

    private bool IsPointerOverUI()
    {
        return EventSystem.current != null &&
               EventSystem.current.IsPointerOverGameObject();
    }
}