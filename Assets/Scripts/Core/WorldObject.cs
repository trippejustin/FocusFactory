using UnityEngine;

public class WorldObject : MonoBehaviour
{
    [SerializeField] private string objectId;
    [SerializeField] private string displayName;

    public string ObjectId => objectId;
    public string DisplayName => displayName;
    public Vector2 Position => transform.position;
}