using System.Collections.Generic;
using UnityEngine;

public class WorldState : MonoBehaviour
{
    private readonly Dictionary<string, WorldObject> objectsById = new();

    private void Awake()
    {
        WorldObject[] worldObjects = FindObjectsByType<WorldObject>(
            FindObjectsSortMode.None
        );

        foreach (WorldObject worldObject in worldObjects)
        {
            objectsById[worldObject.ObjectId] = worldObject;
        }
    }

    public WorldObject GetObject(string objectId)
    {
        if (objectsById.TryGetValue(objectId, out WorldObject worldObject))
            return worldObject;

        Debug.LogWarning($"World object not found: {objectId}");
        return null;
    }
}