using System;
using System.Collections.Generic;
using UnityEngine;

public class TaskManager : MonoBehaviour
{
    [Serializable]
    public class TaskStep
    {
        public string id;
        public string description;
        public bool completed;
    }

    [SerializeField] private TaskDefinition currentTask;

    [Header("Task Steps")]
    [SerializeField] private List<TaskStep> taskSteps =
        new List<TaskStep>();

    private int nextDeliveryIndex;
    public bool UsesOrderedDeliveries => currentTask != null && currentTask.HasOrderedDeliveries;
    public TaskDefinition.Delivery NextDelivery => UsesOrderedDeliveries && nextDeliveryIndex < currentTask.OrderedDeliveries.Length
        ? currentTask.OrderedDeliveries[nextDeliveryIndex] : null;

    public bool CanPickUpForDelivery(string objectId)
    {
        return !UsesOrderedDeliveries || (NextDelivery != null && NextDelivery.batteryId == objectId);
    }

    public bool CanInstallForDelivery(string objectId, string destinationId)
    {
        return !UsesOrderedDeliveries || (NextDelivery != null && NextDelivery.batteryId == objectId && NextDelivery.machineId == destinationId);
    }

    public void RecordDeliveryPickup(string objectId)
    {
        if (UsesOrderedDeliveries && CanPickUpForDelivery(objectId))
            CompleteStep("pickup_" + objectId);
    }

    public void RecordDeliveryInstallation(string objectId, string destinationId)
    {
        if (!UsesOrderedDeliveries || !CanInstallForDelivery(objectId, destinationId))
            return;

        nextDeliveryIndex++;
        CompleteStep("install_" + objectId);
    }

    private float trialStartTime;
    private bool trialCompleted;

    public TaskDefinition CurrentTask => currentTask;
    public IReadOnlyList<TaskStep> TaskSteps => taskSteps;

    public event Action<TaskDefinition> OnTaskStarted;
    public event Action<TaskDefinition> OnTaskCompleted;

    public event Action<TaskStep> OnStepCompleted;
    public event Action OnStepsChanged;

    private void Start()
    {
        trialStartTime = Time.unscaledTime;
    }

    public void StartTask()
    {
        if (currentTask == null)
        {
            Debug.LogWarning("No task assigned.");
            return;
        }

        Debug.Log($"TASK STARTED: {currentTask.Description}");
        OnTaskStarted?.Invoke(currentTask);
    }

    public void CompleteTask()
    {
        if (currentTask == null || trialCompleted)
            return;

        if (!AreAllStepsComplete())
        {
            Debug.LogWarning(
                "Cannot complete task: required steps are unfinished."
            );

            return;
        }

        trialCompleted = true;

        Debug.Log($"TASK COMPLETE: {currentTask.Description}");

        OnTaskCompleted?.Invoke(currentTask);
    }

    public void CompleteTrial()
    {
        Debug.Log("TRIAL COMPLETE");

        StudySessionManager.Instance?.CompleteCurrentTrial(
            Time.unscaledTime - trialStartTime
        );

        if (SceneTransition.Instance != null)
        {
            SceneTransition.Instance.LoadScene("TrialSelection");
        }
        else
        {
            Debug.LogError("SceneTransition instance is missing.");
        }
    }

    public void CompleteStep(string stepId)
    {
        TaskStep step =
            taskSteps.Find(x => x.id == stepId);

        if (step == null)
        {
            Debug.LogWarning(
                $"Task step not found: {stepId}"
            );

            return;
        }

        if (step.completed)
            return;

        step.completed = true;

        Debug.Log(
            $"TASK STEP COMPLETE: {step.description}"
        );

        OnStepCompleted?.Invoke(step);
        OnStepsChanged?.Invoke();
    }

    public bool IsStepComplete(string stepId)
    {
        TaskStep step =
            taskSteps.Find(x => x.id == stepId);

        return step != null && step.completed;
    }

    public bool AreAllStepsComplete()
    {
        return (!UsesOrderedDeliveries || nextDeliveryIndex == currentTask.OrderedDeliveries.Length) &&
               taskSteps.Count > 0 &&
               taskSteps.TrueForAll(
                   step => step.completed
               );
    }
}