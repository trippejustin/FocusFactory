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

        permissionManager.OnPermissionRequested +=
            ShowRequest;

        approveButton.onClick.AddListener(
            Approve
        );

        rejectButton.onClick.AddListener(
            Reject
        );
    }

    private void Update()
    {
        if (panel.activeSelf &&
            !permissionManager.HasPendingRequest)
        {
            panel.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (permissionManager != null)
        {
            permissionManager.OnPermissionRequested -=
                ShowRequest;
        }

        if (approveButton != null)
        {
            approveButton.onClick.RemoveListener(
                Approve
            );
        }

        if (rejectButton != null)
        {
            rejectButton.onClick.RemoveListener(
                Reject
            );
        }
    }

    private void ShowRequest(
        PermissionRegion region)
    {
        messageText.text =
            $"Enter {region.RegionName}?";

        panel.SetActive(true);
    }

    private void Approve()
    {
        // Hide the current prompt BEFORE advancing the queue.
        // If another Ask region is waiting, ApprovePendingRequest()
        // immediately fires OnPermissionRequested and ShowRequest()
        // reopens the panel for the next region.
        panel.SetActive(false);
        permissionManager.ApprovePendingRequest();
    }

    private void Reject()
    {
        panel.SetActive(false);
        permissionManager.RejectPendingRequest();
    }
}