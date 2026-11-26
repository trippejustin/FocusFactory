using TMPro;
using UnityEngine;

public class TaskListUI : MonoBehaviour
{
    [SerializeField] private TaskManager taskManager;
    [SerializeField] private TMP_Text taskText;

    private void Start()
    {
        taskManager.OnStepsChanged += Refresh;
        Refresh();
    }

    private void OnDestroy()
    {
        if (taskManager != null)
            taskManager.OnStepsChanged -= Refresh;
    }

    private void Refresh()
    {
        if (taskManager == null || taskText == null)
            return;

        taskText.text = "";

        foreach (TaskManager.TaskStep step in taskManager.TaskSteps)
        {
            string box = step.completed ? "[X]" : "[  ]";

            taskText.text +=
                $"{box} {step.description}\n";
        }
    }
}