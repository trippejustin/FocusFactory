using UnityEngine;

public class PermissionRegion : MonoBehaviour
{
    [Header("Region")]
    [SerializeField] private string regionName;
    [SerializeField] private PermissionType permission = PermissionType.Allow;

    [Header("Visual")]
    [SerializeField] private SpriteRenderer regionVisual;

    [SerializeField] private Color allowColor = new Color(0.25f, 0.85f, 0.35f, 0.45f);
    [SerializeField] private Color askColor   = new Color(1.00f, 0.80f, 0.20f, 0.45f);
    [SerializeField] private Color avoidColor = new Color(0.90f, 0.25f, 0.25f, 0.45f);

    public string RegionName => regionName;
    public PermissionType Permission => permission;

    private void Awake()
    {
        UpdateVisual();
    }

    public void SetPermission(PermissionType newPermission)
    {
        permission = newPermission;
        UpdateVisual();
    }

    private void UpdateVisual()
    {
        if (regionVisual == null)
            return;

        switch (permission)
        {
            case PermissionType.Allow:
                regionVisual.color = allowColor;
                break;

            case PermissionType.Ask:
                regionVisual.color = askColor;
                break;

            case PermissionType.Avoid:
                regionVisual.color = avoidColor;
                break;
        }
    }

    private void OnValidate()
    {
        UpdateVisual();
    }
}