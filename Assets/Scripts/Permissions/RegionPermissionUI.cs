using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RegionPermissionUI : MonoBehaviour
{
    [SerializeField] private GameObject permissionPanel;

    [SerializeField] private TMP_Text regionNameText;
    [SerializeField] private Button allowButton;
    [SerializeField] private Button askButton;
    [SerializeField] private Button avoidButton;

    private PermissionRegion selectedRegion;

    private void Awake()
    {
        allowButton.onClick.AddListener(SetAllow);
        askButton.onClick.AddListener(SetAsk);
        avoidButton.onClick.AddListener(SetAvoid);

        permissionPanel.SetActive(false);
    }

    private void Update()
    {
        if (!PermissionRegion.CanEditPermissions && permissionPanel.activeSelf)
        {
            selectedRegion = null;
            permissionPanel.SetActive(false);
        }
    }

    public void SelectRegion(PermissionRegion region)
    {
        if (region == null || !PermissionRegion.CanEditPermissions)
            return;

        selectedRegion = region;

        regionNameText.text = selectedRegion.RegionName;

        permissionPanel.SetActive(true);
    }

    private void SetAllow()
    {
        SetPermission(PermissionType.Allow);
    }

    private void SetAsk()
    {
        SetPermission(PermissionType.Ask);
    }

    private void SetAvoid()
    {
        SetPermission(PermissionType.Avoid);
    }

    private void SetPermission(PermissionType permission)
    {
        if (selectedRegion == null)
            return;

        selectedRegion.SetPermission(permission);

        selectedRegion = null;
        permissionPanel.SetActive(false);
    }
}