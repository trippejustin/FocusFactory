using UnityEngine;

public class AgentActionExecutor : MonoBehaviour
{
    [SerializeField] private RobotController robotController;
    [SerializeField] private RobotInteraction robotInteraction;
    [SerializeField] private PermissionManager permissionManager;
    [SerializeField] private WorldState worldState;
    [SerializeField] private TaskManager taskManager;

    [Header("Interaction")]
    [SerializeField] private float interactionRange = 0.8f;

    public bool ExecuteAction(AgentAction action, out string result)
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
                return ExecuteMoveTo(action.targetId, out result);

            case AgentActionType.PickUp:
                return ExecutePickUp(action.targetId, out result);

            case AgentActionType.Drop:
                return ExecuteDrop(action.targetId, out result);

            case AgentActionType.FinishTask:
                taskManager.CompleteTask();
                result = "Task marked complete.";
                return true;
        }

        result = "Unknown action.";
        return false;
    }

    private bool ExecuteMoveTo(string targetId, out string result)
    {
        WorldObject target = worldState.GetObject(targetId);

        if (target == null)
        {
            result = $"MoveTo failed: object '{targetId}' does not exist.";
            return false;
        }

        permissionManager.RequestMove(
            robotController,
            target.Position
        );

        result = $"Movement toward '{targetId}' requested.";
        return true;
    }

    private bool ExecutePickUp(string targetId, out string result)
    {
        WorldObject target = worldState.GetObject(targetId);

        if (target == null)
        {
            result = $"PickUp failed: object '{targetId}' does not exist.";
            return false;
        }

        float distance = Vector2.Distance(
            robotController.transform.position,
            target.Position
        );

        if (distance > interactionRange)
        {
            result =
                $"PickUp failed: '{targetId}' is too far away. " +
                $"Move closer first.";

            return false;
        }

        if (!robotInteraction.PickUp(target))
        {
            result = $"PickUp failed for '{targetId}'.";
            return false;
        }

        result = $"Successfully picked up '{targetId}'.";
        return true;
    }

    private bool ExecuteDrop(string targetId, out string result)
    {
        WorldObject destination = worldState.GetObject(targetId);

        if (destination == null)
        {
            result = $"Drop failed: destination '{targetId}' does not exist.";
            return false;
        }

        if (!robotInteraction.IsCarryingObject)
        {
            result = "Drop failed: the robot is not carrying anything.";
            return false;
        }

        float distance = Vector2.Distance(
            robotController.transform.position,
            destination.Position
        );

        if (distance > interactionRange)
        {
            result =
                $"Drop failed: destination '{targetId}' is too far away. " +
                $"Move closer first.";

            return false;
        }

        if (!robotInteraction.Drop(destination.Position))
        {
            result = $"Drop failed at '{targetId}'.";
            return false;
        }

        result = $"Successfully dropped the object at '{targetId}'.";
        return true;
    }
}