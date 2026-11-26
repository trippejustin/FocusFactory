using UnityEngine;

public class DecontaminationPoint : MonoBehaviour
{
    [SerializeField] private TaskManager taskManager;

    private void Awake()
    {
        if (taskManager == null)
        {
            taskManager =
                FindFirstObjectByType<TaskManager>();
        }
    }

    public bool Decontaminate(
        RobotInteraction robotInteraction)
    {
        if (robotInteraction == null)
            return false;

        WorldObject carried = robotInteraction.CarriedObject;
        BatteryContamination battery = carried != null
            ? carried.GetComponent<BatteryContamination>() : null;

        bool robotWasContaminated = robotInteraction.IsContaminated;
        bool batteryWasContaminated = battery != null && battery.IsContaminated;

        robotInteraction.Clean();
        if (battery != null)
            battery.Clean();

        if (robotWasContaminated || batteryWasContaminated)
        {
            string cleanedSubject = battery != null
                ? "Robot+" + carried.ObjectId
                : "Robot";

            StudyDataLogger.RecordDecontamination(cleanedSubject);
        }

        if (carried == null || (battery == null && carried.ObjectId != "battery"))
        {
            Debug.Log("Robot decontaminated.");
            return true;
        }

        if (taskManager != null)
        {
            foreach (TaskManager.TaskStep step in taskManager.TaskSteps)
            {
                if (step.id == "decontaminate_battery")
                {
                    taskManager.CompleteStep("decontaminate_battery");
                    break;
                }
            }
        }

        Debug.Log(
            "Battery decontaminated."
        );

        return true;
    }
}