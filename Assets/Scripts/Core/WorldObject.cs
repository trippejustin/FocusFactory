using UnityEngine;

public class WorldObject : MonoBehaviour
{
    [SerializeField] private string objectId;
    [SerializeField] private string displayName;

    [Header("Interaction")]
    [SerializeField] private Transform interactionPoint;

    public string ObjectId => objectId;
    public string DisplayName => displayName;

    public Vector2 Position =>
        transform.position;

    public Vector2 InteractionPosition
    {
        get
        {
            if (interactionPoint != null)
            {
                return interactionPoint.position;
            }

            return transform.position;
        }
    }
}