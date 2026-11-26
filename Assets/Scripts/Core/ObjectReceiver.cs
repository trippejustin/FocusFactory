using UnityEngine;

public class ObjectReceiver : MonoBehaviour
{
    [Header("Accepted Object")]
    [SerializeField] private string acceptedObjectId;

    [Header("Inactive Visual")]
    [SerializeField] private SpriteRenderer inactiveSpriteRenderer;

    [Header("Active Visual")]
    [SerializeField] private GameObject activeAnimatedVisual;

    [Header("Hazard Activation (Optional)")]
    [SerializeField] private HazardRegion[] hazardsToActivate;
    [SerializeField] private HazardRegion.HazardMode activatedHazardMode = HazardRegion.HazardMode.Cycling;

    [SerializeField] private HazardRegion[] permanentHazardsToActivate;

    public bool HasReceivedObject { get; private set; }

    private void Start()
    {
        SetActiveState(false);
    }

    public bool TryReceive(WorldObject worldObject)
    {
        if (worldObject == null)
            return false;

        if (HasReceivedObject)
            return false;

        if (worldObject.ObjectId != acceptedObjectId)
            return false;

        BatteryContamination battery =
            worldObject.GetComponent<BatteryContamination>();

        if (battery != null && battery.IsContaminated)
            return false;

        Debug.Log(
            $"{gameObject.name} received {worldObject.DisplayName}."
        );

        HasReceivedObject = true;

        // Battery is consumed by the machine.
        Destroy(worldObject.gameObject);

        SetActiveState(true);

        if (hazardsToActivate != null)
        {
            foreach (HazardRegion hazard in hazardsToActivate)
            {
                if (hazard != null)
                    hazard.SetMode(activatedHazardMode);
            }
        }

        if (permanentHazardsToActivate != null)
        {
            foreach (HazardRegion hazard in permanentHazardsToActivate)
            {
                if (hazard != null)
                    hazard.SetMode(HazardRegion.HazardMode.Permanent);
            }
        }

        return true;
    }

    private void SetActiveState(bool active)
    {
        if (inactiveSpriteRenderer != null)
        {
            inactiveSpriteRenderer.enabled = !active;
        }

        if (activeAnimatedVisual != null)
        {
            activeAnimatedVisual.SetActive(active);
        }
    }
}