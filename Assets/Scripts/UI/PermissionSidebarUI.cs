using UnityEngine;

public class PermissionSidebarUI : MonoBehaviour
{
    [SerializeField] private Transform regionList;
    [SerializeField] private PermissionRowUI permissionRowPrefab;

    private void Start()
    {
        BuildSidebar();
    }

    private void BuildSidebar()
    {
        PermissionRegion[] regions =
            FindObjectsByType<PermissionRegion>(
                FindObjectsSortMode.None
            );

        foreach (PermissionRegion region in regions)
        {
            PermissionRowUI row =
                Instantiate(
                    permissionRowPrefab,
                    regionList
                );

            row.Setup(region);
        }
    }
}