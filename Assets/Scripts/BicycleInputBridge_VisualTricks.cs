using UnityEngine;
using UnityEngine.InputSystem;

namespace SBPScripts
{
    /// <summary>
    /// Input bridge for Visual-Only Tricks
    /// Simple and clean - just passes button presses to the trick system
    /// </summary>
    [RequireComponent(typeof(BicycleController))]
    public class BicycleInputBridge_VisualTricks : MonoBehaviour
    {
        private BicycleController bicycleController;
        private VisualOnlyTricks_v2_Trajectory trickSystem;
        private InputSystem_Actions inputActions;

        // Input values
        private Vector2 moveInput;
        private bool accelerateInput;
        private bool brakeInput;
        private bool sprintInput;
        private bool jumpInput;
        private bool wheelieInput;
        private bool frontflipInput;
        private bool backflipInput;

        // State tracking
        private bool wasJumpPressed;

        [Header("Input Sensitivity")]
        public float steerSensitivityMultiplier = 5f;
        public float stickDeadzone = 0.15f;

        [Header("Trigger Settings")]
        public float triggerPressSpeed = 3f;
        public float triggerReleaseSpeed = 9f;
        public float triggerSnapThreshold = 0.05f;

        [Header("Steering")]
        public bool smoothSteering = false;
        [Range(1f, 20f)]
        public float steeringSmoothSpeed = 10f;

        [Header("Debug")]
        public bool showDebugLogs = false;

        private float smoothedSteer = 0f;
        private float smoothedAcceleration = 0f;

        void Awake()
        {
            bicycleController = GetComponent<BicycleController>();
            trickSystem = GetComponent<VisualOnlyTricks_v2_Trajectory>();  // ← Use v2_Trajectory type!

            if (trickSystem == null)
            {
                Debug.LogWarning("VisualOnlyTricks_v2_Trajectory not found! Tricks won't work.");
            }

            inputActions = new InputSystem_Actions();

            // Subscribe to all inputs
            inputActions.Player.Move.performed += OnMove;
            inputActions.Player.Move.canceled += OnMove;

            inputActions.Player.Accelerate.performed += ctx => OnAccelerate(ctx);
            inputActions.Player.Accelerate.canceled += ctx => OnAccelerate(ctx);

            inputActions.Player.Brake.performed += ctx => OnBrake(ctx);
            inputActions.Player.Brake.canceled += ctx => OnBrake(ctx);

            inputActions.Player.Sprint.performed += ctx => OnSprint(ctx);
            inputActions.Player.Sprint.canceled += ctx => OnSprint(ctx);

            inputActions.Player.Jump.performed += ctx => OnJump(ctx);
            inputActions.Player.Jump.canceled += ctx => OnJump(ctx);

            inputActions.Player.Wheelie.performed += ctx => OnWheelie(ctx);
            inputActions.Player.Wheelie.canceled += ctx => OnWheelie(ctx);

            // TRICK BUTTONS - FIXED! Using FrontFlip and BackFlip actions
            inputActions.Player.FrontFlip.performed += ctx => OnFrontflip(ctx);  // ← CHANGED!
            inputActions.Player.FrontFlip.canceled += ctx => OnFrontflip(ctx);   // ← CHANGED!

            inputActions.Player.BackFlip.performed += ctx => OnBackflip(ctx);    // ← CHANGED!
            inputActions.Player.BackFlip.canceled += ctx => OnBackflip(ctx);     // ← CHANGED!

            Debug.Log("<color=green>BicycleInputBridge_VisualTricks: Initialized</color>");
        }

        void OnEnable()
        {
            inputActions.Enable();
        }

        void OnDisable()
        {
            inputActions.Disable();
        }

        void Update()
        {
            ApplyInputToBicycle();
        }

        void ApplyInputToBicycle()
        {
            // STEERING
            float steerInput = moveInput.x;
            if (Mathf.Abs(steerInput) < stickDeadzone)
            {
                steerInput = 0f;
            }

            float boostedSteerInput = steerInput * steerSensitivityMultiplier;
            boostedSteerInput = Mathf.Clamp(boostedSteerInput, -1f, 1f);

            // ACCELERATION
            float targetAcceleration = 0f;
            if (accelerateInput) targetAcceleration = 1f;
            if (brakeInput) targetAcceleration = -1f;
            if (accelerateInput && brakeInput) targetAcceleration = -1f;

            float accelerationSpeed = Mathf.Abs(targetAcceleration) > Mathf.Abs(smoothedAcceleration)
                ? triggerPressSpeed
                : triggerReleaseSpeed;

            smoothedAcceleration = Mathf.Lerp(smoothedAcceleration, targetAcceleration, Time.deltaTime * accelerationSpeed);

            if (Mathf.Abs(smoothedAcceleration) < triggerSnapThreshold && targetAcceleration == 0f)
            {
                smoothedAcceleration = 0f;
            }

            // Apply to bicycle controller
            if (smoothSteering)
            {
                smoothedSteer = Mathf.Lerp(smoothedSteer, boostedSteerInput, Time.deltaTime * steeringSmoothSpeed);
                bicycleController.customSteerAxis = smoothedSteer;
                bicycleController.customLeanAxis = smoothedSteer;
            }
            else
            {
                bicycleController.customSteerAxis = boostedSteerInput;
                bicycleController.customLeanAxis = boostedSteerInput;
            }

            bicycleController.customAccelerationAxis = smoothedAcceleration;
            bicycleController.rawCustomAccelerationAxis = smoothedAcceleration;

            // BUNNY HOP
            if (jumpInput && !wasJumpPressed)
                bicycleController.bunnyHopInputState = 1;
            else if (!jumpInput && wasJumpPressed)
                bicycleController.bunnyHopInputState = -1;
            else if (jumpInput)
                bicycleController.bunnyHopInputState = 1;
            else
                bicycleController.bunnyHopInputState = 0;

            wasJumpPressed = jumpInput;

            // OTHER BUTTONS
            bicycleController.wheelieInput = wheelieInput;
            bicycleController.sprint = sprintInput;

            // TRICK BUTTONS - Pass to trick system
            if (trickSystem != null)
            {
                trickSystem.frontflipPressed = frontflipInput;
                trickSystem.backflipPressed = backflipInput;
            }

            // Debug
            if (showDebugLogs && (frontflipInput || backflipInput))
            {
                Debug.Log($"<color=magenta>TRICKS - Frontflip: {frontflipInput} | Backflip: {backflipInput}</color>");
            }
        }

        // Input callbacks
        void OnMove(InputAction.CallbackContext context)
        {
            moveInput = context.ReadValue<Vector2>();
        }

        void OnAccelerate(InputAction.CallbackContext context)
        {
            accelerateInput = context.performed;
        }

        void OnBrake(InputAction.CallbackContext context)
        {
            brakeInput = context.performed;
        }

        void OnSprint(InputAction.CallbackContext context)
        {
            sprintInput = context.performed;
        }

        void OnJump(InputAction.CallbackContext context)
        {
            jumpInput = context.performed;
        }

        void OnWheelie(InputAction.CallbackContext context)
        {
            wheelieInput = context.performed;
        }

        void OnFrontflip(InputAction.CallbackContext context)
        {
            if (context.performed)
                frontflipInput = true;
            else if (context.canceled)
                frontflipInput = false;

            if (showDebugLogs)
                Debug.Log($"<color=cyan>X Button (Frontflip): {frontflipInput}</color>");
        }

        void OnBackflip(InputAction.CallbackContext context)
        {
            if (context.performed)
                backflipInput = true;
            else if (context.canceled)
                backflipInput = false;

            if (showDebugLogs)
                Debug.Log($"<color=cyan>B Button (Backflip): {backflipInput}</color>");
        }

        void OnDestroy()
        {
            if (inputActions != null)
            {
                inputActions.Player.Move.performed -= OnMove;
                inputActions.Player.Move.canceled -= OnMove;
                inputActions.Player.Accelerate.performed -= ctx => OnAccelerate(ctx);
                inputActions.Player.Accelerate.canceled -= ctx => OnAccelerate(ctx);
                inputActions.Player.Brake.performed -= ctx => OnBrake(ctx);
                inputActions.Player.Brake.canceled -= ctx => OnBrake(ctx);
                inputActions.Player.Sprint.performed -= ctx => OnSprint(ctx);
                inputActions.Player.Sprint.canceled -= ctx => OnSprint(ctx);
                inputActions.Player.Jump.performed -= ctx => OnJump(ctx);
                inputActions.Player.Jump.canceled -= ctx => OnJump(ctx);
                inputActions.Player.Wheelie.performed -= ctx => OnWheelie(ctx);
                inputActions.Player.Wheelie.canceled -= ctx => OnWheelie(ctx);

                // FIXED! Unsubscribe from FrontFlip and BackFlip
                inputActions.Player.FrontFlip.performed -= ctx => OnFrontflip(ctx);   // ← CHANGED!
                inputActions.Player.FrontFlip.canceled -= ctx => OnFrontflip(ctx);    // ← CHANGED!
                inputActions.Player.BackFlip.performed -= ctx => OnBackflip(ctx);     // ← CHANGED!
                inputActions.Player.BackFlip.canceled -= ctx => OnBackflip(ctx);      // ← CHANGED!
            }
        }
    }
}
