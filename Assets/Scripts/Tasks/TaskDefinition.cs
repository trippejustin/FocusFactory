using UnityEngine;

[CreateAssetMenu(
    fileName = "NewTask",
    menuName = "Spatial Permissions/Task Definition"
)]
public class TaskDefinition : ScriptableObject
{
    [SerializeField] private string taskId;
    [SerializeField] private string description;
    [SerializeField] private string objectToMoveId;
    [SerializeField] private string destinationObjectId;

    public string TaskId => taskId;
    public string Description => description;
    public string ObjectToMoveId => objectToMoveId;
    public string DestinationObjectId => destinationObjectId;
}