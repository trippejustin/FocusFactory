using UnityEngine;
using UnityEngine.InputSystem;

public class RegionSelectionManager : MonoBehaviour
{
    [SerializeField] private Camera mainCamera;
    [SerializeField] private RegionPermissionUI permissionUI;

    private void Awake()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (permissionUI == null)
        {
            permissionUI = FindFirstObjectByType<RegionPermissionUI>(
                FindObjectsInactive.Include
            );
        }
    }

    private void Update()
    {
        if (Mouse.current == null || mainCamera == null || permissionUI == null)
            return;

        if (!Mouse.current.leftButton.wasPressedThisFrame)
            return;

        Vector2 mouseScreenPosition = Mouse.current.position.ReadValue();

        Vector3 worldPosition =
            mainCamera.ScreenToWorldPoint(mouseScreenPosition);

        Collider2D[] hits =
            Physics2D.OverlapPointAll(worldPosition);

        foreach (Collider2D hit in hits)
        {
            PermissionRegion region =
                hit.GetComponentInParent<PermissionRegion>();

            if (region == null)
                continue;

            permissionUI.SelectRegion(region);
            return;
        }
    }
}