using UnityEngine;

public class AgentActionExecutor : MonoBehaviour
{
    [SerializeField] private RobotController robotController;
    [SerializeField] private RobotInteraction robotInteraction;
    [SerializeField] private PermissionManager permissionManager;
    [SerializeField] private WorldState worldState;
    [SerializeField] private TaskManager taskManager;

    public void ExecuteAction(AgentAction action)
    {
        if (action == null)
            return;

        switch (action.actionType)
        {
            case AgentActionType.MoveTo:
                ExecuteMoveTo(action.targetId);
                break;

            case AgentActionType.PickUp:
                ExecutePickUp(action.targetId);
                break;

            case AgentActionType.Drop:
                ExecuteDrop(action.targetId);
                break;

            case AgentActionType.FinishTask:
                taskManager.CompleteTask();
                break;
        }
    }

    private void ExecuteMoveTo(string targetId)
    {
        WorldObject target = worldState.GetObject(targetId);

        if (target == null)
            return;

        permissionManager.RequestMove(
            robotController,
            target.Position
        );
    }

    private void ExecutePickUp(string targetId)
    {
        WorldObject target = worldState.GetObject(targetId);

        if (target == null)
            return;

        robotInteraction.PickUp(target);
    }

    private void ExecuteDrop(string targetId)
    {
        WorldObject target = worldState.GetObject(targetId);

        if (target == null)
            return;

        robotInteraction.Drop(target.Position);
    }
}