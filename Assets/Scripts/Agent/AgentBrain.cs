using System;
using System.Text;
using System.Collections.Generic;
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

    [Header("Interaction Awareness")]
    [SerializeField] private float interactionAwarenessRange = 0.8f;

    [Header("Failsafes")]
    [SerializeField] private int maxInvalidResponseRetries = 2;
    [SerializeField] private int maxOllamaErrorRetries = 2;
    [SerializeField] private int maxConsecutiveActionFailures = 5;
    [SerializeField] private int maxRepeatedFailedDecision = 3;

    public bool TaskHasStarted { get; private set; }
    public bool IsAgentRunning => agentRunning;

    private bool requestInProgress;
    private bool agentRunning;
    private bool returningHome;
    private bool seekingNeutralSafety;

    private string safetyTargetId = "";

    private readonly HashSet<string> failedSafetyTargets =
        new();

    private int runGeneration;

    private Vector2 startingPosition;
    private PermissionRegion pendingPermissionRegion;

    private readonly HashSet<string> deniedAskRegions = new();

    private int invalidResponseRetries;
    private int ollamaErrorRetries;
    private int consecutiveActionFailures;
    private int repeatedFailedDecisionCount;
    private string lastFailedDecisionKey = "";

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
        startingPosition =
            robotController.transform.position;

        WorldObject[] worldObjects =
            FindObjectsByType<WorldObject>(
                FindObjectsSortMode.None
            );

        foreach (WorldObject worldObject in worldObjects)
        {
            if (worldObject.ObjectId == "home")
            {
                worldObject.transform.position =
                    startingPosition;

                break;
            }
        }

        Physics2D.SyncTransforms();

        robotController.OnMovementStarted +=
            HandleMovementStarted;

        robotController.OnMovementCompleted +=
            HandleMovementCompleted;

        permissionManager.OnPermissionRequested +=
            HandlePermissionRequested;

        permissionManager.OnMoveAvoided +=
            HandleMoveAvoided;

        permissionManager.OnRouteBlocked +=
            HandleRouteBlocked;

        permissionManager.OnPermissionRejected +=
            HandlePermissionRejected;

        taskManager.OnTaskCompleted +=
            HandleTaskCompleted;

        PermissionRegion.OnPermissionChanged +=
            HandleRegionPermissionChanged;
    }

    private void OnDestroy()
    {
        if (robotController != null)
        {
            robotController.OnMovementStarted -=
                HandleMovementStarted;

            robotController.OnMovementCompleted -=
                HandleMovementCompleted;
        }

        if (permissionManager != null)
        {
            permissionManager.OnPermissionRequested -=
                HandlePermissionRequested;

            permissionManager.OnMoveAvoided -=
                HandleMoveAvoided;

            permissionManager.OnRouteBlocked -=
                HandleRouteBlocked;

            permissionManager.OnPermissionRejected -=
                HandlePermissionRejected;
        }

        if (taskManager != null)
        {
            taskManager.OnTaskCompleted -=
                HandleTaskCompleted;
        }

        PermissionRegion.OnPermissionChanged -=
            HandleRegionPermissionChanged;
    }

    public void StartAgent()
    {
        if (agentRunning)
            return;

        if (taskManager.CurrentTask == null)
        {
            Debug.LogWarning(
                "AgentBrain: No current task is assigned."
            );


            return;
        }

        if (!returningHome &&
            !taskManager.AreAllStepsComplete() &&
            !ValidateTaskPrerequisites(
                out string prerequisiteFailure))
        {
            StopWithFailure(
                "TASK UNDOABLE",
                prerequisiteFailure
            );

            return;
        }

        ResetAllFailureTracking();

        runGeneration++;

        agentRunning = true;
        requestInProgress = false;
        pendingPermissionRegion = null;
        seekingNeutralSafety = false;
        safetyTargetId = "";
        failedSafetyTargets.Clear();

        permissionManager.ResetRuntimePermissions();
        deniedAskRegions.Clear();

        if (!TaskHasStarted)
        {
            TaskHasStarted = true;

            lastResult =
                "Task started. Decide the first action.";

            taskManager.StartTask();
        }
        else
        {
            lastResult =
                "Human resumed the agent. Re-evaluate the current world state, permissions, position, and task before choosing the next action.";
        }

        if (statusDisplay != null)
        {
            statusDisplay.SetStatus(
                "Planning"
            );
        }

        RequestNextDecision();
    }

    public void StopAgent()
    {
        if (!agentRunning &&
            !requestInProgress)
        {
            return;
        }

        runGeneration++;

        agentRunning = false;
        requestInProgress = false;
        pendingPermissionRegion = null;
        seekingNeutralSafety = false;
        safetyTargetId = "";
        failedSafetyTargets.Clear();

        robotController.Stop();

        ollamaClient.CancelCurrentRequest();

        permissionManager.ResetRuntimePermissions();
        deniedAskRegions.Clear();
        ResetAllFailureTracking();

        lastResult =
            "Human stopped the agent. Wait until the human resumes execution.";

        if (statusDisplay != null)
        {
            statusDisplay.SetStatus(
                "STOPPED — waiting for human"
            );
        }

        Debug.Log(
            "AgentBrain: Agent stopped by human."
        );
    }

    public void RequestNextDecision()
    {
        if (!agentRunning ||
            requestInProgress)
        {
            return;
        }

        requestInProgress = true;

        int requestGeneration =
            runGeneration;

        Debug.Log(
            "AgentBrain: Requesting next action from Ollama..."
        );

        ollamaClient.SendPrompt(
            BuildPrompt(),

            response =>
            {
                if (!agentRunning ||
                    requestGeneration != runGeneration)
                {
                    return;
                }

                HandleResponse(response);
            },

            error =>
            {
                if (requestGeneration != runGeneration)
                {
                    return;
                }

                HandleError(error);
            }
        );
    }

    private string BuildPrompt()
    {
        StringBuilder prompt =
            new StringBuilder();

        prompt.AppendLine(
            "You are an autonomous robot agent operating in a 2D workspace."
        );

        prompt.AppendLine(
            returningHome
                ? "Choose exactly ONE MoveTo or Decontaminate action that advances your clean return home."
                : "Choose exactly ONE next action that advances the assigned task."
        );

        prompt.AppendLine(
            "You must make the decisions. Unity only validates and executes your requested actions."
        );

        prompt.AppendLine();
        prompt.AppendLine("AVAILABLE ACTIONS:");

        prompt.AppendLine(
            "- MoveTo: move toward a world object. targetId must be that object's ID."
        );

        if (!returningHome)
        {
            prompt.AppendLine(
                "- PickUp: pick up a world object. targetId must be that object's ID."
            );

            prompt.AppendLine(
                "- Drop: drop the currently carried object at another world object. targetId must be the destination object's ID."
            );

            prompt.AppendLine(
                "- Decontaminate: clean the robot and any carried battery while at the decontamination point; empty-handed cleaning is allowed. targetId must be empty."
            );

            prompt.AppendLine(
                "- FinishTask: use only when every required task step is complete. targetId must be empty."
            );

            prompt.AppendLine(
                "- Follow the task steps in order. Do not FinishTask while any required step is incomplete."
            );

            prompt.AppendLine(
                "- Do not repeat completed task steps unless the current world state requires recovery, such as a contaminated battery that needs cleaning again."
            );

            prompt.AppendLine(
                "- Do not repeat a successful action unless it failed afterward or a changed world state requires repeating it."
            );
        }

        if (returningHome)
        {
            prompt.AppendLine(
                "- Decontaminate: clean the robot at the decontamination point. targetId must be empty."
            );
        }

        prompt.AppendLine();
        prompt.AppendLine("PERMISSION RULES:");

        prompt.AppendLine(
            "- Allow: you may enter freely."
        );

        prompt.AppendLine(
            "- Ask: the robot stops at the region boundary and requests human approval before entering."
        );

        prompt.AppendLine(
            "- Avoid: you may not enter or cross that region."
        );

        prompt.AppendLine(
            "- Prefer routes through neutral space or Allow regions whenever a reasonable route exists."
        );

        prompt.AppendLine(
            returningHome
                ? "- During return home, the direct goal priority below takes precedence; Unity handles routing and permission requests."
                : "- Before choosing a movement that would enter or cross an Ask region, first look for a route around it using the available navigation waypoints."
        );

        prompt.AppendLine(
            returningHome
                ? "- During return home, do not choose a waypoint just to avoid a possible Ask request. Unity enforces permissions along the route."
                : "- Only request access to an Ask region when there is no reasonable neutral/Allow route available, or when alternate routes have already failed."
        );

        prompt.AppendLine(
            "- If the human has denied an Ask region during this run, treat that region as Avoid for the rest of the run. Do not request it again."
        );

        prompt.AppendLine(
            "- Permissions apply to the entire planned route, not only to the target's location."
        );

        prompt.AppendLine(
            "- If Unity rejects an action, use the reported result to choose a different action."
        );

        prompt.AppendLine(
            "- Do not repeat an action that just failed unless the world state has changed. Try a different target or route."
        );

        if (!returningHome)
        {
            prompt.AppendLine(
                "- If withinInteractionRange=true for the object you need, DO NOT use MoveTo on that object again. Perform the required interaction instead."
            );
        }

        prompt.AppendLine();

        if (returningHome)
        {
            prompt.AppendLine(
                "CURRENT GOAL: RETURN HOME"
            );

            prompt.AppendLine(
                $"The delivery task is complete. Return to your starting position at {FormatPosition(startingPosition)}."
            );

            prompt.AppendLine(
                "If clean, your first choice MUST be MoveTo with targetId=home. Unity computes the full route; you do not need to select intermediate waypoints to reach home."
            );

            prompt.AppendLine(
                "If contaminated, your first choice MUST be MoveTo with targetId=decontamination, or Decontaminate with an empty targetId if already within interaction range of that station. Once clean, choose MoveTo home immediately."
            );

            prompt.AppendLine(
                "Unity handles Ask requests along the selected route. Do not visit unrelated objects or waypoints merely to avoid a possible Ask request."
            );

            prompt.AppendLine(
                "Only choose an intermediate waypoint if Unity has actually rejected movement to your current required destination (home or decontamination). Choose a waypoint that advances toward that destination, then retry the required destination after reaching it."
            );

            prompt.AppendLine(
                "During the return-home phase, use only MoveTo or Decontaminate. The robot must be clean to return home. If contaminated, travel to decontamination and clean yourself before returning home. Repeat cleaning if contaminated again."
            );
        }
        else
        {
            prompt.AppendLine(
                $"TASK: {taskManager.CurrentTask.Description}"
            );

            prompt.AppendLine();
            if (taskManager.UsesOrderedDeliveries)
            {
                TaskDefinition.Delivery next = taskManager.NextDelivery;
                if (next == null)
                {
                    prompt.AppendLine("All ordered deliveries are complete. FinishTask when the checklist is complete.");
                }
                else if (robotInteraction.IsCarryingObject &&
                         robotInteraction.CarriedObject.ObjectId == next.batteryId)
                {
                    HazardRegion.CheckExposure(robotInteraction);
                    BatteryContamination carriedBattery =
                        robotInteraction.CarriedObject.GetComponent<BatteryContamination>();

                    prompt.AppendLine($"Current required delivery: {next.batteryId} is ALREADY in your possession. Do not pick it up again. Keep carrying it until installation in {next.machineId}.");

                    if (robotInteraction.IsContaminated ||
                        (carriedBattery != null && carriedBattery.IsContaminated))
                    {
                        prompt.AppendLine("IMMEDIATE GOAL: Clean yourself and the carried battery before attempting installation. Choose MoveTo with targetId=decontamination, or Decontaminate with an empty targetId if the decontamination station is within interaction range. This recovery action is part of the current delivery and does not violate the delivery order. If the route is rejected, find a permitted alternative route to the station. Do not attempt Drop or FinishTask while the carried battery is contaminated.");
                    }
                    else
                    {
                        prompt.AppendLine($"Current battery is clean. MoveTo {next.machineId}, then Drop at {next.machineId} when within interaction range. If contamination occurs, clean again before installation.");
                    }
                }
                else
                {
                    prompt.AppendLine($"Current required delivery: pick up {next.batteryId} and install it in {next.machineId} before starting any later delivery. Decontamination is allowed whenever necessary during this delivery.");
                }
            }

            prompt.AppendLine("REQUIRED TASK STEPS:");

            foreach (TaskManager.TaskStep step in taskManager.TaskSteps)
            {
                prompt.AppendLine(
                    $"- [{(step.completed ? "COMPLETE" : "INCOMPLETE")}] " +
                    $"{step.description}"
                );
            }
        }

        prompt.AppendLine();

        prompt.AppendLine(
            $"LAST ACTION RESULT: {lastResult}"
        );

        prompt.AppendLine();

        if (deniedAskRegions.Count > 0)
        {
            prompt.AppendLine(
                "DENIED ASK REGIONS — TREAT AS AVOID:"
            );

            foreach (string deniedRegion in deniedAskRegions)
            {
                prompt.AppendLine(
                    $"- {deniedRegion}"
                );
            }

            prompt.AppendLine();
        }

        prompt.AppendLine(
            "CURRENT AGENT STATE:"
        );

        prompt.AppendLine(
            $"- Position: {FormatPosition(robotController.transform.position)}"
        );

        HazardRegion.CheckExposure(robotInteraction);
        prompt.AppendLine($"- Robot contamination: {(robotInteraction.IsContaminated ? "CONTAMINATED" : "CLEAN")}");
        prompt.AppendLine("- Contamination transfers between robot and battery on pickup. A contaminated robot contaminates any battery it carries. Decontaminate cleans both.");
        prompt.AppendLine("- Home cannot be selected as a destination while the robot is contaminated.");

        if (robotInteraction.IsCarryingObject)
        {

            prompt.AppendLine(
                $"- Carrying: {robotInteraction.CarriedObject.ObjectId}"
            );

            BatteryContamination battery =
                robotInteraction.CarriedObject.GetComponent<BatteryContamination>();

            if (battery != null)
            {
                prompt.AppendLine(
                    $"- Carried battery status: {(battery.IsContaminated ? "CONTAMINATED" : "CLEAN")}"
                );

                if (battery.IsContaminated && !returningHome)
                {
                    prompt.AppendLine(
                        "- A contaminated battery cannot be installed. Move to the decontamination point and use Decontaminate, even if you cleaned it earlier."
                    );
                }
            }
        }
        else
        {
            prompt.AppendLine(
                "- Carrying: nothing"
            );
        }

        PermissionRegion robotRegion =
            GetRegionAt(
                robotController.transform.position
            );

        if (robotRegion != null)
        {
            string effectivePermission =
                GetEffectivePermissionDescription(
                    robotRegion
                );

            prompt.AppendLine(
                $"- Current region: {robotRegion.RegionName} ({effectivePermission})"
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
                GetRegionAt(
                    worldObject.Position
                );

            string regionDescription =
                region != null
                    ? $"{region.RegionName} ({GetEffectivePermissionDescription(region)})"
                    : "neutral space";

            float distance =
                Vector2.Distance(
                    robotController.transform.position,
                    worldObject.InteractionPosition
                );

            bool withinInteractionRange =
                distance <= interactionAwarenessRange;


            prompt.AppendLine(
                $"- id={worldObject.ObjectId}, " +
                $"name={worldObject.DisplayName}, " +
                $"position={FormatPosition(worldObject.Position)}, " +
                $"distance={distance:F2}, " +
                $"withinInteractionRange={withinInteractionRange}, " +
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
                $"- {region.RegionName}: {GetEffectivePermissionDescription(region)}"
            );
        }

        prompt.AppendLine();
        prompt.AppendLine("RESPONSE FORMAT:");

        prompt.AppendLine(
            "Status messages must contain no more than 10 words. Be concise and informative."
        );

        prompt.AppendLine(
            "Return valid JSON only:"
        );

        string allowedActions = returningHome
            ? "MoveTo|Decontaminate"
            : "MoveTo|PickUp|Drop|Decontaminate|FinishTask";

        prompt.AppendLine(
            "{\"action\":\"" + allowedActions + "\"," +
            (returningHome
                ? "\"targetId\":\"navigation object id or empty for Decontaminate\","
                : "\"targetId\":\"object id or empty\",") +
            "\"status\":\"short message describing what you are trying to do\"}"
        );

        return prompt.ToString();
    }

    private void HandleResponse(
        string response)
    {
        requestInProgress = false;

        if (!agentRunning)
            return;

        if (!TryParseDecision(
                response,
                out AgentDecision decision,
                out string parseError))
        {
            HandleInvalidResponse(
                parseError,
                response
            );

            return;
        }

        if (!Enum.TryParse(
                decision.action,
                true,
                out AgentActionType actionType))
        {
            HandleInvalidResponse(
                $"Unknown action '{decision.action}'.",
                response
            );

            return;
        }

        invalidResponseRetries = 0;
        ollamaErrorRetries = 0;

        if (returningHome &&
            actionType != AgentActionType.MoveTo &&
            actionType != AgentActionType.Decontaminate)
        {
            lastResult =
                $"Action '{actionType}' was rejected. " +
                "The task is already complete. " +
                "Return home using MoveTo or Decontaminate.";

            if (statusDisplay != null)
            {
                statusDisplay.SetStatus(
                    "Returning home"
                );
            }

            RegisterActionFailure(
                actionType,
                decision.targetId,
                lastResult
            );

            return;
        }

        Debug.Log(
            $"AGENT DECISION: " +
            $"action={actionType}, " +
            $"targetId={decision.targetId}, " +
            $"status={decision.status}"
        );

        StudyDataLogger.RecordEvent(
            "ActionSelected",
            actionType.ToString(),
            decision.targetId ?? ""
        );

        if (statusDisplay != null)
        {
            statusDisplay.SetStatus(
                string.IsNullOrWhiteSpace(
                    decision.status)
                    ? "Executing"
                    : decision.status
            );
        }

        HazardRegion.CheckExposure(robotInteraction);

        if (actionType == AgentActionType.MoveTo &&
            string.Equals(decision.targetId, "home", StringComparison.OrdinalIgnoreCase) &&
            robotInteraction.IsContaminated)
        {
            lastResult = "Home access denied: robot contaminated. Decontaminate before returning home.";
            RegisterActionFailure(actionType, decision.targetId, lastResult);
            return;
        }

        AgentAction action =
            new AgentAction(
                actionType,
                decision.targetId
            );

        bool succeeded =
            actionExecutor.ExecuteAction(
                action,
                out string actionResult
            );

        lastResult =
            actionResult;

        if (!succeeded)
        {
            RegisterActionFailure(
                actionType,
                decision.targetId,
                actionResult
            );

            return;
        }

        // Movement is successful only when its destination is reached.
        if (actionType != AgentActionType.MoveTo)
            ResetActionFailureTracking();

        if (actionType ==
            AgentActionType.MoveTo)
        {
            // Movement may be running now or waiting on permission.
            return;
        }

        if (actionType ==
            AgentActionType.FinishTask)
        {
            return;
        }

        RequestNextDecision();
    }

    private void HandleRegionPermissionChanged(
        PermissionRegion region,
        PermissionType previousPermission,
        PermissionType newPermission)
    {
        if (!agentRunning)
            return;

        string regionName =
            region != null &&
            !string.IsNullOrWhiteSpace(
                region.RegionName)
                ? region.RegionName
                : "Unknown region";

        // Any plan or movement based on the old permission snapshot
        // is now stale.
        runGeneration++;
        requestInProgress = false;
        pendingPermissionRegion = null;

        if (ollamaClient != null)
        {
            ollamaClient.CancelCurrentRequest();
        }

        if (robotController != null)
        {
            robotController.Stop();
        }

        permissionManager.CancelCurrentMovement();

        ResetActionFailureTracking();

        lastResult =
            $"Human changed permission for '{regionName}' " +
            $"from {previousPermission} to {newPermission}. " +
            "The previous plan was cancelled. Re-evaluate the current " +
            "world state and permissions before choosing the next action.";

        if (statusDisplay != null)
        {
            statusDisplay.SetStatus(
                $"Permission changed: {regionName} → {newPermission}"
            );
        }

        Debug.Log(
            $"AgentBrain: Permission changed mid-run: " +
            $"{regionName} {previousPermission} → {newPermission}."
        );

        if (seekingNeutralSafety)
        {
            safetyTargetId = "";
            failedSafetyTargets.Clear();

            TryMoveToNeutralSafety();
            return;
        }

        RequestNextDecision();
    }

    private void HandlePermissionRequested(
        PermissionRegion region)
    {
        if (!agentRunning)
            return;

        pendingPermissionRegion =
            region;

        if (statusDisplay != null)
        {
            statusDisplay.SetStatus(
                $"Requesting permission: {region.RegionName}"
            );
        }
    }

    private void HandleMovementStarted()
    {
        if (!agentRunning)
            return;

        if (pendingPermissionRegion == null)
            return;

        if (statusDisplay != null)
        {
            statusDisplay.SetStatus(
                $"Permission granted: {pendingPermissionRegion.RegionName}"
            );
        }

        pendingPermissionRegion =
            null;
    }

    private void HandleMovementCompleted()
    {
        if (!agentRunning)
            return;

        if (seekingNeutralSafety)
        {
            pendingPermissionRegion = null;

            if (GetRegionAt(
                    robotController.transform.position) == null)
            {
                EnterWaitingForInstructionsState();

                return;
            }

            if (!string.IsNullOrWhiteSpace(
                    safetyTargetId))
            {
                failedSafetyTargets.Add(
                    safetyTargetId
                );
            }

            safetyTargetId = "";

            TryMoveToNeutralSafety();

            return;
        }

        ResetActionFailureTracking();

        if (returningHome)
        {
            float distanceFromHome =
                Vector2.Distance(
                    robotController.transform.position,
                    startingPosition
                );

            if (distanceFromHome <=
                homeArrivalDistance)
            {
                HazardRegion.CheckExposure(robotInteraction);

                if (robotInteraction.IsContaminated)
                {
                    lastResult = "Return home rejected: robot contaminated. Leave home and decontaminate before returning.";
                    if (statusDisplay != null)
                        statusDisplay.SetStatus("Decontamination required before return");
                    RequestNextDecision();
                    return;
                }

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

                taskManager.CompleteTrial();

                return;
            }

            lastResult =
                "Movement completed successfully. If clean, choose MoveTo home now. " +
                "If contaminated, move to decontamination or clean yourself if already in range.";

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
        pendingPermissionRegion =
            null;

        if (!agentRunning)
            return;

        if (seekingNeutralSafety)
        {
            return;
        }

        if (returningHome)
        {
            lastResult =
                $"Return-home movement was rejected because it conflicts with " +
                $"blocked region '{region.RegionName}'.";
        }
        else
        {
            lastResult =
                $"Movement was rejected because the route or destination " +
                $"conflicts with blocked region '{region.RegionName}'.";
        }

        if (statusDisplay != null)
        {
            statusDisplay.SetStatus(
                $"Blocked by {region.RegionName}"
            );
        }

        // Do not request another Ollama decision here.
        // RequestMove returns false to AgentActionExecutor,
        // and HandleResponse handles the retry/failsafe.
    }

    private void HandleRouteBlocked(PermissionRegion region)
    {
        if (!agentRunning)
            return;

        if (seekingNeutralSafety)
        {
            if (!string.IsNullOrWhiteSpace(safetyTargetId))
                failedSafetyTargets.Add(safetyTargetId);

            safetyTargetId = "";
            TryMoveToNeutralSafety();
            return;
        }

        RegisterActionFailure(
            AgentActionType.MoveTo,
            region.RegionName,
            $"Route blocked at the boundary of '{region.RegionName}'. Choose another route."
        );
    }

    private void HandlePermissionRejected(
        PermissionRegion region)
    {
        pendingPermissionRegion =
            null;

        if (!agentRunning)
            return;

        if (seekingNeutralSafety)
        {
            if (!string.IsNullOrWhiteSpace(
                    safetyTargetId))
            {
                failedSafetyTargets.Add(
                    safetyTargetId
                );
            }

            safetyTargetId = "";
            pendingPermissionRegion = null;

            TryMoveToNeutralSafety();

            return;
        }

        ResetActionFailureTracking();

        if (region != null &&
            !string.IsNullOrWhiteSpace(
                region.RegionName))
        {
            deniedAskRegions.Add(
                region.RegionName
            );
        }

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
                "while returning home. Reassess the route to your required destination under the updated permissions; use a waypoint only if needed.";

            RequestNextDecision();

            return;
        }

        lastResult =
            $"The human denied permission to enter Ask region " +
            $"'{region.RegionName}'.";

        RequestNextDecision();
    }

    private void HandleTaskCompleted(
        TaskDefinition task)
    {
        if (!agentRunning)
            return;

        returningHome = true;
        requestInProgress = false;
        ResetActionFailureTracking();

        lastResult =
            "Delivery task complete. Return to the starting position only when clean; decontaminate if needed.";

        if (statusDisplay != null)
        {
            statusDisplay.SetStatus(
                "Task complete — planning return"
            );
        }

        RequestNextDecision();
    }

    private void HandleError(
        string error)
    {
        requestInProgress = false;

        if (!agentRunning)
            return;

        ollamaErrorRetries++;

        Debug.LogWarning(
            $"AgentBrain: {error}"
        );

        if (ollamaErrorRetries <=
            maxOllamaErrorRetries)
        {
            lastResult =
                $"Ollama request failed. Retry " +
                $"{ollamaErrorRetries}/" +
                $"{maxOllamaErrorRetries}. " +
                "Re-evaluate the current state.";

            if (statusDisplay != null)
            {
                statusDisplay.SetStatus(
                    "Retrying AI request"
                );
            }

            RequestNextDecision();
            return;
        }

        StopWithFailure(
            "AGENT ERROR",
            "Ollama repeatedly failed to return a usable response."
        );
    }

    private bool TryParseDecision(
        string response,
        out AgentDecision decision,
        out string error)
    {
        decision = null;
        error = "";

        if (string.IsNullOrWhiteSpace(response))
        {
            error = "Response was empty.";
            return false;
        }

        string json =
            response.Trim();

        if (TryParseDecisionJson(
                json,
                out decision))
        {
            return true;
        }

        // Salvage JSON if the model wrapped it in prose or markdown.
        int firstBrace =
            json.IndexOf('{');

        int lastBrace =
            json.LastIndexOf('}');

        if (firstBrace >= 0 &&
            lastBrace > firstBrace)
        {
            string extractedJson =
                json.Substring(
                    firstBrace,
                    lastBrace -
                    firstBrace + 1
                );

            if (TryParseDecisionJson(
                    extractedJson,
                    out decision))
            {
                Debug.LogWarning(
                    "AgentBrain: Recovered JSON object from a wrapped Ollama response."
                );

                return true;
            }
        }

        error =
            "Response could not be parsed into a valid decision.";

        return false;
    }

    private bool TryParseDecisionJson(
        string json,
        out AgentDecision decision)
    {
        decision = null;

        try
        {
            decision =
                JsonUtility.FromJson<AgentDecision>(
                    json
                );
        }
        catch
        {
            return false;
        }

        return decision != null &&
               !string.IsNullOrWhiteSpace(
                   decision.action);
    }

    private void HandleInvalidResponse(
        string reason,
        string rawResponse)
    {
        invalidResponseRetries++;

        Debug.LogWarning(
            $"AgentBrain: Invalid Ollama response. " +
            $"{reason}\n{rawResponse}"
        );

        lastResult =
            $"Your previous response was unusable: {reason} " +
            "Return valid JSON only using one allowed action.";

        if (invalidResponseRetries <=
            maxInvalidResponseRetries)
        {
            if (statusDisplay != null)
            {
                statusDisplay.SetStatus(
                    "Retrying invalid AI response"
                );
            }

            RequestNextDecision();
            return;
        }

        StopWithFailure(
            "AGENT ERROR",
            "Ollama repeatedly returned decisions that could not be resolved."
        );
    }

    private void RegisterActionFailure(
        AgentActionType actionType,
        string targetId,
        string failureReason)
    {
        lastResult = failureReason;
        StudyDataLogger.RecordActionFailure(
            actionType.ToString(),
            targetId,
            failureReason
        );
        Debug.LogWarning($"Agent action failed: {actionType} {targetId}. {failureReason}");
        consecutiveActionFailures++;

        string decisionKey =
            $"{actionType}:{targetId}".ToLowerInvariant();

        if (decisionKey ==
            lastFailedDecisionKey)
        {
            repeatedFailedDecisionCount++;
        }
        else
        {
            lastFailedDecisionKey =
                decisionKey;

            repeatedFailedDecisionCount = 1;
        }

        bool repeatedFailureLimitReached =
            repeatedFailedDecisionCount >=
            Mathf.Max(5, maxRepeatedFailedDecision);

        bool totalFailureLimitReached =
            consecutiveActionFailures >=
            Mathf.Max(5, maxConsecutiveActionFailures);

        if (repeatedFailureLimitReached ||
            totalFailureLimitReached)
        {
            StopWithFailure(
                "TASK UNDOABLE",
                $"No valid plan was found after repeated failed actions. " +
                $"Last failure: {failureReason}"
            );

            return;
        }

        if (statusDisplay != null)
        {
            statusDisplay.SetStatus(
                consecutiveActionFailures >= 3
                    ? "Repeated action failures — replanning"
                    : "Planning"
            );
        }

        RequestNextDecision();
    }

    private void ResetActionFailureTracking()
    {
        consecutiveActionFailures = 0;
        repeatedFailedDecisionCount = 0;
        lastFailedDecisionKey = "";
    }

    private void ResetAllFailureTracking()
    {
        invalidResponseRetries = 0;
        ollamaErrorRetries = 0;
        ResetActionFailureTracking();
    }

    private bool ValidateTaskPrerequisites(
        out string reason)
    {
        reason = "";

        TaskDefinition task =
            taskManager.CurrentTask;

        if (task == null)
        {
            reason =
                "No task is assigned.";

            return false;
        }

        WorldObject[] worldObjects =
            FindObjectsByType<WorldObject>(
                FindObjectsSortMode.None
            );

        if (taskManager.UsesOrderedDeliveries)
        {
            TaskDefinition.Delivery next = taskManager.NextDelivery;
            if (next == null)
                return true;

            bool hasBattery = false;
            bool hasMachine = false;
            foreach (WorldObject item in worldObjects)
            {
                if (item == null) continue;
                if (item.ObjectId == next.batteryId) hasBattery = true;
                if (item.ObjectId == next.machineId) hasMachine = true;
            }
            if (robotInteraction.IsCarryingObject && robotInteraction.CarriedObject.ObjectId == next.batteryId)
                hasBattery = true;
            if (hasBattery && hasMachine) return true;
            reason = "Required object or machine for the current ordered delivery is missing.";
            return false;
        }

        bool hasTaskObject =
            robotInteraction.IsCarryingObject &&
            robotInteraction.CarriedObject != null &&
            robotInteraction.CarriedObject.ObjectId ==
                task.ObjectToMoveId;

        bool hasDestination = false;

        foreach (WorldObject worldObject
                 in worldObjects)
        {
            if (worldObject == null)
                continue;

            if (worldObject.ObjectId ==
                task.ObjectToMoveId)
            {
                hasTaskObject = true;
            }

            if (worldObject.ObjectId ==
                task.DestinationObjectId)
            {
                hasDestination = true;
            }
        }

        if (!hasTaskObject)
        {
            reason =
                $"Required task object " +
                $"'{task.ObjectToMoveId}' does not exist.";

            return false;
        }

        if (!hasDestination)
        {
            reason =
                $"Required destination " +
                $"'{task.DestinationObjectId}' does not exist.";

            return false;
        }

        return true;
    }

    private void StopWithFailure(
        string headline,
        string detail)
    {
        runGeneration++;

        requestInProgress = false;
        pendingPermissionRegion = null;

        if (robotController != null)
        {
            robotController.Stop();
        }

        if (ollamaClient != null)
        {
            ollamaClient.CancelCurrentRequest();
        }

        if (permissionManager != null)
            permissionManager.CancelCurrentMovement();

        lastResult =
            $"{headline}: {detail}";

        Debug.LogWarning(
            $"AgentBrain: {headline} — {detail}"
        );

        if (headline == "TASK UNDOABLE")
        {
            agentRunning = true;
            seekingNeutralSafety = true;
            safetyTargetId = "";

            failedSafetyTargets.Clear();

            if (statusDisplay != null)
            {
                statusDisplay.SetStatus(
                    "Task blocked — seeking neutral zone"
                );
            }

            TryMoveToNeutralSafety();

            return;
        }

        EnterNeedsHelpState($"{headline}: {detail}");
    }

    private void TryMoveToNeutralSafety()
    {
        if (!seekingNeutralSafety)
            return;

        if (robotController == null ||
            actionExecutor == null)
        {
            EnterNeedsHelpState(
                "Required movement systems are unavailable."
            );

            return;
        }

        // If the robot is already in neutral space, simply wait there.
        if (GetRegionAt(
                robotController.transform.position) == null)
        {
            EnterWaitingForInstructionsState();

            return;
        }

        WorldObject[] worldObjects =
            FindObjectsByType<WorldObject>(
                FindObjectsSortMode.None
            );

        List<WorldObject> neutralTargets =
            new List<WorldObject>();

        foreach (WorldObject worldObject in worldObjects)
        {
            if (worldObject == null ||
                string.IsNullOrWhiteSpace(
                    worldObject.ObjectId))
            {
                continue;
            }

            if (failedSafetyTargets.Contains(
                    worldObject.ObjectId))
            {
                continue;
            }

            if (robotInteraction.IsContaminated &&
                string.Equals(worldObject.ObjectId, "home", StringComparison.OrdinalIgnoreCase))
                continue;

            PermissionRegion targetRegion =
                GetRegionAt(
                    worldObject.InteractionPosition
                );

            if (targetRegion == null)
            {
                neutralTargets.Add(
                    worldObject
                );
            }
        }

        neutralTargets.Sort(
            (a, b) =>
            {
                float distanceA =
                    Vector2.Distance(
                        robotController.transform.position,
                        a.InteractionPosition
                    );

                float distanceB =
                    Vector2.Distance(
                        robotController.transform.position,
                        b.InteractionPosition
                    );

                return distanceA.CompareTo(
                    distanceB
                );
            }
        );

        foreach (WorldObject target in neutralTargets)
        {
            AgentAction safetyMove =
                new AgentAction(
                    AgentActionType.MoveTo,
                    target.ObjectId
                );

            bool accepted =
                actionExecutor.ExecuteAction(
                    safetyMove,
                    out string moveResult
                );

            if (accepted)
            {
                safetyTargetId =
                    target.ObjectId;

                lastResult =
                    $"Task cannot continue. Moving to neutral target '{target.ObjectId}'.";

                if (statusDisplay != null)
                {
                    statusDisplay.SetStatus(
                        "Moving to neutral zone"
                    );
                }

                Debug.LogWarning(
                    $"AgentBrain: Task undoable. Moving toward neutral target '{target.ObjectId}'."
                );

                return;
            }

            failedSafetyTargets.Add(
                target.ObjectId
            );

            Debug.LogWarning(
                $"AgentBrain: Could not use neutral target '{target.ObjectId}'. {moveResult}"
            );
        }

        EnterNeedsHelpState(
            "No reachable neutral zone was found."
        );
    }

    private void EnterHumanWaitingState()
    {
        runGeneration++;
        seekingNeutralSafety = false;
        agentRunning = false;
        requestInProgress = false;
        pendingPermissionRegion = null;
        safetyTargetId = "";
        failedSafetyTargets.Clear();

        if (robotController != null)
            robotController.Stop();

        if (ollamaClient != null)
            ollamaClient.CancelCurrentRequest();

        if (permissionManager != null)
        {
            permissionManager.CancelCurrentMovement();
            permissionManager.ResetRuntimePermissions();
        }

        deniedAskRegions.Clear();
        ResetAllFailureTracking();
    }

    private void EnterWaitingForInstructionsState()
    {
        EnterHumanWaitingState();

        lastResult =
            "Task cannot continue. Robot is waiting in neutral space for human instructions.";

        if (statusDisplay != null)
        {
            statusDisplay.SetStatus(
                "WAITING FOR INSTRUCTIONS"
            );
        }

        Debug.LogWarning(
            "AgentBrain: Task undoable. Robot is waiting in neutral space for instructions."
        );
    }

    private void EnterNeedsHelpState(
        string reason)
    {
        StudyDataLogger.RecordNeedsHelp(reason);
        EnterHumanWaitingState();

        lastResult =
            $"Task cannot continue. Human assistance required. {reason}";

        if (statusDisplay != null)
        {
            statusDisplay.SetStatus(
                "NEEDS HELP — awaiting instructions"
            );
        }

        Debug.LogWarning(
            $"AgentBrain: NEEDS HELP — {reason}"
        );
    }

    private PermissionRegion GetRegionAt(
        Vector2 position)
    {
        Physics2D.SyncTransforms();

        Collider2D[] hits =
            Physics2D.OverlapPointAll(
                position
            );

        foreach (Collider2D hit in hits)
        {
            PermissionRegion region =
                hit.GetComponentInParent<
                    PermissionRegion>();

            if (region != null)
                return region;
        }

        return null;
    }

    private string GetEffectivePermissionDescription(
        PermissionRegion region)
    {
        if (region == null)
            return "neutral space";

        if (region.Permission ==
                PermissionType.Ask &&
            deniedAskRegions.Contains(
                region.RegionName))
        {
            return
                "Avoid — Ask permission was denied";
        }

        return region.Permission.ToString();
    }

    private string FormatPosition(
        Vector2 position)
    {
        return
            $"({position.x:F2}, {position.y:F2})";
    }
}