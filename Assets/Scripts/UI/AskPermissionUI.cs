using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AskPermissionUI : MonoBehaviour
{
    [SerializeField] private PermissionManager permissionManager;
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button approveButton;
    [SerializeField] private Button rejectButton;

    private void Start()
    {
        panel.SetActive(false);

        permissionManager.OnPermissionRequested += ShowRequest;

        approveButton.onClick.AddListener(Approve);
        rejectButton.onClick.AddListener(Reject);
    }

    private void ShowRequest(PermissionRegion region)
    {
        messageText.text = $"Enter {region.RegionName}?";
        panel.SetActive(true);
    }

    private void Approve()
    {
        permissionManager.ApprovePendingRequest();
        panel.SetActive(false);
    }

    private void Reject()
    {
        permissionManager.RejectPendingRequest();
        panel.SetActive(false);
    }
}