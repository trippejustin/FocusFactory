using UnityEngine;

public class RobotInteraction : MonoBehaviour
{
    [SerializeField] private Transform carryPoint;

    private WorldObject carriedObject;

    public WorldObject CarriedObject => carriedObject;
    public bool IsCarryingObject => carriedObject != null;

    public bool PickUp(WorldObject worldObject)
    {
        if (worldObject == null || carriedObject != null)
            return false;

        carriedObject = worldObject;

        carriedObject.transform.SetParent(carryPoint);
        carriedObject.transform.localPosition = Vector3.zero;

        Debug.Log($"Picked up {carriedObject.DisplayName}.");

        return true;
    }

    public bool Drop(Vector2 dropPosition)
    {
        if (carriedObject == null)
            return false;

        WorldObject droppedObject = carriedObject;

        droppedObject.transform.SetParent(null);
        droppedObject.transform.position = dropPosition;

        carriedObject = null;

        Debug.Log($"Dropped {droppedObject.DisplayName}.");

        return true;
    }
}