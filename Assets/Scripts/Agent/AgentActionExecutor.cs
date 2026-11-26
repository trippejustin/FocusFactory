using UnityEngine;

public class AgentActionExecutor : MonoBehaviour
{
    [SerializeField] private RobotController robotController;
    [SerializeField] private RobotInteraction robotInteraction;
    [SerializeField] private PermissionManager permissionManager;
    [SerializeField] private WorldState worldState;
    [SerializeField] private TaskManager taskManager;
    [SerializeField] private DecontaminationPoint decontaminationPoint;

    [Header("Interaction")]
    [SerializeField] private float interactionRange = 0.8f;

    private void Awake()
    {
        if (decontaminationPoint == null)
        {
            decontaminationPoint =
                FindFirstObjectByType<DecontaminationPoint>();
        }
    }

    public bool ExecuteAction(
        AgentAction action,
        out string result)
    {
        result = "";

        if (action == null)
        {
            result = "Action was null.";
            return false;
        }

        switch (action.actionType)
        {
            case AgentActionType.MoveTo:
                return ExecuteMoveTo(
                    action.targetId,
                    out result
                );

            case AgentActionType.PickUp:
                return ExecutePickUp(
                    action.targetId,
                    out result
                );

            case AgentActionType.Drop:
                return ExecuteDrop(
                    action.targetId,
                    out result
                );

            case AgentActionType.Decontaminate:
                return ExecuteDecontaminate(
                    out result
                );

            case AgentActionType.FinishTask:
                if (!taskManager.AreAllStepsComplete())
                {
                    result =
                        "FinishTask failed: required task steps are still incomplete.";

                    return false;
                }

                taskManager.CompleteTask();
                result = "Task marked complete.";
                return true;
        }

        result = "Unknown action.";
        return false;
    }

    private bool ExecuteMoveTo(
        string targetId,
        out string result)
    {
        WorldObject target =
            worldState.GetObject(targetId);

        if (target == null)
        {
            result =
                $"MoveTo failed: object '{targetId}' does not exist.";

            return false;
        }

        bool accepted =
            permissionManager.RequestMove(
                robotController,
                target.InteractionPosition,
                out string moveResult
            );

        if (!accepted)
        {
            result =
                $"MoveTo '{targetId}' failed: " +
                moveResult;

            return false;
        }

        result =
            $"MoveTo '{targetId}' accepted: " +
            moveResult;

        return true;
    }

    private bool ExecutePickUp(
        string targetId,
        out string result)
    {
        WorldObject target =
            worldState.GetObject(targetId);

        if (target == null)
        {
            result =
                $"PickUp failed: object '{targetId}' does not exist.";

            return false;
        }

        float distance =
            Vector2.Distance(
                robotController.transform.position,
                target.InteractionPosition
            );

        if (distance > interactionRange)
        {
            result =
                $"PickUp failed: '{targetId}' is too far away. " +
                $"Move closer first.";

            return false;
        }

        if (taskManager != null && !taskManager.CanPickUpForDelivery(targetId))
        {
            result = "PickUp failed: complete the current ordered delivery first.";
            return false;
        }

        if (!robotInteraction.PickUp(target))
        {
            result =
                $"PickUp failed for '{targetId}'.";

            return false;
        }

        HazardRegion.CheckExposure(robotInteraction);

        result =
            $"Successfully picked up '{targetId}'.";

        return true;
    }


    private bool ExecuteDecontaminate(
        out string result)
    {
        if (decontaminationPoint == null)
        {
            result =
                "Decontaminate failed: no decontamination point exists.";

            return false;
        }

        float distance =
            Vector2.Distance(
                robotController.transform.position,
                decontaminationPoint.transform.position
            );

        if (distance > interactionRange)
        {
            result =
                "Decontaminate failed: move closer to the decontamination station first.";

            return false;
        }

        if (!decontaminationPoint.Decontaminate(
                robotInteraction))
        {
            result =
                "Decontaminate failed: robot could not be cleaned.";

            return false;
        }

        result =
            robotInteraction.IsCarryingObject
                ? "Successfully decontaminated the robot and any carried battery."
                : "Successfully decontaminated the robot.";

        return true;
    }

    private bool ExecuteDrop(
        string targetId,
        out string result)
    {
        WorldObject destination =
            worldState.GetObject(targetId);

        if (destination == null)
        {
            result =
                $"Drop failed: destination '{targetId}' does not exist.";

            return false;
        }

        if (!robotInteraction.IsCarryingObject)
        {
            result =
                "Drop failed: the robot is not carrying anything.";

            return false;
        }

        HazardRegion.CheckExposure(robotInteraction);

        BatteryContamination battery =
            robotInteraction.CarriedObject.GetComponent<BatteryContamination>();

        bool isTaskDestination = taskManager != null &&
            taskManager.CurrentTask != null &&
            targetId == taskManager.CurrentTask.DestinationObjectId;

        bool isReceiver = destination.GetComponent<ObjectReceiver>() != null;

        if (isReceiver && taskManager != null &&
            !taskManager.CanInstallForDelivery(robotInteraction.CarriedObject.ObjectId, targetId))
        {
            result = "Drop failed: use the machine assigned to the current ordered delivery.";
            return false;
        }

        if (battery != null && battery.IsContaminated &&
            (isTaskDestination || isReceiver))
        {
            result = "Drop failed: the battery is contaminated. Decontaminate it first.";
            return false;
        }

        bool requiresDecontaminationStep = battery == null;

        if (battery != null && taskManager != null)
        {
            foreach (TaskManager.TaskStep step in taskManager.TaskSteps)
            {
                if (step.id == "decontaminate_battery")
                {
                    requiresDecontaminationStep = true;
                    break;
                }
            }
        }

        if (isTaskDestination && requiresDecontaminationStep &&
            !taskManager.IsStepComplete("decontaminate_battery"))
        {
            result =
                "Drop failed: the battery must be decontaminated before installation.";

            return false;
        }

        float distance =
            Vector2.Distance(
                robotController.transform.position,
                destination.InteractionPosition
            );

        if (distance > interactionRange)
        {
            result =
                $"Drop failed: destination '{targetId}' is too far away. " +
                $"Move closer first.";

            return false;
        }

        if (!robotInteraction.Drop(destination))
        {
            result =
                $"Drop failed at '{targetId}'.";

            return false;
        }

        result =
            $"Successfully delivered the carried object to '{targetId}'.";

        return true;
    }
}