using System;
using UnityEngine;

public class TaskManager : MonoBehaviour
{
    [SerializeField] private TaskDefinition currentTask;

    public TaskDefinition CurrentTask => currentTask;

    public event Action<TaskDefinition> OnTaskStarted;
    public event Action<TaskDefinition> OnTaskCompleted;

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
        if (currentTask == null)
            return;

        Debug.Log($"TASK COMPLETE: {currentTask.Description}");
        OnTaskCompleted?.Invoke(currentTask);
    }
}