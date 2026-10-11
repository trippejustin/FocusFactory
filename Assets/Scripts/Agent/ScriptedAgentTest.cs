using UnityEngine;
using UnityEngine.InputSystem;

public class ScriptedAgentTest : MonoBehaviour
{
    [SerializeField] private AgentActionExecutor actionExecutor;
    [SerializeField] private RobotController robotController;
    [SerializeField] private PermissionManager permissionManager;
    [SerializeField] private TaskManager taskManager;

    private AgentAction[] actions;
    private int currentActionIndex;
    private bool taskRunning;

    private void Start()
    {
        actions = new AgentAction[]
        {
            new AgentAction(AgentActionType.MoveTo, "battery"),
            new AgentAction(AgentActionType.PickUp, "battery"),
            new AgentAction(AgentActionType.MoveTo, "machine"),
            new AgentAction(AgentActionType.Drop, "machine"),
            new AgentAction(AgentActionType.FinishTask)
        };

        robotController.OnMovementCompleted += HandleMovementCompleted;
        permissionManager.OnMoveAvoided += HandleMoveAvoided;
    }

    private void OnDestroy()
    {
        robotController.OnMovementCompleted -= HandleMovementCompleted;
        permissionManager.OnMoveAvoided -= HandleMoveAvoided;
    }

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.spaceKey.wasPressedThisFrame && !taskRunning)
        {
            StartTestTask();
        }
    }

    private void StartTestTask()
    {
        currentActionIndex = 0;
        taskRunning = true;

        taskManager.StartTask();

        ExecuteNextAction();
    }

    private void ExecuteNextAction()
    {
        while (taskRunning && currentActionIndex < actions.Length)
        {
            AgentAction action = actions[currentActionIndex];

            Debug.Log(
                $"Executing action: {action.actionType} {action.targetId}"
            );

            actionExecutor.ExecuteAction(action);
            currentActionIndex++;

            // Movement takes time, so wait until the robot arrives.
            if (action.actionType == AgentActionType.MoveTo)
                return;

            if (action.actionType == AgentActionType.FinishTask)
            {
                taskRunning = false;
                return;
            }
        }
    }

    private void HandleMovementCompleted()
    {
        if (!taskRunning)
            return;

        ExecuteNextAction();
    }

    private void HandleMoveAvoided(PermissionRegion region)
    {
        if (!taskRunning)
            return;

        Debug.Log($"Task stopped because the agent must avoid {region.RegionName}.");
        taskRunning = false;
    }
}