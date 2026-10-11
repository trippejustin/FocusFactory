using UnityEngine;
using UnityEngine.InputSystem;

public class RobotMovementTest : MonoBehaviour
{
    [SerializeField] private RobotController robot;
    [SerializeField] private PermissionManager permissionManager;
    [SerializeField] private Transform target;

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            permissionManager.RequestMove(robot, target.position);
        }

        if (Keyboard.current.yKey.wasPressedThisFrame)
        {
            permissionManager.ApprovePendingRequest();
        }

        if (Keyboard.current.nKey.wasPressedThisFrame)
        {
            permissionManager.RejectPendingRequest();
        }
    }
}