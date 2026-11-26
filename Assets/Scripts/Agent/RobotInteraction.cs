using UnityEngine;

public class RobotInteraction : MonoBehaviour
{
    [SerializeField] private Transform carryPoint;
    [SerializeField] private TaskManager taskManager;

    [Header("Contamination")]
    [SerializeField] private bool startContaminated;
    [SerializeField] private ParticleSystem contaminationParticles;

    public bool IsContaminated { get; private set; }

    public void Contaminate()
    {
        if (!IsContaminated)
        {
            IsContaminated = true;
            UpdateContaminationVisual();
            StudyDataLogger.RecordRobotContaminated();
            Debug.Log("Robot contaminated.");
        }

        if (carriedObject != null)
            carriedObject.GetComponent<BatteryContamination>()?.Contaminate();
    }

    public void Clean()
    {
        IsContaminated = false;
        UpdateContaminationVisual();
    }

    private void UpdateContaminationVisual()
    {
        if (contaminationParticles == null) return;
        if (IsContaminated) contaminationParticles.Play(true);
        else contaminationParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private WorldObject carriedObject;

    public WorldObject CarriedObject => carriedObject;
    public bool IsCarryingObject => carriedObject != null;

    private void Awake()
    {
        IsContaminated = startContaminated;
        UpdateContaminationVisual();

        if (taskManager == null)
        {
            taskManager =
                FindFirstObjectByType<TaskManager>();
        }
    }

    public bool PickUp(WorldObject worldObject)
    {
        if (worldObject == null || carriedObject != null)
            return false;

        if (taskManager != null && !taskManager.CanPickUpForDelivery(worldObject.ObjectId))
            return false;

        carriedObject = worldObject;

        carriedObject.transform.SetParent(carryPoint);
        carriedObject.transform.localPosition = Vector3.zero;

        BatteryContamination battery = carriedObject.GetComponent<BatteryContamination>();
        if (IsContaminated || (battery != null && battery.IsContaminated))
            Contaminate();

        Debug.Log(
            $"Picked up {carriedObject.DisplayName}."
        );

        if (taskManager != null && taskManager.UsesOrderedDeliveries)
        {
            taskManager.RecordDeliveryPickup(carriedObject.ObjectId);
        }
        else if (taskManager != null &&
            carriedObject.ObjectId == "battery")
        {
            taskManager.CompleteStep(
                "pickup_battery"
            );
        }

        return true;
    }

    public bool Drop(WorldObject destination)
    {
        if (carriedObject == null ||
            destination == null)
        {
            return false;
        }

        ObjectReceiver receiver =
            destination.GetComponent<ObjectReceiver>();

        if (receiver != null)
        {
            WorldObject deliveredObject =
                carriedObject;

            if (taskManager != null && !taskManager.CanInstallForDelivery(deliveredObject.ObjectId, destination.ObjectId))
                return false;

            if (receiver.TryReceive(deliveredObject))
            {
                carriedObject = null;

                // Hide it immediately so the agent's next
                // world-state prompt no longer sees it while
                // Unity waits until end-of-frame to Destroy it.
                deliveredObject.gameObject.SetActive(false);

                Debug.Log(
                    $"Delivered {deliveredObject.DisplayName} " +
                    $"to {destination.DisplayName}."
                );

                if (taskManager != null && taskManager.UsesOrderedDeliveries)
                {
                    taskManager.RecordDeliveryInstallation(deliveredObject.ObjectId, destination.ObjectId);
                }
                else if (taskManager != null &&
                    taskManager.CurrentTask != null &&
                    deliveredObject.ObjectId == "battery" &&
                    destination.ObjectId ==
                        taskManager.CurrentTask.DestinationObjectId)
                {
                    taskManager.CompleteStep(
                        "install_battery"
                    );
                }

                return true;
            }

            if (taskManager != null && taskManager.UsesOrderedDeliveries)
                return false;
        }

        WorldObject droppedObject =
            carriedObject;

        droppedObject.transform.SetParent(null);
        droppedObject.transform.position =
            destination.Position;

        carriedObject = null;

        Debug.Log(
            $"Dropped {droppedObject.DisplayName} " +
            $"at {destination.DisplayName}."
        );

        return true;
    }
}