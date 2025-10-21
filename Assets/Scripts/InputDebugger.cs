using UnityEngine;
using UnityEngine.InputSystem;

public class InputDebugger : MonoBehaviour
{
    private InputSystem_Actions inputActions;
    private Gamepad switchController;

    void Awake()
    {
        inputActions = new InputSystem_Actions();

        // Find the Switch Pro Controller
        foreach (var device in InputSystem.devices)
        {
            Debug.Log($"Device Found: {device.name} | Layout: {device.layout}");

            if (device.layout == "SwitchProControllerHID")
            {
                switchController = device as Gamepad;
                Debug.Log($"<color=green>★ Switch Pro Controller Connected! ★</color>");
            }
        }

        if (switchController == null)
        {
            Debug.LogWarning("Switch Pro Controller not detected as Gamepad!");
            // Try to get any gamepad
            switchController = Gamepad.current;
            if (switchController != null)
            {
                Debug.Log($"Using generic gamepad: {switchController.name}");
            }
        }
    }

    void OnEnable()
    {
        inputActions.Enable();

        // Subscribe to Input Action events
        inputActions.Player.Move.performed += ctx =>
            Debug.Log($"<color=cyan>▶ INPUT ACTION - Move: {ctx.ReadValue<Vector2>()}</color>");

        inputActions.Player.Sprint.performed += ctx =>
            Debug.Log("<color=yellow>▶ INPUT ACTION - Sprint: PRESSED</color>");
        inputActions.Player.Sprint.canceled += ctx =>
            Debug.Log("<color=yellow>▶ INPUT ACTION - Sprint: RELEASED</color>");

        inputActions.Player.Jump.performed += ctx =>
            Debug.Log("<color=green>▶ INPUT ACTION - Jump: PRESSED</color>");
        inputActions.Player.Jump.canceled += ctx =>
            Debug.Log("<color=green>▶ INPUT ACTION - Jump: RELEASED</color>");

        inputActions.Player.Wheelie.performed += ctx =>
            Debug.Log("<color=magenta>▶ INPUT ACTION - Wheelie: PRESSED</color>");
        inputActions.Player.Wheelie.canceled += ctx =>
            Debug.Log("<color=magenta>▶ INPUT ACTION - Wheelie: RELEASED</color>");
    }

    void OnDisable()
    {
        inputActions.Disable();
    }

    void Update()
    {
        if (switchController != null)
        {
            // Read raw controller state
            Vector2 leftStick = switchController.leftStick.ReadValue();

            // Only log when there's input (avoid spam)
            if (leftStick.magnitude > 0.1f)
            {
                Debug.Log($"<color=orange>● RAW Left Stick: X={leftStick.x:F3}, Y={leftStick.y:F3}</color>");
            }

            // Check buttons
            if (switchController.buttonSouth.wasPressedThisFrame)
                Debug.Log("<color=lime>● RAW Button South (B): PRESSED</color>");

            if (switchController.buttonEast.wasPressedThisFrame)
                Debug.Log("<color=lime>● RAW Button East (A): PRESSED</color>");

            if (switchController.buttonWest.wasPressedThisFrame)
                Debug.Log("<color=lime>● RAW Button West (Y): PRESSED</color>");

            if (switchController.buttonNorth.wasPressedThisFrame)
                Debug.Log("<color=lime>● RAW Button North (X): PRESSED</color>");

            if (switchController.leftStickButton.wasPressedThisFrame)
                Debug.Log("<color=lime>● RAW Left Stick Press (L3): PRESSED</color>");

            if (switchController.leftShoulder.wasPressedThisFrame)
                Debug.Log("<color=lime>● RAW Left Shoulder (L): PRESSED</color>");

            if (switchController.rightShoulder.wasPressedThisFrame)
                Debug.Log("<color=lime>● RAW Right Shoulder (R): PRESSED</color>");
        }
    }
}