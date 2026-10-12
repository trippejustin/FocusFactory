using System;
using System.Text;
using UnityEngine;

public class AgentBrain : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private OllamaClient ollamaClient;
    [SerializeField] private TaskManager taskManager;
    [SerializeField] private AgentActionExecutor actionExecutor;
    [SerializeField] private RobotController robotController;
    [SerializeField] private RobotInteraction robotInteraction;
    [SerializeField] private PermissionManager permissionManager;
    [SerializeField] private AgentStatusDisplay statusDisplay;

    [Header("Return Home")]
    [SerializeField] private float homeArrivalDistance = 0.15f;

    private bool requestInProgress;
    private bool agentRunning;
    private bool returningHome;
    private Vector2 startingPosition;
    private PermissionRegion pendingPermissionRegion;

    private string lastResult =
        "No actions have been attempted yet.";

    [Serializable]
    private class AgentDecision
    {
        public string action;
        public string targetId;
        public string status;
    }

    private void Start()
    {
        startingPosition = robotController.transform.position;

        robotController.OnMovementStarted += HandleMovementStarted;
        robotController.OnMovementCompleted += HandleMovementCompleted;

        permissionManager.OnPermissionRequested += HandlePermissionRequested;
        permissionManager.OnMoveAvoided += HandleMoveAvoided;
        permissionManager.OnPermissionRejected += HandlePermissionRejected;

        taskManager.OnTaskCompleted += HandleTaskCompleted;
    }

    private void OnDestroy()
    {
        if (robotController != null)
        {
            robotController.OnMovementStarted -= HandleMovementStarted;
            robotController.OnMovementCompleted -= HandleMovementCompleted;
        }

        if (permissionManager != null)
        {
            permissionManager.OnPermissionRequested -= HandlePermissionRequested;
            permissionManager.OnMoveAvoided -= HandleMoveAvoided;
            permissionManager.OnPermissionRejected -= HandlePermissionRejected;
        }

        if (taskManager != null)
        {
            taskManager.OnTaskCompleted -= HandleTaskCompleted;
        }
    }

    public void StartAgent()
    {
        if (agentRunning || returningHome)
            return;

        if (taskManager.CurrentTask == null)
        {
            Debug.LogWarning(
                "AgentBrain: No current task is assigned."
            );

            return;
        }

        agentRunning = true;

        lastResult =
            "Task started. Decide the first action.";

        taskManager.StartTask();

        RequestNextDecision();
    }

    public void RequestNextDecision()
    {
        if (!agentRunning || requestInProgress)
            return;

        requestInProgress = true;

        Debug.Log(
            "AgentBrain: Requesting next action from Ollama..."
        );

        ollamaClient.SendPrompt(
            BuildPrompt(),
            HandleResponse,
            HandleError
        );
    }

    private string BuildPrompt()
    {
        StringBuilder prompt = new StringBuilder();

        prompt.AppendLine(
            "You are an autonomous robot agent operating in a 2D workspace."
        );

        prompt.AppendLine(
            "Choose exactly ONE next action that advances the assigned task."
        );

        prompt.AppendLine(
            "You must make the decisions. Unity only validates and executes your requested actions."
        );

        prompt.AppendLine();
        prompt.AppendLine("AVAILABLE ACTIONS:");

        prompt.AppendLine(
            "- MoveTo: move toward a world object. targetId must be that object's ID."
        );

        prompt.AppendLine(
            "- PickUp: pick up a world object. targetId must be that object's ID."
        );

        prompt.AppendLine(
            "- Drop: drop the currently carried object at another world object. targetId must be the destination object's ID."
        );

        prompt.AppendLine(
            "- FinishTask: use only when the task is actually complete. targetId must be empty."
        );

        prompt.AppendLine(
            "- When the requested object has been successfully dropped at its intended destination, the task is complete. Your next action must be FinishTask."
        );

        prompt.AppendLine(
            "- Do not pick up, move, or drop an object again after it has already been successfully delivered."
        );

        prompt.AppendLine(
            "- Do not repeat a successful action unless the previous action result says it failed."
        );

        prompt.AppendLine();
        prompt.AppendLine("PERMISSION RULES:");

        prompt.AppendLine(
            "- Allow: you may enter freely."
        );

        prompt.AppendLine(
            "- Ask: Unity pauses the requested movement and asks the human for approval."
        );

        prompt.AppendLine(
            "- Avoid: you may not enter or cross that region."
        );

        prompt.AppendLine(
            "- If Unity rejects an action, use the reported result to choose a different action."
        );

        prompt.AppendLine();

        if (returningHome)
        {
            prompt.AppendLine("CURRENT GOAL: RETURN HOME");

            prompt.AppendLine(
                $"The delivery task is complete. Return to your starting position at {FormatPosition(startingPosition)}."
            );

            prompt.AppendLine(
                "Use MoveTo on the available navigation objects/waypoints. The world object with id=home represents your starting position."
            );

            prompt.AppendLine(
                "If a direct movement is blocked by Avoid or the human denies an Ask request, choose a different waypoint and find another route."
            );

            prompt.AppendLine(
                "During the return-home phase, do not PickUp, Drop, or FinishTask again."
            );
        }
        else
        {
            prompt.AppendLine(
                $"TASK: {taskManager.CurrentTask.Description}"
            );
        }

        prompt.AppendLine();

        prompt.AppendLine(
            $"LAST ACTION RESULT: {lastResult}"
        );

        prompt.AppendLine();
        prompt.AppendLine("CURRENT AGENT STATE:");

        prompt.AppendLine(
            $"- Position: {FormatPosition(robotController.transform.position)}"
        );

        if (robotInteraction.IsCarryingObject)
        {
            prompt.AppendLine(
                $"- Carrying: {robotInteraction.CarriedObject.ObjectId}"
            );
        }
        else
        {
            prompt.AppendLine(
                "- Carrying: nothing"
            );
        }

        PermissionRegion robotRegion =
            GetRegionAt(robotController.transform.position);

        if (robotRegion != null)
        {
            prompt.AppendLine(
                $"- Current region: {robotRegion.RegionName} ({robotRegion.Permission})"
            );
        }
        else
        {
            prompt.AppendLine(
                "- Current region: neutral space"
            );
        }

        prompt.AppendLine();
        prompt.AppendLine("WORLD OBJECTS:");

        WorldObject[] worldObjects =
            FindObjectsByType<WorldObject>(
                FindObjectsSortMode.None
            );

        foreach (WorldObject worldObject in worldObjects)
        {
            PermissionRegion region =
                GetRegionAt(worldObject.Position);

            string regionDescription =
                region != null
                    ? $"{region.RegionName} ({region.Permission})"
                    : "neutral space";

            prompt.AppendLine(
                $"- id={worldObject.ObjectId}, " +
                $"name={worldObject.DisplayName}, " +
                $"position={FormatPosition(worldObject.Position)}, " +
                $"region={regionDescription}"
            );
        }

        prompt.AppendLine();
        prompt.AppendLine("REGIONS:");

        PermissionRegion[] regions =
            FindObjectsByType<PermissionRegion>(
                FindObjectsSortMode.None
            );

        foreach (PermissionRegion region in regions)
        {
            prompt.AppendLine(
                $"- {region.RegionName}: {region.Permission}"
            );
        }

        prompt.AppendLine();
        prompt.AppendLine("RESPONSE FORMAT:");

        prompt.AppendLine(
            "Return valid JSON only:"
        );

        prompt.AppendLine(
            "{\"action\":\"MoveTo|PickUp|Drop|FinishTask\"," +
            "\"targetId\":\"object id or empty\"," +
            "\"status\":\"short message describing what you are trying to do\"}"
        );

        return prompt.ToString();
    }

    private void HandleResponse(string response)
    {
        requestInProgress = false;

        AgentDecision decision;

        try
        {
            decision =
                JsonUtility.FromJson<AgentDecision>(
                    response
                );
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"AgentBrain: Could not parse Ollama response.\n" +
                $"{response}\n{exception}"
            );

            agentRunning = false;

            return;
        }

        if (decision == null ||
            string.IsNullOrWhiteSpace(decision.action))
        {
            Debug.LogError(
                $"AgentBrain: Invalid decision:\n{response}"
            );

            agentRunning = false;

            return;
        }

        if (!Enum.TryParse(
                decision.action,
                true,
                out AgentActionType actionType))
        {
            Debug.LogError(
                $"AgentBrain: Unknown action '{decision.action}'."
            );

            lastResult =
                $"Invalid action '{decision.action}'. " +
                "Choose one of the allowed actions.";

            RequestNextDecision();

            return;
        }

        if (returningHome && actionType != AgentActionType.MoveTo)
        {
            lastResult =
                $"Action '{actionType}' was rejected. The task is already complete. " +
                "Return home using MoveTo and the available navigation waypoints.";

            if (statusDisplay != null)
            {
                statusDisplay.SetStatus(
                    "Returning home"
                );
            }

            RequestNextDecision();
            return;
        }

        Debug.Log(
            $"AGENT DECISION: " +
            $"action={actionType}, " +
            $"targetId={decision.targetId}, " +
            $"status={decision.status}"
        );

        if (statusDisplay != null)
        {
            statusDisplay.SetStatus(
                decision.status
            );
        }

        AgentAction action =
            new AgentAction(
                actionType,
                decision.targetId
            );

        bool succeeded = actionExecutor.ExecuteAction(
            action,
            out string actionResult
        );

        lastResult = actionResult;

        if (!succeeded)
        {
            RequestNextDecision();
            return;
        }

        // Movement is asynchronous.
        // Wait for movement completion, rejection,
        // or human permission before asking again.
        if (actionType == AgentActionType.MoveTo)
            return;

        // TaskManager fires the completion event.
        if (actionType == AgentActionType.FinishTask)
            return;

        // PickUp and Drop currently happen immediately.
        // Keep the detailed result returned by AgentActionExecutor so the
        // agent knows exactly what succeeded before choosing its next action.
        RequestNextDecision();
    }

    private void HandlePermissionRequested(
        PermissionRegion region)
    {
        pendingPermissionRegion = region;

        if (statusDisplay != null)
        {
            statusDisplay.SetStatus(
                $"Requesting permission: {region.RegionName}"
            );
        }
    }

    private void HandleMovementStarted()
    {
        if (pendingPermissionRegion == null)
            return;

        if (statusDisplay != null)
        {
            statusDisplay.SetStatus(
                $"Permission granted: {pendingPermissionRegion.RegionName}"
            );
        }

        pendingPermissionRegion = null;
    }

    private void HandleMovementCompleted()
    {
        if (!agentRunning)
            return;

        if (returningHome)
        {
            float distanceFromHome = Vector2.Distance(
                robotController.transform.position,
                startingPosition
            );

            if (distanceFromHome <= homeArrivalDistance)
            {
                returningHome = false;
                agentRunning = false;
                requestInProgress = false;

                lastResult =
                    "Returned to starting position.";

                if (statusDisplay != null)
                {
                    statusDisplay.SetStatus(
                        "Returned to start"
                    );
                }

                Debug.Log(
                    "AgentBrain: Robot returned to its starting position."
                );

                return;
            }

            lastResult =
                "Movement completed successfully, but the robot is not home yet. " +
                "Choose the next waypoint toward home.";

            RequestNextDecision();
            return;
        }

        lastResult =
            "Movement completed successfully.";

        RequestNextDecision();
    }

    private void HandleMoveAvoided(
        PermissionRegion region)
    {
        pendingPermissionRegion = null;

        if (returningHome)
        {
            lastResult =
                $"Return-home movement was rejected because it conflicts with " +
                $"Avoid region '{region.RegionName}'. Choose a different waypoint or route.";

            if (statusDisplay != null)
            {
                statusDisplay.SetStatus(
                    $"Blocked by {region.RegionName}"
                );
            }

            Debug.Log(
                $"AgentBrain: Return-home route blocked by Avoid region '{region.RegionName}'."
            );

            RequestNextDecision();
            return;
        }

        if (!agentRunning)
            return;

        lastResult =
            $"Movement was rejected because the requested " +
            $"route or destination conflicts with Avoid region " +
            $"'{region.RegionName}'.";

        if (statusDisplay != null)
        {
            statusDisplay.SetStatus(
                $"Blocked by {region.RegionName}"
            );
        }

        RequestNextDecision();
    }

    private void HandlePermissionRejected(
        PermissionRegion region)
    {
        pendingPermissionRegion = null;

        if (statusDisplay != null)
        {
            statusDisplay.SetStatus(
                $"Permission denied: {region.RegionName}"
            );
        }

        if (returningHome)
        {
            lastResult =
                $"The human denied permission to enter Ask region '{region.RegionName}' " +
                "while returning home. Choose a different waypoint or route.";

            Debug.Log(
                $"AgentBrain: Human denied permission while returning home through '{region.RegionName}'."
            );

            RequestNextDecision();
            return;
        }

        if (!agentRunning)
            return;

        lastResult =
            $"The human denied permission to enter Ask region " +
            $"'{region.RegionName}'.";

        RequestNextDecision();
    }

    private void HandleTaskCompleted(
        TaskDefinition task)
    {
        returningHome = true;
        agentRunning = true;
        requestInProgress = false;

        lastResult =
            "Delivery task complete. The next goal is to return to the starting position.";

        if (statusDisplay != null)
        {
            statusDisplay.SetStatus(
                "Task complete — planning return"
            );
        }

        Debug.Log(
            "AgentBrain: Delivery complete. Beginning agentic return-home phase."
        );

        RequestNextDecision();
    }

    private void HandleError(string error)
    {
        requestInProgress = false;
        agentRunning = false;

        Debug.LogError(
            $"AgentBrain: {error}"
        );
    }

    private PermissionRegion GetRegionAt(
        Vector2 position)
    {
        Physics2D.SyncTransforms();

        Collider2D[] hits =
            Physics2D.OverlapPointAll(position);

        foreach (Collider2D hit in hits)
        {
            PermissionRegion region =
                hit.GetComponentInParent<PermissionRegion>();

            if (region != null)
                return region;
        }

        return null;
    }

    private string FormatPosition(
        Vector2 position)
    {
        return
            $"({position.x:F2}, {position.y:F2})";
    }
}