using System;
using UnityEngine;

public class PermissionRegion : MonoBehaviour
{
    public static event Action<
        PermissionRegion,
        PermissionType,
        PermissionType> OnPermissionChanged;

    [Header("Region")]
    [SerializeField] private string regionName;
    [SerializeField] private PermissionType permission = PermissionType.Allow;

    [Header("Visual")]
    [SerializeField] private SpriteRenderer regionVisual;

    [SerializeField] private Color allowColor =
        new Color(0.25f, 0.85f, 0.35f, 0.45f);

    [SerializeField] private Color askColor =
        new Color(1.00f, 0.80f, 0.20f, 0.45f);

    [SerializeField] private Color avoidColor =
        new Color(0.90f, 0.25f, 0.25f, 0.45f);

    [Header("Ambient Permission Pulse")]
    [SerializeField, Range(0.01f, 1f)] private float pulseMinAlpha = 0.15f;
    [SerializeField, Range(0.01f, 1f)] private float pulseMaxAlpha = 0.35f;
    [SerializeField, Min(0.1f)] private float pulseDuration = 3f;

    public string RegionName => regionName;
    public PermissionType Permission => permission;

    private bool spatialPresentationEnabled = true;
    private float presentationAlpha = 1f;

    // -1 means no blink override.
    private float blinkAlphaOverride = -1f;

    private void Awake()
    {
        UpdateVisual();
    }

    private void Update()
    {
        if (spatialPresentationEnabled)
        {
            UpdateVisual();
        }
    }

    private static AgentBrain permissionAgent;

    public static bool CanEditPermissions
    {
        get
        {
            if (!Application.isPlaying)
                return true;

            if (permissionAgent == null)
                permissionAgent = FindFirstObjectByType<AgentBrain>();

            return permissionAgent == null || !permissionAgent.IsAgentRunning;
        }
    }

    public void SetPermission(PermissionType newPermission)
    {
        if (!CanEditPermissions)
            return;

        if (permission == newPermission)
            return;

        PermissionType previousPermission =
            permission;

        permission = newPermission;
        UpdateVisual();

        OnPermissionChanged?.Invoke(
            this,
            previousPermission,
            newPermission
        );
    }

    public void SetPresentationAlpha(float alpha)
    {
        presentationAlpha =
            Mathf.Clamp01(alpha);

        UpdateVisual();
    }

    public void SetBlinkAlphaOverride(float alpha)
    {
        blinkAlphaOverride =
            Mathf.Clamp01(alpha);

        UpdateVisual();
    }

    public void ClearBlinkAlphaOverride()
    {
        blinkAlphaOverride = -1f;
        UpdateVisual();
    }

    public void SetSpatialPresentationEnabled(bool enabled)
    {
        spatialPresentationEnabled = enabled;

        if (regionVisual != null)
        {
            regionVisual.enabled =
                spatialPresentationEnabled;
        }

        RegionPermissionClick[] clickControls =
            GetComponentsInChildren<
                RegionPermissionClick>(true);

        foreach (RegionPermissionClick clickControl
                 in clickControls)
        {
            clickControl.enabled = enabled;
        }

        RegionOutlineBlinker blinker =
            GetComponent<RegionOutlineBlinker>();

        if (blinker != null)
        {
            blinker.SetPresentationEnabled(enabled);
        }

        UpdateVisual();
    }

    private Color GetPermissionColor()
    {
        switch (permission)
        {
            case PermissionType.Allow:
                return allowColor;

            case PermissionType.Ask:
                return askColor;

            case PermissionType.Avoid:
                return avoidColor;

            default:
                return allowColor;
        }
    }

    private void UpdateVisual()
    {
        if (regionVisual == null)
            return;

        regionVisual.enabled =
            spatialPresentationEnabled;

        Color color =
            GetPermissionColor();

        float alpha;

        if (blinkAlphaOverride >= 0f)
        {
            // Preserve the existing permission-request highlight.
            alpha = blinkAlphaOverride * presentationAlpha;
        }
        else if (Application.isPlaying)
        {
            float phase = Time.unscaledTime / Mathf.Max(0.1f, pulseDuration);
            float pulse = (1f - Mathf.Cos(phase * Mathf.PI * 2f)) * 0.5f;
            float min = Mathf.Min(pulseMinAlpha, pulseMaxAlpha);
            float max = Mathf.Max(pulseMinAlpha, pulseMaxAlpha);

            // Keep the overlay visible even during the movement-mode fade.
            alpha = Mathf.Lerp(min, max, pulse) *
                Mathf.Lerp(0.65f, 1f, presentationAlpha);
        }
        else
        {
            alpha = color.a * presentationAlpha;
        }

        color.a = alpha;

        regionVisual.color = color;
    }

    private void OnValidate()
    {
        UpdateVisual();
    }
}
