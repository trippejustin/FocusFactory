using UnityEngine;
using UnityEngine.InputSystem;

public class TestHotkeyListener : MonoBehaviour
{
    [SerializeField]
    private PermissionUIModeController permissionUIModeController;

    private void Awake()
    {
        if (permissionUIModeController == null)
        {
            permissionUIModeController =
                FindFirstObjectByType<PermissionUIModeController>();
        }
    }

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.yKey.wasPressedThisFrame)
        {
            if (permissionUIModeController == null)
            {
                Debug.LogWarning(
                    "TestHotkeyListener: No PermissionUIModeController found."
                );

                return;
            }

            permissionUIModeController.ToggleMode();
        }
    }
}