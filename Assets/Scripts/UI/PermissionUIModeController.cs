using UnityEngine;

public enum PermissionUIMode
{
    Spatial,
    Conventional
}

public class PermissionUIModeController : MonoBehaviour
{
    [Header("Mode")]
    [SerializeField] private PermissionUIMode startingMode =
        PermissionUIMode.Spatial;

    [Header("UI")]
    [SerializeField] private GameObject permissionSidebar;

    public PermissionUIMode CurrentMode { get; private set; }

    private void Awake()
    {
        var study = StudySessionManager.Instance;

        if (study != null &&
            study.CurrentInterface != StudySessionManager.InterfaceStyle.None)
        {
            ApplyMode(
                study.CurrentInterface == StudySessionManager.InterfaceStyle.Spatial
                    ? PermissionUIMode.Spatial
                    : PermissionUIMode.Conventional
            );
        }
        else
        {
            ApplyMode(startingMode);
        }
    }

    public void SetSpatialMode()
    {
        StudySessionManager.Instance?.ChooseInterface(
            StudySessionManager.InterfaceStyle.Spatial
        );

        ApplyMode(PermissionUIMode.Spatial);
    }

    public void SetConventionalMode()
    {
        StudySessionManager.Instance?.ChooseInterface(
            StudySessionManager.InterfaceStyle.Conventional
        );

        ApplyMode(PermissionUIMode.Conventional);
    }

    public void ToggleMode()
    {
        if (CurrentMode == PermissionUIMode.Spatial)
        {
            SetConventionalMode();
        }
        else
        {
            SetSpatialMode();
        }
    }

    public void ApplyMode(PermissionUIMode mode)
    {
        CurrentMode = mode;

        bool spatial =
            mode == PermissionUIMode.Spatial;

        PermissionRegion[] regions =
            FindObjectsByType<PermissionRegion>(
                FindObjectsSortMode.None
            );

        foreach (PermissionRegion region in regions)
        {
            region.SetSpatialPresentationEnabled(
                spatial
            );
        }

        if (permissionSidebar != null)
        {
            permissionSidebar.SetActive(
                !spatial
            );
        }

        Debug.Log(
            $"Permission UI Mode: {mode}"
        );
    }
}