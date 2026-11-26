using System;
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

    [Serializable]
    public class Delivery
    {
        public string batteryId;
        public string machineId;
    }

    [Header("Ordered Deliveries (Optional)")]
    [SerializeField] private Delivery[] orderedDeliveries;

    public Delivery[] OrderedDeliveries => orderedDeliveries;
    public bool HasOrderedDeliveries => orderedDeliveries != null && orderedDeliveries.Length > 0;

    public string TaskId => taskId;
    public string Description => description;
    public string ObjectToMoveId => objectToMoveId;
    public string DestinationObjectId => destinationObjectId;
}