using UnityEngine;
using UnityEngine.InputSystem;

namespace SBPScripts
{
    [RequireComponent(typeof(BicycleController))]
    public class BicycleInputBridge : MonoBehaviour
    {
        private BicycleController bicycleController;
        private InputSystem_Actions inputActions;

        // Input values
        private Vector2 moveInput;
        private bool accelerateInput;  // ZR trigger
        private bool brakeInput;       // ZL trigger
        private bool sprintInput;
        private bool jumpInput;
        private bool wheelieInput;

        // For detecting button state changes
        private bool wasJumpPressed;

        [Header("Input Sensitivity")]
        public float steerSensitivityMultiplier = 5f;
        public float stickDeadzone = 0.15f;

        [Header("Trigger Settings")]
        [Tooltip("How fast triggers ramp up/down (higher = more responsive)")]
        public float triggerSensitivity = 3f;

        [Header("Debug")]
        public bool showDebugLogs = true;

        // Smooth trigger values (for feel, even though triggers are digital)
        private float smoothedAcceleration = 0f;

        void Awake()
        {
            bicycleController = GetComponent<BicycleController>();
            inputActions = new InputSystem_Actions();

            // Subscribe to input events
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

            Debug.Log("<color=green>BicycleInputBridge: Initialized with trigger support</color>");
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
            // Process steering from left stick X only
            float steerInput = moveInput.x;
            if (Mathf.Abs(steerInput) < stickDeadzone)
            {
                steerInput = 0f;
            }

            // Boost steering
            float boostedSteerInput = steerInput * steerSensitivityMultiplier;
            boostedSteerInput = Mathf.Clamp(boostedSteerInput, -1f, 1f);

            // Process triggers for acceleration/braking
            // ZR (accelerate) = +1, ZL (brake) = -1
            float targetAcceleration = 0f;
            if (accelerateInput) targetAcceleration = 1f;
            if (brakeInput) targetAcceleration = -1f;
            // If both pressed, prioritize braking (safer)
            if (accelerateInput && brakeInput) targetAcceleration = -1f;

            // Smooth the acceleration for better feel (optional, but recommended)
            smoothedAcceleration = Mathf.Lerp(smoothedAcceleration, targetAcceleration, Time.deltaTime * triggerSensitivity);

            // Apply to bicycle controller
            CustomInput(boostedSteerInput, ref bicycleController.customSteerAxis, 5, 5, false);
            CustomInput(smoothedAcceleration, ref bicycleController.customAccelerationAxis, 1, 1, false);
            CustomInput(boostedSteerInput, ref bicycleController.customLeanAxis, 1, 1, false);
            CustomInput(smoothedAcceleration, ref bicycleController.rawCustomAccelerationAxis, 1, 1, true);

            // Debug logging
            if (showDebugLogs && (Mathf.Abs(steerInput) > 0.1f || Mathf.Abs(targetAcceleration) > 0.1f))
            {
                Debug.Log($"<color=cyan>INPUT - Steer: {steerInput:F2} → Boosted: {boostedSteerInput:F2} | ZR: {accelerateInput} | ZL: {brakeInput}\n" +
                         $"CONTROLLER - CustomSteer: {bicycleController.customSteerAxis:F2} | Accel: {bicycleController.customAccelerationAxis:F2} (Target: {targetAcceleration:F2})</color>");
            }

            // Bunny hop state machine
            if (jumpInput && !wasJumpPressed)
                bicycleController.bunnyHopInputState = 1;
            else if (!jumpInput && wasJumpPressed)
                bicycleController.bunnyHopInputState = -1;
            else if (jumpInput)
                bicycleController.bunnyHopInputState = 1;
            else
                bicycleController.bunnyHopInputState = 0;

            wasJumpPressed = jumpInput;
            bicycleController.wheelieInput = wheelieInput;

            if (showDebugLogs && (jumpInput || wheelieInput || sprintInput))
            {
                Debug.Log($"<color=yellow>BUTTONS - Sprint: {sprintInput} | Jump: {jumpInput} (State: {bicycleController.bunnyHopInputState}) | Wheelie: {wheelieInput}</color>");
            }
        }

        float CustomInput(float inputValue, ref float axis, float sensitivity, float gravity, bool isRaw)
        {
            float r = inputValue;
            float s = sensitivity;
            float g = gravity;
            float t = Time.unscaledDeltaTime;

            if (isRaw)
                axis = r;
            else
            {
                if (r != 0)
                    axis = Mathf.Clamp(axis + r * s * t, -1f, 1f);
                else
                    axis = Mathf.Clamp01(Mathf.Abs(axis) - g * t) * Mathf.Sign(axis);
            }

            return axis;
        }

        // Input callbacks
        void OnMove(InputAction.CallbackContext context)
        {
            moveInput = context.ReadValue<Vector2>();
        }

        void OnAccelerate(InputAction.CallbackContext context)
        {
            if (context.performed)
                accelerateInput = true;
            else if (context.canceled)
                accelerateInput = false;

            if (showDebugLogs)
                Debug.Log($"<color=lime>ZR (Accelerate): {accelerateInput}</color>");
        }

        void OnBrake(InputAction.CallbackContext context)
        {
            if (context.performed)
                brakeInput = true;
            else if (context.canceled)
                brakeInput = false;

            if (showDebugLogs)
                Debug.Log($"<color=red>ZL (Brake): {brakeInput}</color>");
        }

        void OnSprint(InputAction.CallbackContext context)
        {
            if (context.performed)
                sprintInput = true;
            else if (context.canceled)
                sprintInput = false;
        }

        void OnJump(InputAction.CallbackContext context)
        {
            if (context.performed)
                jumpInput = true;
            else if (context.canceled)
                jumpInput = false;
        }

        void OnWheelie(InputAction.CallbackContext context)
        {
            if (context.performed)
                wheelieInput = true;
            else if (context.canceled)
                wheelieInput = false;
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
            }
        }
    }
}