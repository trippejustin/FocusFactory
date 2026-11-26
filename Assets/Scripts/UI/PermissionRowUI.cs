using TMPro;
using UnityEngine;

public class PermissionRowUI : MonoBehaviour
{
    [SerializeField] private TMP_Text regionNameText;
    [SerializeField] private TMP_Dropdown permissionDropdown;

    private PermissionRegion region;
    private PermissionType lastPermission;

    private void Awake()
    {
        permissionDropdown.onValueChanged.AddListener(
            HandleDropdownChanged
        );
    }

    private void OnDestroy()
    {
        permissionDropdown.onValueChanged.RemoveListener(
            HandleDropdownChanged
        );
    }

    public void Setup(PermissionRegion permissionRegion)
    {
        region = permissionRegion;

        regionNameText.text = region.RegionName;
        lastPermission = region.Permission;

        Refresh();
    }

    private void Update()
    {
        if (region == null)
            return;

        permissionDropdown.interactable = PermissionRegion.CanEditPermissions;

        if (region.Permission != lastPermission)
        {
            Refresh();
        }
    }

    private void HandleDropdownChanged(int optionIndex)
    {
        if (region == null)
            return;

        PermissionType newPermission =
            (PermissionType)optionIndex;

        region.SetPermission(newPermission);

        Refresh();
    }

    private void Refresh()
    {
        lastPermission = region.Permission;

        permissionDropdown.SetValueWithoutNotify(
            (int)region.Permission
        );

        permissionDropdown.targetGraphic.color =
            permissionDropdown.options[(int)region.Permission].color;
    }
}