using System;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

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

    private bool requestInProgress;
    private bool agentRunning;
    private string lastResult = "No actions have been attempted yet.";

    [Serializable]
    private class AgentDecision
    {
        public string action;
        public string targetId;
        public string status;
    }

    private void Start()
    {
        robotController.OnMovementCompleted += HandleMovementCompleted;
        permissionManager.OnMoveAvoided += HandleMoveAvoided;
        permissionManager.OnPermissionRejected += HandlePermissionRejected;
        taskManager.OnTaskCompleted += HandleTaskCompleted;
    }

    private void OnDestroy()
    {
        if (robotController != null)
            robotController.OnMovementCompleted -= HandleMovementCompleted;

        if (permissionManager != null)
        {
            permissionManager.OnMoveAvoided -= HandleMoveAvoided;
            permissionManager.OnPermissionRejected -= HandlePermissionRejected;
        }

        if (taskManager != null)
            taskManager.OnTaskCompleted -= HandleTaskCompleted;
    }

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        // Temporary prototype control:
        // Press G once to start the autonomous agent loop.
        if (Keyboard.current.gKey.wasPressedThisFrame && !agentRunning)
        {
            StartAgent();
        }
    }

    private void StartAgent()
    {
        if (taskManager.CurrentTask == null)
        {
            Debug.LogWarning("AgentBrain: No current task is assigned.");
            return;
        }

        agentRunning = true;
        lastResult = "Task started. Decide the first action.";

        taskManager.StartTask();
        RequestNextDecision();
    }

    public void RequestNextDecision()
    {
        if (!agentRunning || requestInProgress)
            return;

        requestInProgress = true;

        Debug.Log("AgentBrain: Requesting next action from Ollama...");

        ollamaClient.SendPrompt(
            BuildPrompt(),
            HandleResponse,
            HandleError
        );
    }

    private string BuildPrompt()
    {
        StringBuilder prompt = new StringBuilder();

        prompt.AppendLine("You are an autonomous robot agent operating in a 2D workspace.");
        prompt.AppendLine("Choose exactly ONE next action that advances the assigned task.");
        prompt.AppendLine("You must make the decisions. Unity only validates and executes your requested actions.");

        prompt.AppendLine();
        prompt.AppendLine("AVAILABLE ACTIONS:");
        prompt.AppendLine("- MoveTo: move toward a world object. targetId must be that object's ID.");
        prompt.AppendLine("- PickUp: pick up a world object. targetId must be that object's ID.");
        prompt.AppendLine("- Drop: drop the currently carried object at another world object. targetId must be the destination object's ID.");
        prompt.AppendLine("- FinishTask: use only when the task is actually complete. targetId must be empty.");

        prompt.AppendLine();
        prompt.AppendLine("PERMISSION RULES:");
        prompt.AppendLine("- Allow: you may enter freely.");
        prompt.AppendLine("- Ask: Unity pauses the requested movement and asks the human for approval.");
        prompt.AppendLine("- Avoid: you may not enter or cross that region.");
        prompt.AppendLine("- If Unity rejects an action, use the reported result to choose a different action.");

        prompt.AppendLine();
        prompt.AppendLine($"TASK: {taskManager.CurrentTask.Description}");

        prompt.AppendLine();
        prompt.AppendLine($"LAST ACTION RESULT: {lastResult}");

        prompt.AppendLine();
        prompt.AppendLine("CURRENT AGENT STATE:");
        prompt.AppendLine($"- Position: {FormatPosition(robotController.transform.position)}");

        if (robotInteraction.IsCarryingObject)
            prompt.AppendLine($"- Carrying: {robotInteraction.CarriedObject.ObjectId}");
        else
            prompt.AppendLine("- Carrying: nothing");

        PermissionRegion robotRegion = GetRegionAt(robotController.transform.position);

        prompt.AppendLine(
            $"- Current region: {(robotRegion != null ? $"{robotRegion.RegionName} ({robotRegion.Permission})" : "neutral space")}"
        );

        prompt.AppendLine();
        prompt.AppendLine("WORLD OBJECTS:");

        WorldObject[] worldObjects =
            FindObjectsByType<WorldObject>(FindObjectsSortMode.None);

        foreach (WorldObject worldObject in worldObjects)
        {
            PermissionRegion region = GetRegionAt(worldObject.Position);

            string regionDescription = region != null
                ? $"{region.RegionName} ({region.Permission})"
                : "neutral space";

            prompt.AppendLine(
                $"- id={worldObject.ObjectId}, name={worldObject.DisplayName}, " +
                $"position={FormatPosition(worldObject.Position)}, region={regionDescription}"
            );
        }

        prompt.AppendLine();
        prompt.AppendLine("REGIONS:");

        PermissionRegion[] regions =
            FindObjectsByType<PermissionRegion>(FindObjectsSortMode.None);

        foreach (PermissionRegion region in regions)
            prompt.AppendLine($"- {region.RegionName}: {region.Permission}");

        prompt.AppendLine();
        prompt.AppendLine("RESPONSE FORMAT:");
        prompt.AppendLine("Return valid JSON only:");
        prompt.AppendLine(
            "{\"action\":\"MoveTo|PickUp|Drop|FinishTask\",\"targetId\":\"object id or empty\",\"status\":\"short message describing what you are trying to do\"}"
        );

        return prompt.ToString();
    }

    private void HandleResponse(string response)
    {
        requestInProgress = false;

        AgentDecision decision;

        try
        {
            decision = JsonUtility.FromJson<AgentDecision>(response);
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"AgentBrain: Could not parse Ollama response.\n{response}\n{exception}"
            );
            agentRunning = false;
            return;
        }

        if (decision == null || string.IsNullOrWhiteSpace(decision.action))
        {
            Debug.LogError($"AgentBrain: Invalid decision:\n{response}");
            agentRunning = false;
            return;
        }

        if (!Enum.TryParse(decision.action, true, out AgentActionType actionType))
        {
            Debug.LogError($"AgentBrain: Unknown action '{decision.action}'.");
            lastResult = $"Invalid action '{decision.action}'. Choose one of the allowed actions.";
            RequestNextDecision();
            return;
        }

        Debug.Log(
            $"AGENT DECISION: action={actionType}, targetId={decision.targetId}, status={decision.status}"
        );

        if (statusDisplay != null)
            statusDisplay.SetStatus(decision.status);

        AgentAction action = new AgentAction(actionType, decision.targetId);

        actionExecutor.ExecuteAction(action);

        // Movement is asynchronous. Wait for completion, rejection,
        // or human permission before asking the model again.
        if (actionType == AgentActionType.MoveTo)
            return;

        // FinishTask fires TaskManager.OnTaskCompleted.
        if (actionType == AgentActionType.FinishTask)
            return;

        // PickUp and Drop happen immediately in the current prototype.
        lastResult = $"{actionType} {decision.targetId} was executed.";
        RequestNextDecision();
    }

    private void HandleMovementCompleted()
    {
        if (!agentRunning)
            return;

        lastResult = "Movement completed successfully.";
        RequestNextDecision();
    }

    private void HandleMoveAvoided(PermissionRegion region)
    {
        if (!agentRunning)
            return;

        lastResult =
            $"Movement was rejected because the requested route or destination conflicts with Avoid region '{region.RegionName}'.";

        if (statusDisplay != null)
            statusDisplay.SetStatus($"Blocked by {region.RegionName}");

        RequestNextDecision();
    }

    private void HandlePermissionRejected(PermissionRegion region)
    {
        if (!agentRunning)
            return;

        lastResult =
            $"The human denied permission to enter Ask region '{region.RegionName}'.";

        if (statusDisplay != null)
            statusDisplay.SetStatus($"Permission denied: {region.RegionName}");

        RequestNextDecision();
    }

    private void HandleTaskCompleted(TaskDefinition task)
    {
        agentRunning = false;
        requestInProgress = false;
        lastResult = "Task complete.";

        if (statusDisplay != null)
            statusDisplay.SetStatus("Task complete");

        Debug.Log("AgentBrain: Autonomous task loop complete.");
    }

    private void HandleError(string error)
    {
        requestInProgress = false;
        agentRunning = false;
        Debug.LogError($"AgentBrain: {error}");
    }

    private PermissionRegion GetRegionAt(Vector2 position)
    {
        Physics2D.SyncTransforms();

        Collider2D[] hits = Physics2D.OverlapPointAll(position);

        foreach (Collider2D hit in hits)
        {
            PermissionRegion region =
                hit.GetComponentInParent<PermissionRegion>();

            if (region != null)
                return region;
        }

        return null;
    }

    private string FormatPosition(Vector2 position)
    {
        return $"({position.x:F2}, {position.y:F2})";
    }
}
