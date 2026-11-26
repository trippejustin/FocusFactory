using UnityEngine;

public class ScenePresentationController : MonoBehaviour
{
    [Header("State References")]
    [SerializeField] private AgentBrain agentBrain;
    [SerializeField] private OllamaClient ollamaClient;
    [SerializeField] private PermissionManager permissionManager;

    [Header("Room Name Signs")]
    [SerializeField] private SpriteRenderer[] roomNameSigns;

    [SerializeField] private float signFlashSpeed = 2f;
    [SerializeField] private float signMinAlpha = 0.35f;
    [SerializeField] private float signMaxAlpha = 1f;
    [SerializeField] private float signFadeSpeed = 4f;

    [Header("Permission Overlays")]
    [SerializeField] private float actionPermissionAlpha = 0.05f;
    [SerializeField] private float permissionFadeSpeed = 3.5f;

    private PermissionRegion[] permissionRegions;

    private float currentSignAlpha = 1f;
    private float currentPermissionAlpha = 1f;

    private void Start()
    {
        RefreshPermissionRegions();

        currentSignAlpha = 1f;
        currentPermissionAlpha = 1f;

        ApplyPermissionAlpha();
    }

    private void Update()
    {
        bool stopped =
            agentBrain == null ||
            !agentBrain.IsAgentRunning;

        bool asking =
            permissionManager != null &&
            permissionManager.HasPendingRequest;

        bool llmPlanning =
            ollamaClient != null &&
            ollamaClient.HasActiveRequest;

        bool planning =
            stopped ||
            llmPlanning ||
            asking;

        bool actionMode =
            !stopped &&
            !asking &&
            !llmPlanning;

        UpdateRoomSigns(
            planning
        );

        UpdatePermissionOverlays(
            actionMode
        );
    }

    public void RefreshPermissionRegions()
    {
        permissionRegions =
            FindObjectsByType<PermissionRegion>(
                FindObjectsSortMode.None
            );
    }

    private void UpdateRoomSigns(
        bool planning)
    {
        float targetAlpha;

        if (planning)
        {
            float pulse =
                (Mathf.Sin(
                    Time.time *
                    signFlashSpeed *
                    Mathf.PI * 2f
                ) + 1f) * 0.5f;

            targetAlpha =
                Mathf.Lerp(
                    signMinAlpha,
                    signMaxAlpha,
                    pulse
                );
        }
        else
        {
            targetAlpha = 0f;
        }

        currentSignAlpha =
            Mathf.MoveTowards(
                currentSignAlpha,
                targetAlpha,
                signFadeSpeed *
                Time.deltaTime
            );

        if (roomNameSigns == null)
            return;

        foreach (SpriteRenderer sign
                 in roomNameSigns)
        {
            if (sign == null)
                continue;

            Color color = sign.color;
            color.a = currentSignAlpha;
            sign.color = color;
        }
    }

    private void UpdatePermissionOverlays(
        bool actionMode)
    {
        float targetAlpha =
            actionMode
                ? actionPermissionAlpha
                : 1f;

        float nextAlpha =
            Mathf.MoveTowards(
                currentPermissionAlpha,
                targetAlpha,
                permissionFadeSpeed *
                Time.deltaTime
            );

        if (Mathf.Approximately(
                nextAlpha,
                currentPermissionAlpha))
        {
            return;
        }

        currentPermissionAlpha =
            nextAlpha;

        ApplyPermissionAlpha();
    }

    private void ApplyPermissionAlpha()
    {
        if (permissionRegions == null)
            return;

        foreach (PermissionRegion region
                 in permissionRegions)
        {
            if (region == null)
                continue;

            region.SetPresentationAlpha(
                currentPermissionAlpha
            );
        }
    }
}
